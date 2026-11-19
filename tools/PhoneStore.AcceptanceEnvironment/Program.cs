using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Entities;

const string database = "PhoneStoreDB_AcceptanceTest";
const string runtimeLogin = "ps_s323b_runtime";
const string provisionLogin = "ps_s323b_provision";
string Secret(string key) => Environment.GetEnvironmentVariable(key)
    ?? throw new InvalidOperationException("Missing acceptance environment setting.");
string Connection(string db, string user, string password) => new SqlConnectionStringBuilder
{
    DataSource = "127.0.0.1,14334", InitialCatalog = db, UserID = user, Password = password,
    Encrypt = true, TrustServerCertificate = true, MaxPoolSize = 5, ConnectTimeout = 5,
    ApplicationName = "PhoneStore.Acceptance.S323B"
}.ConnectionString;
async Task<object?> Scalar(SqlConnection c, string sql)
{
    using var cmd = new SqlCommand(sql, c); cmd.CommandTimeout = 15;
    return await cmd.ExecuteScalarAsync();
}
async Task Execute(SqlConnection c, string sql)
{
    using var cmd = new SqlCommand(sql, c); cmd.CommandTimeout = 30;
    await cmd.ExecuteNonQueryAsync();
}
async Task Guard(SqlConnection c, string db)
{
    if (!string.Equals(Convert.ToString(await Scalar(c, "SELECT @@SERVERNAME")), Secret("S323B_SERVER"), StringComparison.Ordinal)
        || !string.Equals(c.Database, db, StringComparison.Ordinal))
        throw new InvalidOperationException("Acceptance target guard failed.");
}
try
{
    if (args is ["provision"])
    {
        await using var admin = new SqlConnection(Connection("master", "sa", Secret("S323B_SA")));
        await admin.OpenAsync(); await Guard(admin, "master");
        if (await Scalar(admin, $"SELECT DB_ID(N'{database}')") is not DBNull
            || await Scalar(admin, $"SELECT SUSER_ID(N'{runtimeLogin}')") is not DBNull
            || await Scalar(admin, $"SELECT SUSER_ID(N'{provisionLogin}')") is not DBNull)
            throw new InvalidOperationException("Acceptance target/login already exists; no automatic rerun.");
        // New files only. No restore, REPLACE, DROP or alteration of business databases.
        await Execute(admin, $"""
            CREATE DATABASE [{database}] ON PRIMARY
            (NAME=N'{database}', FILENAME=N'/var/opt/mssql/data/{database}.mdf', SIZE=32MB, MAXSIZE=256MB, FILEGROWTH=8MB)
            LOG ON (NAME=N'{database}_log', FILENAME=N'/var/opt/mssql/data/{database}_log.ldf', SIZE=16MB, MAXSIZE=128MB, FILEGROWTH=8MB);
            """);
        foreach (var (login, key) in new[] { (provisionLogin, "S323B_PROVISION"), (runtimeLogin, "S323B_RUNTIME") })
        {
            using var cmd = new SqlCommand("DECLARE @sql nvarchar(max)=N'CREATE LOGIN '+QUOTENAME(@login)+N' WITH PASSWORD='+QUOTENAME(@password,CHAR(39))+N', CHECK_POLICY=ON, CHECK_EXPIRATION=OFF'; EXEC sys.sp_executesql @sql;", admin);
            cmd.Parameters.AddWithValue("@login", login); cmd.Parameters.AddWithValue("@password", Secret(key));
            await cmd.ExecuteNonQueryAsync();
        }
        await admin.ChangeDatabaseAsync(database); await Guard(admin, database);
        await Execute(admin, $"CREATE USER [{provisionLogin}] FOR LOGIN [{provisionLogin}]; ALTER ROLE db_owner ADD MEMBER [{provisionLogin}]; CREATE USER [{runtimeLogin}] FOR LOGIN [{runtimeLogin}];");
        await using (var db = new PhoneStoreDbContext(new DbContextOptionsBuilder<PhoneStoreDbContext>()
            .UseSqlServer(Connection(database, provisionLogin, Secret("S323B_PROVISION"))).Options))
        {
            await db.Database.OpenConnectionAsync();
            await Guard((SqlConnection)db.Database.GetDbConnection(), database);
            await db.Database.MigrateAsync();
            if (await db.Users.AnyAsync() || await db.Products.AnyAsync())
                throw new InvalidOperationException("Fixture database is not empty.");
            var hasher = new PasswordHasher<User>();
            foreach (var (name, email, role, key) in new[]
            {
                ("Acceptance Admin", "admin@s323b.test", 1, "S323B_ADMIN"),
                ("Acceptance Customer A", "customer-a@s323b.test", 2, "S323B_CUSTOMER_A"),
                ("Acceptance Customer B", "customer-b@s323b.test", 2, "S323B_CUSTOMER_B")
            })
            {
                var user = new User { FullName = name, Email = email, RoleId = role, IsActive = true, CreatedAt = DateTime.UtcNow };
                user.PasswordHash = hasher.HashPassword(user, Secret(key)); db.Users.Add(user);
            }
            var brands = new[] { new Brand { BrandName = "S323B Brand A", IsActive = true }, new Brand { BrandName = "S323B Brand B", IsActive = true } };
            var categories = new[] { new Category { CategoryName = "S323B Category A", IsActive = true }, new Category { CategoryName = "S323B Category B", IsActive = true } };
            db.Brands.AddRange(brands); db.Categories.AddRange(categories);
            var stocks = new[] { 5, 1, 0 };
            for (var n = 0; n < stocks.Length; n++)
                db.Products.Add(new Product { ProductName = $"S323B Product {n + 1}", Brand = brands[n % 2], Category = categories[n % 2], Price = (n + 1) * 1000000m, StockQuantity = stocks[n], RAM = "8 GB", Storage = "128 GB", CreatedAt = DateTime.UtcNow, IsActive = true });
            await db.SaveChangesAsync();
        }
        foreach (var table in new[] { "Roles", "Users", "Brands", "Categories", "Products", "ProductImages", "Orders", "OrderDetails" })
            await Execute(admin, $"GRANT SELECT, INSERT, UPDATE, DELETE ON OBJECT::dbo.[{table}] TO [{runtimeLogin}];");
        await Execute(admin, $"GRANT SELECT ON OBJECT::dbo.__EFMigrationsHistory TO [{runtimeLogin}];");
        await admin.ChangeDatabaseAsync("master"); await Guard(admin, "master");
        await Execute(admin, $"ALTER LOGIN [{provisionLogin}] DISABLE;");
        Console.WriteLine("PROVISION PASS: acceptance schema/fixtures created; provisioning login disabled.");
    }
    else if (args is not ["verify"]) throw new InvalidOperationException("Use provision or verify.");

    await using var runtime = new SqlConnection(Connection(database, runtimeLogin, Secret("S323B_RUNTIME")));
    await runtime.OpenAsync(); await Guard(runtime, database);
    var permissions = Convert.ToString(await Scalar(runtime, "SELECT CONCAT(IS_SRVROLEMEMBER('sysadmin'),'|',HAS_DBACCESS('PhoneStoreDB'),'|',HAS_PERMS_BY_NAME('dbo.Products','OBJECT','SELECT'),'|',HAS_PERMS_BY_NAME('dbo.Products','OBJECT','INSERT'),'|',HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','ALTER'))"));
    if (permissions != "0|0|1|1|0") throw new InvalidOperationException("Runtime permission guard failed.");
    if (Convert.ToInt32(await Scalar(runtime, "SELECT HAS_PERMS_BY_NAME('dbo.__EFMigrationsHistory','OBJECT','UPDATE')")) != 0)
        throw new InvalidOperationException("Runtime must not modify migration history.");
    try
    {
        await using var forbidden = new SqlConnection(Connection("PhoneStoreDB", runtimeLogin, Secret("S323B_RUNTIME")));
        await forbidden.OpenAsync();
        throw new InvalidOperationException("Runtime unexpectedly connected to business database.");
    }
    catch (SqlException ex) when (ex.Number is 4060 or 916 or 18456) { }
    using var read = new SqlCommand("SELECT (SELECT COUNT(*) FROM dbo.Roles),(SELECT COUNT(*) FROM dbo.Users),(SELECT COUNT(*) FROM dbo.Brands),(SELECT COUNT(*) FROM dbo.Categories),(SELECT COUNT(*) FROM dbo.Products),(SELECT COUNT(*) FROM dbo.Orders),(SELECT COUNT(*) FROM dbo.__EFMigrationsHistory); SELECT ProductId,Price,StockQuantity FROM dbo.Products ORDER BY ProductId;", runtime);
    using var reader = await read.ExecuteReaderAsync();
    while (await reader.ReadAsync()) Console.WriteLine($"Roles/Users/Brands/Categories/Products/Orders/Migrations={reader.GetInt32(0)}/{reader.GetInt32(1)}/{reader.GetInt32(2)}/{reader.GetInt32(3)}/{reader.GetInt32(4)}/{reader.GetInt32(5)}/{reader.GetInt32(6)}");
    await reader.NextResultAsync();
    while (await reader.ReadAsync()) Console.WriteLine($"FixtureProduct={reader.GetInt32(0)}|Price={reader.GetDecimal(1)}|Stock={reader.GetInt32(2)}");
    Console.WriteLine("VERIFY PASS: runtime DML limited to acceptance; business DB connection denied; no schema/server privileges.");
    return 0;
}
catch (Exception ex)
{
    // Do not print SQL/connection strings/exception messages containing credentials.
    Console.Error.WriteLine($"STOP: {ex.GetType().Name}; SQL error={(ex is SqlException sql ? sql.Number : 0)}. No automatic retry/cleanup.");
    return 1;
}
