using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhoneStore.Business.Catalog;
using PhoneStore.Data.Context;
using PhoneStore.Models.Catalog;

namespace PhoneStore.Tests;

// Real MVC binding and Razor rendering; deterministic catalog double, no live SQL.
public sealed class CatalogPaginationHttpTests
{
    [Fact]
    public async Task Valid_pages_return_distinct_ten_item_pages()
    {
        await using var app = new CatalogApp();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        foreach (var page in new[] { 1, 2, 3 })
        {
            var html = await ReadAsync(client, $"/products?page={page}");
            Assert.Equal(Enumerable.Range((page - 1) * 10 + 1, 10).Select(i => $"Fixture {i:D2}"), Names(html));
            Assert.Equal(page, app.Catalog.LastQuery!.Page);
            Assert.Equal(10, app.Catalog.LastQuery.PageSize);
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("2147483648")]
    [InlineData("")]
    public async Task Invalid_pages_fall_back_to_first_page(string page)
    {
        await using var app = new CatalogApp();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var html = await ReadAsync(client, $"/products?page={page}");
        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"Fixture {i:D2}"), Names(html));
        Assert.Equal(1, app.Catalog.LastQuery!.Page);
    }

    [Theory]
    [InlineData("search=Fixture", "Fixture 11")]
    [InlineData("brandIds=7", "Fixture 11")]
    [InlineData("categoryId=9", "Fixture 11")]
    [InlineData("sort=price-desc", "Fixture 38")]
    [InlineData("search=Fixture&brandIds=7&categoryId=9&sort=price-desc", "Fixture 26")]
    public async Task Filters_and_sort_survive_real_binding_and_next_page_links(string query, string firstName)
    {
        await using var app = new CatalogApp();
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var html = await ReadAsync(client, $"/products?{query}&page=2");
        Assert.Equal(10, Names(html).Length);
        Assert.Equal(firstName, Names(html)[0]);
        var links = Regex.Matches(html, "href=\"([^\"]+)\"")
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value));
        var next = Assert.Single(links.Where(link => link.StartsWith("/products?") && link.Contains("page=3")).Distinct());
        foreach (var parameter in query.Split('&')) Assert.Contains(parameter, next);
        var nextHtml = await ReadAsync(client, next);
        Assert.Equal(3, app.Catalog.LastQuery!.Page);
        Assert.NotEqual(Names(html)[0], Names(nextHtml)[0]);
    }

    private static async Task<string> ReadAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static string[] Names(string html) => Regex.Matches(html, @"Fixture \d{2}")
        .Select(m => m.Value).Distinct().ToArray();

    private sealed class CatalogApp : WebApplicationFactory<global::Program>
    {
        public FixtureCatalog Catalog { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<IProductService>();
                services.AddSingleton<IProductService>(Catalog);
                services.RemoveAll<DbContextOptions<PhoneStoreDbContext>>();
                services.RemoveAll<PhoneStoreDbContext>();
                services.AddDbContext<PhoneStoreDbContext>(options => options.UseSqlite("Data Source=:memory:"));
            });
        }
    }

    private sealed class FixtureCatalog : IProductService
    {
        public CatalogQuery? LastQuery { get; private set; }
        public Task<PagedCatalogResult> GetPagedProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            IEnumerable<int> ids = Enumerable.Range(1, 48);
            if (query.Search is not null) ids = ids.Where(i => i <= 36);
            if (query.BrandIds?.Count > 0) ids = ids.Where(i => i <= 36 && query.BrandIds.Contains(7));
            if (query.CategoryId.HasValue) ids = ids.Where(i => i <= 36 && query.CategoryId == 9);
            if (query.Sort == "price-desc") ids = ids.OrderDescending();
            var total = ids.Count();
            var page = Math.Clamp(query.Page, 1, Math.Max(1, (total + query.PageSize - 1) / query.PageSize));
            var cards = ids.Skip((page - 1) * query.PageSize).Take(query.PageSize)
                .Select(i => new ProductCard(i, $"Fixture {i:D2}", "Test Brand", "8 GB", "128 GB", i * 1000m, 5, null)).ToArray();
            return Task.FromResult(new PagedCatalogResult(cards, total, page, query.PageSize));
        }
        public Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogOption>>([new(7, "Test Brand")]);
        public Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogOption>>([new(9, "Test Category")]);
        public Task<ProductDetail?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetRelatedAsync(ProductDetail product, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetNewestAsync(int count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetDiversifiedNewestAsync(int count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetBestSellersAsync(int count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
