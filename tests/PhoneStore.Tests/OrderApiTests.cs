using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhoneStore.Api.Contracts.Orders;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;

namespace PhoneStore.Tests;

public sealed class OrderApiTests
{
    [Fact]
    public async Task Orders_enforce_roles_ownership_filtering_and_paging()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, product) = await fixture.SeedCatalogAsync();
        var other = Account(2, "other");
        var admin = Account(1, "admin");
        fixture.Db.AddRange(other, admin);
        var first = OrderFor(customer, product, "A1");
        var second = OrderFor(customer, product, "A2");
        second.Status = OrderStatus.Completed;
        var hidden = OrderFor(other, product, "B1");
        fixture.Db.AddRange(first, second, hidden);
        await fixture.Db.SaveChangesAsync();

        using var anonHost = ApiAuthorizationTests.Host();
        using var anonymous = anonHost.CreateClient();
        foreach (var path in new[] { "/api/orders", "/api/orders/1", "/api/admin/orders", "/api/admin/orders/1" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Patch(anonymous, first.OrderId, "Pending", "Confirmed")).StatusCode);

        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        Identify(client, customer.UserId);
        var page = await client.GetFromJsonAsync<OrderPage>($"/api/orders?userId={other.UserId}&page=2&pageSize=1");
        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(first.OrderId, Assert.Single(page.Items).OrderId);
        var filtered = await client.GetFromJsonAsync<OrderPage>("/api/orders?status=Completed");
        Assert.Equal(second.OrderId, Assert.Single(filtered!.Items).OrderId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/orders?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{hidden.OrderId}?userId={other.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/orders/9999")).StatusCode);
        var receipt = await client.GetFromJsonAsync<OrderDetailResponse>($"/api/orders/{first.OrderId}");
        Assert.Equal(14m, receipt!.Order.TotalAmount);
        Assert.Equal(14m, Assert.Single(receipt.Items).LineTotal);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Patch(client, first.OrderId, "Pending", "Cancelled")).StatusCode);

        Identify(client, admin.UserId);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/orders")).StatusCode);
        page = await client.GetFromJsonAsync<OrderPage>("/api/admin/orders?search=A1&status=Pending&pageSize=1");
        Assert.Equal(1, page!.TotalCount);
        Assert.Equal(first.OrderId, Assert.Single(page.Items).OrderId);
        page = await client.GetFromJsonAsync<OrderPage>("/api/admin/orders?page=3&pageSize=1");
        Assert.Equal(hidden.OrderId, Assert.Single(page!.Items).OrderId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/admin/orders/{hidden.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Patch(client, 9999, "Pending", "Cancelled")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Patch(client, first.OrderId, "Unknown", "Confirmed")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Patch(client, first.OrderId, "Pending", "Completed")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Patch(client, first.OrderId, "Pending", "Confirmed")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Patch(client, first.OrderId, "Pending", "Cancelled")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Patch(client, first.OrderId, "Confirmed", "Shipping")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Patch(client, first.OrderId, "Shipping", "Completed")).StatusCode);
        Assert.Equal(10, (await fixture.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);

        await fixture.Db.Users.Where(u => u.UserId == admin.UserId || u.UserId == customer.UserId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
        Assert.Equal(HttpStatusCode.Forbidden, (await Patch(client, hidden.OrderId, "Pending", "Cancelled")).StatusCode);
        Identify(client, customer.UserId);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/orders")).StatusCode);
    }

    [Fact]
    public async Task Two_admin_requests_with_same_expected_status_restore_stock_once()
    {
        // Each HTTP request needs its own connection and DbContext. The keeper owns the memory database.
        var connectionString = $"Data Source=order-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
        using var keeper = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        await keeper.OpenAsync();
        keeper.CreateFunction("SYSUTCDATETIME", () => DateTime.UtcNow);
        var options = new DbContextOptionsBuilder<PhoneStoreDbContext>().UseSqlite(keeper).Options;
        await using var db = new PhoneStoreDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var customer = Account(2, "buyer");
        var admin = Account(1, "operator");
        var product = new Product { ProductName = "Fixture", Price = 7, StockQuantity = 3,
            Brand = new Brand { BrandName = "B" }, Category = new Category { CategoryName = "C" } };
        var order = OrderFor(customer, product, "C1");
        db.AddRange(order, admin);
        await db.SaveChangesAsync();

        using var authHost = ApiAuthorizationTests.Host(db);
        using var host = authHost.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<PhoneStoreDbContext>();
            services.AddScoped(_ => new PhoneStoreDbContext(new DbContextOptionsBuilder<PhoneStoreDbContext>()
                .UseSqlite(connectionString).Options));
        }));
        using var client = host.CreateClient();
        Identify(client, admin.UserId);
        var replies = await Task.WhenAll(Patch(client, order.OrderId, "Pending", "Cancelled"),
            Patch(client, order.OrderId, "Pending", "Cancelled"));
        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }, replies.Select(r => r.StatusCode).Order());
        Assert.Equal(5, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.AsNoTracking().SingleAsync()).Status);
    }

    private static Task<HttpResponseMessage> Patch(HttpClient client, int id, string expectedStatus, string targetStatus) =>
        client.PatchAsJsonAsync($"/api/admin/orders/{id}/status", new { expectedStatus, targetStatus });

    private static void Identify(HttpClient client, int id)
    {
        client.DefaultRequestHeaders.Remove("Fixture-Subject");
        client.DefaultRequestHeaders.Add("Fixture-Subject", id.ToString());
    }

    private static User Account(int role, string name) => new() { RoleId = role, Email = name + "@example.test",
        FullName = name, PasswordHash = "unused", IsActive = true };

    private static Order OrderFor(User owner, Product product, string code) => new()
    {
        User = owner, OrderCode = code, ReceiverName = "Fixture", ReceiverPhone = "000", ShippingAddress = "Fixture",
        OrderDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), TotalAmount = 14,
        OrderDetails = [new OrderDetail { Product = product, Quantity = 2, UnitPrice = 7 }]
    };
}
