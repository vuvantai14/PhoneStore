using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;
using PhoneStore.Business.Authentication;
using PhoneStore.Data.Authentication;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

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
        services.AddTransient<LoginForm>();
        services.AddTransient<MainForm>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true, ValidateOnBuild = true
        });
    }
}
