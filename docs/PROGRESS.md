# PhoneStore — Tiến độ và điểm tiếp tục
Cập nhật: 2026-11-02 (ngày client, Asia/Bangkok; giữ ngày/múi giờ lịch sử đã ghi). Đọc đầu mỗi phiên; đây không phải trạng thái live.
Ưu tiên mã nguồn và kiểm thử thực tế nếu có mâu thuẫn. Chỉ kiểm tra lại phần liên quan nhiệm vụ.
## 1. Tổng quan và môi trường
- Root C:\PhoneStore\PhoneStore, PhoneStore.sln; .NET 8/SDK 8.0.427, EF/tooling 8.0.28, Web MVC/Admin WinForms.
- Kiến trúc chính: Web/Admin → Business → Data → Models; Web cũng đăng ký Data services.
- Solution: Models, Data, Business, Web, Admin, Tests và AdminBootstrap.
- SQL Server 2022 Developer, instance mặc định MSSQLSERVER; server hiện tại `localhost`.
- Database: `PhoneStoreDB`; Windows Authentication; key `ConnectionStrings:PhoneStoreConnection`.
- Web appsettings mặc định/Development và Admin appsettings cùng trỏ localhost.
- PhoneStoreDbContextFactory đọc Web config; biến ConnectionStrings__PhoneStoreConnection có thể ghi đè.
## 2. Cách hiểu trạng thái
- VERIFIED: có bằng chứng theo phạm vi; PARTIAL: có mã/bằng chứng một phần, chưa đủ runtime/tích hợp.
- NOT IMPLEMENTED: chưa thấy triển khai trong phạm vi đã kiểm kê; UNKNOWN: thiếu bằng chứng/dữ liệu.
## 3. Trạng thái chức năng
| Hạng mục | Trạng thái và bằng chứng |
|---|---|
| Customer đăng ký/đăng nhập | VERIFIED ở service/test SQLite: AuthService, AuthenticationTests |
| Phân quyền và ownership đơn | VERIFIED ở mã/service test: OrdersController, PurchaseStore, CheckoutTests |
| Catalog/search/filter/detail | VERIFIED SQL/HTTP40 sản phẩm ở S29.7F/S29.8A; related source, chưa test riêng |
| Giỏ hàng | PARTIAL: SessionCart, CartPresenter, CartController; chưa kiểm thử HTTP/session đầy đủ |
| Checkout/giá snapshot/trừ kho | VERIFIED ở service SQLite: OrderService, PurchaseStore, CheckoutTests |
| Lịch sử/chi tiết đơn | VERIFIED ở truy vấn/service test; giao diện UNKNOWN |
| Admin login | VERIFIED service/tests, người dùng xác nhận login/live data ở S29.8A; logout UI chưa thử |
| Sản phẩm/ảnh/thương hiệu/danh mục/khách hàng | PARTIAL: Management services/stores và WinForms controls/dialogs |
| Chuyển trạng thái/hủy/hoàn kho | VERIFIED ở workflow test SQLite; SQL Server concurrency UNKNOWN |
| Dashboard/Reports | PARTIAL: ReportingStore/Service và controls; chưa có test báo cáo trong suite |
| Customer Account W09 | NOT IMPLEMENTED theo kiểm kê: tài liệu liệt kê nhưng chưa thấy action/view |
| UI Web/WinForms và SQL integration đầy đủ | PARTIAL: S29.4 HTTP smoke + LoginForm; nghiệp vụ UI chưa thử |
| AdminBootstrap | VERIFIED tạo Admin SQL S29.6; đăng nhập UI thủ công vẫn chờ xác nhận |
## 4. Database/khôi phục — lịch sử kiểm kê
- Lúc kiểm kê localhostSQL2022 ONLINE,Roles2/cácbảngkhác0,InitialCreate; counts lịch sử không thay trạng thái live ở mục29. TLSsandbox từng lỗi nhưng SELECT ngoài sandboxPASS, không đổiTLS.
- Servercũ taiiiii\SERVER, khôngtựkếtnối. S29.7A xácminhDB mới saucàilại,Web dùngDB mới/catalogn0;khôngdo filter.
## 5. Nguồn catalog/ảnh cũ — lịch sử S29.7A
- Chưa tìm thấy backup/export/ảnh gốc trongworkspace/Git/vịtrílocalđãkiểm;Windows.oldkhôngcó. Khôngchứngminhngườinàoxóa;clonekhôngkhôiphụcdataruntime.
- CatalogImport40definitionsdemo;40ảnhAIvềsauđượcngườidùngchấpnhậndemo/importởS29.7F. Khôngtựrerun/import/restore hoặc kếtnốiservercũ.
## 6. Mốc và kiểm thử
- Kiểm kê: 26/26 SQLite; S29: 51/51; S29.3/S29.4: 72/72 PASS, build 0 warning/error.
- S29.1 dry-run SQL PASS, không ghi; S29.5B apply chính PASS; S29.6 tạo Admin PASS, chi tiết bên dưới.
- S29.4 SQL isolation và UI smoke là bằng chứng riêng; chưa có xác nhận thủ công login và nghiệp vụ UI.
## 7. Rủi ro và giới hạn
- Admin tạo S29.6; người dùng xác nhận login và UI đọc kiểm S29.8A; thao tác ghi UI chưa kiểm.
- P1 stale stock đã sửa source/tests ở S29.8B (mục21); chưa xác minh SQL concurrency/UI bản mới, apps đang chạy vẫn binary cũ.
- Chưa kiểm chứng SQL Server locking/concurrency, retry/commit outcome hoặc end-to-end Web–Admin.
- Doanh thu tổng/trend/average chỉ Completed; BestSellers/SalesValue gồm Confirmed/Shipping/Completed.
- Chống checkout trùng dùng memory cache trong tiến trình, chưa kiểm chứng nhiều instance/restart.
- CONTEXT/S17 là bằng chứng lịch sử; có kết luận sẵn sàng S18 mâu thuẫn báo cáo S17.3 blocked.
- Chưa thấy Word/Excel UC01–UC12/SCR01–SCR12 hoặc PROGRESS cũ; không tự suy diễn yêu cầu từ đó.
- Mất hội thoại Codex không thể phục hồi bằng Git; dữ liệu/ảnh chưa commit không có trong clone.
## 8. Git và file liên quan
- HEAD lúc kiểm kê: 9587df1/main; luôn kiểm tra git status/diff theo nhiệm vụ, không tự commit/push.
## 9. Hiện tại, phê duyệt và bước tiếp theo
- S29.5B apply chính PASS; S29.6 được phê duyệt riêng và tạo Admin PASS, Users/Admin=1/1.
- Email đã chọn admin@admin.com, tên Quản trị viên PhoneStore, role Admin ID 1; chi tiết tại mục 15.
## 10. Kết thúc mỗi nhiệm vụ
- Ghi ngày, trạng thái trước → sau và phạm vi bằng chứng; cập nhật mục liên quan thay vì nối log dài.
- Ghi file thay đổi, lệnh test/kết quả thật; nếu không chạy thì ghi rõ, không tái sử dụng PASS như kết quả mới.
- Ghi lỗi/rủi ro còn lại, công việc dở, bước tiếp theo và giới hạn phê duyệt.
- Giữ khoảng 150 dòng trở xuống, không sao chép hội thoại, mật khẩu, hash hoặc log dài.
- Không commit/push/reset/clean/history rewrite, xóa dữ liệu, restore/drop/seed/import/migrate
  hoặc thay đổi database thật nếu chưa được cho phép; không bật full access/bỏ toàn bộ approvals.
- Hướng dẫn agent không thay thế sandbox hoặc database firewall; không tuyên bố mọi lệnh nguy hiểm bị chặn.
## 11. Một Admin duy nhất — S29.3 (2026-11-02)
- Chính thức: tối đa 1 Admin, kể cả inactive; trước bootstrap có thể 0; Customer có nhiều tài khoản.
- Store CreateCustomerAsync không nhận User/RoleId, tự tra Customer role, chặn pending changes; AuthService caller duy nhất.
- Bootstrap riêng, kiểm tra AdminId=1; WinForms không tạo Admin, quản lý Customer không đổi role.
- Mapping Admin=1 đã có trong InitialCreate/seed; SystemRoles ghi quy ước này.
- Migration 20261102122856_EnforceSingleAdmin thêm UX_Users_SingleAdmin WHERE RoleId=1 và CK_Roles_AdminIdentity.
- Giữ IX_Users_RoleId; preflight THROW nếu mapping sai/thiếu hoặc >1 Admin, không tự sửa/xóa dữ liệu.
- Test SQLite + script offline PASS; restore PASS, build 0 warning/error, unit tests 72/72 PASS.
- File S29.3: AuthService/AuthUserStore, Role/UserConfiguration, SystemRoles, BootstrapService,
  migration/Designer/snapshot, SingleAdminConstraintTests. Ràng buộc đã apply ở S29.5B (mục 14).
## 12. S29.4 — SQL Server cách ly và UI smoke (2026-11-02)
- DB_ID test=0 trước tạo; PhoneStoreDB_MigrationTest nhận InitialCreate+EnforceSingleAdmin thành công.
- Role Admin=1, unique filtered index và check identity enabled/trusted; Users test cuối=0 sau ROLLBACK.
- tests/sql/S29_4_EnforceSingleAdmin.sql: guard DB test, chặn Admin thứ hai active/inactive, cho 2 Customer, chặn đổi role.
- sqlcmd lỗi SET QUOTED_IDENTIFIER lúc đầu; bổ sung SET session rồi PASS. Không ghi business rows ở DB chính.
- Build 0 warning/error, unit tests 72/72 PASS; Home/Products/Login HTTP200 ở localhost:5015.
- WinForms LoginForm hiển thị ở S29.4; chưa kiểm thử đăng nhập; cảnh báo Web HTTP thiếu HTTPS redirect port.
- Hai ứng dụng giữ DB chính; migration chính và Admin được thực hiện riêng ở mục 14/15, không suy từ UI smoke.
## 13. S29.5 / S29.5A — backup (2026-11-02)
- Preflight chính ONLINE/FULL/MULTI_USER, InitialCreate, Roles=2, Users và sáu bảng nghiệp vụ khác=0.
- FULL BACKUP COPY_ONLY/CHECKSUM/COMPRESSION PASS; file mới timestamp UTC+GUID, không ghi đè.
- File: C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_FULL_COPYONLY_20261102_123908.738Z_131E14FF3BAC44C391332C4392CC2B72.bak
- backup_set_id=1; 2026-11-02 04:39:08 PST; file=523264 byte, compressed=521823, logical=3686400.
- VERIFYONLY WITH CHECKSUM PASS. Windows ACL chặn Get-Item; metadata đọc qua SQL, không đổi quyền.
- Chưa thử restore; file ở cùng ổ C với DB. VERIFYONLY không bảo đảm phục hồi thực tế.
## 14. S29.5B — apply chính PASS (2026-11-02)
- Phê duyệt có điều kiện, preflight/VERIFYONLY/header đúng target PASS; Data build/script đúng transaction/THROW.
- Đã apply đúng 20261102122856_EnforceSingleAdmin với --no-build và --connection localhost/PhoneStoreDB, exit0, không retry.
- SELECT history đúng hai migrations mỗi bản 1 lần; unique filter RoleId=1 enabled, check AdminIdentity enabled/trusted.
- Sau apply: ONLINE, Roles1Admin/2Customer, Users/Admin0, sáu bảng nghiệp vụ khác0; HTTP Home/Products/Login200.
- Không DML để test constraint/seed/import/restore/Down; unit tests S29.4 gần nhất72/72; Admin tạo riêng ở mục15.
## 15. S29.6 — tạo Admin PASS (2026-11-02)
- Được duyệt riêng; preflight localhost/DESKTOP-ES9362P, PhoneStoreDB ONLINE, EnforceSingleAdmin, Users/Admin=0/0.
- Chạy đúng một lần: dotnet run --project tools/PhoneStore.AdminBootstrap --no-build -- --name="Quản trị viên PhoneStore" --email=admin@admin.com --execute --confirm-server=localhost --confirm-database=PhoneStoreDB
- SELECT VERIFIED: Users/Admin=1/1; UserId1, admin@admin.com, Quản trị viên PhoneStore, RoleId1/Admin, IsActive=1.
- PhoneStoreDB ONLINE; Roles=1 Admin/2 Customer, history giữ InitialCreate+EnforceSingleAdmin; sáu bảng nghiệp vụ khác=0.
- Không đọc/hiển thị hash, không tạo Customer/catalog, không seed/import/migrate/restore/rollback, không retry/commit/push.
## 16. S29.7 — UI thủ công chờ người dùng (2026-11-02)
- Web Home/Products/Login HTTP200 localhost:5015; Admin PID21812 hiện cửa sổ/responding; không suy login PASS.
## 17. S29.7C — chuẩn bị ảnh (2026-11-02)
- Tạo đúng wwwroot/uploads/products và checklist PRODUCT_IMAGES.md; Admin upload dùng GUID, importer dùng slug; không ghi DB.
## 18. S29.7D — kiểm tra ảnh (2026-11-02)
- 40/40 JPEG decode800x800/≤10MiB/HTTP200, khớp tên, không trùng hash; kỹ thuật PASS, nội dung AI PARTIAL.
- Đã xem40 ảnh: crop/chữ méo ở một số file; chi tiết từng file trong PRODUCT_IMAGES.md; giữ HUONG_DAN.txt ghi nguồn AI.
## 19. S29.7E/F — catalog import PASS (2026-11-01)
- Người dùng chấp nhận40 ảnh AI demo; duyệt backup/import riêng. FULL COPY_ONLY/CHECKSUM/COMPRESSION PASS 16:36:47 PST/backup_set2.
- Backup: C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_S29_7F_PREIMPORT_20261102_001906872Z_47B0A15271A6437A8D5FB05E41E61A38.bak
- VERIFYONLY CHECKSUM/header đúng DB/server PASS; file538112 byte. Backup cùng ổC/chưa thử restore; ảnh cần sao lưu riêng.
- Import đúng1 lần exit0:40Products/40Images/8Brands/1Category,tồn740;40 primary/order0/name-brand-price-stock-image khớp. Không thayUsers/Admin/migrations.
- HTTP40/40 detail+ảnh200, Home6/cards40;ảnh runtime giữ nguyên. Importer có thể ghi đè khi chạy lại, không tự rerun/cleanup.
## 20. S29.8A — audit chức năng (2026-11-01), lịch sử
- Chi tiết docs/S29_8A_FUNCTIONAL_AUDIT.md: Web search/filter/sort PASS, account/profile MISSING, cart/auth/order E2E PARTIAL; ưu tiên chức năng trước UI/UX.
- WinForms authenticated7 màn hình đọc PASS:40 SP/8 brands/1 category/tồn740, tìm iPhone6; Customer/Orders0. SQL FK/check/index đúng; không DML.
- 72/72 tests PASS lúc audit. P1 stale stock theo source chuyển sang sửa S29.8B; thiếu SQL concurrency/idempotency và report có đơn vẫn PARTIAL.
## 21. S29.8B — source/regression PASS (2026-11-02)
- Sửa DTO OriginalStockQuantity, ProductManagementStore conditional ExecuteUpdate, ProductManagementService và ProductEditDialog giữ snapshot/hiển thị conflict; không migration/checkout/UI hình thức.
- UPDATE toàn bộ trường WHERE ProductId AND StockQuantity=original; affected0 từ chối mọi thay đổi, yêu cầu đóng/mở form. Thiếu snapshot từ chối; create giữ nguyên. Snapshot không thay version mọi trường/ABA.
- ProductStockConcurrencyTests thêm11 cases: valid save/checkout stale/hai form/missing snapshot/create/deleted/WHERE atomic/rollback/checkout sau edit. SQLite83/83 PASS; build0warning/error, restore PASS; output bin/S29.8B/.
- Main app chưa nạp code mới; SQL/UI verification tiếp ở mục22, deployment chờ mục23. Không ghi DB chính/source migrations/ảnh/commit/push.
## 22. S29.8B.1 — SQL/dialog PASS (2026-11-02 Asia/Bangkok)
- Được duyệt riêng tạo PhoneStoreDB_StockConcurrencyTest sau DB_ID NULL, apply2 migrations CHỈ DB mới; giữ DB, không drop/restore.
- Opt-in tests/PhoneStore.SqlIntegration guard connection/DB_NAME/server trướcEF, không main. Fixture lúc B.1:1Admin/1Customer/1SP,2Orders; password random memory.
- SQL checkout10→9/staleSave rejected, hai form, UPDATE hai connections affected[1,0], valid save/rollback PASS; dialog thật hiện conflict/không lưu. Main không đổi; tests83/83.
- Full shell login/navigation test chưa làm; Admin test6796 lúc B.1 ở login. C bổ sung fixture/kết quả ở mục24; không chạy lại mode B.1 trên fixture đã có.
## 23. S29.8B.2 — staging PASS, CHƯA TRIỂN KHAI (2026-11-02)
- B.2 build bin/S29.8B.2/net8.0-windows PASS0warning/error/tests83/83;4DLL khớp B.1 SQL-tested,config localhost/PhoneStoreDB và uploads đúng. Manifest trong staging.
- Chưa nhận xác nhận đóng main/thay binary; không deploy. S29.8C xác minh hash main vẫn khác B.2, chỉ còn Web8692 chạy; không tự mở Admin.
- Nếu triển khai: hủy form chưa lưu/đóng mềmPID đã xác minh, backup binary mới, copy đủ DLL giữconfig, hashcheck/restart sau duyệt; không force/SQL/ảnh. Staging B.2 không chứa chức năng C mới.
## 24. S29.8C — commerce source/tests PASS, CHƯA DEPLOY (2026-11-02 Asia/Bangkok)
- Báo cáo docs/S29_8C_COMMERCE_REPORT.md; audit+source+tests/SQL/HTTP cách ly PASS, không migration. Checkout tokenGUID→OrderCodeSHA256(user+token)26hex/indexunique + sp_getapplock/transaction/readback, không retry.
- Checkout/hủy khóa UPDLOCK,HOLDLOCK theoProductId trong Serializable; activeCustomer, cleartracking khi kết thúc; Customer cancel ownership/CSRF chỉ Pending/Confirmed, repeatCancelled không hoàn thêm; Admin workflow cũ giữ.
- Files nghiệp vụ: OrderService/interface/PurchaseStore; OrderManagementService/interface/store; OrderReceipt; Checkout/OrdersController/Program/OrderDetail view/Cart.Available. Thêm CartControllerTests/CommerceWorkflowTests/Web reference và SQL/HTTP harness.
- Build0warning/error/tests105/105 ở C; SQL doublecheckout1order, replaynewcontext, doublecancel hoàn1, laststock1 chỉ1thắng, checkout/cancel rollback PASS. HTTP auth/cart/CSRF/serverprice/ownership/history/cancel PASS trên helper5016.
- HTTP lượt đầu lỗi test đọcTempData2lần, dừng trướccheckout; SELECT0order rồi fixture khác, khôngDELETE/retrytransaction. Test cuối C:Products3/Users4/Orders7/Details7/stock9,0,0/history2; C.5 cập nhật tiếp.
- Main chỉSELECT40Products/tồn740/40Images/Users1Admin/Orders0/history2, Web8692 không dừng. C chưa deploy; cart thuộcbrowser session, legacyCreateAsync nội bộ khôngtoken/Web dùngIdempotent; UI Admin nghiệm thu mục25.
## 25. S29.8C.5 — nghiệm thu PASS (2026-11-02 Asia/Bangkok)
- docs/S29_8C_5_ACCEPTANCE_DEPLOYMENT.md: actual OrdersManagementControl/OrderDetailDialog SQLguard PASS list/search/filter/detail/confirm→shipping→completed/cancel/restock1lần; khôngfullshelllogin (testsession reflection).
- HTTP helper5016 PASS AdminConfirmed→Customerhistory/detail/cancelrepeat;2tests keyscopeuser/tokenrotation bổ sung,107/107PASS/build0warning/error/diffcheckPASS.
- TestDBretained3Productsstock8,0,0/Users5/Orders10(5Cancelled,1Completed,4Pending),khôngcleanup/drop/restore. Main40SP/tồn740/40ảnh/Users1Admin/Orders0/history2 khôngđổi.
- Staging hashverified: Web/bin/S29.8C.5-staging/net8.0, Admin/bin/S29.8C.5-staging/net8.0-windows,manifest mỗi bộ; configmain/ảnhresolveđúng.
- Binarybackup121files/33427203bytes/hashPASS: src/PhoneStore.Admin/obj/S29.8C.5-binary-backups/20261102_013925853Z_de37e8a4ab1c44039836ca8a78baeecf (Web,Admin,manifest); khôngclean trướcdeploy.
## 26–28. S29.8C.6/6A — shutdown lịch sử (2026-11-02)
- CtrlCkhôngshutdown Web8692/launcher3008;duyệtCtrlBreakriêng→đúng1event trênconsole198294chỉWeb/launcher/helper→logshutdown/exit/cổngtrốngPASS. Khôngforce/copy/restart trong6A.
## 29. S29.8C.6B — ĐÃ TRIỂN KHAI, SMOKE PASS (2026-11-02 Asia/Bangkok)
- Duyệt copy/restart riêng;preflightbackup121/staging121/sourcehashPASS,107/107testsbaseline. Copy118binaryWeb60/Admin58,giữ3appsettings;posthash118/118/config+uploadsnguyên.
- Copylệnhđầu dừngtrướcthayfile vìhelpernestedJSONarray;kiểmtừnghashmain/stagingnguyênrồisửaparsing/copy,khôngrollback/sourcechange.
- Web22736 Debug/net8.0 exe --urls http://localhost:5015/contentrootWeb/Development;Admin10736 Debug/net8.0-windows exe. Envpinmainlocalhost/PhoneStoreDB/WindowsAuth/uploadsabsolute;SQLsessionscả2main.
- SmokePASS Home6/Products40/Login/Cart200,Orders/checkoutanonymous302login,40detailnameprice-stock-image/40ảnhJPEG200. Usertựnhậppasswordlogin;UIchỉnavigation Dashboard40/tồn740/Products40/Brands8/Orders0 PASS.
- MainONLINE40SP/40Images/8Brands/1Category/Users1Adminactive/Roles2/Orders0/Details0/tồn740/history2giữnguyên;khôngDBwrite/migrate/seed/import/restore/commit/push.
- Logs obj/S29.8C.6B-20261102_015659699Z.{stdout,stderr}.log khôngruntimeerror;HTTPSmissingportwarningHTTPcũ. Docs/S29_8C_6B_DEPLOYMENT_RESULT.md +objcopyplan/processrecord chứa118hash/PIDs;backup/staginggiữ.
## 30–31. S29.8C.7/7A — GIAO DỊCH MAIN PASS (2026-11-02 Asia/Bangkok)
- Được duyệt1Customer tai123@gmail.com/tài vũ,1OrdervivoY17sProduct25qty1giá3490000. Preflight118binaryhash/Web22736/Admin10736/mainONLINE/emailchưacó/Orders0/stock38/total740PASS;khôngsource/config/binary/migration.
- FULLbackup COPY_ONLY/CHECKSUM/COMPRESSION+VERIFYONLYPASS trướcghi,set3/file566272byte,09:08:40BKK(02:08:40UTC).
- Backup: C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_S29_8C_7_PRETRANSACTION_20261102_020840231Z_83DE6E0E3458420896EA6963BCAB5F59.bak
- Usertựnhậppasswordregister/loginWeb;SQLUserId2/CustomerRole2active/Users2/Admin1giữ;khôngpassword/hash/token/cookielog. Browserriêngobj,cart1line/qty1/3490000,checkoutformđúng1lần.
- OrderId1/OrderDetailId1/Customer2/codeORD-05E65B3F0F705464636B525624/price3490000;OrderDate02:12:17.802441UTC. Pendingstock37/total739. UIAutomationGridGetItemnotsupported→dừngPARTIAL banđầu,chưaConfirm.
- 7A usertrực tiếpđốichiếudetail/bấmConfirm+hộpthoại;SQLConfirmedstock37/739 vàCustomerWebĐãxácnhậnPASS. Sauhướngdẫn/chờuserCancel1lần,nhậnxácnhậnWebĐãhủy;khôngscript/APIghi thayUI.
- Hậukiểm SQL/Web/Admin CancelledPASS:Order1/Details1/Users2/Product25stock38/total740/catalog40/40/8/1/AdminRole1active/history2. AdminsnapshotConfirmedcũđượcđóng/mởlạichỉđọc→Đãhủy/terminal/nocancelbutton.
- SELECTlặpchỉđọc09:20:45BKK vẫnstock38/740,khônghoànthêm. Khônggửi requesthủylặp theo7A;khôngtuyênbốđãtestwriterepeatmain. Stockdelta38→37→38 chínhxác1;40productsnamebrandpricestockactive khớpdefinitions,ảnhhashnguyên.
- EvidenceWeb/obj/S29.8C.7-{pending-receipt,confirmed-order,cancelled-order}.png;report docs/S29_8C_7_TRANSACTION_ACCEPTANCE_RESULT.md cậpnhậtPASS/giữlịchsửPARTIAL. GiữCustomer/order/detail,khôngDELETE/stockadjust/restore/seed/import/commit/push.
- Chỉdocs+evidenceruntimecậpnhật;107testsbaselinekhôngrerun. DỪNGchờchỉđạotiếp,khôngđơnthứ2/checkoutlại hoặcphạmvinghiệmthumớichưađượcduyệt.
