using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Profile;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Business.Authentication;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
[Authorize(Policy = ApiAccess.Customer)]
[EnableRateLimiting(ApiPolicies.ProtectedRateLimit)]
[RequestTimeout(ApiPolicies.CatalogTimeout)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class ProfileController(ICustomerProfileService profiles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CustomerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerProfileResponse>> Get(CancellationToken ct)
    {
        if (ApiAccess.UserId(User) is not int customerId) return Forbid();
        var profile = await profiles.GetAsync(customerId, ct);
        if (profile is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found",
                detail: "Customer profile was not found.");

        return Ok(new CustomerProfileResponse(profile.FullName, profile.Email, profile.Phone, profile.Address));
    }

    [HttpPut]
    [ProducesResponseType<CustomerProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CustomerProfileResponse>> Update(
        CustomerProfileUpdateRequest request, CancellationToken ct)
    {
        if (ApiAccess.UserId(User) is not int customerId) return Forbid();
        var result = await profiles.UpdateAsync(
            customerId, new CustomerProfileEdit(request.FullName, request.Phone, request.Address), ct);

        if (result == ProfileUpdateResult.InvalidInput)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request",
                detail: "Profile information is invalid.");
        if (result == ProfileUpdateResult.NotFound)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found",
                detail: "Customer profile was not found.");
        if (result == ProfileUpdateResult.Unavailable)
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service temporarily unavailable",
                detail: "The profile service is temporarily unavailable. Please try again later.");

        var updated = await profiles.GetAsync(customerId, ct);
        if (updated is null)
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Profile not found",
                detail: "Customer profile was not found.");

        return Ok(new CustomerProfileResponse(updated.FullName, updated.Email, updated.Phone, updated.Address));
    }
}
