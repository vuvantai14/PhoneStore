using PhoneStore.Models.Enums;

namespace PhoneStore.Models.Management;

public sealed record CustomerManagementQuery(string? Search, bool? IsActive);
public sealed record CustomerManagementRow(int UserId, string FullName, string Email, string? Phone,
    string? Address, bool IsActive, DateTime CreatedAt)
{
    public string Status => IsActive ? "Đang hoạt động" : "Ngừng hoạt động";
}
public sealed record CustomerEdit(string FullName, string? Phone, string? Address);

public sealed record OrderManagementQuery(string? Search, OrderStatus? Status);
public sealed record OrderManagementRow(int OrderId, string OrderCode, string CustomerName, string CustomerEmail,
    string ReceiverName, string ReceiverPhone, DateTime OrderDate, decimal TotalAmount, OrderStatus Status)
{
    public string StatusLabel => OrderStatusLabels.Display(Status);
}
public sealed record OrderItemRow(string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}
public sealed class OrderManagementDetail
{
    public int OrderId { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public OrderStatus Status { get; init; }
    public decimal TotalAmount { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerEmail { get; init; } = string.Empty;
    public string ReceiverName { get; init; } = string.Empty;
    public string ReceiverPhone { get; init; } = string.Empty;
    public string ShippingAddress { get; init; } = string.Empty;
    public string? Note { get; init; }
    public List<OrderItemRow> Items { get; init; } = [];
    public string StatusLabel => OrderStatusLabels.Display(Status);
    public decimal DetailTotal => Items.Sum(item => item.LineTotal);
}

public static class OrderStatusLabels
{
    public static string Display(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Shipping => "Đang giao",
        OrderStatus.Completed => "Hoàn thành",
        OrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };
}
