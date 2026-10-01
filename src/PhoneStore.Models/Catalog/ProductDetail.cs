namespace PhoneStore.Models.Catalog;

public sealed class ProductDetail
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public int BrandId { get; init; }
    public string BrandName { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int StockQuantity { get; init; }
    public string? RAM { get; init; }
    public string? Storage { get; init; }
    public string? Chip { get; init; }
    public string? Screen { get; init; }
    public string? Camera { get; init; }
    public string? Battery { get; init; }
    public string? OperatingSystem { get; init; }
    public string? Description { get; init; }
    public List<string> Images { get; init; } = [];
}
