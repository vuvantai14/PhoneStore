namespace PhoneStore.Web.CatalogApi;

public sealed record CatalogApiProductResponse(
    int Id,
    string? Name,
    int BrandId,
    string? BrandName,
    int CategoryId,
    string? CategoryName,
    decimal Price,
    int StockQuantity,
    string? Ram,
    string? Storage,
    string? Chip,
    string? Screen,
    string? Camera,
    string? Battery,
    string? OperatingSystem,
    string? Description,
    IReadOnlyList<string>? Images);
