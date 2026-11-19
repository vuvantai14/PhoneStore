using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PhoneStore.Api.Contracts.Customers;

public sealed class AdminCustomerQuery
{
    public string? Search { get; init; }
    public bool? IsActive { get; init; }
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CustomerStatusRequest
{
    public required bool IsActive { get; init; }
}

public sealed record CustomerSummary(int UserId, string FullName, string Email, string? Phone, bool IsActive);
public sealed record CustomerDetail(int UserId, string FullName, string Email, string? Phone,
    string? Address, bool IsActive, DateTime CreatedAt);
public sealed record CustomerPage(IReadOnlyList<CustomerSummary> Items, int TotalCount, int Page, int PageSize);
