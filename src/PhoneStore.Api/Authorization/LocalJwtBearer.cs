using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PhoneStore.Api.Infrastructure;

namespace PhoneStore.Api.Authorization;

internal static class LocalJwtBearer
{
    public const string Scheme = "LocalJwt";
    public const string AccessTokenMediaType = "at+jwt";
    public const string AccessTokenUse = "access";

    internal static void Configure(JwtBearerOptions bearer, ApiAuthenticationOptions auth, TimeProvider timeProvider)
    {
        if (auth.Mode != ApiAuthenticationMode.LocalJwt) return;
        var jwt = auth.LocalJwt;
        var key = new SymmetricSecurityKey(jwt.GetSigningKeyBytes()) { KeyId = jwt.KeyId };

        bearer.MapInboundClaims = false;
        bearer.SaveToken = false;
        bearer.IncludeErrorDetails = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            IssuerValidator = (issuer, _, _) => string.Equals(issuer, jwt.Issuer, StringComparison.Ordinal)
                ? issuer
                : throw new SecurityTokenInvalidIssuerException("Unexpected issuer."),
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            IgnoreTrailingSlashWhenValidatingAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            LifetimeValidator = (notBefore, expires, _, parameters) =>
            {
                if (!expires.HasValue) return false;
                var now = timeProvider.GetUtcNow().UtcDateTime;
                var skew = parameters.ClockSkew;
                if (notBefore.HasValue && now + skew < notBefore.Value) return false;
                return now - skew < expires.Value;
            },
            ValidTypes = [AccessTokenMediaType],
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            TryAllIssuerSigningKeys = false
        };

        bearer.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var claims = context.Principal;
                if (claims is null)
                {
                    context.Fail("Access token principal is missing.");
                    return Task.CompletedTask;
                }

                var subjects = claims.FindAll("sub").ToArray();
                var jtis = claims.FindAll("jti").ToArray();
                var tokenUses = claims.FindAll("token_use").ToArray();
                if (subjects.Length != 1 ||
                    !int.TryParse(subjects[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) ||
                    userId <= 0 ||
                    jtis.Length != 1 || string.IsNullOrWhiteSpace(jtis[0].Value) ||
                    tokenUses.Length != 1 || !string.Equals(tokenUses[0].Value, AccessTokenUse, StringComparison.Ordinal))
                {
                    context.Fail("Verified local user identity required.");
                    return Task.CompletedTask;
                }

                var identity = new ClaimsIdentity(context.Scheme.Name);
                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)));
                identity.AddClaim(new Claim("jti", jtis[0].Value));
                foreach (var scope in claims.FindAll("scope")
                    .SelectMany(s => s.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
                {
                    identity.AddClaim(new Claim("scope", scope));
                }
                context.Principal = new ClaimsPrincipal(identity);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return RequestSafetyMiddleware.WriteProblemAsync(context.HttpContext, 401, "Unauthorized", "Authentication is required.");
            },
            OnForbidden = context =>
                RequestSafetyMiddleware.WriteProblemAsync(context.HttpContext, 403, "Forbidden", "Access is denied.")
        };
    }
}
