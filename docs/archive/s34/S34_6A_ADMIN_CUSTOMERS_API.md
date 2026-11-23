# S34.6A — API quản trị khách hàng

Ngày 2026-11-19. HEAD d6b4fb9, nhánh main, working tree sạch trước khi sửa. S34.5A đã nằm trong checkpoint này.

| Endpoint | Contract |
|---|---|
| GET /api/admin/customers | Tìm theo tên, email, điện thoại; lọc isActive. Count, sắp UserId tăng dần và phân trang trong SQL. page mặc định 1, pageSize mặc định 20, tối đa 100. Trả items, totalCount, page, pageSize. |
| GET /api/admin/customers/{id} | Trả ID, họ tên, email, điện thoại, địa chỉ, thời gian tạo UTC và trạng thái Customer. |
| PATCH /api/admin/customers/{id}/status | Chỉ nhận {"isActive":true} hoặc {"isActive":false}. Trả 204 cả khi lặp lại cùng trạng thái trên Customer hiện có. |

Ba action dùng Api.Admin. Người chưa xác thực nhận 401; Customer và Admin inactive nhận 403. ID Admin và ID dương không tồn tại đều nhận 404 cho GET chi tiết và PATCH. Payload thiếu trạng thái, sai kiểu hoặc có thêm trường nhận 400. Các lỗi trả ProblemDetails.

API dùng CustomerManagementService của WinForms và truy vấn CustomerManagementStore. Câu UPDATE mới đặt duy nhất IsActive với điều kiện ID và role Customer ngay trong SQL. Điều kiện này bảo vệ Admin khi ghi; filtered unique index UX_Users_SingleAdmin tự nó chỉ giới hạn số lượng Admin. Schema và quyền các tài khoản không thay đổi. DTO không chứa password hash, role hay token. Danh sách không xuất địa chỉ.

Test host dùng SQLite trong bộ nhớ. Test HTTP xác nhận phân quyền trên cả ba route, tìm theo điện thoại, phân trang, lọc active, cấu trúc DTO chi tiết, các lỗi input, bảo vệ Admin và giữ nguyên hash/role. Kiểm thử bao gồm gửi lặp lại PATCH ngừng hoạt động và bật lại Customer. Chỉ SELECT trên SQL Docker nghiệp vụ. Chưa triển khai API mới; client thật cần issuer và ánh xạ danh tính như S34.3B.

Release build toàn solution đạt, 0 warning/error. Targeted tests AdminCustomerApiTests, SingleAdminPolicyTests, AdminProductApiTests, OrderApiTests và CatalogApiTests: 42/42 PASS. Không chạy GUI WinForms tương tác.
- Full PhoneStore.Tests Release 278/278 PASS, 0 skipped. Trước/sau: 3 Users (checksum 2014292920), 2 Roles (1107669224), 1 Admin active. Cùng 5 container healthy. Chưa deploy/commit/push.
