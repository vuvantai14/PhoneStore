using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Drawing.Drawing2D;
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
    private readonly TextBox email = new() { Name = "EmailInput", MaxLength = 150, PlaceholderText = "admin@admin.com" };
    private readonly TextBox password = new() { Name = "PasswordInput", MaxLength = 128, UseSystemPasswordChar = true, PlaceholderText = "••••••••" };
    private readonly CheckBox showPassword = new() { Name = "ShowPassword", Text = "Hiển thị mật khẩu", AutoSize = true };
    private readonly Button submit = new() { Name = "LoginButton", Text = "Đăng nhập", Dock = DockStyle.Fill };
    private readonly Button exit = new() { Name = "ExitButton", Text = "Thoát ứng dụng", Dock = DockStyle.Fill };
    private readonly Panel alertBox = new() { Name = "LoginAlertBox", Dock = DockStyle.Fill, Visible = false };
    private readonly Label status = new() { Name = "LoginStatus", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private bool busy;
    private bool lifetimeDisposed;

    public event EventHandler<AdminAuthenticatedEventArgs>? Authenticated;

    public LoginForm(AdminLoginGateway gateway)
    {
        SuspendLayout();
        this.gateway = gateway;
        Text = "Đăng nhập quản trị · PhoneStore Admin";
        Name = "LoginForm";
        Font = AdminTheme.CreateFont(9.5F);
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(680, 380);
        MinimumSize = new Size(650, 370);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.White;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(mainLayout);

        // =========================================================================
        // CỘT TRÁI: BRAND PANEL (XANH PHONESTORE)
        // =========================================================================
        var brandPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AdminTheme.Primary,
            Padding = new Padding(24, 24, 20, 20),
            Margin = Padding.Empty
        };

        var brandLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = AdminTheme.Primary,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        brandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brandLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 0: Logo
        brandLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));     // 1: Thông điệp trung tâm (co giãn)
        brandLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 2: Tính năng tóm tắt (AutoSize, không cắt chữ)
        brandLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));        // 3: Footer thương hiệu

        // 0: Logo thương hiệu
        var brandLogo = AdminTheme.CreateLabel("PhoneStore", 18, true);
        brandLogo.ForeColor = Color.White;
        brandLogo.AutoSize = true;
        brandLogo.Margin = new Padding(0, 0, 0, 4);
        brandLayout.Controls.Add(brandLogo, 0, 0);

        // 1: Thông điệp trung tâm (Slogan)
        var centerBox = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        centerBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        centerBox.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        centerBox.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var intro = AdminTheme.CreateLabel("Không gian\nquản trị.", 18, true);
        intro.ForeColor = Color.White;
        intro.AutoSize = true;
        intro.Margin = new Padding(0, 10, 0, 8);
        centerBox.Controls.Add(intro, 0, 0);

        var caption = AdminTheme.CreateLabel("Hệ thống quản lý PhoneStore tập trung, đồng bộ và bảo mật cao.", 9);
        caption.ForeColor = Color.FromArgb(221, 234, 255);
        caption.AutoSize = true;
        caption.MaximumSize = new Size(206, 0); // Cho phép xuống dòng tự nhiên
        caption.Margin = Padding.Empty;
        centerBox.Controls.Add(caption, 0, 1);
        brandLayout.Controls.Add(centerBox, 0, 1);

        // 2: Tính năng tóm tắt (TableLayoutPanel AutoSize với MaximumSize để tự xuống dòng, KHÔNG BAO GIỜ cắt chữ)
        var badgeLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 14),
            Padding = Padding.Empty
        };
        badgeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        badgeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        badgeLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var badge1 = AdminTheme.CreateLabel("●  Đồng bộ dữ liệu PhoneStoreDB", 8.5f);
        badge1.ForeColor = Color.FromArgb(200, 222, 255);
        badge1.AutoSize = true;
        badge1.MaximumSize = new Size(206, 0); // Cho phép xuống dòng nếu màn hình hẹp
        badge1.Margin = new Padding(0, 0, 0, 6);

        var badge2 = AdminTheme.CreateLabel("●  Chính sách một quản trị viên", 8.5f);
        badge2.ForeColor = Color.FromArgb(200, 222, 255);
        badge2.AutoSize = true;
        badge2.MaximumSize = new Size(206, 0);
        badge2.Margin = Padding.Empty;

        badgeLayout.Controls.Add(badge1, 0, 0);
        badgeLayout.Controls.Add(badge2, 0, 1);
        brandLayout.Controls.Add(badgeLayout, 0, 2);

        // 3: Footer thương hiệu
        var identity = AdminTheme.CreateLabel("PHONESTORE DESKTOP ADMIN", 7.5f, true);
        identity.ForeColor = Color.FromArgb(180, 210, 255);
        identity.AutoSize = true;
        identity.Margin = Padding.Empty;
        brandLayout.Controls.Add(identity, 0, 3);

        brandPanel.Controls.Add(brandLayout);
        mainLayout.Controls.Add(brandPanel, 0, 0);

        // =========================================================================
        // CỘT PHẢI: FORM ĐĂNG NHẬP (NỀN TRẮNG COMPACT)
        // Mỗi label có hàng AutoSize riêng biệt để tuyệt đối không chồng chữ hoặc cắt chữ
        // =========================================================================
        var formLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(32, 20, 32, 18),
            Margin = Padding.Empty,
            ColumnCount = 1,
            RowCount = 10
        };
        formLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 0: Tiêu đề "Đăng nhập quản trị"
        formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 1: Phụ đề (cách tiêu đề 8px)
        formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 2: Nhãn "Email quản trị viên" (cách phụ đề 14-16px)
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // 3: Input Email (cao 38px)
        formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 4: Nhãn "Mật khẩu" (cách ô Email 12px)
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // 5: Input Mật khẩu (cao 38px)
        formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 6: Checkbox hiện mật khẩu (cách ô MK 8px)
        formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 7: Alert thông báo lỗi
        formLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // 8: Nút Đăng nhập (cách checkbox 14px)
        formLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 9: Nút Thoát compact (cách nút Login 16-20px)
        mainLayout.Controls.Add(formLayout, 1, 0);

        // 0: Tiêu đề chính (AutoSize = true, không bị gò bó chiều cao cố định, đủ chỗ cho dấu tiếng Việt)
        var title = AdminTheme.CreateLabel("Đăng nhập quản trị", 16, true);
        title.ForeColor = AdminTheme.Navy;
        title.AutoSize = true;
        title.Margin = new Padding(0, 0, 0, 8); // Phụ đề nằm cách tiêu đề khoảng 8px
        formLayout.Controls.Add(title, 0, 0);

        // 1: Phụ đề (Nằm ở hàng riêng bên dưới tiêu đề, cách nhãn Email 14–16px)
        var subtitle = AdminTheme.CreateLabel("Chào mừng trở lại. Nhập thông tin để tiếp tục.", 9F);
        subtitle.ForeColor = AdminTheme.Secondary;
        subtitle.AutoSize = true;
        subtitle.Margin = new Padding(0, 0, 0, 15); // Nhãn Email cách phụ đề 15px (khoảng 14–16px)
        formLayout.Controls.Add(subtitle, 0, 1);

        // 2 & 3: Email (Ô cao 38px)
        var emailLabel = AdminTheme.CreateLabel("Email quản trị viên", 8.5f, true);
        emailLabel.ForeColor = AdminTheme.Navy;
        emailLabel.AutoSize = true;
        emailLabel.Margin = new Padding(0, 0, 0, 4);
        formLayout.Controls.Add(emailLabel, 0, 2);

        var emailFrame = CreateInputFrame(email, 0);
        emailFrame.Margin = Padding.Empty;
        formLayout.Controls.Add(emailFrame, 0, 3);

        // 4 & 5: Password (Ô cao 38px)
        var passwordLabel = AdminTheme.CreateLabel("Mật khẩu", 8.5f, true);
        passwordLabel.ForeColor = AdminTheme.Navy;
        passwordLabel.AutoSize = true;
        passwordLabel.Margin = new Padding(0, 12, 0, 4); // Nhãn Mật khẩu cách ô Email đúng 12px
        formLayout.Controls.Add(passwordLabel, 0, 4);

        var passwordFrame = CreateInputFrame(password, 1);
        passwordFrame.Margin = Padding.Empty;
        formLayout.Controls.Add(passwordFrame, 0, 5);

        // 6: Show Password Checkbox
        showPassword.TabIndex = 2;
        showPassword.Font = AdminTheme.CreateFont(8.5F);
        showPassword.ForeColor = AdminTheme.Secondary;
        showPassword.AutoSize = true;
        showPassword.Margin = new Padding(0, 8, 0, 14); // Checkbox cách ô Mật khẩu 8px, cách nút Đăng nhập 14px
        showPassword.CheckedChanged += (_, _) => password.UseSystemPasswordChar = !showPassword.Checked;
        formLayout.Controls.Add(showPassword, 0, 6);

        // 7: Alert thông báo lỗi (Chỉ chiếm chỗ khi hiển thị)
        alertBox.Padding = new Padding(10, 6, 10, 6);
        alertBox.Margin = new Padding(0, 0, 0, 10);
        alertBox.Height = 32;
        alertBox.Paint += (_, e) =>
        {
            if (!alertBox.Visible) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(AdminTheme.CancelledBg);
            using var borderPen = new Pen(Color.FromArgb(248, 180, 180), 1f);
            using var path = AdminTheme.CreateRoundedRectanglePath(new Rectangle(0, 0, alertBox.Width - 1, alertBox.Height - 1), 6);
            e.Graphics.FillPath(bg, path);
            e.Graphics.DrawPath(borderPen, path);
        };
        status.Font = AdminTheme.CreateFont(8.5F, FontStyle.Bold);
        status.ForeColor = AdminTheme.CancelledText;
        status.AccessibleName = "Thông báo đăng nhập";
        status.TabStop = false;
        alertBox.Controls.Add(status);
        formLayout.Controls.Add(alertBox, 0, 7);

        // 8: Nút Đăng nhập (Cao 40px, cách nút Thoát 16–20px)
        submit.TabIndex = 3;
        AdminTheme.StyleButton(submit, primary: true, minHeight: 40);
        submit.Font = AdminTheme.CreateFont(10F, FontStyle.Bold);
        submit.Margin = Padding.Empty;
        submit.Click += async (_, _) => await SubmitAsync();
        formLayout.Controls.Add(submit, 0, 8);

        // 9: Nút Thoát ứng dụng (Nút phụ compact dạng flat link cao 26px, cách nút Đăng nhập 18px)
        exit.TabIndex = 4;
        exit.Font = AdminTheme.CreateFont(8.5F);
        exit.FlatStyle = FlatStyle.Flat;
        exit.FlatAppearance.BorderSize = 0;
        exit.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 247, 251);
        exit.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 240, 247);
        exit.ForeColor = AdminTheme.Secondary;
        exit.BackColor = Color.Transparent;
        exit.Cursor = Cursors.Hand;
        exit.Dock = DockStyle.Top;
        exit.Height = 26;
        exit.Margin = new Padding(0, 18, 0, 0); // Đặt dưới nút Đăng nhập 18px (khoảng 16–20px)
        exit.Click += (_, _) => Close();
        formLayout.Controls.Add(exit, 0, 9);

        AcceptButton = submit;
        CancelButton = exit;
        Shown += (_, _) => email.Focus();
        email.TextChanged += (_, _) => { if (alertBox.Visible) ClearError(); };
        password.TextChanged += (_, _) => { if (alertBox.Visible) ClearError(); };
        FormClosing += (_, _) => lifetime.Cancel();
        ResumeLayout(true);
    }

    private static Panel CreateInputFrame(TextBox input, int tabIndex)
    {
        var frame = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = Padding.Empty,
            TabIndex = tabIndex,
            Cursor = Cursors.IBeam
        };

        var isFocused = false;
        frame.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(isFocused ? AdminTheme.Primary : AdminTheme.Border, isFocused ? 1.5f : 1f);
            using var path = AdminTheme.CreateRoundedRectanglePath(new Rectangle(0, 0, frame.Width - 1, frame.Height - 1), 6);
            e.Graphics.DrawPath(pen, path);
        };

        input.BorderStyle = BorderStyle.None;
        input.Font = AdminTheme.CreateFont(10F);
        input.Dock = DockStyle.None;
        input.TabIndex = 0;
        input.ForeColor = AdminTheme.Navy;
        input.BackColor = Color.White;
        input.Enter += (_, _) => { isFocused = true; frame.Invalidate(); };
        input.Leave += (_, _) => { isFocused = false; frame.Invalidate(); };

        void LayoutInput()
        {
            const int left = 10;
            var top = Math.Max(0, (frame.ClientSize.Height - input.Height) / 2);
            var width = Math.Max(0, frame.ClientSize.Width - left - 10);
            input.SetBounds(left, top, width, input.Height);
        }

        frame.Resize += (_, _) => LayoutInput();
        frame.Click += (_, _) => input.Focus();
        frame.Controls.Add(input);
        LayoutInput();
        return frame;
    }

    private void ShowError(string message)
    {
        status.Text = message;
        var hasError = !string.IsNullOrWhiteSpace(message);
        alertBox.Visible = hasError;
        if (hasError && ClientSize.Height < 415)
        {
            ClientSize = new Size(ClientSize.Width, 415);
        }
        else if (!hasError && ClientSize.Height > 380)
        {
            ClientSize = new Size(ClientSize.Width, 380);
        }
        alertBox.Invalidate();
    }

    private void ClearError()
    {
        status.Text = "";
        alertBox.Visible = false;
        if (ClientSize.Height > 380)
        {
            ClientSize = new Size(ClientSize.Width, 380);
        }
    }

    private async Task SubmitAsync()
    {
        if (busy) return;
        var address = email.Text.Trim();
        if (address.Length == 0)
        {
            ShowError("Vui lòng nhập email quản trị viên.");
            email.Focus();
            return;
        }
        if (!new EmailAddressAttribute().IsValid(address))
        {
            ShowError("Định dạng email không hợp lệ.");
            email.Focus();
            return;
        }
        if (password.Text.Length == 0)
        {
            ShowError("Vui lòng nhập mật khẩu.");
            password.Focus();
            return;
        }

        SetBusy(true);
        ClearError();

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

            ShowError(result.Status == AdminLoginStatus.Forbidden
                ? "Tài khoản không có quyền truy cập hệ thống quản trị."
                : "Email hoặc mật khẩu không chính xác.");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            if (!IsDisposed) ShowError("Không thể kết nối dữ liệu. Vui lòng kiểm tra SQL Server.");
        }
        catch (Exception)
        {
            if (!IsDisposed) ShowError("Không thể đăng nhập lúc này. Vui lòng thử lại.");
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
                password.Clear();
                password.Focus();
            }
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        email.Enabled = password.Enabled = showPassword.Enabled = submit.Enabled = !value;
        submit.Text = value ? "Đang xác thực…" : "Đăng nhập";
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
