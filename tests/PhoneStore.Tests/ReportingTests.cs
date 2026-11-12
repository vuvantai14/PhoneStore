using PhoneStore.Business.Reporting;
using PhoneStore.Data.Reporting;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Tests;

public sealed class ReportingTests
{
    [Fact]
    public async Task Bangkok_same_day_uses_exact_local_day_as_half_open_utc_range()
    {
        var store = new CapturingStore();
        var result = await new ReportingService(store).GetReportAsync(new(2026, 11, 2), new(2026, 11, 2));

        Assert.True(result.Succeeded);
        Assert.Equal(new DateTime(2026, 11, 1, 17, 0, 0, DateTimeKind.Utc), store.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc), store.EndExclusiveUtc);
        Assert.Equal(420, store.OffsetMinutes);
    }

    [Fact]
    public async Task Bangkok_multi_day_range_is_converted_once_and_keeps_end_exclusive()
    {
        var store = new CapturingStore();
        var result = await new ReportingService(store).GetReportAsync(new(2026, 11, 1), new(2026, 11, 2));

        Assert.True(result.Succeeded);
        Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), store.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc), store.EndExclusiveUtc);
        Assert.Equal(TimeSpan.FromDays(2), store.EndExclusiveUtc - store.StartUtc);
    }

    [Fact]
    public async Task Empty_report_preserves_zero_completed_revenue_and_empty_trend()
    {
        var store = new CapturingStore { Snapshot = new ReportSnapshot() };
        var result = await new ReportingService(store).GetReportAsync(new(2026, 10, 1), new(2026, 10, 1));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Report);
        Assert.Equal(0m, result.Report.CompletedRevenue);
        Assert.Equal(0m, result.Report.AverageCompletedOrderValue);
        Assert.Empty(result.Report.RevenueTrend);
    }

    [Fact]
    public async Task Invalid_ranges_are_rejected_before_store_query()
    {
        var store = new CapturingStore();
        Assert.False((await new ReportingService(store).GetReportAsync(new(2026, 11, 3), new(2026, 11, 2))).Succeeded);
        Assert.False((await new ReportingService(store).GetReportAsync(new(2026, 11, 2), DateOnly.MaxValue)).Succeeded);
        Assert.Equal(0, store.ReportCalls);
    }

    private sealed class CapturingStore : IReportingStore
    {
        public DateTime StartUtc { get; private set; }
        public DateTime EndExclusiveUtc { get; private set; }
        public int OffsetMinutes { get; private set; }
        public int ReportCalls { get; private set; }
        public int BucketCalls { get; private set; }
        public ReportSnapshot Snapshot { get; init; } = new();

        public IReadOnlyList<RevenueBucketAggregate> BucketsToReturn { get; set; } = [];
        public RevenueChartPeriod? LastBucketPeriod { get; private set; }

        public Task<DashboardSnapshot> DashboardAsync(int lowStockThreshold, CancellationToken ct) =>
            Task.FromResult(new DashboardSnapshot());

        public Task<ReportSnapshot> ReportAsync(DateOnly fromDate, DateOnly toDate, DateTime startUtc,
            DateTime endExclusiveUtc, int localOffsetMinutes, int lowStockThreshold, CancellationToken ct)
        {
            ReportCalls++;
            StartUtc = startUtc;
            EndExclusiveUtc = endExclusiveUtc;
            OffsetMinutes = localOffsetMinutes;
            return Task.FromResult(Snapshot);
        }

        public Task<IReadOnlyList<RevenueBucketAggregate>> CompletedRevenueBucketsAsync(RevenueChartPeriod period,
            DateTime startUtc, DateTime endExclusiveUtc, int localOffsetMinutes, CancellationToken ct)
        {
            BucketCalls++;
            LastBucketPeriod = period;
            StartUtc = startUtc;
            EndExclusiveUtc = endExclusiveUtc;
            OffsetMinutes = localOffsetMinutes;
            return Task.FromResult(BucketsToReturn);
        }
    }

    [Fact]
    public async Task Revenue_series_fills_all_buckets_for_day_month_leap_feb_and_year()
    {
        var store = new CapturingStore();
        var service = new ReportingService(store);

        var day = await service.GetRevenueSeriesAsync(RevenueChartPeriod.Day, new(2026, 11, 13));
        Assert.Equal(24, day.Buckets.Count);
        Assert.Equal("00h", day.Buckets[0].Label);
        Assert.Equal("23h", day.Buckets[23].Label);
        Assert.Equal(new DateTime(2026, 11, 12, 17, 0, 0, DateTimeKind.Utc), store.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 13, 17, 0, 0, DateTimeKind.Utc), store.EndExclusiveUtc);
        Assert.Equal(420, store.OffsetMinutes);
        Assert.All(day.Buckets, b => Assert.Equal(0m, b.Revenue));

        var leapFeb = await service.GetRevenueSeriesAsync(RevenueChartPeriod.Month, new(2024, 2, 10));
        Assert.Equal(29, leapFeb.Buckets.Count);
        Assert.Equal("29", leapFeb.Buckets[^1].Label);
        Assert.Equal(new(2024, 2, 1), leapFeb.FromDate);
        Assert.Equal(new(2024, 2, 29), leapFeb.ToDate);

        var normalFeb = await service.GetRevenueSeriesAsync(RevenueChartPeriod.Month, new(2026, 2, 10));
        Assert.Equal(28, normalFeb.Buckets.Count);

        var nov = await service.GetRevenueSeriesAsync(RevenueChartPeriod.Month, new(2026, 11, 10));
        Assert.Equal(30, nov.Buckets.Count);

        var dec = await service.GetRevenueSeriesAsync(RevenueChartPeriod.Month, new(2026, 12, 10));
        Assert.Equal(31, dec.Buckets.Count);

        var year = await service.GetRevenueSeriesAsync(RevenueChartPeriod.Year, new(2026, 11, 13));
        Assert.Equal(12, year.Buckets.Count);
        Assert.Equal("T1", year.Buckets[0].Label);
        Assert.Equal("T12", year.Buckets[11].Label);
        Assert.Equal(new(2026, 1, 1), year.FromDate);
        Assert.Equal(new(2026, 12, 31), year.ToDate);
    }

    [Fact]
    public async Task Weekly_revenue_uses_monday_to_sunday_in_bangkok_and_fills_seven_days()
    {
        var monday = new DateOnly(2026, 11, 9);
        var sunday = new DateOnly(2026, 11, 15);
        var store = new CapturingStore
        {
            BucketsToReturn =
            [
                new RevenueBucketAggregate(ReportingService.ToDateBucketKey(monday), 12_000_000m, 1),
                new RevenueBucketAggregate(ReportingService.ToDateBucketKey(sunday), 8_500_000m, 2)
            ]
        };

        var series = await new ReportingService(store)
            .GetRevenueSeriesAsync(RevenueChartPeriod.Week, new DateOnly(2026, 11, 12));

        Assert.Equal(RevenueChartPeriod.Week, store.LastBucketPeriod);
        Assert.Equal(monday, series.FromDate);
        Assert.Equal(sunday, series.ToDate);
        Assert.Equal(new DateTime(2026, 11, 8, 17, 0, 0, DateTimeKind.Utc), store.StartUtc);
        Assert.Equal(new DateTime(2026, 11, 15, 17, 0, 0, DateTimeKind.Utc), store.EndExclusiveUtc);
        Assert.Equal(["T2", "T3", "T4", "T5", "T6", "T7", "CN"], series.Buckets.Select(x => x.Label));
        Assert.Equal(7, series.Buckets.Count);
        Assert.Equal("T2 · 09/11/2026", series.Buckets[0].TooltipLabel);
        Assert.Equal("CN · 15/11/2026", series.Buckets[^1].TooltipLabel);
        Assert.Equal(20_500_000m, series.TotalRevenue);
        Assert.Equal(3, series.CompletedOrders);
        Assert.Equal(0m, series.Buckets[1].Revenue);
    }

    [Theory]
    [InlineData(2026, 11, 9)]
    [InlineData(2026, 11, 15)]
    public void Weekly_boundaries_are_stable_for_monday_and_sunday(int year, int month, int day)
    {
        var (from, to) = ReportingService.CurrentWeek(new DateOnly(year, month, day));

        Assert.Equal(new DateOnly(2026, 11, 9), from);
        Assert.Equal(new DateOnly(2026, 11, 15), to);
    }

    [Fact]
    public void Period_navigation_handles_week_month_year_boundaries_and_repeated_steps()
    {
        var week = ReportingService.ShiftPeriod(RevenueChartPeriod.Week, new DateOnly(2026, 12, 30), 1);
        Assert.Equal(new DateOnly(2027, 1, 6), week);
        Assert.Equal((new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 10)),
            ReportingService.GetPeriodRange(RevenueChartPeriod.Week, week));

        var month = ReportingService.ShiftPeriod(RevenueChartPeriod.Month, new DateOnly(2026, 12, 1), 1);
        Assert.Equal(new DateOnly(2027, 1, 1), month);
        var previousMonth = ReportingService.ShiftPeriod(RevenueChartPeriod.Month, month, -1);
        Assert.Equal(new DateOnly(2026, 12, 1), previousMonth);

        var year = ReportingService.ShiftPeriod(RevenueChartPeriod.Year, new DateOnly(2026, 1, 1), 1);
        Assert.Equal(new DateOnly(2027, 1, 1), year);

        var repeated = new DateOnly(2026, 11, 12);
        for (var i = 0; i < 12; i++)
            repeated = ReportingService.ShiftPeriod(RevenueChartPeriod.Week, repeated, 1);
        Assert.Equal(new DateOnly(2027, 2, 4), repeated);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ReportingService.ShiftPeriod(RevenueChartPeriod.CustomRange, new DateOnly(2026, 1, 1), 1));
    }

    [Fact]
    public async Task Revenue_series_merges_sparse_aggregates_into_full_buckets_and_totals()
    {
        var store = new CapturingStore
        {
            BucketsToReturn =
            [
                new RevenueBucketAggregate(1, 30_000_000m, 1),
                new RevenueBucketAggregate(14, 15_500_000m, 2)
            ]
        };
        var service = new ReportingService(store);

        var daySeries = await service.GetRevenueSeriesAsync(RevenueChartPeriod.Day, new(2026, 11, 13));
        Assert.Equal(45_500_000m, daySeries.TotalRevenue);
        Assert.Equal(3, daySeries.CompletedOrders);
        Assert.Equal(30_000_000m, daySeries.Buckets[1].Revenue);
        Assert.Equal(1, daySeries.Buckets[1].CompletedOrders);
        Assert.Equal(15_500_000m, daySeries.Buckets[14].Revenue);
        Assert.Equal(2, daySeries.Buckets[14].CompletedOrders);
        Assert.Equal(0m, daySeries.Buckets[0].Revenue);

        store.BucketsToReturn =
        [
            new RevenueBucketAggregate(1, 30_000_000m, 1),
            new RevenueBucketAggregate(11, 15_500_000m, 2)
        ];
        var yearSeries = await service.GetRevenueSeriesAsync(RevenueChartPeriod.Year, new(2026, 11, 13));
        Assert.Equal(45_500_000m, yearSeries.TotalRevenue);
        Assert.Equal(3, yearSeries.CompletedOrders);
        Assert.Equal(30_000_000m, yearSeries.Buckets[0].Revenue);  // key 1 => T1
        Assert.Equal(15_500_000m, yearSeries.Buckets[10].Revenue); // key 11 => T11
    }

    [Fact]
    public async Task Custom_range_fills_every_day_without_dropping_data_and_handles_cross_year_labels()
    {
        var store = new CapturingStore
        {
            BucketsToReturn =
            [
                new RevenueBucketAggregate(ReportingService.ToDateBucketKey(new(2025, 12, 31)), 25_000_000m, 1),
                new RevenueBucketAggregate(ReportingService.ToDateBucketKey(new(2026, 1, 2)), 40_000_000m, 2)
            ]
        };
        var service = new ReportingService(store);

        var crossYear = await service.GetRevenueSeriesAsync(new DateOnly(2025, 12, 30), new DateOnly(2026, 1, 3));
        Assert.Equal(RevenueChartPeriod.CustomRange, crossYear.Period);
        Assert.Equal(RevenueChartPeriod.CustomRange, store.LastBucketPeriod);
        Assert.Equal(new DateTime(2025, 12, 29, 17, 0, 0, DateTimeKind.Utc), store.StartUtc);
        Assert.Equal(new DateTime(2026, 1, 3, 17, 0, 0, DateTimeKind.Utc), store.EndExclusiveUtc);
        Assert.Equal(5, crossYear.Buckets.Count);
        Assert.Equal("30/12/25", crossYear.Buckets[0].Label);
        Assert.Equal("03/01/26", crossYear.Buckets[4].Label);
        Assert.Equal(65_000_000m, crossYear.TotalRevenue);
        Assert.Equal(3, crossYear.CompletedOrders);
        Assert.Equal(25_000_000m, crossYear.Buckets[1].Revenue);
        Assert.Equal(40_000_000m, crossYear.Buckets[3].Revenue);

        store.BucketsToReturn = [];
        var longSameYear = await service.GetRevenueSeriesAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1));
        Assert.Equal(182, longSameYear.Buckets.Count);
        Assert.Equal("01/01", longSameYear.Buckets[0].Label);
        Assert.Equal("01/07", longSameYear.Buckets[^1].Label);
    }

    [Fact]
    public async Task Custom_range_rejects_inverted_or_invalid_dates_before_store_query()
    {
        var store = new CapturingStore();
        var service = new ReportingService(store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetRevenueSeriesAsync(new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 10)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetRevenueSeriesAsync(new DateOnly(2026, 11, 10), DateOnly.MaxValue));
        Assert.Equal(0, store.BucketCalls);
    }

    [Fact]
    public async Task Custom_range_rejects_unbounded_queries_before_store_query()
    {
        var store = new CapturingStore();
        var service = new ReportingService(store);

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.GetRevenueSeriesAsync(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 2)));

        Assert.Contains(ReportingService.MaxCustomRangeDays.ToString(), ex.Message);
        Assert.Equal(0, store.BucketCalls);
    }
}
