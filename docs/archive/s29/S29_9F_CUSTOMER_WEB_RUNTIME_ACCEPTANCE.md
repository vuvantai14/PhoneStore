# S29.9F — Customer Web final runtime acceptance

Ngày kiểm tra: 2026-11-02. **Runtime CURRENT; các màn hình runtime chỉ đọc PASS. Trạng thái sau POST nghiệp vụ thật vẫn PARTIAL.**

## Runtime và Git

- Xác minh `ece7b67` chỉ chứa giai đoạn D: views Cart/Checkout/Orders, CSS/JS, progress/report/screenshots; không có backend/configuration/database ngoài phạm vi.
- Trong lượt F, commit E `368daedb3d41bcbe0c351d95d6f82234dcbdb1ff` — `Login, Register Profile` — xuất hiện từ thao tác bên ngoài agent. Source E được giữ nguyên. Không stage/commit/push trong lượt này.
- Web cũ PID6652: console chỉ có Web/helper; không có transaction SQL mở khi kiểm tra. Ctrl+C chưa thoát; Ctrl+Break có log `Application is shutting down...`, process thoát, cổng5015 trống. Không force-kill hoặc dừng process khác.
- Runtime mới **PID14764**, executable `src/PhoneStore.Web/bin/S29.9F/PhoneStore.Web.exe`, content root `src/PhoneStore.Web`, URL `http://localhost:5015`.
- Giữ environment **Production** như runtime trước; appsettings/secrets/uploads không sửa. SQL session của PID14764 xác nhận **PhoneStoreDB**, sleeping/open transaction0.
- Login/Cart xác nhận marker Razor D/E mới. CSS và JS HTTP200, nội dung bytes khớp source, kể cả bản sửa header cuối. Runtime **CURRENT**.
- Không tạo backup source/runtime thủ công.

## Browser Edge/CDP trên runtime thật

Kiểm tra viewport chính xác **375/768/1024/1440 px**. Người dùng tự đăng nhập Customer trên Edge; không đọc/lưu password, cookie hoặc token vào báo cáo.

| Màn hình | Kết quả thực tế |
|---|---|
| Home | PASS smoke responsive, không tràn ngang/ảnh lỗi đang hiển thị |
| Products | PASS; điều hướng từ Cart bằng Enter tới danh sách đủ 40 cards |
| Product Detail | PASS Product25 ở bốn viewport; click product link tới detail Product40 PASS |
| Cart | PASS empty state và một sản phẩm tạm; summary, giá, số lượng, CTA; giỏ được đưa về trống |
| Checkout | PASS form Razor mới, summary và submission-token field; chỉ GET, không gửi đặt hàng |
| Orders | PASS Customer đã đăng nhập; order cards/badges theo dữ liệu hiện có |
| Order Detail | PASS `/orders/1`: Cancelled, không có nút hủy, thông tin và tổng tiền hiển thị đúng |
| Login | PASS UI mới; native invalid/summary-focus, keyboard và feedback DOM |
| Register | PASS UI mới; native invalid/summary-focus, hint và feedback DOM |
| Profile | PASS read-only với Customer thật; email tĩnh, không có trường RoleId/PasswordHash; không Lưu |
| Access Denied | PASS nội dung và CTA sản phẩm |

- **44/44 kiểm tra màn hình × viewport PASS**, cộng thêm 4 ca Cart có hàng; không tràn ngang hoặc ảnh lỗi hiển thị. Trước khi có item session, Checkout về Cart trống đúng hành vi hiện có.
- Tab/Shift+Tab/Enter và focus ring PASS; Enter từ error summary tới đúng field. Login có2 lỗi bắt buộc, Register có4; không POST khi kiểm tra native validation.
- Loading/disabled/pageshow reset PASS bằng sự kiện DOM tổng hợp; không coi đây là bằng chứng hoàn tất một POST nghiệp vụ thật.
- Reduced-motion nhận diện đúng, transition còn0.00001s. Không có JavaScript exception trong các lượt browser acceptance.
- Anonymous `/checkout`, `/orders`, `/orders/1`, `/account/profile` HTTP302 tới login.
- 38 screenshots tại [artifacts/S29.9F](../../artifacts/S29.9F/); ảnh Orders/Profile1024 đã cập nhật sau sửa header.

## Lỗi được sửa

**P2: header đã đăng nhập ở1024px ép ô search quá hẹp, nút Tìm chồng lời chào.** Bằng chứng phát hiện bằng screenshot Profile thật. Chỉ sửa `site.css`: tại1001–1199px cho search xuống hàng riêng. Tái kiểm tra header375/768/1024/1199/1200/1440: diện tích chồng=0, nút Tìm nằm trong search, không tràn trang. Không thay đổi Razor/backend/nghiệp vụ.

## Build/tests và dữ liệu

- `dotnet build src/PhoneStore.Web/PhoneStore.Web.csproj --no-restore -p:OutputPath=bin/S29.9F/`: 0 warnings/errors, dùng source gồm D/E.
- Sau sửa CSS: build Web `-p:OutputPath=bin/S29.9F-validation/`: 0 warnings/errors. CSS phục vụ trực tiếp từ content root nên không cần restart lần nữa.
- Không lặp full regression: E targeted40/40 và D full162/162 là bằng chứng lịch sử của source backend/JS không đổi trong F. F chạy build và browser checks cho thay đổi CSS.
- Trước thao tác Cart tạm: xác minh source Add/Remove chỉ ghi SessionCart qua distributed memory, SQL chỉ đọc product. Giỏ Customer ban đầu trống; qua UI thêm **Product25 ×1**, chỉ mở GET Checkout, rồi xóa đúng item vừa thêm. CDP giới hạn POST chỉ `/cart/add` và `/cart/remove`; ghi nhận đúng2 request này, **không POST checkout/profile/cancel**. Giỏ cuối trống.
- SELECT trước/sau cùng baseline thực tế: **PhoneStoreDB, Products40, Images40, tổng tồn733, Orders4, Users2**. Không dùng snapshot cũ740/1 đơn làm trạng thái hiện tại. Không có thao tác SQL DML/migration/seed/restore hoặc thay đổi sản phẩm/đơn/tài khoản.
- Startup kiểm tra sớm trước khi listener sẵn sàng có một lần connection refused; sau đó PID14764 listen và các GET thành công. Log cuối không có `fail:`/unhandled exception; cảnh báo HTTPS redirect khi chạy HTTP vẫn là giới hạn có sẵn.

## Giới hạn và readiness

**PARTIAL** cho server-error/success sau POST đăng ký/lưu hồ sơ/đặt hàng/hủy thật trên main: cố ý không thực hiện. Các trạng thái fixture đã PASS ở D/E, không nâng thành PASS main. Không có lỗi UI chặn sử dụng được ghi nhận sau sửa header.

**READY chuyển sang UI/UX Admin WinForms** trong nhiệm vụ riêng. Customer runtime acceptance chỉ đọc hoàn tất; giữ giới hạn POST nói trên. Không tự sửa Admin, đặt tên commit hoặc push. Thay đổi F gồm CSS, báo cáo/progress và screenshots; chờ người dùng quyết định checkpoint.
