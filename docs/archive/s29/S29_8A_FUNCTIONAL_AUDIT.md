# S29.8A — Audit chức năng PhoneStore

Ngày: 2026-11-01, America/Los_Angeles. Ưu tiên hoàn thiện chức năng; hoãn UI/UX trừ lỗi cản trở sử dụng.
Phạm vi: đọc source/tài liệu, GET HTTP, SELECT, điều hướng/tìm kiếm WinForms và tests SQLite cách ly. Không ghi PhoneStoreDB, không sửa chức năng/ảnh hoặc chạy import/migration/restore.

PASS chỉ áp dụng phạm vi bằng chứng ghi trong từng dòng; không suy từ service test sang toàn bộ giao diện. PARTIAL: có triển khai nhưng thiếu kiểm thử cần thiết. FAIL: đường logic có lỗi xác định, ghi rõ source hay runtime. MISSING: chưa thấy triển khai trong luồng đã kiểm tra.

## 1. Customer Web

| Chức năng | Trạng thái | Bằng chứng và giới hạn |
|---|---|---|
| Đăng ký | PARTIAL | AccountController.Register, AuthService, AuthUserStore; Authentication/SingleAdmin tests PASS, GET /account/register=200. Chưa POST đăng ký/cookie trên Web test. |
| Đăng nhập | PARTIAL | AuthService kiểm password/Customer/active, tests PASS; GET /account/login=200. Chưa xác minh cookie/RememberMe/rehash qua HTTP authenticated. |
| Đăng xuất | PARTIAL | AccountController.Logout là POST có Authorize/anti-forgery; chưa có test HTTP đăng nhập→logout. |
| Xem catalog | PASS | GET /=200 với6 cards, /products=200 với40 cards; SQL40 active. |
| Tìm kiếm | PASS | GET search=iPhone→6, chuỗi không tồn tại→0; ProductCatalogReader lọc tên/brand. |
| Lọc thương hiệu/danh mục/giá | PASS | brandId1→6, categoryId1→40, 10–20 triệu→17; brandId=-1 và min>max→0 cùng thông báo validation trong code. Phạm vi dữ liệu demo hiện tại. |
| Sắp xếp | PASS | price-asc/price-desc/name-asc: HTTP200,40 rows, thứ tự giá/tên được đối chiếu tự động. |
| Chi tiết/ảnh | PASS | S29.7F đã kiểm40/40 detail/name/image/price/max quantity và40 ảnh200; audit này xác minh invalid id/không tồn tại→404. |
| Xem giỏ trống | PASS | GET /cart=200; SessionCart/CartPresenter dùng dữ liệu session và giá server. |
| Thêm/cập nhật/xóa dòng giỏ | PARTIAL | CartController có POST anti-forgery, quantity>0, kiểm active/stock, chống overflow khi cộng. Chưa test HTTP session có sản phẩm; không thực hiện POST trong audit. |
| Đặt hàng/kiểm tồn | PARTIAL | OrderService/PurchaseStore, CheckoutTests PASS giá server/trừ kho/rollback; GET /checkout anonymous→302 login. Chưa E2E SQL concurrency/commit uncertain. |
| Lịch sử/chi tiết đơn | PARTIAL | OrdersController, PurchaseStore lọc UserId; ownership tests PASS, anonymous /orders và /orders/1→302. Chưa dữ liệu order để kiểm giao diện SQL. |
| Thông tin tài khoản | MISSING | AccountController chỉ register/login/logout/access-denied, không profile action/view; /account và /account/profile→404. Layout chỉ hiển thị danh tính, không có quy trình tự xem/sửa hồ sơ. |

Source chính: [controllers](../../../src/PhoneStore.Web/Controllers), [catalog reader](../../../src/PhoneStore.Data/Catalog/ProductCatalogReader.cs), [purchasing](../../../src/PhoneStore.Web/Purchasing).

## 2. Admin WinForms

Người dùng xác nhận đăng nhập và hiển thị dữ liệu. Audit đọc cửa sổ PID8116, tiêu đề PhoneStore Admin, email admin@admin.com; chỉ invoke NavigationButton0–6 và bộ lọc tìm kiếm, không bấm nút ghi hay logout.

| Chức năng | Trạng thái | Bằng chứng và giới hạn |
|---|---|---|
| Đăng nhập Admin | PASS | Xác nhận thủ công của người dùng + shell authenticated hiện danh tính đúng; AdminAuthService tests PASS active/inactive/Customer forbidden. Sai password trên GUI chưa thử. |
| Đăng xuất | PARTIAL | AdminApplicationContext.OnLogout clear session, dispose shell, mở login; có LogoutButton, chưa thao tác để giữ phiên hiện tại. |
| Phân quyền | PARTIAL | MainForm/AdminSession/CatalogManagementGateway.RequireAdmin kiểm role, service login tests PASS; chưa test tự động gateway/logout session. Gateway dựa vào session đã đăng nhập, không truy vấn lại role/active mỗi thao tác. |
| Dashboard | PASS | Mở Tổng quan:40 SP, tồn740,0 Customer,0 Orders,0 doanh thu; cảnh báo1 SP (SQL xác nhận OPPO Find N3 tồn5). Chỉ PASS trạng thái dữ liệu hiện tại. |
| Danh sách sản phẩm | PASS | ProductsGrid40 rows, status40 sản phẩm; UI không lỗi tải. |
| Thêm/sửa/ngừng hoạt động SP | PARTIAL / FAIL sửa tồn cũ | Có dialog/service/store và validation; chưa kiểm thử ghi. Riêng đường Save ghi lại stock từ form có lỗi lost update theo source, xem P1-01. |
| Quản lý ảnh | PARTIAL | ProductImagesDialog/Add/Remove/Primary/Order, LocalProductImageStorage, transaction/lock parent row có triển khai. Chưa mở dialog hoặc ghi/kiểm cleanup runtime; không thay ảnh demo. |
| Tìm kiếm/lọc SP | PASS / PARTIAL | UI tìm iPhone→6 rows, trả bộ lọc ban đầu; store có brand/category/active filters. Các combo filter Admin chưa thao tác đầy đủ. |
| Thương hiệu | PASS đọc / PARTIAL ghi | Mở màn hình:8 rows. LookupManagementService có thêm/sửa/active/chặn trùng tên; chưa chạy các thao tác ghi. |
| Danh mục | PASS đọc / PARTIAL ghi | Mở màn hình:1 row; cùng LookupManagementService/Store. Chưa thêm/sửa/active trong DB test. |
| Khách hàng | PARTIAL | Màn hình tải0 rows đúng SQL; CustomerManagementService/Store chỉ Customer, test không sửa/hạ quyền Admin PASS. Sửa/khóa khách hàng chưa kiểm GUI. |
| Xử lý đơn/trạng thái | PARTIAL | Màn hình0 rows; OrderWorkflowTests PASS transition/expected status/hủy hoàn kho một lần. Chưa thao tác đơn qua UI SQL. |
| Tồn kho | PARTIAL | Có cột tồn trong sản phẩm và cảnh báo Dashboard/Reports; chưa có quy trình kiểm kê/điều chỉnh độc lập được xác minh; sửa tồn có P1-01. Không tự suy đây là tính năng bắt buộc mới. |
| Báo cáo doanh thu | PASS dữ liệu rỗng / PARTIAL nghiệp vụ | Mở Báo cáo:40 SP/tồn740/0 doanh thu, không lỗi runtime; chưa dữ liệu đa trạng thái/ngày để đối chiếu. Source tổng/trend/average chỉ Completed. |

Source chính: [MainForm](../../../src/PhoneStore.Admin/MainForm.cs), [controls](../../../src/PhoneStore.Admin/Controls), [management services](../../../src/PhoneStore.Business/Management).

## 3. Backend và SQL Server

| Hạng mục | Trạng thái | Bằng chứng và giới hạn |
|---|---|---|
| Luồng thực thi | PASS về kiến trúc | Web MVC controllers→Business services→Data stores/readers→EF; WinForms→gateway→services/stores. Không tìm thấy REST API độc lập/ApiController; đây không phải lỗi của kiến trúc hiện tại. |
| Validation/xử lý lỗi | PARTIAL | DataAnnotations + service quantity/price/FK/role/status validation; catches DbException/DbUpdateException trong nhiều luồng. Chưa test fault injection/SQL timeout/mất kết nối đồng bộ và toàn bộ HTTP error paths. |
| Transaction đặt hàng | PASS service / PARTIAL SQL đồng thời | Serializable bao quanh customer/products/order/details/stock, tests SQLite giá snapshot và rollback PASS. Không coi SQLite là bằng chứng locking SQL Server. |
| Tránh bán vượt tồn | PARTIAL | Kiểm tồn trong transaction, check stock>=0 live enabled/trusted. Chưa race SQL hai checkout, checkout với Admin save/cancel, deadlock/retry/commit uncertainty. |
| Chống đặt hàng trùng | PARTIAL | CheckoutSubmissionGuard semaphore+memory cache1h; không thấy khóa idempotency bền vững trên Order theo submission token. Restart/nhiều instance chưa được bảo đảm. |
| Customer/Admin/ownership | PASS service / PARTIAL E2E | Customer-only CreateCustomerAsync, role checks, CustomerCookieEvents revalidate, query ownership; suite PASS; anonymous protected routes redirect. Chưa session test authenticated đầy đủ. |
| Một Admin | PASS phạm vi hiện tại | SELECT1 Admin active; UX_Users_SingleAdmin unique filter RoleId=1 enabled; CK_Roles_AdminIdentity enabled/trusted, RolesAdmin1/Customer2. S29.4 SQL isolation và suite duplicate/inactive/promotion PASS. Không thử DML main. |
| Đồng bộ Web/Admin | PASS đọc / PARTIAL ghi | Cùng localhost/PhoneStoreDB; Web40, Admin40, Dashboard740 khớp SQL. Chưa Web checkout→Admin status→Web receipt end-to-end. |
| Ràng buộc | PASS metadata |7 FK và7 check đều enabled/trusted; unique email/order code/role name và singleAdmin đang bật. Không thử ghi để chứng minh mọi constraint. |
| Revenue Completed | PASS source / PARTIAL dữ liệu | ReportingStore lọc Completed cho tổng/trend/average;0 runtime đúng. BestSeller SalesValue gồm Confirmed/Shipping/Completed, là metric khác; không tự gọi đây là doanh thu Completed sai. |

Baseline SELECT audit: DESKTOP-ES9362P/default instance, PhoneStoreDB ONLINE; Products40/Images40/Brands8/Categories1/Users1/Roles2/Orders0/OrderDetails0,40 active/tồn740; AdminRoleId1 active. History giữ InitialCreate và EnforceSingleAdmin. Không chọn password/hash.

## 4. Lỗi và rủi ro theo ưu tiên

Không phát hiện P0 trong phạm vi đã chạy; không suy ra toàn hệ thống không có P0.

| Mã | Mức | Loại / bằng chứng | Ảnh hưởng và đề xuất |
|---|---|---|---|
| P1-01 | P1 | FAIL logic theo source, chưa tái hiện live: ProductEditDialog dòng190/224 lấy và gửi stock cũ; ProductEdit không có original/version; ProductManagementService dòng50 gán stock không đối chiếu. Serializable khi Save không bảo vệ quãng thời gian form đã mở. | Ví dụ theo đường code: mở form tồn10→checkout còn9→lưu đổi tên vẫn gửi10→stock trở lại10. Bổ sung kiểm soát stock/version hoặc cập nhật có điều kiện; regression hai context, không đổi schema main. |
| P1-R1 | P1 | Rủi ro/chưa phải FAIL thực nghiệm: checkout guard chỉ memory, chưa test SQL concurrency và kết quả commit không rõ. | Kiểm thử hai checkout tranh stock cuối, hủy đồng thời, retry sau timeout/restart trước khi quyết định cần idempotency lưu DB. |
| P2-01 | P2 | MISSING thông tin tài khoản Customer, source +404 runtime. | Sau bảo vệ tồn, thêm luồng xem/sửa hồ sơ chính mình theo UserId; không nhận RoleId/email/password update ngoài phạm vi đã định. |
| P2-R1 | P2 | Thiếu test quản lý catalog/ảnh/report/Web cookie/session/gateway; không phải bằng chứng các luồng đang lỗi. | Bổ sung test theo từng service và E2E trên DB test được duyệt. |
| P2-R2 | P2 | Báo cáo nhận ngày giao diện local nhưng ReportingService chuyển thành UTC midnight; UI hiển thị ngày UTC ở một số màn hình. | Chốt quy ước timezone nghiệp vụ và kiểm biên ngày trước khi sửa; chưa khẳng định lệch yêu cầu khi chưa có đơn thật. |
| P2-R3 | P2 | Admin gateway chỉ tin session, không revalidate tài khoản mỗi lần; import có thể ghi đè khi chạy lại; header-only image validation không chứng minh decode. | Cân nhắc theo phạm vi vận hành, thêm negative tests; không chạy import lại. Không nâng thành lỗi runtime đã tái hiện. |

## 5. Bộ kiểm thử và khoảng trống

Lệnh đã chạy trong audit:

```powershell
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-restore --verbosity minimal
```

Kết quả:72 passed/0 failed/0 skipped. Lệnh có build dependencies Models/Data/Business/AdminBootstrap/Tests; không chạy build toàn solution hoặc test Web/WinForms. TestDatabase dùng SQLite memory hoặc file temp riêng, không dùng PhoneStoreDB.

Có: AuthenticationTests; CheckoutTests; OrderWorkflowTests; AdminBootstrapTests; SingleAdminPolicyTests; SingleAdminConstraintTests (gồm kiểm SQL migration script offline). Script tests/sql/S29_4_EnforceSingleAdmin.sql là bằng chứng SQL lịch sử riêng, KHÔNG chạy lại ở audit.

Thiếu cần ưu tiên: stale stock/product edit, lookup/product validation/duplicate/inactive, ảnh lỗi/copy cleanup/primary concurrency, report đa trạng thái và biên ngày, HTTP login/logout/anti-forgery/RememberMe/cookie revalidation, giỏ session, E2E checkout ownership/order-status, SQL race/rollback/timeout. Không tìm thấy test project tham chiếu Web hoặc Admin để kiểm controller/gateway trực tiếp.

## 6. Kế hoạch nhỏ, chờ phê duyệt

1. **S29.8B: bảo vệ sửa tồn kho cũ.** Dự kiến ProductEditDialog.cs, AdminCatalogModels.cs, ProductManagementService.cs, ProductManagementStore.cs và test mới. Ưu tiên cập nhật có điều kiện dựa giá trị gốc, không migration; từ chối save cũ rõ ràng, giữ tồn hiện tại. Viết regression trước, kiểm sửa thông tin/tồn hợp lệ và chống ghi đè sau checkout/hủy. Không sửa UI hình thức, không ghi DB chính.
2. **S29.8C: thông tin tài khoản Customer.** Chốt trường được phép, triển khai action/service/validation/ownership và tests; không nâng role/reset password; UI tối thiểu để dùng chức năng.
3. **S29.8D: SQL integration cách ly.** Đề xuất database mới PhoneStoreDB_FunctionalTest, phải kiểm chưa tồn tại và được phê duyệt riêng trước tạo/schema/fixture. Dữ liệu test tài khoản/sản phẩm/đơn và thư mục ảnh riêng; không chuyển ứng dụng đang chạy khỏi main. Kiểm checkout concurrency, cancellation, commit uncertainty, CRUD/ảnh/lookup/customer và doanh thu Completed. Không xóa DB test có sẵn.
4. **S29.8E: hoàn tất HTTP/WinForms acceptance.** Chạy app test cấu hình/port riêng sau duyệt, kiểm login/logout/cart/order/account và thao tác Admin; sửa từng lỗi có reproduction, build/test lại. Hoãn bố cục/màu sắc/responsive.

S29.8A chỉ tạo báo cáo và cập nhật PROGRESS. Dừng chờ duyệt S29.8B; chưa triển khai sửa hoặc tạo SQL test database.
