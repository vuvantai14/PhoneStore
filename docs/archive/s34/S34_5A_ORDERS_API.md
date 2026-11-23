# S34.5A — REST API đơn hàng

Ngày 2026-11-19. Nhánh main, HEAD b3c0833 chứa S34.4A. Working tree sạch trước khi thực hiện.

| Route | Policy | Contract |
|---|---|---|
| GET /api/orders | Api.Customer | page/pageSize, status và khoảng thời gian fromUtc (bao gồm) / toExclusiveUtc (loại trừ); trả items, totalCount, page, pageSize. |
| GET /api/orders/{id} | Api.Customer | Đơn của chủ tài khoản, địa chỉ giao hàng và các dòng hàng theo đơn giá lúc mua. Cùng một 404 khi đơn thiếu hoặc thuộc người khác. |
| GET /api/admin/orders | Api.Admin | page/pageSize, search theo mã, người nhận, điện thoại hoặc email và status. |
| GET /api/admin/orders/{id} | Api.Admin | Thông tin giao nhận, trạng thái, tổng đơn đã lưu và các dòng hàng. |
| PATCH /api/admin/orders/{id}/status | Api.Admin | Ví dụ {"expectedStatus":"Pending","targetStatus":"Confirmed"}. Thành công 204. |

- Hai danh sách phân trang trong SQL, mặc định 20 dòng, tối đa 100. Customer dùng luồng lịch sử hiện có: OrderDate rồi ID giảm dần, chuẩn hóa trang vượt giới hạn về trang cuối. Admin dùng ID tăng dần như WinForms, trang vượt kết quả trả danh sách rỗng. Thời gian đơn trả về UTC.
- Ownership sử dụng cùng hàm đọc NameIdentifier của danh tính đã xác thực như policy. Truy vấn Customer giới hạn UserId ngay tại SQL. API đọc Order.TotalAmount và OrderDetail.UnitPrice lịch sử. DTO không chứa email đăng nhập, hash, token hoặc EF navigation.
- OrderManagementService dùng chung với WinForms: Pending → Confirmed/Cancelled, Confirmed → Shipping/Cancelled, Shipping → Completed. Completed và Cancelled là trạng thái cuối. Transaction Serializable và khóa UPDLOCK/HOLDLOCK trên SQL Server bảo vệ việc so khớp expectedStatus. Khi hủy, khóa sản phẩm theo ID rồi hoàn tồn và commit cùng trạng thái. Chỉ bổ sung phân loại lỗi và phân trang; không bổ sung luồng checkout/hủy cho Customer.
- HTTP 400 cho input không hợp lệ; 401/403 qua policy; 404 khi thiếu đơn; 409 khi trạng thái cũ, transition không được phép hoặc không thể hoàn tồn theo quy tắc hiện có; 503 khi lỗi truy cập DB. Cấu hình API thực tế chưa tiếp nhận bearer cho tới khi có issuer/mapping được xác minh.
- Hai test HTTP mới dùng SQLite trong bộ nhớ. Bao phủ quyền, ownership, lịch sử, lọc, phân trang, đơn giá, trạng thái hợp lệ/không hợp lệ và hai request hủy qua các DbContext độc lập: một 204, một 409, stock 3 → 5 cho 2 món. Kiểm thử này không xác nhận hành vi khóa dưới tải trên SQL Server thật.
- Build Release solution sạch (0 warning/error). Bộ test mục tiêu Orders/Catalog/Checkout 50/50 PASS. Chỉ chạy SELECT với PhoneStoreDB; không migration/seed, triển khai hoặc thay đổi container.
- Full PhoneStore.Tests Release: 276/276 PASS, 0 skipped. SELECT trước/sau: Orders 15/-142191037, OrderDetails 37/952380063, Products 40/1010262598 (count/checksum). Đúng 5 container cũ healthy. Chưa commit/push.
