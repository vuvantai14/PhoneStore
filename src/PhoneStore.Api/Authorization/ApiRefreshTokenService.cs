using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhoneStore.Api.Contracts.Auth;
using PhoneStore.Data.Authentication;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Entities;

namespace PhoneStore.Api.Authorization;

public enum RefreshRotationStatus
{
    Success,
    InvalidOrExpired,
    Unavailable
}

public sealed record RefreshRotationResult(RefreshRotationStatus Status, AuthTokenResponse? Response = null);

public enum LogoutRevocationStatus
{
    Success,
    Forbidden,
    Unavailable
}

public interface IApiRefreshTokenService
{
    Task<AuthTokenResponse?> IssueSessionAsync(int userId, string fullName, string email, string role, CancellationToken ct);
    Task<RefreshRotationResult> RotateAsync(string rawRefreshToken, CancellationToken ct);
    Task<LogoutRevocationStatus> RevokeAsync(int authenticatedUserId, string? rawRefreshToken, bool allSessions, CancellationToken ct);
}

internal sealed class ApiRefreshTokenService(
    ApiRefreshTokenStore store,
    ILocalJwtTokenService tokenService,
    IOptions<ApiAuthenticationOptions> options,
    TimeProvider timeProvider) : IApiRefreshTokenService
{
    private const string TokenPrefix = "psrt_";

    public async Task<AuthTokenResponse?> IssueSessionAsync(
        int userId, string fullName, string email, string role, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var refreshDays = options.Value.LocalJwt.RefreshTokenDays;
        var refreshExpiresAt = now.AddDays(refreshDays);
        var (rawRefresh, hash) = GenerateRefreshToken();
        var entity = new ApiRefreshToken
        {
            UserId = userId,
            FamilyId = Guid.NewGuid().ToString("N"),
            TokenHash = hash,
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpiresAt
        };

        try
        {
            store.Add(entity);
            await store.SaveAsync(ct);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return null;
        }
        finally { store.ClearTracking(); }

        var (accessToken, accessExpiresAt, expiresInSeconds) = tokenService.IssueAccessToken(userId, role);
        return new AuthTokenResponse(
            accessToken, "Bearer", expiresInSeconds, accessExpiresAt,
            rawRefresh, refreshExpiresAt,
            new AuthUserResponse(userId, fullName, email, role));
    }

    public async Task<RefreshRotationResult> RotateAsync(string rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken) || rawRefreshToken.Length > 256 ||
            !rawRefreshToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
            return new(RefreshRotationStatus.InvalidOrExpired);

        var hash = HashToken(rawRefreshToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var refreshDays = options.Value.LocalJwt.RefreshTokenDays;

        try
        {
            await using var transaction = await store.BeginAsync(ct);
            var existing = await store.FindByHashForUpdateAsync(hash, ct);
            if (existing is null)
                return new(RefreshRotationStatus.InvalidOrExpired);

            if (existing.RevokedAtUtc.HasValue)
            {
                // Reuse of a revoked or already rotated refresh token compromises the entire session family.
                var family = await store.FindActiveByFamilyAsync(existing.FamilyId, ct);
                foreach (var token in family)
                {
                    token.RevokedAtUtc = now;
                    token.RevokedReason = "ReuseDetected";
                }
                if (family.Count > 0)
                {
                    await store.SaveAsync(ct);
                    await transaction.CommitAsync(ct);
                }
                return new(RefreshRotationStatus.InvalidOrExpired);
            }

            if (existing.ExpiresAtUtc <= now)
            {
                existing.RevokedAtUtc = now;
                existing.RevokedReason = "Expired";
                await store.SaveAsync(ct);
                await transaction.CommitAsync(ct);
                return new(RefreshRotationStatus.InvalidOrExpired);
            }

            var user = await store.FindActiveUserAsync(existing.UserId, ct);
            if (user is null || user.Role.RoleName is not (SystemRoles.Customer or SystemRoles.Admin))
            {
                var family = await store.FindActiveByFamilyAsync(existing.FamilyId, ct);
                foreach (var token in family)
                {
                    token.RevokedAtUtc = now;
                    token.RevokedReason = "AccountInactive";
                }
                await store.SaveAsync(ct);
                await transaction.CommitAsync(ct);
                return new(RefreshRotationStatus.InvalidOrExpired);
            }

            var (newRawRefresh, newHash) = GenerateRefreshToken();
            var newExpiresAt = now.AddDays(refreshDays);

            existing.RevokedAtUtc = now;
            existing.RevokedReason = "Rotated";
            existing.ReplacedByTokenHash = newHash;

            store.Add(new ApiRefreshToken
            {
                UserId = user.UserId,
                FamilyId = existing.FamilyId,
                TokenHash = newHash,
                CreatedAtUtc = now,
                ExpiresAtUtc = newExpiresAt
            });

            await store.SaveAsync(ct);
            await transaction.CommitAsync(ct);

            var (accessToken, accessExpiresAt, expiresInSeconds) = tokenService.IssueAccessToken(user.UserId, user.Role.RoleName);
            return new(RefreshRotationStatus.Success, new AuthTokenResponse(
                accessToken, "Bearer", expiresInSeconds, accessExpiresAt,
                newRawRefresh, newExpiresAt,
                new AuthUserResponse(user.UserId, user.FullName, user.Email, user.Role.RoleName)));
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return new(RefreshRotationStatus.Unavailable);
        }
        finally { store.ClearTracking(); }
    }

    public async Task<LogoutRevocationStatus> RevokeAsync(
        int authenticatedUserId, string? rawRefreshToken, bool allSessions, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        try
        {
            await using var transaction = await store.BeginAsync(ct);
            if (allSessions || string.IsNullOrWhiteSpace(rawRefreshToken))
            {
                var activeTokens = await store.FindActiveByUserAsync(authenticatedUserId, ct);
                foreach (var token in activeTokens)
                {
                    token.RevokedAtUtc = now;
                    token.RevokedReason = "Logout";
                }
                if (activeTokens.Count > 0) await store.SaveAsync(ct);
                await transaction.CommitAsync(ct);
                return LogoutRevocationStatus.Success;
            }

            if (rawRefreshToken.Length > 256 || !rawRefreshToken.StartsWith(TokenPrefix, StringComparison.Ordinal))
                return LogoutRevocationStatus.Success;

            var existing = await store.FindByHashForUpdateAsync(HashToken(rawRefreshToken), ct);
            if (existing is null)
                return LogoutRevocationStatus.Success;
            if (existing.UserId != authenticatedUserId)
                return LogoutRevocationStatus.Forbidden;

            var family = await store.FindActiveByFamilyAsync(existing.FamilyId, ct);
            foreach (var token in family)
            {
                token.RevokedAtUtc = now;
                token.RevokedReason = "Logout";
            }
            if (family.Count > 0) await store.SaveAsync(ct);
            await transaction.CommitAsync(ct);
            return LogoutRevocationStatus.Success;
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            return LogoutRevocationStatus.Unavailable;
        }
        finally { store.ClearTracking(); }
    }

    private static (string RawToken, string Hash) GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var raw = TokenPrefix + WebEncoders.Base64UrlEncode(bytes);
        return (raw, HashToken(raw));
    }

    internal static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();
}
