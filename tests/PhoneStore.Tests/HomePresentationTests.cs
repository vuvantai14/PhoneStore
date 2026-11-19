using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhoneStore.Business.Catalog;
using PhoneStore.Data.Context;
using PhoneStore.Models.Catalog;

namespace PhoneStore.Tests;

public sealed class HomePresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Home_renders_full_brand_names_and_shows_bestsellers_only_when_present(bool hasBestSellers)
    {
        await using var app = new HomeApp(hasBestSellers);
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.GetAsync("/");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("class=\"brand-name\">OnePlus</span>", html);
        Assert.Contains("href=\"/products?brandId=7\"", html);
        Assert.DoesNotContain("brand-logo-fallback", html);
        Assert.Contains("home-hero-button\" href=\"/products\"", html);
        Assert.Contains("action=\"/cart/add\"", html);
        Assert.Contains("home-product-card-v1", html);
        Assert.Contains("class=\"product-spec-tag\" title=\"RAM 8 GB\"", html);
        Assert.Contains("class=\"product-spec-tag\" title=\"Bộ nhớ 256 GB\"", html);
        Assert.DoesNotContain("Mới", html);
        Assert.DoesNotContain("Yêu thích", html);
        Assert.DoesNotContain("home-best-sellers-empty", html);
        Assert.Equal(12, app.Catalog.RequestedNewestCount);
        if (hasBestSellers)
        {
            Assert.Contains("id=\"best-sellers-title\"", html);
            Assert.Contains("Best seller fixture", html);
        }
        else
        {
            Assert.DoesNotContain("best-sellers-title", html);
            Assert.DoesNotContain("Best seller fixture", html);
        }
    }

    private sealed class HomeApp(bool hasBestSellers) : WebApplicationFactory<global::Program>
    {
        public FixtureCatalog Catalog { get; } = new(hasBestSellers);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<IProductService>();
                services.AddSingleton<IProductService>(Catalog);
                // Cookie middleware constructs the auth services even for anonymous GETs.
                // Keep that DI graph valid but replace SQL Server with a private memory provider.
                services.RemoveAll<DbContextOptions<PhoneStoreDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<PhoneStoreDbContext>>();
                services.RemoveAll<PhoneStoreDbContext>();
                services.AddDbContext<PhoneStoreDbContext>(options => options.UseSqlite("Data Source=:memory:"));
            });
        }
    }

    private sealed class FixtureCatalog(bool hasBestSellers) : IProductService
    {
        public int RequestedNewestCount { get; private set; }

        private static ProductCard Product(string name) => new(25, name, "OnePlus", "8 GB", "256 GB", 3490000m, 10, null);
        public Task<IReadOnlyList<ProductCard>> GetDiversifiedNewestAsync(int count, CancellationToken cancellationToken = default)
        {
            RequestedNewestCount = count;
            return Task.FromResult<IReadOnlyList<ProductCard>>([Product("Newest fixture")]);
        }
        public Task<IReadOnlyList<ProductCard>> GetBestSellersAsync(int count, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductCard>>(hasBestSellers ? [Product("Best seller fixture")] : []);
        public Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogOption>>([new(7, "OnePlus")]);
        public Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductDetail?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetRelatedAsync(ProductDetail product, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedCatalogResult> GetPagedProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetNewestAsync(int count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
