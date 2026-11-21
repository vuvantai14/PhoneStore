using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Auth;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Business.Authentication;
using PhoneStore.Data.Authentication;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting(ApiPolicies.AuthRateLimit)]
[RequestTimeout(ApiPolicies.AuthTimeout)]
public sealed class AuthController(
    IOptions<ApiAuthenticationOptions> authOptions,
    IAuthService customerAuth,
    IAdminAuthService adminAuth,
    AuthUserStore users,
    IApiRefreshTokenService sessions) : ControllerBase
{
    private bool IsLocalJwtEnabled => authOptions.Value.Mode == ApiAuthenticationMode.LocalJwt;

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AuthTokenResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!IsLocalJwtEnabled)
            return AuthDisabledProblem();

        CustomerIdentity? customer;
        try
        {
            customer = await customerAuth.LoginAsync(request.Email, request.Password, ct);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return ServiceUnavailableProblem();
        }

        if (customer is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
                detail: "Invalid email or password.");

        var response = await sessions.IssueSessionAsync(
            customer.UserId, customer.FullName, customer.Email, customer.Role, ct);
        return response is null ? ServiceUnavailableProblem() : Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("admin/login")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AuthTokenResponse>> AdminLogin(LoginRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!IsLocalJwtEnabled)
            return AuthDisabledProblem();

        AdminLoginResult result;
        try
        {
            result = await adminAuth.LoginAsync(request.Email, request.Password, ct);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return ServiceUnavailableProblem();
        }

        if (result.Status == AdminLoginStatus.Forbidden)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden",
                detail: "Administrator privileges are required.");

        if (result.Status != AdminLoginStatus.Success || result.Identity is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
                detail: "Invalid email or password.");

        var admin = result.Identity;
        var response = await sessions.IssueSessionAsync(
            admin.UserId, admin.FullName, admin.Email, admin.Role, ct);
        return response is null ? ServiceUnavailableProblem() : Ok(response);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RegisterResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!IsLocalJwtEnabled)
            return AuthDisabledProblem();

        var result = await customerAuth.RegisterAsync(
            request.FullName, request.Email, request.Phone, request.Password, ct);

        return result switch
        {
            RegistrationResult.Success => StatusCode(StatusCodes.Status201Created,
                new RegisterResponse("Registration succeeded.", request.Email.Trim().ToLowerInvariant(), SystemRoles.Customer)),
            RegistrationResult.DuplicateEmail => Problem(statusCode: StatusCodes.Status409Conflict,
                title: "Conflict", detail: "An account with this email already exists."),
            RegistrationResult.InvalidInput => Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request", detail: "Registration information is invalid."),
            _ => ServiceUnavailableProblem()
        };
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(RefreshTokenRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!IsLocalJwtEnabled)
            return AuthDisabledProblem();

        var rotated = await sessions.RotateAsync(request.RefreshToken, ct);
        return rotated.Status switch
        {
            RefreshRotationStatus.Success when rotated.Response is not null => Ok(rotated.Response),
            RefreshRotationStatus.Unavailable => ServiceUnavailableProblem(),
            _ => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
                detail: "Invalid or expired refresh token.")
        };
    }

    [Authorize(Policy = ApiAccess.Authenticated)]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (ApiAccess.UserId(User) is not int userId) return Forbid();

        var status = await sessions.RevokeAsync(userId, request?.RefreshToken, request?.AllSessions ?? false, ct);
        return status switch
        {
            LogoutRevocationStatus.Success => NoContent(),
            LogoutRevocationStatus.Forbidden => Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden", detail: "Access is denied."),
            _ => ServiceUnavailableProblem()
        };
    }

    [Authorize(Policy = ApiAccess.Authenticated)]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken ct)
    {
        if (ApiAccess.UserId(User) is not int userId) return Forbid();
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null || !user.IsActive) return Forbid();

        var scopes = User.FindAll("scope")
            .Select(c => c.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return Ok(new CurrentUserResponse(
            user.UserId,
            user.FullName,
            user.Email,
            user.Phone,
            user.Role.RoleName,
            scopes));
    }

    private ObjectResult AuthDisabledProblem() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Authentication unavailable",
        detail: "Local JWT authentication is not enabled.");

    private ObjectResult ServiceUnavailableProblem() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Service temporarily unavailable",
        detail: "The authentication service is temporarily unavailable. Please try again later.");
}
