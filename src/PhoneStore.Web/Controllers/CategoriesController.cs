using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Web.Models;

namespace PhoneStore.Web.Controllers;

[Route("categories")]
public class CategoriesController(PhoneStoreDbContext db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CategoryName)
            .Select(c => new CategoryItemViewModel(
                c.CategoryId,
                c.CategoryName,
                c.Description,
                c.Products.Count(p => p.IsActive)
            ))
            .ToListAsync(cancellationToken);

        return View(new CategoriesViewModel { Categories = categories });
    }
}
