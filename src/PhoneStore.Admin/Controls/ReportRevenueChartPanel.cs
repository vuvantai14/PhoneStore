using PhoneStore.Admin.Management;
using PhoneStore.Admin.Styling;
using PhoneStore.Business.Reporting;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Admin.Controls;

internal sealed class ReportRevenueChartPanel : UserControl
{
    private const int MinimumYear = 2020;
    private readonly CatalogManagementGateway gateway;
    private readonly CancellationTokenSource lifetime = new();
    private readonly RevenueColumnChartControl chart = new() { Dock = DockStyle.Fill };
    private readonly Button week = ModeButton("Tuần");
    private readonly Button month = ModeButton("Tháng");
    private readonly Button year = ModeButton("Năm");
    private readonly Button previous = NavigationButton("chevron-left", "Xem kỳ trước");
    private readonly Button next = NavigationButton("chevron-right", "Xem kỳ sau");
    private readonly Button filter = ManagementUi.CreateButton("Bộ lọc");
    private readonly Button apply = CompactButton("Áp dụng", true);
    private readonly Button reset = CompactButton("Đặt lại", false);
    private readonly Label period = AdminTheme.CreateLabel("", 8.5f, true);
    private readonly Label summary = AdminTheme.CreateLabel("", 8.5f, true);
    private readonly Label validation = AdminTheme.CreateLabel("", 8.25f);
    private readonly Panel advanced = new()
    {
        Dock = DockStyle.Top, AutoSize = true, Visible = false,
        BackColor = Color.FromArgb(248, 250, 252), Padding = new Padding(8)
    };
    private readonly CheckBox custom = new()
    {
        AutoSize = true, Text = "Khoảng ngày tùy chỉnh", Margin = new Padding(0, 7, 10, 0)
    };
    private readonly Label dateLabel = Caption("Ngày trong kỳ:");
    private readonly Label monthLabel = Caption("Tháng:");
    private readonly Label yearLabel = Caption("Năm:");
    private readonly Label toLabel = Caption("Đến:");
    private readonly DateTimePicker from = DatePicker();
    private readonly DateTimePicker to = DatePicker();
    private readonly ComboBox selectedMonth = new() { Width = 82, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox selectedYear = new() { Width = 78, DropDownStyle = ComboBoxStyle.DropDownList };
    private RevenueChartPeriod appliedMode = RevenueChartPeriod.Week;
    private RevenueChartPeriod standardMode = RevenueChartPeriod.Week;
    private DateOnly anchor = ReportingCalendar.Today;
    private DateOnly rangeFrom = ReportingCalendar.Today;
    private DateOnly rangeTo = ReportingCalendar.Today;
    private bool busy;

    public ReportRevenueChartPanel(CatalogManagementGateway gateway)
    {
        this.gateway = gateway;
        Name = nameof(ReportRevenueChartPanel);
        Dock = DockStyle.Fill;
        BackColor = Color.White;
        Padding = new Padding(18, 14, 18, 14);
        Margin = new Padding(0, 0, 0, 14);
        MinimumSize = new Size(500, 360);

        for (var value = 1; value <= 12; value++) selectedMonth.Items.Add($"Tháng {value:00}");
        for (var value = MinimumYear; value <= ReportingCalendar.Today.Year + 1; value++) selectedYear.Items.Add(value);
        selectedMonth.SelectedIndex = anchor.Month - 1;
        selectedYear.SelectedItem = anchor.Year;
        week.Name = "ReportRevenueWeekButton";
        month.Name = "ReportRevenueMonthButton";
        year.Name = "ReportRevenueYearButton";
        previous.Name = "ReportRevenuePreviousButton";
        next.Name = "ReportRevenueNextButton";
        filter.Name = "ReportRevenueFilterButton";
        filter.AccessibleName = "Mở bộ lọc doanh thu nâng cao";
        apply.Name = "ReportRevenueApplyFilterButton";
        reset.Name = "ReportRevenueResetFilterButton";
        custom.Name = "ReportRevenueCustomRangeToggle";
        selectedMonth.Name = "ReportRevenueMonthSelector";
        selectedYear.Name = "ReportRevenueYearSelector";
        from.Name = "ReportRevenueFromDate";
        to.Name = "ReportRevenueToDate";
        chart.Name = "ReportRevenueChart";
        from.Value = anchor.ToDateTime(TimeOnly.MinValue);
        to.Value = from.Value;
        validation.ForeColor = AdminTheme.CancelledText;
        validation.Visible = false;
        filter.MinimumSize = new Size(0, 32);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var index = 0; index < 5; index++) root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(CreateHeader(), 0, 0);
        root.Controls.Add(CreateQuickModes(), 0, 1);
        root.Controls.Add(CreatePeriodRow(), 0, 2);
        var basis = AdminTheme.CreateLabel("Bộ lọc riêng của biểu đồ · Chỉ tính đơn Hoàn thành theo OrderDate · UTC+7", 7.75f);
        basis.ForeColor = AdminTheme.Secondary;
        basis.Margin = new Padding(0, 0, 0, 6);
        root.Controls.Add(basis, 0, 3);
        BuildAdvancedFilter();
        root.Controls.Add(advanced, 0, 4);
        root.Controls.Add(chart, 0, 5);
        Controls.Add(root);

        week.Click += async (_, _) => await ApplyQuickAsync(RevenueChartPeriod.Week);
        month.Click += async (_, _) => await ApplyQuickAsync(RevenueChartPeriod.Month);
        year.Click += async (_, _) => await ApplyQuickAsync(RevenueChartPeriod.Year);
        previous.Click += async (_, _) => await NavigateAsync(-1);
        next.Click += async (_, _) => await NavigateAsync(1);
        filter.Click += (_, _) => ToggleAdvanced();
        custom.CheckedChanged += (_, _) => UpdateAdvancedVisibility();
        apply.Click += async (_, _) => await ApplyAdvancedAsync();
        reset.Click += async (_, _) => await ResetAsync();
        Load += async (_, _) => await ReloadAsync();
        UpdateChrome();
    }

    private Control CreateHeader()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 4)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(AdminTheme.CreateLabel("Doanh thu hoàn thành", 11.5f, true), 0, 0);
        row.Controls.Add(filter, 1, 0);
        return row;
    }

    private Control CreateQuickModes()
    {
        var row = new FlowLayoutPanel
        {
            AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4)
        };
        row.Controls.AddRange([week, month, year]);
        return row;
    }

    private Control CreatePeriodRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Margin = new Padding(0, 0, 0, 4)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var navigation = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        period.Margin = new Padding(8, 6, 8, 0);
        navigation.Controls.AddRange([previous, period, next]);
        summary.ForeColor = AdminTheme.Primary;
        summary.Anchor = AnchorStyles.Right;
        row.Controls.Add(navigation, 0, 0);
        row.Controls.Add(summary, 1, 0);
        return row;
    }

    private void BuildAdvancedFilter()
    {
        var inputs = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, WrapContents = true,
            BackColor = advanced.BackColor, Margin = Padding.Empty
        };
        inputs.Controls.AddRange([
            custom, dateLabel, from, monthLabel, selectedMonth,
            yearLabel, selectedYear, toLabel, to, reset, apply
        ]);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 2,
            BackColor = advanced.BackColor
        };
        layout.Controls.Add(inputs, 0, 0);
        layout.Controls.Add(validation, 0, 1);
        advanced.Controls.Add(layout);
        UpdateAdvancedVisibility();
    }

    private void ToggleAdvanced()
    {
        if (busy) return;
        var show = !advanced.Visible;
        SetAdvancedVisible(show);
        if (show) SyncDraft();
        validation.Visible = false;
    }

    private void SetAdvancedVisible(bool visible)
    {
        advanced.Visible = visible;
        filter.BackColor = visible ? AdminTheme.PrimaryLight : Color.White;
        filter.ForeColor = visible ? AdminTheme.Primary : AdminTheme.Navy;
        filter.FlatAppearance.BorderColor = visible ? AdminTheme.Primary : AdminTheme.Border;
    }

    private void SyncDraft()
    {
        custom.Checked = appliedMode == RevenueChartPeriod.CustomRange;
        var selected = appliedMode == RevenueChartPeriod.CustomRange ? rangeFrom : anchor;
        from.Value = selected.ToDateTime(TimeOnly.MinValue);
        to.Value = rangeTo.ToDateTime(TimeOnly.MinValue);
        selectedMonth.SelectedIndex = anchor.Month - 1;
        selectedYear.SelectedItem = anchor.Year;
        UpdateAdvancedVisibility();
    }

    private void UpdateAdvancedVisibility()
    {
        var mode = custom.Checked ? RevenueChartPeriod.CustomRange : standardMode;
        dateLabel.Text = mode == RevenueChartPeriod.Week ? "Ngày trong tuần:" : "Từ:";
        dateLabel.Visible = mode is RevenueChartPeriod.Week or RevenueChartPeriod.CustomRange;
        from.Visible = dateLabel.Visible;
        monthLabel.Visible = selectedMonth.Visible = mode == RevenueChartPeriod.Month;
        yearLabel.Visible = selectedYear.Visible = mode is RevenueChartPeriod.Month or RevenueChartPeriod.Year;
        toLabel.Visible = to.Visible = mode == RevenueChartPeriod.CustomRange;
    }

    private async Task ApplyQuickAsync(RevenueChartPeriod mode)
    {
        if (busy) return;
        appliedMode = standardMode = mode;
        var today = ReportingCalendar.Today;
        anchor = mode switch
        {
            RevenueChartPeriod.Month => new DateOnly(today.Year, today.Month, 1),
            RevenueChartPeriod.Year => new DateOnly(today.Year, 1, 1),
            _ => today
        };
        SetAdvancedVisible(false);
        await ReloadAsync();
    }

    private async Task NavigateAsync(int direction)
    {
        if (busy || appliedMode == RevenueChartPeriod.CustomRange) return;
        try
        {
            var candidate = ReportingService.ShiftPeriod(appliedMode, anchor, direction);
            var (start, end) = ReportingService.GetPeriodRange(appliedMode, candidate);
            if (start < new DateOnly(MinimumYear, 1, 1) ||
                end > new DateOnly(ReportingCalendar.Today.Year + 1, 12, 31)) return;
            anchor = candidate;
            await ReloadAsync();
        }
        catch (ArgumentOutOfRangeException) { }
    }

    private async Task ApplyAdvancedAsync()
    {
        if (busy) return;
        if (custom.Checked)
        {
            var draftFrom = DateOnly.FromDateTime(from.Value.Date);
            var draftTo = DateOnly.FromDateTime(to.Value.Date);
            var days = draftTo.DayNumber - draftFrom.DayNumber + 1;
            if (draftFrom > draftTo || days > ReportingService.MaxCustomRangeDays)
            {
                validation.Text = draftFrom > draftTo
                    ? "Ngày bắt đầu không được lớn hơn ngày kết thúc."
                    : $"Khoảng thời gian tối đa {ReportingService.MaxCustomRangeDays} ngày.";
                validation.Visible = true;
                return;
            }
            appliedMode = RevenueChartPeriod.CustomRange;
            rangeFrom = draftFrom;
            rangeTo = draftTo;
        }
        else
        {
            appliedMode = standardMode;
            anchor = standardMode switch
            {
                RevenueChartPeriod.Week => DateOnly.FromDateTime(from.Value.Date),
                RevenueChartPeriod.Month => new DateOnly((int)selectedYear.SelectedItem!, selectedMonth.SelectedIndex + 1, 1),
                RevenueChartPeriod.Year => new DateOnly((int)selectedYear.SelectedItem!, 1, 1),
                _ => anchor
            };
        }
        validation.Visible = false;
        SetAdvancedVisible(false);
        await ReloadAsync();
    }

    private async Task ResetAsync()
    {
        if (busy) return;
        appliedMode = standardMode = RevenueChartPeriod.Week;
        anchor = ReportingCalendar.Today;
        rangeFrom = rangeTo = anchor;
        custom.Checked = false;
        SetAdvancedVisible(false);
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (busy || IsDisposed) return;
        SetBusy(true);
        chart.SetLoading();
        summary.Text = "Đang tải…";
        UpdateChrome();
        try
        {
            var series = appliedMode == RevenueChartPeriod.CustomRange
                ? await gateway.WithReportingAsync(service => service.GetRevenueSeriesAsync(rangeFrom, rangeTo, lifetime.Token))
                : await gateway.WithReportingAsync(service => service.GetRevenueSeriesAsync(appliedMode, anchor, lifetime.Token));
            if (IsDisposed || lifetime.IsCancellationRequested) return;
            var (buckets, unit) = PrepareDisplayBuckets(series);
            chart.SetData(buckets, "Không có doanh thu hoàn thành trong kỳ này.");
            period.Text = FormatPeriod(series, unit);
            summary.Text = $"{ReportingUi.Currency(series.TotalRevenue)} · {ReportingUi.Number(series.CompletedOrders)} đơn";
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                chart.SetError(ManagementUi.ErrorMessage(exception));
                summary.Text = "Không thể tải";
            }
        }
        finally
        {
            if (!IsDisposed) { SetBusy(false); UpdateChrome(); }
        }
    }

    private static (IReadOnlyList<RevenueBucket> Buckets, string Unit) PrepareDisplayBuckets(
        RevenueSeriesSnapshot series)
    {
        if (series.Period != RevenueChartPeriod.CustomRange || series.Buckets.Count <= 45)
            return (series.Buckets, series.Period == RevenueChartPeriod.Year ? "Theo tháng" : "Theo ngày");

        static DateOnly DateFromKey(RevenueBucket bucket) =>
            new(bucket.Key / 10000, bucket.Key / 100 % 100, bucket.Key % 100);

        if (series.Buckets.Count <= 180)
        {
            var weekly = series.Buckets
                .GroupBy(bucket => ReportingService.CurrentWeek(DateFromKey(bucket)).FromDate)
                .Select(group =>
                {
                    var visibleFrom = group.Key < series.FromDate ? series.FromDate : group.Key;
                    var weekEnd = group.Key.AddDays(6);
                    var visibleTo = weekEnd > series.ToDate ? series.ToDate : weekEnd;
                    return new RevenueBucket(
                        ReportingService.ToDateBucketKey(group.Key), group.Key.ToString("dd/MM"),
                        $"Tuần {visibleFrom:dd/MM} – {visibleTo:dd/MM/yyyy}",
                        group.Sum(item => item.Revenue), group.Sum(item => item.CompletedOrders));
                })
                .ToArray();
            return (weekly, "Theo tuần");
        }

        var monthly = series.Buckets
            .GroupBy(bucket => (Year: DateFromKey(bucket).Year, Month: DateFromKey(bucket).Month))
            .Select(group => new RevenueBucket(
                group.Key.Year * 100 + group.Key.Month, $"T{group.Key.Month}",
                $"Tháng {group.Key.Month:00}/{group.Key.Year}", group.Sum(item => item.Revenue),
                group.Sum(item => item.CompletedOrders)))
            .ToArray();
        return (monthly, "Theo tháng");
    }

    private static string FormatPeriod(RevenueSeriesSnapshot series, string unit) => series.Period switch
    {
        RevenueChartPeriod.Week => $"Tuần {series.FromDate:dd/MM} – {series.ToDate:dd/MM/yyyy} · {unit}",
        RevenueChartPeriod.Month => $"Tháng {series.FromDate:MM/yyyy} · {unit}",
        RevenueChartPeriod.Year => $"Năm {series.FromDate:yyyy} · {unit}",
        _ => $"{series.FromDate:dd/MM/yyyy} – {series.ToDate:dd/MM/yyyy} · {unit}"
    };

    private void UpdateChrome()
    {
        StyleMode(week, appliedMode == RevenueChartPeriod.Week);
        StyleMode(month, appliedMode == RevenueChartPeriod.Month);
        StyleMode(year, appliedMode == RevenueChartPeriod.Year);
        var navigable = appliedMode != RevenueChartPeriod.CustomRange;
        previous.Visible = next.Visible = navigable;
        foreach (var control in new Control[] { week, month, year, filter, apply, reset })
            control.Enabled = !busy;
        previous.Enabled = !busy && navigable && CanNavigate(-1);
        next.Enabled = !busy && navigable && CanNavigate(1);
        if (string.IsNullOrEmpty(period.Text))
        {
            var (start, end) = ReportingService.GetPeriodRange(RevenueChartPeriod.Week, anchor);
            period.Text = $"Tuần {start:dd/MM} – {end:dd/MM/yyyy} · Theo ngày";
        }
    }

    private bool CanNavigate(int direction)
    {
        if (appliedMode == RevenueChartPeriod.CustomRange) return false;
        try
        {
            var candidate = ReportingService.ShiftPeriod(appliedMode, anchor, direction);
            var (start, end) = ReportingService.GetPeriodRange(appliedMode, candidate);
            return start >= new DateOnly(MinimumYear, 1, 1) &&
                   end <= new DateOnly(ReportingCalendar.Today.Year + 1, 12, 31);
        }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    private void SetBusy(bool value) { busy = value; UseWaitCursor = value; }

    private static Button ModeButton(string text)
    {
        var button = ManagementUi.CreateButton(text);
        button.MinimumSize = new Size(54, 30);
        button.Padding = new Padding(9, 3, 9, 3);
        button.Margin = new Padding(0, 0, 6, 0);
        return button;
    }

    private static void StyleMode(Button button, bool active)
    {
        button.BackColor = active ? AdminTheme.PrimaryLight : Color.White;
        button.ForeColor = active ? AdminTheme.Primary : AdminTheme.Navy;
        button.FlatAppearance.BorderColor = active ? AdminTheme.Primary : AdminTheme.Border;
    }

    private static Button NavigationButton(string icon, string accessibleName)
    {
        var button = new Button
        {
            Size = new Size(30, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.White,
            AccessibleName = accessibleName, Margin = Padding.Empty
        };
        button.FlatAppearance.BorderColor = AdminTheme.Border;
        button.Paint += (_, e) => AdminTheme.DrawIcon(e.Graphics, icon, new Rectangle(8, 8, 14, 14),
            button.Enabled ? AdminTheme.Navy : AdminTheme.Muted);
        return button;
    }

    private static Button CompactButton(string text, bool primary)
    {
        var button = ManagementUi.CreateButton(text, primary);
        button.MinimumSize = new Size(0, 32);
        button.Margin = new Padding(6, 0, 0, 0);
        return button;
    }

    private static Label Caption(string text)
    {
        var label = AdminTheme.CreateLabel(text, 8.25f);
        label.ForeColor = AdminTheme.Secondary;
        label.Margin = new Padding(0, 8, 4, 0);
        return label;
    }

    private static DateTimePicker DatePicker() => new()
    {
        Width = 112, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy",
        Margin = new Padding(0, 3, 10, 0)
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing) { lifetime.Cancel(); lifetime.Dispose(); }
        base.Dispose(disposing);
    }
}
