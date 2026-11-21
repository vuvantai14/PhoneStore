using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public interface IOrderManagementService
{
    Task<IReadOnlyList<OrderManagementRow>> ListAsync(OrderManagementQuery query, CancellationToken ct = default);
    Task<(IReadOnlyList<OrderManagementRow> Items, int Total)> ListPageAsync(OrderManagementQuery query, int offset, int limit, CancellationToken ct = default);
    Task<OrderManagementDetail?> GetAsync(int id, CancellationToken ct = default);
    IReadOnlyList<OrderStatus> NextStatuses(OrderStatus current);
    Task<ManagementResult> UpdateStatusAsync(int id, OrderStatus expected, OrderStatus target, CancellationToken ct = default);
    Task<ManagementResult> CancelForCustomerAsync(int id, int customerId, CancellationToken ct = default);
}
