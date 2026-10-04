using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Admin.Controls;

public sealed class ReportsControl : UserControl
{
    private readonly CatalogManagementGateway gateway;
    private readonly CancellationTokenSource lifetime = new();
    private readonly DateTimePicker fromDate = new() { Name = "ReportFromDate", Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 150 };
    private readonly DateTimePicker toDate = new() { Name = "ReportToDate", Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 150 };
    private readonly Button loadButton = ManagementUi.CreateButton("Xem báo cáo", true);
    private readonly Label ordersValue;
    private readonly Label ordersDetail;
    private readonly Label revenueValue;
    private readonly Label revenueDetail;
    private readonly Label averageValue;
    private readonly Label averageDetail;
    private readonly Label inventoryValue;
    private readonly Label inventoryDetail;
    private readonly StatusBarsControl statusChart = new() { Dock = DockStyle.Fill };
    private readonly RevenueBarsControl revenueChart = new() { Dock = DockStyle.Fill };
    private readonly DataGridView bestGrid = ReportingUi.CompactGrid();
    private readonly DataGridView inventoryGrid = ReportingUi.CompactGrid();
    private readonly Label bestEmpty = EmptyLabel();
    private readonly Label inventoryEmpty = EmptyLabel();
    private readonly Label status = AdminTheme.CreateLabel("");
    private bool busy;
    private bool disposedLifetime;

    public ReportsControl(CatalogManagementGateway gateway)
    {
        this.gateway = gateway;
        SuspendLayout();
        Name = nameof(ReportsControl);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AdminTheme.Surface;
        Padding = new Padding(24);
        AutoScroll = true;

        var today = DateTime.Today;
        fromDate.Value = new DateTime(today.Year, today.Month, 1);
        toDate.Value = today;
        fromDate.MaxDate = DateTimePicker.MaximumDateTime.Date;
        toDate.MaxDate = DateTimePicker.MaximumDateTime.Date;
        loadButton.Name = "LoadReportButton";

        var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < root.RowCount; index++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(CreateHeader(), 0, 0);
        var cards = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Margin = new Padding(0, 0, 0, 8) };
        for (var index = 0; index < 4; index++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cards.Controls.Add(ReportingUi.SummaryCard("Đơn hàng", "ReportOrdersValue", out ordersValue, out ordersDetail), 0, 0);
        cards.Controls.Add(ReportingUi.SummaryCard("Doanh thu hoàn thành", "ReportRevenueValue", out revenueValue, out revenueDetail), 1, 0);
        cards.Controls.Add(ReportingUi.SummaryCard("Giá trị trung bình", "ReportAverageValue", out averageValue, out averageDetail), 2, 0);
        cards.Controls.Add(ReportingUi.SummaryCard("Tồn kho hiện tại", "ReportInventoryValue", out inventoryValue, out inventoryDetail), 3, 0);
        root.Controls.Add(cards, 0, 1);

        var charts = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 16) };
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        charts.Controls.Add(SectionCard("Đơn hàng theo trạng thái", statusChart, 260), 0, 0);
        charts.Controls.Add(SectionCard("Doanh thu hoàn thành theo ngày", revenueChart, 260), 1, 0);
        root.Controls.Add(charts, 0, 2);

        ConfigureBestGrid();
        ConfigureInventoryGrid();
        var tables = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 10) };
        tables.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        tables.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        tables.Controls.Add(GridCard("Top 10 sản phẩm bán chạy", bestGrid, bestEmpty), 0, 0);
        tables.Controls.Add(GridCard("Cảnh báo tồn kho · ngưỡng ≤ 5", inventoryGrid, inventoryEmpty), 1, 0);
        root.Controls.Add(tables, 0, 3);
        status.ForeColor = AdminTheme.Muted;
        status.Dock = DockStyle.Fill;
        root.Controls.Add(status, 0, 4);
        Controls.Add(root);

        loadButton.Click += async (_, _) => await ReloadAsync();
        Load += async (_, _) => await ReloadAsync();
        ResumeLayout(true);
    }

    private Control CreateHeader()
    {
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 18) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var text = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        var heading = AdminTheme.CreateLabel("Báo cáo & thống kê", 20, true);
        var hint = AdminTheme.CreateLabel("Phân tích đơn hàng, doanh thu và tồn kho từ PhoneStoreDB.");
        hint.ForeColor = AdminTheme.Muted;
        hint.Margin = new Padding(0, 6, 0, 0);
        text.Controls.Add(heading, 0, 0);
        text.Controls.Add(hint, 0, 1);

        var filters = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
        filters.Controls.Add(Field("Từ ngày", fromDate));
        filters.Controls.Add(Field("Đến ngày", toDate));
        loadButton.Margin = new Padding(8, 20, 0, 0);
        filters.Controls.Add(loadButton);
        header.Controls.Add(text, 0, 0);
        header.Controls.Add(filters, 1, 0);
        return header;
    }

    private static Control Field(string caption, Control input)
    {
        var layout = new TableLayoutPanel { AutoSize = true, RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 0, 10, 0) };
        var label = AdminTheme.CreateLabel(caption, 9, true);
        label.ForeColor = AdminTheme.Muted;
        label.Margin = new Padding(0, 0, 0, 4);
        input.Font = new Font("Segoe UI", 10);
        input.Height = 36;
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(input, 0, 1);
        return layout;
    }

    private async Task ReloadAsync()
    {
        if (busy || IsDisposed) return;
        var from = DateOnly.FromDateTime(fromDate.Value.Date);
        var to = DateOnly.FromDateTime(toDate.Value.Date);
        if (from > to)
        {
            status.Text = "Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.";
            status.ForeColor = Color.FromArgb(180, 68, 77);
            return;
        }

        SetBusy(true);
        status.ForeColor = AdminTheme.Muted;
        status.Text = "Đang tải báo cáo…";
        try
        {
            var result = await gateway.WithReportingAsync(service => service.GetReportAsync(from, to, lifetime.Token));
            if (IsDisposed || lifetime.IsCancellationRequested) return;
            if (!result.Succeeded || result.Report is null)
            {
                status.ForeColor = Color.FromArgb(180, 68, 77);
                status.Text = result.Error ?? "Chưa thể tải báo cáo.";
                return;
            }
            Bind(result.Report);
            status.Text = $"Dữ liệu từ {from:dd/MM/yyyy} đến hết ngày {to:dd/MM/yyyy} · cập nhật {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                status.ForeColor = Color.FromArgb(180, 68, 77);
                status.Text = ManagementUi.ErrorMessage(exception);
            }
        }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private void Bind(ReportSnapshot report)
    {
        ordersValue.Text = ReportingUi.Number(report.TotalOrders);
        ordersDetail.Text = $"{ReportingUi.Number(report.CompletedOrders)} đơn hoàn thành";
        revenueValue.Text = ReportingUi.Currency(report.CompletedRevenue);
        revenueDetail.Text = "Chỉ tính đơn đã hoàn thành";
        averageValue.Text = ReportingUi.Currency(report.AverageCompletedOrderValue);
        averageDetail.Text = "Trên mỗi đơn hoàn thành";
        inventoryValue.Text = ReportingUi.Number(report.TotalStock);
        inventoryDetail.Text = $"{ReportingUi.Number(report.ActiveProducts)} sản phẩm · {ReportingUi.Number(report.OutOfStockProducts)} hết hàng";
        statusChart.SetData(report.OrderStatuses);
        revenueChart.SetData(report.RevenueTrend);
        BindGrid(bestGrid, bestEmpty, report.BestSellers, "Chưa có dữ liệu bán hàng.");
        BindGrid(inventoryGrid, inventoryEmpty, report.InventoryAlerts, "Không có sản phẩm sắp hết hàng.");
    }

    private void ConfigureBestGrid()
    {
        bestGrid.Name = "ReportBestSellersGrid";
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.Rank), "#", 42);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.ProductName), "Sản phẩm", 210);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.BrandName), "Thương hiệu", 110);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.QuantitySold), "Đã bán", 75, "N0");
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.SalesValue), "Giá trị", 120, "#,##0");
        bestGrid.Columns[nameof(BestSellerMetric.ProductName)]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private void ConfigureInventoryGrid()
    {
        inventoryGrid.Name = "ReportInventoryGrid";
        ManagementUi.AddColumn(inventoryGrid, nameof(LowStockMetric.ProductName), "Sản phẩm", 190);
        ManagementUi.AddColumn(inventoryGrid, nameof(LowStockMetric.BrandName), "Thương hiệu", 110);
        ManagementUi.AddColumn(inventoryGrid, nameof(LowStockMetric.CategoryName), "Danh mục", 130);
        ManagementUi.AddColumn(inventoryGrid, nameof(LowStockMetric.StockQuantity), "Tồn", 65, "N0");
        ManagementUi.AddColumn(inventoryGrid, nameof(LowStockMetric.StockStatus), "Trạng thái", 100);
        inventoryGrid.Columns[nameof(LowStockMetric.ProductName)]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private static Panel GridCard(string title, DataGridView grid, Label empty)
    {
        var host = new Panel { Dock = DockStyle.Fill, Height = 330, Margin = new Padding(0, 0, 14, 0), BackColor = Color.White, Padding = new Padding(18) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(AdminTheme.CreateLabel(title, 12, true), 0, 0);
        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(grid);
        body.Controls.Add(empty);
        layout.Controls.Add(body, 0, 1);
        host.Controls.Add(layout);
        return host;
    }

    private static Panel SectionCard(string title, Control content, int height)
    {
        var host = new Panel { Dock = DockStyle.Fill, Height = height, Margin = new Padding(0, 0, 14, 0), BackColor = Color.White, Padding = new Padding(18) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(AdminTheme.CreateLabel(title, 12, true), 0, 0);
        layout.Controls.Add(content, 0, 1);
        host.Controls.Add(layout);
        return host;
    }

    private static Label EmptyLabel() => new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = AdminTheme.Muted,
        BackColor = Color.White,
        Visible = false
    };

    private static void BindGrid<T>(DataGridView grid, Label empty, IReadOnlyList<T> rows, string text)
    {
        grid.DataSource = rows.ToList();
        grid.Visible = rows.Count > 0;
        empty.Text = text;
        empty.Visible = rows.Count == 0;
        empty.BringToFront();
    }

    private void SetBusy(bool value)
    {
        busy = value;
        loadButton.Enabled = !value;
        fromDate.Enabled = !value;
        toDate.Enabled = !value;
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
