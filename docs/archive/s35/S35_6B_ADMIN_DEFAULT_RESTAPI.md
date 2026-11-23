# S35.6B — Admin WinForms Default REST API & Runtime Acceptance

- **Ngày thực hiện**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`main`)
- **Default Admin Data Access Mode**: `AdminDataAccess:Mode = RestApi`
- **Default REST API BaseUrl**: `http://127.0.0.1:5025` (`http://127.0.0.1:5025/`)
- **Fallback Mode (Cấu hình thủ công)**: `AdminDataAccess:Mode = DirectSql` (không tự động fallback khi `RestApi` lỗi)
- **Final Status**: **`VERIFIED — PASS` (`SOURCE + UNIT + INTEGRATION + RUNTIME VERIFIED`; Interactive Desktop GUI Click-Through: `MANUAL ACCEPTANCE PENDING`)**

---

## A. Existing Configuration (Trước S35.6B)

Kiểm toán mã nguồn thực tế của `PhoneStore.Admin` và `PhoneStore.Business/AdminApi` trước khi chuyển mặc định:

1. **`src/PhoneStore.Admin/appsettings.json`**:
   - Trước S35.6B, `AdminDataAccess:Mode` đang đặt mặc định là `"DirectSql"` và `ApiBaseUrl` là `"http://localhost:5025"`, kèm `ConnectionStrings:PhoneStoreConnection` trỏ vào `Server=localhost;Database=PhoneStoreDB;Trusted_Connection=True;...` (Windows Host SQL Server `MSSQLSERVER`).
2. **`src/PhoneStore.Business/AdminApi/AdminDataAccessOptions.cs`**:
   - Khi `AdminDataAccess:Mode` bị bỏ trống trong cấu hình, `AdminDataAccessOptions.Parse` mặc định trả về `AdminDataAccessMode.DirectSql`.
3. **`src/PhoneStore.Admin/AdminServices.cs:43-87`**:
   - Khi `dataAccess.Mode == AdminDataAccessMode.RestApi`:
     - Đăng ký `AdminApiSessionStore`, `HttpClient` (`AdminApiClient.CreateHttpClient`), `AdminApiClient`, `RestAdminAuthService` (`IAdminAuthService` + `IAdminSessionLogout`), `RestLookupManagementService` (`ILookupManagementService`), `RestProductManagementService` (`IProductManagementService`), `RestCustomerManagementService` (`ICustomerManagementService`), `RestOrderManagementService` (`IOrderManagementService`), và `RestReportingService` (`IReportingService`).
     - **Không đăng ký `PhoneStoreDbContext`**, `AuthUserStore`, hay bất kỳ SQL store nào trong DI container.
4. **Kiểm toán 25 file `.cs` trong `src/PhoneStore.Admin/`**:
   - Quét toàn bộ `src/PhoneStore.Admin` tìm `PhoneStoreDbContext`, `PhoneStore.Data`, `SqlConnection`, `UseSqlServer`: **chỉ duy nhất `AdminServices.cs` (nhánh `else` của `DirectSql`) có tham chiếu**; toàn bộ `24/25` file còn lại (`LoginForm`, `MainForm`, `AdminApplicationContext`, `AdminLoginGateway`, `CatalogManagementGateway`, toàn bộ 7 `Controls/*` và 5 `Dialogs/*`) có **`0` tham chiếu trực tiếp tới SQL/EF Core**.

---

## B. Changed Files

| File | Thay đổi chính |
|---|---|
| [appsettings.json](file:///c:/PhoneStore/PhoneStore/src/PhoneStore.Admin/appsettings.json) | Chuyển mặc định `"Mode": "RestApi"` và `"ApiBaseUrl": "http://127.0.0.1:5025"`, giữ nguyên `ConnectionStrings` và `ProductImages` cho chế độ dự phòng thủ công `DirectSql`. |
| [AdminDataAccessOptions.cs](file:///c:/PhoneStore/PhoneStore/src/PhoneStore.Business/AdminApi/AdminDataAccessOptions.cs) | Chuyển giá trị mặc định của `Mode` sang `AdminDataAccessMode.RestApi` và `ApiBaseUrl` sang `"http://127.0.0.1:5025"`, giữ nguyên kiểm tra fail-closed và hỗ trợ cấu hình thủ công `"DirectSql"`. |
| [AdminApiClient.cs](file:///c:/PhoneStore/PhoneStore/src/PhoneStore.Business/AdminApi/AdminApiClient.cs) | Bổ sung phân biệt tường minh mã `HTTP 429 TooManyRequests` trong `LoginAsync`, `EnsureAuthorizedAndSuccessAsync` và `MapManagementResponseAsync` với thông báo tiếng Việt rõ ràng (bên cạnh `401`, `403`, timeout và lỗi mạng). |
| [Publish-AdminArtifact.ps1](file:///c:/PhoneStore/PhoneStore/scripts/ci/Publish-AdminArtifact.ps1) | Đóng gói `AdminDataAccess = @{ Mode = 'RestApi'; ApiBaseUrl = 'http://127.0.0.1:5025'; TimeoutSeconds = 15 }` làm mặc định cho bản publish artifact `PhoneStore.Admin`. |
| [AdminRestApiIntegrationTests.cs](file:///c:/PhoneStore/PhoneStore/tests/PhoneStore.Tests/AdminRestApiIntegrationTests.cs) | Cập nhật kiểm thử mặc định `RestApi` (`http://127.0.0.1:5025/`), kiểm thử chọn thủ công `DirectSql`, và bổ sung kiểm thử xử lý `429 TooManyRequests`. |
| [S35_6B_ADMIN_DEFAULT_RESTAPI.md](file:///c:/PhoneStore/PhoneStore/docs/archive/s35/S35_6B_ADMIN_DEFAULT_RESTAPI.md) | Báo cáo nghiệm thu S35.6B. |
| [PROGRESS.md](file:///c:/PhoneStore/PhoneStore/docs/PROGRESS.md) | Cập nhật tiến độ dự án. |

---

## C. Default RestApi Configuration

Sau khi cập nhật, cả 3 tệp cấu hình nguồn và đầu ra build (`src/PhoneStore.Admin/appsettings.json`, `bin/Debug/net10.0-windows/appsettings.json`, `bin/Release/net10.0-windows/appsettings.json`) đều đồng nhất:

```json
{
  "AdminDataAccess": {
    "Mode": "RestApi",
    "ApiBaseUrl": "http://127.0.0.1:5025",
    "TimeoutSeconds": 15
  },
  "ConnectionStrings": {
    "PhoneStoreConnection": "Server=localhost;Database=PhoneStoreDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "ProductImages": {
    "UploadsDirectory": "../../../../PhoneStore.Web/wwwroot/uploads/products"
  }
}
```

- **Không chứa bất kỳ thông tin nhạy cảm nào** (không có mật khẩu SQL, không có JWT `SigningKey`, không có hardcoded token).
- **Không tự động fallback sang `DirectSql`**: Khi `Mode = RestApi`, `PhoneStoreDbContext` không tồn tại trong `ServiceProvider`; nếu `http://127.0.0.1:5025` dừng hoạt động, `AdminApiClient.SendCoreAsync` ném `AdminApiException("Không thể kết nối tới PhoneStore API. Vui lòng kiểm tra dịch vụ API.")` và `LoginForm` hiển thị trực tiếp thông báo lỗi kết nối đó.
- **Cách chuyển thủ công về `DirectSql` khi cần**: Đặt `"Mode": "DirectSql"` trong `appsettings.json` hoặc biến môi trường `AdminDataAccess__Mode=DirectSql`.

---

## D. JWT Login & Session Handling

Luồng xác thực và quản lý phiên trong chế độ `RestApi`:

1. **Đăng nhập tự động lấy JWT (`AdminApiClient.LoginAsync`, dòng `53-89`)**:
   - Khi người dùng nhập Email + Mật khẩu trên `LoginForm` và bấm **Đăng nhập**, `AdminLoginGateway.LoginAsync` gọi `RestAdminAuthService.LoginAsync` -> gửi `POST http://127.0.0.1:5025/api/v1/auth/admin/login`.
   - Người dùng **không bao giờ phải lấy hay dán JWT thủ công từ Swagger**.
2. **Lưu trữ phiên chỉ trong RAM (`AdminApiSessionStore.cs:17-68`)**:
   - `AccessToken`, `AccessExpiresAtUtc`, `RefreshToken`, `RefreshExpiresAtUtc` và `AdminIdentity` chỉ lưu trong biến bộ nhớ `_current` được bảo vệ bởi `lock (_gate)`.
   - Chốt chặn RBAC kép: Nếu phản hồi API hoặc identity có `Role != "Admin"`, `AdminApiSessionStore.Establish` từ chối lưu và ném `UnauthorizedAccessException`.
   - Không bao giờ ghi `AccessToken`, `RefreshToken` hay mật khẩu ra đĩa hoặc log.
3. **Tự động đính kèm Bearer Token & Xoay vòng Refresh Token (`AdminApiClient.cs:130-297`)**:
   - Mọi request bảo vệ đều đi qua `EnsureFreshTokenAsync` và `CreateRequest(..., authenticated: true)` để tự động gắn header `Authorization: Bearer {accessToken}` và `X-Correlation-ID`.
   - Khi `AccessToken` còn `<= 30 giây` trước hạn (`ProactiveRefreshSkew`) hoặc khi một lệnh `GET` nhận `401 Unauthorized`, `TryRefreshSessionAsync` dùng `SemaphoreSlim(1, 1)` gọi `POST /api/v1/auth/refresh` để nhận cặp token mới.
4. **Phân biệt rõ mã lỗi HTTP & mạng (`AdminApiClient.cs:59-77,305-372`)**:
   - `401 Unauthorized` (khi login): `"Email hoặc mật khẩu không chính xác."`
   - `401 Unauthorized` (trong phiên sau khi refresh thất bại): Xóa session RAM, bắn sự kiện `AuthorizationRevoked` trên `CatalogManagementGateway`, đóng `MainForm` và quay về `LoginForm`.
   - `403 Forbidden`: `"Tài khoản không có quyền truy cập hệ thống quản trị."` (khi login) hoặc xóa session ngay lập tức khi quyền Admin bị thu hồi.
   - `429 TooManyRequests`: `"Quá nhiều yêu cầu đăng nhập trong thời gian ngắn. Vui lòng đợi ít giây rồi thử lại."` / `"Thao tác quá nhanh (vượt giới hạn tần suất API). Vui lòng đợi ít giây rồi thử lại."` (giữ nguyên session, không retry mù quáng).
   - `TaskCanceledException` (timeout): `"Yêu cầu tới PhoneStore API đã hết thời gian chờ."` (`IsTimeout = true`, **0 retry trên các thao tác ghi `POST/PUT/PATCH/DELETE`**).
   - `HttpRequestException` (lỗi mạng / API dừng): `"Không thể kết nối tới PhoneStore API. Vui lòng kiểm tra dịch vụ API."` (**không fallback sang SQL**).
5. **Đăng xuất (`AdminApplicationContext.cs:62-75,100-112`, `AdminApiClient.cs:113-134`)**:
   - Khi bấm nút **Đăng xuất** hoặc đóng `MainForm`, ứng dụng gọi `POST /api/v1/auth/logout` kèm `Bearer` token và `refreshToken` để thu hồi family token trên bảng `ApiRefreshTokens`, đồng thời luôn gọi `_sessions.Clear()` trong khối `finally`.

---

## E. Seven-Screen Capability Matrix

Bảng đối chiếu toàn bộ **7 màn hình** của `PhoneStore.Admin` với các REST API endpoints và cấp độ xác minh:

| Màn hình WinForms | Control / Dialog | Dịch vụ Adapter (`RestApi`) | REST API Endpoints gọi tới | Cấp độ xác minh |
|---|---|---|---|---|
| **1. Tổng quan (Dashboard)** | `DashboardControl` | `RestReportingService` | `GET /api/v1/admin/dashboard`<br>`GET /api/v1/admin/reports/revenue?period={Week\|Month\|Year\|CustomRange}` | `SOURCE VERIFIED`<br>`UNIT VERIFIED`<br>`INTEGRATION VERIFIED`<br>`RUNTIME VERIFIED (5025 Read)`<br>`MANUAL ACCEPTANCE PENDING (GUI)` |
| **2. Sản phẩm (Products)** | `ProductsManagementControl`<br>`ProductEditDialog`<br>`ProductImagesDialog` | `RestProductManagementService` | `GET /api/v1/admin/products`<br>`GET /api/v1/admin/products/options`<br>`GET /api/v1/admin/products/{id}`<br>`POST /api/v1/admin/products`<br>`PUT /api/v1/admin/products/{id}`<br>`PATCH /api/v1/admin/products/{id}/status`<br>`GET /api/v1/admin/products/{id}/images`<br>`POST /api/v1/admin/products/{id}/images`<br>`DELETE /api/v1/admin/products/{id}/images/{imageId}`<br>`PATCH .../primary` & `PATCH .../order` | `SOURCE VERIFIED`<br>`UNIT VERIFIED`<br>`INTEGRATION VERIFIED`<br>`RUNTIME VERIFIED (5025 Read / 5026 Write)`<br>`MANUAL ACCEPTANCE PENDING (GUI)` |
| **3. Thương hiệu (Brands)** | `LookupManagementControl(Brand)`<br>`LookupEditDialog` | `RestLookupManagementService` | `GET /api/v1/admin/brands`<br>`POST /api/v1/admin/brands`<br>`PUT /api/v1/admin/brands/{id}`<br>`PATCH /api/v1/admin/brands/{id}/status` | `SOURCE VERIFIED`<br>`UNIT VERIFIED`<br>`INTEGRATION VERIFIED`<br>`RUNTIME VERIFIED (5025 Read / 5026 Write)`<br>`MANUAL ACCEPTANCE PENDING (GUI)` |
| **4. Danh mục (Categories)** | `LookupManagementControl(Category)`<br>`LookupEditDialog` | `RestLookupManagementService` | `GET /api/v1/admin/categories`<br>`POST /api/v1/admin/categories`<br>`PUT /api/v1/admin/categories/{id}`<br>`PATCH /api/v1/admin/categories/{id}/status` | `SOURCE VERIFIED`<br>`UNIT VERIFIED`<br>`INTEGRATION VERIFIED`<br>`RUNTIME VERIFIED (5025 Read / 5026 Write)`<br>`MANUAL ACCEPTANCE PENDING (GUI)` |
| **5. Khách hàng (Customers)** | `CustomersManagementControl`<br>`CustomerEditDialog` | `RestCustomerManagementService` | `GET /api/v1/admin/customers`<br>`GET /api/v1/admin/customers/{id}`<br>`PUT /api/v1/admin/customers/{id}`<br>`PATCH /api/v1/admin/customers/{id}/status` | `SOURCE VERIFIED`<br>`UNIT VERIFIED`<br>`INTEGRATION VERIFIED`<br>`RUNTIME VERIFIED (5025 Read)`<br>`MANUAL ACCEPTANCE PENDING (GUI)` |
| **6. Đơn hàng (Orders)** | `OrdersManagementControl`<br>`OrderDetailDialog` | `RestOrderManagementService` | `GET /api/v1/admin/orders`<br>`GET /api/v1/admin/orders/{id}`<br>`PATCH /api/v1/admin/orders/{id}/status` | `SOURCE VERIFIED`<br>`UNIT VERIFIED`<br>`INTEGRATION VERIFIED`<br>`RUNTIME VERIFIED (5025 Read)`<br>`MANUAL ACCEPTANCE PENDING (GUI)` |
| **7. Báo cáo (Reports)** | `ReportsControl`<br>`ReportRevenueChartPanel` | `RestReportingService` | `GET /api/v1/admin/reports/overview?includeAlerts=true`<br>`GET /api/v1/admin/reports/top-products`<br>`GET /api/v1/admin/reports/revenue?period={Day\|Week\|Month\|Year\|CustomRange}` | `SOURCE VERIFIED`<br>`UNIT VERIFIED`<br>`INTEGRATION VERIFIED`<br>`RUNTIME VERIFIED (5025 Read)`<br>`MANUAL ACCEPTANCE PENDING (GUI)` |

---

## F. Build & Test Results

| Lệnh kiểm tra | Phạm vi | Kết quả |
|---|---|---|
| `dotnet build PhoneStore.sln -c Release --no-restore` | Toàn bộ 8 projects trong solution (gồm `PhoneStore.Admin` `net10.0-windows`) | **PASS (`0 Warning(s), 0 Error(s)`)** |
| `dotnet build src/PhoneStore.Admin/PhoneStore.Admin.csproj -c Debug --no-restore` | Cấu hình `Debug` phục vụ `dotnet run` | **PASS (`0 Warning(s), 0 Error(s)`)** |
| `dotnet test` (`AdminRestApiIntegrationTests`, `LocalJwtAuthenticationTests`, `ApiAuthorizationTests`, `ApiSecurityHardeningTests`) | Kiểm thử tích hợp Admin REST client (7 màn hình, JWT refresh, logout, 401/403/429/timeout/network error) & bảo mật API | **PASS (`50/50`, `0` Failed, `0` Skipped, `5s`)** |
| `git diff --check` | Kiểm tra định dạng whitespace / conflict markers | **PASS (`0` errors)** |

---

## G. Runtime Verification

Đã thực thi kịch bản kiểm chứng runtime thực tế (`Invoke-S356B-RuntimeAndDataAudit.ps1`):

1. **Khởi động `PhoneStore.Admin` bằng `dotnet run` (`src/PhoneStore.Admin`)**:
   - Lệnh: `dotnet run --no-build` tại `C:\PhoneStore\PhoneStore\src\PhoneStore.Admin`.
   - Tiến trình WinForms `PhoneStore.Admin.exe` (`PID = 26804`) khởi tạo thành công với cấu hình mặc định `Mode = RestApi`, `ApiBaseUrl = http://127.0.0.1:5025`.
   - Cửa sổ `LoginForm` mở bình thường (`Responding = True`, `MainWindowTitle = 'Đăng nhập quản trị · PhoneStore Admin'`, `MainWindowHandle = 854032`), không phát sinh kết nối SQL trực tiếp và đóng sạch (`ExitCode = 0`).
2. **Kiểm chứng Read-Only toàn bộ 7 màn hình trên Business API `http://127.0.0.1:5025`**:
   - **Screen A (Dashboard)**: `HTTP 200` — `ActiveProducts = 40`, `TotalStock = 688`, `Customers = 2`, `Orders = 15`, `PendingOrders = 3`, `CompletedOrders = 8`, `CompletedRevenue = 661,130,000.00 ₫`.
   - **Screen B (Products)**: `List = 200` (`Total = 40`), `FilterOptions = 200`, `EditOptions = 200`, `Detail(40) = 200`, `Images(40) = 200`.
   - **Screen C (Brands)**: `List = 200` (`Count = 8`), `Search('Samsung') = 200`.
   - **Screen D (Categories)**: `List = 200` (`Count = 2`).
   - **Screen E (Customers)**: `List = 200` (`Total = 2`), `Detail(2) = 200`.
   - **Screen F (Orders)**: `List = 200` (`Total = 15`), `Detail(1) = 200`.
   - **Screen G (Reports)**: `Overview = 200`, `TopProducts = 200`, `Revenue(Day/Week/Month/Year/CustomRange) = 200`.
3. **Kiểm chứng Write & Session Lifecycle trên Fixture Cách ly `http://127.0.0.1:5026` (`PhoneStoreDB_SwaggerDev`)**:
   - `POST /api/v1/auth/admin/login` (`200`) -> `POST /api/v1/admin/brands` (`201 Created`, `id = 4`) -> `PUT /api/v1/admin/brands/4` (`204 NoContent`) -> `POST /api/v1/auth/refresh` (`200`) -> `POST /api/v1/auth/logout` (`204`) -> gửi lại refresh token sau logout bị chặn (`401 Unauthorized`).

---

## H. Data Source Comparison (Giải thích nguyên nhân `12` Đơn hàng vs `15` Đơn hàng)

Đã truy vấn chỉ đọc (`SELECT`) trên cả hai nguồn dữ liệu để xác định chính xác nguyên nhân chênh lệch số lượng đơn hàng giữa ảnh chụp Admin trước đây (`12` đơn hàng) và môi trường Docker Business S35.5 (`15` đơn hàng):

| Tiêu chí đối chiếu | Windows Host SQL Server (`localhost:1433` / `MSSQLSERVER`) | Docker Business SQL (`127.0.0.1:14334` / `phonestore-restore-c2-db-1`) & REST API `5025` |
|---|---|---|
| **Cách kết nối trước đây** | Mặc định cũ khi chạy `dotnet run` với `Mode = DirectSql` (`Server=localhost;Database=PhoneStoreDB;Trusted_Connection=True`) | Chỉ kết nối khi dùng `scripts/run-admin-docker.ps1` hoặc gọi qua Docker API `5025` |
| **Cách kết nối từ S35.6B** | **Không còn sử dụng mặc định** | **Mặc định khi chạy `dotnet run`** (`Mode = RestApi` -> `http://127.0.0.1:5025` -> `phonestore-restore-c2-db-1`) |
| **Tổng số Sản phẩm (`Products`)** | `40` | `40` |
| **Tổng số Khách hàng (`RoleId = 2`)** | `2` | `2` |
| **Tổng số Đơn hàng (`Orders`)** | **`12`** (`Orders 1..12`, đơn mới nhất lúc `2026-11-13 19:39:10`) | **`15`** (`Orders 1..12` + **3 đơn phát sinh ngày `2026-11-14` trên Docker**: `Order 13`, `Order 14`, `Order 1013`) |
| **Phân bố trạng thái Đơn hàng** | `Completed = 5`, `Pending = 3`, `Confirmed = 1`, `Cancelled = 3` | `Completed = 8`, `Pending = 3`, `Confirmed = 1`, `Cancelled = 3` (Đơn `#9` được hoàn tất + Đơn `#13` và `#1013` hoàn tất, Đơn `#14` đang chờ xử lý) |

### Chi tiết 3 đơn hàng mới chỉ có trên Docker Business (`phonestore-restore-c2-db-1` / API `5025`):

1. **`OrderId = 13`** (`ORD-86DC8D62EFCDE2B7AC5BF86064`): `Completed`, `2026-11-14 06:28:16 UTC`, `79,960,000.00 ₫`
2. **`OrderId = 14`** (`ORD-5397F25DCB7EB9EE5086B3CD41`): `Pending`, `2026-11-14 07:28:56 UTC`, `54,970,000.00 ₫`
3. **`OrderId = 1013`** (`ORD-0DAE1B2993C637F568E72AD32A`): `Completed`, `2026-11-14 12:23:55 UTC`, `61,960,000.00 ₫`
*(Cùng với `OrderId = 9` `ORD-A0DA911FFBFA8BF6A22783E368` đã chuyển từ `Pending` sang `Completed` trên Docker Business).*

**Kết luận**: Việc chuyển mặc định của `PhoneStore.Admin` sang `Mode = RestApi` (`http://127.0.0.1:5025`) đã giải quyết triệt để hiện tượng lệch nguồn dữ liệu — giờ đây khi chạy `dotnet run`, Admin WinForms luôn đọc đúng dữ liệu trực tiếp từ Docker Business API `5025` (`40` sản phẩm, `2` khách hàng, `15` đơn hàng), đồng bộ 100% với Customer Web `5015`.

---

## I. Business Invariants

Đối chiếu trạng thái trước và sau toàn bộ quá trình kiểm thử S35.6B:

- **Chữ ký 3 containers business (`phonestore-restore-c2`)**: Khớp 100% (`Signature Match = True`):
  - `phonestore-restore-c2-db-1`: `Id = addd99b34bf2...`, `StartedAt = 2026-11-21T00:27:43.48526107Z`, `Health = healthy`, `Restarts = 0`.
  - `phonestore-restore-c2-api-1`: `Id = 12a06d6e6694...`, `StartedAt = 2026-11-21T10:09:05.598296081Z`, `Health = healthy`, `Restarts = 0`.
  - `phonestore-restore-c2-web-1`: `Id = 3595f703f7a5...`, `StartedAt = 2026-11-21T10:06:19.636571761Z`, `Health = healthy`, `Restarts = 0`.
- **Dữ liệu `PhoneStoreDB` business**: `Products = 40`, `Orders = 15`, `Users = 3`, `ActiveAdmins = 1`, `ApiRefreshTokens = 0` (không có bất kỳ thao tác ghi nào lên DB business).
- **Swagger Development (`localhost:5026`)**: Tiếp tục hoạt động độc lập (`HTTP 200`), trong khi `/swagger` trên Business API `5025` vẫn đóng (`HTTP 404`).

---

## J. Manual Acceptance Checklist (Dành cho người dùng)

Vì quy tắc an toàn không cho phép tự động đoán hoặc nhập mật khẩu Admin business lên giao diện GUI, phần thao tác tương tác trực tiếp trên cửa sổ WinForms được ghi nhận là **`MANUAL ACCEPTANCE PENDING`**. Bạn có thể nghiệm thu ngay bằng các bước sau:

1. Mở terminal và chạy:
   ```powershell
   cd C:\PhoneStore\PhoneStore\src\PhoneStore.Admin
   dotnet run
   ```
2. Trên cửa sổ **Đăng nhập quản trị · PhoneStore Admin**, nhập email và mật khẩu tài khoản Admin business rồi bấm **Đăng nhập**.
3. Tại màn hình **Tổng quan (Dashboard)**: Xác nhận hiển thị **`40` sản phẩm**, **`2` khách hàng**, **`15` đơn hàng** (`8` hoàn tất, `3` chờ xử lý) và doanh thu **`661.130.000 ₫`**.
4. Chuyển qua các tab **Sản phẩm** (`40` sản phẩm), **Thương hiệu** (`8` thương hiệu), **Danh mục** (`2` danh mục), **Khách hàng** (`2` khách hàng), **Đơn hàng** (`15` đơn hàng), và **Báo cáo** để kiểm tra dữ liệu tải mượt qua REST API `http://127.0.0.1:5025`.
5. Bấm **Đăng xuất** ở góc dưới thanh bên trái để xác nhận ứng dụng xóa phiên JWT và quay về màn hình đăng nhập.

---

## K. Remaining Risks

1. **Kết nối Loopback `http://127.0.0.1:5025`**:
   - URL mặc định `http://127.0.0.1:5025` chỉ dành cho trường hợp chạy `PhoneStore.Admin` trên cùng máy host với Docker Business. Nếu triển khai `PhoneStore.Admin` trên một máy trạm khác qua mạng LAN/Internet, bắt buộc phải đặt Reverse Proxy HTTPS phía trước `PhoneStore.Api` và cập nhật `AdminDataAccess:ApiBaseUrl` sang `https://...`.
2. **Không tự động Fallback**:
   - Nếu container `phonestore-restore-c2-api-1` dừng, `PhoneStore.Admin` sẽ báo lỗi kết nối API chứ không tự động chuyển sang SQL trực tiếp (đúng thiết kế fail-closed để tránh ghi nhầm vào Windows SQL Server `localhost`).

---

## L. Final Decision

**`PASS` (`SOURCE + UNIT + INTEGRATION + RUNTIME VERIFIED`; Interactive GUI Click-Through: `MANUAL ACCEPTANCE PENDING`) — `PhoneStore.Admin` đã chuyển mặc định sang `RestApi` kết nối `http://127.0.0.1:5025`, tự động quản lý phiên JWT trong bộ nhớ và đồng bộ 100% nguồn dữ liệu với Docker Business.**
