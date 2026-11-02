# PhoneStore — Tiến độ và điểm tiếp tục

Cập nhật: 2026-11-02 (ngày client, America/Los_Angeles; giữ ngày lịch sử đã ghi). Đọc đầu mỗi phiên; đây không phải trạng thái live.
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
## 4. Database — bằng chứng kiểm kê 2026-11-02
- SELECT chỉ đọc ngoài sandbox xác nhận MSSQLSERVER Running, SQL 16.0.1000.6, PhoneStoreDB ONLINE.
- Roles = 2 (Admin/Customer); Users, Brands, Categories, Products, ProductImages, Orders, OrderDetails = 0.
- 7 khóa ngoại và 6 check constraint hiện có đều bật/trusted; chưa đối chiếu schema live từng cột/index.
- History có `20261001150857_InitialCreate` / EF 8.0.28; model khớp snapshot lúc kiểm kê.
- TLS/Windows credentials từng lỗi trong sandbox; cùng SELECT ngoài sandbox thành công.
- Server cũ trong CONTEXT/docs/Git: `taiiiii\SERVER`, PhoneStoreDB, Windows Authentication.
## 5. S29.7A — điều tra catalog/ảnh trống (2026-11-02)
- VERIFIED Web PID4692/session SQL dùng PhoneStoreDB; Home/Products HTTP200 không link; Products/Images/Brands/Categories=0.
- Quét vị trí local thông dụng không thấy backup/export/ảnh gốc; Windows.old không có. Không chứng minh ai đã xóa dữ liệu cũ.
- Ưu tiên lấy DB backup/export + uploads từ máy/ổ/cloud cũ; mọi phục hồi/import phải được duyệt riêng.
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
## 19. S29.7E/F — import PASS; Admin UI PARTIAL (ngày client 2026-11-01)
- Người dùng chấp nhận40 ảnh AI demo, duyệt riêng DONG Y BACKUP S29.7F rồi DONG Y IMPORT S29.7F; không cho phép chạy lại hoặc ghi ngoài catalog.
- Backup FULL COPY_ONLY/CHECKSUM/COMPRESSION PASS 2026-11-01 16:36:47 PST; backup_set_id2/typeD/copy_only1/checksums1.
- File: C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_S29_7F_PREIMPORT_20261102_001906872Z_47B0A15271A6437A8D5FB05E41E61A38.bak
- VERIFYONLY WITH CHECKSUM PASS sau backup và chạy lại trước import; HEADERONLY đúng DB/server; file538112 byte, compressed536228/logical4014080 byte.
- 40/40 JPEG đúng tên/mapping checklist/definition, decode/render/≤10MiB và HTTP200; không thiếu/thừa. HUONG_DAN.txt chỉ warning Unsupported, không sửa.
- Đã chạy ĐÚNG MỘT LẦN: pin env ConnectionStrings__PhoneStoreConnection về localhost/PhoneStoreDB rồi dotnet run --project tools/PhoneStore.CatalogImport -- --execute; exit0/ImportComplete, không retry.
- Import tạo mới8 Brands/1 Category/40 Products/40 ProductImages, reused/updated0; chỉ ghi bốn bảng catalog; không migration/seed khác/restore/tài khoản.
- SELECT hậu kiểm PASS40/40 đối chiếu tên/brand/category/giá/tồn/active/URL với source; ProductId1–40, mỗi SP đúng1 ảnh primary=true/DisplayOrder0, tổng tồn740.
- Main DB ONLINE; Users1/UserId1/admin@admin.com/Quản trị viên PhoneStore/RoleId1/active giữ nguyên; RolesAdmin1/Customer2, Orders/OrderDetails0, đúng2 migrations, unique index/check enabled/trusted.
- HTTP hậu kiểm PASS: Home200/6 cards, Products200/40 cards,40/40 chi tiết HTTP200 và tên/ảnh/giá/max quantity khớp,40/40 ảnh HTTP200 image/jpeg; không POST/cart/checkout.
- Rủi ro: importer có thể ghi đè khi chạy lại/thiếu unique business key; không tự retry/restore. Backup cùng ổC/chưa thử restore, ảnh cần sao lưu riêng; formAdmin có rủi ro tồn kho cũ (mục7).
## 20. S29.8A — audit chức năng (2026-11-01), CHỜ DUYỆT SỬA
- Ưu tiên chính thức: hoàn thiện chức năng, hoãn UI/UX/bố cục/màu/responsive trừ lỗi cản sử dụng. Báo cáo docs/S29_8A_FUNCTIONAL_AUDIT.md chứa bảng Web/Admin/Backend, bằng chứng và kế hoạch.
- PASS runtime Web search/filter/sort3 kiểu; Home6/cards40, emptycart200, login/register200, anonymous checkout/orders302, invalid detail404. Profile/account404 + không action/view: MISSING.
- WinForms PID8116 đã login theo người dùng; điều hướng7 màn hình chỉ đọc PASS: Products40/Brands8/Category1/Customer0/Orders0/Dashboard740/Report740-revenue0; tìm iPhone6 rồi trả filter cũ.
- dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-restore --verbosity minimal:72/72 PASS,0 fail/skip, build dependencies thành công; SQLite cách ly, không solution build/UI write test.
- SELECT ONLINE:40 Products/40 Images/8 Brands/1 Category/tồn740/Users1Admin active/Orders0; history2,7 FK+7 check enabled/trusted; uniqueAdmin enabled. Không ghi DB/ảnh/source.
- P1-01: luồng Admin Save gán stock từ form cũ không đối chiếu; đường lost update xác định theo source (10→checkout9→Save10), chưa tái hiện SQL/live. Thiếu SQL concurrency/idempotency tests là rủi ro, không tự coi runtime FAIL.
- PARTIAL: auth HTTP/logout, cart mutation, checkout/order UI, catalog/ảnh/customer CRUD, report có đơn; tổng/trend/average chỉ Completed, bestseller SalesValue là metric khác.
- Đề xuất S29.8B bảo vệ stale stock bằng update có điều kiện/giá trị gốc + regression, không migration; tiếp Customer profile, rồi SQL FunctionalTest cách ly cần duyệt riêng, cuối E2E. Không sửa chức năng khi chưa được duyệt.
- Chỉ tạo báo cáo audit/cập nhật PROGRESS, không commit/push/migrate/import/restore/DB test. Dừng chờ duyệt bước sửa đầu tiên.
## 21. S29.8B — stale stock code/tests PASS (2026-11-02)
- Đã duyệt source/test, không DB chính/schema. Root cause: Save gán stock cũ từ form; transaction chỉ bắt đầu khi Save, không bảo vệ từ lúc mở form.
- ProductEdit thêm OriginalStockQuantity nullable; store GetEdit lấy snapshot, dialog giữ readonly snapshot và gửi khi Save. Edit thiếu snapshot từ chối; create vẫn hỗ trợ như cũ.
- Store dùng ExecuteUpdate tất cả trường cùng statement WHERE ProductId AND StockQuantity=original; affected0 từ chối toàn bộ edit, thông báo đóng/mở lại; không read-check-write. Transaction rollback trước commit; clear tracking.
- File sửa: AdminCatalogModels.cs, ProductManagementStore.cs, ProductManagementService.cs, ProductEditDialog.cs; thêm ProductStockConcurrencyTests.cs; cập nhật PROGRESS. Không sửa checkout/migrations/ảnh/seed/tài khoản.
- 11 regression SQLite PASS: save hợp lệ, checkout trước save (đổi/không đổi stock), hai form/hai context, thiếu/sai snapshot, create, deleted không recreate, SQL WHERE stock, rollback lỗi trước commit, checkout sau save giá/tồn mới.
- restore PhoneStore.sln PASS; build PhoneStore.sln --no-restore --no-incremental -p:OutputPath=bin/S29.8B/ PASS0warning/error; test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8B/ --verbosity minimal PASS83/83,0fail/skip; git diff --check PASS.
- Lần test đầu dùng binary incremental cũ thiếu DTO; build --no-incremental đã giải quyết. Không truy vấn/ghi DB chính trong task; unit tests chỉ SQLite cách ly.
- PARTIAL SQL simultaneous locking/UI conflict thực tế; chỉ so snapshot stock, chưa version mọi trường hoặc phát hiện ABA(stock đổi rồi về giá trị cũ). Không migration, không tự coi tương đương rowversion.
- App live vẫn binary cũ; output riêng tránh khóa file/gián đoạn. Bước tiếp duyệt xác minh SQL DB test cách ly và rebuild/restart Admin để nạp code mới; không chạy bản output riêng với image path tương đối chưa kiểm.
