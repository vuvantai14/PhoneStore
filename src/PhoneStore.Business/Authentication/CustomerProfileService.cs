using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Authentication;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Business.Authentication;

public sealed class CustomerProfileService(CustomerProfileStore store) : ICustomerProfileService
{
    public Task<CustomerProfile?> GetAsync(int authenticatedCustomerId, CancellationToken ct = default) =>
        authenticatedCustomerId <= 0 ? Task.FromResult<CustomerProfile?>(null) : store.GetAsync(authenticatedCustomerId, ct);

    public async Task<ProfileUpdateResult> UpdateAsync(int authenticatedCustomerId, CustomerProfileEdit edit,
        CancellationToken ct = default)
    {
        if (authenticatedCustomerId <= 0) return ProfileUpdateResult.NotFound;
        var normalized = new CustomerProfileEdit(edit.FullName?.Trim(), Optional(edit.Phone), Optional(edit.Address));
        if (normalized.FullName is null || normalized.FullName.Length is < 2 or > 100
            || normalized.Phone is not null && (normalized.Phone.Length > 20 || !new PhoneAttribute().IsValid(normalized.Phone))
            || normalized.Address?.Length > 255)
            return ProfileUpdateResult.InvalidInput;
        try
        {
            return await store.UpdateAsync(authenticatedCustomerId, normalized, ct) == 1
                ? ProfileUpdateResult.Success : ProfileUpdateResult.NotFound;
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            return ProfileUpdateResult.Unavailable;
        }
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
