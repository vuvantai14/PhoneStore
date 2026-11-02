# PhoneStore — Tiến độ và điểm tiếp tục
Cập nhật: 2026-11-02, Asia/Bangkok, sau S29.8D.1C.2 PASS. Đây là snapshot có ngày kiểm tra, không phải cam kết trạng thái live.
Đọc đầu mỗi phiên. Ưu tiên source và kết quả thực tế khi tài liệu mâu thuẫn; chỉ kiểm tra phần liên quan nhiệm vụ.

## 1. Kiến trúc, môi trường và cấu hình
- Root `C:\PhoneStore\PhoneStore`, `PhoneStore.sln`; .NET 8 / SDK 8.0.427, EF/tooling 8.0.28.
- Customer ASP.NET Core MVC và Admin WinForms → Business → Data → Models; EF Core/SQL Server.
- Solution gồm Models, Data, Business, Web, Admin, Tests, AdminBootstrap; CatalogImport/SQL integration harness riêng.
- SQL Server 2022 Developer, MSSQLSERVER; alias localhost, server thực tế DESKTOP-ES9362P.
- Main: PhoneStoreDB, Windows Authentication; key ConnectionStrings:PhoneStoreConnection.
- Connection: `Server=localhost;Database=PhoneStoreDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`.
- Web appsettings/default/Development và Admin appsettings dùng main; factory đọc Web config, env có thể ghi đè.
- CONTEXT.md dùng cho kiến trúc/lịch sử; Current Progress vẫn S29.4 và có server cũ, không dùng làm trạng thái runtime.

## 2. Quy ước bằng chứng
- VERIFIED/PASS thực tế: đã chạy, luôn ghi main hay cách ly và phạm vi; không suy thành mọi ca đều đạt.
- PASS test tự động: có kết quả test đã ghi ngày, không thay nghiệm thu UI; PARTIAL: triển khai/bằng chứng chưa đủ.
- NOT IMPLEMENTED/MISSING: chưa có chức năng; UNKNOWN/NOT TESTED: thiếu bằng chứng, không mặc định là lỗi.
- Báo cáo tổng hợp mới nhất: [S29.8D](S29_8D_FUNCTIONAL_AUDIT.md).

## 3. Runtime và SQL gần nhất — S29.8D.1C.2
- SELECT 2026-11-02 03:26:20 UTC / 10:26:20 Bangkok: localhost/DESKTOP-ES9362P, PhoneStoreDB ONLINE.
- Products40, Images40, Brands8, Category1, Users2, Roles2, Orders1, OrderDetails1, tổng tồn740.
- Admin duy nhất UserId1/admin@admin.com/RoleId1/active; Customer UserId2/tai123@gmail.com/RoleId2/active.
- Customer đã khôi phục tài vũ/09032657212/AddressNULL bằng UI1lần theo duyệtD.1C.2; SQL/Web/greeting khớp, không đăng nhập lại.
- OrderId1, code ORD-05E65B3F0F705464636B525624, Cancelled, 3490000; Product25 vivo Y17s tồn38.
- History: 20261001150857_InitialCreate + 20261102122856_EnforceSingleAdmin; không migration mới cần apply cho C.
- UX_Users_SingleAdmin unique/enabled/filter RoleId1; CK_Roles_AdminIdentity enabled/trusted.
- Web PID19388: `src/PhoneStore.Web/bin/Debug/net8.0/PhoneStore.Web.exe --urls http://localhost:5015` (D.1B mới).
- Admin PID10736: `src/PhoneStore.Admin/bin/Debug/net8.0-windows/PhoneStore.Admin.exe`; đang đăng nhập.
- D.1B Web60/60 runtime khớp staging, backup62/62 và101preservedhash PASS; SQL session cả2 PID main, sleeping/open transaction0.
- GET D: Home6/Products40/Login/Register/Cart HTTP200; search iPhone6, brand1 có6, khoảng10–20triệu có17.
- Detail25 HTTP200, ID0 HTTP404, ảnh40/40 JPEG HTTP200; Customer /orders/1 Đã hủy, không nút hủy.
- Admin D chỉ đọc7 màn hình: Products40/Brands8/Categories1/Customers1/Orders1; Dashboard/Reports doanh thu0, tồn740.

## 4. Chức năng hiện tại
| Nhóm | Trạng thái / phạm vi bằng chứng |
|---|---|
| Customer đăng ký/login | VERIFIED main C.7, người dùng tự nhập mật khẩu; sai/inactive/role PASS tests |
| Customer logout | VERIFIED HTTP cách ly C; không logout phiên main trong audit |
| Catalog/search/filter/sort/detail/ảnh | VERIFIED main A/C.6B/D theo ca đã chạy; chưa phủ mọi tổ hợp filter |
| Giỏ hàng | VERIFIED add qty1 main C.7; update/remove/invalid/stock/session PASS tests + HTTP cách ly |
| Checkout/giá snapshot/trừ kho | VERIFIED main một giao dịch C.7 và SQL/SQLite cách ly; không retry outcome không rõ |
| Checkout idempotency/concurrency | VERIFIED SQL/HTTP cách ly C/C.5; chưa test replay ghi trên main |
| Lịch sử/chi tiết/ownership | VERIFIED UI main C.7/7A; foreign ownership PASS tests/HTTP cách ly |
| Hủy/hoàn kho | VERIFIED main Confirmed→Cancelled, kho38→37→38; double cancel PASS cách ly, không gửi lặp main |
| Hồ sơ Customer self-service | PASS source/tests/deploy/GET/lưu thủ công và khôi phục UI D.1C.2; AddressNULL/cookie/greeting/invariants PASS; đề xuất đóng D.1 |
| Admin login/phân quyền | VERIFIED login main C.6B; role/Customer rejection PASS tests; desktop logout NOT TESTED |
| Dashboard | VERIFIED đọc hiện tại40SP/740kho/1Customer/1Cancelled/0Completed/0doanhthu |
| Sản phẩm/ảnh/lookup/Customer | PARTIAL toàn CRUD; đọc main PASS, còn thiếu write/UI acceptance rộng |
| Stale stock | VERIFIED SQL/dialog thật B.1 + regression; đã triển khai C.6B, không còn chờ deploy |
| Admin quản lý đơn | VERIFIED UI cách ly C.5 toàn workflow; main C.7A Confirm thật và đọc Cancelled |
| Reports | PARTIAL: main đọc0 đúng; source tổng/trend/average chỉ Completed, thiếu regression doanh thu dương/biên ngày |
| AdminBootstrap | VERIFIED S29.6 đã tạo Admin; không chạy lại --execute |

## 5. Mốc và bằng chứng cần giữ
- Kiểm kê sau cài Windows: 26/26 tests; DB mới Roles2/còn lại0; không tìm thấy backup/export/ảnh gốc trong phạm vi đã kiểm.
- Server cũ taiiiii\SERVER không còn là cấu hình chạy; không tự kết nối. Git/clone không khôi phục dữ liệu runtime mất.
- S29 bootstrap source/tests51; S29.3/4 suite72, test EnforceSingleAdmin trên PhoneStoreDB_MigrationTest PASS.
- Quy tắc chính thức: tối đa1 Admin kể cả inactive; Customer nhiều tài khoản. Store tạo Customer không nhận RoleId tùy chọn.
- S29.5B đã apply EnforceSingleAdmin vào main theo duyệt; S29.6 tạo Admin duy nhất theo duyệt.
- S29.7C/D: checklist [PRODUCT_IMAGES](PRODUCT_IMAGES.md),40 JPEG800x800; ảnh AI được người dùng chấp nhận minh họa demo.
- S29.7F: backup/import được duyệt riêng, chạy importer đúng1lần;40/40/8/1/tồn740; không tự chạy lại vì có thể ghi đè.
- S29.8A [audit cũ](S29_8A_FUNCTIONAL_AUDIT.md) phát hiện stale stock; các kết luận commerce cũ đã được C thay thế.
- S29.8B: DTO original stock + atomic conditional UPDATE toàn edit; affected0 từ chối, dialog giữ form/báo xung đột;83tests PASS.
- S29.8B.1 SQL thật/dialog: checkout10→9/stale save từ chối, hai connection affected[1,0], valid save/rollback PASS.
- PhoneStoreDB_StockConcurrencyTest được duyệt tạo/ghi riêng; giữ nguyên, không drop/cleanup/rerun fixture B.1.
- S29.8B.2 chỉ staging lịch sử; bản B đã được đưa vào main cùng C.6B, không triển khai B.2 cũ đè C.
- [S29.8C](S29_8C_COMMERCE_REPORT.md):105tests; transaction/stock locks, durable token OrderCode+unique/sp_getapplock, owner cancel và atomic restock.
- Checkout/cancel khóa UPDLOCK,HOLDLOCK theo ProductId trong Serializable, clear tracking, không tự retry giao dịch.
- [C.5](S29_8C_5_ACCEPTANCE_DEPLOYMENT.md):107tests; actual Orders controls/dialog SQL test PASS confirm→shipping→completed/cancel/filter/invalid; session test bằng reflection, không phải full login.
- C.5 HTTP cách ly PASS auth/cart/CSRF/ownership/price/status sync/replay; không đồng nhất với107 xUnit tests.
- TestDB cuối C.5:3Products stock8/0/0,5Users,10Orders (5Cancelled/1Completed/4Pending); D không ghi/kiểm lại fixture này.
- C.6A shutdown Ctrl+Break được duyệt riêng, PASS đúng console cũ; không suy cách shutdown cho PID mới.
- [C.6B deployment](S29_8C_6B_DEPLOYMENT_RESULT.md):118binary thay/3appsettings giữ;118hash PASS, Web/Admin main smoke PASS, user login thật.
- Staging: Web/bin/S29.8C.5-staging/net8.0 và Admin/bin/S29.8C.5-staging/net8.0-windows, dưới src/PhoneStore.*.
- Binary backup121files: `src/PhoneStore.Admin/obj/S29.8C.5-binary-backups/20261102_013925853Z_de37e8a4ab1c44039836ca8a78baeecf`; giữ manifest, không clean.
- [C.7/7A](S29_8C_7_TRANSACTION_ACCEPTANCE_RESULT.md): đúng1Customer/1Order/1Detail thật; UserId2/OrderId1/DetailId1.
- C.7 checkout qty1 đúng1lần, Pendingstock37/tổng739; user Confirm qua Admin thật, Customer thấy Confirmed.
- User Cancel đúng1lần qua Web; Web/Admin/SQL Cancelled, stock38/tổng740. Không request hủy lặp main.
- SELECT lặp và đối chiếu40catalog/ảnh không đổi; giữ Customer/đơn Cancelled/Detail làm bằng chứng, không xóa.
- Screenshot trong Web/obj: S29.8C.7-pending-receipt.png, confirmed-order.png, cancelled-order.png (cùng tiền tố).

## 6. Backup và ảnh
- Backup SQL trong `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup`.
- S29.5A: `PhoneStoreDB_FULL_COPYONLY_20261102_123908.738Z_131E14FF3BAC44C391332C4392CC2B72.bak`.
- S29.7F: `PhoneStoreDB_S29_7F_PREIMPORT_20261102_001906872Z_47B0A15271A6437A8D5FB05E41E61A38.bak`.
- Gần nhất trước C.7: `PhoneStoreDB_S29_8C_7_PRETRANSACTION_20261102_020840231Z_83DE6E0E3458420896EA6963BCAB5F59.bak`.
- C.7 backup02:08:40UTC/09:08:40Bangkok,566272bytes/set3;FULL COPY_ONLY/CHECKSUM/COMPRESSION + VERIFYONLY CHECKSUM PASS.
- Backup trước giao dịch không chứa Customer2/Order1 mới; chưa thử restore, cùng ổC, VERIFYONLY không thay restore rehearsal.
- Ảnh runtime: `C:\PhoneStore\PhoneStore\src\PhoneStore.Web\wwwroot\uploads\products`; cần backup riêng, không tự xóa/ghi đè.

## 7. Tests và rủi ro còn mở
- Gần nhất D.1B: build0warnings/errors,129/129 PASS,0skip;107 baseline +22profile (12service/10HTTP); restore D.1 PASS.
- D.1C.1 chạy lại22/22profile PASS (SQLite/HTTP thật cách ly), output bin/S29.8D.1C.1-tests; khôngfullsuite/deploy/mainPOST.
- Lệnh: `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8D.1B-build/`; `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8D.1B-build/ --verbosity minimal`.
- Suite SQLite: Authentication, Bootstrap, SingleAdminPolicy/Constraint, Checkout, OrderWorkflow, ProductStockConcurrency, CommerceWorkflow, CartController.
- SQL/HTTP/UI harness riêng opt-in trong tests/PhoneStore.SqlIntegration; không chạy audit read-only để tránh ghi fixture.
- Chưa có suite báo cáo/CRUD lookup/ảnh/Customer đầy đủ; desktop logout, RememberMe dài ngày, stress/commit không rõ chưa nghiệm thu đủ.
- Original-stock guard không phải rowversion mọi field/ABA; cart theo browser session; legacyCreateAsync không token nhưng Web dùng Idempotent.
- Revenue tổng/trend/average chỉ Completed; BestSellers/SalesValue dùng Confirmed/Shipping/Completed, không cùng chỉ số doanh thu.
- DateOnly report được coi UTC nhưng UI ngày local: cần chốt timezone/test biên ngày. Admin gateway kiểm session snapshot, chưa revalidate DB từng command.
- HTTPS redirect thiếu port khi chạy HTTP vẫn warning đã biết; không phát hiện runtime failure mới qua audit đọc.
- Word/Excel UC01–UC12/SCR01–SCR12/rubric chấm điểm chưa được xác minh; không tự suy đáp ứng toàn bộ tài liệu chưa có.

## 8. Nhiệm vụ hiện tại và bước tiếp theo
- S29.8D audit hoàn thành, toàn hệ thống PARTIAL; D.1 profile source/tests PASS, D.1B đã triển khai/GET main PASS.
- [D.1 report](S29_8D_1_CUSTOMER_PROFILE.md): CustomerProfile DTO/store/service/interface, ProfileController/model/view, Program DI/public test entry, layout link,2test files/test csproj và docs.
- Chỉ GET/POST /account/profile, Customer active từ claimID; UPDATE có điều kiện chỉ FullName/Phone/Address; Email readonly, không bind ID/role/hash; CSRF+validation server.
- Đổi tên cập nhật Name claim và giữ expiry/persistent cookie; tests overposting/owner/anonymous/inactive/role/CSRF/validation/error/session PASS.
- WebApplicationFactory chạy Program/MVC/cookie/filter thật với SQLite và DataProtection ephemeral; không SQL test writes, không tạo môi trường SQL mới.
- [D.1B deployment](S29_8D_1B_PROFILE_DEPLOYMENT.md): CtrlC20giây không thoát; recheck console chỉ Web/helper và SQL idle rồi CtrlBreak1lần, log shutdown/exit/5015trống PASS; không force.
- D.1B copy60runtime từ staging62, giữ2appsettings; backup62hash và101Admin/uploads/source-confighash giữ nguyên; chỉ restart Web19388, Admin10736 giữ.
- Artifact/backup/staging/manifests/log: `src/PhoneStore.Web/obj/S29.8D.1B-20261102_024935357Z_9be18a0a8d6a413d9cfa63304bc5d7f7`; giữ, khôngclean.
- Hậu kiểm D.1B mainONLINE40/40/8/1,Users2,Order1Cancelled,Detail1,tồn740,Customer tên tài vũ; cảWeb19388/Admin10736 SQLmain, khôngghi.
- Browser sessionCustomer cũ còn hợp lệ; clicklinkHome→Hồsơ PASS đúngowner/emailreadonly/noSensitiveFields; anonymous302login;40detail/40ảnh200.
- [D.1C acceptance](S29_8D_1C_PROFILE_ACCEPTANCE.md): được duyệt sửa tạm/khôi phục User2 qua UI; preflight đúngPID19388/10736/main/customer và60Web+101preservedhash PASS.
- Gốc SELECT: tài vũ/09032657212/AddressNULL; nhập form Khách hàng Kiểm thử/0900000000/Địa chỉ kiểm thử PhoneStore, thửmouseclick Lưu1lần.
- Helper timeout không xác nhận success; DỪNG, khôngretry/khôi phục tự động. DOM còn giá trị thử chưa lưu, greeting tên cũ; chưa chứng minh POST thực sự được gửi, không kết luận app lỗi.
- SELECT sau dừng hồ sơ vẫn gốc/email-role-active giữ nguyên;9/9 snapshot bảng không đổi (Users không đọc credentials),catalog40/40/tồn740/Order1Cancelled/Admin1active.
- D.1C PARTIAL, cookie sau đổi tên/restore NOT TESTED;129testsbaseline D.1B, khôngrerun. Chỉdocs+evidenceobj; khôngsource/SQLDML/binarychange.
- Artifact `src/PhoneStore.Web/obj/S29.8D.1C-20261102_031553601Z_0f70f82b55224b31a97cc4420a0835cf`; giữedit.attempt, khôngxóa marker/rerunhelper để thửghi.
- [D.1C.1 diagnosis](S29_8D_1C_1_PROFILE_DIAGNOSIS.md): quan sátDOM cho greetingtênmới; SELECT3fieldđãtestvalues, userxácnhận tựbấmLưu. CheckpointgốcC.1 chỉlàlịchsử.
- Formaction/method/name/CSRFpresent đúng, nativevalid3field, nosubmitJS/listeners trực tiếp, noerror. Log3UPDATE nhưng0HTTPrequestmetadata; helperPOSTcũ UNKNOWN, nghi vấn scrollsmooth→tọađộclick chưaổnđịnh, chưa chứngminh.
- C.1.1 chỉđọc main,22tests PASS;8bảngkhác nguyên, Users SELECTchiếu3fieldgốc đểso khớp snapshot→cácfieldkhác/Adminnguyên. Web60hashnguyên, khôngsourcefix.
- [D.1C.2 restore](S29_8D_1C_2_PROFILE_RESTORE.md) PASS sau duyệt riêng: kiểmtra đúngowner/3giátrị/nút rồi nativebuttonclick1lần;1POST302→GET200/reloadGET200, successmessage và greetingtài vũ, sessiongiữ.
- SQLUser2=tài vũ/09032657212/AddressNULL,Email/Role2/active giữ;9/9snapshotgốc khớp (Userskhôngcredentials),40SP/40ảnh/kho740/Order1Cancelled/Detail1/Adminnguyên.
- Web19388/Admin10736/hash60+101 nguyên; khôngsourcefix/build/testmới, chỉdocs/helper/evidence restore-C2-* trongartifactD.1C; giữattempt, khôngrerun.
- Đềxuấtđóng S29.8D.1, chuyển S29.8D.2 CRUDAdmin khi cóchỉđạo; chưa tựthửghiCRUDmain hoặc thêmfixtureSQL.
- Sau đó D.2 nghiệm thu/hoàn thiện CRUD quản trị, D.3 reports/auth-session, D.4 tổng nghiệm thu; mở rộng sau, UI/UX cuối.
- Không coi phê duyệt C.7 một giao dịch hoặc D.1 source/tests là quyền tạo thêm tài khoản/đơn hay triển khai/ghi main.

## 9. Phê duyệt và giới hạn lâu dài
- Đã duyệt main migration/Admin/import,deployC.6B/D.1B,1Customer/1OrderC.7; D.1C.2 khôiphục3fieldUser2quaUI đãPASS. Không suy quyền ghi giao dịch/CRUD tiếp theo.
- Không tự tạo Admin thứ hai, đổi role/password, thêm demo, chỉnh tồn thủ công hoặc xóa bằng chứng giao dịch.
- Không ghi main, migrate/seed/import/restore/drop/truncate/DELETE, thay binary/restart hoặc cleanup dữ liệu khi chưa được duyệt đúng thao tác.
- Không commit/push/reset/clean/history rewrite; không bật full access/bỏ approvals; bảo vệ secrets và thay đổi người dùng.
- Không tự kết nối server cũ, không tạo/drop DB test mới hoặc chạy lại harness có ghi khi chưa thuộc phạm vi được duyệt.
- Kết thúc mỗi nhiệm vụ: cập nhật trước/sau, file thay đổi, lệnh/kết quả thực tế, lỗi còn lại/bước tiếp; không ghi log dài hoặc mật khẩu/hash/cookie/token.
