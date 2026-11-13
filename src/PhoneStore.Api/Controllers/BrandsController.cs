using Microsoft.AspNetCore.Mvc;
using PhoneStore.Api.Contracts;
using PhoneStore.Api.Mapping;
using PhoneStore.Business.Catalog;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Route("api/v1/brands")]
public sealed class BrandsController(IProductService products) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CatalogOptionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<CatalogOptionResponse>>> GetAll(CancellationToken cancellationToken) =>
        Ok((await products.GetBrandsAsync(cancellationToken))
            .Select(CatalogContractMapper.ToResponse).ToArray());
}
