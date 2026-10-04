using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public interface ILookupManagementService
{
    Task<IReadOnlyList<LookupRow>> ListAsync(LookupKind kind, string? search, CancellationToken cancellationToken = default);
    Task<ManagementResult> SaveAsync(LookupKind kind, int? id, LookupEdit edit, CancellationToken cancellationToken = default);
    Task<ManagementResult> SetActiveAsync(LookupKind kind, int id, bool active, CancellationToken cancellationToken = default);
}
