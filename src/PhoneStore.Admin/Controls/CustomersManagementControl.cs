using PhoneStore.Admin.Dialogs;
using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin.Controls;

public sealed class CustomersManagementControl : UserControl
{
    private readonly CatalogManagementGateway gateway;
    private readonly DataGridView grid = ManagementUi.CreateGrid();
    private readonly TextBox search = new() { Width = 270, MaxLength = 150, PlaceholderText = "Họ tên, email hoặc số điện thoại" };
    private readonly ComboBox active = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button detail = ManagementUi.CreateButton("Xem / chỉnh sửa", true);
    private readonly Button toggle = ManagementUi.CreateButton("Ngừng hoạt động");
    private readonly Label status = AdminTheme.CreateLabel("");
    private bool busy;

    public CustomersManagementControl(CatalogManagementGateway gateway)
    {
        this.gateway = gateway; Name = nameof(CustomersManagementControl); Padding = new Padding(24);
        BackColor = AdminTheme.Surface; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = AdminTheme.CreateLabel("Quản lý khách hàng", 21, true); title.Margin = new Padding(0, 0, 0, 8);
        var hint = AdminTheme.CreateLabel("Xem hồ sơ và quản lý trạng thái tài khoản Customer."); hint.ForeColor = AdminTheme.Muted; hint.Margin = new Padding(0, 0, 0, 18);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 14) };
        active.DataSource = new[] { new StatusChoice("Tất cả trạng thái", null), new StatusChoice("Đang hoạt động", true), new StatusChoice("Ngừng hoạt động", false) };
        active.DisplayMember = nameof(StatusChoice.Text);
        var find = ManagementUi.CreateButton("Tìm kiếm"); var refresh = ManagementUi.CreateButton("Tải lại");
        toolbar.Controls.AddRange([search, active, find, refresh, detail, toggle]);
        grid.Name = "CustomersGrid";
        ManagementUi.AddColumn(grid, nameof(CustomerManagementRow.UserId), "ID", 60);
        ManagementUi.AddColumn(grid, nameof(CustomerManagementRow.FullName), "Họ tên", 180);
        ManagementUi.AddColumn(grid, nameof(CustomerManagementRow.Email), "Email", 220);
        ManagementUi.AddColumn(grid, nameof(CustomerManagementRow.Phone), "Số điện thoại", 140);
        ManagementUi.AddColumn(grid, nameof(CustomerManagementRow.Address), "Địa chỉ", 240);
        ManagementUi.AddColumn(grid, nameof(CustomerManagementRow.Status), "Trạng thái", 155);
        ManagementUi.AddColumn(grid, nameof(CustomerManagementRow.CreatedAt), "Ngày tạo (UTC)", 160, "dd/MM/yyyy HH:mm");
        status.Dock = DockStyle.Fill; status.ForeColor = AdminTheme.Muted; status.Margin = new Padding(0, 12, 0, 0);
        layout.Controls.Add(title, 0, 0); layout.Controls.Add(hint, 0, 1); layout.Controls.Add(toolbar, 0, 2);
        layout.Controls.Add(grid, 0, 3); layout.Controls.Add(status, 0, 4); Controls.Add(layout);
        find.Click += async (_, _) => await ReloadAsync(); refresh.Click += async (_, _) => { search.Clear(); active.SelectedIndex = 0; await ReloadAsync(); };
        detail.Click += async (_, _) => await OpenAsync(); toggle.Click += async (_, _) => await ToggleAsync();
        grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await OpenAsync(); };
        grid.SelectionChanged += (_, _) => UpdateActions();
        search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ReloadAsync(); } };
        Load += async (_, _) => await ReloadAsync(); UpdateActions();
    }

    private sealed record StatusChoice(string Text, bool? Value);
    private CustomerManagementRow? Selected => grid.CurrentRow?.DataBoundItem as CustomerManagementRow;
    private async Task ReloadAsync()
    {
        if (busy || IsDisposed) return; SetBusy(true); status.Text = "Đang tải…";
        try
        {
            var rows = await gateway.WithCustomersAsync(service => service.ListAsync(
                new CustomerManagementQuery(search.Text, (active.SelectedItem as StatusChoice)?.Value)));
            if (IsDisposed) return; grid.DataSource = rows.ToList();
            status.Text = rows.Count == 0 ? "Chưa có khách hàng." : $"{rows.Count} khách hàng";
        }
        catch (Exception ex) { if (!IsDisposed) status.Text = ManagementUi.ErrorMessage(ex); }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private async Task OpenAsync()
    {
        if (busy || Selected is not { } row) return;
        var fresh = await gateway.WithCustomersAsync(service => service.GetAsync(row.UserId));
        if (fresh is null) { status.Text = "Khách hàng không còn tồn tại. Vui lòng tải lại."; return; }
        using var dialog = new CustomerEditDialog(fresh, edit => gateway.WithCustomersWriteAsync(service => service.UpdateAsync(row.UserId, edit)));
        if (dialog.ShowDialog(FindForm()) == DialogResult.OK) await ReloadAsync();
    }

    private async Task ToggleAsync()
    {
        if (busy || Selected is not { } row) return;
        var next = !row.IsActive;
        if (MessageBox.Show(FindForm(), next ? "Kích hoạt lại tài khoản khách hàng này?" : "Ngừng hoạt động tài khoản này? Các đơn hàng lịch sử vẫn được giữ nguyên.",
            "Xác nhận trạng thái", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        SetBusy(true);
        try
        {
            var result = await gateway.WithCustomersWriteAsync(service => service.SetActiveAsync(row.UserId, next));
            if (!result.Succeeded) status.Text = result.Error; else { SetBusy(false); await ReloadAsync(); }
        }
        catch (Exception ex) { status.Text = ManagementUi.ErrorMessage(ex); }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private void SetBusy(bool value) { busy = value; grid.Enabled = search.Enabled = active.Enabled = !value; UseWaitCursor = value; UpdateActions(); }
    private void UpdateActions() { var row = Selected; detail.Enabled = toggle.Enabled = !busy && row is not null; toggle.Text = row?.IsActive == false ? "Kích hoạt lại" : "Ngừng hoạt động"; }
}
