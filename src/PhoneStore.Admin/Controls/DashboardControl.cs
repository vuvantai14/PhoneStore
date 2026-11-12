using System.Diagnostics;
using System.Drawing.Drawing2D;
using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Business.Reporting;
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
    private readonly RevenueColumnChartControl revenueColumnChart = new() { Dock = DockStyle.Fill };
    private readonly OrderDonutChartControl orderDonutChart = new() { Dock = DockStyle.Fill };
    private readonly DataGridView recentGrid = ReportingUi.CompactGrid();
    private readonly DataGridView bestGrid = ReportingUi.CompactGrid();
    private readonly DataGridView stockGrid = ReportingUi.CompactGrid();
    private readonly Panel recentEmpty = CreateEmptyPanel("Chưa có đơn hàng gần đây.", "orders");
    private readonly Panel bestEmpty = CreateEmptyPanel("Chưa có dữ liệu bán hàng.", "reports");
    private readonly Panel stockEmpty = CreateEmptyPanel("Không có sản phẩm sắp hết hàng.", "products");
    private readonly Panel stockCard;
    private readonly Panel[] kpiCards;
    private readonly Panel revenueChartCard;
    private readonly Panel donutChartCard;
    private readonly Panel recentOrdersCard;
    private readonly Panel bestSellersCard;
    private readonly TableLayoutPanel cardsLayout;
    private readonly TableLayoutPanel chartsLayout;
    private readonly TableLayoutPanel tablesLayout;
    private readonly Label status = AdminTheme.CreateLabel("");
    private readonly Panel errorBanner;
    private readonly Label errorBannerLabel = AdminTheme.CreateLabel("", 9f, true);
    private readonly Button refresh = ManagementUi.CreateButton("Làm mới", true);

    // Bộ lọc biểu đồ cột doanh thu
    private readonly Button btnToggleRevenueFilter = CreateFilterToggleButton();
    private readonly Panel revenueFilterPanel = new()
    {
        Name = "RevenueFilterPanel",
        Dock = DockStyle.Top,
        AutoSize = false,
        Visible = false,
        Margin = Padding.Empty,
        Padding = new Padding(8, 7, 8, 7),
        BackColor = Color.FromArgb(248, 250, 252)
    };
    private readonly FlowLayoutPanel revenueFilterInputsBar = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        BackColor = Color.FromArgb(248, 250, 252),
        Margin = Padding.Empty,
        Padding = Padding.Empty
    };
    private readonly Button btnQuickWeek = CreateQuickPeriodButton("Tuần");
    private readonly Button btnQuickMonth = CreateQuickPeriodButton("Tháng");
    private readonly Button btnQuickYear = CreateQuickPeriodButton("Năm");
    private readonly Button btnPreviousPeriod = CreatePeriodNavigationButton("chevron-left", "Xem kỳ trước");
    private readonly Button btnNextPeriod = CreatePeriodNavigationButton("chevron-right", "Xem kỳ sau");
    private readonly ToolTip revenueNavigationToolTip = new();
    private readonly CheckBox customRangeToggle = new()
    {
        Name = "RevenueCustomRangeToggle",
        Text = "Khoảng ngày tùy chỉnh",
        AutoSize = true,
        Cursor = Cursors.Hand,
        BackColor = Color.FromArgb(248, 250, 252),
        ForeColor = AdminTheme.Navy,
        Margin = new Padding(0, 5, 10, 3)
    };
    private readonly Label lblMonthCaption = CreateFilterCaption("Tháng:");
    private readonly Label lblYearCaption = CreateFilterCaption("Năm:");
    private readonly Label lblFromDateCaption = CreateFilterCaption("Từ:");
    private readonly Label lblToDateCaption = CreateFilterCaption("Đến:");
    private readonly ComboBox monthCombo = new()
    {
        Name = "RevenueMonthCombo",
        DropDownStyle = ComboBoxStyle.DropDownList
    };
    private readonly ComboBox yearCombo = new()
    {
        Name = "RevenueYearCombo",
        DropDownStyle = ComboBoxStyle.DropDownList
    };
    private readonly DateTimePicker fromDatePicker = new()
    {
        Name = "RevenueFromDatePicker",
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy",
        ShowUpDown = false
    };
    private readonly DateTimePicker toDatePicker = new()
    {
        Name = "RevenueToDatePicker",
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "dd/MM/yyyy",
        ShowUpDown = false
    };
    private readonly Button btnApplyRevenue = CreateCompactActionButton("Áp dụng", true);
    private readonly Button btnResetRevenue = CreateCompactActionButton("Đặt lại", false);
    private readonly Label filterValidationLabel = AdminTheme.CreateLabel("", 8f);
    private readonly Label revenuePeriodLabel = AdminTheme.CreateLabel("", 8.25f, true);
    private readonly Label revenueSummaryLabel = AdminTheme.CreateLabel("", 8.5f, true);

    private RevenueChartPeriod draftPeriod = RevenueChartPeriod.Week;
    private RevenueChartPeriod appliedPeriod = RevenueChartPeriod.Week;
    private RevenueChartPeriod standardPeriod = RevenueChartPeriod.Week;
    private DateOnly appliedAnchorDate;
    private DateOnly appliedFromDate;
    private DateOnly appliedToDate;
    private bool updatingHeights;
    private bool syncingAdvancedFilter;
    private bool hasDashboardLoaded;
    private string? dashboardErrorMessage;
    private string? revenueErrorMessage;
    private int recentCount;
    private int bestCount;
    private int stockCount;
    private bool? currentStacked;
    private bool busy;
    private bool revenueBusy;
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
            RowCount = 7,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        ReportingUi.EnableDoubleBuffering(root);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < root.RowCount; i++)
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        errorBanner = CreateErrorBanner();

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
        // HÀNG 2: 2 BIỂU ĐỒ (Cột doanh thu 55% + Donut 45%)
        // ---------------------------------------------------------------------
        InitializeRevenueFilters();
        chartsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        ReportingUi.EnableDoubleBuffering(chartsLayout);
        revenueChartCard = CreateRevenueChartCard();
        donutChartCard = ReportingUi.CreateDashboardCard("Tỷ lệ trạng thái đơn hàng", orderDonutChart, 330, new Padding(0, 0, 0, 16));
        chartsLayout.Controls.Add(revenueChartCard, 0, 0);
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
        root.Controls.Add(errorBanner, 0, 1);
        root.Controls.Add(cardsLayout, 0, 2);
        root.Controls.Add(chartsLayout, 0, 3);
        root.Controls.Add(tablesLayout, 0, 4);
        root.Controls.Add(stockCard, 0, 5);
        root.Controls.Add(status, 0, 6);
        Controls.Add(root);

        ApplyResponsiveLayout(false);
        ApplyRevenueFilterChromeMetrics();
        UpdateDynamicHeights();

        Resize += (_, _) =>
        {
            ApplyResponsiveLayout(ClientSize.Width > 0 && ClientSize.Width < 850);
            UpdateFilterValidationWrap();
            UpdateDynamicHeights();
        };
        DpiChangedAfterParent += (_, _) =>
        {
            ApplyGridDpiMetrics();
            ApplyRevenueFilterChromeMetrics();
            UpdateDynamicHeights();
        };
        revenueFilterPanel.SizeChanged += (_, _) =>
        {
            if (revenueFilterPanel.Visible)
            {
                UpdateFilterValidationWrap();
                UpdateDynamicHeights();
            }
        };
        refresh.Click += async (_, _) => await ReloadAsync();
        Load += async (_, _) =>
        {
            ApplyGridDpiMetrics();
            ApplyRevenueFilterChromeMetrics();
            ApplyResponsiveLayout(ClientSize.Width > 0 && ClientSize.Width < 850);
            UpdateDynamicHeights();
            await ReloadAsync();
        };
        ResumeLayout(true);
    }

    private sealed record MonthOption(int Month, string Label);

    private void InitializeRevenueFilters()
    {
        var today = ReportingCalendar.Today.ToDateTime(TimeOnly.MinValue);
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);
        var filterFont = AdminTheme.CreateFont(8.5f);

        appliedPeriod = RevenueChartPeriod.Week;
        draftPeriod = RevenueChartPeriod.Week;
        standardPeriod = RevenueChartPeriod.Week;
        appliedAnchorDate = DateOnly.FromDateTime(today);
        appliedFromDate = DateOnly.FromDateTime(firstOfMonth);
        appliedToDate = DateOnly.FromDateTime(today);

        monthCombo.Items.Clear();
        foreach (var m in Enumerable.Range(1, 12))
            monthCombo.Items.Add(new MonthOption(m, $"Thg {m:00}"));
        monthCombo.DisplayMember = nameof(MonthOption.Label);
        monthCombo.Font = filterFont;
        monthCombo.SelectedIndex = today.Month - 1;
        monthCombo.Margin = new Padding(0, 1, 6, 2);

        var minYear = Math.Min(2020, today.Year - 5);
        var maxYear = today.Year + 1;
        yearCombo.Items.Clear();
        for (var y = maxYear; y >= minYear; y--)
            yearCombo.Items.Add(y);
        yearCombo.Font = filterFont;
        yearCombo.SelectedItem = today.Year;
        yearCombo.Margin = new Padding(0, 1, 6, 2);

        fromDatePicker.Font = filterFont;
        fromDatePicker.Value = today;
        fromDatePicker.MinDate = new DateTime(2020, 1, 1);
        fromDatePicker.MaxDate = new DateTime(today.Year + 1, 12, 31);
        fromDatePicker.Margin = new Padding(0, 1, 6, 2);

        toDatePicker.Font = filterFont;
        toDatePicker.Value = today;
        toDatePicker.MinDate = new DateTime(2020, 1, 1);
        toDatePicker.MaxDate = new DateTime(today.Year + 1, 12, 31);
        toDatePicker.Margin = new Padding(0, 1, 6, 2);

        fromDatePicker.ValueChanged += (_, _) => SetFilterValidationError(null);
        toDatePicker.ValueChanged += (_, _) => SetFilterValidationError(null);

        btnQuickWeek.Name = "RevenueQuickWeekButton";
        btnQuickMonth.Name = "RevenueQuickMonthButton";
        btnQuickYear.Name = "RevenueQuickYearButton";
        btnPreviousPeriod.Name = "RevenuePreviousPeriodButton";
        btnNextPeriod.Name = "RevenueNextPeriodButton";
        btnApplyRevenue.Name = "RevenueApplyButton";
        btnResetRevenue.Name = "RevenueResetButton";

        btnToggleRevenueFilter.Click += (_, _) => ToggleRevenueFilterPanel();
        btnQuickWeek.Click += async (_, _) => await ApplyQuickPeriodAsync(RevenueChartPeriod.Week);
        btnQuickMonth.Click += async (_, _) => await ApplyQuickPeriodAsync(RevenueChartPeriod.Month);
        btnQuickYear.Click += async (_, _) => await ApplyQuickPeriodAsync(RevenueChartPeriod.Year);
        btnPreviousPeriod.Click += async (_, _) => await NavigatePeriodAsync(-1);
        btnNextPeriod.Click += async (_, _) => await NavigatePeriodAsync(1);
        customRangeToggle.CheckedChanged += (_, _) =>
        {
            if (syncingAdvancedFilter) return;
            ApplyDraftFilterModeUi(customRangeToggle.Checked ? RevenueChartPeriod.CustomRange : standardPeriod);
            UpdateDynamicHeights();
        };
        btnApplyRevenue.Click += async (_, _) => await ApplyRevenueFilterAsync();
        btnResetRevenue.Click += async (_, _) => await ResetRevenueFilterAsync();

        revenueNavigationToolTip.SetToolTip(btnPreviousPeriod, "Xem kỳ trước");
        revenueNavigationToolTip.SetToolTip(btnNextPeriod, "Xem kỳ sau");

        revenueFilterInputsBar.Controls.AddRange(
        [
            customRangeToggle,
            lblMonthCaption, monthCombo,
            lblYearCaption, yearCombo,
            lblFromDateCaption, fromDatePicker,
            lblToDateCaption, toDatePicker
        ]);

        var actionBar = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };
        btnResetRevenue.Margin = new Padding(0, 1, 4, 2);
        btnApplyRevenue.Margin = new Padding(0, 1, 0, 2);
        actionBar.Controls.AddRange([btnResetRevenue, btnApplyRevenue]);

        var filterRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Color.FromArgb(248, 250, 252),
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterRow.Controls.Add(revenueFilterInputsBar, 0, 0);
        filterRow.Controls.Add(actionBar, 1, 0);

        filterValidationLabel.Name = "RevenueFilterValidationLabel";
        filterValidationLabel.ForeColor = AdminTheme.CancelledText;
        filterValidationLabel.BackColor = Color.FromArgb(248, 250, 252);
        filterValidationLabel.AutoSize = true;
        filterValidationLabel.Margin = new Padding(0, 4, 0, 0);
        filterValidationLabel.Visible = false;

        var filterLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = false,
            BackColor = Color.FromArgb(248, 250, 252),
            RowCount = 2,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        ReportingUi.EnableDoubleBuffering(filterLayout);
        ReportingUi.EnableDoubleBuffering(revenueFilterPanel);
        filterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        filterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        filterLayout.Controls.Add(filterRow, 0, 0);
        filterLayout.Controls.Add(filterValidationLabel, 0, 1);

        revenueFilterPanel.Paint += (_, e) =>
        {
            if (revenueFilterPanel.ClientSize.Width < 4 || revenueFilterPanel.ClientSize.Height < 4) return;
            var rect = new Rectangle(0, 0, revenueFilterPanel.ClientSize.Width - 1, revenueFilterPanel.ClientSize.Height - 1);
            using var path = AdminTheme.CreateRoundedRectanglePath(rect, ScalePx(8));
            using var fill = new SolidBrush(Color.FromArgb(248, 250, 252));
            using var border = new Pen(AdminTheme.Border, 1f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        };
        revenueFilterPanel.Controls.Add(filterLayout);

        ApplyDraftFilterModeUi(RevenueChartPeriod.Week);
        SetFilterPanelExpanded(false);
        StyleQuickPeriodButtons();
        revenuePeriodLabel.Text = FormatAppliedPeriodSummary();
        UpdatePeriodNavigationState();
    }

    private Panel CreateRevenueChartCard()
    {
        var card = new RoundedCardPanel
        {
            Dock = DockStyle.Fill,
            Height = 312,
            Margin = new Padding(0, 0, 12, 16),
            Padding = new Padding(18, 14, 18, 14)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            RowCount = 6,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        ReportingUi.EnableDoubleBuffering(layout);
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var headerRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Color.White,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 2),
            Padding = Padding.Empty
        };
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        headerRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var lblTitle = AdminTheme.CreateLabel("Doanh thu bán hàng", 11.5f, true);
        lblTitle.ForeColor = AdminTheme.Navy;
        lblTitle.AutoSize = true;
        lblTitle.Anchor = AnchorStyles.Left | AnchorStyles.Top;
        lblTitle.Margin = new Padding(0, 2, 8, 0);

        btnToggleRevenueFilter.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        btnToggleRevenueFilter.Margin = Padding.Empty;

        var quickActions = new FlowLayoutPanel
        {
            Name = "RevenueQuickPeriodBar",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.White,
            Margin = new Padding(0, 1, 0, 3),
            Padding = Padding.Empty,
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };
        btnQuickWeek.Margin = new Padding(0, 0, 4, 0);
        btnQuickMonth.Margin = new Padding(0, 0, 4, 0);
        btnQuickYear.Margin = Padding.Empty;
        quickActions.Controls.AddRange([btnQuickWeek, btnQuickMonth, btnQuickYear]);

        headerRow.Controls.Add(lblTitle, 0, 0);
        headerRow.Controls.Add(btnToggleRevenueFilter, 1, 0);

        var metaRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Color.White,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 2, 0, 2),
            Padding = Padding.Empty
        };
        metaRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        metaRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        revenuePeriodLabel.Name = "RevenueChartPeriodLabel";
        revenuePeriodLabel.ForeColor = AdminTheme.Navy;
        revenuePeriodLabel.AutoSize = true;
        revenuePeriodLabel.Anchor = AnchorStyles.Left;
        revenuePeriodLabel.Margin = new Padding(8, 5, 8, 0);

        var periodNavigation = new FlowLayoutPanel
        {
            Name = "RevenuePeriodNavigation",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.White,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Anchor = AnchorStyles.Left | AnchorStyles.Top
        };
        periodNavigation.Controls.AddRange([btnPreviousPeriod, revenuePeriodLabel, btnNextPeriod]);

        revenueSummaryLabel.Name = "RevenueChartPeriodSummary";
        revenueSummaryLabel.ForeColor = AdminTheme.Primary;
        revenueSummaryLabel.AutoSize = true;
        revenueSummaryLabel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        revenueSummaryLabel.Margin = Padding.Empty;
        revenueSummaryLabel.Text = "Đang tải…";

        metaRow.Controls.Add(periodNavigation, 0, 0);
        metaRow.Controls.Add(revenueSummaryLabel, 1, 0);

        var lblBasis = AdminTheme.CreateLabel("Chỉ tính đơn Hoàn thành · theo ngày đặt (OrderDate)", 7.75f);
        lblBasis.Name = "RevenueChartBasisLabel";
        lblBasis.ForeColor = AdminTheme.Secondary;
        lblBasis.AutoSize = true;
        lblBasis.Margin = new Padding(0, 0, 0, 6);

        layout.Controls.Add(headerRow, 0, 0);
        layout.Controls.Add(quickActions, 0, 1);
        layout.Controls.Add(metaRow, 0, 2);
        layout.Controls.Add(lblBasis, 0, 3);
        layout.Controls.Add(revenueFilterPanel, 0, 4);
        layout.Controls.Add(revenueColumnChart, 0, 5);

        card.Controls.Add(layout);
        return card;
    }

    private static Button CreateFilterToggleButton()
    {
        var btn = new Button
        {
            Name = "RevenueFilterToggleButton",
            Text = "Bộ lọc",
            AccessibleName = "Mở bộ lọc doanh thu nâng cao",
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = false,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Font = AdminTheme.CreateFont(8.5f, FontStyle.Bold),
            BackColor = Color.White,
            ForeColor = AdminTheme.Navy
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = AdminTheme.Border;
        btn.Paint += (_, e) =>
        {
            var dpiScale = (btn.DeviceDpi > 0 ? btn.DeviceDpi : 96) / 96.0;
            var padL = Math.Max(8, (int)Math.Round(9 * dpiScale));
            var iconSize = Math.Max(11, (int)Math.Round(13 * dpiScale));
            var iconRect = new Rectangle(padL, (btn.Height - iconSize) / 2, iconSize, iconSize);
            AdminTheme.DrawIcon(e.Graphics, "filter", iconRect, btn.ForeColor);
        };
        return btn;
    }

    private static Button CreateQuickPeriodButton(string text)
    {
        var button = CreateModeButton(text);
        button.MinimumSize = new Size(48, 28);
        button.Padding = new Padding(8, 2, 8, 2);
        button.AccessibleName = $"Xem doanh thu theo {text.ToLowerInvariant()}";
        return button;
    }

    private static Button CreatePeriodNavigationButton(string icon, string accessibleName)
    {
        var button = new Button
        {
            Text = string.Empty,
            AccessibleName = accessibleName,
            Size = new Size(28, 28),
            MinimumSize = new Size(28, 28),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            BackColor = Color.White,
            ForeColor = AdminTheme.Navy,
            TabStop = true
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = AdminTheme.Border;
        button.FlatAppearance.MouseOverBackColor = AdminTheme.PrimaryLight;
        button.Paint += (_, e) =>
        {
            var size = Math.Max(10, Math.Min(14, button.Height / 2));
            var bounds = new Rectangle((button.Width - size) / 2, (button.Height - size) / 2, size, size);
            AdminTheme.DrawIcon(e.Graphics, icon, bounds, button.Enabled ? button.ForeColor : AdminTheme.Muted);
        };
        return button;
    }

    private static Label CreateFilterCaption(string text)
    {
        var label = AdminTheme.CreateLabel(text, 8.25f);
        label.ForeColor = AdminTheme.Secondary;
        label.AutoSize = true;
        label.Margin = new Padding(0, 5, 4, 3);
        return label;
    }

    private static Button CreateModeButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 24),
            Padding = new Padding(5, 2, 5, 2),
            Margin = new Padding(0, 1, 3, 2),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            Font = AdminTheme.CreateFont(7.75f)
        };
        return btn;
    }

    private static Button CreateCompactActionButton(string text, bool primary)
    {
        var btn = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 1, 4, 2)
        };
        AdminTheme.StyleButton(btn, primary, 24);
        btn.Font = AdminTheme.CreateFont(8f, primary ? FontStyle.Bold : FontStyle.Regular);
        btn.Padding = new Padding(8, 2, 8, 2);
        return btn;
    }

    private void ApplyRevenueFilterChromeMetrics()
    {
        var padL = ScalePx(9);
        var iconSize = ScalePx(13);
        var iconGap = ScalePx(6);
        var padR = ScalePx(10);
        var textSz = TextRenderer.MeasureText(
            btnToggleRevenueFilter.Text,
            btnToggleRevenueFilter.Font,
            Size.Empty,
            TextFormatFlags.NoPadding);

        btnToggleRevenueFilter.Size = new Size(padL + iconSize + iconGap + textSz.Width + ScalePx(4) + padR, ScalePx(28));
        btnToggleRevenueFilter.Padding = new Padding(0, 0, padR, 0);

        monthCombo.Width = ScalePx(76);
        yearCombo.Width = ScalePx(68);
        fromDatePicker.Width = ScalePx(104);
        toDatePicker.Width = ScalePx(104);

        var captionTop = ScalePx(4);
        foreach (var cap in new[] { lblMonthCaption, lblYearCaption, lblFromDateCaption, lblToDateCaption })
            cap.Margin = new Padding(0, captionTop, ScalePx(4), ScalePx(2));

        UpdateFilterValidationWrap();
    }

    private void UpdateFilterValidationWrap()
    {
        if (!filterValidationLabel.Visible) return;
        var availW = revenueFilterPanel.ClientSize.Width > 0
            ? revenueFilterPanel.ClientSize.Width - revenueFilterPanel.Padding.Horizontal
            : ScalePx(360);
        var maxW = Math.Max(ScalePx(180), availW);
        if (filterValidationLabel.MaximumSize.Width != maxW)
            filterValidationLabel.MaximumSize = new Size(maxW, 0);
    }

    private void SetFilterValidationError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            filterValidationLabel.Text = "";
            filterValidationLabel.Margin = Padding.Empty;
            filterValidationLabel.Visible = false;
            return;
        }

        filterValidationLabel.Text = message;
        filterValidationLabel.Margin = new Padding(0, 4, 0, 0);
        filterValidationLabel.Visible = true;
        UpdateFilterValidationWrap();
    }

    private void StyleFilterToggleButton(bool active)
    {
        btnToggleRevenueFilter.BackColor = active ? AdminTheme.PrimaryLight : Color.White;
        btnToggleRevenueFilter.ForeColor = active ? AdminTheme.Primary : AdminTheme.Navy;
        btnToggleRevenueFilter.FlatAppearance.BorderColor = active ? AdminTheme.Primary : AdminTheme.Border;
        btnToggleRevenueFilter.FlatAppearance.MouseOverBackColor = active ? AdminTheme.PrimaryLight : AdminTheme.Surface;
        btnToggleRevenueFilter.AccessibleDescription = active
            ? "Bộ lọc doanh thu nâng cao đang mở"
            : "Bộ lọc doanh thu nâng cao đang đóng";
        btnToggleRevenueFilter.Invalidate();
    }

    private void SetFilterPanelExpanded(bool expanded)
    {
        revenueFilterPanel.Margin = expanded ? new Padding(0, 0, 0, 8) : Padding.Empty;
        revenueFilterPanel.Visible = expanded;
        StyleFilterToggleButton(expanded);
        if (!expanded) SetFilterValidationError(null);
    }

    private void ToggleRevenueFilterPanel()
    {
        if (busy || revenueBusy || IsDisposed) return;
        SetFilterValidationError(null);
        var expand = !revenueFilterPanel.Visible;
        if (expand) SyncDraftFilterFromApplied();
        SetFilterPanelExpanded(expand);
        UpdateDynamicHeights();
    }

    private void SyncDraftFilterFromApplied()
    {
        draftPeriod = appliedPeriod;
        if (appliedPeriod != RevenueChartPeriod.CustomRange)
            standardPeriod = appliedPeriod;
        var date = appliedPeriod == RevenueChartPeriod.CustomRange ? appliedFromDate : appliedAnchorDate;
        fromDatePicker.Value = date.ToDateTime(TimeOnly.MinValue);
        toDatePicker.Value = appliedToDate.ToDateTime(TimeOnly.MinValue);
        monthCombo.SelectedIndex = Math.Clamp(appliedAnchorDate.Month - 1, 0, 11);
        yearCombo.SelectedItem = appliedAnchorDate.Year;
        syncingAdvancedFilter = true;
        customRangeToggle.Checked = appliedPeriod == RevenueChartPeriod.CustomRange;
        syncingAdvancedFilter = false;
        ApplyDraftFilterModeUi(appliedPeriod);
    }

    private async Task ApplyQuickPeriodAsync(RevenueChartPeriod period)
    {
        if (busy || revenueBusy || IsDisposed) return;
        var today = ReportingCalendar.Today;
        appliedPeriod = period;
        standardPeriod = period;
        appliedAnchorDate = period switch
        {
            RevenueChartPeriod.Month => new DateOnly(today.Year, today.Month, 1),
            RevenueChartPeriod.Year => new DateOnly(today.Year, 1, 1),
            _ => today
        };
        StyleQuickPeriodButtons();
        SetFilterPanelExpanded(false);
        revenuePeriodLabel.Text = FormatAppliedPeriodSummary();
        UpdatePeriodNavigationState();
        UpdateDynamicHeights();
        await ReloadRevenueOnlyAsync();
    }

    private async Task NavigatePeriodAsync(int direction)
    {
        if (busy || revenueBusy || IsDisposed || appliedPeriod == RevenueChartPeriod.CustomRange) return;
        if (!TryGetShiftedPeriod(direction, out var shifted)) return;

        appliedAnchorDate = shifted;
        SetFilterPanelExpanded(false);
        revenuePeriodLabel.Text = FormatAppliedPeriodSummary();
        UpdatePeriodNavigationState();
        await ReloadRevenueOnlyAsync();
    }

    private bool TryGetShiftedPeriod(int direction, out DateOnly shifted)
    {
        shifted = appliedAnchorDate;
        try
        {
            var candidate = ReportingService.ShiftPeriod(appliedPeriod, appliedAnchorDate, direction);
            var (from, to) = ReportingService.GetPeriodRange(appliedPeriod, candidate);
            var min = new DateOnly(2020, 1, 1);
            var max = new DateOnly(ReportingCalendar.Today.Year + 1, 12, 31);
            if (from < min || to > max) return false;
            shifted = candidate;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private void UpdatePeriodNavigationState()
    {
        var navigable = appliedPeriod is RevenueChartPeriod.Week or RevenueChartPeriod.Month or RevenueChartPeriod.Year;
        btnPreviousPeriod.Visible = navigable;
        btnNextPeriod.Visible = navigable;
        btnPreviousPeriod.Enabled = navigable && !busy && !revenueBusy && TryGetShiftedPeriod(-1, out _);
        btnNextPeriod.Enabled = navigable && !busy && !revenueBusy && TryGetShiftedPeriod(1, out _);
        revenueNavigationToolTip.SetToolTip(btnPreviousPeriod,
            btnPreviousPeriod.Enabled ? "Xem kỳ trước" : "Đã tới giới hạn thời gian trước");
        revenueNavigationToolTip.SetToolTip(btnNextPeriod,
            btnNextPeriod.Enabled ? "Xem kỳ sau" : "Đã tới giới hạn thời gian sau");
    }

    private void ApplyDraftFilterModeUi(RevenueChartPeriod period)
    {
        draftPeriod = period;

        var showWeek = period == RevenueChartPeriod.Week;
        var showMonth = period == RevenueChartPeriod.Month;
        var showYear = period is RevenueChartPeriod.Month or RevenueChartPeriod.Year;
        var showRange = period == RevenueChartPeriod.CustomRange;

        lblMonthCaption.Visible = showMonth;
        monthCombo.Visible = showMonth;
        lblYearCaption.Visible = showYear;
        yearCombo.Visible = showYear;
        lblFromDateCaption.Text = showWeek ? "Ngày trong tuần:" : "Từ:";
        lblFromDateCaption.Visible = showWeek || showRange;
        fromDatePicker.Visible = showWeek || showRange;
        lblToDateCaption.Visible = showRange;
        toDatePicker.Visible = showRange;
        customRangeToggle.Text = showRange ? "Khoảng ngày tùy chỉnh" : "Dùng khoảng ngày tùy chỉnh";
    }

    private static void StyleModeButton(Button button, bool active)
    {
        button.BackColor = active ? AdminTheme.PrimaryLight : Color.White;
        button.ForeColor = active ? AdminTheme.Primary : AdminTheme.Navy;
        button.Font = AdminTheme.CreateFont(7.75f, active ? FontStyle.Bold : FontStyle.Regular);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = active ? AdminTheme.Primary : AdminTheme.Border;
        button.FlatAppearance.MouseOverBackColor = active ? AdminTheme.PrimaryLight : AdminTheme.Surface;
    }

    private void StyleQuickPeriodButtons()
    {
        StyleModeButton(btnQuickWeek, appliedPeriod == RevenueChartPeriod.Week);
        StyleModeButton(btnQuickMonth, appliedPeriod == RevenueChartPeriod.Month);
        StyleModeButton(btnQuickYear, appliedPeriod == RevenueChartPeriod.Year);
    }

    private string FormatAppliedPeriodSummary()
    {
        return appliedPeriod switch
        {
            RevenueChartPeriod.Week => WeekSummary(appliedAnchorDate),
            RevenueChartPeriod.Month => $"Tháng {appliedAnchorDate:MM/yyyy} · Theo ngày",
            RevenueChartPeriod.Year => $"Năm {appliedAnchorDate:yyyy} · Theo tháng",
            RevenueChartPeriod.CustomRange => $"{appliedFromDate:dd/MM/yyyy} – {appliedToDate:dd/MM/yyyy} · Theo ngày",
            RevenueChartPeriod.Day => $"Ngày {appliedAnchorDate:dd/MM/yyyy} · Theo giờ",
            _ => ""
        };
    }

    private static string WeekSummary(DateOnly anchorDate)
    {
        var (from, to) = ReportingService.CurrentWeek(anchorDate);
        return $"Tuần {from:dd/MM} – {to:dd/MM/yyyy} · Theo ngày";
    }

    private async Task ApplyRevenueFilterAsync()
    {
        if (busy || revenueBusy || IsDisposed) return;

        if (draftPeriod == RevenueChartPeriod.CustomRange)
        {
            var from = DateOnly.FromDateTime(fromDatePicker.Value.Date);
            var to = DateOnly.FromDateTime(toDatePicker.Value.Date);
            if (from > to)
            {
                SetFilterValidationError("Ngày bắt đầu không được lớn hơn ngày kết thúc.");
                UpdateDynamicHeights();
                return;
            }
            if (to.DayNumber - from.DayNumber + 1 > ReportingService.MaxCustomRangeDays)
            {
                SetFilterValidationError($"Khoảng thời gian tối đa {ReportingService.MaxCustomRangeDays} ngày.");
                UpdateDynamicHeights();
                return;
            }

            appliedPeriod = RevenueChartPeriod.CustomRange;
            appliedFromDate = from;
            appliedToDate = to;
        }
        else if (draftPeriod == RevenueChartPeriod.Week)
        {
            appliedPeriod = RevenueChartPeriod.Week;
            standardPeriod = RevenueChartPeriod.Week;
            appliedAnchorDate = DateOnly.FromDateTime(fromDatePicker.Value.Date);
        }
        else if (draftPeriod == RevenueChartPeriod.Year)
        {
            var year = yearCombo.SelectedItem is int y ? y : ReportingCalendar.Today.Year;
            appliedPeriod = RevenueChartPeriod.Year;
            standardPeriod = RevenueChartPeriod.Year;
            appliedAnchorDate = new DateOnly(year, 1, 1);
        }
        else if (draftPeriod == RevenueChartPeriod.Month)
        {
            var year = yearCombo.SelectedItem is int y ? y : ReportingCalendar.Today.Year;
            var month = monthCombo.SelectedItem is MonthOption opt ? opt.Month : ReportingCalendar.Today.Month;
            appliedPeriod = RevenueChartPeriod.Month;
            standardPeriod = RevenueChartPeriod.Month;
            appliedAnchorDate = new DateOnly(year, month, 1);
        }
        else throw new InvalidOperationException("Chế độ lọc doanh thu không hợp lệ.");

        SetFilterValidationError(null);
        SetFilterPanelExpanded(false);
        StyleQuickPeriodButtons();
        revenuePeriodLabel.Text = FormatAppliedPeriodSummary();
        UpdatePeriodNavigationState();
        UpdateDynamicHeights();
        await ReloadRevenueOnlyAsync();
    }

    private async Task ResetRevenueFilterAsync()
    {
        if (busy || revenueBusy || IsDisposed) return;
        var today = ReportingCalendar.Today.ToDateTime(TimeOnly.MinValue);
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);

        monthCombo.SelectedIndex = today.Month - 1;
        yearCombo.SelectedItem = today.Year;
        fromDatePicker.Value = today;
        toDatePicker.Value = today;

        appliedPeriod = RevenueChartPeriod.Week;
        standardPeriod = RevenueChartPeriod.Week;
        appliedAnchorDate = DateOnly.FromDateTime(today);
        appliedFromDate = DateOnly.FromDateTime(firstOfMonth);
        appliedToDate = DateOnly.FromDateTime(today);

        ApplyDraftFilterModeUi(RevenueChartPeriod.Week);
        syncingAdvancedFilter = true;
        customRangeToggle.Checked = false;
        syncingAdvancedFilter = false;
        SetFilterValidationError(null);
        SetFilterPanelExpanded(false);
        StyleQuickPeriodButtons();
        revenuePeriodLabel.Text = FormatAppliedPeriodSummary();
        UpdatePeriodNavigationState();
        UpdateDynamicHeights();
        await ReloadRevenueOnlyAsync();
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

        ConfigureTwoCardRow(chartsLayout, revenueChartCard, donutChartCard, stacked);
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

    private Panel CreateErrorBanner()
    {
        var panel = new BufferedPanel
        {
            Name = "DashboardErrorBanner",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AdminTheme.Surface,
            Padding = new Padding(14, 10, 14, 10),
            Margin = Padding.Empty,
            Visible = false
        };

        panel.Paint += (_, e) =>
        {
            if (panel.ClientSize.Width < 4 || panel.ClientSize.Height < 4) return;
            var rect = new Rectangle(0, 0, panel.ClientSize.Width - 1, panel.ClientSize.Height - 1);
            using var path = AdminTheme.CreateRoundedRectanglePath(rect, ScalePx(8));
            using var fill = new SolidBrush(Color.FromArgb(254, 242, 242));
            using var border = new Pen(Color.FromArgb(254, 202, 202), 1f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);
        };

        errorBannerLabel.Name = "DashboardErrorBannerLabel";
        errorBannerLabel.ForeColor = Color.FromArgb(185, 28, 28);
        errorBannerLabel.BackColor = Color.FromArgb(254, 242, 242);
        errorBannerLabel.AutoSize = true;
        errorBannerLabel.Dock = DockStyle.Fill;
        errorBannerLabel.Margin = Padding.Empty;
        errorBannerLabel.UseMnemonic = false;

        panel.Controls.Add(errorBannerLabel);
        panel.SizeChanged += (_, _) => UpdateErrorBannerWrap(panel);
        return panel;
    }

    private void UpdateErrorBannerWrap(Panel panel)
    {
        var width = panel.ClientSize.Width > 0
            ? panel.ClientSize.Width
            : Math.Max(240, ClientSize.Width - Padding.Horizontal);
        var maxW = Math.Max(120, width - panel.Padding.Horizontal);
        if (errorBannerLabel.MaximumSize.Width != maxW)
            errorBannerLabel.MaximumSize = new Size(maxW, 0);
    }

    private void SetErrorBanner(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            errorBannerLabel.Text = "";
            errorBanner.Visible = false;
            errorBanner.Margin = Padding.Empty;
            return;
        }

        errorBannerLabel.Text = message;
        UpdateErrorBannerWrap(errorBanner);
        errorBanner.Margin = new Padding(0, 0, 0, 12);
        errorBanner.Visible = true;
    }

    private string? BuildCombinedErrorBanner()
    {
        if (dashboardErrorMessage != null && revenueErrorMessage != null)
            return $"Không thể tải dữ liệu Dashboard và biểu đồ doanh thu. {dashboardErrorMessage}";
        if (dashboardErrorMessage != null)
            return $"Không thể tải một số liệu KPI/bảng tổng quan. {dashboardErrorMessage}";
        if (revenueErrorMessage != null)
            return $"Không thể tải biểu đồ doanh thu (KPI và bảng tổng quan vẫn hiển thị bình thường). {revenueErrorMessage}";
        return null;
    }

    private void UpdateStatusAndBannerAfterLoad(bool revenueOnly = false, RevenueSeriesSnapshot? series = null)
    {
        var combined = BuildCombinedErrorBanner();
        SetErrorBanner(combined);

        if (combined == null)
        {
            status.ForeColor = AdminTheme.Muted;
            status.Text = revenueOnly && series != null
                ? $"Đã lọc doanh thu ({series.FromDate:dd/MM/yyyy} – {series.ToDate:dd/MM/yyyy}) lúc {DateTime.Now:HH:mm:ss} · PhoneStoreDB"
                : "Đã cập nhật lúc " + DateTime.Now.ToString("HH:mm:ss") + " · PhoneStoreDB";
        }
        else
        {
            status.ForeColor = AdminTheme.CancelledText;
            status.Text = combined;
        }

        UpdateDynamicHeights();
    }

    private async Task ReloadAsync()
    {
        if (busy || IsDisposed) return;
        SetBusy(true);
        status.ForeColor = AdminTheme.Muted;
        status.Text = "Đang tải số liệu…";
        dashboardErrorMessage = null;
        revenueErrorMessage = null;
        SetErrorBanner(null);

        if (!hasDashboardLoaded)
        {
            productsDetail.ForeColor = AdminTheme.Secondary;
            customersDetail.ForeColor = AdminTheme.Secondary;
            ordersDetail.ForeColor = AdminTheme.Secondary;
            revenueDetail.ForeColor = AdminTheme.Secondary;
            productsDetail.Text = "Đang tải…";
            customersDetail.Text = "Đang tải…";
            ordersDetail.Text = "Đang tải…";
            revenueDetail.Text = "Đang tải…";
        }

        revenueSummaryLabel.ForeColor = AdminTheme.Secondary;
        revenueSummaryLabel.Text = "Đang tải…";

        try
        {
            DashboardSnapshot? data = null;
            try
            {
                data = await gateway.WithReportingAsync(service => service.GetDashboardAsync(lifetime.Token));
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogReportingError(nameof(ReloadAsync) + ".Dashboard", ex);
                dashboardErrorMessage = ManagementUi.ErrorMessage(ex);
                if (!IsDisposed) ShowDashboardLoadFailure();
            }

            if (IsDisposed || lifetime.IsCancellationRequested) return;

            if (data != null)
            {
                BindDashboardSnapshot(data);
            }

            try
            {
                var series = await FetchAppliedRevenueSeriesAsync(lifetime.Token);
                if (IsDisposed || lifetime.IsCancellationRequested) return;

                BindRevenueSeries(series);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogReportingError(nameof(ReloadAsync) + ".Revenue", ex);
                revenueErrorMessage = ManagementUi.ErrorMessage(ex);
                if (!IsDisposed) ShowRevenueLoadFailure();
            }

            if (IsDisposed || lifetime.IsCancellationRequested) return;

            UpdateStatusAndBannerAfterLoad();
        }
        finally
        {
            if (!IsDisposed) SetBusy(false);
        }
    }

    private async Task ReloadRevenueOnlyAsync()
    {
        if (busy || revenueBusy || IsDisposed) return;
        SetRevenueBusy(true);
        status.ForeColor = AdminTheme.Muted;
        status.Text = "Đang lọc doanh thu…";
        revenueErrorMessage = null;
        revenueSummaryLabel.ForeColor = AdminTheme.Secondary;
        revenueSummaryLabel.Text = "Đang tải…";
        SetErrorBanner(BuildCombinedErrorBanner());
        try
        {
            var series = await FetchAppliedRevenueSeriesAsync(lifetime.Token);
            if (IsDisposed || lifetime.IsCancellationRequested) return;

            BindRevenueSeries(series);
            UpdateStatusAndBannerAfterLoad(revenueOnly: true, series: series);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex)
        {
            LogReportingError(nameof(ReloadRevenueOnlyAsync), ex);
            revenueErrorMessage = ManagementUi.ErrorMessage(ex);
            if (!IsDisposed)
            {
                ShowRevenueLoadFailure();
                UpdateStatusAndBannerAfterLoad(revenueOnly: true);
            }
        }
        finally
        {
            if (!IsDisposed) SetRevenueBusy(false);
        }
    }

    private Task<RevenueSeriesSnapshot> FetchAppliedRevenueSeriesAsync(CancellationToken ct)
    {
        if (appliedPeriod == RevenueChartPeriod.CustomRange)
        {
            var from = appliedFromDate;
            var to = appliedToDate;
            return gateway.WithReportingAsync(service => service.GetRevenueSeriesAsync(from, to, ct));
        }

        var period = appliedPeriod;
        var anchor = appliedAnchorDate;
        return gateway.WithReportingAsync(service => service.GetRevenueSeriesAsync(period, anchor, ct));
    }

    private void BindDashboardSnapshot(DashboardSnapshot data)
    {
        productsDetail.ForeColor = AdminTheme.Secondary;
        customersDetail.ForeColor = AdminTheme.Secondary;
        ordersDetail.ForeColor = AdminTheme.Secondary;
        revenueDetail.ForeColor = AdminTheme.Secondary;

        productsValue.Text = ReportingUi.Number(data.ActiveProducts);
        productsDetail.Text = $"{ReportingUi.Number(data.TotalStock)} sản phẩm trong kho";

        customersValue.Text = ReportingUi.Number(data.Customers);
        customersDetail.Text = $"{ReportingUi.Number(data.ActiveCustomers)} đang hoạt động";

        ordersValue.Text = ReportingUi.Number(data.Orders);
        ordersDetail.Text = $"{ReportingUi.Number(data.PendingOrders)} chờ xác nhận";

        revenueValue.Text = ReportingUi.Currency(data.CompletedRevenue);
        revenueDetail.Text = $"{ReportingUi.Number(data.CompletedOrders)} đơn hoàn thành";

        orderDonutChart.SetData(data.OrderStatuses);

        var recentRows = data.RecentOrders.Take(5).ToList();
        recentCount = recentRows.Count;
        bestCount = data.BestSellers.Count;
        stockCount = data.LowStockProducts.Count;

        Bind(recentGrid, recentEmpty, recentRows, "Chưa có đơn hàng gần đây.");
        Bind(bestGrid, bestEmpty, data.BestSellers, "Chưa có dữ liệu bán hàng.");
        Bind(stockGrid, stockEmpty, data.LowStockProducts, "Không có sản phẩm sắp hết hàng.");

        hasDashboardLoaded = true;
        ApplyGridDpiMetrics();
        UpdateDynamicHeights();
    }

    private void ShowDashboardLoadFailure()
    {
        if (hasDashboardLoaded) return;

        productsValue.Text = "—";
        customersValue.Text = "—";
        ordersValue.Text = "—";
        revenueValue.Text = "—";

        productsDetail.ForeColor = AdminTheme.CancelledText;
        customersDetail.ForeColor = AdminTheme.CancelledText;
        ordersDetail.ForeColor = AdminTheme.CancelledText;
        revenueDetail.ForeColor = AdminTheme.CancelledText;

        productsDetail.Text = "Không tải được";
        customersDetail.Text = "Không tải được";
        ordersDetail.Text = "Không tải được";
        revenueDetail.Text = "Không tải được";

        orderDonutChart.SetError("Không thể tải dữ liệu trạng thái đơn hàng.");

        recentCount = 0;
        bestCount = 0;
        stockCount = 0;
        Bind(recentGrid, recentEmpty, Array.Empty<RecentOrderMetric>(), "Không thể tải đơn hàng gần đây.", isError: true);
        Bind(bestGrid, bestEmpty, Array.Empty<BestSellerMetric>(), "Không thể tải sản phẩm bán chạy.", isError: true);
        Bind(stockGrid, stockEmpty, Array.Empty<LowStockMetric>(), "Không thể tải cảnh báo tồn kho.", isError: true);
    }

    private void ShowRevenueLoadFailure()
    {
        revenueSummaryLabel.ForeColor = AdminTheme.CancelledText;
        revenueSummaryLabel.Text = "Không tải được";
        revenueColumnChart.SetError($"Không thể tải biểu đồ doanh thu. {revenueErrorMessage}");
    }

    private static void LogReportingError(string operation, Exception ex)
    {
        Trace.TraceError($"[PhoneStore.Admin][Dashboard][{operation}] {ex}");
    }

    private void BindRevenueSeries(RevenueSeriesSnapshot series)
    {
        var periodLabel = series.Period switch
        {
            RevenueChartPeriod.Week => $"tuần {series.FromDate:dd/MM} – {series.ToDate:dd/MM/yyyy}",
            RevenueChartPeriod.Day => $"ngày {series.FromDate:dd/MM/yyyy}",
            RevenueChartPeriod.Month => $"tháng {series.FromDate:MM/yyyy}",
            RevenueChartPeriod.Year => $"năm {series.FromDate:yyyy}",
            RevenueChartPeriod.CustomRange => $"khoảng {series.FromDate:dd/MM/yyyy} – {series.ToDate:dd/MM/yyyy}",
            _ => ""
        };
        revenuePeriodLabel.Text = series.Period switch
        {
            RevenueChartPeriod.Week => $"Tuần {series.FromDate:dd/MM} – {series.ToDate:dd/MM/yyyy} · Theo ngày",
            RevenueChartPeriod.Month => $"Tháng {series.FromDate:MM/yyyy} · Theo ngày",
            RevenueChartPeriod.Year => $"Năm {series.FromDate:yyyy} · Theo tháng",
            RevenueChartPeriod.CustomRange => $"{series.FromDate:dd/MM/yyyy} – {series.ToDate:dd/MM/yyyy} · Theo ngày",
            RevenueChartPeriod.Day => $"Ngày {series.FromDate:dd/MM/yyyy} · Theo giờ",
            _ => FormatAppliedPeriodSummary()
        };
        UpdatePeriodNavigationState();
        revenueSummaryLabel.ForeColor = AdminTheme.Primary;
        revenueSummaryLabel.Text = $"{ReportingUi.Currency(series.TotalRevenue)} · {ReportingUi.Number(series.CompletedOrders)} đơn";
        revenueColumnChart.SetData(series.Buckets, $"Không có doanh thu hoàn thành trong {periodLabel}.");
    }

    private void ConfigureRecentGrid()
    {
        recentGrid.Name = "DashboardRecentOrdersGrid";
        recentGrid.ScrollBars = ScrollBars.Horizontal;
        recentGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        recentGrid.DefaultCellStyle.Padding = new Padding(3, 4, 3, 4);
        recentGrid.ColumnHeadersDefaultCellStyle.Padding = new Padding(3, 4, 3, 4);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.OrderCode), "Mã đơn", 72);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.ReceiverName), "Người nhận", 72);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.TotalAmount), "Tổng tiền", 98, "#,##0 ₫");
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.StatusDisplay), "Trạng thái", 98);
        ManagementUi.AddColumn(recentGrid, nameof(RecentOrderMetric.OrderDate), "Ngày đặt", 82, "dd/MM HH:mm");

        var codeCol = recentGrid.Columns[nameof(RecentOrderMetric.OrderCode)]!;
        codeCol.FillWeight = 15;
        codeCol.MinimumWidth = 56;

        var receiverCol = recentGrid.Columns[nameof(RecentOrderMetric.ReceiverName)]!;
        receiverCol.FillWeight = 16;
        receiverCol.MinimumWidth = 60;

        var totalCol = recentGrid.Columns[nameof(RecentOrderMetric.TotalAmount)]!;
        totalCol.FillWeight = 25;
        totalCol.MinimumWidth = 92;
        totalCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        totalCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;

        var statusCol = recentGrid.Columns[nameof(RecentOrderMetric.StatusDisplay)]!;
        statusCol.FillWeight = 24;
        statusCol.MinimumWidth = 94;

        var dateCol = recentGrid.Columns[nameof(RecentOrderMetric.OrderDate)]!;
        dateCol.FillWeight = 20;
        dateCol.MinimumWidth = 78;
        dateCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        dateCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

        // ManagementUi.AddColumn gán Padding(10,...) lên từng cột; thu gọn đồng đều để tránh cắt chữ ở 1200px
        foreach (DataGridViewColumn col in recentGrid.Columns)
        {
            col.DefaultCellStyle.Padding = new Padding(3, 4, 3, 4);
            col.HeaderCell.Style.Padding = new Padding(3, 4, 3, 4);
        }

        // Giữ nguyên mã đơn đầy đủ (không cắt cứng 8 ký tự), kèm tooltip chi tiết
        recentGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == 0 && e.Value is string code)
            {
                recentGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = code;
            }
            else if (e.ColumnIndex == 1 && e.Value is string receiver)
            {
                recentGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = receiver;
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
        bestGrid.ScrollBars = ScrollBars.Horizontal;
        bestGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        bestGrid.DefaultCellStyle.Padding = new Padding(2, 4, 3, 4);
        bestGrid.ColumnHeadersDefaultCellStyle.Padding = new Padding(2, 4, 3, 4);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.Rank), "STT", 40);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.ProductName), "Sản phẩm", 110);
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.QuantitySold), "Đã bán", 68, "N0");
        ManagementUi.AddColumn(bestGrid, nameof(BestSellerMetric.SalesValue), "Doanh thu", 122, "#,##0 ₫");

        var rankCol = bestGrid.Columns[nameof(BestSellerMetric.Rank)]!;
        rankCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        rankCol.Resizable = DataGridViewTriState.False;
        rankCol.Width = 40;
        rankCol.MinimumWidth = 38;
        rankCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        rankCol.DefaultCellStyle.Padding = new Padding(2, 4, 2, 4);
        rankCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        rankCol.HeaderCell.Style.Padding = new Padding(2, 4, 2, 4);

        var prodCol = bestGrid.Columns[nameof(BestSellerMetric.ProductName)]!;
        prodCol.FillWeight = 32;
        prodCol.MinimumWidth = 82;

        var qtyCol = bestGrid.Columns[nameof(BestSellerMetric.QuantitySold)]!;
        qtyCol.FillWeight = 24;
        qtyCol.MinimumWidth = 64;
        qtyCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        qtyCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;

        var valCol = bestGrid.Columns[nameof(BestSellerMetric.SalesValue)]!;
        valCol.FillWeight = 44;
        valCol.MinimumWidth = 114;
        valCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        valCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;

        // Ghi đè padding cột mặc định của AddColumn để giữ đủ chỗ cho "Đã bán", "Doanh thu"
        foreach (DataGridViewColumn col in bestGrid.Columns)
        {
            if (col == rankCol) continue;
            col.DefaultCellStyle.Padding = new Padding(2, 4, 3, 4);
            col.HeaderCell.Style.Padding = new Padding(2, 4, 3, 4);
        }

        bestGrid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 1 && e.Value is string name)
                bestGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = name;
        };

        // Vẽ trực tiếp cột STT không dùng EndEllipsis để tránh lỗi "2...", "3..." ở mọi DPI
        bestGrid.CellPainting += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == 0 && e.Graphics != null)
            {
                e.Paint(e.ClipBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.SelectionBackground | DataGridViewPaintParts.Border);
                var text = Convert.ToString(e.FormattedValue) ?? "";
                var isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                var style = e.CellStyle ?? bestGrid.DefaultCellStyle;
                var fg = isSelected ? style.SelectionForeColor : style.ForeColor;
                var font = style.Font ?? bestGrid.Font;
                TextRenderer.DrawText(
                    e.Graphics,
                    text,
                    font,
                    e.CellBounds,
                    fg,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                e.Handled = true;
            }
        };
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

    private static void Bind<T>(DataGridView grid, Panel empty, IReadOnlyList<T> rows, string text, bool isError = false)
    {
        grid.DataSource = rows.ToList();
        var hasData = rows.Count > 0;
        grid.Visible = hasData;
        empty.Visible = !hasData;
        if (!hasData)
        {
            var lbl = empty.Controls.OfType<Label>().FirstOrDefault();
            if (lbl != null)
            {
                lbl.ForeColor = isError ? AdminTheme.CancelledText : AdminTheme.Muted;
                lbl.Text = text;
            }
            empty.BringToFront();
        }
    }

    private int ScalePx(int basePx)
    {
        var dpi = DeviceDpi > 0 ? DeviceDpi : 96;
        return (int)Math.Round(basePx * (dpi / 96.0));
    }

    private void ApplyGridDpiMetrics()
    {
        var headerH = ScalePx(36);
        var rowH = ScalePx(34);
        foreach (var grid in new[] { recentGrid, bestGrid, stockGrid })
        {
            grid.ColumnHeadersHeight = headerH;
            grid.RowTemplate.Height = rowH;
            foreach (DataGridViewRow row in grid.Rows)
                row.Height = rowH;
        }

        if (bestGrid.Columns[nameof(BestSellerMetric.Rank)] is { } rankCol)
        {
            rankCol.MinimumWidth = ScalePx(38);
            rankCol.Width = ScalePx(40);
        }
    }

    private void UpdateDynamicHeights()
    {
        if (updatingHeights || IsDisposed) return;
        updatingHeights = true;
        try
        {
            var stacked = currentStacked ?? false;

            var filterPanelExtra = 0;
            if (revenueFilterPanel.Visible)
            {
                var panelW = revenueFilterPanel.Width > 0
                    ? revenueFilterPanel.Width
                    : Math.Max(ScalePx(280), revenueChartCard.ClientSize.Width - revenueChartCard.Padding.Horizontal);
                var innerW = Math.Max(ScalePx(200), panelW - revenueFilterPanel.Padding.Horizontal);
                if (filterValidationLabel.Visible && filterValidationLabel.MaximumSize.Width != innerW)
                    filterValidationLabel.MaximumSize = new Size(innerW, 0);
                var filterLayout = revenueFilterPanel.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
                var contentH = filterLayout?.GetPreferredSize(new Size(innerW, 0)).Height
                    ?? revenueFilterInputsBar.GetPreferredSize(new Size(innerW, 0)).Height;
                var valH = filterValidationLabel.Visible
                    ? filterValidationLabel.GetPreferredSize(new Size(innerW, 0)).Height + filterValidationLabel.Margin.Vertical
                    : 0;
                var h = Math.Max(contentH, valH) + revenueFilterPanel.Padding.Vertical;
                if (revenueFilterPanel.Height != h)
                {
                    revenueFilterPanel.Height = h;
                    revenueFilterPanel.Invalidate();
                }
                filterPanelExtra = h + revenueFilterPanel.Margin.Vertical;
            }
            else if (revenueFilterPanel.Height != 0)
            {
                revenueFilterPanel.Height = 0;
            }

            var chartH = ScalePx(312) + filterPanelExtra;
            revenueChartCard.Height = chartH;
            donutChartCard.Height = stacked ? ScalePx(296) : chartH;

            var recentH = ComputeGridCardHeight(recentOrdersCard, recentGrid, recentCount, 5);
            var bestH = ComputeGridCardHeight(bestSellersCard, bestGrid, bestCount, 5);
            if (stacked)
            {
                recentOrdersCard.Height = recentH;
                bestSellersCard.Height = bestH;
            }
            else
            {
                var sharedTableH = Math.Max(recentH, bestH);
                recentOrdersCard.Height = sharedTableH;
                bestSellersCard.Height = sharedTableH;
            }

            stockCard.Height = ComputeGridCardHeight(stockCard, stockGrid, stockCount, 5, ScalePx(96));
        }
        finally
        {
            updatingHeights = false;
        }
    }

    private int ComputeGridCardHeight(Panel card, DataGridView grid, int rowCount, int maxVisibleRows, int? emptyBodyOverride = null)
    {
        var titleHeight = ScalePx(28);
        if (card.Controls.Count > 0 && card.Controls[0] is TableLayoutPanel layout && layout.Controls.Count > 0)
        {
            var titleCtrl = layout.Controls[0];
            if (titleCtrl.Height > 0)
                titleHeight = titleCtrl.Height + titleCtrl.Margin.Vertical;
        }

        var chromeH = card.Padding.Vertical + titleHeight;
        int bodyH;
        if (rowCount <= 0)
        {
            bodyH = emptyBodyOverride ?? ScalePx(104);
        }
        else
        {
            var rowsToShow = Math.Min(rowCount, maxVisibleRows);
            var rowsH = 0;
            for (var i = 0; i < rowsToShow; i++)
                rowsH += i < grid.Rows.Count ? grid.Rows[i].Height : grid.RowTemplate.Height;
            bodyH = grid.ColumnHeadersHeight + rowsH + 4;
        }

        return chromeH + bodyH;
    }

    private void SetBusy(bool value)
    {
        busy = value;
        refresh.Enabled = !value;
        SetRevenueFilterEnabled(!value && !revenueBusy);
        UseWaitCursor = value;
    }

    private void SetRevenueBusy(bool value)
    {
        revenueBusy = value;
        SetRevenueFilterEnabled(!value && !busy);
        UseWaitCursor = value || busy;
    }

    private void SetRevenueFilterEnabled(bool enabled)
    {
        btnToggleRevenueFilter.Enabled = enabled;
        btnQuickWeek.Enabled = enabled;
        btnQuickMonth.Enabled = enabled;
        btnQuickYear.Enabled = enabled;
        customRangeToggle.Enabled = enabled;
        monthCombo.Enabled = enabled;
        yearCombo.Enabled = enabled;
        fromDatePicker.Enabled = enabled;
        toDatePicker.Enabled = enabled;
        btnApplyRevenue.Enabled = enabled;
        btnResetRevenue.Enabled = enabled;
        UpdatePeriodNavigationState();
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
