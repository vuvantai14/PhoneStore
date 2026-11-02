using Microsoft.Extensions.DependencyInjection;
using PhoneStore.Admin.Authentication;

namespace PhoneStore.Admin;

internal sealed class AdminApplicationContext : ApplicationContext
{
    private readonly IServiceProvider services;
    private readonly AdminSession session;
    private readonly Management.CatalogManagementGateway gateway;
    private LoginForm? login;
    private MainForm? shell;

    public AdminApplicationContext(IServiceProvider services)
    {
        this.services = services;
        session = services.GetRequiredService<AdminSession>();
        gateway = services.GetRequiredService<Management.CatalogManagementGateway>();
        gateway.AuthorizationRevoked += OnAuthorizationRevoked;
        ShowLogin();
    }

    private void ShowLogin()
    {
        login = services.GetRequiredService<LoginForm>();
        login.Authenticated += OnAuthenticated;
        login.FormClosed += OnLoginClosed;
        login.Show();
    }

    private void OnAuthenticated(object? sender, AdminAuthenticatedEventArgs args)
    {
        session.Establish(args.Identity);
        try
        {
            shell = services.GetRequiredService<MainForm>();
            shell.LogoutRequested += OnLogout;
            shell.FormClosed += OnShellClosed;
            shell.Show();
            CloseLogin();
        }
        catch
        {
            session.Clear();
            CloseShell();
            throw;
        }
    }

    private void CloseLogin()
    {
        if (login is null) return;
        login.Authenticated -= OnAuthenticated;
        login.FormClosed -= OnLoginClosed;
        login.Close();
        login.Dispose();
        login = null;
    }

    private void OnLogout(object? sender, EventArgs args)
    {
        session.Clear();
        CloseShell();
        ShowLogin();
    }

    private void OnAuthorizationRevoked(object? sender, EventArgs args)
    {
        var currentShell = shell;
        if (currentShell is null || currentShell.IsDisposed || !currentShell.IsHandleCreated) return;
        currentShell.BeginInvoke(new Action(() =>
        {
            if (session.Current is not null) return;
            CloseShell();
            if (login is null) ShowLogin();
        }));
    }

    private void CloseShell()
    {
        if (shell is null) return;
        shell.LogoutRequested -= OnLogout;
        shell.FormClosed -= OnShellClosed;
        shell.Close();
        shell.Dispose();
        shell = null;
    }

    private void OnLoginClosed(object? sender, FormClosedEventArgs args) => ExitThread();
    private void OnShellClosed(object? sender, FormClosedEventArgs args)
    {
        session.Clear();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            gateway.AuthorizationRevoked -= OnAuthorizationRevoked;
            session.Clear();
            CloseLogin();
            CloseShell();
        }
        base.Dispose(disposing);
    }
}
