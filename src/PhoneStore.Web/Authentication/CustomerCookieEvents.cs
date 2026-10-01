using System.Data.Common;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PhoneStore.Business.Authentication;

namespace PhoneStore.Web.Authentication;

public sealed class CustomerCookieEvents(IAuthService auth) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var valid = false;
        if (int.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            try
            {
                var customer = await auth.GetActiveCustomerAsync(userId, context.HttpContext.RequestAborted);
                valid = customer is not null
                    && context.Principal!.FindFirstValue(ClaimTypes.Role) == customer.Role
                    && context.Principal.FindFirstValue(ClaimTypes.Email) == customer.Email
                    && context.Principal.FindFirstValue(ClaimTypes.Name) == customer.FullName;
            }
            catch (DbException) { /* Reject sessions when account status cannot be verified. */ }
        }
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

