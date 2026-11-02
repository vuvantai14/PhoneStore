# PhoneStore

Đề tài: Xây dựng hệ thống quản lý và bán điện thoại di động.

## Công nghệ

- C# / .NET 8
- ASP.NET Core MVC
- Windows Forms
- Entity Framework Core
- SQL Server
- HTML/CSS/Bootstrap/JavaScript

## Chức năng

### Customer Web

- Xem, tìm kiếm, lọc sản phẩm
- Xem chi tiết sản phẩm
- Đăng ký / đăng nhập / đăng xuất
- Giỏ hàng
- Checkout
- Lịch sử và chi tiết đơn hàng
- Theo dõi trạng thái đơn hàng

### Admin WinForms

- Đăng nhập Admin
- Dashboard
- Quản lý thương hiệu
- Quản lý danh mục
- Quản lý sản phẩm và hình ảnh
- Quản lý khách hàng
- Quản lý đơn hàng
- Cập nhật trạng thái đơn hàng
- Báo cáo và thống kê
## Khởi tạo Admin đầu tiên

Chạy công cụ độc lập từ thư mục chứa `PhoneStore.sln`. Web và Admin không tự tạo tài khoản.
Công cụ đọc cấu hình Web qua `PhoneStoreDbContextFactory`; biến môi trường
`ConnectionStrings__PhoneStoreConnection` được ưu tiên. Chỉ hỗ trợ SQL Server mặc định
`localhost`, Windows Authentication và database đã có schema cùng role `Admin`.

Kiểm tra trước khi tạo (dry-run, chỉ đọc database, không yêu cầu mật khẩu):

```powershell
dotnet run --project tools/PhoneStore.AdminBootstrap -- --name="Quản trị viên" --email=admin@example.com
```

Sau khi kiểm tra đúng đích và được người quản lý database cho phép, tạo tài khoản trong terminal tương tác:

```powershell
dotnet run --project tools/PhoneStore.AdminBootstrap -- --name="Quản trị viên" --email=admin@example.com --execute --confirm-server=localhost --confirm-database=PhoneStoreDB
```

Thay họ tên/email bằng thông tin thực tế. Nhập mật khẩu ẩn hai lần (8–128 ký tự,
không chỉ gồm khoảng trắng); không truyền mật khẩu qua tham số, pipe hoặc file.
Mật khẩu được hash bằng `PasswordHasher<User>` tương thích đăng nhập Admin.

`--execute` bắt buộc xác nhận server và database khớp kết nối thực tế. Công cụ từ chối
nếu đã có bất kỳ Admin nào, kể cả bị khóa, hoặc email trùng tài khoản hiện có;
không ghi đè tài khoản, nâng quyền Customer, tạo database, role hoặc chạy migration.
Dry-run thành công không bảo đảm lần execute sau sẽ thành công nếu dữ liệu đã thay đổi.
Nếu báo lỗi khi ghi/commit, kiểm tra trạng thái bằng truy vấn chỉ đọc trước khi thử lại.

## Database

Database: `PhoneStoreDB`

Tables:

- Roles
- Users
- Brands
- Categories
- Products
- ProductImages
- Orders
- OrderDetails


## Cấu trúc

- PhoneStore.Models
- PhoneStore.Data
- PhoneStore.Business
- PhoneStore.Web
- PhoneStore.Admin
- PhoneStore.Tests

## Cách chạy

Build:

```bash
dotnet build PhoneStore.sln
```

Customer Web:

```bash
dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj
```

Admin:

```bash
dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj
```

Test:

```bash
dotnet test PhoneStore.sln
```

