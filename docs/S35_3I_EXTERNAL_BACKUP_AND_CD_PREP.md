# S35.3I — SQL External Backup & Business Deployment Preparation

**PASS toàn bộ 10 bước chuẩn bị triển khai Business (`SQL External Backup Export` + `SHA-256 & Size Verification` + `RESTORE VERIFYONLY WITH CHECKSUM` + `SqlBackupGate PASS` + `Invoke-BusinessCd.ps1 -ValidateOnly -> VALIDATED_READY`).** Thực hiện ngày 2026-11-21 trên nhánh `main` (`165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`). Không deploy API/Web business, không restore đè SQL business, không chạy migration/seed, không restart container business, không đăng ký Scheduled Task và không commit/push.

## 1. Container SQL Business & Baseline Invariants (Bước 1 & 8)

Đối chiếu `Get-CdBusinessInvariantsSnapshot` trước và sau toàn bộ thao tác S35.3I xác nhận cả 3 container business giữ nguyên tuyệt đối trạng thái:

| Container Business | Container ID | StartedAt UTC | Restarts | Health | Image | Mounts | Kết luận |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `phonestore-restore-c2-db-1` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | `2026-11-21T00:27:43.48526107Z` | `0` | `healthy` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:True` | **Khớp 100%** |
| `phonestore-restore-c2-api-1` | `b79d2e025e11971b7f5fa5a132919bc2ea564a7753e5d22a3c1401d505b67239` | `2026-11-21T00:27:43.503227531Z` | `0` | `healthy` | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` | `[]` | **Khớp 100%** |
| `phonestore-restore-c2-web-1` | `e55bfc7a111747f7db0c06cfb9645a29b352fe863d8a8ea16dea6964192a6839` | `2026-11-21T00:27:43.55036162Z` | `0` | `healthy` | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` | `bind::/app/wwwroot/uploads/products:False` | **Khớp 100%** |

## 2. Sao chép & Xác minh Bản Backup Ngày 19/11/2026 Ngoài Docker Volume (Bước 2–6)

| Hạng mục | Bản gốc trong Docker Volume (`phonestore-restore-c2_mssql_data`) | Bản sao ngoài Docker Volume (Windows Host) | Kết quả đối chiếu |
| --- | --- | --- | --- |
| **Đường dẫn** | `/var/opt/mssql/data/PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` | `C:\PhoneStoreDockerTransfer\backup\PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` | **Cả bản gốc và bản sao đều được giữ nguyên** (cùng bản ngày 14/11/2026 `732.160 bytes`) |
| **Kích thước (Bytes)** | `827.392` | `827.392` | **Khớp 100%** |
| **SHA-256** | `AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706` | `AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706` | **Khớp 100%** |
| **`RESTORE VERIFYONLY WITH CHECKSUM`** | `The backup set on file 1 is valid.` | `The backup set on file 1 is valid.` *(đọc trực tiếp bytes của file ngoài volume đưa vào `/tmp/verify_external_s353i.bak`, kiểm tra `sha256sum`, chạy `RESTORE VERIFYONLY WITH CHECKSUM` rồi xóa file tạm `/tmp`)* | **PASS (Chỉ đọc, không restore đè DB)** |

## 3. Kết quả SQL External Backup Gate & Business CD Preflight (Bước 7 & 9)

- **Chạy lại `Assert-CdBusinessSqlBackup` (Bước 7):**
  ```json
  {"Ready":true,"Status":"PASS","ExternalBackupFound":true,"BackupPath":"C:\\PhoneStoreDockerTransfer\\backup\\PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak","ActualSize":827392,"ActualSha256":"AA76E08284C319312B301AF267562400CF5ABC4926E16E57E3CC9D190580A706","Reason":"Verified external SQL backup outside Docker volume (PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak, 827392 bytes, CHECKSUM + VERIFYONLY)."}
  ```
- **Kiểm tra Release Approval, Baseline Images & Live Preflight (`Invoke-BusinessCd.ps1 -ValidateOnly` — Bước 9):**
  - Hồ sơ phê duyệt `%LOCALAPPDATA%\PhoneStore-BusinessCd\approved-release.json` cho run `44845223690` (`attempt 1`, commit `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`, artifact digest `sha256:93e32c776343f5b1d86d2a711fb7aa768cb956d8b87bd5d54128de68f24c10b1`): **PASS** (`ApprovalCheck = PASS`).
  - Docker Desktop & 3 Business Containers (`db`, `api`, `web`): **PASS** (`DockerPreflight = PASS`).
  - Phased Tiered RAM Gate (`DeployStep/Pre`): **PASS** (`winFreeGiB = 4,63 GiB >= 1,90 GiB`, `wslAvailGiB = 5,69 GiB >= 2,40 GiB`, `wslSwapMiB = 0`).
  - SQL External Backup Gate (`SqlBackupGate`): **PASS** (đã gỡ hoàn toàn `BLOCKED_SQL_BACKUP`).
  - Đối chiếu GitHub Actions API & GHCR Manifest (`ReleaseVerify`): **PASS** (`api = sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050`, `web = sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76`).
  - Rollback Baseline Images cục bộ: **PASS** (`api = sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a`, `web = sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0`).
  - Kết quả tổng thể của `Invoke-BusinessCd.ps1 -ValidateOnly`: **`Outcome = "VALIDATED_READY"`**.
- **Chạy lại bộ kiểm thử `Test-BusinessCdReadiness.ps1`:** **PASS 100%** (Parser 13/13 scripts, Scheduled Task config, Release approval/digest, Live SQL Backup Gate PASS + Simulated missing BLOCKED, Business Compose safety, Execution lock & Rollback safety).

## 4. GO/NO-GO cho Bước Triển Khai Business Tiếp Theo

- **Quyết định: `GO` cho bước triển khai PhoneStore Business tiếp theo.**
- Blocker `BLOCKED_SQL_BACKUP` đã được gỡ hoàn toàn với bản sao backup ngày 19/11/2026 (`827.392 bytes`, SHA-256 khớp 100%, `RESTORE VERIFYONLY WITH CHECKSUM` hợp lệ) nằm ngoài Docker volume tại `C:\PhoneStoreDockerTransfer\backup`, và toàn bộ chuỗi preflight của `Invoke-BusinessCd.ps1 -ValidateOnly` đã đạt trạng thái **`VALIDATED_READY`**.
