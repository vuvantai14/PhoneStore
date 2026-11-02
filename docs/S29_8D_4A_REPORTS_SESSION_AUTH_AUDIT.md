# S29.8D.4A — Audit Reports edge cases và Admin session authorization

Ngày audit: 2026-11-02 (Asia/Bangkok). Chỉ đọc source/SQL/file; không tạo fixture, không sửa source, không chạy test ghi, không deploy/migration/restore/commit/push.

## 1. Reports: hành vi hiện tại

Luồng: `ReportsControl` lấy `DateTimePicker.Value.Date` thành `DateOnly`; `ReportingService` dựng `startUtc` và `endExclusiveUtc`; `ReportingStore` lọc `OrderDate >= start && OrderDate < endExclusive`.

| Trường hợp | Kết luận từ source |
|---|---|
| `from > to` | VERIFIED: UI và service cùng từ chối rõ ràng. |
| Cùng một ngày | PARTIAL: tạo cửa sổ 24 giờ `[00:00Z, 00:00Z ngày kế)`; đúng nếu ngày nhập là UTC, sai semantics nếu UI là ngày Bangkok. |
| Biên cuối ngày | VERIFIED về toán tử: dùng end-exclusive nên không mất timestamp có phần lẻ và không cần `23:59:59.999...`. |
| Khoảng không có đơn | VERIFIED source: total/completed/revenue/average bằng 0, đủ năm status với count 0, trend/best-seller rỗng; inventory vẫn là snapshot hiện tại. Chưa nghiệm thu UI empty range. |
| `DateOnly.MaxValue` | VERIFIED: service từ chối trước `AddDays(1)`. UI không giới hạn tới hôm nay, nên ngày tương lai hợp lệ và trả dữ liệu rỗng. |
| Completed/Cancelled | VERIFIED: revenue, average và trend chỉ `Completed`; `Cancelled` vẫn nằm trong total/status nhưng không vào revenue. |
| Best sellers | VERIFIED: dùng `Confirmed + Shipping + Completed`, không dùng Cancelled/Pending. Vì vậy có thể có “bán chạy” khi KPI Completed revenue bằng 0; đây là khác biệt định nghĩa, chưa chứng minh là lỗi nghiệp vụ. |
| JOIN/trùng dòng | VERIFIED source: revenue đọc trực tiếp Orders; best-seller group OrderDetails theo Product và cộng quantity/value một lần mỗi detail, không có join làm nhân bản detail. |

### Lỗi đã chứng minh: lệch timezone

- `OrderService` ghi `OrderDate = DateTime.UtcNow`; schema cũng có default `SYSUTCDATETIME()`.
- UI trình bày ngày theo máy người dùng/Bangkok, nhưng `ReportingService` dùng `DateTime.SpecifyKind(local-midnight, Utc)` thay vì chuyển local midnight sang UTC.
- Với UTC+7, chọn ngày 02/11 thực tế phải là `[01/11 17:00Z, 02/11 17:00Z)`. Code hiện dùng `[02/11 00:00Z, 03/11 00:00Z)`: bỏ 7 giờ đầu ngày local và nhận nhầm 7 giờ đầu ngày local kế tiếp.
- `RevenueTrend` group bằng `OrderDate.Date`, tức ngày UTC; quanh nửa đêm local có thể hiển thị sai ngày ngay cả sau khi chỉ sửa range.

Đây là lỗi source có thể suy ra chắc chắn. Chưa có fixture ở hai phía biên để chứng minh bằng runtime UI/SQL.

### Phương án sửa tối thiểu được đề xuất

1. Chốt timezone nghiệp vụ của báo cáo là `SE Asia Standard Time` (Windows/Bangkok, UTC+7) và đăng ký một provider/`TimeZoneInfo` dùng chung; không lấy timezone ngầm từ máy chạy.
2. Chuyển `DateOnly` local thành UTC bằng `TimeZoneInfo.ConvertTimeToUtc` với `DateTimeKind.Unspecified`, giữ truy vấn `[startUtc, endExclusiveUtc)` hiện tại.
3. Nhóm trend theo ngày nghiệp vụ, không theo `.Date` UTC. Với SQL Server có thể dùng biểu thức timezone được provider dịch; nếu chọn fixed UTC+7 thì phải ghi rõ đây là quyết định nghiệp vụ và test biên. Không kéo toàn bộ Orders về memory chỉ để group.
4. Giữ Completed-only cho revenue/average/trend. Đổi nhãn best-seller thành phạm vi “đã xác nhận/đang giao/hoàn thành” hoặc chốt lại nghiệp vụ trước khi thay status set.

## 2. Admin Auth/Session hiện tại

- Login (`AdminAuthService`) là nơi duy nhất đọc `User.IsActive`, kiểm password hash và `Role.RoleName == Admin`.
- `AdminSession` chỉ giữ `AdminIdentity` snapshot. `CatalogManagementGateway.RequireAdmin()` chỉ kiểm `session.Current.Role`, không đọc database.
- `MainForm` kiểm snapshot khi mở và khi `ShowControl`; logout xóa session/đóng shell đã PASS D.3.
- Business management services không nhận actor/Admin identity và không tự authorize; authorization boundary hiện nằm ở desktop gateway.

### Rủi ro đã chứng minh và chưa chứng minh

| Nội dung | Phân loại |
|---|---|
| Admin bị inactive/đổi role sau login vẫn có snapshot Role=Admin | VERIFIED từ source. |
| Lệnh quản trị tiếp theo thực sự ghi thành công sau khi revoke | NOT TESTED; chưa tạo fixture/revoke trong D.4A. |
| Logout kết thúc session/shell | PASS WinForms thật ở D.3. |
| Customer login vào Admin bị Forbidden | PASS automated; WinForms thật NOT TESTED. |
| Revoke xảy ra giữa lần recheck và commit | RISK; check-before-write đơn giản vẫn có race nhỏ, chưa kiểm thử. |

### Các thao tác phải tái xác minh ngay trước ghi

- Product: create/edit, active/inactive, add/remove image, set primary.
- Brand/Category: create/edit, active/inactive.
- Customer: edit profile fields, active/inactive.
- Order: mọi chuyển trạng thái, đặc biệt Cancelled vì hoàn kho trong transaction.

Dashboard, Reports, list/search/detail có thể dùng session đã xác minh với cache ngắn để tránh query mỗi thao tác đọc. Tuy nhiên dữ liệu Customer/Order là nhạy cảm; khi phát hiện revoke phải đóng shell ngay, không tiếp tục read.

### Phương án authorization tối thiểu

1. Thêm store/service chỉ đọc `IsCurrentAdminAsync(userId)` bằng một query `AsNoTracking`: đúng UserId, `IsActive`, RoleName `Admin`.
2. Gateway tách rõ read và write. Trước **mọi write**, luôn fresh-check; không dùng TTL. Read có thể dùng cache rất ngắn (ví dụ 30–60 giây) hoặc check khi mở module/refresh.
3. Nếu false: `session.Clear()`, phát sự kiện session revoked để `AdminApplicationContext` đóng/dispose `MainForm` và mở LoginForm; trả thông báo phiên/quyền đã thay đổi.
4. Check phải xảy ra trước mọi side effect, đặc biệt trước khi copy ảnh vào uploads. Không đặt check chỉ trong UI vì service/gateway còn được gọi từ dialog khác.
5. DB/network error phải fail closed cho write nhưng phân biệt với revoke; không tự logout nếu chỉ là lỗi kết nối tạm thời.
6. Race revoke-after-check-before-commit vẫn còn. Phương án tối thiểu chấp nhận và ghi rõ; nếu yêu cầu bảo đảm tuyệt đối thì authorization phải được kiểm trong cùng transaction với write, là thay đổi kiến trúc lớn hơn.

## 3. Test matrix đề xuất trên DB cách ly

Không chạy trong D.4A. Mỗi ca phải snapshot, dùng fixture riêng, restore trạng thái fixture bằng luồng kiểm soát và không đụng D2C rows.

| Ca | Thiết lập fixture | Hành động | Kỳ vọng |
|---|---|---|---|
| Active Admin | Admin active/Role Admin | Ghi một lookup fixture | Thành công đúng một lần. |
| Inactive giữa phiên | Login rồi đặt fixture Admin inactive | Bấm Save đang mở sẵn | Từ chối trước DML/file; clear session, về LoginForm. |
| Role đổi giữa phiên | Login rồi đổi fixture Admin sang Customer | Bấm transition Order/Save Product | Từ chối; không thay order/stock/product/file. |
| Revoke khi dialog mở | Mở Product/Image dialog, sau đó revoke | Save/upload | Không có DB row/file mới. |
| Revoke sau read | Load list/report, revoke, refresh/read | Theo policy cache; write luôn bị chặn, shell đóng khi phát hiện. |
| DB lỗi khi recheck | Chặn/giả lập query auth | Write | Fail closed, không DML; báo lỗi kết nối, không kết luận “revoked”. |
| Re-login sau revoke | Thử login khi inactive/non-Admin | Login | Invalid/Forbidden; không MainForm. |
| Khôi phục fixture | Active+Admin lại bằng fixture controller được duyệt | Login mới | Thành công; không tái dùng session cũ. |
| Race có kiểm soát | Revoke sau check, trước commit bằng interceptor/barrier | Write | Ghi nhận giới hạn minimal design hoặc yêu cầu transaction-level check. |

Không dùng Admin duy nhất của main. Vì test DB có unique-single-Admin, việc đổi role/inactive phải có kế hoạch fixture và reset được phê duyệt riêng; không tạo Admin thứ hai.

## 4. Hậu kiểm

- Read-only main: Roles 2, Users 2, Brands 8, Categories 1, Products 40, ProductImages 40, Orders 1, OrderDetails 1, migrations 2; stock 740, một Cancelled, một active Admin.
- 40/40 ảnh demo khớp SHA-256 baseline.
- Tests/build không chạy lại vì đây là audit tài liệu/source chỉ đọc; baseline gần nhất vẫn 155/155 và build 0 warnings/errors từ D.3.

## 5. Kết luận

- Reports edge cases: **FAIL (source-proven)** cho mapping ngày local Bangkok sang UTC và trend grouping UTC; các phép lọc end-exclusive/empty/Completed-only là đúng theo định nghĩa hiện tại.
- Admin authorization giữa phiên: **PARTIAL/RISK source-proven**; logout/login đúng, nhưng Role/IsActive chỉ kiểm lúc login.
- Đề nghị bước tiếp theo: phê duyệt sửa timezone + fresh authorization trước write, bổ sung unit/integration matrix, rồi mới chạy fixture SQL/WinForms cách ly.
