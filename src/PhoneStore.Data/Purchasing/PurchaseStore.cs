using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Data.Purchasing;

public sealed class PurchaseStore(PhoneStoreDbContext db)
{
    public Task<IDbContextTransaction> BeginAsync(CancellationToken ct) =>
        db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

    public Task<User?> CustomerAsync(int id, CancellationToken ct) =>
        db.Users.AsNoTracking().Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.UserId == id && u.IsActive && u.Role.RoleName == "Customer", ct);

    public Task<List<Product>> ProductsForOrderAsync(int[] ids, CancellationToken ct) =>
        db.Products.Where(p => ids.Contains(p.ProductId)).OrderBy(p => p.ProductId).ToListAsync(ct);

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
                OrderCode = o.OrderCode, OrderDate = o.OrderDate, TotalAmount = o.TotalAmount,
                ReceiverName = o.ReceiverName, ReceiverPhone = o.ReceiverPhone, ShippingAddress = o.ShippingAddress,
                Note = o.Note, Status = o.Status,
                Items = o.OrderDetails.OrderBy(d => d.OrderDetailId)
                    .Select(d => new PurchasedItem(d.Product.ProductName, d.Quantity, d.UnitPrice)).ToList()
            }).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<CustomerOrderRow>> CustomerOrdersAsync(int userId, CancellationToken ct) =>
        await db.Orders.AsNoTracking().Where(order => order.UserId == userId)
            .OrderByDescending(order => order.OrderDate).ThenByDescending(order => order.OrderId)
            .Select(order => new CustomerOrderRow(order.OrderId, order.OrderCode, order.OrderDate,
                order.TotalAmount, order.Status, order.ReceiverName)).ToListAsync(ct);
}

