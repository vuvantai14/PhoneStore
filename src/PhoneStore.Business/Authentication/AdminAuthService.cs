using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using PhoneStore.Data.Authentication;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Business.Authentication;

public sealed class AdminAuthService(AuthUserStore store, IPasswordHasher<User> hasher) : IAdminAuthService
{
    public Task<bool> IsCurrentAdminAsync(int userId, CancellationToken cancellationToken = default) =>
        store.IsActiveAdminAsync(userId, cancellationToken);

    public async Task<AdminLoginResult> LoginAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)
            || password.Length > 128)
            return new(AdminLoginStatus.InvalidCredentials);

        email = email.Trim().ToLowerInvariant();
        if (email.Length > 150 || !new EmailAddressAttribute().IsValid(email))
            return new(AdminLoginStatus.InvalidCredentials);

        var user = await store.FindByEmailAsync(email, cancellationToken);
        if (user is null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
            return new(AdminLoginStatus.InvalidCredentials);

        PasswordVerificationResult verification;
        try
        {
            verification = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            return new(AdminLoginStatus.InvalidCredentials);
        }

        if (verification == PasswordVerificationResult.Failed)
            return new(AdminLoginStatus.InvalidCredentials);
        if (user.Role?.RoleName != "Admin")
            return new(AdminLoginStatus.Forbidden);

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            await store.UpdatePasswordHashAsync(user, hasher.HashPassword(user, password), cancellationToken);

        return new(AdminLoginStatus.Success,
            new AdminIdentity(user.UserId, user.FullName, user.Email, user.Role.RoleName));
    }
}
