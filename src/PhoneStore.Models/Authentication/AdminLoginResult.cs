namespace PhoneStore.Models.Authentication;

public enum AdminLoginStatus
{
    Success,
    InvalidCredentials,
    Forbidden
}

public sealed record AdminLoginResult(AdminLoginStatus Status, AdminIdentity? Identity = null);
