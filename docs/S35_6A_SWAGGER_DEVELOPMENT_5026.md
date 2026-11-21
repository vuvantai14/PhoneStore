# S35.6A — PhoneStore Swagger UI on Port 5026 (`http://localhost:5026/swagger`)

- **Ngày thực hiện**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`main`)
- **Swagger UI URL**: `http://localhost:5026/swagger` (`http://127.0.0.1:5026/swagger`)
- **OpenAPI JSON URL**: `http://localhost:5026/swagger/v1/swagger.json`
- **Isolated Docker Project**: `phonestore-dev-swagger`
- **Isolated Database**: `PhoneStoreDB_SwaggerDev`
- **Final Status**: **`VERIFIED — PASS`**

---

## A. Existing Swagger Configuration

Kiểm tra trực tiếp mã nguồn `src/PhoneStore.Api`:

1. **`src/PhoneStore.Api/PhoneStore.Api.csproj:11-13`**:
   - Đã tích hợp sẵn `Swashbuckle.AspNetCore` (`6.6.2`) và `Microsoft.AspNetCore.Authentication.JwtBearer` (`10.0.15`). Không cần cài thêm bất kỳ NuGet package hay middleware bên ngoài nào.
2. **`src/PhoneStore.Api/Program.cs:132-151,163-167`**:
   - Đăng ký `AddEndpointsApiExplorer()` và `AddSwaggerGen()` với tài liệu `v1` (`Title = "PhoneStore API"`, `Version = "v1"`).
   - Định nghĩa sẵn `OpenApiSecurityScheme` tên `"Bearer"` (`Type = SecuritySchemeType.Http`, `Scheme = "bearer"`, `BearerFormat = "JWT"`, `In = ParameterLocation.Header`).
   - Chỉ bật `app.UseSwagger()` và `app.UseSwaggerUI()` khi `app.Environment.IsDevelopment()` (`ASPNETCORE_ENVIRONMENT=Development`). Trên môi trường Production (`localhost:5025`), `/swagger` trả về `HTTP 404`.
3. **`src/PhoneStore.Api/Infrastructure/AuthorizationDocumentationFilter.cs:10-32`**:
   - Tự động bỏ qua các endpoint có `[AllowAnonymous]` (không yêu cầu nhập JWT khi xem tài liệu hoặc gọi các public GET / auth login endpoints).
   - Tự động gắn `Bearer` security requirement và extension `x-phonestore-policy` (`Api.Customer`, `Api.Admin`, `Api.Authenticated`) cùng mô tả scope (`orders:read`, `orders:write`) cho toàn bộ các protected endpoints.

---

## B. Development Runtime Architecture

Để đảm bảo cách ly 100% với môi trường Docker Business (`phonestore-restore-c2` trên cổng `5025`/`5015`/`14334`) và không chạm tới `PhoneStoreDB` business, môi trường Swagger Development được thiết kế dưới dạng một Docker Compose project độc lập (`phonestore-dev-swagger`):

| Thành phần | Cấu hình Development (`phonestore-dev-swagger`) | Cấu hình Business (`phonestore-restore-c2`) | Cách ly |
|---|---|---|---|
| **Compose Project** | `phonestore-dev-swagger` (`scripts/dev/docker-compose.swagger.yml`) | `phonestore-restore-c2` (`docker-compose.yml` + overrides) | **100% độc lập** |
| **API Container** | `phonestore-dev-swagger-api-1` (`ASPNETCORE_ENVIRONMENT=Development`) | `phonestore-restore-c2-api-1` (`ASPNETCORE_ENVIRONMENT=Production`) | **100% độc lập** |
| **Host Port Binding** | `127.0.0.1:5026:8080` (chỉ bind loopback) | `127.0.0.1:5025:8080` (API), `127.0.0.1:5015:8080` (Web) | **Không xung đột** |
| **SQL Container** | `phonestore-dev-swagger-db-1` (**không publish port ra host**) | `phonestore-restore-c2-db-1` (`127.0.0.1:14334:1433`) | **100% độc lập** |
| **Database & Login** | `PhoneStoreDB_SwaggerDev` / `swagger_dev_app` | `PhoneStoreDB` / `sa` | **100% độc lập** |
| **Docker Network** | `phonestore-dev-swagger_swagger_net` | `phonestore-restore-c2_phonestore-net` | **100% độc lập** |
| **Docker Volumes** | `phonestore-dev-swagger_sql_data`, `phonestore-dev-swagger_uploads_data` | `phonestore-restore-c2_mssql_data`, host bind `uploads/products` | **100% độc lập** |
| **JWT SigningKey & Secrets** | `%LOCALAPPDATA%\PhoneStore-DevSwagger\swagger-dev.secrets.json` (`Issuer=https://auth.phonestore.dev.local`, `Audience=phonestore-api-dev`) | `%LOCALAPPDATA%\PhoneStore-BusinessCd\api-jwt.env` (`Issuer=https://auth.phonestore.local`, `Audience=phonestore-api`) | **Khóa khác nhau hoàn toàn** |

---

## C. Port 5026 Setup

1. **Docker Port Mapping (`scripts/dev/docker-compose.swagger.yml:52-53`)**:
   - Bind chặt vào địa chỉ loopback `127.0.0.1:5026:8080` (không mở `0.0.0.0:5026` ra mạng LAN).
2. **Local Launch Profile (`src/PhoneStore.Api/Properties/launchSettings.json:9`)**:
   - Đã cập nhật `"applicationUrl": "http://127.0.0.1:5026"` (thay vì `http://localhost:5025` trước đây) để ngay cả khi chạy từ IDE cũng không bao giờ đụng cổng `5025` của API Business.

---

## D. OpenAPI & JWT Authorize

### 1. Thông số OpenAPI `v1` (`http://localhost:5026/swagger/v1/swagger.json`)

- **Title / Version**: `PhoneStore API` / `v1`
- **Tổng số Paths**: **`71` paths** (gồm các route chuẩn `/api/v1/...`, `/api/health/...` và các route alias tương thích `/api/...`)
- **Tổng số Operations**: **`86` operations**:
  - **Public operations (`14`)**: Không có `security` requirement — gọi trực tiếp trên trình duyệt mà không cần nhập JWT (`GET /api/health`, `/api/health/live`, `/api/health/ready`, `GET /api/v1/brands`, `GET /api/v1/categories`, `GET /api/v1/products`, `/bestsellers`, `/newest`, `/{id}`, `/{id}/related`, `POST /api/v1/auth/login`, `/admin/login`, `/register`, `/refresh`).
  - **Protected operations (`72`)**: Tự động hiển thị biểu tượng ổ khóa kèm `Bearer` security requirement và `x-phonestore-policy` (`Api.Customer`, `Api.Admin`, `Api.Authenticated`).
  - **Phân bố HTTP Methods**: `GET = 44`, `POST = 17`, `PUT = 9`, `PATCH = 14`, `DELETE = 2`.

### 2. Hướng dẫn sử dụng nút `Authorize` (JWT Bearer) trên Swagger UI

1. Mở trình duyệt tại: **`http://localhost:5026/swagger`**
2. Để thử các **Public GET endpoints** (`/api/v1/products`, `/api/v1/brands`, `/api/v1/categories`, `/api/health/ready`): Bấm **Try it out** -> **Execute** ngay mà không cần đăng nhập.
3. Để thử các **Protected endpoints** (`Admin` hoặc `Customer`):
   - Lấy thông tin tài khoản test Development (`DevAdminEmail` / `DevAdminPassword` hoặc `DevCustomerEmail` / `DevCustomerPassword`) trong file cục bộ chỉ dành cho user hiện tại:
     `%LOCALAPPDATA%\PhoneStore-DevSwagger\swagger-dev.secrets.json`
   - Trên Swagger UI, mở `POST /api/v1/auth/admin/login` (hoặc `POST /api/v1/auth/login`), bấm **Try it out**, nhập `email` và `password` của fixture rồi bấm **Execute**.
   - Sao chép giá trị trường `accessToken` trong JSON phản hồi `200 OK`.
   - Bấm nút **Authorize** (biểu tượng ổ khóa ở góc trên bên phải Swagger UI), dán `accessToken` vào ô **Value** (Swashbuckle `http/bearer` tự động gắn tiền tố `Bearer ` vào header `Authorization: Bearer {access_token}`) và bấm **Authorize**.

---

## E. Runtime HTTP Tests

### 1. Build & Automated Unit/Integration Tests

- `dotnet build src/PhoneStore.Api/PhoneStore.Api.csproj -c Release --no-restore`: **PASS (`0 Warning(s), 0 Error(s)`)**.
- Targeted Swagger/OpenAPI & JWT Authorization test suite (`ApiFoundationTests`, `LocalJwtAuthenticationTests`, `ApiAuthorizationTests`, `ApiSecurityHardeningTests`): **`56/56 PASS` (`0` Failed, `0` Skipped, `6s`)**.

### 2. Live HTTP Verification trên Port `5026` (`Invoke-S356A-ValidateSwagger5026.ps1`)

| Hạng mục / Endpoint | HTTP Method & URL | Auth Header | HTTP Status | Kết quả chi tiết |
|---|---|---|---|---|
| **Swagger UI HTML** | `GET http://localhost:5026/swagger/index.html` | None | **`200`** | `text/html; charset=utf-8` (`4,681` chars, chứa `swagger-ui`) |
| **Swagger JS Bundle** | `GET http://localhost:5026/swagger/swagger-ui-bundle.js` | None | **`200`** | `text/javascript` (`1,455,809` chars, `0` lỗi tải asset) |
| **Swagger CSS** | `GET http://localhost:5026/swagger/swagger-ui.css` | None | **`200`** | `text/css` (`152,034` chars) |
| **OpenAPI v1 JSON** | `GET http://localhost:5026/swagger/v1/swagger.json` | None | **`200`** | `application/json` (`71` paths, `86` operations, scheme `Bearer`) |
| **Health Live** | `GET http://localhost:5026/api/health/live` | None | **`200`** | `{"status":"healthy"}` |
| **Health Ready** | `GET http://localhost:5026/api/health/ready` | None | **`200`** | `{"status":"healthy"}` (kết nối `PhoneStoreDB_SwaggerDev`) |
| **Public Products** | `GET http://localhost:5026/api/v1/products` | None | **`200`** | `totalItems = 3` (dữ liệu mẫu Dev Fixture, tách biệt khỏi `40` sản phẩm business) |
| **Public Brands** | `GET http://localhost:5026/api/v1/brands` | None | **`200`** | `2` brands (`Apple (Dev Fixture)`, `Samsung (Dev Fixture)`) |
| **Public Categories** | `GET http://localhost:5026/api/v1/categories` | None | **`200`** | `2` categories (`Flagship (Dev Fixture)`, `Mid-Range (Dev Fixture)`) |
| **Anonymous -> Protected** | `GET /api/v1/admin/dashboard`, `/admin/products`, `/profile` | None | **`401`** (x3) | `ProblemDetails` + `WWW-Authenticate: Bearer` |
| **Customer Login** | `POST http://localhost:5026/api/v1/auth/login` | None | **`200`** | Cấp JWT `Customer` hợp lệ |
| **Customer -> Own Endpoints** | `GET /api/v1/profile`, `GET /api/v1/orders` | `Bearer <CustomerJWT>` | **`200`** (x2) | Truy cập thành công dữ liệu Customer |
| **Customer -> Admin Endpoints** | `GET /api/v1/admin/dashboard`, `GET /api/v1/admin/products` | `Bearer <CustomerJWT>` | **`403`** (x2) | Chặn đúng policy `Api.Admin` (`403 Forbidden`) |
| **Admin Login** | `POST http://localhost:5026/api/v1/auth/admin/login` | None | **`200`** | Cấp JWT `Admin` hợp lệ |
| **Admin -> Admin Endpoints** | `GET /api/v1/auth/me`, `/admin/dashboard`, `/admin/products`, `/admin/brands`, `/admin/categories` | `Bearer <AdminJWT>` | **`200`** (x5) | Truy cập đầy đủ các API quản trị |
| **Admin -> Customer Orders** | `GET http://localhost:5026/api/v1/orders` | `Bearer <AdminJWT>` | **`403`** | Chặn đúng policy `Api.Customer` (`403 Forbidden`) |

---

## F. Start/Stop Scripts

Đã bổ sung bộ công cụ khởi chạy và dừng an toàn trong `scripts/dev/`:

1. **[Start-PhoneStoreSwagger.ps1](file:///c:/PhoneStore/PhoneStore/scripts/dev/Start-PhoneStoreSwagger.ps1)**:
   - Kiểm tra invariants của stack business `phonestore-restore-c2` trước và sau khi chạy.
   - Kiểm tra cổng `5026`: nếu `phonestore-dev-swagger-api-1` đã chạy và `healthy`, báo `[PASS] PhoneStore Development Swagger is already running and healthy` và thoát `0` (idempotent, không tạo fixture trùng lặp); nếu cổng `5026` bị chiếm bởi tiến trình lạ, dừng `BLOCKED` và không kill tiến trình lạ.
   - Tự khởi tạo file bí mật `%LOCALAPPDATA%\PhoneStore-DevSwagger\swagger-dev.secrets.json` (khóa Windows ACL chỉ cho user hiện tại, đối chiếu `Get-CdForbiddenBusinessSecrets` để đảm bảo không trùng bất kỳ mật khẩu/khóa business nào).
   - Thực thi `Assert-CdResourceGate` (`SqlColdStart/Pre` hoặc `StartApi/Pre`) trước khi bật container SQL/API.
   - Tự động áp dụng 3 EF Core migrations và seed dữ liệu mẫu cách ly vào `PhoneStoreDB_SwaggerDev`.
   - Hỗ trợ tham số `-LaunchBrowser` để mở nhanh trình duyệt tại `http://localhost:5026/swagger`.
2. **[Stop-PhoneStoreSwagger.ps1](file:///c:/PhoneStore/PhoneStore/scripts/dev/Stop-PhoneStoreSwagger.ps1)**:
   - Chỉ dừng và xóa các container/network thuộc project `phonestore-dev-swagger` (hỗ trợ tùy chọn `-RemoveVolumes` nếu muốn xóa cả dữ liệu volume fixture).
   - Kiểm tra invariants của stack business `phonestore-restore-c2` trước và sau khi dừng.
3. **[docker-compose.swagger.yml](file:///c:/PhoneStore/PhoneStore/scripts/dev/docker-compose.swagger.yml)**:
   - Cấu hình Compose cách ly cho `phonestore-dev-swagger` (`db` + `api` trên `127.0.0.1:5026:8080`).

---

## G. Business Invariants

Đối chiếu trạng thái stack Business (`phonestore-restore-c2`) trước và sau khi khởi chạy Swagger Development trên cổng `5026`:

| Container Business | Container ID | `StartedAt` (UTC) | `Health` / `Restarts` | HTTP / Trạng thái |
|---|---|---|---|---|
| `phonestore-restore-c2-db-1` | `addd99b34bf2...c50a6` *(không đổi)* | `2026-11-21T00:27:43.48526107Z` *(không đổi)* | `healthy` / `0` | `PhoneStoreDB` giữ nguyên `40` sản phẩm, `15` đơn hàng, `1` Admin |
| `phonestore-restore-c2-api-1` | `12a06d6e6694...c8d2` *(không đổi)* | `2026-11-21T10:09:05.598296081Z` *(không đổi)* | `healthy` / `0` | `http://127.0.0.1:5025/api/health/ready` -> `200`, `/api/v1/products` -> `200` (`40` items), **`/swagger/index.html` -> `404`** |
| `phonestore-restore-c2-web-1` | `3595f703f7a5...2b8f` *(không đổi)* | `2026-11-21T10:06:19.636571761Z` *(không đổi)* | `healthy` / `0` | `http://127.0.0.1:5015/` -> `200` |

---

## H. Remaining Limitations

1. **Cổng an toàn RAM khi Cold-Start SQL Fixture**:
   - Lần đầu khởi động `phonestore-dev-swagger-db-1` (khi container SQL fixture chưa chạy), `ResourceGate` yêu cầu `WinFree >= 3.80 GiB` và `WslAvail >= 4.50 GiB` để tránh gây áp lực bộ nhớ lên stack business. Khi không còn sử dụng Swagger UI, người dùng có thể chạy `scripts/dev/Stop-PhoneStoreSwagger.ps1` để giải phóng ~1.4 GB RAM của SQL fixture.
2. **Dữ liệu mẫu tách biệt**:
   - Mọi thao tác `POST / PUT / PATCH / DELETE` thực hiện từ Swagger UI trên cổng `5026` chỉ tác động lên database `PhoneStoreDB_SwaggerDev` (chứa 3 sản phẩm mẫu), hoàn toàn không ảnh hưởng tới `PhoneStoreDB` business trên cổng `5025`/`5015`.

---

## I. Final Decision

**`PASS` — Swagger UI Development đang hoạt động thực tế tại `http://localhost:5026/swagger` với đầy đủ 71 paths / 86 operations, hỗ trợ JWT Bearer Authorize và cách ly tuyệt đối với môi trường Business.**
