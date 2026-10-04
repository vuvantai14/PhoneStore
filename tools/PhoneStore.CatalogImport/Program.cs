using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

var execute = args.Contains("--execute", StringComparer.OrdinalIgnoreCase);
var root = FindRepositoryRoot();
var imageDirectory = Path.Combine(root, "src", "PhoneStore.Web", "wwwroot", "uploads", "products");
var supported = new HashSet<string>([".jpg", ".jpeg", ".png", ".webp"], StringComparer.OrdinalIgnoreCase);
var definitions = Catalog.Definitions;
var files = Directory.Exists(imageDirectory)
    ? Directory.GetFiles(imageDirectory).Select(path => new FileInfo(path)).ToArray()
    : [];
var resolvedImages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
var missing = new List<string>();
var duplicates = new List<string>();
foreach (var item in definitions)
{
    var matches = files.Where(file => file.Name.StartsWith(item.ImageBase + ".", StringComparison.OrdinalIgnoreCase)
        && string.Equals(file.Name[..^file.Extension.Length], item.ImageBase, StringComparison.OrdinalIgnoreCase)
        && supported.Contains(file.Extension)).ToArray();
    if (matches.Length == 0) missing.Add(item.ImageBase);
    else if (matches.Length > 1) duplicates.Add(item.ImageBase);
    else resolvedImages[item.ImageBase] = matches[0].Name;
}
var unsupported = files.Where(file => !supported.Contains(file.Extension)).Select(file => file.Name).ToArray();

Console.WriteLine(JsonSerializer.Serialize(new
{
    Phase = "ImageValidation", Expected = definitions.Count, Found = resolvedImages.Count,
    Missing = missing, DuplicateMatches = duplicates, Unsupported = unsupported,
    ImageDirectory = imageDirectory
}, ImportJson.Options));
if (missing.Count > 0 || duplicates.Count > 0)
{
    Environment.ExitCode = 2;
    return;
}
if (!execute)
{
    Console.WriteLine("Validation passed. Run explicitly with --execute to import the catalog.");
    return;
}

var connection = ReadConnectionString(Path.Combine(root, "src", "PhoneStore.Web", "appsettings.Development.json"));
var options = new DbContextOptionsBuilder<PhoneStoreDbContext>().UseSqlServer(connection).Options;
await using var db = new PhoneStoreDbContext(options);
await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
var now = DateTime.UtcNow;
var brandCreated = 0;
var brandReused = 0;
var productCreated = 0;
var productReused = 0;
var productUpdated = 0;
var imageCreated = 0;
var imageReused = 0;
var categoryCreated = false;
Category? category = null;

try
{
    var existingBrands = await db.Brands.ToListAsync();
    var brandMap = new Dictionary<string, Brand>(StringComparer.OrdinalIgnoreCase);
    foreach (var name in Catalog.Brands)
    {
        var brand = existingBrands.SingleOrDefault(value => value.BrandName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (brand is null)
        {
            brand = new Brand { BrandName = name, Description = $"Điện thoại {name}", IsActive = true, CreatedAt = now };
            db.Brands.Add(brand); existingBrands.Add(brand); brandCreated++;
        }
        else
        {
            brand.BrandName = name;
            brand.IsActive = true;
            if (string.IsNullOrWhiteSpace(brand.Description)) brand.Description = $"Điện thoại {name}";
            brandReused++;
        }
        brandMap[name] = brand;
    }

    var categories = await db.Categories.ToListAsync();
    category = categories.SingleOrDefault(value => value.CategoryName.Equals(Catalog.CategoryName, StringComparison.OrdinalIgnoreCase));
    if (category is null)
    {
        category = new Category
        {
            CategoryName = Catalog.CategoryName, Description = Catalog.CategoryName,
            IsActive = true, CreatedAt = now
        };
        db.Categories.Add(category); categoryCreated = true;
    }
    else { category.CategoryName = Catalog.CategoryName; category.IsActive = true; }
    await db.SaveChangesAsync();

    var existingProducts = await db.Products.Include(product => product.Brand)
        .Include(product => product.ProductImages).ToListAsync();
    foreach (var definition in definitions)
    {
        var sameName = existingProducts.Where(product => product.ProductName.Equals(definition.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (sameName.Any(product => !product.Brand.BrandName.Equals(definition.Brand, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Product name conflict with another brand: {definition.Name}");
        var product = sameName.SingleOrDefault(product => product.Brand.BrandName.Equals(definition.Brand, StringComparison.OrdinalIgnoreCase));
        var isNew = product is null;
        if (isNew)
        {
            product = new Product { ProductName = definition.Name, CreatedAt = now };
            db.Products.Add(product); existingProducts.Add(product); productCreated++;
        }

        var changed = Apply(product!, definition, brandMap[definition.Brand].BrandId, category.CategoryId, now);
        if (!isNew)
        {
            if (changed) productUpdated++; else productReused++;
        }

        var imageUrl = "/uploads/products/" + resolvedImages[definition.ImageBase];
        var intended = product!.ProductImages.SingleOrDefault(image => image.ImageUrl.Equals(imageUrl, StringComparison.OrdinalIgnoreCase));
        if (intended is null)
        {
            foreach (var image in product.ProductImages) image.IsPrimary = false;
            product.ProductImages.Add(new ProductImage { ImageUrl = imageUrl, IsPrimary = true, DisplayOrder = 0 });
            imageCreated++;
        }
        else
        {
            foreach (var image in product.ProductImages) image.IsPrimary = ReferenceEquals(image, intended);
            intended.DisplayOrder = 0;
            imageReused++;
        }
    }
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}

db.ChangeTracker.Clear();
var expectedNames = definitions.Select(item => item.Name).ToArray();
var imported = await db.Products.AsNoTracking().Include(product => product.Brand).Include(product => product.ProductImages)
    .Where(product => expectedNames.Contains(product.ProductName)).ToListAsync();
var exact = definitions.Select(definition => imported.SingleOrDefault(product =>
    product.ProductName == definition.Name && product.Brand.BrandName == definition.Brand)).ToArray();
var missingAssociations = definitions.Count(definition =>
{
    var product = imported.SingleOrDefault(value => value.ProductName == definition.Name && value.Brand.BrandName == definition.Brand);
    var url = "/uploads/products/" + resolvedImages[definition.ImageBase];
    return product is null || !product.ProductImages.Any(image => image.ImageUrl == url);
});
var primaryCount = exact.Where(product => product is not null)
    .Sum(product => product!.ProductImages.Count(image => image.IsPrimary));
var brokenFiles = definitions.Count(definition => !File.Exists(Path.Combine(imageDirectory, resolvedImages[definition.ImageBase])));
Console.WriteLine(JsonSerializer.Serialize(new
{
    Phase = "ImportComplete",
    Brands = new { Created = brandCreated, Reused = brandReused, Available = Catalog.Brands.Count },
    Category = new { Name = Catalog.CategoryName, Created = categoryCreated, Active = category!.IsActive },
    Products = new { Expected = definitions.Count, Created = productCreated, Reused = productReused, Updated = productUpdated, Skipped = 0,
        AllFound = exact.All(product => product is not null), Active = exact.Count(product => product?.IsActive == true) },
    Images = new { Created = imageCreated, Reused = imageReused, MissingAssociations = missingAssociations,
        PrimaryCount = primaryCount, BrokenFiles = brokenFiles }
}, ImportJson.Options));

static bool Apply(Product product, CatalogItem item, int brandId, int categoryId, DateTime now)
{
    var description = $"{item.Name} với cấu hình {item.RAM}, bộ nhớ {item.Storage}, phù hợp nhu cầu sử dụng hằng ngày.";
    var changed = product.ProductName != item.Name || product.BrandId != brandId || product.CategoryId != categoryId || product.Price != item.Price
        || product.StockQuantity != item.Stock || product.RAM != item.RAM || product.Storage != item.Storage
        || product.Chip != item.Chip || product.Screen != item.Screen || product.Camera != item.Camera
        || product.Battery != item.Battery || product.OperatingSystem != item.OS
        || product.Description != description || !product.IsActive;
    product.ProductName = item.Name; product.BrandId = brandId; product.CategoryId = categoryId; product.Price = item.Price;
    product.StockQuantity = item.Stock; product.RAM = item.RAM; product.Storage = item.Storage;
    product.Chip = item.Chip; product.Screen = item.Screen; product.Camera = item.Camera;
    product.Battery = item.Battery; product.OperatingSystem = item.OS;
    product.Description = description; product.IsActive = true;
    if (changed || product.ProductId == 0) product.UpdatedAt = now;
    return changed;
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "PhoneStore.sln"))) return directory.FullName;
    throw new DirectoryNotFoundException("PhoneStore.sln was not found.");
}

static string ReadConnectionString(string path)
{
    using var json = JsonDocument.Parse(File.ReadAllText(path));
    return Environment.GetEnvironmentVariable("ConnectionStrings__PhoneStoreConnection")
        ?? json.RootElement.GetProperty("ConnectionStrings").GetProperty("PhoneStoreConnection").GetString()
        ?? throw new InvalidOperationException("PhoneStoreConnection is missing.");
}

internal sealed record CatalogItem(string Name, string Brand, string ImageBase, decimal Price, int Stock,
    string RAM, string Storage, string Chip, string Screen, string Camera, string Battery, string OS);

internal static class Catalog
{
    internal const string CategoryName = "Điện thoại thông minh";
    internal static readonly IReadOnlyList<string> Brands = ["Apple", "Samsung", "Xiaomi", "OPPO", "vivo", "realme", "Google", "OnePlus"];
    internal static readonly IReadOnlyList<CatalogItem> Definitions =
    [
        new("iPhone 12", "Apple", "iphone-12", 12990000, 12, "4 GB", "128 GB", "Apple A14 Bionic", "OLED 6.1 inch", "Kép 12 MP", "2815 mAh", "iOS"),
        new("iPhone 13", "Apple", "iphone-13", 15990000, 18, "4 GB", "128 GB", "Apple A15 Bionic", "OLED 6.1 inch", "Kép 12 MP", "3240 mAh", "iOS"),
        new("iPhone 14", "Apple", "iphone-14", 18990000, 14, "6 GB", "128 GB", "Apple A15 Bionic", "OLED 6.1 inch", "Kép 12 MP", "3279 mAh", "iOS"),
        new("iPhone 14 Pro Max", "Apple", "iphone-14-pro-max", 26990000, 9, "6 GB", "256 GB", "Apple A16 Bionic", "OLED 6.7 inch 120 Hz", "Chính 48 MP", "4323 mAh", "iOS"),
        new("iPhone 15", "Apple", "iphone-15", 21990000, 16, "6 GB", "128 GB", "Apple A16 Bionic", "OLED 6.1 inch", "Chính 48 MP", "3349 mAh", "iOS"),
        new("iPhone 15 Pro Max", "Apple", "iphone-15-pro-max", 34990000, 7, "8 GB", "256 GB", "Apple A17 Pro", "OLED 6.7 inch 120 Hz", "Chính 48 MP, tele 5x", "4441 mAh", "iOS"),
        new("Samsung Galaxy A15", "Samsung", "samsung-galaxy-a15", 3990000, 35, "8 GB", "128 GB", "MediaTek Helio G99", "Super AMOLED 6.5 inch", "Chính 50 MP", "5000 mAh", "Android"),
        new("Samsung Galaxy A25", "Samsung", "samsung-galaxy-a25", 6490000, 28, "8 GB", "128 GB", "Exynos 1280", "Super AMOLED 6.5 inch 120 Hz", "Chính 50 MP OIS", "5000 mAh", "Android"),
        new("Samsung Galaxy A35", "Samsung", "samsung-galaxy-a35", 8290000, 24, "8 GB", "128 GB", "Exynos 1380", "Super AMOLED 6.6 inch 120 Hz", "Chính 50 MP OIS", "5000 mAh", "Android"),
        new("Samsung Galaxy A55", "Samsung", "samsung-galaxy-a55", 10490000, 22, "8 GB", "256 GB", "Exynos 1480", "Super AMOLED 6.6 inch 120 Hz", "Chính 50 MP OIS", "5000 mAh", "Android"),
        new("Samsung Galaxy S23", "Samsung", "samsung-galaxy-s23", 16990000, 13, "8 GB", "256 GB", "Snapdragon 8 Gen 2", "Dynamic AMOLED 6.1 inch 120 Hz", "Chính 50 MP", "3900 mAh", "Android"),
        new("Samsung Galaxy S24 Ultra", "Samsung", "samsung-galaxy-s24-ultra", 32990000, 8, "12 GB", "256 GB", "Snapdragon 8 Gen 3", "Dynamic AMOLED 6.8 inch 120 Hz", "Chính 200 MP", "5000 mAh", "Android"),
        new("Xiaomi Redmi Note 13", "Xiaomi", "xiaomi-redmi-note-13", 4990000, 32, "8 GB", "128 GB", "Snapdragon 685", "AMOLED 6.67 inch 120 Hz", "Chính 108 MP", "5000 mAh", "Android"),
        new("Xiaomi Redmi Note 13 Pro", "Xiaomi", "xiaomi-redmi-note-13-pro", 7490000, 26, "8 GB", "256 GB", "MediaTek Helio G99-Ultra", "AMOLED 6.67 inch 120 Hz", "Chính 200 MP OIS", "5000 mAh", "Android"),
        new("Xiaomi Redmi Note 13 Pro+", "Xiaomi", "xiaomi-redmi-note-13-pro-plus", 10990000, 19, "12 GB", "256 GB", "MediaTek Dimensity 7200-Ultra", "AMOLED 6.67 inch 120 Hz", "Chính 200 MP OIS", "5000 mAh", "Android"),
        new("Xiaomi 13T", "Xiaomi", "xiaomi-13t", 12990000, 15, "12 GB", "256 GB", "MediaTek Dimensity 8200-Ultra", "AMOLED 6.67 inch 144 Hz", "Bộ ba 50 MP", "5000 mAh", "Android"),
        new("Xiaomi 14", "Xiaomi", "xiaomi-14", 22990000, 10, "12 GB", "512 GB", "Snapdragon 8 Gen 3", "AMOLED 6.36 inch 120 Hz", "Bộ ba 50 MP", "4610 mAh", "Android"),
        new("POCO X6 Pro", "Xiaomi", "poco-x6-pro", 9990000, 21, "12 GB", "512 GB", "MediaTek Dimensity 8300-Ultra", "AMOLED 6.67 inch 120 Hz", "Chính 64 MP OIS", "5000 mAh", "Android"),
        new("OPPO A58", "OPPO", "oppo-a58", 4490000, 30, "6 GB", "128 GB", "MediaTek Helio G85", "LCD 6.72 inch", "Chính 50 MP", "5000 mAh", "Android"),
        new("OPPO A79", "OPPO", "oppo-a79", 5990000, 27, "8 GB", "256 GB", "MediaTek Dimensity 6020", "LCD 6.72 inch 90 Hz", "Chính 50 MP", "5000 mAh", "Android"),
        new("OPPO Reno10", "OPPO", "oppo-reno10", 9990000, 17, "8 GB", "256 GB", "MediaTek Dimensity 7050", "AMOLED 6.7 inch 120 Hz", "Chính 64 MP, tele 32 MP", "5000 mAh", "Android"),
        new("OPPO Reno11", "OPPO", "oppo-reno11", 10990000, 18, "8 GB", "256 GB", "MediaTek Dimensity 7050", "AMOLED 6.7 inch 120 Hz", "Chính 50 MP OIS", "5000 mAh", "Android"),
        new("OPPO Reno11 F", "OPPO", "oppo-reno11-f", 8990000, 23, "8 GB", "256 GB", "MediaTek Dimensity 7050", "AMOLED 6.7 inch 120 Hz", "Chính 64 MP", "5000 mAh", "Android"),
        new("OPPO Find N3", "OPPO", "oppo-find-n3", 34990000, 5, "16 GB", "512 GB", "Snapdragon 8 Gen 2", "AMOLED gập 7.82 inch 120 Hz", "Chính 48 MP", "4805 mAh", "Android"),
        new("vivo Y17s", "vivo", "vivo-y17s", 3490000, 38, "6 GB", "128 GB", "MediaTek Helio G85", "LCD 6.56 inch", "Chính 50 MP", "5000 mAh", "Android"),
        new("vivo Y36", "vivo", "vivo-y36", 5490000, 29, "8 GB", "128 GB", "Snapdragon 680", "LCD 6.64 inch 90 Hz", "Chính 50 MP", "5000 mAh", "Android"),
        new("vivo V29", "vivo", "vivo-v29", 10990000, 16, "12 GB", "256 GB", "Snapdragon 778G", "AMOLED 6.78 inch 120 Hz", "Chính 50 MP OIS", "4600 mAh", "Android"),
        new("vivo V30", "vivo", "vivo-v30", 12990000, 14, "12 GB", "256 GB", "Snapdragon 7 Gen 3", "AMOLED 6.78 inch 120 Hz", "Kép 50 MP", "5000 mAh", "Android"),
        new("vivo V30 Pro", "vivo", "vivo-v30-pro", 16990000, 11, "12 GB", "512 GB", "MediaTek Dimensity 8200", "AMOLED 6.78 inch 120 Hz", "Bộ ba 50 MP", "5000 mAh", "Android"),
        new("realme C55", "realme", "realme-c55", 3990000, 34, "8 GB", "256 GB", "MediaTek Helio G88", "LCD 6.72 inch 90 Hz", "Chính 64 MP", "5000 mAh", "Android"),
        new("realme C67", "realme", "realme-c67", 4690000, 31, "8 GB", "128 GB", "Snapdragon 685", "LCD 6.72 inch 90 Hz", "Chính 108 MP", "5000 mAh", "Android"),
        new("realme 11 Pro", "realme", "realme-11-pro", 8990000, 20, "8 GB", "256 GB", "MediaTek Dimensity 7050", "AMOLED 6.7 inch 120 Hz", "Chính 100 MP OIS", "5000 mAh", "Android"),
        new("realme 12 Pro+", "realme", "realme-12-pro-plus", 12990000, 15, "12 GB", "256 GB", "Snapdragon 7s Gen 2", "AMOLED 6.7 inch 120 Hz", "Chính 50 MP, tele 64 MP", "5000 mAh", "Android"),
        new("Google Pixel 7", "Google", "google-pixel-7", 11990000, 12, "8 GB", "128 GB", "Google Tensor G2", "OLED 6.3 inch 90 Hz", "Chính 50 MP", "4355 mAh", "Android"),
        new("Google Pixel 7 Pro", "Google", "google-pixel-7-pro", 15990000, 9, "12 GB", "128 GB", "Google Tensor G2", "LTPO OLED 6.7 inch 120 Hz", "Chính 50 MP, tele 48 MP", "5000 mAh", "Android"),
        new("Google Pixel 8", "Google", "google-pixel-8", 16990000, 13, "8 GB", "128 GB", "Google Tensor G3", "OLED 6.2 inch 120 Hz", "Chính 50 MP", "4575 mAh", "Android"),
        new("Google Pixel 8 Pro", "Google", "google-pixel-8-pro", 23990000, 8, "12 GB", "256 GB", "Google Tensor G3", "LTPO OLED 6.7 inch 120 Hz", "Chính 50 MP, tele 48 MP", "5050 mAh", "Android"),
        new("OnePlus 11", "OnePlus", "oneplus-11", 15990000, 13, "12 GB", "256 GB", "Snapdragon 8 Gen 2", "LTPO AMOLED 6.7 inch 120 Hz", "Chính 50 MP", "5000 mAh", "Android"),
        new("OnePlus 12", "OnePlus", "oneplus-12", 22990000, 10, "16 GB", "512 GB", "Snapdragon 8 Gen 3", "LTPO AMOLED 6.82 inch 120 Hz", "Chính 50 MP, tele 64 MP", "5400 mAh", "Android"),
        new("OnePlus Nord 3", "OnePlus", "oneplus-nord-3", 10990000, 18, "16 GB", "256 GB", "MediaTek Dimensity 9000", "AMOLED 6.74 inch 120 Hz", "Chính 50 MP OIS", "5000 mAh", "Android")
    ];
}

internal static class ImportJson
{
    internal static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
}
