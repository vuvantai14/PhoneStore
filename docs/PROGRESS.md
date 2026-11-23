# PhoneStore — Tiến độ hiện tại và điểm tiếp tục

> Đọc đầu mỗi phiên theo [`AGENTS.md`](../AGENTS.md). Khi tài liệu mâu thuẫn với mã nguồn hoặc kết quả kiểm thử thực tế, ưu tiên mã nguồn và bằng chứng kiểm thử.
> Toàn bộ lịch sử cũ (`S1`–`S35.6B`) được bảo toàn nguyên văn tại [`archive/PROGRESS_HISTORY.md`](archive/PROGRESS_HISTORY.md).

## 1. Tổng quan trạng thái & môi trường hiện hành

| Thành phần | Trạng thái / Cấu hình đã xác minh |
|---|---|
| **Nền tảng** | `.NET 10` (SDK `10.0.403` theo [`global.json`](../global.json)), EF Core `10.0.15`, `8` project trong `PhoneStore.sln` + `3` project phụ trợ CI |
| **Database nghiệp vụ chính** | **Docker SQL Server 2022** (`phonestore-restore-c2-db-1`, `PhoneStoreDB`, `3` migrations gồm `AddApiRefreshTokens`, `9` bảng, `40` sản phẩm, `41` ảnh DB / `42` file ảnh gốc, `16` đơn hàng) |
| **Windows SQL (`localhost`)** | Bản dữ liệu cũ lưu trữ (`12` đơn hàng) — giữ nguyên read-only archive, không ghi nghiệp vụ mới |
| **Docker Business (`phonestore-restore-c2`)** | `3/3` container (`db-1` `127.0.0.1:14334`, `web-1` `127.0.0.1:5015`, `api-1` nội bộ `8080/tcp`, `Production` + `LocalJwt` + shared uploads `api rw`/`web ro`) — triển khai từ GHCR Run #31 (`S35.5`) |
| **Local Development (`dotnet run`)** | Local API `http://127.0.0.1:5025` & Local Customer Web `http://127.0.0.1:5016` dùng chung Docker SQL `127.0.0.1,14334` qua `.NET User Secrets` (ngoài Git); Admin WinForms mặc định `RestApi -> http://127.0.0.1:5025` |
| **Shared Product Uploads** | Local API đọc tương đối `../PhoneStore.Web/wwwroot/uploads/products` (`appsettings.Development.json` + lazy DI); Docker bind-mount `${PRODUCT_UPLOADS_PATH}` (`api rw`, `web ro`) |

## 2. Các giai đoạn gần nhất đã hoàn thành

- **2026-11-23 — `S-DOCS.REORGANIZE.1` (`VERIFIED` tài liệu, chưa commit/push)**: Chuẩn hóa hệ thống tài liệu Markdown — cập nhật 4 tài liệu lõi (`CONTEXT.md`, `docs/PROGRESS.md`, `docs/LOCAL_DEVELOPMENT.md`, `docs/DOCKER_AND_CI_GUIDE.md`) và `tests/PhoneStore.SqlIntegration/README.md`, tách nguyên văn lịch sử cũ sang [`archive/PROGRESS_HISTORY.md`](archive/PROGRESS_HISTORY.md), bổ sung tài liệu kỹ thuật hiện hành trong [`architecture/`](architecture/) và [`testing/TESTING_GUIDE.md`](testing/TESTING_GUIDE.md), di chuyển `129` báo cáo lịch sử vào `docs/archive/{s17,s29,s31-system-analysis,s32,s34,s35}/` bằng `git mv` và xác minh liên kết (`287/288` nội bộ hợp lệ, `0` mới hỏng, `1` hỏng sẵn từ trước). Không sửa code/DB/Docker/CI/skill; chưa chạy lại build/test (chỉ thay đổi tài liệu).
- **2026-11-23 — `S-README.1`–`S-README.3` (`VERIFIED`, commit `3cbe9c4`)**: Tối giản [`README.md`](../README.md) (`133` dòng) cho GitHub và chuyển hướng dẫn thiết lập `.env` / `.NET User Secrets` chi tiết sang [`LOCAL_DEVELOPMENT.md`](LOCAL_DEVELOPMENT.md).
- **2026-11-23 — `S-DB.UNIFY.1A/1B` (`VERIFIED — GO`, commit `86ae753`)**: Thống nhất Local Development và Docker Business dùng chung Docker SQL `127.0.0.1,14334` / `PhoneStoreDB` qua `<UserSecretsId>` trên `PhoneStore.Web` & `PhoneStore.Api`. Đã xác minh trực tiếp qua `sys.dm_exec_sessions` (cả 4 tiến trình Docker Web/API + Local Web `5016` / API `5025` cùng kết nối Docker SQL), `9/9` tồn kho chênh lệch khớp 100%, `Category #2` active, `41/41` ảnh `HTTP 200`, không đổi dữ liệu SQL/Docker Business.
- **2026-11-23 — `S-INTEGRATION.FINAL / FINAL.1` (`VERIFIED — GO`, commit `9a57f8b`)**: Đồng bộ shared uploads Local Dev (`appsettings.Development.json` + lazy DI trong `Program.cs`) và `docker-compose.yml` (`api` `rw` + `ProductImages__UploadsDirectory`), mở rộng `scripts/ci/validate_business_compose.py` và bổ sung `SharedProductUploadsIntegrationTests.cs` (`+2` tests).
- **2026-11-23 — `S-JWT.1A–1F`, `S-CLEANUP.1–2`, `S-INTEGRATION.1A–1C` (`VERIFIED`, commit `757b4e2`)**: Cấu hình LocalJwt User Secrets, gộp biến JWT vào `.env`/`.env.example`, hợp nhất Compose business thành `scripts/cd/docker-compose.business.yml`, áp dụng migration `AddApiRefreshTokens` trên Windows SQL (sau backup `VERIFYONLY`), nghiệm thu live Admin WinForms `7/7` nhóm `HTTP 200` + Logout `204` (`RevokedReason=Logout`, `Active=0`) + Customer Web fallback.
- **2026-11-21 — Chuỗi `S35.4`–`S35.6B` (`VERIFIED`)**: Hoàn thiện REST API (LocalJwt, Refresh Token Rotation, CRUD Products/Images/Orders/Customers/Reports), triển khai thật Docker Business GHCR Run #31 ([`S35_5`](archive/s35/S35_5_REAL_DOCKER_BUSINESS_DEPLOYMENT.md)), stack Swagger cách ly `5026` ([`S35_6A`](archive/s35/S35_6A_SWAGGER_DEVELOPMENT_5026.md)) và chuyển mặc định Admin WinForms sang `RestApi` ([`S35_6B`](archive/s35/S35_6B_ADMIN_DEFAULT_RESTAPI.md)).

## 3. Kết quả kiểm thử gần nhất có bằng chứng

| Bộ kiểm tra | Mốc xác minh | Kết quả thực tế |
|---|---|---|
| `dotnet build PhoneStore.sln -c Release` | 2026-11-23 (`S-DB.UNIFY.1B`) | **PASS** — `8/8` projects, `0` warnings, `0` errors (`13.11s`) |
| `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj -c Release --no-build` | 2026-11-23 (`S-DB.UNIFY.1B`) | **PASS** — **`343/343` Passed**, `0` Failed, `0` Skipped (`9s`, SQLite/TestServer cách ly) |
| `python scripts/ci/validate_business_compose.py` | 2026-11-23 (`S-INTEGRATION.FINAL.1`) | **PASS** — `db, api, web` + shared uploads `rw`/`ro` |
| `scripts/cd/Test-BusinessCdReadiness.ps1` | 2026-11-23 (`S-INTEGRATION.FINAL.1`) | **PASS** — `6/6` (gồm parser `13/13` scripts) |
| `scripts/cd/Test-Rehearsal.ps1` | 2026-11-23 (`S-INTEGRATION.FINAL.1`) | **PASS** — `16/16` |
| Local ↔ Docker SQL parity (read-only) | 2026-11-23 (`S-DB.UNIFY.1B`) | **PASS** — `5016`/`5025`/`5015` khớp `100%` dữ liệu & `41/41` ảnh `HTTP 200` |

## 4. Vấn đề còn tồn tại & giới hạn đã biết

1. **Refresh Token Rotation trên runtime Local WinForms thủ công (`PARTIAL`)**: Đã `PASS` tự động (`42/42` trong `LocalJwtAuthenticationTests` & `AdminRestApiIntegrationTests` gồm rotation & `ReuseDetected`), nhưng chưa kích hoạt trực tiếp qua phiên WinForms kéo dài > 15 phút.
2. **Live SQL Server write fixture mới (`NOT TESTED` gần đây)**: Toàn bộ 11 luồng ghi đã `PASS` trên SQLite/`WebApplicationFactory` (`120/120`, cộng lịch sử `S35.4F` `23/23` Docker SQL Server 2022 E2E); chưa chạy thêm fixture SQL Server live mới.
3. **Phụ thuộc Docker Desktop & cổng trùng**: Chạy Local `dotnet run` yêu cầu Docker Desktop + `phonestore-restore-c2-db-1` đang hoạt động; không chạy đồng thời hai tiến trình cùng giữ cổng `5015` hoặc `5025`.
4. **Script dự phòng `scripts/run-admin-docker.ps1`**: Được thiết kế từ `S31.5G` cho `DirectSql`; khi dùng cần đặt `$env:AdminDataAccess__Mode = "DirectSql"` vì Admin hiện mặc định `RestApi`.

## 5. Quy ước bằng chứng & nguyên tắc an toàn

- Phân biệt rõ **`VERIFIED`** (có bằng chứng chạy thực tế đúng phạm vi), **`PARTIAL`**, **`NOT IMPLEMENTED`** và **`UNKNOWN`**. Không suy kết quả test tự động thành nghiệm thu GUI/production.
- Không chạy `docker compose down -v`, không xóa volume `phonestore-restore-c2_mssql_data`, không chạy migration/seed/restore đè hay sửa dữ liệu `PhoneStoreDB` khi chưa được phê duyệt.
- Không đưa mật khẩu, token, hash hoặc connection string chứa secret vào tài liệu/Git. Không tự commit/push khi chưa có phê duyệt theo phạm vi.

## 6. Bước tiếp theo & liên kết tài liệu

- **Bước tiếp theo**: Chờ người dùng rà soát kết quả `S-DOCS.REORGANIZE.1` trước khi commit/push checkpoint tài liệu.
- **Tài liệu hiện hành**:
  - Hướng dẫn Local & môi trường: [`LOCAL_DEVELOPMENT.md`](LOCAL_DEVELOPMENT.md)
  - Hướng dẫn Docker & CI/CD: [`DOCKER_AND_CI_GUIDE.md`](DOCKER_AND_CI_GUIDE.md)
  - Kiến trúc hệ thống: [`architecture/SYSTEM_ARCHITECTURE.md`](architecture/SYSTEM_ARCHITECTURE.md) · [`architecture/DATABASE_DESIGN.md`](architecture/DATABASE_DESIGN.md) · [`architecture/AUTHENTICATION_AND_JWT.md`](architecture/AUTHENTICATION_AND_JWT.md)
  - Hướng dẫn kiểm thử: [`testing/TESTING_GUIDE.md`](testing/TESTING_GUIDE.md)
  - Kho lưu trữ lịch sử: [`archive/PROGRESS_HISTORY.md`](archive/PROGRESS_HISTORY.md) (`archive/s17/`, `s29/`, `s31-system-analysis/`, `s32/`, `s34/`, `s35/`)
