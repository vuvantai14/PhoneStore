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
public sealed class OrderReceipt
{
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

