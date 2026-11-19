using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.Api.Authorization;
using PhoneStore.Api.Contracts.Reports;
using PhoneStore.Business.Reporting;

namespace PhoneStore.Api.Controllers;

[ApiController]
[Authorize(Policy = ApiAccess.Admin)]
[Route("api/admin/reports")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(503)]
public sealed class AdminReportsController(IReportingService reports) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<ReportOverview>(200)]
    public async Task<ActionResult<ReportOverview>> Overview([FromQuery] ReportRange range, CancellationToken ct)
    {
        var result = await reports.GetReportAsync(range.FromDate, range.ToDate, ct);
        if (result.Report is not { } r) return Problem(statusCode: 503, detail: "Report data is unavailable.");
        return new ReportOverview(r.FromDate, r.ToDate, r.TotalOrders, r.CompletedOrders, r.CompletedRevenue,
            r.AverageCompletedOrderValue, r.OrderStatuses.Select(s => new StatusCount(s.Status.ToString(), s.Count)).ToArray(),
            new CurrentInventory(r.ActiveProducts, r.TotalStock, r.OutOfStockProducts, r.LowStockProducts));
    }

    [HttpGet("revenue")]
    [ProducesResponseType<RevenueReport>(200)]
    public async Task<ActionResult<RevenueReport>> Revenue([FromQuery] ReportRange range, CancellationToken ct)
    {
        var series = await reports.GetRevenueSeriesAsync(range.FromDate, range.ToDate, ct);
        return new RevenueReport(series.FromDate, series.ToDate, series.TotalRevenue, series.CompletedOrders,
            series.Buckets.Select((b, i) => new DailyRevenue(series.FromDate.AddDays(i), b.Revenue, b.CompletedOrders)).ToArray());
    }

    [HttpGet("top-products")]
    [ProducesResponseType<TopProductsReport>(200)]
    public async Task<ActionResult<TopProductsReport>> TopProducts([FromQuery] ReportRange range, CancellationToken ct)
    {
        var result = await reports.GetReportAsync(range.FromDate, range.ToDate, ct);
        if (result.Report is not { } r) return Problem(statusCode: 503, detail: "Report data is unavailable.");
        return new TopProductsReport(r.FromDate, r.ToDate, r.BestSellers.Select(p =>
            new TopProduct(p.Rank, p.ProductName, p.BrandName, p.QuantitySold, p.SalesValue)).ToArray());
    }
}
