# S29.8E.4B — Deploy Customer Web logout-cart isolation fix

Ngày thực hiện: 2026-11-02 (Asia/Bangkok). Được phê duyệt backup/deploy/restart và session-only cart add/logout. Không sửa source, migration, seed, restore DB, commit hoặc push.

## 1. Kết luận

**PARTIAL tổng thể**.

- **PASS deployment/runtime**: Web E.4 mới chạy đúng main, hash/config/uploads đúng, HTTP smoke PASS, không rollback.
- **PASS cart isolation runtime**: người dùng thêm lại vivo Y17s ×1, logout đúng một lần và xác nhận cart anonymous rỗng.
- **PARTIAL database integrity so với kế hoạch**: người dùng tự checkout lần thử đầu tiên ngoài kịch bản, tạo OrderId2 và giảm stock. Không có lỗi deploy nhưng baseline 1 Order/tồn740 không còn giữ nguyên.
- Checkout-token cleanup của logout có automated test PASS; không có runtime evidence riêng trong lượt này vì checkout ngoài kịch bản đã clear token trước phép thử logout tiếp theo.

## 2. Precheck, backup và staging

- Runtime mới và current manifest trước deploy: 62/62 + 62/62 PASS.
- Web cũ PID14572, executable main, port5015, SQL `PhoneStoreDB`, sessions sleeping/openTx0.
- Admin main PID4592 giữ nguyên.
- Precheck DB: Products40, Images40, stock740, Customer1, CancelledOrder1, activeAdmin1; ảnh40/40 hash.
- Backup: `src/PhoneStore.Web/obj/S29.8E.4B-binary-backups/20261102_165417122_predeploy`.
- Backup manifest: `backup-manifest.sha256.txt`, **62/62 PASS**; gồm hai appsettings, không gồm uploads.
- Staging: `src/PhoneStore.Web/obj/S29.8E.4B-staging/20261102_165417122-main`.
- Staging 60 runtime file từ E.4 + hai appsettings main; runtime60/60 PASS, total62; không có test DB/path.

## 3. Shutdown, deploy và restart

- Recheck đúng PID14572/executable/command line; SQL không có session active/open transaction.
- Ctrl+Break đúng một lần qua console helper cô lập; PID14572 thoát, port5015 trống, Admin4592 tiếp tục chạy. Không force-kill.
- Copy đúng 60 runtime files; không xóa file ngoài manifest.
- Sau copy: **60/60 SHA-256 PASS**.
- Hai config giữ nguyên:
  - appsettings `A6F147E5FC54452278333D69C54D269E918B701CF8EA37CB0EBEE2B6D65C86C3`
  - Development `1CC6F4A3F70161146F94E9E08B1943C9A1DB8EC6D50E4399E5BA01043A4F5153`
- Uploads và 40/40 ảnh giữ nguyên hash.
- Web mới PID `12640`, executable `src/PhoneStore.Web/bin/Debug/net8.0/PhoneStore.Web.exe`, command `--urls http://localhost:5015`.
- SQL session PID12640 dùng `PhoneStoreDB`, sleeping/openTx0. Log artifact: `src/PhoneStore.Web/obj/S29.8E.4B-runtime-20261102_165754780`; stderr rỗng, không marker fail/error/unhandled.
- Không rollback.

## 4. HTTP/browser smoke

HTTP GET read-only PASS:

- Home200/6, Products200/40, search iPhone6, search empty0.
- BrandId1=6, CategoryId1=40, detail25=200/4 related.
- Login/Register/Cart200.
- Checkout/Orders/Profile anonymous302→Login.
- 40/40 ảnh HTTP200.
- Warning HTTPS-port HTTP-only đã biết; không gây request failure.

Browser thật: người dùng đăng nhập và thao tác cart thành công. Không có lỗi runtime được báo.

## 5. Sai lệch khỏi kịch bản và trạng thái SQL

Kịch bản được duyệt chỉ cho phép add cart session rồi logout, không checkout. Người dùng báo đã tự đặt hàng thành công trước khi logout. Agent lập tức dừng, chỉ SELECT, không cancel/restore/checkout lại.

Giao dịch ngoài kịch bản đã commit:

- OrderId2, code `ORD-D5AD063ADF220813B7EA345E99`.
- UserId2, status Pending, vivo Y17s ProductId25 ×1.
- UnitPrice/Total 3.490.000đ.
- Product25 stock cuối 37; tổng stock739.
- Orders2, OrderDetails2.

Không có Order thứ ba. OrderId1 vẫn Cancelled. Agent không hủy/confirm/hoàn kho hoặc sửa SQL; rollback binary không thể và không được dùng để đảo giao dịch DB.

## 6. Cart isolation acceptance

Sau khi xác định checkout đã commit, người dùng được hướng dẫn:

1. Thêm lại vivo Y17s ×1 vào cart, không checkout.
2. Logout đúng một lần.
3. Mở cart anonymous.

Người dùng xác nhận **“Đã logout, cart trống”**. Đây là **PASS browser runtime** cho lỗi cart còn sót qua logout. Không có SQL write từ lần add thứ hai/logout; hậu kiểm không có Order thứ ba và stock giữ37.

Checkout token: regression `Logout_clears_cart_and_checkout_token_before_signing_out` PASS trong suite162; runtime lượt logout không quan sát token trực tiếp và không log cookie/session/token.

## 7. Hậu kiểm cuối

- Runtime60/60 hash PASS; backup62/62 hash PASS; config hashes giữ nguyên.
- PhoneStoreDB: Products40, Images40, stock739, Customers1, Orders2, Details2, activeAdmin1.
- Order1 Cancelled; Order2 Pending; Product25 stock37.
- 40/40 ảnh giữ nguyên SHA-256.
- Web12640 và Admin4592 đều đúng executable; log runtime không lỗi.

## 8. Bước cần quyết định tiếp

Giữ nguyên OrderId2 Pending làm bằng chứng cho tới khi người dùng phê duyệt riêng một trong các phương án nghiệp vụ:

- tiếp tục xử lý đơn bình thường; hoặc
- hủy qua UI hợp lệ và xác minh hoàn kho.

Không xóa đơn, chỉnh stock SQL hoặc restore database.
