using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PhoneStore.Business.AdminApi;
using PhoneStore.Business.Management;
using PhoneStore.Data.Context;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Tests;

public sealed class AdminRestApiIntegrationTests
{
    private const string Issuer = "https://auth.phonestore.test";
    private const string Audience = "phonestore-api";
    private const string SigningKey = "S35-4E-Admin-Rest-Api-Integration-Signing-Key-64-Bytes-Strong!!";

    private static readonly byte[] ValidPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAQSURBVBhXY/jPAEIUAAYGAILBAf8lQkk6AAAAAElFTkSuQmCC");

    [Fact]
    public void Data_access_options_default_to_direct_sql_and_fail_closed_on_invalid_config()
    {
        var defaults = AdminDataAccessOptions.Parse(null, null, null);
        Assert.Equal(AdminDataAccessMode.DirectSql, defaults.Mode);
        Assert.Equal(15, defaults.TimeoutSeconds);

        var rest = AdminDataAccessOptions.Parse("RestApi", "https://api.phonestore.local:5025", "20");
        Assert.Equal(AdminDataAccessMode.RestApi, rest.Mode);
        Assert.Equal("https://api.phonestore.local:5025/", rest.GetNormalizedBaseUri().ToString());

        Assert.Throws<InvalidOperationException>(() => AdminDataAccessOptions.Parse("UnknownMode", null, null));
        Assert.Throws<InvalidOperationException>(() => AdminDataAccessOptions.Parse("RestApi", "ftp://bad.host", "15"));
        Assert.Throws<InvalidOperationException>(() => AdminDataAccessOptions.Parse("RestApi", "https://user:pass@bad.host", "15"));
        Assert.Throws<InvalidOperationException>(() => AdminDataAccessOptions.Parse("RestApi", "https://good.host?x=1", "15"));
        Assert.Throws<InvalidOperationException>(() => AdminDataAccessOptions.Parse("RestApi", "https://good.host", "0"));
        Assert.Throws<InvalidOperationException>(() => AdminDataAccessOptions.Parse("RestApi", "https://good.host", "999"));
    }

    [Fact]
    public async Task RestApi_mode_executes_all_seven_admin_modules_jwt_refresh_and_logout_end_to_end()
    {
        var tempUploads = Path.Combine(Path.GetTempPath(), "PhoneStore-S354E-Uploads-" + Guid.NewGuid().ToString("N"));
        var tempSourceDir = Path.Combine(Path.GetTempPath(), "PhoneStore-S354E-Src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempSourceDir);
        var sourcePng = Path.Combine(tempSourceDir, "sample.png");
        await File.WriteAllBytesAsync(sourcePng, ValidPngBytes);

        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 11, 21, 6, 0, 0, TimeSpan.Zero));
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, product, admin, order) = await SeedFullAdminScenarioAsync(fixture, clock.GetUtcNow().UtcDateTime);

        using var factory = CreateApiHost(fixture.Db, tempUploads, clock);
        using var httpClient = factory.CreateClient();
        using var sessionStore = new AdminApiSessionStore();
        var apiClient = new AdminApiClient(httpClient, sessionStore, clock);

        var auth = new RestAdminAuthService(apiClient);
        var lookups = new RestLookupManagementService(apiClient);
        var products = new RestProductManagementService(apiClient);
        var customers = new RestCustomerManagementService(apiClient);
        var orders = new RestOrderManagementService(apiClient);
        var reporting = new RestReportingService(apiClient);

        try
        {
            // 1. Auth: wrong password -> InvalidCredentials; Customer -> Forbidden; Admin -> Success
            Assert.Equal(AdminLoginStatus.InvalidCredentials, (await auth.LoginAsync(admin.Email, "Wrong!Pass")).Status);
            Assert.Equal(AdminLoginStatus.Forbidden, (await auth.LoginAsync(customer.Email, "CustPass!2026")).Status);
            Assert.Null(sessionStore.Current);

            var login = await auth.LoginAsync(admin.Email, "AdminPass!2026");
            Assert.Equal(AdminLoginStatus.Success, login.Status);
            Assert.Equal(admin.UserId, login.Identity!.UserId);
            Assert.True(await auth.IsCurrentAdminAsync(admin.UserId));
            var initialAccessToken = sessionStore.Current!.AccessToken;
            var initialRefreshToken = sessionStore.Current!.RefreshToken;

            // 2. Dashboard & Reports (Day, Week, Month, Year, CustomRange + Full Report)
            var dashboard = await reporting.GetDashboardAsync();
            Assert.Equal(1, dashboard.ActiveProducts);
            Assert.Equal(1, dashboard.Orders);
            Assert.Single(dashboard.RecentOrders);

            var reportRes = await reporting.GetReportAsync(new DateOnly(2026, 11, 21), new DateOnly(2026, 11, 21));
            Assert.True(reportRes.Succeeded);
            Assert.Equal(1, reportRes.Report!.TotalOrders);

            Assert.Equal(24, (await reporting.GetRevenueSeriesAsync(RevenueChartPeriod.Day, new DateOnly(2026, 11, 21))).Buckets.Count);
            Assert.Equal(7, (await reporting.GetRevenueSeriesAsync(RevenueChartPeriod.Week, new DateOnly(2026, 11, 21))).Buckets.Count);
            Assert.Equal(30, (await reporting.GetRevenueSeriesAsync(RevenueChartPeriod.Month, new DateOnly(2026, 11, 21))).Buckets.Count);
            Assert.Equal(12, (await reporting.GetRevenueSeriesAsync(RevenueChartPeriod.Year, new DateOnly(2026, 11, 21))).Buckets.Count);
            Assert.Equal(2, (await reporting.GetRevenueSeriesAsync(new DateOnly(2026, 11, 20), new DateOnly(2026, 11, 21))).Buckets.Count);

            // 3. Proactive automatic JWT refresh when access token nears expiration
            clock.Advance(TimeSpan.FromMinutes(14).Add(TimeSpan.FromSeconds(45)));
            var brandsBefore = await lookups.ListAsync(LookupKind.Brand, null);
            Assert.NotEmpty(brandsBefore);
            Assert.NotEqual(initialAccessToken, sessionStore.Current!.AccessToken);
            Assert.NotEqual(initialRefreshToken, sessionStore.Current!.RefreshToken);

            // 4. Brands & Categories CRUD
            var addBrand = await lookups.SaveAsync(LookupKind.Brand, null, new LookupEdit("Xiaomi", "Mi", true));
            Assert.True(addBrand.Succeeded);
            Assert.Equal(ManagementFailure.Conflict, (await lookups.SaveAsync(LookupKind.Brand, null, new LookupEdit("xiaomi", null, true))).Failure);
            Assert.True((await lookups.SaveAsync(LookupKind.Brand, addBrand.Id, new LookupEdit("Xiaomi Pro", "Updated", true))).Succeeded);
            Assert.True((await lookups.SetActiveAsync(LookupKind.Brand, addBrand.Id!.Value, false)).Succeeded);

            var addCat = await lookups.SaveAsync(LookupKind.Category, null, new LookupEdit("Gaming", "Gaming phones", true));
            Assert.True(addCat.Succeeded);
            Assert.True((await lookups.SetActiveAsync(LookupKind.Category, addCat.Id!.Value, true)).Succeeded);

            // 5. Products CRUD + Options + Optimistic Stock Concurrency
            var filterOpts = await products.GetFilterOptionsAsync();
            Assert.Contains(filterOpts.Brands, b => b.Id == addBrand.Id && !b.IsActive);
            var editorOpts = await products.GetOptionsAsync();
            Assert.DoesNotContain(editorOpts.Brands, b => b.Id == addBrand.Id);

            var createdProd = await products.SaveAsync(null, new ProductEdit
            {
                ProductName = "REST Created Phone",
                BrandId = product.BrandId,
                CategoryId = addCat.Id!.Value,
                Price = 15990000m,
                StockQuantity = 8,
                RAM = "12 GB",
                Storage = "256 GB",
                IsActive = true
            });
            Assert.True(createdProd.Succeeded);
            var loadedEdit = await products.GetEditAsync(createdProd.Id!.Value);
            Assert.Equal(8, loadedEdit!.OriginalStockQuantity);

            loadedEdit.StockQuantity = 6;
            Assert.True((await products.SaveAsync(createdProd.Id, loadedEdit)).Succeeded);
            // Stale OriginalStockQuantity (still 8 instead of 6) -> Conflict
            var staleSave = await products.SaveAsync(createdProd.Id, loadedEdit);
            Assert.False(staleSave.Succeeded);
            Assert.Equal(ManagementFailure.Conflict, staleSave.Failure);
            Assert.True((await products.SetActiveAsync(createdProd.Id.Value, false)).Succeeded);
            Assert.Single(await products.ListAsync(new ProductManagementQuery(Search: "REST Created", IsActive: false)));

            // 6. Product Images via multipart upload (Admin never writes directly to server uploads dir)
            var uploadRes = await products.AddImageAsync(createdProd.Id.Value, sourcePng);
            Assert.True(uploadRes.Succeeded);
            var images = await products.GetImagesAsync(createdProd.Id.Value);
            var uploadedImg = Assert.Single(images);
            Assert.True(uploadedImg.IsPrimary);
            Assert.Matches("^/uploads/products/[0-9a-f]{32}\\.png$", uploadedImg.ImageUrl);
            Assert.True(File.Exists(Path.Combine(tempUploads, Path.GetFileName(uploadedImg.ImageUrl))));

            Assert.True((await products.SetImageOrderAsync(createdProd.Id.Value, uploadedImg.ImageId, 3)).Succeeded);
            Assert.True((await products.RemoveImageAsync(createdProd.Id.Value, uploadedImg.ImageId)).Succeeded);
            Assert.Empty(await products.GetImagesAsync(createdProd.Id.Value));

            // 7. Customer Management
            var listedCustomers = await customers.ListAsync(new CustomerManagementQuery(null, null));
            Assert.Single(listedCustomers);
            var fetchedCustomer = await customers.GetAsync(customer.UserId);
            Assert.Equal(customer.Email, fetchedCustomer!.Email);
            Assert.True((await customers.UpdateAsync(customer.UserId, new CustomerEdit("Rest Edited Customer", "0988776655", "New Addr"))).Succeeded);
            Assert.True((await customers.SetActiveAsync(customer.UserId, false)).Succeeded);
            Assert.False((await customers.GetAsync(customer.UserId))!.IsActive);
            Assert.True((await customers.SetActiveAsync(customer.UserId, true)).Succeeded);

            // 8. Order Management (list, detail, status transitions)
            var listedOrders = await orders.ListAsync(new OrderManagementQuery(null, OrderStatus.Pending));
            Assert.Single(listedOrders);
            var orderDetail = await orders.GetAsync(order.OrderId);
            Assert.Equal(customer.Email, orderDetail!.CustomerEmail);
            Assert.Equal(OrderStatus.Pending, orderDetail.Status);
            Assert.True((await orders.UpdateStatusAsync(order.OrderId, OrderStatus.Pending, OrderStatus.Confirmed)).Succeeded);
            Assert.Equal(ManagementFailure.Conflict, (await orders.UpdateStatusAsync(order.OrderId, OrderStatus.Pending, OrderStatus.Cancelled)).Failure);
            Assert.True((await orders.UpdateStatusAsync(order.OrderId, OrderStatus.Confirmed, OrderStatus.Shipping)).Succeeded);
            Assert.True((await orders.UpdateStatusAsync(order.OrderId, OrderStatus.Shipping, OrderStatus.Completed)).Succeeded);

            // 9. Logout revokes backend refresh session & clears local store
            var revokedRefreshToken = sessionStore.Current!.RefreshToken;
            await auth.LogoutAsync();
            Assert.Null(sessionStore.Current);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reporting.GetDashboardAsync());
            var dbRevoked = await fixture.Db.ApiRefreshTokens.AsNoTracking().CountAsync(t => t.RevokedReason == "Logout");
            Assert.True(dbRevoked >= 1);
            Assert.False(string.IsNullOrEmpty(revokedRefreshToken));
        }
        finally
        {
            if (Directory.Exists(tempUploads)) Directory.Delete(tempUploads, recursive: true);
            if (Directory.Exists(tempSourceDir)) Directory.Delete(tempSourceDir, recursive: true);
        }
    }

    [Fact]
    public async Task RestApi_mode_handles_unavailable_timeout_unauthorized_without_write_retry_or_sql_fallback()
    {
        // 1. API Unavailable -> AdminApiException (no silent SQL fallback)
        var unavailableHandler = new StubHttpHandler(_ => throw new HttpRequestException("Connection refused"));
        using (var http = new HttpClient(unavailableHandler) { BaseAddress = new Uri("http://localhost:5999/") })
        using (var store = new AdminApiSessionStore())
        {
            store.Establish("tok", DateTime.UtcNow.AddMinutes(10), "ref", DateTime.UtcNow.AddDays(1),
                new AdminIdentity(1, "Admin", "a@b.c", SystemRoles.Admin));
            var client = new AdminApiClient(http, store);
            await Assert.ThrowsAsync<AdminApiException>(() => new RestReportingService(client).GetDashboardAsync());
            var writeRes = await new RestLookupManagementService(client).SaveAsync(LookupKind.Brand, null, new LookupEdit("B", null, true));
            Assert.False(writeRes.Succeeded);
            Assert.Equal(ManagementFailure.Database, writeRes.Failure);
            Assert.Equal(2, unavailableHandler.Calls); // 1 GET + 1 POST (ZERO retries on write!)
        }

        // 2. Timeout -> AdminApiException(IsTimeout=true) and ZERO retries on POST/PUT/PATCH
        var timeoutHandler = new StubHttpHandler(_ => throw new TaskCanceledException("Timed out"));
        using (var http = new HttpClient(timeoutHandler) { BaseAddress = new Uri("http://localhost:5999/") })
        using (var store = new AdminApiSessionStore())
        {
            store.Establish("tok", DateTime.UtcNow.AddMinutes(10), "ref", DateTime.UtcNow.AddDays(1),
                new AdminIdentity(1, "Admin", "a@b.c", SystemRoles.Admin));
            var client = new AdminApiClient(http, store);
            var ex = await Assert.ThrowsAsync<AdminApiException>(() => new RestReportingService(client).GetDashboardAsync());
            Assert.True(ex.IsTimeout);
            var writeRes = await new RestOrderManagementService(client).UpdateStatusAsync(1, OrderStatus.Pending, OrderStatus.Confirmed);
            Assert.False(writeRes.Succeeded);
            Assert.Equal(ManagementFailure.Database, writeRes.Failure);
            Assert.Equal(2, timeoutHandler.Calls); // 1 GET + 1 PATCH (ZERO retries on timeout!)
        }

        // 3. 403 Forbidden -> clears session immediately and throws UnauthorizedAccessException (no SQL fallback)
        var forbiddenHandler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using (var http = new HttpClient(forbiddenHandler) { BaseAddress = new Uri("http://localhost:5999/") })
        using (var store = new AdminApiSessionStore())
        {
            store.Establish("tok", DateTime.UtcNow.AddMinutes(10), "ref", DateTime.UtcNow.AddDays(1),
                new AdminIdentity(1, "Admin", "a@b.c", SystemRoles.Admin));
            var client = new AdminApiClient(http, store);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new RestCustomerManagementService(client).ListAsync(new(null, null)));
            Assert.Null(store.Current);
        }

        // 4. Customer identity cannot be established into AdminApiSessionStore
        using (var store = new AdminApiSessionStore())
        {
            Assert.Throws<UnauthorizedAccessException>(() =>
                store.Establish("tok", DateTime.UtcNow.AddMinutes(10), "ref", DateTime.UtcNow.AddDays(1),
                    new AdminIdentity(2, "Cust", "c@b.c", SystemRoles.Customer)));
            Assert.Null(store.Current);
        }
    }

    private static WebApplicationFactory<PhoneStore.Api.Program> CreateApiHost(
        PhoneStoreDbContext db, string uploadsDir, TimeProvider clock) =>
        new WebApplicationFactory<PhoneStore.Api.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.UseSetting("ApiAuthentication:Mode", "LocalJwt");
            builder.UseSetting("ApiAuthentication:LocalJwt:Issuer", Issuer);
            builder.UseSetting("ApiAuthentication:LocalJwt:Audience", Audience);
            builder.UseSetting("ApiAuthentication:LocalJwt:SigningKey", SigningKey);
            builder.UseSetting("ApiAuthentication:LocalJwt:AccessTokenMinutes", "15");
            builder.UseSetting("ApiAuthentication:LocalJwt:RefreshTokenDays", "7");
            builder.UseSetting("ApiOperational:AuthRequestsPerMinute", "100");
            builder.UseSetting("ApiOperational:ProtectedRequestsPerMinute", "500");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PhoneStoreDbContext>();
                services.AddSingleton(db);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(clock);
                services.RemoveAll<IProductImageStorage>();
                services.AddSingleton<IProductImageStorage>(sp =>
                    new LocalProductImageStorage(uploadsDir, sp.GetRequiredService<IProductImageContentValidator>()));
            });
        });

    private static async Task<(User Customer, Product Product, User Admin, Order Order)> SeedFullAdminScenarioAsync(
        TestDatabase fixture, DateTime utcNow)
    {
        var hasher = new PasswordHasher<User>();
        var (customer, product) = await fixture.SeedCatalogAsync();
        customer.PasswordHash = hasher.HashPassword(customer, "CustPass!2026");

        var admin = new User
        {
            FullName = "Admin Operator",
            Email = "admin-rest@test.local",
            RoleId = SystemRoles.AdminId,
            IsActive = true,
            CreatedAt = utcNow
        };
        admin.PasswordHash = hasher.HashPassword(admin, "AdminPass!2026");

        var order = new Order
        {
            UserId = customer.UserId,
            OrderCode = "ORD-S354E-001",
            ReceiverName = customer.FullName,
            ReceiverPhone = "0900111222",
            ShippingAddress = "100 Main St",
            OrderDate = utcNow,
            Status = OrderStatus.Pending,
            TotalAmount = product.Price,
            OrderDetails = [new OrderDetail { ProductId = product.ProductId, Quantity = 1, UnitPrice = product.Price }]
        };
        fixture.Db.Users.Add(admin);
        fixture.Db.Orders.Add(order);
        await fixture.Db.SaveChangesAsync();
        return (customer, product, admin, order);
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(handler(request));
        }
    }
}
