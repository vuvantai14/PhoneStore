using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Authentication;
using PhoneStore.Data.Authentication;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task Registration_hashes_password_and_normalizes_email()
    {
        await using var test = await TestDatabase.CreateAsync();
        var service = CustomerService(test);
        var result = await service.RegisterAsync("  Nguyễn Văn A  ", "  USER@Test.Local ", "0900000000", "Secure@123");
        Assert.Equal(RegistrationResult.Success, result);
        var user = await test.Db.Users.SingleAsync();
        Assert.Equal("user@test.local", user.Email);
        Assert.NotEqual("Secure@123", user.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed, Hasher().VerifyHashedPassword(user, user.PasswordHash, "Secure@123"));
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_case_insensitively()
    {
        await using var test = await TestDatabase.CreateAsync();
        var service = CustomerService(test);
        Assert.Equal(RegistrationResult.Success, await service.RegisterAsync("Nguyễn Văn A", "user@test.local", null, "Secure@123"));
        Assert.Equal(RegistrationResult.DuplicateEmail, await service.RegisterAsync("Nguyễn Văn B", "USER@test.local", null, "Secure@456"));
    }

    [Fact]
    public async Task Customer_login_accepts_valid_and_rejects_invalid_or_inactive()
    {
        await using var test = await TestDatabase.CreateAsync();
        var service = CustomerService(test);
        await service.RegisterAsync("Nguyễn Văn A", "user@test.local", null, "Secure@123");
        Assert.NotNull(await service.LoginAsync("USER@test.local", "Secure@123"));
        Assert.Null(await service.LoginAsync("user@test.local", "wrong-password"));
        var user = await test.Db.Users.SingleAsync();
        user.IsActive = false; await test.Db.SaveChangesAsync(); test.Db.ChangeTracker.Clear();
        Assert.Null(await service.LoginAsync("user@test.local", "Secure@123"));
    }

    [Fact]
    public async Task Customer_role_is_forbidden_from_admin_login()
    {
        await using var test = await TestDatabase.CreateAsync();
        await CustomerService(test).RegisterAsync("Nguyễn Văn A", "user@test.local", null, "Secure@123");
        var result = await AdminService(test).LoginAsync("user@test.local", "Secure@123");
        Assert.Equal(AdminLoginStatus.Forbidden, result.Status);
        Assert.Null(result.Identity);
    }

    [Fact]
    public async Task Active_admin_can_login_and_inactive_admin_is_blocked()
    {
        await using var test = await TestDatabase.CreateAsync();
        var roleId = await test.Db.Roles.Where(x => x.RoleName == "Admin").Select(x => x.RoleId).SingleAsync();
        var user = new User { FullName = "Admin Test", Email = "admin@test.local", RoleId = roleId, IsActive = true, CreatedAt = DateTime.UtcNow };
        user.PasswordHash = Hasher().HashPassword(user, "Admin@123");
        test.Db.Users.Add(user); await test.Db.SaveChangesAsync(); test.Db.ChangeTracker.Clear();
        Assert.Equal(AdminLoginStatus.Success, (await AdminService(test).LoginAsync(user.Email, "Admin@123")).Status);
        var stored = await test.Db.Users.SingleAsync(); stored.IsActive = false; await test.Db.SaveChangesAsync(); test.Db.ChangeTracker.Clear();
        Assert.Equal(AdminLoginStatus.InvalidCredentials, (await AdminService(test).LoginAsync(user.Email, "Admin@123")).Status);
    }

    [Fact]
    public async Task Fresh_admin_authorization_rechecks_active_and_role()
    {
        await using var test = await TestDatabase.CreateAsync();
        var adminRoleId = await test.Db.Roles.Where(x => x.RoleName == "Admin").Select(x => x.RoleId).SingleAsync();
        var customerRoleId = await test.Db.Roles.Where(x => x.RoleName == "Customer").Select(x => x.RoleId).SingleAsync();
        var user = new User { FullName = "Admin Test", Email = "admin@test.local", RoleId = adminRoleId,
            IsActive = true, CreatedAt = DateTime.UtcNow, PasswordHash = "not-used" };
        test.Db.Users.Add(user); await test.Db.SaveChangesAsync(); test.Db.ChangeTracker.Clear();
        var service = AdminService(test);

        Assert.True(await service.IsCurrentAdminAsync(user.UserId));
        var stored = await test.Db.Users.SingleAsync(x => x.UserId == user.UserId);
        stored.IsActive = false; await test.Db.SaveChangesAsync(); test.Db.ChangeTracker.Clear();
        Assert.False(await service.IsCurrentAdminAsync(user.UserId));
        stored = await test.Db.Users.SingleAsync(x => x.UserId == user.UserId);
        stored.IsActive = true; stored.RoleId = customerRoleId;
        await test.Db.SaveChangesAsync(); test.Db.ChangeTracker.Clear();
        Assert.False(await service.IsCurrentAdminAsync(user.UserId));
    }

    [Fact]
    public async Task Fresh_admin_authorization_fails_closed_when_database_is_unavailable()
    {
        var test = await TestDatabase.CreateAsync();
        var service = AdminService(test);
        await test.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.IsCurrentAdminAsync(1));
    }

    private static PasswordHasher<User> Hasher() => new();
    private static AuthService CustomerService(TestDatabase test) => new(new AuthUserStore(test.Db), Hasher());
    private static AdminAuthService AdminService(TestDatabase test) => new(new AuthUserStore(test.Db), Hasher());
}
