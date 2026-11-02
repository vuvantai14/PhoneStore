namespace PhoneStore.Models.Authentication;

// Public profile data deliberately excludes credentials and administrative fields.
public sealed record CustomerProfile(string FullName, string Email, string? Phone, string? Address);
public sealed record CustomerProfileEdit(string? FullName, string? Phone, string? Address);
public enum ProfileUpdateResult { Success, InvalidInput, NotFound, Unavailable }
