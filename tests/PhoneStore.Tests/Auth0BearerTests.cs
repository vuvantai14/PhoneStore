using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using PhoneStore.Api.Authorization;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class Auth0BearerTests
{
    private const string Issuer = "https://issuer.example.test/";
    private const string Audience = "https://orders.example.test";

    [Theory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("audienceSlash")]
    [InlineData("signature")]
    [InlineData("unknownKey")]
    [InlineData("algorithm")]
    [InlineData("idToken")]
    [InlineData("noSubject")]
    [InlineData("machine")]
    [InlineData("otherClient")]
    [InlineData("unmapped")]
    [InlineData("ambiguous")]
    [InlineData("invalidLocalId")]
    public async Task Invalid_or_unlinked_identity_is_challenged(string scenario)
    {
        using var rsa = RSA.Create(2048);
        using var wrong = RSA.Create(2048);
        var lookup = new Lookup(scenario switch { "ambiguous" => [1, 2], "invalidLocalId" => [0], _ => [] });
        using var factory = Host(rsa, lookup);
        using var client = factory.CreateClient();
        if (scenario != "missing") client.DefaultRequestHeaders.Authorization = new("Bearer", Token(
            scenario == "signature" ? wrong : rsa, scenario));
        using var response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
        Assert.Equal(scenario is "unmapped" or "ambiguous" or "invalidLocalId" ? 1 : 0, lookup.Calls);
    }

    [Fact]
    public async Task Signed_customer_reads_only_own_orders_and_local_role_and_status_apply()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, _) = await fixture.SeedCatalogAsync();
        var other = new User { Email = "other@example.test", FullName = "Other", PasswordHash = "unused", RoleId = 2 };
        Order NewOrder(User owner, string code) => new() { User = owner, OrderCode = code, ReceiverName = "Fixture",
            ReceiverPhone = "000", ShippingAddress = "Fixture", OrderDate = DateTime.UtcNow };
        var own = NewOrder(customer, "OWN");
        var hidden = NewOrder(other, "OTHER");
        fixture.Db.AddRange(own, hidden);
        await fixture.Db.SaveChangesAsync();
        using var rsa = RSA.Create(2048);
        using var factory = Host(rsa, new Lookup([customer.UserId]), fixture.Db);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(rsa));
        var page = await client.GetFromJsonAsync<PhoneStore.Api.Contracts.Orders.OrderPage>("/api/orders");
        Assert.Equal(own.OrderId, Assert.Single(page!.Items).OrderId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/orders/{own.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/orders/{hidden.OrderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/orders")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(rsa, "noScope"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/orders")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(rsa));
        customer.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/orders")).StatusCode);
    }

    [Fact]
    public async Task Signed_token_without_provisioned_lookup_stays_closed()
    {
        using var rsa = RSA.Create(2048);
        using var factory = Host(rsa, null);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token(rsa));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/orders")).StatusCode);
    }

    private static WebApplicationFactory<PhoneStore.Api.Program> Host(RSA rsa, Lookup? lookup, PhoneStoreDbContext? db = null)
        => new WebApplicationFactory<PhoneStore.Api.Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiAuthentication:Mode"] = "Disabled",
                ["ApiAuthentication:Oidc:Enabled"] = "true", ["ApiAuthentication:Oidc:Issuer"] = Issuer,
                ["ApiAuthentication:Oidc:Audience"] = Audience, ["ApiAuthentication:Oidc:CustomerClientId"] = "customer-fixture"
            }));
            builder.ConfigureServices(services =>
            {
                if (lookup is not null) services.AddSingleton<IVerifiedIdentityLookup>(lookup);
                if (db is not null)
                {
                    services.RemoveAll<PhoneStoreDbContext>();
                    services.AddSingleton(db);
                }
                services.Configure<JwtBearerOptions>("Bearer", o => o.BackchannelHttpHandler = new Discovery(rsa));
            });
        });

    private sealed class Discovery(RSA rsa) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var key = rsa.ExportParameters(false);
            object body = request.RequestUri!.AbsoluteUri switch
            {
                Issuer + ".well-known/openid-configuration" => new { issuer = Issuer, jwks_uri = Issuer + "keys" },
                Issuer + "keys" => new { keys = new[] { new { kty = "RSA", kid = "fixture",
                    n = Base64UrlEncoder.Encode(key.Modulus!), e = Base64UrlEncoder.Encode(key.Exponent!) } } },
                _ => throw new InvalidOperationException("Unexpected discovery request")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
        }
    }

    private sealed class Lookup(int[] ids) : IVerifiedIdentityLookup
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<int>> FindAsync(string issuer, string subject, CancellationToken ct)
        {
            Assert.Equal(Issuer, issuer);
            Assert.Equal("auth0|fixture", subject);
            Calls++;
            return Task.FromResult<IReadOnlyList<int>>(ids);
        }
    }

    private static string Token(RSA rsa, string scenario = "valid")
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim> { new("role", "Admin"), new(ClaimTypes.NameIdentifier, "999"),
            new("client_id", scenario == "otherClient" ? "machine-fixture" : "customer-fixture"),
            new("scope", scenario == "noScope" ? "openid" : "openid orders:read") };
        if (scenario != "noSubject") claims.Add(new("sub", scenario == "machine" ? "app@clients" : "auth0|fixture"));
        var key = new RsaSecurityKey(rsa) { KeyId = scenario == "unknownKey" ? "missing" : "fixture" };
        var token = new JwtSecurityToken(scenario == "issuer" ? "https://other.example.test/" : Issuer,
            scenario switch { "audience" => "other", "audienceSlash" => Audience + "/", _ => Audience }, claims,
            now.AddHours(-2), scenario == "expired" ? now.AddHours(-1) : now.AddMinutes(5),
            new SigningCredentials(key, scenario == "algorithm" ? SecurityAlgorithms.RsaSha384 : SecurityAlgorithms.RsaSha256));
        token.Header["typ"] = scenario == "idToken" ? "JWT" : "at+jwt";
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
