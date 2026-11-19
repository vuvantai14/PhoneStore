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
public sealed class AdminReportsController(IReportingService reports, ReportSectionReader sections) : ControllerBase
{
    [HttpGet("overview")]
    [ProducesResponseType<ReportOverview>(200)]
    public async Task<ActionResult<ReportOverview>> Overview([FromQuery] ReportRange range, CancellationToken ct)
    {
        var r = await sections.OverviewAsync(range.FromDate, range.ToDate, ct);
        return new ReportOverview(range.FromDate, range.ToDate, r.TotalOrders, r.CompletedOrders, r.CompletedRevenue,
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
        var products = await sections.TopProductsAsync(range.FromDate, range.ToDate, ct);
        return new TopProductsReport(range.FromDate, range.ToDate, products.Select(p =>
            new TopProduct(p.Rank, p.ProductName, p.BrandName, p.QuantitySold, p.SalesValue)).ToArray());
    }
}
