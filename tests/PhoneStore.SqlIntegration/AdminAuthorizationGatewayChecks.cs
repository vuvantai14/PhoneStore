using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;
using PhoneStore.Admin.Management;
using PhoneStore.Business.Authentication;
using PhoneStore.Business.Management;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Management;

internal static partial class Program
{
    static async Task RunAdminAuthorizationGatewayChecksAsync()
    {
        var auth = new ControlledAdminAuth();
        using var provider = new ServiceCollection()
            .AddSingleton<IAdminAuthService>(auth)
            .AddScoped<ILookupManagementService, NoOpLookups>()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var session = new AdminSession();
        Establish(session);
        var gateway = new CatalogManagementGateway(provider.GetRequiredService<IServiceScopeFactory>(), session);

        var invoked = false;
        var value = await gateway.WithLookupsWriteAsync(_ => { invoked = true; return Task.FromResult(7); });
        Check(value == 7 && invoked && auth.Calls == 1, "active Admin fresh-check precedes write action");

        auth.Authorized = false; invoked = false; var revoked = false;
        gateway.AuthorizationRevoked += (_, _) => revoked = true;
        try { await gateway.WithLookupsWriteAsync(_ => { invoked = true; return Task.FromResult(0); });
            throw new InvalidOperationException("revoked session reached action"); }
        catch (UnauthorizedAccessException) { }
        Check(!invoked && revoked && session.Current is null, "revoked Admin clears session and blocks action");

        Establish(session); auth.Failure = new InvalidOperationException("controlled authorization store failure");
        invoked = false;
        try { await gateway.WithLookupsWriteAsync(_ => { invoked = true; return Task.FromResult(0); });
            throw new InvalidOperationException("authorization failure reached action"); }
        catch (InvalidOperationException ex) when (ex.Message == "controlled authorization store failure") { }
        Check(!invoked && session.Current is not null, "authorization DB failure fails closed without mislabeling revoke");
        Console.WriteLine("PASS S29.8D.4B gateway authorization self-tests; no database access.");
    }

    static void Establish(AdminSession session) => typeof(AdminSession)
        .GetMethod("Establish", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(session, [new AdminIdentity(1, "Test Admin", "admin@test.local", "Admin")]);

    sealed class ControlledAdminAuth : IAdminAuthService
    {
        public bool Authorized = true;
        public Exception? Failure;
        public int Calls;
        public Task<bool> IsCurrentAdminAsync(int userId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Failure is null ? Task.FromResult(Authorized) : Task.FromException<bool>(Failure);
        }
        public Task<AdminLoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    sealed class NoOpLookups : ILookupManagementService
    {
        public Task<IReadOnlyList<LookupRow>> ListAsync(LookupKind kind, string? search,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LookupRow>>([]);
        public Task<ManagementResult> SaveAsync(LookupKind kind, int? id, LookupEdit edit,
            CancellationToken cancellationToken = default) => Task.FromResult(ManagementResult.Success(id ?? 1));
        public Task<ManagementResult> SetActiveAsync(LookupKind kind, int id, bool active,
            CancellationToken cancellationToken = default) => Task.FromResult(ManagementResult.Success(id));
    }
}
