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
## Tài khoản Admin mẫu

- Email: admin@phonestore.vn
- Mật khẩu: Admin@123

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

