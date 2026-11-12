using System.Drawing.Drawing2D;
using PhoneStore.Admin.Authentication;
using PhoneStore.Admin.Controls;
using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin;

public sealed class MainForm : Form
{
    private readonly AdminSession _session;
    private readonly Panel _content;
    private readonly Label _pageTitle;
    private readonly Label _pageSubtitle;
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
        Text = "PhoneStore Admin · Hệ thống quản lý";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = AdminTheme.CreateFont(10);
        BackColor = AdminTheme.Surface;
        MinimumSize = new Size(1000, 680);
        ClientSize = new Size(1200, 760);

        // 2 cột chính: Sidebar (228px) và Workspace (Phần còn lại)
        var shell = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 228));
        shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var sidebar = BuildSidebar();

        // Workspace gồm Header (74px) và ContentPanel (Phần còn lại)
        var workspace = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        workspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        workspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // --- HEADER COMPACT (74px) ---
        var header = new BufferedTableLayoutPanel
        {
            Name = "HeaderPanel",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(28, 6, 28, 7),
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Paint += (_, e) =>
        {
            using var pen = new Pen(AdminTheme.Border);
            e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        // Left Header: Tiêu đề trang + phụ đề (căn giữa dọc theo metric thực, tránh double-DPI scaling)
        var titleGroup = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.White
        };
        titleGroup.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleGroup.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        titleGroup.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleGroup.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleGroup.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _pageTitle = AdminTheme.CreateLabel("Tổng quan", 16f, true);
        _pageTitle.Name = "PageTitle";
        _pageTitle.ForeColor = AdminTheme.Navy;
        _pageTitle.AutoSize = true;
        _pageTitle.Anchor = AnchorStyles.Left;
        _pageTitle.Margin = new Padding(0, 0, 0, 4);

        _pageSubtitle = AdminTheme.CreateLabel("PhoneStore Admin · Hệ thống quản lý tập trung", 8.5f);
        _pageSubtitle.Name = "PageSubtitle";
        _pageSubtitle.ForeColor = AdminTheme.Secondary;
        _pageSubtitle.AutoSize = true;
        _pageSubtitle.Anchor = AnchorStyles.Left;
        _pageSubtitle.Margin = Padding.Empty;

        titleGroup.Controls.Add(_pageTitle, 0, 1);
        titleGroup.Controls.Add(_pageSubtitle, 0, 2);
        header.Controls.Add(titleGroup, 0, 0);

        // Right Header: Avatar badge + Admin Name + Email
        var account = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        account.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        account.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        account.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var accountInfo = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = new Padding(0, 0, 10, 0)
        };
        accountInfo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        accountInfo.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        accountInfo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        accountInfo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        accountInfo.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var displayName = string.IsNullOrWhiteSpace(identity.FullName) ? identity.Email : identity.FullName;
        var name = AdminTheme.CreateLabel(displayName, 9.5f, true);
        name.Name = "AdminIdentityLabel";
        name.ForeColor = AdminTheme.Navy;
        name.AutoSize = true;
        name.Anchor = AnchorStyles.Right;
        name.Margin = new Padding(0, 0, 0, 2);

        var role = AdminTheme.CreateLabel($"Quản trị viên · {identity.Email}", 8.5f);
        role.Name = "AdminRoleLabel";
        role.AutoSize = true;
        role.Anchor = AnchorStyles.Right;
        role.ForeColor = AdminTheme.Secondary;
        role.Margin = Padding.Empty;

        accountInfo.Controls.Add(name, 0, 1);
        accountInfo.Controls.Add(role, 0, 2);

        // Avatar circle
        var avatarPanel = new Panel
        {
            Width = 36,
            Height = 36,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Right
        };
        avatarPanel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(AdminTheme.PrimaryLight);
            e.Graphics.FillEllipse(brush, 0, 0, 35, 35);
            using var font = AdminTheme.CreateFont(10, FontStyle.Bold);
            TextRenderer.DrawText(e.Graphics, "AD", font, new Rectangle(0, 0, 36, 36), AdminTheme.Primary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        account.Controls.Add(accountInfo, 0, 0);
        account.Controls.Add(avatarPanel, 1, 0);
        header.Controls.Add(account, 1, 0);

        // --- CONTENT PANEL ---
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

        ShowControl(new DashboardControl(_catalog), "Tổng quan");
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
        _pageSubtitle.Text = $"PhoneStore Admin · Quản lý {title.ToLowerInvariant()}";

        foreach (var entry in _navigationButtons)
        {
            var active = entry.Key == title;
            entry.Value.BackColor = active ? AdminTheme.PrimaryLight : Color.White;
            entry.Value.ForeColor = active ? AdminTheme.Primary : AdminTheme.Navy;
            entry.Value.AccessibleDescription = active ? "Trang hiện tại" : "Mở trang " + entry.Key;
            entry.Value.Invalidate();
        }
        _content.ResumeLayout(true);
    }

    private TableLayoutPanel BuildSidebar()
    {
        var sidebar = new BufferedTableLayoutPanel
        {
            Name = "SidebarPanel",
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0, 0, 1, 12),
            Margin = Padding.Empty
        };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 74)); // Khớp chính xác chiều cao Header
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        sidebar.Paint += (_, e) =>
        {
            using var pen = new Pen(AdminTheme.Border);
            // Đường phân cách phải duy nhất
            e.Graphics.DrawLine(pen, sidebar.Width - 1, 0, sidebar.Width - 1, sidebar.Height);
        };

        // Brand Logo Area
        var brand = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = new Padding(22, 6, 12, 7)
        };
        brand.Paint += (_, e) =>
        {
            using var pen = new Pen(AdminTheme.Border);
            // Đường phân cách ngang duy nhất dưới brand khớp với Header
            e.Graphics.DrawLine(pen, 0, brand.Height - 1, brand.Width, brand.Height - 1);
        };

        brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        brand.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brand.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        brand.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var wordmark = AdminTheme.CreateLabel("PhoneStore", 15.5f, true);
        wordmark.Name = "BrandWordmark";
        wordmark.ForeColor = AdminTheme.Primary;
        wordmark.AutoSize = true;
        wordmark.Anchor = AnchorStyles.Left;
        wordmark.Margin = new Padding(0, 0, 0, 2);

        var subtitle = AdminTheme.CreateLabel("HỆ THỐNG QUẢN TRỊ", 8F, true);
        subtitle.Name = "BrandSubtitle";
        subtitle.ForeColor = AdminTheme.Secondary;
        subtitle.AutoSize = true;
        subtitle.Anchor = AnchorStyles.Left;
        subtitle.Margin = Padding.Empty;

        brand.Controls.Add(wordmark, 0, 1);
        brand.Controls.Add(subtitle, 0, 2);

        // Navigation Items Area
        var navigation = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 1,
            RowCount = 8,
            Margin = new Padding(12, 12, 11, 0),
            Padding = Padding.Empty
        };
        navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var items = new (string Title, string Icon)[]
        {
            ("Tổng quan", "dashboard"),
            ("Sản phẩm", "products"),
            ("Thương hiệu", "brands"),
            ("Danh mục", "categories"),
            ("Khách hàng", "customers"),
            ("Đơn hàng", "orders"),
            ("Báo cáo", "reports")
        };

        for (var i = 0; i < items.Length; i++)
        {
            var (title, icon) = items[i];
            navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            var button = CreateNavButton(i, title, icon);
            _navigationButtons.Add(title, button);
            navigation.Controls.Add(button, 0, i);
        }
        navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Nút Đăng xuất
        var logout = new Button
        {
            Name = "LogoutButton",
            Text = string.Empty, // Bỏ Text để tránh chồng chữ
            AccessibleName = "Đăng xuất",
            Dock = DockStyle.Fill,
            Margin = new Padding(12, 8, 11, 0),
            TabIndex = 7,
            Font = AdminTheme.CreateFont(9.5f),
            FlatStyle = FlatStyle.Flat,
            ForeColor = AdminTheme.Secondary,
            BackColor = Color.White,
            Cursor = Cursors.Hand
        };
        logout.FlatAppearance.BorderSize = 0;
        logout.FlatAppearance.MouseOverBackColor = Color.FromArgb(254, 242, 242);
        logout.FlatAppearance.MouseDownBackColor = Color.FromArgb(254, 226, 226);
        logout.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var sepPen = new Pen(AdminTheme.Border);
            e.Graphics.DrawLine(sepPen, 0, 0, logout.Width, 0);

            // Vẽ icon logout
            var isHover = logout.ClientRectangle.Contains(logout.PointToClient(MousePosition));
            var color = isHover ? AdminTheme.CancelledText : AdminTheme.Secondary;
            AdminTheme.DrawIcon(e.Graphics, "logout", new Rectangle(14, (logout.Height - 16) / 2, 16, 16), color);

            // Vẽ text Đăng xuất duy nhất 1 lần
            using var font = AdminTheme.CreateFont(9.5f);
            var textRect = new Rectangle(40, 0, logout.Width - 44, logout.Height);
            TextRenderer.DrawText(e.Graphics, "Đăng xuất", font, textRect, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        };
        logout.Click += (_, _) => LogoutRequested?.Invoke(this, EventArgs.Empty);

        sidebar.Controls.Add(brand, 0, 0);
        sidebar.Controls.Add(navigation, 0, 1);
        sidebar.Controls.Add(logout, 0, 2);
        return sidebar;
    }

    private Button CreateNavButton(int tabIndex, string title, string icon)
    {
        var button = new Button
        {
            Name = $"NavigationButton{tabIndex}",
            Text = string.Empty, // Bỏ Text để tránh Button vẽ đè lên TextRenderer.DrawText trong Paint event
            AccessibleName = title,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 4),
            TabIndex = tabIndex,
            Font = AdminTheme.CreateFont(9.5f),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = AdminTheme.Navy,
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(245, 248, 255);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(235, 242, 254);
        button.Padding = Padding.Empty;
        button.Click += (_, _) => Navigate(title);

        button.Paint += (_, e) =>
        {
            var active = _activeTitle == title;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Nền bo góc nhẹ cho nút active
            if (active)
            {
                using var bg = new SolidBrush(AdminTheme.PrimaryLight);
                using var path = AdminTheme.CreateRoundedRectanglePath(new Rectangle(2, 2, button.Width - 4, button.Height - 4), 6);
                e.Graphics.FillPath(bg, path);

                // Thanh active indicator ở mép trái
                using var indBrush = new SolidBrush(AdminTheme.Primary);
                using var indPath = AdminTheme.CreateRoundedRectanglePath(new Rectangle(2, 8, 3, button.Height - 16), 1);
                e.Graphics.FillPath(indBrush, indPath);
            }

            // Vẽ vector icon tương ứng
            var iconColor = active ? AdminTheme.Primary : AdminTheme.Secondary;
            AdminTheme.DrawIcon(e.Graphics, icon, new Rectangle(14, (button.Height - 16) / 2, 16, 16), iconColor);

            // Vẽ nhãn DUY NHẤT một lần bằng Be Vietnam Pro
            using var font = AdminTheme.CreateFont(9.5f, active ? FontStyle.Bold : FontStyle.Regular);
            var textRect = new Rectangle(40, 0, button.Width - 44, button.Height);
            TextRenderer.DrawText(e.Graphics, title, font, textRect, active ? AdminTheme.Primary : AdminTheme.Navy,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        };

        return button;
    }

    private void Navigate(string title)
    {
        UserControl control = title switch
        {
            "Tổng quan" => new DashboardControl(_catalog),
            "Thương hiệu" => new LookupManagementControl(LookupKind.Brand, _catalog),
            "Danh mục" => new LookupManagementControl(LookupKind.Category, _catalog),
            "Sản phẩm" => new ProductsManagementControl(_catalog),
            "Khách hàng" => new CustomersManagementControl(_catalog),
            "Đơn hàng" => new OrdersManagementControl(_catalog),
            "Báo cáo" => new ReportsControl(_catalog),
            _ => new ModulePlaceholderControl(title)
        };
        ShowControl(control, title);
    }

    private sealed class BufferedTableLayoutPanel : TableLayoutPanel
    {
        public BufferedTableLayoutPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
