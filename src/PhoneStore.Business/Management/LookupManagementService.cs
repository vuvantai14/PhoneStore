using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Management;
using PhoneStore.Models.Management;

namespace PhoneStore.Business.Management;

public sealed class LookupManagementService(LookupManagementStore store) : ILookupManagementService
{
    public Task<IReadOnlyList<LookupRow>> ListAsync(LookupKind kind, string? search,
        CancellationToken cancellationToken = default) => store.ListAsync(kind, search, cancellationToken);

    public async Task<ManagementResult> SaveAsync(LookupKind kind, int? id, LookupEdit edit,
        CancellationToken cancellationToken = default)
    {
        var title = GetTitle(kind);
        if (edit is null)
            return ManagementResult.Fail("Vui lòng nhập thông tin cần lưu.");
        var name = edit.Name?.Trim() ?? "";
        var description = string.IsNullOrWhiteSpace(edit.Description) ? null : edit.Description.Trim();
        if (name.Length == 0)
            return ManagementResult.Fail($"Vui lòng nhập tên {title}.");
        if (name.Length > 100)
            return ManagementResult.Fail($"Tên {title} không được vượt quá 100 ký tự.");
        if (description?.Length > 500)
            return ManagementResult.Fail("Mô tả không được vượt quá 500 ký tự.");
        if (id is <= 0)
            return ManagementResult.Fail($"Không tìm thấy {title} cần chỉnh sửa.");

        try
        {
            if (await store.NameExistsAsync(kind, id, name.ToUpperInvariant(), cancellationToken))
                return ManagementResult.Fail($"Tên {title} đã tồn tại. Vui lòng chọn tên khác.", ManagementFailure.Conflict);
            var savedId = await store.SaveAsync(kind, id, new LookupEdit(name, description, edit.IsActive), cancellationToken);
            return savedId.HasValue ? ManagementResult.Success(savedId)
                : ManagementResult.Fail($"{char.ToUpper(title[0]) + title[1..]} không còn tồn tại. Vui lòng tải lại danh sách.", ManagementFailure.NotFound);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return ManagementResult.Fail($"Tên {title} đã tồn tại. Vui lòng chọn tên khác.", ManagementFailure.Conflict);
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            return ManagementResult.Fail("Không thể lưu dữ liệu. Vui lòng kiểm tra kết nối và thử lại.", ManagementFailure.Database);
        }
        finally
        {
            store.ClearTracking();
        }
    }

    public async Task<ManagementResult> SetActiveAsync(LookupKind kind, int id, bool active,
        CancellationToken cancellationToken = default)
    {
        var title = GetTitle(kind);
        if (id <= 0)
            return ManagementResult.Fail($"Không tìm thấy {title}.");
        try
        {
            return await store.SetActiveAsync(kind, id, active, cancellationToken)
                ? ManagementResult.Success(id)
                : ManagementResult.Fail("Dữ liệu không còn tồn tại. Vui lòng tải lại danh sách.", ManagementFailure.NotFound);
        }
        catch (Exception exception) when (exception is DbUpdateException or DbException)
        {
            return ManagementResult.Fail("Không thể cập nhật trạng thái. Vui lòng kiểm tra kết nối và thử lại.", ManagementFailure.Database);
        }
        finally
        {
            store.ClearTracking();
        }
    }

    private static string GetTitle(LookupKind kind) => kind switch
    {
        LookupKind.Brand => "thương hiệu",
        LookupKind.Category => "danh mục",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
