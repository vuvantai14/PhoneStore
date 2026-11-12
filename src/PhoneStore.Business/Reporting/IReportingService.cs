using PhoneStore.Models.Reporting;

namespace PhoneStore.Business.Reporting;

public interface IReportingService
{
    Task<DashboardSnapshot> GetDashboardAsync(CancellationToken ct = default);
    Task<ReportLoadResult> GetReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
    Task<RevenueSeriesSnapshot> GetRevenueSeriesAsync(RevenueChartPeriod period, DateOnly anchorDate,
        CancellationToken ct = default);
    Task<RevenueSeriesSnapshot> GetRevenueSeriesAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken ct = default);
}
