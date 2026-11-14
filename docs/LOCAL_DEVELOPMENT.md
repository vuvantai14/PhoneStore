# PhoneStore — Local Development

Tài liệu này hướng dẫn chạy PhoneStore trên Windows cho mục đích phát triển. Không chứa credential thật và không tự động thay đổi database.

## 1. Prerequisites

- Windows 10/11 để chạy `PhoneStore.Admin` WinForms.
- .NET SDK theo [`global.json`](../global.json) — hiện là 8.0.427 với patch roll-forward.
- SQL Server 2022 hoặc phiên bản tương thích EF Core 8.
- Git và PowerShell.
- Database `PhoneStoreDB` có schema tương ứng hai migration hiện tại.

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

## 3. Cấu hình SQL Server

Ba ứng dụng dùng key `ConnectionStrings:PhoneStoreConnection`. Ưu tiên cấu hình local bằng biến môi trường hoặc user-secrets thay vì sửa và commit appsettings.

Ví dụ Windows Authentication cho terminal hiện tại, không chứa mật khẩu:

```powershell
$env:ConnectionStrings__PhoneStoreConnection = "Server=localhost;Database=PhoneStoreDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Yêu cầu an toàn:

- Xác minh đúng server và database trước migration/bootstrap hoặc bất kỳ thao tác ghi nào.
- Không commit connection string chứa username/password.
- Ứng dụng không tự chạy migration hoặc seed khi startup.
- Chỉ áp dụng migration lên database development khi bạn có thẩm quyền và đã đánh giá dữ liệu đích.

Để tạo schema cho **database development mới**, sau khi đã xác minh connection target:

```powershell
dotnet ef database update --project src/PhoneStore.Data/PhoneStore.Data.csproj --startup-project src/PhoneStore.Web/PhoneStore.Web.csproj
```

Không dùng lệnh này như cách “sửa kết nối” và không chạy lại trên database đang vận hành nếu chưa được phê duyệt.

## 4. Chạy REST API

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/PhoneStore.Api/PhoneStore.Api.csproj --launch-profile PhoneStore.Api
```

- Base URL: <http://localhost:5025>
- Health: <http://localhost:5025/api/health>
- Swagger Development: <http://localhost:5025/swagger>
- API root `/` trả 404 là bình thường.

Swagger không được map trong Production.

## 5. Chạy Customer Web

Mặc định feature flag API tắt:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj --launch-profile http
```

Web: <http://localhost:5015>.

### Bật Product Detail qua REST API

Khởi động API trước, sau đó mở terminal khác:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:CatalogApi__Enabled = "true"
$env:CatalogApi__BaseUrl = "http://localhost:5025/"
$env:CatalogApi__TimeoutSeconds = "3"
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj --launch-profile http
```

- OFF: Product Detail đọc qua Business/Data, không gọi API.
- ON: Product Detail gọi `GET /api/v1/products/{id}`.
- API 429/5xx/network/timeout/invalid payload: Web fallback local một lần theo policy hiện tại.
- API 404: Web trả 404, không fallback.

Home, Product List, Cart, Checkout và Orders chưa chuyển sang REST API.

## 6. Chạy Admin WinForms

```powershell
dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj
```

Admin chỉ chạy trên Windows và kết nối Business/Data trực tiếp, không gọi REST API. Nếu build báo DLL bị khóa, đóng đúng instance Admin do bạn đang sử dụng rồi thử lại; không force-kill process không xác định.

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

Kiểm tra listener trước khi khởi động:

```powershell
Get-NetTCPConnection -State Listen | Where-Object LocalPort -in 5015,5025 |
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

## 10. Docker development

Docker Compose hiện chỉ dành cho development. Cấu hình đang track một default SA credential trong `.env.example`, `docker-compose.yml` và Docker guide; phải harden và rotate nếu từng dùng trước khi sử dụng trong môi trường chia sẻ/production. Không coi `.env` ignored là đủ nếu giá trị mặc định vẫn tồn tại trong Git history.

Xem [`DOCKER_AND_CI_GUIDE.md`](DOCKER_AND_CI_GUIDE.md), nhưng không dùng credential mặc định cho môi trường thật.

## 11. Tài liệu tham khảo

- [REST API final acceptance](system-analysis/17-REST-API-FINAL-ACCEPTANCE.md)
- [Architecture audit](system-analysis/10-ARCHITECTURE-AUDIT.md)
- [Database design audit](system-analysis/09-DATABASE-DESIGN-AUDIT.md)
- [Current progress](PROGRESS.md)
