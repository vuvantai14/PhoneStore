using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PhoneStore.Business.Catalog;
using PhoneStore.Models.Catalog;
using PhoneStore.Web.CatalogApi;

namespace PhoneStore.Tests;

public sealed class CustomerWebCatalogApiIntegrationTests
{
    [Fact]
    public async Task Feature_flag_off_uses_local_service_without_calling_api()
    {
        var api = new FakeApi(new(CatalogApiStatus.Success, Product(99)));
        var local = new FakeProducts(Product(25));
        var result = await Resolver(false, api, local).ResolveAsync(25, default);

        Assert.Equal(ProductDetailResolutionStatus.Success, result.Status);
        Assert.Equal(25, result.Product!.ProductId);
        Assert.Equal(0, api.Calls);
        Assert.Equal(1, local.DetailCalls);
    }

    [Fact]
    public async Task Feature_flag_on_uses_complete_api_mapping_without_local_detail_fallback()
    {
        var apiProduct = Product(25);
        var api = new FakeApi(new(CatalogApiStatus.Success, apiProduct));
        var local = new FakeProducts(Product(99));
        var result = await Resolver(true, api, local).ResolveAsync(25, default);

        Assert.Same(apiProduct, result.Product);
        Assert.Equal("/uploads/products/phone.png", result.Product!.Images.Single());
        Assert.Equal("8 GB", result.Product.RAM);
        Assert.Equal(0, local.DetailCalls);
    }

    [Fact]
    public async Task Api_404_returns_not_found_without_fallback()
    {
        var local = new FakeProducts(Product(25));
        var result = await Resolver(true, new FakeApi(new(CatalogApiStatus.NotFound)), local)
            .ResolveAsync(25, default);

        Assert.Equal(ProductDetailResolutionStatus.NotFound, result.Status);
        Assert.Equal(0, local.DetailCalls);
    }

    [Theory]
    [InlineData(CatalogApiStatus.Unavailable)]
    [InlineData(CatalogApiStatus.RateLimited)]
    [InlineData(CatalogApiStatus.InvalidPayload)]
    public async Task Availability_failures_fallback_once_to_authoritative_local_service(CatalogApiStatus status)
    {
        var local = new FakeProducts(Product(25));
        var result = await Resolver(true, new FakeApi(new(status)), local).ResolveAsync(25, default);

        Assert.Equal(ProductDetailResolutionStatus.Success, result.Status);
        Assert.Equal(1, local.DetailCalls);
    }

    [Fact]
    public async Task Api_bad_request_is_not_hidden_by_fallback()
    {
        var local = new FakeProducts(Product(25));
        var result = await Resolver(true, new FakeApi(new(CatalogApiStatus.BadRequest)), local)
            .ResolveAsync(25, default);

        Assert.Equal(ProductDetailResolutionStatus.InvalidApiResponse, result.Status);
        Assert.Equal(0, local.DetailCalls);
    }

    [Fact]
    public async Task Client_cancellation_is_not_fallback()
    {
        var local = new FakeProducts(Product(25));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Resolver(true, new CancelledApi(), local).ResolveAsync(25, cancellation.Token));
        Assert.Equal(0, local.DetailCalls);
    }

    [Fact]
    public async Task Typed_client_maps_api_contract_and_keeps_relative_image_url()
    {
        const string json = """
            {"id":25,"name":"vivo Y17s","brandId":6,"brandName":"vivo","categoryId":1,
             "categoryName":"Điện thoại thông minh","price":3490000,"stockQuantity":38,
             "ram":"8 GB","storage":"128 GB","chip":"Helio","screen":"6.56 inch",
             "camera":"50 MP","battery":"5000 mAh","operatingSystem":"Android",
             "description":"Fixture","images":["/uploads/products/phone.png"]}
            """;
        var client = Client(HttpStatusCode.OK, json);
        var result = await client.GetProductAsync(25, default);

        Assert.Equal(CatalogApiStatus.Success, result.Status);
        Assert.Equal("vivo Y17s", result.Product!.ProductName);
        Assert.Equal(3490000m, result.Product.Price);
        Assert.Equal(38, result.Product.StockQuantity);
        Assert.Equal("/uploads/products/phone.png", result.Product.Images.Single());
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, CatalogApiStatus.Unavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, CatalogApiStatus.Unavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout, CatalogApiStatus.Unavailable)]
    [InlineData(HttpStatusCode.TooManyRequests, CatalogApiStatus.RateLimited)]
    [InlineData(HttpStatusCode.NotFound, CatalogApiStatus.NotFound)]
    public async Task Typed_client_classifies_http_status(HttpStatusCode http, CatalogApiStatus expected)
    {
        Assert.Equal(expected, (await Client(http, "{}").GetProductAsync(25, default)).Status);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"id\":99,\"name\":\"Wrong product\",\"brandId\":1,\"brandName\":\"Brand\",\"categoryId\":1,\"categoryName\":\"Phone\",\"price\":1,\"stockQuantity\":1,\"images\":[]}")]
    [InlineData("{\"id\":25,\"name\":\"Phone\",\"brandId\":1,\"brandName\":\"Brand\",\"categoryId\":1,\"categoryName\":\"Phone\",\"price\":1,\"stockQuantity\":1,\"images\":[\"https://external.test/a.png\"]}")]
    public async Task Invalid_payload_is_rejected(string body)
    {
        Assert.Equal(CatalogApiStatus.InvalidPayload,
            (await Client(HttpStatusCode.OK, body).GetProductAsync(25, default)).Status);
    }

    [Fact]
    public async Task Network_failure_is_classified_for_fallback()
    {
        var client = new CatalogApiClient(
            new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://catalog.test/") },
            NullLogger<CatalogApiClient>.Instance);

        Assert.Equal(CatalogApiStatus.Unavailable,
            (await client.GetProductAsync(25, default)).Status);
    }

    [Fact]
    public async Task Http_client_timeout_is_classified_for_fallback()
    {
        var http = new HttpClient(new DelayedHandler())
        {
            BaseAddress = new Uri("http://catalog.test/"),
            Timeout = TimeSpan.FromMilliseconds(50)
        };
        var client = new CatalogApiClient(http, NullLogger<CatalogApiClient>.Instance);

        Assert.Equal(CatalogApiStatus.Unavailable,
            (await client.GetProductAsync(25, default)).Status);
    }

    private static ProductDetailResolver Resolver(bool enabled, ICatalogApiClient api, IProductService products) =>
        new(Options.Create(new CatalogApiOptions { Enabled = enabled }), api, products,
            NullLogger<ProductDetailResolver>.Instance);

    private static CatalogApiClient Client(HttpStatusCode status, string body) =>
        new(new HttpClient(new FixedHandler(status, body)) { BaseAddress = new Uri("http://catalog.test/") },
            NullLogger<CatalogApiClient>.Instance);

    private static ProductDetail Product(int id) => new()
    {
        ProductId = id, ProductName = "vivo Y17s", BrandId = 6, BrandName = "vivo",
        CategoryId = 1, CategoryName = "Điện thoại thông minh", Price = 3490000m,
        StockQuantity = 38, RAM = "8 GB", Storage = "128 GB", Chip = "Helio",
        Screen = "6.56 inch", Camera = "50 MP", Battery = "5000 mAh",
        OperatingSystem = "Android", Description = "Fixture",
        Images = ["/uploads/products/phone.png"]
    };

    private sealed class FixedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("test-only network failure");
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class FakeApi(CatalogApiResult result) : ICatalogApiClient
    {
        public int Calls { get; private set; }
        public Task<CatalogApiResult> GetProductAsync(int productId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class CancelledApi : ICatalogApiClient
    {
        public Task<CatalogApiResult> GetProductAsync(int productId, CancellationToken cancellationToken) =>
            Task.FromCanceled<CatalogApiResult>(cancellationToken);
    }

    private sealed class FakeProducts(ProductDetail? detail) : IProductService
    {
        public int DetailCalls { get; private set; }
        public Task<ProductDetail?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default)
        {
            DetailCalls++;
            return Task.FromResult(detail);
        }
        public Task<IReadOnlyList<ProductCard>> GetRelatedAsync(ProductDetail product, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductCard>>([]);
        public Task<IReadOnlyList<ProductCard>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedCatalogResult> GetPagedProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetNewestAsync(int count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetDiversifiedNewestAsync(int count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProductCard>> GetBestSellersAsync(int count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
