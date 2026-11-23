# PhoneStore — Hướng dẫn kiểm thử & xác minh

> Tài liệu tổng hợp các quy trình kiểm thử an toàn có thể chạy lại trên PhoneStore.
> Để xem **kết quả chạy gần nhất có bằng chứng**, xem [`docs/PROGRESS.md`](../PROGRESS.md#3-kết-quả-kiểm-thử-gần-nhất-có-bằng-chứng).
> Các báo cáo kiểm thử lịch sử từng giai đoạn nằm trong [`docs/archive/`](../archive/) (gồm mốc kiểm thử ban đầu [`archive/s17/S17_TESTING.md`](../archive/s17/S17_TESTING.md) và [`archive/PROGRESS_HISTORY.md`](../archive/PROGRESS_HISTORY.md)).

## 1. Phân loại các tầng kiểm thử

| Tầng kiểm thử | Phạm vi & hạ tầng | Chạm `PhoneStoreDB` chính? | Khi nào chạy |
|---|---|---|---|
| **1. Build & Unit/Integration Tests** (`PhoneStore.Tests`) | `xUnit`, SQLite in-memory và `WebApplicationFactory` (`TestServer`) | **Không** (cách ly 100%) | Sau mỗi thay đổi code |
| **2. Compose & CD Validation Scripts** | Validator Python + readiness/rehearsal PowerShell | **Không** (chỉ đọc/mock) | Khi sửa Docker Compose, Dockerfile, CI/CD |
| **3. Swagger Dev Stack cách ly (`5026`)** | Stack `phonestore-dev-swagger` với DB riêng `PhoneStoreDB_SwaggerDev` | **Không** (DB & secret riêng) | Khi kiểm tra thủ công hợp đồng REST API / Swagger UI |
| **4. Live Runtime Smoke (Read-only)** | Kiểm tra `GET /api/health/ready`, catalog, ảnh, parity Local ↔ Docker | Chỉ **đọc** (`GET` / `SELECT`) | Khi đổi cấu hình kết nối hoặc sau khi triển khai |
| **5. SQL Integration Opt-in** (`PhoneStore.SqlIntegration`) | Kiểm thử đồng thời/lock trên SQL Server riêng (`PhoneStoreDB_StockConcurrencyTest`) | **Không** (cấm chạm DB chính; **yêu cầu phê duyệt tường minh**) | Chỉ khi kiểm tra đặc thù concurrency SQL Server |

## 2. Chạy bộ kiểm thử tự động chuẩn (`PhoneStore.Tests`)

Đây là bộ kiểm thử mặc định an toàn, không yêu cầu SQL Server sống và không sửa dữ liệu thật:

```powershell
dotnet restore PhoneStore.sln
dotnet build PhoneStore.sln -c Release --no-restore
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj -c Release --no-build
```

Khi đang phát triển lặp nhanh, có thể lọc theo nhóm bài kiểm thử (`--filter`):

```powershell
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --filter "FullyQualifiedName~LocalJwtAuthenticationTests"
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --filter "FullyQualifiedName~AdminRestApiIntegrationTests"
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --filter "FullyQualifiedName~SharedProductUploadsIntegrationTests"
```

Các nhóm bài kiểm thử chính hiện có trong `tests/PhoneStore.Tests`:
- Xác thực & bảo mật: `LocalJwtAuthenticationTests`, `AdminRestApiIntegrationTests`, chính sách Single Admin, khóa tài khoản, rate limit & request safety.
- Nghiệp vụ mua hàng & quản trị: Catalog, Cart/Checkout, hủy đơn & hoàn tồn, báo cáo doanh thu UTC+7 (`Completed` only), quản lý sản phẩm/thương hiệu/danh mục/khách hàng/đơn hàng.
- Ảnh sản phẩm & tích hợp chia sẻ: `SharedProductUploadsIntegrationTests` (kiểm tra đường dẫn tương đối `ProductImages:UploadsDirectory` và luồng upload → đọc ảnh).

## 3. Kiểm tra cấu hình Docker Compose và sẵn sàng CD

Chạy các script kiểm tra tĩnh/rehearsal khi có thay đổi liên quan đến Docker, Compose hoặc pipeline triển khai:

```powershell
# 1. Xác thực schema, healthcheck và shared uploads (api rw / web ro) trên cả local & business compose
python scripts/ci/validate_business_compose.py

# 2. Kiểm tra sẵn sàng các script Windows CD (parser + cấu hình, không deploy thật)
pwsh -NoProfile -File scripts/cd/Test-BusinessCdReadiness.ps1

# 3. Chạy bộ diễn tập (rehearsal) logic triển khai/rollback cách ly
pwsh -NoProfile -File scripts/cd/Test-Rehearsal.ps1
```

Các lệnh trên không đụng đến container đang chạy (`phonestore-restore-c2`) và không thay đổi dữ liệu database.

## 4. Kiểm thử thủ công REST API với Swagger cách ly (`5026`)

Khi cần thử nghiệm trực tiếp các endpoint REST API (gồm cả `POST`/`PUT`/`PATCH`/`DELETE`) mà không ảnh hưởng đến `PhoneStoreDB` chính:

```powershell
# Khởi động stack Swagger cách ly trên http://127.0.0.1:5026/swagger
pwsh -NoProfile -File scripts/dev/Start-PhoneStoreSwagger.ps1

# Dừng stack Swagger sau khi hoàn tất
pwsh -NoProfile -File scripts/dev/Stop-PhoneStoreSwagger.ps1
```

Stack này sử dụng project riêng `phonestore-dev-swagger`, database riêng `PhoneStoreDB_SwaggerDev` và secret riêng trong `%LOCALAPPDATA%\PhoneStore-DevSwagger` (xem chi tiết tại [`LOCAL_DEVELOPMENT.md`](../LOCAL_DEVELOPMENT.md) và [`archive/s35/S35_6A_SWAGGER_DEVELOPMENT_5026.md`](../archive/s35/S35_6A_SWAGGER_DEVELOPMENT_5026.md)).

## 5. Kiểm tra Live Runtime chỉ đọc (Read-only Smoke)

Sau khi thay đổi cấu hình kết nối hoặc cập nhật runtime, thực hiện các kiểm tra chỉ đọc:
1. Kiểm tra trạng thái container Docker Business: `docker compose -p phonestore-restore-c2 -f scripts/cd/docker-compose.business.yml ps` (`3/3 healthy`).
2. Kiểm tra Customer Web Docker (`http://127.0.0.1:5015`) và nếu đang chạy Local Dev thì kiểm tra Local API (`http://127.0.0.1:5025/api/health/ready`, `GET /api/v1/products`) và Local Web (`http://127.0.0.1:5016`).
3. Xác nhận ảnh sản phẩm trả `HTTP 200` và số lượng sản phẩm/danh mục/đơn hàng khớp giữa Local và Docker.

## 6. Kiểm thử tích hợp SQL Server Opt-in (`PhoneStore.SqlIntegration`)

Project [`tests/PhoneStore.SqlIntegration/`](../../tests/PhoneStore.SqlIntegration/README.md) được tách khỏi `PhoneStore.sln` để kiểm thử các kịch bản lock/concurrency đặc thù trên SQL Server thực:
- **Bắt buộc chỉ nhắm tới database cô lập `localhost/PhoneStoreDB_StockConcurrencyTest`** (có guard kiểm tra `DB_NAME()` trước mỗi lệnh EF).
- **Tuyệt đối không chạy các mode ghi (`--execute-approved-*`, `--reset-test-admin-approved-*`) khi chưa có phê duyệt tường minh của người dùng**.
- Đọc đầy đủ điều kiện tiên quyết và quy ước dọn fixture tại [`tests/PhoneStore.SqlIntegration/README.md`](../../tests/PhoneStore.SqlIntegration/README.md) trước khi sử dụng.

## 7. Quy tắc báo cáo kết quả kiểm thử

- Luôn ghi rõ **lệnh thực tế đã chạy**, **số lượng `Passed / Failed / Skipped`**, **ngày xác minh** và **phạm vi môi trường** (SQLite/TestServer, Docker, hay Local WinForms).
- Phân biệt rành mạch `VERIFIED`, `PARTIAL`, `NOT TESTED` và `UNKNOWN`; không suy diễn bài test tự động thành nghiệm thu GUI thủ công hoặc kiểm thử production.
- Cập nhật tóm tắt kết quả mới nhất vào [`docs/PROGRESS.md`](../PROGRESS.md) sau mỗi mốc hoàn thành.
