# S35.3J — Real PhoneStore Business Deployment (`phonestore-restore-c2`)

- **Verification Date**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`main`)
- **Target Docker Project**: `phonestore-restore-c2`
- **Status**: **VERIFIED — PASS (`DEPLOYED`)**

---

## A. Executive Summary

Giai đoạn **S35.3J** đã thực thi triển khai thật **PhoneStore REST API** (`api`) và **Customer Web** (`web`) từ GHCR release đã được phê duyệt (`runId = 44845223690`, `attempt = 1`, `commit = 165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`) lên môi trường Docker business hiện hành (`phonestore-restore-c2`) thông qua `scripts/cd/Invoke-BusinessCd.ps1` và `scripts/cd/Business.Cd.ps1`:

1. **Pre-Deployment Gates (`VALIDATED_READY`)**: Xác nhận approval file hợp lệ, SQL External Backup Gate đạt `PASS` (`PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak`, `827,392` bytes, SHA-256 `AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706`), RAM/WSL2 ResourceGate đạt `PASS` (`WinFree = 4.60–4.82 GiB`, `WslAvail = 5.67–5.68 GiB`, `WslSwap = 0 MiB`), và cả 2 rollback baseline images đều sẵn sàng trong local Docker cache.
2. **Sequential Deployment (`api` -> `web`)**:
   - Recreate riêng `phonestore-restore-c2-api-1` (`up -d --no-deps --no-build --pull never api`), đợi Docker healthcheck đạt `healthy` và xác minh HTTP 200 trên `/api/health/live`, `/api/health/ready`, `/api/v1/products`, đồng thời xác nhận `phonestore-restore-c2-db-1` bất biến.
   - Recreate riêng `phonestore-restore-c2-web-1` (`up -d --no-deps --no-build --pull never web`), đợi Docker healthcheck đạt `healthy` và xác minh HTTP 200 trên `/`, `/products`, `/products/40` (kết nối thực tế sang `http://api:8080/api/v1/products/40`).
3. **5-Minute Post-Deployment Stability Window**: Giám sát từ `2026-11-21T03:54:04Z` đến `2026-11-21T04:00:48Z` (> 6 phút 40 giây). Cả 3 containers (`db`, `api`, `web`) giữ nguyên trạng thái `Running = true`, `Health = healthy`, `RestartCount = 0`, `0` dòng lỗi trong log của `api`/`web`, và container SQL `phonestore-restore-c2-db-1` giữ nguyên tuyệt đối Container ID, `StartedAt`, image và volume mount.

---

## B. Approved Release & Image Digests

| Thuộc tính | Giá trị đã phê duyệt và triển khai |
|---|---|
| `Repository` | `vuvantai14/PhoneStore` |
| `TargetEnvironment` | `phonestore-restore-c2` |
| `RunId` / `Attempt` | `44845223690` / `1` |
| `Commit` | `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` |
| `ArtifactDigest` | `sha256:93e32c776343f5b1d86d2a711fb7aa768cb956d8b87bd5d54128de68f24c10b1` |
| `API Release Reference` | `ghcr.io/vuvantai14/phonestore-api@sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050` |
| `API Local Image ID` | `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050` |
| `Web Release Reference` | `ghcr.io/vuvantai14/phonestore-web@sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` |
| `Web Local Image ID` | `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` |
| `AutoDeployUnapproved` | `false` |

---

## C. Pre-Deployment Baseline Snapshot

Snapshot của stack `phonestore-restore-c2` ngay trước khi triển khai (`2026-11-21T03:38:00Z`):

| Container | Container ID (Trước) | Image ID (Trước / Rollback Baseline) | `StartedAt` (UTC) | `Running` / `Health` / `Restarts` | Ports / Mounts chính |
|---|---|---|---|---|---|
| `phonestore-restore-c2-db-1` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `2026-11-21T00:27:43.48526107Z` | `true` / `healthy` / `0` | `127.0.0.1:14334->1433`, `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:rw` |
| `phonestore-restore-c2-api-1` | `b79d2e025e11971b7f5fa5a132919bc2ea564a7753e5d22a3c1401d505b67239` | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` | `2026-11-21T00:27:43.503227531Z` | `true` / `healthy` / `0` | `127.0.0.1:5025->8080`, `Mounts=[]` |
| `phonestore-restore-c2-web-1` | `e55bfc7a111747f7db0c06cfb9645a29b352fe863d8a8ea16dea6964192a6839` | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` | `2026-11-21T00:27:43.55036162Z` | `true` / `healthy` / `0` | `127.0.0.1:5015->8080`, `bind:.../wwwroot/uploads/products:/app/wwwroot/uploads/products:ro` |

- Cấu hình Compose hợp nhất (`docker-compose.yml` + `docker-compose.restore.yml` + `docker-compose.net10.yml` + `%LOCALAPPDATA%\PhoneStore-BusinessCd\docker-compose.cd-override.yml`) giữ nguyên hoàn toàn host ports (`127.0.0.1:5025`, `127.0.0.1:5015`, `127.0.0.1:14334`), network `phonestore-restore-c2_phonestore-net`, volume/bind mounts, biến môi trường, healthchecks và cấu hình `db`.
- Cả 2 rollback baseline images (`sha256:91b13b71...` và `sha256:bbcd7442...`) được bảo toàn trong local Docker cache trước và sau khi triển khai.

---

## D. SQL External Backup & Resource Gate Results

### 1. SQL External Backup Gate
- **File**: `C:\PhoneStoreDockerTransfer\backup\PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak`
- **Size**: `827,392` bytes
- **SHA-256**: `AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706`
- **Gate Result**: `PASS` (`Verified external SQL backup outside Docker volume (PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak, 827392 bytes, CHECKSUM + VERIFYONLY).`)

### 2. Phased RAM & WSL2 Swap ResourceGate Telemetry

| Thời điểm / Pha | Ngưỡng yêu cầu (`WinFree` / `WslAvail` / `WslSwap`) | Giá trị đo thực tế (`WinFree` / `WslAvail` / `WslSwap`) | `BusinessStatus` | Kết quả |
|---|---|---|---|---|
| `ValidateOnly` (`DeployStep/Pre`, `03:38:01Z`) | `>= 1.90 GiB` / `>= 2.40 GiB` / `<= 128 MiB` | `4.60 GiB` / `5.67 GiB` / `0 MiB` | `healthy` | **PASS** |
| `Apply Preflight` (`DeployStep/Pre`, `03:53:21Z`) | `>= 1.90 GiB` / `>= 2.40 GiB` / `<= 128 MiB` | `4.70 GiB` / `5.68 GiB` / `0 MiB` | `healthy` | **PASS** |
| `ImagePull/Verify` (`DeployStep/Pre`, `03:53:37Z`) | `>= 1.90 GiB` / `>= 2.40 GiB` / `<= 128 MiB` | `4.60 GiB` / `5.68 GiB` / `0 MiB` | `healthy` | **PASS** |
| `ImagePull/Verify` (`DeployStep/Post`, `03:53:42Z`) | `>= 1.80 GiB` / `>= 2.00 GiB` / `<= 128 MiB` | `4.55 GiB` / `5.67 GiB` / `0 MiB` | `healthy` | **PASS** |
| `Before-api` (`DeployStep/Pre`, `03:53:43Z`) | `>= 1.90 GiB` / `>= 2.40 GiB` / `<= 128 MiB` | `4.53 GiB` / `5.67 GiB` / `0 MiB` | `healthy` | **PASS** |
| `Ready-api` (`DeployStep/Post`, `03:53:52Z`) | `>= 1.80 GiB` / `>= 2.00 GiB` / `<= 128 MiB` | `4.53 GiB` / `5.71 GiB` / `0 MiB` | `healthy` | **PASS** |
| `Before-web` (`DeployStep/Pre`, `03:53:53Z`) | `>= 1.90 GiB` / `>= 2.40 GiB` / `<= 128 MiB` | `4.53 GiB` / `5.70 GiB` / `0 MiB` | `healthy` | **PASS** |
| `Ready-web` (`DeployStep/Post`, `03:54:04Z`) | `>= 1.80 GiB` / `>= 2.00 GiB` / `<= 128 MiB` | `4.54 GiB` / `5.77 GiB` / `0 MiB` | `healthy` | **PASS** |
| `T+5m Stability` (`04:00:48Z`) | `>= 1.80 GiB` / `>= 2.00 GiB` / `<= 128 MiB` | `4.56 GiB` / `5.68 GiB` / `0 MiB` | `healthy` | **PASS** |

---

## E. Sequential Deployment Timeline (`api` -> `web`)

Trước khi chạy bản deploy thành công, một lỗi cú pháp ngoặc đơn PowerShell trong `Assert-CdBusinessHttpReadiness` (`(& docker ... -join '')` khiến `-join ''` bị truyền làm tham số cho `docker inspect`) đã được phát hiện ở bước preflight probe (trước khi ghi `Planned` hoặc gọi `Apply`, nên chưa có bất kỳ thay đổi nào lên container) và được sửa triệt để trong `scripts/cd/Business.Cd.ps1` cùng xử lý `$transportResult.Error` trong `scripts/cd/Rehearsal.Probe.ps1` và `-SqlOnlyInvariant` trong `scripts/cd/Rehearsal.ResourceGate.ps1`.

Timeline thực thi triển khai thật (`2026-11-21T03:53:20Z` – `2026-11-21T03:54:04Z`):

1. **`03:53:20.004Z` — `Start` & `ApprovalCheck`**: Xác nhận approval record `runId=44845223690`, `attempt=1`, `commit=165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`.
2. **`03:53:20.282Z` — `DockerPreflight` & `SqlBackupGate` (`03:53:21.606Z`)**: Cả 3 containers business (`db`, `api`, `web`) đạt `healthy`; bản backup ngoài Docker volume đạt `PASS`.
3. **`03:53:35.520Z` — `ReleaseVerify` & Baseline Probes (`03:53:36.466Z` – `03:53:36.890Z`)**: Manifest và GHCR digests khớp tuyệt đối; baseline HTTP probes cho `api` và `web` đạt `PASS`.
4. **`03:53:42.645Z` — `Journal-Planned`**: Ghi transaction phase `Planned` vào `%LOCALAPPDATA%\PhoneStore-BusinessCd\journal.jsonl`.
5. **`03:53:43.712Z` — `Journal-Before-api` & Recreate `api`**:
   - Chạy `docker compose --project-name phonestore-restore-c2 -f docker-compose.yml -f docker-compose.restore.yml -f docker-compose.net10.yml -f docker-compose.cd-override.yml up -d --no-deps --no-build --pull never api`.
   - Container `phonestore-restore-c2-api-1` mới (`Id = cec828cd68c70cd647dda1bed7d6c2d61da6f67ca18eba423d4ce82e798986f3`) khởi động lúc `03:53:44.575Z` với image `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050`.
   - Probe đợi Docker healthcheck chuyển từ `starting` (attempts 1–3) sang `healthy` ở attempt 4 (`03:53:51.551Z`) và xác nhận `/api/health/live`, `/api/health/ready`, `/api/v1/products` đều trả `HTTP 200`.
   - **`03:53:51.683Z` — `Journal-Ready-api`**: Ghi nhận `api` sẵn sàng và `db` bất biến.
6. **`03:53:53.736Z` — `Journal-Before-web` & Recreate `web`**:
   - Chạy `docker compose --project-name phonestore-restore-c2 -f docker-compose.yml -f docker-compose.restore.yml -f docker-compose.net10.yml -f docker-compose.cd-override.yml up -d --no-deps --no-build --pull never web`.
   - Container `phonestore-restore-c2-web-1` mới (`Id = ed6ef0f8027d5ffd50455f9e2db66bc20a386abf7d21ce24bb2646973a46c75b`) khởi động lúc `03:53:54.556Z` với image `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76`.
   - Probe đợi Docker healthcheck chuyển từ `starting` (attempts 1–4) sang `healthy` ở attempt 5 (`03:54:03.351Z`) và xác nhận `/`, `/products` đều trả `HTTP 200` (`text/html`).
   - **`03:54:03.484Z` — `Journal-Ready-web`**: Ghi nhận `web` sẵn sàng và `db` bất biến.
7. **`03:54:04.591Z` – `03:54:04.871Z` — Final Verification & `Journal-Deployed`**:
   - Probe cuối cùng trên cả `api` và `web` đạt `PASS` ngay ở attempt `1/20`.
   - Kiểm tra full-stack `Get-CdBusinessInvariantsSnapshot` xác nhận cả 3 containers (`db`, `api`, `web`) đạt `healthy` với `RestartCount = 0`.
   - Ghi phase `Deployed` (`03:54:04.788Z`) và hoàn tất với `Outcome = DEPLOYED`.

---

## F. HTTP Readiness & Catalog Verification

Kết quả kiểm tra trực tiếp trên host ports (`127.0.0.1:5025` và `127.0.0.1:5015`) tại `T+0m` (`03:55:10Z`) và `T+5m` (`04:00:48Z`):

| Endpoint | HTTP Status | Content-Type | Kích thước Payload | Chi tiết xác minh |
|---|---|---|---|---|
| `http://127.0.0.1:5025/api/health/live` | `200` | `application/json; charset=utf-8` | `20` bytes | `{"status":"healthy"}` |
| `http://127.0.0.1:5025/api/health/ready` | `200` | `application/json; charset=utf-8` | `20` bytes | `{"status":"healthy"}` (kết nối SQL business thành công) |
| `http://127.0.0.1:5025/api/v1/products` | `200` | `application/json; charset=utf-8` | `1,890` bytes | Paged catalog JSON hợp lệ từ `PhoneStoreDB` thật |
| `http://127.0.0.1:5025/api/v1/products/40` | `200` | `application/json; charset=utf-8` | `488` bytes | Chi tiết sản phẩm `ProductId = 40` hợp lệ |
| `http://127.0.0.1:5015/` | `200` | `text/html; charset=utf-8` | `43,304` bytes | Trang chủ PhoneStore Customer Web HTML |
| `http://127.0.0.1:5015/products` | `200` | `text/html; charset=utf-8` | `38,765` bytes | Trang danh sách sản phẩm HTML qua REST API |
| `http://127.0.0.1:5015/products/40` | `200` | `text/html; charset=utf-8` | `19,895` bytes | Trang chi tiết sản phẩm HTML (`GET http://api:8080/api/v1/products/40` -> `200`) |

---

## G. Business SQL & Volume Invariant Verification

So sánh container `phonestore-restore-c2-db-1` trước triển khai, ngay sau triển khai (`T+0m`) và sau cửa sổ giám sát (`T+5m`):

| Thuộc tính `phonestore-restore-c2-db-1` | Trước S35.3J (`03:38:00Z`) | Sau S35.3J (`T+0m`, `03:55:10Z`) | Sau giám sát (`T+5m`, `04:00:48Z`) | Đánh giá |
|---|---|---|---|---|
| `Id` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | **UNCHANGED** |
| `Image` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | **UNCHANGED** |
| `StartedAt` | `2026-11-21T00:27:43.48526107Z` | `2026-11-21T00:27:43.48526107Z` | `2026-11-21T00:27:43.48526107Z` | **UNCHANGED** |
| `Running` / `Health` / `RestartCount` | `true` / `healthy` / `0` | `true` / `healthy` / `0` | `true` / `healthy` / `0` | **UNCHANGED** |
| `Mounts` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:True` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:True` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:True` | **UNCHANGED** |

- Không có bất kỳ thao tác ghi SQL, migration, seed hoặc restore nào được thực hiện trên `PhoneStoreDB` business.

---

## H. Persistent Journal & Log Evidence

Trạng thái journal trong `%LOCALAPPDATA%\PhoneStore-BusinessCd\journal.jsonl`:

| Thứ tự | Timestamp (UTC) | Phase | RunId | Commit |
|---|---|---|---|---|
| 1 | `2026-11-21T03:53:42.6453133Z` | `Planned` | `44845223690` | `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` |
| 2 | `2026-11-21T03:53:43.7121634Z` | `Before-api` | `44845223690` | `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` |
| 3 | `2026-11-21T03:53:51.6834557Z` | `Ready-api` | `44845223690` | `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` |
| 4 | `2026-11-21T03:53:53.7368075Z` | `Before-web` | `44845223690` | `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` |
| 5 | `2026-11-21T03:54:03.4848374Z` | `Ready-web` | `44845223690` | `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` |
| 6 | `2026-11-21T03:54:04.7888952Z` | `Deployed` | `44845223690` | `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` |

- Kiểm tra idempotency sau khi deploy (`./scripts/cd/Invoke-BusinessCd.ps1 -ValidateOnly` lúc `03:55:10Z`): trả về `Outcome = "NOOP_ALREADY_DEPLOYED"` (`Approved release runId=44845223690 commit=165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07 is already deployed.`).

---

## I. 5-Minute Stability Monitoring Result

Giám sát ổn định sau triển khai từ `2026-11-21T03:54:04Z` đến `2026-11-21T04:00:48Z` (6 phút 44 giây):

| Container | Container ID (Sau Deploy) | Image ID (Sau Deploy) | `StartedAt` (UTC) | `Health` (`T+0m` -> `T+5m`) | `RestartCount` (`T+5m`) | Lỗi trong Log (`fail:`/`crit:`/`fatal`) |
|---|---|---|---|---|---|---|
| `phonestore-restore-c2-db-1` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `2026-11-21T00:27:43.48526107Z` | `healthy` -> `healthy` | `0` | `0` |
| `phonestore-restore-c2-api-1` | `cec828cd68c70cd647dda1bed7d6c2d61da6f67ca18eba423d4ce82e798986f3` | `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050` | `2026-11-21T03:53:44.575875686Z` | `healthy` -> `healthy` | `0` | `0` |
| `phonestore-restore-c2-web-1` | `ed6ef0f8027d5ffd50455f9e2db66bc20a386abf7d21ce24bb2646973a46c75b` | `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` | `2026-11-21T03:53:54.556815843Z` | `healthy` -> `healthy` | `0` | `0` |

- Toàn bộ 7 HTTP readiness & catalog endpoints đều phản hồi `HTTP 200` tại đầu và cuối cửa sổ 5 phút.
- RAM Windows Free cuối cửa sổ: `4.56 GiB`; WSL2 `MemAvailable`: `5.68 GiB`; WSL2 `SwapUsed`: `0 MiB`.

---

## J. Rollback Status

- **Rollback Triggered**: **Không (`Not Triggered`)** — cả hai bước deploy `api` và `web` cùng toàn bộ probes và cửa sổ giám sát 5 phút đều đạt `PASS` ngay trong lần chạy, nên hệ thống giữ nguyên trên release mới đã phê duyệt.
- **Rollback Readiness**: Cả hai baseline images (`sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` và `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0`) vẫn được lưu trong local Docker image cache và được ghi nhận trong trường `Transaction.Before` của `journal.jsonl`.

---

## K. Changed Files & Runtime State Files

### 1. Workspace Files
- `scripts/cd/Business.Cd.ps1` (đồng bộ kiểm tra trạng thái Docker `Health == healthy` trong `Assert-CdBusinessHttpReadiness`, sửa ngoặc đơn `-join ''` khi gọi `docker inspect`, và truyền `-SqlOnlyInvariant` cho snapshot/resource gate trong lúc recreate `api`/`web`).
- `scripts/cd/Rehearsal.Probe.ps1` (ghi log `RETRY` khi `$transportResult.Error` báo container đang đợi Docker healthcheck đạt `healthy`).
- `scripts/cd/Rehearsal.ResourceGate.ps1` (bổ sung `-SqlOnlyInvariant` cho `Get-CdBusinessInvariantsSnapshot`, `Assert-CdBusinessInvariants`, `Measure-CdHostResources`, `Assert-CdResourceGate` để luôn kiểm tra nghiêm ngặt `phonestore-restore-c2-db-1` trong khi `api`/`web` đang chuyển trạng thái `starting` -> `healthy`).
- `docs/S35_3J_REAL_BUSINESS_DEPLOYMENT.md` (báo cáo nghiệm thu S35.3J).
- `docs/PROGRESS.md` (cập nhật trạng thái S35.3J).

### 2. Local Runtime State Files (`%LOCALAPPDATA%\PhoneStore-BusinessCd\`)
- `approved-release.json` (bản ghi phê duyệt release `runId = 44845223690`).
- `docker-compose.cd-override.yml` (override digests đã ghim cho `api` và `web`).
- `journal.jsonl` (`Planned -> Before-api -> Ready-api -> Before-web -> Ready-web -> Deployed`).
- `logs\cd-task.log` (nhật ký vận hành CD).

---

## L. Residual Risks & Operational Guidance

1. **Windows Scheduled Task (`\PhoneStore\PhoneStore-Business-PullCd`)**: Theo đúng yêu cầu S35.3J, Scheduled Task **chưa** được đăng ký hay kích hoạt (`Registered PhoneStore scheduled tasks: 0`). Việc triển khai tiếp tục được kiểm soát thủ công qua `Invoke-BusinessCd.ps1 -ValidateOnly` và `Invoke-BusinessCd.ps1 -ApplyApprovedRelease`.
2. **Bảo toàn Rollback Images & External Backup**: Không chạy `docker image prune` hoặc xóa `sha256:91b13b71...` / `sha256:bbcd7442...` và file backup `C:\PhoneStoreDockerTransfer\backup\PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` cho đến khi có bản release kế tiếp và bản backup mới hơn.

---

## M. Final Acceptance Decision: PASS

- **Quyết định nghiệm thu S35.3J**: **PASS (`DEPLOYED`)** — PhoneStore REST API và Customer Web trên stack `phonestore-restore-c2` đã được nâng cấp thành công lên GHCR release `runId = 44845223690` (`commit = 165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`), vận hành ổn định qua cửa sổ giám sát > 5 phút, và đảm bảo an toàn tuyệt đối cho container SQL Server cùng dữ liệu `PhoneStoreDB`.
