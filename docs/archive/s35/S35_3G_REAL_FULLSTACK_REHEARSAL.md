# S35.3G — Real Docker Full-Stack Rehearsal

**PASS toàn bộ chu trình diễn tập Full-stack Docker thật (`SQL Server 2022 fixture` + `PhoneStore.Api` + `PhoneStore.Web` + `GHCR Release Deployment` + `Real Rollback & Recovery` + `Memory Watchdog` + `Cleanup`).** Thực hiện ngày 2026-11-21 trên nhánh `main` (`165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`). Toàn bộ 3 container business và volume `phonestore-restore-c2_mssql_data` giữ nguyên tuyệt đối 100% trạng thái trước, trong và sau diễn tập.

## A. Runtime Before

- **Git HEAD & Branch:** `main` (`origin/main`) tại commit `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`.
- **Trạng thái Business Runtime trước diễn tập:**
  - `phonestore-restore-c2-db-1`: ID `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6`, `StartedAt = 2026-11-21T00:27:43.48526107Z`, `RestartCount = 0`, `Health = healthy`, Image `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd`, Mount `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:True`.
  - `phonestore-restore-c2-api-1`: ID `b79d2e025e11971b7f5fa5a132919bc2ea564a7753e5d22a3c1401d505b67239`, `StartedAt = 2026-11-21T00:27:43.503227531Z`, `RestartCount = 0`, `Health = healthy`, Image `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a`, Mounts `[]`.
  - `phonestore-restore-c2-web-1`: ID `e55bfc7a111747f7db0c06cfb9645a29b352fe863d8a8ea16dea6964192a6839`, `StartedAt = 2026-11-21T00:27:43.55036162Z`, `RestartCount = 0`, `Health = healthy`, Image `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0`, Mount `bind::/app/wwwroot/uploads/products:False`.
- **Tài nguyên rehearsal trước khi chạy:** `containers = 0`, `networks = 0`, `volumes = 0`, `ports 5385/5386 listeners = 0`.
- **Docker Images cục bộ đã xác minh (`linux/amd64`):**
  - Rollback API: `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a`
  - Rollback Web: `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0`
  - SQL Server 2022: `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd`
  - Internal Probe (`phonestore-cd-rehearsal-http:old`): `sha256:dc8c3c54966a2a797eaeba795245cf541c000b8fc66ee5bd8eba0c1b78616b5e`

## B. RAM/WSL2 Preflight

| Hạng mục | Giá trị đo thực tế | Ngưỡng yêu cầu | Kết quả |
| --- | --- | --- | --- |
| Ổ đĩa `C:` trống | **316,96 GiB** | Đủ không gian chứa layer & SQL fixture | **PASS** |
| `ImagePull/Pre` — Windows Free RAM | **4,51 GiB** | `>= 3,00 GiB` | **PASS** |
| `ImagePull/Pre` — WSL2 `MemAvailable` | **5,64 GiB** | `>= 3,50 GiB` | **PASS** |
| `SqlColdStart/Pre` — Windows Free RAM | **4,52 GiB** *(4,14 GiB ngay trước `up -d db`)* | `>= 3,80 GiB` | **PASS** |
| `SqlColdStart/Pre` — WSL2 `MemAvailable` | **5,64 GiB** *(5,55 GiB ngay trước `up -d db`)* | `>= 4,50 GiB` | **PASS** |
| WSL2 Swap Used | **0 MiB** | `<= 128 MiB` | **PASS** |
| Pull GHCR API & Web theo digest (`run 44845223690`, attempt `1`) | API `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050`<br/>Web `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` | Khớp manifest `images.json` & khác baseline ID | **PASS** |

## C. SQL Fixture Provisioning

- **Trạng thái:** **PASS**
- **Khởi động container `db` độc lập:**
  - Lệnh: `docker --context desktop-linux compose --env-file NUL --project-name phonestore-cd-rehearsal -f scripts/cd/docker-compose.rehearsal.yml up -d --no-deps --no-build --pull never db`
  - Container ID: `ba478b62702f854d6848352c2f1a69b72a098dfa8fa8f20c16e4085e21aef099` (Hostname `ba478b62702f`), `StartedAt = 2026-11-21T02:17:32.527459205Z`, đạt `healthy` sau ~9 giây, `MEM = 844,6 MiB / 2,25 GiB` lúc mới lên và `1,041–1,073 GiB / 2,25 GiB` sau khi mở database.
  - Volume duy nhất: `phonestore-cd-rehearsal_sql` tại `/var/opt/mssql`. Network duy nhất: `phonestore-cd-rehearsal_isolated` (`internal = true`). Không publish host port.
- **Thực thi `Invoke-CdSqlFixtureProvisioning -Mode Provision -ConfirmFixtureProvisioning` (và chạy lại lần 2 để xác minh idempotency):**
  - Ba mật khẩu ngẫu nhiên 44 ký tự (`CD_REHEARSAL_SQL_PASSWORD`, `CD_REHEARSAL_PROVISION_PASSWORD`, `CD_REHEARSAL_APP_PASSWORD`) được sinh tách biệt trong phiên, kiểm tra không trùng bất kỳ secret nào của `.env` hay container business.
  - Trong quá trình chạy thực tế trên Docker Engine v29 / `sqlcmd` v18, đã phát hiện và khắc phục 3 chi tiết tương thích runtime trong `Rehearsal.SqlFixture.ps1`: (1) Docker Engine v29 ghi nhận cả named volume `phonestore-cd-rehearsal_sql:/var/opt/mssql:rw` trong `HostConfig.Binds`, (2) filtered unique index `UX_Users_SingleAdmin` yêu cầu `SET QUOTED_IDENTIFIER ON` / cờ `sqlcmd -I`, và (3) `sqlcmd` mặc định cắt cột JSON `nvarchar(max)` ở 256 ký tự nếu không đặt `SQLCMDMAXVARTYPEWIDTH=4000`, đồng thời `cd_rehearsal` bị `DENY SELECT` trên `dbo.Users` nên bước verify kiểm tra `HAS_PERMS_BY_NAME(N'dbo.Users', N'OBJECT', N'SELECT') = 0` thay vì đếm trực tiếp bảng `Users` dưới quyền `cd_rehearsal`.
  - Kết quả xác minh JSON thực tế dưới login `cd_rehearsal` (cả lần 1 lẫn lần chạy lại idempotent):
    ```json
    {"Database":"PhoneStoreDB_CdRehearsal","MachineName":"ba478b62702f","Login":"cd_rehearsal","Migrations":2,"Roles":2,"Brands":1,"Categories":1,"ActiveProductId":1,"ActiveMarker":"S353E-FIXTURE-PHONE","ActiveStock":5,"InactiveProductId":2,"InactiveMarker":"S353E-FIXTURE-PHONE-INACTIVE","TotalProducts":2,"Users":0,"Orders":0,"OrderDetails":0}
    ```

## D. Real API Readiness

- **Trạng thái:** **PASS**
- **Khởi động `api` baseline (`sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a`):**
  - Lệnh: `docker compose ... up -d --no-deps --no-build --pull never api`
  - Container ID ban đầu: `92306f3f1643e4b67ca286b2e8223e337a19babc1e8861aca26781bbd27c91eb`, `MEM = 67,26 MiB / 512 MiB`.
  - Trong lần gọi `docker run --rm` đầu tiên từ Windows PowerShell 5.1, phát hiện PS 5.1 làm mất dấu nháy kép trong chuỗi Python `-c` nhiều dòng; đã chuyển sang truyền payload Python dạng Base64 (`import base64,sys;exec(base64.b64decode(sys.argv[1]))`) trong `Rehearsal.Probe.ps1`.
  - Kết quả `Assert-CdInternalReadiness -Service api` trên network nội bộ `phonestore-cd-rehearsal_isolated`:
    - `[CD-PROBE] utc=2026-11-21T02:24:48.0954039Z service=api mode=PhoneStore attempt=1/15 endpoint=http://api:8080/api/health/ready status=200 result=PASS detail="live=200,ready=200,catalog=200,marker=S353E-FIXTURE-PHONE,id=1"`

## E. Real Web Readiness

- **Trạng thái:** **PASS**
- **Khởi động `web` baseline (`sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0`):**
  - Lệnh: `docker compose ... up -d --no-deps --no-build --pull never web`
  - Container ID ban đầu: `26221265712eeb2642a8a4beb945d4eb3fff47e3c9e6808f2ce9166590d9fc04`, `MEM = 69,30 MiB / 512 MiB`.
  - Kết quả `Assert-CdInternalReadiness -Service web` trên network nội bộ `phonestore-cd-rehearsal_isolated`:
    - `[CD-PROBE] utc=2026-11-21T02:25:04.6963822Z service=web mode=PhoneStore attempt=1/15 endpoint=http://web:8080/ status=200 result=PASS detail="home=200,products=200,contentType=text/html,marker=S353E-FIXTURE-PHONE"`

## F. Catalog Verification

- **Trạng thái:** **PASS**
- Kiểm tra trực tiếp trên cả 3 trạng thái runtime (**Baseline trước deploy**, **Sau khi deploy GHCR release**, và **Sau khi rollback về baseline**):
  - **API `GET http://api:8080/api/v1/products`:** HTTP `200`, `totalItems = 1`, chứa đúng 1 sản phẩm active `id = 1`, `name = "S353E-FIXTURE-PHONE"`, `brandName = "S353-FIXTURE-BRAND"`, `price = 12990000.00`, `stockQuantity = 5`; không rò rỉ sản phẩm inactive (`id = 2`).
  - **API `GET http://api:8080/api/v1/products/1`:** HTTP `200`, trả chi tiết đầy đủ `{"id":1,"name":"S353E-FIXTURE-PHONE","brandId":1,"brandName":"S353-FIXTURE-BRAND","categoryId":1,"categoryName":"S353-FIXTURE-CATEGORY","price":12990000.00,"stockQuantity":5,"ram":"8 GB","storage":"128 GB","chip":"Synthetic SoC","screen":"6.1 inch OLED","camera":"50 MP","battery":"4500 mAh","operatingSystem":"Android 15","description":"Synthetic CD rehearsal active catalog fixture","images":[]}`.
  - **API `GET http://api:8080/api/v1/products/2` (sản phẩm inactive):** HTTP `404` (`Product not found`).
  - **Web `GET http://web:8080/` & `GET http://web:8080/products`:** HTTP `200` (`text/html; charset=utf-8`), hiển thị sản phẩm `S353E-FIXTURE-PHONE`, không hiển thị `S353E-FIXTURE-PHONE-INACTIVE`.
  - **Web `GET http://web:8080/products/1` (gọi qua `CatalogApi__BaseUrl=http://api:8080`):** HTTP `200` (`hasMarker = True`).
  - **Web `GET http://web:8080/products/2` (sản phẩm inactive):** HTTP `404`.

## G. GHCR Deployment

- **Trạng thái:** **PASS**
- **Lệnh thực thi:**
  ```powershell
  ./scripts/cd/Invoke-ReleaseRehearsal.ps1 -RunId 44845223690 -Attempt 1 `
    -Commit 165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07 `
    -ArtifactDigest sha256:93e32c776343f5b1d86d2a711fb7aa768cb956d8b87bd5d54128de68f24c10b1 `
    -ApplyApprovedRelease
  ```
- **Ghi chú kỹ thuật runtime đã xử lý trước khi chốt rollout:** Trong Windows PowerShell 5.1, khi `$ErrorActionPreference = 'Stop'`, việc redirect `2>$null` trên lệnh native `docker compose up -d` làm dòng tiến trình stderr `" Container phonestore-cd-rehearsal-api-1 Recreate "` bị PS 5.1 biến thành `NativeCommandError`. Đã bọc `$ErrorActionPreference = 'Continue'` quanh lệnh gọi native `& docker ... 2>$null` (vẫn kiểm tra nghiêm ngặt `$LASTEXITCODE -ne 0`), chạy `-Recover` khôi phục giao dịch thử nghiệm đó về baseline sạch, rồi thực thi rollout GHCR đầy đủ.
- **Bằng chứng chuyển đổi Container & Image theo thứ tự tuần tự (`api` trước -> readiness PASS -> `web` sau):**

| Service | Trạng thái | Container ID | Image ID | StartedAt UTC |
| --- | --- | --- | --- | --- |
| **`api`** | Baseline trước deploy | `4100545e2ac015ccf2339949eed44ba2394fe21b406fdba4ba9bc2aa55bd20e9` | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` | `2026-11-21T02:35:09.766565669Z` |
| **`api`** | **Sau GHCR Deploy** | `489c2784a6019ab7e9f8d58fa643c3ebd0e36c5b172c83cccfda402fac29753d` | `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050` | `2026-11-21T02:39:01.931095682Z` |
| **`web`** | Baseline trước deploy | `26221265712eeb2642a8a4beb945d4eb3fff47e3c9e6808f2ce9166590d9fc04` | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` | `2026-11-21T02:24:55.787932159Z` |
| **`web`** | **Sau GHCR Deploy** | `01e71469184dacca236845eac71a4550622b40bfda22e9b5deb2deb6d23462a4` | `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` | `2026-11-21T02:39:44.815631626Z` |
| **`db`** | **Giữ nguyên bất biến** | `ba478b62702f854d6848352c2f1a69b72a098dfa8fa8f20c16e4085e21aef099` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `2026-11-21T02:17:32.527459205Z` |

- **Chuỗi pha trong `journal-deployed-s353g.jsonl`:**
  `Planned -> Before-api -> Ready-api -> Before-web -> Ready-web -> Deployed`

## H. Real Rollback

- **Trạng thái:** **PASS**
- **Phương thức thực hiện:** Từ trạng thái cả `api` (`sha256:bf987253...`) và `web` (`sha256:7e2970be...`) đang chạy bản GHCR mới, đặt `journal.jsonl` tại điểm gián đoạn trước `Deployed` (`Ready-web`) và chạy lệnh khôi phục chính thức:
  ```powershell
  ./scripts/cd/Invoke-ReleaseRehearsal.ps1 -Recover
  ```
- **Bằng chứng khôi phục Image IDs về đúng cặp Baseline (`api` trước -> `web` sau):**

| Service | Trạng thái | Container ID | Image ID | StartedAt UTC |
| --- | --- | --- | --- | --- |
| **`api`** | Trước Rollback (GHCR) | `489c2784a6019ab7e9f8d58fa643c3ebd0e36c5b172c83cccfda402fac29753d` | `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050` | `2026-11-21T02:39:01.931095682Z` |
| **`api`** | **Sau Rollback (Baseline)** | `6c31678df977012005c2362d85050a6c23a4b7a2cb4fa5ce2f8aa0ab2706fd98` | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` | `2026-11-21T02:42:48.797529544Z` |
| **`web`** | Trước Rollback (GHCR) | `01e71469184dacca236845eac71a4550622b40bfda22e9b5deb2deb6d23462a4` | `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` | `2026-11-21T02:39:44.815631626Z` |
| **`web`** | **Sau Rollback (Baseline)** | `d6c6e22e40dec74f453b77404f44dcd8ca2f8c99ba422d8d391a1fa0ac40ca06` | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` | `2026-11-21T02:43:10.889693561Z` |
| **`db`** | **Giữ nguyên bất biến** | `ba478b62702f854d6848352c2f1a69b72a098dfa8fa8f20c16e4085e21aef099` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `2026-11-21T02:17:32.527459205Z` |

- **Chuỗi pha trong `journal-rollback-s353g.jsonl`:**
  `Planned -> Before-api -> Ready-api -> Before-web -> Ready-web -> RollbackPending -> RollbackBefore-api -> RollbackAfter-api -> RollbackBefore-web -> RollbackAfter-web -> RolledBack`
- **Hậu kiểm sau Rollback:** Internal probe `api` và `web` đạt HTTP `200` ngay ở attempt `1/15`, `/api/v1/products/1` và `/products/1` trả HTTP `200` kèm marker `S353E-FIXTURE-PHONE`, `/api/v1/products/2` và `/products/2` trả HTTP `404`, và `Invoke-CdSqlFixtureProvisioning -Mode Verify` xác nhận schema/seed/permissions của `PhoneStoreDB_CdRehearsal` nguyên vẹn 100%.

## I. Memory Watchdog Results

- **Trạng thái:** **PASS** (Không có bất kỳ pha nào chạm ngưỡng cảnh báo `< 1,80 GiB` hay ngưỡng cắt khẩn cấp `< 1,50 GiB`).

| Thời điểm / Pha (`Phase/Stage`) | Windows Free RAM | WSL2 `MemAvailable` | WSL2 Swap | Business Status | Mức RAM Container Rehearsal (`docker stats`) |
| --- | --- | --- | --- | --- | --- |
| `ImagePull / Pre` | **4,51 GiB** (`>= 3,00`) | **5,64 GiB** (`>= 3,50`) | `0 MiB` | `healthy` | `0` container |
| `ImagePull / Post` | **4,23 GiB** (`>= 1,80`) | **5,51 GiB** (`>= 2,00`) | `0 MiB` | `healthy` | `0` container |
| `SqlColdStart / Pre` | **4,14 GiB** (`>= 3,80`) | **5,55 GiB** (`>= 4,50`) | `0 MiB` | `healthy` | `0` container |
| `SqlColdStart / Post` | **2,43 GiB** (`>= 1,80`) | **4,97 GiB** (`>= 2,00`) | `0 MiB` | `healthy` | `db = 844,6 MiB` |
| `SqlProvision / Pre & Post` | **2,94 – 2,95 GiB** (`>= 2,20`) | **4,90 – 4,91 GiB** (`>= 3,00`) | `0 MiB` | `healthy` | `db ≈ 1,04 GiB` |
| `StartApi / Pre & Post` | **2,80 -> 2,64 GiB** (`>= 2,20 / 1,80`) | **4,74 -> 4,69 GiB** | `0 MiB` | `healthy` | `db = 1,041 GiB`, `api = 67,26 MiB` |
| `StartWeb / Pre & Post` | **2,65 -> 2,48 GiB** (`>= 2,05 / 1,80`) | **4,69 -> 4,62 GiB** | `0 MiB` | `healthy` | `db = 1,041 GiB`, `api = 67,26 MiB`, `web = 69,30 MiB` |
| `DeployStep` (GHCR `api` -> `web`) | **2,74 -> 2,86 GiB** (`>= 1,90 / 1,80`) | **4,61 -> 4,64 GiB** | `0 MiB` | `healthy` | `db = 1,072 GiB`, `api = 64,44 MiB`, `web = 67,14 MiB` |
| `RollbackStep` (Rollback `api` -> `web`) | **2,68 -> 2,73 GiB** (`>= 1,50`) | **4,60 -> 4,71 GiB** | `0 MiB` | `healthy` | `db = 1,073 GiB`, `api = 62,24 MiB`, `web = 65,48 MiB` |
| `Post-Cleanup` | **2,97 GiB** | **5,53 GiB** | `0 MiB` | `healthy` | `0` container rehearsal |

## J. Business Invariants

- **Trạng thái:** **PASS**
- Đối chiếu chữ ký JSON (`Get-CdBusinessInvariantsSnapshot`) của cả 3 container business trước preflight, tại mọi điểm kiểm tra của Watchdog trong lúc chạy, và sau khi hoàn tất Cleanup:

| Container Business | Container ID | StartedAt UTC | Restarts | Health | Image | Mounts | Kết luận |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `phonestore-restore-c2-db-1` | `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6` | `2026-11-21T00:27:43.48526107Z` | `0` | `healthy` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `volume:phonestore-restore-c2_mssql_data:/var/opt/mssql:True` | **Khớp 100%** |
| `phonestore-restore-c2-api-1` | `b79d2e025e11971b7f5fa5a132919bc2ea564a7753e5d22a3c1401d505b67239` | `2026-11-21T00:27:43.503227531Z` | `0` | `healthy` | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` | `[]` | **Khớp 100%** |
| `phonestore-restore-c2-web-1` | `e55bfc7a111747f7db0c06cfb9645a29b352fe863d8a8ea16dea6964192a6839` | `2026-11-21T00:27:43.55036162Z` | `0` | `healthy` | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` | `bind::/app/wwwroot/uploads/products:False` | **Khớp 100%** |

## K. Cleanup

- **Trạng thái:** **PASS**
- Đã dừng có kiểm soát `web`, `api` (`stop -t 10`) và `db` (`stop -t 60`, `OOMKilled = false`), xóa 3 container rehearsal (`rm -f web api db`).
- Đã xác minh nhãn `com.docker.compose.project = phonestore-cd-rehearsal` trước khi xóa network `phonestore-cd-rehearsal_isolated` và volume `phonestore-cd-rehearsal_sql`.
- Kiểm tra sau dọn dẹp: `containers = 0`, `networks = 0`, `volumes = 0` đối với project `phonestore-cd-rehearsal`.
- Đã xóa file thông tin xác thực tạm `ephemeral-creds.clixml` và giữ nguyên toàn bộ file nhật ký giao dịch trong `%LOCALAPPDATA%\PhoneStore-CdRehearsal\` (`journal.jsonl`, `journal-deployed-s353g.jsonl`, `journal-rollback-s353g.jsonl`, `journal-pre-clean.jsonl`).
- Giữ nguyên toàn bộ image rollback (`sha256:91b13b71...`, `sha256:bbcd7442...`) và image GHCR (`sha256:bf987253...`, `sha256:7e2970be...`) trong Docker cache cục bộ.

## L. GO/NO-GO for Business Rollout

- **Kết quả Full-stack Isolated Rehearsal (S35.3G):** **PASS 100%** — Công cụ CD pull-based trên Windows đã được chứng minh thực tế từ đầu đến cuối với SQL Server 2022 thật, REST API thật, Customer Web thật, image GHCR thật theo digest, internal readiness probe và rollback/recovery qua file journal bền vững.
- **Quyết định cho Business Rollout tiếp theo:** **`CONDITIONAL GO` (Sẵn sàng về mặt kỹ thuật công cụ CD/Image/Rollback; chỉ thực hiện trên hệ thống Business khi có yêu cầu phê duyệt riêng của người dùng kèm bước xác minh backup ngoài volume và tài khoản tác vụ Windows theo thiết kế S35.3A).**
