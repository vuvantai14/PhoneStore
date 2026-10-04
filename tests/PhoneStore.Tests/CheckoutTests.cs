using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Purchasing;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Tests;

public sealed class CheckoutTests
{
    [Fact]
    public async Task Checkout_uses_server_price_and_decrements_stock()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (customer, product) = await test.SeedCatalogAsync(stock: 8, price: 2_500_000m);
        var result = await Service(test).CreateAsync(customer.UserId, [new CartItem(product.ProductId, 3)], Shipping());
        Assert.NotNull(result.OrderId);
        Assert.Null(result.Error);
        var order = await test.Db.Orders.Include(x => x.OrderDetails).SingleAsync();
        Assert.Equal(7_500_000m, order.TotalAmount);
        Assert.Equal(2_500_000m, order.OrderDetails.Single().UnitPrice);
        Assert.Equal(5, (await test.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Nonpositive_quantity_is_rejected_without_writes(int quantity)
    {
        await using var test = await TestDatabase.CreateAsync();
        var (customer, product) = await test.SeedCatalogAsync(stock: 8);
        var result = await Service(test).CreateAsync(customer.UserId, [new CartItem(product.ProductId, quantity)], Shipping());
        Assert.Null(result.OrderId);
        Assert.NotNull(result.Error);
        Assert.Empty(test.Db.Orders);
        Assert.Equal(8, (await test.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Insufficient_stock_rolls_back_order_and_stock()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (customer, product) = await test.SeedCatalogAsync(stock: 2);
        var result = await Service(test).CreateAsync(customer.UserId, [new CartItem(product.ProductId, 3)], Shipping());
        Assert.Null(result.OrderId);
        Assert.NotNull(result.Error);
        Assert.Empty(test.Db.Orders);
        Assert.Equal(2, (await test.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Duplicate_product_lines_are_rejected()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (customer, product) = await test.SeedCatalogAsync();
        var result = await Service(test).CreateAsync(customer.UserId,
            [new CartItem(product.ProductId, 1), new CartItem(product.ProductId, 1)], Shipping());
        Assert.Null(result.OrderId);
        Assert.NotNull(result.Error);
        Assert.Empty(test.Db.Orders);
    }

    [Fact]
    public async Task Receipt_and_history_enforce_customer_ownership()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (customer, product) = await test.SeedCatalogAsync();
        var service = Service(test);
        var result = await service.CreateAsync(customer.UserId, [new CartItem(product.ProductId, 1)], Shipping());
        Assert.NotNull(await service.GetReceiptAsync(result.OrderId!.Value, customer.UserId));
        Assert.Null(await service.GetReceiptAsync(result.OrderId.Value, customer.UserId + 999));
        Assert.Single(await service.GetCustomerOrdersAsync(customer.UserId));
        Assert.Empty(await service.GetCustomerOrdersAsync(customer.UserId + 999));
    }

    private static OrderService Service(TestDatabase test) => new(new PurchaseStore(test.Db));
    private static ShippingInfo Shipping() => new("Người nhận", "0900000000", "Địa chỉ nhận hàng", null);
}
