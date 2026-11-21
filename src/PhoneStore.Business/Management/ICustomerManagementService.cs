using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public interface ICustomerManagementService
{
    Task<IReadOnlyList<CustomerManagementRow>> ListAsync(CustomerManagementQuery query, CancellationToken ct = default);
    Task<(IReadOnlyList<CustomerManagementRow> Items, int Total)> ListPageAsync(CustomerManagementQuery query, int offset, int limit, CancellationToken ct = default);
    Task<CustomerManagementRow?> GetAsync(int id, CancellationToken ct = default);
    Task<ManagementResult> UpdateAsync(int id, CustomerEdit edit, CancellationToken ct = default);
    Task<ManagementResult> SetActiveAsync(int id, bool active, CancellationToken ct = default);
}
