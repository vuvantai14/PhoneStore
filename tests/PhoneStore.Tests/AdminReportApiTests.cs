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
            using var denied = await anon.GetAsync(Root + route + Range);
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            Assert.True(denied.Headers.CacheControl?.NoStore);
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

        int commands = 0;
        var measuredOptions = new DbContextOptionsBuilder<PhoneStore.Data.Context.PhoneStoreDbContext>()
            .UseSqlite(fixture.Db.Database.GetDbConnection())
            .LogTo(_ => commands++, [Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted])
            .Options;
        await using var measuredDb = new PhoneStore.Data.Context.PhoneStoreDbContext(measuredOptions);
        using var host = ApiAuthorizationTests.Host(measuredDb);
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
        commands = 0;
        using var overviewResponse = await client.GetAsync(Root + "overview" + Range);
        overviewResponse.EnsureSuccessStatusCode();
        Assert.True(overviewResponse.Headers.CacheControl?.NoStore);
        var overview = await overviewResponse.Content.ReadFromJsonAsync<ReportOverview>();
        Assert.Equal(3, commands);;
        Assert.Equal(6, overview!.TotalOrders);
        Assert.Equal(revenue.TotalRevenue, overview.CompletedRevenue);
        Assert.Equal(10.5m, overview.AverageCompletedOrderValue);
        Assert.Equal(new CurrentInventory(2, 11, 0, 1), overview.CurrentInventory);
        Assert.Equal(new[] { 1, 1, 1, 2, 1 }, overview.OrderStatuses.Select(s => s.Count));
        commands = 0;
        var top = await client.GetFromJsonAsync<TopProductsReport>(Root + "top-products" + Range);
        Assert.Equal(2, commands);
        Assert.Collection(top!.Items,
            p => Assert.Equal((1, second.ProductName, 4, 28m), (p.Rank, p.ProductName, p.QuantitySold, p.SalesValue)),
            p => Assert.Equal((2, first.ProductName, 3, 21m), (p.Rank, p.ProductName, p.QuantitySold, p.SalesValue)));

        var winForms = new PhoneStore.Business.Reporting.ReportingService(new PhoneStore.Data.Reporting.ReportingStore(fixture.Db));
        var report = (await winForms.GetReportAsync(new(2026, 11, 2), new(2026, 11, 3))).Report!;
        Assert.Equal(report.CompletedRevenue, overview.CompletedRevenue);
        Assert.Equal(report.AverageCompletedOrderValue, overview.AverageCompletedOrderValue);
        Assert.Equal(report.OrderStatuses.Select(s => new StatusCount(s.Status.ToString(), s.Count)), overview.OrderStatuses);
        Assert.Equal(report.BestSellers.Select(p => new TopProduct(p.Rank, p.ProductName, p.BrandName, p.QuantitySold, p.SalesValue)), top.Items);

        const string empty = "?fromDate=2026-12-01&toDate=2026-12-01";
        Assert.Empty((await client.GetFromJsonAsync<TopProductsReport>(Root + "top-products" + empty))!.Items);
        Assert.Equal(0m, Assert.Single((await client.GetFromJsonAsync<RevenueReport>(Root + "revenue" + empty))!.Days).Revenue);
        Assert.Equal(0, (await client.GetFromJsonAsync<ReportOverview>(Root + "overview" + empty))!.TotalOrders);
    }

    [Fact]
    public async Task V1_dashboard_and_multi_period_revenue_match_reporting_service_and_exclude_cancelled()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, first) = await fixture.SeedCatalogAsync();
        first.StockQuantity = 3; // low stock (<= 5)
        var admin = await AddAdmin(fixture);
        // 2026-11-02 10:00 local (UTC+7) = 2026-11-02 03:00 UTC
        var orderTimeUtc = new DateTime(2026, 11, 2, 3, 0, 0, DateTimeKind.Utc);
        fixture.Db.Orders.AddRange(
            new Order
            {
                OrderCode = "ORD-COMP-1", User = customer, OrderDate = orderTimeUtc, Status = OrderStatus.Completed,
                ReceiverName = "Buyer", ReceiverPhone = "0900", ShippingAddress = "Addr", TotalAmount = 1500m,
                OrderDetails = [new OrderDetail { Product = first, Quantity = 1, UnitPrice = 1500m }]
            },
            new Order
            {
                OrderCode = "ORD-CANC-1", User = customer, OrderDate = orderTimeUtc.AddMinutes(5), Status = OrderStatus.Cancelled,
                ReceiverName = "Buyer", ReceiverPhone = "0900", ShippingAddress = "Addr", TotalAmount = 9999m,
                OrderDetails = [new OrderDetail { Product = first, Quantity = 5, UnitPrice = 1999.8m }]
            });
        await fixture.Db.SaveChangesAsync();

        // Authorization check on /api/v1/admin/dashboard
        using var anonHost = ApiAuthorizationTests.Host();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonHost.CreateClient().GetAsync("/api/v1/admin/dashboard")).StatusCode);

        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Fixture-Subject", customer.UserId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/dashboard")).StatusCode);

        client.DefaultRequestHeaders.Remove("Fixture-Subject");
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());

        // Dashboard matches IReportingService.GetDashboardAsync
        var dashboard = await client.GetFromJsonAsync<DashboardResponse>("/api/v1/admin/dashboard");
        var reportingService = new PhoneStore.Business.Reporting.ReportingService(new PhoneStore.Data.Reporting.ReportingStore(fixture.Db));
        var expectedDash = await reportingService.GetDashboardAsync();
        Assert.Equal(expectedDash.ActiveProducts, dashboard!.ActiveProducts);
        Assert.Equal(expectedDash.TotalStock, dashboard.TotalStock);
        Assert.Equal(expectedDash.Customers, dashboard.Customers);
        Assert.Equal(expectedDash.Orders, dashboard.Orders);
        Assert.Equal(expectedDash.CompletedOrders, dashboard.CompletedOrders);
        Assert.Equal(1500m, dashboard.CompletedRevenue); // Cancelled 9999m excluded
        Assert.Single(dashboard.LowStockProducts);
        Assert.Equal(2, dashboard.RecentOrders.Count);

        // Multi-period revenue: Day (24 buckets), Week (7 buckets), Month (30 buckets for Nov), Year (12 buckets)
        var day = await client.GetFromJsonAsync<RevenueReport>("/api/v1/admin/reports/revenue?period=Day&anchorDate=2026-11-02");
        Assert.Equal("Day", day!.Period);
        Assert.Equal(24, day.Buckets!.Count);
        Assert.Equal(1500m, day.TotalRevenue);
        Assert.Equal(1, day.CompletedOrders);

        var week = await client.GetFromJsonAsync<RevenueReport>("/api/v1/admin/reports/revenue?period=Week&anchorDate=2026-11-02");
        Assert.Equal("Week", week!.Period);
        Assert.Equal(7, week.Buckets!.Count);
        Assert.Equal(1500m, week.TotalRevenue);

        var month = await client.GetFromJsonAsync<RevenueReport>("/api/v1/admin/reports/revenue?period=Month&anchorDate=2026-11-02");
        Assert.Equal("Month", month!.Period);
        Assert.Equal(30, month.Buckets!.Count);
        Assert.Equal(1500m, month.TotalRevenue);

        var year = await client.GetFromJsonAsync<RevenueReport>("/api/v1/admin/reports/revenue?period=Year&anchorDate=2026-11-02");
        Assert.Equal("Year", year!.Period);
        Assert.Equal(12, year.Buckets!.Count);
        Assert.Equal(1500m, year.TotalRevenue);

        // Overview with includeAlerts=true includes InventoryAlerts & RevenueTrend
        var fullOverview = await client.GetFromJsonAsync<ReportOverview>(
            "/api/v1/admin/reports/overview?fromDate=2026-11-02&toDate=2026-11-02&includeAlerts=true");
        Assert.NotNull(fullOverview!.InventoryAlerts);
        Assert.Single(fullOverview.InventoryAlerts!);
        Assert.NotNull(fullOverview.RevenueTrend);
        Assert.Single(fullOverview.RevenueTrend!);
    }

    private static async Task<User> AddAdmin(TestDatabase fixture)
    {
        var admin = new User { RoleId = 1, Email = "reports@example.test", FullName = "Fixture", PasswordHash = "unused", IsActive = true };
        fixture.Db.Add(admin);
        await fixture.Db.SaveChangesAsync();
        return admin;
    }
}
