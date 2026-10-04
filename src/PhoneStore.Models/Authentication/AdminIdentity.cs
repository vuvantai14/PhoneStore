namespace PhoneStore.Models.Authentication;

public sealed record AdminIdentity(int UserId, string FullName, string Email, string Role);
