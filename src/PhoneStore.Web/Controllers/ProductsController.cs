using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Business.Catalog;
using PhoneStore.Models.Catalog;
using PhoneStore.Web.Models;

namespace PhoneStore.Web.Controllers;

[Route("products")]
public class ProductsController(IProductService products) : Controller
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id, CancellationToken cancellationToken)
    {
        if (!int.TryParse(id, out var productId) || productId <= 0)
            return new ViewResult { ViewName = "NotFound", StatusCode = StatusCodes.Status404NotFound };

        var product = await products.GetProductDetailAsync(productId, cancellationToken);
        if (product is null)
            return new ViewResult { ViewName = "NotFound", StatusCode = StatusCodes.Status404NotFound };

        var related = await products.GetRelatedAsync(product, cancellationToken);
        return View(new ProductDetailViewModel { Product = product, RelatedProducts = related });
    }
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] ProductFilterViewModel filter, CancellationToken cancellationToken)
    {
        var brands = await products.GetBrandsAsync(cancellationToken);
        var categories = await products.GetCategoriesAsync(cancellationToken);

        // 1. Xử lý khi người dùng chọn "Xóa bộ lọc" / "Đặt lại"
        if (filter.Reset)
        {
            return RedirectToAction(nameof(Index));
        }

        // Bỏ qua lỗi định dạng của param page (nếu có, ví dụ page=abc) và đưa về trang 1
        if (ModelState.ContainsKey(nameof(filter.Page)))
        {
            ModelState.Remove(nameof(filter.Page));
            filter.Page = 1;
        }
        if (filter.Page < 1)
        {
            filter.Page = 1;
        }

        filter.Search = filter.Search?.Trim();

        // 2. Xử lý thương hiệu:
        // CHỈ lọc khi người dùng chủ động chọn. Mặc định không lọc thương hiệu.
        if (filter.BrandId.HasValue && !filter.BrandIds.Contains(filter.BrandId.Value))
        {
            filter.BrandIds.Add(filter.BrandId.Value);
        }
        filter.BrandId = null;

        // 3. Xử lý Tồn kho:
        // CHỈ lọc còn hàng khi người dùng chủ động chọn (inStockOnly=true).
        // Mặc định không lọc tồn kho (hiển thị toàn bộ sản phẩm).
        if (Request.Query.ContainsKey("inStockOnly"))
        {
            var val = Request.Query["inStockOnly"].ToString();
            filter.InStockOnly = val.Equals("true", StringComparison.OrdinalIgnoreCase)
                || val == "1" || val.Equals("on", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            filter.InStockOnly = false;
        }

        // 4. Chuẩn hóa RAM & Storage
        filter.Rams = filter.Rams.Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        filter.Storages = filter.Storages.Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        filter.Sort = filter.Sort is "price-asc" or "price-desc" or "name-asc" ? filter.Sort : "newest";

        // 5. Xử lý khoảng giá nhanh vs tùy chỉnh
        decimal? min = null;
        decimal? max = null;
        if (!string.IsNullOrWhiteSpace(filter.MinPrice) || !string.IsNullOrWhiteSpace(filter.MaxPrice))
        {
            min = ParsePrice(filter.MinPrice, "minPrice", "Giá từ");
            max = ParsePrice(filter.MaxPrice, "maxPrice", "Giá đến");
            if (min.HasValue && max.HasValue && min > max)
                ModelState.AddModelError("maxPrice", "Giá từ phải nhỏ hơn hoặc bằng giá đến.");
        }
        else if (!string.IsNullOrWhiteSpace(filter.PriceRange) && filter.PriceRange != "all")
        {
            switch (filter.PriceRange)
            {
                case "under-5":
                    max = 5000000m;
                    break;
                case "5-10":
                    min = 5000000m;
                    max = 10000000m;
                    break;
                case "10-20":
                    min = 10000000m;
                    max = 20000000m;
                    break;
                case "above-20":
                    min = 20000000m;
                    break;
            }
        }

        const int pageSize = 10;
        var paged = ModelState.IsValid
            ? await products.GetPagedProductsAsync(new CatalogQuery(
                Search: filter.Search,
                BrandId: null,
                CategoryId: filter.CategoryId,
                MinPrice: min,
                MaxPrice: max,
                Sort: filter.Sort,
                Page: filter.Page,
                PageSize: pageSize,
                BrandIds: filter.BrandIds,
                Rams: filter.Rams,
                Storages: filter.Storages,
                InStockOnly: filter.InStockOnly == true), cancellationToken)
            : new PagedCatalogResult([], 0, filter.Page, pageSize);

        // Đồng bộ số trang nếu page vượt quá totalPages hoặc < 1
        filter.Page = paged.PageNumber;

        var appliedFilters = BuildAppliedFilters(filter, brands, categories, min, max);

        return View(new ProductListViewModel
        {
            Filter = filter,
            Products = paged.Items,
            Brands = brands,
            Categories = categories,
            AppliedFilters = appliedFilters,
            TotalItems = paged.TotalItems,
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize
        });
    }

    private static List<AppliedFilterItem> BuildAppliedFilters(
        ProductFilterViewModel filter, IReadOnlyList<CatalogOption> brands, IReadOnlyList<CatalogOption> categories, decimal? min, decimal? max)
    {
        var list = new List<AppliedFilterItem>();
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            list.Add(new AppliedFilterItem("search", filter.Search, $"Tìm: \"{filter.Search}\"",
                BuildFilterUrl(filter, removeKey: "search")));
        }

        if (filter.CategoryId.HasValue)
        {
            var categoryName = categories.FirstOrDefault(c => c.Id == filter.CategoryId.Value)?.Name ?? $"Danh mục #{filter.CategoryId.Value}";
            list.Add(new AppliedFilterItem("category", filter.CategoryId.Value.ToString(), categoryName,
                BuildFilterUrl(filter, removeKey: "categoryId")));
        }

        foreach (var bId in filter.BrandIds)
        {
            var brandName = brands.FirstOrDefault(b => b.Id == bId)?.Name ?? $"Brand #{bId}";
            list.Add(new AppliedFilterItem("brand", bId.ToString(), brandName,
                BuildFilterUrl(filter, removeKey: "brandId", removeValue: bId.ToString())));
        }

        if (min.HasValue || max.HasValue)
        {
            string priceLabel = (min, max) switch
            {
                (not null, not null) => $"{min.Value:N0}₫ – {max.Value:N0}₫",
                (not null, null) => $"Từ {min.Value:N0}₫",
                (null, not null) => $"Dưới {max.Value:N0}₫",
                _ => "Khoảng giá"
            };
            list.Add(new AppliedFilterItem("price", "price", priceLabel,
                BuildFilterUrl(filter, removeKey: "price")));
        }

        foreach (var r in filter.Rams)
        {
            list.Add(new AppliedFilterItem("ram", r, $"RAM: {r}",
                BuildFilterUrl(filter, removeKey: "ramItem", removeValue: r)));
        }

        foreach (var s in filter.Storages)
        {
            list.Add(new AppliedFilterItem("storage", s, $"Bộ nhớ: {s}",
                BuildFilterUrl(filter, removeKey: "storageItem", removeValue: s)));
        }

        if (filter.InStockOnly == true)
        {
            list.Add(new AppliedFilterItem("inStock", "inStock", "Còn hàng",
                BuildFilterUrl(filter, removeKey: "inStock")));
        }

        return list;
    }

    public static string BuildFilterUrl(ProductFilterViewModel filter,
        string? removeKey = null, string? removeValue = null, int? targetPage = null)
    {
        var query = new List<string>();
        if (removeKey != "search" && !string.IsNullOrWhiteSpace(filter.Search))
            query.Add($"search={Uri.EscapeDataString(filter.Search)}");

        if (removeKey != "category" && removeKey != "categoryId" && filter.CategoryId.HasValue)
            query.Add($"categoryId={filter.CategoryId.Value}");

        if (removeKey != "brand")
        {
            foreach (var b in filter.BrandIds)
            {
                if (removeKey == "brandId" && removeValue == b.ToString()) continue;
                query.Add($"brandIds={b}");
            }
        }

        if (removeKey != "price")
        {
            if (!string.IsNullOrWhiteSpace(filter.PriceRange) && filter.PriceRange != "all")
                query.Add($"priceRange={Uri.EscapeDataString(filter.PriceRange)}");
            if (!string.IsNullOrWhiteSpace(filter.MinPrice))
                query.Add($"minPrice={Uri.EscapeDataString(filter.MinPrice)}");
            if (!string.IsNullOrWhiteSpace(filter.MaxPrice))
                query.Add($"maxPrice={Uri.EscapeDataString(filter.MaxPrice)}");
        }

        if (removeKey != "ram")
        {
            foreach (var r in filter.Rams)
            {
                if (removeKey == "ramItem" && string.Equals(removeValue, r, StringComparison.OrdinalIgnoreCase)) continue;
                query.Add($"rams={Uri.EscapeDataString(r)}");
            }
        }

        if (removeKey != "storage")
        {
            foreach (var s in filter.Storages)
            {
                if (removeKey == "storageItem" && string.Equals(removeValue, s, StringComparison.OrdinalIgnoreCase)) continue;
                query.Add($"storages={Uri.EscapeDataString(s)}");
            }
        }

        if (removeKey != "inStock" && filter.InStockOnly == true)
        {
            query.Add("inStockOnly=true");
        }

        if (!string.IsNullOrWhiteSpace(filter.Sort) && filter.Sort != "newest")
            query.Add($"sort={Uri.EscapeDataString(filter.Sort)}");

        if (targetPage.HasValue && targetPage.Value > 1)
            query.Add($"page={targetPage.Value}");

        return query.Count > 0 ? $"/products?{string.Join("&", query)}" : "/products";
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

