using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PhoneStore.Business.Management;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Context;
using PhoneStore.Data.Management;
using PhoneStore.Data.Purchasing;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Management;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Tests;

public sealed class ProductStockConcurrencyTests
{
    [Fact]
    public async Task Unchanged_stock_allows_save_without_creating_a_duplicate()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        var service = Service(test.Db);
        var edit = (await service.GetEditAsync(product.ProductId))!;
        Assert.Equal(10, edit.OriginalStockQuantity);
        edit.StockQuantity = 12;
        edit.ProductName = "Tên mới";
        edit.Price = 2_000_000m;

        Assert.True((await service.SaveAsync(product.ProductId, edit)).Succeeded);
        var actual = await test.Db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(12, actual.StockQuantity);
        Assert.Equal("Tên mới", actual.ProductName);
        Assert.Equal(2_000_000m, actual.Price);
        Assert.Equal(2, await test.Db.Roles.CountAsync());
        Assert.Single(await test.Db.Users.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Checkout_before_save_preserves_stock_and_rejects_entire_edit(bool changeStock)
    {
        await using var test = await TestDatabase.CreateAsync();
        var (customer, product) = await test.SeedCatalogAsync();
        await using var adminDb = OtherContext(test.Db);
        var service = Service(adminDb);
        var edit = (await service.GetEditAsync(product.ProductId))!;
        edit.ProductName = "Không được lưu";
        edit.Price = 9_000_000m;
        if (changeStock) edit.StockQuantity = 20;
        var checkout = await Checkout(test.Db, customer.UserId, product.ProductId, 1);
        Assert.NotNull(checkout.OrderId);

        var result = await service.SaveAsync(product.ProductId, edit);
        Assert.False(result.Succeeded);
        Assert.Contains("Tồn kho đã thay đổi", result.Error);
        Assert.Contains("Không có thay đổi nào được lưu", result.Error);
        Assert.Contains("đóng và mở lại", result.Error);
        var actual = await test.Db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(9, actual.StockQuantity);
        Assert.Equal(product.ProductName, actual.ProductName);
        Assert.Equal(1_000_000m, actual.Price);
        Assert.Single(await test.Db.Orders.ToListAsync());
        Assert.Single(await test.Db.OrderDetails.ToListAsync());
        Assert.Empty(adminDb.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Two_open_forms_cannot_overwrite_the_first_stock_change()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        await using var firstDb = OtherContext(test.Db);
        await using var secondDb = OtherContext(test.Db);
        var first = Service(firstDb);
        var second = Service(secondDb);
        var firstEdit = (await first.GetEditAsync(product.ProductId))!;
        var staleEdit = (await second.GetEditAsync(product.ProductId))!;
        firstEdit.StockQuantity = 15;
        staleEdit.StockQuantity = 30;
        staleEdit.ProductName = "Tên cũ bị từ chối";

        Assert.True((await first.SaveAsync(product.ProductId, firstEdit)).Succeeded);
        Assert.False((await second.SaveAsync(product.ProductId, staleEdit)).Succeeded);
        Assert.Equal(15, (await test.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        Assert.Equal(product.ProductName, (await test.Db.Products.AsNoTracking().SingleAsync()).ProductName);
        var refreshed = (await second.GetEditAsync(product.ProductId))!;
        refreshed.StockQuantity = 16;
        Assert.True((await second.SaveAsync(product.ProductId, refreshed)).Succeeded);
        Assert.Equal(16, (await test.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public async Task Missing_or_invalid_original_stock_fails_closed(int? original)
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        var service = Service(test.Db);
        var edit = (await service.GetEditAsync(product.ProductId))!;
        edit.OriginalStockQuantity = original;
        edit.StockQuantity = 50;
        var result = await service.SaveAsync(product.ProductId, edit);
        Assert.False(result.Succeeded);
        Assert.Contains("tồn kho gốc", result.Error);
        Assert.Equal(10, (await test.Db.Products.AsNoTracking().SingleAsync()).StockQuantity);
    }

    [Fact]
    public async Task Deleted_product_is_not_recreated_by_a_stale_form()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        var service = Service(test.Db);
        var edit = (await service.GetEditAsync(product.ProductId))!;
        await test.Db.Products.Where(p => p.ProductId == product.ProductId).ExecuteDeleteAsync();

        Assert.False((await service.SaveAsync(product.ProductId, edit)).Succeeded);
        Assert.Empty(await test.Db.Products.ToListAsync());
        Assert.Empty(await test.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task New_product_does_not_require_an_original_stock_snapshot()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        var service = Service(test.Db);
        var edit = (await service.GetEditAsync(product.ProductId))!;
        edit.ProductName = "Sản phẩm mới";
        edit.OriginalStockQuantity = null;
        edit.StockQuantity = 3;
        var result = await service.SaveAsync(null, edit);
        Assert.True(result.Succeeded);
        Assert.NotEqual(product.ProductId, result.Id);
        Assert.Equal(2, await test.Db.Products.CountAsync());
    }

    [Fact]
    public async Task Database_update_contains_original_stock_in_where_even_for_same_stock_submission()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        var capture = new UpdateCapture();
        await using var context = OtherContext(test.Db, capture);
        var service = Service(context);
        var edit = (await service.GetEditAsync(product.ProductId))!;
        edit.ProductName = "Chỉ đổi tên";
        Assert.True((await service.SaveAsync(product.ProductId, edit)).Succeeded);
        var sql = Assert.Single(capture.Updates);
        var where = sql[sql.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase)..];
        Assert.Contains("StockQuantity", where);
        Assert.Contains("ProductId", where);
    }

    [Fact]
    public async Task Failure_before_commit_rolls_back_successful_conditional_update()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (_, product) = await test.SeedCatalogAsync();
        var failure = new FailCommit();
        await using var context = OtherContext(test.Db, failure);
        var service = Service(context);
        var edit = (await service.GetEditAsync(product.ProductId))!;
        edit.StockQuantity = 99;
        edit.Price = 8_000_000m;
        edit.ProductName = "Không được commit";

        Assert.False((await service.SaveAsync(product.ProductId, edit)).Succeeded);
        Assert.True(failure.ReachedCommit);
        var actual = await test.Db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(10, actual.StockQuantity);
        Assert.Equal(1_000_000m, actual.Price);
        Assert.Equal(product.ProductName, actual.ProductName);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Empty(await test.Db.Orders.ToListAsync());
    }

    [Fact]
    public async Task Checkout_after_admin_save_uses_new_stock_and_server_price()
    {
        await using var test = await TestDatabase.CreateAsync();
        var (customer, product) = await test.SeedCatalogAsync();
        await using var adminDb = OtherContext(test.Db);
        var service = Service(adminDb);
        var edit = (await service.GetEditAsync(product.ProductId))!;
        edit.StockQuantity = 4;
        edit.Price = 2_000_000m;
        Assert.True((await service.SaveAsync(product.ProductId, edit)).Succeeded);
        // Separate checkout context models the next Web request, without stale tracked entities.
        await using var checkoutDb = OtherContext(test.Db);
        Assert.NotNull((await Checkout(checkoutDb, customer.UserId, product.ProductId, 2)).OrderId);
        Assert.Equal(2, (await checkoutDb.Products.AsNoTracking().SingleAsync()).StockQuantity);
        var order = await checkoutDb.Orders.Include(o => o.OrderDetails).SingleAsync();
        Assert.Equal(4_000_000m, order.TotalAmount);
        Assert.Equal(2_000_000m, order.OrderDetails.Single().UnitPrice);
    }

    private static ProductManagementService Service(PhoneStoreDbContext db) =>
        new(new ProductManagementStore(db), new NoImageWrites());

    private static PhoneStoreDbContext OtherContext(PhoneStoreDbContext db, params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<PhoneStoreDbContext>().UseSqlite(db.Database.GetDbConnection())
            .AddInterceptors(interceptors).Options);

    private static Task<OrderCreationResult> Checkout(PhoneStoreDbContext db, int userId, int productId, int quantity) =>
        new OrderService(new PurchaseStore(db)).CreateAsync(userId, [new CartItem(productId, quantity)],
            new ShippingInfo("Người nhận", "0900000000", "Địa chỉ nhận hàng", null));

    private sealed class NoImageWrites : IProductImageStorage
    {
        public Task<string> StoreAsync(string sourceFile, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Stock editing must not upload images.");
        public bool TryDelete(string imageUrl) => throw new InvalidOperationException("Stock editing must not delete images.");
    }

    private sealed class UpdateCapture : DbCommandInterceptor
    {
        public List<string> Updates { get; } = [];
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase))
                Updates.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailCommit : DbTransactionInterceptor
    {
        public bool ReachedCommit { get; private set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            ReachedCommit = true;
            throw new SqliteException("Isolated test: failure before commit", 1);
        }
    }
}
