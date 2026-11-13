namespace PhoneStore.Api.Infrastructure;

public sealed class ApiOperationalOptions
{
    public const string SectionName = "ApiOperational";

    public int MaxQueryStringLength { get; init; } = 2048;
    public int MaxRequestLineSize { get; init; } = 4096;
    public int CatalogTimeoutSeconds { get; init; } = 10;
    public int HealthTimeoutSeconds { get; init; } = 3;
    public int CatalogRequestsPerMinute { get; init; } = 60;
    public int HealthRequestsPerMinute { get; init; } = 120;
}
