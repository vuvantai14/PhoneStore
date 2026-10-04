using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Purchasing;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Business.Purchasing;

public sealed class OrderService(PurchaseStore store) : IOrderService
{
    public async Task<IReadOnlyList<CartProduct>> GetCartProductsAsync(IEnumerable<int> ids, CancellationToken ct = default) =>
        await store.CartProductsAsync(ids.Distinct().ToArray(), ct);

    public async Task<CheckoutCustomer?> GetCustomerAsync(int userId, CancellationToken ct = default)
    {
        var user = await store.CustomerAsync(userId, ct);
        return user is null ? null : new CheckoutCustomer(user.FullName, user.Phone, user.Address);
    }

    public Task<OrderReceipt?> GetReceiptAsync(int orderId, int userId, CancellationToken ct = default) =>
        store.ReceiptAsync(orderId, userId, ct);

    public Task<IReadOnlyList<CustomerOrderRow>> GetCustomerOrdersAsync(int userId, CancellationToken ct = default) =>
        store.CustomerOrdersAsync(userId, ct);

    public async Task<OrderCreationResult> CreateAsync(int userId, IReadOnlyList<CartItem> items,
        ShippingInfo shipping, CancellationToken ct = default)
    {
        if (items.Count == 0) return new(null, "Giỏ hàng của bạn đang trống.");
        if (items.Any(i => i.ProductId <= 0 || i.Quantity <= 0) || items.Select(i => i.ProductId).Distinct().Count() != items.Count)
            return new(null, "Số lượng sản phẩm không hợp lệ.");
        var name = shipping.ReceiverName.Trim();
        var phone = shipping.ReceiverPhone.Trim();
        var address = shipping.ShippingAddress.Trim();
        if (name.Length is < 1 or > 100 || phone.Length is < 1 or > 20 || !new PhoneAttribute().IsValid(phone)
            || address.Length is < 1 or > 300 || shipping.Note?.Length > 500)
            return new(null, "Vui lòng kiểm tra thông tin giao hàng.");

        try
        {
            // Serializable retains stock read locks until commit; conflicting checkouts roll back.
            await using var transaction = await store.BeginAsync(ct);
            if (await store.CustomerAsync(userId, ct) is null)
                return new(null, "Tài khoản không đủ điều kiện đặt hàng.");
            var products = await store.ProductsForOrderAsync(items.Select(i => i.ProductId).ToArray(), ct);
            if (products.Count != items.Count || products.Any(p => !p.IsActive))
                return new(null, "Có sản phẩm không còn khả dụng. Vui lòng kiểm tra giỏ hàng.");

            var order = new Order
            {
                UserId = userId,
                OrderCode = "ORD-" + Guid.NewGuid().ToString("N")[..26],
                ReceiverName = name, ReceiverPhone = phone, ShippingAddress = address,
                Note = string.IsNullOrWhiteSpace(shipping.Note) ? null : shipping.Note.Trim(),
                OrderDate = DateTime.UtcNow, Status = OrderStatus.Pending
            };
            foreach (var item in items)
            {
                var product = products.Single(p => p.ProductId == item.ProductId);
                if (product.StockQuantity < item.Quantity)
                    return new(null, $"Sản phẩm {product.ProductName} không đủ hàng. Vui lòng điều chỉnh số lượng.");
                order.OrderDetails.Add(new OrderDetail { ProductId = product.ProductId, Quantity = item.Quantity, UnitPrice = product.Price });
                order.TotalAmount = checked(order.TotalAmount + product.Price * item.Quantity);
                product.StockQuantity -= item.Quantity;
            }
            if (order.TotalAmount > 9999999999999999.99m)
                return new(null, "Tổng giá trị đơn hàng vượt giới hạn cho phép.");
            await store.SaveOrderAsync(order, ct);
            await transaction.CommitAsync(ct);
            return new(order.OrderId, null);
            // Uncommitted transactions are rolled back on disposal, including validation failures.
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException or OverflowException)
        {
            return new(null, "Chưa thể tạo đơn hàng. Vui lòng kiểm tra giỏ hàng và thử lại.");
        }
    }
}

