namespace PhoneStore.Models.Authentication;

public static class SystemRoles
{
    // InitialCreate seeds this identity; the Roles check constraint preserves the mapping.
    public const int AdminId = 1;
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}
