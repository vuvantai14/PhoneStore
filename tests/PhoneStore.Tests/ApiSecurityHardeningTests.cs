using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Business.Catalog;
using PhoneStore.Models.Catalog;

namespace PhoneStore.Tests;

public sealed class ApiSecurityHardeningTests
{
    [Fact]
    public async Task Catalog_rate_limit_returns_safe_429_with_retry_after()
    {
        await using var factory = new HardenedApiFactory(new FastCatalogService(), catalogLimit: 2);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/products")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/brands")).StatusCode);
        var rejected = await client.GetAsync("/api/v1/categories");
        var body = await rejected.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType?.MediaType);
        Assert.True(rejected.Headers.RetryAfter is not null);
        Assert.Contains("traceId", body, StringComparison.Ordinal);
        Assert.DoesNotContain("PhoneStoreDB", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Spoofed_forwarded_for_cannot_bypass_rate_limit()
    {
        await using var factory = new HardenedApiFactory(new FastCatalogService(), catalogLimit: 1);
        using var client = factory.CreateClient();

        using var first = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        first.Headers.Add("X-Forwarded-For", "198.51.100.10");
        using var second = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        second.Headers.Add("X-Forwarded-For", "203.0.113.20");

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(second)).StatusCode);
    }

    [Fact]
    public async Task Health_uses_an_independent_rate_limit_policy()
    {
        await using var factory = new HardenedApiFactory(new FastCatalogService(), catalogLimit: 1, healthLimit: 2);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/products")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/v1/brands")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/health")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/health")).StatusCode);
    }

    [Fact]
    public async Task Excessive_query_string_is_rejected_as_safe_400()
    {
        await using var factory = new HardenedApiFactory(new FastCatalogService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products?search=" + new string('x', 2100));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("traceId", body, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 100), body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Validated_client_correlation_id_is_returned_and_invalid_value_is_ignored()
    {
        await using var factory = new HardenedApiFactory(new FastCatalogService());
        using var client = factory.CreateClient();

        using var valid = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        valid.Headers.Add("X-Correlation-ID", "catalog-test_123");
        var validResponse = await client.SendAsync(valid);
        using var invalid = new HttpRequestMessage(HttpMethod.Get, "/api/v1/products");
        invalid.Headers.TryAddWithoutValidation("X-Correlation-ID", "bad value\r\ninjection");
        var invalidResponse = await client.SendAsync(invalid);

        Assert.Equal("catalog-test_123", validResponse.Headers.GetValues("X-Correlation-ID").Single());
        Assert.NotEqual("bad value\r\ninjection", invalidResponse.Headers.GetValues("X-Correlation-ID").Single());
    }

    [Fact]
    public async Task Slow_catalog_request_returns_504_and_cancels_service_work()
    {
        var slow = new SlowCatalogService();
        await using var factory = new HardenedApiFactory(slow, timeoutSeconds: 1);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/products");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("traceId", body, StringComparison.Ordinal);
        Assert.True(await slow.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.DoesNotContain("OperationCanceledException", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_cancellation_flows_to_catalog_service_without_fake_success()
    {
        var slow = new SlowCatalogService();
        await using var factory = new HardenedApiFactory(slow, timeoutSeconds: 10);
        using var client = factory.CreateClient();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.GetAsync("/api/v1/products", cancellation.Token));
        Assert.True(await slow.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task Request_log_excludes_query_string_and_sensitive_values()
    {
        var logger = new CapturingLogger<RequestSafetyMiddleware>();
        var middleware = new RequestSafetyMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            logger,
            new ApiOperationalOptions());
        var context = new DefaultHttpContext { TraceIdentifier = "safe-log-test" };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/v1/products";
        context.Request.QueryString = new QueryString("?password=secret-value&search=phone");

        await middleware.InvokeAsync(context);
        var logs = string.Join(Environment.NewLine, logger.Messages);

        Assert.Contains("GET", logs, StringComparison.Ordinal);
        Assert.Contains("/api/v1/products", logs, StringComparison.Ordinal);
        Assert.Contains("safe-log-test", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("password", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("search=", logs, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class HardenedApiFactory(
        IProductService service,
        int catalogLimit = 60,
        int healthLimit = 120,
        int timeoutSeconds = 10) : WebApplicationFactory<PhoneStore.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ApiOperational:CatalogRequestsPerMinute", catalogLimit.ToString());
            builder.UseSetting("ApiOperational:HealthRequestsPerMinute", healthLimit.ToString());
            builder.UseSetting("ApiOperational:CatalogTimeoutSeconds", timeoutSeconds.ToString());
            builder.UseSetting("ApiOperational:HealthTimeoutSeconds", "3");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProductService>();
                services.AddSingleton(service);
                services.RemoveAll<PhoneStore.Api.Health.IPhoneStoreReadinessProbe>();
                services.AddSingleton<PhoneStore.Api.Health.IPhoneStoreReadinessProbe>(
                    new AvailableReadinessProbe());
            });
        }
    }

    private sealed class AvailableReadinessProbe : PhoneStore.Api.Health.IPhoneStoreReadinessProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private class FastCatalogService : IProductService
    {
        public virtual Task<PagedCatalogResult> GetPagedProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedCatalogResult([], 0, query.Page, query.PageSize));
        public Task<ProductDetail?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<ProductDetail?>(null);
        public Task<IReadOnlyList<ProductCard>> GetRelatedAsync(ProductDetail product, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductCard>>([]);
        public Task<IReadOnlyList<ProductCard>> GetProductsAsync(CatalogQuery query, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductCard>>([]);
        public Task<IReadOnlyList<ProductCard>> GetNewestAsync(int count, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductCard>>([]);
        public Task<IReadOnlyList<ProductCard>> GetDiversifiedNewestAsync(int count, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductCard>>([]);
        public Task<IReadOnlyList<ProductCard>> GetBestSellersAsync(int count, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductCard>>([]);
        public Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CatalogOption>>([]);
        public Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CatalogOption>>([]);
    }

    private sealed class SlowCatalogService : FastCatalogService
    {
        public TaskCompletionSource<bool> CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<PagedCatalogResult> GetPagedProductsAsync(
            CatalogQuery query, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                return new PagedCatalogResult([], 0, query.Page, query.PageSize);
            }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult(true);
                throw;
            }
        }
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
