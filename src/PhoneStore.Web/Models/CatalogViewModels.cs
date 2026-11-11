using PhoneStore.Models.Catalog;

namespace PhoneStore.Web.Models;

public sealed class HomeViewModel
{
    public IReadOnlyList<ProductCard> Products { get; init; } = [];
    public IReadOnlyList<ProductCard> BestSellers { get; init; } = [];
    public IReadOnlyList<CatalogOption> Brands { get; init; } = [];
    public IReadOnlyList<CatalogOption> Categories { get; init; } = [];
}

public sealed class ProductFilterViewModel
{
    public string? Search { get; set; }
    public int? BrandId { get; set; }
    public List<int> BrandIds { get; set; } = [];
    public int? CategoryId { get; set; }
    public string? MinPrice { get; set; }
    public string? MaxPrice { get; set; }
    public string? PriceRange { get; set; }
    public List<string> Rams { get; set; } = [];
    public List<string> Storages { get; set; } = [];
    public bool? InStockOnly { get; set; }
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public bool Reset { get; set; }
    public bool HasFilters => !string.IsNullOrWhiteSpace(Search)
        || BrandIds.Count > 0 || BrandId.HasValue || CategoryId.HasValue
        || !string.IsNullOrWhiteSpace(MinPrice) || !string.IsNullOrWhiteSpace(MaxPrice)
        || (!string.IsNullOrWhiteSpace(PriceRange) && PriceRange != "all")
        || Rams.Count > 0 || Storages.Count > 0 || InStockOnly == true;
}

public sealed record AppliedFilterItem(string Key, string Value, string Label, string RemoveUrl);

public sealed class ProductListViewModel
{
    public ProductFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<ProductCard> Products { get; init; } = [];
    public IReadOnlyList<CatalogOption> Brands { get; init; } = [];
    public IReadOnlyList<CatalogOption> Categories { get; init; } = [];
    public IReadOnlyList<AppliedFilterItem> AppliedFilters { get; init; } = [];
    public int TotalItems { get; init; }
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalItems / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
