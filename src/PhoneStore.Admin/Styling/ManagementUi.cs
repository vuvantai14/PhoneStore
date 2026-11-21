namespace PhoneStore.Admin.Styling;

public static class ManagementUi
{
    public const int FilterControlHeight = 40;
    public const int FilterControlGap = 10;

    public static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            Font = new Font("Segoe UI", 10F), ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 46, GridColor = AdminTheme.Border,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None
        };
        grid.RowTemplate.Height = 44;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = AdminTheme.Surface, ForeColor = AdminTheme.Navy,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold), Padding = new Padding(10, 4, 10, 4),
            SelectionBackColor = AdminTheme.Surface, SelectionForeColor = AdminTheme.Navy
        };
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White, ForeColor = AdminTheme.Navy, Padding = new Padding(10, 4, 10, 4),
            SelectionBackColor = Color.FromArgb(234, 241, 255), SelectionForeColor = AdminTheme.Navy
        };
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 251, 253);
        grid.DataError += (_, args) => { args.ThrowException = false; };
        return grid;
    }

    public static void AddColumn(DataGridView grid, string property, string header, int width, string? format = null)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = property, DataPropertyName = property, HeaderText = header, Width = width,
            MinimumWidth = Math.Min(width, 70), SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle { Format = format ?? "" }
        });
    }

    public static Button CreateButton(string text, bool primary = false)
    {
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 10, 0) };
        AdminTheme.StyleButton(button, primary);
        return button;
    }

    public static Panel CreateFilterControlHost(Control control, int width)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.Font = AdminTheme.CreateFont(9.5f);
        control.Margin = Padding.Empty;
        control.BackColor = Color.White;

        if (control is TextBox textBox)
        {
            textBox.AutoSize = true;
            textBox.BorderStyle = BorderStyle.None;
        }
        else if (control is ComboBox comboBox)
        {
            // Keep the native selector behavior. The host provides the visual height;
            // the native control is centered because DropDownList ignores Height.
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.IntegralHeight = false;
        }

        var host = new Panel
        {
            Width = width,
            Height = FilterControlHeight,
            MinimumSize = new Size(width, FilterControlHeight),
            MaximumSize = new Size(width, FilterControlHeight),
            BackColor = Color.White,
            Margin = new Padding(0, 0, FilterControlGap, 8),
            AccessibleRole = AccessibleRole.Grouping
        };
        host.Controls.Add(control);
        host.Layout += (_, _) => CenterFilterControl(host, control);
        host.Paint += (_, e) =>
        {
            var borderColor = control.Focused ? AdminTheme.Primary : AdminTheme.Border;
            using var pen = new Pen(borderColor, control.Focused ? 2f : 1f);
            var inset = control.Focused ? 1 : 0;
            e.Graphics.DrawRectangle(pen, inset, inset,
                Math.Max(0, host.ClientSize.Width - 1 - (inset * 2)),
                Math.Max(0, host.ClientSize.Height - 1 - (inset * 2)));
        };
        control.Enter += (_, _) => host.Invalidate();
        control.Leave += (_, _) => host.Invalidate();
        return host;
    }

    public static FlowLayoutPanel CreateFilterToolbar() => new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Margin = new Padding(0, 0, 0, 14),
        Padding = Padding.Empty
    };

    public static FlowLayoutPanel CreateToolbarActionGroup(params Control[] controls)
    {
        var group = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        foreach (var control in controls)
        {
            control.Margin = new Padding(0, 0, FilterControlGap, 8);
            group.Controls.Add(control);
        }
        return group;
    }

    private static void CenterFilterControl(Panel host, Control control)
    {
        const int horizontalInset = 10;
        control.Width = Math.Max(0, host.ClientSize.Width - (horizontalInset * 2));
        control.Location = new Point(horizontalInset,
            Math.Max(0, (host.ClientSize.Height - control.Height) / 2));
    }

    public static string ErrorMessage(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.",
        PhoneStore.Business.AdminApi.AdminApiException apiEx => apiEx.Message,
        _ => "Không thể thực hiện thao tác. Vui lòng kiểm tra kết nối và thử lại."
    };
}
