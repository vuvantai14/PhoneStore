using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using PhoneStore.Models.Enums;

namespace PhoneStore.Api.Contracts.Orders;

public class OrderPageQuery
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [EnumDataType(typeof(OrderStatus))] public OrderStatus? Status { get; init; }
}

public sealed class CustomerOrderQuery : OrderPageQuery
{
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToExclusiveUtc { get; init; }
}

public sealed class AdminOrderQuery : OrderPageQuery
{
    public string? Search { get; init; }
}

public sealed record OrderPage(IReadOnlyList<OrderSummary> Items, int TotalCount, int Page, int PageSize);
public sealed record OrderSummary(int OrderId, string OrderCode, DateTime OrderDate, string Status, decimal TotalAmount,
    string ReceiverName);
public sealed record OrderLine(string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}
public sealed record OrderDetailResponse(OrderSummary Order, string ReceiverPhone, string ShippingAddress,
    string? Note, IReadOnlyList<OrderLine> Items);

public sealed record OrderStatusChange
{
    [JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>)), EnumDataType(typeof(OrderStatus))]
    public required OrderStatus ExpectedStatus { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>)), EnumDataType(typeof(OrderStatus))]
    public required OrderStatus TargetStatus { get; init; }
}
