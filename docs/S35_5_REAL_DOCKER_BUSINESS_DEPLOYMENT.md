# S35.5 — PhoneStore Real Docker Business Deployment (`phonestore-restore-c2`)

- **Ngày thực hiện**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`main`)
- **Target Docker Project**: `phonestore-restore-c2`
- **Source Release**: GitHub Actions `PhoneStore CI — Run #31` ([Run 44891116359](https://github.com/vuvantai14/PhoneStore/actions/runs/44891116359)), commit `0e449a9ee7080f788385738c4b8c1d864a85692c` (`feat(api): complete REST API and Admin integration`)
- **Final Outcome**: **`DEPLOYED` (`VERIFIED — PASS`)**

---

## A. User Approval Scope

Người dùng đã phê duyệt thực thi **S35.5 — Real Docker Business Deployment** trong phạm vi có kiểm soát:

- **Được phép thực hiện**:
  1. Tạo và xác minh bản backup SQL business mới (`FULL`, `COPY_ONLY`, `COMPRESSION`, `CHECKSUM`) lưu ngoài Docker volume tại `C:\PhoneStoreDockerTransfer\backup`.
  2. Áp dụng duy nhất migration đã phê duyệt `20261121044020_AddApiRefreshTokens` lên database `PhoneStoreDB` business.
  3. Cấu hình xác thực `LocalJwt` cho `PhoneStore.Api` business với `SigningKey` ngẫu nhiên mật mã (`>= 32 bytes`) lưu ngoài Git và khóa quyền truy cập Windows ACL.
  4. Cấu hình shared product image storage (`/app/wwwroot/uploads/products`) giữa `api` (`read_only: false` / `rw`) và `web` (`read_only: true` / `ro`) trên cùng thư mục host hiện có.
  5. Pull và triển khai tuần tự `api` -> `web` theo đúng digest GHCR của commit `0e449a9ee7080f788385738c4b8c1d864a85692c` (`Run #31 — 44891116359`).
  6. Thực hiện smoke tests và giám sát ổn định liên tục `> 5 phút`.
- **Tuân thủ tuyệt đối các giới hạn cấm**:
  - Không dừng, restart hoặc recreate container SQL `phonestore-restore-c2-db-1`.
  - Không drop/reset database, không xóa volume `phonestore-restore-c2_mssql_data`, không seed/sửa dữ liệu business, không tạo Admin thứ hai.
  - Không xóa ảnh cũ trong `uploads/products`, không xóa rollback images, không chạy `docker system prune`.
  - Không đăng ký hoặc kích hoạt Windows Scheduled Task.
  - Không commit/push.

---

## B. Target Verification

Kết quả kiểm tra thực tế stack Docker business trước khi thao tác:

| Hạng mục | Giá trị kỳ vọng | Giá trị xác minh thực tế | Kết quả |
|---|---|---|---|
| Compose Project | `phonestore-restore-c2` | `com.docker.compose.project = phonestore-restore-c2` | **PASS** |
| Services | `db`, `api`, `web` | `phonestore-restore-c2-db-1`, `phonestore-restore-c2-api-1`, `phonestore-restore-c2-web-1` | **PASS** |
| SQL Database | `PhoneStoreDB` | `PhoneStoreDB` (`ONLINE`, compatibility level `160`) | **PASS** |
| SQL Volume | `phonestore-restore-c2_mssql_data` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:rw` | **PASS** |
| Docker Network | `phonestore-restore-c2_phonestore-net` | `phonestore-restore-c2_phonestore-net` (bridge) | **PASS** |
| Host Ports | Web `5015`, API `5025`, SQL `14334` | Web `127.0.0.1:5015->8080`, API `127.0.0.1:5025->8080`, SQL `127.0.0.1:14334->1433` | **PASS** |
| Compose Config Files | 4 files hợp nhất | `docker-compose.yml`, `docker-compose.restore.yml`, `docker-compose.net10.yml`, `%LOCALAPPDATA%\PhoneStore-BusinessCd\docker-compose.cd-override.yml` | **PASS** |

---

## C. GHCR Release Identity

Xác minh trực tiếp qua GitHub Actions REST API, artifact manifest `images.json` và GHCR registry (`docker buildx imagetools inspect`):

| Thuộc tính | Giá trị xác minh |
|---|---|
| **Workflow Run** | `PhoneStore CI — Run #31` (`run_id = 44891116359`, `run_attempt = 1`, `event = push`, `head_branch = main`) |
| **Commit SHA** | `0e449a9ee7080f788385738c4b8c1d864a85692c` (`feat(api): complete REST API and Admin integration`) |
| **Job 1** | `Build Solution & Test (Windows)` — `completed / success` |
| **Job 2** | `Build Docker Image (Ubuntu)` — `completed / success` |
| **Job 3** | `Publish Web and API to GHCR` — `completed / success` |
| **Manifest Artifact** | `phonestore-images-0e449a9ee7080f788385738c4b8c1d864a85692c-run44891116359.1` (`448` bytes, `expired = false`) |
| **Artifact SHA-256** | `sha256:25c7dc73f57fb8384907400d9331c5c5143241cc9d2c69e6d20d8c0ed7126912` (khớp GitHub metadata & ZIP tải về) |
| **API Image Tag** | `ghcr.io/vuvantai14/phonestore-api:sha-0e449a9ee7080f788385738c4b8c1d864a85692c` |
| **API Image Digest** | `sha256:7ee39dc64cd0b026c26d0016f49f736fff77050f1dddef3144c3ad77267149fb` |
| **API Pinned Reference** | `ghcr.io/vuvantai14/phonestore-api@sha256:7ee39dc64cd0b026c26d0016f49f736fff77050f1dddef3144c3ad77267149fb` (`linux/amd64`) |
| **Web Image Tag** | `ghcr.io/vuvantai14/phonestore-web:sha-0e449a9ee7080f788385738c4b8c1d864a85692c` |
| **Web Image Digest** | `sha256:6a9683b7d23f7aa2a409b73e918c402a401d68d8804747805aa182badee3a457` |
| **Web Pinned Reference** | `ghcr.io/vuvantai14/phonestore-web@sha256:6a9683b7d23f7aa2a409b73e918c402a401d68d8804747805aa182badee3a457` (`linux/amd64`) |

---

## D. Pre-Deployment Snapshot

### 1. Host Resources & Disk

- **Windows Free RAM**: `4.73 GiB` (trước backup) / `5.50 GiB` (trước image pull)
- **WSL2 MemAvailable**: `5.61 GiB`
- **WSL2 Swap Used**: `0 MiB`
- **Disk C Free Space**: `313.61 GiB`
- **Git Working Tree**: Sạch (`HEAD = 0e449a9ee7080f788385738c4b8c1d864a85692c`)

### 2. Baseline Containers & Rollback Images

| Container | Container ID (Trước) | Image Digest (Trước / Rollback Baseline) | `StartedAt` (UTC) | `Health` / `Restarts` | Mounts (Trước) |
|---|---|---|---|---|---|
| `phonestore-restore-c2-db-1` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `2026-11-21T00:27:43.48526107Z` | `healthy` / `0` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:rw` |
| `phonestore-restore-c2-api-1` | `cec828cd68c70cd647dda1bed7d6c2d61da6f67ca18eba423d4ce82e798986f3` | `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050` | `2026-11-21T03:53:44.575875686Z` | `healthy` / `0` | *(none)* |
| `phonestore-restore-c2-web-1` | `ed6ef0f8027d5ffd50455f9e2db66bc20a386abf7d21ce24bb2646973a46c75b` | `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` | `2026-11-21T03:53:54.556815843Z` | `healthy` / `0` | `bind:C:/PhoneStore/PhoneStore/src/PhoneStore.Web/wwwroot/uploads/products:/app/wwwroot/uploads/products:ro` |

- Toàn bộ 4 rollback images (`sha256:bf987253...`, `sha256:7e2970be...`, `sha256:91b13b71...`, `sha256:bbcd7442...`) đều có sẵn trong local Docker cache và bản sao cấu hình baseline đã được lưu tại `%LOCALAPPDATA%\PhoneStore-BusinessCd\rollback-baseline-s355`.

---

## E. SQL Backup Verification

Đã tạo bản backup mới cho `PhoneStoreDB` ngay trước khi chạy migration:

| Thuộc tính Backup | Giá trị xác minh |
|---|---|
| **Loại Backup** | `FULL` (`BackupType = 1`), `COPY_ONLY = 1`, `COMPRESSION = 1` (`MS_XPRESS`), `CHECKSUM = 1` (`HasBackupChecksums = 1`, `IsDamaged = 0`) |
| **Database / Compatibility** | `PhoneStoreDB` / `160` (`SQL Server 2022`, `850` pages = `848` data + `2` log) |
| **Backup Timestamp (UTC)** | `2026-11-21T09:58:50Z` |
| **Container Backup Path** | `/var/opt/mssql/data/PhoneStoreDB_S35_5_PREMIG_COPYONLY_20261121T095850341Z.bak` |
| **External Host Backup Path** | `C:\PhoneStoreDockerTransfer\backup\PhoneStoreDB_S35_5_PREMIG_COPYONLY_20261121T095850341Z.bak` |
| **File Size** | `847,872` bytes (`BackupSize = 7,041,024` bytes, `CompressedBackupSize = 831,411` bytes) |
| **SHA-256 Checksum** | `2D99C78D309DDED377736834DF087063A4A6694B287E9899EA761EBE026A4ACA` (khớp 100% giữa trong container và ngoài Windows host) |
| **Verification (`VERIFYONLY`)** | `RESTORE VERIFYONLY FROM DISK = ... WITH CHECKSUM` -> **`The backup set on file 1 is valid.`**; `Test-CdBusinessSqlBackup` -> **`PASS`** |

*(Các bản backup trước đó ngày 14/11 và 19/11 cùng bản backup mới ngày 21/11 đều được giữ nguyên tại `C:\PhoneStoreDockerTransfer\backup`).*

---

## F. Migration Result

Đã áp dụng có kiểm soát trong giao dịch (`SET XACT_ABORT ON; BEGIN TRANSACTION ... COMMIT`) duy nhất migration `20261121044020_AddApiRefreshTokens`:

1. **`__EFMigrationsHistory` (Trước -> Sau)**:
   - Trước: `20261001150857_InitialCreate` (`8.0.28`), `20261102122856_EnforceSingleAdmin` (`8.0.28`).
   - Sau: Thêm đúng 1 dòng `20261121044020_AddApiRefreshTokens` (`10.0.15`).
2. **Schema & Indexes của bảng `dbo.ApiRefreshTokens`**:
   - Primary Key: `PK_ApiRefreshTokens` (`CLUSTERED`, `UNIQUE = 1`)
   - Unique TokenHash Index: `UX_ApiRefreshTokens_TokenHash` (`NONCLUSTERED`, `UNIQUE = 1`)
   - Family Index: `IX_ApiRefreshTokens_FamilyId` (`NONCLUSTERED`, `UNIQUE = 0`)
   - User Index: `IX_ApiRefreshTokens_UserId` (`NONCLUSTERED`, `UNIQUE = 0`)
   - Foreign Key: `FK_ApiRefreshTokens_Users_UserId` (`REFERENCES dbo.Users(UserId) ON DELETE CASCADE`)
3. **Backward Compatibility Check**:
   - Ngay sau khi migration hoàn tất (trước khi recreate `api`/`web`), các container baseline `api` và `web` cũ vẫn vượt qua `Assert-CdBusinessHttpReadiness` (`HTTP 200`), chứng minh schema mới tương thích ngược hoàn toàn nếu cần rollback.

---

## G. JWT Configuration

Cấu hình Local JWT production cho `phonestore-restore-c2-api-1`:

- **File cấu hình ngoài Git**: `C:\Users\Admikn\AppData\Local\PhoneStore-BusinessCd\api-jwt.env`
- **Bảo vệ quyền truy cập (Windows ACL)**: Đã khóa bằng `icacls /inheritance:r /grant:r "DESKTOP-ES9362P\Admikn:(F)"` (chỉ tài khoản operator hiện tại có quyền đọc/ghi).
- **Thông số phi bí mật**:
  - `ASPNETCORE_ENVIRONMENT=Production` (đặt cả ở `environment` của `api` trong `docker-compose.cd-override.yml` để ghi đè biến mặc định `Development` trong `.env`, đảm bảo tắt Swagger ở Production trong khi giữ `web` ở cấu hình cookie `SameAsRequest` trên `http://localhost:5015`)
  - `ApiAuthentication__Mode=LocalJwt`
  - `ApiAuthentication__LocalJwt__Issuer=https://auth.phonestore.local`
  - `ApiAuthentication__LocalJwt__Audience=phonestore-api`
  - `ApiAuthentication__LocalJwt__SigningKey`: Sinh ngẫu nhiên bằng `RandomNumberGenerator` (`48` random bytes -> `64` ký tự Base64Url `>= 32 bytes`, `43` ký tự phân biệt `>= 12`, không in ra terminal/log/report)
  - `ApiAuthentication__LocalJwt__AccessTokenMinutes=15`
  - `ApiAuthentication__LocalJwt__RefreshTokenDays=7`
  - `ApiAuthentication__LocalJwt__KeyId=biz-hs256-v1`
- **Topology HTTPS**:
  - Stack hiện tại bind cổng loopback cục bộ `127.0.0.1:5025->8080` và mạng nội bộ `phonestore-restore-c2_phonestore-net` (`http://api:8080/` cho `web`), chưa đặt sau Reverse Proxy TLS bên ngoài. Giữ nguyên cấu hình mặc định `RequireHttps=false`, không giả mạo `X-Forwarded-Proto` và không mở port ra mạng công cộng.

---

## H. Shared Uploads

Cấu hình lưu trữ ảnh sản phẩm dùng chung giữa `api` và `web`:

| Thuộc tính | Giá trị xác minh |
|---|---|
| **Host Source Directory** | `C:\PhoneStore\PhoneStore\src\PhoneStore.Web\wwwroot\uploads\products` |
| **Physical Backup Path** | `C:\PhoneStoreDockerTransfer\backup\uploads_s35_5_baseline` (`42` files, `3,234,445` bytes) |
| **Baseline Manifest SHA-256** | `2694C6210298E538F6BB9D53CEC28C1EB683230CD8A934F70DEDC1A4BD07A830` (`42` files) |
| **API Container Mount** | `type=bind`, `source=C:/PhoneStore/PhoneStore/src/PhoneStore.Web/wwwroot/uploads/products`, `target=/app/wwwroot/uploads/products`, **`RW = True` (`read_only: false`)** |
| **Web Container Mount** | `type=bind`, `source=C:/PhoneStore/PhoneStore/src/PhoneStore.Web/wwwroot/uploads/products`, `target=/app/wwwroot/uploads/products`, **`RW = False` (`read_only: true`)** |
| **Runtime Permission Probe** | `api` tạo & xóa tệp probe tạm `.s355-rw-probe.tmp` thành công (`ExitCode = 0`); `web` thử `touch .s355-ro-probe.tmp` bị từ chối (`ExitCode = 1`, `Read-only file system`); toàn bộ `42` file ảnh gốc giữ nguyên SHA-256 manifest `2694C621...A830`. |

---

## I. API/Web Deployment

Thực thi triển khai tuần tự có kiểm soát qua `Assert-CdBusinessComposeCommandSafety` (`docker compose --project-name phonestore-restore-c2 ... up -d --no-deps --no-build --pull never <api|web>`) và ghi nhận đầy đủ vào `%LOCALAPPDATA%\PhoneStore-BusinessCd\journal.jsonl`:

1. **Pull & Verify Pinned Digests (`10:05:54Z` – `10:06:08Z`)**:
   - `ImagePull/Pre`: `WinFree = 5.50 GiB`, `WslAvail = 5.61 GiB`, `WslSwap = 0 MiB` (**PASS**).
   - Pull `ghcr.io/vuvantai14/phonestore-api@sha256:7ee39dc64cd0b026c26d0016f49f736fff77050f1dddef3144c3ad77267149fb` -> Local Image ID khớp `sha256:7ee39dc64cd0b026c26d0016f49f736fff77050f1dddef3144c3ad77267149fb`.
   - Pull `ghcr.io/vuvantai14/phonestore-web@sha256:6a9683b7d23f7aa2a409b73e918c402a401d68d8804747805aa182badee3a457` -> Local Image ID khớp `sha256:6a9683b7d23f7aa2a409b73e918c402a401d68d8804747805aa182badee3a457`.
2. **Step A — Recreate `api` (`10:06:09Z` & cập nhật override `ASPNETCORE_ENVIRONMENT=Production` lúc `10:09:05Z`)**:
   - Container `phonestore-restore-c2-api-1` mới (`Id = 12a06d6e669428ff0894b8972571046d4f32de8c886962ff4cf543eb10acc8d2`) đạt `healthy` ở attempt `4/20` (`10:09:12Z`), `RestartCount = 0`.
   - Container SQL `phonestore-restore-c2-db-1` giữ nguyên 100% ID (`addd99b34bf2...`) và `StartedAt` (`2026-11-21T00:27:43.48526107Z`).
3. **Step B — Recreate `web` (`10:06:18Z` – `10:06:29Z`)**:
   - Container `phonestore-restore-c2-web-1` mới (`Id = 3595f703f7a5ebeeff133528fa6b4be4a996b7b45c7fc4b728dc64d10cd12b8f`) đạt `healthy` ở attempt `5/20` (`10:06:28Z`), `RestartCount = 0`.
   - Ghi nhận journal `Planned -> Before-api -> Ready-api -> Before-web -> Ready-web -> Deployed`.

### Trạng thái Containers Sau Triển Khai

| Container | Container ID (Sau) | Image Digest (Sau) | `StartedAt` (UTC) | `Health` / `Restarts` | Mounts (Sau) |
|---|---|---|---|---|---|
| `phonestore-restore-c2-db-1` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` *(không đổi)* | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `2026-11-21T00:27:43.48526107Z` *(không đổi)* | `healthy` / `0` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:rw` |
| `phonestore-restore-c2-api-1` | `12a06d6e669428ff0894b8972571046d4f32de8c886962ff4cf543eb10acc8d2` | `sha256:7ee39dc64cd0b026c26d0016f49f736fff77050f1dddef3144c3ad77267149fb` | `2026-11-21T10:09:05.598296081Z` | `healthy` / `0` | `bind:C:/PhoneStore/PhoneStore/src/PhoneStore.Web/wwwroot/uploads/products:/app/wwwroot/uploads/products:rw` |
| `phonestore-restore-c2-web-1` | `3595f703f7a5ebeeff133528fa6b4be4a996b7b45c7fc4b728dc64d10cd12b8f` | `sha256:6a9683b7d23f7aa2a409b73e918c402a401d68d8804747805aa182badee3a457` | `2026-11-21T10:06:19.636571761Z` | `healthy` / `0` | `bind:C:/PhoneStore/PhoneStore/src/PhoneStore.Web/wwwroot/uploads/products:/app/wwwroot/uploads/products:ro` |

---

## J. Smoke Tests

Toàn bộ **29/29** kiểm tra smoke test sau triển khai đều đạt **PASS** (không ghi hay tạo dữ liệu test trong `PhoneStoreDB` business):

| Nhóm | Endpoint / Kiểm tra | HTTP / Kết quả | Content-Type / Header | Payload / Chi tiết |
|---|---|---|---|---|
| **Public Web** | `GET http://127.0.0.1:5015/` | **`200`** | `text/html; charset=utf-8` | `43,974` bytes |
| **Public Web** | `GET http://127.0.0.1:5015/products` | **`200`** | `text/html; charset=utf-8` | `39,383` bytes |
| **Public Web** | `GET http://127.0.0.1:5015/products/40` | **`200`** | `text/html; charset=utf-8` | `20,257` bytes (gọi qua `http://api:8080/api/v1/products/40`) |
| **Public Web** | `GET http://127.0.0.1:5015/products/9` | **`200`** | `text/html; charset=utf-8` | `20,265` bytes |
| **Public Web** | `GET http://127.0.0.1:5015/uploads/products/samsung-galaxy-a35.jpg` | **`200`** | `image/jpeg` | `60,612` bytes |
| **Public Web** | `GET http://127.0.0.1:5015/uploads/products/samsung-galaxy-a25.jpg` | **`200`** | `image/jpeg` | `64,212` bytes |
| **REST API** | `GET http://127.0.0.1:5025/api/health/live` | **`200`** | `application/json; charset=utf-8` | `{"status":"healthy"}` |
| **REST API** | `GET http://127.0.0.1:5025/api/health/ready` | **`200`** | `application/json; charset=utf-8` | `{"status":"healthy"}` |
| **REST API** | `GET http://127.0.0.1:5025/api/v1/products` | **`200`** | `application/json; charset=utf-8` | `1,890` bytes |
| **REST API** | `GET http://127.0.0.1:5025/api/v1/products/40` | **`200`** | `application/json; charset=utf-8` | `516` bytes |
| **REST API** | `GET http://127.0.0.1:5025/api/v1/products/bestsellers?count=4` | **`200`** | `application/json; charset=utf-8` | `726` bytes |
| **REST API** | `GET http://127.0.0.1:5025/api/v1/products/newest?count=4` | **`200`** | `application/json; charset=utf-8` | `729` bytes |
| **REST API** | `GET http://127.0.0.1:5025/api/v1/products/40/related` | **`200`** | `application/json; charset=utf-8` | `1,100` bytes |
| **REST API** | `GET http://127.0.0.1:5025/api/v1/brands` | **`200`** | `application/json; charset=utf-8` | `198` bytes (`8` brands) |
| **REST API** | `GET http://127.0.0.1:5025/api/v1/categories` | **`200`** | `application/json; charset=utf-8` | `93` bytes (`2` categories) |
| **Prod Hardening** | `GET http://127.0.0.1:5025/swagger/index.html` | **`404`** | *(disabled in Production)* | Swagger UI tắt hoàn toàn |
| **Prod Hardening** | `GET http://127.0.0.1:5025/swagger/v1/swagger.json` | **`404`** | *(disabled in Production)* | OpenAPI JSON tắt hoàn toàn |
| **JWT Security** | `POST /api/v1/auth/login` (email không tồn tại) | **`401`** | `Cache-Control: no-store` | Xác nhận `Mode=LocalJwt` đang hoạt động (không trả `503`) |
| **JWT Security** | Anonymous -> `GET /api/v1/admin/{dashboard,products,orders,customers}` | **`401`** (x4) | `application/problem+json`, `no-store` | Chặn truy cập trái phép |
| **JWT Security** | Anonymous -> `GET /api/v1/orders` & `GET /api/v1/profile` | **`401`** (x2) | `application/problem+json`, `no-store` | Chặn truy cập trái phép |
| **Role Isolation** | In-memory Customer JWT (`UserId=2`) -> `GET /api/v1/profile` & `/api/v1/orders` | **`200`** (x2) | `Cache-Control: no-store` | Xác thực HS256 + `ActiveRoleHandler` (`Customer`) thành công, `0` DB write |
| **Role Isolation** | In-memory Customer JWT (`UserId=2`) -> `GET /api/v1/admin/{dashboard,products,orders,customers}` | **`403`** (x4) | `application/problem+json`, `no-store` | Customer bị từ chối quyền Admin (`403 Forbidden`) |

---

## K. Observation (`> 5 Minutes` Continuous Stability Window)

Giám sát liên tục từ `2026-11-21T10:11:02Z` đến `2026-11-21T10:16:36Z` (**5 phút 34 giây** cửa sổ quan sát; tổng thời gian chạy liên tục sau recreate là **> 7 phút 30 giây** với `api` và **> 10 phút 16 giây** với `web`):

| Mẫu quan sát | Thời điểm (UTC) | `db` / `api` / `web` Health & Restarts | HTTP Probes (`api` + `web`) | `WinFree` | `WslAvail` | `WslSwap` | Severe Logs |
|---|---|---|---|---|---|---|---|
| `#1/6` | `10:11:02Z` | `healthy(0)` / `healthy(0)` / `healthy(0)` | `200` / `200` | `5.24 GiB` | `5.59 GiB` | `0 MiB` | `0` |
| `#2/6` | `10:12:08Z` | `healthy(0)` / `healthy(0)` / `healthy(0)` | `200` / `200` | `5.04 GiB` | `5.62 GiB` | `0 MiB` | `0` |
| `#3/6` | `10:13:15Z` | `healthy(0)` / `healthy(0)` / `healthy(0)` | `200` / `200` | `5.40 GiB` | `5.63 GiB` | `0 MiB` | `0` |
| `#4/6` | `10:14:21Z` | `healthy(0)` / `healthy(0)` / `healthy(0)` | `200` / `200` | `4.88 GiB` | `5.63 GiB` | `0 MiB` | `0` |
| `#5/6` | `10:15:28Z` | `healthy(0)` / `healthy(0)` / `healthy(0)` | `200` / `200` | `4.97 GiB` | `5.63 GiB` | `0 MiB` | `0` |
| `#6/6` | `10:16:34Z` | `healthy(0)` / `healthy(0)` / `healthy(0)` | `200` / `200` | `4.98 GiB` | `5.63 GiB` | `0 MiB` | `0` |

- Quét bảo mật trên toàn bộ `221,996` ký tự Docker logs của `api` và `web`: **`0` rò rỉ `SigningKey`, password hoặc token**.

---

## L. Rollback Status

- **Không kích hoạt rollback** vì toàn bộ các cổng kiểm tra, triển khai tuần tự, smoke tests và cửa sổ quan sát > 5 phút đều đạt **PASS 100%**.
- Toàn bộ rollback baseline images (`sha256:bf987253...`, `sha256:7e2970be...`, `sha256:91b13b71...`, `sha256:bbcd7442...`), bản backup cấu hình tại `%LOCALAPPDATA%\PhoneStore-BusinessCd\rollback-baseline-s355` và bản backup SQL tại `C:\PhoneStoreDockerTransfer\backup\PhoneStoreDB_S35_5_PREMIG_COPYONLY_20261121T095850341Z.bak` vẫn được giữ nguyên để sẵn sàng phục hồi bất cứ lúc nào.

---

## M. Business Data Integrity

Đối chiếu số lượng bản ghi trong `PhoneStoreDB` và thư mục `uploads/products` trước và sau triển khai:

| Bảng / Tài nguyên | Trước triển khai (Pre-Migration) | Sau triển khai & 5m Observation | Trạng thái |
|---|---|---|---|
| `Roles` | `2` | `2` | **Khớp 100%** |
| `Users` | `3` | `3` | **Khớp 100%** |
| `ActiveAdmins` (`RoleId=1, IsActive=1`) | `1` | `1` (`UX_Users_SingleAdmin` intact) | **Khớp 100%** |
| `Customers` (`RoleId=2`) | `2` | `2` | **Khớp 100%** |
| `Brands` | `8` | `8` | **Khớp 100%** |
| `Categories` | `2` | `2` | **Khớp 100%** |
| `Products` (`ActiveProducts`) | `40` (`40`) | `40` (`40`) | **Khớp 100%** |
| `ProductImages` | `41` | `41` | **Khớp 100%** |
| `Orders` | `15` | `15` | **Khớp 100%** |
| `OrderDetails` | `37` | `37` | **Khớp 100%** |
| `ApiRefreshTokens` *(bảng mới)* | *(chưa có)* | `0` (4 indexes + FK verified) | **Hợp lệ (Additive)** |
| `__EFMigrationsHistory` | `2` migrations | `3` migrations (`+20261121044020_AddApiRefreshTokens`) | **Hợp lệ** |
| `wwwroot/uploads/products` | `42` files (`SHA-256: 2694C621...A830`) | `42` files (`SHA-256: 2694C621...A830`) | **Khớp 100%** |

---

## N. Remaining Risks

1. **Admin WinForms Desktop (`RestApi` mode)**:
   - Mặc định `src/PhoneStore.Admin/appsettings.json` vẫn giữ `AdminDataAccess:Mode = DirectSql` để đảm bảo an toàn tối đa. Khi người dùng muốn chuyển ứng dụng Admin WinForms trên máy trạm sang gọi trực tiếp `http://localhost:5025/`, chỉ cần đặt `AdminDataAccess:Mode = RestApi` và `AdminDataAccess:ApiBaseUrl = http://localhost:5025/`.
2. **Bảo quản file cấu hình ngoài Git**:
   - File `C:\Users\Admikn\AppData\Local\PhoneStore-BusinessCd\api-jwt.env` và `docker-compose.cd-override.yml` nằm ngoài Git để bảo vệ `SigningKey`. Nếu chạy lại `Invoke-BusinessCd.ps1 -ApplyApprovedRelease` cho một release tương lai, cần lưu ý giữ các dòng `env_file`, `environment` (`ASPNETCORE_ENVIRONMENT: Production`) và `volumes` (`uploads/products`) trong `Business.Cd.ps1` hoặc `docker-compose.cd-override.yml`.
3. **Reverse Proxy TLS (Khi mở rộng ra ngoài localhost)**:
   - Hiện tại `api` (`127.0.0.1:5025`) và `web` (`127.0.0.1:5015`) chỉ bind trên loopback cục bộ. Nếu triển khai ra mạng ngoài qua Reverse Proxy TLS trong tương lai, cần bật `ApiOperational__RequireHttps=true` và cấu hình `TrustedProxies`/`TrustedProxyNetworks`.

---

## O. Final Decision

**`DEPLOYED` — Triển khai thực tế phiên bản PhoneStore REST API hoàn chỉnh (`Run #31 — 44891116359`, commit `0e449a9ee7080f788385738c4b8c1d864a85692c`) lên môi trường Docker Business (`phonestore-restore-c2`) thành công 100%, an toàn dữ liệu tuyệt đối.**
