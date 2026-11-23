# S29.8E.1 — Rà soát chức năng Customer Web PhoneStore

Ngày audit: 2026-11-02 (Asia/Bangkok). Chỉ đọc source, tài liệu, tiến trình, HTTP, SQL và hash ảnh; không sửa source, không POST, DML, deploy, migration, seed, restore, commit hoặc push.

## 1. Kết luận

**PARTIAL**.

Luồng bán hàng Customer cốt lõi có source đầy đủ, automated tests và nhiều bằng chứng browser/main thực tế từ S29.8C–D.1. Không phát hiện P0 hay lỗi toàn vẹn dữ liệu mới. Tuy nhiên Customer Web **không chạy tại thời điểm audit**: không có process/listener cổng 5015 và HTTP không kết nối được. Vì vậy không thể gọi trạng thái hiện tại là PASS runtime hoặc tạo bằng chứng browser mới.

## 2. Runtime và dữ liệu

- Cấu hình source Web và Development cùng trỏ `localhost/PhoneStoreDB`; launch profile dùng `http://localhost:5015`.
- Không có `PhoneStore.Web`/`dotnet` phù hợp, không listener 5015; GET `http://localhost:5015/` không kết nối.
- Main SQL read-only: `DESKTOP-ES9362P/PhoneStoreDB` ONLINE; Products 40, ProductImages 40, tổng tồn 740, Customers 1, Orders 1 (Cancelled), một Admin active.
- 40/40 ảnh khớp SHA-256 baseline.
- Admin main không bị tác động.

## 3. Ma trận chức năng Customer

| Chức năng | Source / automated | Browser/runtime thực tế | Kết luận |
|---|---|---|---|
| Đăng ký | Customer role được lookup theo tên; caller không chọn RoleId; hash `PasswordHasher<User>`; email normalize/unique; CSRF + validation. AuthenticationTests PASS trong baseline 161. | Main C.7 tạo đúng một Customer RoleId2, active; người dùng tự nhập mật khẩu. | **PASS thực tế lịch sử + automated** |
| Đăng nhập | Chỉ active Customer; cookie HttpOnly/SameSite, local return URL; principal được revalidate với DB. Tests phủ đúng/sai/inactive/role. | Main C.7 login PASS. Runtime hiện không chạy. | **PASS thực tế lịch sử; hiện unavailable** |
| Đăng xuất | POST, `[Authorize]`, anti-forgery, sign-out cookie. | PASS HTTP cách ly; chưa có bằng chứng browser main mới trong E.1. | **PASS automated/HTTP cách ly; PARTIAL browser main** |
| Trang chủ | Lấy newest đa dạng, best sellers và brand; catalog chỉ đọc. | Trước đây Home HTTP200/6 cards. | **PASS thực tế lịch sử** |
| Danh sách/tìm/lọc/sort | Chỉ Product active; search tên/brand; brand/category/giá; sort newest/price/name; server validation khoảng giá. | Main trước đây Products40, search iPhone6, brand1=6, giá10–20m=17, sort route200. Chưa phủ mọi tổ hợp/biên. | **PASS thực tế trong phạm vi; PARTIAL exhaustive** |
| Chi tiết/ảnh | Chỉ Product active; ID sai/inactive trả 404; ảnh primary/order; related products. | 40 trang chi tiết và 40 ảnh HTTP200 ở C.6B; detail25/ID0 kiểm lại ở D. | **PASS thực tế lịch sử** |
| Thêm giỏ | POST + CSRF; đọc giá/kho/active từ server, cart theo session. | Main C.7 add Product25 qty1 PASS. | **PASS thực tế** |
| Sửa/xóa giỏ | Quantity dương, chống vượt kho/missing/inactive; remove theo session. | PASS HTTP cách ly và CartControllerTests; chưa thao tác lại main. | **PASS automated/isolated; PARTIAL main UI** |
| Giá/tổng server-side | CartPresenter và checkout tải lại Product/Price từ DB; client không gửi giá đáng tin cậy. | Main C.7 order giá 3.490.000 đúng server. | **PASS thực tế + automated** |
| Checkout/transaction | Customer policy, CSRF, shipping validation; Serializable; lock sản phẩm `UPDLOCK,HOLDLOCK`; order/detail/stock cùng transaction; failure rollback. | Main C.7 một checkout thật: đúng 1 Order/Detail, stock 38→37. | **PASS thực tế + automated/SQL isolated** |
| Idempotency/double submit | Token session → OrderCode bền theo user; unique DB/order lookup; `sp_getapplock`; guard process-local; không auto-retry. | PASS automated + SQL/HTTP cách ly; không replay main theo giới hạn an toàn. | **PASS isolated; NOT TESTED replay main** |
| Concurrency/oversell | Serializable, row locks theo ProductId, check stock trước decrement; DB check stock không âm. | SQL isolated concurrency PASS trước đây. | **PASS SQL isolated/automated** |
| Lịch sử/chi tiết | Query luôn theo authenticated UserId; receipt/history không nhận owner từ client. | Main C.7/7A hiển thị đúng đơn/trạng thái. | **PASS thực tế + automated** |
| Ownership đơn | Foreign order trả 404; checkout success cũng query theo UserId. | PASS automated/HTTP cách ly; không thử tài khoản thứ hai trên main. | **PASS automated/isolated** |
| Hủy/hoàn kho | Chỉ Pending/Confirmed; active owner; Serializable + order/product locks; terminal/double cancel không hoàn lần hai. | Main C.7A Confirmed→Cancelled, stock37→38; Web/Admin/SQL khớp. Double cancel chỉ PASS cách ly. | **PASS thực tế + isolated** |
| Hồ sơ xem/sửa | Customer policy; identity lấy từ claim; chỉ FullName/Phone/Address; Email readonly; conditional update; CSRF; renew Name claim; role/hash/id không bind. | D.1C.2 UI thật cập nhật/khôi phục và greeting/cookie PASS. | **PASS thực tế + automated** |
| Session bị inactive/đổi role | Cookie validation recheck DB và fail-closed khi DbException. | Tests profile/auth phủ inactive/no-longer-customer; chưa revoke Customer main. | **PASS automated; NOT TESTED main** |

## 4. Transaction, validation và bảo mật

- Mọi POST quan trọng có anti-forgery: registration/login/logout, cart, checkout, cancel, profile.
- Checkout, orders và profile yêu cầu policy `Customer`; Customer role không thể tự chọn khi đăng ký.
- Giá, tổng tiền, active status và stock được lấy lại từ DB ở server.
- Order history/detail/cancel đều scope theo `UserId`; không phân biệt foreign/missing trong response nhạy cảm.
- Checkout không tự retry khi outcome không rõ; idempotency bền qua restart nhờ OrderCode/token.
- Hủy đơn và hoàn kho nằm trong transaction; order/product bị khóa và terminal status chặn hoàn lặp.
- Cookie authentication được revalidate với active Customer/role/email/name. Session cart là theo browser, không phải bảng cart đa thiết bị; đây là lựa chọn kiến trúc, không phải lỗi đã chứng minh.

## 5. Automated tests hiện có

Baseline gần nhất **161/161 PASS**, build 0 warnings/errors; không chạy lại trong audit này vì không sửa source và Web runtime đang dừng.

Các nhóm liên quan trực tiếp:

- `AuthenticationTests`: registration hash/normalize/duplicate; Customer/Admin/active/role; fail-closed authorization.
- `CartControllerTests`: add/update/remove, server price, session isolation, invalid/excess quantity, missing/inactive/out-of-stock, token rotation/replay.
- `CheckoutTests`: server price/stock, invalid quantity, insufficient-stock rollback, duplicate lines, ownership.
- `CommerceWorkflowTests`: idempotency across context, token scope, inactive Customer, double checkout/cancel, state rules, rollback contamination, Web–Admin status sync.
- `OrderWorkflowTests`: valid/invalid transition, stale status, restock exactly once.
- `CustomerProfileTests` và `CustomerProfileHttpTests`: ownership, allowed columns, validation, CSRF, overposting, cookie renewal, inactive/role revoke.
- SQL/HTTP harness cách ly từng kiểm concurrency, locks, checkout/cancel và integration; không được tính vào 161 xUnit nếu báo cáo ghi riêng.

Khoảng trống test:

- Không có browser acceptance mới vì Web hiện dừng.
- Chưa exhaustive mọi tổ hợp catalog filter/sort và ảnh thiếu/inactive product.
- RememberMe/expiry dài ngày, mất session giữa checkout và lỗi mạng thực tế chưa được kiểm end-to-end dài hạn.
- Không stress tải lớn hoặc fault injection tại đúng thời điểm SQL commit trên main.
- Main không được dùng để thử foreign owner, replay checkout hoặc double cancel, đúng giới hạn an toàn.

## 6. Đối chiếu yêu cầu đồ án

Các chức năng bắt buộc bán điện thoại đã có: tài khoản Customer, catalog/tìm/lọc/chi tiết/ảnh, giỏ, checkout, tồn kho, lịch sử/chi tiết/hủy đơn, hồ sơ và đồng bộ trạng thái với Admin. Không thấy chức năng bắt buộc nào trong phạm vi yêu cầu hiện tại là **MISSING**.

Không có đổi/quên mật khẩu, thanh toán online, wishlist, đánh giá, coupon hoặc cart đồng bộ đa thiết bị. Các mục này chưa nằm trong phạm vi PhoneStore đã phê duyệt; không tự coi là lỗi hay tự bổ sung.

## 7. Lỗi và ưu tiên

### P0

- Không có lỗi bảo mật, mất dữ liệu hoặc sai dữ liệu mới được chứng minh.

### P1

1. **Customer Web hiện không chạy** — không listener 5015, toàn bộ URL không truy cập được. Đây là blocker vận hành hiện tại, dù binary/source từng nghiệm thu PASS.

### P2 / test gaps

1. Chưa có browser smoke mới sau các thay đổi/triển khai Admin gần đây.
2. Catalog filters chưa có ma trận browser đầy đủ cho mọi biên và tổ hợp.
3. Logout main, cart update/remove và các validation lỗi chỉ có bằng chứng lịch sử cách ly/automated ở một số ca.
4. Các tình huống mạng/SQL mất kết nối đúng lúc commit và kiểm tra dài hạn RememberMe chưa được nghiệm thu thực tế.

## 8. Đề xuất S29.8E.2

**S29.8E.2 — Khởi động an toàn và browser acceptance Customer Web**, không sửa source trước:

1. Xác minh binary/config Web hiện có, manifest/backup và đúng `PhoneStoreDB`; khởi động cổng 5015 theo phê duyệt riêng.
2. Smoke anonymous read-only: Home, Products, search/filter/sort, details, 40 ảnh, Login/Register/Cart và redirect protected routes.
3. Người dùng đăng nhập Customer hiện có; nghiệm thu logout/login, profile GET, history/detail Cancelled và ownership bằng thao tác không ghi.
4. Nếu cần kiểm cart update/remove hoặc validation ghi session, lập phạm vi rõ; không checkout/tạo order main nếu chưa phê duyệt riêng.
5. Chỉ sửa source khi browser/runtime tạo được lỗi tái hiện cụ thể.

Không đề xuất tính năng mở rộng trước khi Web runtime và các chức năng bắt buộc được nghiệm thu lại.
