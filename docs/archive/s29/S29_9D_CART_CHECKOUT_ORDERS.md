# S29.9D — Cart, Checkout & Orders UI/UX

Ngày kiểm tra: 2026-11-02. **Source/build/browser cách ly PASS; nghiệm thu runtime main PARTIAL.**

## Kết quả giao diện

| Màn hình | Thay đổi | Browser Edge cách ly |
|---|---|---|
| Cart | Card sản phẩm, nhãn số lượng, cập nhật/xóa, tổng tiền, sidebar, empty/unavailable state | PASS |
| Checkout | Form giao hàng, dấu bắt buộc, summary, inline errors + summary nhận focus và liên kết tới ô lỗi | PASS |
| Orders | Order cards, ngày/tổng tiền, badge trạng thái bằng chữ và màu, empty state | PASS |
| Order Detail | Breadcrumb, mã/trạng thái, thông tin giao hàng/sản phẩm, cảnh báo và xác nhận hủy | PASS |
| Success | Đồng bộ typography, thông tin người nhận và summary | PASS render cách ly |

Tái sử dụng tokens S29.9B/C. Không đổi giá, tồn kho, điều kiện hủy, URL/action, session, submission token hoặc logic backend. Giữ POST/antiforgery hiện có; JavaScript chỉ thêm feedback khi gửi, disable nút lúc đang xử lý, khôi phục khi quay lại bằng browser và xác nhận hủy native.

## File source thay đổi

- `src/PhoneStore.Web/Views/Cart/Index.cshtml`
- `src/PhoneStore.Web/Views/Checkout/Index.cshtml`
- `src/PhoneStore.Web/Views/Checkout/Success.cshtml`
- `src/PhoneStore.Web/Views/Orders/Index.cshtml`
- `src/PhoneStore.Web/Views/Orders/Detail.cshtml`
- `src/PhoneStore.Web/wwwroot/css/site.css`
- `src/PhoneStore.Web/wwwroot/js/site.js`

## Kiểm thử thực tế

- `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.9D/`: 0 warnings/errors.
- `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.9D/ --verbosity minimal`: **162/162 PASS**, 0 skipped.
- Sau chỉnh wrapper checkout cuối cùng: build lại Web `--no-restore -p:OutputPath=bin/S29.9D/`, 0 warnings/errors; không lặp full regression cho thay đổi wrapper.
- `node --check src/PhoneStore.Web/wwwroot/js/site.js`: PASS (chạy ngoài sandbox vì Node bị chặn lstat trong sandbox).
- Edge thật qua CDP, viewport chính xác **375/768/1024/1440**: **40/40 kiểm tra layout PASS** (10 trạng thái × 4 kích thước), không tràn ngang hoặc ảnh hỏng trong fixture.
- Trạng thái: Cart có hàng/trống/không khả dụng; checkout bình thường/lỗi server; Orders đủ trạng thái/trống; detail Cancelled/Pending; success.
- Lỗi client: 3 trường bắt buộc tạo 3 inline errors và 3 link summary; focus đúng summary, Enter tới ô tên; Tab/Shift+Tab và focus ring PASS.
- Busy/disabled/nhãn đang gửi, chặn submit lặp lúc pending, giữ token và reset khi pageshow: PASS tương tác DOM trong browser, **không gửi POST giao dịch**.
- Hủy: thử đóng confirmation, submit bị chặn; đơn Cancelled không có action hủy. Không thực thi cancel.
- Reduced-motion: matches=true, transition còn 0.00001s. Contrast tokens text/muted/primary/error/success/warning trên nền trắng: 5.64–15.59:1, đạt AA; đây không phải chứng nhận WCAG toàn website.
- Không có JavaScript exception trong lượt browser cuối.
- Bằng chứng: [screenshots](../../artifacts/S29.9D/) gồm 16 ảnh Cart/Checkout/Orders/Detail tại 4 breakpoint.

## Phạm vi và giới hạn bằng chứng

Preview tạm trong `src/PhoneStore.Web/obj/S29.9D-preview/` render **compiled Razor thật** và CSS/JS hiện tại, với view models fixture trong bộ nhớ; không đăng ký DbContext hoặc SQL/business service. Chỉ GET được chấp nhận, mọi method khác trả 405. Browser preview tại cổng 5196 được đóng sau kiểm tra; không phải deployment main.

Main `GET http://localhost:5015/cart` HTTP 200. Không restart/deploy main; chưa nghiệm thu Razor mới với session Customer trên main. CSS/JS trong source wwwroot có thể được runtime hiện tại đọc trực tiếp; không suy HTTP 200 là nghiệm thu các view mới. Các ảnh fixture preview dùng placeholder có sẵn để kiểm tra bố cục, không thêm/thay ảnh sản phẩm thật.

Harness preview lần restore đầu có NU1900 do không truy cập được vulnerability feed NuGet; solution build/test không có warning này. Không thêm dependency ứng dụng. `py` báo chưa có Python: đọc và áp dụng trực tiếp skill ui-ux-pro-max, không chạy search.py hoặc tự cài Python.

Không có thao tác SQL/DML/main checkout/cancel/cart update/profile update. Automated tests dùng dữ liệu cách ly hiện có; không tạo Customer/Order trên PhoneStoreDB. Không sửa controller/service/repository, Admin, configuration, migration hoặc database; không commit/push.

## Bước tiếp theo

**READY về source cho S29.9E.** Chờ người dùng đặt tên commit và yêu cầu push. Nghiệm thu Razor mới trên runtime main cần bước triển khai được duyệt riêng; không dùng bước đó để tạo/hủy đơn thật nếu chưa có phê duyệt ghi dữ liệu.
