using PhoneStore.Models.Enums;

namespace PhoneStore.Models.Reporting;

public sealed record OrderStatusMetric(OrderStatus Status, int Count);
public sealed record RecentOrderMetric(string OrderCode, string ReceiverName, decimal TotalAmount,
    OrderStatus Status, DateTime OrderDate)
{
    public string StatusDisplay => Status switch
    {
        OrderStatus.Pending => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Shipping => "Đang giao",
        OrderStatus.Completed => "Hoàn thành",
        OrderStatus.Cancelled => "Đã hủy",
        _ => Status.ToString()
    };
}
public sealed record BestSellerMetric(int Rank, string ProductName, string BrandName, int QuantitySold,
    decimal SalesValue);
public sealed record LowStockMetric(string ProductName, string BrandName, string CategoryName,
    int StockQuantity, bool IsActive)
{
    public string StockStatus => StockQuantity == 0 ? "Hết hàng" : "Sắp hết";
}
public sealed record RevenuePoint(DateTime Date, decimal Revenue);
public enum RevenueChartPeriod { Day = 0, Month = 1, Year = 2, CustomRange = 3, Week = 4 }
public sealed record RevenueBucket(int Key, string Label, string TooltipLabel, decimal Revenue, int CompletedOrders);

public sealed class RevenueSeriesSnapshot
{
    public RevenueChartPeriod Period { get; init; }
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public decimal TotalRevenue { get; init; }
    public int CompletedOrders { get; init; }
    public IReadOnlyList<RevenueBucket> Buckets { get; init; } = [];
}

public sealed class DashboardSnapshot
{
    public int ActiveProducts { get; init; }
    public long TotalStock { get; init; }
    public int Customers { get; init; }
    public int ActiveCustomers { get; init; }
    public int Orders { get; init; }
    public int PendingOrders { get; init; }
    public int CompletedOrders { get; init; }
    public decimal CompletedRevenue { get; init; }
    public IReadOnlyList<OrderStatusMetric> OrderStatuses { get; init; } = [];
    public IReadOnlyList<RecentOrderMetric> RecentOrders { get; init; } = [];
    public IReadOnlyList<BestSellerMetric> BestSellers { get; init; } = [];
    public IReadOnlyList<LowStockMetric> LowStockProducts { get; init; } = [];
}

public sealed class ReportSnapshot
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public int TotalOrders { get; init; }
    public int CompletedOrders { get; init; }
    public decimal CompletedRevenue { get; init; }
    public decimal AverageCompletedOrderValue { get; init; }
    public IReadOnlyList<OrderStatusMetric> OrderStatuses { get; init; } = [];
    public IReadOnlyList<BestSellerMetric> BestSellers { get; init; } = [];
    public int ActiveProducts { get; init; }
    public long TotalStock { get; init; }
    public int OutOfStockProducts { get; init; }
    public int LowStockProducts { get; init; }
    public IReadOnlyList<LowStockMetric> InventoryAlerts { get; init; } = [];
    public IReadOnlyList<RevenuePoint> RevenueTrend { get; init; } = [];
}

public sealed record ReportLoadResult(bool Succeeded, ReportSnapshot? Report, string? Error)
{
    public static ReportLoadResult Success(ReportSnapshot report) => new(true, report, null);
    public static ReportLoadResult Fail(string error) => new(false, null, error);
}
