using PhoneStore.Data.Catalog;
using PhoneStore.Models.Catalog;

namespace PhoneStore.Business.Catalog;

public sealed class ProductService(ProductCatalogReader reader) : IProductService
{
    public Task<IReadOnlyList<ProductCard>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) =>
        reader.GetProductsAsync(query, cancellationToken);

    public Task<IReadOnlyList<ProductCard>> GetNewestAsync(int count, CancellationToken cancellationToken = default) =>
        reader.GetNewestAsync(count, cancellationToken);

    public Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
        reader.GetBrandsAsync(cancellationToken);

    public Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        reader.GetCategoriesAsync(cancellationToken);
}
