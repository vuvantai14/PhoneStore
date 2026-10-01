using PhoneStore.Models.Catalog;

namespace PhoneStore.Business.Catalog;

public interface IProductService
{
    Task<IReadOnlyList<ProductCard>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductCard>> GetNewestAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default);
}

