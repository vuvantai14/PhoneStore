using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Authentication;
using PhoneStore.Business.Management;
using PhoneStore.Data.Authentication;
using PhoneStore.Data.Management;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Management;

namespace PhoneStore.Tests;

public sealed class SingleAdminPolicyTests
{
    [Fact]
    public async Task Multiple_customers_can_register_without_creating_or_modifying_admin()
    {
        await using var test = await TestDatabase.CreateAsync();
        var admin = await AddAdmin(test);
        var originalHash = admin.PasswordHash;
        var auth = CustomerService(test);
        Assert.Equal(RegistrationResult.Success, await auth.RegisterAsync("Customer One", "one@example.com", null, "Customer-test!42"));
        Assert.Equal(RegistrationResult.Success, await auth.RegisterAsync("Customer Two", "two@example.com", null, "Customer-test!43"));
        test.Db.ChangeTracker.Clear();
        var users = await test.Db.Users.Include(u => u.Role).ToListAsync();
        Assert.Equal(2, users.Count(u => u.Role.RoleName == "Customer"));
        var storedAdmin = Assert.Single(users, u => u.Role.RoleName == "Admin");
        Assert.Equal(admin.UserId, storedAdmin.UserId);
        Assert.Equal(originalHash, storedAdmin.PasswordHash);
        Assert.True(storedAdmin.IsActive);
    }

    [Fact]
    public async Task Registration_resolves_customer_role_by_name_instead_of_assuming_id()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Db.Roles.Remove(await test.Db.Roles.SingleAsync(r => r.RoleName == "Customer"));
        await test.Db.SaveChangesAsync();
        test.Db.Roles.Add(new Role { RoleId = 17, RoleName = "Customer" });
        await test.Db.SaveChangesAsync();
        Assert.Equal(RegistrationResult.Success, await CustomerService(test)
            .RegisterAsync("Customer Name", "customer@example.com", null, "Customer-test!42"));
        var user = await test.Db.Users.Include(u => u.Role).SingleAsync();
        Assert.Equal(17, user.RoleId);
        Assert.Equal("Customer", user.Role.RoleName);
    }

    [Fact]
    public async Task Missing_customer_role_does_not_fall_back_to_admin()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Db.Roles.Remove(await test.Db.Roles.SingleAsync(r => r.RoleName == "Customer"));
        await test.Db.SaveChangesAsync();
        Assert.Equal(RegistrationResult.CustomerRoleMissing, await CustomerService(test)
            .RegisterAsync("Customer Name", "customer@example.com", null, "Customer-test!42"));
        Assert.Empty(await test.Db.Users.ToListAsync());
        Assert.Equal("Admin", (await test.Db.Roles.SingleAsync()).RoleName);
    }

    [Fact]
    public async Task Registering_existing_admin_email_cannot_replace_or_demote_admin()
    {
        await using var test = await TestDatabase.CreateAsync();
        var admin = await AddAdmin(test);
        var originalHash = admin.PasswordHash;
        Assert.Equal(RegistrationResult.DuplicateEmail, await CustomerService(test)
            .RegisterAsync("Replacement Name", " ADMIN@EXAMPLE.COM ", null, "Customer-test!42"));
        var user = await test.Db.Users.Include(u => u.Role).SingleAsync();
        Assert.Equal("Admin", user.Role.RoleName);
        Assert.Equal("Original Admin", user.FullName);
        Assert.Equal(originalHash, user.PasswordHash);
    }

    [Fact]
    public async Task Customer_management_cannot_edit_or_disable_admin()
    {
        await using var test = await TestDatabase.CreateAsync();
        var admin = await AddAdmin(test);
        var service = new CustomerManagementService(new CustomerManagementStore(test.Db));
        Assert.Null(await service.GetAsync(admin.UserId));
        Assert.False((await service.UpdateAsync(admin.UserId, new CustomerEdit("Changed Name", "0900000000", "Changed address"))).Succeeded);
        Assert.False((await service.SetActiveAsync(admin.UserId, false)).Succeeded);
        var stored = await test.Db.Users.Include(u => u.Role).SingleAsync();
        Assert.Equal("Original Admin", stored.FullName);
        Assert.Equal("Admin", stored.Role.RoleName);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task Customer_management_preserves_customer_role_and_blocks_admin_login()
    {
        await using var test = await TestDatabase.CreateAsync();
        await CustomerService(test).RegisterAsync("Customer Name", "customer@example.com", null, "Customer-test!42");
        var userId = await test.Db.Users.Select(u => u.UserId).SingleAsync();
        var service = new CustomerManagementService(new CustomerManagementStore(test.Db));
        Assert.True((await service.UpdateAsync(userId, new CustomerEdit("Edited Customer", "0900000000", "New address"))).Succeeded);
        Assert.True((await service.SetActiveAsync(userId, false)).Succeeded);
        Assert.True((await service.SetActiveAsync(userId, true)).Succeeded);
        var stored = await test.Db.Users.Include(u => u.Role).SingleAsync();
        Assert.Equal("Customer", stored.Role.RoleName);
        var login = await new AdminAuthService(new AuthUserStore(test.Db), Hasher())
            .LoginAsync(stored.Email, "Customer-test!42");
        Assert.Equal(AdminLoginStatus.Forbidden, login.Status);
        Assert.Null(login.Identity);
        Assert.Equal("Customer", stored.Role.RoleName);
    }

    [Fact]
    public async Task Admin_cannot_use_customer_login_and_login_does_not_change_role()
    {
        await using var test = await TestDatabase.CreateAsync();
        var admin = await AddAdmin(test);
        Assert.Null(await CustomerService(test).LoginAsync(admin.Email, "Admin-test-only!42"));
        var stored = await test.Db.Users.Include(u => u.Role).SingleAsync();
        Assert.Equal("Admin", stored.Role.RoleName);
        Assert.True(stored.IsActive);
    }

    private static PasswordHasher<User> Hasher() => new();
    private static AuthService CustomerService(TestDatabase test) => new(new AuthUserStore(test.Db), Hasher());
    private static async Task<User> AddAdmin(TestDatabase test)
    {
        var roleId = await test.Db.Roles.Where(r => r.RoleName == "Admin").Select(r => r.RoleId).SingleAsync();
        var admin = new User { FullName = "Original Admin", Email = "admin@example.com", RoleId = roleId,
            IsActive = true, CreatedAt = DateTime.UtcNow };
        admin.PasswordHash = Hasher().HashPassword(admin, "Admin-test-only!42");
        test.Db.Users.Add(admin);
        await test.Db.SaveChangesAsync();
        return admin;
    }
}
