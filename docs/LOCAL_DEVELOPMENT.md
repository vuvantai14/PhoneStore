# PhoneStore — Local Development

Tài liệu này hướng dẫn thiết lập môi trường và chạy PhoneStore trên Windows cho mục đích phát triển. Không chứa credential thật và không tự động thay đổi database.

## 1. Yêu cầu môi trường

- Windows 10/11 (cần thiết để chạy `PhoneStore.Admin` WinForms).
- `.NET 10 SDK` theo [`global.json`](../global.json) (`10.0.403`).
- **Docker Desktop** (Linux containers) đang chạy cùng SQL Server 2022 (`127.0.0.1,14334`) — là **database nghiệp vụ chính duy nhất**.
- Git và PowerShell.
- Database `PhoneStoreDB` đã có đủ 3 EF Core migrations (`InitialCreate`, `EnforceSingleAdmin`, `AddApiRefreshTokens`).

Kiểm tra nhanh công cụ:

```powershell
dotnet --info
dotnet tool restore
dotnet ef --version
```

## 2. Clone, restore và build

```powershell
git clone https://github.com/vuvantai14/PhoneStore.git
cd PhoneStore
dotnet tool restore
dotnet restore PhoneStore.sln
dotnet build PhoneStore.sln
```

## 3. Cấu hình SQL Server (Database thống nhất) & `.NET User Secrets`

PhoneStore thống nhất **một nguồn dữ liệu nghiệp vụ chính duy nhất**:

| Môi trường | SQL Server đích | Database | Cơ chế cấu hình |
|---|---|---|---|
| **Docker Business** (`localhost:5015`) | `db,1433` (bridge DNS nội bộ) | `PhoneStoreDB` | `docker-compose.yml` + `.env` (ngoài Git) |
| **Local Development** (`dotnet run` Web/API) | `127.0.0.1,14334` (Docker SQL loopback) | `PhoneStoreDB` | `.NET User Secrets` (`ConnectionStrings:PhoneStoreConnection`, ngoài Git) |
| **Windows SQL (`localhost`)** | `DESKTOP-ES9362P` (`PhoneStoreDB`) | `PhoneStoreDB` | **Bản dữ liệu cũ lưu trữ (read-only archive)** — không xóa, không restore đè, không ghi nghiệp vụ mới |

> **Lưu ý quan trọng:** Vì Local Web và Local API kết nối Docker SQL `127.0.0.1,14334`, **Docker Desktop và container SQL Server phải đang chạy** trước khi khởi động `dotnet run`.

### Thiết lập trên máy mới (không lưu secret trong Git)

Cả `PhoneStore.Web` và `PhoneStore.Api` đều khai báo `<UserSecretsId>` trong `.csproj`. Vì User Secrets nằm ngoài Git nên trên máy mới cần thiết lập một lần (thay placeholder bằng giá trị riêng trên máy bạn, không sửa `appsettings.json`):

```powershell
# 1. Chuẩn bị file .env cho Docker SQL (nếu chưa có) và điền MSSQL_SA_PASSWORD, PRODUCT_UPLOADS_PATH
if (-not (Test-Path .env)) { Copy-Item .env.example .env }

# 2. Chuỗi kết nối Docker SQL cho Web và API (khớp MSSQL_SA_PASSWORD trong .env)
dotnet user-secrets set "ConnectionStrings:PhoneStoreConnection" "Server=127.0.0.1,14334;Database=PhoneStoreDB;User Id=sa;Password=<MatKhauSQL>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project src/PhoneStore.Web/PhoneStore.Web.csproj
dotnet user-secrets set "ConnectionStrings:PhoneStoreConnection" "Server=127.0.0.1,14334;Database=PhoneStoreDB;User Id=sa;Password=<MatKhauSQL>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project src/PhoneStore.Api/PhoneStore.Api.csproj

# 3. Cấu hình Local JWT cho PhoneStore.Api (SigningKey ngẫu nhiên tối thiểu 32 ký tự)
dotnet user-secrets set "ApiAuthentication:Mode" "LocalJwt" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:Issuer" "https://phonestore.local" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:Audience" "phonestore-api" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:SigningKey" "<KhoaBiMatNgauNhienToiThieu32KyTu>" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:AccessTokenMinutes" "15" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:RefreshTokenDays" "7" --project src/PhoneStore.Api/PhoneStore.Api.csproj

# 4. Kiểm tra danh sách khóa đã nạp (không sao chép giá trị bí mật vào log/tài liệu)
dotnet user-secrets list --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets list --project src/PhoneStore.Web/PhoneStore.Web.csproj
```

### Rollback nhanh User Secrets về Windows SQL mặc định

Nếu cần quay tạm Local Web/API về mặc định trong `appsettings.json` (`Server=localhost;Database=PhoneStoreDB`) mà không đụng database hay Docker:

```powershell
dotnet user-secrets remove "ConnectionStrings:PhoneStoreConnection" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets remove "ConnectionStrings:PhoneStoreConnection" --project src/PhoneStore.Web/PhoneStore.Web.csproj
```

### Quy ước cho EF Core CLI (`dotnet ef`)

- `PhoneStoreDbContextFactory` ưu tiên biến môi trường `ConnectionStrings__PhoneStoreConnection` (không tự đọc User Secrets). Khi cần chạy lệnh EF CLI lên một database mục tiêu đã xác minh, hãy truyền rõ biến môi trường đó trong phiên PowerShell hiện tại.
- Ứng dụng không tự chạy migration hoặc seed khi khởi động. Chỉ chạy `dotnet ef database update` trên **database development mới** sau khi đã xác minh server/database đích và có phê duyệt:

```powershell
dotnet ef database update --project src/PhoneStore.Data/PhoneStore.Data.csproj --startup-project src/PhoneStore.Web/PhoneStore.Web.csproj
```

## 4. Chạy REST API (Local — cổng `5025`)

Chạy từ thư mục gốc repository trong cửa sổ PowerShell riêng:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/PhoneStore.Api/PhoneStore.Api.csproj --no-launch-profile --urls "http://127.0.0.1:5025"
```

- **Base URL**: `http://127.0.0.1:5025`
- **Health**: `http://127.0.0.1:5025/api/health/live` và `http://127.0.0.1:5025/api/health/ready`
- **Swagger (`Development` only)**: `http://127.0.0.1:5025/swagger` (trả `404` trong `Production`; đường dẫn `/` trả `404` là bình thường)
- **Thư mục ảnh chia sẻ**: [`src/PhoneStore.Api/appsettings.Development.json`](../src/PhoneStore.Api/appsettings.Development.json) trỏ tương đối `../PhoneStore.Web/wwwroot/uploads/products`, đồng bộ với Customer Web.

### Chạy Swagger độc lập trên cổng `5026` (tùy chọn, cách ly hoàn toàn)

Repository cung cấp stack Docker Development riêng trên cổng `5026` (`phonestore-dev-swagger`, dùng database riêng `PhoneStoreDB_SwaggerDev` và khóa JWT cục bộ tại `%LOCALAPPDATA%\PhoneStore-DevSwagger`, không chạm DB chính hay cổng `5015`/`5025`/`14334`):

```powershell
.\scripts\dev\Start-PhoneStoreSwagger.ps1
# Truy cập: http://localhost:5026/swagger
# Dừng sạch khi xong:
.\scripts\dev\Stop-PhoneStoreSwagger.ps1
```

## 5. Chạy Customer Web (Local — cổng `5016`)

Vì Docker Business Customer Web dùng cổng `5015`, quy ước chạy Local Customer Web song song tại **`http://127.0.0.1:5016`**:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj --no-launch-profile --urls "http://127.0.0.1:5016"
```

### Bật Product Detail qua REST API (tùy chọn)

Khởi chạy Local API (`5025`) trước, sau đó chạy Customer Web với feature flag bật:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:CatalogApi__Enabled = "true"
$env:CatalogApi__BaseUrl = "http://127.0.0.1:5025/"
$env:CatalogApi__TimeoutSeconds = "3"
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj --no-launch-profile --urls "http://127.0.0.1:5016"
```

- `OFF` (mặc định ở `appsettings.json`): Product Detail đọc trực tiếp từ DB qua Business/Data.
- `ON`: Product Detail gọi `GET /api/v1/products/{id}`. Nếu API lỗi `429`/5xx/mạng/timeout/payload hỏng, Web tự fallback về DB một lần; nếu API trả `404`, Web trả `404` không fallback.

## 6. Chạy Admin WinForms

```powershell
dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj
```

- Mặc định từ `S35.6B`, Admin WinForms dùng **`AdminDataAccess:Mode = RestApi`** trỏ tới `http://127.0.0.1:5025` (xác thực Local JWT qua `PhoneStore.Api`, không đăng ký `PhoneStoreDbContext`).
- Khi Local API (`5025`) trỏ Docker SQL `127.0.0.1,14334`, Admin WinForms tự động dùng chung dữ liệu với Customer Web Local (`5016`) và Docker (`5015`).
- Nếu build báo khóa DLL trong `bin/`, đóng đúng cửa sổ `PhoneStore.Admin` đang mở rồi build lại.

### Chế độ dự phòng `DirectSql` (tùy chọn)

Script [`scripts/run-admin-docker.ps1`](../scripts/run-admin-docker.ps1) đọc `.env` để truyền `ConnectionStrings__PhoneStoreConnection` trỏ `127.0.0.1,14334` (cần cài `sqlcmd`). Vì Admin mặc định `RestApi`, chỉ kích hoạt `DirectSql` khi thật sự cần dự phòng:

```powershell
.\scripts\run-admin-docker.ps1 -CheckOnly
$env:AdminDataAccess__Mode = "DirectSql"
.\scripts\run-admin-docker.ps1
```

### Tạo tài khoản Admin đầu tiên trên môi trường mới

Dry-run chỉ đọc:

```powershell
dotnet run --project tools/PhoneStore.AdminBootstrap/PhoneStore.AdminBootstrap.csproj -- --name="Quản trị viên" --email=admin@example.com
```

Chỉ chạy `--execute` trên môi trường mới sau khi xác minh đúng đích và được phép ghi (`AdminBootstrap` mặc định giới hạn `localhost` + Windows Auth, nhập mật khẩu ẩn hai lần ở terminal tương tác và từ chối tạo Admin thứ hai).

## 7. Chạy kiểm thử an toàn

```powershell
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj
```

Toàn bộ suite trong `PhoneStore.Tests` chạy cách ly bằng SQLite `:memory:` / `WebApplicationFactory` và không kết nối `PhoneStoreDB` thật. Xem hướng dẫn đầy đủ các bộ test (xUnit, CD PowerShell, CI Compose validator và rào chắn cho `tests/PhoneStore.SqlIntegration`) tại [`testing/TESTING_GUIDE.md`](testing/TESTING_GUIDE.md), và xem kết quả xác minh mới nhất tại [`PROGRESS.md`](PROGRESS.md).

## 8. Quy ước cổng

| Thành phần | Cổng mặc định | Ghi chú |
|---|---|---|
| Docker Business Customer Web (`web-1`) | `127.0.0.1:5015` | Kết nối `db,1433` và gọi `api:8080` nội bộ |
| Docker Business SQL Server (`db-1`) | `127.0.0.1:14334` | Database chính `PhoneStoreDB` |
| Local Customer Web (`dotnet run`) | `127.0.0.1:5016` | Dùng User Secrets → `127.0.0.1,14334` |
| Local REST API (`dotnet run`) | `127.0.0.1:5025` | Dùng User Secrets → `127.0.0.1,14334`, phục vụ Admin WinForms (`RestApi`) |
| Swagger Development cách ly (tùy chọn) | `127.0.0.1:5026` | Stack riêng `phonestore-dev-swagger` |

Kiểm tra tiến trình giữ cổng trước khi khởi động:

```powershell
Get-NetTCPConnection -State Listen | Where-Object LocalPort -in 5015,5016,5025,5026,14334 |
    Select-Object LocalAddress,LocalPort,OwningProcess
Get-Process -Id <PID> | Select-Object Id,ProcessName,Path
```

Không dừng tiến trình khi chưa xác minh rõ ownership.

## 9. Troubleshooting

- **Trùng cổng `5015` hoặc `5025`**: Xác minh PID/path như mục 8; dùng đúng instance đang chạy hoặc đóng mềm đúng tiến trình bạn sở hữu. Không chạy đồng thời hai ứng dụng trên cùng cổng.
- **Lỗi kết nối SQL Server**: Kiểm tra Docker Desktop và container SQL đang `healthy`, kiểm tra tên khóa User Secrets bằng `dotnet user-secrets list` (không in mật khẩu) và xác minh bằng `SELECT DB_NAME()` chỉ đọc.
- **Lỗi TLS / certificate khi kết nối SQL**: Không tự đặt `Encrypt=False` để chạy tạm. Xem chẩn đoán chi tiết tại [`archive/s31-system-analysis/08-SQL-CONNECTION-DIAGNOSIS.md`](archive/s31-system-analysis/08-SQL-CONNECTION-DIAGNOSIS.md).
- **Swagger trả `404`**: Swagger chỉ bật khi `ASPNETCORE_ENVIRONMENT=Development`; trong `Production` (Docker Business) Swagger bị tắt có chủ đích.
- **Customer Web không gọi API hoặc vẫn mở trang khi API tắt**: Kiểm tra `CatalogApi:Enabled`; khi `ON` mà API gặp `429`/5xx/timeout/lỗi mạng, Web chủ động fallback local một lần.
- **Admin WinForms không mở hoặc không đăng nhập được**: Chạy trong Windows interactive session, kiểm tra Local API đang phản hồi `http://127.0.0.1:5025/api/health/ready` và đã bật `ApiAuthentication:Mode = LocalJwt` trong User Secrets.

## 10. Docker Compose & CI/CD

- **Môi trường Docker mới độc lập**: Sử dụng `docker-compose.yml` cùng `.env` (sao chép từ `.env.example`, chọn `COMPOSE_PROJECT_NAME` và cổng không trùng stack Business nếu chạy song song).
- **Stack Docker Business (`phonestore-restore-c2`)**: Hợp nhất từ `docker-compose.yml` + [`scripts/cd/docker-compose.business.yml`](../scripts/cd/docker-compose.business.yml) + file override vận hành ngoài repository. **Không** chạy trực tiếp `docker compose up -d --build` hoặc `docker compose down -v` lên stack Business đang hoạt động.
- Xem hướng dẫn đầy đủ về Docker Compose, GitHub Actions, GHCR, Windows pull-based CD, backup và rollback tại [`DOCKER_AND_CI_GUIDE.md`](DOCKER_AND_CI_GUIDE.md).

## 11. Tài liệu tham khảo

- [Kiến trúc hệ thống hiện hành](architecture/SYSTEM_ARCHITECTURE.md)
- [Thiết kế cơ sở dữ liệu hiện hành](architecture/DATABASE_DESIGN.md)
- [Xác thực, phân quyền và Local JWT](architecture/AUTHENTICATION_AND_JWT.md)
- [Hướng dẫn kiểm thử](testing/TESTING_GUIDE.md)
- [Hướng dẫn Docker Compose & CI/CD](DOCKER_AND_CI_GUIDE.md)
- [Tiến độ hiện tại](PROGRESS.md)
