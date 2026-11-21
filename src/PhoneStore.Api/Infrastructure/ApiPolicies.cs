namespace PhoneStore.Api.Infrastructure;

public static class ApiPolicies
{
    public const string CatalogRateLimit = "catalog";
    public const string HealthRateLimit = "health";
    public const string AuthRateLimit = "auth";
    public const string ProtectedRateLimit = "protected";
    public const string CatalogTimeout = "catalog-timeout";
    public const string HealthTimeout = "health-timeout";
    public const string AuthTimeout = "auth-timeout";
}
