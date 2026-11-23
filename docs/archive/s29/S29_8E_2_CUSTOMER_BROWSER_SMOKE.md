# S29.8E.2 — Khởi động Customer Web và browser smoke read-only

Ngày thực hiện: 2026-11-02 (Asia/Bangkok). Không sửa source, build/deploy lại, DML, migration, seed, restore, commit hoặc push.

## 1. Khởi động runtime

| Hạng mục | Kết quả |
|---|---|
| Binary | `src/PhoneStore.Web/bin/Debug/net8.0/PhoneStore.Web.exe` |
| PID | `14572` |
| Command line | `PhoneStore.Web.exe --urls http://localhost:5015` |
| Listener | `127.0.0.1:5015` và `[::1]:5015`, owner PID 14572 |
| Environment | Development |
| Database runtime | SQL session PID14572 → `PhoneStoreDB`, sleeping, open transaction 0 |
| Log artifact | `src/PhoneStore.Web/obj/S29.8E.2-20261102_164157987` |

Precheck xác nhận cổng trống, không có Web process cũ, binary/config hiện có hợp lệ, source và runtime appsettings cùng trỏ `localhost/PhoneStoreDB`, uploads có 40 ảnh. Web được khởi động từ runtime hiện có; không build, không ghi đè config/secrets.

Startup log xác nhận listening, application started và content root đúng. Sau smoke: stderr rỗng, không có marker fail/error/unhandled exception. Có một warning đã biết `Failed to determine the https port` khi chạy HTTP-only; không gây lỗi request.

## 2. HTTP smoke tự động — PASS

| Route/ca | Kết quả |
|---|---|
| `/` | HTTP200, 6 product links |
| `/products` | HTTP200, 40 product links |
| Search `iPhone` | HTTP200, 6 kết quả |
| Search từ khóa không tồn tại | HTTP200, 0 kết quả |
| `BrandId=1` | HTTP200, 6 kết quả |
| `CategoryId=1` | HTTP200, 40 kết quả |
| `/products/25` | HTTP200, vivo Y17s và 4 related links |
| `/account/login` | HTTP200 |
| `/account/register` | HTTP200 |
| `/cart` anonymous | HTTP200, không thay session cart |
| `/checkout` anonymous | HTTP302 → login với ReturnUrl |
| `/orders` anonymous | HTTP302 → login với ReturnUrl |
| `/account/profile` anonymous | HTTP302 → login với ReturnUrl |
| 40 file ảnh uploads | **40/40 HTTP200** |

Tất cả là GET; không gửi form hoặc request ghi.

## 3. Browser thật — PASS

Chrome mở trang `http://localhost:5015`, title `Trang chủ · PhoneStore`. Người dùng trực tiếp xác nhận **PASS hết** cho checklist:

- Trang chủ hiển thị sản phẩm và ảnh.
- Products hiển thị 40 sản phẩm.
- Tìm `iPhone` có kết quả; từ khóa không tồn tại cho danh sách rỗng.
- Lọc thương hiệu và danh mục hoạt động.
- Chi tiết vivo Y17s hiển thị ảnh, giá và tồn kho.
- Login và Register mở được.
- Orders/Profile anonymous chuyển về Login.
- Điều hướng qua lại không có lỗi hiển thị/console được quan sát.

Không đăng nhập, gửi form, thêm/xóa giỏ, checkout, cancel hoặc sửa profile trong E.2.

## 4. Hậu kiểm

- `PhoneStoreDB`: Products40, ProductImages40, tổng tồn740, Customers1, Orders1, Cancelled1, active Admin1.
- 40/40 ảnh khớp SHA-256 baseline.
- Web PID14572 vẫn responsive/listening và SQL session main open transaction0.
- Admin main PID4592 vẫn chạy đúng executable; không bị tác động.
- Không có DML nghiệp vụ ngoài ý muốn.

## 5. Phân loại

| Nhóm | Trạng thái |
|---|---|
| Startup/runtime/database target | **PASS runtime + SQL read-only** |
| Home/catalog/search/filter/detail/images/navigation | **PASS browser thật + HTTP** |
| Login/Register pages | **PASS browser thật + HTTP GET**, submit NOT TESTED trong E.2 |
| Protected route redirects | **PASS browser thật + HTTP** |
| Cart display anonymous | **PASS HTTP GET**; add/update/remove NOT TESTED trong E.2 |
| Account login/logout/profile/history | Automated/lịch sử trước đây PASS; authenticated browser flow **NOT TESTED trong E.2** |
| Checkout/cancel | Automated/lịch sử trước đây PASS; **NOT TESTED trong E.2** |

Baseline automated gần nhất vẫn 161/161 PASS, build 0 warnings/errors; không chạy lại vì E.2 không sửa source.

## 6. Kết luận và bước tiếp theo

**PASS** cho phạm vi S29.8E.2.

Đề xuất S29.8E.3 — nghiệm thu tài khoản và commerce có kiểm soát:

1. Dùng Customer hiện có, người dùng tự nhập mật khẩu; login/logout, profile GET, history/detail Cancelled trước.
2. Nghiệm thu cart add/update/remove bằng đúng một sản phẩm và session browser nếu được phê duyệt; cart chỉ ghi session, không SQL.
3. Không checkout/tạo Order mới trên main nếu chưa có phê duyệt ghi riêng. Có thể dùng test DB cho các ca checkout/cancel ghi hoặc lập một giao dịch main được phê duyệt cụ thể.
4. Chỉ sửa source nếu browser tạo được lỗi chức năng tái hiện rõ.
