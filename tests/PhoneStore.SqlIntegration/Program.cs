using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PhoneStore.Admin.Dialogs;
using PhoneStore.Business.Management;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Context;
using PhoneStore.Data.Management;
using PhoneStore.Data.Purchasing;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Purchasing;

// Opt-in executable. Never run by solution/unit tests; never accepts another target.
internal static partial class Program
{
    const string Database = "PhoneStoreDB_StockConcurrencyTest";
    const string Connection = "Server=localhost;Database=PhoneStoreDB_StockConcurrencyTest;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
    static readonly TargetGuard Guard = new();
    static int productId, customerId;

    [STAThread]
    static int Main(string[] args)
    {
        if (args is ["--selftest-admin-authorization-s29.8d.4b"])
        {
            try { RunAdminAuthorizationGatewayChecksAsync().GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is ["--check-readonly-s29.8d.4b"])
        {
            try { RunReportingAuthorizationReadOnlyAsync().GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is ["--preflight-test-admin-s29.8d.2c.1"])
        {
            try { TestAdminCredentialPreflightAsync().GetAwaiter().GetResult(); return 0; }
            catch { Console.Error.WriteLine("STOP: test Admin preflight failed; no credential update."); return 1; }
        }
        if (args is ["--reset-test-admin-approved-s29.8d.2c.1"])
            return ResetApprovedTestAdminAsync().GetAwaiter().GetResult();
        if (args is ["--check-images-s29.8d.2b"])
        {
            try { RunImageFileChecksAsync().GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is ["--execute-approved-s29.8d.2b"])
        {
            try { RunImageSqlChecksAsync().GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is ["--execute-approved-s29.8d.2a"])
        {
            try { RunAdminCrudAsync().GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is ["--execute-approved-s29.8c.5-http-sync"])
        {
            try { RunHttpCommerceAsync("customer@s29-8c5-sync.test").GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is ["--execute-approved-s29.8c.5-ui"])
        {
            try { ApplicationConfiguration.Initialize(); RunAdminOrdersUi(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is ["--execute-approved-s29.8c-final"])
        {
            try { RunCommerceAsync("S29.8C Final Last Stock Test").GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is ["--execute-approved-s29.8c-http"])
        {
            try { RunHttpCommerceAsync().GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is ["--execute-approved-s29.8c"])
        {
            try { RunCommerceAsync().GetAwaiter().GetResult(); return 0; }
            catch (Exception ex) { Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}"); return 1; }
        }
        if (args is not ["--execute-approved-s29.8b.1"]) return 2;
        try
        {
            RunSqlAsync().GetAwaiter().GetResult();
            ApplicationConfiguration.Initialize();
            RunDialog();
            Console.WriteLine("PASS: SQL + real ProductEditDialog; no main database connection.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"STOP: {ex.GetType().Name}: {ex.Message}");
            return 1; // Never retry or repair.
        }
    }

    static PhoneStoreDbContext Context(params IInterceptor[] extra) => new(
        new DbContextOptionsBuilder<PhoneStoreDbContext>().UseSqlServer(Connection)
            .AddInterceptors(new IInterceptor[] { Guard }.Concat(extra)).Options);
    static ProductManagementService Service(PhoneStoreDbContext db) =>
        new(new ProductManagementStore(db), new NoImages());
    static void Check(bool valid, string name)
    {
        if (!valid) throw new InvalidOperationException(name);
        Console.WriteLine("PASS: " + name);
    }
    static async Task ResetAsync()
    {
        await using var db = Context();
        Check(await db.Products.Where(p => p.ProductId == productId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.StockQuantity, 10).SetProperty(p => p.ProductName, "S29.8B.1 Test Phone")
            .SetProperty(p => p.Price, 1_000_000m)) == 1, "reset ONLY test product to stock10");
    }
    static async Task<int> StockAsync()
    {
        await using var db = Context();
        return await db.Products.Where(p => p.ProductId == productId).Select(p => p.StockQuantity).SingleAsync();
    }
    static async Task CheckoutAsync()
    {
        await using var db = Context();
        var result = await new OrderService(new PurchaseStore(db)).CreateAsync(customerId,
            [new CartItem(productId, 1)], new ShippingInfo("SQL Test Receiver", "0900000000", "Isolated test address", null));
        Check(result.OrderId.HasValue, "actual OrderService checkout committed");
    }

    static async Task RunSqlAsync()
    {
        await using (var db = Context())
        {
            Check(!await db.Users.AnyAsync() && !await db.Products.AnyAsync()
                && !await db.Brands.AnyAsync() && !await db.Categories.AnyAsync(), "fresh empty test fixture");
            Check((await db.Database.GetAppliedMigrationsAsync()).Count() == 2, "two test migrations");
            var admin = new User { FullName = "SQL Test Admin", Email = "admin@s29-8b1.test", RoleId = 1 };
            var customer = new User { FullName = "SQL Test Customer", Email = "customer@s29-8b1.test", RoleId = 2 };
            var hasher = new PasswordHasher<User>();
            // Random test credentials exist only in memory, never printed or reused from main.
            admin.PasswordHash = hasher.HashPassword(admin, Guid.NewGuid().ToString("N"));
            customer.PasswordHash = hasher.HashPassword(customer, Guid.NewGuid().ToString("N"));
            var product = new Product { ProductName = "S29.8B.1 Test Phone", Price = 1_000_000m, StockQuantity = 10,
                Brand = new Brand { BrandName = "S29.8B.1 Test Brand" },
                Category = new Category { CategoryName = "S29.8B.1 Test Category" } };
            db.AddRange(admin, customer, product);
            await db.SaveChangesAsync();
            productId = product.ProductId; customerId = customer.UserId;
        }
        await using (var db = Context())
        {
            var service = Service(db);
            var edit = (await service.GetEditAsync(productId))!;
            Check(edit.OriginalStockQuantity == 10, "Admin snapshot10");
            await CheckoutAsync();
            edit.Price = 9_000_000m; edit.ProductName = "Must not persist";
            var result = await service.SaveAsync(productId, edit);
            Check(!result.Succeeded && result.Error!.Contains("Tồn kho đã thay đổi") && await StockAsync() == 9,
                "checkout10-to9; stale edit rejected; final9");
            var actual = await db.Products.AsNoTracking().SingleAsync();
            Check(actual.Price == 1_000_000m && actual.ProductName == "S29.8B.1 Test Phone", "no partial name/price changes");
        }
        await ResetAsync();
        await using (var firstDb = Context())
        await using (var secondDb = Context())
        {
            var first = (await Service(firstDb).GetEditAsync(productId))!;
            var second = (await Service(secondDb).GetEditAsync(productId))!;
            first.StockQuantity = 11; second.StockQuantity = 12;
            Check((await Service(firstDb).SaveAsync(productId, first)).Succeeded, "first form valid save");
            Check(!(await Service(secondDb).SaveAsync(productId, second)).Succeeded && await StockAsync() == 11,
                "second open form rejected without lost stock");
        }
        await ResetAsync();
        await using (var firstDb = Context())
        await using (var secondDb = Context())
        {
            var first = await firstDb.Products.AsNoTracking().SingleAsync();
            var second = await secondDb.Products.AsNoTracking().SingleAsync();
            first.StockQuantity = 11; second.StockQuantity = 12;
            // Independent SQL connections, genuinely overlapping conditional UPDATEs.
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<int> Update(PhoneStoreDbContext db, Product p)
            { await start.Task; return await new ProductManagementStore(db).UpdateIfStockMatchesAsync(p, 10, default); }
            var a = Update(firstDb, first); var b = Update(secondDb, second); start.SetResult();
            var rows = await Task.WhenAll(a, b);
            var stock = await StockAsync();
            Check(rows.Order().SequenceEqual(new[] { 0, 1 }) && stock == (rows[0] == 1 ? 11 : 12),
                $"atomic two connections affected=[{rows[0]},{rows[1]}], final={stock}");
        }
        await ResetAsync();
        var fault = new FailCommit();
        await using (var db = Context(fault))
        {
            var service = Service(db); var edit = (await service.GetEditAsync(productId))!;
            edit.StockQuantity = 99; edit.Price = 8_000_000m;
            Check(!(await service.SaveAsync(productId, edit)).Succeeded && fault.Reached, "controlled pre-commit failure");
            Check(await StockAsync() == 10 && (await db.Products.AsNoTracking().SingleAsync()).Price == 1_000_000m,
                "rollback preserved test stock and price");
        }
        Check(Guard.ConditionalUpdates > 0, "captured SQL UPDATE with stock predicate");
        Console.WriteLine($"SQL conditional updates={Guard.ConditionalUpdates}; affectedRows={string.Join(',', Guard.Rows)}");
        await ResetAsync(); // Prepare the real form test at stock10.
    }

    static void RunDialog()
    {
        using var db = Context();
        var service = Service(db);
        var edit = service.GetEditAsync(productId).GetAwaiter().GetResult()!;
        var options = service.GetOptionsAsync(productId).GetAwaiter().GetResult();
        using var dialog = new ProductEditDialog(edit, options, (value, ct) => service.SaveAsync(productId, value, ct));
        Exception? failure = null;
        dialog.Shown += async (_, _) =>
        {
            try
            {
                Check(edit.StockQuantity == 10, "real WinForms opened snapshot10");
                await CheckoutAsync();
                ((TextBox)dialog.Controls.Find("ProductNameInput", true).Single()).Text = "UI must not persist";
                ((Button)dialog.Controls.Find("SaveProductButton", true).Single()).PerformClick();
                var label = (Label)dialog.Controls.Find("ValidationMessage", true).Single();
                var until = DateTime.UtcNow.AddSeconds(15);
                while (label.Text == "Đang lưu sản phẩm…" && DateTime.UtcNow < until) await Task.Delay(50);
                Check(label.Text.Contains("Tồn kho đã thay đổi") && label.Text.Contains("Không có thay đổi nào được lưu")
                    && label.Text.Contains("đóng và mở lại"), "real WinForms conflict label: " + label.Text);
                Check(dialog.Visible && dialog.DialogResult != DialogResult.OK && await StockAsync() == 9,
                    "real dialog remains open; test stock9");
                Check((await db.Products.AsNoTracking().SingleAsync()).ProductName == "S29.8B.1 Test Phone", "UI failed save did not change name");
            }
            catch (Exception ex) { failure = ex; }
            finally { dialog.Close(); }
        };
        Application.Run(dialog);
        if (failure is not null) throw failure;
    }

    sealed class NoImages : IProductImageStorage
    {
        public Task<string> StoreAsync(string path, CancellationToken ct = default) => throw new InvalidOperationException("No image writes");
        public bool TryDelete(string url) => throw new InvalidOperationException("No image deletes");
    }
    sealed class ControlledFailure : DbException { public ControlledFailure() : base("Expected test failure before commit") { } }
    sealed class FailCommit : DbTransactionInterceptor
    {
        public bool Reached;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData data, InterceptionResult result, CancellationToken ct = default)
        { TargetGuard.Verify(transaction.Connection!, transaction); Reached = true; throw new ControlledFailure(); }
    }
    sealed class TargetGuard : DbCommandInterceptor
    {
        public int ConditionalUpdates;
        public List<int> Rows { get; } = [];
        public static void Verify(DbConnection connection, DbTransaction? transaction)
        {
            var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
            if (builder.DataSource != "localhost" || builder.InitialCatalog != Database || !builder.IntegratedSecurity)
                throw new InvalidOperationException("Unexpected connection string; STOP");
            using var guard = connection.CreateCommand(); guard.Transaction = transaction;
            guard.CommandText = "SELECT CASE WHEN DB_NAME()=N'PhoneStoreDB_StockConcurrencyTest' AND @@SERVERNAME=N'DESKTOP-ES9362P' THEN 1 ELSE 0 END";
            if (!Equals(guard.ExecuteScalar(), 1)) throw new InvalidOperationException("Unexpected DB_NAME/server; STOP");
        }
        void Before(DbCommand command)
        {
            Verify(command.Connection!, command.Transaction);
            if (command.CommandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
            {
                var where = command.CommandText.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase);
                if (where >= 0 && command.CommandText[where..].Contains("StockQuantity"))
                { Interlocked.Increment(ref ConditionalUpdates); Console.WriteLine("SQL: " + command.CommandText.ReplaceLineEndings(" ")); }
            }
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        { Before(command); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        { Before(command); return ValueTask.FromResult(result); }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData data,
            int result, CancellationToken ct = default)
        { lock (Rows) Rows.Add(result); return ValueTask.FromResult(result); }
    }
}
