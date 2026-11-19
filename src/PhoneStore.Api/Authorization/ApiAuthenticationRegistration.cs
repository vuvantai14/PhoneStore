using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace PhoneStore.Api.Authorization;

internal sealed class ApiOidcOptions
{
    public string? Issuer { get; set; }
    public string? Audience { get; set; }

    internal bool IsValid()
    {
        if (Issuer is null && Audience is null) return true;
        return !string.IsNullOrWhiteSpace(Audience) && Audience == Audience.Trim() &&
            !string.IsNullOrWhiteSpace(Issuer) && Issuer == Issuer.Trim() &&
            Uri.TryCreate(Issuer, UriKind.Absolute, out var uri) && uri.IsWellFormedOriginalString() &&
            uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 &&
            uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0;
    }
}

internal static class ApiAuthenticationRegistration
{
    internal static void AddPhoneStoreApiAccess(this IServiceCollection services, IConfiguration configuration)
    {
        // These settings describe a candidate provider. They never activate token authentication.
        services.AddOptions<ApiOidcOptions>()
            .Bind(configuration.GetSection("ApiAuthentication:Oidc"), binder => binder.ErrorOnUnknownConfiguration = true)
            .Validate(options => options.IsValid(), "Configure both an HTTPS OIDC issuer and its API audience, or omit both.")
            .ValidateOnStart();
        services.AddAuthentication(ApiAccess.Scheme)
            .AddScheme<AuthenticationSchemeOptions, UnconfiguredApiAuthentication>(ApiAccess.Scheme, _ => { });
        services.AddAuthorization(ApiAccess.Configure);
        services.AddScoped<IAuthorizationHandler, ActiveRoleHandler>();
    }
}
