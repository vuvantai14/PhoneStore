using PhoneStore.Admin.Authentication;
using PhoneStore.Admin.Controls;
using PhoneStore.Admin.Styling;
using PhoneStore.Admin.Management;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin;

public sealed class MainForm : Form
{
    private readonly AdminSession _session;
    private readonly Panel _content;
    private readonly Label _pageTitle;
    private readonly Dictionary<string, Button> _navigationButtons = new();
    private string _activeTitle = string.Empty;
    private readonly CatalogManagementGateway _catalog;

    public event EventHandler? LogoutRequested;

    public MainForm(AdminSession session, CatalogManagementGateway catalog)
    {
        ArgumentNullException.ThrowIfNull(session);
        var identity = session.Current
            ?? throw new InvalidOperationException("Cần đăng nhập quản trị viên trước khi mở giao diện quản trị.");
        if (identity.Role != "Admin")
        {
            throw new InvalidOperationException("Tài khoản không có quyền truy cập hệ thống quản trị.");
        }
        SuspendLayout();
        _session = session;
        _catalog = catalog;
        Name = nameof(MainForm);
        Text = "PhoneStore Admin";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        BackColor = AdminTheme.Surface;
        MinimumSize = new Size(1000, 680);
        ClientSize = new Size(1200, 760);

        var shell = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 244));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebar = BuildSidebar();
        var workspace = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel
        {
            Name = "HeaderPanel",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(32, 20, 32, 20),
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(AdminTheme.Border);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };
        _pageTitle = AdminTheme.CreateLabel("Tổng quan", 21, true);
        _pageTitle.Name = "PageTitle";
        _pageTitle.AutoSize = false;
        _pageTitle.AutoEllipsis = true;
        _pageTitle.Dock = DockStyle.Fill;
        _pageTitle.TextAlign = ContentAlignment.MiddleLeft;

        var account = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        account.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        account.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        account.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var displayName = string.IsNullOrWhiteSpace(identity.FullName) ? identity.Email : identity.FullName;
        var name = AdminTheme.CreateLabel($"Xin chào, {displayName}", 10.5f, true);
        name.Name = "AdminIdentityLabel";
        name.AutoSize = false;
        name.AutoEllipsis = true;
        name.Dock = DockStyle.Fill;
        name.TextAlign = ContentAlignment.MiddleRight;
        var role = AdminTheme.CreateLabel($"Quản trị viên · {identity.Email}", 9);
        role.Name = "AdminRoleLabel";
        role.AutoSize = false;
        role.AutoEllipsis = true;
        role.Dock = DockStyle.Fill;
        role.ForeColor = AdminTheme.Muted;
        role.TextAlign = ContentAlignment.MiddleRight;
        account.Controls.Add(name, 0, 0);
        account.Controls.Add(role, 0, 1);
        header.Controls.Add(_pageTitle, 0, 0);
        header.Controls.Add(account, 1, 0);

        _content = new Panel
        {
            Name = "ContentPanel",
            Dock = DockStyle.Fill,
            BackColor = AdminTheme.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        workspace.Controls.Add(header, 0, 0);
        workspace.Controls.Add(_content, 0, 1);
        shell.Controls.Add(sidebar, 0, 0);
        shell.Controls.Add(workspace, 1, 0);
        Controls.Add(shell);
        ShowControl(new DashboardControl(), "Tổng quan");
        ResumeLayout(true);
    }

    public void ShowControl(UserControl control, string title)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (_session.Current is null || _session.Current.Role != "Admin")
        {
            control.Dispose();
            throw new InvalidOperationException("Phiên đăng nhập quản trị đã kết thúc.");
        }

        _content.SuspendLayout();
        foreach (var previous in _content.Controls.Cast<Control>().ToArray())
        {
            previous.Dispose();
        }
        _content.Controls.Clear();
        control.Dock = DockStyle.Fill;
        control.Margin = Padding.Empty;
        _content.Controls.Add(control);
        _pageTitle.Text = title;
        _activeTitle = title;
        foreach (var entry in _navigationButtons)
        {
            var active = entry.Key == title;
            entry.Value.BackColor = active ? Color.FromArgb(234, 241, 255) : Color.White;
            entry.Value.ForeColor = active ? AdminTheme.Primary : AdminTheme.Navy;
            entry.Value.AccessibleDescription = active ? "Trang hiện tại" : "Mở trang " + entry.Key;
            entry.Value.Invalidate();
        }
        _content.ResumeLayout(true);
    }

    private TableLayoutPanel BuildSidebar()
    {
        var sidebar = new TableLayoutPanel
        {
            Name = "SidebarPanel",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16, 20, 16, 16),
            Margin = Padding.Empty
        };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 104));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(AdminTheme.Border);
            e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        var brand = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(10, 7, 0, 0)
        };
        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        brand.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var wordmark = AdminTheme.CreateLabel("PhoneStore", 20, true);
        wordmark.ForeColor = AdminTheme.Primary;
        wordmark.AutoSize = false;
        wordmark.Dock = DockStyle.Fill;
        var subtitle = AdminTheme.CreateLabel("QUẢN TRỊ HỆ THỐNG", 8.5f, true);
        subtitle.ForeColor = AdminTheme.Muted;
        subtitle.AutoSize = false;
        subtitle.Dock = DockStyle.Fill;
        brand.Controls.Add(wordmark, 0, 0);
        brand.Controls.Add(subtitle, 0, 1);

        var navigation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var titles = new[] { "Tổng quan", "Sản phẩm", "Thương hiệu", "Danh mục", "Khách hàng", "Đơn hàng", "Báo cáo" };
        for (var i = 0; i < titles.Length; i++)
        {
            var title = titles[i];
            navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            var button = new Button
            {
                Name = $"NavigationButton{i}",
                Text = title,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 6),
                TabIndex = i
            };
            AdminTheme.StyleButton(button);
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(240, 245, 255);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(225, 235, 255);
            button.Padding = new Padding(16, 6, 12, 6);
            button.Click += (_, _) => Navigate(title);
            button.Paint += (_, e) =>
            {
                if (_activeTitle == title)
                {
                    using var brush = new SolidBrush(AdminTheme.Primary);
                    e.Graphics.FillRectangle(brush, 0, 10, 3, Math.Max(1, button.Height - 20));
                }
            };
            _navigationButtons.Add(title, button);
            navigation.Controls.Add(button, 0, i);
        }
        navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var logout = new Button
        {
            Name = "LogoutButton",
            Text = "Đăng xuất",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 16, 0, 0),
            TabIndex = 7
        };
        AdminTheme.StyleButton(logout);
        logout.Padding = new Padding(16, 6, 12, 6);
        logout.Click += (_, _) => LogoutRequested?.Invoke(this, EventArgs.Empty);
        sidebar.Controls.Add(brand, 0, 0);
        sidebar.Controls.Add(navigation, 0, 1);
        sidebar.Controls.Add(logout, 0, 2);
        return sidebar;
    }

    private void Navigate(string title)
    {
        UserControl control = title switch
        {
            "Tổng quan" => new DashboardControl(),
            "Thương hiệu" => new LookupManagementControl(LookupKind.Brand, _catalog),
            "Danh mục" => new LookupManagementControl(LookupKind.Category, _catalog),
            "Sản phẩm" => new ProductsManagementControl(_catalog),
            _ => new ModulePlaceholderControl(title)
        };
        ShowControl(control, title);
    }
}
