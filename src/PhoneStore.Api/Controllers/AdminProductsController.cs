using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Products;
using PhoneStore.Business.Management;
using PhoneStore.Models.Management;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Authorize(Policy = ApiAccess.Admin)]
[Route("api/admin/products")]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(400)]
public sealed class AdminProductsController(ProductEditorService products) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<AdminProductPage>(200)]
    public async Task<ActionResult<AdminProductPage>> List([FromQuery] AdminProductQuery query, CancellationToken ct)
    {
        var offset = (long)(query.Page - 1) * query.PageSize;
        if (offset > int.MaxValue) return Problem(statusCode: 400, detail: "Page offset exceeds the supported range.");
        var filter = new ProductManagementQuery(query.Search, query.BrandId, query.CategoryId, query.IsActive);
        var page = await products.ListPageAsync(filter, (int)offset, query.PageSize, ct);
        var items = page.Items.Select(p => new AdminProductSummary(p.ProductId, p.ProductName, p.BrandName,
            p.CategoryName, p.Price, p.StockQuantity, p.RAM, p.Storage, p.IsActive, p.UpdatedAt)).ToArray();
        return new AdminProductPage(items, page.TotalCount, query.Page, query.PageSize);
    }

    [ProducesResponseType<AdminProductDetail>(200)]
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<AdminProductDetail>> Detail(int id, CancellationToken ct)
    {
        var p = await products.GetEditAsync(id, ct);
        if (p is null) return Problem(statusCode: 404, detail: "Product not found.");
        return new AdminProductDetail(id, p.ProductName, p.BrandId, p.CategoryId, p.Price, p.StockQuantity,
            p.StockQuantity, p.IsActive, p.RAM, p.Storage, p.Chip, p.Screen, p.Camera, p.Battery, p.OperatingSystem, p.Description);
    }

    [HttpPost]
    [ProducesResponseType<AdminProductId>(201)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Create(ProductWriteRequest request, CancellationToken ct)
    {
        var result = await products.SaveAsync(null, request.ToEdit(), ct);
        if (!result.Succeeded) return Error(result);
        var id = result.Id!.Value;
        return CreatedAtAction(nameof(Detail), new { id }, new AdminProductId(id));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(409)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> Update(int id, ProductUpdateRequest request, CancellationToken ct)
    {
        var result = await products.SaveAsync(id, request.ToEdit(request.OriginalStockQuantity), ct);
        return result.Succeeded ? NoContent() : Error(result);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> SetStatus(int id, ProductStatusRequest request, CancellationToken ct)
    {
        var result = await products.SetActiveAsync(id, request.IsActive, ct);
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
