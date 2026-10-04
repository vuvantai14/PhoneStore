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

    private async Task<IActionResult> Change(int id, int quantity, bool add, CancellationToken ct)
    {
        if (!ModelState.IsValid || id <= 0 || quantity <= 0)
        { TempData["CartMessage"] = "Số lượng phải là số nguyên lớn hơn 0."; return RedirectToAction(nameof(Index)); }
        try
        {
            var product = (await orders.GetCartProductsAsync([id], ct)).SingleOrDefault();
            var items = cart.Read().ToList();
            var existing = items.SingleOrDefault(i => i.ProductId == id);
            var requested = add ? (long)(existing?.Quantity ?? 0) + quantity : quantity;
            if (product is null || !product.IsActive)
                TempData["CartMessage"] = "Sản phẩm không còn khả dụng.";
            else if (requested > product.StockQuantity)
                TempData["CartMessage"] = "Số lượng vượt quá tồn kho hiện tại.";
            else if (!add && existing is null)
                TempData["CartMessage"] = "Sản phẩm không có trong giỏ hàng.";
            else
            {
                items.RemoveAll(i => i.ProductId == id);
                items.Add(new CartItem(id, (int)requested));
                cart.Write(items);
                TempData["CartMessage"] = "Đã cập nhật giỏ hàng.";
            }
        }
        catch (DbException) { TempData["CartMessage"] = "Chưa thể cập nhật giỏ hàng. Vui lòng thử lại sau."; }
        return RedirectToAction(nameof(Index));
    }
}

