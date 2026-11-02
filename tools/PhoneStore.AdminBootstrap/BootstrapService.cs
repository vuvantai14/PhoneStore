using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

namespace PhoneStore.AdminBootstrap;

public sealed record BootstrapRequest(string Name, string Email, bool Execute = false,
    string? ConfirmServer = null, string? ConfirmDatabase = null, string? Password = null);

public sealed record BootstrapResult(bool Succeeded, bool Created, string Message);

public sealed class BootstrapService(PhoneStoreDbContext db, IPasswordHasher<User> hasher)
{
    public static string? Validate(BootstrapRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 100)
            return "Họ tên phải có từ 2 đến 100 ký tự.";
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Trim().Length > 150
            || !new EmailAddressAttribute().IsValid(request.Email.Trim()))
            return "Email không hợp lệ hoặc vượt quá 150 ký tự.";
        if (request.Execute && (string.IsNullOrWhiteSpace(request.ConfirmServer)
            || string.IsNullOrWhiteSpace(request.ConfirmDatabase)))
            return "Execute yêu cầu xác nhận cả server và database.";
        if (request.Execute && (string.IsNullOrWhiteSpace(request.Password)
            || request.Password.Length is < 8 or > 128))
            return "Mật khẩu phải có từ 8 đến 128 ký tự và không chỉ gồm khoảng trắng.";
        return null;
    }

    public async Task<BootstrapResult> RunAsync(BootstrapRequest request, CancellationToken ct = default)
    {
        var error = Validate(request);
        if (error is not null) return new(false, false, error);
        // This command owns a fresh context; never persist unrelated tracked changes.
        if (db.ChangeTracker.HasChanges()) return new(false, false, "DbContext có thay đổi chưa lưu; từ chối thực hiện.");
        var email = request.Email.Trim().ToLowerInvariant();
        try
        {
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                var connection = db.Database.GetDbConnection();
                if (request.Execute && (!string.Equals(connection.DataSource, request.ConfirmServer, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(connection.Database, request.ConfirmDatabase, StringComparison.Ordinal)))
                    return new(false, false, "Server/database thực tế không khớp xác nhận; không ghi dữ liệu.");

                // Serialize the absence check and insert. Concurrent attempts may fail safely
                // with a deadlock/unique constraint error; never automatically retry writes.
                await using var transaction = request.Execute
                    ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
                var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.RoleName == "Admin", ct);
                if (role is null) return new(false, false, "Thiếu role Admin; không tự tạo role hoặc migration.");
                if (await db.Users.AsNoTracking().AnyAsync(u => u.RoleId == role.RoleId, ct))
                    return new(false, false, "Đã có Admin (kể cả bị khóa); không tạo thêm hoặc ghi đè.");
                if (await db.Users.AsNoTracking().AnyAsync(u => u.Email.Trim().ToUpper() == email.ToUpper(), ct))
                    return new(false, false, "Email đã tồn tại; không ghi đè hoặc nâng quyền tài khoản.");
                if (!request.Execute)
                    return new(true, false, "DRY-RUN: đủ điều kiện tạo Admin đầu tiên; chưa nhập mật khẩu, không ghi database.");

                var user = new User
                {
                    FullName = request.Name.Trim(), Email = email, RoleId = role.RoleId,
                    IsActive = true, CreatedAt = DateTime.UtcNow
                };
                user.PasswordHash = hasher.HashPassword(user, request.Password!);
                db.Users.Add(user);
                await db.SaveChangesAsync(ct);
                await transaction!.CommitAsync(ct);
                return new(true, true, "Đã tạo Admin đầu tiên.");
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            // Do not print SQL, connection strings, password hashes or exception details.
            return new(false, false, "Không thể hoàn tất. Kiểm tra database bằng truy vấn chỉ đọc trước khi thử lại; kết quả commit có thể chưa xác định.");
        }
        finally { db.ChangeTracker.Clear(); }
    }
}
