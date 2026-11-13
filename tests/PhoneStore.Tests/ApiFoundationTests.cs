using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Api.Health;
using PhoneStore.Business.Catalog;
using PhoneStore.Data.Catalog;
using PhoneStore.Data.Context;

namespace PhoneStore.Tests;

public sealed class ApiFoundationTests
{
    [Fact]
    public async Task Development_api_starts_and_exposes_healthy_readiness()
    {
        await using var factory = new ApiFactory(true, Environments.Development);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", JsonDocument.Parse(json).RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Unavailable_database_returns_503_without_infrastructure_details()
    {
        await using var factory = new ApiFactory(false, Environments.Development);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("unhealthy", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localhost", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PhoneStoreDB", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connection", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Development_exposes_swagger_json_and_ui_with_health_endpoint()
    {
        await using var factory = new ApiFactory(true, Environments.Development);
        using var client = factory.CreateClient();

        var document = await client.GetAsync("/swagger/v1/swagger.json");
        var body = await document.Content.ReadAsStringAsync();
        var ui = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Contains("/api/health", body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }

    [Fact]
    public async Task Production_does_not_expose_swagger()
    {
        await using var factory = new ApiFactory(true, Environments.Production);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/index.html")).StatusCode);
    }

    [Fact]
    public void Data_access_services_are_scoped_not_singletons()
    {
        using var factory = new ApiFactory(true, Environments.Development);
        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();

        Assert.Same(first.ServiceProvider.GetRequiredService<PhoneStoreDbContext>(),
            first.ServiceProvider.GetRequiredService<PhoneStoreDbContext>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<PhoneStoreDbContext>(),
            second.ServiceProvider.GetRequiredService<PhoneStoreDbContext>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<ProductCatalogReader>(),
            second.ServiceProvider.GetRequiredService<ProductCatalogReader>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<IProductService>(),
            second.ServiceProvider.GetRequiredService<IProductService>());
    }

    [Fact]
    public async Task Exception_handler_returns_safe_problem_details()
    {
        await using var factory = new ApiFactory(true, Environments.Development);
        var problemDetails = factory.Services.GetRequiredService<IProblemDetailsService>();
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance, problemDetails);
        var context = new DefaultHttpContext
        {
            RequestServices = factory.Services,
            TraceIdentifier = "api-test-trace"
        };
        context.Request.Path = "/api/test-failure";
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context,
            new InvalidOperationException("Server=private;Database=private;Password=private"), default);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Contains("Unexpected server error", body, StringComparison.Ordinal);
        Assert.Contains("api-test-trace", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Database=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
    }

    private sealed class ApiFactory(bool databaseAvailable, string environment)
        : WebApplicationFactory<PhoneStore.Api.Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPhoneStoreReadinessProbe>();
                services.AddSingleton<IPhoneStoreReadinessProbe>(new FixedReadinessProbe(databaseAvailable));
            });
        }
    }

    private sealed class FixedReadinessProbe(bool available) : IPhoneStoreReadinessProbe
    {
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken) => Task.FromResult(available);
    }
}
