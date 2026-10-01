using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Authentication;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Business.Authentication;

public sealed class AuthService(AuthUserStore store, IPasswordHasher<User> hasher) : IAuthService
{
    public async Task<RegistrationResult> RegisterAsync(string fullName, string email, string? phone,
        string password, CancellationToken cancellationToken = default)
    {
        fullName = fullName.Trim();
        email = NormalizeEmail(email);
        phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (fullName.Length is < 2 or > 100 || email.Length > 150
            || !new EmailAddressAttribute().IsValid(email) || password.Length is < 8 or > 128
            || (phone is not null && (phone.Length > 20 || !new PhoneAttribute().IsValid(phone))))
            return RegistrationResult.InvalidInput;

        try
        {
            if (await store.FindByEmailAsync(email, cancellationToken) is not null)
                return RegistrationResult.DuplicateEmail;
            var roleId = await store.CustomerRoleIdAsync(cancellationToken);
            if (roleId is null) return RegistrationResult.CustomerRoleMissing;
            var user = new User
            {
                FullName = fullName, Email = email, Phone = phone,
                RoleId = roleId.Value, IsActive = true, CreatedAt = DateTime.UtcNow
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            return await store.CreateAsync(user, cancellationToken)
                ? RegistrationResult.Success : RegistrationResult.DuplicateEmail;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            return RegistrationResult.Unavailable;
        }
    }

    public async Task<CustomerIdentity?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await store.FindByEmailAsync(NormalizeEmail(email), cancellationToken);
        if (user is null || !user.IsActive || user.Role.RoleName != "Customer") return null;
        PasswordVerificationResult result;
        try { result = hasher.VerifyHashedPassword(user, user.PasswordHash, password); }
        catch (FormatException) { return null; }
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            await store.UpdatePasswordHashAsync(user, hasher.HashPassword(user, password), cancellationToken);
        return Identity(user);
    }

    public async Task<CustomerIdentity?> GetActiveCustomerAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await store.FindByIdAsync(userId, cancellationToken);
        return user is { IsActive: true } && user.Role.RoleName == "Customer" ? Identity(user) : null;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static CustomerIdentity Identity(User user) => new(user.UserId, user.FullName, user.Email, user.Role.RoleName);
}

