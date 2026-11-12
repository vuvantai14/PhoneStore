namespace PhoneStore.Models.Purchasing;

using PhoneStore.Models.Enums;

public sealed record CartItem(int ProductId, int Quantity);
public sealed record CartProduct(int ProductId, string ProductName, decimal Price, int StockQuantity, bool IsActive, string? ImageUrl);
public sealed record ShippingInfo(string ReceiverName, string ReceiverPhone, string ShippingAddress, string? Note);
public sealed record CheckoutCustomer(string FullName, string? Phone, string? Address);
public sealed record OrderCreationResult(int? OrderId, string? Error);
public sealed record PurchasedItem(string ProductName, int Quantity, decimal UnitPrice);
public sealed record CustomerOrderRow(int OrderId, string OrderCode, DateTime OrderDate, decimal TotalAmount,
    OrderStatus Status, string ReceiverName);
public sealed record PagedCustomerOrdersResult(
    IReadOnlyList<CustomerOrderRow> Orders,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public int CurrentPage => PageNumber;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
public sealed class OrderReceipt
{
    public int OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public decimal TotalAmount { get; init; }
    public string ReceiverName { get; init; } = string.Empty;
    public string ReceiverPhone { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
    public string? Note { get; init; }
    public OrderStatus Status { get; init; }
    public List<PurchasedItem> Items { get; init; } = [];
}

