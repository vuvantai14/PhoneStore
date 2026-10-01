using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Web.Models;
using PhoneStore.Business.Catalog;

namespace PhoneStore.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IProductService _products;

    public HomeController(ILogger<HomeController> logger, IProductService products)
    {
        _logger = logger;
        _products = products;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var products = await _products.GetNewestAsync(8, cancellationToken);
        var brands = await _products.GetBrandsAsync(cancellationToken);
        var categories = await _products.GetCategoriesAsync(cancellationToken);
        return View(new HomeViewModel { Products = products, Brands = brands, Categories = categories });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
