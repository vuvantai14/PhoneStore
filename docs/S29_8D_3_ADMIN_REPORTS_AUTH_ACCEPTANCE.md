# S29.8D.3 — Admin Reports, Dashboard và Auth/Session

Ngày kiểm tra: 2026-11-02 (Asia/Bangkok). Phạm vi: source, Admin WinForms test thật và SQL chỉ đọc. Không ghi `PhoneStoreDB`, không tạo fixture mới, không sửa source/deploy/migration/restore/commit/push.

## Runtime và dữ liệu cách ly

- Admin test PID `9840`, executable trong `src/PhoneStore.Admin/obj/S29.8D.2C-20261102_035250536Z/app`.
- Cấu hình và session SQL xác nhận `localhost/PhoneStoreDB_StockConcurrencyTest`; uploads là thư mục test riêng.
- Không cần fixture mới: 10 Orders gồm Pending 4, Completed 1, Cancelled 5; Completed revenue 1.000.000, Cancelled amount 5.000.000.
- Cuối kiểm tra: test DB vẫn Products 5, Images 4, Brands 2, Categories 2, Users 5, Orders 10, Details 10. Product 13/14, Image 22/24/25/26 và năm file fixture/orphan giữ nguyên.

## Dashboard và Reports

| Hạng mục | Công thức/source | Kết quả |
|---|---|---|
| Active products / stock | Chỉ `Products.IsActive`; tổng stock kiểu `long` | PASS WinForms thật + SQL: 4 / 9 |
| Customers | RoleName `Customer`; tách tổng/active | PASS: 4 / 4 |
| Orders | Tất cả status; Pending hiển thị riêng | PASS: 10 / 4 |
| Completed revenue | Chỉ `Status=Completed`; không tính Cancelled | PASS: 1.000.000 / 1 Completed |
| Status chart | Đủ năm trạng thái, kể cả số 0 | PASS theo UI người dùng + SQL |
| Best sellers | Confirmed, Shipping, Completed; group theo Product | PASS source/SQL; khác chủ ý với KPI doanh thu Completed-only |
| Low stock | Active và stock `<=5`; hết hàng là stock 0 | PASS source/SQL |
| Report 01–02/11/2026 | Khoảng UTC nửa mở theo source | PASS UI: 10 đơn, 1 Completed, revenue/average 1.000.000, stock 9/4, hết hàng 2 |

UI dùng ngày local nhưng service chuyển trực tiếp thành UTC. Empty range và ca sát biên ngày/timezone chưa được nghiệm thu riêng.

## Auth và Session

- `AdminAuthService` kiểm email/password, active/hash bằng `PasswordHasher<User>`, rồi yêu cầu RoleName `Admin`.
- `AdminSession.Establish` và `MainForm` từ chối identity không phải Admin. `ShowControl` kiểm session/role khi chuyển module.
- `AdminApplicationContext.OnLogout` gọi `session.Clear()`, đóng/dispose `MainForm`, rồi mở `LoginForm` mới.
- PASS WinForms thật: logout một lần; PID 9840 chỉ còn một `LoginForm`, không còn `MainForm` để tiếp tục thao tác quản trị.
- PASS WinForms thật: mật khẩu cố ý sai hiển thị thông báo chung “Email hoặc mật khẩu không chính xác”, vẫn ở LoginForm.
- PASS WinForms thật: người dùng tự nhập credential test và đăng nhập lại; chỉ có một `MainForm`. SQL có đúng một session test DB và không có transaction mở.
- PASS automated: Customer/non-Admin trả `Forbidden` trong authentication tests. NOT TESTED WinForms thật vì không có credential Customer phù hợp và phạm vi cấm tạo/reset fixture.
- Giới hạn: session là snapshot trong memory; quyền/active không được tái đọc DB trước từng lệnh. Thay đổi quyền từ tiến trình khác giữa phiên chưa bị cưỡng chế tức thời.

## Build, regression và hậu kiểm

- `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8D.3/`: PASS, 0 warnings/errors.
- `dotnet test PhoneStore.sln --no-restore --no-build -p:OutputPath=bin/S29.8D.3/`: PASS 155/155, 0 failed/skipped.
- Main: Products 40, Images 40, stock 740, Orders 1 Cancelled, Details 1, Users 2, active Admin 1.
- Snapshot main: bảy bảng khớp SHA-256 JSON trực tiếp; Users và Orders được SELECT toàn bộ cột không nhạy cảm và khớp baseline. Như vậy 9/9 dữ liệu đối chiếu không đổi; khác biệt hash của hai bảng do đường xuất JSON/sqlcmd.
- 40/40 ảnh demo khớp SHA-256 baseline.

## Kết luận

- **Dashboard: PASS WinForms thật + SQL/source.**
- **Reports: PASS WinForms thật** cho dữ liệu dương và Completed-only; **PARTIAL** cho empty range/biên ngày-timezone.
- **Auth: PASS WinForms thật** cho Admin hợp lệ, sai mật khẩu và login lại; non-Admin **PASS automated / NOT TESTED WinForms thật**.
- **Session/logout: PASS WinForms thật**; revalidation quyền giữa phiên là rủi ro thiết kế còn mở.
- Tổng S29.8D.3: **PARTIAL**; không phát hiện lỗi KPI/session thực tế cần sửa ngay.
