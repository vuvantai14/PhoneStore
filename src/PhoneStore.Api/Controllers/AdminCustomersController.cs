using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Customers;
using PhoneStore.Business.Management;
using PhoneStore.Models.Management;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Authorize(Policy = ApiAccess.Admin)]
[Route("api/admin/customers")]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
public sealed class AdminCustomersController(CustomerManagementService customers) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<CustomerPage>(200)]
    [ProducesResponseType<ProblemDetails>(400)]
    public async Task<ActionResult<CustomerPage>> List([FromQuery] AdminCustomerQuery query, CancellationToken ct)
    {
        var offset = (long)(query.Page - 1) * query.PageSize;
        if (offset > int.MaxValue) return Problem(statusCode: 400, detail: "Page offset exceeds the supported range.");
        var page = await customers.ListPageAsync(new CustomerManagementQuery(query.Search, query.IsActive),
            (int)offset, query.PageSize, ct);
        var items = page.Items.Select(c => new CustomerSummary(c.UserId, c.FullName, c.Email, c.Phone, c.IsActive));
        return new CustomerPage(items.ToArray(), page.Total, query.Page, query.PageSize);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<CustomerDetail>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<CustomerDetail>> Detail(int id, CancellationToken ct)
    {
        var c = await customers.GetAsync(id, ct);
        if (c is null) return Problem(statusCode: 404, detail: "Customer not found.");
        return new CustomerDetail(c.UserId, c.FullName, c.Email, c.Phone, c.Address, c.IsActive,
            DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc));
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(400)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> SetStatus(int id, CustomerStatusRequest request, CancellationToken ct)
    {
        var result = await customers.SetActiveAsync(id, request.IsActive, ct);
        if (result.Succeeded) return NoContent();
        return Problem(statusCode: result.Failure switch
        {
            ManagementFailure.NotFound => 404,
            ManagementFailure.Database => 503,
            _ => 400
        }, detail: result.Error);
    }
}
