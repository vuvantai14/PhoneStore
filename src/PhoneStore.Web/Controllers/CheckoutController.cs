using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Business.Purchasing;
using PhoneStore.Models.Purchasing;
using PhoneStore.Web.Models;
using PhoneStore.Web.Purchasing;

namespace PhoneStore.Web.Controllers;

[Authorize(Policy = "Customer"), Route("checkout")]
public class CheckoutController(SessionCart cart, CartPresenter presenter, IOrderService orders, CheckoutSubmissionGuard guard) : Controller
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            var summary = await presenter.GetAsync(ct);
            if (!summary.CanCheckout) return BackToCart();
            var customer = await orders.GetCustomerAsync(UserId, ct);
            if (customer is null) return Forbid();
            return View(new CheckoutViewModel { ReceiverName = customer.FullName, ReceiverPhone = customer.Phone ?? "",
                ShippingAddress = customer.Address ?? "", Cart = summary, SubmissionToken = cart.CheckoutToken() });
        }
        catch (DbException) { return BackToCart("Chưa thể tải thanh toán. Vui lòng thử lại sau."); }
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutViewModel model, CancellationToken ct)
    {
        if (!cart.ValidToken(model.SubmissionToken)) return BackToCart("Phiên thanh toán không hợp lệ. Vui lòng mở lại giỏ hàng.");
        var submission = guard.Get(UserId, model.SubmissionToken!);
        await submission.Gate.WaitAsync(ct);
        try
        {
            if (submission.OrderId.HasValue) return RedirectToAction(nameof(Success), new { id = submission.OrderId });
            model.Cart = await presenter.GetAsync(ct);
            if (!model.Cart.CanCheckout) return BackToCart();
            if (!ModelState.IsValid) return View(model);
            var result = await orders.CreateAsync(UserId, cart.Read(),
                new ShippingInfo(model.ReceiverName, model.ReceiverPhone, model.ShippingAddress, model.Note), ct);
            if (result.OrderId is null)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Chưa thể đặt hàng.");
                return View(model);
            }
            submission.OrderId = result.OrderId;
            cart.Clear();
            cart.ClearCheckoutToken();
            await HttpContext.Session.CommitAsync(ct);
            return RedirectToAction(nameof(Success), new { id = result.OrderId });
        }
        catch (DbException) { ModelState.AddModelError(string.Empty, "Chưa thể đặt hàng. Vui lòng thử lại sau."); return View(model); }
        finally { submission.Gate.Release(); }
    }

    [HttpGet("success/{id:int}")]
    public async Task<IActionResult> Success(int id, CancellationToken ct)
    {
        try
        {
            var receipt = await orders.GetReceiptAsync(id, UserId, ct);
            if (receipt is null) return NotFound();
            return View(receipt);
        }
        catch (DbException) { return StatusCode(503, "Chưa thể tải thông tin đơn hàng. Vui lòng thử lại sau."); }
    }

    private IActionResult BackToCart(string message = "Giỏ hàng trống hoặc có sản phẩm không đủ hàng. Vui lòng kiểm tra lại.")
    {
        TempData["CartMessage"] = message;
        return RedirectToAction("Index", "Cart");
    }
}

