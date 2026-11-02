# S29.8E.4 — Sửa cô lập giỏ hàng khi Customer logout

Ngày thực hiện: 2026-11-02 (Asia/Bangkok). Được phê duyệt sửa source và tests; chưa deploy Web main, không DML/migration/commit/push.

## Root cause

Authentication cookie và cart dùng hai cơ chế độc lập. `AccountController.Logout()` trước đây chỉ gọi `SignOutAsync`, trong khi `SessionCart` vẫn giữ hai key `PhoneStore.Cart` và `PhoneStore.CheckoutToken` trong browser session. Customer tiếp theo dùng cùng browser vì vậy có thể thấy giỏ của người trước.

Phân loại: **P1 session isolation**. Không có bằng chứng Orders/SQL bị lộ hoặc thay đổi; rủi ro là giỏ và ý định checkout bị dùng chéo trên cùng browser.

## Thay đổi

- `src/PhoneStore.Web/Controllers/AccountController.cs`
  - Inject `SessionCart` từ DI hiện có.
  - Logout xóa cart và checkout token trước khi sign-out cookie.
  - Không gọi `ISession.Clear()`, tránh xóa session data không liên quan.
- `tests/PhoneStore.Tests/CartControllerTests.cs`
  - Thêm test `Logout_clears_cart_and_checkout_token_before_signing_out`.
  - Test tạo cart/token, gọi action logout thật, xác minh cart rỗng, token cũ vô hiệu, auth scheme được sign-out và redirect Home.

## Kiểm thử

- Targeted regression cuối: **1/1 PASS**.
- `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8E.4-full/ --verbosity minimal`: **PASS, 0 warnings, 0 errors**.
- `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8E.4-full/ --verbosity minimal`: **162/162 PASS**, 0 fail/skip.

Hai lần chạy targeted ban đầu FAIL chỉ do test HttpContext thiếu MVC URL helper/route metadata khi tạo `RedirectToActionResult`; harness được hoàn thiện bằng MVC services + `ActionContext`. Logic ứng dụng không phải nguyên nhân các lần FAIL đó.

## Hậu kiểm

- Web main PID14572 và Admin main PID4592 vẫn chạy binary cũ/đã triển khai; không bị thay.
- Main DB vẫn Products40, Images40, total stock740, Customer1, Order1/Cancelled1, Detail1, active Admin1.
- 40/40 ảnh giữ nguyên SHA-256.
- Không ghi main DB.

## Trạng thái

**PASS source/automated; CHƯA DEPLOY main**.

Customer Web đang chạy vẫn là binary trước E.4, vì deploy/restart cần phê duyệt riêng. Cho đến khi deploy, lỗi logout-cart vẫn tồn tại trong runtime main hiện tại.

Rủi ro còn lại: nếu auth cookie hết hạn/revoke mà không đi qua action logout, cart session vẫn có thể tồn tại. Yêu cầu đã báo là logout thủ công; xử lý toàn bộ auth-bound cart lifecycle cần thiết kế owner binding riêng và chưa nằm trong thay đổi tối thiểu này.
