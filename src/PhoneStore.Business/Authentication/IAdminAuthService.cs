using PhoneStore.Models.Authentication;

namespace PhoneStore.Business.Authentication;

public interface IAdminAuthService
{
    Task<AdminLoginResult> LoginAsync(string email, string password,
        CancellationToken cancellationToken = default);
}
