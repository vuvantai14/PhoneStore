# S29.8C.7 — Chuẩn bị nghiệm thu một giao dịch main

Ngày 2026-11-02, Asia/Bangkok. CHỈ CHUẨN BỊ; chưa được phép tạo Customer/cart/order hoặc đổi trạng thái.

## Preflight chỉ đọc

- Web PID22736, bin/Debug/net8.0/PhoneStore.Web.exe --urls http://localhost:5015, listener đúngPID; Admin PID10736, bin/Debug/net8.0-windows/PhoneStore.Admin.exe, shell PhoneStore Admin responding.
- SHA256118/118 file deployed khớp copy-plan C.6B. Không rebuild/restart/change config ở bước này;107tests là evidence trước triển khai, không chạy lại trong task chỉ chuẩn bị.
- SELECT localhost→DESKTOP-ES9362P/PhoneStoreDB ONLINE; cả2PID có sessionSQL main, sleeping/openTx0. RolesAdmin1/Customer2; history InitialCreate+EnforceSingleAdmin giữ nguyên.
- Products40/Images40/tồn740/Users1Adminactive/Customer0/Orders0/OrderDetails0. Không có Customer demo sẵn để đăng nhập; cần duyệt tạo DUY NHẤT một Customer qua đăng ký Web. Không dùng Admin làm Customer hoặc sửa role.
- GET Home/products/product25/register/login HTTP200. Không gửi POST; không GET checkout/cart có phiên Customer hoặc thiết lập cart/submission token.

## Sản phẩm đề xuất

ProductId25, **vivo Y17s**, brand vivo, IsActive=true, brand/category active, Price3490000 VND, StockQuantity38. Ảnh primary /uploads/products/vivo-y17s.jpg, DisplayOrder0. Giá thấp nhất trong catalog hiện tại, đủ tồn cho số lượng1.

Đây là snapshot. Ngay trước giao dịch phải kiểm lại target/price/stock/Customer/counts; nếu khác38/740/Orders0 hoặc có giao dịch khác thì dừng xin duyệt phạm vi mới, không ép về baseline.

## Tài khoản và thông tin cần chốt

- Email/tên hiển thị Customer: do người dùng chọn và duyệt trước khi tạo, chưa tự gán tài khoản mẫu. Chỉ tạo một user RoleCustomer2/active.
- Người dùng tự đặt mật khẩu trong Web, không gửi chat/CLI/source/log. Không sao chép password/hash của Admin hoặc test DB.
- Thông tin giao hàng dùng dữ liệu demo nội bộ đã duyệt, ghi rõ không giao hàng thật; chốt ReceiverName/ReceiverPhone/ShippingAddress trước checkout. Note có thể là S29.8C.7 nghiệm thu nội bộ - không giao hàng, nếu được duyệt.
- Dùng cửa sổ trình duyệt riêng với session giỏ rỗng; không xóa/thay giỏ có sẵn của người dùng. Cart hiện thuộc browser session, không có bảng Carts hoặc OrderItems.

## Phạm vi ghi dự kiến cần duyệt riêng

| Đối tượng | Trước | Ghi dự kiến | Sau kết thúc |
|---|---|---|---|
| Users/Customer | Users1,Customer0 | INSERT đúng1 Customer qua Register | Users2:1Admin+1Customer; giữ Customer làm bằng chứng |
| Cart/session | Session demo mới chưa tạo |1 dòng ProductId25/quantity1; tokencheckout sinh khi vào checkout | Cart rỗng sau success; không có dòng cart trong SQL |
| Orders |0 | INSERT1 Pending/Total3490000/UserIdCustomer; UPDATE cùng đơn Confirmed rồi Cancelled |1 đơn Cancelled, không xóa |
| OrderDetails (tên bảng thực tế) |0 | INSERT1 ProductId25/Quantity1/UnitPrice3490000 |1 detail giữ nguyên, LineTotal3490000 |
| Products row25 | Stock38 | Checkout38→37; cancellation37→38 và UpdatedAt theo luồng hủy | Stock38, tổng tồn740; timestamp có thể mới |
| Toàn kho |740 |740→739→740 |740 |
| Roles/Admin/Images/Brands/Categories/schema | Baseline | Không ghi | Giữ nguyên |

Giá/tên/active/ảnh sản phẩm không đổi. Admin chỉ xác nhận một lần Pending→Confirmed; Customer thực hiện hủy cùng đơn Confirmed→Cancelled. Không chuyển Shipping/Completed vì sẽ mất quyền hủy theo nghiệp vụ. Revenue Completed vẫn0 sau kịch bản.

Nếu được duyệt kiểm hủy lặp: gửi lại chính request hủy cho cùng OrderId tối đa1lần để xác minh stock vẫn38/Orders1/Details1; không tạo đơn thứ hai. Đây là thao tác phải nằm trong phê duyệt giao dịch, chưa chạy. Sau Cancelled UI không còn nút hủy, nên không giả đánh dấu UI click lặp PASS; có thể chỉ kiểm SELECT hoặc request kiểm soát đã duyệt.

## Kịch bản và quan sát

1. Recheck main và hashes/PIDs; chốt Customer/shipping, kiểm email chưa tồn tại. Sau duyệt mới Register một Customer; SELECT email/role/active, không chọn password/hash.
2. Người dùng đăng nhập Customer trên Web, thêm vivoY17s quantity1 vào giỏ. Cart phải có đúng1dòng/total3490000; không gửi giá/tổng tiền giả ở bước giao dịch main.
3. Checkout đúng một lần; ghi OrderId/OrderCode thực tế từ receipt và SELECT theo owner. Không đoán identity bằng1 hoặc dựa order count. Cart rỗng, Orders1/Details1/Pending, stock37/tổng739; price snapshot3490000.
4. Admin mở/tải lại Đơn hàng, tìm OrderCode đó; kiểm customer/receiver/quantity/price/status. Nhấn Xác nhận và đồng ý đúng một lần. SELECT Confirmed/stock37; Customer refresh history/detail thấy Đã xác nhận.
5. Customer hủy đơn của mình khi Confirmed; kiểm kết quả Đã hủy trên Web, Admin refresh thấy Cancelled. SELECT stock38/tổng740/Orders1/Details1/Cancelled; chỉ một lần hoàn kho.
6. Chỉ khi đã duyệt riêng lần kiểm lặp, thực hiện cùngrequest hủy một lần, đối chiếu stock không đổi. Không đặt đơn mới để kiểm duplicate.
7. Lưu evidence counts/status/stock/OrderCode vào PROGRESS, không password/hash/token/cookie hoặc địa chỉ cá nhân. Giữ Customer/order/detail trong DB, không cleanup.

Web hiện có /orders và /orders/{id}, /checkout/success/{id}, POST /orders/{id}/cancel (Customer policy/anti-forgery). Admin OrdersManagementControl/OrderDetailDialog có tìm/filter/detail/confirm. SELECT đủ quan sát baseline/snapshot. Authenticated Customer runtime trên main CHƯA KIỂM vì chưa có Customer; không suy từ HTTP200 sang PASS giao dịch.

## Dừng khi có lỗi

- Request timeout/500/mất mạng hoặc receipt không rõ: dừng bấm checkout, SELECT theo UserId/OrderCode/key quy chiếu bằng công cụ được phép, xác định commit. Không sinh token mới, không retry transaction tự động. Replay idempotent đã PASS cách ly không thay thế việc kiểm tra outcome main.
- Trước commit thất bại kỳ vọng không order/detail/stock change; nếu có dữ liệu một phần hoặc stock bất thường, giữ nguyên, báo cáo và xin hướng dẫn. Không sửa stock/đơn bằng UPDATE trực tiếp hoặc restore database.
- Admin thấy Shipping/Completed hoặc expected status thay đổi: không hủy, không chuyển ngược. Dừng và báo trạng thái thực tế.
- Hủy lỗi/timeout: SELECT status/stock trước bất kỳ thao tác tiếp theo; không cộng kho thủ công hoặc xóa đơn. Nếu Cancelled mà stock không đúng, dừng để điều tra.
- Dữ liệu demo trên main sẽ tồn tại lâu dài, ảnh hưởng đếm đơn/Customer. Không có thanh toán hoặc giao hàng bên ngoài trong phạm vi nghiệm thu này.
- Các backup SQL đã ghi nhận thuộc các mốc trước catalog/Admin khác nhau; chưa có bằng chứng backup hiện trạng40SP ngay trước giao dịch. Đề xuất FULL COPY_ONLY/CHECKSUM/COMPRESSION+VERIFYONLY mới nếu muốn recovery point hiện tại; backup là phê duyệt riêng, chưa chạy. Binary backup không phục hồi dữ liệu SQL.

Kết luận: chuẩn bị PASS; thực thi giao dịch CHƯA DUYỆT/CHƯA CHẠY. Cần người dùng chốt email/tên/shipping và duyệt rõ1Customer+1Order+1Detail, Pending→Confirmed→Cancelled, stock38→37→38, tùy chọn1cancelrepeat; không có quyền thực thi từ tài liệu này.
