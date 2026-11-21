# S35.4F — Final REST API Regression, Security Audit & Docker Release Readiness

- **Ngày thực hiện**: 2026-11-21
- **Workspace**: `C:\PhoneStore\PhoneStore`
- **Repository**: `vuvantai14/PhoneStore`
- **Branch / Base Commit**: `main` @ `70c9cd4bab2a8cb33e143fb0ecbd0bc46288612a`
- **Phạm vi nghiệm thu**: Toàn bộ chuỗi `S35.4A` → `S35.4F` (Local JWT Authentication, Product/Brand/Category/Image Management API, Customer/Admin Order & Profile/Customer & Dashboard/Reporting API, Admin WinForms Dual Mode `DirectSql | RestApi`).
- **Quyết định cuối cùng**:
  - **1. GO FOR COMMIT/PUSH**: **`GO`** (Sẵn sàng commit và push một lần lên `origin/main`).
  - **2. GO FOR BUSINESS DEPLOYMENT**: **`NO-GO / CONDITIONAL`** (Chưa triển khai lên stack business `phonestore-restore-c2`; chờ phê duyệt riêng cho backup + migration `AddApiRefreshTokens`, cấu hình shared volume ảnh `uploads`, cấp phát `SigningKey` production + Reverse Proxy TLS, và nghiệm thu GUI thủ công WinForms chế độ `RestApi`).

---

## A. Executive Summary

Giai đoạn **S35.4F** đã thực hiện kiểm thử hồi quy toàn diện, kiểm toán bảo mật từ mã nguồn, kiểm thử tích hợp E2E full-stack thật trên **SQL Server 2022 + Linux Docker `PhoneStore.Api` + Linux Docker `PhoneStore.Web`**, kiểm chứng lưu trữ ảnh dùng chung (`api` mount `rw`, `web` mount `ro`) qua kịch bản tái tạo container (`--force-recreate`), kiểm chứng an toàn migration `Up`/`Down` và kiểm toán tiền commit trên toàn bộ working tree:

1. **Build & Full Regression**:
   - `dotnet restore PhoneStore.sln` & `dotnet build PhoneStore.sln -c Release --no-restore`: **PASS (8/8 projects, gồm `PhoneStore.Admin` `net10.0-windows`, `0 Warning(s)`, `0 Error(s)`)**.
   - 3 project phụ trong `.github/workflows/ci.yml` (`PhoneStore.CatalogImport`, `PhoneStore.AcceptanceEnvironment`, `PhoneStore.SqlIntegration`): **PASS (3/3, `0 Warning(s)`, `0 Error(s)`)**.
   - Full regression `PhoneStore.Tests` (Release): **`341/341 PASS` (`0` Failed, `0` Skipped, thời gian `7s`)** — tăng từ `298` test ở baseline đầu chuỗi S35.4 lên `341` test (`+43` test bảo mật/tích hợp mới).
2. **Real Full-Stack E2E (`SQL Server 2022` + `PhoneStore.Api` Linux + `PhoneStore.Web` Linux)**:
   - **`23/23 PASS`** trong project Docker cách ly `phonestore-s354f-e2e` (cộng với **`34/34 PASS`** ở S35.4B.1 và **`20/20 PASS`** ở S35.4E — tổng cộng **`77/77` kiểm chứng E2E thực tế trên SQL Server 2022 + Linux Docker**).
   - Xác minh trực tiếp shared volume `uploads_data`: Admin upload PNG qua REST API (`POST /api/v1/admin/products/{id}/images`) -> lưu metadata SQL -> Customer Web container (`read_only: true`) phục vụ ngay HTTP `200 image/png` khớp tuyệt đối mã băm SHA-256 và hiển thị trong trang `/products/{id}` -> **`--force-recreate` cả 2 container `api` và `web`** -> ảnh vẫn truy cập HTTP `200` khớp SHA-256.
3. **Zero Impact lên Business Stack**:
   - Cả 3 containers business (`phonestore-restore-c2-db-1`, `phonestore-restore-c2-api-1`, `phonestore-restore-c2-web-1`) giữ nguyên 100% `Id`, `Image`, `StartedAt`, `Health=healthy`, `RestartCount=0` và `Mounts`.
   - Sau khi kết thúc kiểm thử, `0` container, `0` network, `0` volume và `0` image tạm của fixture còn sót lại.

---

## B. Safety Preflight & Business Invariants

### 1. Phased Tiered ResourceGate Telemetry (`Invoke-S354F-FullStackValidation.ps1`)

| Giai đoạn | Ngưỡng yêu cầu (`WinFree` / `WslAvail` / `Swap`) | Thực tế đo được | Kết quả |
|---|---|---|---|
| `SqlColdStart / Pre` | `>= 3.80 GiB` / `>= 4.50 GiB` / `<= 128 MiB` | `WinFree = 4.44 GiB`, `WslAvail = 5.62 GiB`, `Swap = 0 MiB` | **PASS** |
| `SqlProvision / Pre` | `>= 2.20 GiB` / `>= 3.00 GiB` / `<= 128 MiB` | `WinFree = 2.95 GiB`, `WslAvail = 4.99 GiB`, `Swap = 0 MiB` | **PASS** |
| `StartApi / Pre` | `>= 2.20 GiB` / `>= 2.80 GiB` / `<= 128 MiB` | `WinFree = 2.92 GiB`, `WslAvail = 4.95 GiB`, `Swap = 0 MiB` | **PASS** |
| `StartWeb / Pre` | `>= 2.05 GiB` / `>= 2.60 GiB` / `<= 128 MiB` | `WinFree = 2.85 GiB`, `WslAvail = 4.90 GiB`, `Swap = 0 MiB` | **PASS** |
| `Post-E2E` | `BusinessHealthy = true`, `Swap <= 128 MiB` | `WinFree = 2.59 GiB`, `WslAvail = 4.77 GiB`, `Swap = 0 MiB` | **PASS** |

### 2. Business Stack Snapshot (`phonestore-restore-c2` — Trước và Sau S35.4F khớp 100%)

| Container | Container ID | Image Digest | StartedAt (UTC) | Health / Restarts | Mounts |
|---|---|---|---|---|---|
| `phonestore-restore-c2-db-1` | `addd99b34bf2...c50a6` | `sha256:11927f5edc51...73fd` | `2026-11-21T00:27:43.48526107Z` | `healthy` / `0` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:True` |
| `phonestore-restore-c2-api-1` | `cec828cd68c7...986f3` | `sha256:bf98725379df...050` | `2026-11-21T03:53:44.575875686Z` | `healthy` / `0` | *(none)* |
| `phonestore-restore-c2-web-1` | `ed6ef0f8027d...675b` | `sha256:7e2970be3e8c...de76` | `2026-11-21T03:53:54.556815843Z` | `healthy` / `0` | `bind::/app/wwwroot/uploads/products:False` |

- **Windows Scheduled Tasks (`*PhoneStore*`)**: `0` task đăng ký hoặc kích hoạt.

---

## C. Full Solution Build & Regression Results

### 1. Build Verification (`Release`)

| Lệnh kiểm tra | Phạm vi | Warnings | Errors | Kết quả |
|---|---|---|---|---|
| `dotnet restore PhoneStore.sln` | 8 solution projects | `0` | `0` | **PASS** |
| `dotnet build PhoneStore.sln -c Release --no-restore` | `Models`, `Data`, `Business`, `Api`, `Web`, `Admin` (`net10.0-windows`), `Seeder`, `Tests` | `0` | `0` | **PASS (8/8)** |
| `dotnet build tools/PhoneStore.CatalogImport/PhoneStore.CatalogImport.csproj -c Release` | CI supplemental tool 1 | `0` | `0` | **PASS** |
| `dotnet build tools/PhoneStore.AcceptanceEnvironment/PhoneStore.AcceptanceEnvironment.csproj -c Release` | CI supplemental tool 2 | `0` | `0` | **PASS** |
| `dotnet build tests/PhoneStore.SqlIntegration/PhoneStore.SqlIntegration.csproj -c Release` | CI supplemental test project (`net10.0-windows`) | `0` | `0` | **PASS** |

### 2. Automated Test Suite Summary (`PhoneStore.Tests`)

- **Lệnh thực thi**: `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj -c Release --no-build`
- **Kết quả**: **`Total: 341 | Passed: 341 | Failed: 0 | Skipped: 0 | Duration: 7 s`**

| Nhóm kiểm thử | Test Classes tiêu biểu | Số lượng / Trạng thái |
|---|---|---|
| Local JWT, Refresh Rotation, Reuse Detection, Security Hardening | `LocalJwtAuthenticationTests`, `ApiSecurityHardeningTests`, `ApiAuthorizationTests`, `Auth0BearerTests`, `AuthenticationTests`, `SingleAdminConstraintTests`, `SingleAdminPolicyTests` | **PASS (100%)** |
| Public Catalog, Multi-Filter, BestSellers, Newest, Related, Customer Web Integration | `CatalogApiTests`, `CatalogPaginationHttpTests`, `CustomerWebCatalogApiIntegrationTests`, `ApiFoundationTests` | **PASS (100%)** |
| Admin Product, Lookup (Brands/Categories), Cross-Platform Image Upload & Storage Safety | `AdminProductApiTests`, `ImageStorageSafetyTests`, `AdminCrudTests`, `ManagementOrderingTests`, `ProductStockConcurrencyTests` | **PASS (100%)** |
| Customer/Admin Order Lifecycle, Idempotent Checkout, Customer Profile, Admin Customers | `OrderApiTests`, `OrderWorkflowTests`, `OrderPaginationTests`, `CheckoutTests`, `CommerceWorkflowTests`, `CustomerProfileHttpTests`, `CustomerProfileTests`, `AdminCustomerApiTests` | **PASS (100%)** |
| Admin Dashboard & Multi-Period Revenue Reporting (`Day\|Week\|Month\|Year\|CustomRange`) | `AdminReportApiTests`, `ReportingTests` | **PASS (100%)** |
| Admin WinForms REST API Client, Proactive Refresh, Revocation & Dual Mode DI | `AdminRestApiIntegrationTests`, `AdminBootstrapTests` | **PASS (100%)** |

---

## D. Security Audit Matrix

Toàn bộ các chốt chặn bảo mật được kiểm toán trực tiếp trên mã nguồn kèm đường dẫn file và số dòng chính xác:

| Hạng mục kiểm toán | Bằng chứng mã nguồn (`file:line`) | Cơ chế bảo vệ & Kiểm chứng | Kết quả |
|---|---|---|---|
| **1. Startup Validation & Fail-Closed Auth** | `src/PhoneStore.Api/Authorization/ApiAuthenticationRegistration.cs:37-49`, `src/PhoneStore.Api/Authorization/LocalJwtOptions.cs:41-69`, `src/PhoneStore.Api/Program.cs:190-198` | Mặc định `Mode = Disabled` (`UnconfiguredApiAuthentication` từ chối mọi Bearer token `401`). Khi bật `LocalJwt`, `ValidateOnStart()` bắt buộc `Issuer` là HTTPS tuyệt đối, `Audience` hợp lệ, `SigningKey >= 32 bytes` (`>= 12` ký tự phân biệt, chặn 11 mẫu khóa yếu trong `DisallowedWeakMarkers`), `AccessTokenMinutes` `5..60`, `RefreshTokenDays` `1..30`. Nếu sai cấu hình, `Program.cs:194-198` bắt `OptionsValidationException` và thoát sạch với `ExitCode = 1`. | **PASS** |
| **2. JWT Signing & Validation Hardening** | `src/PhoneStore.Api/Authorization/LocalJwtTokenService.cs:45-57`, `src/PhoneStore.Api/Authorization/LocalJwtBearer.cs:24-87` | Ký `HS256` với header `typ = "at+jwt"`, claim `sub`, `jti`, `iat`, `token_use = "access"`, `scope`. Validator khóa cứng `ValidAlgorithms = [HmacSha256]`, `ValidTypes = ["at+jwt"]`, `ValidateIssuer = true`, `ValidateAudience = true`, `ValidateIssuerSigningKey = true`, `ValidateLifetime = true`, `ClockSkew = 30s`, và kiểm tra đúng 1 claim `sub`/`jti`/`token_use=access`. | **PASS** |
| **3. Refresh Token Hashing, Atomic Rotation & Reuse Detection** | `src/PhoneStore.Api/Authorization/ApiRefreshTokenService.cs:51-166`, `src/PhoneStore.Data/Authentication/ApiRefreshTokenStore.cs:11-24` | Refresh token nguyên bản (`psrt_<base64url(32 bytes)>`) không bao giờ lưu DB; chỉ lưu `SHA-256` hex (`TokenHash`) với unique index `UX_ApiRefreshTokens_TokenHash`. Khi xoay vòng (`RotateAsync`), SQL Server dùng `IsolationLevel.Serializable` kết hợp `WITH (UPDLOCK, HOLDLOCK)`. Nếu phát hiện token đã `RevokedAtUtc != null` được gửi lại, toàn bộ token active cùng `FamilyId` bị thu hồi ngay với `RevokedReason = "ReuseDetected"`. | **PASS** |
| **4. Per-Request Active User & Role Verification** | `src/PhoneStore.Api/Authorization/ApiAccess.cs:25-73`, `src/PhoneStore.Api/Program.cs:185-189` | `DefaultPolicy` là fail-closed (`RequireAssertion(_ => false)`). Mọi endpoint không gắn `[AllowAnonymous]` đều bắt buộc xác thực. `ActiveRoleHandler` (`Api.Admin`, `Api.Customer`) và `ActiveAnyRoleHandler` (`Api.Authenticated`) truy vấn bảng `Users` trên DB mỗi request để xác nhận `u.IsActive && u.Role.RoleName == expected` — tài khoản bị khóa hoặc đổi quyền bị chặn tức thì (`403`) ngay cả khi JWT chưa hết hạn. | **PASS** |
| **5. Single Admin Rule & Privilege Escalation Prevention** | `src/PhoneStore.Api/Contracts/Auth/AuthContracts.cs:16-30`, `src/PhoneStore.Api/Controllers/AuthController.cs:99-122`, `src/PhoneStore.Api/Controllers/AdminCustomersController.cs:43-86` | `RegisterRequest` gắn `[JsonUnmappedMemberHandling(Disallow)]` (gửi thêm `roleId` bị chặn `400`), `AuthService.RegisterAsync` chỉ gán role `Customer`, kết hợp filtered unique index SQL `UX_Users_SingleAdmin` (`RoleId = 1`). `CustomerManagementStore` lọc cứng role `Customer` ở tầng truy vấn SQL nên `AdminCustomersController` (`GET/PUT/PATCH`) không thể sửa hoặc khóa tài khoản Admin (`404`). | **PASS** |
| **6. Input, Overposting & Upload Security** | `src/PhoneStore.Api/Controllers/AdminProductsController.cs:24-25,111-178`, `src/PhoneStore.Business/Management/CrossPlatformProductImageContentValidator.cs:17-337`, `src/PhoneStore.Business/Management/LocalProductImageStorage.cs:25-95` | Request DTO nhạy cảm khóa unmapped JSON properties. Upload ảnh giới hạn `10 MiB`, kiểm tra tên file chống `\0` và `..`, chặn `.webp`/`.exe`, giải mã thật cấu trúc chunk PNG (chặn APNG `acTL`/`fcTL`/`fdAT`, kiểm tra ZLib scanlines) và marker/entropy JPEG bằng managed code đa nền tảng (`CrossPlatformProductImageContentValidator`), giới hạn `<= 4096x4096` & `<= 16M pixels`, lưu tên file `Guid.NewGuid():N` vào thư mục đích đã chuẩn hóa chống path traversal. | **PASS** |
| **7. Transport, Proxy, Rate Limit, Size & Cache Headers** | `src/PhoneStore.Api/Program.cs:50-93,156-184,201-270`, `src/PhoneStore.Api/Infrastructure/RequestSafetyMiddleware.cs:16-101` | Kestrel giới hạn `MaxRequestBodyBytes` (`64 KiB` cho JSON; `10 MiB + 64 KiB` chỉ cho route multipart upload ảnh). Rate limit cố định theo IP đã chuẩn hóa IPv4-mapped IPv6 (`AuthRateLimit`, `ProtectedRateLimit`, `CatalogRateLimit`, `HealthRateLimit`) trả `429 + Retry-After`. `ForwardedHeaders` chỉ bật khi cấu hình tường minh `TrustedProxies`/`TrustedProxyNetworks`. `RequireHttps` chặn HTTP ngoài với `400` nhưng cho phép loopback health probe (`127.0.0.1` / `::1` / `::ffff:127.0.0.1`). Mọi endpoint có `[Authorize]` gắn `Cache-Control: no-store`. Swagger chỉ bật khi `IsDevelopment()`. | **PASS** |
| **8. Log & Exception Safety** | `src/PhoneStore.Api/Program.cs:43,59-64`, `src/PhoneStore.Api/Infrastructure/ApiExceptionHandler.cs:20-47`, `src/PhoneStore.Api/Infrastructure/RequestSafetyMiddleware.cs:74-80` | Suppress log exception mặc định của framework; `ApiExceptionHandler` chỉ ghi `ExceptionType` + `TraceId` và trả `ProblemDetails` chuẩn (`503` cho lỗi DB, `500` cho lỗi khác), tuyệt đối không lộ stack trace, SQL query, connection string, password hay JWT/refresh token. Đã quét `93.290` ký tự Docker logs thực tế của `api` + `web`: **`0` rò rỉ**. | **PASS** |
| **9. Admin WinForms Client Security** | `src/PhoneStore.Admin/AdminServices.cs:43-60`, `src/PhoneStore.Business/AdminApi/AdminApiSessionStore.cs:17-68`, `src/PhoneStore.Business/AdminApi/AdminApiClient.cs:85-235` | Chế độ `RestApi` không đăng ký `PhoneStoreDbContext` (không thể fallback ngầm sang SQL). Token chỉ giữ trong bộ nhớ RAM (`AdminApiSessionStore`), từ chối lưu nếu role khác `Admin`. `AdminApiClient` dùng `SemaphoreSlim(1,1)` xoay vòng token chủ động trước 30s hoặc khi gặp `401`, xóa sạch session khi logout hoặc khi bị thu hồi quyền (`401/403`), và **không tự động retry các lệnh ghi (`POST/PUT/PATCH/DELETE`) khi timeout/lỗi mạng** để tránh ghi trùng. | **PASS** |

---

## E. End-to-End API Workflow Verification (SQL Server 2022 + Linux Docker API + Web)

Kịch bản kiểm thử tích hợp thực tế (`Invoke-S354F-FullStackValidation.ps1`, project `phonestore-s354f-e2e`, database `PhoneStoreDB_S354F` trên container SQL Server 2022 không mở host port) đã xác minh **23/23 hạng mục PASS**:

| Nhóm | Hạng mục kiểm chứng thực tế | Kết quả chi tiết | Trạng thái |
|---|---|---|---|
| **Preflight & Fixture** | Kiểm tra image `phonestore-api:s35-4f-test` & `phonestore-web:s35-4f-test`, cổng RAM `SqlColdStart/Pre`, khởi tạo SQL Server 2022 + API (`127.0.0.1:5396`) + Web (`127.0.0.1:5398`) | `WinFree = 4.44 -> 2.85 GiB`, `Swap = 0 MiB`, cả 3 container `healthy` | **PASS** |
| **Migration & Rollback** | Áp dụng 3 EF migrations -> seed 3 Users -> chạy `Down` (`DROP TABLE ApiRefreshTokens`) -> áp dụng lại `Up` | Bảo toàn `Users = 3`, xác minh đủ 4 indexes trên `ApiRefreshTokens` | **PASS** |
| **10-DockerProd** | `GET /api/health/live`, `GET /api/health/ready`, `GET /swagger/index.html` trong môi trường `Production` | `live = 200`, `ready = 200`, `swagger = 404` | **PASS** |
| **7A-Auth** | Admin login (`200`), Customer login (`200`), Inactive login (`401`), Customer gọi `admin/login` (`403`), Register kèm `roleId:1` (`400`), Register hợp lệ (`201 Customer`), Register trùng email (`409`), Single Admin (`= 1`), Customer gọi Admin route (`403`), Admin gọi Customer order route (`403`), Anonymous (`401`), `/api/v1/auth/me` (`200`, `Cache-Control: no-store`) | Mọi mã HTTP và header khớp tuyệt đối | **PASS** |
| **7B-Product & Lookups** | Tạo Brand (`201`), Brand trùng tên khác hoa/thường (`409`), tạo Category (`201`), tạo Product `iPhone 16 Pro Max` tồn `6` (`201`), cập nhật giá & tồn `6 -> 5` (`204`), cập nhật với `originalStockQuantity` cũ (`409 Conflict`) | Bảo toàn optimistic concurrency cho tồn kho sản phẩm | **PASS** |
| **7B-CatalogParity** | `GET /api/v1/admin/products/options` (`200`), `GET /api/v1/products?brandIds=1&rams=8%20GB&storages=256%20GB` (`200`, `1` item), `/bestsellers?count=4` (`200`), `/newest?count=4` (`200`), `/{id}/related` (`200`), `DELETE /api/v1/admin/products/{id}` (`405`), `PATCH /status` (`isActive=false` -> `204`) -> cả API `/api/v1/products/{id}` và Customer Web `/products/{id}` đều trả `404` | Chặn xóa cứng (`405`), ẩn sản phẩm ngừng kinh doanh đồng bộ trên cả API và Web (`404`) | **PASS** |
| **7C-Orders** | Gửi 2 request `POST /api/v1/orders` **đồng thời** cùng `Idempotency-Key: s354f-idem-001` -> chỉ tạo 1 đơn (`o1 == o2 == 1`), hủy đơn 1 (`204`) hoàn tồn `5 -> 6`; tạo đơn 2 (`o2 = 2`, tồn `6 -> 5`) -> chuyển trạng thái sai `Pending -> Completed` (`409`) -> chuyển chuẩn `Pending -> Confirmed -> Shipping -> Completed` (`204` x 3, tồn cuối `= 4` do tạo 2 đơn số lượng 1 và hủy 1 đơn? Ghi chú: tồn sau update Product là `5`, đơn 1 trừ `1` rồi hủy hoàn `1` về `5`, đơn 2 trừ `1` còn `4`) | Chống tạo đơn trùng khi race condition, hoàn tồn kho chính xác (`stock = 4`) | **PASS** |
| **7D-Customers** | Customer `PUT /api/v1/profile` (`200`), Admin `PUT /api/v1/admin/customers/{customerId}` (`204`), Admin thử `PUT /api/v1/admin/customers/{adminId}` (`404`) | Bảo vệ tài khoản Admin khỏi thao tác qua Customer Management API | **PASS** |
| **7E-Reports** | `GET /api/v1/admin/dashboard` (`200`, `no-store`, `completedOrders = 1`, `completedRevenue = 33,990,000` — loại trừ đơn đã hủy), `GET /api/v1/admin/orders/{id}` trả đủ `customerEmail`, `receiverPhone`, `statusLabel`, `nextStatuses`; `GET /api/v1/admin/reports/revenue` cho `Day` (`24` buckets), `Week` (`7` buckets), `Month` (`30` buckets), `Year` (`12` buckets), `CustomRange` (`33,990,000`) | Doanh thu và số bucket khớp chính xác theo múi giờ nghiệp vụ UTC+7 | **PASS** |
| **7A-RefreshLogout** | `POST /api/v1/auth/refresh` xoay vòng token (`200`), gửi lại refresh token cũ bị chặn (`401 ReuseDetected`), `POST /api/v1/auth/logout` (`204`), refresh sau logout (`401`) | Thu hồi phiên và chống phát lại refresh token | **PASS** |
| **6-LogSafety & Cleanup** | Quét `93.290` ký tự log của `api` + `web` (`0` bí mật rò rỉ), xóa toàn bộ container/network/volume/image fixture (`0` còn sót), đối chiếu chữ ký 3 containers business | `leaks = 0`, `leftovers = 0`, business invariants khớp 100% | **PASS** |

---

## F. Product Image Storage & Customer Web Integration

Đã kiểm chứng kiến trúc lưu trữ ảnh dùng chung giữa `PhoneStore.Api` (`rw`) và `PhoneStore.Web` (`ro`) trên Linux Docker (`docker-compose.s354f.yml`):

1. **Cấu hình Volume cách ly**:
   - `api`: mount named volume `uploads_data:/app/wwwroot/uploads/products` (**Read-Write**), `ProductImages__UploadsDirectory=/app/wwwroot/uploads/products`.
   - `web`: mount cùng volume `uploads_data:/app/wwwroot/uploads/products` với `read_only: true` (**Read-Only**), `CatalogApi__Enabled=true`, `CatalogApi__BaseUrl=http://api:8080/`.
2. **Kiểm chứng thực tế trên Docker Linux**:
   - **Read-only enforcement trên Web**: Chạy `touch /app/wwwroot/uploads/products/ro-probe.txt` trong container `web` trả `ExitCode = 1` (`Read-only file system`).
   - **Chặn tệp độc hại**: Upload `bad.exe` (`MZ` header) qua `POST /api/v1/admin/products/1/images` bị từ chối với HTTP `400` (`ProblemDetails`), không tạo file trên volume.
   - **Upload ảnh hợp lệ & hiển thị trên Customer Web**:
     - Upload ảnh PNG hợp lệ (`iphone16-front.png`) -> HTTP `201 Created`, trả `imageUrl = "/uploads/products/c62d0d9f6e754a2084b24d079f36585a.png"`, `isPrimary = true`.
     - Gọi `GET http://127.0.0.1:5398/uploads/products/c62d0d9f6e754a2084b24d079f36585a.png` trên **Customer Web** -> HTTP `200 OK`, `Content-Type: image/png`, mã băm **SHA-256 khớp 100%** với mảng byte gốc.
     - Gọi `GET http://127.0.0.1:5398/products/1` trên **Customer Web** -> HTTP `200 OK`, HTML trang chi tiết sản phẩm chứa đúng đường dẫn `/uploads/products/c62d0d9f6e754a2084b24d079f36585a.png`.
   - **Tính bền vững khi tái tạo container (`--force-recreate`)**:
     - Chạy `docker compose up -d --force-recreate --no-deps --no-build --pull never api web` (đổi Container ID `api` từ `9cdab3f7c67c` -> `1fb909485f62`, `web` từ `dcd5da756958` -> `20f553e80f4f`).
     - Sau khi 2 container mới healthy, gọi lại `GET http://127.0.0.1:5398/uploads/products/c62d0d9f6e754a2084b24d079f36585a.png` vẫn trả HTTP `200 OK` với **SHA-256 không đổi**.
   - **Đổi ảnh chính & xóa ảnh an toàn**:
     - Upload ảnh thứ 2 (`iphone16-back.jpg`, `201 Created`), gọi `PATCH /images/{img2Id}/primary` (`204`) -> trang `/products/1` trên Customer Web cập nhật sang URL ảnh 2.
     - Gọi `DELETE /images/{img2Id}` (`200`) -> ảnh 1 được tự động khôi phục làm `isPrimary = true` và file ảnh 1 trên Customer Web vẫn trả HTTP `200 OK`.

---

## G. Admin WinForms Dual Mode Readiness (`DirectSql | RestApi`)

### 1. Trạng thái xác minh theo cấp độ

| Cấp độ xác minh | Chế độ `DirectSql` (Mặc định) | Chế độ `RestApi` (Tích hợp S35.4E) | Bằng chứng |
|---|---|---|---|
| **Build & Publish (`net10.0-windows`)** | **VERIFIED (`PASS`)** | **VERIFIED (`PASS`)** | `dotnet build PhoneStore.sln -c Release` (`0` warnings/errors) + `scripts/ci/Publish-AdminArtifact.ps1` (`PASS`) |
| **DI Container & Fail-Closed Startup** | **UNIT VERIFIED (`PASS`)** | **UNIT VERIFIED (`PASS`)** | `AdminServices.Create` (`ValidateScopes=true`, `ValidateOnBuild=true`); `RestApi` không đăng ký `PhoneStoreDbContext`; mode lạ hoặc URL thiếu trong `RestApi` ném `InvalidOperationException` |
| **Automated HTTP + SQLite Integration** | **INTEGRATION VERIFIED (`PASS`)** | **INTEGRATION VERIFIED (`PASS`)** | `AdminRestApiIntegrationTests` (`9/9 PASS` trong suite `341/341`) kiểm thử `RestAdminAuthService`, `RestLookupManagementService`, `RestProductManagementService`, `RestCustomerManagementService`, `RestOrderManagementService`, `RestReportingService`, proactive refresh và revocation `401/403` |
| **Live SQL Server 2022 + Linux Docker API E2E** | N/A | **INTEGRATION VERIFIED (`PASS`)** | `20/20 PASS` (S35.4E) + `23/23 PASS` (S35.4F) |
| **Interactive Desktop GUI Click-Through** | Đã nghiệm thu ở các mốc S32.3B | **`MANUAL ACCEPTANCE PENDING`** | Không giả lập đã bấm GUI thủ công khi chỉ chạy kiểm thử tự động |

### 2. Checklist nghiệm thu GUI thủ công ngắn gọn cho người dùng (Khi chạy trên môi trường cách ly)

> **Lưu ý an toàn**: Chỉ thực hiện checklist có thao tác ghi dữ liệu khi trỏ `ApiBaseUrl` vào stack API + SQL fixture cách ly (không ghi dữ liệu thử nghiệm vào `PhoneStoreDB` business).

1. Cấu hình `src/PhoneStore.Admin/appsettings.json` (hoặc biến môi trường `AdminDataAccess__Mode=RestApi`, `AdminDataAccess__ApiBaseUrl=http://localhost:<fixture_api_port>/`).
2. Mở `PhoneStore.Admin.exe` -> Đăng nhập bằng tài khoản Admin -> Xác nhận vào `MainForm` và hiển thị đúng thẻ tổng quan Dashboard.
3. Mở tab **Thương hiệu / Danh mục** -> Tạo mới, sửa, bật/tắt trạng thái.
4. Mở tab **Sản phẩm** -> Tạo sản phẩm mới, cập nhật giá/tồn kho, tải lên 2 ảnh PNG/JPG, đặt ảnh chính, xóa 1 ảnh, ngừng kinh doanh sản phẩm.
5. Mở tab **Đơn hàng** -> Lọc đơn hàng, mở chi tiết, chuyển trạng thái `Pending -> Confirmed -> Shipping -> Completed`.
6. Mở tab **Khách hàng** -> Tìm kiếm, sửa họ tên/SĐT/địa chỉ, khóa/mở khóa khách hàng.
7. Mở tab **Báo cáo** -> Chuyển đổi biểu đồ doanh thu `Ngày / Tuần / Tháng / Năm / Tùy chọn` và kiểm tra nút **Đăng xuất** quay về `LoginForm`.

---

## H. Migration & Rollback Plan

### 1. Danh sách EF Core Migrations hiện có trong `src/PhoneStore.Data/Migrations`

| Thứ tự | Migration ID | File | Mục đích | Trạng thái trên `PhoneStoreDB` Business |
|---|---|---|---|---|
| 1 | `20261001150857_InitialCreate` | `20261001150857_InitialCreate.cs` | Schema nền tảng (`Roles`, `Users`, `Brands`, `Categories`, `Products`, `ProductImages`, `Orders`, `OrderDetails`) | Đã áp dụng |
| 2 | `20261102122856_EnforceSingleAdmin` | `20261102122856_EnforceSingleAdmin.cs` | Filtered unique index `UX_Users_SingleAdmin` (`RoleId = 1`) + cột `Orders.CheckoutToken` | Đã áp dụng |
| 3 | `20261121044020_AddApiRefreshTokens` | `20261121044020_AddApiRefreshTokens.cs` | Tạo bảng `ApiRefreshTokens` (`PK_ApiRefreshTokens`, `FK_ApiRefreshTokens_Users_UserId` cascade, `UX_ApiRefreshTokens_TokenHash`, `IX_ApiRefreshTokens_FamilyId`, `IX_ApiRefreshTokens_UserId`) | **CHƯA ÁP DỤNG (Pending Approval)** |

### 2. Đánh giá an toàn & Quy trình Rollback

- **Tính chất thay đổi**: `20261121044020_AddApiRefreshTokens` là migration **thuần bổ sung (additive-only)** — chỉ tạo bảng mới `ApiRefreshTokens`, không sửa đổi hay xóa bất kỳ bảng/cột/index hiện có nào.
- **Tương thích ngược**:
  - Khi `ApiAuthentication:Mode = Disabled` (mặc định trong `appsettings.json`), `PhoneStore.Api`, `PhoneStore.Web` và `PhoneStore.Admin` (`DirectSql`) hoạt động bình thường ngay cả khi bảng `ApiRefreshTokens` chưa được tạo hoặc đang trống.
  - Khi cần rollback từ `LocalJwt` về trạng thái trước S35.4B:
    1. Đặt `ApiAuthentication__Mode=Disabled` (hoặc rollback container image `api`/`web` về digest đã ghim ở S35.3J).
    2. Bảng `ApiRefreshTokens` có thể giữ nguyên an toàn mà không ảnh hưởng tới image cũ, hoặc thu hồi sạch bằng phương thức `Down` (`DROP TABLE [ApiRefreshTokens]; DELETE FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261121044020_AddApiRefreshTokens';`) — đã kiểm chứng thực tế trên SQL Server 2022 fixture ở Mục E bảo toàn 100% dữ liệu bảng `Users`.

---

## I. Docker Release Readiness Assessment

Đánh giá 5 chiều cho việc phát hành Docker sau khi commit/push:

| Chiều đánh giá | Hiện trạng & Kiểm chứng | Sẵn sàng cho Commit/CI | Điều kiện trước khi Deploy Business |
|---|---|---|---|
| **1. API Container (`PhoneStore.Api`)** | Build `linux/amd64` thành công (`src/PhoneStore.Api/Dockerfile`, `.NET 10.0`). Healthcheck `/api/health`, `/api/health/live`, `/api/health/ready` trả `200`; Swagger tắt ở `Production` (`404`); `ExitCode = 1` khi cấu hình sai. | **READY (`PASS`)** | Cần bổ sung volume mount `/app/wwwroot/uploads/products` (`rw`) vào `docker-compose.yml` và cấp biến môi trường `ApiAuthentication__Mode=LocalJwt` + `SigningKey` ngoài Git. |
| **2. Web Container (`PhoneStore.Web`)** | Build `linux/amd64` thành công (`Dockerfile`, `.NET 10.0`). Gọi `CatalogApi` nội bộ (`http://api:8080/`) và phục vụ ảnh tĩnh từ `/app/wwwroot/uploads/products` (`read_only: true`) khớp SHA-256. | **READY (`PASS`)** | Dùng chung nguồn lưu trữ `/app/wwwroot/uploads/products` (`ro`) với container `api`. |
| **3. DB Container (`SQL Server 2022`)** | Giữ nguyên image ghim SHA256 và volume `phonestore-restore-c2_mssql_data`. Không bao giờ recreate container `db` trong luồng CD (`Invoke-BusinessCd.ps1`). | **READY (`PASS`)** | Cần tạo bản backup `COPY_ONLY` mới ra ngoài volume (`C:\PhoneStoreDockerTransfer\backup`), verify `RESTORE VERIFYONLY WITH CHECKSUM`, rồi mới áp dụng migration `20261121044020_AddApiRefreshTokens` khi được phê duyệt. |
| **4. Reverse Proxy / TLS** | Hỗ trợ `ApiOperational:RequireHttps=true`, HSTS, và `UseForwardedHeaders` fail-closed (chỉ tin `X-Forwarded-Proto` từ `TrustedProxies`/`TrustedProxyNetworks`, luôn cho phép loopback healthcheck `127.0.0.1` / `::1` / `::ffff:127.0.0.1`). | **READY (`PASS`)** | Nếu chạy sau Reverse Proxy TLS, cần cấu hình `TrustedProxies`/`TrustedProxyNetworks` và `RequireHttps=true`. |
| **5. Resource Footprint** | Trong bài E2E full-stack chạy đồng thời **2 bộ** (3 containers business + 3 containers fixture SQL/API/Web), `WinFree` duy trì `2.59–4.44 GiB`, `WslAvail` `4.77–5.62 GiB`, `WslSwap = 0 MiB`. | **READY (`PASS`)** | Tuân thủ `Rehearsal.ResourceGate.ps1` và tuần tự `api` -> `web` qua `Invoke-BusinessCd.ps1`. |

---

## J. CI/CD Pipeline Compatibility

Đã kiểm tra toàn bộ pipeline CI/CD hiện có với các thay đổi S35.4A–S35.4F:

1. **`.github/workflows/ci.yml`**:
   - Bước restore, build Release solution + 3 project phụ (`CatalogImport`, `AcceptanceEnvironment`, `SqlIntegration`), chạy `PhoneStore.Tests` (`341/341`), chạy `Publish-AdminArtifact.ps1`, `validate_business_compose.py`, `prepare_image_context.py`, và `docker build` cho `web` + `api` đều tương thích 100%.
   - Tất cả GitHub Actions trong `.github/workflows/ci.yml` vẫn giữ nguyên trạng thái **ghim full commit SHA (40 hex)**.
2. **`scripts/ci/prepare_image_context.py`**:
   - Script dùng `git ls-files` để đóng gói các file thuộc `src/PhoneStore.{Web,Api,Business,Data,Models}` vào Docker build context trên CI và xóa `ConnectionStrings` khỏi `appsettings*.json`. Sau khi `git add` & `git commit` toàn bộ file mới của S35.4B–E, `prepare_image_context.py` sẽ tự động đưa đầy đủ các file mới vào image GHCR.
3. **`scripts/ci/Publish-AdminArtifact.ps1`**:
   - Đã chạy thử nghiệm thực tế: xuất thành công artifact `PhoneStore.Admin` (`win-x64`), tạo `appsettings.json` sạch không chứa secret và khởi chạy mặc định ở chế độ `DirectSql` an toàn.
4. **`scripts/ci/validate_business_compose.py` & `scripts/cd/*.ps1`**:
   - `python scripts/ci/validate_business_compose.py`: **PASS** (`Business Compose model validated (db, api, web).`).
   - `scripts/cd/Test-Rehearsal.ps1`: **16/16 PASS**.
   - `scripts/cd/Test-BusinessCdReadiness.ps1`: **6/6 PASS** (gồm parser AST **13/13** scripts CD).

---

## K. Complete Changed Files Inventory

Tổng số file thay đổi và thêm mới trong working tree (tính cả báo cáo nghiệm thu `S35.4F` này): **73 files** (**43 modified** + **30 untracked/added**, `0` deleted, `0` binary/runtime/secret files).

### 1. Modified Files (43 files — `1,966 insertions(+), 172 deletions(-)`)

- **API (`src/PhoneStore.Api`) — 19 files**:
  - `Program.cs`, `appsettings.json`
  - `Authorization/ApiAccess.cs`, `Authorization/ApiAuthenticationRegistration.cs`
  - `Contracts/Customers/AdminCustomerContracts.cs`, `Contracts/Orders/OrderContracts.cs`, `Contracts/Products/AdminProductContracts.cs`, `Contracts/Products/ProductQueryRequest.cs`, `Contracts/Reports/ReportContracts.cs`
  - `Controllers/AdminCustomersController.cs`, `Controllers/AdminOrdersController.cs`, `Controllers/AdminProductsController.cs`, `Controllers/AdminReportsController.cs`, `Controllers/CustomerOrdersController.cs`, `Controllers/ProductsController.cs`
  - `Infrastructure/ApiOperationalOptions.cs`, `Infrastructure/ApiPolicies.cs`, `Infrastructure/AuthorizationDocumentationFilter.cs`, `Infrastructure/RequestSafetyMiddleware.cs`
- **Business (`src/PhoneStore.Business`) — 6 files**:
  - `Management/CustomerManagementService.cs`, `Management/ICustomerManagementService.cs`, `Management/IOrderManagementService.cs`, `Management/IProductManagementService.cs`, `Management/LookupManagementService.cs`, `Management/ProductManagementService.cs`
- **Data & Models (`src/PhoneStore.Data`, `src/PhoneStore.Models`) — 4 files**:
  - `src/PhoneStore.Data/Context/PhoneStoreDbContext.cs`, `src/PhoneStore.Data/Management/ProductManagementStore.cs`, `src/PhoneStore.Data/Migrations/PhoneStoreDbContextModelSnapshot.cs`
  - `src/PhoneStore.Models/Entities/User.cs`
- **Admin WinForms (`src/PhoneStore.Admin`) — 7 files**:
  - `AdminApplicationContext.cs`, `AdminServices.cs`, `LoginForm.cs`, `appsettings.json`, `Authentication/AdminLoginGateway.cs`, `Management/CatalogManagementGateway.cs`, `Styling/ManagementUi.cs`
- **Tests & Docs (`tests/PhoneStore.Tests`, `docs`) — 7 files**:
  - `tests/PhoneStore.Tests/AdminCustomerApiTests.cs`, `AdminProductApiTests.cs`, `AdminReportApiTests.cs`, `ApiAuthorizationTests.cs`, `CatalogApiTests.cs`, `OrderApiTests.cs`
  - `docs/PROGRESS.md`

### 2. Added / Untracked Files (30 files)

- **API Authentication, Controllers & Contracts (10 files)**:
  - `src/PhoneStore.Api/Authorization/ApiRefreshTokenService.cs`
  - `src/PhoneStore.Api/Authorization/LocalJwtBearer.cs`
  - `src/PhoneStore.Api/Authorization/LocalJwtOptions.cs`
  - `src/PhoneStore.Api/Authorization/LocalJwtTokenService.cs`
  - `src/PhoneStore.Api/Contracts/Auth/AuthContracts.cs`
  - `src/PhoneStore.Api/Contracts/Profile/CustomerProfileContracts.cs`
  - `src/PhoneStore.Api/Controllers/AdminBrandsController.cs`
  - `src/PhoneStore.Api/Controllers/AdminCategoriesController.cs`
  - `src/PhoneStore.Api/Controllers/AuthController.cs`
  - `src/PhoneStore.Api/Controllers/ProfileController.cs`
- **Business Cross-Platform Image Validator & Admin REST Adapters (6 files)**:
  - `src/PhoneStore.Business/Management/CrossPlatformProductImageContentValidator.cs`
  - `src/PhoneStore.Business/AdminApi/AdminApiClient.cs`
  - `src/PhoneStore.Business/AdminApi/AdminApiDtos.cs`
  - `src/PhoneStore.Business/AdminApi/AdminApiSessionStore.cs`
  - `src/PhoneStore.Business/AdminApi/AdminDataAccessOptions.cs`
  - `src/PhoneStore.Business/AdminApi/RestAdminServices.cs`
- **Data Entity, Store, Configuration & EF Migration (5 files)**:
  - `src/PhoneStore.Models/Entities/ApiRefreshToken.cs`
  - `src/PhoneStore.Data/Authentication/ApiRefreshTokenStore.cs`
  - `src/PhoneStore.Data/Configurations/ApiRefreshTokenConfiguration.cs`
  - `src/PhoneStore.Data/Migrations/20261121044020_AddApiRefreshTokens.cs`
  - `src/PhoneStore.Data/Migrations/20261121044020_AddApiRefreshTokens.Designer.cs`
- **New Automated Test Suites (2 files)**:
  - `tests/PhoneStore.Tests/LocalJwtAuthenticationTests.cs`
  - `tests/PhoneStore.Tests/AdminRestApiIntegrationTests.cs`
- **Phase Audit & Validation Documentation (7 files)**:
  - `docs/S35_4A_FULL_API_CAPABILITY_AUDIT.md`
  - `docs/S35_4B_LOCAL_JWT_AUTHENTICATION.md`
  - `docs/S35_4B1_JWT_REAL_INTEGRATION_VALIDATION.md`
  - `docs/S35_4C_PRODUCT_MANAGEMENT_API.md`
  - `docs/S35_4D_ORDER_CUSTOMER_REPORTING_API.md`
  - `docs/S35_4E_ADMIN_WINFORMS_API_INTEGRATION.md`
  - `docs/S35_4F_FINAL_API_RELEASE_READINESS.md`

---

## L. API Contract Coverage Table

Bảng tổng hợp toàn bộ **46 operation mappings** (bao gồm các route `/api/v1/...` chính thức và alias tương thích `/api/...`) trên **13 controllers** của `PhoneStore.Api`:

| # | Controller | HTTP | Route (kèm alias tương thích) | Auth / Policy | Status | Unit / HTTP Test | SQL Server 2022 + Docker E2E | Admin WinForms Mode |
|---|---|---|---|---|---|---|---|---|
| 1 | `HealthController` | `GET` | `/api/health/live` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 2 | `HealthController` | `GET` | `/api/health` & `/api/health/ready` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 3 | `BrandsController` | `GET` | `/api/v1/brands` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 4 | `CategoriesController` | `GET` | `/api/v1/categories` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 5 | `ProductsController` | `GET` | `/api/v1/products` (`brandIds[]`, `rams[]`, `storages[]`, `inStockOnly`, `sort`, `page`) | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 6 | `ProductsController` | `GET` | `/api/v1/products/bestsellers` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 7 | `ProductsController` | `GET` | `/api/v1/products/newest` (`diversified`) | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 8 | `ProductsController` | `GET` | `/api/v1/products/{id}` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` (Web + API) | N/A |
| 9 | `ProductsController` | `GET` | `/api/v1/products/{id:int}/related` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 10 | `AuthController` | `POST` | `/api/v1/auth/login` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 11 | `AuthController` | `POST` | `/api/v1/auth/admin/login` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 12 | `AuthController` | `POST` | `/api/v1/auth/register` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 13 | `AuthController` | `POST` | `/api/v1/auth/refresh` | `AllowAnonymous` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 14 | `AuthController` | `POST` | `/api/v1/auth/logout` | `Api.Authenticated` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 15 | `AuthController` | `GET` | `/api/v1/auth/me` | `Api.Authenticated` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 16 | `ProfileController` | `GET` | `/api/v1/profile` | `Api.Customer` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 17 | `ProfileController` | `PUT` | `/api/v1/profile` | `Api.Customer` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 18 | `CustomerOrdersController` | `GET` | `/api/v1/orders` (alias `/api/orders`) | `Api.Customer` + `Orders.Read` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 19 | `CustomerOrdersController` | `GET` | `/api/v1/orders/{id:int}` (alias `/api/orders/{id:int}`) | `Api.Customer` + `Orders.Read` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 20 | `CustomerOrdersController` | `POST` | `/api/v1/orders` (alias `/api/orders`, `Idempotency-Key`) | `Api.Customer` + `Orders.Write` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 21 | `CustomerOrdersController` | `POST` | `/api/v1/orders/{id:int}/cancel` (alias `/api/orders/{id:int}/cancel`) | `Api.Customer` + `Orders.Write` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | N/A |
| 22 | `AdminBrandsController` | `GET` | `/api/v1/admin/brands` (alias `/api/admin/brands`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 23 | `AdminBrandsController` | `POST` | `/api/v1/admin/brands` (alias `/api/admin/brands`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 24 | `AdminBrandsController` | `PUT` | `/api/v1/admin/brands/{id:int}` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 25 | `AdminBrandsController` | `PATCH` | `/api/v1/admin/brands/{id:int}/status` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 26 | `AdminCategoriesController` | `GET` | `/api/v1/admin/categories` (alias `/api/admin/categories`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 27 | `AdminCategoriesController` | `POST` | `/api/v1/admin/categories` (alias `/api/admin/categories`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 28 | `AdminCategoriesController` | `PUT` | `/api/v1/admin/categories/{id:int}` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 29 | `AdminCategoriesController` | `PATCH` | `/api/v1/admin/categories/{id:int}/status` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 30 | `AdminProductsController` | `GET` | `/api/v1/admin/products` (alias `/api/admin/products`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 31 | `AdminProductsController` | `GET` | `/api/v1/admin/products/options` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 32 | `AdminProductsController` | `GET` | `/api/v1/admin/products/{id:int}` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 33 | `AdminProductsController` | `POST` | `/api/v1/admin/products` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 34 | `AdminProductsController` | `PUT` | `/api/v1/admin/products/{id:int}` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 35 | `AdminProductsController` | `PATCH` | `/api/v1/admin/products/{id:int}/status` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 36 | `AdminProductsController` | `GET` | `/api/v1/admin/products/{id:int}/images` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 37 | `AdminProductsController` | `POST` | `/api/v1/admin/products/{id:int}/images` (multipart `10 MiB`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` (API `rw` + Web `ro`) | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 38 | `AdminProductsController` | `DELETE` | `/api/v1/admin/products/{id:int}/images/{imageId:int}` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 39 | `AdminProductsController` | `PATCH` | `/api/v1/admin/products/{id:int}/images/{imageId:int}/primary` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 40 | `AdminProductsController` | `PATCH` | `/api/v1/admin/products/{id:int}/images/{imageId:int}/order` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 41 | `AdminOrdersController` | `GET` | `/api/v1/admin/orders` (alias `/api/admin/orders`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 42 | `AdminOrdersController` | `GET` | `/api/v1/admin/orders/{id:int}` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 43 | `AdminOrdersController` | `PATCH` | `/api/v1/admin/orders/{id:int}/status` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 44 | `AdminCustomersController` | `GET` | `/api/v1/admin/customers` (alias `/api/admin/customers`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 45 | `AdminCustomersController` | `GET` | `/api/v1/admin/customers/{id:int}` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 46 | `AdminCustomersController` | `PUT` | `/api/v1/admin/customers/{id:int}` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 47 | `AdminCustomersController` | `PATCH` | `/api/v1/admin/customers/{id:int}/status` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 48 | `AdminReportsController` | `GET` | `/api/v1/admin/dashboard` (alias `/api/admin/dashboard`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 49 | `AdminReportsController` | `GET` | `/api/v1/admin/reports/overview` (`includeAlerts`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 50 | `AdminReportsController` | `GET` | `/api/v1/admin/reports/revenue` (`Day\|Week\|Month\|Year\|CustomRange`) | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |
| 51 | `AdminReportsController` | `GET` | `/api/v1/admin/reports/top-products` | `Api.Admin` | `IMPLEMENTED` | `UNIT VERIFIED` | `INTEGRATION VERIFIED` | `RestApi` (`MANUAL ACCEPTANCE PENDING` GUI) |

---

## M. Remaining Risks & Preconditions for Business Deployment

Các điều kiện tiên quyết cần được phê duyệt và thực hiện riêng trước khi bật tính năng mới trên môi trường **Business (`phonestore-restore-c2`)**:

1. **Database Backup & Migration `20261121044020_AddApiRefreshTokens`**:
   - Phải tạo bản backup `COPY_ONLY` mới của `PhoneStoreDB` business ra ngoài Docker volume (`C:\PhoneStoreDockerTransfer\backup`) và kiểm tra `RESTORE VERIFYONLY WITH CHECKSUM` thành công trước khi áp dụng migration `AddApiRefreshTokens`.
2. **Shared Product Image Storage trên Business Compose**:
   - Trong `docker-compose.yml` hiện tại, `web` đang mount `uploads/products` dạng `read_only: true`, nhưng `api` chưa có mount cho `/app/wwwroot/uploads/products`. Trước khi sử dụng tính năng upload ảnh qua REST API trên business, cần phê duyệt bổ sung mount `/app/wwwroot/uploads/products` (`rw`) cho service `api` trỏ vào cùng thư mục/volume với `web` (`ro`), đúng như mô hình đã kiểm chứng ở Mục F.
3. **Production JWT Secrets & Reverse Proxy TLS**:
   - Cung cấp `ApiAuthentication__Mode=LocalJwt`, `ApiAuthentication__LocalJwt__Issuer`, `ApiAuthentication__LocalJwt__Audience`, và `ApiAuthentication__LocalJwt__SigningKey` (khóa ngẫu nhiên крипто `>= 32 bytes`) qua biến môi trường hoặc secret store ngoài Git.
   - Nếu chạy sau Reverse Proxy TLS, cấu hình `ApiOperational__RequireHttps=true` và `TrustedProxies`/`TrustedProxyNetworks`.
4. **Nghiệm thu GUI thủ công cho Admin WinForms (`RestApi` mode)**:
   - Thực hiện checklist ngắn gọn ở Mục G.2 trước khi chuyển mặc định `AdminDataAccess:Mode` của bản phân phối Admin từ `DirectSql` sang `RestApi`.

---

## N. Final GO / NO-GO Decision

### 1. Quyết định cho Commit & Push lên GitHub (`main`)

- **Verdict**: **`GO FOR COMMIT/PUSH`**
- **Lý do**:
  - Toàn bộ 11/11 projects build Release sạch (`0 warnings, 0 errors`).
  - Full regression đạt **`341/341 PASS`**; kiểm thử E2E thực tế trên SQL Server 2022 + Linux Docker API + Web đạt **`23/23 PASS`** (cùng `34/34` ở S35.4B.1 và `20/20` ở S35.4E).
  - Kiểm toán Git pre-commit sạch tuyệt đối (`git diff --check` = `0` lỗi, `0` secrets, `0` file nhị phân/runtime).
  - Tương thích 100% với `.github/workflows/ci.yml`, `prepare_image_context.py`, `validate_business_compose.py`, `Publish-AdminArtifact.ps1` và bộ công cụ CD.
- **Commit Message đề xuất**:
  ```text
  feat(api): complete REST API and Admin integration
  ```

### 2. Quyết định cho Triển khai lên Môi trường Business (`phonestore-restore-c2`)

- **Verdict**: **`NO-GO / CONDITIONAL FOR BUSINESS DEPLOYMENT`**
- **Lý do**:
  - Theo đúng quy tắc an toàn của S35.4F, chưa thực hiện backup/migration `AddApiRefreshTokens` trên `PhoneStoreDB` business, chưa sửa volume mount của `phonestore-restore-c2-api-1`, chưa cấp phát khóa ký JWT production và chờ bước push lên GitHub chạy xong CI/GHCR publish digest mới trước khi lập kế hoạch CD cho business.
