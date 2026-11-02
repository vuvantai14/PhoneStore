using System.Data;
using System.Drawing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Admin.Management;
using PhoneStore.Business.Management;
using PhoneStore.Data.Management;
using PhoneStore.Models.Entities;

internal static partial class Program
{
    static string NewImageFixtureRoot() => Path.Combine(AppContext.BaseDirectory, "image-fixtures", "D2B-" + Guid.NewGuid().ToString("N"));
    static void FixtureImage(string path, int width = 4, int height = 4)
    {
        using var bitmap = new Bitmap(width, height);
        bitmap.SetPixel(0, 0, Color.Red);
        bitmap.Save(path, Path.GetExtension(path) == ".png" ? System.Drawing.Imaging.ImageFormat.Png : System.Drawing.Imaging.ImageFormat.Jpeg);
    }
    static async Task RunImageFileChecksAsync()
    {
        var root = NewImageFixtureRoot(); Directory.CreateDirectory(root);
        var uploads = Path.Combine(root, "uploads");
        var validator = new WindowsProductImageContentValidator();
        var storage = new LocalProductImageStorage(uploads, validator);
        var jpeg = Path.Combine(root, "valid.jpg"); var png = Path.Combine(root, "valid.png");
        FixtureImage(jpeg); FixtureImage(png);
        foreach (var path in new[] { jpeg, png })
        {
            var url = await storage.StoreAsync(path);
            Check(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(Path.Combine(uploads, Path.GetFileName(url)))), "full decoder valid " + Path.GetExtension(path));
        }
        async Task Reject(string name, byte[] bytes)
        {
            var path = Path.Combine(root, name); await File.WriteAllBytesAsync(path, bytes);
            var before = Directory.GetFiles(uploads).Length;
            var rejected = false;
            try { await storage.StoreAsync(path); }
            catch (Exception e) when (e is InvalidDataException or ArgumentException or NotSupportedException) { rejected = true; }
            Check(rejected && Directory.GetFiles(uploads).Length == before, "reject with no file published: " + name);
        }
        var jpgBytes = await File.ReadAllBytesAsync(jpeg); var pngBytes = await File.ReadAllBytesAsync(png);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "D2B-browser-fixtures.json"),
            System.Text.Json.JsonSerializer.Serialize(new { jpeg = Convert.ToBase64String(jpgBytes), png = Convert.ToBase64String(pngBytes) }));
        await Reject("header-only.jpg", [255, 216, 255, 0, 255, 217]);
        await Reject("header-only.png", [137,80,78,71,13,10,26,10,0,0,0,0,73,69,78,68,174,66,96,130]);
        await Reject("truncated.jpg", jpgBytes[..(jpgBytes.Length / 2)]);
        await Reject("truncated.png", pngBytes[..(pngBytes.Length / 2)]);
        // Preserve both container signature and ending, corrupt the inner structure.
        var brokenPng = (byte[])pngBytes.Clone(); Array.Fill(brokenPng, (byte)0xFF, 8, brokenPng.Length - 20);
        var brokenJpg = (byte[])jpgBytes.Clone(); Array.Fill(brokenJpg, (byte)0x55, 3, brokenJpg.Length - 5);
        await Reject("corrupt.png", brokenPng); await Reject("corrupt.jpg", brokenJpg);
        await Reject("wrong-extension.jpg", pngBytes);
        await Reject("wrong-extension.png", jpgBytes);
        await Reject("fake-mime.jpg", System.Text.Encoding.UTF8.GetBytes("Content-Type: image/jpeg\r\n<html>not an image</html>"));
        await Reject("valid.webp", Convert.FromBase64String("UklGRh4AAABXRUJQVlA4TBEAAAAvAAAAAAfQ//73v/+BiOh/AAA="));
        await Reject("oversize.jpg", new byte[10 * 1024 * 1024 + 1]);
        var wide = Path.Combine(root, "wide.png"); FixtureImage(wide, 4097, 1); await Reject("too-wide.png", await File.ReadAllBytesAsync(wide));
        var high = Path.Combine(root, "high.png"); FixtureImage(high, 1, 4097); await Reject("too-high.png", await File.ReadAllBytesAsync(high));
        var pixels = Path.Combine(root, "pixels.png"); FixtureImage(pixels, 4001, 4000); await Reject("too-many-pixels.png", await File.ReadAllBytesAsync(pixels));
        var blocked = Path.Combine(root, "blocked-directory"); await File.WriteAllTextAsync(blocked, "owned fixture");
        var ioFailure = false;
        try { await new LocalProductImageStorage(blocked, validator).StoreAsync(png); }
        catch (IOException) { ioFailure = true; }
        Check(ioFailure && await File.ReadAllTextAsync(blocked) == "owned fixture", "file write failure never overwrites directory blocker");
        Check(!storage.TryDelete("/uploads/products/../valid.png") && !storage.TryDelete("/uploads/products/demo-slug.jpg"), "traversal and demo slug delete rejected");
        // Only own generated fixture files; verified root is a unique child of this harness output.
        foreach (var file in Directory.GetFiles(uploads)) File.Delete(file);
        Directory.Delete(uploads);
        foreach (var file in Directory.GetFiles(root)) File.Delete(file);
        Directory.Delete(root);
        Console.WriteLine("PASS: image file checks, fixture cleanup complete; no SQL connection.");
    }

    static async Task RunImageSqlChecksAsync()
    {
        await using var lease = new SqlConnection(Connection); await lease.OpenAsync(); TargetGuard.Verify(lease, null);
        await using (var cmd = lease.CreateCommand())
        {
            cmd.CommandText = "IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE is_user_process=1 AND session_id<>@@SPID AND database_id=DB_ID()) THROW 51040,'Test database in use',1; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'PhoneStore_AdminCrud_S29_8D2B',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; IF @r<0 THROW 51041,'Image test already running',1;";
            await cmd.ExecuteNonQueryAsync();
        }
        var root = NewImageFixtureRoot(); Directory.CreateDirectory(root);
        var prefix = Path.GetFileName(root); var uploads = Path.Combine(root, "uploads");
        var png = Path.Combine(root, "new.png"); FixtureImage(png);
        var storage = new LocalProductImageStorage(uploads, new WindowsProductImageContentValidator());
        int firstId, secondId, brandId, categoryId; string sharedUrl;
        await using (var db = Context())
        {
            var brand = new Brand { BrandName = prefix }; var category = new Category { CategoryName = prefix };
            var a = new Product { ProductName = prefix + " A", Price = 1, StockQuantity = 1, Brand = brand, Category = category };
            var b = new Product { ProductName = prefix + " B", Price = 1, StockQuantity = 1, Brand = brand, Category = category };
            db.AddRange(a, b); await db.SaveChangesAsync(); firstId = a.ProductId; secondId = b.ProductId; brandId = brand.BrandId; categoryId = category.CategoryId;
            var service = new ProductManagementService(new(db), storage);
            Check((await service.AddImageAsync(firstId, png)).Succeeded, "initial committed image");
            sharedUrl = (await service.GetImagesAsync(firstId)).Single().ImageUrl;
            // Controlled shared-reference fixture; the actual UI never copies URLs between Products.
            db.ProductImages.Add(new ProductImage { ProductId = secondId, ImageUrl = sharedUrl, IsPrimary = true });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        var sharedPath = Path.Combine(uploads, Path.GetFileName(sharedUrl));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Replace(int id)
        {
            await using var db = Context(); var service = new ProductManagementService(new(db), storage);
            var old = (await service.GetImagesAsync(id)).Single(); await start.Task;
            var added = await service.AddImageAsync(id, png); Check(added.Succeeded, "concurrent replacement upload");
            Check(File.Exists(sharedPath), "old file retained before DB replacement commit");
            Check((await service.SetPrimaryImageAsync(id, added.Id!.Value)).Succeeded, "new primary committed");
            var removed = await service.RemoveImageAsync(id, old.ImageId);
            Check(removed.Succeeded && removed.Notice is not null, "unlink reports retained/shared orphan notice");
            Check(File.Exists(sharedPath), "never delete shared old file during concurrent replacements");
        }
        var aTask = Replace(firstId); var bTask = Replace(secondId); start.SetResult(); await Task.WhenAll(aTask, bTask);
        await using (var db = Context())
        {
            var service = new ProductManagementService(new(db), storage);
            foreach (var id in new[] { firstId, secondId })
            {
                var images = await service.GetImagesAsync(id);
                Check(images.Count == 1 && images.Single().IsPrimary && images.Single().ImageUrl != sharedUrl, "concurrent final associations one primary");
            }
        }
        // Concurrent first upload for the SAME Product must still result in one primary.
        await using (var db = Context())
        {
            var service = new ProductManagementService(new(db), storage);
            foreach (var image in await service.GetImagesAsync(firstId))
                Check((await service.RemoveImageAsync(firstId, image.ImageId)).Succeeded, "prepare empty image list on owned fixture only");
        }
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Add(int id) { await gate.Task; await using var db = Context(); Check((await new ProductManagementService(new(db), storage).AddImageAsync(id, png)).Succeeded, "concurrent upload serialized by parent lock"); }
        var add1 = Add(firstId); var add2 = Add(firstId); gate.SetResult(); await Task.WhenAll(add1, add2);
        await using (var db = Context()) Check(await db.ProductImages.CountAsync(i => i.ProductId == firstId && i.IsPrimary) == 1, "one primary after same-product concurrent uploads");
        var fault = new FailCommit(); var beforeFiles = Directory.GetFiles(uploads).Length;
        await using (var db = Context(fault))
        {
            var result = await new ProductManagementService(new(db), storage).AddImageAsync(firstId, png);
            Check(!result.Succeeded && fault.Reached && result.Notice is not null, "DB pre-commit failure returns error plus retained-file notice");
        }
        Check(Directory.GetFiles(uploads).Length == beforeFiles + 1 && File.Exists(sharedPath), "valid uncertain/orphan copy retained, old file intact");
        await using (var db = Context())
        {
            Check(await db.ProductImages.CountAsync(i => i.ProductId == firstId) == 2, "failed image DB insert rolled back");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            Check(await db.Products.CountAsync(p => (p.ProductId == firstId || p.ProductId == secondId) && p.ProductName.StartsWith(prefix)) == 2, "cleanup guard owns exactly two Products");
            await db.ProductImages.Where(i => (i.ProductId == firstId || i.ProductId == secondId) && i.Product.ProductName.StartsWith(prefix)).ExecuteDeleteAsync();
            await db.Products.Where(p => (p.ProductId == firstId || p.ProductId == secondId) && p.ProductName.StartsWith(prefix)).ExecuteDeleteAsync();
            await db.Brands.Where(b => b.BrandId == brandId && b.BrandName == prefix).ExecuteDeleteAsync();
            await db.Categories.Where(c => c.CategoryId == categoryId && c.CategoryName == prefix).ExecuteDeleteAsync();
            await tx.CommitAsync();
            foreach (var file in Directory.GetFiles(uploads))
            {
                var url = "/uploads/products/" + Path.GetFileName(file);
                Check(!await db.ProductImages.AnyAsync(i => i.ImageUrl == url), "fixture file no references before controlled cleanup");
                File.Delete(file);
            }
        }
        Directory.Delete(uploads); File.Delete(png); Directory.Delete(root);
        Console.WriteLine("PASS: SQL image concurrency, shared retention, rollback and scoped fixture cleanup.");
    }
}
