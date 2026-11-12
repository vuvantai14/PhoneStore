using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Admin.Styling;

public sealed class RoundedCardPanel : Panel
{
    public int CornerRadius { get; set; } = 12;
    public Color CardFillColor { get; set; } = Color.White;
    public Color BorderColor { get; set; } = AdminTheme.Border;

    public RoundedCardPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        UpdateStyles();
        BackColor = Color.White;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var bg = Parent?.BackColor ?? AdminTheme.Surface;
        using var clearBrush = new SolidBrush(bg);
        e.Graphics.FillRectangle(clearBrush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 1 || Height <= 1) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = AdminTheme.CreateRoundedRectanglePath(rect, CornerRadius);
        using var fillBrush = new SolidBrush(CardFillColor);
        using var pen = new Pen(BorderColor, 1f);
        e.Graphics.FillPath(fillBrush, path);
        e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class BufferedPanel : Panel
{
    public BufferedPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        UpdateStyles();
    }
}

internal sealed class FittingLabel : Label
{
    private readonly float maxPointSize;
    private readonly float minPointSize;

    public FittingLabel(float maxPointSize, float minPointSize)
    {
        this.maxPointSize = maxPointSize;
        this.minPointSize = minPointSize;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        UpdateStyles();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (string.IsNullOrEmpty(Text) || ClientSize.Width <= 8 || ClientSize.Height <= 4) return;

        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var availW = Math.Max(8, ClientSize.Width - Padding.Horizontal - 2);
        var style = Font.Style;
        var chosenSize = minPointSize;

        for (var size = maxPointSize; size >= minPointSize; size -= 0.5f)
        {
            using var probe = AdminTheme.CreateFont(size, style);
            var measured = TextRenderer.MeasureText(e.Graphics, Text, probe, Size.Empty, TextFormatFlags.NoPadding);
            if (measured.Width <= availW)
            {
                chosenSize = size;
                break;
            }
        }

        using var drawFont = AdminTheme.CreateFont(chosenSize, style);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            drawFont,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }
}

public static class ReportingUi
{
    internal static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly PropertyInfo? DoubleBufferedProp =
        typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

    public static string Number(long value) => value.ToString("N0", Vietnamese);
    public static string Currency(decimal value) => value.ToString("N0", Vietnamese) + " ₫";

    public static void EnableDoubleBuffering(Control control)
    {
        DoubleBufferedProp?.SetValue(control, true);
    }

    public static Color GetStatusColor(OrderStatus status) => status switch
    {
        OrderStatus.Pending => Color.FromArgb(245, 158, 11),   // Cam #F59E0B
        OrderStatus.Confirmed => Color.FromArgb(37, 99, 235),  // Xanh dương #2563EB
        OrderStatus.Shipping => Color.FromArgb(56, 189, 248),  // Xanh da trời #38BDF8
        OrderStatus.Completed => Color.FromArgb(34, 197, 94),  // Xanh lá #22C55E
        OrderStatus.Cancelled => Color.FromArgb(239, 68, 68),  // Đỏ #EF4444
        _ => AdminTheme.Muted
    };

    public static Panel SummaryCard(string title, string name, out Label value, out Label detail)
    {
        var (icon, bg, fg) = title.ToLowerInvariant() switch
        {
            var t when t.Contains("khách") => ("customers", Color.FromArgb(241, 245, 249), Color.FromArgb(71, 85, 105)),
            var t when t.Contains("đơn") => ("orders", Color.FromArgb(254, 243, 199), Color.FromArgb(180, 83, 9)),
            var t when t.Contains("doanh") || t.Contains("giá trị") => ("revenue", Color.FromArgb(220, 252, 231), Color.FromArgb(21, 128, 61)),
            _ => ("products", AdminTheme.PrimaryLight, AdminTheme.Primary)
        };
        return SummaryCard(title, icon, bg, fg, name, out value, out detail);
    }

    public static Panel SummaryCard(string title, string iconName, Color iconBg, Color iconColor, string name, out Label value, out Label detail)
    {
        var card = new RoundedCardPanel
        {
            Dock = DockStyle.Fill,
            Height = 118,
            Margin = new Padding(0, 0, 12, 12),
            Padding = new Padding(15, 12, 15, 12)
        };

        var caption = AdminTheme.CreateLabel(title.ToUpperInvariant(), 8f, true);
        caption.ForeColor = AdminTheme.Secondary;
        caption.AutoSize = false;
        caption.AutoEllipsis = true;
        caption.Dock = DockStyle.Fill;
        caption.TextAlign = ContentAlignment.MiddleLeft;
        caption.Margin = new Padding(0, 0, 4, 0);

        var valueLabel = new FittingLabel(17f, 10.5f)
        {
            Name = name,
            Text = "—",
            Font = AdminTheme.CreateFont(17f, FontStyle.Bold),
            ForeColor = AdminTheme.Navy,
            BackColor = Color.White,
            AutoSize = false,
            Dock = DockStyle.Fill,
            Height = Math.Max(30, AdminTheme.CreateFont(17f, FontStyle.Bold).Height + 2),
            Margin = new Padding(0, 2, 0, 2),
            UseMnemonic = false
        };
        value = valueLabel;

        detail = AdminTheme.CreateLabel("Đang tải…", 8f);
        detail.ForeColor = AdminTheme.Secondary;
        detail.AutoSize = false;
        detail.AutoEllipsis = true;
        detail.Dock = DockStyle.Fill;
        detail.TextAlign = ContentAlignment.MiddleLeft;
        detail.Margin = Padding.Empty;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            RowCount = 3,
            ColumnCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, valueLabel.Height + 4));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var iconBox = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = Padding.Empty
        };
        iconBox.Paint += (_, e) =>
        {
            e.Graphics.Clear(Color.White);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var badgeSize = Math.Max(22, Math.Min(28, Math.Min(iconBox.Width, iconBox.Height) - 2));
            var bx = Math.Max(0, iconBox.Width - badgeSize);
            var by = Math.Max(0, (iconBox.Height - badgeSize) / 2);
            using var bgBrush = new SolidBrush(iconBg);
            using var bgPath = AdminTheme.CreateRoundedRectanglePath(new Rectangle(bx, by, badgeSize, badgeSize), 7);
            e.Graphics.FillPath(bgBrush, bgPath);
            var iconPad = Math.Max(5, badgeSize / 5);
            AdminTheme.DrawIcon(e.Graphics, iconName, new Rectangle(bx + iconPad, by + iconPad, badgeSize - iconPad * 2, badgeSize - iconPad * 2), iconColor);
        };

        layout.Controls.Add(caption, 0, 0);
        layout.Controls.Add(iconBox, 1, 0);
        layout.Controls.Add(valueLabel, 0, 1);
        layout.SetColumnSpan(valueLabel, 2);
        layout.Controls.Add(detail, 0, 2);
        layout.SetColumnSpan(detail, 2);

        card.Controls.Add(layout);
        return card;
    }

    public static Panel CreateDashboardCard(string title, Control content, int height = 280, Padding? margin = null)
    {
        var card = new RoundedCardPanel
        {
            Dock = DockStyle.Fill,
            Height = height,
            Margin = margin ?? new Padding(0, 0, 0, 16),
            Padding = new Padding(18, 14, 18, 14)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            RowCount = 2,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        var lblTitle = AdminTheme.CreateLabel(title, 11.5f, true);
        lblTitle.ForeColor = AdminTheme.Navy;
        lblTitle.AutoSize = true;
        lblTitle.Margin = new Padding(0, 0, 0, 10);

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(lblTitle, 0, 0);

        content.Dock = DockStyle.Fill;
        content.Margin = Padding.Empty;
        layout.Controls.Add(content, 0, 1);

        card.Controls.Add(layout);
        return card;
    }

    public static DataGridView CompactGrid()
    {
        var grid = ManagementUi.CreateGrid();
        EnableDoubleBuffering(grid);
        grid.Font = AdminTheme.CreateFont(9F);
        grid.ColumnHeadersDefaultCellStyle.Font = AdminTheme.CreateFont(8.5F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
        grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        grid.RowTemplate.Height = 36;
        grid.ColumnHeadersHeight = 38;
        grid.AllowUserToResizeColumns = false;
        return grid;
    }
}

public sealed class StatusBarsControl : Control
{
    private IReadOnlyList<OrderStatusMetric> data = [];
    private static readonly OrderStatus[] CanonicalStatuses =
    [
        OrderStatus.Pending,
        OrderStatus.Confirmed,
        OrderStatus.Shipping,
        OrderStatus.Completed,
        OrderStatus.Cancelled
    ];

    public StatusBarsControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        UpdateStyles();
        BackColor = Color.White;
        MinimumSize = new Size(240, 180);
    }

    public void SetData(IReadOnlyList<OrderStatusMetric> values)
    {
        data = values;
        Invalidate();
    }

    private int GetCount(OrderStatus status) =>
        data.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (Width < 60 || Height < 60) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var labelFont = AdminTheme.CreateFont(9f);
        using var countFont = AdminTheme.CreateFont(9f, FontStyle.Bold);

        var maxLabelWidth = 0;
        var maxCountWidth = 0;
        var maxCount = 1;

        foreach (var status in CanonicalStatuses)
        {
            var count = GetCount(status);
            if (count > maxCount) maxCount = count;

            var labelText = OrderStatusLabels.Display(status);
            var countText = count.ToString("N0", ReportingUi.Vietnamese);
            maxLabelWidth = Math.Max(maxLabelWidth, TextRenderer.MeasureText(e.Graphics, labelText, labelFont, Size.Empty, TextFormatFlags.NoPadding).Width);
            maxCountWidth = Math.Max(maxCountWidth, TextRenderer.MeasureText(e.Graphics, countText, countFont, Size.Empty, TextFormatFlags.NoPadding).Width);
        }

        var labelColWidth = Math.Min(maxLabelWidth + 10, Math.Max(80, (int)(Width * 0.42)));
        var countColWidth = Math.Max(28, maxCountWidth + 10);
        var barLeft = 4 + labelColWidth + 10;
        var available = Math.Max(24, Width - barLeft - countColWidth - 8);

        var rowHeight = Math.Max(26, Math.Min(40, (Height - 8) / CanonicalStatuses.Length));
        var startY = Math.Max(2, (Height - (CanonicalStatuses.Length * rowHeight)) / 2);
        var barHeight = Math.Max(10, Math.Min(14, (int)Math.Round(labelFont.Height * 0.78)));

        for (var i = 0; i < CanonicalStatuses.Length; i++)
        {
            var status = CanonicalStatuses[i];
            var count = GetCount(status);
            var y = startY + i * rowHeight;
            var rowInnerHeight = rowHeight - 4;

            // Nhãn trạng thái (Bên trái)
            var label = OrderStatusLabels.Display(status);
            var textRect = new Rectangle(4, y, labelColWidth, rowInnerHeight);
            TextRenderer.DrawText(e.Graphics, label, labelFont, textRect, AdminTheme.Navy,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            // Vùng thanh biểu đồ
            var barY = y + (rowInnerHeight - barHeight) / 2;
            var bgRect = new Rectangle(barLeft, barY, available, barHeight);
            using var bgPath = AdminTheme.CreateRoundedRectanglePath(bgRect, 4);
            using var bgBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
            e.Graphics.FillPath(bgBrush, bgPath);

            // Thanh giá trị (chỉ vẽ khi count > 0)
            if (count > 0)
            {
                var fillWidth = Math.Min(available, Math.Max(6, (int)Math.Round(available * count / (double)maxCount)));
                var fillRect = new Rectangle(barLeft, barY, fillWidth, barHeight);
                using var fillPath = AdminTheme.CreateRoundedRectanglePath(fillRect, 4);
                using var fillBrush = new SolidBrush(ReportingUi.GetStatusColor(status));
                e.Graphics.FillPath(fillBrush, fillPath);
            }

            // Số lượng ở cuối thanh
            var countRect = new Rectangle(barLeft + available + 8, y, countColWidth, rowInnerHeight);
            TextRenderer.DrawText(e.Graphics, count.ToString("N0", ReportingUi.Vietnamese), countFont, countRect,
                count > 0 ? AdminTheme.Navy : AdminTheme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPadding);
        }
    }
}

public sealed class OrderDonutChartControl : UserControl
{
    private static readonly OrderStatus[] CanonicalStatuses =
    [
        OrderStatus.Pending,
        OrderStatus.Confirmed,
        OrderStatus.Shipping,
        OrderStatus.Completed,
        OrderStatus.Cancelled
    ];

    private IReadOnlyList<OrderStatusMetric> data = [];
    private readonly DonutCanvasControl canvas;
    private readonly DonutLegendControl legend;

    public OrderDonutChartControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        UpdateStyles();
        BackColor = Color.White;
        MinimumSize = new Size(260, 180);

        canvas = new DonutCanvasControl { Dock = DockStyle.Fill, Margin = Padding.Empty };
        legend = new DonutLegendControl { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0) };

        var split = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        ReportingUi.EnableDoubleBuffering(split);
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
        split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        split.Controls.Add(canvas, 0, 0);
        split.Controls.Add(legend, 1, 0);

        Controls.Add(split);
    }

    public void SetData(IReadOnlyList<OrderStatusMetric> values)
    {
        data = values;
        canvas.SetData(data);
        legend.SetData(data);
    }

    private sealed class DonutCanvasControl : Control
    {
        private IReadOnlyList<OrderStatusMetric> data = [];

        public DonutCanvasControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            UpdateStyles();
            BackColor = Color.White;
        }

        public void SetData(IReadOnlyList<OrderStatusMetric> values)
        {
            data = values;
            Invalidate();
        }

        private int GetCount(OrderStatus status) =>
            data.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (Width < 48 || Height < 48) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var diameter = Math.Max(48, Math.Min(Width, Height) - 16);
            var outerRadius = diameter / 2;
            diameter = outerRadius * 2;
            var innerRadius = (int)Math.Round(outerRadius * 0.60);
            var cx = Width / 2;
            var cy = Height / 2;

            var outerRect = new Rectangle(cx - outerRadius, cy - outerRadius, diameter, diameter);
            var innerRect = new Rectangle(cx - innerRadius, cy - innerRadius, innerRadius * 2, innerRadius * 2);

            var total = CanonicalStatuses.Sum(s => (long)Math.Max(0, GetCount(s)));
            if (total == 0)
            {
                using var emptyBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
                using var holeBrush = new SolidBrush(Color.White);
                using var borderPen = new Pen(AdminTheme.Border, 1f);

                e.Graphics.FillEllipse(emptyBrush, outerRect);
                e.Graphics.FillEllipse(holeBrush, innerRect);
                e.Graphics.DrawEllipse(borderPen, outerRect);
                e.Graphics.DrawEllipse(borderPen, innerRect);

                DrawCenterText(e.Graphics, "0", "đơn hàng", innerRect);
                return;
            }

            var active = CanonicalStatuses
                .Select(s => (Status: s, Count: Math.Max(0, GetCount(s))))
                .Where(item => item.Count > 0)
                .ToList();

            if (active.Count == 1)
            {
                using var fill = new SolidBrush(ReportingUi.GetStatusColor(active[0].Status));
                e.Graphics.FillEllipse(fill, outerRect);
            }
            else
            {
                var currentAngle = -90f;
                var boundaryAngles = new List<float>(active.Count) { currentAngle };

                for (var i = 0; i < active.Count; i++)
                {
                    var sweep = i == active.Count - 1
                        ? Math.Max(0.5f, 270f - currentAngle)
                        : (float)(active[i].Count * 360.0 / total);

                    using var fill = new SolidBrush(ReportingUi.GetStatusColor(active[i].Status));
                    e.Graphics.FillPie(fill, outerRect, currentAngle, sweep);

                    currentAngle += sweep;
                    if (i < active.Count - 1)
                        boundaryAngles.Add(currentAngle);
                }

                // Vạch tách trắng sắc nét giữa các lát cắt
                using var sepPen = new Pen(Color.White, 2f);
                foreach (var angleDeg in boundaryAngles)
                {
                    var rad = angleDeg * Math.PI / 180.0;
                    var ex = (float)(cx + (outerRadius + 2) * Math.Cos(rad));
                    var ey = (float)(cy + (outerRadius + 2) * Math.Sin(rad));
                    e.Graphics.DrawLine(sepPen, cx, cy, ex, ey);
                }
            }

            // Đục lỗ tròn trắng ở giữa tạo Donut sạch sẽ
            using (var holeBrush = new SolidBrush(Color.White))
            {
                e.Graphics.FillEllipse(holeBrush, innerRect);
            }

            DrawCenterText(e.Graphics, total.ToString("N0", ReportingUi.Vietnamese), "đơn hàng", innerRect);
        }

        private static void DrawCenterText(Graphics g, string countText, string labelText, Rectangle innerRect)
        {
            if (innerRect.Width < 28 || innerRect.Height < 28) return;

            var maxCountFont = Math.Clamp(innerRect.Width * 0.21f, 9f, 16f);
            using var countFont = FitFont(g, countText, maxCountFont, FontStyle.Bold, innerRect.Width - 10);
            var labelFontSize = Math.Clamp(innerRect.Width * 0.11f, 7f, 8.5f);
            using var labelFont = AdminTheme.CreateFont(labelFontSize);

            var countSz = TextRenderer.MeasureText(g, countText, countFont, Size.Empty, TextFormatFlags.NoPadding);
            var labelSz = TextRenderer.MeasureText(g, labelText, labelFont, Size.Empty, TextFormatFlags.NoPadding);
            var gap = 1;
            var totalHeight = countSz.Height + gap + labelSz.Height;
            var top = innerRect.Y + (innerRect.Height - totalHeight) / 2;

            var countRect = new Rectangle(innerRect.X + 2, top, innerRect.Width - 4, countSz.Height);
            var labelRect = new Rectangle(innerRect.X + 2, top + countSz.Height + gap, innerRect.Width - 4, labelSz.Height);

            TextRenderer.DrawText(g, countText, countFont, countRect, AdminTheme.Navy,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, labelText, labelFont, labelRect, AdminTheme.Secondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }

        private static Font FitFont(Graphics g, string text, float startSize, FontStyle style, int maxWidth)
        {
            var size = startSize;
            while (size > 8.5f)
            {
                var candidate = AdminTheme.CreateFont(size, style);
                var measured = TextRenderer.MeasureText(g, text, candidate, Size.Empty, TextFormatFlags.NoPadding);
                if (measured.Width <= maxWidth)
                    return candidate;
                candidate.Dispose();
                size -= 1f;
            }
            return AdminTheme.CreateFont(size, style);
        }
    }

    private sealed class DonutLegendControl : Control
    {
        private IReadOnlyList<OrderStatusMetric> data = [];

        public DonutLegendControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            UpdateStyles();
            BackColor = Color.White;
        }

        public void SetData(IReadOnlyList<OrderStatusMetric> values)
        {
            data = values;
            Invalidate();
        }

        private int GetCount(OrderStatus status) =>
            data.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (Width < 60 || Height < 48) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var total = CanonicalStatuses.Sum(s => (long)Math.Max(0, GetCount(s)));
            if (total == 0)
            {
                using var emptyFont = AdminTheme.CreateFont(9f);
                TextRenderer.DrawText(e.Graphics, "Chưa có dữ liệu đơn hàng", emptyFont, ClientRectangle, AdminTheme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }

            using var nameFont = AdminTheme.CreateFont(8.5f);
            using var valBoldFont = AdminTheme.CreateFont(8.5f, FontStyle.Bold);
            using var valRegFont = AdminTheme.CreateFont(8.5f, FontStyle.Regular);

            var rows = new (OrderStatus Status, string Label, int Count, string ValText)[CanonicalStatuses.Length];
            var maxValWidth = 0;

            for (var i = 0; i < CanonicalStatuses.Length; i++)
            {
                var status = CanonicalStatuses[i];
                var count = Math.Max(0, GetCount(status));
                var pct = (count * 100.0 / total).ToString("0.#", ReportingUi.Vietnamese) + "%";
                var valText = $"{count.ToString("N0", ReportingUi.Vietnamese)} ({pct})";
                rows[i] = (status, OrderStatusLabels.Display(status), count, valText);

                var font = count > 0 ? valBoldFont : valRegFont;
                var valW = TextRenderer.MeasureText(e.Graphics, valText, font, Size.Empty, TextFormatFlags.NoPadding).Width;
                if (valW > maxValWidth) maxValWidth = valW;
            }

            const int leftPad = 4;
            const int swatchSize = 10;
            const int nameLeft = leftPad + swatchSize + 8;
            const int rightPad = 4;
            var valColWidth = Math.Min(maxValWidth + 8, Math.Max(40, Width / 2));
            var valLeft = Width - rightPad - valColWidth;
            var nameWidth = Math.Max(20, valLeft - nameLeft - 6);

            var rowHeight = Math.Max(22, Math.Min(34, (Height - 10) / rows.Length));
            var startY = Math.Max(4, (Height - rows.Length * rowHeight) / 2);

            for (var i = 0; i < rows.Length; i++)
            {
                var (status, label, count, valText) = rows[i];
                var y = startY + i * rowHeight;

                var swatchRect = new Rectangle(leftPad, y + (rowHeight - swatchSize) / 2, swatchSize, swatchSize);
                using var swatchPath = AdminTheme.CreateRoundedRectanglePath(swatchRect, 3);
                using var swatchBrush = new SolidBrush(ReportingUi.GetStatusColor(status));
                e.Graphics.FillPath(swatchBrush, swatchPath);

                var textColor = count > 0 ? AdminTheme.Navy : AdminTheme.Muted;
                var nameRect = new Rectangle(nameLeft, y, nameWidth, rowHeight);
                TextRenderer.DrawText(e.Graphics, label, nameFont, nameRect, textColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

                var valRect = new Rectangle(valLeft, y, valColWidth, rowHeight);
                var valFont = count > 0 ? valBoldFont : valRegFont;
                TextRenderer.DrawText(e.Graphics, valText, valFont, valRect, textColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPadding);
            }
        }
    }
}

public sealed class RevenueBarsControl : Control
{
    private IReadOnlyList<RevenuePoint> data = [];
    public RevenueBarsControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        UpdateStyles();
        BackColor = Color.White;
        MinimumSize = new Size(300, 190);
    }
    public void SetData(IReadOnlyList<RevenuePoint> values) { data = values; Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
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
