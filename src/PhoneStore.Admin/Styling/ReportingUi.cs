using System.Globalization;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Admin.Styling;

public static class ReportingUi
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    public static string Number(long value) => value.ToString("N0", Vietnamese);
    public static string Currency(decimal value) => value.ToString("N0", Vietnamese) + " ₫";

    public static Panel SummaryCard(string title, string name, out Label value, out Label detail)
    {
        var card = AdminTheme.CreateCard(); card.Dock = DockStyle.Fill; card.Margin = new Padding(0, 0, 14, 14);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var caption = AdminTheme.CreateLabel(title.ToUpperInvariant(), 9, true); caption.ForeColor = AdminTheme.Muted; caption.Margin = new Padding(0, 0, 0, 10);
        value = AdminTheme.CreateLabel("—", 20, true); value.Name = name; value.ForeColor = AdminTheme.Primary; value.Margin = new Padding(0, 0, 0, 8);
        detail = AdminTheme.CreateLabel("Đang tải…", 9.5f); detail.ForeColor = AdminTheme.Muted;
        layout.Controls.Add(caption, 0, 0); layout.Controls.Add(value, 0, 1); layout.Controls.Add(detail, 0, 2); card.Controls.Add(layout); return card;
    }

    public static DataGridView CompactGrid() { var grid = ManagementUi.CreateGrid(); grid.RowTemplate.Height = 38; grid.ColumnHeadersHeight = 42; return grid; }
}

public sealed class StatusBarsControl : Control
{
    private IReadOnlyList<OrderStatusMetric> data = [];
    public StatusBarsControl() { DoubleBuffered = true; BackColor = Color.White; MinimumSize = new Size(300, 190); }
    public void SetData(IReadOnlyList<OrderStatusMetric> values) { data = values; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        if (data.Count == 0 || data.All(item => item.Count == 0)) { DrawEmpty(e.Graphics, "Chưa có đơn hàng."); return; }
        var maximum = Math.Max(1, data.Max(item => item.Count)); var rowHeight = Math.Max(28, (Height - 22) / data.Count);
        for (var index = 0; index < data.Count; index++)
        {
            var item = data[index]; var y = 10 + index * rowHeight;
            var label = OrderStatusLabels.Display(item.Status); var barLeft = 120; var available = Math.Max(20, Width - barLeft - 55);
            TextRenderer.DrawText(e.Graphics, label, Font, new Rectangle(8, y, 108, rowHeight - 5), AdminTheme.Navy,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using var background = new SolidBrush(AdminTheme.Surface); e.Graphics.FillRectangle(background, barLeft, y + 7, available, Math.Max(10, rowHeight - 16));
            using var fill = new SolidBrush(ColorFor(item.Status));
            e.Graphics.FillRectangle(fill, barLeft, y + 7, (int)Math.Round(available * item.Count / (double)maximum), Math.Max(10, rowHeight - 16));
            TextRenderer.DrawText(e.Graphics, item.Count.ToString("N0"), Font, new Rectangle(barLeft + available + 7, y, 45, rowHeight - 5),
                AdminTheme.Navy, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }
    }
    private void DrawEmpty(Graphics graphics, string text) => TextRenderer.DrawText(graphics, text, Font, ClientRectangle,
        AdminTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    private static Color ColorFor(OrderStatus status) => status switch
    {
        OrderStatus.Pending => Color.FromArgb(219, 151, 31), OrderStatus.Confirmed => AdminTheme.Primary,
        OrderStatus.Shipping => Color.FromArgb(84, 105, 212), OrderStatus.Completed => Color.FromArgb(40, 139, 91),
        OrderStatus.Cancelled => Color.FromArgb(180, 68, 77), _ => AdminTheme.Muted
    };
}

public sealed class RevenueBarsControl : Control
{
    private IReadOnlyList<RevenuePoint> data = [];
    public RevenueBarsControl() { DoubleBuffered = true; BackColor = Color.White; MinimumSize = new Size(300, 190); }
    public void SetData(IReadOnlyList<RevenuePoint> values) { data = values; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (data.Count == 0 || data.All(item => item.Revenue == 0))
        {
            TextRenderer.DrawText(e.Graphics, "Chưa có doanh thu hoàn thành.", Font, ClientRectangle, AdminTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return;
        }
        var shown = data.TakeLast(Math.Min(14, data.Count)).ToArray(); var max = Math.Max(1m, shown.Max(item => item.Revenue));
        var chart = new Rectangle(42, 12, Math.Max(40, Width - 54), Math.Max(60, Height - 45));
        using var axis = new Pen(AdminTheme.Border); e.Graphics.DrawLine(axis, chart.Left, chart.Bottom, chart.Right, chart.Bottom);
        var slot = chart.Width / (float)shown.Length;
        for (var i = 0; i < shown.Length; i++)
        {
            var barHeight = (int)Math.Round((chart.Height - 10) * shown[i].Revenue / max);
            var rectangle = new RectangleF(chart.Left + i * slot + 4, chart.Bottom - barHeight, Math.Max(4, slot - 8), barHeight);
            using var brush = new SolidBrush(AdminTheme.Primary); e.Graphics.FillRectangle(brush, rectangle);
            if (shown.Length <= 8 || i % 2 == 0)
                TextRenderer.DrawText(e.Graphics, shown[i].Date.ToString("dd/MM"), new Font(Font.FontFamily, 8),
                    new Rectangle((int)(chart.Left + i * slot), chart.Bottom + 3, (int)Math.Ceiling(slot), 20), AdminTheme.Muted,
                    TextFormatFlags.HorizontalCenter);
        }
    }
}
