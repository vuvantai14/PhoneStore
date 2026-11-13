using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PhoneStore.Api.Health;
using PhoneStore.Api.Mapping;
using PhoneStore.Business.Catalog;
using PhoneStore.Data.Catalog;
using PhoneStore.Models.Catalog;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

public sealed class CatalogApiTests
{
    [Fact]
    public async Task Products_use_default_pagination_and_camel_case_public_contract()
    {
        await using var factory = new CatalogApiFactory(new FixtureProductService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products");
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(10, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(3, root.GetProperty("totalItems").GetInt32());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt32());
        Assert.Equal(3, root.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain("ProductId", body, StringComparison.Ordinal);
        Assert.DoesNotContain("isActive", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("createdAt", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("orderDetails", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
    }

    public static IEnumerable<object[]> InvalidQueries()
    {
        yield return ["page=0"];
        yield return ["pageSize=0"];
        yield return ["pageSize=51"];
        yield return ["search=" + new string('x', 101)];
        yield return ["brandId=0"];
        yield return ["categoryId=-1"];
        yield return ["minPrice=-1"];
        yield return ["maxPrice=-1"];
        yield return ["minPrice=20&maxPrice=10"];
        yield return ["sort=unsupported"];
    }

    [Theory]
    [MemberData(nameof(InvalidQueries))]
    public async Task Invalid_product_query_returns_validation_problem(string query)
    {
        await using var factory = new CatalogApiFactory(new FixtureProductService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products?" + query);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("errors", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Filtering_sorting_and_pagination_map_to_existing_catalog_behavior()
    {
        await using var factory = new CatalogApiFactory(new FixtureProductService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/api/v1/products?brandId=2&inStockOnly=true&sort=price-desc&page=1&pageSize=1");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, root.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, root.GetProperty("totalPages").GetInt32());
        var item = root.GetProperty("items")[0];
        Assert.Equal(25, item.GetProperty("id").GetInt32());
        Assert.Equal(3490000m, item.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task Product_detail_returns_dto_404_for_missing_or_inactive_and_400_for_invalid_id()
    {
        await using var factory = new CatalogApiFactory(new FixtureProductService());
        using var client = factory.CreateClient();

        var found = await client.GetAsync("/api/v1/products/25");
        var foundBody = await found.Content.ReadAsStringAsync();
        using var foundJson = JsonDocument.Parse(foundBody);
        var missing = await client.GetAsync("/api/v1/products/404");
        var inactive = await client.GetAsync("/api/v1/products/99");
        var zero = await client.GetAsync("/api/v1/products/0");
        var malformed = await client.GetAsync("/api/v1/products/not-an-id");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(25, foundJson.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("vivo", foundJson.RootElement.GetProperty("brandName").GetString());
        Assert.Equal("/uploads/products/vivo-y17s.png", foundJson.RootElement.GetProperty("images")[0].GetString());
        Assert.DoesNotContain("isActive", foundBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, inactive.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    [Fact]
    public async Task Brands_categories_and_empty_product_query_return_200()
    {
        await using var factory = new CatalogApiFactory(new FixtureProductService());
        using var client = factory.CreateClient();

        var brands = await client.GetAsync("/api/v1/brands");
        var categories = await client.GetAsync("/api/v1/categories");
        var empty = await client.GetAsync("/api/v1/products?search=does-not-exist");
        var brandsBody = await brands.Content.ReadAsStringAsync();
        var categoriesBody = await categories.Content.ReadAsStringAsync();
        using var emptyJson = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, brands.StatusCode);
        Assert.Contains("\"id\":2", brandsBody, StringComparison.Ordinal);
        Assert.DoesNotContain("description", brandsBody, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);
        Assert.Contains("\"name\":\"Phone\"", categoriesBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Equal(0, emptyJson.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(0, emptyJson.RootElement.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public void Image_contract_keeps_valid_relative_paths_and_rejects_physical_or_external_paths()
    {
        Assert.Equal("/uploads/products/phone.png",
            CatalogContractMapper.SafeRelativeImageUrl("/uploads/products/phone.png"));
        Assert.Equal("uploads/products/phone.png",
            CatalogContractMapper.SafeRelativeImageUrl("uploads/products/phone.png"));
        Assert.Null(CatalogContractMapper.SafeRelativeImageUrl(@"C:\private\phone.png"));
        Assert.Null(CatalogContractMapper.SafeRelativeImageUrl(@"\\server\share\phone.png"));
        Assert.Null(CatalogContractMapper.SafeRelativeImageUrl("file:///private/phone.png"));
        Assert.Null(CatalogContractMapper.SafeRelativeImageUrl("https://example.test/phone.png"));
        Assert.Null(CatalogContractMapper.SafeRelativeImageUrl("../private/phone.png"));
    }

    [Fact]
    public async Task Real_catalog_reader_excludes_inactive_products_brands_and_categories()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (_, active) = await database.SeedCatalogAsync();
        active.ProductImages.Add(new ProductImage
        {
            ImageUrl = "/uploads/products/active.png", IsPrimary = true, DisplayOrder = 0
        });
        database.Db.Products.Add(new Product
        {
            ProductName = "Inactive fixture", Price = 1000, StockQuantity = 1, IsActive = false,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            Brand = new Brand { BrandName = "Inactive Brand", IsActive = false },
            Category = new Category { CategoryName = "Inactive Category", IsActive = false }
        });
        await database.Db.SaveChangesAsync();
        var reader = new ProductCatalogReader(database.Db);

        var products = await reader.GetPagedProductsAsync(new CatalogQuery(PageSize: 50));
        var brands = await reader.GetBrandsAsync();
        var categories = await reader.GetCategoriesAsync();

        Assert.Single(products.Items);
        Assert.Equal(active.ProductId, products.Items[0].ProductId);
        Assert.DoesNotContain(brands, option => option.Name == "Inactive Brand");
        Assert.DoesNotContain(categories, option => option.Name == "Inactive Category");
        var inactiveId = await database.Db.Products.Where(product => !product.IsActive)
            .Select(product => product.ProductId).SingleAsync();
        Assert.Null(await reader.GetProductDetailAsync(inactiveId));
    }

    [Fact]
    public async Task OpenApi_describes_catalog_and_health_endpoints()
    {
        await using var factory = new CatalogApiFactory(new FixtureProductService());
        using var client = factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var paths = json.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/health", out _));
        Assert.True(paths.TryGetProperty("/api/v1/products", out _));
        Assert.True(paths.TryGetProperty("/api/v1/products/{id}", out _));
        Assert.True(paths.TryGetProperty("/api/v1/brands", out _));
        Assert.True(paths.TryGetProperty("/api/v1/categories", out _));
        Assert.Equal(5, paths.EnumerateObject().Count());
    }

    [Fact]
    public async Task Database_exception_returns_safe_503_problem_details()
    {
        await using var factory = new CatalogApiFactory(new UnavailableProductService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Service temporarily unavailable", body, StringComparison.Ordinal);
        Assert.DoesNotContain("private-server", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(TestDatabaseException), body, StringComparison.Ordinal);
    }

    private sealed class CatalogApiFactory(IProductService service)
        : WebApplicationFactory<PhoneStore.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProductService>();
                services.AddSingleton(service);
                services.RemoveAll<IPhoneStoreReadinessProbe>();
                services.AddSingleton<IPhoneStoreReadinessProbe>(new HealthyProbe());
            });
        }
    }

    private sealed class HealthyProbe : IPhoneStoreReadinessProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed record FixtureItem(int Id, string Name, int BrandId, string BrandName,
        int CategoryId, decimal Price, int Stock, string? ImageUrl);

    private sealed class FixtureProductService : IProductService
    {
        private static readonly FixtureItem[] Items =
        [
            new(25, "vivo Y17s", 2, "vivo", 1, 3490000m, 38, "/uploads/products/vivo-y17s.png"),
            new(26, "vivo V29", 2, "vivo", 1, 2990000m, 4, "uploads/products/vivo-v29.png"),
            new(27, "Apple fixture", 1, "Apple", 1, 24990000m, 0, @"C:\private\do-not-expose.png")
        ];

        public Task<PagedCatalogResult> GetPagedProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default)
        {
            IEnumerable<FixtureItem> filtered = Items;
            if (!string.IsNullOrWhiteSpace(query.Search))
                filtered = filtered.Where(item => item.Name.Contains(query.Search, StringComparison.OrdinalIgnoreCase)
                    || item.BrandName.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
            if (query.BrandId.HasValue) filtered = filtered.Where(item => item.BrandId == query.BrandId.Value);
            if (query.CategoryId.HasValue) filtered = filtered.Where(item => item.CategoryId == query.CategoryId.Value);
            if (query.MinPrice.HasValue) filtered = filtered.Where(item => item.Price >= query.MinPrice.Value);
            if (query.MaxPrice.HasValue) filtered = filtered.Where(item => item.Price <= query.MaxPrice.Value);
            if (query.InStockOnly) filtered = filtered.Where(item => item.Stock > 0);
            filtered = query.Sort switch
            {
                "price-asc" => filtered.OrderBy(item => item.Price).ThenBy(item => item.Id),
                "price-desc" => filtered.OrderByDescending(item => item.Price).ThenBy(item => item.Id),
                "name-asc" => filtered.OrderBy(item => item.Name).ThenBy(item => item.Id),
                _ => filtered.OrderByDescending(item => item.Id)
            };
            var all = filtered.ToArray();
            var pageItems = all.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
                .Select(Card).ToArray();
            return Task.FromResult(new PagedCatalogResult(pageItems, all.Length, query.Page, query.PageSize));
        }

        public Task<ProductDetail?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default)
        {
            if (id == 99) return Task.FromResult<ProductDetail?>(null);
            var item = Items.SingleOrDefault(product => product.Id == id);
            return Task.FromResult(item is null ? null : new ProductDetail
            {
                ProductId = item.Id, ProductName = item.Name,
                BrandId = item.BrandId, BrandName = item.BrandName,
                CategoryId = item.CategoryId, CategoryName = "Phone",
                Price = item.Price, StockQuantity = item.Stock, RAM = "8 GB", Storage = "128 GB",
                Chip = "Fixture chip", Screen = "Fixture screen", Camera = "Fixture camera",
                Battery = "5000 mAh", OperatingSystem = "Fixture OS", Description = "Fixture description",
                Images = item.ImageUrl is null ? [] : [item.ImageUrl]
            });
        }

        public Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogOption>>([new(1, "Apple"), new(2, "vivo")]);

        public Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogOption>>([new(1, "Phone")]);

        private static ProductCard Card(FixtureItem item) => new(item.Id, item.Name, item.BrandName,
            "8 GB", "128 GB", item.Price, item.Stock, item.ImageUrl);

        public Task<IReadOnlyList<ProductCard>> GetRelatedAsync(ProductDetail product, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductCard>>([]);
        public Task<IReadOnlyList<ProductCard>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetNewestAsync(int count, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetDiversifiedNewestAsync(int count, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetBestSellersAsync(int count, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnavailableProductService : IProductService
    {
        private static Task<T> Unavailable<T>() => Task.FromException<T>(new TestDatabaseException());
        public Task<PagedCatalogResult> GetPagedProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) => Unavailable<PagedCatalogResult>();
        public Task<ProductDetail?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default) => Unavailable<ProductDetail?>();
        public Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<CatalogOption>>();
        public Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<CatalogOption>>();
        public Task<IReadOnlyList<ProductCard>> GetRelatedAsync(ProductDetail product, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<ProductCard>>();
        public Task<IReadOnlyList<ProductCard>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<ProductCard>>();
        public Task<IReadOnlyList<ProductCard>> GetNewestAsync(int count, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<ProductCard>>();
        public Task<IReadOnlyList<ProductCard>> GetDiversifiedNewestAsync(int count, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<ProductCard>>();
        public Task<IReadOnlyList<ProductCard>> GetBestSellersAsync(int count, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<ProductCard>>();
    }

    private sealed class TestDatabaseException : DbException
    {
        public override string Message => "private-server database failure";
    }
}
