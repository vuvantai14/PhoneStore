# PhoneStore — Xác thực, Phân quyền và Local JWT

> Tài liệu mô tả cơ chế xác thực và phân quyền hiện hành trên cả ba ứng dụng (`PhoneStore.Web`, `PhoneStore.Api`, `PhoneStore.Admin`), đối chiếu trực tiếp từ mã nguồn `src/PhoneStore.Api/Authorization/` và `src/PhoneStore.Admin/AdminServices.cs`.
> Báo cáo triển khai và xác minh chi tiết lưu tại [`archive/s35/S35_4B_LOCAL_JWT_AUTHENTICATION.md`](../archive/s35/S35_4B_LOCAL_JWT_AUTHENTICATION.md) và [`archive/s35/S35_4B1_JWT_REAL_INTEGRATION_VALIDATION.md`](../archive/s35/S35_4B1_JWT_REAL_INTEGRATION_VALIDATION.md).

## 1. Tổng quan bảo mật tài khoản

- **Lưu trữ mật khẩu**: Sử dụng `IPasswordHasher<User>` (`PasswordHasher<User>` — PBKDF2 của ASP.NET Core Identity) trên bảng `Users`; không thêm bảng ASP.NET Identity phụ.
- **Quy tắc Single Admin**: Hệ thống chỉ cho phép tối đa **1 tài khoản `Admin`** (kể cả khi `IsActive = false`), cưỡng chế đồng thời ở tầng nghiệp vụ và tầng SQL (`UX_Users_SingleAdmin` + `CK_Roles_AdminIdentity`). Mọi luồng đăng ký công khai luôn tạo vai trò `Customer`.
- **Kiểm tra trạng thái hoạt động (`IsActive`)**: Tài khoản bị khóa (`IsActive = false`) không thể đăng nhập, không thể làm mới refresh token và bị từ chối ngay ở request tiếp theo.

## 2. Xác thực trên Customer Web (`PhoneStore.Web`)

- Sử dụng **Cookie Authentication** (scheme `PhoneStore.Auth`).
- Kiểm tra vai trò `Customer` và trạng thái active trong DB đối với các thao tác đặt hàng, hồ sơ và quản lý đơn hàng cá nhân.
- Giỏ hàng (`SessionCart`) và mã chống gửi trùng đơn (`CheckoutToken`) gắn ràng buộc chủ sở hữu theo phiên người dùng, tự làm sạch an toàn khi đăng nhập/đăng xuất.

## 3. Xác thực & Local JWT trên REST API (`PhoneStore.Api`)

### 3.1. Các chế độ `ApiAuthentication`

Được đăng ký và kiểm tra ngay lúc khởi động (`ValidateOnStart` + `ErrorOnUnknownConfiguration = true`) trong [`ApiAuthenticationRegistration.cs`](../../src/PhoneStore.Api/Authorization/ApiAuthenticationRegistration.cs) và [`LocalJwtOptions.cs`](../../src/PhoneStore.Api/Authorization/LocalJwtOptions.cs):

| Chế độ | Mô tả | Yêu cầu cấu hình |
|---|---|---|
| `Disabled` (mặc định trong `appsettings.json`) | Không bật JWT; mọi endpoint được bảo vệ trả từ chối (`UnconfiguredApiAuthentication`), chỉ Public Catalog & Health hoạt động | Không yêu cầu secret (an toàn khi chưa cấu hình) |
| **`LocalJwt`** (chế độ vận hành chính) | Cấp phát và xác thực JWT cục bộ bằng **`HS256`** | `Issuer` (HTTPS tuyệt đối hợp lệ), `Audience` (`3–200` ký tự), `SigningKey` mạnh `>= 32 bytes`, `>= 12` ký tự phân biệt, không chứa chuỗi placeholder yếu (`changeme`, `dev-secret`, ...), `AccessTokenMinutes` (`5–60`, mặc định `15`), `RefreshTokenDays` (`1–30`, mặc định `7`), `KeyId` (mặc định `local-hs256-v1`). Không được bật đồng thời với `Oidc:Enabled = true`. |
| `Oidc` (`Auth0Bearer`) | Chế độ OIDC tùy chọn (`ApiAuthentication:Oidc:Enabled = true` khi `Mode = Disabled`) | Mặc định tắt (`false`) trong vận hành hiện hành |

> Mọi giá trị `SigningKey` phải được cấp qua **`.NET User Secrets`** (Local Dev) hoặc **biến môi trường / file `.env` ngoài Git** (Docker), tuyệt đối không đưa vào mã nguồn hay tài liệu.

### 3.2. Cấu trúc Access Token (`LocalJwtTokenService`)

- **Header**: `alg = HS256`, `typ = at+jwt`, `kid = KeyId`.
- **Claims**:
  - `sub`: `UserId` dạng số nguyên dương.
  - `jti`: GUID không dấu gạch ngang.
  - `iat`: Unix timestamp (UTC).
  - `token_use`: `access` (`LocalJwtBearer.AccessTokenUse`).
  - `scope`:
    - `Customer`: `"orders:read orders:write profile:read profile:write"`
    - `Admin`: `"admin:all"`
- **Thời hạn**: Mặc định `15` phút (`AccessTokenMinutes`), cấu hình hợp lệ trong khoảng `5–60` phút.

### 3.3. Refresh Token Rotation & Thu hồi phiên (`ApiRefreshTokenService`)

Xử lý tại [`ApiRefreshTokenService.cs`](../../src/PhoneStore.Api/Authorization/ApiRefreshTokenService.cs):
- **Định dạng**: Tiền tố `psrt_` + 32 byte ngẫu nhiên mã hóa `Base64Url`. Database chỉ lưu **SHA-256 hex** (`ApiRefreshTokens.TokenHash`), gắn theo `FamilyId`.
- **Xoay vòng (`POST /api/v1/auth/refresh`)**:
  - Mỗi lần refresh hợp lệ: đánh dấu token cũ `RevokedReason = "Rotated"` + ghi `ReplacedByTokenHash`, phát hành cặp Access Token + Refresh Token mới trong cùng `FamilyId`.
  - **Phát hiện tái sử dụng (`ReuseDetected`)**: Nếu một refresh token đã bị thu hồi hoặc đã xoay vòng tiếp tục được gửi lên, hệ thống lập tức thu hồi **toàn bộ token còn hoạt động trong cùng `FamilyId`** với lý do `ReuseDetected` và trả từ chối.
  - Nếu tài khoản đã bị vô hiệu hóa (`IsActive = false`), toàn bộ `FamilyId` bị thu hồi với lý do `AccountInactive`.
  - Nếu token hết hạn, cập nhật `RevokedReason = "Expired"`.
- **Đăng xuất (`POST /api/v1/auth/logout`, yêu cầu `Api.Authenticated`)**:
  - Thu hồi toàn bộ `FamilyId` của refresh token được gửi (kiểm tra đúng chủ sở hữu `UserId`, khác chủ trả `403 Forbidden`), hoặc thu hồi tất cả phiên đang hoạt động của user khi `allSessions = true` / không gửi refresh token (`RevokedReason = "Logout"`), trả `HTTP 204 No Content`.

### 3.4. Chính sách phân quyền (Fail-Closed + Kiểm tra DB tươi)

Định nghĩa tại [`ApiAccess.cs`](../../src/PhoneStore.Api/Authorization/ApiAccess.cs):
- **Fail-Closed Default**: `DefaultPolicy` từ chối mọi request chưa phân loại; mọi Controller action không có `[AllowAnonymous]` đều tự động yêu cầu xác thực.
- **Chính sách hoạt động**:
  - `Api.Customer`: Yêu cầu JWT hợp lệ + truy vấn DB xác nhận `User.IsActive == true` và `Role.RoleName == "Customer"`.
  - `Api.Admin`: Yêu cầu JWT hợp lệ + truy vấn DB xác nhận `User.IsActive == true` và `Role.RoleName == "Admin"`.
  - `Api.Authenticated`: Yêu cầu JWT hợp lệ + user còn active thuộc vai trò `Customer` hoặc `Admin`.
  - `Orders.Read` / `Orders.Write`: Kiểm tra bổ sung claim `scope` chứa `orders:read` / `orders:write`.
- **Chống cache dữ liệu nhạy cảm**: Middleware tự động gắn `Cache-Control: no-store` cho mọi endpoint có `[Authorize]`.

## 4. Phiên Admin WinForms ở chế độ `RestApi` (`PhoneStore.Admin`)

- **Đăng nhập**: `RestAdminAuthService` gọi `POST /api/v1/auth/admin/login` (endpoint này chỉ cấp token cho tài khoản vai trò `Admin` đang active).
- **Lưu trữ phiên**: Token chỉ lưu trong bộ nhớ tiến trình (`AdminApiSessionStore`, không ghi plaintext ra ổ đĩa hay registry).
- **Làm mới chủ động**: `AdminApiClient` dùng đồng bộ hóa khóa (`SemaphoreSlim`) để làm mới access token trước khi hết hạn hoặc khi gặp `401`, tránh nhiều lời gọi refresh song song gây hiểu nhầm `ReuseDetected`.
- **Đăng xuất**: Gọi `POST /api/v1/auth/logout` gửi refresh token hiện tại để thu hồi phiên trên SQL Server (`RevokedReason = Logout`) và xóa sạch bộ nhớ phiên cục bộ.
- **Không fallback ngầm**: Khi `AdminDataAccess:Mode = RestApi`, `PhoneStoreDbContext` không được đăng ký vào DI container nên Admin WinForms không thể âm thầm kết nối thẳng SQL Server.
