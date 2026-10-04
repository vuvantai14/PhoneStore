using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Reporting;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Business.Reporting;

public sealed class ReportingService(ReportingStore store) : IReportingService
{
    public const int LowStockThreshold = 5;

    public Task<DashboardSnapshot> GetDashboardAsync(CancellationToken ct = default) =>
        store.DashboardAsync(LowStockThreshold, ct);

    public async Task<ReportLoadResult> GetReportAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken ct = default)
    {
        if (fromDate > toDate) return ReportLoadResult.Fail("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
        if (toDate == DateOnly.MaxValue) return ReportLoadResult.Fail("Ngày kết thúc không hợp lệ.");
        try
        {
            var start = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endExclusive = DateTime.SpecifyKind(toDate.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            return ReportLoadResult.Success(await store.ReportAsync(fromDate, toDate, start, endExclusive,
                LowStockThreshold, ct));
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            return ReportLoadResult.Fail("Chưa thể tải báo cáo. Vui lòng kiểm tra kết nối dữ liệu và thử lại.");
        }
    }
}
