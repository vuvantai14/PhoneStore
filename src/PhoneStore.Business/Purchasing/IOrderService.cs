using PhoneStore.Models.Enums;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Business.Purchasing;

public interface IOrderService
{
    Task<IReadOnlyList<CartProduct>> GetCartProductsAsync(IEnumerable<int> ids, CancellationToken ct = default);
    Task<CheckoutCustomer?> GetCustomerAsync(int userId, CancellationToken ct = default);
    Task<OrderCreationResult> CreateAsync(int userId, IReadOnlyList<CartItem> items, ShippingInfo shipping, CancellationToken ct = default);
    Task<OrderCreationResult> CreateIdempotentAsync(int userId, IReadOnlyList<CartItem> items, ShippingInfo shipping, string? submissionToken, CancellationToken ct = default);
    Task<int?> GetCheckoutOrderIdAsync(int userId, string? submissionToken, CancellationToken ct = default);
    Task<OrderReceipt?> GetReceiptAsync(int orderId, int userId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerOrderRow>> GetCustomerOrdersAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<CustomerOrderRow>> GetCustomerOrdersAsync(int userId, OrderStatus? status, DateTime? fromUtc, DateTime? toExclusiveUtc, CancellationToken ct = default);
    Task<PagedCustomerOrdersResult> GetPagedCustomerOrdersAsync(int userId, OrderStatus? status, DateTime? fromUtc, DateTime? toExclusiveUtc, int page = 1, int pageSize = 5, CancellationToken ct = default);
}

