namespace PhoneStore.Models.Authentication;

public sealed record CustomerIdentity(int UserId, string FullName, string Email, string Role);
public enum RegistrationResult { Success, DuplicateEmail, CustomerRoleMissing, InvalidInput, Unavailable }

