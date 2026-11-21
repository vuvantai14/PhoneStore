using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;
using PhoneStore.Admin.Management;
using PhoneStore.Business.AdminApi;
using PhoneStore.Business.Authentication;
using PhoneStore.Business.Management;
using PhoneStore.Business.Reporting;
using PhoneStore.Data.Authentication;
using PhoneStore.Data.Context;
using PhoneStore.Data.Management;
using PhoneStore.Data.Reporting;
using PhoneStore.Models.Entities;

namespace PhoneStore.Admin;

internal static class AdminServices
{
    internal static ServiceProvider Create(IConfiguration? overrideConfiguration = null, HttpClient? overrideApiHttpClient = null)
    {
        var configuration = overrideConfiguration ?? new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        var dataAccess = AdminDataAccessOptions.Parse(
            configuration["AdminDataAccess:Mode"],
            configuration["AdminDataAccess:ApiBaseUrl"],
            configuration["AdminDataAccess:TimeoutSeconds"]);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(dataAccess);
        services.AddSingleton<AdminLoginGateway>();
        services.AddSingleton<AdminSession>();
        services.AddSingleton<CatalogManagementGateway>();
        services.AddTransient<LoginForm>();
        services.AddTransient<MainForm>();

        if (dataAccess.Mode == AdminDataAccessMode.RestApi)
        {
            // RestApi mode intentionally does NOT register PhoneStoreDbContext so no call can silently fall back to DirectSql.
            services.AddSingleton<AdminApiSessionStore>();
            if (overrideApiHttpClient is not null)
                services.AddSingleton(overrideApiHttpClient);
            else
                services.AddSingleton(_ => AdminApiClient.CreateHttpClient(dataAccess));
            services.AddSingleton<AdminApiClient>();
            services.AddScoped<RestAdminAuthService>();
            services.AddScoped<IAdminAuthService>(sp => sp.GetRequiredService<RestAdminAuthService>());
            services.AddScoped<IAdminSessionLogout>(sp => sp.GetRequiredService<RestAdminAuthService>());
            services.AddScoped<ILookupManagementService, RestLookupManagementService>();
            services.AddScoped<IProductManagementService, RestProductManagementService>();
            services.AddScoped<ICustomerManagementService, RestCustomerManagementService>();
            services.AddScoped<IOrderManagementService, RestOrderManagementService>();
            services.AddScoped<IReportingService, RestReportingService>();
        }
        else
        {
            var connection = configuration.GetConnectionString("PhoneStoreConnection");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("PhoneStoreConnection configuration is required.");
            var uploadsPath = configuration["ProductImages:UploadsDirectory"];
            if (string.IsNullOrWhiteSpace(uploadsPath))
                throw new InvalidOperationException("ProductImages:UploadsDirectory configuration is required.");

            services.AddDbContext<PhoneStoreDbContext>(options => options.UseSqlServer(connection));
            services.AddScoped<AuthUserStore>();
            services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
            services.AddScoped<IAdminAuthService, AdminAuthService>();
            services.AddSingleton<IProductImageStorage>(new LocalProductImageStorage(
                Path.GetFullPath(uploadsPath, AppContext.BaseDirectory), new WindowsProductImageContentValidator()));
            services.AddScoped<LookupManagementStore>();
            services.AddScoped<ILookupManagementService, LookupManagementService>();
            services.AddScoped<ProductManagementStore>();
            services.AddScoped<IProductManagementService, ProductManagementService>();
            services.AddScoped<CustomerManagementStore>();
            services.AddScoped<ICustomerManagementService, CustomerManagementService>();
            services.AddScoped<OrderManagementStore>();
            services.AddScoped<IOrderManagementService, OrderManagementService>();
            services.AddScoped<ReportingStore>();
            services.AddScoped<IReportingStore>(provider => provider.GetRequiredService<ReportingStore>());
            services.AddScoped<IReportingService, ReportingService>();
        }

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }
}
