# PhoneStore

PhoneStore là đồ án quản lý và bán điện thoại được xây dựng trên .NET 8. Hệ thống gồm website mua hàng cho khách, ứng dụng quản trị Windows Forms, REST API catalog đọc và các thư viện nghiệp vụ/dữ liệu dùng chung.

> Trạng thái hiện tại: build và full regression gần nhất đạt **232/232 tests** (S31.3G, ngày 14/11/2026). API/Web đã có bằng chứng runtime với SQL Server ở S31.3E–F; lần kiểm tra live mới nhất từ môi trường Codex bị giới hạn TLS và không được ghi nhận là một lần live PASS mới.

## Chức năng chính

### Customer Web

- Xem trang chủ, danh sách và chi tiết sản phẩm.
- Tìm kiếm, lọc theo thương hiệu/danh mục và sắp xếp catalog.
- Đăng ký, đăng nhập, đăng xuất và cập nhật hồ sơ cá nhân.
- Quản lý giỏ hàng, kiểm tra giá/tồn kho phía server và đặt hàng.
- Xem lịch sử, chi tiết, trạng thái và hủy đơn khi nghiệp vụ cho phép.
- Giao diện responsive, validation và bảo vệ route theo tài khoản Customer.

### Admin WinForms

- Đăng nhập và kiểm tra quyền quản trị.
- Dashboard KPI, biểu đồ trạng thái và doanh thu.
- Quản lý sản phẩm, ảnh, thương hiệu và danh mục.
- Quản lý khách hàng và đơn hàng.
- Báo cáo, thống kê, bộ lọc thời gian và cảnh báo tồn kho.
- Fresh authorization check trước thao tác ghi quan trọng.

### REST API V1

- Health/readiness của SQL Server.
- Danh sách và chi tiết sản phẩm active.
- Danh sách thương hiệu và danh mục active.
- Pagination, search, filter, sort và validation.
- Problem Details, correlation ID, structured logging an toàn.
- Rate limiting, request limits, timeout và cancellation.
- Swagger/OpenAPI chỉ trong môi trường Development.
- Product Detail của Web gọi API qua typed `HttpClient` khi feature flag bật; có fallback đọc local theo policy đã kiểm thử.

API V1 hiện chỉ đọc và không cung cấp endpoint cho tài khoản, giỏ hàng hay đơn hàng.

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Customer Web | ASP.NET Core MVC/Razor, .NET 8, HTML/CSS/JavaScript |
| Admin | Windows Forms, .NET 8 Windows |
| REST API | ASP.NET Core Web API, Swagger/OpenAPI |
| Business/Data | C#, Entity Framework Core 8, SQL Server |
| Authentication | ASP.NET Core cookie authentication, `PasswordHasher<TUser>` |
| Tests | xUnit, ASP.NET Core TestServer, EF Core SQLite |

SDK được khóa tại .NET `8.0.427` với `latestPatch` trong [`global.json`](global.json).

## Kiến trúc

```text
Customer Web ───────────────→ Business → Data/EF Core → SQL Server
      │
      └─ Product Detail (flag ON) → REST API → Business/Data → SQL Server

Admin WinForms ─────────────→ Business → Data/EF Core → SQL Server
```

Đây là kiến trúc phân lớp trong một repository dùng chung, có thêm HTTP boundary đọc cho catalog. Dự án không được mô tả là microservices hoặc Clean Architecture thuần túy.

### Project chính

| Project | Trách nhiệm |
|---|---|
| `PhoneStore.Web` | Customer Web MVC/Razor |
| `PhoneStore.Admin` | Ứng dụng quản trị WinForms |
| `PhoneStore.Api` | REST API catalog read-only |
| `PhoneStore.Business` | Use case và quy tắc nghiệp vụ |
| `PhoneStore.Data` | EF Core DbContext, mapping và truy vấn dữ liệu |
| `PhoneStore.Models` | Entity, DTO/model dùng chung |
| `PhoneStore.Tests` | Automated tests cách ly |
| `PhoneStore.AdminBootstrap` | Công cụ tạo Admin đầu tiên có guard an toàn |

Phân tích kiến trúc chi tiết: [`docs/system-analysis/10-ARCHITECTURE-AUDIT.md`](docs/system-analysis/10-ARCHITECTURE-AUDIT.md).

## Chạy dự án

### Yêu cầu

- Windows 10/11 (bắt buộc cho Admin WinForms).
- .NET SDK 8.0.427 hoặc bản patch tương thích theo `global.json`.
- SQL Server 2022 hoặc SQL Server tương thích EF Core 8.
- Database `PhoneStoreDB` có schema đúng migrations hiện tại.

### Thiết lập nhanh

```powershell
git clone https://github.com/vuvantai14/PhoneStore.git
cd PhoneStore
dotnet tool restore
dotnet restore PhoneStore.sln
dotnet build PhoneStore.sln
```

Cấu hình `ConnectionStrings__PhoneStoreConnection` bằng biến môi trường hoặc user-secrets phù hợp với máy của bạn. Không commit credential vào repository. Ứng dụng không tự migration hoặc seed database khi khởi động.

### Chạy API

```powershell
dotnet run --project src/PhoneStore.Api/PhoneStore.Api.csproj --launch-profile PhoneStore.Api
```

- API: <http://localhost:5025>
- Swagger trong Development: <http://localhost:5025/swagger>
- Root `/` trả 404 là hành vi bình thường.

### Chạy Customer Web

```powershell
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj --launch-profile http
```

Web chạy tại <http://localhost:5015>.

Để Product Detail dùng API:

```powershell
$env:CatalogApi__Enabled = "true"
$env:CatalogApi__BaseUrl = "http://localhost:5025/"
$env:CatalogApi__TimeoutSeconds = "3"
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj --launch-profile http
```

Flag mặc định là `false`. Khi OFF, Product Detail dùng trực tiếp Business/Data và không gọi API.

### Chạy Admin

```powershell
dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj
```

Không ghi tài khoản/mật khẩu Admin vào source hoặc tài liệu. Quy trình bootstrap Admin đầu tiên được mô tả trong [hướng dẫn local](docs/LOCAL_DEVELOPMENT.md#tạo-admin-đầu-tiên-khi-cần).

Để chạy Admin Windows với SQL Docker trên `127.0.0.1,14334`, dùng environment override lấy secret từ `.env` ignored theo [hướng dẫn Admin ↔ Docker SQL](docs/LOCAL_DEVELOPMENT.md#chạy-admin-với-sql-server-docker). Không sửa `appsettings.json` hoặc hardcode credential.

### Chạy bằng Docker Compose

Docker Compose đóng gói Customer Web, REST API và SQL Server; Admin WinForms vẫn chạy trực tiếp trên Windows.

```powershell
Copy-Item .env.example .env
# Điền MSSQL_SA_PASSWORD mạnh và PRODUCT_UPLOADS_PATH tuyệt đối trong .env (không commit file này).
docker compose -f docker-compose.yml -f docker-compose.restore.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.restore.yml up -d --build
docker compose -f docker-compose.yml -f docker-compose.restore.yml ps
```

Các cổng mặc định của cấu hình cách ly: Web `5015`, API `5025`, SQL Server `14334`. Trong container, Web gọi API qua `http://api:8080/` và Web/API gọi SQL qua service `db:1433`; không dùng `localhost` cho giao tiếp nội bộ.

> Database Docker phải được restore hoặc khởi tạo theo quy trình đã phê duyệt trước khi khởi động toàn stack. Không chạy migration/seed tự động và không dùng `RESTORE ... WITH REPLACE`. Backup `.bak`, `.env` và dữ liệu người dùng không được đưa lên GitHub.

Chi tiết vận hành và trạng thái nghiệm thu: [Local Development](docs/LOCAL_DEVELOPMENT.md#10-docker-compose-cách-ly) và [Docker final completion](docs/system-analysis/30-DOCKER-FINAL-COMPLETION.md).

S31.5F đã nghiệm thu runtime trên project cách ly: SQL/API/Web healthy, API health và các trang Web chính HTTP 200, catalog 40 sản phẩm và 41/41 ảnh được phục vụ thành công. Named volume trên máy đã nghiệm thu đã chứa database; không restore lại khi chỉ khởi động lại stack.

### Chạy tests

```powershell
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj
```

Tests chính dùng SQLite/doubles/TestServer cách ly. Không trỏ test tùy ý vào `PhoneStoreDB` đang vận hành.

Hướng dẫn đầy đủ và troubleshooting: [`docs/LOCAL_DEVELOPMENT.md`](docs/LOCAL_DEVELOPMENT.md).

## API Reference

| Method | Endpoint | Mô tả |
|---|---|---|
| GET | `/api/health` | SQL readiness |
| GET | `/api/v1/products` | Catalog có pagination/filter/sort |
| GET | `/api/v1/products/{id}` | Chi tiết sản phẩm active |
| GET | `/api/v1/brands` | Thương hiệu active |
| GET | `/api/v1/categories` | Danh mục active |

Contracts, status codes, security và runbook: [`docs/system-analysis/17-REST-API-FINAL-ACCEPTANCE.md`](docs/system-analysis/17-REST-API-FINAL-ACCEPTANCE.md).

## Database

Các bảng nghiệp vụ hiện có: `Roles`, `Users`, `Brands`, `Categories`, `Products`, `ProductImages`, `Orders`, `OrderDetails`.

- Thiết kế database: [`docs/system-analysis/09-DATABASE-DESIGN-AUDIT.md`](docs/system-analysis/09-DATABASE-DESIGN-AUDIT.md)
- Schema inventory: [`docs/system-analysis/07-DATABASE-SCHEMA-INVENTORY.md`](docs/system-analysis/07-DATABASE-SCHEMA-INVENTORY.md)

Không chạy migration, seed, restore hoặc bootstrap Admin trên database quan trọng nếu chưa kiểm tra đúng server/database và có phê duyệt phù hợp.

## Testing và nghiệm thu

- S31.5F: Docker Compose ba service runtime PASS; database restore có kiểm soát, Web/API/SQL healthy và smoke test read-only PASS.
- S31.3G: solution build cách ly `0 warnings / 0 errors`; **232/232 tests PASS**, không skip.
- S31.3E–F: API, Web, SQL Server và feature flag/fallback có bằng chứng live runtime.
- Admin WinForms đã được người dùng nghiệm thu thủ công; tài liệu phân biệt rõ manual evidence với automated evidence.
- Fresh live recheck S31.3G từ token Codex gặp lỗi TLS trước SQL; không được dùng để tuyên bố một lần live PASS mới.

Tiến độ hiện tại: [`docs/PROGRESS.md`](docs/PROGRESS.md).

## Giới hạn hiện tại

- API V1 chỉ đọc và chưa có API authentication/JWT.
- Chỉ Product Detail dùng REST API khi feature flag bật.
- Rate limiter lưu trong memory của từng process, chưa hỗ trợ multi-instance distributed state.
- SQL principal cho API chưa tách thành account read-only least privilege.
- Chưa production deployment và chưa load/stress test đầy đủ.
- Docker Compose hiện dành cho development/nghiệm thu cục bộ. Credential bắt buộc lấy từ `.env` ignored; lịch sử Git từng chứa credential cũ nên credential đó không được tái sử dụng.

## Tài liệu

- [Hướng dẫn phát triển local](docs/LOCAL_DEVELOPMENT.md)
- [REST API final acceptance](docs/system-analysis/17-REST-API-FINAL-ACCEPTANCE.md)
- [Architecture audit](docs/system-analysis/10-ARCHITECTURE-AUDIT.md)
- [Database design audit](docs/system-analysis/09-DATABASE-DESIGN-AUDIT.md)
- [Docker & CI guide](docs/DOCKER_AND_CI_GUIDE.md)

## Bảo mật repository

- Không commit `.env`, secrets, database backup, certificate, `bin/`, `obj/`, logs hoặc test output.
- `.env.example` chỉ nên chứa placeholder, không chứa credential có thể sử dụng.
- Nếu một credential từng được dùng thật và đã vào Git history, phải rotate credential; chỉ xóa ở commit mới không loại bỏ giá trị khỏi history.
