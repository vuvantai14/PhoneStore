# PhoneStore — Docker Compose & CI/CD Guide

Tài liệu này mô tả chính xác cấu hình Docker Compose, GitHub Actions CI, phát hành GHCR và quy trình Windows pull-based CD hiện hành của PhoneStore (`.NET 10`, SQL Server 2022). Không thực thi deployment hay thay đổi dữ liệu tự động.

---

## 1. Phân biệt Local Development Docker và Docker Business

| Khía cạnh | Local Development Docker (môi trường mới) | Docker Business Production (`phonestore-restore-c2`) |
|---|---|---|
| **Compose files** | [`docker-compose.yml`](../docker-compose.yml) | [`docker-compose.yml`](../docker-compose.yml) + [`scripts/cd/docker-compose.business.yml`](../scripts/cd/docker-compose.business.yml) + `%LOCALAPPDATA%\PhoneStore-BusinessCd\docker-compose.cd-override.yml` *(ngoài Git)* |
| **Project name** | `COMPOSE_PROJECT_NAME` trong `.env` (mẫu: `phonestore-acceptance`) | `phonestore-restore-c2` |
| **Nguồn image `web` & `api`** | Build cục bộ từ `Dockerfile` và `src/PhoneStore.Api/Dockerfile` | Pin immutable SHA-256 digest từ GHCR (rollback về local baseline digest trong `docker-compose.business.yml`) |
| **Nguồn image `db`** | `mcr.microsoft.com/mssql/server:2022-latest` | Pin SHA-256 digest cố định, `mem_limit: 2304m`, `MSSQL_MEMORY_LIMIT_MB: 1536`, `stop_grace_period: 60s` |
| **ASPNETCORE_ENVIRONMENT** | `Development` (mặc định) | `api`: `Production` (tắt Swagger), `web`: theo cấu hình triển khai |
| **JWT Secrets cho `api`** | Nạp từ `.env` (ngoài Git) | Nạp từ `api-jwt.env` (ACL user-only, độ ưu tiên cao hơn `.env`) |
| **Cổng host** | `5015` (Web), `5025` (API), `14334` (SQL) — chỉnh khác nếu chạy song song Business | `127.0.0.1:5015` (Web), `127.0.0.1:14334` (SQL), `api` phục vụ nội bộ `8080/tcp` |

> **Quy tắc an toàn bắt buộc:** Không chạy trực tiếp `docker compose up -d --build` lên stack Business `phonestore-restore-c2`; không bao giờ chạy `docker compose down -v` hoặc xóa volume `phonestore-restore-c2_mssql_data`.

---

## 2. Kiến trúc Docker Compose & Services

Ba service chạy trên Linux containers cùng bridge network `phonestore-net` (Admin WinForms chạy trực tiếp trên Windows):

1. **`db` (SQL Server 2022)**:
   - Volume persistent `mssql_data:/var/opt/mssql`.
   - Trong `scripts/cd/docker-compose.business.yml`: pin SHA-256 image SQL Server 2022, giới hạn `mem_limit: 2304m`, `MSSQL_MEMORY_LIMIT_MB: "1536"`, `stop_grace_period: 60s` và bind-mount thư mục backup `C:/PhoneStoreDockerTransfer/backup:/var/opt/mssql/backup:ro`.
   - Healthcheck: chạy `sqlcmd` kiểm tra `SELECT 1` mỗi 10s.
2. **`api` (`PhoneStore.Api`)**:
   - Đóng gói bởi [`src/PhoneStore.Api/Dockerfile`](../src/PhoneStore.Api/Dockerfile) (`.NET 10`, port nội bộ `8080`).
   - Kết nối SQL qua `Server=db,1433;Database=${DB_NAME:-PhoneStoreDB}` (`depends_on: db: service_healthy`).
   - Chủ động ghi đè `MSSQL_SA_PASSWORD: ""` trong `environment` để không lộ biến SA độc lập khi nạp `env_file: .env`.
   - Shared uploads: đặt `ProductImages__UploadsDirectory: "/app/wwwroot/uploads/products"` và bind-mount `${PRODUCT_UPLOADS_PATH}` ở chế độ **`read_only: false` (`rw`)**.
   - Healthcheck: TCP HTTP probe `GET /api/health HTTP/1.1` trả `200` mỗi 15s.
3. **`web` (`PhoneStore.Web`)**:
   - Đóng gói bởi [`Dockerfile`](../Dockerfile) (`.NET 10`, port nội bộ `8080`, publish `127.0.0.1:5015`).
   - Kết nối SQL qua `Server=db,1433` và gọi API nội bộ qua `CatalogApi__BaseUrl: "http://api:8080/"` (`CatalogApi__Enabled: true`, `depends_on: db + api: service_healthy`).
   - Shared uploads: bind-mount cùng `${PRODUCT_UPLOADS_PATH}` vào `/app/wwwroot/uploads/products` ở chế độ **`read_only: true` (`ro`)**.
   - Healthcheck: TCP HTTP probe `GET / HTTP/1.1` trả `200` mỗi 15s.

### Chuẩn bị & kiểm tra tĩnh cho môi trường mới

```powershell
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
# Điền MSSQL_SA_PASSWORD, PRODUCT_UPLOADS_PATH và ApiAuthentication__LocalJwt__SigningKey trong .env (đã bị Git ignore)
docker compose -f docker-compose.yml config --quiet
```

---

## 3. GitHub Actions CI & GHCR Publishing

Workflow [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) (mọi action đều được pin theo SHA đầy đủ, quyền mặc định `contents: read`) gồm 3 job:

1. **`build-and-test-windows` (`windows-latest`)**:
   - Cài .NET SDK từ `global.json` (`10.0.403`), `dotnet restore`, `dotnet build PhoneStore.sln --configuration Release`.
   - Build thêm 3 project phụ trợ (`PhoneStore.CatalogImport`, `PhoneStore.AcceptanceEnvironment`, `PhoneStore.SqlIntegration`).
   - Chạy `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-build --configuration Release`.
   - Publish & kiểm tra artifact Admin WinForms bằng [`scripts/ci/Publish-AdminArtifact.ps1`](../scripts/ci/Publish-AdminArtifact.ps1) (mặc định `RestApi -> 127.0.0.1:5025`) và upload artifact giữ 14 ngày.
2. **`build-docker-ubuntu` (`ubuntu-latest`)**:
   - Chuẩn bị Docker build context sạch từ Git bằng [`scripts/ci/prepare_image_context.py`](../scripts/ci/prepare_image_context.py) (loại bỏ `bin/`, `obj/`, `uploads/`, `.env*`, file DB/backup và strip `ConnectionStrings` khỏi `appsettings*.json`).
   - Build kiểm chứng `linux/amd64` cho cả `PhoneStore.Web` và `PhoneStore.Api` (`push: false`).
   - Chạy [`scripts/ci/validate_business_compose.py`](../scripts/ci/validate_business_compose.py) kiểm tra offline model Compose hợp nhất (`docker-compose.yml` + `scripts/cd/docker-compose.business.yml`): kiểm tra SQL image digest, giới hạn RAM, shutdown grace period, volume, backup mount, healthchecks và shared uploads (`api` `rw` khớp nguồn `web` `ro`).
3. **`publish-ghcr` (`ubuntu-latest`, chỉ chạy khi push vào `main` và 2 job trên PASS)**:
   - Quyền nâng phạm vi hẹp `packages: write`.
   - Build và push `ghcr.io/vuvantai14/phonestore-web:sha-<commit>` và `ghcr.io/vuvantai14/phonestore-api:sha-<commit>`.
   - Đối chiếu digest giữa Buildx và `docker buildx imagetools inspect`, xuất manifest `images.json` và upload artifact giữ 30 ngày cho quy trình CD.

---

## 4. Quy trình Windows Pull-Based CD (`scripts/cd/`)

Hệ thống dùng mô hình **Windows máy đích chủ động kéo release đã phê duyệt** (không mở inbound SSH/runner tới máy host):

- **Công cụ chính**:
  - [`scripts/cd/Business.Cd.ps1`](../scripts/cd/Business.Cd.ps1) & [`scripts/cd/Invoke-BusinessCd.ps1`](../scripts/cd/Invoke-BusinessCd.ps1): thực thi chu trình kiểm tra (`-ValidateOnly`) hoặc áp dụng (`-ApplyApprovedRelease`).
  - [`scripts/cd/Register-BusinessCdTask.ps1`](../scripts/cd/Register-BusinessCdTask.ps1): đặc tả Scheduled Task quyền tối thiểu (`RunLevel=Limited`, không lưu mật khẩu, yêu cầu xác nhận tường minh `-ConfirmTaskRegistration`).
  - [`scripts/cd/Rehearsal.Engine.ps1`](../scripts/cd/Rehearsal.Engine.ps1), `Rehearsal.Release.ps1`, `Rehearsal.Probe.ps1`, `Rehearsal.ResourceGate.ps1`, `Rehearsal.SqlFixture.ps1`: engine diễn tập cách ly, Phased Tiered RAM Gates và internal probe.
- **Các cổng bảo vệ bắt buộc trước khi deploy**:
  1. **Phê duyệt digest hai lớp**: đối chiếu `approved-release.json` ngoài Git (`AutoDeployUnapproved=false`) với manifest `images.json` trên GHCR.
  2. **Khóa thực thi**: `deployment.lock` ngăn hai phiên CD chạy chồng chéo.
  3. **Cổng backup SQL ngoài volume (`Test-CdBusinessSqlBackup`)**: xác minh file backup `COPY_ONLY + CHECKSUM + VERIFYONLY` tồn tại ngoài Docker volume tại `C:\PhoneStoreDockerTransfer\backup` khớp kích thước và SHA-256.
  4. **Cổng tài nguyên RAM**: kiểm tra Windows Free RAM, WSL2 `MemAvailable` và Swap trước từng bước.
  5. **Khóa lệnh Compose (`Assert-CdBusinessComposeCommandSafety`)**: chỉ cho phép `up -d --no-deps --no-build --pull never <api|web>`; chặn tuyệt đối `down`, `prune`, `rm`, `db` và mọi từ khóa migrate/seed.
  6. **Thứ tự tuần tự**: cập nhật `api` → kiểm readiness → cập nhật `web` → kiểm readiness; **tuyệt đối không recreate container `db`**.

---

## 5. Backup, Rollback & Xác minh triển khai

- **Backup trước thay đổi rủi ro**: Tạo bản `BACKUP DATABASE [PhoneStoreDB] TO DISK = ... WITH COPY_ONLY, COMPRESSION, CHECKSUM` ra ngoài volume (`C:\PhoneStoreDockerTransfer\backup`), xác minh bằng `RESTORE VERIFYONLY WITH CHECKSUM` và đối chiếu SHA-256.
- **Rollback tự động / thủ công**:
  - Engine CD ghi nhật ký giao dịch vào `journal.jsonl` (`Planned → Before-api → Ready-api → Before-web → Ready-web → Deployed`).
  - Nếu readiness của `api` hoặc `web` thất bại, CD tự động rollback cả hai service ứng dụng về image baseline đã pin trong `scripts/cd/docker-compose.business.yml` (hoặc khôi phục phiên bị gián đoạn bằng `-Recover`), giữ nguyên hoàn toàn container `db`.
- **Xác minh chỉ đọc sau vận hành**:

```powershell
# 1. Trạng thái & health 3 container Business
docker ps --filter "label=com.docker.compose.project=phonestore-restore-c2" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# 2. Kiểm tra Customer Web Business
Invoke-WebRequest -Uri "http://localhost:5015/" -UseBasicParsing | Select-Object StatusCode
Invoke-WebRequest -Uri "http://localhost:5015/products" -UseBasicParsing | Select-Object StatusCode

# 3. Chạy kiểm tra tĩnh Compose & bộ test an toàn CD (không chạm runtime)
python scripts/ci/validate_business_compose.py
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-BusinessCdReadiness.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-Rehearsal.ps1
```

---

## 6. Tài liệu liên quan

- [Hướng dẫn Local Development](LOCAL_DEVELOPMENT.md)
- [Hướng dẫn kiểm thử](testing/TESTING_GUIDE.md)
- [Bằng chứng deploy production gần nhất (`S35.5`)](archive/s35/S35_5_REAL_DOCKER_BUSINESS_DEPLOYMENT.md)
- [Tiến độ hiện tại](PROGRESS.md)
