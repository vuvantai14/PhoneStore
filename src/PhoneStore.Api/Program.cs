using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using PhoneStore.Api.Health;
using PhoneStore.Api.Infrastructure;
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

        builder.Services.AddControllers();
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddDbContext<PhoneStoreDbContext>(options => options.UseSqlServer(connection));
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
                Description = "Read-only integration API for PhoneStore."
            });
        });

        var app = builder.Build();

        app.UseExceptionHandler();
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();
        app.Run();
    }
}
