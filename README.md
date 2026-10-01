# PhoneStore

Đề tài: Xây dựng hệ thống quản lý và bán điện thoại di động.

Kiến trúc:

- PhoneStore.Web: ASP.NET Core MVC dành cho khách hàng.
- PhoneStore.Admin: Windows Forms dành cho quản trị viên.
- PhoneStore.Business: xử lý nghiệp vụ dùng chung.
- PhoneStore.Data: tầng truy cập dữ liệu.
- PhoneStore.Models: model/entity dùng chung.
- Database: Microsoft SQL Server (dự kiến cho giai đoạn sau).

Đã khởi tạo Solution (S7) và nền tảng dữ liệu EF Core (S8); chưa triển khai chức năng nghiệp vụ.
Đọc `CONTEXT.md` trước khi làm việc. Migration đã tạo; cấu hình development dùng `taiiiii\SERVER`
với Windows Authentication. `PhoneStoreDB` đã được tạo và kiểm tra: đủ bảng theo S8,
chỉ seed roles Admin/Customer, các bảng nghiệp vụ khác chưa có dữ liệu.

Sử dụng .NET 8, SDK 8.0.319 (cấu hình trong `global.json`). Windows Forms cần Windows.

Build: `dotnet build PhoneStore.sln`

Chạy Web: `dotnet run --project src/PhoneStore.Web/PhoneStore.Web.csproj`

Chạy Admin trên Windows: `dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj`
