# S35.4D — Order, Customer & Reporting API Completion

- **Execution Date**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`main`)
- **Stage Status**: **`PASS`**

---

## 1. Phạm vi & Kiến trúc triển khai

Hoàn thiện toàn bộ các endpoint Customer Order, Admin Order, Admin Customer và Admin Dashboard/Reporting trong `PhoneStore.Api`, duy trì tương thích ngược 100% với các route hiện có (`/api/orders`, `/api/admin/...`) và bổ sung chuẩn `/api/v1/...`, tái sử dụng trực tiếp `IOrderService`, `IOrderManagementService`, `ICustomerManagementService`, `IReportingService`:

1. **Customer Order API (`/api/v1/orders` & `/api/orders`)** — `src/PhoneStore.Api/Controllers/CustomerOrdersController.cs:15-157`, `src/PhoneStore.Api/Authorization/ApiAccess.cs:35-38`:
   - `GET /api/v1/orders`, `GET /api/v1/orders/{id}` (`Api.Customer` + `Orders.Read`): chỉ đọc đơn hàng thuộc chủ sở hữu `sub` từ JWT.
   - `POST /api/v1/orders` (`Api.Customer` + `Orders.Write`):
     - Lấy `userId` tuyệt đối từ JWT claim (`ApiAccess.UserId(User)`); DTO `CreateOrderRequest` khóa `[JsonUnmappedMemberHandling(Disallow)]` nên mọi trường `userId` giả mạo trong body bị từ chối `400 BadRequest`.
     - Bắt buộc header `Idempotency-Key` (`1..128` ký tự in được), chuẩn hóa xác định sang token `32`-hex và gọi trực tiếp `IOrderService.CreateIdempotentAsync` (khóa `sp_getapplock` + `Serializable` + `UPDLOCK, HOLDLOCK` trên `Products`).
     - Lần tạo đầu trả `201 Created` (`Replayed = false`), retry cùng `Idempotency-Key` trả `200 OK` (`Replayed = true`) cùng `OrderId` cũ mà không tạo đơn trùng và không trừ tồn kho lần 2.
     - Hết hàng trả `409 Conflict`, sản phẩm ngừng hoạt động trả `400 BadRequest`, tài khoản inactive bị chặn `403 Forbidden`.
   - `POST /api/v1/orders/{id}/cancel` (`Api.Customer` + `Orders.Write`):
     - Xác minh chủ sở hữu qua `IOrderService.GetReceiptAsync(id, owner)` trước khi gọi `IOrderManagementService.CancelForCustomerAsync` — đơn của người khác trả `404 NotFound` (không lộ thông tin và không động tới tồn kho).
     - Hủy đơn `Pending`/`Confirmed` trả `204 NoContent` và hoàn tồn kho một lần; replay hủy tiếp tục trả `204` an toàn (idempotent); hủy đơn `Shipping`/`Completed` bị từ chối `409 Conflict`.

2. **Admin Order API (`/api/v1/admin/orders` & `/api/admin/orders`)** — `src/PhoneStore.Api/Controllers/AdminOrdersController.cs:13-87`, `src/PhoneStore.Api/Contracts/Orders/OrderContracts.cs:25-54`:
   - `GET /api/v1/admin/orders`, `GET /api/v1/admin/orders/{id}`, `PATCH /api/v1/admin/orders/{id}/status` (`Api.Admin`).
   - Bổ sung đầy đủ các trường DTO phục vụ Admin WinForms mà không phá vỡ tương thích ngược: `CustomerName`, `CustomerEmail`, `ReceiverPhone`, `StatusLabel`, `NextStatuses` (tính trực tiếp từ `IOrderManagementService.NextStatuses`).
   - Bảo toàn chuyển trạng thái nguyên tử `ExpectedStatus -> TargetStatus` và chống tranh chấp hoàn tồn kho đồng thời (`409 Conflict`).

3. **Admin Customer API (`/api/v1/admin/customers` & `/api/admin/customers`)** — `src/PhoneStore.Api/Controllers/AdminCustomersController.cs:13-77`, `src/PhoneStore.Api/Contracts/Customers/AdminCustomerContracts.cs:20-41`:
   - `GET /api/v1/admin/customers` (bổ sung `Address`, `CreatedAt` vào `CustomerSummary`), `GET /api/v1/admin/customers/{id}`, `PATCH /api/v1/admin/customers/{id}/status`.
   - Bổ sung `PUT /api/v1/admin/customers/{id}` tái sử dụng `ICustomerManagementService.UpdateAsync`: chỉ cho sửa `FullName`, `Phone`, `Address`; `[JsonUnmappedMemberHandling(Disallow)]` chặn `roleId`, `passwordHash`, `email` (`400 BadRequest`); `CustomerManagementStore` lọc cứng `RoleName == "Customer"` nên mọi nỗ lực sửa/ngừng tài khoản Admin trả `404 NotFound`, bảo toàn `Single Admin` và `PasswordHash`.

4. **Admin Dashboard & Reports (`/api/v1/admin/dashboard` & `/api/v1/admin/reports/*`)** — `src/PhoneStore.Api/Controllers/AdminReportsController.cs:14-121`, `src/PhoneStore.Api/Contracts/Reports/ReportContracts.cs:25-115`:
   - `GET /api/v1/admin/dashboard` (& `/api/admin/dashboard`): tái sử dụng `IReportingService.GetDashboardAsync` trả đúng các KPI hiện có (`ActiveProducts`, `TotalStock`, `Customers`, `ActiveCustomers`, `Orders`, `PendingOrders`, `CompletedOrders`, `CompletedRevenue`, `OrderStatuses`, `RecentOrders`, `BestSellers`, `LowStockProducts`).
   - `GET /api/v1/admin/reports/revenue` (& `/api/admin/reports/revenue`): hỗ trợ đầy đủ `period=Day|Week|Month|Year|CustomRange` + `anchorDate` / `fromDate`..`toDate` tái sử dụng `IReportingService.GetRevenueSeriesAsync`, trả `Buckets` (24 giờ / 7 ngày / ngày trong tháng / 12 tháng / custom range) kèm `Days` tương thích ngược, loại trừ hoàn toàn đơn đã hủy khỏi doanh thu.
   - `GET /api/v1/admin/reports/overview` (& `/api/admin/reports/overview`): giữ đúng 3 câu SQL khi `includeAlerts=false`, hỗ trợ `?includeAlerts=true` trả kèm `InventoryAlerts` và `RevenueTrend` cho Admin WinForms.
   - `GET /api/v1/admin/reports/top-products` (& `/api/admin/reports/top-products`).

---

## 2. Kết quả kiểm thử S35.4D

- `dotnet build PhoneStore.sln -c Release --no-restore`: **PASS (`0 Warning(s)`, `0 Error(s)`)**.
- Targeted test suite (`OrderApiTests`, `AdminCustomerApiTests`, `AdminReportApiTests`, `CatalogApiTests`, `ReportingTests`, `CheckoutTests`, `OrderWorkflowTests`, `LocalJwtAuthenticationTests`): **PASS (`106/106`)**.
- Kiểm chứng trực tiếp trên SQL Server 2022 Docker fixture cách ly sẽ được chạy tổng hợp cùng S35.4E ở bước nghiệm thu cuối.
