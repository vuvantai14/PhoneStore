using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PhoneStore.Business.Reporting;

namespace PhoneStore.Api.Contracts.Reports;

public sealed class ReportRange : IValidatableObject
{
    [BindRequired] public DateOnly FromDate { get; init; }
    [BindRequired] public DateOnly ToDate { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromDate == DateOnly.MinValue || ToDate == DateOnly.MaxValue || FromDate > ToDate ||
            ToDate.DayNumber - FromDate.DayNumber + 1 > ReportingService.MaxCustomRangeDays)
            yield return new ValidationResult("Supply a valid inclusive local date range of at most 366 days.",
                [nameof(FromDate), nameof(ToDate)]);
    }
}

public sealed record StatusCount(string Status, int Count);
public sealed record CurrentInventory(int ActiveProducts, long TotalStock, int OutOfStockProducts, int LowStockProducts);
public sealed record ReportOverview(DateOnly FromDate, DateOnly ToDate, int TotalOrders, int CompletedOrders,
    decimal CompletedRevenue, decimal AverageCompletedOrderValue, IReadOnlyList<StatusCount> OrderStatuses,
    CurrentInventory CurrentInventory);
public sealed record DailyRevenue(DateOnly Date, decimal Revenue, int CompletedOrders);
public sealed record RevenueReport(DateOnly FromDate, DateOnly ToDate, decimal TotalRevenue, int CompletedOrders,
    IReadOnlyList<DailyRevenue> Days);
public sealed record TopProduct(int Rank, string ProductName, string BrandName, int QuantitySold, decimal SalesValue);
public sealed record TopProductsReport(DateOnly FromDate, DateOnly ToDate, IReadOnlyList<TopProduct> Items);
