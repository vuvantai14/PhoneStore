using System.ComponentModel.DataAnnotations;
using PhoneStore.Models.Catalog;

namespace PhoneStore.Api.Contracts.Products;

public sealed class ProductQueryRequest : IValidatableObject
{
    [StringLength(100, ErrorMessage = "search must not exceed 100 characters.")]
    public string? Search { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "brandId must be greater than 0.")]
    public int? BrandId { get; init; }

    public int[]? BrandIds { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "categoryId must be greater than 0.")]
    public int? CategoryId { get; init; }

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "minPrice must be a non-negative valid price.")]
    public decimal? MinPrice { get; init; }

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "maxPrice must be a non-negative valid price.")]
    public decimal? MaxPrice { get; init; }

    public string[]? Rams { get; init; }

    public string[]? Storages { get; init; }

    [RegularExpression("^(newest|price-asc|price-desc|name-asc)$",
        ErrorMessage = "sort must be one of: newest, price-asc, price-desc, name-asc.")]
    public string Sort { get; init; } = "newest";

    [Range(1, int.MaxValue, ErrorMessage = "page must be greater than 0.")]
    public int Page { get; init; } = 1;

    [Range(1, 50, ErrorMessage = "pageSize must be between 1 and 50.")]
    public int PageSize { get; init; } = 10;

    public bool InStockOnly { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice.HasValue && MaxPrice.HasValue && MinPrice.Value > MaxPrice.Value)
        {
            yield return new ValidationResult(
                "minPrice must be less than or equal to maxPrice.",
                [nameof(MinPrice), nameof(MaxPrice)]);
        }

        if (BrandIds is { Length: > 0 })
        {
            if (BrandIds.Length > 50 || BrandIds.Any(id => id <= 0))
            {
                yield return new ValidationResult(
                    "brandIds must contain between 1 and 50 positive integers.",
                    [nameof(BrandIds)]);
            }
        }

        if (Rams is { Length: > 0 })
        {
            if (Rams.Length > 20 || Rams.Any(r => string.IsNullOrWhiteSpace(r) || r.Trim().Length > 50))
            {
                yield return new ValidationResult(
                    "rams must contain between 1 and 20 non-empty values of at most 50 characters.",
                    [nameof(Rams)]);
            }
        }

        if (Storages is { Length: > 0 })
        {
            if (Storages.Length > 20 || Storages.Any(s => string.IsNullOrWhiteSpace(s) || s.Trim().Length > 50))
            {
                yield return new ValidationResult(
                    "storages must contain between 1 and 20 non-empty values of at most 50 characters.",
                    [nameof(Storages)]);
            }
        }
    }

    public CatalogQuery ToCatalogQuery() => new(
        Search: Search?.Trim(),
        BrandId: BrandId,
        CategoryId: CategoryId,
        MinPrice: MinPrice,
        MaxPrice: MaxPrice,
        Sort: Sort,
        Page: Page,
        PageSize: PageSize,
        BrandIds: BrandIds is { Length: > 0 } ? BrandIds.Distinct().ToArray() : null,
        Rams: Rams is { Length: > 0 } ? Rams.Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() : null,
        Storages: Storages is { Length: > 0 } ? Storages.Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() : null,
        InStockOnly: InStockOnly);
}
