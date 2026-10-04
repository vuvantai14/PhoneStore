using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;

namespace PhoneStore.Admin.Dialogs;

public sealed class OrderDetailDialog : Form
{
    private readonly int orderId;
    private readonly CatalogManagementGateway gateway;
    private readonly Label heading = AdminTheme.CreateLabel("", 19, true);
    private readonly Label summary = AdminTheme.CreateLabel("");
    private readonly Label shipping = AdminTheme.CreateLabel("");
    private readonly Label total = AdminTheme.CreateLabel("", 12, true);
    private readonly Label status = AdminTheme.CreateLabel("");
    private readonly DataGridView items = ManagementUi.CreateGrid();
    private readonly FlowLayoutPanel actions = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
    private bool busy;
    public bool HasChanges { get; private set; }

    public OrderDetailDialog(int orderId, CatalogManagementGateway gateway)
    {
        this.orderId = orderId; this.gateway = gateway;
        Text = "Chi tiết đơn hàng"; Name = nameof(OrderDetailDialog); StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false; MinimizeBox = MaximizeBox = false; ClientSize = new Size(900, 700); MinimumSize = new Size(760, 620);
        Font = new Font("Segoe UI", 10); BackColor = Color.White; AutoScaleMode = AutoScaleMode.Dpi;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, Padding = new Padding(26) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        foreach (var label in new[] { heading, summary, shipping, total, status }) { label.Dock = DockStyle.Fill; label.MaximumSize = new Size(820, 0); }
        summary.Margin = shipping.Margin = new Padding(0, 8, 0, 4); status.ForeColor = AdminTheme.Muted;
        items.Name = "OrderItemsGrid";
        ManagementUi.AddColumn(items, nameof(OrderItemRow.ProductName), "Sản phẩm", 380);
        ManagementUi.AddColumn(items, nameof(OrderItemRow.Quantity), "Số lượng", 110, "N0");
        ManagementUi.AddColumn(items, nameof(OrderItemRow.UnitPrice), "Đơn giá", 150, "#,##0.##");
        ManagementUi.AddColumn(items, nameof(OrderItemRow.LineTotal), "Thành tiền", 160, "#,##0.##");
        items.Columns[nameof(OrderItemRow.ProductName)]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        var close = ManagementUi.CreateButton("Đóng"); close.DialogResult = DialogResult.Cancel;
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        footer.Controls.Add(close); CancelButton = close;
        layout.Controls.Add(heading, 0, 0); layout.Controls.Add(summary, 0, 1); layout.Controls.Add(shipping, 0, 2);
        layout.Controls.Add(AdminTheme.CreateLabel("Sản phẩm", 12, true), 0, 3); layout.Controls.Add(items, 0, 4);
        layout.Controls.Add(total, 0, 5); layout.Controls.Add(actions, 0, 6); layout.Controls.Add(status, 0, 7);
        layout.Controls.Add(footer, 0, 8); Controls.Add(layout);
        Shown += async (_, _) => await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (busy) return; SetBusy(true);
        try
        {
            var detail = await gateway.WithOrdersAsync(service => service.GetAsync(orderId));
            if (detail is null) { status.Text = "Đơn hàng không còn tồn tại."; return; }
            heading.Text = $"Đơn hàng {detail.OrderCode} · {detail.StatusLabel}";
            summary.Text = $"Ngày đặt (UTC): {detail.OrderDate:dd/MM/yyyy HH:mm}\nKhách hàng: {detail.CustomerName} · {detail.CustomerEmail}";
            shipping.Text = $"Người nhận: {detail.ReceiverName} · {detail.ReceiverPhone}\nĐịa chỉ: {detail.ShippingAddress}"
                + (string.IsNullOrWhiteSpace(detail.Note) ? "" : $"\nGhi chú: {detail.Note}");
            items.DataSource = detail.Items;
            total.Text = $"Tổng đơn: {detail.TotalAmount:N0} ₫ · Tổng chi tiết: {detail.DetailTotal:N0} ₫";
            BuildActions(detail);
            status.Text = detail.TotalAmount == detail.DetailTotal ? "Tổng tiền khớp dữ liệu chi tiết." : "Tổng lưu và tổng chi tiết có chênh lệch; dữ liệu lịch sử không bị tự động sửa.";
        }
        catch (Exception ex) { status.Text = ManagementUi.ErrorMessage(ex); }
        finally { SetBusy(false); }
    }

    private void BuildActions(OrderManagementDetail detail)
    {
        actions.Controls.Clear();
        foreach (var target in Next(detail.Status))
        {
            var button = ManagementUi.CreateButton(ActionLabel(target), target != OrderStatus.Cancelled);
            button.Tag = target;
            button.Click += async (_, _) => await ChangeStatusAsync(detail.Status, target);
            actions.Controls.Add(button);
        }
        if (actions.Controls.Count == 0) actions.Controls.Add(AdminTheme.CreateLabel("Đơn hàng đã ở trạng thái kết thúc."));
    }

    private async Task ChangeStatusAsync(OrderStatus expected, OrderStatus target)
    {
        var question = target == OrderStatus.Cancelled
            ? "Hủy đơn hàng và hoàn lại tồn kho? Thao tác này không thể đảo ngược."
            : $"Chuyển đơn hàng sang “{OrderStatusLabels.Display(target)}”?";
        if (MessageBox.Show(this, question, "Xác nhận cập nhật", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        SetBusy(true);
        try
        {
            var result = await gateway.WithOrdersAsync(service => service.UpdateStatusAsync(orderId, expected, target));
            if (!result.Succeeded) { status.Text = result.Error; return; }
            HasChanges = true;
        }
        catch (Exception ex) { status.Text = ManagementUi.ErrorMessage(ex); return; }
        finally { SetBusy(false); }
        await ReloadAsync();
    }

    private static IReadOnlyList<OrderStatus> Next(OrderStatus current) => current switch
    {
        OrderStatus.Pending => [OrderStatus.Confirmed, OrderStatus.Cancelled],
        OrderStatus.Confirmed => [OrderStatus.Shipping, OrderStatus.Cancelled],
        OrderStatus.Shipping => [OrderStatus.Completed], _ => []
    };
    private static string ActionLabel(OrderStatus target) => target switch
    {
        OrderStatus.Confirmed => "Xác nhận", OrderStatus.Shipping => "Chuyển sang giao hàng",
        OrderStatus.Completed => "Hoàn thành", OrderStatus.Cancelled => "Hủy đơn", _ => target.ToString()
    };
    private void SetBusy(bool value) { busy = value; items.Enabled = actions.Enabled = !value; UseWaitCursor = value; }
}
