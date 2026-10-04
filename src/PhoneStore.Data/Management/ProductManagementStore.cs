using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Management;

namespace PhoneStore.Data.Management;

public sealed class ProductManagementStore(PhoneStoreDbContext db)
{
    public async Task<IReadOnlyList<ProductManagementRow>> ListAsync(ProductManagementQuery filter, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.ProductName.Contains(search) || p.Brand.BrandName.Contains(search));
        if (filter.BrandId.HasValue) query = query.Where(p => p.BrandId == filter.BrandId.Value);
        if (filter.CategoryId.HasValue) query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
        if (filter.IsActive.HasValue) query = query.Where(p => p.IsActive == filter.IsActive.Value);
        return await query.OrderByDescending(p => p.ProductId)
            .Select(p => new ProductManagementRow(p.ProductId, p.ProductName, p.Brand.BrandName,
                p.Category.CategoryName, p.Price, p.StockQuantity, p.RAM, p.Storage, p.IsActive, p.UpdatedAt))
            .ToListAsync(ct);
    }

    public Task<ProductEdit?> GetEditAsync(int id, CancellationToken ct) =>
        db.Products.AsNoTracking().Where(p => p.ProductId == id).Select(p => new ProductEdit
        {
            ProductName = p.ProductName, BrandId = p.BrandId, CategoryId = p.CategoryId,
            Price = p.Price, StockQuantity = p.StockQuantity, RAM = p.RAM, Storage = p.Storage,
            Chip = p.Chip, Screen = p.Screen, Camera = p.Camera, Battery = p.Battery,
            OperatingSystem = p.OperatingSystem, Description = p.Description, IsActive = p.IsActive
        }).SingleOrDefaultAsync(ct);

    public async Task<ProductEditOptions> GetOptionsAsync(int? productId, CancellationToken ct)
    {
        var current = productId.HasValue
            ? await db.Products.AsNoTracking().Where(p => p.ProductId == productId.Value)
                .Select(p => new { p.BrandId, p.CategoryId }).SingleOrDefaultAsync(ct)
            : null;
        var currentBrandId = current?.BrandId;
        var currentCategoryId = current?.CategoryId;
        var brands = await db.Brands.AsNoTracking().Where(b => b.IsActive || b.BrandId == currentBrandId)
            .OrderBy(b => b.BrandName).Select(b => new LookupOption(b.BrandId, b.BrandName, b.IsActive)).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive || c.CategoryId == currentCategoryId)
            .OrderBy(c => c.CategoryName).Select(c => new LookupOption(c.CategoryId, c.CategoryName, c.IsActive)).ToListAsync(ct);
        return new ProductEditOptions(brands, categories);
    }

    public async Task<ProductEditOptions> GetFilterOptionsAsync(CancellationToken ct)
    {
        var brands = await db.Brands.AsNoTracking().OrderBy(b => b.BrandName)
            .Select(b => new LookupOption(b.BrandId, b.BrandName, b.IsActive)).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.CategoryName)
            .Select(c => new LookupOption(c.CategoryId, c.CategoryName, c.IsActive)).ToListAsync(ct);
        return new ProductEditOptions(brands, categories);
    }

    public Task<Product?> FindAsync(int id, CancellationToken ct) =>
        db.Products.SingleOrDefaultAsync(p => p.ProductId == id, ct);

    public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
        db.Products.AsNoTracking().AnyAsync(p => p.ProductId == id, ct);

    public Task<bool?> BrandActiveAsync(int id, CancellationToken ct) =>
        db.Brands.AsNoTracking().Where(b => b.BrandId == id).Select(b => (bool?)b.IsActive).SingleOrDefaultAsync(ct);

    public Task<bool?> CategoryActiveAsync(int id, CancellationToken ct) =>
        db.Categories.AsNoTracking().Where(c => c.CategoryId == id).Select(c => (bool?)c.IsActive).SingleOrDefaultAsync(ct);

    public void Add(Product product) => db.Products.Add(product);
    public void AddImage(ProductImage image) => db.ProductImages.Add(image);
    public void RemoveImage(ProductImage image) => db.ProductImages.Remove(image);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public void ClearTracking() => db.ChangeTracker.Clear();

    public Task<IDbContextTransaction> BeginAsync(CancellationToken ct) =>
        db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

    // The parent row is a shared lock point even when no image rows exist yet.
    // This prevents two concurrent first uploads from both becoming primary.
    public Task<Product?> LockProductAsync(int id, CancellationToken ct) =>
        db.Products.FromSqlInterpolated($"SELECT * FROM [Products] WITH (UPDLOCK, HOLDLOCK) WHERE [ProductId] = {id}")
            .SingleOrDefaultAsync(ct);

    public Task<List<ProductImage>> ImagesForUpdateAsync(int productId, CancellationToken ct) =>
        db.ProductImages.Where(i => i.ProductId == productId).OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.ImageId).ToListAsync(ct);

    public async Task<IReadOnlyList<ProductImageRow>> GetImagesAsync(int productId, CancellationToken ct) =>
        await db.ProductImages.AsNoTracking().Where(i => i.ProductId == productId)
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).ThenBy(i => i.ImageId)
            .Select(i => new ProductImageRow(i.ImageId, i.ProductId, i.ImageUrl, i.IsPrimary, i.DisplayOrder))
            .ToListAsync(ct);

    public Task<bool> IsImageUrlReferencedAsync(string imageUrl, CancellationToken ct) =>
        db.ProductImages.AsNoTracking().AnyAsync(i => i.ImageUrl == imageUrl, ct);
}
