using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Orders;
using PhoneStore.Business.Purchasing;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Policy = ApiAccess.Customer)]
[Authorize(Policy = "Orders.Read")]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
public sealed class CustomerOrdersController(IOrderService orders) : ControllerBase
{
    [HttpGet]
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
            o.Status.ToString(), o.TotalAmount, o.ReceiverName)).ToArray(), page.TotalCount, page.PageNumber, page.PageSize);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDetailResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<OrderDetailResponse>> Detail(int id, CancellationToken ct)
    {
        if (ApiAccess.UserId(User) is not int owner) return Forbid();
        var o = await orders.GetReceiptAsync(id, owner, ct);
        if (o is null) return Problem(statusCode: 404, detail: "Order not found.");
        return new OrderDetailResponse(new OrderSummary(o.OrderId, o.OrderCode, DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc), o.Status.ToString(),
            o.TotalAmount, o.ReceiverName), o.ReceiverPhone, o.ShippingAddress, o.Note,
            o.Items.Select(i => new OrderLine(i.ProductName, i.Quantity, i.UnitPrice)).ToArray());
    }
}
