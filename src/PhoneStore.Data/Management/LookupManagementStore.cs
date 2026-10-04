using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Management;

namespace PhoneStore.Data.Management;

public sealed class LookupManagementStore(PhoneStoreDbContext db)
{
    public void ClearTracking() => db.ChangeTracker.Clear();

    public async Task<IReadOnlyList<LookupRow>> ListAsync(LookupKind kind, string? search,
        CancellationToken cancellationToken = default)
    {
        var term = search?.Trim();
        if (kind == LookupKind.Brand)
        {
            var query = db.Brands.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(term))
                query = query.Where(x => x.BrandName.Contains(term));
            return await query.OrderBy(x => x.BrandName).ThenBy(x => x.BrandId)
                .Select(x => new LookupRow(x.BrandId, x.BrandName, x.Description, x.IsActive, x.CreatedAt))
                .ToListAsync(cancellationToken);
        }

        if (kind != LookupKind.Category)
            throw new ArgumentOutOfRangeException(nameof(kind));
        var categories = db.Categories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(term))
            categories = categories.Where(x => x.CategoryName.Contains(term));
        return await categories.OrderBy(x => x.CategoryName).ThenBy(x => x.CategoryId)
            .Select(x => new LookupRow(x.CategoryId, x.CategoryName, x.Description, x.IsActive, x.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(LookupKind kind, int? exceptId, string normalizedName,
        CancellationToken cancellationToken = default)
    {
        return kind switch
        {
            LookupKind.Brand => db.Brands.AnyAsync(x => (!exceptId.HasValue || x.BrandId != exceptId.Value)
                && x.BrandName.Trim().ToUpper() == normalizedName, cancellationToken),
            LookupKind.Category => db.Categories.AnyAsync(x => (!exceptId.HasValue || x.CategoryId != exceptId.Value)
                && x.CategoryName.Trim().ToUpper() == normalizedName, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public async Task<int?> SaveAsync(LookupKind kind, int? id, LookupEdit edit,
        CancellationToken cancellationToken = default)
    {
        if (kind == LookupKind.Brand)
        {
            var brand = id.HasValue ? await db.Brands.FindAsync([id.Value], cancellationToken) : new Brand();
            if (brand is null)
                return null;
            brand.BrandName = edit.Name;
            brand.Description = edit.Description;
            brand.IsActive = edit.IsActive;
            if (!id.HasValue)
            {
                brand.CreatedAt = DateTime.UtcNow;
                db.Brands.Add(brand);
            }
            await db.SaveChangesAsync(cancellationToken);
            return brand.BrandId;
        }

        if (kind != LookupKind.Category)
            throw new ArgumentOutOfRangeException(nameof(kind));
        var category = id.HasValue ? await db.Categories.FindAsync([id.Value], cancellationToken) : new Category();
        if (category is null)
            return null;
        category.CategoryName = edit.Name;
        category.Description = edit.Description;
        category.IsActive = edit.IsActive;
        if (!id.HasValue)
        {
            category.CreatedAt = DateTime.UtcNow;
            db.Categories.Add(category);
        }
        await db.SaveChangesAsync(cancellationToken);
        return category.CategoryId;
    }

    public async Task<bool> SetActiveAsync(LookupKind kind, int id, bool active,
        CancellationToken cancellationToken = default)
    {
        if (kind == LookupKind.Brand)
        {
            var brand = await db.Brands.FindAsync([id], cancellationToken);
            if (brand is null)
                return false;
            brand.IsActive = active;
        }
        else if (kind == LookupKind.Category)
        {
            var category = await db.Categories.FindAsync([id], cancellationToken);
            if (category is null)
                return false;
            category.IsActive = active;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
