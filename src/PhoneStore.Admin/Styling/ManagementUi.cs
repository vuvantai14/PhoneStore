namespace PhoneStore.Admin.Styling;

public static class ManagementUi
{
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

    public static string ErrorMessage(Exception exception) => exception is UnauthorizedAccessException
        ? "Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại."
        : "Không thể thực hiện thao tác. Vui lòng kiểm tra kết nối và thử lại.";
}
