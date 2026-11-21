using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

namespace PhoneStore.Data.Authentication;

public sealed class ApiRefreshTokenStore(PhoneStoreDbContext db)
{
    public Task<IDbContextTransaction> BeginAsync(CancellationToken ct) =>
        db.Database.IsSqlServer()
            ? db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : db.Database.BeginTransactionAsync(ct);

    public Task<ApiRefreshToken?> FindByHashForUpdateAsync(string tokenHash, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer())
            return db.ApiRefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        return db.ApiRefreshTokens
            .FromSqlInterpolated($"SELECT * FROM [ApiRefreshTokens] WITH (UPDLOCK, HOLDLOCK) WHERE [TokenHash] = {tokenHash}")
            .SingleOrDefaultAsync(ct);
    }

    public Task<List<ApiRefreshToken>> FindActiveByFamilyAsync(string familyId, CancellationToken ct) =>
        db.ApiRefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

    public Task<List<ApiRefreshToken>> FindActiveByUserAsync(int userId, CancellationToken ct) =>
        db.ApiRefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ToListAsync(ct);

    public Task<User?> FindActiveUserAsync(int userId, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.UserId == userId && u.IsActive, ct);

    public void Add(ApiRefreshToken token) => db.ApiRefreshTokens.Add(token);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public void ClearTracking() => db.ChangeTracker.Clear();
}
