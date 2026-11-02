using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using PhoneStore.Models.Entities;

internal static partial class Program
{
    const string TestAdminEmail = "admin@s29-8b1.test";
    static readonly string CredentialStatus = Path.Combine(AppContext.BaseDirectory, "D2C1-password-reset-status.json");

    static async Task TestAdminCredentialPreflightAsync()
    {
        await using var cn = new SqlConnection(Connection); await cn.OpenAsync(); TargetGuard.Verify(cn, null);
        var user = await ReadExactTestAdminAsync(cn, null);
        Console.WriteLine($"PASS read-only: localhost/{Database}; actual DESKTOP-ES9362P; UserId={user.UserId}; email={user.Email}; role=Admin; active=true.");
    }

    static async Task<User> ReadExactTestAdminAsync(SqlConnection cn, SqlTransaction? tx)
    {
        TargetGuard.Verify(cn, tx);
        await using var cmd = cn.CreateCommand(); cmd.Transaction = tx;
        var hint = tx is null ? "" : "WITH (UPDLOCK,HOLDLOCK)";
        cmd.CommandText = $"SELECT u.UserId,u.Email,u.FullName,u.RoleId,u.IsActive FROM dbo.Users u {hint} JOIN dbo.Roles r ON r.RoleId=u.RoleId WHERE u.UserId=1 AND u.Email COLLATE Latin1_General_100_BIN2=@email AND u.RoleId=1 AND r.RoleName COLLATE Latin1_General_100_BIN2=N'Admin' AND u.IsActive=1;";
        cmd.Parameters.Add("@email", SqlDbType.NVarChar, 150).Value = TestAdminEmail;
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("Expected test identity does not match");
        var user = new User { UserId = reader.GetInt32(0), Email = reader.GetString(1), FullName = reader.GetString(2), RoleId = reader.GetInt32(3), IsActive = reader.GetBoolean(4) };
        if (await reader.ReadAsync() || user.Email != TestAdminEmail) throw new InvalidOperationException("Unexpected test identity");
        return user;
    }

    static async Task<int> ResetApprovedTestAdminAsync()
    {
        var updateAttempted = false; var commitAttempted = false; var rows = 0;
        string? newHash = null;
        void Status(string stage, bool? verified = null) => File.WriteAllText(CredentialStatus,
            JsonSerializer.Serialize(new { Stage = stage, Database, UserId = 1, Email = TestAdminEmail,
                UpdateAttempted = updateAttempted, CommitAttempted = commitAttempted,
                AffectedRows = rows, StoredHashMatchesNewCredential = verified, CheckedUtc = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
        try
        {
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
                throw new InvalidOperationException("Interactive terminal required");
            Console.Title = "TEST ONLY - S29.8D.2C.1 - Admin credential";
            Console.WriteLine($"TEST ONLY: localhost/{Database}; UserId 1; {TestAdminEmail}");
            await using (var cn = new SqlConnection(Connection))
            { await cn.OpenAsync(); _ = await ReadExactTestAdminAsync(cn, null); }
            Status("AwaitingHiddenPasswordInput");
            Console.WriteLine("Verified actual server/database and active Admin. Main database is never used.");
            Console.Write("New test password (8-128 characters, hidden): "); var password = ReadHiddenTestPassword();
            Console.Write("Confirm new test password (hidden): "); var repeat = ReadHiddenTestPassword();
            if (password.Length is < 8 or > 128 || string.IsNullOrWhiteSpace(password) || password != repeat)
                throw new InvalidOperationException("Password validation or confirmation failed");
            await using (var cn = new SqlConnection(Connection))
            {
                await cn.OpenAsync(); TargetGuard.Verify(cn, null);
                await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable);
                var user = await ReadExactTestAdminAsync(cn, tx);
                var hasher = new PasswordHasher<User>();
                newHash = hasher.HashPassword(user, password);
                if (hasher.VerifyHashedPassword(user, newHash, password) != PasswordVerificationResult.Success)
                    throw new InvalidOperationException("Hasher verification failed");
                password = repeat = string.Empty; // Never persisted or passed to CLI/logging.
                using (var marker = new FileStream(Path.Combine(AppContext.BaseDirectory, "D2C1-password-reset.attempt"), FileMode.CreateNew)) { }
                TargetGuard.Verify(cn, tx);
                await using var cmd = cn.CreateCommand(); cmd.Transaction = tx;
                cmd.CommandText = "UPDATE dbo.Users SET PasswordHash=@hash WHERE UserId=1 AND Email COLLATE Latin1_General_100_BIN2=@email AND RoleId=1 AND IsActive=1 AND EXISTS(SELECT 1 FROM dbo.Roles WHERE RoleId=1 AND RoleName COLLATE Latin1_General_100_BIN2=N'Admin');";
                cmd.Parameters.Add("@hash", SqlDbType.NVarChar, 500).Value = newHash;
                cmd.Parameters.Add("@email", SqlDbType.NVarChar, 150).Value = TestAdminEmail;
                updateAttempted = true;
                rows = await cmd.ExecuteNonQueryAsync();
                if (rows != 1) throw new InvalidOperationException("Expected exactly one updated row");
                commitAttempted = true;
                await tx.CommitAsync();
            }
            var verified = await CredentialMatchesAsync(newHash);
            if (!verified) throw new InvalidOperationException("Post-commit verification failed");
            Status("CommittedAndVerified", true);
            Console.WriteLine("PASS: exactly one test PasswordHash updated and verified; other columns were not assigned.");
            Console.WriteLine("Enter this password yourself in the TEST Admin window. No CRUD will start automatically.");
            Console.WriteLine("Press Enter to close this terminal."); Console.ReadLine();
            return 0;
        }
        catch
        {
            bool? matches = null;
            // Determine outcome by SELECT, never repeat UPDATE after error/ambiguous commit.
            if (updateAttempted && newHash is not null)
                try { matches = await CredentialMatchesAsync(newHash); } catch { }
            Status("StoppedWithoutRetry", matches);
            Console.Error.WriteLine("STOP: validation/update/commit could not be confirmed. No automatic retry. Report this status; do not repeat.");
            if (!Console.IsInputRedirected) { Console.WriteLine("Press Enter to close."); Console.ReadLine(); }
            return 1;
        }
        finally { newHash = null; }
    }

    static async Task<bool> CredentialMatchesAsync(string hash)
    {
        await using var cn = new SqlConnection(Connection); await cn.OpenAsync();
        _ = await ReadExactTestAdminAsync(cn, null); TargetGuard.Verify(cn, null);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT CASE WHEN COUNT(*)=1 THEN 1 ELSE 0 END FROM dbo.Users WHERE UserId=1 AND Email COLLATE Latin1_General_100_BIN2=@email AND RoleId=1 AND IsActive=1 AND PasswordHash COLLATE Latin1_General_100_BIN2=@hash;";
        cmd.Parameters.Add("@email", SqlDbType.NVarChar, 150).Value = TestAdminEmail;
        cmd.Parameters.Add("@hash", SqlDbType.NVarChar, 500).Value = hash;
        return Equals(await cmd.ExecuteScalarAsync(), 1);
    }

    static string ReadHiddenTestPassword()
    {
        var buffer = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return buffer.ToString(); }
            if (key.Key == ConsoleKey.Escape || key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                throw new OperationCanceledException();
            if (key.Key == ConsoleKey.Backspace) { if (buffer.Length > 0) buffer.Length--; continue; }
            if (!char.IsControl(key.KeyChar)) { if (buffer.Length >= 128) throw new InvalidOperationException(); buffer.Append(key.KeyChar); }
        }
    }
}
