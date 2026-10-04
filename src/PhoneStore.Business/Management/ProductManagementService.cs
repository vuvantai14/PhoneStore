using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Management;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public sealed class ProductManagementService(ProductManagementStore store, IProductImageStorage imageStorage)
    : IProductManagementService
{
    private const string DatabaseError = "Chưa thể lưu thay đổi. Vui lòng kiểm tra kết nối dữ liệu và thử lại.";
    private const string CleanupNotice = "Đã bỏ liên kết ảnh; tệp chưa được xóa an toàn, cần kiểm tra thư mục ảnh.";

    public Task<IReadOnlyList<ProductManagementRow>> ListAsync(ProductManagementQuery query, CancellationToken cancellationToken = default) =>
        store.ListAsync(query, cancellationToken);

    public Task<ProductEdit?> GetEditAsync(int id, CancellationToken cancellationToken = default) =>
        store.GetEditAsync(id, cancellationToken);

    public Task<ProductEditOptions> GetOptionsAsync(int? productId = null, CancellationToken cancellationToken = default) =>
        store.GetOptionsAsync(productId, cancellationToken);

    public Task<ProductEditOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default) =>
        store.GetFilterOptionsAsync(cancellationToken);

    public async Task<ManagementResult> SaveAsync(int? id, ProductEdit edit, CancellationToken cancellationToken = default)
    {
        var error = Validate(edit);
        if (error is not null) return ManagementResult.Fail(error);
        if (id is <= 0) return ManagementResult.Fail("Sản phẩm không hợp lệ.");
        try
        {
            await using var transaction = await store.BeginAsync(cancellationToken);
            var product = id.HasValue ? await store.FindAsync(id.Value, cancellationToken) : new Product();
            if (product is null) return ManagementResult.Fail("Sản phẩm không còn tồn tại. Vui lòng tải lại danh sách.");
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
            }
            else product.UpdatedAt = DateTime.UtcNow;
            await store.SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ManagementResult.Success(product.ProductId);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return ManagementResult.Fail(DatabaseError);
        }
        finally { store.ClearTracking(); }
    }

    public async Task<ManagementResult> SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return ManagementResult.Fail("Sản phẩm không hợp lệ.");
        try
        {
            var product = await store.FindAsync(id, cancellationToken);
            if (product is null) return ManagementResult.Fail("Sản phẩm không còn tồn tại. Vui lòng tải lại danh sách.");
            product.IsActive = active;
            product.UpdatedAt = DateTime.UtcNow;
            await store.SaveAsync(cancellationToken);
            return ManagementResult.Success(id);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return ManagementResult.Fail(DatabaseError);
        }
        finally { store.ClearTracking(); }
    }

    public Task<IReadOnlyList<ProductImageRow>> GetImagesAsync(int productId, CancellationToken cancellationToken = default) =>
        store.GetImagesAsync(productId, cancellationToken);

    public async Task<ManagementResult> AddImageAsync(int productId, string sourceFile, CancellationToken cancellationToken = default)
    {
        if (productId <= 0) return ManagementResult.Fail("Vui lòng lưu sản phẩm trước khi thêm ảnh.");
        if (string.IsNullOrWhiteSpace(sourceFile)) return ManagementResult.Fail("Vui lòng chọn tệp ảnh.");
        string? copiedUrl = null;
        ManagementResult result;
        try
        {
            if (!await store.ExistsAsync(productId, cancellationToken))
                return ManagementResult.Fail("Sản phẩm không còn tồn tại. Vui lòng tải lại danh sách.");
            copiedUrl = await imageStorage.StoreAsync(sourceFile, cancellationToken);
            result = await AddImageRecordAsync(productId, copiedUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            result = ManagementResult.Fail("Không thể thêm ảnh. Vui lòng chọn ảnh JPG/JPEG, PNG hoặc WebP hợp lệ và kiểm tra thư mục lưu ảnh.");
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            result = ManagementResult.Fail(DatabaseError);
        }
        catch (OperationCanceledException)
        {
            store.ClearTracking();
            if (copiedUrl is not null) await CleanupCopiedImageAsync(copiedUrl);
            throw;
        }
        finally { store.ClearTracking(); }

        if (!result.Succeeded && copiedUrl is not null)
        {
            var notice = await CleanupCopiedImageAsync(copiedUrl);
            if (notice is not null) result = result with { Error = result.Error + " " + notice, Notice = notice };
        }
        return result;
    }

    private async Task<ManagementResult> AddImageRecordAsync(int productId, string imageUrl, CancellationToken ct)
    {
        await using var transaction = await store.BeginAsync(ct);
        var product = await store.LockProductAsync(productId, ct);
        if (product is null) return ManagementResult.Fail("Sản phẩm không còn tồn tại. Vui lòng tải lại danh sách.");
        var images = await store.ImagesForUpdateAsync(productId, ct);
        if (images.Count > 0 && images.Max(i => i.DisplayOrder) == int.MaxValue)
            return ManagementResult.Fail("Thứ tự ảnh đã đạt giới hạn. Vui lòng điều chỉnh thứ tự trước khi thêm ảnh.");
        var image = new ProductImage
        {
            ProductId = productId, ImageUrl = imageUrl,
            DisplayOrder = images.Count == 0 ? 0 : images.Max(i => i.DisplayOrder) + 1
        };
        images.Add(image);
        NormalizePrimary(images);
        store.AddImage(image);
        product.UpdatedAt = DateTime.UtcNow;
        await store.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return ManagementResult.Success(image.ImageId);
    }

    private async Task<string?> CleanupCopiedImageAsync(string imageUrl)
    {
        const string notice = "Vui lòng tải lại danh sách ảnh. Tệp được giữ an toàn; ảnh không còn liên kết có thể cần dọn dẹp thủ công.";
        try
        {
            // A failed/cancelled commit can have an unknown outcome. Never remove its file
            // unless a fresh query confirms that no committed image association uses it.
            if (await store.IsImageUrlReferencedAsync(imageUrl, CancellationToken.None)) return notice;
            return imageStorage.TryDelete(imageUrl) ? null : notice;
        }
        catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
        {
            return notice;
        }
    }

    public async Task<ManagementResult> RemoveImageAsync(int productId, int imageId, CancellationToken cancellationToken = default)
    {
        if (productId <= 0 || imageId <= 0) return ManagementResult.Fail("Ảnh sản phẩm không hợp lệ.");
        string removedUrl;
        try
        {
            await using var transaction = await store.BeginAsync(cancellationToken);
            var product = await store.LockProductAsync(productId, cancellationToken);
            if (product is null) return ManagementResult.Fail("Sản phẩm không còn tồn tại. Vui lòng tải lại danh sách.");
            var images = await store.ImagesForUpdateAsync(productId, cancellationToken);
            var image = images.SingleOrDefault(i => i.ImageId == imageId);
            if (image is null) return ManagementResult.Fail("Ảnh không còn tồn tại hoặc không thuộc sản phẩm này.");
            removedUrl = image.ImageUrl;
            store.RemoveImage(image);
            images.Remove(image);
            NormalizePrimary(images);
            product.UpdatedAt = DateTime.UtcNow;
            await store.SaveAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return ManagementResult.Fail(DatabaseError);
        }
        finally { store.ClearTracking(); }

        // The association has committed. A cleanup failure must not pretend the removal failed.
        try
        {
            if (await store.IsImageUrlReferencedAsync(removedUrl, cancellationToken))
                return ManagementResult.Success(imageId, "Đã bỏ liên kết ảnh; tệp được giữ vì sản phẩm khác còn sử dụng.");
            return ManagementResult.Success(imageId, imageStorage.TryDelete(removedUrl) ? null : CleanupNotice);
        }
        catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
        {
            return ManagementResult.Success(imageId, CleanupNotice);
        }
    }

    public Task<ManagementResult> SetPrimaryImageAsync(int productId, int imageId, CancellationToken cancellationToken = default) =>
        UpdateImageAsync(productId, imageId, null, cancellationToken);

    public Task<ManagementResult> SetImageOrderAsync(int productId, int imageId, int displayOrder, CancellationToken cancellationToken = default)
    {
        if (displayOrder < 0) return Task.FromResult(ManagementResult.Fail("Thứ tự ảnh phải là số nguyên không âm."));
        return UpdateImageAsync(productId, imageId, displayOrder, cancellationToken);
    }

    private async Task<ManagementResult> UpdateImageAsync(int productId, int imageId, int? displayOrder, CancellationToken ct)
    {
        if (productId <= 0 || imageId <= 0) return ManagementResult.Fail("Ảnh sản phẩm không hợp lệ.");
        try
        {
            await using var transaction = await store.BeginAsync(ct);
            var product = await store.LockProductAsync(productId, ct);
            if (product is null) return ManagementResult.Fail("Sản phẩm không còn tồn tại. Vui lòng tải lại danh sách.");
            var images = await store.ImagesForUpdateAsync(productId, ct);
            var image = images.SingleOrDefault(i => i.ImageId == imageId);
            if (image is null) return ManagementResult.Fail("Ảnh không còn tồn tại hoặc không thuộc sản phẩm này.");
            if (displayOrder.HasValue)
            {
                image.DisplayOrder = displayOrder.Value;
                NormalizePrimary(images);
            }
            else NormalizePrimary(images, image);
            product.UpdatedAt = DateTime.UtcNow;
            await store.SaveAsync(ct);
            await transaction.CommitAsync(ct);
            return ManagementResult.Success(imageId);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return ManagementResult.Fail(DatabaseError);
        }
        finally { store.ClearTracking(); }
    }

    private static void NormalizePrimary(IReadOnlyList<ProductImage> images, ProductImage? preferred = null)
    {
        var primary = preferred ?? images.FirstOrDefault(i => i.IsPrimary) ?? images.FirstOrDefault();
        foreach (var image in images) image.IsPrimary = ReferenceEquals(image, primary);
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
