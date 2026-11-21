namespace PhoneStore.Api.Infrastructure;

public sealed class ApiOperationalOptions
{
    public const string SectionName = "ApiOperational";

    public int MaxQueryStringLength { get; init; } = 2048;
    public int MaxRequestLineSize { get; init; } = 4096;
    public int MaxRequestBodyBytes { get; init; } = 65536;
    public int MaxImageUploadBytes { get; init; } = 10 * 1024 * 1024 + 65536;
    public int CatalogTimeoutSeconds { get; init; } = 10;
    public int HealthTimeoutSeconds { get; init; } = 3;
    public int AuthTimeoutSeconds { get; init; } = 5;
    public int CatalogRequestsPerMinute { get; init; } = 60;
    public int HealthRequestsPerMinute { get; init; } = 120;
    public int AuthRequestsPerMinute { get; init; } = 10;
    public int ProtectedRequestsPerMinute { get; init; } = 120;
    public bool RequireHttps { get; init; }
    public string[] TrustedProxies { get; init; } = [];
    public string[] TrustedProxyNetworks { get; init; } = [];
}
