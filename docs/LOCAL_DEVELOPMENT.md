# PhoneStore — Local Development

Tài liệu này hướng dẫn chạy PhoneStore trên Windows cho mục đích phát triển. Không chứa credential thật và không tự động thay đổi database.

## 1. Prerequisites

- Windows 10/11 để chạy `PhoneStore.Admin` WinForms.
- .NET SDK theo [`global.json`](../global.json) — hiện là `.NET 10` (`10.0.403`).
- **Docker Desktop** đang chạy với stack SQL Server 2022 (`phonestore-restore-c2-db-1` tại `127.0.0.1,14334`) vì đây là **database nghiệp vụ chính duy nhất**.
- Git và PowerShell.
- Database `PhoneStoreDB` đã áp dụng đủ 3 EF Core migrations hiện tại (`InitialCreate`, `EnforceSingleAdmin`, `AddApiRefreshTokens`).

Kiểm tra môi trường:

```powershell
dotnet --info
dotnet ef --version
```

Repository có local tool manifest cho `dotnet-ef`; nếu lệnh chưa khả dụng, chạy `dotnet tool restore`.

## 2. Clone, restore và build

```powershell
git clone https://github.com/vuvantai14/PhoneStore.git
cd PhoneStore
dotnet tool restore
dotnet restore PhoneStore.sln
dotnet build PhoneStore.sln
```

Không cần tạo `bin/`, `obj/` hoặc thư mục output thủ công.

## 3. Cấu hình SQL Server (Database thống nhất)

PhoneStore thống nhất **một nguồn dữ liệu nghiệp vụ chính duy nhất**:

| Môi trường | SQL Server đích | Database | Cơ chế cấu hình |
| :--- | :--- | :--- | :--- |
| **Docker Business** (`localhost:5015`) | `db,1433` (bridge DNS nội bộ) | `PhoneStoreDB` | `docker-compose.yml` + `.env` (ngoài Git) |
| **Local Development** (`dotnet run` Web/API) | `127.0.0.1,14334` (Docker SQL loopback) | `PhoneStoreDB` | `.NET User Secrets` (`ConnectionStrings:PhoneStoreConnection`, ngoài Git) |
| **Windows SQL (`localhost`)** | `DESKTOP-ES9362P` (`PhoneStoreDB`) | `PhoneStoreDB` | **Bản dữ liệu cũ lưu trữ (read-only archive)** — không xóa, không restore đè, không ghi nghiệp vụ mới |

> **Lưu ý quan trọng:** Vì Local Web và Local API kết nối tới Docker SQL `127.0.0.1,14334`, **Docker Desktop và container `phonestore-restore-c2-db-1` phải đang chạy** trước khi khởi chạy `dotnet run`.

### Thiết lập User Secrets cho Local Development (không lưu credential trong Git)

Cả `PhoneStore.Web` và `PhoneStore.Api` đều hỗ trợ `.NET User Secrets` (`<UserSecretsId>` trong `.csproj`). Vì User Secrets nằm ngoài Git nên trên máy mới cần thiết lập một lần cho kết nối Docker SQL (`127.0.0.1,14334`) và Local JWT của API (thay các placeholder bằng giá trị riêng trên máy bạn, không sửa `appsettings.json`):

```powershell
# 1. Chuẩn bị file .env cho Docker SQL (nếu chưa có) và điền MSSQL_SA_PASSWORD, PRODUCT_UPLOADS_PATH
if (-not (Test-Path .env)) { Copy-Item .env.example .env }

# 2. Cấu hình chuỗi kết nối Docker SQL cho Web và API (thay <MatKhauSQL> khớp MSSQL_SA_PASSWORD trong .env)
dotnet user-secrets set "ConnectionStrings:PhoneStoreConnection" "Server=127.0.0.1,14334;Database=PhoneStoreDB;User Id=sa;Password=<MatKhauSQL>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project src/PhoneStore.Web/PhoneStore.Web.csproj
dotnet user-secrets set "ConnectionStrings:PhoneStoreConnection" "Server=127.0.0.1,14334;Database=PhoneStoreDB;User Id=sa;Password=<MatKhauSQL>;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True" --project src/PhoneStore.Api/PhoneStore.Api.csproj

# 3. Cấu hình Local JWT cho PhoneStore.Api (SigningKey ngẫu nhiên tối thiểu 32 ký tự)
dotnet user-secrets set "ApiAuthentication:Mode" "LocalJwt" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:Issuer" "https://phonestore.local" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:Audience" "phonestore-api" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:SigningKey" "<KhoaBiMatNgauNhienToiThieu32KyTu>" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:AccessTokenMinutes" "15" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets set "ApiAuthentication:LocalJwt:RefreshTokenDays" "7" --project src/PhoneStore.Api/PhoneStore.Api.csproj

# 4. Kiểm tra danh sách khóa đã nạp
dotnet user-secrets list --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets list --project src/PhoneStore.Web/PhoneStore.Web.csproj
```

*(Khi kiểm tra bằng `dotnet user-secrets list`, tuyệt đối không chia sẻ/sao chép giá trị bí mật vào log, chat hoặc tài liệu.)*

### Rollback nhanh User Secrets về Windows SQL mặc định

Nếu cần quay tạm Local Web/API về mặc định trong `appsettings.json` (`Server=localhost;Database=PhoneStoreDB`) mà không đụng tới database hay Docker:

```powershell
dotnet user-secrets remove "ConnectionStrings:PhoneStoreConnection" --project src/PhoneStore.Api/PhoneStore.Api.csproj
dotnet user-secrets remove "ConnectionStrings:PhoneStoreConnection" --project src/PhoneStore.Web/PhoneStore.Web.csproj
```

Sau đó khởi động lại tiến trình Local Web/API đang chạy.

### Quy ước cho EF Core CLI (`dotnet ef`) và an toàn database

- `PhoneStoreDbContextFactory` ưu tiên đọc biến môi trường `ConnectionStrings__PhoneStoreConnection` (không tự đọc User Secrets). Khi cần chạy lệnh EF CLI lên database chính, phải truyền rõ `ConnectionStrings__PhoneStoreConnection` trong phiên terminal hiện tại và xác minh đúng đích `127.0.0.1,14334` / `PhoneStoreDB`.
- Không commit connection string chứa username/password.
- Ứng dụng không tự chạy migration hoặc seed khi startup.
- Không chạy `docker compose down -v` hoặc `docker volume prune` vì sẽ xóa volume `phonestore-restore-c2_mssql_data`.

Để tạo schema cho **database development mới** (chỉ sau khi đã xác minh connection target và được phê duyệt):

```powershell
dotnet ef database update --project src/PhoneStore.Data/PhoneStore.Data.csproj --startup-project src/PhoneStore.Web/PhoneStore.Web.csproj
```

Không dùng lệnh này như cách “sửa kết nối” và không chạy lại trên database đang vận hành nếu chưa được phê duyệt.

## 4. Chạy REST API (Local — cổng `5025`)

Khi chạy song song với Docker Business (`web-1` ở `5015`, `api-1` nội bộ `8080/tcp`), quy ước chạy Local API tại **`http://127.0.0.1:5025`** (khớp sẵn `AdminDataAccess:ApiBaseUrl` của Admin WinForms):

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/PhoneStore.Api/PhoneStore.Api.csproj --no-launch-profile --urls "http://127.0.0.1:5025"
```

- Base URL: <http://127.0.0.1:5025>
- Health: <http://127.0.0.1:5025/api/health/live> và <http://127.0.0.1:5025/api/health/ready>
- Swagger Development: <http://127.0.0.1:5025/swagger>
- Thư mục upload ảnh chia sẻ (`src/PhoneStore.Api/appsettings.Development.json`): `../PhoneStore.Web/wwwroot/uploads/products`
- API root `/` trả 404 là bình thường.

Swagger không được map trong Production.

## 5. Chạy Customer Web (Local — cổng `5016`)

Vì Docker Business Customer Web đang giữ cổng **`5015`**, quy ước chạy Local Customer Web song song tại **`http://127.0.0.1:5016`**:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj --no-launch-profile --urls "http://127.0.0.1:5016"
```

Web Local: <http://127.0.0.1:5016> (đọc cùng database Docker SQL `PhoneStoreDB` với Web Docker tại <http://localhost:5015>).

### Bật Product Detail qua REST API

Khởi động Local API (`5025`) trước, sau đó mở terminal khác:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:CatalogApi__Enabled = "true"
$env:CatalogApi__BaseUrl = "http://127.0.0.1:5025/"
$env:CatalogApi__TimeoutSeconds = "3"
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj --no-launch-profile --urls "http://127.0.0.1:5016"
```

- OFF (mặc định trong `appsettings.json`): Product Detail đọc trực tiếp từ DB qua Business/Data (`127.0.0.1,14334` qua User Secrets), không gọi API.
- ON: Product Detail gọi `GET /api/v1/products/{id}`.
- API 429/5xx/network/timeout/invalid payload: Web fallback local một lần theo policy hiện tại.
- API 404: Web trả 404, không fallback.

Home, Product List, Cart, Checkout và Orders đọc/ghi qua Business/Data trên cùng database Docker SQL `PhoneStoreDB`.

## 6. Chạy Admin WinForms

```powershell
dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj
```

Mặc định từ `S35.6B`, Admin WinForms chạy ở chế độ **`AdminDataAccess:Mode = RestApi`** trỏ tới `http://127.0.0.1:5025` (sử dụng Local JWT qua `PhoneStore.Api`, không đăng ký `PhoneStoreDbContext`). Khi Local API (`5025`) dùng User Secrets trỏ `127.0.0.1,14334`, Admin WinForms tự động làm việc trên cùng database chính với Web Docker (`5015`) và Web Local (`5016`). Nếu build báo DLL bị khóa, đóng đúng instance Admin do bạn đang sử dụng rồi thử lại; không force-kill process không xác định.

### Chạy Admin với SQL Server Docker

Admin nạp `appsettings.json` rồi environment variables, nên `ConnectionStrings__PhoneStoreConnection` chỉ override cấu hình cho process được khởi chạy. Stack Docker mở SQL tại `127.0.0.1,14334`; dùng loopback IPv4 vì `localhost` từng bị ODBC 17 timeout trên máy nghiệm thu. TLS giữ nguyên `Encrypt=True;TrustServerCertificate=True` như Web/API Docker.

Đảm bảo `.env` local (đã được Git ignore) có `MSSQL_SA_PASSWORD`, `SQL_PORT=14334` và `DB_NAME=PhoneStoreDB`. Sau đó chạy một lệnh tại repository:

```powershell
.\scripts\run-admin-docker.ps1
```

Launcher thực hiện một truy vấn `SELECT` nhẹ bằng `sqlcmd`, không đưa password lên command line/log, rồi truyền connection string qua environment chỉ cho tiến trình Admin. Launcher không khởi động hoặc restart Docker. Nếu chỉ muốn kiểm tra cấu hình và kết nối:

```powershell
.\scripts\run-admin-docker.ps1 -CheckOnly
```

Nếu Admin đang mở, launcher dừng với thông báo để tránh chạy hai instance. Hãy đóng Admin hiện tại theo cách bình thường rồi chạy lại. Để quay về SQL Server Windows, chạy lệnh Admin thông thường ở đầu mục 6; `appsettings.json` mặc định vẫn dùng Windows Authentication và launcher không thay đổi environment cấp hệ thống.

Sau khi đăng nhập, mở **Quản lý đơn hàng**, chọn **Tất cả trạng thái** và nhấn **Tải lại**. Nếu dữ liệu vẫn cũ, chạy `-CheckOnly` và xác nhận dòng PASS hiển thị `127.0.0.1,14334` cùng `PhoneStoreDB`; không sao chép connection string hoặc password vào tài liệu/log.

Admin và Docker Web dùng chung thư mục ảnh `src/PhoneStore.Web/wwwroot/uploads/products`; không cần đổi `ProductImages:UploadsDirectory`.

### Tạo Admin đầu tiên khi cần

Dry-run chỉ đọc:

```powershell
dotnet run --project tools/PhoneStore.AdminBootstrap/PhoneStore.AdminBootstrap.csproj -- --name="Quản trị viên" --email=admin@example.com
```

Chỉ sau khi xác minh target và được phép ghi database:

```powershell
dotnet run --project tools/PhoneStore.AdminBootstrap/PhoneStore.AdminBootstrap.csproj -- --name="Quản trị viên" --email=admin@example.com --execute --confirm-server=localhost --confirm-database=PhoneStoreDB
```

Nhập mật khẩu trực tiếp ở terminal ẩn; không truyền qua chat, command line, pipe hoặc file. Tool từ chối tạo Admin thứ hai và không chạy migration.

## 7. Chạy tests

Full automated suite:

```powershell
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj
```

Các tests chính dùng SQLite/doubles/TestServer. `tests/PhoneStore.SqlIntegration` là công cụ acceptance đặc biệt, không thuộc solution và không nên chạy nếu chưa đọc README/guard của nó hoặc chưa xác minh database đích.

Kết quả gần nhất ngày 14/11/2026: **232/232 PASS**, 0 fail, 0 skipped.

## 8. Ports

Quy ước cổng khi chạy song song Docker Business và Local Development:

| Thành phần | Cổng mặc định | Ghi chú |
| :--- | :---: | :--- |
| Docker Business Customer Web (`web-1`) | `127.0.0.1:5015` | Đọc `db,1433` và gọi `api:8080` |
| Docker Business SQL Server (`db-1`) | `127.0.0.1:14334` | Database chính `PhoneStoreDB` |
| Local Customer Web (`dotnet run`) | `127.0.0.1:5016` | Dùng User Secrets -> `127.0.0.1,14334` |
| Local REST API (`dotnet run`) | `127.0.0.1:5025` | Dùng User Secrets -> `127.0.0.1,14334`, phục vụ Admin WinForms (`RestApi`) |

Kiểm tra listener trước khi khởi động:

```powershell
Get-NetTCPConnection -State Listen | Where-Object LocalPort -in 5015,5016,5025,14334 |
    Select-Object LocalAddress,LocalPort,OwningProcess
```

Sau đó kiểm tra process path:

```powershell
Get-Process -Id <PID> | Select-Object Id,ProcessName,Path
```

Không dừng process nếu chưa xác minh ownership.

## 9. Troubleshooting

### Port 5015 hoặc 5025 đã được sử dụng

Xác minh PID/path như mục 8. Dùng đúng instance hiện có hoặc đóng mềm process PhoneStore do bạn sở hữu; không force-kill process không xác định.

### SQL Server connection failure

1. Kiểm tra SQL Server service và database name.
2. Kiểm tra identity đang chạy process có quyền Windows Authentication.
3. Kiểm tra cấu hình hiệu lực từ environment/appsettings mà không in secrets.
4. Dùng SELECT read-only như `DB_NAME()` để xác minh target trước thao tác ghi.

### TLS/certificate error

- Không tự đặt `Encrypt=False`, tắt Force Encryption hoặc bỏ kiểm tra certificate để “chạy cho được”.
- So sánh driver/runtime, Windows identity, certificate binding và môi trường sandbox.
- Dùng cấu hình đã được quản trị SQL Server phê duyệt. Xem [`system-analysis/08-SQL-CONNECTION-DIAGNOSIS.md`](system-analysis/08-SQL-CONNECTION-DIAGNOSIS.md).

### Swagger trả 404

Swagger chỉ bật khi `ASPNETCORE_ENVIRONMENT=Development`. API root `/` cũng chủ động không có endpoint.

### Feature flag OFF không gọi API

Đây là hành vi thiết kế. Đặt `CatalogApi__Enabled=true` trước khi khởi động Web và bảo đảm API đang nghe ở BaseUrl.

### API unavailable nhưng Product Detail vẫn mở

Với 429/5xx/network/timeout/invalid payload, resolver fallback local một lần. Kiểm tra warning log an toàn; không coi fallback là bằng chứng API đang khỏe.

### Admin không hiển thị

- Chạy từ Windows interactive desktop session.
- Kiểm tra startup exception/log và cấu hình SQL.
- Kiểm tra chỉ có một instance mong muốn.
- Không reset credential hoặc thay database để xử lý lỗi hiển thị.

## 10. Docker Compose cách ly

Stack gồm `web`, `api`, `db`; Admin WinForms tiếp tục chạy trên Windows. Cấu hình restore dùng project `phonestore-restore-c2`, volume SQL riêng, SQL host port `14334`, backup mount read-only và SQL Server image đã pin digest.

### Chuẩn bị

```powershell
Copy-Item .env.example .env
```

Trong `.env`, đặt credential SA mới đủ mạnh và đường dẫn tuyệt đối `PRODUCT_UPLOADS_PATH` tới thư mục ảnh sản phẩm. Không commit `.env`, backup `.bak`, uploads chứa dữ liệu thật hoặc secret. Web/API trong Compose phải dùng DNS service `db` và `api`, không dùng `localhost`.

Kiểm tra cấu hình mà không khởi động container:

```powershell
docker compose -f docker-compose.yml -f docker-compose.restore.yml config --quiet
```

### Chuẩn bị database Docker từ backup trên máy mới

1. Nhận backup demo đã được loại bỏ dữ liệu nhạy cảm qua kênh riêng; không đặt `.bak` trong repository.
2. Copy backup vào thư mục transfer ngoài repository đang được bind read-only trong `docker-compose.restore.yml`.
3. Xác minh kích thước/SHA-256, rồi khởi động riêng `db` và chờ healthy.
4. Chạy `RESTORE HEADERONLY`, `FILELISTONLY` và `VERIFYONLY WITH CHECKSUM` trong container.
5. Chỉ khi `PhoneStoreDB` chưa tồn tại, restore đúng một lần bằng `WITH MOVE` tới `/var/opt/mssql/data/PhoneStoreDB.mdf` và `/var/opt/mssql/data/PhoneStoreDB_log.ldf`, kèm `CHECKSUM`; không dùng `REPLACE`.
6. SELECT xác minh database ONLINE, migrations và row counts trước khi mở API/Web.

Trên máy đã hoàn tất S31.5F, named volume `phonestore-restore-c2_mssql_data` đã có database hợp lệ; bỏ qua toàn bộ bước restore. Không chạy lại restore để “làm mới” dữ liệu.

### Khởi động sau khi database Docker đã sẵn sàng

```powershell
docker compose -f docker-compose.yml -f docker-compose.restore.yml up -d db
docker compose -f docker-compose.yml -f docker-compose.restore.yml ps

# Chỉ chạy sau khi PhoneStoreDB đã được restore/kiểm tra trong target volume.
docker compose -f docker-compose.yml -f docker-compose.restore.yml up -d --build api web
docker compose -f docker-compose.yml -f docker-compose.restore.yml ps
```

Kiểm tra:

```powershell
Invoke-WebRequest http://localhost:5025/api/health -UseBasicParsing
Invoke-WebRequest http://localhost:5015/ -UseBasicParsing
Invoke-WebRequest http://localhost:5015/products -UseBasicParsing
docker compose -f docker-compose.yml -f docker-compose.restore.yml logs --tail 100 db api web
```

Ports mặc định: Web `5015`, API `5025`, SQL `14334`. Dừng an toàn:

```powershell
docker compose -f docker-compose.yml -f docker-compose.restore.yml stop
```

Không dùng `down -v` vì sẽ xóa volume dữ liệu. Không restore với `REPLACE`, không migration/seed tự động và không trỏ Admin Windows vào SQL Docker khi chưa cấu hình/kiểm tra riêng.

SQL container được giới hạn bằng cơ chế hỗ trợ của SQL Server (`MSSQL_MEMORY_LIMIT_MB`) cùng cgroup memory limit trong restore override. Không bỏ các giới hạn này trên máy ít RAM. Xem [`DOCKER_AND_CI_GUIDE.md`](DOCKER_AND_CI_GUIDE.md) và [`system-analysis/30-DOCKER-FINAL-COMPLETION.md`](system-analysis/30-DOCKER-FINAL-COMPLETION.md).

## 11. Tài liệu tham khảo

- [REST API final acceptance](system-analysis/17-REST-API-FINAL-ACCEPTANCE.md)
- [Architecture audit](system-analysis/10-ARCHITECTURE-AUDIT.md)
- [Database design audit](system-analysis/09-DATABASE-DESIGN-AUDIT.md)
- [Current progress](PROGRESS.md)
