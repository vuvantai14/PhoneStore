using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;

namespace PhoneStore.Api.Health;

public sealed class PhoneStoreReadinessProbe(PhoneStoreDbContext db) : IPhoneStoreReadinessProbe
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        db.Database.CanConnectAsync(cancellationToken);
}
