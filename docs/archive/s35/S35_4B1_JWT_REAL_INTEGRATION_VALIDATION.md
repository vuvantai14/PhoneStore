# S35.4B.1 — Real JWT End-to-End Integration Validation

- **Execution Date**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`main`, `HEAD = 70c9cd4bab2a8cb33e143fb0ecbd0bc46288612a`)
- **Final Status**: **`PASS`** *(34/34 real SQL Server 2022 + Linux Docker HTTP integration checks PASS; 0 fixture resources remaining; 3/3 business containers 100% unchanged)*

---

## A. Safety Preflight

Kết quả kiểm tra trực tiếp trước và trong suốt quá trình thực thi lượt hoàn chỉnh (`task-4620`, `2026-11-21T05:26Z`):

| Hạng mục Preflight | Giá trị đo thực tế | Ngưỡng bắt buộc | Trạng thái |
|---|---|---|---|
| Git Branch / HEAD | `main` / `70c9cd4bab2a8cb33e143fb0ecbd0bc46288612a` | Hợp lệ | **PASS** |
| Docker Desktop / WSL2 | Hoạt động bình thường (`desktop-linux`) | Running | **PASS** |
| Business Containers (`phonestore-restore-c2`) | `db-1`, `api-1`, `web-1` đều `Running = true`, `Health = healthy`, `Restarts = 0` | 3/3 healthy | **PASS** |
| Fixture Isolation (`phonestore-s354b1-jwt`) | Compose project, network `phonestore-s354b1-jwt_jwt_net`, volume `phonestore-s354b1-jwt_sql_data` tách biệt hoàn toàn; SQL **không** publish port host | Cách ly 100% | **PASS** |
| `SqlColdStart/Pre` RAM Gate | `WinFree = 4.11 GiB`, `WslAvail = 5.37 GiB`, `Swap = 0 MiB` | `WinFree >= 3.80 GiB`, `WslAvail >= 4.50 GiB`, `Swap <= 128 MiB` | **PASS** |
| `SqlProvision/Pre` RAM Gate (sau SQL cold-start) | `WinFree = 3.02 GiB`, `WslAvail = 4.84 GiB`, `Swap = 0 MiB` | `WinFree >= 2.20 GiB`, `WslAvail >= 3.00 GiB`, `Swap <= 128 MiB` | **PASS** |
| `StartApi/Pre` RAM Gate | `WinFree = 2.99 GiB`, `WslAvail = 4.82 GiB`, `Swap = 0 MiB` | `WinFree >= 2.20 GiB`, `WslAvail >= 2.80 GiB`, `Swap <= 128 MiB` | **PASS** |
| Post-Test RAM & Business Health | `WinFree = 2.91 GiB`, `WslAvail = 4.70 GiB`, `Swap = 0 MiB`, `business = healthy` | Healthy, không chạm WarningFloor | **PASS** |

*(Ghi nhận vận hành RAM: ở lượt preflight ban đầu trước khi giải phóng Chrome/VS Code và clean Linux `buff/cache` sau `docker build`, Phased Tiered RAM Gate đã tự động chặn đúng tại `WinFree = 1.58 GiB < 3.80 GiB` và `1.80 GiB < 2.20 GiB` mà không vượt rào; sau khi người dùng đóng ứng dụng và cho phép xả clean page cache WSL2, toàn bộ các pha đều vượt chuẩn an toàn.)*

---

## B. SQL Fixture & Migrations

- **Container SQL Server 2022 fixture**: `49f0366d1cffc73019c6684e1be7a48929786d64951dc7bc48fc0d92ce162d2c` (`mcr.microsoft.com/mssql/server@sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd`, `mem_limit: 2304m`, `MSSQL_MEMORY_LIMIT_MB=1536`, **không mở port host**). Trạng thái: **`healthy`**.
- **Database & Login tách biệt**: Tạo `PhoneStoreDB_JwtIntegration` (có guard chặn `IF DB_ID(N'PhoneStoreDB') IS NOT NULL THROW 52000`) và login tối thiểu quyền `jwt_fixture_app` (`db_datareader` + `db_datawriter`), sinh mật khẩu ephemeral ngẫu nhiên trong bộ nhớ (xác minh không trùng `.env`).
- **EF Core Migrations thật (`dotnet ef migrations script --idempotent --configuration Release`)**: Áp dụng thành công đầy đủ **3/3 migrations** vào `PhoneStoreDB_JwtIntegration`:
  1. `20261001150857_InitialCreate`
  2. `20261102122856_EnforceSingleAdmin`
  3. `20261121044020_AddApiRefreshTokens`
- **Kiểm tra schema & indexes `ApiRefreshTokens`**: Xác nhận đủ 4 index trên SQL Server 2022 thật:
  - `PK_ApiRefreshTokens`
  - `UX_ApiRefreshTokens_TokenHash` (UNIQUE)
  - `IX_ApiRefreshTokens_FamilyId`
  - `IX_ApiRefreshTokens_UserId`
- **Dữ liệu tổng hợp (synthetic seed)**: `1 Admin` (`RoleId = 1`), `1 Active Customer` (`RoleId = 2`), `1 Inactive Customer` (`RoleId = 2`, `IsActive = 0`), `1 Brand`, `1 Category`, `1 Product`, `1 Order` (`Status = 'Pending'`) + `1 OrderDetail` (dùng mật khẩu PBKDF2-HMAC-SHA512 ASP.NET Core Identity v3 thật, không dùng dữ liệu hay tài khoản business).

---

## C. Real API Docker Startup

- **Docker Image build từ source S35.4B (`src/PhoneStore.Api/Dockerfile`, `linux/amd64`)**: `phonestore-api:s35-4b1-test` (`sha256:5695cd1625d4c2e6623511da9485109e1704fd0dbd5c89b6e96cbd0bd7e1e91d`).
- **API Container thật**: `25b5f1a77a326740de757b775e5940a01c203de6daf5ad725cc8a5513a7e8425` (chạy `ASPNETCORE_ENVIRONMENT=Production`, `ApiAuthentication__Mode=LocalJwt`, ephemeral `SigningKey` >= 32 bytes, bind riêng `127.0.0.1:5396:8080`, kết nối SQL fixture qua mạng nội bộ `Server=db,1433`).
- **Kết quả kiểm tra khởi động**:
  - Docker healthcheck: **`healthy`**.
  - `GET /api/health/live`: **`HTTP 200`**.
  - `GET /api/health/ready` (kết nối thật `PhoneStoreDB_JwtIntegration`): **`HTTP 200`**.
  - `GET /swagger/v1/swagger.json` trong `Production`: **`HTTP 404`** (Swagger tắt hoàn toàn).

---

## D. Customer Authentication (Live SQL Server + Docker HTTP)

| Kịch bản kiểm thử | Kết quả HTTP & Kiểm chứng | Trạng thái |
|---|---|---|
| `POST /api/v1/auth/login` (Customer hợp lệ) | `HTTP 200`, `Cache-Control: no-store`, trả Bearer JWT + Refresh Token (`psrt_*`), `userId=2`, `role=Customer`, không chứa `passwordHash` | **PASS** |
| Sai mật khẩu / Email không tồn tại / Customer bị vô hiệu hóa (`IsActive=0`) | Đều trả `HTTP 401`, nội dung `detail` sai mật khẩu và không tồn tại email **giống hệt nhau** (chống account enumeration) | **PASS** |
| `GET /api/v1/auth/me` với Customer Bearer JWT | `HTTP 200`, `userId=2`, `role=Customer`, `scopes=orders:read,orders:write,profile:read,profile:write` | **PASS** |
| `POST /api/v1/auth/register` kèm `roleId: 1, role: "Admin"` | `HTTP 400` (`JsonUnmappedMemberHandling.Disallow` chặn leo thang đặc quyền) | **PASS** |
| `POST /api/v1/auth/register` hợp lệ | `HTTP 201`, tạo duy nhất `Customer`, không lộ mật khẩu/hash; truy vấn SQL `SELECT COUNT(*) FROM Users WHERE RoleId=1` = **`1`** (`Single Admin` giữ nguyên) | **PASS** |
| `POST /api/v1/auth/register` trùng email | `HTTP 409 Conflict` | **PASS** |

---

## E. Admin Authentication (Live SQL Server + Docker HTTP)

| Kịch bản kiểm thử | Kết quả HTTP & Kiểm chứng | Trạng thái |
|---|---|---|
| `POST /api/v1/auth/admin/login` bằng tài khoản Admin hợp lệ | `HTTP 200`, `role=Admin`, cấp Bearer JWT + Refresh Token | **PASS** |
| `POST /api/v1/auth/admin/login` bằng tài khoản Customer | `HTTP 403 Forbidden` | **PASS** |

---

## F. Authorization (Live SQL Server + Docker HTTP)

Kiểm chứng trên 4 Admin GET routes (`/api/admin/products`, `/api/admin/orders`, `/api/admin/customers`, `/api/admin/reports/overview?fromDate=2026-11-01&toDate=2026-11-21`) và 2 Customer routes (`/api/orders`, `/api/v1/profile`):

| Nhóm quyền | Admin GET endpoints (4 routes) | Customer `/api/orders` | Customer `/api/v1/profile` | Trạng thái |
|---|---|---|---|---|
| **Admin JWT** | **4/4 `HTTP 200`** | **`HTTP 403`** | **`HTTP 403`** | **PASS** |
| **Customer JWT** | **4/4 `HTTP 403`** | **`HTTP 200`** (`totalCount=1`) | **`HTTP 200`** | **PASS** |
| **Anonymous (không token)** | **4/4 `HTTP 401`** | **`HTTP 401`** | **`HTTP 401`** | **PASS** |

---

## G. Refresh Rotation & Logout (Live SQL Server 2022 + Race Condition)

| Kịch bản kiểm thử trên SQL Server 2022 thật | Kết quả thực tế | Trạng thái |
|---|---|---|
| Không lưu plaintext refresh token trong `ApiRefreshTokens` | `SELECT COUNT(*) FROM ApiRefreshTokens WHERE TokenHash LIKE 'psrt_%'` = **`0`** | **PASS** |
| `POST /api/v1/auth/refresh` hợp lệ | **`HTTP 200`**, xoay vòng cấp cặp Access + Refresh mới khác token cũ | **PASS** |
| Reuse detection (dùng lại refresh token đã xoay vòng) | Gửi lại token cũ -> **`HTTP 401`**; gửi token mới trong cùng `FamilyId` -> **`HTTP 401`**; SQL xác nhận `RevokedReason = 'ReuseDetected'` (`reuseDetectedRows = 1`) | **PASS** |
| **Concurrent Refresh Race Condition** (2 HTTP request song song cùng gửi 1 refresh token thật vào SQL Server 2022 `UPDLOCK, HOLDLOCK` + `Serializable`) | **`ParallelStatuses = 200, 401`** — tuần tự hóa an toàn, tối đa 1 request thành công, không tạo 2 phiên song song và không deadlock | **PASS** |
| Cross-user logout revocation (`Customer` gọi `POST /api/v1/auth/logout` với `refreshToken` của `Admin`) | **`HTTP 403 Forbidden`**; refresh token của Admin sau đó vẫn hoạt động (`HTTP 200`) | **PASS** |
| Own logout revocation (`POST /api/v1/auth/logout` với token của chính mình) | **`HTTP 204 No Content`**; gọi lại `/api/v1/auth/refresh` bằng token đã logout -> **`HTTP 401`** | **PASS** |

---

## H. Profile API (Live SQL Server + Docker HTTP)

- `GET /api/v1/profile` (Customer JWT): **`HTTP 200`**.
- `PUT /api/v1/profile` (cập nhật `fullName`, `phone`, `address`): **`HTTP 200`**; kiểm tra trực tiếp bảng `Users` trên SQL Server 2022 fixture khớp tuyệt đối: `Fixture Customer Updated|0911223344|789 Updated Ave|2|customer.fixture@jwt-test.local`.
- `PUT /api/v1/profile` gửi thêm trường nhạy cảm (`roleId: 1`, `email`): **`HTTP 400`** (bị chặn bởi `JsonUnmappedMemberHandling.Disallow`, `RoleId` và `Email` trong SQL không thay đổi).

---

## I. JWT Security & Hardening (Live Docker HTTP)

| Hạng mục bảo mật | Kết quả thực tế | Trạng thái |
|---|---|---|
| JWT validation (tampered signature / wrong issuer / wrong audience / expired / missing) | **`401 / 401 / 401 / 401 / 401`** | **PASS** |
| Giới hạn kích thước request body (`MaxRequestBodyBytes = 1024`, gửi payload `2048` bytes) | **`HTTP 413 Payload Too Large`** | **PASS** |
| Rate limiting đăng nhập (`AuthRequestsPerMinute = 2`, gửi 3 request từ cùng `X-Forwarded-For` qua proxy tin cậy) | **`(401, 401, 429)`** kèm header **`Retry-After: 60`** | **PASS** |
| Quét log rò rỉ bí mật trên cả 2 container API (`53,346` ký tự log) | **`0` rò rỉ** mật khẩu (Admin/Customer/Inactive/Registered/SA/App), `SigningKey`, JWT access token, refresh token `psrt_*` hoặc `Authorization: Bearer` | **PASS** |

---

## J. Exit Code 139 Investigation — **ROOT CAUSE IDENTIFIED, FIXED & VERIFIED ON LINUX DOCKER**

1. **Nguyên nhân gốc**: Khi chạy `PhoneStore.Api` với `ApiAuthentication__Mode=LocalJwt` mà thiếu `SigningKey`, `StartupValidator` ném `OptionsValidationException` hợp lệ, nhưng `Program.Main` trước đây không bắt ngoại lệ này nên ngoại lệ chưa xử lý thoát khỏi `Main()` và làm CoreCLR trên Linux Docker (`--memory 256m`) kết thúc bằng native crash (`exit code 139`).
2. **Khắc phục (`src/PhoneStore.Api/Program.cs:176-184`)**: Bọc `app.Run()` bằng `try { app.Run(); } catch (OptionsValidationException ex)` (giữ nguyên `HostAbortedException`), ghi `[FATAL] Startup configuration validation failed ...` ra `stderr` và đặt `Environment.ExitCode = 1`.
3. **Bằng chứng trên Linux Docker thật**: Chạy container `phonestore-api:s35-4b1-test` (`--network none --memory 256m --cpus 0.5`, `ASPNETCORE_ENVIRONMENT=Production`, `ApiAuthentication__Mode=LocalJwt`, thiếu `SigningKey`):
   - Mã thoát: **`ExitCode = 1`** (không còn `SIGSEGV 139`).
   - Output xác nhận đầy đủ `[FATAL] Startup configuration validation failed` và `OptionsValidationException`.

---

## K. HTTPS & Reverse Proxy — **GAPS FIXED & VERIFIED ON LINUX DOCKER**

1. **Khoảng trống & lỗi thực tế phát hiện và đã sửa trong S35.4B.1**:
   - **Reverse Proxy TLS (`ForwardedHeaders`)**: Bổ sung `ApiOperational:TrustedProxies` và `ApiOperational:TrustedProxyNetworks` (mặc định rỗng `[]`, fail-closed) và cấu hình `app.UseForwardedHeaders(...)` (`ForwardLimit = 1`, xóa sạch `KnownProxies`/`KnownIPNetworks` mặc định) trong `src/PhoneStore.Api/Program.cs` và `src/PhoneStore.Api/Infrastructure/ApiOperationalOptions.cs`.
   - **Lỗi thực tế trên Linux Kestrel dual-stack phát hiện trong lượt chạy Docker thật**: Kestrel bind `[::]:8080` giao kết nối loopback IPv4 (`127.0.0.1`) dưới dạng IPv4-mapped IPv6 `::ffff:127.0.0.1`, khiến `IPAddress.IsLoopback` trả `false` và chặn healthcheck nội bộ khi bật `RequireHttps=true`. Đã sửa chuẩn hóa `remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote` trong `src/PhoneStore.Api/Infrastructure/RequestSafetyMiddleware.cs:81-91` và `src/PhoneStore.Api/Program.cs:213-225`, kèm unit test bao phủ cả `127.0.0.1`, `::1` và `::ffff:127.0.0.1` (`tests/PhoneStore.Tests/LocalJwtAuthenticationTests.cs:441-458`).
2. **Kiểm chứng trên container Docker Production thật (`5390cfcf2e69bd5d4c39e9a8e3ada46f4018b609c5977a796d3ce5df9c0314cc`, `RequireHttps=true`, `TrustedProxyNetworks` cho mạng cầu Docker)**:
   - Healthcheck loopback nội bộ (`GET http://127.0.0.1:8080/api/health`): **`HTTP/1.1 200 OK`**.
   - HTTP thường không có `X-Forwarded-Proto: https` tới `/api/v1/auth/me`: **`HTTP 400`** (`HTTPS required`).
   - Request xác thực JWT Bearer qua reverse proxy tin cậy gửi `X-Forwarded-Proto: https` + `X-Forwarded-For`: **`HTTP 200`**.
3. **Kiểm chứng build & automated tests sau sửa lỗi cuối**:
   - `dotnet build PhoneStore.sln -c Release --no-restore`: **PASS (`0 Warning(s)`, `0 Error(s)`)**.
   - Targeted API & JWT Security tests (`LocalJwtAuthenticationTests` + `ApiSecurityHardeningTests`): **PASS (`37/37`)** *(cùng `85/85` bộ mở rộng trước sửa 4 dòng dual-stack)*.

---

## L. Cleanup

Sau khi kết thúc bộ kiểm thử, khối `finally` đã dừng có kiểm soát (`api -t 10`, `db -t 60`) và xóa sạch **chỉ riêng** tài nguyên fixture `phonestore-s354b1-jwt` và image tạm `phonestore-api:s35-4b1-test`:

- `docker ps -aq --filter label=com.docker.compose.project=phonestore-s354b1-jwt`: **`0`** (rỗng).
- `docker network ls --filter label=com.docker.compose.project=phonestore-s354b1-jwt`: **`0`** (rỗng).
- `docker volume ls --filter label=com.docker.compose.project=phonestore-s354b1-jwt`: **`0`** (rỗng).
- Image tạm `phonestore-api:s35-4b1-test`: **đã xóa**; không chạy `docker system prune`, không xóa rollback images hay business volumes.

---

## M. Business Invariants

Đối chiếu chữ ký JSON đầy đủ từ `Get-CdBusinessInvariantsSnapshot` trước (`BIZ_BEFORE`) và sau (`BIZ_AFTER`) toàn bộ quá trình S35.4B.1 — **khớp chính xác 100% (`Exact signature match`)**:

| Container Business | Container ID | Image ID | `StartedAt` (UTC) | `Running` / `Health` / `Restarts` | Mounts | Kết luận |
|---|---|---|---|---|---|---|
| `phonestore-restore-c2-db-1` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `2026-11-21T00:27:43.48526107Z` | `true` / `healthy` / `0` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:True` | **100% UNCHANGED** |
| `phonestore-restore-c2-api-1` | `cec828cd68c70cd647dda1bed7d6c2d61da6f67ca18eba423d4ce82e798986f3` | `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050` | `2026-11-21T03:53:44.575875686Z` | `true` / `healthy` / `0` | *(none)* | **100% UNCHANGED** |
| `phonestore-restore-c2-web-1` | `ed6ef0f8027d5ffd50455f9e2db66bc20a386abf7d21ce24bb2646973a46c75b` | `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` | `2026-11-21T03:53:54.556815843Z` | `true` / `healthy` / `0` | `bind::/app/wwwroot/uploads/products:False` | **100% UNCHANGED** |

---

## N. Final Status: **`PASS`**

- Toàn bộ **34/34** kiểm thử tích hợp thực tế trên **SQL Server 2022 thật** (`PhoneStoreDB_JwtIntegration`) và **Linux Docker `PhoneStore.Api` thật** đều **PASS**.
- Đã phát hiện và khắc phục triệt để 2 vấn đề thực tế chỉ lộ diện khi chạy Linux Docker (`ExitCode 139 -> 1` và dual-stack `::ffff:127.0.0.1` loopback healthcheck khi `RequireHttps=true`), đồng thời bổ sung hỗ trợ Reverse Proxy TLS fail-closed (`TrustedProxies`/`TrustedProxyNetworks`).
- Không triển khai lên Docker business, không chạy migration/seed lên SQL business, không kích hoạt Windows Scheduled Task và chưa commit/push. Sẵn sàng cho bước audit pre-commit/push hoặc giai đoạn tiếp theo (**S35.4C**).
