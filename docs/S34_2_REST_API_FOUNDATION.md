# S34.2 — REST API Foundation

Ngày 19/11/2026. **PASS ở source và automated tests.** Docker đang chạy bản trước S34.2.

## A. Baseline

`main` ở checkpoint S32 `b0d07a73119cba4e09367e3f17bcee1ae4df0594`, khớp ref `origin/main` cục bộ. S34.1 đã xác minh hash này trực tiếp trên GitHub. Trước khi sửa, working tree chỉ có báo cáo S34.1 untracked và mục tiến độ S34.1. Cả hai được bảo toàn. Docker business và acceptance có 5/5 container healthy. Hai API trên 5025 và 5286 trả HTTP 200 cho `/api/health`.

## B. Existing API Contracts

| Hợp đồng | Response hiện có |
| --- | --- |
| `GET /api/v1/products` | `PagedProductsResponse` có `items`, `page`, `pageSize`, `totalItems`, `totalPages`. Status 200 cho kết quả rỗng, 400 cho query sai. |
| `GET /api/v1/products/{id}` | `ProductDetailResponse` gồm thông số, giá, tồn kho, ảnh tương đối. Status 200, 400 cho ID sai và 404 cho sản phẩm thiếu hoặc inactive. |
| `GET /api/v1/brands` | Danh sách `CatalogOptionResponse` với `id` và `name`, status 200. |
| `GET /api/v1/categories` | Danh sách `CatalogOptionResponse` với `id` và `name`, status 200. |

## C. Implementation Summary

Thêm liveness và readiness trong `HealthController`. Readiness dùng chung action với health cũ. Catalog DTO, mapping, routing, truy vấn và validation hiện có được giữ nguyên. Regression tests bổ sung kiểm tra method 405, tập trường JSON và kiểu giá trị, query malformed/overflow, bộ lọc kết hợp, biên giá, trang 2, kết quả rỗng, 404 và OpenAPI.

## D. Files Changed

- [HealthController.cs](../src/PhoneStore.Api/Controllers/HealthController.cs)
- [ApiFoundationTests.cs](../tests/PhoneStore.Tests/ApiFoundationTests.cs)
- [CatalogApiTests.cs](../tests/PhoneStore.Tests/CatalogApiTests.cs)
- Báo cáo này và [PROGRESS.md](PROGRESS.md).

## E. Error Handling

Centralized exception handler và request middleware hiện có tiếp tục phục vụ API. Validation v1 vẫn dùng `application/problem+json` và object `errors` cho lỗi model binding hoặc validation. Handler hiện map lỗi database thành 503 và lỗi bất ngờ thành 500, dùng nội dung chung và `traceId`. Test HTTP 503 đối chiếu `traceId` với `X-Correlation-ID` và kiểm tra thông tin hạ tầng được lược bỏ. Test 500 hiện có kiểm tra an toàn nội dung lỗi. Rate limiting và timeout hiện có giữ nguyên.

## F. Health Checks

| Endpoint | Ý nghĩa | Response |
| --- | --- | --- |
| `GET /api/health` | Contract legacy, kiểm tra kết nối SQL qua probe hiện có | 200 `{"status":"healthy"}` hoặc 503 ProblemDetails có `health: "unhealthy"` |
| `GET /api/health/live` | Xác nhận process phục vụ được request, không gọi probe SQL | 200 `{"status":"healthy"}` khi request đến action |
| `GET /api/health/ready` | Kiểm tra phụ thuộc hiện tại của API là kết nối SQL | Cùng 200/503 như legacy, với `instance` tương ứng route |

Ba route dùng chung chính sách health rate limiting và timeout hiện có. Readiness xác nhận khả năng kết nối, phạm vi chưa bao gồm kiểm tra schema. Docker Compose business và acceptance vẫn probe `/api/health`.

## G. Automated Test Results

| Lệnh / phạm vi | Kết quả |
| --- | --- |
| `dotnet build PhoneStore.sln --no-restore -m:1 --verbosity minimal` | PASS, 0 warning, 0 error |
| `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-build --no-restore -m:1 --filter "FullyQualifiedName~ApiFoundationTests\|FullyQualifiedName~CatalogApiTests\|FullyQualifiedName~ApiSecurityHardeningTests\|FullyQualifiedName~CustomerWebCatalogApiIntegrationTests" --verbosity minimal` | **65/65 PASS** |
| `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-build --no-restore -m:1 --verbosity minimal` | **263/263 PASS**, 0 skipped |
| `git diff --check` | PASS |

Các test HTTP chạy qua TestServer với service/probe giả lập. Các ca cần dữ liệu dùng SQLite cách ly. SQL Server business/acceptance nằm ngoài các fixture của đợt test này.

## H. Compatibility Verification

HTTP contract và typed client Customer Web PASS qua automated tests. Catalog source và DTO không có diff. Hậu kiểm Docker xác nhận 5/5 container healthy và khớp ID, image ID, StartedAt trước/sau. Hai API đang chạy vẫn trả HTTP 200 trên health cũ. Hai route mới chỉ được nghiệm thu ở TestServer vì chưa triển khai Docker.

## I. Remaining Risks

Backlog từ S34.1 gồm .NET còn hỗ trợ, bearer/OIDC khi cần endpoint protected, CORS theo client, cấu hình rate limiting sau proxy, SQL least privilege, kiểm chứng TLS và cấu hình môi trường Production. Các thay đổi này cần phạm vi riêng.

## J. Suggested Commit Message

`feat(api): add health probes and lock catalog v1 contracts`

Dừng sau S34.2. Chưa stage, commit, push hoặc deploy.
