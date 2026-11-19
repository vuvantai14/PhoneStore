using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PhoneStore.Api.Infrastructure;

namespace PhoneStore.Api.Authorization;

// A future verified store must return at most two candidates so ambiguity fails closed.
public interface IVerifiedIdentityLookup
{
    Task<IReadOnlyList<int>> FindAsync(string issuer, string subject, CancellationToken cancellationToken);
}

internal static class Auth0Bearer
{
    internal static void Configure(JwtBearerOptions bearer, ApiOidcOptions oidc)
    {
        if (!oidc.Enabled) return;
        bearer.Authority = oidc.Issuer;
        bearer.MapInboundClaims = false;
        bearer.SaveToken = false;
        bearer.IncludeErrorDetails = false;
        bearer.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            IssuerValidator = (issuer, _, _) => issuer == oidc.Issuer ? issuer :
                throw new SecurityTokenInvalidIssuerException("Unexpected issuer."),
            ValidAudience = oidc.Audience,
            ValidateAudience = true,
            IgnoreTrailingSlashWhenValidatingAudience = false,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidTypes = ["at+jwt"],
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            TryAllIssuerSigningKeys = false
        };
        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = context => MapVerifiedIdentity(context, oidc.CustomerClientId!),
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return RequestSafetyMiddleware.WriteProblemAsync(context.HttpContext, 401, "Unauthorized", "Authentication is required.");
            },
            OnForbidden = context => RequestSafetyMiddleware.WriteProblemAsync(context.HttpContext, 403, "Forbidden", "Access is denied.")
        };
    }

    private static async Task MapVerifiedIdentity(TokenValidatedContext context, string allowedClientId)
    {
        var claims = context.Principal!;
        var subjects = claims.FindAll("sub").ToArray();
        var lookup = context.HttpContext.RequestServices.GetService<IVerifiedIdentityLookup>();
        if (!claims.FindAll("client_id").Select(c => c.Value).SequenceEqual([allowedClientId]) ||
            subjects.Length != 1 || string.IsNullOrWhiteSpace(subjects[0].Value) ||
            subjects[0].Value.EndsWith("@clients", StringComparison.Ordinal) || lookup is null)
        {
            context.Fail("Verified user identity required.");
            return;
        }
        var ids = await lookup.FindAsync(context.SecurityToken.Issuer, subjects[0].Value, context.HttpContext.RequestAborted);
        if (ids.Count != 1 || ids[0] <= 0)
        {
            context.Fail("Verified user identity required.");
            return;
        }
        var identity = new ClaimsIdentity(context.Scheme.Name);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, ids[0].ToString(CultureInfo.InvariantCulture)));
        foreach (var scope in claims.FindAll("scope").SelectMany(s => s.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            identity.AddClaim(new Claim("scope", scope));
        context.Principal = new ClaimsPrincipal(identity);
    }
}
