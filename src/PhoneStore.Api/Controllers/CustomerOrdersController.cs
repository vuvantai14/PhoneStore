using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Orders;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Business.Management;
using PhoneStore.Business.Purchasing;
using PhoneStore.Models.Management;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Route("api/orders")]
[Authorize(Policy = ApiAccess.Customer)]
[EnableRateLimiting(ApiPolicies.ProtectedRateLimit)]
[RequestTimeout(ApiPolicies.CatalogTimeout)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
public sealed class CustomerOrdersController(
    IOrderService orders,
    IOrderManagementService orderManagement) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Orders.Read")]
    [ProducesResponseType<OrderPage>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<ActionResult<OrderPage>> List([FromQuery] CustomerOrderQuery query, CancellationToken ct)
    {
        if (ApiAccess.UserId(User) is not int owner) return Forbid();
        if (query.FromUtc >= query.ToExclusiveUtc)
            return Problem(statusCode: 400, detail: "The end of the range must be after the start.");
        var page = await orders.GetPagedCustomerOrdersAsync(owner, query.Status, query.FromUtc?.UtcDateTime,
            query.ToExclusiveUtc?.UtcDateTime, query.Page, query.PageSize, ct);
        return new OrderPage(page.Orders.Select(o => new OrderSummary(o.OrderId, o.OrderCode, DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc),
            o.Status.ToString(), o.TotalAmount, o.ReceiverName, StatusLabel: OrderStatusLabels.Display(o.Status))).ToArray(),
            page.TotalCount, page.PageNumber, page.PageSize);
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = "Orders.Read")]
    [ProducesResponseType<OrderDetailResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<OrderDetailResponse>> Detail(int id, CancellationToken ct)
    {
        if (ApiAccess.UserId(User) is not int owner) return Forbid();
        var o = await orders.GetReceiptAsync(id, owner, ct);
        if (o is null) return Problem(statusCode: 404, detail: "Order not found.");
        return new OrderDetailResponse(
            new OrderSummary(o.OrderId, o.OrderCode, DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc), o.Status.ToString(),
                o.TotalAmount, o.ReceiverName, ReceiverPhone: o.ReceiverPhone, StatusLabel: OrderStatusLabels.Display(o.Status)),
            o.ReceiverPhone,
            o.ShippingAddress,
            o.Note,
            o.Items.Select(i => new OrderLine(i.ProductName, i.Quantity, i.UnitPrice)).ToArray(),
            StatusLabel: OrderStatusLabels.Display(o.Status));
    }

    [HttpPost]
    [Authorize(Policy = "Orders.Write")]
    [ProducesResponseType<OrderCreatedResponse>(201)]
    [ProducesResponseType<OrderCreatedResponse>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Create(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CreateOrderRequest request,
        CancellationToken ct)
    {
        if (ApiAccess.UserId(User) is not int owner) return Forbid();

        var submissionToken = NormalizeIdempotencyToken(idempotencyKey);
        if (submissionToken is null)
            return Problem(statusCode: 400, title: "Bad Request",
                detail: "A valid Idempotency-Key header (1-128 printable characters) is required.");

        var preExistingOrderId = await orders.GetCheckoutOrderIdAsync(owner, submissionToken, ct);

        var cartItems = request.Items.Select(i => new CartItem(i.ProductId, i.Quantity)).ToArray();
        var shipping = new ShippingInfo(request.ReceiverName, request.ReceiverPhone, request.ShippingAddress, request.Note);

        var result = await orders.CreateIdempotentAsync(owner, cartItems, shipping, submissionToken, ct);
        if (result.OrderId is null)
        {
            var error = result.Error ?? "Chưa thể tạo đơn hàng.";
            var statusCode = ClassifyOrderCreationFailure(error);
            return Problem(statusCode: statusCode, detail: error);
        }

        var receipt = await orders.GetReceiptAsync(result.OrderId.Value, owner, ct);
        var replayed = preExistingOrderId.HasValue && preExistingOrderId.Value == result.OrderId.Value;
        var payload = new OrderCreatedResponse(
            result.OrderId.Value,
            receipt?.OrderCode ?? string.Empty,
            receipt?.Status.ToString() ?? "Pending",
            receipt?.TotalAmount ?? 0m,
            replayed);

        if (replayed)
            return Ok(payload);

        var basePath = Request.Path.Value?.TrimEnd('/') ?? "/api/v1/orders";
        return Created(new Uri($"{Request.Scheme}://{Request.Host}{basePath}/{result.OrderId.Value}"), payload);
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = "Orders.Write")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        if (ApiAccess.UserId(User) is not int owner) return Forbid();
        if (id <= 0) return Problem(statusCode: 400, detail: "Order id must be greater than 0.");

        var receipt = await orders.GetReceiptAsync(id, owner, ct);
        if (receipt is null) return Problem(statusCode: 404, detail: "Order not found.");

        var result = await orderManagement.CancelForCustomerAsync(id, owner, ct);
        if (result.Succeeded) return NoContent();

        return Problem(statusCode: result.Failure switch
        {
            ManagementFailure.NotFound => 404,
            ManagementFailure.Conflict => 409,
            ManagementFailure.Database => 503,
            _ => 400
        }, detail: result.Error);
    }

    private static string? NormalizeIdempotencyToken(string? rawKey)
    {
        if (string.IsNullOrWhiteSpace(rawKey)) return null;
        var trimmed = rawKey.Trim();
        if (trimmed.Length is < 1 or > 128) return null;
        foreach (var ch in trimmed)
        {
            if (char.IsControl(ch) || ch > 126) return null;
        }

        if (Guid.TryParse(trimmed, out var guid))
            return guid.ToString("N");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("phonestore-api-idemp:" + trimmed));
        return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }

    private static int ClassifyOrderCreationFailure(string error)
    {
        if (error.Contains("không đủ hàng", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status409Conflict;
        if (error.Contains("Chưa thể tạo đơn hàng", StringComparison.OrdinalIgnoreCase))
            return StatusCodes.Status503ServiceUnavailable;
        return StatusCodes.Status400BadRequest;
    }
}
