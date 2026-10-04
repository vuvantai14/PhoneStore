using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;

namespace PhoneStore.Data.Management;

public sealed class OrderManagementStore(PhoneStoreDbContext db)
{
    public async Task<IReadOnlyList<OrderManagementRow>> ListAsync(OrderManagementQuery filter, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking();
        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
            query = query.Where(order => order.OrderCode.Contains(search) || order.ReceiverName.Contains(search)
                || order.ReceiverPhone.Contains(search) || order.User.Email.Contains(search));
        if (filter.Status.HasValue) query = query.Where(order => order.Status == filter.Status.Value);
        return await query.OrderByDescending(order => order.OrderDate).ThenByDescending(order => order.OrderId)
            .Select(order => new OrderManagementRow(order.OrderId, order.OrderCode, order.User.FullName,
                order.User.Email, order.ReceiverName, order.ReceiverPhone, order.OrderDate,
                order.TotalAmount, order.Status)).ToListAsync(ct);
    }

    public Task<OrderManagementDetail?> GetAsync(int id, CancellationToken ct) => db.Orders.AsNoTracking()
        .Where(order => order.OrderId == id).Select(order => new OrderManagementDetail
        {
            OrderId = order.OrderId, OrderCode = order.OrderCode, OrderDate = order.OrderDate,
            Status = order.Status, TotalAmount = order.TotalAmount,
            CustomerName = order.User.FullName, CustomerEmail = order.User.Email,
            ReceiverName = order.ReceiverName, ReceiverPhone = order.ReceiverPhone,
            ShippingAddress = order.ShippingAddress, Note = order.Note,
            Items = order.OrderDetails.OrderBy(detail => detail.OrderDetailId)
                .Select(detail => new OrderItemRow(detail.Product.ProductName, detail.Quantity, detail.UnitPrice)).ToList()
        }).SingleOrDefaultAsync(ct);

    public Task<IDbContextTransaction> BeginAsync(CancellationToken ct) =>
        db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

    public Task<Order?> LockAsync(int id, CancellationToken ct)
    {
        if (!db.Database.IsSqlServer())
            return db.Orders.SingleOrDefaultAsync(order => order.OrderId == id, ct);
        return db.Orders
            .FromSqlInterpolated($"SELECT * FROM [Orders] WITH (UPDLOCK, HOLDLOCK) WHERE [OrderId] = {id}")
            .SingleOrDefaultAsync(ct);
    }

    public Task<List<OrderDetail>> DetailsForCancellationAsync(int orderId, CancellationToken ct) =>
        db.OrderDetails.Include(detail => detail.Product).Where(detail => detail.OrderId == orderId)
            .OrderBy(detail => detail.OrderDetailId).ToListAsync(ct);

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public void ClearTracking() => db.ChangeTracker.Clear();
}
