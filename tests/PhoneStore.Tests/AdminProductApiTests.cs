using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Api.Contracts.Products;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class AdminProductApiTests
{
    private const string Collection = "/api/admin/products";

    [Fact]
    public async Task All_management_operations_require_an_active_database_admin()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (customer, product) = await fixture.SeedCatalogAsync();
        var admin = await AddAdmin(fixture, false);
        using var anonymousHost = ApiAuthorizationTests.Host();
        using var anonymous = anonymousHost.CreateClient();
        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var restricted = host.CreateClient();

        await CheckBoundary(anonymous, HttpStatusCode.Unauthorized);
        foreach (var user in new[] { customer, admin })
        {
            restricted.DefaultRequestHeaders.Remove("Fixture-Subject");
            restricted.DefaultRequestHeaders.Add("Fixture-Subject", user.UserId.ToString());
            await CheckBoundary(restricted, HttpStatusCode.Forbidden);
        }

        async Task CheckBoundary(HttpClient client, HttpStatusCode expected)
        {
            var item = $"{Collection}/{product.ProductId}";
            foreach (var (method, url) in new[] { (HttpMethod.Get, Collection), (HttpMethod.Get, item),
                (HttpMethod.Post, Collection), (HttpMethod.Put, item), (HttpMethod.Patch, item + "/status") })
            {
                using var request = new HttpRequestMessage(method, url);
                using var response = await client.SendAsync(request);
                Assert.Equal(expected, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            }
        }
    }

    [Fact]
    public async Task Admin_can_manage_products_with_filters_and_original_stock_protection()
    {
        await using var fixture = await TestDatabase.CreateAsync();
        var (_, existing) = await fixture.SeedCatalogAsync();
        var admin = await AddAdmin(fixture, true);
        using var host = ApiAuthorizationTests.Host(fixture.Db);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Fixture-Subject", admin.UserId.ToString());
        var create = new ProductWriteRequest { ProductName = "Fixture phone", BrandId = existing.BrandId,
            CategoryId = existing.CategoryId, StockQuantity = 3, IsActive = true, Price = 12.50m };

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Collection, create with { Price = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Collection, create with { BrandId = int.MaxValue })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Collection}/{int.MaxValue}")).StatusCode);
        using var created = await client.PostAsJsonAsync(Collection, create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<AdminProductId>())!.ProductId;
        var item = $"{Collection}/{id}";
        Assert.Equal(item, created.Headers.Location!.AbsolutePath);
        var detail = await client.GetFromJsonAsync<AdminProductDetail>(item);
        Assert.Equal(3, detail!.OriginalStockQuantity);

        var update = new ProductUpdateRequest { ProductName = "Edited phone", Price = 20,
            BrandId = create.BrandId, CategoryId = create.CategoryId, StockQuantity = 2,
            OriginalStockQuantity = detail.OriginalStockQuantity, IsActive = true };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(item, update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(item, update with { Price = 99 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(item, create)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PatchAsJsonAsync(item + "/status", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(item + "/status", new { isActive = false })).StatusCode);
        var stored = await fixture.Db.Products.AsNoTracking().SingleAsync(p => p.ProductId == id);
        Assert.Equal(20m, stored.Price);
        Assert.Equal(2, stored.StockQuantity);
        Assert.False(stored.IsActive);
        var page = await client.GetFromJsonAsync<AdminProductPage>(
            $"{Collection}?search=Edited&brandId={create.BrandId}&categoryId={create.CategoryId}&isActive=false");
        Assert.Equal(id, Assert.Single(page!.Items).ProductId);
        page = await client.GetFromJsonAsync<AdminProductPage>($"{Collection}?page=2&pageSize=1");
        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(id, Assert.Single(page.Items).ProductId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Collection + "?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PatchAsJsonAsync(item + "/status", new { isActive = true })).StatusCode);
        Assert.True((await client.GetFromJsonAsync<AdminProductDetail>(item))!.IsActive);
    }

    private static async Task<User> AddAdmin(TestDatabase fixture, bool active)
    {
        var admin = new User { FullName = "Fixture", Email = "product-admin@test.local",
            PasswordHash = "unused", RoleId = SystemRoles.AdminId, IsActive = active };
        fixture.Db.Users.Add(admin);
        await fixture.Db.SaveChangesAsync();
        return admin;
    }
}
