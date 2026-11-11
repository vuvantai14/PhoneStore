namespace PhoneStore.Models.Catalog;

public sealed record CatalogQuery(
    string? Search = null,
    int? BrandId = null,
    int? CategoryId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string Sort = "newest",
    int Page = 1,
    int PageSize = 10,
    IReadOnlyList<int>? BrandIds = null,
    IReadOnlyList<string>? Rams = null,
    IReadOnlyList<string>? Storages = null,
    bool InStockOnly = false);

public sealed record PagedCatalogResult(
    IReadOnlyList<ProductCard> Items,
    int TotalItems,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
