using System.ComponentModel.DataAnnotations;
using PhoneStore.Models.Management;

namespace PhoneStore.Api.Contracts.Products;

public sealed class AdminProductQuery
{
    public string? Search { get; init; }
    [Range(1, int.MaxValue)] public int? BrandId { get; init; }
    [Range(1, int.MaxValue)] public int? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

public record ProductWriteRequest
{
    public required string ProductName { get; init; }
    public required int BrandId { get; init; }
    public required int CategoryId { get; init; }
    public required decimal Price { get; init; }
    public required int StockQuantity { get; init; }
    public required bool IsActive { get; init; }
    public string? RAM { get; init; }
    public string? Storage { get; init; }
    public string? Chip { get; init; }
    public string? Screen { get; init; }
    public string? Camera { get; init; }
    public string? Battery { get; init; }
    public string? OperatingSystem { get; init; }
    public string? Description { get; init; }

    internal ProductEdit ToEdit(int? originalStock = null) => new()
    {
        ProductName = ProductName, BrandId = BrandId, CategoryId = CategoryId,
        Price = Price, StockQuantity = StockQuantity, OriginalStockQuantity = originalStock,
        IsActive = IsActive, RAM = RAM, Storage = Storage, Chip = Chip, Screen = Screen,
        Camera = Camera, Battery = Battery, OperatingSystem = OperatingSystem, Description = Description
    };
}

public sealed record ProductUpdateRequest : ProductWriteRequest
{
    public required int OriginalStockQuantity { get; init; }
}

public sealed record ProductStatusRequest
{
    public required bool IsActive { get; init; }
}

public sealed record AdminProductId(int ProductId);
public sealed record AdminProductDetail(int ProductId, string ProductName, int BrandId, int CategoryId,
    decimal Price, int StockQuantity, int OriginalStockQuantity, bool IsActive, string? RAM, string? Storage,
    string? Chip, string? Screen, string? Camera, string? Battery, string? OperatingSystem, string? Description);
public sealed record AdminProductSummary(int ProductId, string ProductName, string BrandName, string CategoryName,
    decimal Price, int StockQuantity, string? RAM, string? Storage, bool IsActive, DateTime? UpdatedAt);
public sealed record AdminProductPage(IReadOnlyList<AdminProductSummary> Items, int TotalCount, int Page, int PageSize);
