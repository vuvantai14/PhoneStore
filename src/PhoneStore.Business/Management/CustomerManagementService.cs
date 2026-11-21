using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Management;
using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public sealed class CustomerManagementService(CustomerManagementStore store) : ICustomerManagementService
{
    private const string DatabaseError = "Chưa thể lưu thay đổi. Vui lòng kiểm tra kết nối dữ liệu và thử lại.";

    public Task<IReadOnlyList<CustomerManagementRow>> ListAsync(CustomerManagementQuery query, CancellationToken ct = default) =>
        store.ListAsync(query, ct);

    public Task<(IReadOnlyList<CustomerManagementRow> Items, int Total)> ListPageAsync(
        CustomerManagementQuery query, int offset, int limit, CancellationToken ct) =>
        store.ListPageAsync(query, offset, limit, ct);

    public Task<CustomerManagementRow?> GetAsync(int id, CancellationToken ct = default) =>
        id > 0 ? store.GetRowAsync(id, ct) : Task.FromResult<CustomerManagementRow?>(null);

    public async Task<ManagementResult> UpdateAsync(int id, CustomerEdit edit, CancellationToken ct = default)
    {
        if (id <= 0) return ManagementResult.Fail("Khách hàng không hợp lệ.");
        var error = Validate(edit);
        if (error is not null) return ManagementResult.Fail(error);
        try
        {
            var customer = await store.FindAsync(id, ct);
            if (customer is null) return ManagementResult.Fail("Khách hàng không còn tồn tại. Vui lòng tải lại danh sách.", ManagementFailure.NotFound);
            customer.FullName = edit.FullName.Trim();
            customer.Phone = TrimOptional(edit.Phone);
            customer.Address = TrimOptional(edit.Address);
            await store.SaveAsync(ct);
            return ManagementResult.Success(id);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            return ManagementResult.Fail(DatabaseError, ManagementFailure.Database);
        }
        finally { store.ClearTracking(); }
    }

    public async Task<ManagementResult> SetActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        if (id <= 0) return ManagementResult.Fail("Khách hàng không hợp lệ.");
        try
        {
            var changed = await store.SetActiveAsync(id, active, ct);
            return changed == 1 ? ManagementResult.Success(id)
                : ManagementResult.Fail("Khách hàng không còn tồn tại. Vui lòng tải lại danh sách.", ManagementFailure.NotFound);
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            return ManagementResult.Fail(DatabaseError, ManagementFailure.Database);
        }
        finally { store.ClearTracking(); }
    }

    private static string? Validate(CustomerEdit edit)
    {
        if (edit is null || string.IsNullOrWhiteSpace(edit.FullName)) return "Vui lòng nhập họ tên khách hàng.";
        if (edit.FullName.Trim().Length > 100) return "Họ tên không được vượt quá 100 ký tự.";
        if (edit.Phone?.Trim().Length > 20) return "Số điện thoại không được vượt quá 20 ký tự.";
        if (edit.Address?.Trim().Length > 255) return "Địa chỉ không được vượt quá 255 ký tự.";
        return null;
    }

    private static string? TrimOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
