using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace PhoneStore.Api.Authorization;

internal sealed class ApiOidcOptions
{
    public bool Enabled { get; set; }
    public string? CustomerClientId { get; set; }
    public string? Issuer { get; set; }
    public string? Audience { get; set; }

    internal bool IsValid()
    {
        if (Issuer is null && Audience is null) return !Enabled;
        return !string.IsNullOrWhiteSpace(Audience) && Audience == Audience.Trim() &&
            !string.IsNullOrWhiteSpace(Issuer) && Issuer == Issuer.Trim() &&
            Uri.TryCreate(Issuer, UriKind.Absolute, out var uri) && uri.IsWellFormedOriginalString() &&
            uri.Scheme == Uri.UriSchemeHttps && uri.Host.Length > 0 &&
            uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 &&
            (!Enabled || (uri.AbsolutePath == "/" && Issuer.EndsWith('/') &&
                !string.IsNullOrWhiteSpace(CustomerClientId) && CustomerClientId == CustomerClientId.Trim()));
    }
}

internal static class ApiAuthenticationRegistration
{
    internal static void AddPhoneStoreApiAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ApiOidcOptions>()
            .Bind(configuration.GetSection("ApiAuthentication:Oidc"), binder => binder.ErrorOnUnknownConfiguration = true)
            .Validate(options => options.IsValid(), "Configure issuer and audience together. Enabled Auth0 requires a tenant HTTPS root ending in / and a Customer client ID.")
            .ValidateOnStart();
        services.AddAuthentication(ApiAccess.Scheme)
            .AddScheme<AuthenticationSchemeOptions, UnconfiguredApiAuthentication>(ApiAccess.Scheme, _ => { })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });
        services.AddOptions<AuthenticationSchemeOptions>(ApiAccess.Scheme)
            .Configure<IOptions<ApiOidcOptions>>((scheme, oidc) =>
                scheme.ForwardDefault = oidc.Value.Enabled ? JwtBearerDefaults.AuthenticationScheme : null);
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<ApiOidcOptions>>((bearer, oidc) => Auth0Bearer.Configure(bearer, oidc.Value));
        services.AddAuthorization(ApiAccess.Configure);
        services.AddScoped<IAuthorizationHandler, ActiveRoleHandler>();
    }
}
