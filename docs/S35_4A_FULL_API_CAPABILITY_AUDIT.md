# S35.4A — Full REST API Capability & Architecture Audit

- **Audit Date**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`vuvantai14/PhoneStore`)
- **Branch / HEAD**: `main` (`70c9cd4bab2a8cb33e143fb0ecbd0bc46288612a`)
- **Deployed Business Image Commit**: `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` (`git diff --stat 165b1fce..HEAD -- src/` = **0 changes**; running API container is 100% code-identical to `HEAD`)
- **Audit Mode**: Strictly **READ-ONLY** (source inspection + live `GET` probes + read-only `docker inspect`; zero code edits, zero DB writes, zero container restarts)

---

## A. Current API Architecture

`PhoneStore.Api` (`src/PhoneStore.Api/PhoneStore.Api.csproj:1-21`) là một ASP.NET Core Web API trên `.NET 10` (`net10.0`), sử dụng mô hình **Controller-based MVC** (không có Minimal API endpoints; đăng ký qua `builder.Services.AddControllers()` và `app.MapControllers()` tại `src/PhoneStore.Api/Program.cs:46,135`).

### 1. Tham chiếu lớp và Dependency Injection (`src/PhoneStore.Api/Program.cs:79-96`)
- **Project references** (`src/PhoneStore.Api/PhoneStore.Api.csproj:5-7`): `PhoneStore.Business`, `PhoneStore.Data`, `PhoneStore.Models`.
- **NuGet packages** (`src/PhoneStore.Api/PhoneStore.Api.csproj:11-12`): `Microsoft.AspNetCore.Authentication.JwtBearer 10.0.15`, `Swashbuckle.AspNetCore 6.6.2`.
- **Services đã đăng ký trong DI**:
  - `PhoneStoreDbContext` (SQL Server từ `ConnectionStrings:PhoneStoreConnection`, fail-fast nếu rỗng — `Program.cs:37-39,79`).
  - Public Catalog: `ProductCatalogReader`, `IProductService -> ProductService` (`Program.cs:92-93`).
  - Product Admin (metadata only): `ProductManagementStore`, `ProductEditorService` (`Program.cs:90-91`). **Không đăng ký** `ProductManagementService` / `IProductManagementService`, `IProductImageStorage`, `LookupManagementStore`, `ILookupManagementService`.
  - Orders: `PurchaseStore`, `IOrderService -> OrderService`, `OrderManagementStore`, `OrderManagementService` (`Program.cs:80-81,88-89`).
  - Customers: `CustomerManagementStore`, `CustomerManagementService` (`Program.cs:86-87`).
  - Reporting: `ReportingStore`, `IReportingStore`, `ReportSectionReader`, `IReportingService -> ReportingService` (`Program.cs:82-85`).
  - Health: `IPhoneStoreReadinessProbe -> PhoneStoreReadinessProbe`, `PhoneStoreDatabaseHealthCheck` (`Program.cs:94-96`).
  - Auth/Profile (**CHƯA đăng ký trong API**): `AuthUserStore`, `IPasswordHasher<User>`, `IAuthService`, `IAdminAuthService`, `CustomerProfileStore`, `ICustomerProfileService`, và `IVerifiedIdentityLookup` (interface khai báo tại `Authorization/Auth0Bearer.cs:10-13` nhưng chưa có implementation nào được đăng ký trong DI).

### 2. Middleware & Operational Hardening (`src/PhoneStore.Api/Program.cs:40-78,111-139`)
- **Error Handling**: `ApiExceptionHandler` (`Infrastructure/ApiExceptionHandler.cs:8-49`) chuyển đổi `DbException`/`DbUpdateException` thành `503 Service Unavailable` và ngoại lệ khác thành `500 Internal Server Error` theo chuẩn RFC 7807 `ProblemDetails` kèm `traceId`, không rò rỉ stack trace.
- **Request Safety & Correlation**: `RequestSafetyMiddleware` (`Infrastructure/RequestSafetyMiddleware.cs:8-93`) giới hạn `MaxQueryStringLength = 2048`, `MaxRequestLineSize = 4096` (`ApiOperationalOptions.cs:7-8`), xác thực/truyền tiếp header `X-Correlation-ID` (`^[A-Za-z0-9._-]+$`, 1–64 ký tự) và ghi log method/path/status/duration/traceId.
- **Rate Limiting & Timeouts**: `CatalogRateLimit` (`60 req/min/IP`), `HealthRateLimit` (`120 req/min/IP`), `CatalogTimeout` (`10s`), `HealthTimeout` (`3s`) trả `429`/`504` `application/problem+json` (`Program.cs:55-78`). **Lưu ý**: Chỉ áp dụng cho 4 public controllers (`Health`, `Products`, `Brands`, `Categories`); **5 protected controllers chưa gắn `[EnableRateLimiting]` hoặc `[RequestTimeout]`**.
- **Cache-Control & Fail-Closed Routing**:
  - Tự động đặt `Cache-Control: no-store` trên mọi endpoint có `IAuthorizeData` và không có `IAllowAnonymous` (`Program.cs:120-130`).
  - Mọi endpoint controller không gắn `[AllowAnonymous]` hoặc `[Authorize]` đều bị tự động gán `[Authorize]` rơi vào `DefaultPolicy` (`RequireAssertion(_ => false)`, `ApiAccess.cs:27-28`) -> **fail-closed tuyệt đối** (`Program.cs:135-139`).
- **Swagger / OpenAPI**: Đăng ký `AddSwaggerGen` kèm `AuthorizationDocumentationFilter` (`Program.cs:98-107`, `Infrastructure/AuthorizationDocumentationFilter.cs:8-22`), bật `UseSwagger()`/`UseSwaggerUI()` khi `app.Environment.IsDevelopment()` (`Program.cs:113-117`).

---

## B. Complete Endpoint Inventory

Thực tế mã nguồn `src/PhoneStore.Api/Controllers/` có **9 Controllers** với **23 ánh xạ HTTP Method + Route** (tương ứng **21 OpenAPI paths** xác minh trực tiếp từ `GET http://localhost:5025/swagger/v1/swagger.json`). **Không chỉ có `GET` sản phẩm.**

| # | HTTP Method | Route | Controller / Action (File:Line) | Chức năng | Authentication | Authorization | Implementation Status | Runtime Verification (`localhost:5025`) |
|---|---|---|---|---|---|---|---|---|
| 1 | `GET` | `/api/health/live` | `HealthController.Live` (`HealthController.cs:19`) | Liveness probe tiến trình API | None (`[AllowAnonymous]`) | Public | **IMPLEMENTED** | **VERIFIED** (`HTTP 200`, `{"status":"healthy"}`) |
| 2 | `GET` | `/api/health` | `HealthController.Get` (`HealthController.cs:24`) | Readiness probe kết nối SQL | None (`[AllowAnonymous]`) | Public | **IMPLEMENTED** | **VERIFIED** (`HTTP 200`, `{"status":"healthy"}`) |
| 3 | `GET` | `/api/health/ready` | `HealthController.Get` (`HealthController.cs:25`) | Readiness probe alias | None (`[AllowAnonymous]`) | Public | **IMPLEMENTED** | **VERIFIED** (`HTTP 200`, `{"status":"healthy"}`) |
| 4 | `GET` | `/api/v1/products` | `ProductsController.GetAll` (`ProductsController.cs:19`) | Danh sách sản phẩm active có lọc/sắp xếp/phân trang | None (`[AllowAnonymous]`) | Public | **IMPLEMENTED** | **VERIFIED** (`HTTP 200`, `totalItems=40`) |
| 5 | `GET` | `/api/v1/products/{id}` | `ProductsController.GetById` (`ProductsController.cs:38`) | Chi tiết sản phẩm active theo ID | None (`[AllowAnonymous]`) | Public | **IMPLEMENTED** | **VERIFIED** (`HTTP 200` với `id=40`) |
| 6 | `GET` | `/api/v1/brands` | `BrandsController.GetAll` (`BrandsController.cs:19`) | Danh sách thương hiệu active | None (`[AllowAnonymous]`) | Public | **IMPLEMENTED** | **VERIFIED** (`HTTP 200`, 8 brands) |
| 7 | `GET` | `/api/v1/categories` | `CategoriesController.GetAll` (`CategoriesController.cs:19`) | Danh sách danh mục active | None (`[AllowAnonymous]`) | Public | **IMPLEMENTED** | **VERIFIED** (`HTTP 200`, 2 categories) |
| 8 | `GET` | `/api/orders` | `CustomerOrdersController.List` (`CustomerOrdersController.cs:17`) | Lịch sử đơn hàng phân trang của khách hàng hiện tại | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Customer` + `Orders.Read` (`scope=orders:read`) | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 9 | `GET` | `/api/orders/{id:int}` | `CustomerOrdersController.Detail` (`CustomerOrdersController.cs:31`) | Chi tiết hóa đơn của chính khách hàng | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Customer` + `Orders.Read` + owner check | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 10 | `GET` | `/api/admin/products` | `AdminProductsController.List` (`AdminProductsController.cs:18`) | Danh sách quản trị sản phẩm (cả active/inactive) | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` (`ActiveRoleHandler` kiểm DB) | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 11 | `GET` | `/api/admin/products/{id:int}` | `AdminProductsController.Detail` (`AdminProductsController.cs:32`) | Chi tiết chỉnh sửa sản phẩm kèm `OriginalStockQuantity` | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 12 | `POST` | `/api/admin/products` | `AdminProductsController.Create` (`AdminProductsController.cs:42`) | Tạo mới sản phẩm | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **NOT VERIFIED** *(chỉ đọc; xuất hiện trong `swagger.json`)* |
| 13 | `PUT` | `/api/admin/products/{id:int}` | `AdminProductsController.Update` (`AdminProductsController.cs:53`) | Cập nhật sản phẩm có kiểm tra tồn kho gốc (`409` nếu xung đột) | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **NOT VERIFIED** *(chỉ đọc; xuất hiện trong `swagger.json`)* |
| 14 | `PATCH` | `/api/admin/products/{id:int}/status` | `AdminProductsController.SetStatus` (`AdminProductsController.cs:64`) | Bật/tắt (`IsActive`) soft-delete sản phẩm | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **NOT VERIFIED** *(chỉ đọc; xuất hiện trong `swagger.json`)* |
| 15 | `GET` | `/api/admin/orders` | `AdminOrdersController.List` (`AdminOrdersController.cs:17`) | Danh sách quản trị toàn bộ đơn hàng | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 16 | `GET` | `/api/admin/orders/{id:int}` | `AdminOrdersController.Detail` (`AdminOrdersController.cs:30`) | Chi tiết đơn hàng quản trị | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 17 | `PATCH` | `/api/admin/orders/{id:int}/status` | `AdminOrdersController.UpdateStatus` (`AdminOrdersController.cs:42`) | Chuyển trạng thái đơn hàng theo state machine + hoàn tồn kho khi hủy | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **NOT VERIFIED** *(chỉ đọc; xuất hiện trong `swagger.json`)* |
| 18 | `GET` | `/api/admin/customers` | `AdminCustomersController.List` (`AdminCustomersController.cs:17`) | Danh sách khách hàng quản trị | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 19 | `GET` | `/api/admin/customers/{id:int}` | `AdminCustomersController.Detail` (`AdminCustomersController.cs:30`) | Chi tiết khách hàng quản trị | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 20 | `PATCH` | `/api/admin/customers/{id:int}/status` | `AdminCustomersController.SetStatus` (`AdminCustomersController.cs:41`) | Khóa/mở khóa tài khoản khách hàng (`IsActive`) | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **NOT VERIFIED** *(chỉ đọc; xuất hiện trong `swagger.json`)* |
| 21 | `GET` | `/api/admin/reports/overview` | `AdminReportsController.Overview` (`AdminReportsController.cs:18`) | Báo cáo tổng quan đơn hàng, doanh thu & tồn kho theo khoảng ngày | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 22 | `GET` | `/api/admin/reports/revenue` | `AdminReportsController.Revenue` (`AdminReportsController.cs:28`) | Chuỗi doanh thu theo ngày (`fromDate`..`toDate`, tối đa 366 ngày) | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |
| 23 | `GET` | `/api/admin/reports/top-products` | `AdminReportsController.TopProducts` (`AdminReportsController.cs:37`) | Top sản phẩm bán chạy theo khoảng ngày | Scheme `PhoneStore.Api` *(unconfigured -> 401)* | `Api.Admin` | **IMPLEMENTED** | **VERIFIED (401 Fail-Closed)** / Authenticated: **NOT VERIFIED** |

### Các nhóm Endpoint chưa tồn tại (`MISSING` trong `PhoneStore.Api`)
- **Authentication & Token Issuance**: `POST /api/auth/login`, `POST /api/auth/admin/login`, `POST /api/auth/register`, `POST /api/auth/logout`, `POST /api/auth/refresh`, `GET /api/auth/me` — **MISSING**.
- **Customer Profile**: `GET /api/profile`, `PUT /api/profile` — **MISSING**.
- **Customer Order Mutations**: `POST /api/orders` (đặt hàng idempotent), `POST /api/orders/{id}/cancel` (khách tự hủy đơn) — **MISSING**.
- **Admin Brands & Categories CRUD**: `GET/POST/PUT/PATCH /api/admin/brands`, `GET/POST/PUT/PATCH /api/admin/categories` — **MISSING**.
- **Admin Product Images CRUD**: `GET/POST/DELETE/PATCH /api/admin/products/{id}/images...` — **MISSING**.
- **Admin Customer Profile Update**: `PUT /api/admin/customers/{id}` — **MISSING**.
- **Admin Dashboard Snapshot & Period Revenue**: `GET /api/admin/dashboard`, `GET /api/admin/reports/revenue` theo `period=Day|Week|Month|Year` — **MISSING**.
- **Public Catalog Discovery & Multi-facet**: `brandIds[]`, `rams[]`, `storages[]` trên `GET /api/v1/products`, `GET /api/v1/products/{id}/related`, `GET /api/v1/products/bestsellers`, `GET /api/v1/products/newest` — **MISSING**.

---

## C. Product CRUD Coverage

Đối chiếu từng nghiệp vụ sản phẩm giữa 4 tầng (`Business/Data`, `PhoneStore.Api`, `PhoneStore.Web`, `PhoneStore.Admin`):

| Nghiệp vụ Sản phẩm | Business / Data Layer | PhoneStore.Api | PhoneStore.Web | PhoneStore.Admin WinForms | Khoảng trống cần lưu ý |
|---|---|---|---|---|---|
| Danh sách sản phẩm công khai (lọc/sắp xếp/phân trang) | `ProductCatalogReader.GetPagedProductsAsync` (`ProductCatalogReader.cs:49-74`) hỗ trợ `Search`, `BrandId`, `BrandIds[]`, `CategoryId`, `MinPrice`, `MaxPrice`, `InStockOnly`, `Rams[]`, `Storages[]`, `Sort` | **CÓ** (`GET /api/v1/products`, `ProductsController.cs:19-35`) nhưng `ProductQueryRequest` (`ProductQueryRequest.cs:6-55`) chỉ expose `BrandId` đơn, thiếu `BrandIds[]`, `Rams[]`, `Storages[]` | **CÓ** (`ProductsController.cs:30-148`), gọi thẳng `IProductService` -> SQL | N/A | API chưa expose multi-select `brandIds[]`, `rams[]`, `storages[]` dù `ProductCatalogReader` đã có sẵn |
| Chi tiết sản phẩm công khai | `ProductCatalogReader.GetProductDetailAsync` (`ProductCatalogReader.cs:11-24`) | **CÓ** (`GET /api/v1/products/{id}`, `ProductsController.cs:38-57`) | **CÓ** (`ProductsController.cs:14-28`) — **chức năng DUY NHẤT của Web gọi `PhoneStore.Api`** qua `ProductDetailResolver` + `CatalogApiClient` (`CatalogApiClient.cs:15-16`), fallback về SQL nếu lỗi | N/A | Đã đồng bộ API <-> Web |
| Sản phẩm liên quan, mới nhất đa hãng, bán chạy | `GetRelatedAsync`, `GetDiversifiedNewestAsync`, `GetBestSellersAsync` (`ProductCatalogReader.cs:26-32,143-196`) | **MISSING** | **CÓ** (`HomeController.cs:21-22`, `ProductsController.cs:26`), gọi thẳng `IProductService` -> SQL | N/A | Đã có trong Business/Data, chưa expose qua API |
| Danh sách & chi tiết sản phẩm Admin | `ProductEditorService.ListPageAsync` & `GetEditAsync` (`ProductEditorService.cs:17-21`) | **CÓ** (`GET /api/admin/products`, `GET /api/admin/products/{id}`) | N/A | **CÓ** (`ProductsManagementControl.cs:234,260`), gọi trực tiếp `IProductManagementService` -> SQL | Admin chưa gọi API; API thiếu `GetOptionsAsync` / `GetFilterOptionsAsync` |
| Thêm sản phẩm (`POST`) | `ProductEditorService.SaveAsync(null, edit)` (`ProductEditorService.cs:23-78`) | **CÓ** (`POST /api/admin/products`, `AdminProductsController.cs:42-51`) | N/A | **CÓ** (`ProductsManagementControl.cs:274`) -> SQL | Đã có API nhưng bị khóa `401` do chưa có token issuer |
| Cập nhật sản phẩm & tồn kho (`PUT`) | `ProductEditorService.SaveAsync(id, edit)` dùng `UpdateIfStockMatchesAsync` (`ProductManagementStore.cs:77-94`) trong transaction `Serializable` | **CÓ** (`PUT /api/admin/products/{id}`, `AdminProductsController.cs:53-62`, bắt buộc `OriginalStockQuantity`, trả `409 Conflict`) | N/A | **CÓ** (`ProductsManagementControl.cs:274`) -> SQL | Chưa có endpoint điều chỉnh tồn kho riêng (`PATCH .../stock`); hiện cập nhật qua full `PUT` |
| Xóa / Soft-delete sản phẩm | `ProductEditorService.SetActiveAsync` (`ProductEditorService.cs:80-97`); hard-delete cố ý không có để bảo toàn khóa ngoại `OrderDetails` | **CÓ** (`PATCH /api/admin/products/{id}/status`, `AdminProductsController.cs:64-72`) | N/A | **CÓ** (`ProductsManagementControl.cs:290`) -> SQL | Đã có soft-delete ở API |
| Quản lý Thương hiệu (Brand CRUD) | `LookupManagementService` (`LookupKind.Brand`, `LookupManagementService.cs:9-81`) | **CHỈ CÓ `GET /api/v1/brands`** (public active); **MISSING** toàn bộ Admin CRUD (`GET/POST/PUT/PATCH /api/admin/brands`) | `BrandsController.cs:9-28` truy vấn **trực tiếp `PhoneStoreDbContext.Brands`** (kèm `Description` và số lượng sản phẩm active) | **CÓ** (`LookupManagementControl.cs:135,160,177`) -> SQL | Nghiệp vụ đã hoàn thiện trong Business/Data nhưng chưa expose Admin API |
| Quản lý Danh mục (Category CRUD) | `LookupManagementService` (`LookupKind.Category`, `LookupManagementService.cs:9-81`) | **CHỈ CÓ `GET /api/v1/categories`** (public active); **MISSING** toàn bộ Admin CRUD (`GET/POST/PUT/PATCH /api/admin/categories`) | `CategoriesController.cs:9-27` truy vấn **trực tiếp `PhoneStoreDbContext.Categories`** (kèm `Description` và số lượng sản phẩm active) | **CÓ** (`LookupManagementControl.cs:135,160,177`) -> SQL | Nghiệp vụ đã hoàn thiện trong Business/Data nhưng chưa expose Admin API |
| Quản lý Hình ảnh sản phẩm (List, Upload, Delete, Primary, Order) | `ProductManagementService` (`ProductManagementService.cs:20-179`) + `ProductManagementStore.LockProductAsync` (`sp_getapplock` + `UPDLOCK, HOLDLOCK`) + `LocalProductImageStorage` | **MISSING hoàn toàn** (API chỉ đăng ký `ProductEditorService`, không đăng ký `ProductManagementService` hay `IProductImageStorage`) | Chỉ phục vụ ảnh tĩnh `/uploads/products/*` (bind mount `:ro` trong Docker) | **CÓ** (`ProductImagesDialog.cs:150-205`) dùng `WindowsProductImageContentValidator` (GDI+) ghi thẳng vào `wwwroot/uploads/products` | Cần validator ảnh đa nền tảng cho Linux container + volume mount `rw` cho `api` trước khi mở endpoint upload ảnh |

---

## D. Order Management Coverage

| Nghiệp vụ Đơn hàng | Lớp xử lý hiện tại | PhoneStore.Api | PhoneStore.Web | PhoneStore.Admin | Transaction & Concurrency |
|---|---|---|---|---|---|
| Xem lịch sử đơn hàng khách hàng (lọc trạng thái, khoảng ngày UTC, phân trang) | `OrderService.GetPagedCustomerOrdersAsync` -> `PurchaseStore.CustomerOrdersPagedAsync` (`PurchaseStore.cs:91-141`) | **CÓ** (`GET /api/orders`, `CustomerOrdersController.cs:17-29`, yêu cầu `Api.Customer` + `Orders.Read`) | **CÓ** (`OrdersController.cs:18-55`), gọi thẳng `IOrderService` -> SQL | N/A | Read-only `AsNoTracking` |
| Xem chi tiết đơn hàng khách hàng | `OrderService.GetReceiptAsync(orderId, userId)` (`PurchaseStore.cs:60-70`) | **CÓ** (`GET /api/orders/{id:int}`, `CustomerOrdersController.cs:31-42`) | **CÓ** (`OrdersController.cs:169-178`, `CheckoutController.cs:69-79`) -> SQL | N/A | Enforces `o.UserId == userId` chống IDOR |
| Tạo đơn hàng + kiểm tra tồn kho + trừ tồn kho + idempotency | `OrderService.CreateIdempotentAsync` (`OrderService.cs:51-121`) -> `PurchaseStore` (`PurchaseStore.cs:13-58`) | **MISSING** (`POST /api/orders` chưa tồn tại) | **CÓ** (`CheckoutController.cs:32-67`) kèm `SessionCart` và `CheckoutSubmissionGuard` -> SQL | N/A | **`IsolationLevel.Serializable`** + `sys.sp_getapplock` (`PhoneStore:checkout:{code}`, timeout 10s) + `SELECT ... FROM [Products] WITH (UPDLOCK, HOLDLOCK)` theo thứ tự `ProductId` tăng dần (tránh deadlock) + `OrderCode` sinh từ SHA-256 `(userId:token)` |
| Khách hàng hủy đơn hàng (+ hoàn tồn kho) | `OrderManagementService.CancelForCustomerAsync` (`OrderManagementService.cs:38-84`) -> `OrderManagementStore` (`OrderManagementStore.cs:50-79`) | **MISSING** (`POST /api/orders/{id}/cancel` chưa tồn tại) | **CÓ** (`OrdersController.cs:180-192`) -> SQL | N/A | **`IsolationLevel.Serializable`** + `UPDLOCK, HOLDLOCK` trên `Orders` và `Products` (theo thứ tự `ProductId` tăng dần) + chỉ hủy khi `Pending`/`Confirmed` + idempotent nếu đã `Cancelled` |
| Danh sách & chi tiết đơn hàng Admin | `OrderManagementService.ListPageAsync` / `ListAsync` / `GetAsync` (`OrderManagementStore.cs:13-48`) | **CÓ** (`GET /api/admin/orders`, `GET /api/admin/orders/{id:int}`, `AdminOrdersController.cs:17-40`) | N/A | **CÓ** (`OrdersManagementControl.cs:61`, `OrderDetailDialog.cs:57`) -> SQL | Lưu ý DTO gap: `AdminOrdersController` chưa trả `CustomerName`, `CustomerEmail` và danh sách `NextStatuses` mà WinForms đang hiển thị |
| Admin cập nhật trạng thái đơn hàng (+ hoàn tồn kho khi hủy) | `OrderManagementService.UpdateStatusAsync(id, expected, target)` (`OrderManagementService.cs:12-20,35-84`) | **CÓ** (`PATCH /api/admin/orders/{id:int}/status`, `AdminOrdersController.cs:42-59`, trả `409 Conflict` nếu sai `ExpectedStatus` hoặc sai state transition) | N/A | **CÓ** (`OrderDetailDialog.cs:95`) -> SQL | State machine chặt (`Pending -> Confirmed|Cancelled`, `Confirmed -> Shipping|Cancelled`, `Shipping -> Completed`, terminal `Completed`/`Cancelled`) + `Serializable` + `UPDLOCK, HOLDLOCK` |
| Giỏ hàng (Cart) | `SessionCart` + `CartPresenter` (`PhoneStore.Web/Purchasing/`) + `OrderService.GetCartProductsAsync` | **MISSING** (không có persistent DB cart) | **CÓ** (`CartController.cs:10-122`), lưu trong ASP.NET Core Session (`PhoneStore.Session`) | N/A | Chỉ tồn tại ở Web MVC |

---

## E. Authentication & Authorization

### 1. Hiện trạng Xác thực & Phân quyền trên toàn hệ thống

| Thành phần | Cơ chế hiện tại | Lưu trữ & Hashing | Chính sách phân quyền | Trạng thái trong `PhoneStore.Api` |
|---|---|---|---|---|
| **Password Hashing & User Store** | `Microsoft.AspNetCore.Identity.IPasswordHasher<User>` (`PasswordHasher<User>` PBKDF2-HMAC-SHA512) + `AuthUserStore` (`src/PhoneStore.Data/Authentication/AuthUserStore.cs`) | Lưu trong bảng `Users` (`PasswordHash`, `IsActive`, `RoleId`) và `Roles` (`Admin`, `Customer` — `UX_Users_SingleAdmin` giới hạn tối đa 1 Admin) | `AuthService` chỉ cho `Customer` active (`AuthService.cs:46`); `AdminAuthService` chỉ cho `Admin` active (`AdminAuthService.cs:41-42`) | **CHƯA ĐĂNG KÝ** trong `PhoneStore.Api/Program.cs` |
| **Customer Web** (`PhoneStore.Web`) | ASP.NET Core Cookie Auth (`PhoneStore.Auth`, `HttpOnly`, `SameSite=Lax`, 8h sliding / 14d remember-me — `Web/Program.cs:72-85`) | `AccountController` (`/account/register`, `/account/login`, `/account/logout`) + `CustomerCookieEvents.ValidatePrincipal` (`CustomerCookieEvents.cs:11-31`) xác minh lại DB mỗi request | `[Authorize(Policy = "Customer")]` (`Web/Program.cs:86-87`) | Tách riêng hoàn toàn khỏi `PhoneStore.Api` |
| **Admin WinForms** (`PhoneStore.Admin`) | In-memory `AdminSession` (`AdminLoginGateway.cs:9-15`) gọi trực tiếp `IAdminAuthService.LoginAsync` | Kiểm tra lại DB `IsCurrentAdminAsync` trước mỗi thao tác ghi (`CatalogManagementGateway.cs:64-71`) | `RequireAdmin()` kiểm `session.Current.Role == "Admin"` (`CatalogManagementGateway.cs:75-80`) | Không dùng token hay API |
| **REST API** (`PhoneStore.Api`) | `AddPhoneStoreApiAccess` (`ApiAuthenticationRegistration.cs:30-46`): scheme mặc định `PhoneStore.Api` dùng `UnconfiguredApiAuthentication` (`UnconfiguredApiAuthentication.cs:9-20`) vì `appsettings.json:4` đặt `"ApiAuthentication:Oidc:Enabled": false` | Nếu bật OIDC, `Auth0Bearer` (`Auth0Bearer.cs:17-77`) yêu cầu `at+jwt` ký RS256 và tra cứu `IVerifiedIdentityLookup` (chưa có implementation). Hơn nữa mốc **S34.9E** đã quyết định **bỏ Auth0**, hướng tới tự host (OpenIddict hoặc JWT ký cục bộ dùng lại `Users`/`Roles`). | 3 policies tại `ApiAccess.cs:27-37`: `DefaultPolicy` (deny all), `Api.Customer`, `Api.Admin` (cả hai kiểm tra DB `IsActive && RoleName` qua `ActiveRoleHandler` — `ApiAccess.cs:42-52`), `Orders.Read` (`scope=orders:read`) | **FAIL-CLOSED (`401`) trên toàn bộ 16 protected endpoints**; **MISSING** hoàn toàn auth/token endpoints |

### 2. Yêu cầu Bảo mật Bắt buộc Trước Khi Bổ sung / Kích hoạt API Ghi Dữ Liệu
1. **Không bao giờ bỏ qua xác thực hoặc mở public endpoint quản trị**: Giữ nguyên cơ chế fail-closed tại `Program.cs:135-139`, `ApiAccess.cs:27-28`, `ActiveRoleHandler` (`ApiAccess.cs:42-52`) và `Cache-Control: no-store`.
2. **Triển khai Token Issuer cục bộ thay thế cho `Auth0Bearer` đã bỏ ở S34.9E**:
   - Tái sử dụng trực tiếp `IAuthService`, `IAdminAuthService`, `IPasswordHasher<User>`, và `Users`/`Roles` hiện có (không tạo hệ thống password riêng).
   - Khóa ký JWT (asymmetric RSA/ECDSA hoặc signing key tối thiểu 256-bit) phải nạp từ environment/secret store ngoài Git (tuyệt đối không hardcode trong `appsettings.json`), thời hạn access token ngắn (ví dụ 15–30 phút) kèm cơ chế refresh token có thể thu hồi hoặc re-check DB `IsActive` qua `ActiveRoleHandler`.
   - Áp dụng **Rate Limiting nghiêm ngặt** cho endpoint đăng nhập/đăng ký (chống brute-force) và cho toàn bộ protected write endpoints.
   - Phân tách rõ audience/scopes (`orders:read`, `orders:write`, `profile:write`, `admin:*`) và xác minh sở hữu dữ liệu (`UserId` luôn lấy từ `ClaimTypes.NameIdentifier` đã xác thực, tuyệt đối không tin `userId` từ body/query).
   - Tắt Swagger/OpenAPI khi chạy production (`ASPNETCORE_ENVIRONMENT=Production` trong cấu hình triển khai thực tế).

---

## F. Admin WinForms Data Access

Kiểm tra toàn bộ `src/PhoneStore.Admin/` (`AdminServices.cs:20-62`, `AdminLoginGateway.cs`, `CatalogManagementGateway.cs`, `MainForm.cs:443-457`, toàn bộ Controls & Dialogs, và `scripts/run-admin-docker.ps1:122-143`) cho thấy **100% tính năng Admin WinForms đang truy cập SQL Server trực tiếp qua EF Core (`PhoneStoreDbContext`)**, **0% gọi REST API** (không có bất kỳ tham chiếu `HttpClient` nào trong `PhoneStore.Admin`).

| Admin Feature (UI Control / Dialog) | Current Data Access | Existing API (`PhoneStore.Api`) | Missing API | Recommended Integration |
|---|---|---|---|---|
| **Đăng nhập / Đăng xuất Admin** (`LoginForm.cs:384`, `MainForm.cs:381`) | `AdminLoginGateway` -> `IAdminAuthService` -> SQL (`AuthUserStore`) | Không có | `POST /api/v1/auth/admin/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout` | Chuyển đầu tiên trong S35.4E để lấy Bearer token cho `AdminSession` |
| **Tổng quan / Dashboard** (`DashboardControl.cs:1219,1308,1313`) | `CatalogManagementGateway.WithReportingAsync` -> `IReportingService.GetDashboardAsync` & `GetRevenueSeriesAsync(period/range)` -> SQL | Có `GET /api/admin/reports/revenue` (chỉ custom daily range) | `GET /api/v1/admin/dashboard`, `GET /api/v1/admin/reports/revenue` theo `period=Day\|Week\|Month\|Year` | Bổ sung 2 endpoint đọc trước rồi chuyển màn hình Dashboard (read-only, rủi ro thấp) |
| **Quản lý Sản phẩm (List, Create, Update, Toggle Active)** (`ProductsManagementControl.cs:210,234,260,274,290`) | `WithProductsAsync` / `WithProductsWriteAsync` -> `IProductManagementService` -> SQL | **Đã có 5 endpoints**: `GET/POST /api/admin/products`, `GET/PUT /api/admin/products/{id}`, `PATCH /api/admin/products/{id}/status` | `GET /api/v1/admin/products/options` (dropdown thương hiệu/danh mục kể cả inactive khi sửa) | Tái sử dụng 5 endpoints sẵn có (sau khi có Auth + endpoint options) |
| **Quản lý Ảnh Sản phẩm** (`ProductImagesDialog.cs:150,179,193,205`) | `WithProductsAsync` / `WithProductsWriteAsync` -> `LocalProductImageStorage` (file cục bộ `../../../../PhoneStore.Web/wwwroot/uploads/products` + GDI+) + SQL | Không có | `GET/POST/DELETE/PATCH /api/v1/admin/products/{id}/images...` + Linux image validator + RW volume mount trên container `api` | Chuyển sau cùng vì phụ thuộc đồng bộ lưu trữ file giữa container `api` và `web` |
| **Quản lý Thương hiệu** (`LookupManagementControl.cs:135,160,177`) | `WithLookupsAsync` / `WithLookupsWriteAsync` -> `ILookupManagementService(Brand)` -> SQL | Chỉ có public `GET /api/v1/brands` | `GET/POST/PUT /api/v1/admin/brands`, `PATCH /api/v1/admin/brands/{id}/status` | Bổ sung `AdminBrandsController` bọc `ILookupManagementService` rồi nối WinForms |
| **Quản lý Danh mục** (`LookupManagementControl.cs:135,160,177`) | `WithLookupsAsync` / `WithLookupsWriteAsync` -> `ILookupManagementService(Category)` -> SQL | Chỉ có public `GET /api/v1/categories` | `GET/POST/PUT /api/v1/admin/categories`, `PATCH /api/v1/admin/categories/{id}/status` | Bổ sung `AdminCategoriesController` bọc `ILookupManagementService` rồi nối WinForms |
| **Quản lý Khách hàng** (`CustomersManagementControl.cs:63,75,77,90`) | `WithCustomersAsync` / `WithCustomersWriteAsync` -> `ICustomerManagementService` -> SQL | **Đã có 3 endpoints**: `GET /api/admin/customers`, `GET /api/admin/customers/{id}`, `PATCH /api/admin/customers/{id}/status` | `PUT /api/v1/admin/customers/{id}` (`UpdateAsync`) | Bổ sung `PUT` cho thông tin khách hàng rồi chuyển màn hình Khách hàng |
| **Quản lý Đơn hàng** (`OrdersManagementControl.cs:61`, `OrderDetailDialog.cs:57,95`) | `WithOrdersAsync` / `WithOrdersWriteAsync` -> `IOrderManagementService` -> SQL | **Đã có 3 endpoints**: `GET /api/admin/orders`, `GET /api/admin/orders/{id}`, `PATCH /api/admin/orders/{id}/status` | Bổ sung `CustomerName`, `CustomerEmail`, `NextStatuses` vào DTO phản hồi của `AdminOrdersController` | Mở rộng DTO không phá tương thích rồi chuyển màn hình Đơn hàng |
| **Báo cáo Doanh thu & Top Sản phẩm** (`ReportsControl.cs:143`, `ReportRevenueChartPanel.cs:305-306`) | `WithReportingAsync` -> `IReportingService.GetReportAsync` & `GetRevenueSeriesAsync` -> SQL | **Đã có 3 endpoints**: `overview`, `revenue`, `top-products` | Hỗ trợ `period=Day\|Week\|Month\|Year` cho `revenue` | Chuyển cùng đợt với Dashboard |

### Phương án Mở rộng Không Làm Hỏng Admin WinForms Hiện Tại
- Giữ nguyên các interface `IProductManagementService`, `ILookupManagementService`, `ICustomerManagementService`, `IOrderManagementService`, `IReportingService`, `IAdminAuthService` và `CatalogManagementGateway`.
- Triển khai các adapter HTTP thực thi chính các interface trên trong `PhoneStore.Admin`, chuyển đổi qua cấu hình `AdminDataAccess:Mode = DirectSql | RestApi` (Strangler Fig pattern). Khi cần, có thể quay lại `DirectSql` ngay lập tức mà không ảnh hưởng nghiệp vụ Admin đang vận hành.

---

## G. Docker Runtime Verification (READ-ONLY)

Thực hiện ngày `2026-11-21` trên container business `phonestore-restore-c2-api-1` (`http://localhost:5025`):

### 1. Trạng thái Container & Đối chiếu Source Code vs Image
- **Container ID**: `cec828cd68c70cd647dda1bed7d6c2d61da6f67ca18eba423d4ce82e798986f3`
- **Image Reference (`Config.Image`)**: `ghcr.io/vuvantai14/phonestore-api@sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050`
- **Local Image ID**: `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050`
- **Runtime Status**: `Running = true`, `Health = healthy`, `Restarts = 0`, `StartedAt = 2026-11-21T03:53:44.575875686Z`
- **Environment**: `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_HTTP_PORTS=8080`, `DOTNET_VERSION=10.0.15`
- **Đối chiếu Source Code vs Running Image**: Commit build image là `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`, `HEAD` hiện tại là `70c9cd4bab2a8cb33e143fb0ecbd0bc46288612a` (chỉ thêm `scripts/cd/` và `docs/`). Kết quả `git diff --stat 165b1fce..HEAD -- src/` là **0 file khác biệt** -> **API đang chạy khớp 100% với source code hiện tại**.

### 2. Kết quả Kiểm tra Live Read-Only (`GET`)
- **Public Endpoints (`PASS 7/7`)**:
  - `GET /api/health/live` -> `HTTP 200` (`{"status":"healthy"}`)
  - `GET /api/health` -> `HTTP 200` (`{"status":"healthy"}`)
  - `GET /api/health/ready` -> `HTTP 200` (`{"status":"healthy"}`)
  - `GET /api/v1/products?page=1&pageSize=3` -> `HTTP 200` (`602` bytes, `OnePlus Nord 3`, ...)
  - `GET /api/v1/products/40` -> `HTTP 200` (`488` bytes, `OnePlus Nord 3`, `brandName="OnePlus"`, `stockQuantity=11`)
  - `GET /api/v1/brands` -> `HTTP 200` (`198` bytes, 8 brands: Apple, Google, OnePlus, OPPO, realme, Samsung, vivo, Xiaomi)
  - `GET /api/v1/categories` -> `HTTP 200` (`80` bytes, 2 categories)
- **OpenAPI Spec**: `GET /swagger/v1/swagger.json` -> `HTTP 200` (`96,882` bytes, liệt kê đúng **21 OpenAPI paths** khớp 100% với 9 controllers trong source code).
- **Protected `GET` Endpoints (`PASS 11/11` Fail-Closed)**:
  - Toàn bộ 11 `GET` endpoints được bảo vệ (`/api/orders`, `/api/orders/1`, `/api/admin/products`, `/api/admin/products/40`, `/api/admin/orders`, `/api/admin/orders/1`, `/api/admin/customers`, `/api/admin/customers/1`, `/api/admin/reports/overview`, `/api/admin/reports/revenue`, `/api/admin/reports/top-products`) đều trả `HTTP 401 Unauthorized`, `Content-Type: application/problem+json`, `Cache-Control: no-store`, kèm `traceId`.

---

## H. API Architecture Gaps

| Khía cạnh Kiến trúc | Hiện trạng đã có | Khoảng trống / Vấn đề thực tế | Mức độ ưu tiên |
|---|---|---|---|
| **1. Authentication & Authorization** | Fail-closed policy + `ActiveRoleHandler` kiểm DB + `Cache-Control: no-store` | Chưa có local token issuer (`ApiAuthentication:Oidc:Enabled = false`; `Auth0Bearer` còn treo sau quyết định bỏ Auth0 ở S34.9E) -> **toàn bộ 16 protected endpoints không thể gọi được ở runtime** | **CRITICAL (Blocker)** |
| **2. API Versioning Consistency** | Public catalog dùng `/api/v1/{products,brands,categories}` | Protected endpoints dùng `/api/orders` và `/api/admin/*` không có prefix `/v1/` -> thiếu nhất quán route versioning | **MEDIUM** |
| **3. Rate Limiting & Timeouts** | Đã cấu hình trên `Health`, `Products`, `Brands`, `Categories` (`Program.cs:55-78`) | **5 protected controllers** (`CustomerOrders`, `AdminProducts`, `AdminOrders`, `AdminCustomers`, `AdminReports`) chưa gắn `[EnableRateLimiting]` và `[RequestTimeout]` | **HIGH** |
| **4. DTO Validation Consistency** | `ProductQueryRequest` & `ReportRange` có DataAnnotations + `IValidatableObject`; `CustomerStatusRequest` có `JsonUnmappedMemberHandling.Disallow` | `ProductWriteRequest`/`ProductUpdateRequest` (`AdminProductContracts.cs:16-45`) chỉ có `required`, chưa có `[StringLength]`/`[Range]` (để validation rơi xuống `ProductEditorService`), chưa áp dụng đồng nhất `JsonUnmappedMemberHandling.Disallow` trên các write DTOs | **MEDIUM** |
| **5. Error Handling & Logging** | `ApiExceptionHandler` + `RequestSafetyMiddleware` + `X-Correlation-ID` + `ProblemDetails` | Đã rất tốt; cần bổ sung audit log cho các thao tác Admin ghi (`ActorUserId`, `TargetId`, `Action`) mà không ghi secrets/PII | **MEDIUM** |
| **6. Pagination & Filtering** | Tất cả danh sách chính đều đã phân trang (`Page`, `PageSize` giới hạn `1..50` hoặc `1..100`) | Public `GET /api/v1/products` thiếu `brandIds[]`, `rams[]`, `storages[]`; `AdminOrdersController` chưa lọc theo khoảng ngày; `AdminProducts`/`AdminOrders` sắp xếp cố định theo `Id` tăng dần (`ProductManagementStore.cs:30`, `OrderManagementStore.cs:32`) | **MEDIUM** |
| **7. Transactions & Optimistic Concurrency** | Checkout (`Serializable` + `sp_getapplock` + `UPDLOCK`), Order status (`Serializable` + `ExpectedStatus` + `UPDLOCK`), Product update (`UpdateIfStockMatchesAsync` kiểm `OriginalStockQuantity`) | Chưa có `rowversion` tổng quát cho các trường khác của Product (ví dụ 2 admin cùng sửa `Price` khi `StockQuantity` không đổi) và Customer/Brand/Category (`CustomerManagementService.UpdateAsync` ghi đè trực tiếp không kiểm tra concurrency) | **MEDIUM** |
| **8. Idempotency** | `OrderService.CreateIdempotentAsync` đã có idempotency ở tầng Business/Data | Chưa có REST endpoint `POST /api/orders` chấp nhận `Idempotency-Key` header ánh xạ sang `submissionToken` | **HIGH** |
| **9. Swagger & Production Environment** | Swagger chỉ bật ở `Development` (`Program.cs:113-117`), có `AuthorizationDocumentationFilter` | `docker-compose.yml` hiện đặt `ASPNETCORE_ENVIRONMENT=Development`, làm lộ Swagger UI & `swagger.json` trên container business; cần tách cấu hình `Production` tắt Swagger và bổ sung OpenAPI Bearer SecurityScheme cho môi trường `Development` | **HIGH** |
| **10. Cross-Platform Image Storage (Docker)** | `LocalProductImageStorage` + `WindowsProductImageContentValidator` (chỉ chạy trên Windows WinForms) | Container `api` chạy trên Linux (`aspnet:10.0-noble`) và không có mount tới thư mục `uploads/products` (`web` mount `:ro`) -> cần validator ảnh thuần managed/Linux-compatible và chiến lược shared volume trước khi mở upload ảnh qua API | **HIGH** *(cho tính năng ảnh)* |
| **11. Reusability of Business/Data Layers** | `PhoneStore.Business` và `PhoneStore.Data` đã tách gọn khỏi UI (`Web` và `Admin`) | Tỷ lệ tái sử dụng rất cao (~90%); chỉ có `Web/BrandsController` & `Web/CategoriesController` gọi thẳng `PhoneStoreDbContext` và `ProductManagementService` phụ thuộc `IProductImageStorage` | **LOW (Điểm mạnh)** |

---

## I. Recommended Implementation Roadmap

**Đề xuất điều chỉnh thứ tự so với khung gốc để xử lý đúng phụ thuộc bảo mật và kỹ thuật**: Vì `PhoneStore.Api` **đã có sẵn** 14 Admin endpoints và 2 Customer endpoints nhưng toàn bộ đều bị khóa `401` do chưa có cơ chế xác thực/cấp phát token hoạt động, **Authentication & Authorization phải đi trước** để mọi API ghi mới và có sẵn đều được kiểm thử bảo mật và xác minh end-to-end ngay khi xây dựng.

### Giai đoạn 1 — `S35.4B`: Authentication, Token Issuance & Security Hardening *(Đẩy lên làm nền tảng đầu tiên — tương ứng S35.4D gốc)*
- **Scope**: Triển khai cơ chế cấp phát và xác thực JWT Bearer nội bộ (theo định hướng `S34.9E` bỏ Auth0, tự host dùng lại `Users`/`Roles`), gỡ bỏ phụ thuộc chết `Auth0Bearer` hoặc giữ cô lập, kích hoạt đăng nhập Admin & Customer qua API, bổ sung rate limit/timeout cho toàn bộ protected controllers và API hồ sơ khách hàng (`GET/PUT /api/v1/profile`).
- **Existing Reusable Components**: `AuthUserStore`, `IPasswordHasher<User>`, `IAuthService`, `IAdminAuthService`, `ICustomerProfileService`, `ActiveRoleHandler` (`ApiAccess.cs:42-52`).
- **Required Endpoints**:
  - `POST /api/v1/auth/login` (Customer token), `POST /api/v1/auth/register` (Customer register), `POST /api/v1/auth/admin/login` (Admin token), `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, `GET /api/v1/auth/me`
  - `GET /api/v1/profile`, `PUT /api/v1/profile`
- **Security Requirements**: Khóa ký JWT nạp từ env/secret ngoài Git; token ngắn hạn (15–30 phút) + refresh rotation/revocation; rate limit riêng cho `/auth/*` (ví dụ `10 req/min/IP`); `ActiveRoleHandler` tiếp tục kiểm tra DB `IsActive` & `Role`; `Cache-Control: no-store`.
- **Tests**: Unit & HTTP integration tests cho login thành công/sai mật khẩu/tài khoản bị khóa/sai role, token hết hạn, sai chữ ký, thu hồi quyền tức thì khi `IsActive = false`, brute-force `429`.
- **Risks**: Cấu hình khóa ký sai khi deploy Docker (cần fail-fast `ValidateOnStart`); đảm bảo không ảnh hưởng Cookie Auth hiện hành của Customer Web.
- **Acceptance Criteria**: Toàn bộ 16 protected endpoints hiện có + auth endpoints hoạt động với token hợp lệ và vẫn trả `401`/`403` fail-closed khi thiếu hoặc sai quyền.

### Giai đoạn 2 — `S35.4C`: Product, Lookup & Image Management API Completion *(tương ứng S35.4B gốc)*
- **Scope**: Hoàn thiện phần còn thiếu của nhóm Sản phẩm/Thương hiệu/Danh mục/Hình ảnh và nâng cấp bộ lọc public catalog.
- **Existing Reusable Components**: `ProductEditorService`, `ProductManagementService`, `LookupManagementService`, `ProductCatalogReader`, `AdminProductsController`.
- **Required Endpoints**:
  - Chuẩn hóa route `/api/v1/admin/products` (giữ alias `/api/admin/products` nếu cần tương thích) + `GET /api/v1/admin/products/options`
  - `GET /api/v1/admin/brands`, `POST /api/v1/admin/brands`, `PUT /api/v1/admin/brands/{id}`, `PATCH /api/v1/admin/brands/{id}/status`
  - `GET /api/v1/admin/categories`, `POST /api/v1/admin/categories`, `PUT /api/v1/admin/categories/{id}`, `PATCH /api/v1/admin/categories/{id}/status`
  - `GET /api/v1/admin/products/{id}/images`, `POST /api/v1/admin/products/{id}/images` (multipart/form-data), `DELETE /api/v1/admin/products/{id}/images/{imageId}`, `PATCH /api/v1/admin/products/{id}/images/{imageId}/primary`, `PATCH /api/v1/admin/products/{id}/images/{imageId}/order`
  - Bổ sung `brandIds[]`, `rams[]`, `storages[]` vào `GET /api/v1/products`, cộng `GET /api/v1/products/{id}/related`, `GET /api/v1/products/bestsellers`, `GET /api/v1/products/newest`
- **Security Requirements**: Chỉ `Api.Admin` được gọi `/api/v1/admin/*`; kiểm tra kích thước (`<= 10 MB`), magic bytes & độ phân giải ảnh (`<= 4096px`, `<= 16MP`) bằng bộ giải mã đa nền tảng trên Linux (không dùng `System.Drawing`), chống path traversal và `JsonUnmappedMemberHandling.Disallow`.
- **Tests**: CRUD tests cho Brands/Categories/Products, stock conflict `409`, image upload hợp lệ/tệp giả mạo/quá dung lượng/concurrent upload primary normalization, multi-facet catalog query tests.
- **Risks**: Chia sẻ thư mục `uploads/products` giữa container `api` (`rw`) và `web` (`ro`) trong `docker-compose.yml` (cần đồng bộ quyền `APP_UID=1654` trên Linux container).
- **Acceptance Criteria**: Toàn bộ nghiệp vụ Product/Brand/Category/Image đều có REST API, pass unit/integration tests, không làm vỡ `CatalogApiClient` của Web.

### Giai đoạn 3 — `S35.4D`: Order, Customer & Dashboard Reporting API Completion *(tương ứng S35.4C gốc)*
- **Scope**: Hoàn thiện các API giao dịch đơn hàng của khách hàng, bổ sung đầy đủ trường quản trị đơn hàng/khách hàng và báo cáo Dashboard cho Admin.
- **Existing Reusable Components**: `OrderService.CreateIdempotentAsync`, `OrderManagementService.CancelForCustomerAsync`, `CustomerManagementService.UpdateAsync`, `IReportingService.GetDashboardAsync` & `GetRevenueSeriesAsync(RevenueChartPeriod, DateOnly)`.
- **Required Endpoints**:
  - `POST /api/v1/orders` (bắt buộc header `Idempotency-Key`, tái sử dụng `CreateIdempotentAsync`)
  - `POST /api/v1/orders/{id:int}/cancel` (khách tự hủy đơn sở hữu, tái sử dụng `CancelForCustomerAsync`)
  - Mở rộng `AdminOrdersController` trả thêm `CustomerName`, `CustomerEmail`, `NextStatuses`
  - `PUT /api/v1/admin/customers/{id:int}` (tái sử dụng `CustomerManagementService.UpdateAsync`)
  - `GET /api/v1/admin/dashboard` (KPIs, recent orders, low stock, top products, status breakdown)
  - Mở rộng `GET /api/v1/admin/reports/revenue` hỗ trợ `period=Day|Week|Month|Year` + `anchorDate`
- **Security Requirements**: `POST /api/v1/orders` và `cancel` yêu cầu `Api.Customer` + scope `orders:write` và lấy `userId` duy nhất từ token claim; không lộ sự tồn tại đơn hàng của khách khác (`404`); giới hạn tần suất tạo đơn hàng; giữ nguyên `Serializable` + `sp_getapplock` + `UPDLOCK`.
- **Tests**: Concurrent order creation chống oversell, idempotency retry trả cùng `OrderId`, customer cancel hoàn tồn kho & chặn hủy đơn người khác, admin order state-machine conflict `409`, dashboard/period revenue tests.
- **Risks**: Deadlock/timeout SQL nếu nhiều request đặt hàng đồng thời (đã có lock ordering theo `ProductId` tăng dần, cần kiểm tra dưới tải thử nghiệm).
- **Acceptance Criteria**: Toàn bộ luồng đặt hàng/hủy đơn/quản lý khách hàng/dashboard có REST API đầy đủ với concurrency & idempotency được kiểm chứng.

### Giai đoạn 4 — `S35.4E`: Admin WinForms API Integration
- **Scope**: Chuyển dần Admin WinForms từ truy cập SQL trực tiếp sang gọi REST API thông qua feature flag `AdminDataAccess:Mode = DirectSql | RestApi`, bảo toàn 100% UI và nghiệp vụ hiện có.
- **Existing Reusable Components**: `AdminLoginGateway`, `CatalogManagementGateway`, `AdminSession`, toàn bộ Controls/Dialogs không cần đổi chữ ký nghiệp vụ.
- **Required Endpoints**: Sử dụng toàn bộ `/api/v1/auth/admin/*` và `/api/v1/admin/*` đã hoàn thiện ở S35.4B–S35.4D.
- **Security Requirements**: Lưu access/refresh token chỉ trong bộ nhớ tiến trình (`AdminSession`), xóa sạch khi logout hoặc khi nhận `401`/`403` (`AuthorizationRevoked`), dùng HTTPS khi kết nối ngoài localhost, không ghi token vào log hay file cấu hình.
- **Tests**: Integration test từng module WinForms với `RestApi` mode (Login -> Dashboard -> Reports -> Brands/Categories -> Products -> Product Images -> Customers -> Orders) + fallback test `DirectSql`.
- **Risks**: Khác biệt xử lý lỗi mạng/timeout/token expiry trên UI thread WinForms -> cần map HTTP `401`/`403`/`409`/`429`/`503` sang thông báo tiếng Việt nhất quán.
- **Acceptance Criteria**: Toàn bộ 7 mục sidebar của Admin WinForms chạy ổn định qua REST API, không cần chuỗi kết nối SQL `sa` trên máy trạm Admin khi bật `RestApi`.

### Giai đoạn 5 — `S35.4F`: API Regression, Security Hardening & Docker CD Validation
- **Scope**: Chạy hồi quy toàn diện giải pháp (`.NET` unit/integration + SQL integration + OpenAPI contract diff), chuyển cấu hình Docker production tách bạch `ASPNETCORE_ENVIRONMENT=Production` (tắt Swagger ở production), kiểm thử diễn tập cách ly (`S35.3G` pipeline) và nghiệm thu triển khai business qua `Invoke-BusinessCd.ps1`.
- **Existing Reusable Components**: `.github/workflows/ci.yml`, `scripts/cd/*` (`Invoke-ReleaseRehearsal.ps1`, `Invoke-BusinessCd.ps1`, `Rehearsal.Probe.ps1`), `tests/PhoneStore.Tests`, `tests/PhoneStore.SqlIntegration`.
- **Security Requirements**: Xác minh Swagger tắt khi `Production`, toàn bộ rate limit/timeout/auth tests PASS, không rò rỉ secret qua CI/Docker image, SQL business backup ngoài volume và rollback baseline sẵn sàng.
- **Tests**: Full `dotnet test`, `PhoneStore.SqlIntegration`, Docker full-stack rehearsal (`Deploy` + `Rollback`), kiểm tra tải nhẹ cho rate limiting & concurrency.
- **Risks**: Biến môi trường mới (khóa ký JWT, uploads mount) thiếu trên host business sẽ khiến container API fail-fast khi khởi động -> phải cập nhật và kiểm tra trước bằng `-ValidateOnly` và rehearsal cách ly.
- **Acceptance Criteria**: CI xanh, GHCR digest mới được duyệt, full-stack rehearsal PASS 100%, business rollout PASS kèm cửa sổ giám sát ổn định.

---

## J. Final Assessment

1. **Điểm mạnh kiến trúc hiện có**: `PhoneStore.Api` không phải là một API trống chỉ có `GET` sản phẩm — nó đã có nền tảng bảo mật fail-closed xuất sắc, RFC 7807 `ProblemDetails`, correlation ID, rate limiting/timeouts trên public endpoints, **9 Controllers với 23 route mappings**, và tầng `PhoneStore.Business` / `PhoneStore.Data` đã tách lớp rất sạch với đầy đủ transaction `Serializable`, `sp_getapplock`, `UPDLOCK` và idempotency.
2. **Nút thắt lớn nhất hiện tại**: Toàn bộ **16 protected endpoints đã triển khai** đều đang bị khóa `401 Unauthorized` tại runtime vì chưa có local token issuer (`ApiAuthentication:Oidc:Enabled = false`), đồng thời **Admin WinForms vẫn đang truy cập SQL Server 100% trực tiếp** và một số nghiệp vụ đã có ở tầng Business/Data (`Brands/Categories` CRUD, `Product Images`, `POST /api/orders`, `cancel`, `Customer Update`, `Dashboard`, `Period Revenue`, `Auth/Profile`) chưa được expose qua REST API.
3. **Kết luận**: Kiến trúc hiện tại **rất thuận lợi để mở rộng gia tăng mà không cần refactor lớn**, miễn là thực hiện **Authentication & Token Issuance (`S35.4B`) trước tiên** để mở khóa an toàn cho các protected endpoints đã có và sắp bổ sung.
