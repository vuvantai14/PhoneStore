# S29.8E.3 — Customer account, profile, order history và cart acceptance

Ngày nghiệm thu: 2026-11-02 (Asia/Bangkok). Browser thật trên Customer Web main; chỉ đọc dữ liệu nghiệp vụ. Không sửa source, deploy, DML, migration, tạo Customer/Order, sửa profile/cart, checkout, cancel, commit hoặc push.

## 1. Precheck

- Web PID `14572`, executable `src/PhoneStore.Web/bin/Debug/net8.0/PhoneStore.Web.exe`, listener HTTP 5015.
- SQL session thực tế của Web dùng `PhoneStoreDB`, open transaction 0.
- Admin main PID `4592` vẫn đúng executable và không bị tác động.
- Snapshot trước: Products40, ProductImages40, tổng tồn740, Customers1, Orders1, Cancelled1, OrderDetails1, active Admin1.
- Customer hiện có: UserId2, `tai123@gmail.com`, `tài vũ`, phone `09032657212`, Address NULL, RoleId2, active.
- OrderId1: `ORD-05E65B3F0F705464636B525624`, Cancelled, vivo Y17s ×1, UnitPrice/Total 3.490.000đ.

## 2. Nghiệm thu browser thật

Người dùng tự nhập credential trực tiếp trên Chrome; không cung cấp password cho Codex, không đọc/ghi cookie hoặc token.

| Chức năng | Bằng chứng | Kết quả |
|---|---|---|
| Login | Người dùng đăng nhập Customer hiện có; giao diện nhận diện `tài vũ` | **PASS browser thật** |
| Protected routes khi authenticated | `/account/profile` và `/orders` mở thành công | **PASS browser thật** |
| Profile read-only | Hiển thị tên `tài vũ`, phone `09032657212`, Address trống và email `tai123@gmail.com` readonly; không bấm Lưu | **PASS browser thật** |
| Order history | Đúng một đơn `ORD-05E65B3F0F705464636B525624`, trạng thái Đã hủy | **PASS browser thật** |
| Order detail | vivo Y17s ×1, tổng 3.490.000đ, không có hành động hủy hợp lệ | **PASS browser thật** |
| Cart read-only | Trang tải bình thường; empty/current state hiển thị đúng, không thêm/sửa/xóa | **PASS browser thật** |
| Logout | Người dùng đăng xuất thành công | **PASS browser thật** |
| Protected routes sau logout | Orders/Profile chuyển về Login; session cũ không tiếp tục truy cập trang bảo vệ | **PASS browser thật** |

Cookie/session chỉ được nghiệm thu theo hành vi đăng nhập và logout; không kiểm tra hoặc lưu nội dung bí mật.

## 3. Authorization và ownership

- Controller checkout/orders/profile yêu cầu policy `Customer`; logout là POST có anti-forgery.
- Identity/owner lấy từ NameIdentifier claim, không nhận UserId từ form/query.
- Order receipt/history/cancel luôn lọc `Order.UserId == authenticated userId`; foreign/missing trả 404.
- Profile query/update chỉ active Customer hiện tại và chỉ FullName/Phone/Address; email, role, ID, hash không bind.
- `CustomerCookieEvents` revalidate UserId, active, role, email và name từ DB; lỗi DB fail-closed.

Ownership foreign-user không được thử bằng sửa ID trên main theo giới hạn. Bằng chứng cho ca này là **PASS automated/HTTP isolated lịch sử**, không phải PASS browser main trong E.3.

## 4. Hậu kiểm

SELECT read-only sau browser acceptance khớp hoàn toàn snapshot trước:

- Products40, ProductImages40, tổng tồn740.
- Customers1; Orders1/Cancelled1; OrderDetails1.
- Một active Admin.
- Customer UserId2 và ba trường hồ sơ không đổi.
- OrderId1/mã/trạng thái/item/giá không đổi.
- 40/40 ảnh khớp SHA-256 baseline.
- Web PID14572 vẫn chạy, mọi SQL session quan sát được đều `PhoneStoreDB`, sleeping/open transaction0.
- Admin PID4592 không bị ảnh hưởng.
- Log Web không có marker fail/error/unhandled; stderr rỗng.

## 5. Phân loại cuối

| Nhóm | Trạng thái |
|---|---|
| Login | **PASS browser thật** |
| Logout | **PASS browser thật** |
| Protected routes | **PASS browser thật + HTTP E.2** |
| Profile read-only | **PASS browser thật** |
| Order history | **PASS browser thật** |
| Order detail | **PASS browser thật** |
| Cart read-only | **PASS browser thật** |
| Ownership chính tài khoản | **PASS browser thật** |
| Foreign ownership | **PASS automated/isolated lịch sử; NOT TESTED browser main E.3** |
| Database integrity | **PASS SQL read-only + SHA-256** |

Baseline automated gần nhất vẫn 161/161 PASS; không chạy lại vì không sửa source.

## 6. Kết luận và bước tiếp theo

**PASS** cho phạm vi S29.8E.3.

Đề xuất bước tiếp theo:

1. Cart CRUD có kiểm soát: add/update/remove chỉ thay browser session; xác định một sản phẩm và trạng thái trước/sau, không checkout.
2. Profile update chỉ khi phê duyệt ba giá trị tạm và khôi phục qua UI như D.1C; hiện đã PASS lịch sử nên không cần lặp nếu không có thay đổi source.
3. Checkout/cancel trên main chỉ với phê duyệt ghi riêng, backup và đúng một giao dịch; ưu tiên test DB cho các ca lỗi/concurrency đã có.
