# S31.3F — Customer Web REST API Integration

Ngày xác minh: 2026-11-13  
Trạng thái: **PASS — GO cho S31.3G**

## A. Preflight & Git State

- Branch `main`, HEAD `9d189045b079489d1b9dbe0289c8df8396076478`.
- Bảo toàn `docs/PROGRESS.md` và báo cáo S31.3E chưa commit.
- Người dùng xác nhận S31.3E đã FULL PASS thủ công gồm Admin WinForms; đây là điều kiện bổ sung để bắt đầu task. Báo cáo runtime S31.3E gốc vẫn giữ nguyên bằng chứng Codex đã quan sát.
- Chỉ tích hợp `Customer Web -> Product Detail`; không đổi Home, Product List, Cart, Checkout, Orders, authentication, Admin, UI/CSS/JS, Business/Data/Models hay API contract.
- Không commit/push.

## B. Files Modified

- `src/PhoneStore.Web/CatalogApi/CatalogApiOptions.cs`
- `src/PhoneStore.Web/CatalogApi/CatalogApiProductResponse.cs`
- `src/PhoneStore.Web/CatalogApi/CatalogApiResult.cs`
- `src/PhoneStore.Web/CatalogApi/CatalogApiClient.cs`
- `src/PhoneStore.Web/CatalogApi/ProductDetailResolver.cs`
- `src/PhoneStore.Web/Controllers/ProductsController.cs`
- `src/PhoneStore.Web/Program.cs`
- `src/PhoneStore.Web/appsettings.json`
- `tests/PhoneStore.Tests/CustomerWebCatalogApiIntegrationTests.cs`
- tài liệu này và `docs/PROGRESS.md`.

## C. Feature Flag & DI

Section mặc định:

```json
"CatalogApi": {
  "Enabled": false,
  "BaseUrl": "http://localhost:5025/",
  "TimeoutSeconds": 3
}
```

- OFF: resolver gọi trực tiếp `IProductService`; không phát sinh HTTP request.
- ON: chỉ action Product Detail gọi API.
- Options hỗ trợ environment override; BaseUrl phải là absolute HTTP(S), timeout 1–30 giây và được validate lúc startup.
- Typed client đăng ký qua `AddHttpClient`; không thay lifetime DbContext/service hiện có, không CORS/JWT.

## D. Typed HttpClient

- `ICatalogApiClient.GetProductAsync` gọi `GET api/v1/products/{id}` với `ResponseHeadersRead` và `CancellationToken`.
- `HttpClient.Timeout` theo config, mặc định 3 giây; không tạo `new HttpClient()` theo request.
- Phân loại rõ: 200 success; 400 bad contract; 404 not found; 429 rate-limited; 5xx unavailable; network/timeout unavailable; malformed/unsafe payload invalid.
- Không retry, không đọc/log response body, không ghi secrets.

## E. DTO/ViewModel Mapping

- DTO Web độc lập với DTO API assembly; map đầy đủ ID, name, brand/category IDs/names, price, stock, description, RAM/storage/chip/screen/camera/battery/OS và images.
- Kiểm tra payload: đúng requested ID, trường bắt buộc, giá/stock không âm và relative image URL an toàn.
- Map sang `PhoneStore.Models.Catalog.ProductDetail`, nên Razor/ViewModel hiện có không đổi.
- Related products vẫn dùng `IProductService.GetRelatedAsync` với product đã map; giữ nguyên UI/hành vi.
- Relative `/uploads/...` được Razor phục vụ từ Customer Web origin, không phải API origin.

## F. Fallback Behavior

| Trạng thái | Hành vi Web |
|---|---|
| Flag OFF | Local Business/Data, không gọi API |
| API 200 | Dùng dữ liệu API |
| API 404 | Web 404, không fallback |
| API 400 | 502, không che lỗi contract bằng fallback |
| API 429 | Không retry; fallback local một lần để giữ khả dụng read-only |
| API 500/503/504 | Fallback local một lần |
| Network/timeout | Fallback local một lần |
| Invalid payload | Không render payload; fallback local một lần |
| Client cancellation | Propagate cancellation, không fallback |

Fallback được log một warning có status/ProductId; không log body, secret hay exception message.

## G. Automated Test Results

| Kiểm tra | Kết quả |
|---|---|
| `dotnet restore PhoneStore.sln` | PASS |
| `dotnet build PhoneStore.sln --no-restore` | PASS — 0 warnings, 0 errors |
| Customer Web API integration + API/Web regression | **53/53 PASS**, 0 fail, 0 skipped |

Tests bao phủ flag OFF/ON, 200 mapping, 404 no-fallback, 400 contract failure, 429 policy, 500/503/504 fallback, network failure, timeout, caller cancellation, invalid JSON/ID/external image, relative image, API foundation/catalog/security và Home presentation.

## H. Live Web/API Runtime

- Feature OFF: Product 1 HTTP 200, Home/List 200; API log có **0** request detail từ Web.
- Feature ON: Product 1 HTTP 200; API log xác nhận Web gọi thật `/api/v1/products/1` và API trả 200.
- Product 999999: Web 404 khi API trả 404; không fallback thành local 200.
- Sau khi dừng riêng API test, Product 1 qua Web vẫn 200 bằng local fallback; log có đúng fallback warning.
- Home/Product List vẫn 200; ảnh Web `/uploads/products/iphone-12.jpg` 200.
- API/Web process do task tạo đã dừng không-force; 5015/5025 trống.

## I. UI Parity Verification

OFF và ON Product Detail ID 1 cùng HTTP 200 và cùng response length 21,528 bytes. ON giữ các marker:

- `iPhone 12`, `Apple`.
- Giá hiển thị `12.990.000`.
- Stock/specification hiện hữu.
- `/uploads/products/iphone-12.jpg`.
- Razor View, ViewModel, CSS và JavaScript không sửa.

Kết quả: UI/data parity **PASS** trong smoke HTML thực; không redesign.

## J. Database Safety

Snapshot trước/sau giống nhau:

| Roles | Users | Brands | Categories | Products | Images | Orders | Details |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 3 | 8 | 2 | 40 | 41 | 11 | 26 |

Migration history giữ nguyên hai migration EF Core 8.0.28. Không DML/DDL/migration/seed hoặc thay credentials.

## K. Remaining Risks

- Product Detail ON phụ thuộc API cho lần đọc chính nhưng có fallback local; kiến trúc chuyển tiếp vẫn giữ hai đường đọc đến cùng SQL Server.
- 429 được fallback để giữ trải nghiệm read-only; điều này giảm tải API nhưng Web vẫn truy vấn SQL local, nên không phải circuit breaker tổng thể.
- Invalid payload fallback có thể che outage contract khỏi người dùng nhưng có warning vận hành; không hiển thị payload sai.
- Chưa tích hợp API cho Home/List theo đúng phạm vi; không nên suy ra các trang đó đã chuyển sang API.

## L. GO / NO-GO for S31.3G

**GO.** Feature flag OFF/ON, API call thật, no-fallback 404, controlled fallback, cancellation, mapping/UI parity, build/tests và SQL safety đều PASS. Không tự chuyển sang S31.3G.
