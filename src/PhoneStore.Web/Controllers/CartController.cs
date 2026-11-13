using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Business.Purchasing;
using PhoneStore.Models.Purchasing;
using PhoneStore.Web.Models;
using PhoneStore.Web.Purchasing;

namespace PhoneStore.Web.Controllers;

[Route("cart")]
public class CartController(SessionCart cart, CartPresenter presenter, IOrderService orders) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try { return View(await presenter.GetAsync(ct)); }
        catch (DbException) { TempData["CartMessage"] = "Chưa thể tải giỏ hàng. Vui lòng thử lại sau."; return View(new CartViewModel()); }
    }

    [HttpGet("count")]
    public IActionResult Count() => Json(new { count = cart.Count });

    [HttpPost("add"), ValidateAntiForgeryToken]
    public Task<IActionResult> Add(int productId, int quantity, CancellationToken ct) => Change(productId, quantity, true, ct);
    [HttpPost("update"), ValidateAntiForgeryToken]
    public Task<IActionResult> Update(int productId, int quantity, CancellationToken ct) => Change(productId, quantity, false, ct);
    [HttpPost("remove"), ValidateAntiForgeryToken]
    public IActionResult Remove(int productId)
    {
        cart.Write(cart.Read().Where(i => i.ProductId != productId));
        return RedirectToAction(nameof(Index));
    }

    private bool IsAjaxRequest()
    {
        if (Request?.Headers is null) return false;
        if (string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return true;
        return Request.Headers.Accept.Any(h => h?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
    }

    private async Task<IActionResult> Change(int id, int quantity, bool add, CancellationToken ct)
    {
        var isAjax = IsAjaxRequest();
        if (!ModelState.IsValid || id <= 0 || quantity <= 0)
        {
            const string msg = "Số lượng phải là số nguyên lớn hơn 0.";
            if (isAjax) return Json(new { success = false, message = msg, cartCount = cart.Count });
            TempData["CartMessage"] = msg;
            return RedirectToAction(nameof(Index));
        }
        try
        {
            var product = (await orders.GetCartProductsAsync([id], ct)).SingleOrDefault();
            var items = cart.Read().ToList();
            var existing = items.SingleOrDefault(i => i.ProductId == id);
            var requested = add ? (long)(existing?.Quantity ?? 0) + quantity : quantity;
            if (product is null || !product.IsActive)
            {
                const string msg = "Sản phẩm không còn khả dụng.";
                if (isAjax) return Json(new { success = false, message = msg, cartCount = cart.Count });
                TempData["CartMessage"] = msg;
            }
            else if (product.StockQuantity <= 0)
            {
                const string msg = "Sản phẩm hiện đã hết hàng.";
                if (isAjax) return Json(new { success = false, message = msg, cartCount = cart.Count });
                TempData["CartMessage"] = "Số lượng vượt quá tồn kho hiện tại.";
            }
            else if (requested > product.StockQuantity)
            {
                var msg = existing is not null && existing.Quantity > 0
                    ? $"Số lượng yêu cầu ({requested}) vượt quá tồn kho ({product.StockQuantity}). Giỏ hàng đã có {existing.Quantity} sản phẩm."
                    : $"Số lượng vượt quá tồn kho hiện tại (chỉ còn {product.StockQuantity} sản phẩm).";
                if (isAjax) return Json(new { success = false, message = msg, cartCount = cart.Count });
                TempData["CartMessage"] = "Số lượng vượt quá tồn kho hiện tại.";
            }
            else if (!add && existing is null)
            {
                const string msg = "Sản phẩm không có trong giỏ hàng.";
                if (isAjax) return Json(new { success = false, message = msg, cartCount = cart.Count });
                TempData["CartMessage"] = msg;
            }
            else
            {
                var replacement = new CartItem(id, (int)requested);
                var existingIndex = items.FindIndex(i => i.ProductId == id);
                if (existingIndex >= 0) items[existingIndex] = replacement;
                else items.Add(replacement);
                cart.Write(items);
                var msg = add ? "Đã thêm vào giỏ hàng." : "Đã cập nhật giỏ hàng.";
                if (isAjax)
                {
                    var snapshot = await presenter.GetAsync(ct);
                    var line = snapshot.Items.Single(i => i.Item.ProductId == id);
                    return Json(new
                    {
                        success = true,
                        message = msg,
                        productId = id,
                        quantity = line.Item.Quantity,
                        stockQuantity = line.Product?.StockQuantity ?? 0,
                        unitPrice = line.Product?.Price ?? 0m,
                        lineTotal = line.LineTotal,
                        totalQuantity = snapshot.TotalQuantity,
                        totalAmount = snapshot.TotalAmount,
                        canCheckout = snapshot.CanCheckout,
                        cartCount = snapshot.TotalQuantity
                    });
                }
                TempData["CartMessage"] = "Đã cập nhật giỏ hàng.";
            }
        }
        catch (DbException)
        {
            const string msg = "Chưa thể cập nhật giỏ hàng. Vui lòng thử lại sau.";
            if (isAjax) return Json(new { success = false, message = msg, cartCount = cart.Count });
            TempData["CartMessage"] = msg;
        }
        return RedirectToAction(nameof(Index));
    }
}

