using PhoneStore.Admin.Dialogs;
using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin.Controls;

public sealed class LookupManagementControl : UserControl
{
    private readonly LookupKind kind;
    private readonly CatalogManagementGateway gateway;
    private readonly DataGridView grid = ManagementUi.CreateGrid();
    private readonly TextBox search = new() { Name = "LookupSearchInput", Width = 280, MaxLength = 100 };
    private readonly Button searchButton = ManagementUi.CreateButton("Tìm kiếm");
    private readonly Button addButton = ManagementUi.CreateButton("+ Thêm", primary: true);
    private readonly Button editButton = ManagementUi.CreateButton("Chỉnh sửa");
    private readonly Button activeButton = ManagementUi.CreateButton("Ngừng hoạt động");
    private readonly Button refreshButton = ManagementUi.CreateButton("Tải lại");
    private readonly Label status = AdminTheme.CreateLabel("", 10);
    private bool busy;

    public LookupManagementControl(LookupKind kind, CatalogManagementGateway gateway)
    {
        SuspendLayout();
        this.kind = kind;
        this.gateway = gateway;
        var title = kind == LookupKind.Brand ? "Thương hiệu" : "Danh mục";
        Name = kind + "ManagementControl";
        Font = new Font("Segoe UI", 10);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AdminTheme.Surface;
        Padding = new Padding(28);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
            Margin = Padding.Empty, Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        var heading = AdminTheme.CreateLabel("Quản lý " + title.ToLowerInvariant(), 21, true);
        heading.Dock = DockStyle.Fill;
        heading.Margin = new Padding(0, 0, 0, 10);
        layout.Controls.Add(heading, 0, 0);
        var hint = AdminTheme.CreateLabel("Thêm, chỉnh sửa và quản lý trạng thái " + title.ToLowerInvariant() + ".", 10.5f);
        hint.Dock = DockStyle.Fill;
        hint.ForeColor = AdminTheme.Muted;
        hint.Margin = new Padding(0, 0, 0, 20);
        layout.Controls.Add(hint, 0, 1);

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 0, 0, 16), Padding = Padding.Empty
        };
        var inputFrame = new Panel
        {
            Width = 300, Height = 44, Padding = new Padding(10, 10, 10, 8),
            BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 12, 8)
        };
        search.Dock = DockStyle.Fill;
        search.BorderStyle = BorderStyle.None;
        search.Font = new Font("Segoe UI", 11);
        search.PlaceholderText = "Tìm theo tên " + title.ToLowerInvariant();
        search.AccessibleName = "Tìm " + title.ToLowerInvariant();
        inputFrame.Controls.Add(search);
        toolbar.Controls.Add(inputFrame);
        foreach (var button in new[] { searchButton, refreshButton, addButton })
        {
            button.Margin = new Padding(0, 0, 12, 8);
            toolbar.Controls.Add(button);
        }
        layout.Controls.Add(toolbar, 0, 2);

        grid.Name = "LookupGrid";
        ManagementUi.AddColumn(grid, nameof(LookupRow.Id), "ID", 64);
        ManagementUi.AddColumn(grid, nameof(LookupRow.Name), "Tên " + title.ToLowerInvariant(), 220);
        ManagementUi.AddColumn(grid, nameof(LookupRow.Description), "Mô tả", 270);
        ManagementUi.AddColumn(grid, nameof(LookupRow.Status), "Trạng thái", 155);
        ManagementUi.AddColumn(grid, nameof(LookupRow.CreatedAt), "Ngày tạo (UTC)", 158, "dd/MM/yyyy HH:mm");
        grid.Margin = Padding.Empty;
        layout.Controls.Add(grid, 0, 3);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = true,
            Margin = new Padding(0, 16, 0, 0), Padding = Padding.Empty
        };
        editButton.Margin = new Padding(0, 0, 12, 8);
        activeButton.Margin = new Padding(0, 0, 20, 8);
        status.ForeColor = AdminTheme.Muted;
        status.Margin = new Padding(0, 12, 0, 8);
        status.MaximumSize = new Size(470, 0);
        bottom.Controls.Add(editButton);
        bottom.Controls.Add(activeButton);
        bottom.Controls.Add(status);
        layout.Controls.Add(bottom, 0, 4);

        grid.SelectionChanged += (_, _) => UpdateSelectionActions();
        grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await OpenEditAsync(); };
        searchButton.Click += async (_, _) => await ReloadAsync();
        refreshButton.Click += async (_, _) => await ReloadAsync();
        search.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.Handled = e.SuppressKeyPress = true;
            await ReloadAsync();
        };
        addButton.Click += async (_, _) => await OpenEditAsync(add: true);
        editButton.Click += async (_, _) => await OpenEditAsync();
        activeButton.Click += async (_, _) => await ChangeActiveAsync();
        Load += async (_, _) => await ReloadAsync();
        UpdateSelectionActions();
        ResumeLayout(true);
    }

    private LookupRow? SelectedRow => grid.CurrentRow?.DataBoundItem as LookupRow;

    private async Task ReloadAsync()
    {
        if (busy || IsDisposed) return;
        SetBusy(true);
        status.ForeColor = AdminTheme.Muted;
        status.Text = "Đang tải…";
        try
        {
            var rows = await gateway.WithLookupsAsync(service => service.ListAsync(kind, search.Text));
            if (IsDisposed) return;
            grid.DataSource = rows.ToList();
            status.Text = rows.Count == 0 ? "Chưa có dữ liệu phù hợp." : $"{rows.Count} kết quả";
        }
        catch (Exception exception)
        {
            if (IsDisposed) return;
            grid.DataSource = null;
            status.ForeColor = Color.FromArgb(165, 47, 58);
            status.Text = ManagementUi.ErrorMessage(exception);
        }
        finally
        {
            if (!IsDisposed) SetBusy(false);
        }
    }

    private async Task OpenEditAsync(bool add = false)
    {
        if (busy) return;
        var row = add ? null : SelectedRow;
        if (!add && row is null) return;
        using var dialog = new LookupEditDialog(kind,
            row is null ? null : new LookupEdit(row.Name, row.Description, row.IsActive),
            edit => gateway.WithLookupsAsync(service => service.SaveAsync(kind, row?.Id, edit)));
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            await ReloadAsync();
    }

    private async Task ChangeActiveAsync()
    {
        if (busy || SelectedRow is not { } row) return;
        var active = !row.IsActive;
        var question = active ? $"Kích hoạt lại \"{row.Name}\"?"
            : $"Ngừng hoạt động \"{row.Name}\"?\nCác sản phẩm liên quan và dữ liệu lịch sử vẫn được giữ nguyên.";
        if (MessageBox.Show(FindForm(), question, "Xác nhận trạng thái", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;
        SetBusy(true);
        try
        {
            var result = await gateway.WithLookupsAsync(service => service.SetActiveAsync(kind, row.Id, active));
            if (IsDisposed) return;
            if (!result.Succeeded)
            {
                status.ForeColor = Color.FromArgb(165, 47, 58);
                status.Text = result.Error ?? "Không thể cập nhật trạng thái.";
                return;
            }
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                status.ForeColor = Color.FromArgb(165, 47, 58);
                status.Text = ManagementUi.ErrorMessage(exception);
            }
            return;
        }
        finally
        {
            if (!IsDisposed) SetBusy(false);
        }
        if (!IsDisposed) await ReloadAsync();
    }

    private void UpdateSelectionActions()
    {
        var row = SelectedRow;
        editButton.Enabled = activeButton.Enabled = !busy && row is not null;
        activeButton.Text = row?.IsActive == false ? "Kích hoạt lại" : "Ngừng hoạt động";
    }

    private void SetBusy(bool value)
    {
        busy = value;
        grid.Enabled = search.Enabled = searchButton.Enabled = refreshButton.Enabled = addButton.Enabled = !value;
        UseWaitCursor = value;
        UpdateSelectionActions();
    }
}
