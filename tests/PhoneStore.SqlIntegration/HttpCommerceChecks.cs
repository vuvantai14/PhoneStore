using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Management;
using PhoneStore.Data.Management;
using PhoneStore.Models.Enums;

internal static partial class Program
{
    static async Task RunHttpCommerceAsync(string email = "customer@s29-8c-http-verified.test")
    {
        int stockBefore;
        if (IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(x => x.Port == 5016))
            throw new InvalidOperationException("Test port5016 already occupied");
        await using (var preflight = Context())
        {
            Check(!await preflight.Users.AnyAsync(u => u.Email == email), "HTTP fixture not previously run");
            stockBefore = await preflight.Products.Where(p => p.ProductId == 1).Select(p => p.StockQuantity).SingleAsync();
            Check(stockBefore >= 3, "enough isolated stock for HTTP cart checks");
        }
        var root = Directory.GetCurrentDirectory();
        var exe = Path.Combine(root, "src/PhoneStore.Web/bin/S29.8C/PhoneStore.Web.exe");
        var start = new ProcessStartInfo(exe)
        {
            UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = Path.Combine(root, "src/PhoneStore.Web"),
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.Environment["ConnectionStrings__PhoneStoreConnection"] = Connection;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_URLS"] = "http://127.0.0.1:5016";
        using var process = Process.Start(start)!;
        // Drain diagnostics without printing SQL parameters or credentials.
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        Console.WriteLine($"HTTP test Web PID={process.Id}, URL=http://127.0.0.1:5016; main process untouched");
        using var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5016"), Timeout = TimeSpan.FromSeconds(20) };
        try
        {
            for (var i = 0; ; i++)
            {
                try { (await client.GetAsync("/products")).EnsureSuccessStatusCode(); break; }
                catch (HttpRequestException) when (i < 30 && !process.HasExited) { await Task.Delay(200); }
            }
            async Task VerifyServer()
            {
                if (process.HasExited) throw new InvalidOperationException("Test Web exited");
                await using var connection = new SqlConnection(Connection); await connection.OpenAsync();
                TargetGuard.Verify(connection, null);
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE host_process_id=@pid AND database_id=DB_ID()";
                cmd.Parameters.AddWithValue("@pid", process.Id);
                if ((int)(await cmd.ExecuteScalarAsync())! < 1) throw new InvalidOperationException("Test Web SQL session not verified");
            }
            async Task<string> Page(string url) => WebUtility.HtmlDecode(await client.GetStringAsync(url));
            static string Field(string html, string name)
            {
                var input = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>");
                var value = Regex.Match(input.Value, "value=\"([^\"]*)\"");
                if (!value.Success) throw new InvalidOperationException("Missing form field " + name);
                return WebUtility.HtmlDecode(value.Groups[1].Value);
            }
            async Task<HttpResponseMessage> Post(string url, Dictionary<string, string> values, string token)
            {
                await VerifyServer(); // Explicit destination/server confirmation before EACH write request.
                values["__RequestVerificationToken"] = token;
                return await client.PostAsync(url, new FormUrlEncodedContent(values));
            }
            var password = Guid.NewGuid().ToString("N") + "aA9!"; // Memory only, never output.
            var register = await Page("/account/register");
            var registered = await Post("/account/register", new() { ["FullName"] = "S29.8C HTTP Customer", ["Email"] = email,
                ["Password"] = password, ["ConfirmPassword"] = password }, Field(register, "__RequestVerificationToken"));
            Check(registered.RequestMessage!.RequestUri!.AbsolutePath == "/account/login", "HTTP registration in test DB");
            var login = await Page("/account/login");
            var logged = await Post("/account/login", new() { ["Email"] = email, ["Password"] = password }, Field(login, "__RequestVerificationToken"));
            Check(logged.RequestMessage!.RequestUri!.AbsolutePath == "/", "HTTP customer login cookie");
            var cart = await Page("/cart"); var csrf = Field(cart, "__RequestVerificationToken");
            using var outsider = new HttpClient { BaseAddress = client.BaseAddress };
            Check((await outsider.GetAsync("/orders")).RequestMessage!.RequestUri!.AbsolutePath == "/account/login",
                "anonymous order history requires login");
            var missingToken = await client.PostAsync("/cart/add", new FormUrlEncodedContent(new Dictionary<string, string>
                { ["productId"] = "1", ["quantity"] = "1" }));
            Check(missingToken.StatusCode == HttpStatusCode.BadRequest, "HTTP anti-forgery missing token400");
            await Post("/cart/add", new() { ["productId"] = "1", ["quantity"] = "2", ["price"] = "1" }, csrf);
            cart = await Page("/cart");
            Check(cart.Contains("2.000.000"), "HTTP cart uses server total, ignores injected price");
            await Post("/cart/update", new() { ["productId"] = "1", ["quantity"] = "3" }, csrf);
            Check((await Page("/cart")).Contains("3.000.000"), "HTTP update quantity3");
            await Post("/cart/remove", new() { ["productId"] = "1" }, csrf);
            Check(!(await Page("/cart")).Contains("3.000.000"), "HTTP remove cart line");
            var invalidQuantity = await Post("/cart/add", new() { ["productId"] = "1", ["quantity"] = "0" }, csrf);
            // Redirect response already renders/consumes TempData; do not GET it a second time.
            Check(WebUtility.HtmlDecode(await invalidQuantity.Content.ReadAsStringAsync()).Contains("Số lượng phải"), "HTTP invalid quantity rejected");
            await Post("/cart/add", new() { ["productId"] = "1", ["quantity"] = "1" }, csrf);
            var checkout = await Page("/checkout"); var submission = Field(checkout, "SubmissionToken");
            var checkoutCsrf = Field(checkout, "__RequestVerificationToken");
            Dictionary<string, string> Payload() => new() { ["ReceiverName"] = "HTTP Receiver", ["ReceiverPhone"] = "0900000000",
                ["ShippingAddress"] = "Isolated HTTP address", ["SubmissionToken"] = submission, ["TotalAmount"] = "1" };
            var responses = await Task.WhenAll(Post("/checkout", Payload(), checkoutCsrf), Post("/checkout", Payload(), checkoutCsrf));
            var paths = responses.Select(x => x.RequestMessage!.RequestUri!.AbsolutePath).ToArray();
            Check(paths[0] == paths[1] && paths[0].StartsWith("/checkout/success/"), "HTTP concurrent duplicate checkout same receipt");
            var replay = await Post("/checkout", Payload(), checkoutCsrf);
            Check(replay.RequestMessage!.RequestUri!.AbsolutePath == paths[0], "HTTP replay after cart/session-token clear");
            var orderId = int.Parse(paths[0].Split('/').Last());
            await using (var adminDb = Context())
            {
                var update = await new OrderManagementService(new OrderManagementStore(adminDb))
                    .UpdateStatusAsync(orderId, OrderStatus.Pending, OrderStatus.Confirmed);
                Check(update.Succeeded, "Admin service confirms HTTP customer's order");
            }
            Check((await Page("/orders/" + orderId)).Contains("Đã xác nhận") && (await Page("/orders")).Contains("Đã xác nhận"),
                "Customer HTTP history/detail reflects Admin Confirmed");
            Check((await client.GetAsync("/orders/1")).StatusCode == HttpStatusCode.NotFound, "HTTP foreign order404");
            var denied = await Post("/orders/1/cancel", new(), checkoutCsrf);
            Check(denied.StatusCode == HttpStatusCode.NotFound, "HTTP foreign cancel404");
            var detail = await Page("/orders/" + orderId);
            var cancelCsrf = Field(detail, "__RequestVerificationToken");
            await Post($"/orders/{orderId}/cancel", new(), cancelCsrf);
            await Post($"/orders/{orderId}/cancel", new(), cancelCsrf);
            Check((await Page("/orders/" + orderId)).Contains("Đã hủy"), "HTTP detail shows Cancelled after repeated cancel");
            Check((await Page("/orders")).Contains("Đã hủy"), "HTTP order history shows Cancelled");
            await using var db = Context();
            var customer = await db.Users.SingleAsync(x => x.Email == email);
            Check(await db.Orders.CountAsync(x => x.UserId == customer.UserId) == 1 &&
                await db.Products.Where(x => x.ProductId == 1).Select(x => x.StockQuantity).SingleAsync() == stockBefore,
                "HTTP one order only and stock restored once");
            var managed = await new OrderManagementService(new OrderManagementStore(db)).GetAsync(orderId);
            Check(managed!.Status == OrderStatus.Cancelled && managed.TotalAmount == 1_000_000m,
                "Admin reader agrees with Customer; server price snapshot");
            var logoutCsrf = Field(await Page("/"), "__RequestVerificationToken");
            await Post("/account/logout", new(), logoutCsrf);
            Check((await client.GetAsync("/orders")).RequestMessage!.RequestUri!.AbsolutePath == "/account/login", "HTTP logout clears auth");
            Console.WriteLine("PASS S29.8C HTTP commerce on test DB only");
        }
        finally
        {
            // Stop ONLY the helper created above, never discover/kill a main application.
            if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); }
        }
    }
}
