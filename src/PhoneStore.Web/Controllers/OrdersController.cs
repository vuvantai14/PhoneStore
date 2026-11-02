using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Business.Purchasing;
using PhoneStore.Business.Management;

namespace PhoneStore.Web.Controllers;

[Authorize(Policy = "Customer"), Route("orders")]
public sealed class OrdersController(IOrderService orders, IOrderManagementService management) : Controller
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try { return View(await orders.GetCustomerOrdersAsync(UserId, ct)); }
        catch (DbException) { return StatusCode(503, "Chưa thể tải danh sách đơn hàng. Vui lòng thử lại sau."); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        try
        {
            var order = await orders.GetReceiptAsync(id, UserId, ct);
            return order is null ? NotFound() : View(order);
        }
        catch (DbException) { return StatusCode(503, "Chưa thể tải thông tin đơn hàng. Vui lòng thử lại sau."); }
    }

    [HttpPost("{id:int}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        // Do not expose whether an order belongs to another account.
        try
        {
            if (await orders.GetReceiptAsync(id, UserId, ct) is null) return NotFound();
            var result = await management.CancelForCustomerAsync(id, UserId, ct);
            TempData["OrderMessage"] = result.Succeeded ? "Đơn hàng đã được hủy." : result.Error;
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (DbException) { return StatusCode(503, "Chưa thể hủy đơn hàng. Vui lòng thử lại sau."); }
    }
}
