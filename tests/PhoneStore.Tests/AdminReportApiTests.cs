using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Api.Contracts.Reports;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;

namespace PhoneStore.Tests;

public sealed class AdminReportApiTests
{
    private const string Root = "/api/admin/reports/";
    private const string Range = "?fromDate=2026-11-02&toDate=2026-11-03";
    private static readonly string[] Routes = ["overview", "revenue", "top-products"];

    [Fact]
    public async Task Every_report_requires_an_active_database_admin()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, _) = await fixture.SeedCatalogAsync();
        var admin = await AddAdmin(fixture);
        await fixture.Db.Users.Where(u => u.UserId == admin.UserId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        using var anonHost = ApiAuthorizationTests.Host();
        using var anon = anonHost.CreateClient();
        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        foreach (var route in Routes)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(Root + route + Range)).StatusCode);
            foreach (var user in new[] { customer, admin })
            {
                client.DefaultRequestHeaders.Remove("Fixture-Subject");
                client.DefaultRequestHeaders.Add("Fixture-Subject", user.UserId.ToString());
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Root + route + Range)).StatusCode);
            }
        }
    }

    [Fact]
    public async Task Report_endpoints_share_local_day_boundaries_and_keep_distinct_sales_rules()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, first) = await fixture.SeedCatalogAsync();
        var admin = await AddAdmin(fixture);
        var second = new Product { ProductName = "Second", BrandId = first.BrandId, CategoryId = first.CategoryId,
            Price = 100, IsActive = true, StockQuantity = 1 };
        var startUtc = new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc);
        void Add(string code, DateTime date, OrderStatus status, Product product, int qty)
        {
            fixture.Db.Orders.Add(new Order { OrderCode = code, User = customer, OrderDate = date, Status = status,
                ReceiverName = "Fixture", ReceiverPhone = "000", ShippingAddress = "Fixture", TotalAmount = qty * 7m,
                OrderDetails = [new OrderDetail { Product = product, Quantity = qty, UnitPrice = 7m }] });
        }
        Add("before", startUtc.AddSeconds(-1), OrderStatus.Completed, first, 10);
        Add("start", startUtc, OrderStatus.Completed, first, 2);
        Add("confirmed", startUtc.AddSeconds(1), OrderStatus.Confirmed, second, 3);
        Add("shipping", startUtc.AddMinutes(1), OrderStatus.Shipping, second, 1);
        Add("pending", startUtc, OrderStatus.Pending, first, 20);
        Add("cancelled", startUtc, OrderStatus.Cancelled, second, 30);
        Add("last", startUtc.AddDays(2).AddSeconds(-1), OrderStatus.Completed, first, 1);
        Add("end", startUtc.AddDays(2), OrderStatus.Completed, second, 10);
        await fixture.Db.SaveChangesAsync();

        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());
        foreach (var invalid in new[] { "", "?fromDate=2026-11-02", "?fromDate=x&toDate=2026-11-02",
            "?fromDate=2026-11-03&toDate=2026-11-02", "?fromDate=2024-01-01&toDate=2025-01-01" })
        {
            using var response = await client.GetAsync(Root + "overview" + invalid);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
        var revenue = await client.GetFromJsonAsync<RevenueReport>(Root + "revenue" + Range);
        Assert.Equal(21m, revenue!.TotalRevenue);
        Assert.Equal(2, revenue.CompletedOrders);
        Assert.Equal(new decimal[] { 14, 7 }, revenue.Days.Select(d => d.Revenue));
        var overview = await client.GetFromJsonAsync<ReportOverview>(Root + "overview" + Range);
        Assert.Equal(6, overview!.TotalOrders);
        Assert.Equal(revenue.TotalRevenue, overview.CompletedRevenue);
        Assert.Equal(10.5m, overview.AverageCompletedOrderValue);
        var top = await client.GetFromJsonAsync<TopProductsReport>(Root + "top-products" + Range);
        Assert.Collection(top!.Items,
            p => Assert.Equal((1, second.ProductName, 4, 28m), (p.Rank, p.ProductName, p.QuantitySold, p.SalesValue)),
            p => Assert.Equal((2, first.ProductName, 3, 21m), (p.Rank, p.ProductName, p.QuantitySold, p.SalesValue)));

        const string empty = "?fromDate=2026-12-01&toDate=2026-12-01";
        Assert.Empty((await client.GetFromJsonAsync<TopProductsReport>(Root + "top-products" + empty))!.Items);
        Assert.Equal(0m, Assert.Single((await client.GetFromJsonAsync<RevenueReport>(Root + "revenue" + empty))!.Days).Revenue);
        Assert.Equal(0, (await client.GetFromJsonAsync<ReportOverview>(Root + "overview" + empty))!.TotalOrders);
    }

    private static async Task<User> AddAdmin(TestDatabase fixture)
    {
        var admin = new User { RoleId = 1, Email = "reports@example.test", FullName = "Fixture", PasswordHash = "unused", IsActive = true };
        fixture.Db.Add(admin);
        await fixture.Db.SaveChangesAsync();
        return admin;
    }
}
