using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Management;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public class ProductEditorService(ProductManagementStore productStore)
{
    protected readonly ProductManagementStore store = productStore;
    protected const string DatabaseError = "Chưa thể lưu thay đổi. Vui lòng kiểm tra kết nối dữ liệu và thử lại.";

    public Task<IReadOnlyList<ProductManagementRow>> ListAsync(ProductManagementQuery query, CancellationToken cancellationToken = default) =>
        store.ListAsync(query, cancellationToken);

    public Task<ProductManagementPage> ListPageAsync(ProductManagementQuery query, int offset, int limit, CancellationToken ct) =>
        store.ListPageAsync(query, offset, limit, ct);

    public Task<ProductEdit?> GetEditAsync(int id, CancellationToken cancellationToken = default) =>
        store.GetEditAsync(id, cancellationToken);

    public async Task<ManagementResult> SaveAsync(int? id, ProductEdit edit, CancellationToken cancellationToken = default)
    {
        var error = Validate(edit);
        if (error is not null) return ManagementResult.Fail(error);
        if (id is <= 0) return ManagementResult.Fail("Sản phẩm không hợp lệ.");
        if (id.HasValue && (edit.OriginalStockQuantity is null or < 0))
            return ManagementResult.Fail("Thiếu tồn kho gốc. Vui lòng đóng và mở lại sản phẩm trước khi lưu.");
        try
        {
            await using var transaction = await store.BeginAsync(cancellationToken);
            var product = id.HasValue ? await store.FindForSaveAsync(id.Value, cancellationToken) : new Product();
            if (product is null) return ManagementResult.Fail("Sản phẩm không còn tồn tại. Vui lòng tải lại danh sách.", ManagementFailure.NotFound);
            var brandActive = await store.BrandActiveAsync(edit.BrandId, cancellationToken);
            var categoryActive = await store.CategoryActiveAsync(edit.CategoryId, cancellationToken);
            if (!brandActive.HasValue) return ManagementResult.Fail("Thương hiệu không tồn tại. Vui lòng chọn lại.");
            if (!categoryActive.HasValue) return ManagementResult.Fail("Danh mục không tồn tại. Vui lòng chọn lại.");
            if (!brandActive.Value && (!id.HasValue || product.BrandId != edit.BrandId))
                return ManagementResult.Fail("Vui lòng chọn thương hiệu đang hoạt động.");
            if (!categoryActive.Value && (!id.HasValue || product.CategoryId != edit.CategoryId))
                return ManagementResult.Fail("Vui lòng chọn danh mục đang hoạt động.");

            product.ProductName = edit.ProductName.Trim();
            product.BrandId = edit.BrandId;
            product.CategoryId = edit.CategoryId;
            product.Price = edit.Price;
            product.StockQuantity = edit.StockQuantity;
            product.RAM = TrimOptional(edit.RAM);
            product.Storage = TrimOptional(edit.Storage);
            product.Chip = TrimOptional(edit.Chip);
            product.Screen = TrimOptional(edit.Screen);
            product.Camera = TrimOptional(edit.Camera);
            product.Battery = TrimOptional(edit.Battery);
            product.OperatingSystem = TrimOptional(edit.OperatingSystem);
            product.Description = TrimOptional(edit.Description);
            product.IsActive = edit.IsActive;
            if (!id.HasValue)
            {
                product.CreatedAt = DateTime.UtcNow;
                store.Add(product);
                await store.SaveAsync(cancellationToken);
            }
            else
            {
                product.UpdatedAt = DateTime.UtcNow;
                if (await store.UpdateIfStockMatchesAsync(product, edit.OriginalStockQuantity!.Value, cancellationToken) != 1)
                    return ManagementResult.Fail("Tồn kho đã thay đổi hoặc sản phẩm không còn tồn tại. Không có thay đổi nào được lưu. Vui lòng đóng và mở lại sản phẩm để tải dữ liệu mới.", ManagementFailure.Conflict);
            }
            await transaction.CommitAsync(cancellationToken);
            return ManagementResult.Success(product.ProductId);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return ManagementResult.Fail(DatabaseError, ManagementFailure.Database);
        }
        finally { store.ClearTracking(); }
    }

    public async Task<ManagementResult> SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return ManagementResult.Fail("Sản phẩm không hợp lệ.");
        try
        {
            var product = await store.FindAsync(id, cancellationToken);
            if (product is null) return ManagementResult.Fail("Sản phẩm không còn tồn tại. Vui lòng tải lại danh sách.", ManagementFailure.NotFound);
            product.IsActive = active;
            product.UpdatedAt = DateTime.UtcNow;
            await store.SaveAsync(cancellationToken);
            return ManagementResult.Success(id);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return ManagementResult.Fail(DatabaseError, ManagementFailure.Database);
        }
        finally { store.ClearTracking(); }
    }

    private static string? Validate(ProductEdit edit)
    {
        if (edit is null || string.IsNullOrWhiteSpace(edit.ProductName)) return "Vui lòng nhập tên sản phẩm.";
        if (edit.ProductName.Trim().Length > 200) return "Tên sản phẩm không được vượt quá 200 ký tự.";
        if (edit.BrandId <= 0) return "Vui lòng chọn thương hiệu.";
        if (edit.CategoryId <= 0) return "Vui lòng chọn danh mục.";
        if (edit.Price <= 0 || edit.Price > 9999999999999999.99m)
            return "Giá phải lớn hơn 0 và trong giới hạn cho phép.";
        if (decimal.Round(edit.Price, 2) != edit.Price) return "Giá chỉ được có tối đa 2 chữ số thập phân.";
        if (edit.StockQuantity < 0) return "Tồn kho phải là số nguyên không âm.";
        var fields = new (string? Value, int Max, string Label)[]
        {
            (edit.RAM, 50, "RAM"), (edit.Storage, 50, "Bộ nhớ"), (edit.Chip, 150, "Chip"),
            (edit.Screen, 200, "Màn hình"), (edit.Camera, 250, "Camera"),
            (edit.Battery, 100, "Pin"), (edit.OperatingSystem, 100, "Hệ điều hành")
        };
        foreach (var field in fields)
            if (field.Value?.Trim().Length > field.Max) return $"{field.Label} không được vượt quá {field.Max} ký tự.";
        return null;
    }

    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
