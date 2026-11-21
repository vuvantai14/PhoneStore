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

public sealed record OrderSummary(
    int OrderId,
    string OrderCode,
    DateTime OrderDate,
    string Status,
    decimal TotalAmount,
    string ReceiverName,
    string? CustomerName = null,
    string? CustomerEmail = null,
    string? ReceiverPhone = null,
    string? StatusLabel = null);

public sealed record OrderLine(string ProductName, int Quantity, decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public sealed record OrderDetailResponse(
    OrderSummary Order,
    string ReceiverPhone,
    string ShippingAddress,
    string? Note,
    IReadOnlyList<OrderLine> Items,
    string? CustomerName = null,
    string? CustomerEmail = null,
    string? StatusLabel = null,
    IReadOnlyList<string>? NextStatuses = null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderItemRequest
{
    [Range(1, int.MaxValue)]
    public required int ProductId { get; init; }

    [Range(1, 1000)]
    public required int Quantity { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public required string ReceiverName { get; init; }

    [Required, Phone, StringLength(20, MinimumLength = 1)]
    public required string ReceiverPhone { get; init; }

    [Required, StringLength(300, MinimumLength = 1)]
    public required string ShippingAddress { get; init; }

    [StringLength(500)]
    public string? Note { get; init; }

    [Required, MinLength(1), MaxLength(50)]
    public required IReadOnlyList<CreateOrderItemRequest> Items { get; init; }
}

public sealed record OrderCreatedResponse(
    int OrderId,
    string OrderCode,
    string Status,
    decimal TotalAmount,
    bool Replayed);

public sealed record OrderStatusChange
{
    [JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>)), EnumDataType(typeof(OrderStatus))]
    public required OrderStatus ExpectedStatus { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>)), EnumDataType(typeof(OrderStatus))]
    public required OrderStatus TargetStatus { get; init; }
}
