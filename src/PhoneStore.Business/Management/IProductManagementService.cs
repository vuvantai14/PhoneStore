using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public interface IProductManagementService
{
    Task<IReadOnlyList<ProductManagementRow>> ListAsync(ProductManagementQuery query, CancellationToken cancellationToken = default);
    Task<ProductEdit?> GetEditAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductEditOptions> GetOptionsAsync(int? productId = null, CancellationToken cancellationToken = default);
    Task<ProductEditOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default);
    Task<ManagementResult> SaveAsync(int? id, ProductEdit edit, CancellationToken cancellationToken = default);
    Task<ManagementResult> SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductImageRow>> GetImagesAsync(int productId, CancellationToken cancellationToken = default);
    Task<ManagementResult> AddImageAsync(int productId, string sourceFile, CancellationToken cancellationToken = default);
    Task<ManagementResult> RemoveImageAsync(int productId, int imageId, CancellationToken cancellationToken = default);
    Task<ManagementResult> SetPrimaryImageAsync(int productId, int imageId, CancellationToken cancellationToken = default);
    Task<ManagementResult> SetImageOrderAsync(int productId, int imageId, int displayOrder, CancellationToken cancellationToken = default);
}
