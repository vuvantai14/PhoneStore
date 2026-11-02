# PhoneStore — Tiến độ và điểm tiếp tục
Cập nhật: 2026-11-02, D.4C.3 Admin tổng hợp CONDITIONAL READY; core/runtime PASS, known limits+backup/config guard chờ duyệt deploy riêng. Đây là snapshot.
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

## 3. Runtime và SQL gần nhất — S29.8D.2B
- SELECT 2026-11-02 03:55:44 UTC / 10:55:44 Bangkok: localhost/DESKTOP-ES9362P, PhoneStoreDB ONLINE.
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
| Admin login/phân quyền | D.3 PASS WinForms test: logout/xóa session, sai mật khẩu, Admin login lại; Customer forbidden PASS automated/NOT TESTED UI |
| Dashboard | D.3 PASS WinForms+SQL test:4activeSP/9kho/4Customer/10Orders/1Completed/1trdoanhthu |
| Product/Brand/Category/ảnh | D2A/B SQL/testPASS; D2C coreCRUD WinForms PASS: lookup add/edit/search/deactivate/reactivate, Product CRUD/softdelete/filter, image add/6reject/unlink/primary; ca phụ còn PARTIAL, chưa deploy |
| Quản lý Customer Admin | PARTIAL: đọc/bảo vệAdmin có evidence, sửa/khóaGUI chưa nghiệmthu; ngoài D.2A |
| Stale stock | VERIFIED SQL/dialog thật B.1 + regression; đã triển khai C.6B, không còn chờ deploy |
| Admin quản lý đơn | VERIFIED UI cách ly C.5 toàn workflow; main C.7A Confirm thật và đọc Cancelled |
| Reports | D.4B fixed Bangkok timezone; D.4C.1 Admin test UI PASS range01–02/same-day02/empty01, chưa deploy main |
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
- Gần nhất D.4B buildsolution0warnings/errors,161/161 PASS,0skip; SQL report/auth read-only + gateway fake PASS; outputbin/S29.8D.4B-final/.
- D.1C.1 chạy lại22/22profile PASS (SQLite/HTTP thật cách ly), output bin/S29.8D.1C.1-tests; khôngfullsuite/deploy/mainPOST.
- LệnhD2B: `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8D.2B/`; `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8D.2B/ --verbosity minimal`.
- Suite SQLite: Authentication, Bootstrap, SingleAdminPolicy/Constraint, Checkout, OrderWorkflow, ProductStockConcurrency, CommerceWorkflow, CartController.
- SQL/HTTP/UI harness riêng opt-in trong tests/PhoneStore.SqlIntegration; không chạy audit read-only để tránh ghi fixture.
- Có18testsCRUD+8safety/WIC/SQLshared/concurrentprimary; WebP policyreject, GUI/report/CustomerCRUD/logout/stresschưađủ; orphan giữ cóchủđích,khôngautodelete.
- Original-stock guard không phải rowversion mọi field/ABA; cart theo browser session; legacyCreateAsync không token nhưng Web dùng Idempotent.
- Revenue tổng/trend/average chỉ Completed; BestSellers/SalesValue dùng Confirmed/Shipping/Completed, không cùng chỉ số doanh thu.
- [D.4C.3](S29_8D_4C_3_ADMIN_ACCEPTANCE_SUMMARY.md): Admin CONDITIONAL READY. Core CRUD/Dashboard/Reports/Auth/Orders + revoke IsActive/RoleId runtime PASS; partial DisplayOrder/close-during-save/Brand text/Customer-write và race check→commit. Deploy cần backup+main-config staging riêng.
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
- [D.1C](S29_8D_1C_PROFILE_ACCEPTANCE.md) helperclick timeout→dừng/SELECTgốc; [C.1 diagnosis](S29_8D_1C_1_PROFILE_DIAGNOSIS.md) user xácnhận đã tựLưu, SQL/greetingtestvalues; rootcausehelpercũ UNKNOWN.
- [D.1C.2 restore](S29_8D_1C_2_PROFILE_RESTORE.md) duyệtriêng UI1click/POST302→GET200/reload, Customer2=tài vũ/09032657212/AddressNULL,session/greeting/invariantsPASS;đềxuấtđóngD.1.
- EvidenceD.1C `src/PhoneStore.Web/obj/S29.8D.1C-20261102_031553601Z_0f70f82b55224b31a97cc4420a0835cf`; giữmarkers,khôngrerunhelperghi.
- [D.2A audit](S29_8D_2A_ADMIN_CRUD_AUDIT.md) PARTIAL toànphạmvi; source/SQL/testPASS, chưa fullWinFormsCRUD. Đã sửaLookupManagementService catchDbExceptionchung; thêmAdminCrudTests/AdminCrudChecks/modeProgram+harnessREADME/docs.
- TestDBđượcduyệtghi+dọn:trướckhôngsession/processkhác,guardDB_NAMEtừngEFcommand+cooperativelock. FixtureD2Ariêng1Brand/1Category/3Products/1OrderCancelled/1Detail,khôngtạo/sửaUsers.
- ChạySQL1lầnPASS43assertions:validation/duplicate/lookups/softdeactivate/referenceFK/stockpredicate/ảnhJPEG-PNG-primary-order-remove/rollback/filecleanup/gatewaydeny.
- Cleanupchỉprefix/IDmới+filefixture,giữfixturecũ; main/test9/9snapshotsnguyên (Userskhôngcredentials),identitytestcóthểtăng,khôngreseed/drop/migrate.
- HậuSELECTD2A03:32:01UTC mainONLINE40/40/tồn740/Users2/Order1Cancelled/Detail1/Customernguyên/Adminnguyên;Web60+101preservedhashPASS,khôngdeploy.
- Evidence `tests/PhoneStore.SqlIntegration/obj/S29.8D.2A-20261102_033101218Z`;giữmarker/log/snapshots,khôngtựrerunoldmodes.
- Giới hạn ảnh signature-only của D.2A là lịch sử; đã được D.2B thay thế bên dưới, GUI vẫn chưa nghiệm thu đầy đủ.
- DỪNGchờchỉđạo,khôngtriểnkhaiAdmin/ghimain. Softdeactivate là luồngxóaUI hiệncó;ProductNamekhôngunique,lookupinactivekhôngcascadeẩnProduct,cầnchốt nếumuốnđổi.
- [D.2B report](S29_8D_2B_IMAGE_VALIDATION.md): JPEG/PNG≤10MiB/4096mỗichiều/16Mpixels/1frame,decodeWICmọipixel;CRC/IEND/EOI/APNGgate;WebP mớitừchốirõ,ảnhcũgiữ. FrameworkWPFđãcài,khôngNuGetcodec/migration.
- Storagebắtbuộcvalidator,snapshotbấtbiến,GUID/CreateNew/atomicmove,chỉdọn.tmpđãtạo. Servicegiữfilepublish sauunlink/DBfailure/unknowncommit+noticeURL;dọnorphanphảikiểmtra/duyệtriêng.
- SQLlầnđầuFAILdeadlock PK_ProductImages RangeS-S→X (system_healtheventfile),đãSELECToutcome/dọnđúngfixturemới;thêmsp_getapplockExclusive/Transactiontrướcmọilockảnh,khôngretry. FixturemớisausửaPASS,cảbảncuốiCRC.
- SQLtestdùnglạiStockConcurrencyTest,fixture2Products/1Brand/1Category/images,khôngUsers/Orders. Sharedreplace+firstuploadđồngthời/primary/rollback/orphannoticePASS;cleanupchỉfixture,main/test9snapshotsnguyên.
- Evidence `tests/PhoneStore.SqlIntegration/obj/S29.8D.2B-20261102_033917998Z`;logs/markers/snapshots/browserJPEG-PNG-WebPdecode. BrowserofflinePASS,khônguploadmain.
- SourceD2B: validatorinterface+AdminWIC,LocalStorage/ProductService/DataStore,AdminDI/dialogfilter/frameworkref;testsSafety/SQLImageChecks/modes/README,D2Acasesupdatedpolicy,docs. Chưa deployWeb/Admin.
- HậuD2Bmain40/40/tồn740/1Cancelled/Detail1/Customer-Adminnguyên;Web19388/Admin10736responsive,Web60+preserved101hashgiữ(40ảnhdemo). Khôngcommit/push.
- D2Bsource/testsPASSkhôngthayUIacceptance. Rủiro:orphan/dunglượng,nativecodec/ACL,globalimagelockgiảmthroughput;khôngtựdọnmain.
- [D.2C UI](S29_8D_2C_ADMIN_WINFORMS_ACCEPTANCE.md) PARTIAL: appcopy/config/envcáchly,PID13368đãlogintestD2C1;DBStockConcurrencyTest/ảnhriêng,main/test9snapshotspublic+40ảnhhashnguyên,chưaCRUDghi.
- Artifact `src/PhoneStore.Admin/obj/S29.8D.2C-20261102_035250536Z`; D2C1duyệtriêngPasswordHashtestUser1/admin@s29-8b1.test,guardSQLthật+Serializable+WHEREchặt,1rowcommit/SELECTbooleanPASS03:58:54UTC.
- Usertựnhậppassẩnx2terminal,tựlogin vàxácnhận;UI13368shellđúngtestAdmin/SQLtestDBPASS03:59:38UTC;khôngsecretslog/file/chat. Main/test9snapshotnoncredentials và40ảnhnguyên.
- D2C1utility build0warning/error,giữattempt/passwordtest,khôngresetlặp. D2C3 testPID5944 appcopy59hashPASS;userlogin/shellSQLTestAdmin/SQLsessiontestDBPASS(openTx0);Dashboard3SP/8kho/10đơn,Products3/Brands1/Categories1 tảiPASS. Config/env/DI pinảnhriêng,uploads0file(chưaupload). UI tạo/sửa/tìm Brand6+Category6 S29D2C;Categoryrequired/duplicate báo rõ;Brandduplicate không thêmrow nhưng chưađọcđượctext. 13:42 UIA đồngthời thấy ProductEditDialog+LookupEditDialog cũ: dừng trướcSaveProduct,chờuserHủy/đóngmodal;0Product/0Image mới. Main9snapshot+40ảnhnguyên;test7bảngnguyên,Brands/Categories chỉthêm2fixtureID6active,giữđểtiếp,chưacleanup. Evidence *-D2C3-before/comparison.json+D2C3-fixture-checkpoint.json. Khôngbuild/testlại/deploy/mainDML.
- D.2C.2 PARTIAL 13:35Bangkok: PID13368/10736/19388 không còn, không có PhoneStore process hoặc listener5015; dừng theo điều kiện precheck, không tự restart/CRUD. Config test vẫn đúng DB/ảnh riêng0file. SELECT main40/40/tồn740/Users2/Order1Cancelled/Detail1; test3Products/0Images/tồn8/Users5/10Orders;40ảnhhash nguyên. Chỉ docs, không build/test lại; chờ mở lại Admin test/login để tiếp. D.3 reports/auth-session, D.4 tổng nghiệm thu sau D.2.
- D2C coreCRUD/D.3 Auth PASS; D.4B chỉ deploy test, main chưa deploy. C.2C.2 cuối: User1 Admin-active/login, test5Users/2Brand/2Cat/5Product/4Image/10Order/10Detail stock14; main9bảng2/2/8/1/40/40/1/1/2 stock740/1Cancelled/1Admin+40hash nguyên.

## 9. Phê duyệt và giới hạn lâu dài
- Đã duyệt main migration/Admin/import,deployC.6B/D.1B,1Customer/1OrderC.7; D.1C.2 khôiphục3fieldUser2quaUI đãPASS. Không suy quyền ghi giao dịch/CRUD tiếp theo.
- Không tự tạo Admin thứ hai, đổi role/password, thêm demo, chỉnh tồn thủ công hoặc xóa bằng chứng giao dịch.
- Không ghi main, migrate/seed/import/restore/drop/truncate/DELETE, thay binary/restart hoặc cleanup dữ liệu khi chưa được duyệt đúng thao tác.
- Không commit/push/reset/clean/history rewrite; không bật full access/bỏ approvals; bảo vệ secrets và thay đổi người dùng.
- Không tự kết nối server cũ, không tạo/drop DB test mới hoặc chạy lại harness có ghi khi chưa thuộc phạm vi được duyệt.
- Kết thúc mỗi nhiệm vụ: cập nhật trước/sau, file thay đổi, lệnh/kết quả thực tế, lỗi còn lại/bước tiếp; không ghi log dài hoặc mật khẩu/hash/cookie/token.
