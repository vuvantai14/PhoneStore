using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;
using PhoneStore.Business.Management;

namespace PhoneStore.Admin.Management;

public sealed class CatalogManagementGateway(IServiceScopeFactory scopeFactory, AdminSession session)
{
    public async Task<T> WithLookupsAsync<T>(Func<ILookupManagementService, Task<T>> action)
    {
        RequireAdmin();
        using var scope = scopeFactory.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ILookupManagementService>());
    }

    public async Task<T> WithProductsAsync<T>(Func<IProductManagementService, Task<T>> action)
    {
        RequireAdmin();
        using var scope = scopeFactory.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IProductManagementService>());
    }

    public async Task<T> WithCustomersAsync<T>(Func<ICustomerManagementService, Task<T>> action)
    {
        RequireAdmin();
        using var scope = scopeFactory.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ICustomerManagementService>());
    }

    public async Task<T> WithOrdersAsync<T>(Func<IOrderManagementService, Task<T>> action)
    {
        RequireAdmin();
        using var scope = scopeFactory.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IOrderManagementService>());
    }

    private void RequireAdmin()
    {
        if (session.Current?.Role != "Admin")
            throw new UnauthorizedAccessException("Phiên đăng nhập quản trị đã kết thúc.");
    }
}
