using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Products;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Business.Management;
using PhoneStore.Models.Management;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Authorize(Policy = ApiAccess.Admin)]
[Route("api/v1/admin/brands")]
[Route("api/admin/brands")]
[EnableRateLimiting(ApiPolicies.ProtectedRateLimit)]
[RequestTimeout(ApiPolicies.CatalogTimeout)]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
public sealed class AdminBrandsController(ILookupManagementService lookups) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AdminLookupItem>>(200)]
    public async Task<ActionResult<IReadOnlyList<AdminLookupItem>>> List(
        [FromQuery] AdminLookupQuery query,
        CancellationToken ct)
    {
        var rows = await lookups.ListAsync(LookupKind.Brand, query.Search, ct);
        return rows.Select(r => new AdminLookupItem(
            r.Id, r.Name, r.Description, r.IsActive, DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc))).ToArray();
    }

    [HttpPost]
    [ProducesResponseType<AdminLookupId>(201)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Create(AdminLookupWriteRequest request, CancellationToken ct)
    {
        var result = await lookups.SaveAsync(LookupKind.Brand, null, request.ToEdit(), ct);
        if (!result.Succeeded) return Error(result);
        var id = result.Id!.Value;
        var basePath = Request.Path.Value?.TrimEnd('/') ?? "/api/v1/admin/brands";
        return Created(new Uri($"{Request.Scheme}://{Request.Host}{basePath}/{id}"), new AdminLookupId(id));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Update(int id, AdminLookupWriteRequest request, CancellationToken ct)
    {
        var result = await lookups.SaveAsync(LookupKind.Brand, id, request.ToEdit(), ct);
        return result.Succeeded ? NoContent() : Error(result);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> SetStatus(int id, AdminLookupStatusRequest request, CancellationToken ct)
    {
        var result = await lookups.SetActiveAsync(LookupKind.Brand, id, request.IsActive, ct);
        return result.Succeeded ? NoContent() : Error(result);
    }

    private ObjectResult Error(ManagementResult result) => Problem(statusCode: result.Failure switch
    {
        ManagementFailure.NotFound => 404,
        ManagementFailure.Conflict => 409,
        ManagementFailure.Database => 503,
        _ => 400
    }, detail: result.Error);
}
