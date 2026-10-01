namespace PhoneStore.Models.Catalog;

public sealed record ProductCard(
    int ProductId, string ProductName, string BrandName,
    string? RAM, string? Storage, decimal Price, int StockQuantity, string? ImageUrl);

public sealed record CatalogOption(int Id, string Name);

