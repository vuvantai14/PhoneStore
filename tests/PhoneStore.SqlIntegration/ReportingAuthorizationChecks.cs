using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhoneStore.Business.Authentication;
using PhoneStore.Business.Reporting;
using PhoneStore.Data.Authentication;
using PhoneStore.Data.Reporting;
using PhoneStore.Models.Entities;
using PhoneStore.Models.Enums;

internal static partial class Program
{
    static async Task RunReportingAuthorizationReadOnlyAsync()
    {
        await using var db = Context();
        Check(await db.Database.CanConnectAsync(), "isolated SQL database reachable");

        var service = new ReportingService(new ReportingStore(db));
        var report = await service.GetReportAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2));
        Check(report is { Succeeded: true, Report: not null }, "Bangkok report SQL query translated");

        var range = ReportingCalendar.ToUtcRange(new(2026, 11, 1), new(2026, 11, 2));
        var orders = await db.Orders.AsNoTracking()
            .Where(x => x.OrderDate >= range.StartUtc && x.OrderDate < range.EndExclusiveUtc).ToListAsync();
        var completed = orders.Where(x => x.Status == OrderStatus.Completed).ToArray();
        var expectedTrend = completed.GroupBy(x => TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(x.OrderDate, DateTimeKind.Utc), ReportingCalendar.TimeZone).Date)
            .OrderBy(x => x.Key).Select(x => (x.Key, Revenue: x.Sum(y => y.TotalAmount))).ToArray();
        Check(report.Report!.TotalOrders == orders.Count, "report total matches independent half-open range");
        Check(report.Report.CompletedOrders == completed.Length
            && report.Report.CompletedRevenue == completed.Sum(x => x.TotalAmount),
            "Completed-only revenue excludes other statuses");
        Check(report.Report.RevenueTrend.Select(x => (x.Date, x.Revenue)).SequenceEqual(expectedTrend),
            "SQL revenue trend groups by Bangkok local date without double conversion");

        var empty = await service.GetReportAsync(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1));
        Check(empty is { Succeeded: true, Report.TotalOrders: 0, Report.CompletedOrders: 0,
            Report.CompletedRevenue: 0 } && empty.Report.RevenueTrend.Count == 0,
            "empty SQL range returns zero revenue and empty trend");

        var adminId = await db.Users.AsNoTracking().Where(x => x.Email == "admin@s29-8b1.test")
            .Select(x => x.UserId).SingleAsync();
        var auth = new AdminAuthService(new AuthUserStore(db), new PasswordHasher<User>());
        Check(await auth.IsCurrentAdminAsync(adminId), "fresh authorization accepts active isolated Admin");
        Check(!await auth.IsCurrentAdminAsync(0), "fresh authorization rejects invalid identity");
        Console.WriteLine("PASS S29.8D.4B read-only SQL reporting/authorization; no fixture or data changes.");
    }
}
