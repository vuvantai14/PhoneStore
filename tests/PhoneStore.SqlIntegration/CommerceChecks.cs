using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Management;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Management;
using PhoneStore.Data.Purchasing;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Purchasing;

internal static partial class Program
{
    static async Task RunCommerceAsync(string fixtureName = "S29.8C Last Stock Test")
    {
        int c, p, lastStockProduct;
        await using (var db = Context())
        {
            Check(!await db.Products.AnyAsync(x => x.ProductName == fixtureName), "S29.8C has not run before");
            var customer = await db.Users.SingleAsync(x => x.Email == "customer@s29-8b1.test" && x.IsActive && x.RoleId == 2);
            var original = await db.Products.SingleAsync(x => x.ProductName == "S29.8B.1 Test Phone");
            Check(original.StockQuantity == 9, "preserved B.1 fixture baseline9");
            c = customer.UserId; p = original.ProductId;
            var last = new Product { ProductName = fixtureName, Price = 1_000_000m, StockQuantity = 1,
                BrandId = original.BrandId, CategoryId = original.CategoryId };
            db.Products.Add(last); await db.SaveChangesAsync(); lastStockProduct = last.ProductId;
        }
        var shipping = new ShippingInfo("S29.8C Receiver", "0900000000", "Isolated test address", null);
        var key = Guid.NewGuid().ToString("N");
        await using (var a = Context())
        await using (var b = Context())
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<OrderCreationResult> Submit(PhoneStore.Data.Context.PhoneStoreDbContext db)
            {
                await gate.Task;
                return await new OrderService(new PurchaseStore(db)).CreateIdempotentAsync(c, [new(p, 1)], shipping, key);
            }
            var first = Submit(a); var second = Submit(b); gate.SetResult();
            var results = await Task.WhenAll(first, second);
            Check(results.All(x => x.OrderId.HasValue) && results[0].OrderId == results[1].OrderId,
                "double checkout same key: same committed order");
            await using var verify = Context();
            Check(await verify.Products.Where(x => x.ProductId == p).Select(x => x.StockQuantity).SingleAsync() == 8,
                "same key decrements once9-to8");
            var replay = await new OrderService(new PurchaseStore(verify)).CreateIdempotentAsync(c, [], shipping, key);
            Check(replay.OrderId == results[0].OrderId, "new context/restart-style replay without cart");
            var id = results[0].OrderId!.Value;
            // Distinct scoped services, same order: both requests may report success, stock restores once.
            var cancelA = new OrderManagementService(new OrderManagementStore(a));
            var cancelB = new OrderManagementService(new OrderManagementStore(b));
            var cancels = await Task.WhenAll(cancelA.CancelForCustomerAsync(id, c), cancelB.CancelForCustomerAsync(id, c));
            Check(cancels.All(x => x.Succeeded), "double customer cancel accepted idempotently");
            Check(await verify.Products.Where(x => x.ProductId == p).Select(x => x.StockQuantity).SingleAsync() == 9,
                "double cancel restored exactly once8-to9");
            Check((await new OrderService(new PurchaseStore(verify)).GetReceiptAsync(id, c))!.Status == OrderStatus.Cancelled,
                "customer reads Cancelled written by management service");
        }
        await using (var a = Context())
        await using (var b = Context())
        {
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<OrderCreationResult> Submit(PhoneStore.Data.Context.PhoneStoreDbContext db)
            {
                await gate.Task;
                return await new OrderService(new PurchaseStore(db)).CreateIdempotentAsync(c, [new(lastStockProduct, 1)],
                    shipping, Guid.NewGuid().ToString("N"));
            }
            var one = Submit(a); var two = Submit(b); gate.SetResult();
            var results = await Task.WhenAll(one, two);
            Check(results.Count(x => x.OrderId.HasValue) == 1, "two distinct requests compete for last item: one order only");
            await using var verify = Context();
            Check(await verify.Products.Where(x => x.ProductId == lastStockProduct).Select(x => x.StockQuantity).SingleAsync() == 0,
                "last item final stock0, never negative");
        }
        int beforeOrders;
        await using (var db = Context()) beforeOrders = await db.Orders.CountAsync();
        var fault = new FailCommit();
        await using (var db = Context(fault))
        {
            var failed = await new OrderService(new PurchaseStore(db)).CreateIdempotentAsync(c, [new(p, 1)], shipping,
                Guid.NewGuid().ToString("N"));
            Check(failed.OrderId is null && fault.Reached, "checkout fault injected before commit");
        }
        await using (var db = Context())
        {
            Check(await db.Orders.CountAsync() == beforeOrders &&
                await db.Products.Where(x => x.ProductId == p).Select(x => x.StockQuantity).SingleAsync() == 9,
                "checkout rollback preserved orders/details/stock");
            var order = await db.Orders.Where(x => x.OrderDetails.Any(d => d.ProductId == lastStockProduct)).SingleAsync();
            var id = order.OrderId;
            var failedCancel = new FailCommit();
            await using (var other = Context(failedCancel))
            {
                var failed = await new OrderManagementService(new OrderManagementStore(other)).CancelForCustomerAsync(id, c);
                Check(!failed.Succeeded && failedCancel.Reached, "cancel fault injected before commit");
            }
            Check(await db.Orders.AsNoTracking().Where(x => x.OrderId == id).Select(x => x.Status).SingleAsync() == OrderStatus.Pending
                && await db.Products.Where(x => x.ProductId == lastStockProduct).Select(x => x.StockQuantity).SingleAsync() == 0,
                "cancel rollback preserved Pending and stock0");
        }
        Console.WriteLine("PASS S29.8C: guarded existing test DB only, no schema changes/no cleanup/no retries.");
    }
}
