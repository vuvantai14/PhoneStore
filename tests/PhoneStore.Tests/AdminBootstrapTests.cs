using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhoneStore.AdminBootstrap;
using PhoneStore.Business.Authentication;
using PhoneStore.Data.Authentication;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class AdminBootstrapTests
{
    [Theory]
    [InlineData("", "admin@example.com")]
    [InlineData("A", "admin@example.com")]
    [InlineData("Admin", "invalid")]
    [InlineData("Admin", "")]
    public async Task Invalid_identity_cannot_write(string name, string email)
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        var result = await Service(test).RunAsync(new(name, email));
        Assert.False(result.Succeeded);
        Assert.Empty(await test.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Dry_run_without_password_does_not_create_user_or_modify_roles()
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        var result = await Service(test).RunAsync(new(" Admin Name ", " ADMIN@Example.COM "));
        Assert.True(result.Succeeded, result.Message);
        Assert.False(result.Created);
        Assert.Empty(await test.Db.Users.ToListAsync());
        Assert.Equal(2, await test.Db.Roles.CountAsync());
    }

    [Fact]
    public async Task Execute_hashes_normalizes_and_is_compatible_with_admin_login()
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        var request = Execute(test);
        var result = await Service(test).RunAsync(request);
        Assert.True(result.Created, result.Message);
        var user = await test.Db.Users.SingleAsync();
        Assert.Equal("admin@example.com", user.Email);
        Assert.Equal("First Admin", user.FullName);
        Assert.True(user.IsActive);
        Assert.NotEqual(request.Password, user.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, request.Password!));
        var login = await new AdminAuthService(new AuthUserStore(test.Db), new PasswordHasher<User>())
            .LoginAsync(user.Email, request.Password!);
        Assert.Equal(AdminLoginStatus.Success, login.Status);
        Assert.Equal("Admin", login.Identity!.Role);
        Assert.Equal(2, await test.Db.Roles.CountAsync());
        Assert.DoesNotContain(request.Password!, result.Message);
        Assert.DoesNotContain(user.PasswordHash, result.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("        ")]
    public async Task Missing_or_invalid_password_refuses_execute(string? password)
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        Assert.False((await Service(test).RunAsync(Execute(test) with { Password = password })).Succeeded);
        Assert.Empty(await test.Db.Users.ToListAsync());
    }

    [Fact]
    public void Length_limits_are_validated()
    {
        Assert.NotNull(BootstrapService.Validate(new(new string('n', 101), "admin@example.com")));
        Assert.NotNull(BootstrapService.Validate(new("Admin", new string('a', 151) + "@example.com")));
        Assert.NotNull(BootstrapService.Validate(new("Admin", "admin@example.com", true, "localhost", "db", new string('p', 129))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Unconfirmed_or_wrong_target_cannot_write(int mismatch)
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        var request = Execute(test);
        request = mismatch switch
        {
            0 => request with { ConfirmServer = null, ConfirmDatabase = null },
            1 => request with { ConfirmServer = "wrong-server" },
            _ => request with { ConfirmDatabase = "wrong-database" }
        };
        var result = await Service(test).RunAsync(request);
        Assert.False(result.Succeeded);
        Assert.Empty(await test.Db.Users.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Existing_customer_is_never_overwritten_or_promoted(bool execute)
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        test.Db.Users.Add(new User { FullName = "Existing Customer", Email = " ADMIN@EXAMPLE.COM ",
            RoleId = 2, PasswordHash = "original-hash", IsActive = false });
        await test.Db.SaveChangesAsync();
        var request = execute ? Execute(test) : new BootstrapRequest("First Admin", "admin@example.com");
        Assert.False((await Service(test).RunAsync(request)).Succeeded);
        var user = await test.Db.Users.SingleAsync();
        Assert.Equal(2, user.RoleId);
        Assert.Equal("original-hash", user.PasswordHash);
        Assert.Equal("Existing Customer", user.FullName);
        Assert.False(user.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Any_existing_admin_including_inactive_blocks_creation(bool active)
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        test.Db.Users.Add(new User { FullName = "Existing Admin", Email = "other@example.com",
            RoleId = 1, PasswordHash = "original-hash", IsActive = active });
        await test.Db.SaveChangesAsync();
        Assert.False((await Service(test).RunAsync(Execute(test))).Succeeded);
        Assert.Equal("original-hash", (await test.Db.Users.SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task Missing_admin_role_is_not_created()
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        test.Db.Roles.Remove(await test.Db.Roles.SingleAsync(r => r.RoleName == "Admin"));
        await test.Db.SaveChangesAsync();
        Assert.False((await Service(test).RunAsync(Execute(test))).Succeeded);
        Assert.Empty(await test.Db.Users.ToListAsync());
        Assert.Single(await test.Db.Roles.ToListAsync());
    }

    [Fact]
    public async Task Unrelated_pending_changes_are_never_saved()
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        test.Db.Brands.Add(new Brand { BrandName = "Pending Brand" });
        Assert.False((await Service(test).RunAsync(Execute(test))).Succeeded);
        test.Db.ChangeTracker.Clear();
        Assert.Empty(await test.Db.Brands.ToListAsync());
        Assert.Empty(await test.Db.Users.ToListAsync());
    }

    [Fact]
    public async Task Repeated_execute_never_creates_second_admin()
    {
        await using var test = await TestDatabase.CreateAsync(useTemporaryFile: true);
        Assert.True((await Service(test).RunAsync(Execute(test))).Created);
        Assert.False((await Service(test).RunAsync(Execute(test) with { Email = "second@example.com" })).Succeeded);
        Assert.Single(await test.Db.Users.ToListAsync());
    }

    [Theory]
    [InlineData("--execute")]
    [InlineData("--password=secret")]
    [InlineData("--unknown=value")]
    public void Invalid_or_unsafe_cli_options_are_rejected(string argument) =>
        Assert.Throws<ArgumentException>(() => BootstrapOptions.Parse([argument]));

    [Fact]
    public void Cli_defaults_to_dry_run_and_rejects_duplicate_options()
    {
        Assert.False(BootstrapOptions.Parse(["--name=Admin", "--email=admin@example.com"]).Execute);
        Assert.Throws<ArgumentException>(() => BootstrapOptions.Parse(["--email=a@example.com", "--email=b@example.com"]));
        Assert.Throws<ArgumentException>(() => BootstrapOptions.Parse(["--execute", "--execute"]));
    }

    private static BootstrapService Service(TestDatabase test) => new(test.Db, new PasswordHasher<User>());
    private static BootstrapRequest Execute(TestDatabase test)
    {
        var connection = test.Db.Database.GetDbConnection();
        return new(" First Admin ", " ADMIN@Example.COM ", true, connection.DataSource, connection.Database, "Test-only-password!42");
    }
}
