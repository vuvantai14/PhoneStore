using PhoneStore.Models.Catalog;

namespace PhoneStore.Business.Catalog;

public interface IProductService
{
    Task<ProductDetail?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCard>> GetRelatedAsync(ProductDetail product, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCard>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCard>> GetNewestAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default);
}

