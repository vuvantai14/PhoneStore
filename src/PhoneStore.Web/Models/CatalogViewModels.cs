using PhoneStore.Models.Catalog;

namespace PhoneStore.Web.Models;

public sealed class HomeViewModel
{
    public IReadOnlyList<ProductCard> Products { get; init; } = [];
    public IReadOnlyList<CatalogOption> Brands { get; init; } = [];
    public IReadOnlyList<CatalogOption> Categories { get; init; } = [];
}

public sealed class ProductFilterViewModel
{
    public string? Search { get; set; }
    public int? BrandId { get; set; }
    public int? CategoryId { get; set; }
    public string? MinPrice { get; set; }
    public string? MaxPrice { get; set; }
    public string Sort { get; set; } = "newest";
    public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || BrandId.HasValue
        || CategoryId.HasValue || !string.IsNullOrWhiteSpace(MinPrice) || !string.IsNullOrWhiteSpace(MaxPrice);
}

public sealed class ProductListViewModel
{
    public ProductFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<ProductCard> Products { get; init; } = [];
    public IReadOnlyList<CatalogOption> Brands { get; init; } = [];
    public IReadOnlyList<CatalogOption> Categories { get; init; } = [];
}

