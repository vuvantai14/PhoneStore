using System.Text;

namespace PhoneStore.Api.Authorization;

public enum ApiAuthenticationMode
{
    Disabled = 0,
    LocalJwt = 1
}

public sealed class ApiAuthenticationOptions
{
    public ApiAuthenticationMode Mode { get; set; } = ApiAuthenticationMode.Disabled;
    public LocalJwtOptions LocalJwt { get; set; } = new();
    public ApiOidcOptions Oidc { get; set; } = new();

    internal bool IsValid()
    {
        if (!Enum.IsDefined(Mode)) return false;
        if (Mode == ApiAuthenticationMode.LocalJwt && Oidc.Enabled) return false;
        if (Mode == ApiAuthenticationMode.Disabled) return true;
        return LocalJwt.IsValid();
    }
}

public sealed class LocalJwtOptions
{
    private static readonly string[] DisallowedWeakMarkers =
    [
        "changeme", "change-me", "replace-me", "your-secret", "default-key",
        "default-signing", "placeholder", "dev-secret", "insecure-key", "00000000", "aaaaaaaa"
    ];

    public string? Issuer { get; set; }
    public string? Audience { get; set; }
    public string? SigningKey { get; set; }
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
    public string KeyId { get; set; } = "local-hs256-v1";

    internal bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Issuer) || Issuer != Issuer.Trim()) return false;
        if (!Uri.TryCreate(Issuer, UriKind.Absolute, out var uri) || !uri.IsWellFormedOriginalString()) return false;
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Host.Length == 0 ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            return false;

        if (string.IsNullOrWhiteSpace(Audience) || Audience != Audience.Trim() || Audience.Length is < 3 or > 200)
            return false;

        if (string.IsNullOrWhiteSpace(SigningKey) || SigningKey != SigningKey.Trim())
            return false;
        if (Encoding.UTF8.GetByteCount(SigningKey) < 32)
            return false;
        if (SigningKey.Distinct().Count() < 12)
            return false;
        foreach (var marker in DisallowedWeakMarkers)
        {
            if (SigningKey.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (AccessTokenMinutes is < 5 or > 60) return false;
        if (RefreshTokenDays is < 1 or > 30) return false;
        if (string.IsNullOrWhiteSpace(KeyId) || KeyId != KeyId.Trim() || KeyId.Length > 64) return false;

        return true;
    }

    internal byte[] GetSigningKeyBytes() => Encoding.UTF8.GetBytes(SigningKey!);
}
