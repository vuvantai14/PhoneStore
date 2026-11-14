# S31.3G — PhoneStore REST API Final Acceptance

Ngày xác minh: 2026-11-14  
Kết luận: **PASS theo bằng chứng tích lũy S31.3; live recheck S31.3G bị giới hạn TLS của môi trường Codex**

## A. Executive Summary

S31.3 hoàn tất phần mở rộng REST API đọc cho catalog PhoneStore: API .NET 8 độc lập, năm endpoint công khai, hardening vận hành, tích hợp Product Detail của Customer Web qua feature flag và fallback có kiểm soát. Không có write API, migration, seed hay thay đổi schema/database.

Bằng chứng kết luận gồm source hiện tại, full regression **232/232 PASS**, build cách ly **0 warning / 0 error**, live SQL/API/Web đã PASS ở S31.3E–F và xác nhận Admin thủ công của người dùng. Lần chạy lại API trong S31.3G khởi động/Swagger thành công nhưng SQL bị chặn trước truy vấn bởi lỗi TLS riêng của token Codex; vì vậy không được tính là một live catalog PASS mới và không phủ định live evidence trước đó.

## B. Architecture

```text
Customer Web Product Detail
  ├─ CatalogApi.Enabled = false → Business → Data/EF Core → SQL Server
  └─ CatalogApi.Enabled = true  → typed HttpClient → PhoneStore.Api
                                  → Business → Data/EF Core → SQL Server

Admin WinForms → Business → Data/EF Core → SQL Server
```

- `PhoneStore.Api` là ASP.NET Core .NET 8 process riêng, không phải microservice deployment đã hoàn thiện.
- Controller trả DTO contract, không serialize EF entity.
- `DbContext`, catalog reader và product service dùng scoped lifetime; không phát hiện captive DbContext.
- Web dùng `AddHttpClient`; không tạo `HttpClient` mới theo request.
- Admin không gọi REST API và không bị thay đổi bởi S31.3.
- Đây là layered modular monolith có thêm HTTP boundary đọc, không được mô tả là Clean Architecture hoặc hệ microservices.

## C. API Reference

| Method | Endpoint | Nội dung |
|---|---|---|
| GET | `/api/health` | Readiness của SQL Server |
| GET | `/api/v1/products` | Catalog active, phân trang/tìm kiếm/lọc/sắp xếp |
| GET | `/api/v1/products/{id}` | Chi tiết sản phẩm active |
| GET | `/api/v1/brands` | Thương hiệu active |
| GET | `/api/v1/categories` | Danh mục active |

Products hỗ trợ các query đã triển khai: `pageNumber`, `pageSize`, `search`, `brandId`, `categoryId`, `minPrice`, `maxPrice`, `sort`. Validation giới hạn page size và các giá trị số; truy vấn hợp lệ trả paging metadata cùng DTO độc lập.

Product detail gồm định danh, tên, brand/category, giá, tồn, mô tả, thông số và danh sách ảnh. Image URL là relative URL an toàn để Web phục vụ từ cùng origin; response không xuất entity navigation, mật khẩu hoặc trường quản trị nội bộ.

Status contract đã được automated/live evidence bao phủ:

- `200`: request hợp lệ.
- `400`: validation/request contract sai.
- `404`: product không tồn tại hoặc inactive.
- `429`: vượt fixed-window rate limit, có `Retry-After` khi khả dụng.
- `500`: lỗi không dự kiến, Problem Details không lộ exception nội bộ.
- `503`: database unavailable/health unhealthy.
- `504`: request timeout.

Problem Details kèm trace ID; Swagger Development mô tả đúng năm path.

## D. Security & Operations

- Catalog limit 60 request/phút/client; health 120/phút/client; queue bằng 0.
- Timeout catalog 10 giây, health 3 giây; cancellation token truyền xuống service/EF.
- Giới hạn query string/request line được validate fail-closed lúc startup.
- Correlation ID được kiểm tra; structured request log không ghi query/body/secret.
- Central exception handler che chi tiết nội bộ.
- Swagger chỉ bật trong Development; CORS mặc định không bật.
- Không thêm JWT vì V1 chỉ là public read-only catalog.
- Không tin `X-Forwarded-For` khi chưa cấu hình trusted proxy.

Cấu hình cần thiết: `PhoneStoreConnection`, `ApiOperational`, và phía Web là `CatalogApi.Enabled`, `CatalogApi.BaseUrl`, `CatalogApi.TimeoutSeconds`. Không đưa credential vào tài liệu.

## E. Customer Web Integration

- Chỉ Product Detail dùng API khi flag ON; Home/Product List/Cart/Checkout/Orders giữ đường hiện có.
- OFF: resolver gọi local `IProductService`, không gửi HTTP.
- ON + 200: typed client map đầy đủ DTO sang model trình bày hiện có.
- 404: Web trả 404, không fallback.
- 400: Web trả 502 theo policy contract hiện tại.
- 429/5xx/network/timeout/invalid payload: fallback local đúng một lần và ghi warning an toàn.
- Caller cancellation: propagate, không fallback.
- Razor/CSS/JavaScript không bị thay đổi bởi integration.

Live S31.3F đã chứng minh OFF không tạo API call; ON tạo thật `GET /api/v1/products/1`; API unavailable vẫn trả detail bằng local fallback; missing product vẫn 404; HTML/ảnh/giá/tồn/specification giữ parity.

## F. Testing Evidence

### S31.3G hiện tại

| Kiểm tra | Kết quả |
|---|---|
| Build mặc định | Không hoàn tất vì Admin PID 3604 khóa DLL trong `bin`; không dừng process người dùng |
| Build solution ra `scratch/s31_3g-build` | **PASS — 0 warning, 0 error** |
| Full `PhoneStore.Tests` | **232/232 PASS**, 0 fail, 0 skipped |
| Test isolation audit | SQLite/doubles/TestServer; không có test ghi PhoneStoreDB |
| Swagger Development runtime | 200; OpenAPI 3.0.1; 5 paths |
| Validation `pageSize=51` | 400 |
| Live SQL catalog recheck | **BLOCKED** trước SELECT bởi TLS trong môi trường Codex |
| Web OFF/ON/fallback recheck | Không chạy lại vì API prerequisite không đạt; dùng live evidence S31.3F và ghi rõ nguồn |

### Bằng chứng runtime đã có

- S31.3E: API 5 endpoint, SQL thực, Swagger, Web/API concurrent và data parity PASS; Admin UI sau đó được người dùng xác nhận thủ công FULL PASS.
- S31.3F: feature OFF/ON, HTTP call thật, 404, fallback, Home/List/image và SQL snapshot PASS.
- Automated hardening: 400/404/429/500/503/504, timeout, cancellation, logging, Swagger Production 404 và forwarded-IP policy.

Admin manual evidence được ghi đúng nguồn: người dùng xác nhận; S31.3G không tự động kiểm thử lại UI Admin.

## G. Known Limitations

- Không có write API; không có API auth/JWT vì catalog V1 là public read-only.
- Rate limiter in-memory theo từng process, không chia sẻ trạng thái giữa nhiều instance.
- Trusted proxy chưa cấu hình; cần cấu hình hạ tầng trước khi dùng forwarded headers.
- SQL principal đang dùng chung với ứng dụng, chưa tách account read-only least privilege cho API.
- Cancellation xuống EF không bảo đảm SQL Server dừng execution ngay tức thì.
- Chưa deploy production; chưa có load/stress test hoặc multi-instance validation.
- Chỉ Product Detail dùng API khi flag ON; không được mô tả Home/List đã chuyển sang API.
- Web vẫn có đường local fallback đến cùng SQL Server, nên API outage không tách hoàn toàn Web khỏi data layer.
- Live recheck ngày 2026-11-14 từ process Codex gặp lỗi “SQL Server requires encryption but this machine does not support it”; không hạ `Encrypt`/`TrustServerCertificate` hay sửa server để né lỗi.

## H. Runbook

Điều kiện: SQL Server/`PhoneStoreDB` hoạt động, .NET 8 SDK/runtime, Windows Authentication phù hợp và cổng 5015/5025 trống.

```powershell
# API
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/PhoneStore.Api/PhoneStore.Api.csproj --launch-profile http

# Web (terminal khác)
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj --launch-profile http
```

- API: `http://localhost:5025`; Swagger Development: `/swagger`.
- Web: `http://localhost:5015`.
- Flag mặc định OFF trong `src/PhoneStore.Web/appsettings.json`.
- Override theo environment: `CatalogApi__Enabled=true`, `CatalogApi__BaseUrl=http://localhost:5025/`, `CatalogApi__TimeoutSeconds=3`.
- Xác minh fallback: bật flag, xác minh detail qua API; dừng **đúng API test**, tải lại một product tồn tại và kiểm warning/fallback. Không dừng SQL Server hay process không thuộc test.
- Port conflict: dùng `Get-NetTCPConnection`/process path để xác minh owner trước khi xử lý.
- Connection failure: kiểm database identity, Windows identity, TLS/certificate và config hiệu lực; không tự hạ bảo mật hoặc log connection string.

## I. Final Acceptance

| Tiêu chí | Trạng thái |
|---|---|
| Architecture/contracts/source | PASS |
| Build | PASS bằng output cách ly |
| Full automated regression | PASS — 232/232 |
| API security behavior | PASS automated + runtime lịch sử |
| Live API/SQL | PASS S31.3E/F; S31.3G recheck BLOCKED bởi TLS Codex |
| Web feature OFF/ON + fallback | PASS live S31.3F |
| Admin unaffected | PASS source/build; manual runtime do người dùng xác nhận |
| Database safety | PASS — snapshot trước/sau không đổi |
| Documentation | PASS |

**Verdict S31.3: PASS theo bằng chứng tích lũy, không có blocker sản phẩm đã chứng minh.** Điều kiện trung thực: lần live recheck riêng trong S31.3G không tái tạo được catalog 200 do môi trường TLS Codex; không được gọi phần đó là PASS mới.

## J. Git Status

- Preflight: branch `main`, HEAD `018633cce5f3c99d4b39a5e17d2d61d2f8b7aac1`, working tree sạch.
- S31.3G chỉ thêm/cập nhật tài liệu; không sửa application source, config hay database.
- Không commit/push.
- Commit đề xuất: `feat(api): finalize PhoneStore REST API integration and acceptance`.

## Database Snapshot & Cleanup

Snapshot trước và sau S31.3G giống nhau:

| Roles | Users | Brands | Categories | Products | ProductImages | Orders | OrderDetails |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 3 | 8 | 2 | 40 | 41 | 11 | 26 |

Migration history giữ nguyên `20261001150857_InitialCreate` và `20261102122856_EnforceSingleAdmin`, EF Core 8.0.28. Không có DML/DDL/migration/seed.

API process tạm của task đã được cleanup; do launcher tách child process và non-force shutdown thất bại, chỉ PID task đã xác minh 21652 mới bị force-stop. Cổng 5015/5025 sau cleanup đều trống. Admin người dùng PID 3604 không bị tác động.
