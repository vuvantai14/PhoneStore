using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Management;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public sealed class ProductManagementService(ProductManagementStore productStore, IProductImageStorage imageStorage)
    : ProductEditorService(productStore), IProductManagementService
{
    private const string CleanupNotice = "Đã bỏ liên kết ảnh; tệp được giữ để tránh xóa nhầm khi có thao tác đồng thời. Cần đối chiếu liên kết trước khi dọn tệp:";

    public Task<ProductEditOptions> GetOptionsAsync(int? productId = null, CancellationToken cancellationToken = default) =>
        store.GetOptionsAsync(productId, cancellationToken);

    public Task<ProductEditOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default) =>
        store.GetFilterOptionsAsync(cancellationToken);

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
            result = ManagementResult.Fail("Không thể thêm ảnh. Chỉ tải lên JPEG/PNG giải mã được, tối đa 10 MB, mỗi chiều 4096 và 16 triệu điểm ảnh. WebP chưa được hỗ trợ. Kiểm tra tệp và thư mục lưu ảnh.");
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
            // A fresh reference check cannot make a later filesystem delete atomic with SQL.
            // Retain validated files after any DB attempt; report orphans for controlled cleanup.
            if (await store.IsImageUrlReferencedAsync(imageUrl, CancellationToken.None)) return notice + " " + imageUrl;
            return notice + " " + imageUrl;
        }
        catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
        {
            return notice + " " + imageUrl;
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
            return ManagementResult.Success(imageId, CleanupNotice + " " + removedUrl);
        }
        catch (Exception ex) when (ex is DbException or IOException or UnauthorizedAccessException)
        {
            return ManagementResult.Success(imageId, CleanupNotice + " " + removedUrl);
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
}
