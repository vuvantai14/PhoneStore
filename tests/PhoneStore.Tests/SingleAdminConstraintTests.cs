using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PhoneStore.AdminBootstrap;
using PhoneStore.Data.Authentication;
using PhoneStore.Data.Context;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class SingleAdminConstraintTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Database_rejects_second_admin_even_when_inactive(bool firstActive, bool secondActive)
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Db.Users.Add(Admin("first@example.com", firstActive));
        await test.Db.SaveChangesAsync();
        test.Db.Users.Add(Admin("second@example.com", secondActive));
        await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
        test.Db.ChangeTracker.Clear();
        var remaining = await test.Db.Users.SingleAsync();
        Assert.Equal("first@example.com", remaining.Email);
        Assert.Equal(firstActive, remaining.IsActive);
    }

    [Fact]
    public async Task Database_rejects_promoting_customer_when_admin_already_exists()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Db.Users.Add(Admin("first@example.com", false));
        var customer = new User { FullName = "Customer", Email = "customer@example.com", RoleId = 2, PasswordHash = "unchanged" };
        test.Db.Users.Add(customer);
        await test.Db.SaveChangesAsync();
        customer.RoleId = SystemRoles.AdminId;
        await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
        test.Db.ChangeTracker.Clear();
        Assert.Equal(2, (await test.Db.Users.SingleAsync(u => u.Email == "customer@example.com")).RoleId);
        Assert.Equal(1, await test.Db.Users.CountAsync(u => u.RoleId == SystemRoles.AdminId));
    }

    [Fact]
    public async Task Database_rejects_reassigning_admin_role_name_to_another_id()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Db.Roles.Remove(await test.Db.Roles.SingleAsync(r => r.RoleId == SystemRoles.AdminId));
        await test.Db.SaveChangesAsync();
        test.Db.Roles.Add(new Role { RoleId = 17, RoleName = SystemRoles.Admin });
        await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Database_rejects_renaming_reserved_admin_role()
    {
        await using var test = await TestDatabase.CreateAsync();
        var role = await test.Db.Roles.SingleAsync(r => r.RoleId == SystemRoles.AdminId);
        role.RoleName = "Other";
        await Assert.ThrowsAsync<DbUpdateException>(() => test.Db.SaveChangesAsync());
        test.Db.ChangeTracker.Clear();
        Assert.Equal(SystemRoles.Admin, (await test.Db.Roles.FindAsync(SystemRoles.AdminId))!.RoleName);
    }

    [Fact]
    public async Task Registration_store_selects_customer_role_and_allows_multiple_customers()
    {
        await using var test = await TestDatabase.CreateAsync();
        test.Db.Users.Add(Admin("first@example.com", true));
        await test.Db.SaveChangesAsync();
        var store = new AuthUserStore(test.Db);
        Assert.Equal(RegistrationResult.Success, await store.CreateCustomerAsync("Customer One", "one@example.com", null, "hash-one", default));
        Assert.Equal(RegistrationResult.Success, await store.CreateCustomerAsync("Customer Two", "two@example.com", null, "hash-two", default));
        Assert.Equal(2, await test.Db.Users.CountAsync(u => u.Role.RoleName == SystemRoles.Customer));
        Assert.Equal(1, await test.Db.Users.CountAsync(u => u.Role.RoleName == SystemRoles.Admin));
    }

    [Fact]
    public async Task Registration_cannot_flush_pending_unauthorized_role_changes()
    {
        await using var test = await TestDatabase.CreateAsync();
        var customer = new User { FullName = "Customer", Email = "original@example.com", RoleId = 2, PasswordHash = "unchanged" };
        test.Db.Users.Add(customer);
        await test.Db.SaveChangesAsync();
        customer.RoleId = SystemRoles.AdminId;
        var result = await new AuthUserStore(test.Db)
            .CreateCustomerAsync("New Customer", "new@example.com", null, "hash", default);
        Assert.Equal(RegistrationResult.Unavailable, result);
        test.Db.ChangeTracker.Clear();
        var stored = await test.Db.Users.SingleAsync();
        Assert.Equal(2, stored.RoleId);
        Assert.Equal("unchanged", stored.PasswordHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bootstrap_refuses_legacy_admin_role_mapping_without_repairing_it(bool execute)
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        // Simulate a legacy database before this constraint exists, only in isolated SQLite.
        await test.Db.Database.ExecuteSqlRawAsync("PRAGMA ignore_check_constraints = ON");
        test.Db.Roles.Remove(await test.Db.Roles.SingleAsync(r => r.RoleId == SystemRoles.AdminId));
        await test.Db.SaveChangesAsync();
        test.Db.Roles.Add(new Role { RoleId = 17, RoleName = SystemRoles.Admin });
        await test.Db.SaveChangesAsync();
        test.Db.ChangeTracker.Clear();
        var connection = test.Db.Database.GetDbConnection();
        var result = await new BootstrapService(test.Db, new PasswordHasher<User>()).RunAsync(
            new("Admin Name", "admin@example.com", execute, connection.DataSource, connection.Database, "Test-only-password!42"));
        Assert.False(result.Succeeded);
        Assert.Contains("ID", result.Message);
        Assert.Empty(await test.Db.Users.ToListAsync());
        Assert.Equal(17, (await test.Db.Roles.SingleAsync(r => r.RoleName == SystemRoles.Admin)).RoleId);
    }

    [Fact]
    public void Sql_server_migration_script_preserves_data_and_checks_legacy_mapping()
    {
        // Script generation only: no SQL Server connection is opened.
        using var db = new PhoneStoreDbContext(new DbContextOptionsBuilder<PhoneStoreDbContext>()
            .UseSqlServer("Server=localhost;Database=UnusedOfflineScript;Integrated Security=true;TrustServerCertificate=true").Options);
        var script = db.GetService<IMigrator>().GenerateScript("20261001150857_InitialCreate", "20261102122856_EnforceSingleAdmin");
        Assert.Contains("THROW 51000", script);
        Assert.Contains("THROW 51001", script);
        Assert.Contains("CREATE UNIQUE INDEX [UX_Users_SingleAdmin] ON [Users] ([RoleId]) WHERE [RoleId] = 1", script);
        Assert.Contains("CK_Roles_AdminIdentity", script);
        Assert.DoesNotContain("DROP INDEX [IX_Users_RoleId]", script);
        Assert.DoesNotContain("DELETE FROM [Users]", script);
        Assert.DoesNotContain("UPDATE [Users]", script);
        Assert.DoesNotContain("INSERT INTO [Users]", script);
        Assert.DoesNotContain("UPDATE [Roles]", script);
    }

    private static User Admin(string email, bool active) => new()
    {
        FullName = "Existing Admin", Email = email, RoleId = SystemRoles.AdminId,
        PasswordHash = "unchanged-existing-hash", IsActive = active, CreatedAt = DateTime.UtcNow
    };
}
