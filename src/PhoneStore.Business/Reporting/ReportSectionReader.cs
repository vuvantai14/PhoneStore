using PhoneStore.Data.Reporting;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Business.Reporting;

public sealed class ReportSectionReader(ReportingStore store)
{
    public Task<ReportOverviewMetrics> OverviewAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var range = ReportingCalendar.ToUtcRange(from, to);
        return store.OverviewAsync(range.StartUtc, range.EndExclusiveUtc, ReportingService.LowStockThreshold, ct);
    }

    public Task<IReadOnlyList<BestSellerMetric>> TopProductsAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var range = ReportingCalendar.ToUtcRange(from, to);
        return store.TopProductsAsync(range.StartUtc, range.EndExclusiveUtc, ct);
    }
}
