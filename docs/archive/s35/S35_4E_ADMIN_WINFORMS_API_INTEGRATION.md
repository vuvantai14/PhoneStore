# S35.4E — Admin WinForms REST API Integration

- **Execution Date**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`main`)
- **Stage Status**: **`PASS`**

---

## 1. Kiến trúc Dual Data Access Mode (`DirectSql | RestApi`)

- **Cấu hình (`src/PhoneStore.Admin/appsettings.json:2-6`, `src/PhoneStore.Business/AdminApi/AdminDataAccessOptions.cs:3-76`)**:
  - Bổ sung section `AdminDataAccess` với `Mode = DirectSql | RestApi`, `ApiBaseUrl = "http://localhost:5025"`, `TimeoutSeconds = 15`.
  - Mặc định giữ **`DirectSql`** cho tới khi `RestApi` được phê duyệt rollout business.
  - Cấu hình sai giá trị `Mode`, `ApiBaseUrl` (không phải `http`/`https`, chứa credentials/query/fragment) hoặc `TimeoutSeconds` ngoài `[1, 120]` sẽ **fail-closed** (`InvalidOperationException`) ngay lúc khởi động.
- **Tách biệt tuyệt đối trong DI (`src/PhoneStore.Admin/AdminServices.cs:21-85`)**:
  - `DirectSql`: giữ nguyên 100% đăng ký `PhoneStoreDbContext`, `AuthUserStore`, `AdminAuthService`, `LocalProductImageStorage` và các service nghiệp vụ hiện tại.
  - `RestApi`: **không đăng ký `PhoneStoreDbContext`** vào DI container, không yêu cầu chuỗi kết nối SQL hay thư mục local `ProductImages:UploadsDirectory`, loại bỏ hoàn toàn khả năng tự fallback âm thầm sang SQL khi gặp `401`/`403`, timeout, lỗi mạng hoặc lỗi ghi dữ liệu.
- **Không thay đổi UI/UX WinForms**: toàn bộ 7 nhóm chức năng Admin tiếp tục đi qua `CatalogManagementGateway` và 6 interface nghiệp vụ hiện có (`IAdminAuthService`, `ILookupManagementService`, `IProductManagementService`, `ICustomerManagementService`, `IOrderManagementService`, `IReportingService`).

---

## 2. Xác thực Admin, HTTP Client & 7 Nhóm chức năng

- **Session & Token Management (`src/PhoneStore.Business/AdminApi/AdminApiSessionStore.cs:16-66`, `src/PhoneStore.Business/AdminApi/AdminApiClient.cs:18-323`)**:
  - Đăng nhập qua `POST /api/v1/auth/admin/login`; từ chối token Customer (`AdminLoginStatus.Forbidden`, `AdminApiSessionStore.Establish` chặn `Role != "Admin"`).
  - Lưu access/refresh token thuần trong RAM (`AdminApiSessionStore`), **không** ghi plaintext ra `appsettings.json`, ổ đĩa hoặc log.
  - Tự động refresh trước hạn (`30s` skew, khóa `SemaphoreSlim` chống gọi refresh trùng) và retry duy nhất với `GET` khi `401`; **không** tự retry `POST`/`PUT`/`PATCH`/`DELETE` khi gặp timeout/5xx/lỗi mạng.
  - Gửi header `X-Correlation-ID` cho mỗi request, parse chuẩn `application/problem+json` (`detail`, `errors`, `title`), quản lý vòng đời `HttpClient` qua `SocketsHttpHandler` (`PooledConnectionLifetime = 2m`).
  - Đăng xuất (`AdminLoginGateway.LogoutAsync`, `AdminApplicationContext.OnLogout` & `OnShellClosed`) gọi `POST /api/v1/auth/logout` thu hồi refresh session trên server và xóa trắng token bộ nhớ.
  - Khi gặp `401`/`403`, `CatalogManagementGateway` (`src/PhoneStore.Admin/Management/CatalogManagementGateway.cs:40-80`) lập tức xóa session và phát `AuthorizationRevoked` đưa người dùng về `LoginForm`.
- **7 nhóm chức năng tích hợp (`src/PhoneStore.Business/AdminApi/RestAdminServices.cs:1-349`)**:
  1. **Dashboard**: `RestReportingService.GetDashboardAsync` -> `GET /api/v1/admin/dashboard`.
  2. **Reports**: `RestReportingService.GetReportAsync` & `GetRevenueSeriesAsync` (`Day`, `Week`, `Month`, `Year`, `CustomRange`) -> `/api/v1/admin/reports/{overview,revenue,top-products}`.
  3. **Brands & Categories**: `RestLookupManagementService` -> `/api/v1/admin/brands`, `/api/v1/admin/categories`.
  4. **Products & Options**: `RestProductManagementService` -> `/api/v1/admin/products`, `/options` (hỗ trợ cả `productId` và `includeInactive=true`, giữ nguyên optimistic stock concurrency `OriginalStockQuantity`).
  5. **Customers**: `RestCustomerManagementService` -> `/api/v1/admin/customers` (`GET`, `GET /{id}`, `PUT /{id}`, `PATCH /{id}/status`).
  6. **Orders**: `RestOrderManagementService` -> `/api/v1/admin/orders` (`GET`, `GET /{id}`, `PATCH /{id}/status`).
  7. **Product Images (`multipart/form-data`)**: `RestProductManagementService.AddImageAsync` đọc stream file chọn từ máy client và upload qua `POST /api/v1/admin/products/{id}/images`; backend xác thực nội dung và lưu file, trả đường dẫn tương đối `/uploads/products/<guid>.<ext>` dùng chung cho Customer Web (Admin không tự ghi vào Docker filesystem).

---

## 3. Kết quả kiểm thử S35.4E, Full Regression & Live SQL Server 2022 + Linux Docker

- `dotnet build PhoneStore.sln -c Release --no-restore` (8/8 project gồm cả `PhoneStore.Admin` `net10.0-windows`): **PASS (`0 Warning(s)`, `0 Error(s)`)**.
- **Full Regression Suite (`PhoneStore.Tests`)**: **PASS (`341/341`, `0 Failed`, `0 Skipped`)**, bao gồm `AdminRestApiIntegrationTests` kiểm thử đầy đủ `DirectSql` mặc định, fail-closed cấu hình sai, `RestApi` end-to-end cả 7 nhóm chức năng Admin, proactive JWT refresh, logout revocation, API unavailable, timeout không retry write, `403` clear session và chặn token Customer.
- **Live SQL Server 2022 + Linux Docker API (`phonestore-s354cde-api`, `20/20 PASS`)**:
  - Build image Linux Docker từ source S35.4C–E: `sha256:806ddf3e7dd29e1b99517f0a2fdc1d238c64aa6943507720a6fcc99da6388394` (PASS).
  - RAM Gates: `SqlColdStart/Pre` `4.00 GiB` Win / `5.40 GiB` WSL, `SqlProvision/Pre` `2.75 GiB`, `StartApi/Pre` `2.25 GiB`, `Post` `2.41 GiB`, `Swap = 0 MiB`.
  - Xác minh thật trên SQL Server 2022 (`ac151ca35236`) + Linux Docker API (`5814cdf82f5b`): Brands/Categories CRUD + duplicate `409`, Product CRUD + optimistic stock conflict `409`, Linux multipart image upload (`sp_getapplock` + `CrossPlatformProductImageContentValidator`, `.exe` bị chặn `400`, PNG upload `201`), Public Catalog `brandIds/rams/storages` + `newest` + `related`, Concurrent Customer Idempotent Checkout (`2` request song song tạo đúng `1` đơn, trừ tồn `1` lần), Customer cancel hoàn tồn nguyên tử (`204`), Admin Order detail (`CustomerEmail`, `NextStatuses`) & chuyển trạng thái đầy đủ (`204,204,204`), Admin Customer `PUT` (`204`) + bảo vệ tài khoản Admin (`404`), Dashboard & Revenue `Day/Week/Month/Year` (`buckets=(24,7,30,12)`, loại trừ đơn hủy), Admin REST refresh rotation (`200`) & logout revocation (`204 -> 401`).
  - Cleanup sạch `0` container/network/volume fixture và xóa image tạm; 3 containers business (`phonestore-restore-c2-{db,api,web}-1`) khớp tuyệt đối chữ ký trước/sau (`Exact signature match`).

