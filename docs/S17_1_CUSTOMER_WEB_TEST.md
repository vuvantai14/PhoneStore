# S17.1 — Kiểm tra chức năng Customer Web

Ngày kiểm tra: **19/10/2026**  
Phạm vi: Customer Web, Business, Data, test cô lập và tích hợp đọc với `PhoneStoreDB`.

## 1. Môi trường và baseline

- Windows, .NET SDK 8.0.319, .NET 8, EF Core 8.0.28.
- SQL Server mục tiêu: `taiiiii\SERVER`, database `PhoneStoreDB`, Windows Authentication.
- Test phá hủy chạy trên SQLite in-memory; không ghi vào `PhoneStoreDB`.
- `dotnet restore PhoneStore.sln`: thành công, tất cả project đã up-to-date.
- `dotnet build PhoneStore.sln --no-restore`: **0 warning, 0 error**.
- `dotnet test PhoneStore.sln --no-restore`: **26 passed, 0 failed, 0 skipped**.
- Browser automation: Microsoft Edge headless; chỉ chạy các trang/luồng không ghi dữ liệu.

### Baseline dữ liệu

Không thể đọc lại số lượng hiện tại trong lần chạy này. Dịch vụ đúng instance `MSSQL$SERVER` đang `Stopped`; tiến trình kiểm thử không có quyền khởi động dịch vụ. `sqlcmd` và EF Core đều trả lỗi SQL Network Interfaces error 26. Không chuyển sang instance khác.

Baseline gần nhất đã được xác minh và ghi trong S17:

| Dữ liệu | Baseline S17 gần nhất | S17.1 hiện tại |
|---|---:|---|
| Roles | 2 | MANUAL PENDING |
| Admin | 1 | MANUAL PENDING |
| Customer | 1 | MANUAL PENDING |
| Products | 41 (41 active; gồm 40 catalog nhập) | MANUAL PENDING |
| ProductImages | 42 | MANUAL PENDING |
| Orders | 1 Completed | MANUAL PENDING |
| Tổng tồn | 740 | MANUAL PENDING |

S17.1 không tạo user/order, không cập nhật sản phẩm/tồn kho/trạng thái, không đổi schema và không tạo migration.

## 2. Bảng kiểm tra

Ký hiệu: **PASS** = đã thực thi hoặc có test cô lập thực thi; **MANUAL PENDING** = chưa thể chạy an toàn trong môi trường hiện tại. Kiểm tra tĩnh được ghi rõ, không thay thế bằng PASS cho GUI chưa chạy.

| ID | Thao tác | Kết quả mong đợi | Kết quả thực tế | Trạng thái |
|---|---|---|---|---|
| BASE-01 | Restore solution | Restore thành công | Tất cả project up-to-date | PASS |
| BASE-02 | Build solution | 0 warning, 0 error | 0 warning, 0 error | PASS |
| BASE-03 | Chạy test solution | Tất cả test pass | 26/26 pass, 0 fail, 0 skip | PASS |
| BASE-04 | Đọc counts PhoneStoreDB trước/sau | Counts không đổi | SQL service dừng; chưa đọc lại được | MANUAL PENDING |
| AUTH-01 | Đăng ký hợp lệ trong DB cô lập | Normalize email, hash password, role Customer | xUnit xác minh email normalize, hash không phải plaintext và verify được | PASS |
| AUTH-02 | Email trùng khác hoa/thường | Không tạo bản ghi trùng | xUnit trả `DuplicateEmail` | PASS |
| AUTH-03 | Validation đăng ký | Chặn tên/email/phone/password/confirm sai | DataAnnotations và Business validation đã rà; GUI submit chưa chạy | MANUAL PENDING |
| AUTH-04 | Login đúng | Customer active nhận cookie | Logic hasher/role/active đã test; không có phiên credential Customer để chạy GUI | MANUAL PENDING |
| AUTH-05 | Sai mật khẩu | Không đăng nhập | xUnit trả null | PASS |
| AUTH-06 | Tài khoản inactive | Cookie/login bị từ chối | xUnit login bị chặn; `CustomerCookieEvents` revalidate và sign-out | PASS |
| AUTH-07 | RememberMe | Cookie persistent 14 ngày; thường 8 giờ | Controller cấu hình đúng; chưa quan sát cookie qua login thật | MANUAL PENDING |
| AUTH-08 | Logout | POST, anti-forgery, xóa cookie | Attribute và form Tag Helper đã rà; GUI authenticated chưa chạy | MANUAL PENDING |
| AUTH-09 | Protected routes | Anonymous về login kèm local ReturnUrl | Edge: `/checkout` và `/orders` về login đúng | PASS |
| AUTH-10 | External ReturnUrl | Không redirect ra ngoài | Cả GET/POST dùng `Url.IsLocalUrl` trước redirect; hidden field có thể giữ input nhưng không được dùng để redirect ngoài | PASS |
| AUTH-11 | Anti-forgery Login/Register | Token có mặt, POST được bảo vệ | Edge thấy 1 token/form; actions có `ValidateAntiForgeryToken` | PASS |
| CAT-01 | Home | HTTP 200 và dữ liệu DB thật | HTTP 500 do SQL error 26 khi service dừng | MANUAL PENDING |
| CAT-02 | Products | HTTP 200 và catalog active | HTTP 500 do SQL error 26 khi service dừng | MANUAL PENDING |
| CAT-03 | Search | Lọc tên/brand, trim input | Truy vấn server-side đã rà; runtime DB pending | MANUAL PENDING |
| CAT-04 | Brand/category filters | Đúng BrandId/CategoryId active | Truy vấn kết hợp đã rà; runtime DB pending | MANUAL PENDING |
| CAT-05 | Sort | newest/price/name ổn định | Có secondary ProductId; runtime DB pending | MANUAL PENDING |
| CAT-06 | Combined filters | Áp dụng đồng thời, giá hợp lệ | Controller/Data đã rà; runtime DB pending | MANUAL PENDING |
| CAT-07 | Product detail | Giá, tồn, thông số, primary image, related thật | Query active-only và projection đã rà; runtime DB pending | MANUAL PENDING |
| CAT-08 | Invalid/inactive ID | 404 | Controller xử lý ID sai; inactive query trả null; runtime DB pending | MANUAL PENDING |
| CAT-09 | Image URL | Local URL hoặc placeholder, không external URL | Guard URL đã rà; ảnh runtime pending | MANUAL PENDING |
| CAT-10 | Home latest | Active, recent, đa dạng brand, deterministic | Query `AsNoTracking`, candidate giới hạn, interleave deterministic; runtime DB pending | MANUAL PENDING |
| CAT-11 | Home best seller | OrderDetails thật; Confirmed/Shipping/Completed | Query GroupBy/Sum/Take, không fake fallback; runtime DB pending | MANUAL PENDING |
| CART-01 | Add/update/remove | Session đổi đúng, POST có anti-forgery | Controller guards và form Tag Helper đã rà; GUI với catalog pending | MANUAL PENDING |
| CART-02 | Badge/session | Tổng quantity đúng, giữ trong cùng session | `SessionCart.Count` và JSON session đã rà; browser pending | MANUAL PENDING |
| CART-03 | Quantity <= 0 | Từ chối | Controller guard; service tests 0/-1 không ghi order | PASS |
| CART-04 | Quá tồn/hết hàng/inactive | Từ chối | Controller so sánh stock/active từ DB; runtime pending | MANUAL PENDING |
| CART-05 | Giá hiện tại phía server | Không tin giá client | `CartPresenter` lấy `CartProduct.Price` từ DB | PASS |
| CART-06 | Anti-forgery | Mọi thao tác ghi được bảo vệ | Add/Update/Remove có attribute; Razor Form Tag Helper phát token | PASS |
| CHECK-01 | Yêu cầu đăng nhập | Anonymous về login | Edge đạt | PASS |
| CHECK-02 | Receiver/phone/address/note | Validation đúng giới hạn | DataAnnotations + Business validation đã rà; GUI pending | MANUAL PENDING |
| CHECK-03 | Giỏ trống/không hợp lệ | Không tạo order | Controller `CanCheckout`; business từ chối empty; GUI pending | MANUAL PENDING |
| CHECK-04 | Stock recheck | Recheck trong transaction | xUnit insufficient stock rollback đạt | PASS |
| CHECK-05 | Tạo Pending/OrderCode | Tạo đúng trạng thái/mã | Code tạo `Pending`, `ORD-` + 26 ký tự; không tạo order live | MANUAL PENDING |
| CHECK-06 | UnitPrice/Total/stock | Snapshot giá, tổng đúng, trừ tồn | xUnit dùng giá server, tổng 7.500.000, tồn 8→5 | PASS |
| CHECK-07 | Rollback | Không lưu order/trừ tồn khi lỗi | xUnit insufficient stock giữ nguyên order/tồn | PASS |
| CHECK-08 | Duplicate lines/concurrency | Chặn duplicate; Serializable + submission gate | xUnit duplicate line đạt; transaction Serializable/guard đã rà; concurrent HTTP pending | MANUAL PENDING |
| CHECK-09 | Cart clear/idempotency | Clear sau commit; refresh không tạo lại | Controller clear/commit/guard đã rà; authenticated GUI pending | MANUAL PENDING |
| ORDER-01 | History | Chỉ đơn của user, mới nhất trước | Query ownership + OrderDate/OrderId DESC đã rà; GUI pending | MANUAL PENDING |
| ORDER-02 | Detail/historical price | Owner xem được, dùng UnitPrice snapshot | xUnit ownership đạt; projection dùng `OrderDetail.UnitPrice` | PASS |
| ORDER-03 | Unauthorized/cross-customer | 404/không lộ đơn | xUnit owner khác nhận null/empty | PASS |
| ORDER-04 | Status display | Phản ánh trạng thái Admin | Shared DB/query đã rà; runtime pending | MANUAL PENDING |
| INT-01 | Checkout Web → Admin | Đơn mới xuất hiện trong Admin | Evidence S17 có đơn thật; S17.1 không tạo order live | MANUAL PENDING |
| INT-02 | Admin status → Customer | Customer thấy Completed | Evidence S17 đã xác minh; S17.1 không đổi Completed order | MANUAL PENDING |
| SEC-01 | Password hashing | Không lưu plaintext | xUnit hash/verify đạt | PASS |
| SEC-02 | Ownership/Customer policy | Không vượt quyền | Policy + query ownership + xUnit đạt | PASS |
| SEC-03 | Database/schema safety | Không schema/migration/data write | Không có migration mới; không chạy lệnh ghi DB | PASS |

## 3. Walkthrough thủ công bắt buộc

Trước khi chạy, người dùng cần khởi động **SQL Server (SERVER)** bằng quyền quản trị và xác nhận Web kết nối được `taiiiii\SERVER\PhoneStoreDB`. Dùng tài khoản Customer hiện hữu; không gửi mật khẩu vào tài liệu hoặc ảnh chụp.

### A. Auth

1. Mở `/account/register`; thử bỏ trống, email sai, password dưới 8 ký tự, confirm khác password. Chụp validation.
2. Không hoàn tất đăng ký trên DB thật nếu không muốn tạo user. Muốn kiểm tra đăng ký live phải được chủ DB chấp thuận trước.
3. Mở `/account/login`; thử sai password và chụp thông báo an toàn.
4. Đăng nhập Customer hiện hữu với RememberMe tắt, kiểm tra cookie session; logout qua nút giao diện.
5. Đăng nhập lại với RememberMe bật, đóng/mở browser và xác nhận phiên còn hiệu lực.
6. Vô hiệu hóa tài khoản chỉ qua chức năng Admin hợp lệ nếu được phép, xác nhận phiên Customer bị thu hồi, rồi khôi phục. Không sửa SQL trực tiếp.

### B. Home → Products → Detail

1. Mở Home; chụp latest và best-selling, ghi tên/giá/tồn/ảnh.
2. Đối chiếu best-selling với OrderDetails của các đơn Confirmed/Shipping/Completed.
3. Vào Products; thử search, từng brand/category, giá min/max, sort và combined filter.
4. Mở một sản phẩm active; kiểm tra gallery, thông số, related, giá, tồn.
5. Mở `/products/999999`; kỳ vọng 404. Mở ID inactive nếu có; kỳ vọng 404.

### C. Cart

1. Add sản phẩm còn hàng; badge tăng và cart dùng giá hiện tại.
2. Update quantity hợp lệ; thử 0, số âm, lớn hơn stock; xác nhận bị chặn.
3. Remove; xác nhận badge/tổng cập nhật.
4. Mở tab mới cùng browser để xác nhận cùng session; browser profile khác phải có cart riêng.
5. Gửi POST không có anti-forgery bằng công cụ dev chỉ trong môi trường test; kỳ vọng 400. Không làm trên production.

### D. Checkout → My Orders

1. Chuẩn bị cart nhưng **dừng trước nút đặt hàng**.
2. Kiểm tra prefill, validation receiver/phone/address/note và summary.
3. **Cần user phê duyệt rõ ràng trước khi tạo checkout live mới.** Sau khi được duyệt, ghi counts/tồn trước và sau.
4. Đặt một lần; xác nhận Order Pending, OrderCode, UnitPrice, TotalAmount, stock giảm và cart clear.
5. Refresh success; xác nhận không tạo đơn thứ hai.
6. Vào My Orders; kiểm tra newest-first, status và detail.
7. Dùng Customer khác mở URL order; kỳ vọng 404.
8. Trong Admin, chuyển trạng thái bằng workflow hợp lệ; quay lại Customer và xác nhận status mới. Không thay đổi Completed order hiện hữu.

## 4. Bằng chứng cần lưu

- Ảnh Home, Products/filter, Product Detail, Cart, Checkout validation, Success, My Orders và Order Detail.
- Network/status cho 404, redirect login, anti-forgery 400 và ownership 404.
- Cookie attributes cho RememberMe tắt/bật; che toàn bộ giá trị cookie/token.
- SQL counts trước/sau, OrderCode, status, UnitPrice, TotalAmount và stock delta; không chụp PasswordHash/connection string.
- Ảnh Admin order/status cho kiểm tra cross-application nếu được phê duyệt.

## 5. Bugs và fixes

- Không phát hiện defect chức năng/bảo mật mới từ 26 test cô lập và browser checks có thể chạy.
- Blocker môi trường: `MSSQL$SERVER` stopped; lệnh khởi động bị từ chối quyền. Home/Products trả 500 trong Development với SQL error 26. Không đổi instance hay cấu hình để che lỗi.
- Không sửa source chức năng trong S17.1.

## 6. Kết luận

S17.1 ở trạng thái **PARTIAL**. Build/test cô lập, anti-forgery GET forms, anonymous authorization và local ReturnUrl đã xác minh. Baseline DB hiện tại, catalog/cart runtime, authenticated GUI, checkout và Customer Orders còn **MANUAL PENDING** do SQL service dừng và do không được phép tạo order live. Sau khi bật đúng SQL instance, chạy walkthrough read-only trước; mọi checkout live mới phải xin phê duyệt riêng.
