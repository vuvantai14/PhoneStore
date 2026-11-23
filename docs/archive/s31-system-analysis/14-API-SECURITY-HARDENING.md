# S31.3D — PhoneStore REST API Security & Operational Hardening

Ngày xác minh: 2026-11-13  
Trạng thái: **PASS — GO cho S31.3E**

## A. Preflight & Git State

- Branch `main`, HEAD ban đầu `4f53a0d406cb15c18c7169676b3e171bee665337`.
- Bảo toàn toàn bộ diff S31.1–S31.3C đang có; không reset, commit hoặc push.
- Chỉ sửa `src/PhoneStore.Api/**`, API tests và tài liệu được cho phép. Không sửa Web/Admin/Business/Data/Models.

## B. Process Ownership & Port Cleanup

- PID cũ 5600 và 7308 được xác minh bằng executable path và start time là `PhoneStore.Api.exe` / `PhoneStore.Web.exe` trong workspace, do runtime S31.3C tạo.
- Ctrl+C trước đó không hiệu lực; task dừng đúng hai PID bằng `Stop-Process` không `-Force`, sau đó 5015/5025 trống.
- Runtime S31.3D tạo API PID 21332 và Web PID 3576; sau smoke đã dừng đúng hai PID, hậu kiểm `NO_LISTENERS_5015_5025`.

## C. Files Modified

- `src/PhoneStore.Api/Program.cs`
- `src/PhoneStore.Api/appsettings.json`
- `src/PhoneStore.Api/Controllers/HealthController.cs`
- `src/PhoneStore.Api/Controllers/ProductsController.cs`
- `src/PhoneStore.Api/Controllers/BrandsController.cs`
- `src/PhoneStore.Api/Controllers/CategoriesController.cs`
- `src/PhoneStore.Api/Infrastructure/ApiExceptionHandler.cs`
- `src/PhoneStore.Api/Infrastructure/ApiOperationalOptions.cs`
- `src/PhoneStore.Api/Infrastructure/ApiPolicies.cs`
- `src/PhoneStore.Api/Infrastructure/RequestSafetyMiddleware.cs`
- `tests/PhoneStore.Tests/ApiFoundationTests.cs`
- `tests/PhoneStore.Tests/ApiSecurityHardeningTests.cs`
- tài liệu này và `docs/PROGRESS.md`.

## D. Query & Request Limits

- Giữ validation S31.3C: `page >= 1`, `pageSize 1..50`, search tối đa 100, giá không âm, min/max hợp lệ, sort allowlist, ID dương.
- Kestrel `MaxRequestLineSize=4096`; middleware từ chối query string dài hơn 2048 bằng 400 Problem Details.
- Catalog luôn phân trang; không bổ sung collection filter không giới hạn.
- Các giới hạn nằm trong section `ApiOperational` và được kiểm tra range khi startup; cấu hình sai làm startup fail-closed.

## E. Rate Limiting

- Dùng ASP.NET Core fixed-window limiter, catalog 60 request/phút/client, queue 0; health có policy riêng 120/phút/client.
- Partition key lấy `RemoteIpAddress`; không đọc `X-Forwarded-For`, không bật Forwarded Headers khi chưa có trusted proxy.
- Vượt giới hạn trả 429 Problem Details và `Retry-After` khi limiter cung cấp metadata.
- Automated test dùng giới hạn riêng 1–2 request để kiểm chứng 429 có kiểm soát, không spam runtime SQL thật.

## F. Timeout & Cancellation

- Catalog timeout 10 giây; health timeout 3 giây, cấu hình qua `ApiOperational`.
- Timeout trả 504 Problem Details, không trả 200 rỗng.
- Controller → `IProductService` → `ProductCatalogReader` → EF async đều nhận và truyền `CancellationToken`; test slow service xác minh token bị cancel.
- Client disconnect test xác minh cancellation truyền xuống service và không có fake success.
- Giới hạn: automated test chứng minh token propagation tại boundary/service; không tuyên bố SQL Server đã hủy execution ở server. EF command timeout riêng vẫn theo provider (runtime log là 30 giây).

## G. Logging & Correlation ID

- Response có `X-Correlation-ID`; mặc định dùng `HttpContext.TraceIdentifier`.
- Client ID chỉ được chấp nhận nếu dài 1–64 và chỉ gồm chữ/số/`.`/`_`/`-`; input khác bị bỏ qua để ngăn log injection.
- Structured log ghi method, path, status, duration và trace ID; không ghi query string, request/response body, cookie, token hoặc connection string.
- Exception handler chỉ ghi exception type + trace ID; response không có stack trace/message nội bộ. Framework duplicate exception log tiếp tục bị suppress.

## H. Problem Details & Swagger/CORS

- Validation 400, missing product 404, rate limit 429, timeout 504, database exception 503 và unexpected exception 500 dùng safe Problem Details có trace ID.
- Health unavailable 503 cũng trả `application/problem+json`, không lộ SQL/server.
- Swagger UI/JSON chỉ map trong Development; Production exposure regression vẫn PASS.
- OpenAPI Development mô tả đúng 5 route hiện có.
- Không gọi `AddCors`/`UseCors`, không wildcard origin, không JWT và không Forwarded Headers.

## I. Automated Test Results

| Kiểm tra | Kết quả |
|---|---|
| `dotnet restore PhoneStore.sln` | PASS |
| `dotnet build PhoneStore.sln --no-restore` | PASS — 0 warnings, 0 errors |
| `ApiSecurityHardeningTests` | 8/8 PASS |
| `ApiFoundationTests` | 6/6 PASS |
| `CatalogApiTests` | 18/18 PASS |
| `HomePresentationTests` | 2/2 PASS |
| Lần chạy gộp cuối | 34/34 PASS |

Security tests bao phủ 429 + Retry-After, spoofed X-Forwarded-For, policy health độc lập, query limit 400, correlation validation, timeout 504, client cancellation và xác minh request log không chứa query/sensitive values.

## J. Live Runtime Verification

- API chạy thật tại `http://localhost:5025`, kết nối SQL Server `PhoneStoreDB` read-only.
- Health, products list, product 40, brands, categories đều 200; pageSize 51 trả 400; product 999999 trả 404.
- Mọi response runtime có correlation header; không có `Access-Control-Allow-Origin`.
- Swagger JSON 200 trong Development và đúng 5 paths.
- Log runtime xác minh SQL chỉ SELECT, request log không chứa query string/body và có status/duration/trace ID.
- Customer Web chạy đồng thời tại 5015: Home và Products 200. Admin không chạy và source không đổi.
- 429 được kiểm chứng bằng TestServer có policy giới hạn riêng, không tạo 61 request vào API live.

## K. Database Safety

Snapshot trước/sau runtime giống nhau:

| Roles | Users | Brands | Categories | Products | Images | Orders | Details | Active products | Active brands | Active categories |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 3 | 8 | 2 | 40 | 41 | 11 | 26 | 40 | 8 | 1 |

Không migration, seed, DML hoặc DDL. Tests dùng doubles/TestServer và dữ liệu cách ly.

## L. Remaining Risks

- Chưa có trusted reverse proxy; rate-limit key dựa trên peer IP. Khi triển khai sau proxy phải cấu hình KnownProxies/KnownNetworks trước khi dùng forwarded headers.
- Fixed-window in-memory là per-process; chưa chia sẻ quota khi scale nhiều instance.
- HTTP timeout truyền cancellation xuống EF nhưng không bảo đảm SQL Server ngừng ngay sau cancellation.
- API dùng Windows Authentication/account chung hiện có; least-privilege read-only SQL principal vẫn là hardening deployment tương lai.
- Không có CORS theo chủ đích; browser origin khác không gọi được cho tới khi exact origins được phê duyệt.

## M. GO / NO-GO for S31.3E

**GO.** Năm endpoint chạy thật, limits/rate limit/timeout/cancellation/logging/Problem Details/Swagger policy đều có automated evidence; runtime SQL read-only và Web concurrent PASS; database không đổi. Chưa tự chuyển sang S31.3E.
