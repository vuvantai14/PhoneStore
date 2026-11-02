using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

namespace PhoneStore.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly string? temporaryFile;
    private TestDatabase(bool useTemporaryFile)
    {
        temporaryFile = useTemporaryFile
            ? Path.Combine(Path.GetTempPath(), "PhoneStore-bootstrap-" + Guid.NewGuid().ToString("N") + ".db") : null;
        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = temporaryFile ?? ":memory:", Mode = temporaryFile is null ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private, Pooling = false
        }.ToString());
    }
    public PhoneStoreDbContext Db { get; private set; } = null!;

    public static async Task<TestDatabase> CreateAsync(bool useTemporaryFile = false)
    {
        var database = new TestDatabase(useTemporaryFile);
        database.connection.CreateFunction("SYSUTCDATETIME", () => DateTime.UtcNow);
        await database.connection.OpenAsync();
        database.Db = new PhoneStoreDbContext(new DbContextOptionsBuilder<PhoneStoreDbContext>()
            .UseSqlite(database.connection).Options);
        await database.Db.Database.EnsureCreatedAsync();
        return database;
    }

    public async Task<(User Customer, Product Product)> SeedCatalogAsync(int stock = 10, decimal price = 1_000_000m)
    {
        var customerRoleId = await Db.Roles.Where(x => x.RoleName == "Customer").Select(x => x.RoleId).SingleAsync();
        var customer = new User { FullName = "Khách kiểm thử", Email = "customer@test.local", PasswordHash = "hashed", IsActive = true, RoleId = customerRoleId };
        var product = new Product
        {
            ProductName = "Điện thoại kiểm thử", Price = price, StockQuantity = stock, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            Brand = new Brand { BrandName = "Test Brand", IsActive = true },
            Category = new Category { CategoryName = "Test Category", IsActive = true }
        };
        Db.AddRange(customer, product);
        await Db.SaveChangesAsync();
        return (customer, product);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await connection.DisposeAsync();
        if (temporaryFile is not null) File.Delete(temporaryFile);
    }
}
