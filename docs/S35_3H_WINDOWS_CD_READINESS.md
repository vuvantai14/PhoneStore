# S35.3H — Windows Automated CD Readiness

**PASS cho công cụ Windows pull-based CD (`Business.Cd.ps1`, `Invoke-BusinessCd.ps1`, `Register-BusinessCdTask.ps1`) và bộ kiểm thử `Test-BusinessCdReadiness.ps1`. BLOCKED cho triển khai thực tế lên hệ thống Business cho tới khi bản backup SQL ngày 19/11/2026 được xuất ra ngoài Docker volume.** Thực hiện ngày 2026-11-21 trên nhánh `main` (`165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`). Không deploy business, không tạo hoặc kích hoạt Scheduled Task thật, không thay đổi SQL/containers/volumes business và không commit/push.

## A. Existing CI/CD State

| Thành phần | Trạng thái trước S35.3H | Chi tiết đã xác minh |
| --- | --- | --- |
| **GitHub Actions CI & `publish-ghcr`** (`.github/workflows/ci.yml`) | **HOÀN THIỆN (VERIFIED)** | Mọi action ghim SHA đầy đủ; quyền tối thiểu (`contents: read`, riêng job `publish-ghcr` có `packages: write` khi `push` lên `main`). Kiểm tra build/test Windows, build image Ubuntu từ context sạch (`scripts/ci/prepare_image_context.py`), kiểm tra merged Compose (`scripts/ci/validate_business_compose.py`), push tag `sha-<commit>` lên GHCR, đối chiếu `buildx imagetools inspect` và xuất artifact `phonestore-images-<sha>-run<id>.<attempt>` chứa `images.json`. |
| **Release Manifest & Digests** (`scripts/cd/Rehearsal.Release.ps1`) | **HOÀN THIỆN (VERIFIED)** | Xác minh run `44845223690` (`attempt 1`, commit `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`, artifact digest `sha256:93e32c776343f5b1d86d2a711fb7aa768cb956d8b87bd5d54128de68f24c10b1`), API digest `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050`, Web digest `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76`. |
| **CD Engine, Persistent Journal & Memory Watchdog** (`Rehearsal.Engine.ps1`, `Rehearsal.ResourceGate.ps1`) | **HOÀN THIỆN (VERIFIED ở S35.3G)** | Đã nghiệm thu thực tế trên Docker full-stack ở [S35.3G](S35_3G_REAL_FULLSTACK_REHEARSAL.md) với SQL Server 2022, API và Web thật (`Planned -> Deployed` và `-Recover -> RolledBack`). |
| **Docker Compose Business** (`docker-compose.yml`, `docker-compose.restore.yml`, `docker-compose.net10.yml`) | **HOÀN THIỆN (VERIFIED)** | Project `phonestore-restore-c2`; `db` (`addd99b34bf2`, port `127.0.0.1:14334`, volume `phonestore-restore-c2_mssql_data`), `api` (`b79d2e025e11`, port `127.0.0.1:5025`, image `sha256:91b13b71...`), `web` (`e55bfc7a1117`, port `127.0.0.1:5015`, image `sha256:bbcd7442...`, bind uploads `ro`). |
| **Business Pull-Based CD Runner, SQL External Backup Gate & Windows Scheduled Task Spec** | **CÒN THIẾU TRƯỚC S35.3H → ĐÃ BỔ SUNG** | Đã triển khai `scripts/cd/Business.Cd.ps1`, `scripts/cd/Invoke-BusinessCd.ps1`, `scripts/cd/Register-BusinessCdTask.ps1` và `scripts/cd/Test-BusinessCdReadiness.ps1`. |

## B. Windows Scheduled Task Design

Công cụ `scripts/cd/Register-BusinessCdTask.ps1` và hàm `New-CdBusinessScheduledTaskSpec` / `Test-CdBusinessScheduledTaskSpec` trong `scripts/cd/Business.Cd.ps1` định nghĩa cấu hình Windows Scheduled Task chuẩn pull-based CD:

- **Tên và đường dẫn tác vụ:** `\PhoneStore\PhoneStore-Business-PullCd`.
- **Tài khoản và nguyên tắc quyền tối thiểu (`Least Privilege`):**
  - Chạy dưới tài khoản người vận hành cục bộ hiện tại (`DESKTOP-ES9362P\Admikn`, `IsAdmin = False`) với `RunLevel = Limited` (tuyệt đối từ chối `RunLevel = Highest`, `NT AUTHORITY\SYSTEM`, `LOCAL SERVICE`, `NETWORK SERVICE` hoặc `Administrator`).
  - `LogonType = Interactive` (hoặc `S4U`), `StorePassword = $false`: không lưu mật khẩu Windows, GitHub token hay SQL password dưới dạng plaintext hoặc обратимый.
- **Lịch chạy và chống chồng lấn:**
  - Chu kỳ kiểm tra mặc định mỗi `15 phút` (`PollingIntervalMinutes = 15`, giới hạn `5..1440`), `ExecutionTimeLimitMinutes = 30`, `MultipleInstancesPolicy = IgnoreNew`.
  - Bên trong tiến trình PowerShell, `Invoke-CdBusinessPullCycle` mở khóa độc quyền `%LOCALAPPDATA%\PhoneStore-BusinessCd\deployment.lock` với `[IO.FileShare]::None`; nếu có phiên khác đang giữ khóa, tác vụ ghi log `SKIPPED_LOCKED` và thoát an toàn ngay lập tức.
- **Lệnh thực thi của Task:**
  ```powershell
  powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "C:\PhoneStore\PhoneStore\scripts\cd\Invoke-BusinessCd.ps1" -PollApprovedRelease -ApprovalFile "C:\Users\Admikn\AppData\Local\PhoneStore-BusinessCd\approved-release.json" -StateDirectory "C:\Users\Admikn\AppData\Local\PhoneStore-BusinessCd"
  ```
- **Xử lý sự cố môi trường không gây lỗi dây chuyền:**
  - **Mất mạng / không kết nối được GitHub hoặc GHCR:** Ghi log `DEFERRED_NETWORK_UNAVAILABLE`, giữ nguyên container đang chạy và thoát chờ chu kỳ sau.
  - **Docker Desktop chưa chạy hoặc pipe chưa sẵn sàng:** Ghi log `DEFERRED_DOCKER_UNAVAILABLE` trước khi mở giao dịch journal.
  - **Thiếu RAM / vượt ngưỡng WSL2 Swap:** Ghi log `DEFERRED_LOW_MEMORY` theo `Assert-CdResourceGate` trước khi pull hoặc recreate.
- **Trạng thái đăng ký:** Theo đúng yêu cầu an toàn của S35.3H, script mặc định chạy ở chế độ `-ValidateOnly` và **không tự động đăng ký hay kích hoạt Scheduled Task trên Windows**.

## C. Release Approval

Hệ thống **không bao giờ tự động triển khai mọi image mới xuất hiện trên GHCR**. Một release chỉ được chấp nhận khi thỏa mãn đồng thời hai lớp xác thực:

1. **Hồ sơ phê duyệt cục bộ (`%LOCALAPPDATA%\PhoneStore-BusinessCd\approved-release.json`):**
   - Được kiểm tra nghiêm ngặt qua `Test-CdBusinessApprovalRecord`:
     - `Repository == "vuvantai14/PhoneStore"`, `TargetEnvironment == "phonestore-restore-c2"`, `AutoDeployUnapproved == false`.
     - Ghim cứng `RunId`, `Attempt`, `Commit` (40 ký tự hex), `ArtifactDigest` (`sha256:...`), `ApiDigest` (`sha256:...`), `WebDigest` (`sha256:...`), `ApprovedBy` và `ApprovedAtUtc` (ISO-8601).
     - Chặn mọi thuộc tính hoặc giá trị chứa `password`, `secret`, `token`, `ghp_`, `github_pat_`.
   - Nếu chưa có file `approved-release.json`, chu kỳ poll trả về `NOOP_NO_APPROVAL`. Nếu `RunId` và `Commit` đã được ghi nhận `Deployed` trong `journal.jsonl`, chu kỳ poll trả về `NOOP_ALREADY_DEPLOYED`.
2. **Đối chiếu chéo GitHub Actions CI & GHCR (`Get-CdRelease` + `Assert-CdBusinessReleaseMatchesApproval`):**
   - Xác minh run trên GitHub Actions có đủ 3 job bắt buộc (`Build Solution & Test (Windows)`, `Build Docker Image (Ubuntu)`, `Publish Web and API to GHCR`) đều `conclusion == "success"`.
   - Kiểm tra SHA-256 của file ZIP artifact khớp `ArtifactDigest` đã duyệt trước khi giải nén `images.json`.
   - Đối chiếu `images.api.digest == ApprovalRecord.ApiDigest`, `images.web.digest == ApprovalRecord.WebDigest` và truy vấn `docker buildx imagetools inspect` xác nhận cả tag `sha-<commit>` lẫn reference `@sha256:...` trên GHCR khớp digest `linux/amd64`.

## D. SQL Backup Readiness

Kiểm tra thực tế bằng `Test-CdBusinessSqlBackup` và `docker inspect` / `exec` chỉ đọc trên máy:

| Vị trí Backup | File quan sát được | Kích thước & SHA-256 | Trạng thái `CHECKSUM` / `VERIFYONLY` | Đánh giá cho S35.3H |
| --- | --- | --- | --- | --- |
| **Trong Docker volume SQL** (`/var/opt/mssql/data/` trên `phonestore-restore-c2_mssql_data`) | `PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` | `827.392 bytes`<br/>`AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706` | Tạo ngày 19/11/2026 (S34.2R-04) với `COPY_ONLY, CHECKSUM, COMPRESSION`; đã PASS `RESTORE VERIFYONLY WITH CHECKSUM` (15 Orders, 40 Products). | **Bản mới nhất nhưng còn nằm trong cùng Docker volume với DB.** |
| **Ngoài Docker volume** (`C:\PhoneStoreDockerTransfer\backup`) | `PhoneStoreDB_DOCKER_COPYONLY_20261114_014304166Z_AEB0E519BE794420B7747A5029FE36A5.bak` | `732.160 bytes`<br/>`D806D221FD49FEFB9187C51D6EF0781F653312FF3606726309055F60B007D4E2` | Tạo ngày 14/11/2026 (S31.5C2C.1) với `COPY_ONLY, CHECKSUM`; đã PASS `VERIFYONLY` (12 Orders, 40 Products). | **Bản ngoài volume đã cũ hơn trạng thái dữ liệu hiện hành (12 vs 15 Orders).** |

- **Cơ chế chặn tự động (`Assert-CdBusinessSqlBackup`):** Hàm kiểm tra bắt buộc file `PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` (`827.392 bytes`, SHA-256 `AA76E082...`) phải hiện diện tại thư mục ngoài Docker volume `C:\PhoneStoreDockerTransfer\backup`. Vì thư mục ngoài volume hiện mới chỉ có bản ngày 14/11/2026, cổng `SqlBackupGate` trả về **`BLOCKED_SQL_BACKUP`** và dừng trước mọi bước pull/deploy lên business.
- **Phương án Restore đã tài liệu hóa (chỉ thực hiện khi có phê duyệt khôi phục riêng):**
  1. Thư mục `C:\PhoneStoreDockerTransfer\backup` được mount read-only vào `/var/opt/mssql/backup` theo `docker-compose.restore.yml`.
  2. Kiểm tra tính toàn vẹn trước restore:
     `RESTORE HEADERONLY FROM DISK = N'/var/opt/mssql/backup/<file>.bak';`
     `RESTORE FILELISTONLY FROM DISK = N'/var/opt/mssql/backup/<file>.bak';`
     `RESTORE VERIFYONLY FROM DISK = N'/var/opt/mssql/backup/<file>.bak' WITH CHECKSUM;`
  3. Khôi phục với `WITH MOVE` và `CHECKSUM`, không dùng `REPLACE` trừ khi có quy trình thu hồi/đổi tên DB được phê duyệt riêng.
  4. Quy trình CD chỉ cập nhật container `api` và `web`, **tuyệt đối không chạy EF migrations, không seed và không thay đổi dữ liệu trong `PhoneStoreDB`**.

## E. Business Deployment Safety

Các chốt bảo vệ đã triển khai trong `scripts/cd/Business.Cd.ps1` và `scripts/cd/Rehearsal.Engine.ps1`:

1. **Chỉ dùng approved release digest & kiểm tra baseline rollback:** Kiểm tra cả hai image ID baseline (`sha256:91b13b71...` cho `api`, `sha256:bbcd7442...` cho `web`) có sẵn trong Docker cache cục bộ trước khi ghi `Planned`.
2. **Kiểm tra RAM, WSL2 Swap, Docker health và SQL Business healthy:** Yêu cầu cả 3 container `phonestore-restore-c2-db-1`, `api-1`, `web-1` đang `Running = true`, `Health = healthy`, `RestartCount = 0`, mount `phonestore-restore-c2_mssql_data` nguyên vẹn và vượt qua cổng RAM `DeployStep` (`Windows Free >= 1,90 GiB`, `WSL2 MemAvailable >= 2,40 GiB`, `WSL2 Swap <= 128 MiB`).
3. **Khóa cứng phạm vi lệnh Docker Compose (`Assert-CdBusinessComposeCommandSafety`):**
   - Mọi lệnh cập nhật đều phải có dạng:
     `docker compose --project-name phonestore-restore-c2 -f docker-compose.yml -f docker-compose.restore.yml -f docker-compose.net10.yml -f <cd-override.yml> up -d --no-deps --no-build --pull never <api|web>`
   - Chặn tuyệt đối các lệnh `down`, `prune`, `rm`, `kill`, `stop`, `restart`, `volume`, `system`, `build`, `exec`, `run` và chặn mọi lệnh nhắm vào service `db`.
4. **Thứ tự tuần tự & Readiness Probe thật:**
   - Cập nhật `api` trước -> kiểm tra `http://127.0.0.1:5025/api/health/live`, `/api/health/ready` (`{"status":"healthy"}`) và `/api/v1/products` (`totalItems >= 1`, từ chối mock HTTP 200) -> đối chiếu bất biến SQL container `addd99b34bf2` -> sau đó mới cập nhật `web` -> kiểm tra `http://127.0.0.1:5015/` và `/products` (`text/html` chứa `PhoneStore`).
5. **Rollback & Bảo vệ khi thiếu RAM nguy hiểm:**
   - Nếu deploy hoặc readiness của `api` hay `web` thất bại (và RAM `>= 1,50 GiB`), `Invoke-CdRollback` khôi phục `api` trước rồi `web` về đúng image ID baseline và kiểm tra lại HTTP readiness cùng bất biến SQL trước khi ghi `RolledBack`.
   - Nếu RAM rơi xuống dưới sàn khẩn cấp `< 1,50 GiB`, engine ghi `PausedLowMemory` vào `journal.jsonl`, **bỏ qua việc recreate container để rollback** và giữ nguyên các container business đang chạy để không gây thêm áp lực bộ nhớ.

## F. Validation Results

| Hạng mục kiểm tra | Lệnh / Phạm vi | Kết quả |
| --- | --- | --- |
| **PowerShell Parser** | Parse AST toàn bộ `scripts/cd/*.ps1` (13 file) | **PASS (13/13 file, 0 lỗi cú pháp)** |
| **Scheduled Task Configuration** | `Register-BusinessCdTask.ps1 -ValidateOnly` & `Test-CdBusinessScheduledTaskSpec` | **PASS** (`RunLevel=Limited`, `StorePassword=False`, `MultipleInstancesPolicy=IgnoreNew`; chặn `Highest`, `SYSTEM`, `StorePassword=True`, tham số chứa secret và chặn `-Register` thiếu `-ConfirmTaskRegistration`) |
| **Release Approval & Digest Validation** | `Test-CdBusinessApprovalRecord` & `Assert-CdBusinessReleaseMatchesApproval` | **PASS** (Chấp nhận bản ghi hợp lệ; chặn `AutoDeployUnapproved=True`, chặn token plaintext `ghp_...`, chặn lệch digest giữa manifest và approval, kiểm tra `NOOP_NO_APPROVAL` và `NOOP_ALREADY_DEPLOYED`) |
| **SQL Business Backup Gate** | `Test-CdBusinessSqlBackup` (trực tiếp trên host + mô phỏng) | **PASS** (Phát hiện chính xác thư mục ngoài volume `C:\PhoneStoreDockerTransfer\backup` mới chỉ có bản 14/11/2026 và thiếu bản 19/11/2026 `PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` -> trả về `BLOCKED`; mô phỏng khi đủ file + SHA-256 `AA76E082...` -> trả về `PASS`) |
| **Business Compose Command Safety** | `Assert-CdBusinessComposeCommandSafety` | **PASS** (Chặn `docker compose down`, `system prune`, `up ... db` và lệnh thiếu `--no-deps` / `--pull never`) |
| **Execution Lock & Failure Deferral** | `Invoke-CdBusinessPullCycle` trong `Test-BusinessCdReadiness.ps1` | **PASS** (Khóa `deployment.lock` trả về `SKIPPED_LOCKED`; Docker tắt trả về `DEFERRED_DOCKER_UNAVAILABLE`; RAM `1,75 GiB` trả về `DEFERRED_LOW_MEMORY`; mất mạng trả về `DEFERRED_NETWORK_UNAVAILABLE`) |
| **Sequential Deploy & Rollback Safety** | Mô phỏng `Apply` thành công và `Apply` lỗi Web readiness trong `Test-BusinessCdReadiness.ps1` | **PASS** (Khi Web trả 503, tự động rollback cả `api` và `web` về baseline `sha256:91b13b71...` / `sha256:bbcd7442...` và ghi `RolledBack`; khi thành công, cập nhật theo thứ tự `api -> web` và lần poll kế tiếp trả `NOOP_ALREADY_DEPLOYED`) |
| **`git diff --check` & Whitespace** | Kiểm tra định dạng toàn bộ scripts và tài liệu | **PASS (0 lỗi)** |

## G. Remaining Blockers

1. **Xuất bản sao backup SQL ngày 19/11/2026 ra ngoài Docker volume (`BLOCKER` duy nhất về dữ liệu an toàn trước khi rollout Business):**
   - File `/var/opt/mssql/data/PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` (`827.392 bytes`, SHA-256 `AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706`) đã có `CHECKSUM` và `VERIFYONLY PASS`, nhưng hiện vẫn nằm trong volume `phonestore-restore-c2_mssql_data`. Cần copy file này ra `C:\PhoneStoreDockerTransfer\backup\PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` và đối chiếu SHA-256 trước khi cho phép triển khai lên Business.
2. **Phê duyệt vận hành của người dùng:**
   - Tạo file `%LOCALAPPDATA%\PhoneStore-BusinessCd\approved-release.json` khi người dùng muốn duyệt một release cụ thể và đăng ký Scheduled Task khi người dùng yêu cầu bật chế độ tự động định kỳ.

## H. GO/NO-GO

- **Công cụ Windows Pull-Based CD & Kiểm thử an toàn (S35.3H):** **`PASS / GO`** — Toàn bộ cơ chế kiểm tra approval digest, khóa thực thi, xử lý mất mạng/Docker/RAM, bảo vệ Compose business, rollback tự động và cấu hình Scheduled Task quyền tối thiểu đã hoàn tất và vượt qua kiểm thử.
- **Triển khai thực tế lên hệ thống Business tại thời điểm hiện tại:** **`NO-GO (BLOCKED bởi SQL External Backup Gate)`** — Công cụ tự động chặn rollout business (`BLOCKED_SQL_BACKUP`) cho tới khi file backup mới nhất `PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` được sao chép ra ngoài Docker volume tới `C:\PhoneStoreDockerTransfer\backup` và có phê duyệt rollout business của người dùng.
