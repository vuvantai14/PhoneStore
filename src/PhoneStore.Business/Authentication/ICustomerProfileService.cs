using PhoneStore.Models.Authentication;

namespace PhoneStore.Business.Authentication;

public interface ICustomerProfileService
{
    Task<CustomerProfile?> GetAsync(int authenticatedCustomerId, CancellationToken ct = default);
    Task<ProfileUpdateResult> UpdateAsync(int authenticatedCustomerId, CustomerProfileEdit edit, CancellationToken ct = default);
}
