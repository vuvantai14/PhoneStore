# S29.8E.4A — Logout cart fix pre-deployment

Ngày kiểm tra: 2026-11-02 (Asia/Bangkok). Chỉ chuẩn bị; chưa backup/copy/shutdown/restart/deploy, không sửa source/config, không DML/migration/commit/push.

## 1. Source và test provenance

`git diff -- src tests` chỉ có đúng hai file thuộc E.4:

- `src/PhoneStore.Web/Controllers/AccountController.cs`: inject `SessionCart`; logout gọi `Clear()` và `ClearCheckoutToken()` trước `SignOutAsync`.
- `tests/PhoneStore.Tests/CartControllerTests.cs`: regression test logout xóa cart/token, sign-out đúng cookie scheme và redirect Home.

Không có source diff ngoài phạm vi. Hai file source có timestamp trước `PhoneStore.Web.dll` và test DLL trong output `S29.8E.4-full`; output tương ứng source hiện tại. Kết quả đã chạy: targeted 1/1 PASS, build solution 0 warnings/errors, full **162/162 PASS**. Không có source change sau build, nên chưa cần chạy lại tests ở E.4A.

## 2. Đánh giá logic logout

- Chỉ xóa hai key session thuộc cart hiện tại: `PhoneStore.Cart` và `PhoneStore.CheckoutToken`; không gọi `ISession.Clear()`.
- Session là browser-scoped; không query/update User hoặc session của tài khoản khác.
- `SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)` và redirect Home giữ nguyên.
- Không có code Order/Product/Stock, repository hoặc DbContext trong thay đổi.
- Không log password, cookie, checkout token hoặc cart content.
- Regression test gọi action logout thật và xác minh cả cart, token, sign-out và redirect.

Giới hạn đã biết: cookie tự hết hạn hoặc bị revoke không đi qua action logout nên không kích hoạt clear. Fix tối thiểu này giải quyết đúng lỗi logout thủ công được báo; owner-binding cho mọi lifecycle cần thiết kế riêng.

## 3. Runtime hiện tại

- Web main PID `14572`, executable `src/PhoneStore.Web/bin/Debug/net8.0/PhoneStore.Web.exe`, command `--urls http://localhost:5015`.
- Listener `127.0.0.1:5015` và `[::1]:5015`; SQL sessions dùng `PhoneStoreDB`, sleeping/open transaction0.
- Runtime hiện tại vẫn binary cũ: Web DLL hash khác output E.4, nên lỗi vẫn tồn tại cho tới deploy.
- Admin main PID `4592`, đúng executable và không bị ảnh hưởng.
- Current appsettings hashes phải giữ nguyên:
  - `appsettings.json`: `A6F147E5FC54452278333D69C54D269E918B701CF8EA37CB0EBEE2B6D65C86C3`
  - `appsettings.Development.json`: `1CC6F4A3F70161146F94E9E08B1943C9A1DB8EC6D50E4399E5BA01043A4F5153`

## 4. Runtime manifests

- Runtime mới: [S29_8E_4A_WEB_NEW_RUNTIME_SHA256.txt](S29_8E_4A_WEB_NEW_RUNTIME_SHA256.txt), source `src/PhoneStore.Web/bin/S29.8E.4-full`, **62/62 verified**.
- Runtime main hiện tại: [S29_8E_4A_WEB_CURRENT_RUNTIME_SHA256.txt](S29_8E_4A_WEB_CURRENT_RUNTIME_SHA256.txt), source `src/PhoneStore.Web/bin/Debug/net8.0`, **62/62 verified**.
- Hai file cấu hình `appsettings.json` và `appsettings.Development.json` phải preserve, không copy từ build output.
- Deployment subset là **60 file**. So với main hiện tại có 8 file khác hash: PhoneStore Models/Data/Business/Web DLL và PDB; các dependency/runtime khác khớp nhưng vẫn copy đủ subset để tránh bộ binary lẫn phiên bản.
- Không copy `obj`, test binaries, caches hoặc `wwwroot/uploads/products`.

## 5. Backup và staging dự kiến

Đường dẫn backup mới đã kiểm tra chưa tồn tại, **chưa tạo**:

`src/PhoneStore.Web/obj/S29.8E.4B-binary-backups/20261102_165417122_predeploy`

Kế hoạch D.4B:

1. Recheck PID/path/port/SQL transaction và không có request ghi đang xử lý.
2. Backup đủ 62 file runtime main hiện tại, gồm hai config; tạo manifest và verify độc lập 62/62. Không backup/ghi đè uploads.
3. Tạo staging riêng từ 60 file runtime E.4 + hai config copy từ main; xác minh 62/62, config hashes giữ nguyên và không có test DB/path.
4. Shutdown mềm Web PID được khám phá lại. PID14572 là direct EXE không parent/MainWindow. Helper console cũ chạy `Mode=Inspect` qua PowerShell child exit0 và không gửi signal; ở bước deploy phải inspect lại console rồi gửi Ctrl+Break đúng một lần chỉ khi console chứa target/helper, theo phê duyệt. Không force-kill.
5. Chỉ sau khi process thoát và 5015 trống mới copy đủ 60 file, preserve hai config và uploads; verify hashes.
6. Restart HTTP5015, xác minh executable/listener/SQL main/log.
7. Smoke GET Home/Products/Login/Cart/protected redirects/detail/40 ảnh và hậu kiểm SQL/hash.

## 6. Nghiệm thu logout-cart sau deploy

Ca an toàn không ghi SQL nhưng **có ghi browser session**, cần phê duyệt rõ trong E.4B:

1. Người dùng đăng nhập Customer hiện có, tự nhập password.
2. Thêm đúng một sản phẩm còn hàng, quantity1 vào cart. Đây chỉ ghi `PhoneStore.Cart` trong session; không checkout, không Order/stock DML.
3. Xác minh cart có đúng một dòng, sau đó logout qua UI đúng một lần.
4. Mở Cart ở trạng thái anonymous, phải empty; protected routes phải redirect Login.
5. Đăng nhập lại nếu cần và xác minh cart vẫn empty; không bấm checkout.
6. SQL snapshot trước/sau phải giống nhau. Checkout token được chứng minh clear bằng regression test; không đọc/log token browser.

Nếu người dùng không phê duyệt session write, chỉ có thể smoke logout/auth và giữ trạng thái runtime **PARTIAL** cho cart clear.

## 7. Rollback

Nếu startup/hash/config/database target/smoke FAIL:

1. Shutdown mềm binary mới sau khi xác minh console.
2. Restore đủ runtime từ backup mới; giữ/verify config main và uploads.
3. Verify manifest, restart binary cũ và smoke read-only.
4. Không restore database. Nếu commit/session outcome hoặc rollback hash không chắc chắn, dừng và báo BLOCKED.

## 8. Hậu kiểm read-only E.4A

- PhoneStoreDB: Products40, ProductImages40, total stock740, Customers1, Cancelled Orders1, active Admin1.
- 40/40 ảnh khớp SHA-256 baseline.
- Web14572 và Admin4592 vẫn chạy, không bị restart/thay binary.

## 9. Kết luận

**READY** cho S29.8E.4B với phê duyệt riêng bao gồm: backup/staging/shutdown/deploy/restart và một session-only cart add/logout acceptance như mục 6.

Chưa thực hiện deploy hoặc session write trong E.4A.
