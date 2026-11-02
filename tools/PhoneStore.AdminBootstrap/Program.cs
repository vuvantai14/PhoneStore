using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PhoneStore.AdminBootstrap;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("AdminBootstrap --name=\"Họ tên\" --email=admin@example.com [--execute --confirm-server=localhost --confirm-database=PhoneStoreDB]");
    Console.WriteLine("Mặc định dry-run (chỉ đọc). Execute nhập mật khẩu ẩn hai lần trong terminal tương tác. Chạy từ repository root.");
    return 0;
}

try
{
    var options = BootstrapOptions.Parse(args);
    var request = new BootstrapRequest(options.Name, options.Email);
    var validation = BootstrapService.Validate(request);
    if (validation is not null) { Console.Error.WriteLine(validation); return 2; }
    await using var db = new PhoneStoreDbContextFactory().CreateDbContext([]);
    var connection = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
    // Initial bootstrap is intentionally limited to the local default instance.
    if (!string.Equals(connection.DataSource, "localhost", StringComparison.OrdinalIgnoreCase)
        || !connection.IntegratedSecurity || string.IsNullOrWhiteSpace(connection.InitialCatalog)
        || !string.IsNullOrEmpty(connection.AttachDBFilename))
    {
        Console.Error.WriteLine("Chỉ hỗ trợ localhost, Windows Authentication và database có tên rõ ràng; không attach file.");
        return 2;
    }
    Console.WriteLine($"Đích cấu hình: server={connection.DataSource}; database={connection.InitialCatalog}");
    string? password = null;
    if (options.Execute)
    {
        if (!string.Equals(options.ConfirmServer, connection.DataSource, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(options.ConfirmDatabase, connection.InitialCatalog, StringComparison.Ordinal))
        { Console.Error.WriteLine("Xác nhận không khớp đích cấu hình."); return 2; }
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        { Console.Error.WriteLine("Execute yêu cầu terminal tương tác; không nhận mật khẩu qua pipe/file."); return 2; }
        Console.Write("Mật khẩu: "); password = ReadPassword();
        Console.Write("Nhập lại mật khẩu: "); var repeat = ReadPassword();
        if (!string.Equals(password, repeat, StringComparison.Ordinal))
        { Console.Error.WriteLine("Mật khẩu nhập lại không khớp."); return 2; }
    }
    var result = await new BootstrapService(db, new PasswordHasher<User>()).RunAsync(
        request with { Execute = options.Execute, ConfirmServer = options.ConfirmServer,
            ConfirmDatabase = options.ConfirmDatabase, Password = password });
    Console.WriteLine(result.Message);
    return result.Succeeded ? 0 : 1;
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException
    or System.Text.Json.JsonException or OperationCanceledException)
{
    Console.Error.WriteLine("Không thể chạy. Kiểm tra tham số, cấu hình và terminal; không truyền mật khẩu qua dòng lệnh.");
    return 2;
}

static string ReadPassword()
{
    var buffer = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return buffer.ToString(); }
        if (key.Key == ConsoleKey.Escape || (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.C))
            throw new OperationCanceledException();
        if (key.Key == ConsoleKey.Backspace) { if (buffer.Length > 0) buffer.Length--; continue; }
        if (!char.IsControl(key.KeyChar))
        {
            if (buffer.Length >= 128) throw new ArgumentException("Mật khẩu quá dài.");
            buffer.Append(key.KeyChar);
        }
    }
}
