using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Management;
using PhoneStore.Data.Management;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;

namespace PhoneStore.Tests;

public sealed class OrderWorkflowTests
{
    public static TheoryData<OrderStatus, OrderStatus> ValidTransitions => new()
    {
        { OrderStatus.Pending, OrderStatus.Confirmed },
        { OrderStatus.Pending, OrderStatus.Cancelled },
        { OrderStatus.Confirmed, OrderStatus.Shipping },
        { OrderStatus.Confirmed, OrderStatus.Cancelled },
        { OrderStatus.Shipping, OrderStatus.Completed }
    };

    public static TheoryData<OrderStatus, OrderStatus> InvalidTransitions => new()
    {
        { OrderStatus.Pending, OrderStatus.Shipping },
        { OrderStatus.Pending, OrderStatus.Completed },
        { OrderStatus.Shipping, OrderStatus.Confirmed },
        { OrderStatus.Shipping, OrderStatus.Cancelled },
        { OrderStatus.Completed, OrderStatus.Pending },
        { OrderStatus.Completed, OrderStatus.Cancelled },
        { OrderStatus.Cancelled, OrderStatus.Confirmed }
    };

    [Theory, MemberData(nameof(ValidTransitions))]
    public async Task Valid_transition_succeeds(OrderStatus from, OrderStatus to)
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        var order = await AddOrder(test, product, from, 2);
        var result = await Service(test).UpdateStatusAsync(order.OrderId, from, to);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(to, (await test.Db.Orders.FindAsync(order.OrderId))!.Status);
    }

    [Theory, MemberData(nameof(InvalidTransitions))]
    public async Task Invalid_transition_is_rejected(OrderStatus from, OrderStatus to)
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        var order = await AddOrder(test, product, from, 2);
        var stock = product.StockQuantity;
        var result = await Service(test).UpdateStatusAsync(order.OrderId, from, to);
        Assert.False(result.Succeeded);
        Assert.Equal(from, (await test.Db.Orders.AsNoTracking().SingleAsync(x => x.OrderId == order.OrderId)).Status);
        Assert.Equal(stock, (await test.Db.Products.AsNoTracking().SingleAsync(x => x.ProductId == product.ProductId)).StockQuantity);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    public async Task Cancellation_restores_each_detail_once(OrderStatus from)
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync(stock: 7);
        var order = await AddOrder(test, product, from, 3);
        var result = await Service(test).UpdateStatusAsync(order.OrderId, from, OrderStatus.Cancelled);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(10, (await test.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        var second = await Service(test).UpdateStatusAsync(order.OrderId, OrderStatus.Cancelled, OrderStatus.Cancelled);
        Assert.False(second.Succeeded);
        Assert.Equal(10, (await test.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Stale_expected_status_is_rejected()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        var order = await AddOrder(test, product, OrderStatus.Confirmed, 1);
        var result = await Service(test).UpdateStatusAsync(order.OrderId, OrderStatus.Pending, OrderStatus.Cancelled);
        Assert.False(result.Succeeded);
        Assert.Equal(OrderStatus.Confirmed, (await test.Db.Orders.AsNoTracking().SingleAsync()).Status);
    }

    private static OrderManagementService Service(TestDatabase test) => new(new OrderManagementStore(test.Db));

    private static async Task<Order> AddOrder(TestDatabase test, Product product, OrderStatus status, int quantity)
    {
        var customer = await test.Db.Users.SingleAsync();
        var order = new Order
        {
            UserId = customer.UserId, OrderCode = Guid.NewGuid().ToString("N"), OrderDate = DateTime.UtcNow,
            ReceiverName = customer.FullName, ReceiverPhone = "0900000000", ShippingAddress = "Địa chỉ kiểm thử",
            Status = status, TotalAmount = product.Price * quantity,
            OrderDetails = [new OrderDetail { ProductId = product.ProductId, Quantity = quantity, UnitPrice = product.Price }]
        };
        test.Db.Orders.Add(order);
        await test.Db.SaveChangesAsync();
        test.Db.ChangeTracker.Clear();
        return order;
    }
}
