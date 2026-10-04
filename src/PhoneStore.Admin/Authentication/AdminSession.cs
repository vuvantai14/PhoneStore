using PhoneStore.Models.Authentication;

namespace PhoneStore.Admin.Authentication;

public sealed class AdminSession
{
    public AdminIdentity? Current { get; private set; }

    internal void Establish(AdminIdentity identity)
    {
        if (identity.UserId <= 0 || identity.Role != "Admin")
            throw new InvalidOperationException("An authenticated Admin identity is required.");
        Current = identity;
    }

    public void Clear() => Current = null;
}
