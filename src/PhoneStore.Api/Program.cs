using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Health;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Business.Authentication;
using PhoneStore.Business.Reporting;
using PhoneStore.Data.Authentication;
using PhoneStore.Data.Reporting;
using PhoneStore.Business.Management;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Purchasing;
using PhoneStore.Data.Management;
using PhoneStore.Business.Catalog;
using PhoneStore.Data.Catalog;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

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
        {
            options.Limits.MaxRequestLineSize = operational.MaxRequestLineSize;
            options.Limits.MaxRequestBodySize = Math.Max(operational.MaxRequestBodyBytes, operational.MaxImageUploadBytes);
        });

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
            options.AddPolicy(ApiPolicies.AuthTimeout, CreateTimeoutPolicy(operational.AuthTimeoutSeconds));
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
            options.AddPolicy(ApiPolicies.AuthRateLimit, context =>
                CreateFixedWindowPartition(context, operational.AuthRequestsPerMinute));
            options.AddPolicy(ApiPolicies.ProtectedRateLimit, context =>
                CreateFixedWindowPartition(context, operational.ProtectedRequestsPerMinute));
        });
        builder.Services.AddDbContext<PhoneStoreDbContext>(options => options.UseSqlServer(connection));
        builder.Services.AddScoped<AuthUserStore>();
        builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
        builder.Services.AddScoped<CustomerProfileStore>();
        builder.Services.AddScoped<ICustomerProfileService, CustomerProfileService>();
        builder.Services.AddScoped<PurchaseStore>();
        builder.Services.AddScoped<IOrderService, OrderService>();
        builder.Services.AddScoped<ReportingStore>();
        builder.Services.AddScoped<IReportingStore>(sp => sp.GetRequiredService<ReportingStore>());
        builder.Services.AddScoped<ReportSectionReader>();
        builder.Services.AddScoped<IReportingService, ReportingService>();
        builder.Services.AddScoped<CustomerManagementStore>();
        builder.Services.AddScoped<CustomerManagementService>();
        builder.Services.AddScoped<ICustomerManagementService>(sp => sp.GetRequiredService<CustomerManagementService>());
        builder.Services.AddScoped<OrderManagementStore>();
        builder.Services.AddScoped<OrderManagementService>();
        builder.Services.AddScoped<IOrderManagementService>(sp => sp.GetRequiredService<OrderManagementService>());
        builder.Services.AddScoped<LookupManagementStore>();
        builder.Services.AddScoped<LookupManagementService>();
        builder.Services.AddScoped<ILookupManagementService>(sp => sp.GetRequiredService<LookupManagementService>());
        builder.Services.AddSingleton<IProductImageContentValidator, CrossPlatformProductImageContentValidator>();
        builder.Services.AddSingleton<IProductImageStorage>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var environment = sp.GetRequiredService<IWebHostEnvironment>();
            var configuredUploadsPath = configuration["ProductImages:UploadsDirectory"];
            var resolvedUploadsPath = string.IsNullOrWhiteSpace(configuredUploadsPath)
                ? Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", "products")
                : Path.GetFullPath(configuredUploadsPath, environment.ContentRootPath);
            return new LocalProductImageStorage(resolvedUploadsPath, sp.GetRequiredService<IProductImageContentValidator>());
        });
        builder.Services.AddScoped<ProductManagementStore>();
        builder.Services.AddScoped<ProductManagementService>();
        builder.Services.AddScoped<IProductManagementService>(sp => sp.GetRequiredService<ProductManagementService>());
        builder.Services.AddScoped<ProductEditorService>(sp => sp.GetRequiredService<ProductManagementService>());
        builder.Services.AddScoped<ProductCatalogReader>();
        builder.Services.AddScoped<IProductService, ProductService>();
        builder.Services.AddScoped<IPhoneStoreReadinessProbe, PhoneStoreReadinessProbe>();
        builder.Services.AddHealthChecks()
            .AddCheck<PhoneStoreDatabaseHealthCheck>(HealthCheckNames.Database);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Enter a valid Local JWT access token."
            });
            options.OperationFilter<AuthorizationDocumentationFilter>();
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PhoneStore API",
                Version = "v1",
                Description = "Public catalog and health endpoints. Protected operations require a verified local account with the appropriate role."
            });
        });

        var app = builder.Build();

        app.UseExceptionHandler();
        if (operational.TrustedProxies.Length > 0 || operational.TrustedProxyNetworks.Length > 0)
        {
            app.UseForwardedHeaders(CreateForwardedHeadersOptions(operational));
        }
        if (!app.Environment.IsDevelopment())
            app.UseHsts();
        app.UseMiddleware<RequestSafetyMiddleware>();
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseRouting();
        app.Use(async (context, next) =>
        {
            var metadata = context.GetEndpoint()?.Metadata;
            if (metadata?.GetMetadata<IAuthorizeData>() is not null && metadata.GetMetadata<IAllowAnonymous>() is null)
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                });
            await next(context);
        });
        app.UseRateLimiter();
        app.UseRequestTimeouts();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers().Add(endpoint =>
        {
            if (!endpoint.Metadata.OfType<IAllowAnonymous>().Any() && !endpoint.Metadata.OfType<IAuthorizeData>().Any())
                endpoint.Metadata.Add(new AuthorizeAttribute());
        });
        try
        {
            app.Run();
        }
        catch (OptionsValidationException ex)
        {
            Console.Error.WriteLine($"[FATAL] Startup configuration validation failed ({ex.OptionsType.Name}): {ex.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static ForwardedHeadersOptions CreateForwardedHeadersOptions(ApiOperationalOptions operational)
    {
        var forwarded = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };
        forwarded.KnownProxies.Clear();
        forwarded.KnownIPNetworks.Clear();
        foreach (var proxy in operational.TrustedProxies)
            forwarded.KnownProxies.Add(IPAddress.Parse(proxy.Trim()));
        foreach (var network in operational.TrustedProxyNetworks)
            forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network.Trim()));
        return forwarded;
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
        var remote = context.Connection.RemoteIpAddress;
        var normalized = remote is { IsIPv4MappedToIPv6: true } ? remote.MapToIPv4() : remote;
        var key = normalized?.ToString() ?? "unknown";
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
            options.MaxRequestBodyBytes is < 1024 or > 1048576 ||
            options.MaxImageUploadBytes is < 1024 or > (20 * 1024 * 1024) ||
            options.CatalogTimeoutSeconds is < 1 or > 120 ||
            options.HealthTimeoutSeconds is < 1 or > 30 ||
            options.AuthTimeoutSeconds is < 1 or > 30 ||
            options.CatalogRequestsPerMinute is < 1 or > 10000 ||
            options.HealthRequestsPerMinute is < 1 or > 10000 ||
            options.AuthRequestsPerMinute is < 1 or > 1000 ||
            options.ProtectedRequestsPerMinute is < 1 or > 10000)
        {
            throw new InvalidOperationException("ApiOperational configuration is invalid.");
        }

        foreach (var proxy in options.TrustedProxies)
        {
            if (string.IsNullOrWhiteSpace(proxy) || !IPAddress.TryParse(proxy.Trim(), out _))
                throw new InvalidOperationException("ApiOperational:TrustedProxies contains an invalid IP address.");
        }

        foreach (var network in options.TrustedProxyNetworks)
        {
            if (string.IsNullOrWhiteSpace(network) || !System.Net.IPNetwork.TryParse(network.Trim(), out _))
                throw new InvalidOperationException("ApiOperational:TrustedProxyNetworks contains an invalid CIDR network.");
        }
    }
}
