using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhoneStore.Business.Reporting;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Api.Contracts.Reports;

public sealed class ReportRange : IValidatableObject
{
    [BindRequired] public DateOnly FromDate { get; init; }
    [BindRequired] public DateOnly ToDate { get; init; }
    public bool IncludeAlerts { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromDate == DateOnly.MinValue || ToDate == DateOnly.MaxValue || FromDate > ToDate ||
            ToDate.DayNumber - FromDate.DayNumber + 1 > ReportingService.MaxCustomRangeDays)
            yield return new ValidationResult("Supply a valid inclusive local date range of at most 366 days.",
                [nameof(FromDate), nameof(ToDate)]);
    }
}

public sealed class RevenueReportQuery : IValidatableObject
{
    public RevenueChartPeriod? Period { get; init; }
    public DateOnly? AnchorDate { get; init; }
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Period.HasValue && !Enum.IsDefined(Period.Value))
        {
            yield return new ValidationResult("Period must be one of: Day, Week, Month, Year, CustomRange.", [nameof(Period)]);
            yield break;
        }

        var effectivePeriod = Period ?? RevenueChartPeriod.CustomRange;
        if (effectivePeriod == RevenueChartPeriod.CustomRange)
        {
            if (!FromDate.HasValue || !ToDate.HasValue)
            {
                yield return new ValidationResult("fromDate and toDate are required for CustomRange revenue reports.",
                    [nameof(FromDate), nameof(ToDate)]);
                yield break;
            }

            var from = FromDate.Value;
            var to = ToDate.Value;
            if (from == DateOnly.MinValue || to == DateOnly.MaxValue || from > to ||
                to.DayNumber - from.DayNumber + 1 > ReportingService.MaxCustomRangeDays)
            {
                yield return new ValidationResult("Supply a valid inclusive local date range of at most 366 days.",
                    [nameof(FromDate), nameof(ToDate)]);
            }
        }
        else if (AnchorDate.HasValue && (AnchorDate.Value == DateOnly.MinValue || AnchorDate.Value == DateOnly.MaxValue))
        {
            yield return new ValidationResult("Supply a valid anchorDate.", [nameof(AnchorDate)]);
        }
    }
}

public sealed record StatusCount(string Status, int Count);
public sealed record CurrentInventory(int ActiveProducts, long TotalStock, int OutOfStockProducts, int LowStockProducts);
public sealed record InventoryAlertItem(string ProductName, string BrandName, string CategoryName, int StockQuantity, bool IsActive);
public sealed record RevenueTrendPoint(DateTime Date, decimal Revenue);

public sealed record ReportOverview(
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalOrders,
    int CompletedOrders,
    decimal CompletedRevenue,
    decimal AverageCompletedOrderValue,
    IReadOnlyList<StatusCount> OrderStatuses,
    CurrentInventory CurrentInventory,
    IReadOnlyList<InventoryAlertItem>? InventoryAlerts = null,
    IReadOnlyList<RevenueTrendPoint>? RevenueTrend = null);

public sealed record DailyRevenue(DateOnly Date, decimal Revenue, int CompletedOrders);
public sealed record RevenueBucketItem(int Key, string Label, string TooltipLabel, decimal Revenue, int CompletedOrders);

public sealed record RevenueReport(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalRevenue,
    int CompletedOrders,
    IReadOnlyList<DailyRevenue> Days,
    string Period = "CustomRange",
    IReadOnlyList<RevenueBucketItem>? Buckets = null);

public sealed record TopProduct(int Rank, string ProductName, string BrandName, int QuantitySold, decimal SalesValue);
public sealed record TopProductsReport(DateOnly FromDate, DateOnly ToDate, IReadOnlyList<TopProduct> Items);

public sealed record DashboardRecentOrder(string OrderCode, string ReceiverName, decimal TotalAmount, string Status, DateTime OrderDate);

public sealed record DashboardResponse(
    int ActiveProducts,
    long TotalStock,
    int Customers,
    int ActiveCustomers,
    int Orders,
    int PendingOrders,
    int CompletedOrders,
    decimal CompletedRevenue,
    IReadOnlyList<StatusCount> OrderStatuses,
    IReadOnlyList<DashboardRecentOrder> RecentOrders,
    IReadOnlyList<TopProduct> BestSellers,
    IReadOnlyList<InventoryAlertItem> LowStockProducts);
