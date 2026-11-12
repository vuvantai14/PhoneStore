using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Reporting;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Business.Reporting;

public sealed class ReportingService(IReportingStore store) : IReportingService
{
    public const int MaxCustomRangeDays = 366;

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
            var range = ReportingCalendar.ToUtcRange(fromDate, toDate);
            return ReportLoadResult.Success(await store.ReportAsync(fromDate, toDate, range.StartUtc,
                range.EndExclusiveUtc, range.OffsetMinutes, LowStockThreshold, ct));
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException)
        {
            return ReportLoadResult.Fail("Chưa thể tải báo cáo. Vui lòng kiểm tra kết nối dữ liệu và thử lại.");
        }
    }

    public async Task<RevenueSeriesSnapshot> GetRevenueSeriesAsync(RevenueChartPeriod period, DateOnly anchorDate,
        CancellationToken ct = default)
    {
        if (period == RevenueChartPeriod.CustomRange)
            return await GetRevenueSeriesAsync(anchorDate, anchorDate, ct);

        var (fromDate, toDate) = GetPeriodRange(period, anchorDate);

        var range = ReportingCalendar.ToUtcRange(fromDate, toDate);
        var raw = await store.CompletedRevenueBucketsAsync(period, range.StartUtc, range.EndExclusiveUtc,
            range.OffsetMinutes, ct);
        var byKey = raw.ToDictionary(item => item.Key);

        var buckets = BuildZeroFilledBuckets(period, anchorDate, byKey);
        return new RevenueSeriesSnapshot
        {
            Period = period,
            FromDate = fromDate,
            ToDate = toDate,
            TotalRevenue = buckets.Sum(item => item.Revenue),
            CompletedOrders = buckets.Sum(item => item.CompletedOrders),
            Buckets = buckets
        };
    }

    public async Task<RevenueSeriesSnapshot> GetRevenueSeriesAsync(DateOnly fromDate, DateOnly toDate,
        CancellationToken ct = default)
    {
        if (fromDate > toDate)
            throw new ArgumentOutOfRangeException(nameof(fromDate), "Ngày bắt đầu không được lớn hơn ngày kết thúc.");
        if (toDate == DateOnly.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(toDate), "Ngày kết thúc không hợp lệ.");

        var totalDays = toDate.DayNumber - fromDate.DayNumber + 1;
        if (totalDays > MaxCustomRangeDays)
            throw new ArgumentOutOfRangeException(nameof(toDate),
                $"Khoảng thời gian không được vượt quá {MaxCustomRangeDays} ngày.");

        var range = ReportingCalendar.ToUtcRange(fromDate, toDate);
        var raw = await store.CompletedRevenueBucketsAsync(RevenueChartPeriod.CustomRange, range.StartUtc,
            range.EndExclusiveUtc, range.OffsetMinutes, ct);
        var byKey = raw.ToDictionary(item => item.Key);

        var crossYear = fromDate.Year != toDate.Year;
        var buckets = new RevenueBucket[totalDays];
        for (var i = 0; i < totalDays; i++)
        {
            var date = fromDate.AddDays(i);
            var key = ToDateBucketKey(date);
            byKey.TryGetValue(key, out var agg);
            buckets[i] = new RevenueBucket(
                key,
                crossYear ? date.ToString("dd/MM/yy") : date.ToString("dd/MM"),
                $"Ngày {date:dd/MM/yyyy}",
                agg?.Revenue ?? 0m,
                agg?.CompletedOrders ?? 0);
        }

        return new RevenueSeriesSnapshot
        {
            Period = RevenueChartPeriod.CustomRange,
            FromDate = fromDate,
            ToDate = toDate,
            TotalRevenue = buckets.Sum(item => item.Revenue),
            CompletedOrders = buckets.Sum(item => item.CompletedOrders),
            Buckets = buckets
        };
    }

    public static int ToDateBucketKey(DateOnly date) =>
        date.Year * 10000 + date.Month * 100 + date.Day;

    public static (DateOnly FromDate, DateOnly ToDate) CurrentWeek(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        var monday = date.AddDays(-daysSinceMonday);
        return (monday, monday.AddDays(6));
    }

    public static DateOnly ShiftPeriod(RevenueChartPeriod period, DateOnly anchorDate, int direction) =>
        period switch
        {
            RevenueChartPeriod.Week => anchorDate.AddDays(checked(direction * 7)),
            RevenueChartPeriod.Month => anchorDate.AddMonths(direction),
            RevenueChartPeriod.Year => anchorDate.AddYears(direction),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period,
                "Chỉ tuần, tháng và năm hỗ trợ điều hướng kỳ.")
        };

    public static (DateOnly FromDate, DateOnly ToDate) GetPeriodRange(
        RevenueChartPeriod period, DateOnly anchorDate) => period switch
        {
            RevenueChartPeriod.Week => CurrentWeek(anchorDate),
            RevenueChartPeriod.Day => (anchorDate, anchorDate),
            RevenueChartPeriod.Month => (
                new DateOnly(anchorDate.Year, anchorDate.Month, 1),
                new DateOnly(anchorDate.Year, anchorDate.Month, DateTime.DaysInMonth(anchorDate.Year, anchorDate.Month))),
            RevenueChartPeriod.Year => (
                new DateOnly(anchorDate.Year, 1, 1),
                new DateOnly(anchorDate.Year, 12, 31)),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null)
        };

    private static RevenueBucket[] BuildZeroFilledBuckets(RevenueChartPeriod period, DateOnly anchorDate,
        IReadOnlyDictionary<int, RevenueBucketAggregate> byKey)
    {
        return period switch
        {
            RevenueChartPeriod.Week => BuildWeekBuckets(anchorDate, byKey),
            RevenueChartPeriod.Day => Enumerable.Range(0, 24).Select(hour =>
            {
                byKey.TryGetValue(hour, out var agg);
                return new RevenueBucket(
                    hour,
                    $"{hour:00}h",
                    $"{hour:00}:00 – {hour:00}:59 · {anchorDate:dd/MM/yyyy}",
                    agg?.Revenue ?? 0m,
                    agg?.CompletedOrders ?? 0);
            }).ToArray(),

            RevenueChartPeriod.Month => Enumerable.Range(1, DateTime.DaysInMonth(anchorDate.Year, anchorDate.Month))
                .Select(day =>
                {
                    byKey.TryGetValue(day, out var agg);
                    return new RevenueBucket(
                        day,
                        day.ToString(),
                        $"Ngày {day:00}/{anchorDate.Month:00}/{anchorDate.Year}",
                        agg?.Revenue ?? 0m,
                        agg?.CompletedOrders ?? 0);
                }).ToArray(),

            RevenueChartPeriod.Year => Enumerable.Range(1, 12).Select(month =>
            {
                byKey.TryGetValue(month, out var agg);
                return new RevenueBucket(
                    month,
                    $"T{month}",
                    $"Tháng {month:00}/{anchorDate.Year}",
                    agg?.Revenue ?? 0m,
                    agg?.CompletedOrders ?? 0);
            }).ToArray(),

            _ => []
        };
    }

    private static RevenueBucket[] BuildWeekBuckets(DateOnly anchorDate,
        IReadOnlyDictionary<int, RevenueBucketAggregate> byKey)
    {
        var (monday, _) = CurrentWeek(anchorDate);
        string[] labels = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];
        return Enumerable.Range(0, 7).Select(index =>
        {
            var date = monday.AddDays(index);
            var key = ToDateBucketKey(date);
            byKey.TryGetValue(key, out var agg);
            return new RevenueBucket(
                key,
                labels[index],
                $"{labels[index]} · {date:dd/MM/yyyy}",
                agg?.Revenue ?? 0m,
                agg?.CompletedOrders ?? 0);
        }).ToArray();
    }
}
