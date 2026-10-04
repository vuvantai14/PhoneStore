using PhoneStore.Business.Purchasing;
using PhoneStore.Web.Models;

namespace PhoneStore.Web.Purchasing;

public sealed class CartPresenter(SessionCart cart, IOrderService orders)
{
    public async Task<CartViewModel> GetAsync(CancellationToken ct)
    {
        var items = cart.Read();
        var products = await orders.GetCartProductsAsync(items.Select(i => i.ProductId), ct);
        return new CartViewModel { Items = items.Select(i => new CartLineViewModel(i, products.SingleOrDefault(p => p.ProductId == i.ProductId))).ToList() };
    }
}

