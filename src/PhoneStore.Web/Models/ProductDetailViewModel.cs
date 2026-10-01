using PhoneStore.Models.Catalog;

namespace PhoneStore.Web.Models;

public sealed class ProductDetailViewModel
{
    public required ProductDetail Product { get; init; }
    public IReadOnlyList<ProductCard> RelatedProducts { get; init; } = [];

    public IReadOnlyList<KeyValuePair<string, string?>> Specifications =>
        new KeyValuePair<string, string?>[]
        {
            new("RAM", Product.RAM), new("Bộ nhớ", Product.Storage),
            new("Chip", Product.Chip), new("Màn hình", Product.Screen),
            new("Camera", Product.Camera), new("Pin", Product.Battery),
            new("Hệ điều hành", Product.OperatingSystem)
        }.Where(item => !string.IsNullOrWhiteSpace(item.Value)).ToList();
}
