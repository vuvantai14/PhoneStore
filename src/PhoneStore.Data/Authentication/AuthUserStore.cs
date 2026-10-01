using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

namespace PhoneStore.Data.Authentication;

public sealed class AuthUserStore(PhoneStoreDbContext db)
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> FindByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == id, cancellationToken);

    public Task<int?> CustomerRoleIdAsync(CancellationToken cancellationToken) =>
        db.Roles.Where(r => r.RoleName == "Customer").Select(r => (int?)r.RoleId).SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> CreateAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(user).State = EntityState.Detached;
            return false;
        }
    }

    public async Task UpdatePasswordHashAsync(User user, string hash, CancellationToken cancellationToken)
    {
        user.PasswordHash = hash;
        await db.SaveChangesAsync(cancellationToken);
    }
}

