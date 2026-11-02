namespace PhoneStore.AdminBootstrap;

public sealed record BootstrapOptions(string Name, string Email, bool Execute,
    string? ConfirmServer, string? ConfirmDatabase)
{
    public static BootstrapOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var execute = false;
        foreach (var argument in args)
        {
            if (argument == "--execute")
            {
                if (execute) throw new ArgumentException("Tham số --execute bị lặp.");
                execute = true;
                continue;
            }
            var pair = argument.Split('=', 2);
            if (pair.Length != 2 || pair[0] is not ("--name" or "--email" or "--confirm-server" or "--confirm-database")
                || !values.TryAdd(pair[0], pair[1]))
                throw new ArgumentException("Tham số không hợp lệ hoặc bị lặp. Dùng --help; không truyền mật khẩu qua dòng lệnh.");
        }
        var options = new BootstrapOptions(values.GetValueOrDefault("--name", ""), values.GetValueOrDefault("--email", ""),
            execute, values.GetValueOrDefault("--confirm-server"), values.GetValueOrDefault("--confirm-database"));
        if (execute && (string.IsNullOrWhiteSpace(options.ConfirmServer) || string.IsNullOrWhiteSpace(options.ConfirmDatabase)))
            throw new ArgumentException("--execute yêu cầu --confirm-server và --confirm-database.");
        return options;
    }
}
