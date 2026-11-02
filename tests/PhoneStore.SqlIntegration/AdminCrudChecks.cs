using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;
using PhoneStore.Admin.Management;
using PhoneStore.Business.Management;
using PhoneStore.Data.Context;
using PhoneStore.Data.Management;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Management;

internal static partial class Program
{
    static async Task RunAdminCrudAsync()
    {
        // Exclusive cooperative test lock, plus check all other sessions before any fixture write.
        await using var lease = new SqlConnection(Connection);
        await lease.OpenAsync(); TargetGuard.Verify(lease, null);
        await using (var cmd = lease.CreateCommand())
        {
            cmd.CommandText = "IF EXISTS(SELECT 1 FROM sys.dm_exec_sessions WHERE is_user_process=1 AND session_id<>@@SPID AND database_id=DB_ID()) THROW 51030,'Test database in use; STOP',1; DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'PhoneStore_AdminCrud_S29_8D2A',@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=0; IF @r<0 THROW 51031,'Another CRUD test is running',1;";
            await cmd.ExecuteNonQueryAsync();
        }
        var prefix = "D2A-" + Guid.NewGuid().ToString("N")[..12];
        var root = Path.Combine(AppContext.BaseDirectory, "crud-fixtures", prefix);
        Directory.CreateDirectory(root);
        var uploads = Path.Combine(root, "uploads");
        var storage = new LocalProductImageStorage(uploads, new WindowsProductImageContentValidator());
        int brand, category, product, otherProduct, order;
        int[] baseline;
        await using (var db = Context())
        {
            baseline = [await db.Products.CountAsync(), await db.ProductImages.CountAsync(), await db.Brands.CountAsync(),
                await db.Categories.CountAsync(), await db.Orders.CountAsync(), await db.OrderDetails.CountAsync(), await db.Users.CountAsync()];
            Check((await db.Database.GetAppliedMigrationsAsync()).Count() == 2, "existing test schema; no migrations applied");
            var lookups = new LookupManagementService(new(db));
            brand = (await lookups.SaveAsync(LookupKind.Brand, null, new(prefix + " Brand", null, true))).Id!.Value;
            category = (await lookups.SaveAsync(LookupKind.Category, null, new(prefix + " Category", null, true))).Id!.Value;
            Check(!(await lookups.SaveAsync(LookupKind.Brand, null, new(prefix.ToLowerInvariant() + " brand", null, true))).Succeeded, "duplicate Brand rejected case-insensitively");
            Check(!(await lookups.SaveAsync(LookupKind.Category, null, new(prefix + " Category", null, true))).Succeeded, "duplicate Category rejected");
            Check(!(await lookups.SaveAsync(LookupKind.Brand, null, new(" ", null, true))).Succeeded, "required lookup name");
            Check((await lookups.SaveAsync(LookupKind.Brand, brand, new(prefix + " Renamed Brand", "Changed", true))).Succeeded, "edit Brand");
            Check((await lookups.SaveAsync(LookupKind.Category, category, new(prefix + " Renamed Category", "Changed", true))).Succeeded, "edit Category");
            Check((await lookups.ListAsync(LookupKind.Brand, prefix)).Count == 1 && (await lookups.ListAsync(LookupKind.Category, prefix)).Count == 1, "lookup search");
            var products = new ProductManagementService(new(db), storage);
            ProductEdit Edit(string name) => new() { ProductName = name, BrandId = brand, CategoryId = category, Price = 1000000, StockQuantity = 10 };
            product = (await products.SaveAsync(null, Edit(prefix + " Phone"))).Id!.Value;
            otherProduct = (await products.SaveAsync(null, Edit(prefix + " Other"))).Id!.Value;
            var repeatedName = await products.SaveAsync(null, Edit(prefix + " Phone"));
            Check(repeatedName.Succeeded, "duplicate product display names allowed by current schema; distinct IDs");
            // Owned duplicate is included in final prefix-scoped cleanup.
            Check((await products.ListAsync(new(prefix, brand, category, true))).Count == 3, "product search and all filters");
            foreach (var edit in new[] { Edit(""), Edit(prefix + " invalid price"), Edit(prefix + " invalid stock") }.Select((e, i) => { if (i == 1) e.Price = -1; if (i == 2) e.StockQuantity = -1; return e; }))
                Check(!(await products.SaveAsync(null, edit)).Succeeded, "invalid product input rejected");
            var customer = await db.Users.Where(u => u.RoleId == 2).Select(u => u.UserId).FirstAsync();
            var history = new Order { OrderCode = prefix + "-order", UserId = customer, ReceiverName = prefix,
                ReceiverPhone = "0900000000", ShippingAddress = "Isolated fixture", OrderDate = DateTime.UtcNow,
                Status = PhoneStore.Models.Enums.OrderStatus.Cancelled, TotalAmount = 1000000,
                OrderDetails = [new OrderDetail { ProductId = product, Quantity = 1, UnitPrice = 1000000 }] };
            db.Orders.Add(history); await db.SaveChangesAsync(); order = history.OrderId; db.ChangeTracker.Clear();
            Check((await products.SetActiveAsync(product, false)).Succeeded && await db.OrderDetails.AnyAsync(d => d.OrderId == order), "deactivate referenced Product retains history");
            Check((await products.SetActiveAsync(product, true)).Succeeded, "reactivate Product");
            Check((await lookups.SetActiveAsync(LookupKind.Brand, brand, false)).Succeeded && (await lookups.SetActiveAsync(LookupKind.Category, category, false)).Succeeded, "deactivate referenced lookups retains Products");
            Check(!(await products.SaveAsync(null, Edit(prefix + " inactive reference"))).Succeeded, "new Product cannot use inactive lookup");
            var existing = (await products.GetEditAsync(product))!;
            existing.Price = 2000000;
            Check((await products.SaveAsync(product, existing)).Succeeded, "edit retains existing inactive associations");
            Check((await lookups.SetActiveAsync(LookupKind.Brand, brand, true)).Succeeded && (await lookups.SetActiveAsync(LookupKind.Category, category, true)).Succeeded, "reactivate lookups");
        }
        foreach (var target in new[] { "product", "brand", "category" })
        {
            await using var db = Context();
            var rejected = false;
            try
            {
                if (target == "product") await db.Products.Where(p => p.ProductId == product).ExecuteDeleteAsync();
                else if (target == "brand") await db.Brands.Where(b => b.BrandId == brand).ExecuteDeleteAsync();
                else await db.Categories.Where(c => c.CategoryId == category).ExecuteDeleteAsync();
            }
            catch (SqlException e) when (e.Number == 547) { rejected = true; }
            Check(rejected, "SQL FK rejects hard delete referenced " + target);
        }
        await using (var db = Context())
        {
            var products = new ProductManagementService(new(db), storage);
            var first = (await products.GetEditAsync(product))!;
            var second = (await products.GetEditAsync(product))!;
            await db.Products.Where(p => p.ProductId == product).ExecuteUpdateAsync(s => s.SetProperty(p => p.StockQuantity, 9));
            first.ProductName = prefix + " stale overwrite";
            Check(!(await products.SaveAsync(product, first)).Succeeded && await db.Products.Where(p => p.ProductId == product).Select(p => p.StockQuantity).SingleAsync() == 9, "stale snapshot10 rejects stock9 overwrite");
            first = (await products.GetEditAsync(product))!; second = (await products.GetEditAsync(product))!;
            first.StockQuantity = 11; second.StockQuantity = 12;
            Check((await products.SaveAsync(product, first)).Succeeded && !(await products.SaveAsync(product, second)).Succeeded, "two forms only first stock change succeeds");
        }
        var png = Path.Combine(root, "source.png");
        using (var bitmap = new System.Drawing.Bitmap(2, 2)) bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        var jpg = Path.Combine(root, "source.jpg");
        using (var bitmap = new System.Drawing.Bitmap(2, 2)) bitmap.Save(jpg, System.Drawing.Imaging.ImageFormat.Jpeg);
        var invalid = Path.Combine(root, "invalid.jpg"); await File.WriteAllBytesAsync(invalid, [1, 2, 3]);
        await using (var db = Context())
        {
            var products = new ProductManagementService(new(db), storage);
            var first = await products.AddImageAsync(product, png);
            var second = await products.AddImageAsync(product, jpg);
            Check(first.Succeeded && second.Succeeded, "real PNG/JPEG uploads");
            Check(!(await products.AddImageAsync(product, invalid)).Succeeded, "invalid image rejected");
            Check((await products.SetPrimaryImageAsync(product, second.Id!.Value)).Succeeded, "set replacement image primary");
            Check((await products.SetImageOrderAsync(product, second.Id.Value, 0)).Succeeded && !(await products.SetImageOrderAsync(product, second.Id.Value, -1)).Succeeded, "image order and negative validation");
            Check(!(await products.RemoveImageAsync(otherProduct, first.Id!.Value)).Succeeded, "foreign Product image cannot be removed");
            Check((await products.RemoveImageAsync(product, first.Id.Value)).Succeeded, "remove old image association; retain file safely");
            var images = await products.GetImagesAsync(product);
            Check(images.Count == 1 && images[0].IsPrimary && File.Exists(Path.Combine(uploads, Path.GetFileName(images[0].ImageUrl))), "exactly one primary and existing image path");
            Check((await products.RemoveImageAsync(product, second.Id.Value)).Succeeded && Directory.GetFiles(uploads).Length == 2, "last image remove retains orphan files for controlled cleanup");
        }
        var fault = new FailCommit();
        await using (var db = Context(fault))
        {
            var products = new ProductManagementService(new(db), storage);
            var edit = (await products.GetEditAsync(product))!; edit.StockQuantity = 99;
            Check(!(await products.SaveAsync(product, edit)).Succeeded && fault.Reached, "product commit fault returns error");
            Check(!(await products.AddImageAsync(product, png)).Succeeded && Directory.GetFiles(uploads).Length == 3, "image DB failure rollback with retained validated orphan");
        }
        await using (var db = Context()) Check(await db.Products.Where(p => p.ProductId == product).Select(p => p.StockQuantity).SingleAsync() == 11, "fault did not persist stock99");
        using (var provider = new ServiceCollection().BuildServiceProvider())
        {
            var session = new AdminSession();
            var gateway = new CatalogManagementGateway(provider.GetRequiredService<IServiceScopeFactory>(), session);
            foreach (var action in new Func<Task>[] {
                async () => { await gateway.WithProductsAsync(_ => Task.FromResult(1)); },
                async () => { await gateway.WithLookupsAsync(_ => Task.FromResult(1)); } })
            {
                try { await action(); throw new InvalidOperationException("Unauthorized gateway accepted"); }
                catch (UnauthorizedAccessException) { Check(true, "unauthenticated/cleared gateway rejects before resolving service"); }
            }
            var rejected = false;
            try { typeof(AdminSession).GetMethod("Establish", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(session,
                [new PhoneStore.Models.Authentication.AdminIdentity(2, "Customer", "not-used@test.local", "Customer")]); }
            catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
            Check(rejected, "Customer cannot establish Admin session");
        }
        // Cleanup is authorized ONLY for this run's prefix and IDs. Never touches older fixtures.
        await using (var db = Context())
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            Check(await db.Orders.CountAsync(o => o.OrderId == order && o.ReceiverName == prefix) == 1, "cleanup owned order guard");
            await db.OrderDetails.Where(d => d.OrderId == order && d.Order.ReceiverName == prefix).ExecuteDeleteAsync();
            await db.Orders.Where(o => o.OrderId == order && o.ReceiverName == prefix).ExecuteDeleteAsync();
            await db.ProductImages.Where(i => i.Product.BrandId == brand && i.Product.ProductName.StartsWith(prefix)).ExecuteDeleteAsync();
            Check(await db.Products.Where(p => p.BrandId == brand && p.CategoryId == category && p.ProductName.StartsWith(prefix)).ExecuteDeleteAsync() == 3, "cleanup exactly three owned Products");
            Check(await db.Brands.Where(b => b.BrandId == brand && b.BrandName.StartsWith(prefix)).ExecuteDeleteAsync() == 1, "cleanup own Brand");
            Check(await db.Categories.Where(c => c.CategoryId == category && c.CategoryName.StartsWith(prefix)).ExecuteDeleteAsync() == 1, "cleanup own Category");
            await tx.CommitAsync();
            int[] after = [await db.Products.CountAsync(), await db.ProductImages.CountAsync(), await db.Brands.CountAsync(), await db.Categories.CountAsync(), await db.Orders.CountAsync(), await db.OrderDetails.CountAsync(), await db.Users.CountAsync()];
            Check(after.SequenceEqual(baseline), "all test row counts restored; old fixtures and Users retained");
        }
        foreach (var path in new[] { png, jpg, invalid }) File.Delete(path);
        foreach (var file in Directory.GetFiles(uploads)) File.Delete(file); // Only owned, now-unreferenced fixture files.
        Check(Directory.GetFiles(uploads).Length == 0, "no new uploaded files remain");
        Directory.Delete(uploads); Directory.Delete(root);
        Console.WriteLine("PASS S29.8D.2A SQL/service CRUD. No new database, migrations, main connection, full UI interaction or old fixture cleanup.");
    }
}

