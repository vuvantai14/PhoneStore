using System.Net;
using System.Net.Http.Json;
using PhoneStore.Models.Catalog;

namespace PhoneStore.Web.CatalogApi;

public sealed class CatalogApiClient(HttpClient httpClient, ILogger<CatalogApiClient> logger)
    : ICatalogApiClient
{
    public async Task<CatalogApiResult> GetProductAsync(
        int productId, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                $"api/v1/products/{productId}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(CatalogApiStatus.NotFound);
            if (response.StatusCode == HttpStatusCode.BadRequest)
                return new(CatalogApiStatus.BadRequest);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return new(CatalogApiStatus.RateLimited);
            if ((int)response.StatusCode >= 500)
                return new(CatalogApiStatus.Unavailable);
            if (response.StatusCode != HttpStatusCode.OK)
                return new(CatalogApiStatus.InvalidPayload);

            var dto = await response.Content.ReadFromJsonAsync<CatalogApiProductResponse>(
                cancellationToken: cancellationToken);
            var product = Map(dto, productId);
            return product is null
                ? new(CatalogApiStatus.InvalidPayload)
                : new(CatalogApiStatus.Success, product);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Catalog API timed out for product {ProductId}.", productId);
            return new(CatalogApiStatus.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Catalog API request failed for product {ProductId}: {FailureType}.",
                productId, exception.GetType().Name);
            return new(CatalogApiStatus.Unavailable);
        }
        catch (System.Text.Json.JsonException exception)
        {
            logger.LogWarning("Catalog API returned an invalid payload for product {ProductId}: {FailureType}.",
                productId, exception.GetType().Name);
            return new(CatalogApiStatus.InvalidPayload);
        }
    }

    private static ProductDetail? Map(CatalogApiProductResponse? dto, int expectedId)
    {
        if (dto is null || dto.Id != expectedId || dto.Id <= 0 ||
            string.IsNullOrWhiteSpace(dto.Name) || dto.BrandId <= 0 ||
            string.IsNullOrWhiteSpace(dto.BrandName) || dto.CategoryId <= 0 ||
            string.IsNullOrWhiteSpace(dto.CategoryName) || dto.Price < 0 || dto.StockQuantity < 0)
            return null;

        var images = dto.Images ?? [];
        if (images.Any(image => !IsSafeRelativeImageUrl(image))) return null;

        return new ProductDetail
        {
            ProductId = dto.Id,
            ProductName = dto.Name,
            BrandId = dto.BrandId,
            BrandName = dto.BrandName,
            CategoryId = dto.CategoryId,
            CategoryName = dto.CategoryName,
            Price = dto.Price,
            StockQuantity = dto.StockQuantity,
            RAM = dto.Ram,
            Storage = dto.Storage,
            Chip = dto.Chip,
            Screen = dto.Screen,
            Camera = dto.Camera,
            Battery = dto.Battery,
            OperatingSystem = dto.OperatingSystem,
            Description = dto.Description,
            Images = images.ToList()
        };
    }

    private static bool IsSafeRelativeImageUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\\') || value.StartsWith("//", StringComparison.Ordinal))
            return false;
        var path = value[0] == '/' ? value[1..] : value;
        return path.Length > 0 && !Uri.TryCreate(path, UriKind.Absolute, out _) &&
            !path.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..");
    }
}
