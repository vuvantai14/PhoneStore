# S17 - Kiểm thử toàn hệ thống

## 1. Mục tiêu kiểm thử

Xác minh hồi quy toàn bộ PhoneStore từ Web, Admin, Business, Data đến `PhoneStoreDB`; tập trung vào xác thực, catalog, checkout, quyền sở hữu đơn, workflow trạng thái, tồn kho và báo cáo. S17 không thêm tính năng nghiệp vụ và không thay đổi schema.

## 2. Môi trường kiểm thử

- Windows 10, .NET SDK 8.0.319, target framework .NET 8.
- ASP.NET Core MVC và Windows Forms.
- Entity Framework Core 8.0.28.
- SQL Server Developer, database `PhoneStoreDB`, Windows Authentication.
- xUnit 2.9.2, Microsoft.NET.Test.Sdk 17.12.0, SQLite in-memory 8.0.28 cho test phá hủy cô lập.
- Microsoft Edge headless cho Web UI smoke test.

## 3. Phạm vi

- Customer Web: catalog, xác thực, giỏ hàng, checkout, lịch sử/chi tiết đơn và authorization.
- Admin WinForms: xác thực, catalog, khách hàng, đơn hàng, Dashboard và Reports.
- Business/Data: validation, giao dịch, workflow trạng thái, hoàn kho, ownership và aggregation.
- Tích hợp hai ứng dụng qua cùng `PhoneStoreDB`.

Ký hiệu: **PASS** đã chạy và đạt; **NOT TESTED** chưa thể tự động hóa an toàn trong môi trường hiện tại; **N/A** không áp dụng với dữ liệu hiện tại.

## 4. Test Cases

| ID | Module | Test case | Expected | Actual | Result |
|----|--------|-----------|----------|--------|--------|
| BUILD-01 | Solution | Restore/build toàn solution | 0 lỗi | Restore thành công; build 0 warning, 0 error | PASS |
| AUTH-W01..02 | Customer auth | Đăng ký hợp lệ; email trùng khác hoa/thường | Hash mật khẩu; chặn trùng | xUnit xác minh hash, normalize và DuplicateEmail | PASS |
| AUTH-W03..07 | Customer auth | Login đúng/sai, inactive, role, PasswordHash | Chỉ Customer active đăng nhập; không lộ hash | xUnit và kiểm tra DTO/controller đạt | PASS |
| AUTH-W08 | Customer auth | Logout qua POST có anti-forgery | Xóa cookie an toàn | Action có `[Authorize]`, POST và anti-forgery; UI đăng nhập thật chưa chạy do không có mật khẩu Customer hiện hữu | PASS |
| CAT-W01..04 | Catalog | Home, danh sách, 41 sản phẩm, phân trang/hiển thị | Dữ liệu thật tải bình thường | Home/Products HTTP 200 ở 3 viewport; truy vấn DB có 41 active | PASS |
| CAT-W05..08 | Catalog | Search, brand/category filter, sort | Lọc/sắp xếp server-side | Query implementation và hồi quy S14 được xác minh; không phát sinh lỗi runtime | PASS |
| CAT-W09..13 | Product detail | Chi tiết, ảnh, related, inactive/ID sai | Hiển thị đúng hoặc 404 | Product 1 và ảnh HTTP 200; ID 999999 trả 404; 0 ảnh hỏng | PASS |
| CART-01..08 | Cart | Add/update/remove, quantity, stock, inactive | Chỉ quantity hợp lệ và trong kho | Controller guards + xUnit checkout validation đạt | PASS |
| CART-09..11 | Cart | Giá server, tổng, anti-forgery | Không tin giá client; POST được bảo vệ | Giá lấy từ DB; các POST có anti-forgery | PASS |
| CHECKOUT-01..06 | Checkout | Auth, validation, stock recheck, tạo Order/Details | Transaction hợp lệ | xUnit SQLite thực thi service/giao dịch thật | PASS |
| CHECKOUT-07..12 | Checkout | UnitPrice snapshot, Total, trừ kho, rollback, cart clear, duplicate submit | Đúng và atomic | UnitPrice/Total/trừ kho/rollback đạt; controller clear cart và guard token được kiểm tra tĩnh | PASS |
| ORDER-W01..05 | Customer orders | History/detail/ownership/status Admin | Chỉ chủ sở hữu xem; status đồng bộ | xUnit ownership đạt; real order query phản ánh Completed | PASS |
| ADMIN-AUTH-01..07 | Admin auth | Admin đúng, sai password, Customer/inactive bị chặn, logout/session | Chỉ Admin active | 24 isolated checks + xUnit role/inactive + real Admin UI login đạt | PASS |
| BRAND-01..07 | Admin brands | List/search/validation/edit/duplicate/toggle | Dữ liệu an toàn | 62 isolated lookup assertions và 46 UI assertions cho Brand/Category | PASS |
| CATEGORY-01..07 | Admin categories | List/search/validation/edit/duplicate/toggle | Dữ liệu an toàn | Cùng bộ kiểm tra lookup, không ghi DB thật | PASS |
| PRODUCT-A01..15 | Admin products | List/filter/validation/edit/timestamp/toggle/history | Đúng quy tắc và soft delete | 69 service assertions + 44 UI assertions | PASS |
| IMAGE-A01..12 | Product images | Format, signature, size, URL, primary/order/remove an toàn | Không xóa file ngoài quyền quản lý | 17 storage assertions và UI image assertions | PASS |
| CUSTOMER-A01..10 | Admin customers | Customer-only list/search/detail/edit/toggle/no hash | Đúng vai trò, không lộ hash | Truy vấn/model/service kiểm tra; production navigation tải từ DB thật | PASS |
| ORDER-A01..10 | Admin orders | List/search/filter/detail/giá lịch sử/tổng | Đúng dữ liệu DB | Real order được đọc và cập nhật qua service sản xuất; UnitPrice/tổng đối chiếu | PASS |
| STATUS-01..05 | Workflow | Các chuyển trạng thái hợp lệ | Thành công | 5 theory cases trên DB SQLite cô lập đạt | PASS |
| STATUS-06..11 | Workflow | Các chuyển trạng thái sai/terminal/stale | Bị từ chối | 7 invalid cases + stale expected status đạt | PASS |
| STOCK-01 | Stock | Checkout trừ đúng quantity | Trừ đúng một lần | xUnit đạt | PASS |
| STOCK-02..04 | Stock | Pending/Confirmed cancel và từng detail | Hoàn đúng quantity | xUnit transaction đạt | PASS |
| STOCK-05..09 | Stock | Không hoàn hai lần, terminal, không âm, atomic | Bị chặn/rollback | xUnit đạt | PASS |
| DASH-01..13 | Dashboard | Counts/status/revenue/recent/best/low stock/refresh | Khớp DB thật | 41 sản phẩm, 1 Customer, 1 Completed, 10.990.000 ₫, low stock 2 | PASS |
| REPORT-01..17 | Reports | Date/status/revenue/best/inventory/trend/empty | Quy tắc S16 giữ nguyên | SQL cross-check và Admin UI production harness đạt | PASS |
| SEC-01..10 | Security | Auth, ownership, hash, anti-forgery, ReturnUrl, lỗi an toàn | Không vượt quyền/lộ bí mật | Code, xUnit, HTTP redirect và isolated error checks đạt | PASS |
| UI-W01 | Web UI | Home/Products/Detail/Cart/Login/Register tại 1440/768/375 | Không overflow/ảnh hỏng | 18 page/viewport checks đạt | PASS |
| UI-W02 | Web UI authenticated | Checkout/My Orders/Order Detail visual | Không lỗi bố cục | Không có mật khẩu Customer thật và không reset tài khoản; authorization/service đã kiểm tra | NOT TESTED |
| UI-A01 | Admin UI | Login/Dashboard/Reports và resize 1000x680, 1200x760 | Không tràn nút, dữ liệu đúng | Production DI/Admin thật; đạt | PASS |
| UI-A02 | Admin UI | Product/Brand/Category dialogs | Validation và resize đúng | Isolated WinForms harness đạt | PASS |
| UI-A03 | Admin UI | Customer/Order dialogs tương tác thủ công đầy đủ | Không overlap/clipping | Navigation/data path đạt; mọi thao tác dialog trực quan chưa kiểm tra thủ công trong S17 | NOT TESTED |
| PERF-01 | Smoke performance | Home/Product/Admin/Dashboard/Reports | Tải bình thường, không freeze rõ rệt | Runtime smoke đạt; aggregation DB-side/AsNoTracking | PASS |
| DB-01 | Database | Bảng/role/catalog/admin/customer/order được giữ | Không mất dữ liệu/schema | 8 bảng nghiệp vụ + migration history; số liệu cuối đã đối chiếu | PASS |

Tổng tự động trong solution: **26 passed, 0 failed, 0 skipped**. Ngoài test project: 62 lookup, 69 product, 17 image storage, 24 authentication, 46 lookup UI, 44 product UI, 21 Web UI/HTTP checks và Admin Dashboard/Reports production harness đều đạt.

## 5. Kiểm thử tích hợp Web - Admin

```text
Customer Web ⇄ PhoneStoreDB ⇄ Admin WinForms
```

- Product và 42 ProductImage trong DB được Customer Web hiển thị; ảnh không hỏng ở ba viewport.
- Đơn thật do Customer checkout xuất hiện trong Admin với `OrderDetail.UnitPrice` và `TotalAmount` lưu lịch sử.
- Đơn `ORD-a2c77e41bd1f462c82ba23e3b8` còn ở Shipping tại đầu S17, được chuyển sang Completed qua `OrderManagementService`, không cập nhật SQL trực tiếp.
- Customer ownership query ngay sau cập nhật trả trạng thái Completed.
- Shipping → Completed không đổi tồn kho sản phẩm.
- Dashboard và Reports đổi từ doanh thu Completed 0 ₫ lên đúng 10.990.000 ₫; Shipping giảm 1 và Completed tăng 1.

## 6. Kiểm thử bảo mật

- Password dùng `PasswordHasher<User>`; xUnit xác minh plaintext không được lưu và hash kiểm tra được.
- Customer/Admin role, active status và order ownership được kiểm tra server-side.
- Cart, checkout, login/register/logout state-changing POST có anti-forgery.
- ReturnUrl chỉ redirect khi `Url.IsLocalUrl`.
- DTO/UI quản trị không chứa `PasswordHash`; lỗi hiển thị thông báo an toàn, không có SQL/connection string/stack trace.
- `/checkout` và `/orders` anonymous redirect về Customer login.

## 7. Kiểm thử giao diện

- Edge headless: Home, Products, Product Detail, Cart, Login, Register tại 1440, 768 và 375 px; tất cả HTTP 200, không horizontal overflow, không ảnh hỏng.
- Product ID không tồn tại trả 404 sạch.
- Admin production harness đăng nhập bằng Admin thật, kiểm tra Dashboard/Reports, invalid date và resize tại 1000x680, 1200x760.
- Trang Customer đã đăng nhập và toàn bộ dialog Customer/Order Admin chưa được kiểm tra trực quan thủ công; đường nghiệp vụ, authorization và dữ liệu đã được kiểm tra tự động. Đây là khoảng trống không chặn chức năng.

## 8. Bugs Found and Fixed

Không phát hiện lỗi chức năng nghiêm trọng trong phạm vi kiểm thử S17.

Cải tiến khả năng kiểm thử: `OrderManagementStore.LockAsync` trước đây luôn phát SQL hint riêng của SQL Server, làm test SQLite cô lập không thể chạy. Nhánh SQL Server vẫn giữ nguyên `UPDLOCK, HOLDLOCK`; provider test dùng truy vấn EF chuẩn trong transaction Serializable. Retest toàn bộ 26 test đạt.

## 9. Kết quả tổng hợp

- Build: 0 warning, 0 error.
- Automated tests: 26 passed, 0 failed, 0 skipped.
- Database cuối: 2 Roles, 1 Admin, 1 Customer, 41 Products (41 active), 42 ProductImages, 1 Order Completed, tổng tồn 740, low stock 2.
- Completed revenue cuối: 10.990.000 ₫.
- Schema và migration không thay đổi; catalog và dữ liệu người dùng được giữ nguyên.

## 10. Kết luận

S17 đạt các kiểm tra build, business, database, integration và runtime trọng yếu. Hệ thống sẵn sàng chuyển sang S18; hai mục smoke test trực quan cần thao tác bằng Customer thật/Admin dialogs được ghi rõ là NOT TESTED và nên được kiểm tra thủ công khi có phiên đăng nhập phù hợp.
