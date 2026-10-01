using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Business.Catalog;
using PhoneStore.Models.Catalog;
using PhoneStore.Web.Models;

namespace PhoneStore.Web.Controllers;

[Route("products")]
public class ProductsController(IProductService products) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ProductFilterViewModel filter, CancellationToken cancellationToken)
    {
        filter.Search = filter.Search?.Trim();
        filter.Sort = filter.Sort is "price-asc" or "price-desc" or "name-asc" ? filter.Sort : "newest";
        if (filter.BrandId <= 0) ModelState.AddModelError("brandId", "Thương hiệu không hợp lệ.");
        if (filter.CategoryId <= 0) ModelState.AddModelError("categoryId", "Danh mục không hợp lệ.");
        var min = ParsePrice(filter.MinPrice, "minPrice", "Giá từ");
        var max = ParsePrice(filter.MaxPrice, "maxPrice", "Giá đến");
        if (min.HasValue && max.HasValue && min > max)
            ModelState.AddModelError("maxPrice", "Giá từ phải nhỏ hơn hoặc bằng giá đến.");

        var brands = await products.GetBrandsAsync(cancellationToken);
        var categories = await products.GetCategoriesAsync(cancellationToken);
        var results = ModelState.IsValid
            ? await products.GetProductsAsync(new CatalogQuery(filter.Search, filter.BrandId,
                filter.CategoryId, min, max, filter.Sort), cancellationToken)
            : Array.Empty<ProductCard>();
        return View(new ProductListViewModel { Filter = filter, Products = results, Brands = brands, Categories = categories });
    }

    private decimal? ParsePrice(string? input, string key, string label)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        if (!decimal.TryParse(input.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var price) || price < 0 || price > 9999999999999999.99m)
        {
            ModelState.AddModelError(key, $"{label} phải là số không âm hợp lệ.");
            return null;
        }
        return price;
    }
}

