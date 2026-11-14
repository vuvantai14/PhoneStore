# S31.5F — Docker Restore & Full Runtime Acceptance

Ngày nghiệm thu: 2026-11-14  
Kết luận: **DOCKER RUNTIME PASS / READY FOR DEMO**

## 1. Phạm vi

- Customer Web ASP.NET Core MVC: container `web`.
- REST API ASP.NET Core: container `api`.
- SQL Server 2022: container `db`, database `PhoneStoreDB`.
- Admin WinForms tiếp tục chạy trực tiếp trên Windows.
- Không chỉnh sửa UI; UI UX Pro Max được dùng làm checklist giữ nguyên usability/accessibility hiện có trong smoke test.

## 2. Preflight và cách ly

- Docker Engine 29.9.2, Linux containers, 12 CPU, Docker memory khoảng 7.64 GiB.
- Pre-start RAM 6.89 GiB; ports 5015, 5025, 14334 trống.
- Project `phonestore-restore-c2`; target volume `phonestore-restore-c2_mssql_data` tách biệt volume cũ `phonestore_mssql_data`.
- Backup mount `/var/opt/mssql/backup` read-only.
- Backup physical size 732160 bytes; SHA-256 `D806D221FD49FEFB9187C51D6EF0781F653312FF3606726309055F60B007D4E2`.
- SQL image pin digest; cgroup memory 2.25 GiB, `MSSQL_MEMORY_LIMIT_MB=1536`, stop timeout 60 giây.

## 3. Restore database

Restore thực hiện đúng một lần sau khi guard xác nhận:

- SQL target healthy, OOM false, restart 0.
- RAM trước restore 3.03 GiB.
- `DB_ID('PhoneStoreDB') IS NULL`.
- Target MDF/LDF chưa tồn tại.
- Dùng backup đã HEADERONLY/FILELISTONLY/VERIFYONLY WITH CHECKSUM PASS.

Thao tác dùng `RESTORE DATABASE ... WITH MOVE, CHECKSUM, STATS=10`; **không `REPLACE`**, không migration, seed hoặc restore lần hai. Kết quả: 809 pages restored, database ONLINE, compatibility level 160.

| Table | Rows |
|---|---:|
| Roles | 2 |
| Users | 3 |
| Brands | 8 |
| Categories | 2 |
| Products | 40 |
| ProductImages | 41 |
| Orders | 12 |
| OrderDetails | 28 |

EF history có đúng hai migrations `InitialCreate` và `EnforceSingleAdmin`, EF Core 8.0.28. SQL Server Windows được SELECT hậu kiểm và vẫn ONLINE với cùng counts; không có thao tác ghi vào database nguồn.

## 4. Runtime acceptance

| Thành phần | Bằng chứng | Kết quả |
|---|---|---|
| SQL Docker | healthy, OOM=false, restart=0 | PASS |
| API health | `GET http://localhost:5025/api/health` → 200 | PASS |
| API catalog | 40 products, 4 pages, page size 10 | PASS |
| Web Home | `GET http://localhost:5015/` → 200 | PASS |
| Product listing | `GET /products` → 200 | PASS |
| Product detail | `GET /products/40` → 200 | PASS |
| Product images | 41/41 database image URLs → HTTP 200 image | PASS |
| Logs | API/Web không có fatal/unhandled/HTTP 500 | PASS |

SQL log có hai dòng login failure do một lệnh kiểm tra nội bộ truyền password literal trước restore; lệnh không chạy SQL và được sửa ngay. Không phải lỗi credential của API/Web; cả hai service hiện kết nối database thành công.

Trạng thái cuối:

- `db`, `api`, `web`: running + healthy.
- OOMKilled: false cho cả ba; restart count: 0.
- Memory quan sát: SQL ~1.05 GiB, API ~91 MiB, Web ~99 MiB.
- RAM host ổn định gần nhất: 2.73 GiB. Đây là mức đủ cho demo hiện tại nhưng cần đóng ứng dụng không cần thiết trước khi build/restart trên máy 16 GiB.

## 5. Runbook giảng viên

### Yêu cầu

- Windows 10/11, Docker Desktop chạy Linux containers, .NET SDK 8 cho Admin.
- Khuyến nghị tối thiểu 16 GiB RAM và ít nhất 5 GiB RAM trống trước lần start SQL/restore.
- File `.env` local có password SA mạnh và `PRODUCT_UPLOADS_PATH` tuyệt đối; không commit `.env`.
- Bản backup demo hợp lệ được chuyển riêng, ngoài Git repository; không dùng dữ liệu cá nhân thật.

### Validate, build và chạy

```powershell
Copy-Item .env.example .env
# Điền các giá trị local trong .env.
docker compose -f docker-compose.yml -f docker-compose.restore.yml config --quiet
docker compose -f docker-compose.yml -f docker-compose.restore.yml build web api
docker compose -f docker-compose.yml -f docker-compose.restore.yml up -d
docker compose -f docker-compose.yml -f docker-compose.restore.yml ps
```

Trên máy hiện tại target volume đã có `PhoneStoreDB`; không restore lại. Trên máy mới, đặt backup demo ngoài repository vào thư mục bind mount, khởi động riêng `db`, chạy HEADERONLY/FILELISTONLY/VERIFYONLY WITH CHECKSUM rồi restore một lần vào database chưa tồn tại bằng `WITH MOVE`, không `REPLACE`. Chi tiết ở `docs/LOCAL_DEVELOPMENT.md`.

### URL và logs

```powershell
Invoke-WebRequest http://localhost:5025/api/health -UseBasicParsing
Invoke-WebRequest http://localhost:5015/ -UseBasicParsing
Invoke-WebRequest http://localhost:5015/products -UseBasicParsing
docker compose -f docker-compose.yml -f docker-compose.restore.yml logs --tail 100 db api web
```

### Dừng an toàn

```powershell
docker compose -f docker-compose.yml -f docker-compose.restore.yml stop
```

Không dùng `docker compose down -v`, không xóa named volume và không restore lại database đã tồn tại.

### Admin WinForms

```powershell
dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj
```

Admin mặc định dùng SQL Server Windows/local. Nếu muốn kết nối SQL Docker, cấu hình riêng `localhost,14334` bằng secret local rồi nghiệm thu read-only trước; không ghi credential vào source.

## 6. Giới hạn

- Nghiệm thu dùng HTTP/runtime read-only; không tạo tài khoản, cart, order hoặc DML.
- Không chạy browser visual automation lại vì không sửa UI.
- Persistence qua một chu kỳ stop/start sau restore chưa được thử trong S31.5F để tránh lặp thao tác shutdown khi RAM thấp; named volume đã được xác minh đúng và vẫn tồn tại.
- Không commit/push, không xóa backup/container/volume, không thay đổi SQL Server Windows.

