using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Api.Contracts.Products;
using PhoneStore.Api.Mapping;
using PhoneStore.Business.Catalog;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
[EnableRateLimiting(ApiPolicies.CatalogRateLimit)]
[RequestTimeout(ApiPolicies.CatalogTimeout)]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType<PagedProductsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PagedProductsResponse>> GetAll(
        [FromQuery] ProductQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await products.GetPagedProductsAsync(request.ToCatalogQuery(), cancellationToken);
        return Ok(new PagedProductsResponse(
            result.Items.Select(CatalogContractMapper.ToResponse).ToArray(),
            result.PageNumber,
            result.PageSize,
            result.TotalItems,
            result.TotalPages));
    }

    [AllowAnonymous]
    [HttpGet("bestsellers")]
    [ProducesResponseType<IReadOnlyList<ProductSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<ProductSummaryResponse>>> GetBestSellers(
        [FromQuery, Range(1, 24)] int count = 8,
        CancellationToken cancellationToken = default)
    {
        var items = await products.GetBestSellersAsync(count, cancellationToken);
        return Ok(items.Select(CatalogContractMapper.ToResponse).ToArray());
    }

    [AllowAnonymous]
    [HttpGet("newest")]
    [ProducesResponseType<IReadOnlyList<ProductSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<ProductSummaryResponse>>> GetNewest(
        [FromQuery, Range(1, 24)] int count = 8,
        [FromQuery] bool diversified = false,
        CancellationToken cancellationToken = default)
    {
        var items = diversified
            ? await products.GetDiversifiedNewestAsync(count, cancellationToken)
            : await products.GetNewestAsync(count, cancellationToken);
        return Ok(items.Select(CatalogContractMapper.ToResponse).ToArray());
    }

    [AllowAnonymous]
    [HttpGet("{id}")]
    [ProducesResponseType<ProductDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ProductDetailResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            ModelState.AddModelError(nameof(id), "id must be greater than 0.");
            return ValidationProblem(ModelState);
        }

        var product = await products.GetProductDetailAsync(id, cancellationToken);
        return product is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found",
                detail: "No active product was found for the specified id.")
            : Ok(CatalogContractMapper.ToResponse(product));
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/related")]
    [ProducesResponseType<IReadOnlyList<ProductSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<ProductSummaryResponse>>> GetRelated(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            ModelState.AddModelError(nameof(id), "id must be greater than 0.");
            return ValidationProblem(ModelState);
        }

        var product = await products.GetProductDetailAsync(id, cancellationToken);
        if (product is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found",
                detail: "No active product was found for the specified id.");
        }

        var related = await products.GetRelatedAsync(product, cancellationToken);
        return Ok(related.Select(CatalogContractMapper.ToResponse).ToArray());
    }
}
