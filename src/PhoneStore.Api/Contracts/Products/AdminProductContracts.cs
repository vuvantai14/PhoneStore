using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
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

public sealed class AdminProductOptionsQuery
{
    [Range(1, int.MaxValue)] public int? ProductId { get; init; }
    public bool IncludeInactive { get; init; }
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

public sealed record AdminLookupOption(int Id, string Name, bool IsActive);
public sealed record AdminProductOptionsResponse(
    IReadOnlyList<AdminLookupOption> Brands,
    IReadOnlyList<AdminLookupOption> Categories);

public sealed class AdminLookupQuery
{
    [StringLength(100)] public string? Search { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminLookupWriteRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;

    internal LookupEdit ToEdit() => new(Name, Description, IsActive);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminLookupStatusRequest
{
    public required bool IsActive { get; init; }
}

public sealed record AdminLookupId(int Id);
public sealed record AdminLookupItem(int Id, string Name, string? Description, bool IsActive, DateTime CreatedAt);

public sealed record AdminProductImageItem(
    int ImageId,
    int ProductId,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder);

public sealed record AdminProductImageUploadResponse(
    int ImageId,
    int ProductId,
    string ImageUrl,
    bool IsPrimary,
    int DisplayOrder,
    string? Notice = null);

public sealed record AdminProductImageOperationResponse(
    int ImageId,
    string? Notice = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AdminProductImageOrderRequest
{
    [Range(0, int.MaxValue)]
    public required int DisplayOrder { get; init; }
}
