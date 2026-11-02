using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Business.Authentication;
using PhoneStore.Models.Authentication;
using PhoneStore.Web.Models;

namespace PhoneStore.Web.Controllers;

[Authorize(Policy = "Customer"), Route("account/profile")]
public sealed class ProfileController(ICustomerProfileService profiles) : Controller
{
    private int CustomerId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            var profile = await profiles.GetAsync(CustomerId, ct);
            return profile is null ? NotFound() : View(new CustomerProfileViewModel
            { FullName = profile.FullName, Email = profile.Email, Phone = profile.Phone, Address = profile.Address });
        }
        catch (DbException) { return StatusCode(503, "Chưa thể tải hồ sơ. Vui lòng thử lại sau."); }
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CustomerProfileViewModel model, CancellationToken ct)
    {
        CustomerProfile? current;
        try { current = await profiles.GetAsync(CustomerId, ct); }
        catch (DbException) { return StatusCode(503, "Chưa thể tải hồ sơ. Vui lòng thử lại sau."); }
        if (current is null) return NotFound();
        // Identity always comes from the authenticated principal, never route/query/form IDs.
        model.Email = current.Email;
        if (!ModelState.IsValid) return View(model);
        var ticket = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var result = await profiles.UpdateAsync(CustomerId, new(model.FullName, model.Phone, model.Address), ct);
        if (result == ProfileUpdateResult.NotFound) return NotFound();
        if (result != ProfileUpdateResult.Success)
        {
            ModelState.AddModelError(string.Empty, result == ProfileUpdateResult.InvalidInput
                ? "Thông tin chưa hợp lệ. Họ tên cần 2–100 ký tự, điện thoại tối đa 20 ký tự hợp lệ, địa chỉ tối đa 255 ký tự."
                : "Chưa thể lưu hồ sơ. Vui lòng thử lại sau.");
            if (result == ProfileUpdateResult.Unavailable) Response.StatusCode = 503;
            return View(model);
        }
        // Cookie validation compares FullName with the DB; renew claims without extending ticket expiry.
        var identity = new ClaimsIdentity(User.Identity as ClaimsIdentity);
        foreach (var claim in identity.FindAll(ClaimTypes.Name).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(ClaimTypes.Name, model.FullName.Trim()));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), ticket.Properties);
        TempData["ProfileSuccess"] = "Đã cập nhật hồ sơ của bạn.";
        return RedirectToAction(nameof(Index));
    }
}
