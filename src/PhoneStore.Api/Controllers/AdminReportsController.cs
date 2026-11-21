using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Reports;
using PhoneStore.Api.Infrastructure;
using PhoneStore.Business.Reporting;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Authorize(Policy = ApiAccess.Admin)]
[Route("api/v1/admin/reports")]
[Route("api/admin/reports")]
[EnableRateLimiting(ApiPolicies.ProtectedRateLimit)]
[RequestTimeout(ApiPolicies.CatalogTimeout)]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(503)]
public sealed class AdminReportsController(IReportingService reports, ReportSectionReader sections) : ControllerBase
{
    [HttpGet("/api/v1/admin/dashboard")]
    [HttpGet("/api/admin/dashboard")]
    [ProducesResponseType<DashboardResponse>(200)]
    public async Task<ActionResult<DashboardResponse>> Dashboard(CancellationToken ct)
    {
        var d = await reports.GetDashboardAsync(ct);
        return new DashboardResponse(
            d.ActiveProducts,
            d.TotalStock,
            d.Customers,
            d.ActiveCustomers,
            d.Orders,
            d.PendingOrders,
            d.CompletedOrders,
            d.CompletedRevenue,
            d.OrderStatuses.Select(s => new StatusCount(s.Status.ToString(), s.Count)).ToArray(),
            d.RecentOrders.Select(o => new DashboardRecentOrder(
                o.OrderCode,
                o.ReceiverName,
                o.TotalAmount,
                o.Status.ToString(),
                DateTime.SpecifyKind(o.OrderDate, DateTimeKind.Utc))).ToArray(),
            d.BestSellers.Select(p => new TopProduct(p.Rank, p.ProductName, p.BrandName, p.QuantitySold, p.SalesValue)).ToArray(),
            d.LowStockProducts.Select(l => new InventoryAlertItem(l.ProductName, l.BrandName, l.CategoryName, l.StockQuantity, l.IsActive)).ToArray());
    }

    [HttpGet("overview")]
    [ProducesResponseType<ReportOverview>(200)]
    public async Task<ActionResult<ReportOverview>> Overview([FromQuery] ReportRange range, CancellationToken ct)
    {
        if (range.IncludeAlerts)
        {
            var full = await reports.GetReportAsync(range.FromDate, range.ToDate, ct);
            if (!full.Succeeded || full.Report is null)
                return Problem(statusCode: 503, detail: full.Error ?? "Unable to load report.");
            var rep = full.Report;
            return new ReportOverview(
                rep.FromDate,
                rep.ToDate,
                rep.TotalOrders,
                rep.CompletedOrders,
                rep.CompletedRevenue,
                rep.AverageCompletedOrderValue,
                rep.OrderStatuses.Select(s => new StatusCount(s.Status.ToString(), s.Count)).ToArray(),
                new CurrentInventory(rep.ActiveProducts, rep.TotalStock, rep.OutOfStockProducts, rep.LowStockProducts),
                rep.InventoryAlerts.Select(l => new InventoryAlertItem(l.ProductName, l.BrandName, l.CategoryName, l.StockQuantity, l.IsActive)).ToArray(),
                rep.RevenueTrend.Select(t => new RevenueTrendPoint(t.Date, t.Revenue)).ToArray());
        }

        var r = await sections.OverviewAsync(range.FromDate, range.ToDate, ct);
        return new ReportOverview(range.FromDate, range.ToDate, r.TotalOrders, r.CompletedOrders, r.CompletedRevenue,
            r.AverageCompletedOrderValue, r.OrderStatuses.Select(s => new StatusCount(s.Status.ToString(), s.Count)).ToArray(),
            new CurrentInventory(r.ActiveProducts, r.TotalStock, r.OutOfStockProducts, r.LowStockProducts));
    }

    [HttpGet("revenue")]
    [ProducesResponseType<RevenueReport>(200)]
    public async Task<ActionResult<RevenueReport>> Revenue([FromQuery] RevenueReportQuery query, CancellationToken ct)
    {
        var effectivePeriod = query.Period ?? RevenueChartPeriod.CustomRange;
        RevenueSeriesSnapshot series;
        if (effectivePeriod == RevenueChartPeriod.CustomRange)
        {
            series = await reports.GetRevenueSeriesAsync(query.FromDate!.Value, query.ToDate!.Value, ct);
        }
        else
        {
            var anchor = query.AnchorDate ?? ReportingCalendar.Today;
            series = await reports.GetRevenueSeriesAsync(effectivePeriod, anchor, ct);
        }

        var days = effectivePeriod is RevenueChartPeriod.CustomRange or RevenueChartPeriod.Week or RevenueChartPeriod.Month
            ? series.Buckets.Select((b, i) => new DailyRevenue(series.FromDate.AddDays(i), b.Revenue, b.CompletedOrders)).ToArray()
            : [];
        var buckets = series.Buckets
            .Select(b => new RevenueBucketItem(b.Key, b.Label, b.TooltipLabel, b.Revenue, b.CompletedOrders))
            .ToArray();

        return new RevenueReport(
            series.FromDate,
            series.ToDate,
            series.TotalRevenue,
            series.CompletedOrders,
            days,
            series.Period.ToString(),
            buckets);
    }

    [HttpGet("top-products")]
    [ProducesResponseType<TopProductsReport>(200)]
    public async Task<ActionResult<TopProductsReport>> TopProducts([FromQuery] ReportRange range, CancellationToken ct)
    {
        var products = await sections.TopProductsAsync(range.FromDate, range.ToDate, ct);
        return new TopProductsReport(range.FromDate, range.ToDate, products.Select(p =>
            new TopProduct(p.Rank, p.ProductName, p.BrandName, p.QuantitySold, p.SalesValue)).ToArray());
    }
}
