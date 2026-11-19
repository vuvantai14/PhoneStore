# S34.4A — API quản lý sản phẩm

Ngày 2026-11-19. Nhánh main, HEAD 2c7fa8d đã chứa S34.3A/B. Working tree sạch ở preflight. API sử dụng DbContext và connection string PhoneStoreConnection hiện có. Nghiệm thu HTTP dùng test host, chưa triển khai vào Docker.

## Hợp đồng HTTP

Tất cả action dưới /api/admin/products yêu cầu Api.Admin. Scheme hiện chưa có issuer tin cậy để tiếp nhận client thật. Swagger mô tả năm operation trong môi trường Development.

| Method và đường dẫn | Dữ liệu | Thành công |
|---|---|---|
| GET /api/admin/products | search (tên sản phẩm hoặc thương hiệu), brandId, categoryId, isActive, page, pageSize | 200 với items, totalCount, page, pageSize. Mặc định trang 1, 20 dòng, tối đa 100. Lọc rồi sắp ID tăng dần và phân trang trong SQL. |
| GET /api/admin/products/{id} | ID sản phẩm | 200 với dữ liệu chỉnh sửa và originalStockQuantity. |
| POST /api/admin/products | productName, brandId, categoryId, price, stockQuantity, isActive; các thông số và mô tả tùy chọn | 201 với productId và Location trỏ đến chi tiết. |
| PUT /api/admin/products/{id} | Toàn bộ trường chỉnh sửa như POST, thêm originalStockQuantity lấy từ GET chi tiết | 204. Trường thông số tùy chọn bị bỏ trong PUT được xóa theo ngữ nghĩa thay thế. |
| PATCH /api/admin/products/{id}/status | isActive (boolean bắt buộc) | 204 khi ngừng hoặc bật lại sản phẩm. |

Lỗi dùng ProblemDetails. 400 cho payload, tham chiếu hoặc query sai; 401 cho người chưa xác thực; 403 cho Customer hoặc Admin inactive trong test host; 404 khi thiếu sản phẩm; 409 khi UPDATE có điều kiện không khớp tồn gốc; 503 khi cơ sở dữ liệu không khả dụng.

## Nghiệp vụ và giới hạn

ProductEditorService chứa logic chỉnh sửa đang dùng ở WinForms. ProductManagementService kế thừa phần này và giữ các thao tác quản lý ảnh. API chỉ đăng ký service chỉnh sản phẩm, không cần quyền filesystem. Danh sách dùng chung projection và điều kiện lọc với WinForms. DTO REST giới hạn các trường được nhận và trả.

Business tiếp tục kiểm tra tên, độ dài thông số, giá dương tối đa hai chữ số thập phân, tồn không âm, sự tồn tại/trạng thái của thương hiệu và danh mục. Khi sửa có thể giữ lookup inactive hiện tại. Tên sản phẩm không có quy tắc unique. PUT kiểm tra tồn gốc trong câu UPDATE; đây là bảo vệ tồn kho, chưa phải version cho toàn bộ bản ghi. API không có thao tác xóa vật lý hoặc quản lý ảnh.

## Bằng chứng

- Release build solution: 0 warning, 0 error. Nhóm AdminProductApiTests, CatalogApiTests và ApiAuthorizationTests: 40/40 PASS.
- Kiểm thử HTTP dùng scheme của test host và SQLite trong bộ nhớ với service thật. Kiểm tra quyền trên cả năm operation, dữ liệu sai, sản phẩm thiếu, phân trang/lọc, tạo và Location, cập nhật, xung đột tồn gốc, bật/tắt trạng thái, Swagger và Catalog v1.
- Chỉ SELECT trên PhoneStoreDB. Không chạy migration, seed hoặc CRUD trên SQL Docker. Chưa nghiệm thu thao tác ghi bằng SQL Server hoặc bearer token thật.
- Full PhoneStore.Tests Release 274/274 PASS, 0 skipped. SQL trước/sau 40 Products (checksum 1010262598), 15 Orders. Cùng 5 container healthy. Chưa commit/push.
