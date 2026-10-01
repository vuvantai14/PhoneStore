namespace PhoneStore.Models.Catalog;

public sealed record CatalogQuery(
    string? Search = null, int? BrandId = null, int? CategoryId = null,
    decimal? MinPrice = null, decimal? MaxPrice = null, string Sort = "newest");

