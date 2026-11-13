using PhoneStore.Models.Catalog;

namespace PhoneStore.Web.CatalogApi;

public enum CatalogApiStatus
{
    Success,
    NotFound,
    BadRequest,
    RateLimited,
    Unavailable,
    InvalidPayload
}

public sealed record CatalogApiResult(CatalogApiStatus Status, ProductDetail? Product = null);

public interface ICatalogApiClient
{
    Task<CatalogApiResult> GetProductAsync(int productId, CancellationToken cancellationToken);
}
