using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;
using PhoneStore.Business.Authentication;
using PhoneStore.Business.Management;
using PhoneStore.Business.Reporting;

namespace PhoneStore.Admin.Management;

public sealed class CatalogManagementGateway(IServiceScopeFactory scopeFactory, AdminSession session)
{
    public event EventHandler? AuthorizationRevoked;

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

    public async Task<T> WithReportingAsync<T>(Func<IReportingService, Task<T>> action)
    {
        RequireAdmin();
        using var scope = scopeFactory.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<IReportingService>());
    }

    public Task<T> WithLookupsWriteAsync<T>(Func<ILookupManagementService, Task<T>> action) =>
        WithWriteAsync(action);

    public Task<T> WithProductsWriteAsync<T>(Func<IProductManagementService, Task<T>> action) =>
        WithWriteAsync(action);

    public Task<T> WithCustomersWriteAsync<T>(Func<ICustomerManagementService, Task<T>> action) =>
        WithWriteAsync(action);

    public Task<T> WithOrdersWriteAsync<T>(Func<IOrderManagementService, Task<T>> action) =>
        WithWriteAsync(action);

    private async Task<T> WithWriteAsync<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        var identity = RequireAdmin();
        using var scope = scopeFactory.CreateScope();
        var authorized = await scope.ServiceProvider.GetRequiredService<IAdminAuthService>()
            .IsCurrentAdminAsync(identity.UserId);
        if (!authorized)
        {
            session.Clear();
            AuthorizationRevoked?.Invoke(this, EventArgs.Empty);
            throw new UnauthorizedAccessException("Tài khoản quản trị không còn hoạt động hoặc không còn quyền Admin.");
        }
        return await action(scope.ServiceProvider.GetRequiredService<TService>());
    }

    private PhoneStore.Models.Authentication.AdminIdentity RequireAdmin()
    {
        if (session.Current is not { Role: "Admin" } identity)
            throw new UnauthorizedAccessException("Phiên đăng nhập quản trị đã kết thúc.");
        return identity;
    }
}
