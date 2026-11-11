using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Data.Purchasing;

public sealed class PurchaseStore(PhoneStoreDbContext db)
{
    public Task<IDbContextTransaction> BeginAsync(CancellationToken ct) =>
        db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

    public Task<User?> CustomerAsync(int id, CancellationToken ct) =>
        db.Users.AsNoTracking().Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.UserId == id && u.IsActive && u.Role.RoleName == "Customer", ct);

    public void ClearTracking() => db.ChangeTracker.Clear();

    public Task<int?> CheckoutOrderAsync(int userId, string code, CancellationToken ct) =>
        db.Orders.AsNoTracking().Where(o => o.UserId == userId && o.OrderCode == code)
            .Select(o => (int?)o.OrderId).SingleOrDefaultAsync(ct);

    public async Task LockCheckoutAsync(string code, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer()) return;
        var resource = "PhoneStore:checkout:" + code;
        // Transaction-owned, shared by all Web processes; no automatic retry on timeout.
        await db.Database.ExecuteSqlInterpolatedAsync($"DECLARE @r int; EXEC @r = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r < 0 THROW 51020, 'Checkout lock unavailable', 1;", ct);
    }

    public async Task<List<Product>> ProductsForOrderAsync(int[] ids, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer())
            return await db.Products.Where(p => ids.Contains(p.ProductId)).OrderBy(p => p.ProductId).ToListAsync(ct);
        var products = new List<Product>();
        foreach (var id in ids.Distinct().Order())
        {
            var product = await db.Products.FromSqlInterpolated($"SELECT * FROM [Products] WITH (UPDLOCK, HOLDLOCK) WHERE [ProductId]={id}")
                .SingleOrDefaultAsync(ct);
            if (product is not null) products.Add(product);
        }
        return products;
    }

    public Task<List<CartProduct>> CartProductsAsync(int[] ids, CancellationToken ct) =>
        db.Products.AsNoTracking().Where(p => ids.Contains(p.ProductId))
            .Select(p => new CartProduct(p.ProductId, p.ProductName, p.Price, p.StockQuantity, p.IsActive,
                p.ProductImages.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).ThenBy(i => i.ImageId)
                    .Select(i => i.ImageUrl).FirstOrDefault())).ToListAsync(ct);

    public async Task SaveOrderAsync(Order order, CancellationToken ct)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
    }

    public Task<OrderReceipt?> ReceiptAsync(int orderId, int userId, CancellationToken ct) =>
        db.Orders.AsNoTracking().Where(o => o.OrderId == orderId && o.UserId == userId)
            .Select(o => new OrderReceipt
            {
                OrderId = o.OrderId,
                OrderCode = o.OrderCode, OrderDate = o.OrderDate, TotalAmount = o.TotalAmount,
                ReceiverName = o.ReceiverName, ReceiverPhone = o.ReceiverPhone, ShippingAddress = o.ShippingAddress,
                Note = o.Note, Status = o.Status,
                Items = o.OrderDetails.OrderBy(d => d.OrderDetailId)
                    .Select(d => new PurchasedItem(d.Product.ProductName, d.Quantity, d.UnitPrice)).ToList()
            }).SingleOrDefaultAsync(ct);

    public Task<IReadOnlyList<CustomerOrderRow>> CustomerOrdersAsync(int userId, CancellationToken ct) =>
        CustomerOrdersAsync(userId, null, null, null, ct);

    public async Task<IReadOnlyList<CustomerOrderRow>> CustomerOrdersAsync(int userId, OrderStatus? status, DateTime? fromUtc, DateTime? toExclusiveUtc, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().Where(order => order.UserId == userId);
        if (status.HasValue)
            query = query.Where(order => order.Status == status.Value);
        if (fromUtc.HasValue)
            query = query.Where(order => order.OrderDate >= fromUtc.Value);
        if (toExclusiveUtc.HasValue)
            query = query.Where(order => order.OrderDate < toExclusiveUtc.Value);

        return await query
            .OrderByDescending(order => order.OrderDate).ThenByDescending(order => order.OrderId)
            .Select(order => new CustomerOrderRow(order.OrderId, order.OrderCode, order.OrderDate,
                order.TotalAmount, order.Status, order.ReceiverName)).ToListAsync(ct);
    }
}

