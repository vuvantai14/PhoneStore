using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

// Runs the real Program, MVC views, cookie auth/authorization and antiforgery middleware.
// Replaces the SQL provider before resolving any DbContext; never opens the main database.
public sealed class CustomerProfileHttpTests
{
    private sealed class ProfileApp : WebApplicationFactory<global::Program>
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public int CustomerId { get; private set; }
        public int OtherId { get; private set; }
        public string Password { get; } = Guid.NewGuid().ToString("N") + "aA1!";
        public AcceptanceClock Clock { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme,
                    options => options.TimeProvider = Clock);
                services.RemoveAll<DbContextOptions<PhoneStoreDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<PhoneStoreDbContext>>();
                services.RemoveAll<PhoneStoreDbContext>();
                connection.CreateFunction("SYSUTCDATETIME", () => DateTime.UtcNow);
                connection.Open();
                services.AddDbContext<PhoneStoreDbContext>(o => o.UseSqlite(connection));
            });
        }

        public async Task<HttpClient> StartAsync()
        {
            var client = CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PhoneStoreDbContext>();
            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", db.Database.ProviderName);
            await db.Database.EnsureCreatedAsync();
            var hasher = new PasswordHasher<User>();
            var user = new User { FullName = "Profile Customer", Email = "profile@test.local", RoleId = 2 };
            user.PasswordHash = hasher.HashPassword(user, Password);
            var other = new User { FullName = "Other Customer", Email = "other@test.local", RoleId = 2, PasswordHash = "other-secret-hash" };
            other.PasswordHash = hasher.HashPassword(other, Password);
            db.Users.AddRange(user, other); await db.SaveChangesAsync();
            CustomerId = user.UserId; OtherId = other.UserId;
            return client;
        }

        public async Task<User> ReadAsync(int id)
        {
            using var scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<PhoneStoreDbContext>().Users.AsNoTracking().SingleAsync(u => u.UserId == id);
        }

        public async Task ChangeAuthorityAsync(bool inactive)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PhoneStoreDbContext>();
            if (inactive)
                await db.Users.Where(u => u.UserId == CustomerId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
            else
                await db.Users.Where(u => u.UserId == CustomerId).ExecuteUpdateAsync(s => s.SetProperty(u => u.RoleId, 1));
        }

        public override async ValueTask DisposeAsync() { await base.DisposeAsync(); await connection.DisposeAsync(); }
    }

    private sealed class AcceptanceClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("role")]
    [InlineData("expired")]
    public async Task Cart_owner_isolation_through_real_cookie_pipeline(string invalidation)
    {
        await using var app = new ProfileApp(); using var client = await app.StartAsync();
        int productId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PhoneStoreDbContext>();
            var product = new Product { ProductName = "Isolation fixture", Price = 1000000m, StockQuantity = 5,
                Brand = new Brand { BrandName = "Isolation brand" }, Category = new Category { CategoryName = "Isolation category" } };
            db.Products.Add(product); await db.SaveChangesAsync(); productId = product.ProductId;
        }
        await Login(app, client);
        var cartPage = await client.GetStringAsync("/cart");
        var added = await client.PostAsync("/cart/add", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["productId"] = productId.ToString(), ["quantity"] = "2", ["__RequestVerificationToken"] = Token(cartPage) }));
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        var checkout = await client.GetStringAsync("/checkout");
        var oldToken = WebUtility.HtmlDecode(Regex.Match(checkout, "name=\"SubmissionToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.NotEmpty(oldToken);
        // Normal requests retain both the owner's cart and token.
        Assert.Contains("\"count\":2", await client.GetStringAsync("/cart/count"));
        Assert.Contains(oldToken, await client.GetStringAsync("/checkout"));
        if (invalidation == "expired") app.Clock.Now = app.Clock.Now.AddDays(15);
        else await app.ChangeAuthorityAsync(invalidation == "disabled");
        var rejected = await client.GetAsync("/account/profile");
        Assert.Equal(HttpStatusCode.Redirect, rejected.StatusCode);
        Assert.Contains("/account/login", rejected.Headers.Location!.ToString());
        Assert.Contains("\"count\":0", await client.GetStringAsync("/cart/count"));
        var loginPage = await client.GetStringAsync("/account/login");
        var loginB = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Email"] = "other@test.local", ["Password"] = app.Password, ["__RequestVerificationToken"] = Token(loginPage) }));
        Assert.Equal(HttpStatusCode.Redirect, loginB.StatusCode);
        // Do not resurrect A's expired ticket before B's login. Return the test
        // clock after the new ticket is issued (controller expiry uses UTC now).
        app.Clock.Now = DateTimeOffset.UtcNow;
        Assert.Contains("\"count\":0", await client.GetStringAsync("/cart/count"));
        cartPage = await client.GetStringAsync("/cart");
        await client.PostAsync("/cart/add", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["productId"] = productId.ToString(), ["quantity"] = "1", ["__RequestVerificationToken"] = Token(cartPage) }));
        checkout = await client.GetStringAsync("/checkout");
        Assert.DoesNotContain(oldToken, checkout);
        var replay = await client.PostAsync("/checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["SubmissionToken"] = oldToken, ["ReceiverName"] = "Test customer", ["ReceiverPhone"] = "0900000001",
            ["ShippingAddress"] = "Test address", ["__RequestVerificationToken"] = Token(checkout) }));
        Assert.Equal(HttpStatusCode.Redirect, replay.StatusCode);
        Assert.Equal("/cart", replay.Headers.Location!.ToString());
        // Explicit logout remains safe, including after the new owner has a cart.
        cartPage = await client.GetStringAsync("/cart");
        var logout = await client.PostAsync("/account/logout", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["__RequestVerificationToken"] = Token(cartPage) }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Contains("\"count\":0", await client.GetStringAsync("/cart/count"));
        using var verifyScope = app.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<PhoneStoreDbContext>();
        Assert.Empty(await verify.Orders.ToListAsync());
        Assert.Equal(5, (await verify.Products.SingleAsync()).StockQuantity);
    }

    private static string Token(string html) => WebUtility.HtmlDecode(Regex.Match(html,
        "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);

    private static AuthenticationTicket Ticket(ProfileApp app, HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("PhoneStore.Auth="));
        var value = header.Split(';')[0]["PhoneStore.Auth=".Length..];
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        return options.TicketDataFormat.Unprotect(value)!;
    }

    private static async Task<AuthenticationTicket> Login(ProfileApp app, HttpClient client)
    {
        var page = await client.GetAsync("/account/login");
        var html = await page.Content.ReadAsStringAsync();
        Assert.True(page.IsSuccessStatusCode, html);
        var result = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Email"] = "profile@test.local", ["Password"] = app.Password, ["RememberMe"] = "true", ["__RequestVerificationToken"] = Token(html) }));
        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        return Ticket(app, result);
    }

    [Fact]
    public async Task Anonymous_get_and_post_require_login()
    {
        await using var app = new ProfileApp(); using var client = await app.StartAsync();
        foreach (var response in new[] { await client.GetAsync("/account/profile"),
            await client.PostAsync("/account/profile", new FormUrlEncodedContent(new Dictionary<string, string>())) })
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("https://localhost/account/login", response.Headers.Location!.ToString());
        }
    }

    [Fact]
    public async Task Anonymous_checkout_cannot_render_a_submission_token()
    {
        await using var app = new ProfileApp();
        using var client = await app.StartAsync();
        using var response = await client.GetAsync("/checkout");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://localhost/account/login", response.Headers.Location!.ToString());
        Assert.DoesNotContain("SubmissionToken", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Authenticated_read_renders_own_profile_without_credentials()
    {
        await using var app = new ProfileApp(); using var client = await app.StartAsync(); await Login(app, client);
        var html = await client.GetStringAsync("/account/profile?userId=" + app.OtherId);
        Assert.Contains("profile@test.local", html); Assert.Contains("Profile Customer", html);
        Assert.DoesNotContain("other@test.local", html); Assert.DoesNotContain("PasswordHash", html);
        Assert.DoesNotContain("name=\"RoleId\"", html); Assert.DoesNotContain("name=\"UserId\"", html);
        Assert.DoesNotContain("type=\"password\"", html); Assert.NotEmpty(Token(html));
        Assert.Contains("/account/profile", await client.GetStringAsync("/"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/account/profile/" + app.OtherId)).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_forged_csrf_is_rejected_without_writes(bool forged)
    {
        await using var app = new ProfileApp(); using var client = await app.StartAsync(); await Login(app, client);
        var form = new Dictionary<string, string> { ["FullName"] = "Changed name" };
        if (forged) form["__RequestVerificationToken"] = "forged";
        var response = await client.PostAsync("/account/profile", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Profile Customer", (await app.ReadAsync(app.CustomerId)).FullName);
    }

    [Fact]
    public async Task Edit_ignores_forged_identity_fields_and_renews_name_without_logging_out()
    {
        await using var app = new ProfileApp(); using var client = await app.StartAsync(); var originalTicket = await Login(app, client);
        var before = await app.ReadAsync(app.CustomerId);
        var html = await client.GetStringAsync("/account/profile");
        var response = await client.PostAsync("/account/profile?userId=" + app.OtherId,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FullName"] = "  Updated Customer  ", ["Phone"] = "09032657212", ["Address"] = "New address",
                ["UserId"] = app.OtherId.ToString(), ["RoleId"] = "1", ["Email"] = "attacker@test.local",
                ["PasswordHash"] = "forged", ["IsActive"] = "false", ["__RequestVerificationToken"] = Token(html)
            }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/profile", response.Headers.Location!.ToString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("PhoneStore.Auth=") && c.Contains("expires="));
        var renewedTicket = Ticket(app, response);
        Assert.Equal(originalTicket.Properties.ExpiresUtc, renewedTicket.Properties.ExpiresUtc);
        Assert.Equal(originalTicket.Properties.IsPersistent, renewedTicket.Properties.IsPersistent);
        var after = await app.ReadAsync(app.CustomerId);
        Assert.Equal("Updated Customer", after.FullName); Assert.Equal("New address", after.Address);
        Assert.Equal(before.Email, after.Email); Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(2, after.RoleId); Assert.True(after.IsActive); Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal("Other Customer", (await app.ReadAsync(app.OtherId)).FullName);
        var refreshed = await client.GetStringAsync("/account/profile");
        Assert.Contains("Updated Customer", refreshed); Assert.Contains("profile@test.local", refreshed);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/orders")).StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Inactive_or_no_longer_customer_session_cannot_read_or_write_profile(bool inactive)
    {
        await using var app = new ProfileApp(); using var client = await app.StartAsync(); await Login(app, client);
        var html = await client.GetStringAsync("/account/profile");
        await app.ChangeAuthorityAsync(inactive);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/account/profile", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["FullName"] = "Unauthorized edit", ["__RequestVerificationToken"] = Token(html) }))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/account/profile")).StatusCode);
        Assert.Equal("Profile Customer", (await app.ReadAsync(app.CustomerId)).FullName);
    }

    [Fact]
    public async Task Antiforgery_token_from_another_browser_session_is_rejected()
    {
        await using var app = new ProfileApp(); using var client = await app.StartAsync(); await Login(app, client);
        using var otherBrowser = app.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var foreignToken = Token(await otherBrowser.GetStringAsync("/account/login"));
        var response = await client.PostAsync("/account/profile", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["FullName"] = "Unauthorized edit", ["__RequestVerificationToken"] = foreignToken }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Profile Customer", (await app.ReadAsync(app.CustomerId)).FullName);
    }

    [Theory]
    [InlineData(" ", "0900000000")]
    [InlineData("Valid Customer", "invalid-phone")]
    public async Task Server_validation_returns_form_without_modifying_database(string name, string phone)
    {
        await using var app = new ProfileApp(); using var client = await app.StartAsync(); await Login(app, client);
        var html = await client.GetStringAsync("/account/profile");
        var response = await client.PostAsync("/account/profile", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["FullName"] = name, ["Phone"] = phone, ["__RequestVerificationToken"] = Token(html) }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("profile@test.local", await response.Content.ReadAsStringAsync());
        Assert.Equal("Profile Customer", (await app.ReadAsync(app.CustomerId)).FullName);
    }
}
