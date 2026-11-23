# PhoneStore — Kiến trúc hệ thống hiện hành

> Tài liệu mô tả kiến trúc thực tế đang vận hành của PhoneStore trên `.NET 10`, đối chiếu trực tiếp từ mã nguồn.
> Tài liệu phân tích/lịch sử triển khai liên quan được lưu tại [`archive/s31-system-analysis/10-ARCHITECTURE-AUDIT.md`](../archive/s31-system-analysis/10-ARCHITECTURE-AUDIT.md), [`archive/s35/S35_4A_FULL_API_CAPABILITY_AUDIT.md`](../archive/s35/S35_4A_FULL_API_CAPABILITY_AUDIT.md), [`archive/s35/S35_4E_ADMIN_WINFORMS_API_INTEGRATION.md`](../archive/s35/S35_4E_ADMIN_WINFORMS_API_INTEGRATION.md) và [`archive/s35/S35_6B_ADMIN_DEFAULT_RESTAPI.md`](../archive/s35/S35_6B_ADMIN_DEFAULT_RESTAPI.md).

## 1. Tổng quan nền tảng và giải pháp

- **SDK & Runtime**: `.NET 10` (`net10.0` cho Web/API/libraries/tests, `net10.0-windows` cho Admin WinForms), ghim SDK `10.0.403` (`rollForward: latestPatch`) trong [`global.json`](../../global.json).
- **Solution [`PhoneStore.sln`](../../PhoneStore.sln)** gồm **8 project chính**:
  - `src/PhoneStore.Models` — Entities, Enums, DTOs, hằng số nghiệp vụ/vai trò.
  - `src/PhoneStore.Data` — EF Core `10.0.15`, [`PhoneStoreDbContext`](../../src/PhoneStore.Data/Context/PhoneStoreDbContext.cs), Entity Configurations, Migrations và các Store truy cập dữ liệu.
  - `src/PhoneStore.Business` — Tầng nghiệp vụ dùng chung (Catalog, Authentication, Purchasing, Management, Reporting, Image Storage/Validation và REST API client cho Admin).
  - `src/PhoneStore.Web` — Ứng dụng Customer Web (ASP.NET Core MVC, Razor Views, Cookie Auth, Session).
  - `src/PhoneStore.Api` — Ứng dụng REST API (ASP.NET Core Web API, LocalJwt + Refresh Token Rotation, Health checks, Swagger ở `Development`).
  - `src/PhoneStore.Admin` — Ứng dụng Admin Desktop (Windows Forms, mặc định gọi REST API, hỗ trợ dự phòng `DirectSql`).
  - `tests/PhoneStore.Tests` — Bộ kiểm thử tự động (`xUnit`, SQLite in-memory và `WebApplicationFactory` cách ly).
  - `tools/PhoneStore.AdminBootstrap` — Công cụ CLI cấp/cấu hình tài khoản Admin đơn nhất (chỉ dùng khi được phê duyệt).
- **3 project phụ trợ ngoài solution chính** (build/kiểm tra riêng theo nhu cầu hoặc CI):
  - `tools/PhoneStore.CatalogImport` — Tiện ích nhập dữ liệu danh mục.
  - `tools/PhoneStore.AcceptanceEnvironment` — Hỗ trợ dựng môi trường nghiệm thu cách ly.
  - `tests/PhoneStore.SqlIntegration` — Kiểm thử tích hợp SQL Server opt-in có guard chống chạm DB chính (xem [`tests/PhoneStore.SqlIntegration/README.md`](../../tests/PhoneStore.SqlIntegration/README.md)).

## 2. Sơ đồ phân tầng & phụ thuộc

```text
PhoneStore.Models
  ↑
PhoneStore.Data (EF Core 10, PhoneStoreDbContext, 9 DbSets, 3 Migrations)
  ↑
PhoneStore.Business (Catalog, Auth, Purchasing, Management, Reporting, AdminApi Clients)
  ↑
  ├── PhoneStore.Web    (Customer MVC Web; tùy chọn gọi REST API cho Product Detail qua CatalogApi)
  ├── PhoneStore.Api    (REST API: Public Catalog, Health, Auth, Customer, Admin)
  └── PhoneStore.Admin  (WinForms: mặc định RestApi -> PhoneStore.Api; dự phòng DirectSql)
```

Nguyên tắc phân tầng:
- Không phụ thuộc vòng (`circular dependency`).
- Luật nghiệp vụ đặt tại `PhoneStore.Business`, không lặp lại trong Controller/Form.
- Dữ liệu nghiệp vụ chỉ dùng **một nguồn SQL Server thống nhất** (`PhoneStoreDB` trên Docker SQL Server 2022; chi tiết tại [`DATABASE_DESIGN.md`](DATABASE_DESIGN.md)).

## 3. Ba ứng dụng đầu cuối

### 3.1. Customer Web (`src/PhoneStore.Web`)
- Giao diện người dùng cuối (`W01–W11`: Trang chủ, Danh sách/Chi tiết sản phẩm, Giỏ hàng, Đăng nhập/Đăng ký, Thanh toán, Đặt hàng thành công, Hồ sơ, Đơn hàng của tôi, Chi tiết đơn).
- Đọc/ghi nghiệp vụ qua `PhoneStore.Business` + `PhoneStoreDbContext`, riêng màn hình Chi tiết sản phẩm có cờ cấu hình `CatalogApi` (`Enabled`, `BaseUrl`, `TimeoutSeconds` trong [`src/PhoneStore.Web/appsettings.json`](../../src/PhoneStore.Web/appsettings.json)):
  - Khi `CatalogApi:Enabled = true` (mặc định trong Docker Business), Web gọi `PhoneStore.Api` (`/api/v1/products/{id}` và `/related`) để đọc chi tiết sản phẩm; nếu API gặp lỗi mạng/timeout, hệ thống fallback an toàn sang truy vấn trực tiếp để không làm gián đoạn trải nghiệm mua sắm.
  - Khi `CatalogApi:Enabled = false` (mặc định Local Dev), Web đọc trực tiếp cùng SQL Server thông qua tầng Business.
- Xác thực bằng Cookie Auth (`PhoneStore.Auth`), giỏ hàng lưu Session gắn định danh chủ sở hữu và kiểm tra CSRF/idempotency khi đặt hàng.

### 3.2. REST API (`src/PhoneStore.Api`)
- Phục vụ cho Admin WinForms (`RestApi`), Customer Web (`CatalogApi`) và các API client chuẩn REST.
- Pipeline bảo vệ tích hợp trong [`src/PhoneStore.Api/Program.cs`](../../src/PhoneStore.Api/Program.cs):
  - `ApiExceptionHandler` + `ProblemDetails` kèm `traceId` (không rò rỉ stack trace).
  - `RequestSafetyMiddleware` kiểm soát kích thước request line (`4096`), query string (`2048`), JSON body (`64 KB`), image upload (tối đa cấu hình tới `20 MB`, nghiệp vụ ảnh tối đa `10 MB`) và `RequireHttps` khi bật.
  - `UseForwardedHeaders` giới hạn `ForwardLimit = 1` chỉ cho `TrustedProxies` / `TrustedProxyNetworks` đã cấu hình tường minh.
  - `RateLimiter` cố định theo IP (`Catalog: 60/phút`, `Health: 120/phút`, `Auth: 10/phút`, `Protected: 120/phút`) trả `HTTP 429` + `Retry-After`.
  - `RequestTimeouts` (`Catalog: 10s`, `Health: 3s`, `Auth: 5s`) trả `HTTP 504`.
  - Mọi endpoint có `[Authorize]` tự động gắn `Cache-Control: no-store`; endpoint không gắn `[AllowAnonymous]` mặc định yêu cầu xác thực (`fail-closed`).
  - Swagger UI chỉ kích hoạt khi `ASPNETCORE_ENVIRONMENT=Development`.

**Bản đồ nhóm endpoint hiện hành**:

| Nhóm | Route chính | Chính sách truy cập |
|---|---|---|
| **Health** | `GET /api/health`, `/api/health/live`, `/api/health/ready` | `AllowAnonymous` |
| **Public Catalog** | `GET /api/v1/products`, `/bestsellers`, `/newest`, `/{id}`, `/{id}/related`; `GET /api/v1/brands`; `GET /api/v1/categories` | `AllowAnonymous` |
| **Authentication** | `POST /api/v1/auth/login`, `/admin/login`, `/register`, `/refresh` (`AllowAnonymous`); `POST /logout`, `GET /me` (`Api.Authenticated`) | `LocalJwt` (chi tiết [`AUTHENTICATION_AND_JWT.md`](AUTHENTICATION_AND_JWT.md)) |
| **Customer Profile** | `GET`, `PUT /api/v1/profile` | `Api.Customer` |
| **Customer Orders** | `GET`, `POST /api/v1/orders` (alias `/api/orders`), `GET /{id}`, `POST /{id}/cancel` | `Api.Customer` + scope `orders:read` / `orders:write` |
| **Admin Dashboard & Reports** | `GET /api/v1/admin/dashboard`, `/api/v1/admin/reports/{overview,revenue,top-products}` (alias `/api/admin/...`) | `Api.Admin` |
| **Admin Products & Images** | `GET/POST /api/v1/admin/products`, `GET /options`, `GET/PUT /{id}`, `PATCH /{id}/status`, `GET/POST /{id}/images`, `DELETE /{id}/images/{imageId}`, `PATCH .../primary`, `PATCH .../order` | `Api.Admin` |
| **Admin Brands & Categories** | `GET/POST /api/v1/admin/{brands,categories}`, `PUT /{id}`, `PATCH /{id}/status` | `Api.Admin` |
| **Admin Customers & Orders** | `GET/PUT /api/v1/admin/customers`, `PATCH /{id}/status`; `GET /api/v1/admin/orders`, `GET /{id}`, `PATCH /{id}/status` | `Api.Admin` |

### 3.3. Admin Desktop (`src/PhoneStore.Admin`)
- Giao diện quản trị WinForms (`A01–A10`: Login, Dashboard, Products, Product Add/Edit, Brands, Categories, Customers, Orders, Order Detail, Reports).
- Hỗ trợ **Dual Mode** qua `AdminDataAccess:Mode` trong [`src/PhoneStore.Admin/appsettings.json`](../../src/PhoneStore.Admin/appsettings.json) và [`src/PhoneStore.Admin/AdminServices.cs`](../../src/PhoneStore.Admin/AdminServices.cs):
  - **`RestApi` (mặc định)**: Kết nối `AdminDataAccess:ApiBaseUrl` (mặc định `http://127.0.0.1:5025`, timeout `15s`). Trong chế độ này, `AdminServices` **cố ý không đăng ký `PhoneStoreDbContext`** để ngăn hoàn toàn việc fallback ngầm sang SQL trực tiếp. Phiên token giữ trong bộ nhớ RAM (`AdminApiSessionStore`) với làm mới chủ động và thu hồi token khi đăng xuất.
  - **`DirectSql` (dự phòng)**: Truy cập trực tiếp SQL Server qua `ConnectionStrings:PhoneStoreConnection` và thư mục ảnh `ProductImages:UploadsDirectory`. Chỉ kích hoạt khi cấu hình tường minh `$env:AdminDataAccess__Mode = "DirectSql"`.

## 4. Kiến trúc chia sẻ ảnh sản phẩm (Shared Product Uploads)

Ảnh sản phẩm tải lên từ Admin (qua REST API) cần được Customer Web phục vụ tức thì tại `/uploads/products/{fileName}`:
- **Kiểm tra nội dung ảnh**: Chỉ chấp nhận định dạng ảnh **JPEG/PNG** hợp lệ (kiểm tra chữ ký/decode bằng `CrossPlatformProductImageContentValidator` trong API Linux/Windows và `WindowsProductImageContentValidator` trong `DirectSql`), kích thước tối đa `10 MB`, lưu bằng tên GUID an toàn để tránh path traversal.
- **Môi trường Local Development**: [`src/PhoneStore.Api/appsettings.Development.json`](../../src/PhoneStore.Api/appsettings.Development.json) cấu hình đường dẫn tương đối `ProductImages:UploadsDirectory = "../PhoneStore.Web/wwwroot/uploads/products"`. [`src/PhoneStore.Api/Program.cs`](../../src/PhoneStore.Api/Program.cs) resolve đường dẫn theo `IWebHostEnvironment.ContentRootPath` trong lazy factory của `IProductImageStorage`, nên ảnh upload qua Local API ghi trực tiếp vào thư mục tĩnh của Local Customer Web.
- **Môi trường Docker Compose**: Cả [`docker-compose.yml`](../../docker-compose.yml) và [`scripts/cd/docker-compose.business.yml`](../../scripts/cd/docker-compose.business.yml) gắn chung thư mục host (`${PRODUCT_UPLOADS_PATH}` hoặc `./src/PhoneStore.Web/wwwroot/uploads/products`) vào container `api` với quyền **`rw`** (`ProductImages__UploadsDirectory=/app/wwwroot/uploads/products`) và container `web` với quyền **`ro`** (chỉ đọc để giảm bề mặt rủi ro).

## 5. Mô hình triển khai

Hệ thống có 3 chế độ vận hành rõ ràng (xem chi tiết hướng dẫn chạy tại [`LOCAL_DEVELOPMENT.md`](../LOCAL_DEVELOPMENT.md) và [`DOCKER_AND_CI_GUIDE.md`](../DOCKER_AND_CI_GUIDE.md)):
1. **Docker Business (`phonestore-restore-c2`)**: `3` container (`db` trên `127.0.0.1:14334`, `web` trên `127.0.0.1:5015`, `api` chỉ mở nội bộ `8080/tcp`), cấu hình `Production` + `LocalJwt`, dùng volume bền vững `phonestore-restore-c2_mssql_data`.
2. **Local Development (`dotnet run`)**: Local API (`127.0.0.1:5025`), Local Customer Web (`127.0.0.1:5016`) và Admin WinForms kết nối cùng Docker SQL `127.0.0.1,14334` thông qua `.NET User Secrets`.
3. **Swagger Development cách ly (`phonestore-dev-swagger`)**: Stack riêng cổng `127.0.0.1:5026` dùng database tách biệt `PhoneStoreDB_SwaggerDev` (xem [`archive/s35/S35_6A_SWAGGER_DEVELOPMENT_5026.md`](../archive/s35/S35_6A_SWAGGER_DEVELOPMENT_5026.md)).
