# S34.7A — Reporting REST API

Ngày 2026-11-19. HEAD 1a96390 trên main chứa S34.6A, working tree sạch trước khi thực hiện.

| Endpoint GET | Dữ liệu |
|---|---|
| /api/admin/reports/overview | Tổng đơn, số đơn và doanh thu Completed, giá trị đơn Completed trung bình, số đơn mỗi trạng thái, tồn kho hiện tại của sản phẩm active. |
| /api/admin/reports/revenue | Doanh thu Completed, số đơn Completed và chuỗi theo ngày Việt Nam. Ngày trống vẫn có giá trị 0. |
| /api/admin/reports/top-products | Tối đa 10 sản phẩm, xếp theo số lượng bán giảm dần, tiếp theo là giá trị bán giảm dần rồi tên sản phẩm tăng dần. |

Cả ba route yêu cầu Api.Admin và nhận hai ngày địa phương bắt buộc, ví dụ GET /api/admin/reports/revenue?fromDate=2026-11-02&toDate=2026-11-03. Khoảng ngày bao gồm hai đầu, tối đa 366 ngày. API trả 400 ProblemDetails nếu ngày thiếu, sai hoặc ngoài phạm vi. ReportingCalendar chuyển khoảng đã chọn thành [00:00 ngày đầu, 00:00 ngày sau ngày cuối) theo UTC+7 rồi lọc OrderDate UTC. Ví dụ trên tương ứng [2026-11-01 17:00Z, 2026-11-03 17:00Z).

Controller ánh xạ DTO từ IReportingService và ReportingStore của WinForms. CompletedRevenue lấy tổng Orders.TotalAmount của đơn Completed. Top-products của màn hình Reports dùng Confirmed, Shipping và Completed; SalesValue lấy số lượng nhân đơn giá lúc mua. currentInventory là tồn kho lúc đọc. Hai endpoint overview/top-products cùng dùng GetReportAsync: mỗi request tải snapshot báo cáo đầy đủ, kể cả các phần không xuất ra DTO. Revenue dùng truy vấn bucket riêng. Không sửa công thức, schema hoặc WinForms.

Kiểm thử HTTP qua SQLite trong bộ nhớ với service/store thực, bao gồm anonymous 401, Customer và Admin inactive 403, Admin active 200, validation, 5 trạng thái, hai biên nửa đêm, doanh thu theo ngày, giá trị trung bình, thứ hạng và khoảng ngày trống. So sánh SQL Docker chỉ dùng SELECT tổng hợp. Chưa deploy hoặc nghiệm thu client thật; issuer và ánh xạ danh tính vẫn là dependency S34.3B.

Release build toàn solution 0 warning/error. Targeted AdminReportApiTests và ReportingTests: 15/15 PASS.
- Full suite Release 280/280 PASS, 0 skipped. Trước/sau: 15 Orders, 37 OrderDetails và 40 Products khớp checksum; 8 Completed, tổng 661.130.000 đồng toàn thời gian. Cùng 5 container healthy. Chưa commit/push.
