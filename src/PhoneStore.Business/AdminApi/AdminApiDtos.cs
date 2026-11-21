namespace PhoneStore.Business.AdminApi;

internal sealed record ApiAuthUserDto(int UserId, string FullName, string Email, string Role);

internal sealed record ApiAuthTokenDto(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshExpiresAtUtc,
    ApiAuthUserDto? User);

internal sealed record ApiCurrentUserDto(int UserId, string FullName, string Email, string? Phone, string Role);

internal sealed record ApiLookupItemDto(int Id, string Name, string? Description, bool IsActive, DateTime CreatedAt);
internal sealed record ApiLookupIdDto(int Id);
internal sealed record ApiLookupOptionDto(int Id, string Name, bool IsActive);
internal sealed record ApiProductOptionsDto(IReadOnlyList<ApiLookupOptionDto> Brands, IReadOnlyList<ApiLookupOptionDto> Categories);

internal sealed record ApiProductIdDto(int ProductId);
internal sealed record ApiProductSummaryDto(int ProductId, string ProductName, string BrandName, string CategoryName,
    decimal Price, int StockQuantity, string? RAM, string? Storage, bool IsActive, DateTime? UpdatedAt);
internal sealed record ApiProductPageDto(IReadOnlyList<ApiProductSummaryDto> Items, int TotalCount, int Page, int PageSize);
internal sealed record ApiProductDetailDto(int ProductId, string ProductName, int BrandId, int CategoryId,
    decimal Price, int StockQuantity, int OriginalStockQuantity, bool IsActive, string? RAM, string? Storage,
    string? Chip, string? Screen, string? Camera, string? Battery, string? OperatingSystem, string? Description);

internal sealed record ApiProductImageDto(int ImageId, int ProductId, string ImageUrl, bool IsPrimary, int DisplayOrder);
internal sealed record ApiProductImageUploadDto(int ImageId, int ProductId, string ImageUrl, bool IsPrimary, int DisplayOrder, string? Notice);
internal sealed record ApiProductImageOperationDto(int ImageId, string? Notice);

internal sealed record ApiCustomerSummaryDto(int UserId, string FullName, string Email, string? Phone, bool IsActive, string? Address, DateTime CreatedAt);
internal sealed record ApiCustomerDetailDto(int UserId, string FullName, string Email, string? Phone, string? Address, bool IsActive, DateTime CreatedAt);
internal sealed record ApiCustomerPageDto(IReadOnlyList<ApiCustomerSummaryDto> Items, int TotalCount, int Page, int PageSize);

internal sealed record ApiOrderSummaryDto(int OrderId, string OrderCode, DateTime OrderDate, string Status, decimal TotalAmount,
    string ReceiverName, string? CustomerName, string? CustomerEmail, string? ReceiverPhone, string? StatusLabel);
internal sealed record ApiOrderLineDto(string ProductName, int Quantity, decimal UnitPrice);
internal sealed record ApiOrderDetailDto(ApiOrderSummaryDto Order, string ReceiverPhone, string ShippingAddress, string? Note,
    IReadOnlyList<ApiOrderLineDto> Items, string? CustomerName, string? CustomerEmail, string? StatusLabel, IReadOnlyList<string>? NextStatuses);
internal sealed record ApiOrderPageDto(IReadOnlyList<ApiOrderSummaryDto> Items, int TotalCount, int Page, int PageSize);

internal sealed record ApiStatusCountDto(string Status, int Count);
internal sealed record ApiCurrentInventoryDto(int ActiveProducts, long TotalStock, int OutOfStockProducts, int LowStockProducts);
internal sealed record ApiInventoryAlertDto(string ProductName, string BrandName, string CategoryName, int StockQuantity, bool IsActive);
internal sealed record ApiRevenueTrendDto(DateTime Date, decimal Revenue);
internal sealed record ApiTopProductDto(int Rank, string ProductName, string BrandName, int QuantitySold, decimal SalesValue);
internal sealed record ApiDashboardRecentOrderDto(string OrderCode, string ReceiverName, decimal TotalAmount, string Status, DateTime OrderDate);
internal sealed record ApiDashboardDto(int ActiveProducts, long TotalStock, int Customers, int ActiveCustomers,
    int Orders, int PendingOrders, int CompletedOrders, decimal CompletedRevenue,
    IReadOnlyList<ApiStatusCountDto> OrderStatuses, IReadOnlyList<ApiDashboardRecentOrderDto> RecentOrders,
    IReadOnlyList<ApiTopProductDto> BestSellers, IReadOnlyList<ApiInventoryAlertDto> LowStockProducts);
internal sealed record ApiReportOverviewDto(DateOnly FromDate, DateOnly ToDate, int TotalOrders, int CompletedOrders,
    decimal CompletedRevenue, decimal AverageCompletedOrderValue, IReadOnlyList<ApiStatusCountDto> OrderStatuses,
    ApiCurrentInventoryDto CurrentInventory, IReadOnlyList<ApiInventoryAlertDto>? InventoryAlerts, IReadOnlyList<ApiRevenueTrendDto>? RevenueTrend);
internal sealed record ApiTopProductsReportDto(DateOnly FromDate, DateOnly ToDate, IReadOnlyList<ApiTopProductDto> Items);
internal sealed record ApiRevenueBucketDto(int Key, string Label, string TooltipLabel, decimal Revenue, int CompletedOrders);
internal sealed record ApiRevenueReportDto(DateOnly FromDate, DateOnly ToDate, decimal TotalRevenue, int CompletedOrders,
    string Period, IReadOnlyList<ApiRevenueBucketDto>? Buckets);
