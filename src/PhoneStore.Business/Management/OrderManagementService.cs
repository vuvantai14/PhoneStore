using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Management;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public sealed class OrderManagementService(OrderManagementStore store) : IOrderManagementService
{
    private const string DatabaseError = "Chưa thể cập nhật đơn hàng. Vui lòng kiểm tra kết nối dữ liệu và thử lại.";
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> Allowed =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Shipping, OrderStatus.Cancelled],
            [OrderStatus.Shipping] = [OrderStatus.Completed],
            [OrderStatus.Completed] = [],
            [OrderStatus.Cancelled] = []
        };

    public Task<IReadOnlyList<OrderManagementRow>> ListAsync(OrderManagementQuery query, CancellationToken ct = default) =>
        store.ListAsync(query, ct);

    public Task<OrderManagementDetail?> GetAsync(int id, CancellationToken ct = default) =>
        id > 0 ? store.GetAsync(id, ct) : Task.FromResult<OrderManagementDetail?>(null);

    public IReadOnlyList<OrderStatus> NextStatuses(OrderStatus current) =>
        Allowed.TryGetValue(current, out var statuses) ? statuses : [];

    public async Task<ManagementResult> UpdateStatusAsync(int id, OrderStatus expected, OrderStatus target,
        CancellationToken ct = default)
    {
        if (id <= 0 || !Enum.IsDefined(expected) || !Enum.IsDefined(target))
            return ManagementResult.Fail("Đơn hàng hoặc trạng thái không hợp lệ.");
        try
        {
            await using var transaction = await store.BeginAsync(ct);
            var order = await store.LockAsync(id, ct);
            if (order is null) return ManagementResult.Fail("Đơn hàng không còn tồn tại. Vui lòng tải lại danh sách.");
            if (order.Status != expected)
                return ManagementResult.Fail("Trạng thái đơn hàng đã thay đổi. Vui lòng tải lại trước khi thao tác.");
            if (!NextStatuses(order.Status).Contains(target))
                return ManagementResult.Fail("Không thể chuyển đơn hàng sang trạng thái đã chọn.");

            if (target == OrderStatus.Cancelled)
            {
                var details = await store.DetailsForCancellationAsync(id, ct);
                if (!RestoreStock(details))
                    return ManagementResult.Fail("Không thể hoàn tồn kho vì sản phẩm không còn tồn tại.");
            }

            order.Status = target;
            await store.SaveAsync(ct);
            await transaction.CommitAsync(ct);
            return ManagementResult.Success(id);
        }
        catch (OverflowException)
        {
            return ManagementResult.Fail("Không thể hoàn tồn kho vì số lượng vượt giới hạn cho phép.");
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            return ManagementResult.Fail(DatabaseError);
        }
        finally { store.ClearTracking(); }
    }

    private static bool RestoreStock(IEnumerable<PhoneStore.Models.Entities.OrderDetail> details)
    {
        foreach (var detail in details)
        {
            if (detail.Product is null) return false;
            detail.Product.StockQuantity = checked(detail.Product.StockQuantity + detail.Quantity);
            detail.Product.UpdatedAt = DateTime.UtcNow;
        }
        return true;
    }
}
