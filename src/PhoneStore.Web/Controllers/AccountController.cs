using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Authentication;
using PhoneStore.Models.Authentication;
using PhoneStore.Web.Models;

namespace PhoneStore.Web.Controllers;

[Route("account")]
public class AccountController(IAuthService auth) : Controller
{
    [AllowAnonymous, HttpGet("register")]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new RegisterViewModel());

    [AllowAnonymous, HttpPost("register"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await auth.RegisterAsync(model.FullName, model.Email, model.Phone, model.Password, cancellationToken);
        if (result == RegistrationResult.Success)
        {
            TempData["AuthSuccess"] = "Đăng ký thành công. Bạn có thể đăng nhập ngay.";
            return RedirectToAction(nameof(Login));
        }
        if (result == RegistrationResult.DuplicateEmail)
            ModelState.AddModelError(nameof(model.Email), "Email này đã được sử dụng.");
        else
            ModelState.AddModelError(string.Empty, result == RegistrationResult.InvalidInput
                ? "Vui lòng kiểm tra thông tin đăng ký."
                : "Chưa thể đăng ký lúc này. Vui lòng thử lại sau.");
        return View(model);
    }

    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home")
            : View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });

    [AllowAnonymous, HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        CustomerIdentity? customer;
        try { customer = await auth.LoginAsync(model.Email, model.Password, cancellationToken); }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Chưa thể đăng nhập lúc này. Vui lòng thử lại sau.");
            return View(model);
        }
        if (customer is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, customer.UserId.ToString()),
            new Claim(ClaimTypes.Name, customer.FullName),
            new Claim(ClaimTypes.Email, customer.Email),
            new Claim(ClaimTypes.Role, customer.Role)
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.Add(model.RememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8))
            });
        return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Home");
    }

    [Authorize, HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous, HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}

