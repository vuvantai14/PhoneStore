using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;
using PhoneStore.Admin.Controls;
using PhoneStore.Admin.Dialogs;
using PhoneStore.Admin.Management;
using PhoneStore.Business.Management;
using PhoneStore.Business.Purchasing;
using PhoneStore.Data.Management;
using PhoneStore.Data.Purchasing;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;
using PhoneStore.Models.Purchasing;

internal static partial class Program
{
    static T Field<T>(object instance, string name) => (T)instance.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
    static Task InvokeAsync(object instance, string name, params object[] args) =>
        (Task)instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, args)!;
    static IEnumerable<Button> Buttons(Control parent) => parent.Controls.Cast<Control>()
        .SelectMany(c => (c is Button b ? new[] { b } : Array.Empty<Button>()).Concat(Buttons(c)));

    static void RunAdminOrdersUi()
    {
        using var provider = new ServiceCollection()
            .AddDbContext<PhoneStore.Data.Context.PhoneStoreDbContext>(o => o.UseSqlServer(Connection).AddInterceptors(Guard))
            .AddScoped<OrderManagementStore>().AddScoped<IOrderManagementService, OrderManagementService>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var session = new AdminSession();
        // Test-only session establishment; never reads/resets an Admin password.
        typeof(AdminSession).GetMethod("Establish", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(session, [new AdminIdentity(1, "SQL Test Admin", "admin@s29-8b1.test", "Admin")]);
        var gateway = new CatalogManagementGateway(provider.GetRequiredService<IServiceScopeFactory>(), session);
        using var shell = new Form { Text = "S29.8C.5 isolated orders acceptance", Width = 1200, Height = 850 };
        using var control = new OrdersManagementControl(gateway) { Dock = DockStyle.Fill };
        shell.Controls.Add(control);
        Exception? failure = null;
        shell.Shown += async (_, _) =>
        {
            try
            {
                int customer;
                await using (var db = Context())
                {
                    Check(!await db.Orders.AnyAsync(o => o.ReceiverName == "S29.8C.5 UI Receiver"), "UI fixture not previously run");
                    customer = await db.Users.Where(u => u.Email == "customer@s29-8b1.test" && u.IsActive).Select(u => u.UserId).SingleAsync();
                    productId = await db.Products.Where(p => p.ProductName == "S29.8B.1 Test Phone").Select(p => p.ProductId).SingleAsync();
                }
                var stockBefore = await StockAsync();
                var shipping = new ShippingInfo("S29.8C.5 UI Receiver", "0900000000", "SQL test address", null);
                var key1 = Guid.NewGuid().ToString("N"); var key2 = Guid.NewGuid().ToString("N");
                int firstId, secondId;
                await using (var db = Context())
                {
                    var service = new OrderService(new PurchaseStore(db));
                    firstId = (await service.CreateIdempotentAsync(customer, [new(productId, 1)], shipping, key1)).OrderId
                        ?? throw new InvalidOperationException("first fixture checkout failed");
                    secondId = (await service.CreateIdempotentAsync(customer, [new(productId, 1)], shipping, key2)).OrderId
                        ?? throw new InvalidOperationException("second fixture checkout failed");
                    Check(firstId != secondId, "independent checkout keys create independent orders");
                    Check((await service.CreateIdempotentAsync(customer, [], shipping, key1)).OrderId == firstId, "same request replay returns original");
                }
                await Task.Delay(300);
                await InvokeAsync(control, "ReloadAsync");
                var grid = Field<DataGridView>(control, "grid");
                await using (var db = Context()) Check(grid.Rows.Count == await db.Orders.CountAsync(), "real Admin grid lists all test orders");
                Field<TextBox>(control, "search").Text = shipping.ReceiverName;
                Buttons(control).Single(b => b.Text == "Tìm kiếm").PerformClick();
                await WaitUntil(() => !Field<bool>(control, "busy"));
                Check(grid.Rows.Count == 2, "real search button finds two UI fixtures");
                var filter = Field<ComboBox>(control, "statusFilter");
                filter.SelectedIndex = 1; // Pending is enum0, preceded by All.
                Buttons(control).Single(b => b.Text == "Tìm kiếm").PerformClick();
                await WaitUntil(() => !Field<bool>(control, "busy"));
                Check(grid.Rows.Count == 2, "real Pending status filter");
                using (var dialog = new OrderDetailDialog(firstId, gateway))
                {
                    dialog.Show(shell);
                    await WaitUntil(() => Field<Label>(dialog, "heading").Text.Contains("ORD-"));
                    Check(Field<DataGridView>(dialog, "items").Rows.Count == 1 &&
                        Field<Label>(dialog, "summary").Text.Contains("customer@s29-8b1.test") &&
                        Field<Label>(dialog, "shipping").Text.Contains(shipping.ReceiverName), "real detail items/customer/shipping");
                    Check(!Buttons(Field<FlowLayoutPanel>(dialog, "actions")).Any(b => b.Text == "Hoàn thành"), "Pending UI offers no invalid Completed transition");
                    await ClickTransition(dialog, "Xác nhận", "Đã xác nhận");
                    await ClickTransition(dialog, "Chuyển sang giao hàng", "Đang giao");
                    Check(!Buttons(Field<FlowLayoutPanel>(dialog, "actions")).Any(b => b.Text == "Hủy đơn"), "Shipping UI cannot cancel");
                    await using (var db = Context())
                    {
                        var admin = new OrderManagementService(new OrderManagementStore(db));
                        Check(!(await admin.UpdateStatusAsync(firstId, OrderStatus.Shipping, OrderStatus.Pending)).Succeeded, "backend rejects reverse transition");
                        Check(!(await admin.CancelForCustomerAsync(firstId, customer)).Succeeded, "Customer cannot cancel Shipping");
                    }
                    await ClickTransition(dialog, "Hoàn thành", "Hoàn thành");
                    Check(!Buttons(Field<FlowLayoutPanel>(dialog, "actions")).Any(), "Completed UI has no transitions");
                    await using (var db = Context())
                    {
                        Check((await new OrderService(new PurchaseStore(db)).GetReceiptAsync(firstId, customer))!.Status == OrderStatus.Completed,
                            "Customer reader sees UI Completed");
                        Check(!(await new OrderManagementService(new OrderManagementStore(db)).CancelForCustomerAsync(firstId, customer)).Succeeded,
                            "Customer cannot cancel Completed");
                    }
                    dialog.Close();
                }
                using (var dialog = new OrderDetailDialog(secondId, gateway))
                {
                    dialog.Show(shell);
                    await WaitUntil(() => Field<Label>(dialog, "heading").Text.Contains("ORD-"));
                    await ClickTransition(dialog, "Hủy đơn", "Đã hủy");
                    Check(!Buttons(Field<FlowLayoutPanel>(dialog, "actions")).Any(), "Cancelled UI has no actions");
                    await using var db = Context();
                    var admin = new OrderManagementService(new OrderManagementStore(db));
                    Check(!(await admin.UpdateStatusAsync(secondId, OrderStatus.Pending, OrderStatus.Cancelled)).Succeeded,
                        "stale Admin repeat cancel rejected");
                    Check((await admin.CancelForCustomerAsync(secondId, customer)).Succeeded, "Customer repeat cancel idempotent");
                    Check(await StockAsync() == stockBefore - 1, "UI cancel restores exactly one item; completed order stays deducted");
                    Check((await new OrderService(new PurchaseStore(db)).GetReceiptAsync(secondId, customer))!.Status == OrderStatus.Cancelled,
                        "Customer reader sees UI Cancelled");
                    dialog.Close();
                }
                filter.SelectedIndex = 0; await InvokeAsync(control, "ReloadAsync");
                Check(grid.Rows.Count == 2, "list refresh keeps both fixture orders visible");
                Console.WriteLine($"PASS UI orders acceptance: CompletedId={firstId}, CancelledId={secondId}, stock={await StockAsync()}");
            }
            catch (Exception ex) { failure = ex; }
            finally { shell.Close(); }
        };
        Application.Run(shell);
        if (failure is not null) throw failure;
    }

    static async Task WaitUntil(Func<bool> predicate)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!predicate() && DateTime.UtcNow < until) await Task.Delay(50);
        if (!predicate()) throw new TimeoutException("UI operation did not complete");
    }
    static async Task ClickTransition(OrderDetailDialog dialog, string label, string status)
    {
        var button = Buttons(Field<FlowLayoutPanel>(dialog, "actions")).Single(b => b.Text == label);
        using var confirmation = new System.Windows.Forms.Timer { Interval = 100 };
        var confirmed = false;
        confirmation.Tick += (_, _) =>
        {
            var window = FindWindow("#32770", "Xác nhận cập nhật");
            if (window == IntPtr.Zero) return;
            GetWindowThreadProcessId(window, out var pid);
            if (pid != Environment.ProcessId) return; // Never touch another app's confirmation.
            var yes = GetDlgItem(window, 6);
            if (yes == IntPtr.Zero) return;
            confirmation.Stop(); confirmed = true; SendMessage(yes, 0x00F5, IntPtr.Zero, IntPtr.Zero);
        };
        confirmation.Start(); button.PerformClick();
        await WaitUntil(() => !Field<bool>(dialog, "busy") && Field<Label>(dialog, "heading").Text.Contains(status));
        Check(confirmed && dialog.HasChanges, "real UI transition confirmed: " + label);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string className, string windowName);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out int pid);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr window, int id);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
