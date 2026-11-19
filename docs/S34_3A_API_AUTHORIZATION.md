# S34.3A — Phân quyền REST API

## Phạm vi và checkpoint

Ngày 2026-11-19, nhánh main tại 3660ef4 (S34.2R-04), working tree sạch trước khi sửa. Docker báo cả năm container healthy. Web và API nghiệp vụ đều có Microsoft.AspNetCore.App 10.0.15. File backup R-04 còn đọc được metadata filesystem (827.392 byte), hai image rollback .NET 8 còn trên máy. Kết quả VERIFYONLY thuộc R-04. Backup nằm trên cùng volume SQL.

## Quyết định xác thực

Customer Web gọi Catalog công khai từ server. Customer đăng nhập Web bằng cookie, WinForms xác thực Admin qua Business và duy trì AdminSession. Hai ứng dụng hiện dùng PasswordHasher<User>. Chưa có client cần đăng nhập qua API hoặc issuer OIDC đã cấu hình. Scheme PhoneStore.Api hiện luôn trả NoResult. Các đường dẫn thử policy chỉ tồn tại trong test host.

## Ranh giới truy cập

- Các action hiện có của Products, Brands, Categories và Health khai báo AllowAnonymous. Swagger tiếp tục theo môi trường. Controller action mới thiếu khai báo audience bị chặn.
- Api.Customer và Api.Admin yêu cầu danh tính đã xác thực từ scheme API và đúng một NameIdentifier là UserId dương. Mỗi lần đánh giá, EF kiểm tra Users.IsActive và Roles.RoleName bằng truy vấn tồn tại. Role claim do client mang theo không cấp quyền. Truy vấn không đọc password hash.
- Challenge trả 401, forbid trả 403, cùng application/problem+json và traceId. Tài khoản inactive hoặc sai role bị từ chối. Lỗi truy vấn đi qua exception handler hiện tại và action không chạy.
- Khi có client được phê duyệt, tích hợp issuer chuẩn, kiểm tra issuer/audience/chữ ký/hạn dùng và ánh xạ ổn định cặp issuer/subject sang UserId nội bộ. Phải giữ semantics challenge/forbid và bổ sung kiểm thử bearer thật. Với API tài nguyên cá nhân, bổ sung kiểm tra ownership cho từng đối tượng. Quy ước mặc định ở đây áp dụng cho MVC controllers; nếu bổ sung minimal API, cần cấu hình authorization cho route group.

## Bằng chứng

- Test HTTP dùng WebApplicationFactory, controller chỉ nạp trong test và fixture SQLite trong bộ test. Scheme thật từ chối bearer/cookie mẫu. Với danh tính fixture, kiểm tra cả hai chiều Customer/Admin, claim Admin không khớp database, hai loại tài khoản bị disable giữa các request, action chưa phân loại, response không redirect và ProblemDetails kèm traceId.
- SELECT trên SQL Docker xác nhận PhoneStoreDB ONLINE, Users 3, Roles 2, Products 40, Orders 15, khớp số lượng ở checkpoint. Không có thao tác ghi, migration, tạo database SQL Server hay deploy.
- Build Release (solution, --no-restore) thành công với 0 warning/error. Test HTTP ApiAuthorizationTests 2/2 PASS. Toàn bộ PhoneStore.Tests trên .NET 10 chạy tuần tự 265/265 PASS, 0 skipped (2 phút 43 giây).
