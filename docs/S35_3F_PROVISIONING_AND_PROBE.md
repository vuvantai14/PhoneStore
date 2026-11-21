# S35.3F — SQL Fixture Provisioning & Internal Readiness Probe

**PASS cho công cụ provisioning SQL fixture, internal readiness probe, Phased Tiered RAM Gates + Watchdog và bộ kiểm thử an toàn/unit/mock. GO để bắt đầu diễn tập full-stack cách ly khi có lệnh chạy Docker thật.** Thực hiện ngày 2026-11-21 trên nhánh `main` (`165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`). Không khởi chạy SQL Server rehearsal, không pull/chạy thêm container và không tác động hệ thống business.

## A. Baseline

- **Git HEAD & working tree:** `main` và `origin/main` ở `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`. Working tree trước phiên có cập nhật `docs/PROGRESS.md`, các báo cáo `docs/S35_3A..E` và thư mục `scripts/cd/` từ các bước S35.3B–S35.3E.
- **Kết quả S35.3B–S35.3E:**
  - [S35.3B](S35_3B_CD_TOOLING_REHEARSAL.md): Xây dựng verifier GitHub/GHCR (`Rehearsal.Release.ps1`), deployment engine (`Rehearsal.Engine.ps1`), entry point (`Invoke-ReleaseRehearsal.ps1`), Compose rehearsal (`docker-compose.rehearsal.yml`) và test doubles (`Test-Rehearsal.ps1`). Dry-run release thật đạt tại run `44845223690`.
  - [S35.3C](S35_3C_ISOLATED_DOCKER_READINESS.md): Xác nhận mô hình cách ly tĩnh và ngân sách RAM cho full-stack rehearsal (khoảng `3,5 GiB` cold-start + `1,5 GiB` dự phòng host = `~5,0 GiB`).
  - [S35.3D](S35_3D_DOCKER_MOCK_REHEARSAL.md): Diễn tập Docker với HTTP mock (`Test-DockerMockRehearsal.ps1`, `scripts/cd/mock/*`) đạt 5/5 ca; ghi nhận Docker Desktop trên Windows có thể không mở host listener `127.0.0.1:5385/5386` khi Compose network đặt `internal: true`.
  - [S35.3E](S35_3E_FULLSTACK_READINESS.md): Bổ sung giới hạn CPU/RAM và SQL healthcheck vào `scripts/cd/docker-compose.rehearsal.yml`; xác định 2 khoảng trống công cụ cần đóng trước khi chạy full-stack: (1) công cụ provisioning SQL fixture cách ly và (2) internal readiness probe từ network fixture không phụ thuộc host port publishing.
- **Migrations hiện có (`src/PhoneStore.Data/Migrations`):**
  1. `20261001150857_InitialCreate`: Tạo 8 bảng `Brands`, `Categories`, `Roles` (seed sẵn `(1, 'Admin')`, `(2, 'Customer')`), `Products` (`CK_Products_Price > 0`, `CK_Products_StockQuantity >= 0`), `Users`, `ProductImages` (`CK_ProductImages_DisplayOrder >= 0`), `Orders` (`CK_Orders_TotalAmount >= 0`), `OrderDetails` (`CK_OrderDetails_Quantity > 0`, `CK_OrderDetails_UnitPrice >= 0`) cùng các khóa ngoại và unique index.
  2. `20261102122856_EnforceSingleAdmin`: Kiểm tra fail-closed mapping role `Admin` (`RoleId = 1`) và số lượng tài khoản Admin (`<= 1`), tạo filtered unique index `UX_Users_SingleAdmin` (`[RoleId] = 1`) và check constraint `CK_Roles_AdminIdentity`.
- **Cấu hình kết nối SQL & readiness của Web/API:**
  - `PhoneStore.Api` và `PhoneStore.Web` đọc `ConnectionStrings:PhoneStoreConnection` (`ConnectionStrings__PhoneStoreConnection`) qua `UseSqlServer`.
  - Trong `scripts/cd/docker-compose.rehearsal.yml`, cả hai app trỏ `Server=db,1433;Database=PhoneStoreDB_CdRehearsal;User Id=cd_rehearsal;Password=${CD_REHEARSAL_APP_PASSWORD:?Required};Encrypt=True;TrustServerCertificate=True;Max Pool Size=5`. Web bật `CatalogApi__Enabled=true` và `CatalogApi__BaseUrl=http://api:8080`.
  - API: `GET /api/health/live` trả HTTP 200 `{"status":"healthy"}` (liveness); `GET /api/health/ready` và `GET /api/health` kiểm tra `db.Database.CanConnectAsync()` trả HTTP 200 `{"status":"healthy"}` hoặc HTTP 503 `application/problem+json`; `GET /api/v1/products` và `GET /api/v1/products/{id}` trả `PagedProductsResponse` / `ProductDetailResponse`.
  - Web: `GET /` (`HomeController.Index`) truy vấn `Products`, `OrderDetails`/`Orders` (`GetBestSellersAsync`), `Brands`; `GET /products` (`ProductsController.Index`) truy vấn `Brands`, `Categories`, `Products`.

## B. Files Changed

| File | Thay đổi |
| --- | --- |
| `scripts/cd/Rehearsal.SqlFixture.ps1` | Module provisioning và xác minh SQL fixture: kiểm tra cách ly 6 lớp (Compose project, SQL hostname, database name, connection string, volume/mounts/network/ports, business secrets), sinh T-SQL idempotent cho 2 migrations + synthetic catalog seed + least-privilege lockdown và kiểm tra JSON kết quả dưới quyền `cd_rehearsal`. |
| `scripts/cd/Invoke-SqlFixtureProvisioning.ps1` | CLI entry point cho provisioning SQL fixture với 3 chế độ `-Mode ValidateOnly` (mặc định), `-Mode Verify` và `-Mode Provision -ConfirmFixtureProvisioning`. |
| `scripts/cd/Rehearsal.Probe.ps1` | Module internal readiness probe từ network fixture (`http://api:8080` và `http://web:8080`), phân biệt hợp đồng API vs Web, từ chối HTTP 200 của mock trong chế độ `PhoneStore`, có timeout/retry giới hạn và structured logs `[CD-PROBE]`. |
| `scripts/cd/Rehearsal.ResourceGate.ps1` | Module Phased Tiered RAM Gates, Memory Watchdog (`Assert-CdResourceGate`), kiểm tra bất biến business trước/sau từng pha (`Assert-CdBusinessInvariants`), dừng khẩn cấp không recreate (`Invoke-CdEmergencyRehearsalStop`) và bootstrap tuần tự (`Invoke-CdSequentialBootstrap`). |
| `scripts/cd/Invoke-ReleaseRehearsal.ps1` | Tích hợp `Rehearsal.Probe.ps1`, `Rehearsal.SqlFixture.ps1` và `Rehearsal.ResourceGate.ps1`; chuẩn hóa đường dẫn Windows khi đối chiếu `com.docker.compose.project.config_files`; dùng `Assert-CdInternalReadiness`, `Assert-CdResourceGate`, `Invoke-CdEmergencyRehearsalStop`, `Write-CdJournalRecord` và `Read-CdJournalRecords`. |
| `scripts/cd/Rehearsal.Engine.ps1` | Bổ sung `Write-CdJournalRecord`, `Read-CdJournalRecords`, kiểm tra cổng RAM trước/sau `ImagePull`, `DeployStep` và `RollbackStep`; khi RAM chạm mức nguy hiểm (`< 1,50 GiB`), ghi `PausedLowMemory`, gọi `EmergencyStop` và tuyệt đối không recreate container để rollback. |
| `scripts/cd/Test-DockerMockRehearsal.ps1` | Tái sử dụng `Assert-CdInternalReadiness -AllowMock` từ `Rehearsal.Probe.ps1` cho bước kiểm tra readiness của mock harness. |
| `scripts/cd/Test-Rehearsal.ps1` | Mở rộng bộ kiểm thử tự động: thêm kiểm thử persistent disk journal recovery, 9 nhóm kiểm thử internal readiness probe/mock rejection, 10 nhóm kiểm thử an toàn/idempotency cho SQL fixture provisioning và 4 nhóm kiểm thử Phased Tiered RAM Gates / Watchdog / `PausedLowMemory`. |
| `docs/S35_3F_PROVISIONING_AND_PROBE.md` | Báo cáo kỹ thuật S35.3F. |
| `docs/PROGRESS.md` | Cập nhật tiến độ S35.3F. |

## C. SQL Provisioning Design

Công cụ `scripts/cd/Rehearsal.SqlFixture.ps1` và `scripts/cd/Invoke-SqlFixtureProvisioning.ps1` chỉ cho phép kết nối tới SQL fixture của project `phonestore-cd-rehearsal` và dừng ngay (fail-closed) nếu không chứng minh được mục tiêu là fixture:

1. **Sáu cổng kiểm tra an toàn bắt buộc trước mọi lệnh SQL:**
   - **Compose project:** Bắt buộc `com.docker.compose.project = phonestore-cd-rehearsal`, service `db`, và nhãn `com.docker.compose.project.config_files` sau chuẩn hóa đường dẫn (`\` / `/`) phải khớp tuyệt đối `scripts/cd/docker-compose.rehearsal.yml`. Từ chối `phonestore-restore-c2`, tên container `phonestore-restore-c2-*` hoặc bất kỳ Container ID nào trùng với 3 container business đang chạy.
   - **SQL hostname:** Cấu hình kết nối ứng dụng bắt buộc `DataSource = db,1433`. Từ chối `127.0.0.1,14334`, `localhost,14334`, `localhost` hoặc mọi cổng host. Bên trong phiên SQL, đối chiếu `CAST(SERVERPROPERTY('MachineName') AS nvarchar(128))` khớp tuyệt đối với `$db.Config.Hostname` của container rehearsal (và khác `addd99b34bf2`).
   - **Database name:** Bắt buộc `PhoneStoreDB_CdRehearsal`. Từ chối `PhoneStoreDB`, `PhoneStoreDB_AcceptanceTest` và các system DB cho bước schema/seed. Mọi batch SQL đều kiểm tra `DB_ID(N'PhoneStoreDB') IS NULL AND DB_ID(N'PhoneStoreDB_AcceptanceTest') IS NULL` và `DB_NAME() = N'PhoneStoreDB_CdRehearsal'`.
   - **Không dùng business connection string:** Phân tích qua `SqlConnectionStringBuilder`; cấm `IntegratedSecurity` / `Trusted_Connection`, bắt buộc `Encrypt=True`, `TrustServerCertificate=True`, `MaxPoolSize` giới hạn `1..10`, `UserID = cd_rehearsal` (hoặc `cd_rehearsal_provision`).
   - **Không mount business volumes:** Container `db` phải không privileged, không có `HostConfig.Binds`, không publish host port (`PortBindings` rỗng), chỉ nối mạng nội bộ `phonestore-cd-rehearsal_isolated` (`Internal = true`) và chỉ mount đúng 1 volume `phonestore-cd-rehearsal_sql` tại `/var/opt/mssql`. Từ chối `phonestore-restore-c2_mssql_data`.
   - **Không dùng business secrets:** Kiểm tra định dạng (`16..128` ký tự), bắt buộc `CD_REHEARSAL_SQL_PASSWORD`, `CD_REHEARSAL_PROVISION_PASSWORD` và `CD_REHEARSAL_APP_PASSWORD` đôi một khác nhau, đồng thời thu thập danh sách bí mật cấm từ `.env`, biến môi trường tiến trình và cấu hình inspect của container business `phonestore-restore-c2-*` để chặn ngay nếu có tái sử dụng secret.
2. **Năm pha thực thi idempotent (khi chạy với `-Mode Provision -ConfirmFixtureProvisioning` trong giai đoạn được duyệt):**
   - `SqlPreflight`: Xác minh `SERVERPROPERTY('MachineName')` và sự vắng mặt của `PhoneStoreDB` / `PhoneStoreDB_AcceptanceTest`.
   - `Bootstrap`: Tạo database `PhoneStoreDB_CdRehearsal` với kích thước file giới hạn nếu chưa có; tạo/cập nhật login `cd_rehearsal_provision` (tạm bật để chạy migration/seed) và `cd_rehearsal`.
   - `MigrateAndSeed` (chạy dưới `cd_rehearsal_provision` trong transaction `XACT_ABORT ON`): Tạo `__EFMigrationsHistory`, áp dụng idempotent 2 migrations `20261001150857_InitialCreate` và `20261102122856_EnforceSingleAdmin` (`ProductVersion = 10.0.15`), upsert thương hiệu `S353-FIXTURE-BRAND`, danh mục `S353-FIXTURE-CATEGORY`, sản phẩm active `S353E-FIXTURE-PHONE` (`Price = 12990000.00`, `StockQuantity = 5`, `IsActive = 1`) và sản phẩm inactive `S353E-FIXTURE-PHONE-INACTIVE` (`Price = 9990000.00`, `StockQuantity = 0`, `IsActive = 0`), đồng thời xác nhận `Users = 0`, `Orders = 0`, `OrderDetails = 0`.
   - `Lockdown`: Cấp `GRANT SELECT` trên các bảng đọc cho `cd_rehearsal`, `DENY INSERT, UPDATE, DELETE, ALTER ON SCHEMA::[dbo] TO [cd_rehearsal]` và `ALTER LOGIN [cd_rehearsal_provision] DISABLE`.
   - `Verify` (chạy dưới `cd_rehearsal`): Kiểm tra quyền least-privilege (`canSelectProducts = 1`, mọi quyền ghi/DDL/sysadmin/business DB = `0`), 2 migrations, số lượng bản ghi đã seed và trả kết quả JSON có cấu trúc.

## D. Internal Probe Implementation

Module `scripts/cd/Rehearsal.Probe.ps1` cung cấp `Assert-CdInternalReadiness` và được tích hợp trực tiếp vào `Assert-CdHealth` trong `scripts/cd/Invoke-ReleaseRehearsal.ps1`:

- **Không phụ thuộc Windows host port publishing:** Khi chạy trên Docker, `Invoke-CdDockerNetworkHttpGet` kiểm tra container đích (`api` hoặc `web`) đang chạy trên mạng nội bộ `phonestore-cd-rehearsal_isolated` (`Internal = true`) rồi khởi chạy container probe Python ngắn hạn (`docker run --rm --network phonestore-cd-rehearsal_isolated --platform linux/amd64 --pull never --read-only --cap-drop ALL --security-opt no-new-privileges:true --memory 64m --cpus 0.25 --pids-limit 32 --user 10001:10001`) gọi trực tiếp `http://api:8080` hoặc `http://web:8080`. Container probe tự hủy (`--rm`) ngay khi trả JSON, không để lại thành viên thừa trên network trước lần chụp `Get-CdSnapshot` tiếp theo.
- **Không làm suy yếu TLS hoặc authentication:** Không sửa mã nguồn Web/API, không tắt HTTPS/cookie policy hay SQL `Encrypt=True`, không mount volume, không mở port và không truyền bất kỳ secret nào vào container probe.
- **Phân biệt rõ API readiness và Web readiness & từ chối mock HTTP 200:**
  - Mặc định (`Mode = PhoneStore`), mọi phản hồi có chữ ký mock (`{"version":...,"ready":true}`) đều bị đánh dấu lỗi terminal (`IsTerminal = $true`) và dừng ngay ở lần thử đầu tiên mà không retry.
  - **API readiness (`Service = 'api'`):** Kiểm tra đồng thời `GET /api/health/live` (HTTP 200, `application/json`, `status == "healthy"`), `GET /api/health/ready` (HTTP 200, `application/json`, `status == "healthy"`) và `GET /api/v1/products` (HTTP 200, `application/json`, cấu trúc `PagedProductsResponse` có `items` chứa đúng 1 sản phẩm active `name == "S353E-FIXTURE-PHONE"`, `brandName == "S353-FIXTURE-BRAND"`, `id > 0` và tuyệt đối không rò rỉ `S353E-FIXTURE-PHONE-INACTIVE`).
  - **Web readiness (`Service = 'web'`):** Kiểm tra `GET /` và `GET /products` đều trả HTTP 200 với `Content-Type: text/html` (từ chối `application/json`), tài liệu HTML hợp lệ có `PhoneStore`, trang `/products` chứa marker `S353E-FIXTURE-PHONE` và không chứa `S353E-FIXTURE-PHONE-INACTIVE`.
- **Timeout, retry giới hạn và logging:** Hỗ trợ `MaxAttempts` (mặc định `15`, giới hạn `1..30`), `TimeoutSeconds` mỗi request (mặc định `4s`, giới hạn `1..15s`), `RetryDelayMilliseconds` (mặc định `2000ms`) và `TotalTimeoutSeconds` (mặc định `60s`). Mỗi lần thử ghi log cấu trúc `[CD-PROBE] utc=... service=... mode=... attempt=... endpoint=... status=... result=PASS|RETRY|FAIL detail="..."`.

## E. Rollback Baseline

| Hạng mục | Giá trị đã xác minh |
| --- | --- |
| API image ID cũ (local rollback baseline) | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` (`phonestore-api:s34-2r02-net10`, `linux/amd64`, tạo `2026-11-19T04:29:40.09618804Z`) |
| Web image ID cũ (local rollback baseline) | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` (`phonestore-web:s34-2r02-net10`, `linux/amd64`, tạo `2026-11-19T04:30:12.751951715Z`) |
| SQL fixture pinned image | `mcr.microsoft.com/mssql/server@sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` (local ID `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd`, `linux/amd64`) |
| Approved release run & artifact ZIP digest | Run `44845223690`, attempt `1`, commit `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`, artifact digest `sha256:93e32c776343f5b1d86d2a711fb7aa768cb956d8b87bd5d54128de68f24c10b1` |
| API image reference mới (GHCR manifest digest) | `ghcr.io/vuvantai14/phonestore-api@sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050` |
| Web image reference mới (GHCR manifest digest) | `ghcr.io/vuvantai14/phonestore-web@sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` |

- **Truy xuất rollback target & lưu transaction journal:** Trước khi thay container đầu tiên, `Invoke-CdDeployment` chụp `$before = & $Adapter.Snapshot`, kiểm tra cả hai image ID `$before.api` và `$before.web` tồn tại cục bộ qua `docker image inspect --format '{{.Id}}'`, kiểm tra internal readiness của cả hai service và ghi bản ghi `Planned` (kèm `Before`, `Target`, `References`) vào `%LOCALAPPDATA%\PhoneStore-CdRehearsal\journal.jsonl` qua `Write-CdJournalRecord` với `FileShare.Read` và `$log.Flush($true)` dưới khóa độc quyền `deployment.lock`.
- **Phased Tiered RAM Gates & Memory Watchdog (`scripts/cd/Rehearsal.ResourceGate.ps1`):**
  - Áp dụng bảng ngưỡng phân tầng trước (`Pre`) và sau (`Post`) mỗi pha kèm kiểm tra `WSL2 SwapUsed` và `Assert-CdBusinessInvariants`:
    - `ImagePull/Pre`: Windows Free `>= 3,00 GiB`, WSL2 `MemAvailable >= 3,50 GiB`, Swap `<= 128 MiB`.
    - `SqlColdStart/Pre`: Windows Free `>= 3,80 GiB`, WSL2 `MemAvailable >= 4,50 GiB`, Swap `<= 128 MiB`.
    - `SqlProvision/Pre`: Windows Free `>= 2,20 GiB`, WSL2 `MemAvailable >= 3,00 GiB`, Swap `<= 128 MiB`.
    - `StartApi/Pre`: Windows Free `>= 2,20 GiB`, WSL2 `MemAvailable >= 2,80 GiB`, Swap `<= 128 MiB`.
    - `StartWeb/Pre`: Windows Free `>= 2,05 GiB`, WSL2 `MemAvailable >= 2,60 GiB`, Swap `<= 128 MiB`.
    - `DeployStep/Pre`: Windows Free `>= 1,90 GiB`, WSL2 `MemAvailable >= 2,40 GiB`, Swap `<= 128 MiB`.
    - `WarningFloor` (`Post` mọi pha): Windows Free `>= 1,80 GiB`, WSL2 `MemAvailable >= 2,00 GiB`, Swap `<= 128 MiB` (chặn bắt đầu pha tiếp theo nếu RAM dưới ngưỡng cảnh báo).
    - `RollbackStep` / `EmergencyFloor`: Windows Free `>= 1,50 GiB`, WSL2 `MemAvailable >= 1,50 GiB`, Swap `<= 256 MiB`.
  - **Không recreate container để rollback khi RAM ở mức nguy hiểm (`< 1,50 GiB`):** Nếu phát hiện `CRITICAL_EMERGENCY` (`WinFree < 1,50 GiB`, `WslAvail < 1,50 GiB`, `WslSwap > 256 MiB` hoặc business container suy giảm), engine tuyệt đối không gọi `Adapter.Apply` để recreate container, ghi ngay pha `PausedLowMemory` (kèm `LowMemoryStopReason`) vào `journal.jsonl` và gọi `Invoke-CdEmergencyRehearsalStop` (`stop -t 10 web api`, sau đó `stop -t 60 db`) để trả RAM về host.
- **Recovery khi PowerShell bị ngắt hoặc dừng do `PausedLowMemory`:** Nếu tiến trình bị ngắt ở bất kỳ pha trung gian nào (`Planned`, `Before-api`, `Ready-api`, `Before-web`, `PausedLowMemory`, hoặc giữa quá trình rollback `RollbackPending` / `RollbackBefore-*` / `RollbackAfter-*`), bản ghi cuối trong `journal.jsonl` có `Phase -notin @('Deployed', 'RolledBack')`. Mọi lệnh apply mới đều bị chặn (`An interrupted deployment requires -Recover.`). Khi chạy `-Recover`, engine đọc `journal.jsonl` qua `Read-CdJournalRecords`, kiểm tra `RollbackStep` gate (`>= 1,50 GiB` RAM; nếu vẫn dưới `1,50 GiB` sẽ tiếp tục từ chối recreate), xác nhận hai image rollback còn ở local, chỉ recreate service nào đã lệch khỏi `Transaction.Before` hoặc không còn chạy (theo thứ tự `api` trước, `web` sau), kiểm tra lại readiness của cả `api` và `web` bằng internal probe, đối chiếu `Sql` invariant và ghi `RolledBack`.

## F. Test Results

| Hạng mục kiểm tra | Lệnh thực thi | Kết quả |
| --- | --- | --- |
| PowerShell parser (9 scripts) | `[System.Management.Automation.Language.Parser]::ParseFile(...)` trên `scripts/cd/*.ps1` | **PASS (9/9 file, 0 lỗi cú pháp)** |
| Unit tests & persistent journal recovery | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-Rehearsal.ps1` | **PASS** (10/10 ca deployment engine, 4 ca manifest/registry, recovery từ file `journal.jsonl` trên đĩa và kiểm tra baseline thiếu dữ liệu) |
| Mock probe tests | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-Rehearsal.ps1` (nhóm P1–P9) | **PASS** (API readiness, Web readiness, từ chối mock HTTP 200 ngay ở attempt 1 cho cả API và Web, từ chối lệch hợp đồng HTML/JSON, phát hiện thiếu marker hoặc rò rỉ sản phẩm inactive, retry 503 có giới hạn, cờ bảo mật `docker run --rm --network` và rollback tự động khi candidate trả mock HTTP 200) |
| SQL fixture safety tests | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-Rehearsal.ps1` (nhóm S1–S10) | **PASS** (Chặn sai Compose project, trùng container ID/hostname business, sai SQL hostname/database/connection string, sai volume/port/network, tái sử dụng business secret; kiểm tra `-Mode ValidateOnly` và mô phỏng `-Mode Provision` chạy lại 2 lần idempotent) |
| Phased Tiered RAM Gates & Watchdog tests | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-Rehearsal.ps1` (nhóm R1–R4) | **PASS** (Kiểm tra biên `3,80/4,50 GiB` cho `SqlColdStart`, `3,00/3,50 GiB` cho `ImagePull`, WSL Swap, Business Health; bootstrap tuần tự `pull -> db -> sql-provision -> api -> probe -> web -> probe`; dừng trước `StartWeb` khi RAM `2,00 < 2,05 GiB`; freeze tại vùng cảnh báo `1,85 GiB` kèm rollback an toàn; chạm sàn nguy hiểm `1,35 < 1,50 GiB` bỏ qua recreate, ghi `PausedLowMemory` ra đĩa, gọi `EmergencyStop` và chỉ cho phép `-Recover` khi RAM lên lại `2,40 GiB`) |
| Live Phased RAM Gate & Business preflight | `Assert-CdResourceGate -Phase 'ImagePull'` & `-Phase 'SqlColdStart'` trên máy thật | **PASS** (`winFreeGiB=4,52..4,53 GiB >= 3,80 GiB`, `wslAvailGiB=5,65 GiB >= 4,50 GiB`, `wslSwapMiB=0`, `business=healthy`) |
| Compose static validation (mock & full-stack) | `Test-DockerMockRehearsal.ps1 -ValidateOnly` và `docker --context desktop-linux compose --env-file NUL -p phonestore-cd-rehearsal -f scripts/cd/docker-compose.rehearsal.yml config --format json` | **PASS** (`services=api,db,web`, `network=phonestore-cd-rehearsal_isolated` `internal=true`, `volume=phonestore-cd-rehearsal_sql`) |
| Whitespace & diff formatting | `git diff --check` | **PASS (0 lỗi định dạng)** |

## G. Business Invariants

Đối chiếu `docker --context desktop-linux inspect` của 3 container business trước và sau toàn bộ quá trình kiểm tra:

| Service | Container name | Container ID | StartedAt UTC | Restarts | Health | Image | Mounts |
| --- | --- | --- | --- | --- | --- | --- | --- |
| SQL | `phonestore-restore-c2-db-1` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | `2026-11-21T00:27:43.48526107Z` | `0` | `healthy` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | Volume `phonestore-restore-c2_mssql_data` -> `/var/opt/mssql` (`rw`) |
| API | `phonestore-restore-c2-api-1` | `b79d2e025e11971b7f5fa5a132919bc2ea564a7753e5d22a3c1401d505b67239` | `2026-11-21T00:27:43.503227531Z` | `0` | `healthy` | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` | `[]` |
| Web | `phonestore-restore-c2-web-1` | `e55bfc7a111747f7db0c06cfb9645a29b352fe863d8a8ea16dea6964192a6839` | `2026-11-21T00:27:43.55036162Z` | `0` | `healthy` | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` | Bind `.../wwwroot/uploads/products` -> `/app/wwwroot/uploads/products` (`ro`) |

Cả 3 container business giữ nguyên tuyệt đối ID, `StartedAt`, `RestartCount = 0`, `healthy`, image ID và mounts. Không có container, network hoặc volume `phonestore-cd-rehearsal*` nào được tạo ra trong phiên này.

## H. Remaining Blockers

- **Toàn bộ blocker công cụ và ngưỡng RAM preflight của S35.3E đã được giải quyết:**
  1. SQL fixture provisioning tool (`Rehearsal.SqlFixture.ps1`, `Invoke-SqlFixtureProvisioning.ps1`) đã hoàn tất và vượt qua 10 nhóm safety/idempotency tests.
  2. Internal readiness probe từ network fixture (`Rehearsal.Probe.ps1`) đã hoàn tất và vượt qua 9 nhóm probe/mock-rejection tests.
  3. Phased Tiered RAM Gates + Memory Watchdog (`Rehearsal.ResourceGate.ps1`) đã tích hợp vào engine/entrypoint và vượt qua kiểm tra trực tiếp trên máy thật (`Windows Free = 4,52–4,53 GiB >= 3,80 GiB`, `WSL2 MemAvailable = 5,65 GiB >= 4,50 GiB`, `WSL2 Swap = 0 MiB`, `Business = 3/3 healthy`).
- **Bước thực thi Docker thật** (khởi chạy container `db`, provision `PhoneStoreDB_CdRehearsal`, bật baseline `api` -> `web` và chạy kịch bản rollout/rollback full-stack) chỉ còn chờ lệnh bắt đầu bước thực thi tiếp theo (ví dụ S35.3G).

## I. GO/NO-GO for Full-stack Rehearsal

- **Quyết định: `GO` cho bước thực thi Full-stack Isolated Rehearsal theo quy trình khởi động tuần tự và Phased Tiered RAM Gates.**
- Tất cả điều kiện tiên quyết về công cụ, kiểm thử tĩnh/test doubles, bất biến business và cổng RAM thực tế tại thời điểm đo (`4,52–4,53 GiB` Windows Free / `5,65 GiB` WSL2 `MemAvailable`) đều đạt.
