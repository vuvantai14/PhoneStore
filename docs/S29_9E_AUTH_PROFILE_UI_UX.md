# S29.9E — Auth & Profile UI/UX

Ngày kiểm tra: 2026-11-02. **PASS source/build/browser cách ly; PARTIAL nghiệm thu runtime main.**

## Màn hình và thay đổi

| Màn hình | Kết quả |
|---|---|
| Login | Card theo tokens B/C/D, typography, input/checkbox, focus, lỗi đăng nhập, thông báo đăng ký thành công, loading/disabled |
| Register | Nhãn bắt buộc/tùy chọn, hint mật khẩu, summary và inline errors, CTA và liên kết tài khoản |
| Profile | Email hiển thị tĩnh, phân biệt trường được sửa, nhãn và error association đầy đủ, thông báo thành công, trạng thái lưu |
| Access Denied | Icon khóa trang trí, heading, nội dung và CTA sản phẩm theo cùng hệ thống |

Không thêm tính năng tài khoản hoặc thay đổi authentication/authorization/session. Form action, antiforgery, ReturnUrl, RememberMe và field names giữ nguyên. `required`/`minlength` chỉ phản ánh quy tắc model đã có; server validation tiếp tục quyết định kết quả. Không thêm chức năng hiện mật khẩu hoặc đặt lại mật khẩu.

Tái sử dụng helper form S29.9D. Bổ sung cách tìm `.field-error` qua nhiều ID trong `aria-describedby`, để hint mật khẩu không bị ghi đè khi hiển thị lỗi. Các form D với một ID tiếp tục được hỗ trợ.

## File thay đổi riêng S29.9E

- `src/PhoneStore.Web/Views/Account/Login.cshtml`
- `src/PhoneStore.Web/Views/Account/Register.cshtml`
- `src/PhoneStore.Web/Views/Account/AccessDenied.cshtml`
- `src/PhoneStore.Web/Views/Profile/Index.cshtml`
- `src/PhoneStore.Web/wwwroot/css/site.css`: thêm styles có scope `.customer-account-page`.
- `src/PhoneStore.Web/wwwroot/js/site.js`: resolver inline error hỗ trợ nhiều ID.
- Báo cáo này, `docs/PROGRESS.md`, 19 screenshots trong [artifacts/S29.9E](artifacts/S29.9E/).

## Kiểm thử

- `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.9E/`: **0 warnings, 0 errors**.
- `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.9E/ --filter "FullyQualifiedName~AuthTests|FullyQualifiedName~CustomerProfile|FullyQualifiedName~CartControllerTests" --verbosity minimal`: **33/33 PASS** (Profile/Cart; `AuthTests` không khớp tên lớp AuthenticationTests).
- Chạy riêng `--filter "FullyQualifiedName~AuthenticationTests"` cùng output: **7/7 PASS**. Tổng targeted **40/40**, không skipped. Không chạy lại full suite; 162/162 là baseline S29.9D.
- `node --check src/PhoneStore.Web/wwwroot/js/site.js`: PASS.
- Edge/CDP: **36/36 kiểm tra** = 9 trạng thái × 375/768/1024/1440 px. Không tràn ngang; labels đầy đủ; password inputs trống khi render; profile email không là input sửa được.
- Server-error fixtures nhận focus ở summary; lỗi password có 2 liên kết tới đúng fields, hint còn nguyên.
- Client invalid: Login 2 lỗi, Register 4 lỗi, Profile 1 lỗi; summary nhận focus, Enter đến field, Tab/Shift+Tab có focus ring.
- Busy/disabled, chặn submit lặp lúc pending, reset khi pageshow và giữ nguyên antiforgery/ReturnUrl/RememberMe: PASS sự kiện DOM, không POST.
- Reduced-motion nhận diện đúng; transition 0.00001s. Contrast: primary 6.12:1, error 6.53:1, success 6.41:1, disabled 4.63:1; viền input/nền 3.25:1. Không coi đây là chứng nhận WCAG toàn website.
- Không có JavaScript exception trong lượt browser cuối.

## Cách ly và giới hạn

Preview dùng compiled Razor thật với view models và TempData fixture, không đăng ký DbContext/SQL hoặc gọi các service ghi dữ liệu; chỉ GET, mọi method khác trả 405. Preview và Edge được đóng sau kiểm tra. Host tạm ở `obj` có cảnh báo DPAPI/key-ring do sandbox; các GET vẫn render thành công bằng provider ephemeral. Không sửa cấu hình runtime main để xử lý cảnh báo của harness.

Không thực hiện đăng nhập, đăng ký hoặc lưu hồ sơ main; chưa nghiệm thu header/session Customer thật với Razor E. Không deploy/restart main. Static CSS/JS trong wwwroot có thể được runtime đang chạy đọc trực tiếp. Skill áp dụng bằng hướng dẫn đã đọc; Python chưa khả dụng theo kiểm tra trước, không tự cài hoặc chạy lại kiểm kê.

Không thay đổi dữ liệu PhoneStoreDB/demo, migration/seed, Admin hoặc backend. Các targeted tests dùng fixture cách ly; không tạo dữ liệu SQL main.

## Git và bước tiếp theo

Trong lúc thực hiện, HEAD đổi từ `d99a4cf` sang commit D `ece7b676c6baab7f4a738203223b79d3e41cc0a6` — `Shopping cart, checkout, customer order`, ngoài thao tác của agent trong lượt này. Năm views D và báo cáo D được đối chiếu trước/sau và giữ nguyên; CSS D được giữ, helper được mở rộng tương thích.

S29.9E còn **unstaged/uncommitted**; agent không stage, commit hoặc push. **READY để chuẩn bị nghiệm thu tổng thể Customer Web**, cần phê duyệt triển khai Razor D/E lên runtime main rồi browser read-only với Customer thật. Giao dịch hoặc cập nhật dữ liệu main vẫn cần phê duyệt riêng.
