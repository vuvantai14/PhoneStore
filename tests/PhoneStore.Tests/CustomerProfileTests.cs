using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Authentication;
using PhoneStore.Data.Authentication;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class CustomerProfileTests
{
    private static CustomerProfileService Service(TestDatabase t) => new(new CustomerProfileStore(t.Db));

    [Fact]
    public async Task Read_returns_only_active_customer_profile()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (customer, _) = await t.SeedCatalogAsync();
        var service = Service(t);
        Assert.Equal(customer.Email, (await service.GetAsync(customer.UserId))!.Email);
        Assert.Null(await service.GetAsync(0));
        Assert.Null(await service.GetAsync(99999));
        await t.Db.Users.Where(u => u.UserId == customer.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Null(await service.GetAsync(customer.UserId));
        Assert.Equal(ProfileUpdateResult.NotFound, await service.UpdateAsync(customer.UserId, new("Changed name", null, null)));
    }

    [Fact]
    public async Task Update_changes_only_allowed_columns_and_leaves_other_users_and_catalog_unchanged()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (customer, product) = await t.SeedCatalogAsync();
        var admin = new User { FullName = "Admin", Email = "admin@profile.test", RoleId = 1, PasswordHash = "unchanged" };
        t.Db.Users.Add(admin); await t.Db.SaveChangesAsync(); t.Db.ChangeTracker.Clear();
        var before = await t.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == customer.UserId);
        Assert.Equal(ProfileUpdateResult.Success, await Service(t).UpdateAsync(customer.UserId, new("  Tên mới  ", " 09032657212 ", "  Địa chỉ  ")));
        var after = await t.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == customer.UserId);
        Assert.Equal("Tên mới", after.FullName); Assert.Equal("09032657212", after.Phone); Assert.Equal("Địa chỉ", after.Address);
        Assert.Equal(before.Email, after.Email); Assert.Equal(before.RoleId, after.RoleId);
        Assert.Equal(before.PasswordHash, after.PasswordHash); Assert.Equal(before.CreatedAt, after.CreatedAt); Assert.True(after.IsActive);
        Assert.Equal("Admin", (await t.Db.Users.AsNoTracking().SingleAsync(u => u.UserId == admin.UserId)).FullName);
        Assert.Equal(product.StockQuantity, await t.Db.Products.Where(p => p.ProductId == product.ProductId).Select(p => p.StockQuantity).SingleAsync());
        Assert.Equal(0, await t.Db.Orders.CountAsync());
    }

    [Fact]
    public async Task Admin_cannot_be_read_or_updated_as_customer()
    {
        await using var t = await TestDatabase.CreateAsync();
        var admin = new User { FullName = "Admin", Email = "admin@profile.test", RoleId = 1, PasswordHash = "unchanged" };
        t.Db.Users.Add(admin); await t.Db.SaveChangesAsync(); t.Db.ChangeTracker.Clear();
        Assert.Null(await Service(t).GetAsync(admin.UserId));
        Assert.Equal(ProfileUpdateResult.NotFound, await Service(t).UpdateAsync(admin.UserId, new("Changed name", null, null)));
        Assert.Equal("Admin", (await t.Db.Users.AsNoTracking().SingleAsync()).FullName);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(" ", null, null)]
    [InlineData("a", null, null)]
    [InlineData("Valid name", "not a phone", null)]
    public async Task Invalid_input_is_rejected_without_writes(string? name, string? phone, string? address)
    {
        await using var t = await TestDatabase.CreateAsync();
        var (customer, _) = await t.SeedCatalogAsync();
        Assert.Equal(ProfileUpdateResult.InvalidInput, await Service(t).UpdateAsync(customer.UserId, new(name, phone, address)));
        Assert.Equal(customer.FullName, (await t.Db.Users.AsNoTracking().SingleAsync()).FullName);
    }

    [Theory]
    [InlineData(101, 0, 0)]
    [InlineData(4, 21, 0)]
    [InlineData(4, 0, 256)]
    public async Task Oversized_fields_are_rejected(int nameLength, int phoneLength, int addressLength)
    {
        await using var t = await TestDatabase.CreateAsync();
        var (customer, _) = await t.SeedCatalogAsync();
        Assert.Equal(ProfileUpdateResult.InvalidInput, await Service(t).UpdateAsync(customer.UserId,
            new(new string('a', nameLength), new string('1', phoneLength), new string('a', addressLength))));
    }

    [Fact]
    public async Task Database_failure_returns_unavailable()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (customer, _) = await t.SeedCatalogAsync();
        // Closing this private in-memory database removes its tables, simulating an unavailable store.
        await t.Db.Database.GetDbConnection().CloseAsync();
        Assert.Equal(ProfileUpdateResult.Unavailable, await Service(t).UpdateAsync(customer.UserId, new("Valid name", null, null)));
    }

    [Fact]
    public async Task Optional_fields_can_be_cleared_and_boundary_values_are_supported()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (customer, _) = await t.SeedCatalogAsync();
        Assert.Equal(ProfileUpdateResult.Success, await Service(t).UpdateAsync(customer.UserId, new(new string('a', 100), "  ", new string('b', 255))));
        Assert.Null((await Service(t).GetAsync(customer.UserId))!.Phone);
        Assert.Equal(ProfileUpdateResult.Success, await Service(t).UpdateAsync(customer.UserId, new("Valid name", null, "  ")));
        Assert.Null((await Service(t).GetAsync(customer.UserId))!.Address);
    }
}
