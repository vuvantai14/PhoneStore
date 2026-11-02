using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Data.Authentication;

public sealed class AuthUserStore(PhoneStoreDbContext db)
{
    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> FindByIdAsync(int id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == id, cancellationToken);

    // Registration never accepts an entity, role id, or role navigation from callers.
    public async Task<RegistrationResult> CreateCustomerAsync(string fullName, string email, string? phone,
        string passwordHash, CancellationToken cancellationToken)
    {
        if (db.ChangeTracker.HasChanges()) return RegistrationResult.Unavailable;
        var roleId = await db.Roles.Where(r => r.RoleName == SystemRoles.Customer)
            .Select(r => (int?)r.RoleId).SingleOrDefaultAsync(cancellationToken);
        if (roleId is null) return RegistrationResult.CustomerRoleMissing;
        var user = new User { FullName = fullName, Email = email, Phone = phone,
            PasswordHash = passwordHash, RoleId = roleId.Value, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return RegistrationResult.Success;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.Entry(user).State = EntityState.Detached;
            return RegistrationResult.DuplicateEmail;
        }
    }

    public async Task UpdatePasswordHashAsync(User user, string hash, CancellationToken cancellationToken)
    {
        user.PasswordHash = hash;
        await db.SaveChangesAsync(cancellationToken);
    }
}

