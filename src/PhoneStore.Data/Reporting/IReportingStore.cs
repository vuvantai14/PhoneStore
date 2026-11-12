using PhoneStore.Models.Reporting;

namespace PhoneStore.Data.Reporting;

public interface IReportingStore
{
    Task<DashboardSnapshot> DashboardAsync(int lowStockThreshold, CancellationToken ct);
    Task<ReportSnapshot> ReportAsync(DateOnly fromDate, DateOnly toDate, DateTime startUtc,
        DateTime endExclusiveUtc, int localOffsetMinutes, int lowStockThreshold, CancellationToken ct);
    Task<IReadOnlyList<RevenueBucketAggregate>> CompletedRevenueBucketsAsync(RevenueChartPeriod period,
        DateTime startUtc, DateTime endExclusiveUtc, int localOffsetMinutes, CancellationToken ct);
}

public sealed record RevenueBucketAggregate(int Key, decimal Revenue, int CompletedOrders);
