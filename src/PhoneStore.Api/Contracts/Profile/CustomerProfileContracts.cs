using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PhoneStore.Api.Contracts.Profile;

public sealed record CustomerProfileResponse(
    string FullName,
    string Email,
    string? Phone,
    string? Address);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomerProfileUpdateRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public required string FullName { get; init; }

    [Phone, StringLength(20)]
    public string? Phone { get; init; }

    [StringLength(255)]
    public string? Address { get; init; }
}
