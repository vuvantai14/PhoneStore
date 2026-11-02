namespace PhoneStore.Models.Management;

public enum LookupKind { Brand, Category }

public sealed record LookupRow(int Id, string Name, string? Description, bool IsActive, DateTime CreatedAt)
{
    public string Status => IsActive ? "Đang hoạt động" : "Ngừng hoạt động";
}

public sealed record LookupEdit(string Name, string? Description, bool IsActive);

public sealed record LookupOption(int Id, string Name, bool IsActive)
{
    public string DisplayName => IsActive ? Name : Name + " (ngừng hoạt động)";
}

public sealed record ProductManagementQuery(string? Search = null, int? BrandId = null,
    int? CategoryId = null, bool? IsActive = null);

public sealed record ProductManagementRow(int ProductId, string ProductName, string BrandName,
    string CategoryName, decimal Price, int StockQuantity, string? RAM, string? Storage,
    bool IsActive, DateTime? UpdatedAt)
{
    public string Status => IsActive ? "Đang hoạt động" : "Ngừng hoạt động";
}

public sealed class ProductEdit
{
    public string ProductName { get; set; } = "";
    public int BrandId { get; set; }
    public int CategoryId { get; set; }
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    // Snapshot from opening an existing product; never replace it with the submitted stock.
    public int? OriginalStockQuantity { get; set; }
    public string? RAM { get; set; }
    public string? Storage { get; set; }
    public string? Chip { get; set; }
    public string? Screen { get; set; }
    public string? Camera { get; set; }
    public string? Battery { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record ProductEditOptions(IReadOnlyList<LookupOption> Brands, IReadOnlyList<LookupOption> Categories);

public sealed record ProductImageRow(int ImageId, int ProductId, string ImageUrl, bool IsPrimary, int DisplayOrder)
{
    public string PrimaryLabel => IsPrimary ? "Ảnh chính" : "";
}

public sealed record ManagementResult(bool Succeeded, string? Error = null, int? Id = null, string? Notice = null)
{
    public static ManagementResult Fail(string error) => new(false, error);
    public static ManagementResult Success(int? id = null, string? notice = null) => new(true, Id: id, Notice: notice);
}
