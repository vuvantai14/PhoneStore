namespace PhoneStore.Web.CatalogApi;

public sealed class CatalogApiOptions
{
    public const string SectionName = "CatalogApi";

    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = "http://localhost:5025/";
    public int TimeoutSeconds { get; init; } = 3;
}
