using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Catalog;
using PhoneStore.Data.Reporting;

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
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId).Take(6))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductCard>> GetProductsAsync(
        CatalogQuery filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(db.Products.AsNoTracking().Where(p => p.IsActive), filter);

        query = filter.Sort switch
        {
            "price-asc" => query.OrderBy(p => p.Price).ThenBy(p => p.ProductId),
            "price-desc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.ProductId),
            "name-asc" => query.OrderBy(p => p.ProductName).ThenBy(p => p.ProductId),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId)
        };
        return await Cards(query).ToListAsync(cancellationToken);
    }

    public async Task<PagedCatalogResult> GetPagedProductsAsync(
        CatalogQuery filter, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(db.Products.AsNoTracking().Where(p => p.IsActive), filter);

        var totalItems = await query.CountAsync(cancellationToken);

        query = filter.Sort switch
        {
            "price-asc" => query.OrderBy(p => p.Price).ThenBy(p => p.ProductId),
            "price-desc" => query.OrderByDescending(p => p.Price).ThenBy(p => p.ProductId),
            "name-asc" => query.OrderBy(p => p.ProductName).ThenBy(p => p.ProductId),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId)
        };

        var pageSize = filter.PageSize <= 0 ? 10 : filter.PageSize;
        var totalPages = totalItems > 0 ? (int)Math.Ceiling((double)totalItems / pageSize) : 1;
        var page = filter.Page < 1 ? 1 : filter.Page;
        if (totalItems > 0 && page > totalPages)
        {
            page = totalPages;
        }
        var items = await Cards(query.Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(cancellationToken);

        return new PagedCatalogResult(items, totalItems, page, pageSize);
    }

    private static IQueryable<Product> ApplyFilters(IQueryable<Product> query, CatalogQuery filter)
    {
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(p => p.ProductName.Contains(search) || p.Brand.BrandName.Contains(search));

        if (filter.BrandIds is { Count: > 0 })
            query = query.Where(p => filter.BrandIds.Contains(p.BrandId));
        else if (filter.BrandId.HasValue)
            query = query.Where(p => p.BrandId == filter.BrandId.Value);

        if (filter.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);

        if (filter.MinPrice.HasValue)
            query = query.Where(p => p.Price >= filter.MinPrice.Value);
        if (filter.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= filter.MaxPrice.Value);

        if (filter.InStockOnly)
            query = query.Where(p => p.StockQuantity > 0);

        if (filter.Rams is { Count: > 0 })
        {
            var ramVariants = ExpandCapacityVariants(filter.Rams);
            query = query.Where(p => p.RAM != null && ramVariants.Contains(p.RAM));
        }

        if (filter.Storages is { Count: > 0 })
        {
            var storageVariants = ExpandCapacityVariants(filter.Storages);
            query = query.Where(p => p.Storage != null && storageVariants.Contains(p.Storage));
        }

        return query;
    }

    private static List<string> ExpandCapacityVariants(IEnumerable<string> values)
    {
        var list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            var clean = v.Trim();
            list.Add(clean);
            if (clean.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
            {
                var num = clean[..^2].Trim();
                list.Add($"{num} GB");
                list.Add($"{num}GB");
            }
            else if (clean.EndsWith("TB", StringComparison.OrdinalIgnoreCase))
            {
                var num = clean[..^2].Trim();
                list.Add($"{num} TB");
                list.Add($"{num}TB");
            }
        }
        return list.ToList();
    }

    public async Task<IReadOnlyList<ProductCard>> GetNewestAsync(
        int count, CancellationToken cancellationToken = default) =>
        await Cards(db.Products.AsNoTracking().Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId).Take(count))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProductCard>> GetDiversifiedNewestAsync(
        int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        var candidateCount = Math.Min(count * 8, 64);
        var candidates = await Cards(db.Products.AsNoTracking().Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.ProductId)
            .Take(candidateCount)).ToListAsync(cancellationToken);

        var brandQueues = candidates.GroupBy(product => product.BrandName)
            .Select(group => new Queue<ProductCard>(group)).ToList();
        var diversified = new List<ProductCard>(Math.Min(count, candidates.Count));
        while (diversified.Count < count && brandQueues.Count > 0)
        {
            foreach (var queue in brandQueues.ToArray())
            {
                diversified.Add(queue.Dequeue());
                if (queue.Count == 0) brandQueues.Remove(queue);
                if (diversified.Count == count) break;
            }
        }
        return diversified;
    }

    public async Task<IReadOnlyList<ProductCard>> GetBestSellersAsync(
        int count, CancellationToken cancellationToken = default)
    {
        if (count <= 0) return [];

        var rankedIds = await db.OrderDetails.AsNoTracking()
            .Where(detail => detail.Product.IsActive
                && SalesOrderStatuses.Values.Contains(detail.Order.Status))
            .GroupBy(detail => new { detail.ProductId, detail.Product.ProductName })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ProductName,
                QuantitySold = group.Sum(detail => detail.Quantity)
            })
            .OrderByDescending(item => item.QuantitySold)
            .ThenBy(item => item.ProductName)
            .ThenBy(item => item.ProductId)
            .Take(count)
            .Select(item => item.ProductId)
            .ToListAsync(cancellationToken);

        if (rankedIds.Count == 0) return [];
        var cards = await Cards(db.Products.AsNoTracking()
            .Where(product => rankedIds.Contains(product.ProductId)))
            .ToListAsync(cancellationToken);
        var byId = cards.ToDictionary(product => product.ProductId);
        return rankedIds.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
    }

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

