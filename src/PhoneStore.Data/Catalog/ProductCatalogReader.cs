using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Catalog;

namespace PhoneStore.Data.Catalog;

public sealed class ProductCatalogReader(PhoneStoreDbContext db)
{
    public Task<ProductDetail?> GetProductDetailAsync(int id, CancellationToken cancellationToken = default) =>
        db.Products.AsNoTracking().Where(p => p.ProductId == id && p.IsActive)
            .Select(p => new ProductDetail
            {
                ProductId = p.ProductId, ProductName = p.ProductName,
                BrandId = p.BrandId, BrandName = p.Brand.BrandName,
                CategoryId = p.CategoryId, CategoryName = p.Category.CategoryName,
                Price = p.Price, StockQuantity = p.StockQuantity,
                RAM = p.RAM, Storage = p.Storage, Chip = p.Chip, Screen = p.Screen,
                Camera = p.Camera, Battery = p.Battery, OperatingSystem = p.OperatingSystem,
                Description = p.Description,
                Images = p.ProductImages.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder)
                    .ThenBy(i => i.ImageId).Select(i => i.ImageUrl).ToList()
            }).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductCard>> GetRelatedAsync(
        ProductDetail product, CancellationToken cancellationToken = default) =>
        await Cards(db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.ProductId != product.ProductId
                && (p.CategoryId == product.CategoryId || p.BrandId == product.BrandId))
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId).Take(4))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductCard>> GetProductsAsync(
        CatalogQuery filter, CancellationToken cancellationToken = default)
    {
        var query = db.Products.AsNoTracking().Where(p => p.IsActive);
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.ProductName.Contains(search) || p.Brand.BrandName.Contains(search));
        if (filter.BrandId.HasValue)
            query = query.Where(p => p.BrandId == filter.BrandId.Value);
        if (filter.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);
        if (filter.MinPrice.HasValue)
            query = query.Where(p => p.Price >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= filter.MaxPrice.Value);

        query = filter.Sort switch
        {
            "price-asc" => query.OrderBy(p => p.Price).ThenBy(p => p.ProductId),
            "price-desc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.ProductId),
            "name-asc" => query.OrderBy(p => p.ProductName).ThenBy(p => p.ProductId),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId)
        };
        return await Cards(query).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductCard>> GetNewestAsync(
        int count, CancellationToken cancellationToken = default) =>
        await Cards(db.Products.AsNoTracking().Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId).Take(count))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CatalogOption>> GetBrandsAsync(CancellationToken cancellationToken = default) =>
        await db.Brands.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BrandName)
            .Select(b => new CatalogOption(b.BrandId, b.BrandName)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CatalogOption>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.CategoryName)
            .Select(c => new CatalogOption(c.CategoryId, c.CategoryName)).ToListAsync(cancellationToken);

    private static IQueryable<ProductCard> Cards(IQueryable<Product> query) =>
        query.Select(p => new ProductCard(
            p.ProductId, p.ProductName, p.Brand.BrandName, p.RAM, p.Storage,
            p.Price, p.StockQuantity,
            p.ProductImages.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder)
                .ThenBy(i => i.ImageId).Select(i => i.ImageUrl).FirstOrDefault()));
}

