using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Web.Models;

namespace PhoneStore.Web.Controllers;

[Route("brands")]
public class BrandsController(PhoneStoreDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var brands = await db.Brands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.BrandName)
            .Select(b => new BrandItemViewModel(
                b.BrandId,
                b.BrandName,
                b.Description,
                b.Products.Count(p => p.IsActive),
                BrandLogoMap.GetLogoPath(b.BrandName)
            ))
            .ToListAsync(cancellationToken);

        return View(new BrandsViewModel { Brands = brands });
    }
}
