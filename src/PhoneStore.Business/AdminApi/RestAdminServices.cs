using System.Globalization;
using System.Net.Http.Json;
using PhoneStore.Business.Authentication;
using PhoneStore.Business.Management;
using PhoneStore.Business.Reporting;
using PhoneStore.Models.Authentication;
using PhoneStore.Models.Enums;
using PhoneStore.Models.Management;
using PhoneStore.Models.Reporting;

namespace PhoneStore.Business.AdminApi;

public sealed class RestAdminAuthService(AdminApiClient client) : IAdminAuthService, IAdminSessionLogout
{
    public Task<AdminLoginResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        client.LoginAsync(email, password, cancellationToken);

    public Task<bool> IsCurrentAdminAsync(int userId, CancellationToken cancellationToken = default) =>
        client.VerifyCurrentAdminAsync(userId, cancellationToken);

    public Task LogoutAsync(CancellationToken cancellationToken = default) =>
        client.LogoutAsync(cancellationToken);
}

public sealed class RestLookupManagementService(AdminApiClient client) : ILookupManagementService
{
    public async Task<IReadOnlyList<LookupRow>> ListAsync(LookupKind kind, string? search, CancellationToken cancellationToken = default)
    {
        var route = Route(kind);
        if (!string.IsNullOrWhiteSpace(search))
            route += "?search=" + Uri.EscapeDataString(search.Trim());
        var rows = await client.GetAsync<ApiLookupItemDto[]>(route, cancellationToken) ?? [];
        return rows.Select(r => new LookupRow(r.Id, r.Name, r.Description, r.IsActive, r.CreatedAt)).ToArray();
    }

    public Task<ManagementResult> SaveAsync(LookupKind kind, int? id, LookupEdit edit, CancellationToken cancellationToken = default)
    {
        if (edit is null)
            return Task.FromResult(ManagementResult.Fail("Vui lòng nhập thông tin cần lưu."));
        var body = new { name = edit.Name, description = edit.Description, isActive = edit.IsActive };
        if (id.HasValue)
            return client.SendManagementAsync(HttpMethod.Put, $"{Route(kind)}/{id.Value}", body, cancellationToken);

        return client.SendManagementAsync(HttpMethod.Post, Route(kind), body, cancellationToken,
            async (resp, ct) =>
            {
                var created = await resp.Content.ReadFromJsonAsync<ApiLookupIdDto>(AdminApiClient.SerializerOptions, ct);
                return ManagementResult.Success(created?.Id);
            });
    }

    public Task<ManagementResult> SetActiveAsync(LookupKind kind, int id, bool active, CancellationToken cancellationToken = default) =>
        client.SendManagementAsync(HttpMethod.Patch, $"{Route(kind)}/{id}/status", new { isActive = active }, cancellationToken);

    private static string Route(LookupKind kind) => kind switch
    {
        LookupKind.Brand => "api/v1/admin/brands",
        LookupKind.Category => "api/v1/admin/categories",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

public sealed class RestProductManagementService(AdminApiClient client) : IProductManagementService
{
    private const int PageSize = 100;

    public async Task<IReadOnlyList<ProductManagementRow>> ListAsync(ProductManagementQuery query, CancellationToken cancellationToken = default)
    {
        var all = new List<ProductManagementRow>();
        var pageNumber = 1;
        while (true)
        {
            var page = await FetchPageAsync(query, pageNumber, PageSize, cancellationToken);
            all.AddRange(page.Items);
            if (page.Items.Count < PageSize || all.Count >= page.TotalCount)
                break;
            pageNumber++;
        }
        return all;
    }

    public Task<ProductManagementPage> ListPageAsync(ProductManagementQuery query, int offset, int limit, CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, PageSize);
        var pageNumber = Math.Max(1, (offset / safeLimit) + 1);
        return FetchPageAsync(query, pageNumber, safeLimit, cancellationToken);
    }

    private async Task<ProductManagementPage> FetchPageAsync(ProductManagementQuery query, int pageNumber, int pageSize, CancellationToken ct)
    {
        var parts = new List<string> { $"page={pageNumber}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(query.Search)) parts.Add("search=" + Uri.EscapeDataString(query.Search.Trim()));
        if (query.BrandId.HasValue) parts.Add($"brandId={query.BrandId.Value}");
        if (query.CategoryId.HasValue) parts.Add($"categoryId={query.CategoryId.Value}");
        if (query.IsActive.HasValue) parts.Add($"isActive={query.IsActive.Value.ToString().ToLowerInvariant()}");

        var dto = await client.GetAsync<ApiProductPageDto>("api/v1/admin/products?" + string.Join('&', parts), ct);
        if (dto is null) return new ProductManagementPage([], 0);
        var rows = dto.Items.Select(p => new ProductManagementRow(
            p.ProductId, p.ProductName, p.BrandName, p.CategoryName, p.Price,
            p.StockQuantity, p.RAM, p.Storage, p.IsActive, p.UpdatedAt)).ToArray();
        return new ProductManagementPage(rows, dto.TotalCount);
    }

    public async Task<ProductEdit?> GetEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var p = await client.GetAsync<ApiProductDetailDto>($"api/v1/admin/products/{id}", cancellationToken, allowNotFound: true);
        if (p is null) return null;
        return new ProductEdit
        {
            ProductName = p.ProductName,
            BrandId = p.BrandId,
            CategoryId = p.CategoryId,
            Price = p.Price,
            StockQuantity = p.StockQuantity,
            OriginalStockQuantity = p.OriginalStockQuantity,
            RAM = p.RAM,
            Storage = p.Storage,
            Chip = p.Chip,
            Screen = p.Screen,
            Camera = p.Camera,
            Battery = p.Battery,
            OperatingSystem = p.OperatingSystem,
            Description = p.Description,
            IsActive = p.IsActive
        };
    }

    public async Task<ProductEditOptions> GetOptionsAsync(int? productId = null, CancellationToken cancellationToken = default)
    {
        var url = productId.HasValue ? $"api/v1/admin/products/options?productId={productId.Value}" : "api/v1/admin/products/options";
        var dto = await client.GetAsync<ApiProductOptionsDto>(url, cancellationToken);
        return MapOptions(dto);
    }

    public async Task<ProductEditOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var dto = await client.GetAsync<ApiProductOptionsDto>("api/v1/admin/products/options?includeInactive=true", cancellationToken);
        return MapOptions(dto);
    }

    public Task<ManagementResult> SaveAsync(int? id, ProductEdit edit, CancellationToken cancellationToken = default)
    {
        if (edit is null)
            return Task.FromResult(ManagementResult.Fail("Vui lòng nhập thông tin sản phẩm."));

        if (id.HasValue)
        {
            if (edit.OriginalStockQuantity is null or < 0)
                return Task.FromResult(ManagementResult.Fail("Thiếu tồn kho gốc. Vui lòng đóng và mở lại sản phẩm trước khi lưu."));

            var updateBody = new
            {
                productName = edit.ProductName,
                brandId = edit.BrandId,
                categoryId = edit.CategoryId,
                price = edit.Price,
                stockQuantity = edit.StockQuantity,
                originalStockQuantity = edit.OriginalStockQuantity.Value,
                isActive = edit.IsActive,
                ram = edit.RAM,
                storage = edit.Storage,
                chip = edit.Chip,
                screen = edit.Screen,
                camera = edit.Camera,
                battery = edit.Battery,
                operatingSystem = edit.OperatingSystem,
                description = edit.Description
            };
            return client.SendManagementAsync(HttpMethod.Put, $"api/v1/admin/products/{id.Value}", updateBody, cancellationToken);
        }

        var createBody = new
        {
            productName = edit.ProductName,
            brandId = edit.BrandId,
            categoryId = edit.CategoryId,
            price = edit.Price,
            stockQuantity = edit.StockQuantity,
            isActive = edit.IsActive,
            ram = edit.RAM,
            storage = edit.Storage,
            chip = edit.Chip,
            screen = edit.Screen,
            camera = edit.Camera,
            battery = edit.Battery,
            operatingSystem = edit.OperatingSystem,
            description = edit.Description
        };
        return client.SendManagementAsync(HttpMethod.Post, "api/v1/admin/products", createBody, cancellationToken,
            async (resp, ct) =>
            {
                var created = await resp.Content.ReadFromJsonAsync<ApiProductIdDto>(AdminApiClient.SerializerOptions, ct);
                return ManagementResult.Success(created?.ProductId);
            });
    }

    public Task<ManagementResult> SetActiveAsync(int id, bool active, CancellationToken cancellationToken = default) =>
        client.SendManagementAsync(HttpMethod.Patch, $"api/v1/admin/products/{id}/status", new { isActive = active }, cancellationToken);

    public async Task<IReadOnlyList<ProductImageRow>> GetImagesAsync(int productId, CancellationToken cancellationToken = default)
    {
        var rows = await client.GetAsync<ApiProductImageDto[]>($"api/v1/admin/products/{productId}/images", cancellationToken, allowNotFound: true) ?? [];
        return rows.Select(i => new ProductImageRow(i.ImageId, i.ProductId, i.ImageUrl, i.IsPrimary, i.DisplayOrder)).ToArray();
    }

    public Task<ManagementResult> AddImageAsync(int productId, string sourceFile, CancellationToken cancellationToken = default) =>
        client.UploadMultipartAsync($"api/v1/admin/products/{productId}/images", sourceFile, cancellationToken);

    public Task<ManagementResult> RemoveImageAsync(int productId, int imageId, CancellationToken cancellationToken = default) =>
        client.SendManagementAsync(HttpMethod.Delete, $"api/v1/admin/products/{productId}/images/{imageId}", null, cancellationToken,
            async (resp, ct) =>
            {
                var body = await resp.Content.ReadFromJsonAsync<ApiProductImageOperationDto>(AdminApiClient.SerializerOptions, ct);
                return ManagementResult.Success(body?.ImageId ?? imageId, body?.Notice);
            });

    public Task<ManagementResult> SetPrimaryImageAsync(int productId, int imageId, CancellationToken cancellationToken = default) =>
        client.SendManagementAsync(HttpMethod.Patch, $"api/v1/admin/products/{productId}/images/{imageId}/primary", null, cancellationToken);

    public Task<ManagementResult> SetImageOrderAsync(int productId, int imageId, int displayOrder, CancellationToken cancellationToken = default) =>
        client.SendManagementAsync(HttpMethod.Patch, $"api/v1/admin/products/{productId}/images/{imageId}/order",
            new { displayOrder }, cancellationToken);

    private static ProductEditOptions MapOptions(ApiProductOptionsDto? dto) => dto is null
        ? new ProductEditOptions([], [])
        : new ProductEditOptions(
            dto.Brands.Select(b => new LookupOption(b.Id, b.Name, b.IsActive)).ToArray(),
            dto.Categories.Select(c => new LookupOption(c.Id, c.Name, c.IsActive)).ToArray());
}

public sealed class RestCustomerManagementService(AdminApiClient client) : ICustomerManagementService
{
    private const int PageSize = 100;

    public async Task<IReadOnlyList<CustomerManagementRow>> ListAsync(CustomerManagementQuery query, CancellationToken ct = default)
    {
        var all = new List<CustomerManagementRow>();
        var pageNumber = 1;
        while (true)
        {
            var (items, total) = await FetchPageAsync(query, pageNumber, PageSize, ct);
            all.AddRange(items);
            if (items.Count < PageSize || all.Count >= total)
                break;
            pageNumber++;
        }
        return all;
    }

    public Task<(IReadOnlyList<CustomerManagementRow> Items, int Total)> ListPageAsync(
        CustomerManagementQuery query, int offset, int limit, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, PageSize);
        var pageNumber = Math.Max(1, (offset / safeLimit) + 1);
        return FetchPageAsync(query, pageNumber, safeLimit, ct);
    }

    private async Task<(IReadOnlyList<CustomerManagementRow> Items, int Total)> FetchPageAsync(
        CustomerManagementQuery query, int pageNumber, int pageSize, CancellationToken ct)
    {
        var parts = new List<string> { $"page={pageNumber}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(query.Search)) parts.Add("search=" + Uri.EscapeDataString(query.Search.Trim()));
        if (query.IsActive.HasValue) parts.Add($"isActive={query.IsActive.Value.ToString().ToLowerInvariant()}");

        var dto = await client.GetAsync<ApiCustomerPageDto>("api/v1/admin/customers?" + string.Join('&', parts), ct);
        if (dto is null) return ([], 0);
        var rows = dto.Items.Select(c => new CustomerManagementRow(
            c.UserId, c.FullName, c.Email, c.Phone, c.Address, c.IsActive, c.CreatedAt)).ToArray();
        return (rows, dto.TotalCount);
    }

    public async Task<CustomerManagementRow?> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await client.GetAsync<ApiCustomerDetailDto>($"api/v1/admin/customers/{id}", ct, allowNotFound: true);
        return dto is null ? null : new CustomerManagementRow(dto.UserId, dto.FullName, dto.Email, dto.Phone, dto.Address, dto.IsActive, dto.CreatedAt);
    }

    public Task<ManagementResult> UpdateAsync(int id, CustomerEdit edit, CancellationToken ct = default) =>
        client.SendManagementAsync(HttpMethod.Put, $"api/v1/admin/customers/{id}",
            new { fullName = edit.FullName, phone = edit.Phone, address = edit.Address }, ct);

    public Task<ManagementResult> SetActiveAsync(int id, bool active, CancellationToken ct = default) =>
        client.SendManagementAsync(HttpMethod.Patch, $"api/v1/admin/customers/{id}/status",
            new { isActive = active }, ct);
}

public sealed class RestOrderManagementService(AdminApiClient client) : IOrderManagementService
{
    private const int PageSize = 100;
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> AllowedTransitions =
        new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Shipping, OrderStatus.Cancelled],
            [OrderStatus.Shipping] = [OrderStatus.Completed],
            [OrderStatus.Completed] = [],
            [OrderStatus.Cancelled] = []
        };

    public async Task<IReadOnlyList<OrderManagementRow>> ListAsync(OrderManagementQuery query, CancellationToken ct = default)
    {
        var all = new List<OrderManagementRow>();
        var pageNumber = 1;
        while (true)
        {
            var (items, total) = await FetchPageAsync(query, pageNumber, PageSize, ct);
            all.AddRange(items);
            if (items.Count < PageSize || all.Count >= total)
                break;
            pageNumber++;
        }
        return all;
    }

    public Task<(IReadOnlyList<OrderManagementRow> Items, int Total)> ListPageAsync(
        OrderManagementQuery query, int offset, int limit, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit, 1, PageSize);
        var pageNumber = Math.Max(1, (offset / safeLimit) + 1);
        return FetchPageAsync(query, pageNumber, safeLimit, ct);
    }

    private async Task<(IReadOnlyList<OrderManagementRow> Items, int Total)> FetchPageAsync(
        OrderManagementQuery query, int pageNumber, int pageSize, CancellationToken ct)
    {
        var parts = new List<string> { $"page={pageNumber}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(query.Search)) parts.Add("search=" + Uri.EscapeDataString(query.Search.Trim()));
        if (query.Status.HasValue) parts.Add("status=" + query.Status.Value);

        var dto = await client.GetAsync<ApiOrderPageDto>("api/v1/admin/orders?" + string.Join('&', parts), ct);
        if (dto is null) return ([], 0);
        var rows = dto.Items.Select(o => new OrderManagementRow(
            o.OrderId, o.OrderCode, o.CustomerName ?? string.Empty, o.CustomerEmail ?? string.Empty,
            o.ReceiverName, o.ReceiverPhone ?? string.Empty, o.OrderDate, o.TotalAmount, ParseStatus(o.Status))).ToArray();
        return (rows, dto.TotalCount);
    }

    public async Task<OrderManagementDetail?> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await client.GetAsync<ApiOrderDetailDto>($"api/v1/admin/orders/{id}", ct, allowNotFound: true);
        if (dto is null) return null;
        return new OrderManagementDetail
        {
            OrderId = dto.Order.OrderId,
            OrderCode = dto.Order.OrderCode,
            OrderDate = dto.Order.OrderDate,
            Status = ParseStatus(dto.Order.Status),
            TotalAmount = dto.Order.TotalAmount,
            CustomerName = dto.CustomerName ?? dto.Order.CustomerName ?? string.Empty,
            CustomerEmail = dto.CustomerEmail ?? dto.Order.CustomerEmail ?? string.Empty,
            ReceiverName = dto.Order.ReceiverName,
            ReceiverPhone = dto.ReceiverPhone,
            ShippingAddress = dto.ShippingAddress,
            Note = dto.Note,
            Items = dto.Items.Select(i => new OrderItemRow(i.ProductName, i.Quantity, i.UnitPrice)).ToList()
        };
    }

    public IReadOnlyList<OrderStatus> NextStatuses(OrderStatus current) =>
        AllowedTransitions.TryGetValue(current, out var next) ? next : [];

    public Task<ManagementResult> UpdateStatusAsync(int id, OrderStatus expected, OrderStatus target, CancellationToken ct = default) =>
        client.SendManagementAsync(HttpMethod.Patch, $"api/v1/admin/orders/{id}/status",
            new { expectedStatus = expected.ToString(), targetStatus = target.ToString() }, ct);

    public Task<ManagementResult> CancelForCustomerAsync(int id, int customerId, CancellationToken ct = default) =>
        throw new NotSupportedException("Admin REST client uses UpdateStatusAsync for order status transitions.");

    private static OrderStatus ParseStatus(string value) =>
        Enum.TryParse<OrderStatus>(value, ignoreCase: true, out var status) ? status : OrderStatus.Pending;
}

public sealed class RestReportingService(AdminApiClient client) : IReportingService
{
    public async Task<DashboardSnapshot> GetDashboardAsync(CancellationToken ct = default)
    {
        var dto = await client.GetAsync<ApiDashboardDto>("api/v1/admin/dashboard", ct)
            ?? throw new AdminApiException("Không nhận được dữ liệu tổng quan từ PhoneStore API.");

        return new DashboardSnapshot
        {
            ActiveProducts = dto.ActiveProducts,
            TotalStock = dto.TotalStock,
            Customers = dto.Customers,
            ActiveCustomers = dto.ActiveCustomers,
            Orders = dto.Orders,
            PendingOrders = dto.PendingOrders,
            CompletedOrders = dto.CompletedOrders,
            CompletedRevenue = dto.CompletedRevenue,
            OrderStatuses = dto.OrderStatuses.Select(s => new OrderStatusMetric(ParseStatus(s.Status), s.Count)).ToArray(),
            RecentOrders = dto.RecentOrders.Select(o => new RecentOrderMetric(o.OrderCode, o.ReceiverName, o.TotalAmount, ParseStatus(o.Status), o.OrderDate)).ToArray(),
            BestSellers = dto.BestSellers.Select(p => new BestSellerMetric(p.Rank, p.ProductName, p.BrandName, p.QuantitySold, p.SalesValue)).ToArray(),
            LowStockProducts = dto.LowStockProducts.Select(l => new LowStockMetric(l.ProductName, l.BrandName, l.CategoryName, l.StockQuantity, l.IsActive)).ToArray()
        };
    }

    public async Task<ReportLoadResult> GetReportAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        if (fromDate > toDate) return ReportLoadResult.Fail("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
        if (toDate == DateOnly.MaxValue) return ReportLoadResult.Fail("Ngày kết thúc không hợp lệ.");

        var range = $"fromDate={fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&toDate={toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        try
        {
            var overview = await client.GetAsync<ApiReportOverviewDto>($"api/v1/admin/reports/overview?{range}&includeAlerts=true", ct);
            var top = await client.GetAsync<ApiTopProductsReportDto>($"api/v1/admin/reports/top-products?{range}", ct);
            if (overview is null || top is null)
                return ReportLoadResult.Fail("Chưa thể tải báo cáo từ PhoneStore API.");

            return ReportLoadResult.Success(new ReportSnapshot
            {
                FromDate = overview.FromDate,
                ToDate = overview.ToDate,
                TotalOrders = overview.TotalOrders,
                CompletedOrders = overview.CompletedOrders,
                CompletedRevenue = overview.CompletedRevenue,
                AverageCompletedOrderValue = overview.AverageCompletedOrderValue,
                OrderStatuses = overview.OrderStatuses.Select(s => new OrderStatusMetric(ParseStatus(s.Status), s.Count)).ToArray(),
                BestSellers = top.Items.Select(p => new BestSellerMetric(p.Rank, p.ProductName, p.BrandName, p.QuantitySold, p.SalesValue)).ToArray(),
                ActiveProducts = overview.CurrentInventory.ActiveProducts,
                TotalStock = overview.CurrentInventory.TotalStock,
                OutOfStockProducts = overview.CurrentInventory.OutOfStockProducts,
                LowStockProducts = overview.CurrentInventory.LowStockProducts,
                InventoryAlerts = (overview.InventoryAlerts ?? []).Select(l => new LowStockMetric(l.ProductName, l.BrandName, l.CategoryName, l.StockQuantity, l.IsActive)).ToArray(),
                RevenueTrend = (overview.RevenueTrend ?? []).Select(t => new RevenuePoint(t.Date, t.Revenue)).ToArray()
            });
        }
        catch (AdminApiException ex)
        {
            return ReportLoadResult.Fail(ex.Message);
        }
    }

    public async Task<RevenueSeriesSnapshot> GetRevenueSeriesAsync(RevenueChartPeriod period, DateOnly anchorDate, CancellationToken ct = default)
    {
        if (period == RevenueChartPeriod.CustomRange)
            return await GetRevenueSeriesAsync(anchorDate, anchorDate, ct);

        var url = $"api/v1/admin/reports/revenue?period={period}&anchorDate={anchorDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var dto = await client.GetAsync<ApiRevenueReportDto>(url, ct)
            ?? throw new AdminApiException("Không nhận được dữ liệu doanh thu từ PhoneStore API.");
        return MapRevenueSeries(dto, period);
    }

    public async Task<RevenueSeriesSnapshot> GetRevenueSeriesAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var url = $"api/v1/admin/reports/revenue?period=CustomRange&fromDate={fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&toDate={toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        var dto = await client.GetAsync<ApiRevenueReportDto>(url, ct)
            ?? throw new AdminApiException("Không nhận được dữ liệu doanh thu từ PhoneStore API.");
        return MapRevenueSeries(dto, RevenueChartPeriod.CustomRange);
    }

    private static RevenueSeriesSnapshot MapRevenueSeries(ApiRevenueReportDto dto, RevenueChartPeriod fallbackPeriod)
    {
        var parsedPeriod = Enum.TryParse<RevenueChartPeriod>(dto.Period, ignoreCase: true, out var p) ? p : fallbackPeriod;
        return new RevenueSeriesSnapshot
        {
            Period = parsedPeriod,
            FromDate = dto.FromDate,
            ToDate = dto.ToDate,
            TotalRevenue = dto.TotalRevenue,
            CompletedOrders = dto.CompletedOrders,
            Buckets = (dto.Buckets ?? []).Select(b => new RevenueBucket(b.Key, b.Label, b.TooltipLabel, b.Revenue, b.CompletedOrders)).ToArray()
        };
    }

    private static OrderStatus ParseStatus(string value) =>
        Enum.TryParse<OrderStatus>(value, ignoreCase: true, out var status) ? status : OrderStatus.Pending;
}
