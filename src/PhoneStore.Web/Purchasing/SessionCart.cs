using System.Text.Json;
using System.Security.Claims;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Web.Purchasing;

public sealed class SessionCart(IHttpContextAccessor accessor)
{
    private const string Key = "PhoneStore.Cart";
    private ISession Session => accessor.HttpContext!.Session;
    private const string OwnerKey = "PhoneStore.CartOwner";

    // An anonymous cart may be adopted at login, but a customer's cart never
    // becomes anonymous or belongs to a different customer after auth expires.
    public void BindOwner(string? userId)
    {
        var owner = Session.GetString(OwnerKey);
        if (owner == userId) return;
        if (owner is not null) Clear();
        ClearCheckoutToken();
        if (userId is null) Session.Remove(OwnerKey);
        else Session.SetString(OwnerKey, userId);
    }

    public void EnsureOwner() => BindOwner(accessor.HttpContext!.User.Identity?.IsAuthenticated == true
        ? accessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) : null);

    public IReadOnlyList<CartItem> Read()
    {
        EnsureOwner();
        var json = Session.GetString(Key);
        if (json is null) return [];
        try { return JsonSerializer.Deserialize<List<CartItem>>(json)?.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList() ?? []; }
        catch (JsonException) { return []; }
    }

    public long Count => Read().Sum(i => (long)i.Quantity);
    public void Write(IEnumerable<CartItem> items)
    {
        EnsureOwner();
        Session.SetString(Key, JsonSerializer.Serialize(items));
    }
    public void Clear() => Session.Remove(Key);
    public string CheckoutToken()
    {
        EnsureOwner();
        var token = Session.GetString("PhoneStore.CheckoutToken") ?? Guid.NewGuid().ToString("N");
        Session.SetString("PhoneStore.CheckoutToken", token);
        return token;
    }
    public bool ValidToken(string? token)
    {
        EnsureOwner();
        return !string.IsNullOrEmpty(token) && token == Session.GetString("PhoneStore.CheckoutToken");
    }
    public void ClearCheckoutToken() => Session.Remove("PhoneStore.CheckoutToken");
}

