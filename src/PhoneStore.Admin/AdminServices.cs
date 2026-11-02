using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;
using PhoneStore.Business.Authentication;
using PhoneStore.Data.Authentication;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Admin.Management;
using PhoneStore.Business.Management;
using PhoneStore.Data.Management;
using PhoneStore.Business.Reporting;
using PhoneStore.Data.Reporting;

namespace PhoneStore.Admin;

internal static class AdminServices
{
    internal static ServiceProvider Create()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables()
            .Build();
        var connection = configuration.GetConnectionString("PhoneStoreConnection");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("PhoneStoreConnection configuration is required.");

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContext<PhoneStoreDbContext>(options => options.UseSqlServer(connection));
        services.AddScoped<AuthUserStore>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddSingleton<AdminLoginGateway>();
        services.AddSingleton<AdminSession>();
        var uploadsPath = configuration["ProductImages:UploadsDirectory"];
        if (string.IsNullOrWhiteSpace(uploadsPath))
            throw new InvalidOperationException("ProductImages:UploadsDirectory configuration is required.");
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
        services.AddScoped<IReportingService, ReportingService>();
        services.AddSingleton<CatalogManagementGateway>();
        services.AddTransient<LoginForm>();
        services.AddTransient<MainForm>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true, ValidateOnBuild = true
        });
    }
}
