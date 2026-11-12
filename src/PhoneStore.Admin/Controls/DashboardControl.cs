using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Admin.Controls;

public sealed class DashboardControl : UserControl
{
    private readonly CatalogManagementGateway gateway;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Label productsValue;
    private readonly Label productsDetail;
    private readonly Label customersValue;
    private readonly Label customersDetail;
    private readonly Label ordersValue;
    private readonly Label ordersDetail;
    private readonly Label revenueValue;
    private readonly Label revenueDetail;
    private readonly StatusBarsControl statusBarsChart = new() { Dock = DockStyle.Fill };
    private readonly OrderDonutChartControl orderDonutChart = new() { Dock = DockStyle.Fill };
    private readonly DataGridView recentGrid = ReportingUi.CompactGrid();
    private readonly DataGridView bestGrid = ReportingUi.CompactGrid();
    private readonly DataGridView stockGrid = ReportingUi.CompactGrid();
    private readonly Panel recentEmpty = CreateEmptyPanel("Chưa có đơn hàng gần đây.", "orders");
    private readonly Panel bestEmpty = CreateEmptyPanel("Chưa có dữ liệu bán hàng.", "reports");
    private readonly Panel stockEmpty = CreateEmptyPanel("Không có sản phẩm sắp hết hàng.", "products");
    private readonly Panel stockCard;
    private readonly Panel[] kpiCards;
    private readonly Panel statusBarsCard;
    private readonly Panel donutChartCard;
    private readonly Panel recentOrdersCard;
    private readonly Panel bestSellersCard;
    private readonly TableLayoutPanel cardsLayout;
    private readonly TableLayoutPanel chartsLayout;
    private readonly TableLayoutPanel tablesLayout;
    private readonly Label status = AdminTheme.CreateLabel("");
    private readonly Button refresh = ManagementUi.CreateButton("Làm mới", true);
    private bool? currentStacked;
    private bool busy;
    private bool disposedLifetime;

    public DashboardControl(CatalogManagementGateway gateway)
    {
        this.gateway = gateway;
        SuspendLayout();
        Name = nameof(DashboardControl);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AdminTheme.Surface;
        Padding = new Padding(24);
        AutoScroll = true;
        ReportingUi.EnableDoubleBuffering(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 6,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        ReportingUi.EnableDoubleBuffering(root);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < root.RowCount; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // ---------------------------------------------------------------------
        // HEADER: Tiêu đề + Phụ đề + Nút Làm mới
        // ---------------------------------------------------------------------
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 16)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var headerText = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = Padding.Empty };
        var heading = AdminTheme.CreateLabel("Tổng quan hoạt động", 19, true);
        heading.AutoSize = true;
        heading.Margin = Padding.Empty;
        var hint = AdminTheme.CreateLabel("Số liệu trực tiếp từ PhoneStoreDB.");
        hint.ForeColor = AdminTheme.Secondary;
        hint.Margin = new Padding(0, 4, 0, 0);
        headerText.Controls.Add(heading, 0, 0);
        headerText.Controls.Add(hint, 0, 1);
        header.Controls.Add(headerText, 0, 0);
        header.Controls.Add(refresh, 1, 0);

        // ---------------------------------------------------------------------
        // HÀNG 1: 4 KPI CARDS
        // ---------------------------------------------------------------------
        cardsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 6)
        };
        ReportingUi.EnableDoubleBuffering(cardsLayout);

        kpiCards =
        [
            ReportingUi.SummaryCard("Sản phẩm", "products", AdminTheme.PrimaryLight, AdminTheme.Primary, "DashboardProductsValue", out productsValue, out productsDetail),
            ReportingUi.SummaryCard("Khách hàng", "customers", Color.FromArgb(241, 245, 249), Color.FromArgb(71, 85, 105), "DashboardCustomersValue", out customersValue, out customersDetail),
            ReportingUi.SummaryCard("Đơn hàng", "orders", Color.FromArgb(254, 243, 199), Color.FromArgb(180, 83, 9), "DashboardOrdersValue", out ordersValue, out ordersDetail),
            ReportingUi.SummaryCard("Doanh thu", "revenue", Color.FromArgb(220, 252, 231), Color.FromArgb(21, 128, 61), "DashboardRevenueValue", out revenueValue, out revenueDetail)
        ];
        for (var i = 0; i < kpiCards.Length; i++)
            cardsLayout.Controls.Add(kpiCards[i], i, 0);

        // ---------------------------------------------------------------------
        // HÀNG 2: 2 BIỂU ĐỒ (Thanh ngang 55% + Donut 45%)
        // ---------------------------------------------------------------------
        chartsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        ReportingUi.EnableDoubleBuffering(chartsLayout);
        statusBarsCard = ReportingUi.CreateDashboardCard("Trạng thái đơn hàng", statusBarsChart, 280, new Padding(0, 0, 12, 16));
        donutChartCard = ReportingUi.CreateDashboardCard("Tỷ lệ trạng thái đơn hàng", orderDonutChart, 280, new Padding(0, 0, 0, 16));
        chartsLayout.Controls.Add(statusBarsCard, 0, 0);
        chartsLayout.Controls.Add(donutChartCard, 1, 0);

        // ---------------------------------------------------------------------
        // HÀNG 3: 2 BẢNG (Đơn hàng gần đây 55% + Sản phẩm bán chạy 45%)
        // ---------------------------------------------------------------------
        ConfigureRecentGrid();
        ConfigureBestGrid();
        ConfigureStockGrid();

        tablesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        ReportingUi.EnableDoubleBuffering(tablesLayout);
        recentOrdersCard = GridCard("Đơn hàng gần đây", recentGrid, recentEmpty, 280, new Padding(0, 0, 12, 16));
        bestSellersCard = GridCard("Sản phẩm bán chạy", bestGrid, bestEmpty, 280, new Padding(0, 0, 0, 16));
        tablesLayout.Controls.Add(recentOrdersCard, 0, 0);
        tablesLayout.Controls.Add(bestSellersCard, 1, 0);

        // ---------------------------------------------------------------------
        // HÀNG 4: CẢNH BÁO TỒN KHO (100% width)
        // ---------------------------------------------------------------------
        stockCard = GridCard("Sản phẩm sắp hết hàng · ngưỡng ≤ 5", stockGrid, stockEmpty, 190, new Padding(0, 0, 0, 12));

        // ---------------------------------------------------------------------
        // DÒNG THỜI GIAN CẬP NHẬT
        // ---------------------------------------------------------------------
        status.ForeColor = AdminTheme.Muted;
        status.Font = AdminTheme.CreateFont(8.5f);
        status.Dock = DockStyle.Fill;
        status.Margin = new Padding(0, 4, 0, 16);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(cardsLayout, 0, 1);
        root.Controls.Add(chartsLayout, 0, 2);
        root.Controls.Add(tablesLayout, 0, 3);
        root.Controls.Add(stockCard, 0, 4);
        root.Controls.Add(status, 0, 5);
        Controls.Add(root);

        ApplyResponsiveLayout(false);

        Resize += (_, _) => ApplyResponsiveLayout(ClientSize.Width > 0 && ClientSize.Width < 850);
        refresh.Click += async (_, _) => await ReloadAsync();
        Load += async (_, _) =>
        {
            ApplyResponsiveLayout(ClientSize.Width > 0 && ClientSize.Width < 850);
            await ReloadAsync();
        };
        ResumeLayout(true);
    }

    private void ApplyResponsiveLayout(bool stacked)
    {
        if (currentStacked == stacked) return;
        currentStacked = stacked;

        cardsLayout.SuspendLayout();
        chartsLayout.SuspendLayout();
        tablesLayout.SuspendLayout();

        cardsLayout.ColumnStyles.Clear();
        cardsLayout.RowStyles.Clear();
        if (stacked)
        {
            cardsLayout.ColumnCount = 2;
            cardsLayout.RowCount = 2;
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            cardsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            cardsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (var i = 0; i < kpiCards.Length; i++)
            {
                cardsLayout.SetCellPosition(kpiCards[i], new TableLayoutPanelCellPosition(i % 2, i / 2));
                kpiCards[i].Margin = new Padding(0, 0, i % 2 == 0 ? 12 : 0, 12);
            }
        }
        else
        {
            cardsLayout.ColumnCount = 4;
            cardsLayout.RowCount = 1;
            for (var i = 0; i < 4; i++)
                cardsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            cardsLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (var i = 0; i < kpiCards.Length; i++)
            {
                cardsLayout.SetCellPosition(kpiCards[i], new TableLayoutPanelCellPosition(i, 0));
                kpiCards[i].Margin = new Padding(0, 0, i < 3 ? 12 : 0, 12);
            }
        }

        ConfigureTwoCardRow(chartsLayout, statusBarsCard, donutChartCard, stacked);
        ConfigureTwoCardRow(tablesLayout, recentOrdersCard, bestSellersCard, stacked);

        cardsLayout.ResumeLayout(true);
        chartsLayout.ResumeLayout(true);
        tablesLayout.ResumeLayout(true);
    }

    private static void ConfigureTwoCardRow(TableLayoutPanel container, Panel first, Panel second, bool stacked)
    {
        container.ColumnStyles.Clear();
        container.RowStyles.Clear();
        if (stacked)
        {
            container.ColumnCount = 1;
            container.RowCount = 2;
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            container.SetCellPosition(first, new TableLayoutPanelCellPosition(0, 0));
            container.SetCellPosition(second, new TableLayoutPanelCellPosition(0, 1));
            first.Margin = new Padding(0, 0, 0, 16);
            second.Margin = new Padding(0, 0, 0, 16);
        }
        else
        {
            container.ColumnCount = 2;
            container.RowCount = 1;
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            container.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            container.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            container.SetCellPosition(first, new TableLayoutPanelCellPosition(0, 0));
            container.SetCellPosition(second, new TableLayoutPanelCellPosition(1, 0));
            first.Margin = new Padding(0, 0, 12, 16);
            second.Margin = new Padding(0, 0, 0, 16);
        }
    }

    private async Task ReloadAsync()
    {
        if (busy || IsDisposed) return;
        SetBusy(true);
        status.Text = "Đang tải số liệu…";
        try
        {
            var data = await gateway.WithReportingAsync(service => service.GetDashboardAsync(lifetime.Token));
            if (IsDisposed || lifetime.IsCancellationRequested) return;

            productsValue.Text = ReportingUi.Number(data.ActiveProducts);
            productsDetail.Text = $"{ReportingUi.Number(data.TotalStock)} sản phẩm trong kho";

            customersValue.Text = ReportingUi.Number(data.Customers);
            customersDetail.Text = $"{ReportingUi.Number(data.ActiveCustomers)} đang hoạt động";

            ordersValue.Text = ReportingUi.Number(data.Orders);
            ordersDetail.Text = $"{ReportingUi.Number(data.PendingOrders)} chờ xác nhận";

            revenueValue.Text = ReportingUi.Currency(data.CompletedRevenue);
            revenueDetail.Text = $"{ReportingUi.Number(data.CompletedOrders)} đơn hoàn thành";

            statusBarsChart.SetData(data.OrderStatuses);
            orderDonutChart.SetData(data.OrderStatuses);

            Bind(recentGrid, recentEmpty, data.RecentOrders, "Chưa có đơn hàng gần đây.");
            Bind(bestGrid, bestEmpty, data.BestSellers, "Chưa có dữ liệu bán hàng.");
            Bind(stockGrid, stockEmpty, data.LowStockProducts, "Không có sản phẩm sắp hết hàng.");

            var stockRows = data.LowStockProducts.Count;
            var desiredStockBodyHeight = stockRows == 0
                ? 110
                : stockGrid.ColumnHeadersHeight + (Math.Min(stockRows, 5) * stockGrid.RowTemplate.Height) + 8;
            stockCard.Height = Math.Clamp(desiredStockBodyHeight + 62, 148, 280);

            status.Text = "Đã cập nhật lúc " + DateTime.Now.ToString("HH:mm:ss") + " · PhoneStoreDB";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (!IsDisposed) status.Text = ManagementUi.ErrorMessage(ex);
        }
        finally
        {
            if (!IsDisposed) SetBusy(false);
        }
    }

    private void ConfigureRecentGrid()
    {
        recentGrid.Name = "DashboardRecentOrdersGrid";
        recentGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.OrderCode), "Mã đơn", 74);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.ReceiverName), "Người nhận", 86);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.TotalAmount), "Tổng tiền", 80, "#,##0 ₫");
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.StatusDisplay), "Trạng thái", 96);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.OrderDate), "Ngày đặt", 70, "dd/MM HH:mm");

        var codeCol = recentGrid.Columns[nameof(RecentOrderMetric.OrderCode)]!;
        codeCol.FillWeight = 18;
        codeCol.MinimumWidth = 68;

        var receiverCol = recentGrid.Columns[nameof(RecentOrderMetric.ReceiverName)]!;
        receiverCol.FillWeight = 22;
        receiverCol.MinimumWidth = 76;

        var totalCol = recentGrid.Columns[nameof(RecentOrderMetric.TotalAmount)]!;
        totalCol.FillWeight = 20;
        totalCol.MinimumWidth = 76;
        totalCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        var statusCol = recentGrid.Columns[nameof(RecentOrderMetric.StatusDisplay)]!;
        statusCol.FillWeight = 23;
        statusCol.MinimumWidth = 92;

        var dateCol = recentGrid.Columns[nameof(RecentOrderMetric.OrderDate)]!;
        dateCol.FillWeight = 17;
        dateCol.MinimumWidth = 68;
        dateCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        // Rút gọn mã đơn dài và tooltip hiển thị đầy đủ mã đơn + ngày giờ
        recentGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == 0 && e.Value is string code)
            {
                recentGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = code;
                if (code.Length > 10)
                {
                    e.Value = code[..8] + "…";
                    e.FormattingApplied = true;
                }
            }
            else if (e.ColumnIndex == 4 && e.Value is DateTime orderDate)
            {
                recentGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = orderDate.ToString("dd/MM/yyyy HH:mm");
            }
        };

        // Badge màu cho cột Trạng thái (giữ nguyên viền ngang dưới ô)
        recentGrid.CellPainting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 3 && e.Value != null && e.Graphics != null)
            {
                e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);
                var text = e.Value.ToString() ?? "";
                var row = recentGrid.Rows[e.RowIndex].DataBoundItem as RecentOrderMetric;
                var (bg, fg) = GetStatusBadgeColors(row?.Status ?? OrderStatus.Pending);
                var badgeWidth = Math.Max(56, Math.Min(e.CellBounds.Width - 6, 108));
                var badgeX = e.CellBounds.X + (e.CellBounds.Width - badgeWidth) / 2;
                var badgeRect = new Rectangle(badgeX, e.CellBounds.Y + (e.CellBounds.Height - 24) / 2, badgeWidth, 24);
                AdminTheme.DrawBadge(e.Graphics, badgeRect, text, bg, fg, 7.75f);
                e.Handled = true;
            }
        };
    }

    private void ConfigureBestGrid()
    {
        bestGrid.Name = "DashboardBestSellersGrid";
        bestGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.Rank), "#", 38);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.ProductName), "Sản phẩm", 160);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.QuantitySold), "Đã bán", 66, "N0");
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.SalesValue), "Doanh thu", 100, "#,##0 ₫");

        var rankCol = bestGrid.Columns[nameof(BestSellerMetric.Rank)]!;
        rankCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        rankCol.Width = 38;
        rankCol.MinimumWidth = 34;
        rankCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var prodCol = bestGrid.Columns[nameof(BestSellerMetric.ProductName)]!;
        prodCol.FillWeight = 52;
        prodCol.MinimumWidth = 110;

        var qtyCol = bestGrid.Columns[nameof(BestSellerMetric.QuantitySold)]!;
        qtyCol.FillWeight = 19;
        qtyCol.MinimumWidth = 62;
        qtyCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        var valCol = bestGrid.Columns[nameof(BestSellerMetric.SalesValue)]!;
        valCol.FillWeight = 29;
        valCol.MinimumWidth = 94;
        valCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
    }

    private void ConfigureStockGrid()
    {
        stockGrid.Name = "DashboardLowStockGrid";
        stockGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.ProductName), "Sản phẩm", 220);
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.BrandName), "Thương hiệu", 120);
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.CategoryName), "Danh mục", 140);
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.StockQuantity), "Tồn kho", 80, "N0");
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.StockStatus), "Cảnh báo", 104);

        var prodCol = stockGrid.Columns[nameof(LowStockMetric.ProductName)]!;
        prodCol.FillWeight = 34;
        prodCol.MinimumWidth = 150;

        var brandCol = stockGrid.Columns[nameof(LowStockMetric.BrandName)]!;
        brandCol.FillWeight = 18;
        brandCol.MinimumWidth = 95;

        var catCol = stockGrid.Columns[nameof(LowStockMetric.CategoryName)]!;
        catCol.FillWeight = 20;
        catCol.MinimumWidth = 105;

        var qtyCol = stockGrid.Columns[nameof(LowStockMetric.StockQuantity)]!;
        qtyCol.FillWeight = 12;
        qtyCol.MinimumWidth = 70;
        qtyCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        var alertCol = stockGrid.Columns[nameof(LowStockMetric.StockStatus)]!;
        alertCol.FillWeight = 16;
        alertCol.MinimumWidth = 96;

        // Badge màu cho cột Cảnh báo
        stockGrid.CellPainting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 4 && e.Value != null && e.Graphics != null)
            {
                e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);
                var text = e.Value.ToString() ?? "";
                var row = stockGrid.Rows[e.RowIndex].DataBoundItem as LowStockMetric;
                var isOut = row != null && row.StockQuantity == 0;
                var bg = isOut ? AdminTheme.CancelledBg : AdminTheme.PendingBg;
                var fg = isOut ? AdminTheme.CancelledText : AdminTheme.PendingText;
                var badgeWidth = Math.Max(56, Math.Min(e.CellBounds.Width - 10, 100));
                var badgeX = e.CellBounds.X + (e.CellBounds.Width - badgeWidth) / 2;
                var badgeRect = new Rectangle(badgeX, e.CellBounds.Y + (e.CellBounds.Height - 24) / 2, badgeWidth, 24);
                AdminTheme.DrawBadge(e.Graphics, badgeRect, text, bg, fg);
                e.Handled = true;
            }
        };
    }

    private static (Color bg, Color fg) GetStatusBadgeColors(OrderStatus status) => status switch
    {
        OrderStatus.Pending => (AdminTheme.PendingBg, AdminTheme.PendingText),
        OrderStatus.Confirmed => (AdminTheme.ConfirmedBg, AdminTheme.ConfirmedText),
        OrderStatus.Shipping => (AdminTheme.ShippingBg, AdminTheme.ShippingText),
        OrderStatus.Completed => (AdminTheme.CompletedBg, AdminTheme.CompletedText),
        OrderStatus.Cancelled => (AdminTheme.CancelledBg, AdminTheme.CancelledText),
        _ => (AdminTheme.Surface, AdminTheme.Navy)
    };

    private static Panel GridCard(string title, DataGridView grid, Panel empty, int height = 280, Padding? margin = null)
    {
        var host = new RoundedCardPanel
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

        var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = Padding.Empty };
        body.Controls.Add(grid);
        body.Controls.Add(empty);
        layout.Controls.Add(body, 0, 1);
        host.Controls.Add(layout);
        return host;
    }

    private static Panel CreateEmptyPanel(string text, string iconName = "reports")
    {
        var panel = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Visible = false
        };

        var iconBox = new BufferedPanel
        {
            Size = new Size(32, 32),
            BackColor = Color.White
        };
        iconBox.Paint += (_, e) =>
        {
            e.Graphics.Clear(Color.White);
            AdminTheme.DrawIcon(e.Graphics, iconName, new Rectangle(2, 2, 28, 28), AdminTheme.Muted);
        };

        var label = AdminTheme.CreateLabel(text, 9.5f);
        label.ForeColor = AdminTheme.Muted;
        label.AutoSize = true;

        void CenterControls()
        {
            var totalH = iconBox.Height + 8 + label.Height;
            var startY = Math.Max(0, (panel.Height - totalH) / 2);
            iconBox.Location = new Point(Math.Max(0, (panel.Width - iconBox.Width) / 2), startY);
            label.Location = new Point(Math.Max(0, (panel.Width - label.Width) / 2), startY + iconBox.Height + 8);
        }

        panel.Resize += (_, _) => CenterControls();
        label.SizeChanged += (_, _) => CenterControls();
        panel.Controls.Add(iconBox);
        panel.Controls.Add(label);
        CenterControls();
        return panel;
    }

    private static void Bind<T>(DataGridView grid, Panel empty, IReadOnlyList<T> rows, string text)
    {
        grid.DataSource = rows.ToList();
        var hasData = rows.Count > 0;
        grid.Visible = hasData;
        empty.Visible = !hasData;
        if (!hasData)
        {
            var lbl = empty.Controls.OfType<Label>().FirstOrDefault();
            if (lbl != null) lbl.Text = text;
            empty.BringToFront();
        }
    }

    private void SetBusy(bool value)
    {
        busy = value;
        refresh.Enabled = !value;
        UseWaitCursor = value;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposedLifetime)
        {
            lifetime.Cancel();
            lifetime.Dispose();
            disposedLifetime = true;
        }
        base.Dispose(disposing);
    }
}
