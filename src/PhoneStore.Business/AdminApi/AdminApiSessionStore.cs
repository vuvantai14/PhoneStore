using PhoneStore.Models.Authentication;

namespace PhoneStore.Business.AdminApi;

public sealed record AdminTokenSnapshot(
    string AccessToken,
    DateTime AccessExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshExpiresAtUtc,
    AdminIdentity Identity);

public interface IAdminSessionLogout
{
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminApiSessionStore : IDisposable
{
    private readonly object _gate = new();
    private AdminTokenSnapshot? _current;

    internal SemaphoreSlim RefreshLock { get; } = new(1, 1);

    public AdminTokenSnapshot? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Establish(
        string accessToken,
        DateTime accessExpiresAtUtc,
        string refreshToken,
        DateTime refreshExpiresAtUtc,
        AdminIdentity identity)
    {
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Valid access and refresh tokens are required.");
        if (identity.UserId <= 0 || !string.Equals(identity.Role, SystemRoles.Admin, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Administrator privileges are required.");

        var snapshot = new AdminTokenSnapshot(
            accessToken,
            DateTime.SpecifyKind(accessExpiresAtUtc, DateTimeKind.Utc),
            refreshToken,
            DateTime.SpecifyKind(refreshExpiresAtUtc, DateTimeKind.Utc),
            identity);

        lock (_gate)
        {
            _current = snapshot;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current = null;
        }
    }

    public void Dispose() => RefreshLock.Dispose();
}
