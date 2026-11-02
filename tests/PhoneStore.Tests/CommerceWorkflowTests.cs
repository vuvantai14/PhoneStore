using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Management;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Management;
using PhoneStore.Data.Purchasing;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Tests;

public sealed class CommerceWorkflowTests
{
    static ShippingInfo Shipping => new("Receiver", "0900000000", "Test address", null);
    static OrderService Orders(TestDatabase t) => new(new PurchaseStore(t.Db));
    static OrderManagementService Admin(TestDatabase t) => new(new OrderManagementStore(t.Db));

    [Fact]
    public async Task Same_token_for_different_customers_never_reuses_another_customers_order()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (first, product) = await t.SeedCatalogAsync();
        var second = new User { FullName = "Second Customer", Email = "second-key@test.local", RoleId = first.RoleId,
            PasswordHash = "isolated", IsActive = true };
        t.Db.Users.Add(second); await t.Db.SaveChangesAsync();
        var key = Guid.NewGuid().ToString("N");
        var one = await Orders(t).CreateIdempotentAsync(first.UserId, [new(product.ProductId, 1)], Shipping, key);
        var two = await Orders(t).CreateIdempotentAsync(second.UserId, [new(product.ProductId, 1)], Shipping, key);
        Assert.NotNull(one.OrderId); Assert.NotNull(two.OrderId);
        Assert.NotEqual(one.OrderId, two.OrderId);
        Assert.Equal(2, await t.Db.Orders.Select(o => o.OrderCode).Distinct().CountAsync());
        Assert.Null(await Orders(t).GetReceiptAsync(one.OrderId!.Value, second.UserId));
    }

    [Fact]
    public async Task Same_checkout_token_survives_new_context_and_empty_cart_without_second_decrement()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (customer, product) = await t.SeedCatalogAsync();
        var token = Guid.NewGuid().ToString("N");
        var first = await Orders(t).CreateIdempotentAsync(customer.UserId, [new(product.ProductId, 1)], Shipping, token);
        await using var other = new PhoneStore.Data.Context.PhoneStoreDbContext(
            new DbContextOptionsBuilder<PhoneStore.Data.Context.PhoneStoreDbContext>().UseSqlite(t.Db.Database.GetDbConnection()).Options);
        var replay = await new OrderService(new PurchaseStore(other)).CreateIdempotentAsync(customer.UserId, [], Shipping, token);
        Assert.NotNull(first.OrderId);
        Assert.Equal(first.OrderId, replay.OrderId);
        Assert.Single(await t.Db.Orders.ToListAsync());
        Assert.Equal(9, (await t.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        Assert.Null(await Orders(t).GetCheckoutOrderIdAsync(customer.UserId + 999, token));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public async Task Invalid_checkout_token_cannot_write(string? token)
    {
        await using var t = await TestDatabase.CreateAsync();
        var (c, p) = await t.SeedCatalogAsync();
        Assert.Null((await Orders(t).CreateIdempotentAsync(c.UserId, [new(p.ProductId, 1)], Shipping, token)).OrderId);
        Assert.Empty(await t.Db.Orders.ToListAsync());
        Assert.Equal(10, (await t.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Inactive_customer_cannot_replay_checkout_or_place_a_new_order()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (c, p) = await t.SeedCatalogAsync();
        var key = Guid.NewGuid().ToString("N");
        Assert.NotNull((await Orders(t).CreateIdempotentAsync(c.UserId, [new(p.ProductId, 1)], Shipping, key)).OrderId);
        await t.Db.Users.Where(u => u.UserId == c.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Null((await Orders(t).CreateIdempotentAsync(c.UserId, [new(p.ProductId, 1)], Shipping, key)).OrderId);
        Assert.Single(await t.Db.Orders.ToListAsync());
        Assert.Equal(9, (await t.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Distinct_tokens_create_distinct_orders_and_replay_cancelled_order_does_not_reorder()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (c, p) = await t.SeedCatalogAsync();
        var key = Guid.NewGuid().ToString("N");
        var first = await Orders(t).CreateIdempotentAsync(c.UserId, [new(p.ProductId, 1)], Shipping, key);
        var second = await Orders(t).CreateIdempotentAsync(c.UserId, [new(p.ProductId, 1)], Shipping, Guid.NewGuid().ToString("N"));
        Assert.NotEqual(first.OrderId, second.OrderId);
        Assert.True((await Admin(t).CancelForCustomerAsync(first.OrderId!.Value, c.UserId)).Succeeded);
        Assert.Equal(first.OrderId, (await Orders(t).CreateIdempotentAsync(c.UserId, [new(p.ProductId, 1)], Shipping, key)).OrderId);
        Assert.Equal(2, await t.Db.Orders.CountAsync());
        Assert.Equal(9, (await t.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Shipping, false)]
    [InlineData(OrderStatus.Completed, false)]
    public async Task Customer_cancel_enforces_current_workflow_and_restores_once(OrderStatus state, bool allowed)
    {
        await using var t = await TestDatabase.CreateAsync();
        var (c, p) = await t.SeedCatalogAsync();
        var result = await Orders(t).CreateAsync(c.UserId, [new(p.ProductId, 2)], Shipping);
        var id = result.OrderId!.Value;
        await t.Db.Orders.Where(o => o.OrderId == id).ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, state));
        var cancel = await Admin(t).CancelForCustomerAsync(id, c.UserId);
        Assert.Equal(allowed, cancel.Succeeded);
        Assert.Equal(allowed ? 10 : 8, (await t.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        if (allowed)
        {
            Assert.True((await Admin(t).CancelForCustomerAsync(id, c.UserId)).Succeeded);
            Assert.Equal(10, (await t.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
            Assert.Equal(OrderStatus.Cancelled, (await Orders(t).GetReceiptAsync(id, c.UserId))!.Status);
        }
    }

    [Fact]
    public async Task Foreign_customer_or_inactive_owner_cannot_cancel_or_view_foreign_order()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (c, p) = await t.SeedCatalogAsync();
        var id = (await Orders(t).CreateAsync(c.UserId, [new(p.ProductId, 1)], Shipping)).OrderId!.Value;
        Assert.False((await Admin(t).CancelForCustomerAsync(id, c.UserId + 999)).Succeeded);
        Assert.Null(await Orders(t).GetReceiptAsync(id, c.UserId + 999));
        await t.Db.Users.Where(u => u.UserId == c.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.False((await Admin(t).CancelForCustomerAsync(id, c.UserId)).Succeeded);
        Assert.Equal(9, (await t.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Validation_failure_after_first_line_does_not_contaminate_next_checkout()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (c, p) = await t.SeedCatalogAsync();
        var other = new Product { ProductName = "Out of stock", BrandId = p.BrandId, CategoryId = p.CategoryId,
            Price = 1_000_000m, StockQuantity = 0 };
        t.Db.Products.Add(other); await t.Db.SaveChangesAsync();
        Assert.Null((await Orders(t).CreateAsync(c.UserId, [new(p.ProductId, 2), new(other.ProductId, 1)], Shipping)).OrderId);
        Assert.Empty(t.Db.ChangeTracker.Entries());
        Assert.Empty(await t.Db.Orders.ToListAsync());
        Assert.NotNull((await Orders(t).CreateAsync(c.UserId, [new(p.ProductId, 1)], Shipping)).OrderId);
        Assert.Equal(9, (await t.Db.Products.AsNoTracking().SingleAsync(x => x.ProductId == p.ProductId)).StockQuantity);
    }

    [Fact]
    public async Task Admin_filter_detail_and_customer_receipt_share_status_and_price_snapshot()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (c, p) = await t.SeedCatalogAsync();
        var id = (await Orders(t).CreateAsync(c.UserId, [new(p.ProductId, 1)], Shipping)).OrderId!.Value;
        Assert.True((await Admin(t).UpdateStatusAsync(id, OrderStatus.Pending, OrderStatus.Confirmed)).Succeeded);
        var rows = await Admin(t).ListAsync(new OrderManagementQuery(Search: "Receiver", Status: OrderStatus.Confirmed));
        Assert.Single(rows);
        Assert.Equal((await Admin(t).GetAsync(id))!.Status, (await Orders(t).GetReceiptAsync(id, c.UserId))!.Status);
        Assert.Single(await Orders(t).GetCustomerOrdersAsync(c.UserId));
        await t.Db.Products.Where(x => x.ProductId == p.ProductId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, 9_000_000m));
        Assert.Equal(1_000_000m, (await Orders(t).GetReceiptAsync(id, c.UserId))!.Items.Single().UnitPrice);
    }
}
