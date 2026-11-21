using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Business.AdminApi;
using PhoneStore.Business.Authentication;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Admin.Authentication;

public sealed class AdminLoginGateway(IServiceScopeFactory scopeFactory)
{
    public async Task<AdminLoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAdminAuthService>()
            .LoginAsync(email, password, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var logout = scope.ServiceProvider.GetService<IAdminSessionLogout>();
        if (logout is not null)
            await logout.LogoutAsync(cancellationToken);
    }
}
