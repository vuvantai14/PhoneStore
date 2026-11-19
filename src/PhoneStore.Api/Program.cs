using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Health;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Business.Management;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Purchasing;
using PhoneStore.Data.Management;
using PhoneStore.Business.Catalog;
using PhoneStore.Data.Catalog;
using PhoneStore.Data.Context;

namespace PhoneStore.Api;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        if (builder.Environment.IsDevelopment())
        {
            // Development/test hosts may not be allowed to write to Windows Event Log.
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.AddDebug();
        }
        // The framework middleware logs the full exception before the custom handler runs.
        // Suppress that copy; ApiExceptionHandler records only exception type + trace id.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.Critical);
        var connection = builder.Configuration.GetConnectionString("PhoneStoreConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("PhoneStoreConnection configuration is required.");
        var operational = builder.Configuration.GetSection("ApiOperational")
            .Get<ApiOperationalOptions>() ?? new ApiOperationalOptions();
        ValidateOperationalOptions(operational);
        builder.WebHost.ConfigureKestrel(options =>
            options.Limits.MaxRequestLineSize = operational.MaxRequestLineSize);

        builder.Services.AddControllers();
        builder.Services.AddPhoneStoreApiAccess(builder.Configuration);
        builder.Services.AddSingleton(operational);
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddRequestTimeouts(options =>
        {
            options.AddPolicy(ApiPolicies.CatalogTimeout, CreateTimeoutPolicy(operational.CatalogTimeoutSeconds));
            options.AddPolicy(ApiPolicies.HealthTimeout, CreateTimeoutPolicy(operational.HealthTimeoutSeconds));
        });
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                    ? Math.Max(1, (int)Math.Ceiling(value.TotalSeconds))
                    : (int?)null;
                await RequestSafetyMiddleware.WriteProblemAsync(context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "Too many requests",
                    "The request rate limit has been exceeded. Please try again later.",
                    retryAfter);
            };
            options.AddPolicy(ApiPolicies.CatalogRateLimit, context =>
                CreateFixedWindowPartition(context, operational.CatalogRequestsPerMinute));
            options.AddPolicy(ApiPolicies.HealthRateLimit, context =>
                CreateFixedWindowPartition(context, operational.HealthRequestsPerMinute));
        });
        builder.Services.AddDbContext<PhoneStoreDbContext>(options => options.UseSqlServer(connection));
        builder.Services.AddScoped<PurchaseStore>();
        builder.Services.AddScoped<IOrderService, OrderService>();
        builder.Services.AddScoped<OrderManagementStore>();
        builder.Services.AddScoped<OrderManagementService>();
        builder.Services.AddScoped<ProductManagementStore>();
        builder.Services.AddScoped<ProductEditorService>();
        builder.Services.AddScoped<ProductCatalogReader>();
        builder.Services.AddScoped<IProductService, ProductService>();
        builder.Services.AddScoped<IPhoneStoreReadinessProbe, PhoneStoreReadinessProbe>();
        builder.Services.AddHealthChecks()
            .AddCheck<PhoneStoreDatabaseHealthCheck>(HealthCheckNames.Database);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PhoneStore API",
                Version = "v1",
                Description = "Public catalog and health endpoints. Admin management requires an active Admin account; client authentication is not enabled."
            });
        });

        var app = builder.Build();

        app.UseExceptionHandler();
        app.UseMiddleware<RequestSafetyMiddleware>();
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRouting();
        app.UseRateLimiter();
        app.UseRequestTimeouts();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers().Add(endpoint =>
        {
            if (!endpoint.Metadata.OfType<IAllowAnonymous>().Any() && !endpoint.Metadata.OfType<IAuthorizeData>().Any())
                endpoint.Metadata.Add(new AuthorizeAttribute());
        });
        app.Run();
    }

    private static RequestTimeoutPolicy CreateTimeoutPolicy(int seconds) => new()
    {
        Timeout = TimeSpan.FromSeconds(seconds),
        TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
        WriteTimeoutResponse = context => RequestSafetyMiddleware.WriteProblemAsync(context,
            StatusCodes.Status504GatewayTimeout,
            "Request timed out",
            "The request exceeded the allowed processing time.")
    };

    private static RateLimitPartition<string> CreateFixedWindowPartition(HttpContext context, int requestsPerMinute)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = requestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    private static void ValidateOperationalOptions(ApiOperationalOptions options)
    {
        if (options.MaxQueryStringLength is < 256 or > 8192 ||
            options.MaxRequestLineSize is < 1024 or > 16384 ||
            options.MaxQueryStringLength >= options.MaxRequestLineSize ||
            options.CatalogTimeoutSeconds is < 1 or > 120 ||
            options.HealthTimeoutSeconds is < 1 or > 30 ||
            options.CatalogRequestsPerMinute is < 1 or > 10000 ||
            options.HealthRequestsPerMinute is < 1 or > 10000)
        {
            throw new InvalidOperationException("ApiOperational configuration is invalid.");
        }
    }
}
