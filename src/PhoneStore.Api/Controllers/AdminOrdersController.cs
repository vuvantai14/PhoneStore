using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Orders;
using PhoneStore.Business.Management;
using PhoneStore.Models.Management;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = ApiAccess.Admin)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
public sealed class AdminOrdersController(OrderManagementService orders) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<OrderPage>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<ActionResult<OrderPage>> List([FromQuery] AdminOrderQuery query, CancellationToken ct)
    {
        var offset = (long)(query.Page - 1) * query.PageSize;
        if (offset > int.MaxValue) return Problem(statusCode: 400, detail: "Page offset exceeds the supported range.");
        var page = await orders.ListPageAsync(new OrderManagementQuery(query.Search, query.Status),
            (int)offset, query.PageSize, ct);
        return new OrderPage(page.Items.Select(o => new OrderSummary(o.OrderId, o.OrderCode, DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc),
            o.Status.ToString(), o.TotalAmount, o.ReceiverName)).ToArray(), page.Total, query.Page, query.PageSize);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDetailResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<OrderDetailResponse>> Detail(int id, CancellationToken ct)
    {
        var o = await orders.GetAsync(id, ct);
        if (o is null) return Problem(statusCode: 404, detail: "Order not found.");
        return new OrderDetailResponse(new OrderSummary(o.OrderId, o.OrderCode, DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc), o.Status.ToString(),
            o.TotalAmount, o.ReceiverName), o.ReceiverPhone, o.ShippingAddress, o.Note,
            o.Items.Select(i => new OrderLine(i.ProductName, i.Quantity, i.UnitPrice)).ToArray());
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatusChange request, CancellationToken ct)
    {
        var result = await orders.UpdateStatusAsync(id, request.ExpectedStatus, request.TargetStatus, ct);
        if (result.Succeeded) return NoContent();
        return Problem(statusCode: result.Failure switch
        {
            ManagementFailure.NotFound => 404,
            ManagementFailure.Conflict => 409,
            ManagementFailure.Database => 503,
            _ => 400
        }, detail: result.Error);
    }
}
