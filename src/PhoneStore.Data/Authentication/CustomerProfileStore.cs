using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Data.Authentication;

public sealed class CustomerProfileStore(PhoneStoreDbContext db)
{
    public Task<CustomerProfile?> GetAsync(int customerId, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(u => u.UserId == customerId && u.IsActive && u.Role.RoleName == SystemRoles.Customer)
            .Select(u => new CustomerProfile(u.FullName, u.Email, u.Phone, u.Address))
            .SingleOrDefaultAsync(ct);

    // One conditional UPDATE: never attach a client-supplied User or save unrelated changes.
    public Task<int> UpdateAsync(int customerId, CustomerProfileEdit edit, CancellationToken ct) =>
        db.Users.Where(u => u.UserId == customerId && u.IsActive && u.Role.RoleName == SystemRoles.Customer)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.FullName, edit.FullName!)
                .SetProperty(u => u.Phone, edit.Phone)
                .SetProperty(u => u.Address, edit.Address), ct);
}
