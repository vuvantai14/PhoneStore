# S29.8D.4C.3 — Tổng hợp nghiệm thu Admin và readiness triển khai

Ngày tổng hợp: 2026-11-02 (Asia/Bangkok). Audit/tổng hợp và SQL chỉ đọc; không sửa source, deploy, migration, restore, seed, commit/push hoặc DML.

## 1. Ma trận nghiệm thu

| Nhóm | Source / automated / SQL isolated | WinForms thật | Kết luận |
|---|---|---|---|
| Login Admin | Auth validation/role/active và Customer forbidden PASS tests | Admin login, sai password, logout, login lại PASS test DB | **PASS**; non-Admin login thật chưa dùng credential Customer nhưng service/test phủ |
| Session logout | Session clear/application context có tests/source | MainForm đóng→LoginForm; không thao tác tiếp bằng session cũ | **PASS** |
| Revoke IsActive | Fresh-check/gateway/fail-closed PASS automated | IsActive1→0, dialog mở trước revoke bị chặn, shell→Login; restore0→1+login PASS | **PASS runtime** |
| Revoke RoleId | Fresh-check RoleName PASS automated | Role1→2 bị chặn/clear session; restore2→1+login PASS | **PASS runtime** |
| Dashboard | Store formulas + SQL đối chiếu | KPI 4 active products/stock9/4 customers/10 orders/1 Completed revenue1m PASS | **PASS** |
| Reports | Bangkok calendar, half-open UTC range, Completed-only, empty/same-day tests + SQL translation PASS | Positive range, same-day và empty range PASS D.4C.1 | **PASS** trong dữ liệu hiện có |
| Product CRUD | Validation/FK/stale-stock/atomic conditional update/rollback PASS automated+SQL | Create/edit/search/filter/soft-deactivate/required validation PASS | **PASS core** |
| Brand CRUD | Duplicate/reference/error/deactivate PASS automated+SQL | Add/edit/search/deactivate/reactivate PASS | **PASS core**, text validation cụ thể PARTIAL |
| Category CRUD | Validation/reference/deactivate PASS automated+SQL | Add/edit/search/required/duplicate/deactivate/reactivate PASS | **PASS** |
| Product images | Decode/limits/APNG/WebP/concurrency/rollback/shared-reference PASS automated+SQL | JPEG/PNG add, 6 rejects, set primary, unlink+retention PASS | **PASS core**; DisplayOrder edit PARTIAL; replace-file NOT SUPPORTED |
| Stale stock | Atomic predicate and SQL race scenarios PASS | Dialog conflict trước đây PASS; bản fix nằm trong D.4B binary | **PASS** |
| Orders | Transition/transaction/restock/idempotency PASS automated+SQL | List/detail/filter/valid-invalid transitions PASS cách ly; main Confirm PASS lịch sử | **PASS** |
| Customers | Store/service validation, protect Admin PASS tests | Danh sách read-only PASS; edit/activate Customer chưa nghiệm thu UI đầy đủ | **PARTIAL**, không phải lỗi đã chứng minh |
| Authorization DB unavailable | Gateway fake fail-closed PASS | Không ngắt DB thật để thử | **PASS automated / NOT TESTED UI** |

Baseline mới nhất: full build D.4B 0 warnings/errors; 161/161 tests PASS; tám SQL integration read-only PASS. Không chạy lại trong D.4C.3 vì không sửa source.

## 2. Revoke runtime và giới hạn

- Cả hai ca IsActive và RoleId đã PASS trên Admin test D.4B/SQL Server thật; fixture cuối cùng UserId1/RoleId1/Admin/IsActive1 và đăng nhập lại thành công.
- Nội dung thông báo revoke không được giữ lại sau transition nhanh sang LoginForm: **PARTIAL về evidence text**, không ảnh hưởng bằng chứng chặn DML/session clear.
- Fresh-check xảy ra ngay trước service write nhưng không cùng transaction nghiệp vụ. Revoke đúng khoảng sau check và trước commit vẫn có thể cho một write in-flight hoàn tất. Đây là **known security limitation**, không được tuyên bố đã loại bỏ.
- Với ứng dụng desktop một Admin, local SQL và phạm vi đồ án, race hẹp này không chặn deploy nếu người dùng chấp nhận; nếu yêu cầu thu hồi quyền tuyệt đối tức thời thì đây là blocker và cần transaction-level authorization trước triển khai.

## 3. Các PARTIAL CRUD cũ

| Điểm | Đánh giá readiness |
|---|---|
| Sửa `DisplayOrder` ảnh | UI hiện không có chức năng chỉnh order; add/primary/unlink hoạt động. **Known limitation**, không blocker trừ khi đặc tả bắt buộc sắp thứ tự thủ công. |
| Đóng form giữa lúc Save | Lifetime/busy/error handling và transaction có trong source/tests, nhưng chưa có full UI evidence. Chưa có lỗi thực tế. **Test gap**, không blocker; smoke sau deploy và tránh force-close lúc đang lưu. |
| Thông báo blank/duplicate Brand | Dữ liệu bị từ chối và không phát sinh row đã chứng minh; chỉ text status không đọc đầy đủ qua UIA. **Evidence/UI wording gap**, không blocker. |
| Replace file trực tiếp | NOT SUPPORTED theo thiết kế; quy trình là add ảnh mới→set primary→unlink ảnh cũ. Không phải regression/blocker. |
| Customer edit/lock UI | Chưa nghiệm thu đầy đủ. **Functional gap còn lại**; nếu rubric bắt buộc Admin chỉnh/khóa Customer thì phải nghiệm thu trước khi gọi toàn hệ thống complete, nhưng không chặn deploy các fix D.4B. |

Không có FAIL runtime còn mở trong phạm vi D.4B. Không có P0/P1 data-integrity defect đã chứng minh.

## 4. Cấu hình, binary và rollback

- Main config source và `bin/Debug/net8.0-windows/appsettings.json`: `localhost/PhoneStoreDB`, uploads relative tới Web `wwwroot/uploads/products`.
- Test D.4B staging: `obj/S29.8D.4C.1-20261102_155007187/app`; config của nó trỏ `PhoneStoreDB_StockConcurrencyTest` và uploads test. **Không được sao chép appsettings test vào main.**
- D.4C.1 manifest hiện kiểm đúng field SHA256: **59/59 PASS**. Bốn PhoneStore DLL staging khớp D.4B-final. Bốn DLL trong main Debug hiện cũng khớp staging trên đĩa, nhưng chưa coi là deployed vì không có Admin main runtime/smoke trên main; PID15548 là test staging duy nhất.
- Backup C.5 cũ không phải rollback baseline đủ mới cho lần deploy D.4B. Trước deploy phải tạo **backup binary main mới có timestamp + manifest**, giữ riêng appsettings/secrets/uploads.

Kế hoạch deploy bắt buộc sau phê duyệt:

1. Xác minh source không đổi từ build 161 tests, hoặc build/test lại và tạo main staging mới; manifest toàn bộ runtime.
2. Chụp PID/path/dialog và đóng mềm đúng Admin main nếu đang chạy; không đóng Admin test nhầm.
3. Backup toàn bộ runtime main hiện tại + hash manifest; snapshot config riêng. Không backup/ghi đè uploads qua bước binary.
4. Thay đầy đủ runtime từ staging đã nghiệm thu nhưng **preserve main appsettings**; kiểm hash DLL/runtime và kiểm không có chuỗi test DB trong config main.
5. Khởi động main, xác minh SQL session tới `PhoneStoreDB`, uploads main; login thủ công.
6. Smoke read-only Dashboard, Reports, Product/Brand/Category/Customer/Order và hậu kiểm DB/ảnh.
7. Nếu binary lỗi, dừng rồi rollback từ backup binary mới; không restore DB. Nếu cấu hình/hash không chắc chắn, không rollback tự động.

## 5. Hậu kiểm main

- `DESKTOP-ES9362P/PhoneStoreDB` ONLINE.
- Roles2, Users2, Brands8, Categories1, Products40, ProductImages40, Orders1, OrderDetails1, migrations2.
- Tổng tồn740, một Order Cancelled, một Admin active (`admin@admin.com`).
- 40/40 ảnh demo khớp SHA-256 baseline.

## 6. Kết luận

**CONDITIONAL READY** để triển khai Admin chính.

Điều kiện trước deploy: người dùng chấp nhận race fresh-check→commit và các known limitations trên; tạo backup/manifest mới; staging main không chứa config test; shutdown/copy/restart/smoke phải được phê duyệt riêng. Nếu yêu cầu authorization tuyệt đối cùng transaction hoặc Customer edit/lock là tiêu chí bắt buộc trước release, trạng thái chuyển thành **BLOCKED** cho tới khi hoàn thiện/kiểm thử.
