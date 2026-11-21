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

    [Fact]
    public async Task Customer_can_create_idempotent_order_and_cancel_with_ownership_and_inventory_safety()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, product) = await fixture.SeedCatalogAsync();
        product.StockQuantity = 5;
        product.Price = 1000000m;
        var inactiveProduct = new Product
        {
            ProductName = "Retired Phone", BrandId = product.BrandId, CategoryId = product.CategoryId,
            Price = 500000m, StockQuantity = 10, IsActive = false, CreatedAt = DateTime.UtcNow
        };
        var otherCustomer = Account(2, "other-buyer");
        var admin = Account(1, "order-admin");
        fixture.Db.AddRange(inactiveProduct, otherCustomer, admin);
        await fixture.Db.SaveChangesAsync();

        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        Identify(client, customer.UserId);

        var orderPayload = new CreateOrderRequest
        {
            ReceiverName = "Nguyen Van A",
            ReceiverPhone = "0901234567",
            ShippingAddress = "123 Le Loi, Q1",
            Note = "Giao gio hanh chinh",
            Items = [new CreateOrderItemRequest { ProductId = product.ProductId, Quantity = 2 }]
        };

        // 1. Missing Idempotency-Key -> 400
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/orders", orderPayload)).StatusCode);

        // 2. Arbitrary userId injection in body rejected -> 400
        using (var forgedReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders"))
        {
            forgedReq.Headers.Add("Idempotency-Key", "key-forged-user");
            forgedReq.Content = new StringContent(
                $$"""{"userId":{{otherCustomer.UserId}},"receiverName":"A","receiverPhone":"0901234567","shippingAddress":"Addr","items":[{"productId":{{product.ProductId}},"quantity":1}]}""",
                System.Text.Encoding.UTF8, "application/json");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(forgedReq)).StatusCode);
        }

        // 3. Inactive product -> 400; out-of-stock -> 409
        Assert.Equal(HttpStatusCode.BadRequest, (await PostOrder(client, orderPayload with
        {
            Items = [new CreateOrderItemRequest { ProductId = inactiveProduct.ProductId, Quantity = 1 }]
        }, "key-inactive-product")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostOrder(client, orderPayload with
        {
            Items = [new CreateOrderItemRequest { ProductId = product.ProductId, Quantity = 99 }]
        }, "key-oversold")).StatusCode);

        // 4. Valid idempotent create -> 201, deducts stock 5 -> 3
        using var createResp = await PostOrder(client, orderPayload, "order-idem-key-001");
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = (await createResp.Content.ReadFromJsonAsync<OrderCreatedResponse>())!;
        Assert.False(created.Replayed);
        Assert.Equal(2000000m, created.TotalAmount);
        Assert.Equal("Pending", created.Status);
        Assert.Equal(3, (await fixture.Db.Products.AsNoTracking().SingleAsync(p => p.ProductId == product.ProductId)).StockQuantity);

        // 5. Idempotent replay with same Idempotency-Key -> 200, same OrderId, NO duplicate order or extra stock deduction
        using var replayResp = await PostOrder(client, orderPayload, "order-idem-key-001");
        Assert.Equal(HttpStatusCode.OK, replayResp.StatusCode);
        var replayed = (await replayResp.Content.ReadFromJsonAsync<OrderCreatedResponse>())!;
        Assert.True(replayed.Replayed);
        Assert.Equal(created.OrderId, replayed.OrderId);
        Assert.Equal(1, await fixture.Db.Orders.CountAsync());
        Assert.Equal(3, (await fixture.Db.Products.AsNoTracking().SingleAsync(p => p.ProductId == product.ProductId)).StockQuantity);

        // 6. Admin order detail includes CustomerName, CustomerEmail, ReceiverPhone, StatusLabel, NextStatuses
        Identify(client, admin.UserId);
        var adminList = await client.GetFromJsonAsync<OrderPage>("/api/v1/admin/orders");
        var listed = Assert.Single(adminList!.Items);
        Assert.Equal(customer.FullName, listed.CustomerName);
        Assert.Equal(customer.Email, listed.CustomerEmail);
        Assert.Equal("0901234567", listed.ReceiverPhone);
        Assert.False(string.IsNullOrWhiteSpace(listed.StatusLabel));

        var adminDetail = await client.GetFromJsonAsync<OrderDetailResponse>($"/api/v1/admin/orders/{created.OrderId}");
        Assert.Equal(customer.FullName, adminDetail!.CustomerName);
        Assert.Equal(customer.Email, adminDetail.CustomerEmail);
        Assert.Equal(new[] { "Confirmed", "Cancelled" }, adminDetail.NextStatuses);

        // Admin cannot call customer cancel -> 403
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/v1/orders/{created.OrderId}/cancel", null)).StatusCode);

        // 7. Other customer cannot cancel -> 404, stock untouched
        Identify(client, otherCustomer.UserId);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/v1/orders/{created.OrderId}/cancel", null)).StatusCode);
        Assert.Equal(3, (await fixture.Db.Products.AsNoTracking().SingleAsync(p => p.ProductId == product.ProductId)).StockQuantity);

        // 8. Owner cancels Pending order -> 204, stock restored 3 -> 5; replay cancel -> 204, stock stays 5
        Identify(client, customer.UserId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/orders/{created.OrderId}/cancel", null)).StatusCode);
        Assert.Equal(5, (await fixture.Db.Products.AsNoTracking().SingleAsync(p => p.ProductId == product.ProductId)).StockQuantity);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/orders/{created.OrderId}/cancel", null)).StatusCode);
        Assert.Equal(5, (await fixture.Db.Products.AsNoTracking().SingleAsync(p => p.ProductId == product.ProductId)).StockQuantity);

        // 9. Shipping order cannot be cancelled by customer -> 409 Conflict
        using var order2Resp = await PostOrder(client, orderPayload with
        {
            Items = [new CreateOrderItemRequest { ProductId = product.ProductId, Quantity = 1 }]
        }, "order-idem-key-002");
        Assert.Equal(HttpStatusCode.Created, order2Resp.StatusCode);
        var order2Id = (await order2Resp.Content.ReadFromJsonAsync<OrderCreatedResponse>())!.OrderId;
        Identify(client, admin.UserId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync($"/api/v1/admin/orders/{order2Id}/status", new { expectedStatus = "Pending", targetStatus = "Confirmed" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync($"/api/v1/admin/orders/{order2Id}/status", new { expectedStatus = "Confirmed", targetStatus = "Shipping" })).StatusCode);
        var shippingDetail = await client.GetFromJsonAsync<OrderDetailResponse>($"/api/v1/admin/orders/{order2Id}");
        Assert.Equal(new[] { "Completed" }, shippingDetail!.NextStatuses);

        Identify(client, customer.UserId);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/v1/orders/{order2Id}/cancel", null)).StatusCode);
        Assert.Equal(4, (await fixture.Db.Products.AsNoTracking().SingleAsync(p => p.ProductId == product.ProductId)).StockQuantity);

        // 10. Inactive customer cannot create orders -> 403
        await fixture.Db.Users.Where(u => u.UserId == customer.UserId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        Assert.Equal(HttpStatusCode.Forbidden, (await PostOrder(client, orderPayload, "key-inactive-customer")).StatusCode);
    }

    private static async Task<HttpResponseMessage> PostOrder(HttpClient client, CreateOrderRequest payload, string idempotencyKey)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(payload)
        };
        req.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(req);
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
