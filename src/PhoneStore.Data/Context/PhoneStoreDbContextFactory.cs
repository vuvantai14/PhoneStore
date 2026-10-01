using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PhoneStore.Data.Context;

/// <summary>Allows migration generation without requiring a reachable database.</summary>
public class PhoneStoreDbContextFactory : IDesignTimeDbContextFactory<PhoneStoreDbContext>
{
    public PhoneStoreDbContext CreateDbContext(string[] args)
    {
        string? connectionString = null;
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PhoneStore.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is not null)
        {
            var webPath = Path.Combine(directory.FullName, "src", "PhoneStore.Web");
            connectionString = ReadConnectionString(Path.Combine(webPath, "appsettings.json"));
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
            connectionString = ReadConnectionString(Path.Combine(webPath, $"appsettings.{environment}.json"))
                ?? connectionString;
        }

        connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__PhoneStoreConnection")
            ?? connectionString;
        var options = new DbContextOptionsBuilder<PhoneStoreDbContext>();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Configure only the provider for offline schema generation; no server is invented.
            options.UseSqlServer(_ => { });
        }
        else
        {
            options.UseSqlServer(connectionString);
        }

        return new PhoneStoreDbContext(options.Options);
    }

    private static string? ReadConnectionString(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty("ConnectionStrings", out var connections)
            && connections.TryGetProperty("PhoneStoreConnection", out var value)
            ? value.GetString() : null;
    }
}
