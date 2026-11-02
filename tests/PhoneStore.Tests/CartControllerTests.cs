using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Purchasing;
using PhoneStore.Models.Purchasing;
using PhoneStore.Web.Controllers;
using PhoneStore.Web.Models;
using PhoneStore.Web.Purchasing;

namespace PhoneStore.Tests;

public sealed class CartControllerTests
{
    [Fact]
    public async Task Completed_submission_token_is_rotated_for_the_next_independent_checkout()
    {
        await using var t = await TestDatabase.CreateAsync();
        var setup = Setup(t);
        var first = setup.Cart.CheckoutToken();
        Assert.Equal(first, setup.Cart.CheckoutToken());
        setup.Cart.ClearCheckoutToken();
        var second = setup.Cart.CheckoutToken();
        Assert.NotEqual(first, second);
        Assert.False(setup.Cart.ValidToken(first));
        Assert.True(setup.Cart.ValidToken(second));
    }
    static (CartController Controller, SessionCart Cart, CartPresenter Presenter) Setup(TestDatabase test)
    {
        var context = Context();
        var cart = new SessionCart(new FixedAccessor { HttpContext = context });
        var orders = new OrderService(new PurchaseStore(test.Db));
        var presenter = new CartPresenter(cart, orders);
        var controller = new CartController(cart, presenter, orders) { ControllerContext = new() { HttpContext = context },
            TempData = new TempDataDictionary(context, new EmptyTempData()) };
        return (controller, cart, presenter);
    }

    [Fact]
    public async Task Add_update_remove_use_server_price_and_isolate_sessions()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (_, p) = await t.SeedCatalogAsync();
        var a = Setup(t); var b = Setup(t);
        await a.Controller.Add(p.ProductId, 2, default);
        await a.Controller.Add(p.ProductId, 1, default);
        Assert.Equal(3, a.Cart.Read().Single().Quantity);
        Assert.Empty(b.Cart.Read());
        await t.Db.Products.Where(x => x.ProductId == p.ProductId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Price, 2_000_000m));
        Assert.Equal(6_000_000m, (await a.Presenter.GetAsync(default)).TotalAmount);
        await a.Controller.Update(p.ProductId, 4, default);
        Assert.Equal(8_000_000m, (await a.Presenter.GetAsync(default)).TotalAmount);
        a.Controller.Remove(p.ProductId);
        Assert.Empty(a.Cart.Read());
        Assert.False((await a.Presenter.GetAsync(default)).CanCheckout);
        Assert.Empty(await t.Db.Orders.ToListAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(11)]
    [InlineData(int.MaxValue)]
    public async Task Invalid_or_excess_quantity_does_not_change_cart(int quantity)
    {
        await using var t = await TestDatabase.CreateAsync();
        var (_, p) = await t.SeedCatalogAsync();
        var s = Setup(t);
        await s.Controller.Add(p.ProductId, 1, default);
        await s.Controller.Update(p.ProductId, quantity, default);
        Assert.Equal(1, s.Cart.Read().Single().Quantity);
        Assert.NotNull(s.Controller.TempData["CartMessage"]);
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 0)]
    public async Task Inactive_or_out_of_stock_product_is_rejected(bool active, int stock)
    {
        await using var t = await TestDatabase.CreateAsync();
        var (_, p) = await t.SeedCatalogAsync();
        var s = Setup(t);
        s.Cart.Write([new CartItem(p.ProductId, 1)]);
        await t.Db.Products.Where(x => x.ProductId == p.ProductId).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.IsActive, active).SetProperty(x => x.StockQuantity, stock));
        Assert.False((await s.Presenter.GetAsync(default)).CanCheckout);
        await s.Controller.Add(p.ProductId, 1, default);
        Assert.Equal(1, s.Cart.Read().Single().Quantity);
    }

    [Fact]
    public async Task Missing_product_and_update_of_nonexistent_cart_line_are_rejected()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (_, p) = await t.SeedCatalogAsync();
        var s = Setup(t);
        await s.Controller.Add(p.ProductId + 999, 1, default);
        await s.Controller.Update(p.ProductId, 1, default);
        Assert.Empty(s.Cart.Read());
    }

    [Fact]
    public async Task Checkout_post_replay_uses_database_after_memory_and_session_token_are_gone()
    {
        await using var t = await TestDatabase.CreateAsync();
        var (c, p) = await t.SeedCatalogAsync();
        var setup = Setup(t);
        var token = setup.Cart.CheckoutToken();
        var service = new OrderService(new PurchaseStore(t.Db));
        var first = await service.CreateIdempotentAsync(c.UserId, [new(p.ProductId, 1)],
            new ShippingInfo("Receiver", "0900000000", "Address", null), token);
        setup.Cart.Clear(); setup.Cart.ClearCheckoutToken();
        var context = Context();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, c.UserId.ToString())], "test"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = new CheckoutController(setup.Cart, setup.Presenter, service, new CheckoutSubmissionGuard(cache))
        { ControllerContext = new() { HttpContext = context } };
        var result = Assert.IsType<RedirectToActionResult>(await controller.Index(new CheckoutViewModel { SubmissionToken = token }, default));
        Assert.Equal("Success", result.ActionName);
        Assert.Equal(first.OrderId, result.RouteValues!["id"]);
        Assert.Single(await t.Db.Orders.ToListAsync());
    }

    static DefaultHttpContext Context() => new() { Session = new MemorySession() };
    // HttpContextAccessor uses a shared AsyncLocal. Independent simulated requests
    // on the same test thread need separate accessors rather than overwriting it.
    sealed class FixedAccessor : IHttpContextAccessor { public HttpContext? HttpContext { get; set; } }
    sealed class EmptyTempData : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
    sealed class MemorySession : ISession
    {
        readonly Dictionary<string, byte[]> data = new();
        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => data.Keys;
        public void Clear() => data.Clear();
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Remove(string key) => data.Remove(key);
        public void Set(string key, byte[] value) => data[key] = value;
        public bool TryGetValue(string key, out byte[] value) => data.TryGetValue(key, out value!);
    }
}
