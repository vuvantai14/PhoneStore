using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
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
    private readonly StatusBarsControl statusChart = new() { Dock = DockStyle.Fill };
    private readonly DataGridView recentGrid = ReportingUi.CompactGrid();
    private readonly DataGridView bestGrid = ReportingUi.CompactGrid();
    private readonly DataGridView stockGrid = ReportingUi.CompactGrid();
    private readonly Label recentEmpty = EmptyLabel();
    private readonly Label bestEmpty = EmptyLabel();
    private readonly Label stockEmpty = EmptyLabel();
    private readonly Label status = AdminTheme.CreateLabel("");
    private readonly Button refresh = ManagementUi.CreateButton("Làm mới");
    private bool busy;
    private bool disposedLifetime;

    public DashboardControl(CatalogManagementGateway gateway)
    {
        this.gateway = gateway;
        SuspendLayout(); Name = nameof(DashboardControl); AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi; BackColor = AdminTheme.Surface; Padding = new Padding(24); AutoScroll = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < root.RowCount; i++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 18) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var headerText = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        var heading = AdminTheme.CreateLabel("Tổng quan hoạt động", 20, true); heading.Dock = DockStyle.Fill;
        var hint = AdminTheme.CreateLabel("Số liệu trực tiếp từ PhoneStoreDB."); hint.ForeColor = AdminTheme.Muted; hint.Margin = new Padding(0, 6, 0, 0);
        headerText.Controls.Add(heading, 0, 0); headerText.Controls.Add(hint, 0, 1); header.Controls.Add(headerText, 0, 0); header.Controls.Add(refresh, 1, 0);
        var cards = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Margin = new Padding(0, 0, 0, 8) };
        for (var i = 0; i < 4; i++) cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        cards.Controls.Add(ReportingUi.SummaryCard("Sản phẩm", "DashboardProductsValue", out productsValue, out productsDetail), 0, 0);
        cards.Controls.Add(ReportingUi.SummaryCard("Khách hàng", "DashboardCustomersValue", out customersValue, out customersDetail), 1, 0);
        cards.Controls.Add(ReportingUi.SummaryCard("Đơn hàng", "DashboardOrdersValue", out ordersValue, out ordersDetail), 2, 0);
        cards.Controls.Add(ReportingUi.SummaryCard("Doanh thu", "DashboardRevenueValue", out revenueValue, out revenueDetail), 3, 0);
        var statusCard = SectionCard("Trạng thái đơn hàng", statusChart, 250); statusCard.Margin = new Padding(0, 0, 0, 16);
        ConfigureRecentGrid(); ConfigureBestGrid(); ConfigureStockGrid();
        var middle = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 16) };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        middle.Controls.Add(GridCard("Đơn hàng gần đây", recentGrid, recentEmpty), 0, 0);
        middle.Controls.Add(GridCard("Sản phẩm bán chạy", bestGrid, bestEmpty), 1, 0);
        var stockCard = GridCard("Sản phẩm sắp hết hàng · ngưỡng ≤ 5", stockGrid, stockEmpty); stockCard.Margin = new Padding(0, 0, 0, 10);
        status.ForeColor = AdminTheme.Muted; status.Dock = DockStyle.Fill;
        root.Controls.Add(header, 0, 0); root.Controls.Add(cards, 0, 1); root.Controls.Add(statusCard, 0, 2);
        root.Controls.Add(middle, 0, 3); root.Controls.Add(stockCard, 0, 4); root.Controls.Add(status, 0, 5);
        Controls.Add(root);
        refresh.Click += async (_, _) => await ReloadAsync(); Load += async (_, _) => await ReloadAsync(); ResumeLayout(true);
    }

    private async Task ReloadAsync()
    {
        if (busy || IsDisposed) return; SetBusy(true); status.Text = "Đang tải số liệu…";
        try
        {
            var data = await gateway.WithReportingAsync(service => service.GetDashboardAsync(lifetime.Token));
            if (IsDisposed || lifetime.IsCancellationRequested) return;
            productsValue.Text = ReportingUi.Number(data.ActiveProducts); productsDetail.Text = $"{ReportingUi.Number(data.TotalStock)} sản phẩm trong kho";
            customersValue.Text = ReportingUi.Number(data.Customers); customersDetail.Text = $"{ReportingUi.Number(data.ActiveCustomers)} đang hoạt động";
            ordersValue.Text = ReportingUi.Number(data.Orders); ordersDetail.Text = $"{ReportingUi.Number(data.PendingOrders)} chờ xác nhận";
            revenueValue.Text = ReportingUi.Currency(data.CompletedRevenue); revenueDetail.Text = $"{ReportingUi.Number(data.CompletedOrders)} đơn hoàn thành";
            statusChart.SetData(data.OrderStatuses);
            Bind(recentGrid, recentEmpty, data.RecentOrders, "Chưa có đơn hàng.");
            Bind(bestGrid, bestEmpty, data.BestSellers, "Chưa có dữ liệu bán hàng.");
            Bind(stockGrid, stockEmpty, data.LowStockProducts, "Không có sản phẩm sắp hết hàng.");
            status.Text = "Đã cập nhật " + DateTime.Now.ToString("HH:mm:ss");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { if (!IsDisposed) status.Text = ManagementUi.ErrorMessage(ex); }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private void ConfigureRecentGrid()
    {
        recentGrid.Name = "DashboardRecentOrdersGrid";
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.OrderCode), "Mã đơn", 160);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.ReceiverName), "Người nhận", 145);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.TotalAmount), "Tổng tiền", 125, "#,##0");
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.StatusDisplay), "Trạng thái", 115);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.OrderDate), "Ngày đặt", 140, "dd/MM/yyyy HH:mm");
    }
    private void ConfigureBestGrid()
    {
        bestGrid.Name = "DashboardBestSellersGrid";
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.Rank), "#", 45);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.ProductName), "Sản phẩm", 210);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.QuantitySold), "Đã bán", 85, "N0");
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.SalesValue), "Giá trị", 130, "#,##0");
    }
    private void ConfigureStockGrid()
    {
        stockGrid.Name = "DashboardLowStockGrid";
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.ProductName), "Sản phẩm", 260);
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.BrandName), "Thương hiệu", 150);
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.CategoryName), "Danh mục", 190);
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.StockQuantity), "Tồn kho", 100, "N0");
        ManagementUi.AddColumn(stockGrid, nameof(LowStockMetric.StockStatus), "Cảnh báo", 120);
        stockGrid.Columns[nameof(LowStockMetric.ProductName)]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }
    private static Panel GridCard(string title, DataGridView grid, Label empty)
    {
        var host = new Panel { Dock = DockStyle.Fill, Height = 265, Margin = new Padding(0, 0, 14, 0), BackColor = Color.White, Padding = new Padding(18) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(AdminTheme.CreateLabel(title, 12, true), 0, 0);
        var body = new Panel { Dock = DockStyle.Fill }; body.Controls.Add(grid); body.Controls.Add(empty); layout.Controls.Add(body, 0, 1); host.Controls.Add(layout); return host;
    }
    private static Panel SectionCard(string title, Control content, int height)
    {
        var host = new Panel { Dock = DockStyle.Fill, Height = height, BackColor = Color.White, Padding = new Padding(18) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(AdminTheme.CreateLabel(title, 12, true), 0, 0); layout.Controls.Add(content, 0, 1); host.Controls.Add(layout); return host;
    }
    private static Label EmptyLabel() => new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = AdminTheme.Muted, BackColor = Color.White, Visible = false };
    private static void Bind<T>(DataGridView grid, Label empty, IReadOnlyList<T> rows, string text)
    {
        grid.DataSource = rows.ToList(); grid.Visible = rows.Count > 0; empty.Text = text; empty.Visible = rows.Count == 0; empty.BringToFront();
    }
    private void SetBusy(bool value) { busy = value; refresh.Enabled = !value; UseWaitCursor = value; }
    protected override void Dispose(bool disposing) { if (disposing && !disposedLifetime) { lifetime.Cancel(); lifetime.Dispose(); disposedLifetime = true; } base.Dispose(disposing); }
}
