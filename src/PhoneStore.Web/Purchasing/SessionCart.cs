using System.Text.Json;
using PhoneStore.Models.Purchasing;

namespace PhoneStore.Web.Purchasing;

public sealed class SessionCart(IHttpContextAccessor accessor)
{
    private const string Key = "PhoneStore.Cart";
    private ISession Session => accessor.HttpContext!.Session;

    public IReadOnlyList<CartItem> Read()
    {
        var json = Session.GetString(Key);
        if (json is null) return [];
        try { return JsonSerializer.Deserialize<List<CartItem>>(json)?.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList() ?? []; }
        catch (JsonException) { return []; }
    }

    public long Count => Read().Sum(i => (long)i.Quantity);
    public void Write(IEnumerable<CartItem> items) => Session.SetString(Key, JsonSerializer.Serialize(items));
    public void Clear() => Session.Remove(Key);
    public string CheckoutToken()
    {
        var token = Session.GetString("PhoneStore.CheckoutToken") ?? Guid.NewGuid().ToString("N");
        Session.SetString("PhoneStore.CheckoutToken", token);
        return token;
    }
    public bool ValidToken(string? token) => !string.IsNullOrEmpty(token) && token == Session.GetString("PhoneStore.CheckoutToken");
    public void ClearCheckoutToken() => Session.Remove("PhoneStore.CheckoutToken");
}

