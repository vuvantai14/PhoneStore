using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Management;

namespace PhoneStore.Data.Management;

public sealed class CustomerManagementStore(PhoneStoreDbContext db)
{
    private IQueryable<User> Customers => db.Users.Where(user => user.Role.RoleName == "Customer");

    public async Task<IReadOnlyList<CustomerManagementRow>> ListAsync(CustomerManagementQuery filter, CancellationToken ct)
    {
        var query = Customers.AsNoTracking();
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(user => user.FullName.Contains(search) || user.Email.Contains(search)
                || (user.Phone != null && user.Phone.Contains(search)));
        if (filter.IsActive.HasValue) query = query.Where(user => user.IsActive == filter.IsActive.Value);
        return await query.OrderBy(user => user.UserId)
            .Select(user => new CustomerManagementRow(user.UserId, user.FullName, user.Email, user.Phone,
                user.Address, user.IsActive, user.CreatedAt)).ToListAsync(ct);
    }

    public Task<CustomerManagementRow?> GetRowAsync(int id, CancellationToken ct) => Customers.AsNoTracking()
        .Where(user => user.UserId == id)
        .Select(user => new CustomerManagementRow(user.UserId, user.FullName, user.Email, user.Phone,
            user.Address, user.IsActive, user.CreatedAt)).SingleOrDefaultAsync(ct);

    public Task<User?> FindAsync(int id, CancellationToken ct) => Customers.SingleOrDefaultAsync(user => user.UserId == id, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public void ClearTracking() => db.ChangeTracker.Clear();
}
