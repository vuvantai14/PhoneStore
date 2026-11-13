namespace PhoneStore.Api.Contracts.Products;

public sealed record ProductSummaryResponse(
    int Id,
    string Name,
    string BrandName,
    string? Ram,
    string? Storage,
    decimal Price,
    int StockQuantity,
    string? PrimaryImageUrl);
