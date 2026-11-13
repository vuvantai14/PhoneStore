using PhoneStore.Api.Contracts;
using PhoneStore.Api.Contracts.Products;
using PhoneStore.Models.Catalog;

namespace PhoneStore.Api.Mapping;

public static class CatalogContractMapper
{
    public static ProductSummaryResponse ToResponse(ProductCard product) => new(
        product.ProductId,
        product.ProductName,
        product.BrandName,
        product.RAM,
        product.Storage,
        product.Price,
        product.StockQuantity,
        SafeRelativeImageUrl(product.ImageUrl));

    public static ProductDetailResponse ToResponse(ProductDetail product) => new(
        product.ProductId,
        product.ProductName,
        product.BrandId,
        product.BrandName,
        product.CategoryId,
        product.CategoryName,
        product.Price,
        product.StockQuantity,
        product.RAM,
        product.Storage,
        product.Chip,
        product.Screen,
        product.Camera,
        product.Battery,
        product.OperatingSystem,
        product.Description,
        product.Images.Select(SafeRelativeImageUrl).OfType<string>().ToArray());

    public static CatalogOptionResponse ToResponse(CatalogOption option) => new(option.Id, option.Name);

    public static string? SafeRelativeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        var value = imageUrl.Trim();
        if (value.Contains('\\') || value.StartsWith("//", StringComparison.Ordinal)) return null;

        var pathToCheck = value[0] == '/' ? value[1..] : value;
        if (pathToCheck.Length == 0 || Uri.TryCreate(pathToCheck, UriKind.Absolute, out _)) return null;
        if (pathToCheck.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..")) return null;
        return value;
    }
}
