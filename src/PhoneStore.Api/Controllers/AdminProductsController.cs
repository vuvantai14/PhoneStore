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
[Route("api/v1/admin/products")]
[Route("api/admin/products")]
[EnableRateLimiting(ApiPolicies.ProtectedRateLimit)]
[RequestTimeout(ApiPolicies.CatalogTimeout)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(400)]
public sealed class AdminProductsController(IProductManagementService products) : ControllerBase
{
    public const long MaxImageFileBytes = 10 * 1024 * 1024;
    public const long MaxImageMultipartBodyBytes = MaxImageFileBytes + 65536;

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

    [HttpGet("options")]
    [ProducesResponseType<AdminProductOptionsResponse>(200)]
    public async Task<ActionResult<AdminProductOptionsResponse>> Options(
        [FromQuery] AdminProductOptionsQuery query,
        CancellationToken ct)
    {
        var options = query.IncludeInactive
            ? await products.GetFilterOptionsAsync(ct)
            : await products.GetOptionsAsync(query.ProductId, ct);
        return new AdminProductOptionsResponse(
            options.Brands.Select(b => new AdminLookupOption(b.Id, b.Name, b.IsActive)).ToArray(),
            options.Categories.Select(c => new AdminLookupOption(c.Id, c.Name, c.IsActive)).ToArray());
    }

    [ProducesResponseType<AdminProductDetail>(200)]
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<AdminProductDetail>> Detail(int id, CancellationToken ct)
    {
        if (id <= 0) return Problem(statusCode: 400, detail: "Product id must be greater than 0.");
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
        var basePath = Request.Path.Value?.TrimEnd('/') ?? "/api/v1/admin/products";
        return Created(new Uri($"{Request.Scheme}://{Request.Host}{basePath}/{id}"), new AdminProductId(id));
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

    [HttpGet("{id:int}/images")]
    [ProducesResponseType<IReadOnlyList<AdminProductImageItem>>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<IReadOnlyList<AdminProductImageItem>>> GetImages(int id, CancellationToken ct)
    {
        if (id <= 0) return Problem(statusCode: 400, detail: "Product id must be greater than 0.");
        var rows = await products.GetImagesAsync(id, ct);
        if (rows.Count == 0 && await products.GetEditAsync(id, ct) is null)
            return Problem(statusCode: 404, detail: "Product not found.");
        return rows.Select(i => new AdminProductImageItem(i.ImageId, i.ProductId, i.ImageUrl, i.IsPrimary, i.DisplayOrder)).ToArray();
    }

    [HttpPost("{id:int}/images")]
    [RequestSizeLimit(MaxImageMultipartBodyBytes)]
    [ProducesResponseType<AdminProductImageUploadResponse>(201)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(413)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> UploadImage(int id, IFormFile? file, CancellationToken ct)
    {
        if (id <= 0) return Problem(statusCode: 400, detail: "Vui lòng lưu sản phẩm trước khi thêm ảnh.");
        if (file is null || file.Length <= 0)
            return Problem(statusCode: 400, detail: "Vui lòng chọn tệp ảnh.");
        if (file.Length > MaxImageFileBytes)
            return Problem(statusCode: 413, title: "Request body is too large", detail: "Ảnh không được vượt quá 10 MB.");

        var rawName = file.FileName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawName) || rawName.IndexOf('\0') >= 0 ||
            rawName.Contains("..", StringComparison.Ordinal))
        {
            return Problem(statusCode: 400, detail: "Tên tệp ảnh không hợp lệ.");
        }

        var fileName = Path.GetFileName(rawName);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension == ".webp")
            return Problem(statusCode: 400, detail: "Chưa hỗ trợ tải lên WebP. Vui lòng chuyển ảnh sang JPEG hoặc PNG.");
        if (extension is not (".jpg" or ".jpeg" or ".png"))
            return Problem(statusCode: 400, detail: "Chỉ hỗ trợ tải lên ảnh JPG, JPEG hoặc PNG.");

        var tempFile = Path.Combine(Path.GetTempPath(), $"phonestore-api-img-{Guid.NewGuid():N}{extension}");
        try
        {
            await using (var input = file.OpenReadStream())
            await using (var output = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    total += read;
                    if (total > MaxImageFileBytes)
                        return Problem(statusCode: 413, title: "Request body is too large", detail: "Ảnh không được vượt quá 10 MB.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                await output.FlushAsync(ct);
            }

            var result = await products.AddImageAsync(id, tempFile, ct);
            if (!result.Succeeded) return Error(result);

            var imageId = result.Id!.Value;
            var images = await products.GetImagesAsync(id, ct);
            var added = images.FirstOrDefault(i => i.ImageId == imageId);
            var response = new AdminProductImageUploadResponse(
                imageId,
                id,
                added?.ImageUrl ?? string.Empty,
                added?.IsPrimary ?? true,
                added?.DisplayOrder ?? 0,
                result.Notice);
            var basePath = Request.Path.Value?.TrimEnd('/') ?? $"/api/v1/admin/products/{id}/images";
            return Created(new Uri($"{Request.Scheme}://{Request.Host}{basePath}/{imageId}"), response);
        }
        finally
        {
            TryDeleteTempFile(tempFile);
        }
    }

    [HttpDelete("{id:int}/images/{imageId:int}")]
    [ProducesResponseType<AdminProductImageOperationResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> DeleteImage(int id, int imageId, CancellationToken ct)
    {
        var result = await products.RemoveImageAsync(id, imageId, ct);
        return result.Succeeded
            ? Ok(new AdminProductImageOperationResponse(imageId, result.Notice))
            : Error(result);
    }

    [HttpPatch("{id:int}/images/{imageId:int}/primary")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> SetPrimaryImage(int id, int imageId, CancellationToken ct)
    {
        var result = await products.SetPrimaryImageAsync(id, imageId, ct);
        return result.Succeeded ? NoContent() : Error(result);
    }

    [HttpPatch("{id:int}/images/{imageId:int}/order")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404)]
    [ProducesResponseType<ProblemDetails>(503)]
    public async Task<IActionResult> SetImageOrder(int id, int imageId, AdminProductImageOrderRequest request, CancellationToken ct)
    {
        var result = await products.SetImageOrderAsync(id, imageId, request.DisplayOrder, ct);
        return result.Succeeded ? NoContent() : Error(result);
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private ObjectResult Error(ManagementResult result) => Problem(statusCode: result.Failure switch
    {
        ManagementFailure.NotFound => 404,
        ManagementFailure.Conflict => 409,
        ManagementFailure.Database => 503,
        _ => 400
    }, detail: result.Error);
}
