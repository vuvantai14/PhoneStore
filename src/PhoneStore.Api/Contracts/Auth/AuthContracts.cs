using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PhoneStore.Api.Contracts.Auth;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoginRequest
{
    [Required, EmailAddress, StringLength(150)]
    public required string Email { get; init; }

    [Required, StringLength(128, MinimumLength = 1)]
    public required string Password { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RegisterRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public required string FullName { get; init; }

    [Required, EmailAddress, StringLength(150)]
    public required string Email { get; init; }

    [Phone, StringLength(20)]
    public string? Phone { get; init; }

    [Required, StringLength(128, MinimumLength = 8)]
    public required string Password { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RefreshTokenRequest
{
    [Required, StringLength(256, MinimumLength = 16)]
    public required string RefreshToken { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LogoutRequest
{
    [StringLength(256)]
    public string? RefreshToken { get; init; }

    public bool AllSessions { get; init; }
}

public sealed record AuthUserResponse(int UserId, string FullName, string Email, string Role);

public sealed record AuthTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshExpiresAtUtc,
    AuthUserResponse User);

public sealed record RegisterResponse(string Message, string Email, string Role);

public sealed record CurrentUserResponse(
    int UserId,
    string FullName,
    string Email,
    string? Phone,
    string Role,
    IReadOnlyList<string> Scopes);
