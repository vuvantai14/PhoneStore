using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Business.Authentication;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Admin.Authentication;

public sealed class AdminLoginGateway(IServiceScopeFactory scopeFactory)
{
    public async Task<AdminLoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        // Each attempt has its own DbContext, disposed before returning the safe identity.
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IAdminAuthService>()
            .LoginAsync(email, password, cancellationToken);
    }
}
