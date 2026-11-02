# S29.8C.7/7A — Nghiệm thu giao dịch main PASS

Ngày 2026-11-02, Asia/Bangkok. Kết quả cuối sau S29.8C.7A: PASS luồng một giao dịch thật; Admin Confirm và Customer Cancel do người dùng trực tiếp thao tác UI. Lần dừng PARTIAL trước đây được giữ làm lịch sử bên dưới.

## Phạm vi và baseline

Người dùng duyệt riêng1Customer tai123@gmail.com/tài vũ,1đơn vivoY17s ProductId25/qty1/giá3490000, AdminPending→Confirmed rồi Customerhủy/hoànkho. Password chỉ do người dùng nhập trực tiếp Web. Không ghi/log/đọc password/hash/cookie/token, không sửa Admin.

Preflight PASS: binary118hash đúng bản C.6B, Web22736/Admin10736 đúngpath/responding;localhost/DESKTOP-ES9362P/PhoneStoreDB ONLINE;email chưa có/Users1Admin/Orders0/Details0/product25stock38/total740. Không đổi binary/config/ảnh hoặc migration.

## Backup bắt buộc PASS

```text
C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_S29_8C_7_PRETRANSACTION_20261102_020840231Z_83DE6E0E3458420896EA6963BCAB5F59.bak
```

FULL COPY_ONLY/CHECKSUM/COMPRESSION/file mới không ghi đè. RESTORE VERIFYONLY WITH CHECKSUM PASS. File566272byte, compressed564894/logical5652480byte, backup_set_id3/typeD/copy_only1/checksums1, đúng PhoneStoreDB. Backup09:08:40 Asia/Bangkok (02:08:40UTC). Không restore.

## Kết quả từng bước

| Bước | Kết quả |
|---|---|
| Preflight | PASS |
| Backup/VERIFYONLY | PASS |
| Customer đăng ký/login thật | PASS người dùng tự nhập mật khẩu trên Edge riêng và xác nhận; SQL UserId2/Emailtai123@gmail.com/FullNametài vũ/RoleId2Customer/active; Users2,Admin không đổi |
| Giỏ hàng | PASS giỏ ban đầu rỗng; form addproduct25qty1 đúng1lần;DOMcart1line/3490000, không sửa giáclient |
| Checkout | PASS form thật gửi đúng1lần với token hiện có;return /checkout/success/1,khôngerror;SQL đúng1Order/1Detail/Pending/stock37/total739 |
| Admin xem/tìm đơn | PASS người dùng đối chiếu đúng chi tiết; UIAutomation cell GetItem không hỗ trợ nên không dùng làm điều kiện ghi |
| Admin Confirmed | PASS người dùng bấm nút + confirmation thật; SELECT Confirmed/stock37/total739; Customer Web hiển thị Đã xác nhận |
| Customer hủy/Cancelled/hoàn kho | PASS người dùng bấm hủy một lần; SQL/Web/Admin đều Cancelled,stock38/total740 |
| Hủy lặp | Không gửi thêm thao tác ghi theo giới hạn 7A; đối chiếu checkpoint38→37→38 và SELECT lặp chỉ đọc vẫn38/740, không có hoàn kho thêm. Không tuyên bố đã thử request hủy lặp trên main |

Đơn đã commit:

```text
OrderId: 1
OrderCode: ORD-05E65B3F0F705464636B525624
Customer UserId: 2
OrderDetailId: 1
ProductId: 25
Quantity: 1
UnitPrice/TotalAmount: 3490000 VND
OrderDate: 2026-11-02 02:12:17.802441 UTC (09:12:17 Asia/Bangkok)
Status cuối: Cancelled (đã đi qua Pending và Confirmed)
```

Ảnh receipt thật: src/PhoneStore.Web/obj/S29.8C.7-pending-receipt.png. Không chụp trang mật khẩu. Browser riêng có profile runtime trong obj; không đưa dữ liệu phiên vào Git. Không tạo customer/order test thứ hai, không lưu password/hash vào report/source/CLI/chat.

## Lý do dừng PARTIAL ban đầu — lịch sử

UIAutomation Name của các descendants OrdersGrid không cung cấp code text cần xác minh. Probe chỉ đọc GridPattern.GetItem(0,col) trả NotImplementedException, không đọc được value. Không kết luận WinForms/service hỏng vì lỗi nằm ở automation provider/khả năng quan sát.

Đã làm đúng yêu cầu gặp lỗi thì dừng/SELECT: chưa bấm nút Xem chi tiết hoặc Xác nhận, chưa có confirmation ghi, không thử SQL UPDATE hoặc service call thay UI. Không có request checkout timeout hoặc commit không rõ, vì receipt và SQL cùng xác nhận thành công.

Checkpoint lúc dừng: Orders1/Details1/Pending,product25stock37,total739. Đây là lịch sử, đã được hoàn tất qua thao tác UI người dùng ở 7A; không tự sửa stock/xóa/restore.

Để tiếp tục cần người dùng chỉ đạo/xác nhận sau báo cáo: có thể người dùng tự mở đúng mã đơn trên Admin và thực hiện nút/confirmation thật, rồi báo để SQL đối chiếu trước bước Customer hủy; hoặc duyệt phương án quan sát UI khác. Không thực hiện phương án thay thế trong lượt dừng này.

## Files và giới hạn

- Tạo báo cáo này, cập nhật PROGRESS; helper browser/attemptmarkers/screenshot chỉ trong obj (ignored).
- Không sửa source ứng dụng/UI/config/binary/migrations, không build/test lại. Không commit/push/cleanup dữ liệu.
- Tiếp tục dùng đúng OrderId1 hiện tại, không tạo tokencheckout/đơn mới. Kịch bản hủy/hoàn kho và finalCancelled còn chờ thực hiện; database test giữ nguyên.

## S29.8C.7A — tiếp tục bằng thao tác người dùng (2026-11-02 Asia/Bangkok)

- SELECT checkpoint trước Confirm PASS: đúng OrderId1/code ORD-05E65B3F0F705464636B525624/Customer2/Pending/qty1/Product25/UnitPrice3490000/Total3490000; Orders1/Details1, stock37/total739.
- Đã hướng dẫn người dùng tự mở chi tiết Admin, đối chiếu mã đơn/Customer/sản phẩm/số lượng/giá trước khi bấm Xác nhận và đồng ý hộp thoại.
- Đã chờ và nhận xác nhận người dùng sau từng bước Confirm/Cancel; checkpoint kết quả cuối ở mục dưới. Không thao tác ghi thay UI.
- Không thử hủy lặp bằng request/script; chỉ xác minh dữ liệu hiện có theo giới hạn mới. Không tạo user/order, checkout, SQLUPDATE, sửa stock, migration/config/binary hoặc commit/push.

### Checkpoint Admin Confirm — PASS
- Người dùng xác nhận đã đối chiếu đúng và bấm Xác nhận + confirmation trên Admin thật.
- SELECT sau thao tác: OrderId1/code đúng/UserId2/Confirmed/Total3490000;Orders1/Details1/stock37/total739.
- GET Customer /orders/1 trong session đã đăng nhập hiển thị Đã xác nhận và nút Hủy đơn hàng; screenshot obj/S29.8C.7-confirmed-order.png.
- Đã hướng dẫn và chờ người dùng tự hủy đúng đơn một lần; người dùng xác nhận Web hiển thị Đã hủy. Không tự submit/callAPI để hủy.

### Checkpoint Customer Cancel và kết quả cuối — PASS

- SELECT guard đúng main/OrderId1/code/owner/item/price: Cancelled,Orders1/OrderDetails1/Users2,Product25stock38,total740; databaseONLINE,40Products/40Images/8Brands/1Category,AdminRole1active/history2 giữ nguyên.
- Customer GET /orders/1 hiển thị Đã hủy, cancelAvailable=false. Screenshot src/PhoneStore.Web/obj/S29.8C.7-cancelled-order.png; screenshot Confirmed ở cùng thư mục S29.8C.7-confirmed-order.png.
- Admin dialog đang mở còn snapshot Confirmed; chỉ bấm Đóng rồi mở lại Đơn hàng/Xem chi tiết (không ghi), heading đúng OrderCode · Đã hủy, thông báo trạng thái kết thúc, không nút Hủy đơn.
- SELECT lần hai lúc 2026-11-02 02:20:45.9003746 UTC (09:20:45 Asia/Bangkok) vẫn Cancelled/stock38/740. Không request hủy lặp; hoàn kho chính xác1 theo chênh lệch38→37→38, không tăng thành39/741.
- Đối chiếu40/40 sản phẩm name/brand/price/stock/active khớp CatalogImport; file uploads giữ hash preflight. Product25UpdatedAt có thể đổi theo nghiệp vụ hoàn kho, không sửa giá hoặc sản phẩm khác.
- Giữ lại CustomerUserId2,OrderId1,OrderDetailId1 làm evidence. Không user/order thứ hai,checkout lại,SQLUPDATE/stockadjust,restore/migration/binary/config/commit/push. Không build/test lại;107tests là baseline trước nghiệm thu.
