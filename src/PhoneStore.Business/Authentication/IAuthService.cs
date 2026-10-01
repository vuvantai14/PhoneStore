using PhoneStore.Models.Authentication;

namespace PhoneStore.Business.Authentication;

public interface IAuthService
{
    Task<RegistrationResult> RegisterAsync(string fullName, string email, string? phone, string password, CancellationToken cancellationToken = default);
    Task<CustomerIdentity?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<CustomerIdentity?> GetActiveCustomerAsync(int userId, CancellationToken cancellationToken = default);
}

