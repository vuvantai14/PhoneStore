using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PhoneStore.Api.Infrastructure;

namespace PhoneStore.Api.Authorization;

// No issuer is configured. This scheme never reads credentials or creates a principal.
internal sealed class UnconfiguredApiAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        RequestSafetyMiddleware.WriteProblemAsync(Context, 401, "Unauthorized", "Authentication is required.");

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        RequestSafetyMiddleware.WriteProblemAsync(Context, 403, "Forbidden", "Access is denied.");
}
