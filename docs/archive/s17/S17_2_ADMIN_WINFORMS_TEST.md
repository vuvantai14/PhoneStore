# S17.2 — Kiểm thử chức năng Admin WinForms

## 1. Môi trường và nguyên tắc

- Ngày kiểm tra: 20/10/2026.
- Windows, .NET SDK 8.0.319, .NET 8, EF Core 8.0.28, SQL Server Developer.
- Cấu hình giữ nguyên: `taiiiii\SERVER`, database `PhoneStoreDB`, Windows Authentication.
- Không đổi connection string, schema, migration hoặc dữ liệu nghiệp vụ.
- Không tạo sản phẩm, khách hàng hay đơn thử trên database thật.
- Các thao tác ghi, rollback, cạnh tranh và chuyển trạng thái dùng SQLite in-memory cô lập.

## 2. Database baseline và final

Trong lần kiểm tra tự động trước, EF Core (`Microsoft.Data.SqlClient`) và `sqlcmd` không thể mở kết nối dù `MSSQL$SERVER` đang chạy. Lỗi chính xác:

> The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.

Kiểm tra khi đó cho thấy SQL Server có `ForceEncryption = 0`, không gắn certificate, còn `Get-TlsCipherSuite` trên Windows trả danh sách rỗng. Trong phiên kiểm tra mới, người dùng xác nhận Admin đã kết nối thành công với `PhoneStoreDB` và hoàn thành A01–A10. Vì vậy lỗi TLS/Schannel trước đây không còn chặn ứng dụng Admin trong phiên manual mới.

Ngày 20/10/2026, truy vấn đếm chỉ đọc độc lập bằng `sqlcmd` được thử lại nhưng client ODBC vẫn trả `Encryption not supported on the client` và `No credentials are available in the security package`. Không thay đổi connection string hay cấu hình SQL để lách lỗi.

| Bảng | Baseline hiện tại | Final hiện tại | Ghi chú |
|---|---:|---:|---|
| Products | BLOCKED | BLOCKED | `sqlcmd` hiện tại không truy vấn được |
| Brands | BLOCKED | BLOCKED | `sqlcmd` hiện tại không truy vấn được |
| Categories | BLOCKED | BLOCKED | `sqlcmd` hiện tại không truy vấn được |
| Users | BLOCKED | BLOCKED | `sqlcmd` hiện tại không truy vấn được |
| Orders | BLOCKED | BLOCKED | `sqlcmd` hiện tại không truy vấn được |
| OrderDetails | BLOCKED | BLOCKED | `sqlcmd` hiện tại không truy vấn được |
| ProductImages | BLOCKED | BLOCKED | `sqlcmd` hiện tại không truy vấn được |

Mốc gần nhất đã xác minh trong S17, chỉ dùng làm tham chiếu lịch sử: 41 Products, 8 Brands, 1 Category, 2 Users (1 Admin, 1 Customer), 1 Order Completed, 1 OrderDetail và 42 ProductImages. Đây không được ghi nhận là baseline S17.2 vì truy vấn hiện tại chưa chạy được.

Không có lệnh ghi database do tác vụ cập nhật tài liệu này thực hiện. Người dùng đã kiểm tra CRUD/workflow thủ công trong phiên mới, nhưng không có số đếm trước và sau phiên đó để đối chiếu. Vì vậy chưa ghi nhận database preservation là đã được xác minh.

## 3. Kết quả kiểm thử

Quy ước: **PASS (AUTOMATED)** là kết quả test/build đã chạy trong báo cáo trước; **PASS (MANUAL — USER CONFIRMED)** là kết quả người dùng trực tiếp kiểm tra trong phiên mới; **BLOCKED** là bằng chứng độc lập chưa thu được. Không quy đổi xác nhận manual thành automated.

| ID | Hành động kiểm tra | Kết quả mong đợi | Kết quả thực tế | Trạng thái | Bằng chứng |
|---|---|---|---|---|---|
| BASE-01 | Restore/build/test solution | Không lỗi | Báo cáo trước: restore đạt; build 0 warning/0 error; 26/26 test đạt; chưa chạy lại trong lần cập nhật này | PASS (AUTOMATED — PREVIOUS RUN) | Lệnh xác minh mục 6 |
| BASE-02 | Kết nối đúng `PhoneStoreDB` | Kết nối Windows Auth thành công | Admin đã kết nối DB thành công trong phiên manual mới; `sqlcmd` độc lập vẫn lỗi TLS | PASS (MANUAL — USER CONFIRMED) | Xác nhận người dùng + lỗi client độc lập mục 2 |
| BASE-03 | Ghi baseline 7 bảng | Có số đếm hiện tại | Không thể truy vấn | BLOCKED | Mục 2 |
| AUTH-01 | Đăng nhập Admin thật | Admin active vào MainForm | Đăng nhập/RBAC đã được người dùng kiểm tra thành công | PASS (MANUAL — USER CONFIRMED) | A01 |
| AUTH-02 | Sai mật khẩu | Từ chối bằng thông báo an toàn | Logic hash/invalid credentials và test cô lập đạt | PASS | `AuthenticationTests` |
| AUTH-03 | Tài khoản Customer | Trả Forbidden, không mở Admin | Test role cô lập đạt | PASS | `AuthenticationTests` |
| AUTH-04 | Admin inactive | Từ chối | Test mutation cô lập đạt | PASS | `AuthenticationTests` |
| AUTH-05 | Logout và route được bảo vệ | Xóa session, đóng shell, trở lại login | Người dùng xác nhận PASS | PASS (MANUAL — USER CONFIRMED) | A01 |
| AUTH-06 | PasswordHash/credential/log | Không hiển thị hoặc log bí mật | DTO/grid không có PasswordHash; không có logging credentials | PASS | Static scan Admin/Business/Data |
| NAV-01 | Dashboard/Products/Brands/Categories/Customers/Orders/Reports | Đúng UserControl, không duplicate handler | Người dùng xác nhận toàn bộ navigation A02–A10 hoạt động | PASS (MANUAL — USER CONFIRMED) | A02–A10 |
| BRAND-01 | List/search | Trả dữ liệu DB thật | List/search/CRUD/validation đã được kiểm tra trên Admin | PASS (MANUAL — USER CONFIRMED) | A05 |
| BRAND-02 | Add/edit/duplicate validation | Trim, bắt buộc tên, chặn trùng | Kiểm tra service/unique exception đã có; test phá hủy S17 cô lập | PASS | `LookupManagementService`, S17 evidence |
| BRAND-03 | Activate/deactivate | Chỉ đổi `IsActive`, giữ quan hệ | Store không xóa vật lý | PASS | `SetActiveAsync` |
| CATEGORY-01 | List/search | Trả dữ liệu DB thật | List/search/CRUD/validation đã được kiểm tra trên Admin | PASS (MANUAL — USER CONFIRMED) | A06 |
| CATEGORY-02 | Add/edit/duplicate validation | Trim, bắt buộc tên, chặn trùng | Kiểm tra service/unique exception đã có; test S17 cô lập | PASS | `LookupManagementService`, S17 evidence |
| CATEGORY-03 | Activate/deactivate | Chỉ đổi `IsActive`, giữ quan hệ | Store không xóa vật lý | PASS | `SetActiveAsync` |
| PROD-01 | List/search/filter brand/category/status | Kết quả đúng | Người dùng xác nhận list/search/filter và CRUD hoạt động | PASS (MANUAL — USER CONFIRMED) | A03–A04 |
| PROD-02 | Price/stock/foreign key validation | Price > 0, stock >= 0, lookup hợp lệ | Service validation và DB constraint tồn tại; test S17 cô lập đạt | PASS | `ProductManagementService` |
| PROD-03 | CreatedAt/UpdatedAt | Giữ CreatedAt; cập nhật UpdatedAt | Save chỉ đặt CreatedAt khi thêm, UpdatedAt khi lưu | PASS | `ProductManagementService` |
| PROD-04 | Activate/deactivate | Soft toggle | Chỉ thay `IsActive` | PASS | `SetActiveAsync` |
| PROD-05 | Giữ catalog 40 + sản phẩm thủ công | Dữ liệu hiện có được giữ | Catalog được quan sát trong Admin; số đếm độc lập trước/sau chưa có | BLOCKED | A03 + mục 2 |
| IMG-01 | JPG/JPEG/PNG/WebP, signature, <=10 MB | Chỉ nhận file hợp lệ | Kiểm tra extension, magic bytes, 10 MB và test storage S17 đạt | PASS | `LocalProductImageStorage` |
| IMG-02 | Primary duy nhất/display order | Một primary, order >= 0 | Transaction Serializable, khóa parent, normalize primary, validation order | PASS | `ProductManagementService`, `ProductManagementStore` |
| IMG-03 | Xóa file quản lý an toàn | Không xóa file ngoài thư mục/GUID | Canonical path + generated GUID + reference check | PASS | `LocalProductImageStorage` |
| IMG-04 | Ảnh thật tải và URL Web tồn tại | Tất cả URL resolve | Người dùng xác nhận quản lý/hiển thị hình ảnh hoạt động | PASS (MANUAL — USER CONFIRMED) | A04 |
| CUST-01 | Customer-only list/search/detail | Loại Admin; tìm tên/email/phone | Người dùng xác nhận danh sách, search, detail, validation và RBAC đúng | PASS (MANUAL — USER CONFIRMED) | A07 |
| CUST-02 | Edit validation/toggle | Validate và chỉ đổi hồ sơ/IsActive | Service/store đúng; không sửa live Customer | PASS | `CustomerManagementService` |
| ORDER-01 | List newest/search/5 status | Đúng thứ tự và filter | Người dùng xác nhận list/search/filter hoạt động | PASS (MANUAL — USER CONFIRMED) | A08 |
| ORDER-02 | Detail/customer/shipping/history totals | Dùng UnitPrice lịch sử, giữ TotalAmount | Projection dùng `OrderDetail.UnitPrice`; dialog so sánh detail total | PASS | `OrderManagementStore.GetAsync`, `OrderDetailDialog` |
| FLOW-01 | Pending → Confirmed/Cancelled | Cho phép | Theory tests cô lập đạt | PASS | `OrderWorkflowTests` |
| FLOW-02 | Confirmed → Shipping/Cancelled | Cho phép | Theory tests cô lập đạt | PASS | `OrderWorkflowTests` |
| FLOW-03 | Shipping → Completed | Cho phép, không đổi stock | Test cô lập đạt | PASS | `OrderWorkflowTests` |
| FLOW-04 | Completed/Cancelled terminal và transition sai | Chặn | Invalid/stale tests cô lập đạt | PASS | `OrderWorkflowTests` |
| STOCK-01 | Checkout trừ stock/rollback | Serializable, atomic | Test SQLite cô lập đạt | PASS | `CheckoutTests`, `PurchaseStore` |
| STOCK-02 | Cancel Pending/Confirmed hoàn stock | Hoàn đúng một lần | Test cô lập đạt; transaction Serializable | PASS | `OrderWorkflowTests` |
| STOCK-03 | Double restore/Shipping→Completed | Không đổi stock lần hai | Test cô lập đạt | PASS | `OrderWorkflowTests` |
| DASH-01 | Count/revenue/status/recent/best/low stock/refresh | Dashboard hoạt động với DB thật | Người dùng xác nhận Dashboard và refresh PASS; chưa có query độc lập để đối chiếu số | PASS (MANUAL — USER CONFIRMED) | A02 |
| REPORT-01 | Date validation/inclusive end | From <= To; ngày cuối được tính | Service dùng `endExclusive = toDate + 1`; invalid range chặn | PASS | `ReportingService` |
| REPORT-02 | Completed revenue/count/average/trend | Chỉ Completed; giá lịch sử | Query đúng `TotalAmount`; cancelled loại khỏi revenue | PASS | `ReportingStore.ReportAsync` |
| REPORT-03 | Top 10/inventory/low stock <=5/empty | Đúng dữ liệu DB | Người dùng xác nhận Reports PASS; query độc lập chưa đối chiếu lại | PASS (MANUAL — USER CONFIRMED) | A10 |
| INT-01 | Admin product → Web, image → Web | Dùng chung DB và URL | Người dùng xác nhận phần Admin/image; bằng chứng cross-app tự động giữ từ S17 | PASS (MANUAL + PREVIOUS EVIDENCE) | A03–A04, `docs/S17_TESTING.md` |
| INT-02 | Checkout → Admin → Customer status | Dùng chung order | Người dùng xác nhận order/workflow; bằng chứng cross-app tự động giữ từ S17 | PASS (MANUAL + PREVIOUS EVIDENCE) | A08–A09, `docs/S17_TESTING.md` |
| DBSAFE-01 | Không schema/migration ngoài ý muốn | Không thay đổi cấu trúc | Không có source/migration mới; 26 automated tests dùng SQLite. Phiên manual có kiểm tra CRUD/workflow nên không khẳng định zero live writes | PASS cho schema; BLOCKED cho preservation | Git/migration inventory + thiếu counts |

## 4. Checklist GUI thủ công A01–A10

Người dùng xác nhận đã trực tiếp kiểm tra A01–A10 trên Admin kết nối với `PhoneStoreDB`. Đây là bằng chứng manual, không phải kết quả automation và không thay thế baseline/final bằng truy vấn độc lập.

| ID | Các bước thủ công | Kết quả mong đợi | Bằng chứng cần lưu | Trạng thái |
|---|---|---|---|---|
| A01 Login | Login, sai password, RBAC, logout | Xác thực và session đúng | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |
| A02 Dashboard | Dashboard, refresh và số liệu hiển thị | Không lỗi, số liệu hoạt động | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |
| A03 Products | List, search và filters | Kết quả đúng | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |
| A04 Add/Edit Product | CRUD, validation và hình ảnh | Quy tắc hợp lệ được áp dụng | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |
| A05 Brands | List/search/CRUD/validation/active | Hoạt động đúng | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |
| A06 Categories | List/search/CRUD/validation/active | Hoạt động đúng | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |
| A07 Customers | List/search/detail/edit/active/RBAC | Chỉ Customer, không lộ hash | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |
| A08 Orders | List/search/filter/status | Hoạt động đúng | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |
| A09 Order Detail | Detail/workflow/tồn kho/tổng lịch sử | Workflow và stock đúng | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |
| A10 Reports | Date validation, thống kê và báo cáo | Hoạt động đúng | Xác nhận trực tiếp của người dùng | PASS (MANUAL — USER CONFIRMED) |

Kết quả manual tổng hợp: **10/10 PASS theo xác nhận người dùng**.

## 5. Bugs và vấn đề

- Không phát hiện defect chức năng hoặc bảo mật mới trong source code.
- TLS/Schannel từng chặn probe tự động, nhưng Admin đã kết nối thành công trong phiên manual mới và A01–A10 đều PASS.
- Client `sqlcmd` hiện vẫn báo `Encryption not supported on the client` / `SSL Provider: No credentials are available in the security package`; vì vậy baseline/final độc lập chưa thu được.
- Không sửa source hoặc cấu hình kết nối.

## 6. Xác minh cuối

```text
dotnet restore PhoneStore.sln              (báo cáo trước, không chạy lại)
  succeeded

dotnet build PhoneStore.sln --no-restore   (báo cáo trước, không chạy lại)
  0 warnings, 0 errors

dotnet test PhoneStore.sln --no-restore --no-build   (báo cáo trước, không chạy lại)
  26 total, 26 passed, 0 failed, 0 skipped
```

- Migration trước/sau: chỉ `20261001150857_InitialCreate`; không tạo migration mới.
- Schema changed: No.
- Live database writes: tác vụ cập nhật tài liệu không ghi DB; phiên manual có kiểm tra CRUD/workflow nên số thao tác ghi không được suy đoán.
- Probe tạm thời đã được xóa; solution không giữ code setup/test production tạm.

## 7. Đánh giá

**Kết quả chức năng S17.2 = PASS.** A01–A10 PASS theo xác nhận manual của người dùng; báo cáo tự động trước đó có build 0 warning/0 error và 26/26 test PASS.

**Điều kiện đóng hồ sơ S17.2 = BLOCKED.** Chưa có baseline/final counts độc lập cho 7 bảng nên chưa thể ghi database preservation verified. Cần chạy được truy vấn chỉ đọc bằng một SQL client hợp lệ và ghi cùng một bộ số đếm trước/sau một phiên không ghi dữ liệu. Không tự chuyển sang S17.3.
