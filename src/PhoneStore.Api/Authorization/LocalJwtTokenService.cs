using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Api.Authorization;

public interface ILocalJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc, int ExpiresInSeconds) IssueAccessToken(int userId, string role);
}

internal sealed class LocalJwtTokenService(
    IOptions<ApiAuthenticationOptions> options,
    TimeProvider timeProvider) : ILocalJwtTokenService
{
    public (string Token, DateTime ExpiresAtUtc, int ExpiresInSeconds) IssueAccessToken(int userId, string role)
    {
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        var auth = options.Value;
        if (auth.Mode != ApiAuthenticationMode.LocalJwt)
            throw new InvalidOperationException("Local JWT authentication mode is not enabled.");

        var jwt = auth.LocalJwt;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(jwt.AccessTokenMinutes);
        var scopes = role switch
        {
            SystemRoles.Customer => "orders:read orders:write profile:read profile:write",
            SystemRoles.Admin => "admin:all",
            _ => throw new InvalidOperationException("Unsupported role for token issuance.")
        };

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new("token_use", LocalJwtBearer.AccessTokenUse),
            new("scope", scopes)
        };

        var key = new SymmetricSecurityKey(jwt.GetSigningKeyBytes()) { KeyId = jwt.KeyId };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);
        token.Header["typ"] = LocalJwtBearer.AccessTokenMediaType;

        var serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return (serialized, expiresAt.UtcDateTime, jwt.AccessTokenMinutes * 60);
    }
}
