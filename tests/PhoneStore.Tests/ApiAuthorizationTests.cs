using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PhoneStore.Api.Authorization;
using PhoneStore.Data.Context;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class ApiAuthorizationTests
{
    [Fact]
    public async Task Unconfigured_api_challenges_even_when_credentials_are_present()
    {
        using var factory = Host();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer fixture");
        client.DefaultRequestHeaders.Add("Cookie", "PhoneStore.Customer=fixture");
        using var response = await client.GetAsync("/test-api-access/admin");
        await AssertDenial(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Policies_use_current_database_role_and_active_status_on_every_request()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, _) = await fixture.SeedCatalogAsync();
        var admin = new User { FullName = "Fixture", Email = "api-admin@test.local",
            PasswordHash = "unused", RoleId = SystemRoles.AdminId, IsActive = true };
        fixture.Db.Users.Add(admin);
        await fixture.Db.SaveChangesAsync();
        using var factory = Host(fixture.Db);
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Add("Fixture-Subject", customer.UserId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/test-api-access/customer")).StatusCode);
        await AssertDenial(await client.GetAsync("/test-api-access/admin"), HttpStatusCode.Forbidden);
        customer.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        await AssertDenial(await client.GetAsync("/test-api-access/customer"), HttpStatusCode.Forbidden);

        client.DefaultRequestHeaders.Remove("Fixture-Subject");
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/test-api-access/admin")).StatusCode);
        await AssertDenial(await client.GetAsync("/test-api-access/customer"), HttpStatusCode.Forbidden);
        await AssertDenial(await client.GetAsync("/test-api-access/unclassified"), HttpStatusCode.Forbidden);
        admin.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        await AssertDenial(await client.GetAsync("/test-api-access/admin"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Candidate_provider_settings_never_enable_bearer_authentication()
    {
        const string issuer = "https://issuer.example.test";
        const string audience = "phone-store";
        using var host = Host().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiAuthentication:Oidc:Issuer"] = issuer,
                ["ApiAuthentication:Oidc:Audience"] = audience
            })));
        using var client = host.CreateClient();
        var key = new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32));
        var signing = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;
        string Token(string iss, string aud, DateTime expiry) => new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(iss, aud, [new Claim("sub", "1")], now.AddHours(-2), expiry, signing));

        foreach (var bearer in new[] { null, "invalid", Token(issuer, audience, now.AddHours(-1)),
            Token("https://other.example.test", audience, now.AddHours(1)),
            Token(issuer, "other-api", now.AddHours(1)), Token(issuer, audience, now.AddHours(1)) })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/test-api-access/customer");
            if (bearer is not null) request.Headers.Authorization = new("Bearer", bearer);
            using var response = await client.SendAsync(request);
            await AssertDenial(response, HttpStatusCode.Unauthorized);
        }
    }

    [Theory]
    [InlineData(null, "api")]
    [InlineData("https://issuer.example.test", null)]
    [InlineData("http://issuer.example.test", "api")]
    [InlineData("https://issuer.example.test?query=1", "api")]
    [InlineData("https://issuer.example.test/path#fragment", "api")]
    [InlineData("https://user@issuer.example.test", "api")]
    public void Invalid_oidc_configuration_rejects_startup(string? issuer, string? audience)
    {
        using var host = Host().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiAuthentication:Oidc:Issuer"] = issuer,
                ["ApiAuthentication:Oidc:Audience"] = audience
            })));
        Assert.Throws<OptionsValidationException>(() => host.CreateClient());
    }

    internal static WebApplicationFactory<PhoneStore.Api.Program> Host(PhoneStoreDbContext? db = null) =>
        new WebApplicationFactory<PhoneStore.Api.Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(ApiAccessProbeController).Assembly);
                if (db is null) return;
                services.RemoveAll<PhoneStoreDbContext>();
                services.AddSingleton(db);
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FixtureIdentity>("fixture", _ => { });
                services.Configure<AuthenticationSchemeOptions>(ApiAccess.Scheme,
                    options => options.ForwardAuthenticate = "fixture");
            }));

    private static async Task AssertDenial(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("traceId").GetString()));
    }

    private sealed class FixtureIdentity(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([
                new Claim("scope", "orders:read"),
                new Claim(ClaimTypes.NameIdentifier, Request.Headers["Fixture-Subject"].ToString()),
                new Claim(ClaimTypes.Role, SystemRoles.Admin)], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }
}

// Discovered only by the test host that explicitly adds this assembly.
[Route("test-api-access")]
public sealed class ApiAccessProbeController : ControllerBase
{
    [HttpGet("customer"), Authorize(Policy = ApiAccess.Customer)]
    public IActionResult Customer() => Ok();

    [HttpGet("admin"), Authorize(Policy = ApiAccess.Admin)]
    public IActionResult Admin() => Ok();

    [HttpGet("unclassified")]
    public IActionResult Unclassified() => Ok();
}
