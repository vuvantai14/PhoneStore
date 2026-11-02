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
        public ReportSnapshot Snapshot { get; init; } = new();

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
    }
}
