# S35.4B — Local JWT Authentication & API Security Hardening

- **Verification Date**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`main`)
- **Status**: **VERIFIED — PASS**
- **Test Results**: `dotnet build PhoneStore.sln -c Release` (`0` warnings, `0` errors) | `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj -c Release` (**327 / 327 PASS**, `0` failed, `0` skipped — gồm **298** tests cũ + **29** test cases mới trong `LocalJwtAuthenticationTests`) | Isolated Linux Docker build & runtime validation (**PASS**)

---

## A. Existing Authentication Architecture

Kiểm tra toàn bộ tầng xác thực và phân quyền trước khi sửa đổi xác nhận:

1. **Password Hashing & User/Role Store**:
   - `PhoneStore.Business` sử dụng `Microsoft.AspNetCore.Identity.IPasswordHasher<User>` (`PasswordHasher<User>`) và `AuthUserStore` (`src/PhoneStore.Data/Authentication/AuthUserStore.cs:9-50`) trên bảng `Users` và `Roles`.
   - `AuthService` (`src/PhoneStore.Business/Authentication/AuthService.cs:11-64`) xử lý đăng ký (`RegisterAsync` — luôn gán vai trò `SystemRoles.Customer` thông qua `AuthUserStore.CreateCustomerAsync`) và đăng nhập khách hàng (`LoginAsync` — kiểm `IsActive` và `RoleName == "Customer"`, tự rehash nếu cần).
   - `AdminAuthService` (`src/PhoneStore.Business/Authentication/AdminAuthService.cs:9-50`) xác thực quản trị viên (`IsActive`, hash mật khẩu, trả `AdminLoginStatus.Forbidden` nếu là Customer) và kiểm tra quyền Admin theo DB (`IsCurrentAdminAsync`).
   - Ràng buộc **Single Admin** được bảo vệ ở tầng schema SQL bằng `UX_Users_SingleAdmin` (`[RoleId] = 1`, `UserConfiguration.cs:22-23`) và check constraint `CK_Roles_AdminIdentity` (`20261102122856_EnforceSingleAdmin.cs:29-32`).
2. **Customer Web & Admin WinForms (giữ nguyên 100%)**:
   - Customer Web giữ nguyên ASP.NET Core Cookie Auth (`PhoneStore.Auth` + `CustomerCookieEvents`).
   - Admin WinForms giữ nguyên `AdminLoginGateway` + `AdminSession` + `CatalogManagementGateway`.
3. **REST API trước S35.4B**:
   - `ApiAccess.Scheme` (`"PhoneStore.Api"`) mặc định chạy `UnconfiguredApiAuthentication` (`401` fail-closed) vì `ApiAuthentication:Oidc:Enabled = false`.
   - Toàn bộ 16 protected endpoints đã có trong `CustomerOrdersController` và 4 `Admin*Controller` đều trả `401` khi chạy thực tế.

---

## B. Local JWT Design

Triển khai Local JWT Bearer authentication tại `src/PhoneStore.Api/Authorization/`:

- **Configuration Schema (`src/PhoneStore.Api/Authorization/LocalJwtOptions.cs`, `src/PhoneStore.Api/appsettings.json`)**:
  ```json
  "ApiAuthentication": {
    "Mode": "Disabled",
    "Oidc": {
      "Enabled": false
    }
  }
  ```
  - Hỗ trợ `Mode: Disabled | LocalJwt` (mặc định `Disabled` để giữ tương thích hoàn toàn với Docker business hiện hành và không tự bật trên business).
  - Không hardcode `SigningKey` trong bất kỳ file `appsettings*.json` hay mã nguồn nào; khi bật `LocalJwt`, khóa được nạp qua biến môi trường/secret store (`ApiAuthentication__LocalJwt__SigningKey`, `Issuer`, `Audience`, `AccessTokenMinutes`, `RefreshTokenDays`).
- **Fail-Closed Startup Validation (`ApiAuthenticationRegistration.cs:35-40`, `LocalJwtOptions.cs:11-75`)**:
  - `ValidateOnStart()` cùng `binder.ErrorOnUnknownConfiguration = true` từ chối khởi động (`OptionsValidationException`) nếu:
    - Kết hợp đồng thời `Mode = LocalJwt` và `Oidc:Enabled = true`.
    - `Mode = LocalJwt` nhưng thiếu `Issuer`, `Audience`, hoặc `SigningKey`.
    - `Issuer` không phải URI `https://` hợp lệ (hoặc chứa userinfo/query/fragment).
    - `SigningKey` nhỏ hơn `32 bytes` (256-bit), ít hơn `12` ký tự phân biệt, hoặc khớp chuỗi mặc định/placeholder yếu (`changeme`, `default-signing`, `placeholder`, `00000000`, ...).
    - `AccessTokenMinutes` ngoài khoảng `5..60` (mặc định `15` phút — tuyệt đối không cấp access token dài hạn thay refresh token) hoặc `RefreshTokenDays` ngoài `1..30` (mặc định `7` ngày).
- **Strict Token Signing & Validation (`LocalJwtBearer.cs`, `LocalJwtTokenService.cs`)**:
  - Thuật toán duy nhất được chấp nhận: **`HS256`** (`SecurityAlgorithms.HmacSha256`), `RequireSignedTokens = true`, `RequireExpirationTime = true`, `ValidateLifetime = true` (`ClockSkew = 30s`).
  - Media type header bắt buộc: **`typ = "at+jwt"`** (`ValidTypes = ["at+jwt"]`), kiểm tra `token_use = "access"`, `sub` là 1 số nguyên dương duy nhất và có `jti`.
  - Xác thực chính xác `Issuer` (ordinal match) và `Audience` (`IgnoreTrailingSlashWhenValidatingAudience = false`).
  - `OnTokenValidated` xây dựng lại `ClaimsIdentity` sạch chỉ chứa `ClaimTypes.NameIdentifier`, `jti` và các `scope` hợp lệ — bỏ qua hoàn toàn mọi claim `role` do client giả mạo.
  - `OnChallenge` trả `401 Unauthorized` kèm `WWW-Authenticate: Bearer` và RFC 7807 `application/problem+json` có `traceId`.

---

## C. Implemented Endpoints

Tất cả endpoint mới đều nằm dưới phiên bản `/api/v1/` (nâng tổng OpenAPI spec lên **28 paths**):

| HTTP Method | Route | Controller / Action | Authentication & Authorization | Chức năng chính |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/login` | `AuthController.Login` (`AuthController.cs:35`) | `[AllowAnonymous]` *(trả `503` nếu `Mode=Disabled`)* | Đăng nhập Customer qua `IAuthService.LoginAsync`; lỗi sai mật khẩu / không tồn tại / inactive đều trả chung `401` (chống enumeration); trả Access Token (`15m`) + Refresh Token (`7d`) + `Cache-Control: no-store`. |
| `POST` | `/api/v1/auth/admin/login` | `AuthController.AdminLogin` (`AuthController.cs:64`) | `[AllowAnonymous]` *(trả `503` nếu `Mode=Disabled`)* | Đăng nhập Admin qua `IAdminAuthService.LoginAsync`; Customer bị từ chối `403 Forbidden`; cấp Admin token (`scope=admin:all`). |
| `POST` | `/api/v1/auth/register` | `AuthController.Register` (`AuthController.cs:96`) | `[AllowAnonymous]` *(trả `503` nếu `Mode=Disabled`)* | Đăng ký chỉ tạo `Customer` qua `IAuthService.RegisterAsync`; `[JsonUnmappedMemberHandling(Disallow)]` chặn `roleId`/`role` từ client (`400`); không trả `PasswordHash`; trùng email trả `409`. |
| `POST` | `/api/v1/auth/refresh` | `AuthController.Refresh` (`AuthController.cs:122`) | `[AllowAnonymous]` *(trả `503` nếu `Mode=Disabled`)* | Xoay vòng refresh token nguyên tử (`RotateAsync`), phát hiện reuse và thu hồi cả `FamilyId`, kiểm tra lại DB `User.IsActive` & `Role`. |
| `POST` | `/api/v1/auth/logout` | `AuthController.Logout` (`AuthController.cs:145`) | `[Authorize(Policy = ApiAccess.Authenticated)]` | Thu hồi `FamilyId` của refresh token thuộc chính người dùng (hoặc toàn bộ phiên nếu `allSessions=true`/không truyền token); chặn thu hồi chéo người dùng khác (`403`). |
| `GET` | `/api/v1/auth/me` | `AuthController.Me` (`AuthController.cs:164`) | `[Authorize(Policy = ApiAccess.Authenticated)]` | Trả `CurrentUserResponse` (`UserId`, `FullName`, `Email`, `Phone`, `Role`, `Scopes`) trực tiếp từ DB cho tài khoản đang hoạt động; không lộ dữ liệu nhạy cảm. |
| `GET` | `/api/v1/profile` | `ProfileController.Get` (`ProfileController.cs:22`) | `[Authorize(Policy = ApiAccess.Customer)]` | Đọc hồ sơ của chính Customer đã xác thực qua `ICustomerProfileService.GetAsync`. |
| `PUT` | `/api/v1/profile` | `ProfileController.Update` (`ProfileController.cs:34`) | `[Authorize(Policy = ApiAccess.Customer)]` | Cập nhật `FullName`, `Phone`, `Address` của chính Customer qua `ICustomerProfileService.UpdateAsync`; chặn trường nhạy cảm (`roleId`, `email`, `passwordHash`, `isActive`) bằng `JsonUnmappedMemberHandling.Disallow` (`400`). |

---

## D. Authorization Policies

Cập nhật tại `src/PhoneStore.Api/Authorization/ApiAccess.cs:10-65`:

- **`DefaultPolicy`**: Giữ nguyên `RequireAssertion(_ => false)` — mọi action chưa phân loại đều fail-closed.
- **`Api.Customer`**: Yêu cầu xác thực + `ActiveRoleHandler` truy vấn DB kiểm tra `u.UserId == userId && u.IsActive && u.Role.RoleName == "Customer"` trên **mỗi request**. Áp dụng cho `CustomerOrdersController` và `ProfileController`.
- **`Api.Admin`**: Yêu cầu xác thực + `ActiveRoleHandler` truy vấn DB kiểm tra `u.UserId == userId && u.IsActive && u.Role.RoleName == "Admin"` trên **mỗi request**. Áp dụng cho `AdminProductsController`, `AdminOrdersController`, `AdminCustomersController`, `AdminReportsController`.
- **`Api.Authenticated`** *(mới)*: Yêu cầu xác thực + `ActiveAnyRoleHandler` truy vấn DB kiểm tra `u.IsActive` và vai trò thuộc `{Customer, Admin}`. Áp dụng cho `/api/v1/auth/me` và `/api/v1/auth/logout`.
- **`Orders.Read`**: Giữ nguyên yêu cầu claim `scope = orders:read` (chỉ cấp cho Customer token, không cấp cho Admin token).
- Khóa tài khoản (`Users.IsActive = false`) có hiệu lực thu hồi quyền **ngay lập tức** (`403 Forbidden`) trên lần gọi kế tiếp kể cả khi access token chưa hết hạn.

---

## E. Refresh Token Security

Triển khai hệ thống Refresh Token bền vững, có rotation và phát hiện tái sử dụng (reuse detection) — **không sử dụng cơ chế giả hay JWT dài hạn**:

- **Entity & EF Core Migration (chỉ tạo trong source, KHÔNG áp dụng lên SQL business)**:
  - Entity: `src/PhoneStore.Models/Entities/ApiRefreshToken.cs`, navigation trên `User.cs`.
  - Configuration: `src/PhoneStore.Data/Configurations/ApiRefreshTokenConfiguration.cs` (unique index `UX_ApiRefreshTokens_TokenHash`, index `IX_ApiRefreshTokens_UserId`, `IX_ApiRefreshTokens_FamilyId`, FK cascade tới `Users`).
  - Migration trong source: `src/PhoneStore.Data/Migrations/20261121044020_AddApiRefreshTokens.cs` (và `.Designer.cs`, cập nhật `PhoneStoreDbContextModelSnapshot.cs`). **Chưa áp dụng lên `PhoneStoreDB` business**.
- **Hash-only Storage & Rotation (`src/PhoneStore.Api/Authorization/ApiRefreshTokenService.cs`, `src/PhoneStore.Data/Authentication/ApiRefreshTokenStore.cs`)**:
  - Refresh token sinh 32 bytes ngẫu nhiên mật mã (`RandomNumberGenerator.GetBytes(32)`), mã hóa Base64Url có tiền tố `psrt_`.
  - Chỉ lưu **SHA-256 hex lowercase (`TokenHash`)** vào bảng `ApiRefreshTokens`; plaintext không bao giờ xuất hiện trong database.
  - **Atomic Rotation**: Thực thi trong transaction (`IsolationLevel.Serializable` + `UPDLOCK, HOLDLOCK` trên SQL Server), đánh dấu token cũ `RevokedReason = "Rotated"` + `ReplacedByTokenHash` và tạo token mới cùng `FamilyId`.
  - **Reuse Detection**: Nếu bất kỳ token đã bị thu hồi/xoay vòng nào bị gửi lại tới `/api/v1/auth/refresh`, toàn bộ token còn hiệu lực trong cùng `FamilyId` bị thu hồi ngay lập tức với `RevokedReason = "ReuseDetected"` và trả `401`.
  - **Expiration & Account Status**: Token hết hạn bị đánh dấu `Expired` (`401`); tài khoản bị khóa bị thu hồi cả family `AccountInactive` (`401`).

---

## F. Customer Profile API

- Triển khai tại `src/PhoneStore.Api/Controllers/ProfileController.cs` và `src/PhoneStore.Api/Contracts/Profile/CustomerProfileContracts.cs`.
- Tái sử dụng trực tiếp `ICustomerProfileService` / `CustomerProfileService` / `CustomerProfileStore`.
- Luôn lấy `customerId` từ `ApiAccess.UserId(User)` (không nhận `userId` từ route/query/body).
- Bảo vệ `PUT /api/v1/profile` bằng `[JsonUnmappedMemberHandling(Disallow)]` + DataAnnotations (`FullName` 2–100 ký tự, `Phone` hợp lệ tối đa 20 ký tự, `Address` tối đa 255 ký tự), đảm bảo không thể sửa `RoleId`, `Email`, `PasswordHash`, `IsActive`.

---

## G. Security Hardening

1. **Rate Limiting & Timeouts (`Program.cs`, `ApiOperationalOptions.cs`, `ApiPolicies.cs`)**:
   - Thêm `AuthRateLimit` (`10 req/min/IP`) và `AuthTimeout` (`5s`) cho `AuthController` (chống brute-force login/register/refresh).
   - Thêm `ProtectedRateLimit` (`120 req/min/IP`) và gắn `[EnableRateLimiting]` + `[RequestTimeout]` lên toàn bộ 5 protected controllers cũ (`CustomerOrders`, `AdminProducts`, `AdminOrders`, `AdminCustomers`, `AdminReports`) và `ProfileController`.
2. **Request Body Size Limit**: Giới hạn `MaxRequestBodyBytes = 65536` (64 KiB) ở cả Kestrel (`Program.cs:50`) và `RequestSafetyMiddleware` (`413 Payload Too Large` theo `application/problem+json`).
3. **HTTPS & HSTS cho Production**: Bật `app.UseHsts()` khi không phải `Development` (`Program.cs:148-149`), bổ sung `ApiOperational:RequireHttps` (từ chối HTTP không mã hóa khi bật) và bắt buộc `https://` trên `LocalJwt:Issuer`.
4. **Logging & Cache Safety**: `RequestSafetyMiddleware` tuyệt đối không ghi `Authorization` header, query string hay request body; toàn bộ phản hồi `/api/v1/auth/*` và protected endpoints đều trả `Cache-Control: no-store`.
5. **Swagger & CORS**: Swagger chỉ bật trong `Development` (đã bổ sung Bearer SecurityScheme cho Development; xác minh trả `404` trong `Production` trên cả unit test và Linux Docker). Không mở CORS wildcard.

---

## H. Tests & Regression

| Bộ kiểm thử | Lệnh thực thi | Kết quả |
|---|---|---|
| Release Solution Build | `dotnet build PhoneStore.sln -c Release --no-restore` | **PASS (`0 Warning(s)`, `0 Error(s)`)** |
| Full Automated Regression Suite (gồm 29 test cases mới của S35.4B + 298 tests cũ) | `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj -c Release --no-build` | **PASS (`327/327`, `0` failed, `0` skipped, 10s)** |

### Các vấn đề phát hiện trong lúc chạy test và cách xử lý
1. `AuthController` khai báo `public` nhận `IOptions<ApiAuthenticationOptions>` (`internal`) -> lỗi biên dịch `CS0051`; đã nâng `ApiOidcOptions`, `ApiAuthenticationOptions`, `LocalJwtOptions` lên `public sealed class`.
2. Trong `LocalJwtAuthenticationTests`: (a) `StrongKey` ban đầu 41 bytes chưa đủ 48 bytes để ký negative test `HS384` -> nâng lên 54 bytes; (b) `ConfigureAppConfiguration` bị `appsettings.json` ghi đè trong `WebApplicationFactory` -> chuyển sang `builder.UseSetting(...)`; (c) `ClearTracking()` trên shared `DbContext` detach entity trước khi test đổi `IsActive = false` -> chuyển sang `ExecuteUpdateAsync`.
3. `CatalogApiTests.OpenApi_describes_catalog_and_health_endpoints` kiểm tra danh sách toàn bộ OpenAPI paths -> đã cập nhật để xác nhận chặt chẽ cả 28 paths, HTTP methods và `x-phonestore-policy`.

---

## I. Docker Compatibility (Isolated Only)

Thực hiện build và chạy kiểm thử cách ly bằng `src/PhoneStore.Api/Dockerfile` (`phonestore-api:s35-4b-validation`, `linux/amd64`, `aspnet:10.0-noble`), sau đó dọn sạch container và image tạm:

- **Docker Build (`linux/amd64`)**: **PASS** (`dotnet restore` + `dotnet publish -c Release` thành công).
- **Startup Fail-Closed (`ASPNETCORE_ENVIRONMENT=Production`, `ApiAuthentication__Mode=LocalJwt`, thiếu `Issuer`/`Audience`/`SigningKey`)**: **PASS** (tiến trình dừng ngay với mã thoát khác 0 `exitCode=139`, không mở cổng).
- **Valid Local JWT Secret Injection via Environment Variables (`Production`)**:
  - `GET /api/health/live` -> **`HTTP 200`** (`{"status":"healthy"}`).
  - `GET /swagger/v1/swagger.json` -> **`HTTP 404`** (Swagger tắt trong Production).
  - `GET /api/v1/auth/me`, `/api/v1/profile`, `/api/admin/products` (anonymous) -> **`HTTP 401`** (`application/problem+json`, `WWW-Authenticate: Bearer`, `Cache-Control: no-store`).
- **Business Container Invariants (`Get-CdBusinessInvariantsSnapshot`)**: Cả 3 containers business (`phonestore-restore-c2-db-1`, `api-1`, `web-1`) giữ nguyên 100% ID, `StartedAt`, `healthy`, image và mounts trước và sau kiểm thử.

---

## J. Remaining Risks & Files Changed

### Rủi ro còn lại / Điều kiện triển khai sau này
1. **Migration `20261121044020_AddApiRefreshTokens`**: Hiện chỉ có trong source code và đã test trên SQLite cách ly; **bắt buộc** phải có phê duyệt riêng để áp dụng migration lên SQL business trước khi chuyển `ApiAuthentication__Mode=LocalJwt` trên môi trường business.
2. **Bảo quản `SigningKey` trên môi trường triển khai**: Khi bật `LocalJwt` trên Docker, khóa ký >= 32 bytes phải được nạp từ biến môi trường / secret store ngoài Git và tuyệt đối không commit vào repository.
3. **Phiên bản route protected cũ**: Các endpoint hiện hữu (`/api/orders`, `/api/admin/*`) được giữ nguyên đường dẫn để không phá vỡ tương thích ngược; chuẩn hóa thêm prefix `/api/v1/` cho nhóm này sẽ xử lý trong S35.4C/S35.4D.

### Danh sách tệp đã tạo / sửa đổi
- **Models & Data**:
  - `src/PhoneStore.Models/Entities/ApiRefreshToken.cs` *(mới)*
  - `src/PhoneStore.Models/Entities/User.cs` *(sửa)*
  - `src/PhoneStore.Data/Configurations/ApiRefreshTokenConfiguration.cs` *(mới)*
  - `src/PhoneStore.Data/Context/PhoneStoreDbContext.cs` *(sửa)*
  - `src/PhoneStore.Data/Authentication/ApiRefreshTokenStore.cs` *(mới)*
  - `src/PhoneStore.Data/Migrations/20261121044020_AddApiRefreshTokens.cs` *(mới)*
  - `src/PhoneStore.Data/Migrations/20261121044020_AddApiRefreshTokens.Designer.cs` *(mới)*
  - `src/PhoneStore.Data/Migrations/PhoneStoreDbContextModelSnapshot.cs` *(sửa)*
- **PhoneStore.Api**:
  - `src/PhoneStore.Api/Authorization/ApiAccess.cs` *(sửa)*
  - `src/PhoneStore.Api/Authorization/ApiAuthenticationRegistration.cs` *(sửa)*
  - `src/PhoneStore.Api/Authorization/LocalJwtOptions.cs` *(mới)*
  - `src/PhoneStore.Api/Authorization/LocalJwtBearer.cs` *(mới)*
  - `src/PhoneStore.Api/Authorization/LocalJwtTokenService.cs` *(mới)*
  - `src/PhoneStore.Api/Authorization/ApiRefreshTokenService.cs` *(mới)*
  - `src/PhoneStore.Api/Contracts/Auth/AuthContracts.cs` *(mới)*
  - `src/PhoneStore.Api/Contracts/Profile/CustomerProfileContracts.cs` *(mới)*
  - `src/PhoneStore.Api/Controllers/AuthController.cs` *(mới)*
  - `src/PhoneStore.Api/Controllers/ProfileController.cs` *(mới)*
  - `src/PhoneStore.Api/Controllers/CustomerOrdersController.cs` *(sửa — gắn RateLimiting & Timeout)*
  - `src/PhoneStore.Api/Controllers/AdminProductsController.cs` *(sửa — gắn RateLimiting & Timeout)*
  - `src/PhoneStore.Api/Controllers/AdminOrdersController.cs` *(sửa — gắn RateLimiting & Timeout)*
  - `src/PhoneStore.Api/Controllers/AdminCustomersController.cs` *(sửa — gắn RateLimiting & Timeout)*
  - `src/PhoneStore.Api/Controllers/AdminReportsController.cs` *(sửa — gắn RateLimiting & Timeout)*
  - `src/PhoneStore.Api/Infrastructure/ApiOperationalOptions.cs` *(sửa)*
  - `src/PhoneStore.Api/Infrastructure/ApiPolicies.cs` *(sửa)*
  - `src/PhoneStore.Api/Infrastructure/AuthorizationDocumentationFilter.cs` *(sửa)*
  - `src/PhoneStore.Api/Infrastructure/RequestSafetyMiddleware.cs` *(sửa)*
  - `src/PhoneStore.Api/Program.cs` *(sửa)*
  - `src/PhoneStore.Api/appsettings.json` *(sửa)*
- **Tests & Documentation**:
  - `tests/PhoneStore.Tests/LocalJwtAuthenticationTests.cs` *(mới — 29 test cases)*
  - `tests/PhoneStore.Tests/CatalogApiTests.cs` *(sửa — xác nhận 28 OpenAPI paths & policies)*
  - `docs/S35_4B_LOCAL_JWT_AUTHENTICATION.md` *(mới)*
  - `docs/PROGRESS.md` *(sửa)*

---

## K. Final Status: **PASS**

Toàn bộ mục tiêu của **S35.4B** (Local JWT Authentication fail-closed, 6 auth endpoints với refresh rotation/reuse detection/logout revocation, Customer Profile API, security hardening trên toàn bộ API, full regression **327/327 PASS**, và kiểm chứng Linux Docker cách ly) đã hoàn thành **PASS** mà không đụng chạm tới SQL business, Docker business hay các chức năng Web/Admin hiện hành.
