# S31.5G — Admin WinForms ↔ Docker SQL Server

Ngày xác minh: 2026-11-14  
Trạng thái: **READY FOR USER TEST**

## Kết quả

- Không cần sửa application source hoặc business logic.
- `AdminServices.Create()` nạp `appsettings.json`, sau đó `AddEnvironmentVariables()`. Biến `ConnectionStrings__PhoneStoreConnection` override connection string mặc định cho riêng process Admin.
- `PhoneStoreDbContext` tiếp tục được đăng ký qua DI bằng `UseSqlServer(connection)`; toàn bộ Business/Data hiện tại được giữ nguyên.
- Mặc định `appsettings.json` vẫn dùng SQL Server Windows với Windows Authentication. Xóa environment override là quay lại cấu hình cũ.
- SQL Docker dùng SQL Authentication trên `127.0.0.1,14334`, database `PhoneStoreDB`, `Encrypt=True;TrustServerCertificate=True`, khớp chính sách TLS hiện tại của Compose.
- `localhost,14334` bị ODBC Driver 17 login timeout trên host này dù listener hoạt động; loopback IPv4 `127.0.0.1,14334` PASS và được dùng trong runbook.

## Bằng chứng read-only

Kết nối từ Windows host tới Docker SQL bằng password đọc vào biến process `SQLCMDPASSWORD`, không in/log secret:

- Docker SQL: running + healthy, OOM=false, restart=0.
- Database: `PhoneStoreDB`.
- SQL Server: 16.0.4315.2.
- Products: 40.
- Orders: 12.
- Active Admins: 1.
- Không INSERT/UPDATE/DELETE, migration hoặc seed.

Đường dẫn ảnh Admin resolve thành `C:\PhoneStore\PhoneStore\src\PhoneStore.Web\wwwroot\uploads\products`, khớp chính xác `PRODUCT_UPLOADS_PATH` Docker; 42 files trên disk. Vì vậy Admin Windows và Web Docker dùng chung storage ảnh host.

## Build

```text
dotnet build src/PhoneStore.Admin/PhoneStore.Admin.csproj --no-restore --disable-build-servers -m:1 -nologo -o scratch/s31_5g/admin-out
```

Kết quả: **0 warnings, 0 errors**. Không khởi động Admin trong task.

## Chạy và quay lại SQL Windows

Lệnh chạy an toàn, không hardcode/echo secret, nằm trong `docs/LOCAL_DEVELOPMENT.md#chạy-admin-với-sql-server-docker`.

- Docker SQL: đặt environment override trong PowerShell rồi chạy Admin.
- Windows SQL: xóa `ConnectionStrings__PhoneStoreConnection` hoặc mở PowerShell mới, sau đó chạy Admin bình thường.

## User acceptance còn lại

1. Chạy lệnh Admin Docker SQL từ `LOCAL_DEVELOPMENT.md`.
2. Đăng nhập bằng Admin fixture hiện có; không reset password.
3. Kiểm tra read-only Dashboard, Products và Orders hiển thị dữ liệu Docker.
4. Không bấm Save/Delete/Update trong lượt nghiệm thu đầu.
5. Đóng Admin; biến override được cleanup trong `finally`.

UI UX Pro Max được áp dụng ở mức bảo toàn: không thay UI, không thay navigation/focus/layout, chỉ thay data target bằng cấu hình process-local.

