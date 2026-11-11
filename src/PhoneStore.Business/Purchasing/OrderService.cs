using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
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

    public Task<IReadOnlyList<CustomerOrderRow>> GetCustomerOrdersAsync(int userId, OrderStatus? status, DateTime? fromUtc, DateTime? toExclusiveUtc, CancellationToken ct = default) =>
        store.CustomerOrdersAsync(userId, status, fromUtc, toExclusiveUtc, ct);

    public Task<OrderCreationResult> CreateAsync(int userId, IReadOnlyList<CartItem> items,
        ShippingInfo shipping, CancellationToken ct = default) => CreateCoreAsync(userId, items, shipping, null, ct);

    private static string? CheckoutCode(int userId, string? token) =>
        userId > 0 && Guid.TryParseExact(token, "N", out var key)
            ? "ORD-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{key:N}")))[..26]
            : null;

    public async Task<int?> GetCheckoutOrderIdAsync(int userId, string? submissionToken, CancellationToken ct = default)
    {
        var code = CheckoutCode(userId, submissionToken);
        if (code is null || await store.CustomerAsync(userId, ct) is null) return null;
        return await store.CheckoutOrderAsync(userId, code, ct);
    }

    public async Task<OrderCreationResult> CreateIdempotentAsync(int userId, IReadOnlyList<CartItem> items,
        ShippingInfo shipping, string? submissionToken, CancellationToken ct = default)
    {
        var code = CheckoutCode(userId, submissionToken);
        if (code is null) return new(null, "Phiên thanh toán không hợp lệ.");
        var existing = await GetCheckoutOrderIdAsync(userId, submissionToken, ct);
        return existing.HasValue ? new(existing, null) : await CreateCoreAsync(userId, items, shipping, code, ct);
    }

    private async Task<OrderCreationResult> CreateCoreAsync(int userId, IReadOnlyList<CartItem> items,
        ShippingInfo shipping, string? checkoutCode, CancellationToken ct)
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
            if (checkoutCode is not null)
            {
                await store.LockCheckoutAsync(checkoutCode, ct);
            }
            if (await store.CustomerAsync(userId, ct) is null)
                return new(null, "Tài khoản không đủ điều kiện đặt hàng.");
            if (checkoutCode is not null)
            {
                var existing = await store.CheckoutOrderAsync(userId, checkoutCode, ct);
                if (existing.HasValue) return new(existing, null);
            }
            var products = await store.ProductsForOrderAsync(items.Select(i => i.ProductId).ToArray(), ct);
            if (products.Count != items.Count || products.Any(p => !p.IsActive))
                return new(null, "Có sản phẩm không còn khả dụng. Vui lòng kiểm tra giỏ hàng.");

            var order = new Order
            {
                UserId = userId,
                OrderCode = checkoutCode ?? "ORD-" + Guid.NewGuid().ToString("N")[..26],
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
        finally { store.ClearTracking(); }
    }
}

