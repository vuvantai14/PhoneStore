# S34.8A — Security, performance và concurrency của REST API

## Baseline và thay đổi

Ngày 2026-11-19, main tại 77fb3eb (S34.7A), working tree sạch. Local ahead 1 so với origin/main đã lưu ở máy; không fetch/push. Thay đổi tập trung ở ReportingStore, ReportingModels, ReportSectionReader, AdminReportsController, Program và AdminReportApiTests.

## Báo cáo và hiệu năng

Cùng fixture SQLite trong bộ nhớ, cùng HTTP test qua middleware Api.Admin. Đếm EF RelationalEventId.CommandExecuted theo từng request; mỗi số đã gồm một SELECT kiểm tra account/role. Trước khi sửa, kiểm thử xác nhận cả hai endpoint thực hiện 12 câu.

| Endpoint | Trước | Sau | Truy vấn dữ liệu sau tối ưu |
|---|---:|---:|---|
| GET overview | 12 | 3 | Một GROUP BY trạng thái đơn trong kỳ, một tổng hợp tồn kho sản phẩm active hiện tại |
| GET top-products | 12 | 2 | Một GROUP BY sản phẩm, sắp xếp và lấy tối đa 10 |

Cả WinForms và API sử dụng cùng truy vấn cho hai phần này. Snapshot đầy đủ của WinForms tiếp tục lấy cả trend và cảnh báo kho. Doanh thu chỉ tính Completed trên OrderDate, top tính Confirmed/Shipping/Completed theo quantity, sales value rồi tên. Giữ ngày đầu bao gồm và ngày cuối kết thúc tại 00:00 hôm sau theo UTC+7. Kiểm thử bao gồm số tiền và thứ hạng kỳ vọng độc lập, số 0 khi không có đơn, hai biên nửa đêm và so với snapshot WinForms trên cùng fixture. Đây là số lượt truy vấn; chưa đo latency hay execution plan production.

## Bảo mật

- Tái hiện thiếu Cache-Control bằng HTTP test trên route báo cáo. Đã thêm no-store cho response của MVC endpoint có metadata authorization, gồm cả 401/403. Test kiểm tra header trên 401 và 200. Catalog công khai giữ chính sách cũ.
- Action MVC chưa phân loại bị deny. Api.Customer và Api.Admin dùng một NameIdentifier dương từ identity đã xác thực, tra trạng thái active và role qua SQL; API chưa bật issuer. Customer orders giới hạn owner trong WHERE, không tin UserId từ query. DTO thu hẹp dữ liệu trả về; Customer status chỉ nhận isActive. ProblemDetails qua handler chung che lỗi DB; RequestSafetyMiddleware ghi method/path/status/trace.
- Các truy vấn đã rà soát dùng LINQ hoặc FromSqlInterpolated với tham số. Không phát hiện SQL động ghép chuỗi từ input trên các đường dẫn này. Có giới hạn query string, giới hạn page size và cancellation token truyền tới EF. Rate-limit/timeout hiện có áp dụng theo metadata cho Catalog/Health; chưa có số liệu tải cho policy riêng của route protected. Chưa kiểm tra nhà cung cấp OIDC thật.

## Concurrency, phân trang và index

- Orders: kiểm tra expectedStatus sau khi khóa order trong transaction Serializable. SQL Server dùng UPDLOCK/HOLDLOCK, khóa sản phẩm theo ID tăng dần trước khi hoàn tồn và commit cùng thay đổi trạng thái. Mỗi sản phẩm riêng trên đơn hủy cần một truy vấn khóa; giữ thứ tự khóa hiện tại. Test hai request độc lập chạy trên SQLite xác nhận một 204, một 409 và chỉ hoàn tồn một lần. SQL Server concurrency, deadlock/tải thực: chưa nghiệm thu.
- Product PUT: một UPDATE chứa điều kiện ProductId và originalStockQuantity. Nếu tồn gốc sai, toàn bộ thay đổi bị từ chối. Hai form có cùng tồn gốc vẫn có thể ghi đè tên hoặc giá khi tồn không đổi. Đề xuất rowversion cùng expected version cho WinForms/API khi được duyệt thay đổi schema và contract.
- Các danh sách quản trị dùng projection, AsNoTracking, sort ID và Skip/Take trong SQL; lịch sử Customer dùng OrderDate rồi OrderId. Báo cáo top có thể hòa cả ba khóa xếp hạng; giữ quy tắc hiện hành. SQL metadata có PK và các index FK, gồm IX_Orders_UserId, IX_OrderDetails_OrderId, IX_OrderDetails_ProductId; chưa thấy index có cột dẫn OrderDate. DMV phản ánh toàn instance/database kể từ lúc thống kê, không chứng minh plan cho request mới. Chỉ cân nhắc index sau khi có plan/số liệu tải đại diện.

## Kiểm thử và vận hành

Release build solution 0 warning/error. Targeted 30/30 PASS (reporting, ownership, active roles, product/stock và order concurrency). Cấu hình Docker đọc qua HostConfig: acceptance API 256 MiB, Web 384 MiB, mỗi service 0,5 CPU. Ba container business có Memory và NanoCPUs bằng 0 (không đặt cap ở hai trường này); giới hạn Docker VM/SQL nội bộ có thể khác. Không load test, deployment, migration, ghi vào SQL Docker hay thay đổi volume.
- Full regression 280/280 PASS, 0 skipped. SELECT trước/sau khớp số lượng/checksum: Orders 15/-142191037, OrderDetails 37/952380063, Products 40/1010262598, Users 3/2014292920. Năm container healthy, ID và StartedAt khớp. Chưa commit/push.
