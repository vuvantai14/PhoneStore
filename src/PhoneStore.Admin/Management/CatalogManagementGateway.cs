using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;
using PhoneStore.Business.Authentication;
using PhoneStore.Business.Management;
using PhoneStore.Business.Reporting;

namespace PhoneStore.Admin.Management;

public sealed class CatalogManagementGateway(IServiceScopeFactory scopeFactory, AdminSession session)
{
    public event EventHandler? AuthorizationRevoked;

    public Task<T> WithLookupsAsync<T>(Func<ILookupManagementService, Task<T>> action) =>
        WithReadAsync(action);

    public Task<T> WithProductsAsync<T>(Func<IProductManagementService, Task<T>> action) =>
        WithReadAsync(action);

    public Task<T> WithCustomersAsync<T>(Func<ICustomerManagementService, Task<T>> action) =>
        WithReadAsync(action);

    public Task<T> WithOrdersAsync<T>(Func<IOrderManagementService, Task<T>> action) =>
        WithReadAsync(action);

    public Task<T> WithReportingAsync<T>(Func<IReportingService, Task<T>> action) =>
        WithReadAsync(action);

    public Task<T> WithLookupsWriteAsync<T>(Func<ILookupManagementService, Task<T>> action) =>
        WithWriteAsync(action);

    public Task<T> WithProductsWriteAsync<T>(Func<IProductManagementService, Task<T>> action) =>
        WithWriteAsync(action);

    public Task<T> WithCustomersWriteAsync<T>(Func<ICustomerManagementService, Task<T>> action) =>
        WithWriteAsync(action);

    public Task<T> WithOrdersWriteAsync<T>(Func<IOrderManagementService, Task<T>> action) =>
        WithWriteAsync(action);

    private async Task<T> WithReadAsync<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        RequireAdmin();
        using var scope = scopeFactory.CreateScope();
        try
        {
            return await action(scope.ServiceProvider.GetRequiredService<TService>());
        }
        catch (UnauthorizedAccessException)
        {
            RevokeSession();
            throw;
        }
    }

    private async Task<T> WithWriteAsync<TService, T>(Func<TService, Task<T>> action) where TService : notnull
    {
        var identity = RequireAdmin();
        using var scope = scopeFactory.CreateScope();
        var authorized = await scope.ServiceProvider.GetRequiredService<IAdminAuthService>()
            .IsCurrentAdminAsync(identity.UserId);
        if (!authorized)
        {
            RevokeSession();
            throw new UnauthorizedAccessException("Tài khoản quản trị không còn hoạt động hoặc không còn quyền Admin.");
        }

        try
        {
            return await action(scope.ServiceProvider.GetRequiredService<TService>());
        }
        catch (UnauthorizedAccessException)
        {
            RevokeSession();
            throw;
        }
    }

    private void RevokeSession()
    {
        session.Clear();
        AuthorizationRevoked?.Invoke(this, EventArgs.Empty);
    }

    private PhoneStore.Models.Authentication.AdminIdentity RequireAdmin()
    {
        if (session.Current is not { Role: "Admin" } identity)
            throw new UnauthorizedAccessException("Phiên đăng nhập quản trị đã kết thúc.");
        return identity;
    }
}
