using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Admin.Authentication;
using PhoneStore.Admin.Styling;
using PhoneStore.Models.Authentication;

namespace PhoneStore.Admin;

public sealed class AdminAuthenticatedEventArgs(AdminIdentity identity) : EventArgs
{
    public AdminIdentity Identity { get; } = identity;
}

public sealed class LoginForm : Form
{
    private readonly AdminLoginGateway gateway;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TextBox email = new() { Name = "EmailInput", MaxLength = 150, PlaceholderText = "Email quản trị viên" };
    private readonly TextBox password = new() { Name = "PasswordInput", MaxLength = 128, UseSystemPasswordChar = true, PlaceholderText = "Mật khẩu" };
    private readonly CheckBox showPassword = new() { Name = "ShowPassword", Text = "Hiển thị mật khẩu", AutoSize = true };
    private readonly Button submit = new() { Name = "LoginButton", Text = "Đăng nhập", Dock = DockStyle.Fill };
    private readonly Label status = new() { Name = "LoginStatus", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(165, 47, 58), TextAlign = ContentAlignment.MiddleLeft };
    private bool busy;
    private bool lifetimeDisposed;

    public event EventHandler<AdminAuthenticatedEventArgs>? Authenticated;

    public LoginForm(AdminLoginGateway gateway)
    {
        SuspendLayout();
        this.gateway = gateway;
        Text = "Đăng nhập · PhoneStore Admin";
        Name = "LoginForm";
        Font = new Font("Segoe UI", 10F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(920, 660);
        MinimumSize = new Size(820, 620);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.White;

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var brandPanel = new Panel { Dock = DockStyle.Fill, BackColor = AdminTheme.Primary, Padding = new Padding(32), Margin = Padding.Empty };
        var brandLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = AdminTheme.Primary };
        brandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        var logo = AdminTheme.CreateLabel("PhoneStore", 24, true);
        logo.ForeColor = Color.White;
        logo.Dock = DockStyle.Fill;
        brandLayout.Controls.Add(logo, 0, 0);
        var intro = AdminTheme.CreateLabel("Không gian\nquản trị.", 27, true);
        intro.ForeColor = Color.White;
        intro.Dock = DockStyle.Fill;
        intro.TextAlign = ContentAlignment.MiddleLeft;
        brandLayout.Controls.Add(intro, 0, 1);
        var caption = AdminTheme.CreateLabel("Quản lý PhoneStore trong một không gian làm việc thống nhất.", 11);
        caption.ForeColor = Color.FromArgb(221, 234, 255);
        caption.Dock = DockStyle.Fill;
        brandLayout.Controls.Add(caption, 0, 2);
        var identity = AdminTheme.CreateLabel("PHONESTORE ADMIN", 9, true);
        identity.ForeColor = Color.FromArgb(221, 234, 255);
        identity.Dock = DockStyle.Fill;
        brandLayout.Controls.Add(identity, 0, 3);
        brandPanel.Controls.Add(brandLayout);
        layout.Controls.Add(brandPanel, 0, 0);

        var formLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(44, 30, 44, 24), Margin = Padding.Empty,
            ColumnCount = 1, RowCount = 11
        };
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        foreach (var height in new[] { 90, 30, 52, 18, 30, 52, 48, 70, 48, 52 })
            formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        formLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(formLayout, 1, 0);
        var formIntro = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        formIntro.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        formIntro.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var title = AdminTheme.CreateLabel("Chào mừng trở lại.", 22, true);
        title.Dock = DockStyle.Fill;
        formIntro.Controls.Add(title, 0, 0);
        var hint = AdminTheme.CreateLabel("Đăng nhập bằng tài khoản quản trị viên.", 10);
        hint.ForeColor = AdminTheme.Muted;
        hint.Dock = DockStyle.Fill;
        formIntro.Controls.Add(hint, 0, 1);
        formLayout.Controls.Add(formIntro, 0, 0);
        formLayout.Controls.Add(FieldLabel("Email", email), 0, 1);
        formLayout.Controls.Add(InputFrame(email, 0), 0, 2);
        formLayout.Controls.Add(FieldLabel("Mật khẩu", password), 0, 4);
        formLayout.Controls.Add(InputFrame(password, 1), 0, 5);
        showPassword.TabIndex = 2;
        showPassword.ForeColor = AdminTheme.Muted;
        showPassword.Anchor = AnchorStyles.Left;
        showPassword.CheckedChanged += (_, _) => password.UseSystemPasswordChar = !showPassword.Checked;
        formLayout.Controls.Add(showPassword, 0, 6);
        status.Font = new Font("Segoe UI", 10F);
        status.AccessibleName = "Thông báo đăng nhập";
        status.TabStop = false;
        formLayout.Controls.Add(status, 0, 7);
        submit.TabIndex = 3;
        AdminTheme.StyleButton(submit, primary: true);
        submit.Click += async (_, _) => await SubmitAsync();
        formLayout.Controls.Add(submit, 0, 8);
        var exit = new Button { Name = "ExitButton", Text = "Thoát ứng dụng", Dock = DockStyle.Fill, TabIndex = 4, Margin = new Padding(0, 12, 0, 0) };
        AdminTheme.StyleButton(exit);
        exit.Click += (_, _) => Close();
        formLayout.Controls.Add(exit, 0, 9);
        AcceptButton = submit;
        CancelButton = exit;
        Shown += (_, _) => email.Focus();
        FormClosing += (_, _) => lifetime.Cancel();
        ResumeLayout(true);
    }

    private static Label FieldLabel(string text, TextBox input)
    {
        input.AccessibleName = text;
        var label = AdminTheme.CreateLabel(text, 10, true);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        return label;
    }

    private static Panel InputFrame(TextBox input, int tabIndex)
    {
        var frame = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 12, 12, 8), BorderStyle = BorderStyle.FixedSingle, Margin = Padding.Empty, TabIndex = tabIndex };
        input.BorderStyle = BorderStyle.None;
        input.Font = new Font("Segoe UI", 11F);
        input.Dock = DockStyle.Fill;
        input.TabIndex = 0;
        input.ForeColor = AdminTheme.Navy;
        input.Enter += (_, _) => frame.BackColor = input.BackColor = Color.FromArgb(237, 243, 255);
        input.Leave += (_, _) => frame.BackColor = input.BackColor = Color.White;
        frame.Controls.Add(input);
        return frame;
    }

    private async Task SubmitAsync()
    {
        if (busy) return;
        var address = email.Text.Trim();
        if (address.Length == 0) { status.Text = "Vui lòng nhập email."; email.Focus(); return; }
        if (!new EmailAddressAttribute().IsValid(address)) { status.Text = "Vui lòng nhập email hợp lệ."; email.Focus(); return; }
        if (password.Text.Length == 0) { status.Text = "Vui lòng nhập mật khẩu."; password.Focus(); return; }
        SetBusy(true);
        status.Text = "";
        try
        {
            var result = await gateway.LoginAsync(address, password.Text, lifetime.Token);
            if (IsDisposed || lifetime.IsCancellationRequested) return;
            password.Clear();
            if (result is { Status: AdminLoginStatus.Success, Identity: { Role: "Admin" } identity })
            {
                Authenticated?.Invoke(this, new AdminAuthenticatedEventArgs(identity));
                return;
            }
            status.Text = result.Status == AdminLoginStatus.Forbidden
                ? "Tài khoản không có quyền truy cập hệ thống quản trị."
                : "Email hoặc mật khẩu không chính xác.";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            if (!IsDisposed) status.Text = "Không thể kết nối dữ liệu. Vui lòng thử lại sau.";
        }
        catch (Exception)
        {
            if (!IsDisposed) status.Text = "Không thể đăng nhập lúc này. Vui lòng thử lại.";
        }
        finally
        {
            if (!IsDisposed) { SetBusy(false); password.Clear(); password.Focus(); }
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        email.Enabled = password.Enabled = showPassword.Enabled = submit.Enabled = !value;
        submit.Text = value ? "Đang đăng nhập…" : "Đăng nhập";
        UseWaitCursor = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !lifetimeDisposed)
        {
            lifetime.Cancel();
            lifetime.Dispose();
            lifetimeDisposed = true;
        }
        base.Dispose(disposing);
    }
}
