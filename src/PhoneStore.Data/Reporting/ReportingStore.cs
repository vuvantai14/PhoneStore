using Microsoft.EntityFrameworkCore;
using PhoneStore.Data.Context;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Data.Reporting;

public sealed class ReportingStore(PhoneStoreDbContext db)
{
    private static readonly OrderStatus[] SalesStatuses =
        [OrderStatus.Confirmed, OrderStatus.Shipping, OrderStatus.Completed];

    public async Task<DashboardSnapshot> DashboardAsync(int lowStockThreshold, CancellationToken ct)
    {
        var activeProducts = await db.Products.AsNoTracking().CountAsync(product => product.IsActive, ct);
        var totalStock = await db.Products.AsNoTracking().Where(product => product.IsActive)
            .SumAsync(product => (long)product.StockQuantity, ct);
        var customerQuery = db.Users.AsNoTracking().Where(user => user.Role.RoleName == "Customer");
        var customers = await customerQuery.CountAsync(ct);
        var activeCustomers = await customerQuery.CountAsync(user => user.IsActive, ct);
        var orders = await db.Orders.AsNoTracking().CountAsync(ct);
        var statusCounts = await StatusCounts(db.Orders.AsNoTracking(), ct);
        var completedRevenue = await db.Orders.AsNoTracking().Where(order => order.Status == OrderStatus.Completed)
            .SumAsync(order => (decimal?)order.TotalAmount, ct) ?? 0m;
        var recent = await db.Orders.AsNoTracking().OrderByDescending(order => order.OrderDate)
            .ThenByDescending(order => order.OrderId).Take(8)
            .Select(order => new RecentOrderMetric(order.OrderCode, order.ReceiverName, order.TotalAmount,
                order.Status, order.OrderDate)).ToListAsync(ct);
        var best = await BestSellers(db.OrderDetails.AsNoTracking()
            .Where(detail => SalesStatuses.Contains(detail.Order.Status)), 5, ct);
        var low = await LowStock(lowStockThreshold, 8, ct);
        return new DashboardSnapshot
        {
            ActiveProducts = activeProducts, TotalStock = totalStock,
            Customers = customers, ActiveCustomers = activeCustomers, Orders = orders,
            PendingOrders = statusCounts.FirstOrDefault(item => item.Status == OrderStatus.Pending)?.Count ?? 0,
            CompletedOrders = statusCounts.FirstOrDefault(item => item.Status == OrderStatus.Completed)?.Count ?? 0,
            CompletedRevenue = completedRevenue, OrderStatuses = statusCounts,
            RecentOrders = recent, BestSellers = Rank(best), LowStockProducts = low
        };
    }

    public async Task<ReportSnapshot> ReportAsync(DateOnly fromDate, DateOnly toDate,
        DateTime startUtc, DateTime endExclusiveUtc, int lowStockThreshold, CancellationToken ct)
    {
        var orders = db.Orders.AsNoTracking().Where(order => order.OrderDate >= startUtc && order.OrderDate < endExclusiveUtc);
        var totalOrders = await orders.CountAsync(ct);
        var statusCounts = await StatusCounts(orders, ct);
        var completed = orders.Where(order => order.Status == OrderStatus.Completed);
        var completedOrders = await completed.CountAsync(ct);
        var completedRevenue = await completed.SumAsync(order => (decimal?)order.TotalAmount, ct) ?? 0m;
        var best = await BestSellers(db.OrderDetails.AsNoTracking().Where(detail =>
            detail.Order.OrderDate >= startUtc && detail.Order.OrderDate < endExclusiveUtc
            && SalesStatuses.Contains(detail.Order.Status)), 10, ct);
        var activeProducts = await db.Products.AsNoTracking().CountAsync(product => product.IsActive, ct);
        var totalStock = await db.Products.AsNoTracking().Where(product => product.IsActive)
            .SumAsync(product => (long)product.StockQuantity, ct);
        var outOfStock = await db.Products.AsNoTracking().CountAsync(product => product.IsActive && product.StockQuantity == 0, ct);
        var lowStock = await db.Products.AsNoTracking().CountAsync(product => product.IsActive && product.StockQuantity <= lowStockThreshold, ct);
        var alerts = await LowStock(lowStockThreshold, 50, ct);
        var trendRows = await completed.GroupBy(order => order.OrderDate.Date)
            .Select(group => new { Date = group.Key, Revenue = group.Sum(order => order.TotalAmount) })
            .OrderBy(point => point.Date).ToListAsync(ct);
        var trend = trendRows.Select(point => new RevenuePoint(point.Date, point.Revenue)).ToArray();
        return new ReportSnapshot
        {
            FromDate = fromDate, ToDate = toDate, TotalOrders = totalOrders,
            CompletedOrders = completedOrders, CompletedRevenue = completedRevenue,
            AverageCompletedOrderValue = completedOrders == 0 ? 0m : completedRevenue / completedOrders,
            OrderStatuses = statusCounts, BestSellers = Rank(best), ActiveProducts = activeProducts,
            TotalStock = totalStock, OutOfStockProducts = outOfStock, LowStockProducts = lowStock,
            InventoryAlerts = alerts, RevenueTrend = trend
        };
    }

    private static async Task<IReadOnlyList<OrderStatusMetric>> StatusCounts(IQueryable<PhoneStore.Models.Entities.Order> query, CancellationToken ct)
    {
        var present = await query.GroupBy(order => order.Status)
            .Select(group => new OrderStatusMetric(group.Key, group.Count())).ToListAsync(ct);
        return Enum.GetValues<OrderStatus>().Select(status =>
            present.SingleOrDefault(item => item.Status == status) ?? new OrderStatusMetric(status, 0)).ToArray();
    }

    private static async Task<List<BestSellerMetric>> BestSellers(IQueryable<PhoneStore.Models.Entities.OrderDetail> query,
        int count, CancellationToken ct)
    {
        var rows = await query.GroupBy(detail => new
            {
                detail.ProductId, detail.Product.ProductName, detail.Product.Brand.BrandName
            }).Select(group => new
            {
                group.Key.ProductName,
                group.Key.BrandName,
                QuantitySold = group.Sum(detail => detail.Quantity),
                SalesValue = group.Sum(detail => detail.Quantity * detail.UnitPrice)
            })
            .OrderByDescending(item => item.QuantitySold).ThenByDescending(item => item.SalesValue)
            .ThenBy(item => item.ProductName).Take(count).ToListAsync(ct);
        return rows.Select(item => new BestSellerMetric(0, item.ProductName, item.BrandName,
            item.QuantitySold, item.SalesValue)).ToList();
    }

    private async Task<IReadOnlyList<LowStockMetric>> LowStock(int threshold, int count, CancellationToken ct) =>
        await db.Products.AsNoTracking().Where(product => product.IsActive && product.StockQuantity <= threshold)
            .OrderBy(product => product.StockQuantity).ThenBy(product => product.ProductName).Take(count)
            .Select(product => new LowStockMetric(product.ProductName, product.Brand.BrandName,
                product.Category.CategoryName, product.StockQuantity, product.IsActive)).ToListAsync(ct);

    private static IReadOnlyList<BestSellerMetric> Rank(IEnumerable<BestSellerMetric> rows) =>
        rows.Select((row, index) => row with { Rank = index + 1 }).ToArray();
}
