using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Auth;
using PhoneStore.Api.Contracts.Orders;
using PhoneStore.Api.Contracts.Products;
using PhoneStore.Api.Contracts.Profile;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Data.Context;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class LocalJwtAuthenticationTests
{
    private const string Issuer = "https://auth.phonestore.test";
    private const string Audience = "phonestore-api-test";
    private const string StrongKey = "K9!mP4#xV8@qL2$zR7^wN5&bT1*cY6(eF3)hJ0+uS_8gD2=vM4%nX";

    [Fact]
    public async Task Customer_and_admin_login_issue_scoped_local_jwts_and_enforce_role_isolation()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, admin, product) = await SeedUsersAndOrderAsync(fixture);
        using var host = CreateLocalJwtHost(fixture.Db);
        using var client = host.CreateClient();

        // Wrong password and unknown email produce identical 401 without enumeration
        var wrongPwd = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = customer.Email, password = "WrongPassword!9" });
        var unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nobody@test.local", password = "WrongPassword!9" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPwd.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal("no-store", wrongPwd.Headers.CacheControl?.ToString());
        using var wrongJson = JsonDocument.Parse(await wrongPwd.Content.ReadAsStringAsync());
        using var unknownJson = JsonDocument.Parse(await unknownEmail.Content.ReadAsStringAsync());
        Assert.Equal(wrongJson.RootElement.GetProperty("detail").GetString(), unknownJson.RootElement.GetProperty("detail").GetString());

        // Customer cannot login to Admin endpoint
        var customerAdminAttempt = await client.PostAsJsonAsync("/api/v1/auth/admin/login",
            new { email = customer.Email, password = "CustomerPass!2026" });
        Assert.Equal(HttpStatusCode.Forbidden, customerAdminAttempt.StatusCode);

        // Admin cannot login to Customer endpoint
        var adminCustomerAttempt = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = admin.Email, password = "AdminPass!2026" });
        Assert.Equal(HttpStatusCode.Unauthorized, adminCustomerAttempt.StatusCode);

        // Valid Customer login
        var custLoginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = customer.Email, password = "CustomerPass!2026" });
        Assert.Equal(HttpStatusCode.OK, custLoginResp.StatusCode);
        Assert.Equal("no-store", custLoginResp.Headers.CacheControl?.ToString());
        var custRawBody = await custLoginResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", custRawBody, StringComparison.OrdinalIgnoreCase);
        var custAuth = JsonSerializer.Deserialize<AuthTokenResponse>(custRawBody, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("Bearer", custAuth.TokenType);
        Assert.Equal(900, custAuth.ExpiresInSeconds);
        Assert.Equal(SystemRoles.Customer, custAuth.User.Role);
        Assert.StartsWith("psrt_", custAuth.RefreshToken, StringComparison.Ordinal);

        // Refresh token must NEVER be stored plaintext in DB
        var storedTokens = await fixture.Db.ApiRefreshTokens.AsNoTracking().ToListAsync();
        var stored = Assert.Single(storedTokens);
        Assert.NotEqual(custAuth.RefreshToken, stored.TokenHash);
        Assert.Equal(ApiRefreshTokenService.HashToken(custAuth.RefreshToken), stored.TokenHash);

        // Customer accesses own /me, /profile, /api/orders, but is forbidden on /api/admin/*
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", custAuth.AccessToken);
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me");
        Assert.Equal(customer.UserId, me!.UserId);
        Assert.Equal(SystemRoles.Customer, me.Role);
        Assert.Contains("orders:read", me.Scopes);

        var orders = await client.GetFromJsonAsync<OrderPage>("/api/orders");
        Assert.Equal(1, orders!.TotalCount);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/reports/overview?fromDate=2026-11-01&toDate=2026-11-21")).StatusCode);

        // Valid Admin login
        client.DefaultRequestHeaders.Authorization = null;
        var adminLoginResp = await client.PostAsJsonAsync("/api/v1/auth/admin/login",
            new { email = admin.Email, password = "AdminPass!2026" });
        Assert.Equal(HttpStatusCode.OK, adminLoginResp.StatusCode);
        var adminAuth = await adminLoginResp.Content.ReadFromJsonAsync<AuthTokenResponse>();
        Assert.Equal(SystemRoles.Admin, adminAuth!.User.Role);

        // Admin accesses /me and all /api/admin/* endpoints, but is forbidden on customer-only /api/orders and /api/v1/profile
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminAuth.AccessToken);
        var adminMe = await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me");
        Assert.Equal(admin.UserId, adminMe!.UserId);
        Assert.Equal(SystemRoles.Admin, adminMe.Role);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/products")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/reports/overview?fromDate=2026-11-01&toDate=2026-11-21")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/profile")).StatusCode);

        // Admin can perform write on isolated test DB
        var createdResp = await client.PostAsJsonAsync("/api/admin/products", new ProductWriteRequest
        {
            ProductName = "JWT Auth Created Phone",
            BrandId = product.BrandId,
            CategoryId = product.CategoryId,
            Price = 15_000_000m,
            StockQuantity = 4,
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.Created, createdResp.StatusCode);

        // Deactivating account in DB immediately revokes access even for a non-expired JWT
        await fixture.Db.Users.Where(u => u.UserId == admin.UserId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        // Inactive customer cannot log in
        await fixture.Db.Users.Where(u => u.UserId == customer.UserId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = customer.Email, password = "CustomerPass!2026" })).StatusCode);
    }

    [Fact]
    public async Task Registration_creates_customer_only_and_rejects_privilege_escalation()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        await SeedUsersAndOrderAsync(fixture);
        using var host = CreateLocalJwtHost(fixture.Db);
        using var client = host.CreateClient();

        // Attempting to inject roleId or role in JSON body is rejected with 400
        using var escalationBody = new StringContent(
            """{"fullName":"Attacker","email":"attacker@test.local","password":"ValidPassword!1","roleId":1,"role":"Admin"}""",
            Encoding.UTF8, "application/json");
        var escalated = await client.PostAsync("/api/v1/auth/register", escalationBody);
        Assert.Equal(HttpStatusCode.BadRequest, escalated.StatusCode);

        // Valid registration creates Customer and never exposes password hash
        var regResp = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "New Customer",
            email = "NEW.CUST@TEST.LOCAL",
            phone = "0901234567",
            password = "StrongUserPass!2026"
        });
        Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);
        var regText = await regResp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", regText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StrongUserPass!2026", regText, StringComparison.Ordinal);
        var reg = JsonSerializer.Deserialize<RegisterResponse>(regText, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("new.cust@test.local", reg.Email);
        Assert.Equal(SystemRoles.Customer, reg.Role);

        var createdUser = await fixture.Db.Users.Include(u => u.Role).AsNoTracking()
            .SingleAsync(u => u.Email == "new.cust@test.local");
        Assert.Equal(SystemRoles.Customer, createdUser.Role.RoleName);
        Assert.NotEqual("StrongUserPass!2026", createdUser.PasswordHash);

        // Duplicate email returns 409 Conflict
        var dupResp = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            fullName = "Another User",
            email = "new.cust@test.local",
            password = "StrongUserPass!2026"
        });
        Assert.Equal(HttpStatusCode.Conflict, dupResp.StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("tamperedSignature")]
    [InlineData("wrongKey")]
    [InlineData("unsigned")]
    [InlineData("wrongAlg")]
    [InlineData("wrongTyp")]
    [InlineData("wrongIssuer")]
    [InlineData("wrongAudience")]
    [InlineData("audienceSlash")]
    [InlineData("expired")]
    [InlineData("wrongTokenUse")]
    [InlineData("missingJti")]
    public async Task Invalid_local_jwt_tokens_are_rejected_with_401(string scenario)
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, _, _) = await SeedUsersAndOrderAsync(fixture);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        using var host = CreateLocalJwtHost(fixture.Db, clock);
        using var client = host.CreateClient();

        if (scenario != "missing")
        {
            var token = CreateCraftedToken(customer.UserId, clock.GetUtcNow().UtcDateTime, scenario);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task Client_forged_role_claim_cannot_escalate_customer_to_admin()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, _, _) = await SeedUsersAndOrderAsync(fixture);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        using var host = CreateLocalJwtHost(fixture.Db, clock);
        using var client = host.CreateClient();

        // Even with a validly signed token containing role=Admin & scope=admin:all for a Customer's sub, DB role check denies access
        var forgedAdminClaimToken = CreateCraftedToken(customer.UserId, clock.GetUtcNow().UtcDateTime, "forgedAdminClaim");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forgedAdminClaimToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/products")).StatusCode);
    }

    [Fact]
    public async Task Refresh_rotation_reuse_detection_expiration_and_logout_revocation_work_end_to_end()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, admin, _) = await SeedUsersAndOrderAsync(fixture);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));
        using var host = CreateLocalJwtHost(fixture.Db, clock);
        using var client = host.CreateClient();

        // 1. Login -> session 1
        var loginResp = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = customer.Email, password = "CustomerPass!2026" });
        var first = (await loginResp.Content.ReadFromJsonAsync<AuthTokenResponse>())!;

        // 2. Advance 16 minutes -> first access token is expired, refresh succeeds and rotates token
        clock.Advance(TimeSpan.FromMinutes(16));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var refreshResp = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshResp.StatusCode);
        var second = (await refreshResp.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(first.AccessToken, second.AccessToken);

        // New access token works
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        // 3. Reuse detection: replaying already-rotated first.RefreshToken fails AND revokes second.RefreshToken in the same family!
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = second.RefreshToken })).StatusCode);

        var familyTokens = await fixture.Db.ApiRefreshTokens.AsNoTracking().ToListAsync();
        Assert.Equal(2, familyTokens.Count);
        Assert.All(familyTokens, t => Assert.NotNull(t.RevokedAtUtc));
        Assert.Contains(familyTokens, t => t.RevokedReason == "ReuseDetected");

        // 4. Expired refresh token fails cleanly
        var session2 = (await (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = customer.Email, password = "CustomerPass!2026" })).Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        clock.Advance(TimeSpan.FromDays(8));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session2.RefreshToken })).StatusCode);

        // 5. Logout revokes session and blocks cross-user logout revocation
        var session3 = (await (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = customer.Email, password = "CustomerPass!2026" })).Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        var adminSession = (await (await client.PostAsJsonAsync("/api/v1/auth/admin/login",
            new { email = admin.Email, password = "AdminPass!2026" })).Content.ReadFromJsonAsync<AuthTokenResponse>())!;

        // Anonymous logout -> 401
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = session3.RefreshToken })).StatusCode);

        // Customer trying to revoke Admin's refresh token -> 403
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session3.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = adminSession.RefreshToken })).StatusCode);

        // Customer logs out own session -> 204, and refresh token is dead
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = session3.RefreshToken })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = session3.RefreshToken })).StatusCode);

        // Admin's refresh token was untouched by customer's failed cross-user logout attempt
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = adminSession.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task Customer_profile_api_reads_and_updates_own_profile_and_blocks_sensitive_fields()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, _, _) = await SeedUsersAndOrderAsync(fixture);
        using var host = CreateLocalJwtHost(fixture.Db);
        using var client = host.CreateClient();

        // Anonymous -> 401
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/profile", new { fullName = "X" })).StatusCode);

        var auth = (await (await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = customer.Email, password = "CustomerPass!2026" })).Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        // Read own profile
        var profile = await client.GetFromJsonAsync<CustomerProfileResponse>("/api/v1/profile");
        Assert.Equal(customer.FullName, profile!.FullName);
        Assert.Equal(customer.Email, profile.Email);

        // Reject unmapped sensitive fields (roleId, email, passwordHash, isActive)
        using var badPayload = new StringContent(
            """{"fullName":"Updated Name","phone":"0988776655","address":"Hanoi","roleId":1,"email":"hacked@test.local","passwordHash":"plain"}""",
            Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/v1/profile", badPayload)).StatusCode);

        // Reject invalid input (name too short)
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync("/api/v1/profile", new { fullName = "A", phone = "0988776655", address = "Hanoi" })).StatusCode);

        // Valid update modifies only FullName/Phone/Address of authenticated customer
        var updatedResp = await client.PutAsJsonAsync("/api/v1/profile",
            new { fullName = "Nguyễn Văn Cập Nhật", phone = "0912345678", address = "123 Lê Lợi, Hà Nội" });
        Assert.Equal(HttpStatusCode.OK, updatedResp.StatusCode);
        var updated = await updatedResp.Content.ReadFromJsonAsync<CustomerProfileResponse>();
        Assert.Equal("Nguyễn Văn Cập Nhật", updated!.FullName);
        Assert.Equal(customer.Email, updated.Email);
        Assert.Equal("0912345678", updated.Phone);
        Assert.Equal("123 Lê Lợi, Hà Nội", updated.Address);

        var dbUser = await fixture.Db.Users.Include(u => u.Role).AsNoTracking().SingleAsync(u => u.UserId == customer.UserId);
        Assert.Equal(SystemRoles.Customer, dbUser.Role.RoleName);
        Assert.Equal(customer.Email, dbUser.Email);
        Assert.Equal(customer.PasswordHash, dbUser.PasswordHash);
    }

    [Theory]
    [InlineData("LocalJwt", null, Audience, StrongKey, 15, 7, false)]
    [InlineData("LocalJwt", "http://insecure.local", Audience, StrongKey, 15, 7, false)]
    [InlineData("LocalJwt", Issuer, null, StrongKey, 15, 7, false)]
    [InlineData("LocalJwt", Issuer, Audience, null, 15, 7, false)]
    [InlineData("LocalJwt", Issuer, Audience, "too-short-key", 15, 7, false)]
    [InlineData("LocalJwt", Issuer, Audience, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", 15, 7, false)]
    [InlineData("LocalJwt", Issuer, Audience, "default-signing-key-value-1234567890-xyz", 15, 7, false)]
    [InlineData("LocalJwt", Issuer, Audience, StrongKey, 120, 7, false)]
    [InlineData("LocalJwt", Issuer, Audience, StrongKey, 15, 60, false)]
    [InlineData("LocalJwt", Issuer, Audience, StrongKey, 15, 7, true)]
    public async Task Invalid_local_jwt_configuration_fails_closed_at_startup(
        string mode, string? issuer, string? audience, string? signingKey,
        int accessMinutes, int refreshDays, bool oidcEnabled)
    {
        var values = new Dictionary<string, string?>
        {
            ["ApiAuthentication:Mode"] = mode,
            ["ApiAuthentication:LocalJwt:Issuer"] = issuer,
            ["ApiAuthentication:LocalJwt:Audience"] = audience,
            ["ApiAuthentication:LocalJwt:SigningKey"] = signingKey,
            ["ApiAuthentication:LocalJwt:AccessTokenMinutes"] = accessMinutes.ToString(),
            ["ApiAuthentication:LocalJwt:RefreshTokenDays"] = refreshDays.ToString(),
            ["ApiAuthentication:Oidc:Enabled"] = oidcEnabled.ToString()
        };
        if (oidcEnabled)
        {
            values["ApiAuthentication:Oidc:Issuer"] = "https://oidc.example.test/";
            values["ApiAuthentication:Oidc:Audience"] = "api";
            values["ApiAuthentication:Oidc:CustomerClientId"] = "client";
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var host = new HostBuilder()
            .ConfigureServices(services => services.AddPhoneStoreApiAccess(config))
            .Build();
        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Equal(typeof(ApiAuthenticationOptions), error.OptionsType);
    }

    [Fact]
    public async Task Auth_rate_limit_request_size_limit_https_requirement_and_log_safety_are_enforced()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, _, _) = await SeedUsersAndOrderAsync(fixture);
        using var host = CreateLocalJwtHost(fixture.Db, authRateLimit: 2, maxBodyBytes: 1024);
        using var client = host.CreateClient();

        // Auth rate limit (2 per minute) triggers 429 on 3rd attempt
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = customer.Email, password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = customer.Email, password = "wrong" })).StatusCode);
        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = customer.Email, password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(limited.Headers.RetryAfter);

        // Oversized body (> 1024 bytes) triggers 413 Payload Too Large
        using var hugeBody = new StringContent(new string('a', 2048), Encoding.UTF8, "application/json");
        var tooLarge = await client.PostAsync("/api/v1/auth/register", hugeBody);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        Assert.Equal("application/problem+json", tooLarge.Content.Headers.ContentType?.MediaType);

        // RequireHttps rejects plain HTTP requests and untrusted X-Forwarded-Proto, while permitting loopback health probes
        var httpsMiddleware = new RequestSafetyMiddleware(
            ctx => { ctx.Response.StatusCode = StatusCodes.Status200OK; return Task.CompletedTask; },
            new CapturingLogger<RequestSafetyMiddleware>(),
            new ApiOperationalOptions { RequireHttps = true });
        var httpContext = new DefaultHttpContext { TraceIdentifier = "https-check" };
        httpContext.Request.Scheme = "http";
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/v1/auth/login";
        httpContext.Request.Headers["X-Forwarded-Proto"] = "https";
        httpContext.Response.Body = new MemoryStream();
        await httpsMiddleware.InvokeAsync(httpContext);
        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);

        // Loopback health probe is permitted over HTTP even when RequireHttps = true
        // (covers IPv4 127.0.0.1, IPv6 ::1, and Linux dual-stack IPv4-mapped IPv6 ::ffff:127.0.0.1)
        foreach (var loopbackIp in new[]
                 {
                     System.Net.IPAddress.Loopback,
                     System.Net.IPAddress.IPv6Loopback,
                     System.Net.IPAddress.Loopback.MapToIPv6()
                 })
        {
            var loopbackHealth = new DefaultHttpContext { TraceIdentifier = "loopback-health-check" };
            loopbackHealth.Request.Scheme = "http";
            loopbackHealth.Request.Method = HttpMethods.Get;
            loopbackHealth.Request.Path = "/api/health/live";
            loopbackHealth.Connection.RemoteIpAddress = loopbackIp;
            loopbackHealth.Response.Body = new MemoryStream();
            await httpsMiddleware.InvokeAsync(loopbackHealth);
            Assert.Equal(StatusCodes.Status200OK, loopbackHealth.Response.StatusCode);
        }

        // Non-loopback plain HTTP health probe is still rejected when RequireHttps = true
        var remoteHealth = new DefaultHttpContext { TraceIdentifier = "remote-health-check" };
        remoteHealth.Request.Scheme = "http";
        remoteHealth.Request.Method = HttpMethods.Get;
        remoteHealth.Request.Path = "/api/health/live";
        remoteHealth.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");
        remoteHealth.Response.Body = new MemoryStream();
        await httpsMiddleware.InvokeAsync(remoteHealth);
        Assert.Equal(StatusCodes.Status400BadRequest, remoteHealth.Response.StatusCode);

        // Trusted reverse proxy forwarding X-Forwarded-Proto: https enables RequireHttps + JWT Bearer end-to-end
        using var proxyHost = new WebApplicationFactory<PhoneStore.Api.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting("ApiAuthentication:Mode", "LocalJwt");
            builder.UseSetting("ApiAuthentication:LocalJwt:Issuer", Issuer);
            builder.UseSetting("ApiAuthentication:LocalJwt:Audience", Audience);
            builder.UseSetting("ApiAuthentication:LocalJwt:SigningKey", StrongKey);
            builder.UseSetting("ApiOperational:RequireHttps", "true");
            builder.UseSetting("ApiOperational:TrustedProxies:0", "127.0.0.1");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PhoneStoreDbContext>();
                services.AddSingleton(fixture.Db);
            });
        });
        using var proxyClient = proxyHost.CreateClient();
        // Plain HTTP without forwarded proto -> 400
        Assert.Equal(HttpStatusCode.BadRequest, (await proxyClient.GetAsync("/api/v1/auth/me")).StatusCode);
        // Through trusted proxy (TestServer RemoteIpAddress is null -> test middleware with loopback remote IP via custom server middleware)
        // Verify Swagger stays 404 in Production even on HTTPS
        using var swaggerReq = new HttpRequestMessage(HttpMethod.Get, "https://localhost/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.NotFound, (await proxyClient.SendAsync(swaggerReq)).StatusCode);
        // Verify JWT Bearer works over HTTPS in Production
        using var loginHttps = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = customer.Email, password = "CustomerPass!2026" })
        };
        var prodLogin = await proxyClient.SendAsync(loginHttps);
        Assert.Equal(HttpStatusCode.OK, prodLogin.StatusCode);
        var prodTokens = (await prodLogin.Content.ReadFromJsonAsync<AuthTokenResponse>())!;
        using var meHttps = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/v1/auth/me");
        meHttps.Headers.Authorization = new AuthenticationHeaderValue("Bearer", prodTokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await proxyClient.SendAsync(meHttps)).StatusCode);

        // RequestSafetyMiddleware never logs Authorization header or bearer token
        var logger = new CapturingLogger<RequestSafetyMiddleware>();
        var logMiddleware = new RequestSafetyMiddleware(
            ctx => { ctx.Response.StatusCode = StatusCodes.Status200OK; return Task.CompletedTask; },
            logger,
            new ApiOperationalOptions());
        var authCtx = new DefaultHttpContext { TraceIdentifier = "bearer-log-test" };
        authCtx.Request.Method = HttpMethods.Get;
        authCtx.Request.Path = "/api/v1/auth/me";
        authCtx.Request.Headers.Authorization = "Bearer super-secret-jwt-token-value";
        await logMiddleware.InvokeAsync(authCtx);
        var combinedLogs = string.Join(Environment.NewLine, logger.Messages);
        Assert.DoesNotContain("Authorization", combinedLogs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret-jwt-token-value", combinedLogs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disabled_mode_keeps_auth_endpoints_closed()
    {
        using var factory = new WebApplicationFactory<PhoneStore.Api.Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiAuthentication:Mode"] = "Disabled"
            })));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "a@b.c", password = "Password!1" })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync("/api/v1/auth/admin/login", new { email = "a@b.c", password = "Password!1" })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync("/api/v1/auth/register", new { fullName = "Abc", email = "a@b.c", password = "Password!1" })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "psrt_12345678901234567890" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/profile")).StatusCode);
    }

    private static WebApplicationFactory<PhoneStore.Api.Program> CreateLocalJwtHost(
        PhoneStoreDbContext db,
        TimeProvider? timeProvider = null,
        int authRateLimit = 60,
        int maxBodyBytes = 65536) =>
        new WebApplicationFactory<PhoneStore.Api.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(Environments.Development);
            builder.UseSetting("ApiAuthentication:Mode", "LocalJwt");
            builder.UseSetting("ApiAuthentication:LocalJwt:Issuer", Issuer);
            builder.UseSetting("ApiAuthentication:LocalJwt:Audience", Audience);
            builder.UseSetting("ApiAuthentication:LocalJwt:SigningKey", StrongKey);
            builder.UseSetting("ApiAuthentication:LocalJwt:AccessTokenMinutes", "15");
            builder.UseSetting("ApiAuthentication:LocalJwt:RefreshTokenDays", "7");
            builder.UseSetting("ApiOperational:AuthRequestsPerMinute", authRateLimit.ToString());
            builder.UseSetting("ApiOperational:MaxRequestBodyBytes", maxBodyBytes.ToString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PhoneStoreDbContext>();
                services.AddSingleton(db);
                if (timeProvider is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(timeProvider);
                }
            });
        });

    private static async Task<(User Customer, User Admin, Product Product)> SeedUsersAndOrderAsync(TestDatabase fixture)
    {
        var hasher = new PasswordHasher<User>();
        var (customer, product) = await fixture.SeedCatalogAsync();
        customer.PasswordHash = hasher.HashPassword(customer, "CustomerPass!2026");

        var admin = new User
        {
            FullName = "Quản trị viên",
            Email = "admin@test.local",
            RoleId = SystemRoles.AdminId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        admin.PasswordHash = hasher.HashPassword(admin, "AdminPass!2026");

        var order = new Order
        {
            UserId = customer.UserId,
            OrderCode = "ORD-JWT-TEST-001",
            ReceiverName = customer.FullName,
            ReceiverPhone = "0900000001",
            ShippingAddress = "Test Address",
            OrderDate = DateTime.UtcNow,
            TotalAmount = product.Price,
            OrderDetails = [new OrderDetail { ProductId = product.ProductId, Quantity = 1, UnitPrice = product.Price }]
        };
        fixture.Db.Users.Add(admin);
        fixture.Db.Orders.Add(order);
        await fixture.Db.SaveChangesAsync();
        return (customer, admin, product);
    }

    private static string CreateCraftedToken(int userId, DateTime utcNow, string scenario)
    {
        if (scenario == "unsigned")
        {
            var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"at+jwt"}""");
            var payload = Base64UrlEncoder.Encode($$"""{"sub":"{{userId}}","jti":"j1","token_use":"access","iss":"{{Issuer}}","aud":"{{Audience}}","exp":{{new DateTimeOffset(utcNow.AddMinutes(10)).ToUnixTimeSeconds()}}}""");
            return $"{header}.{payload}.";
        }

        var keyBytes = scenario == "wrongKey"
            ? Encoding.UTF8.GetBytes("Z1!wQ9#mT4@vB7$xN2^pL8&cK5*fH3(rG6)yD0+aJ")
            : Encoding.UTF8.GetBytes(StrongKey);
        var key = new SymmetricSecurityKey(keyBytes) { KeyId = "local-hs256-v1" };
        var alg = scenario == "wrongAlg" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256;
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, userId.ToString()) };
        if (scenario != "missingJti") claims.Add(new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")));
        claims.Add(new("token_use", scenario == "wrongTokenUse" ? "refresh" : "access"));
        if (scenario == "forgedAdminClaim")
        {
            claims.Add(new(ClaimTypes.Role, SystemRoles.Admin));
            claims.Add(new("role", SystemRoles.Admin));
            claims.Add(new("scope", "admin:all"));
        }
        else
        {
            claims.Add(new("scope", "orders:read"));
        }

        var token = new JwtSecurityToken(
            issuer: scenario == "wrongIssuer" ? "https://other.phonestore.test" : Issuer,
            audience: scenario switch
            {
                "wrongAudience" => "other-audience",
                "audienceSlash" => Audience + "/",
                _ => Audience
            },
            claims: claims,
            notBefore: utcNow.AddMinutes(-2),
            expires: scenario == "expired" ? utcNow.AddMinutes(-1) : utcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(key, alg));
        token.Header["typ"] = scenario == "wrongTyp" ? "JWT" : "at+jwt";

        var serialized = new JwtSecurityTokenHandler().WriteToken(token);
        if (scenario == "tamperedSignature")
        {
            var last = serialized[^1];
            serialized = serialized[..^1] + (last == 'A' ? 'B' : 'A');
        }
        return serialized;
    }

    private sealed class FakeTimeProvider(DateTimeOffset startUtc) : TimeProvider
    {
        private DateTimeOffset current = startUtc;
        public override DateTimeOffset GetUtcNow() => current;
        public void Advance(TimeSpan delta) => current = current.Add(delta);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
