using Microsoft.Extensions.Options;
using PhoneStore.Business.Catalog;
using PhoneStore.Models.Catalog;

namespace PhoneStore.Web.CatalogApi;

public enum ProductDetailResolutionStatus { Success, NotFound, InvalidApiResponse }
public sealed record ProductDetailResolution(ProductDetailResolutionStatus Status, ProductDetail? Product = null);

public interface IProductDetailResolver
{
    Task<ProductDetailResolution> ResolveAsync(int productId, CancellationToken cancellationToken);
}

public sealed class ProductDetailResolver(
    IOptions<CatalogApiOptions> options,
    ICatalogApiClient api,
    IProductService products,
    ILogger<ProductDetailResolver> logger) : IProductDetailResolver
{
    public async Task<ProductDetailResolution> ResolveAsync(
        int productId, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return FromLocal(await products.GetProductDetailAsync(productId, cancellationToken));

        var result = await api.GetProductAsync(productId, cancellationToken);
        switch (result.Status)
        {
            case CatalogApiStatus.Success:
                return new(ProductDetailResolutionStatus.Success, result.Product);
            case CatalogApiStatus.NotFound:
                return new(ProductDetailResolutionStatus.NotFound);
            case CatalogApiStatus.BadRequest:
                logger.LogWarning("Catalog API rejected valid Web product id {ProductId}.", productId);
                return new(ProductDetailResolutionStatus.InvalidApiResponse);
            case CatalogApiStatus.RateLimited:
            case CatalogApiStatus.Unavailable:
            case CatalogApiStatus.InvalidPayload:
                logger.LogWarning("Catalog API status {ApiStatus}; using local catalog for product {ProductId}.",
                    result.Status, productId);
                return FromLocal(await products.GetProductDetailAsync(productId, cancellationToken));
            default:
                return new(ProductDetailResolutionStatus.InvalidApiResponse);
        }
    }

    private static ProductDetailResolution FromLocal(ProductDetail? product) => product is null
        ? new(ProductDetailResolutionStatus.NotFound)
        : new(ProductDetailResolutionStatus.Success, product);
}
