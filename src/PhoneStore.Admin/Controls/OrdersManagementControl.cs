using PhoneStore.Admin.Dialogs;
using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin.Controls;

public sealed class OrdersManagementControl : UserControl
{
    private readonly CatalogManagementGateway gateway;
    private readonly DataGridView grid = ManagementUi.CreateGrid();
    private readonly TextBox search = new() { Width = 260, MaxLength = 150, PlaceholderText = "Mã đơn, người nhận, SĐT hoặc email" };
    private readonly ComboBox statusFilter = new() { Width = 175, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button detail = ManagementUi.CreateButton("Xem chi tiết", true);
    private readonly Label status = AdminTheme.CreateLabel("");
    private bool busy;

    public OrdersManagementControl(CatalogManagementGateway gateway)
    {
        this.gateway = gateway; Name = nameof(OrdersManagementControl); Padding = new Padding(24);
        BackColor = AdminTheme.Surface; Font = new Font("Segoe UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var title = AdminTheme.CreateLabel("Quản lý đơn hàng", 21, true); title.Margin = new Padding(0, 0, 0, 8);
        var hint = AdminTheme.CreateLabel("Theo dõi đơn hàng, xem giá mua lịch sử và cập nhật đúng quy trình."); hint.ForeColor = AdminTheme.Muted; hint.Margin = new Padding(0, 0, 0, 18);
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 14) };
        var choices = new List<StatusChoice> { new("Tất cả trạng thái", null) };
        choices.AddRange(Enum.GetValues<OrderStatus>().Select(value => new StatusChoice(OrderStatusLabels.Display(value), value)));
        statusFilter.DataSource = choices; statusFilter.DisplayMember = nameof(StatusChoice.Text);
        var find = ManagementUi.CreateButton("Tìm kiếm"); var refresh = ManagementUi.CreateButton("Tải lại");
        toolbar.Controls.AddRange([search, statusFilter, find, refresh, detail]);
        grid.Name = "OrdersGrid";
        ManagementUi.AddColumn(grid, nameof(OrderManagementRow.OrderId), "ID", 60);
        ManagementUi.AddColumn(grid, nameof(OrderManagementRow.OrderCode), "Mã đơn", 210);
        ManagementUi.AddColumn(grid, nameof(OrderManagementRow.ReceiverName), "Người nhận", 170);
        ManagementUi.AddColumn(grid, nameof(OrderManagementRow.ReceiverPhone), "Số điện thoại", 130);
        ManagementUi.AddColumn(grid, nameof(OrderManagementRow.OrderDate), "Ngày đặt (UTC)", 160, "dd/MM/yyyy HH:mm");
        ManagementUi.AddColumn(grid, nameof(OrderManagementRow.TotalAmount), "Tổng tiền", 145, "#,##0.##");
        ManagementUi.AddColumn(grid, nameof(OrderManagementRow.StatusLabel), "Trạng thái", 150);
        status.Dock = DockStyle.Fill; status.ForeColor = AdminTheme.Muted; status.Margin = new Padding(0, 12, 0, 0);
        layout.Controls.Add(title, 0, 0); layout.Controls.Add(hint, 0, 1); layout.Controls.Add(toolbar, 0, 2); layout.Controls.Add(grid, 0, 3); layout.Controls.Add(status, 0, 4); Controls.Add(layout);
        find.Click += async (_, _) => await ReloadAsync(); refresh.Click += async (_, _) => { search.Clear(); statusFilter.SelectedIndex = 0; await ReloadAsync(); };
        detail.Click += async (_, _) => await OpenAsync(); grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await OpenAsync(); };
        grid.SelectionChanged += (_, _) => detail.Enabled = !busy && Selected is not null;
        search.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ReloadAsync(); } };
        Load += async (_, _) => await ReloadAsync(); detail.Enabled = false;
    }

    private sealed record StatusChoice(string Text, OrderStatus? Value);
    private OrderManagementRow? Selected => grid.CurrentRow?.DataBoundItem as OrderManagementRow;
    private async Task ReloadAsync()
    {
        if (busy || IsDisposed) return; SetBusy(true); status.Text = "Đang tải…";
        try
        {
            var rows = await gateway.WithOrdersAsync(service => service.ListAsync(new OrderManagementQuery(search.Text,
                (statusFilter.SelectedItem as StatusChoice)?.Value)));
            if (IsDisposed) return; grid.DataSource = rows.ToList();
            status.Text = rows.Count == 0 ? "Chưa có đơn hàng." : $"{rows.Count} đơn hàng";
        }
        catch (Exception ex) { if (!IsDisposed) status.Text = ManagementUi.ErrorMessage(ex); }
        finally { if (!IsDisposed) SetBusy(false); }
    }
    private async Task OpenAsync()
    {
        if (busy || Selected is not { } row) return;
        using var dialog = new OrderDetailDialog(row.OrderId, gateway);
        dialog.ShowDialog(FindForm()); if (dialog.HasChanges) await ReloadAsync();
    }
    private void SetBusy(bool value) { busy = value; grid.Enabled = search.Enabled = statusFilter.Enabled = !value; detail.Enabled = !value && Selected is not null; UseWaitCursor = value; }
}
