using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Management;
using PhoneStore.Data.Management;
using PhoneStore.Models.Management;

namespace PhoneStore.Tests;

public sealed class AdminCrudTests
{
    [Theory]
    [InlineData(LookupKind.Brand)]
    [InlineData(LookupKind.Category)]
    public async Task Lookup_create_edit_search_duplicate_and_soft_deactivate(LookupKind kind)
    {
        await using var t = await TestDatabase.CreateAsync();
        var service = new LookupManagementService(new(t.Db));
        var added = await service.SaveAsync(kind, null, new("  D2A Unique  ", " desc ", true));
        Assert.True(added.Succeeded);
        Assert.False((await service.SaveAsync(kind, null, new("d2a unique", null, true))).Succeeded);
        Assert.True((await service.SaveAsync(kind, added.Id, new("D2A Renamed", null, true))).Succeeded);
        var rows = await service.ListAsync(kind, "Renamed");
        Assert.Single(rows); Assert.Equal(added.Id, rows[0].Id);
        Assert.True((await service.SetActiveAsync(kind, added.Id!.Value, false)).Succeeded);
        Assert.False((await service.ListAsync(kind, null)).Single().IsActive);
        Assert.True((await service.SetActiveAsync(kind, added.Id.Value, true)).Succeeded);
    }

    [Theory]
    [InlineData(LookupKind.Brand, "", null)]
    [InlineData(LookupKind.Category, "   ", null)]
    public async Task Lookup_required_validation_does_not_write(LookupKind kind, string name, string? description)
    {
        await using var t = await TestDatabase.CreateAsync();
        Assert.False((await new LookupManagementService(new(t.Db)).SaveAsync(kind, null, new(name, description, true))).Succeeded);
        Assert.Empty(await t.Db.Brands.ToListAsync()); Assert.Empty(await t.Db.Categories.ToListAsync());
    }

    [Theory]
    [InlineData(LookupKind.Brand)]
    [InlineData(LookupKind.Category)]
    public async Task Lookup_limits_missing_and_database_failure_are_handled(LookupKind kind)
    {
        await using var t = await TestDatabase.CreateAsync();
        var service = new LookupManagementService(new(t.Db));
        Assert.False((await service.SaveAsync(kind, null, new(new string('x', 101), null, true))).Succeeded);
        Assert.False((await service.SaveAsync(kind, null, new("Name", new string('x', 501), true))).Succeeded);
        Assert.False((await service.SaveAsync(kind, 9999, new("Name", null, true))).Succeeded);
        Assert.False((await service.SetActiveAsync(kind, 9999, false)).Succeeded);
        await t.Db.Database.GetDbConnection().CloseAsync();
        Assert.False((await service.SaveAsync(kind, null, new("Name", null, true))).Succeeded);
        Assert.False((await service.SetActiveAsync(kind, 1, false)).Succeeded);
    }

    [Theory]
    [InlineData("", 1, 0)]
    [InlineData("Name", -1, 0)]
    [InlineData("Name", 0, 0)]
    [InlineData("Name", 1, -1)]
    public async Task Product_invalid_values_do_not_create(string name, int price, int stock)
    {
        await using var t = await TestDatabase.CreateAsync(); var (_, p) = await t.SeedCatalogAsync();
        var service = new ProductManagementService(new(t.Db), new NoFiles());
        Assert.False((await service.SaveAsync(null, new() { ProductName = name, Price = price,
            StockQuantity = stock, BrandId = p.BrandId, CategoryId = p.CategoryId })).Succeeded);
        Assert.Equal(1, await t.Db.Products.CountAsync());
    }

    [Fact]
    public async Task Product_edit_inactive_references_and_precision_are_validated()
    {
        await using var t = await TestDatabase.CreateAsync(); var (_, p) = await t.SeedCatalogAsync();
        var service = new ProductManagementService(new(t.Db), new NoFiles());
        var edit = (await service.GetEditAsync(p.ProductId))!;
        edit.Price = 1.001m; Assert.False((await service.SaveAsync(null, edit)).Succeeded);
        edit.Price = 100; edit.RAM = new string('x', 51); Assert.False((await service.SaveAsync(null, edit)).Succeeded);
        edit.RAM = null; edit.BrandId = 99999; Assert.False((await service.SaveAsync(null, edit)).Succeeded);
        edit.BrandId = p.BrandId;
        await t.Db.Brands.Where(b => b.BrandId == p.BrandId).ExecuteUpdateAsync(s => s.SetProperty(b => b.IsActive, false));
        Assert.False((await service.SaveAsync(null, edit)).Succeeded);
        Assert.True((await service.SaveAsync(p.ProductId, edit)).Succeeded); // existing association is retained
        Assert.True((await service.SetActiveAsync(p.ProductId, false)).Succeeded);
        Assert.Single(await service.ListAsync(new(IsActive: false)));
    }

    [Theory]
    [InlineData(".exe", new byte[] { 1, 2, 3 })]
    [InlineData(".jpg", new byte[] { 1, 2, 3 })]
    [InlineData(".png", new byte[] { })]
    [InlineData(".webp", new byte[] { 1, 2, 3 })]
    public async Task Image_invalid_extension_header_empty_is_rejected(string extension, byte[] bytes)
    {
        var root = Path.Combine(Path.GetTempPath(), "PhoneStore-D2A-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var input = Path.Combine(root, "input" + extension); var output = Path.Combine(root, "uploads");
        try
        {
            await File.WriteAllBytesAsync(input, bytes);
            await Assert.ThrowsAnyAsync<Exception>(() => new LocalProductImageStorage(output, new StorageOnlyValidator()).StoreAsync(input));
            Assert.False(Directory.Exists(output));
        }
        finally { File.Delete(input); Directory.Delete(root); }
    }

    [Fact]
    public async Task Image_size_limit_missing_file_and_cancel_are_safe()
    {
        var root = Path.Combine(Path.GetTempPath(), "PhoneStore-D2A-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); var path = Path.Combine(root, "large.jpg"); var output = Path.Combine(root, "uploads");
        try
        {
            var storage = new LocalProductImageStorage(output, new StorageOnlyValidator());
            await Assert.ThrowsAsync<FileNotFoundException>(() => storage.StoreAsync(Path.Combine(root, "missing.jpg")));
            await using (var file = File.Create(path)) { file.SetLength(10 * 1024 * 1024 + 1); }
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.StoreAsync(path));
            await File.WriteAllBytesAsync(path, [255, 216, 255]);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.StoreAsync(path, cancel.Token));
            Assert.False(Directory.Exists(output));
        }
        finally { File.Delete(path); Directory.Delete(root); }
    }

    [Fact]
    public async Task Image_guid_path_cannot_overwrite_source_and_only_managed_copy_is_deleted()
    {
        var root = Path.Combine(Path.GetTempPath(), "PhoneStore-D2A-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); var input = Path.Combine(root, "source.png"); var output = Path.Combine(root, "uploads");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAQSURBVBhXY/jPAEIUAAYGAILBAf8lQkk6AAAAAElFTkSuQmCC");
        try
        {
            await File.WriteAllBytesAsync(input, png); var storage = new LocalProductImageStorage(output, new StorageOnlyValidator());
            var first = await storage.StoreAsync(input); var second = await storage.StoreAsync(input);
            Assert.NotEqual(first, second); Assert.Matches("^/uploads/products/[0-9a-f]{32}\\.png$", first);
            Assert.Equal(png, await File.ReadAllBytesAsync(Path.Combine(output, Path.GetFileName(first))));
            Assert.True(storage.TryDelete(first)); Assert.True(storage.TryDelete(second)); Assert.True(File.Exists(input));
            Assert.Empty(Directory.GetFiles(output));
        }
        finally { File.Delete(input); if (Directory.Exists(output)) Directory.Delete(output); Directory.Delete(root); }
    }

    [Fact]
    public void Image_cleanup_rejects_unmanaged_and_traversal_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "PhoneStore-D2A-" + Guid.NewGuid().ToString("N"));
        var storage = new LocalProductImageStorage(root, new StorageOnlyValidator());
        Assert.False(storage.TryDelete("/uploads/products/../protected.jpg"));
        Assert.False(storage.TryDelete("/uploads/products/demo-slug.jpg"));
        Assert.False(storage.TryDelete("https://external/image.jpg"));
    }

    // Storage orchestration tests; the opt-in harness exercises the real Windows decoder.
    private sealed class StorageOnlyValidator : IProductImageContentValidator
    {
        public void Validate(Stream content, string extension, CancellationToken ct = default) { ct.ThrowIfCancellationRequested(); }
    }
    private sealed class NoFiles : IProductImageStorage
    {
        public Task<string> StoreAsync(string sourceFile, CancellationToken ct = default) => throw new InvalidOperationException();
        public bool TryDelete(string imageUrl) => throw new InvalidOperationException();
    }
}

