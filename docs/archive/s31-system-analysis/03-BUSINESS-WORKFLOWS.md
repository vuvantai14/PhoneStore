# 03 — Luồng nghiệp vụ end-to-end

## A. Tài khoản Customer — VERIFIED-AUTO

Register validate + antiforgery → chuẩn hóa email/unique → hash mật khẩu → role Customer. Login chỉ cấp cookie cho Customer active. Profile lấy UserId từ claim, không nhận target user. Logout xóa cart/checkout token trước sign-out.

## B. Quản trị sản phẩm — VERIFIED-AUTO

Admin identity → fresh authorization trước write → validate product/reference → update có điều kiện `OriginalStockQuantity` → soft-state thay hard delete. Ảnh được decode/limit/path-safe; DB commit trước cleanup và không xóa file còn tham chiếu. UI CRUD có bằng chứng lịch sử; DisplayOrder/đóng dialog lúc save còn cần acceptance.

## C. Checkout — VERIFIED-AUTO

Customer + session cart → server tải Product/giá/tồn → token gắn UserId → transaction serializable + application lock → kiểm active/quantity/stock → tạo Order/Details và trừ tồn → commit. Exception rollback; replay cùng token trả order cũ, token khác là giao dịch độc lập.

## D. Doanh thu — VERIFIED-AUTO, UI PARTIAL

Store chỉ chọn Completed → ngày Bangkok đổi một lần sang UTC half-open → tổng `Order.TotalAmount` nhóm `OrderDate` → service lấp bucket 0 và labels. Dashboard chart filter độc lập KPI/donut/bảng; Reports có global filter và chart-local filter riêng.

## E. Hủy đơn/hoàn kho — VERIFIED-AUTO

Customer chỉ thao tác order thuộc claim UserId. Pending/Confirmed mới được cancel; Admin cũng theo transition map. Transaction serializable đổi trạng thái và cộng từng OrderDetail. Cancelled replay trả success không cộng kho lần hai.

