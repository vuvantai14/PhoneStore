# PhoneStore — Tiến độ và điểm tiếp tục

Cập nhật: 2026-11-02 (America/Los_Angeles). Đọc đầu mỗi phiên; đây không phải trạng thái live.
Ưu tiên mã nguồn và kiểm thử thực tế nếu có mâu thuẫn. Chỉ kiểm tra lại phần liên quan nhiệm vụ.

## 1. Tổng quan và môi trường
- Root: `C:\PhoneStore\PhoneStore`; solution: `PhoneStore.sln`.
- .NET 8; SDK theo `global.json`: 8.0.427; EF Core/SqlServer/Design/dotnet-ef: 8.0.28.
- Customer Web: ASP.NET Core MVC; Admin: WinForms `net8.0-windows`.
- Kiến trúc chính: Web/Admin → Business → Data → Models; Web cũng đăng ký Data services.
- Solution: Models, Data, Business, Web, Admin, Tests và AdminBootstrap.
- `tools/PhoneStore.CatalogImport` là công cụ riêng, không nằm trong solution.
- SQL Server 2022 Developer, instance mặc định MSSQLSERVER; server hiện tại `localhost`.
- Database: `PhoneStoreDB`; Windows Authentication; key `ConnectionStrings:PhoneStoreConnection`.
- Web appsettings mặc định/Development và Admin appsettings cùng trỏ localhost.
- Biến `ConnectionStrings__PhoneStoreConnection` có thể ghi đè cấu hình.
- Factory: `src/PhoneStore.Data/Context/PhoneStoreDbContextFactory.cs` đọc cấu hình Web.
- EF tooling dùng Data làm cả project và startup; Web startup hiện không có Design reference trực tiếp.

## 2. Cách hiểu trạng thái
- VERIFIED: đã có bằng chứng trực tiếp, chỉ trong phạm vi ghi kèm.
- PARTIAL: có mã/bằng chứng một phần; chưa xác minh đầy đủ runtime hoặc tích hợp.
- NOT IMPLEMENTED: chưa thấy triển khai trong phạm vi đã kiểm kê.
- UNKNOWN: chưa kiểm chứng hoặc chưa có nguồn dữ liệu cần thiết.

## 3. Trạng thái chức năng
| Hạng mục | Trạng thái và bằng chứng |
|---|---|
| Customer đăng ký/đăng nhập | VERIFIED ở service/test SQLite: AuthService, AuthenticationTests |
| Phân quyền và ownership đơn | VERIFIED ở mã/service test: OrdersController, PurchaseStore, CheckoutTests |
| Catalog/search/filter/detail/related | PARTIAL: ProductsController, ProductCatalogReader, Views; chưa có catalog live |
| Giỏ hàng | PARTIAL: SessionCart, CartPresenter, CartController; chưa kiểm thử HTTP/session đầy đủ |
| Checkout/giá snapshot/trừ kho | VERIFIED ở service SQLite: OrderService, PurchaseStore, CheckoutTests |
| Lịch sử/chi tiết đơn | VERIFIED ở truy vấn/service test; giao diện UNKNOWN |
| Admin login | VERIFIED ở service SQLite; không có Admin trong DB tại lần kiểm kê |
| Sản phẩm/ảnh/thương hiệu/danh mục/khách hàng | PARTIAL: Management services/stores và WinForms controls/dialogs |
| Chuyển trạng thái/hủy/hoàn kho | VERIFIED ở workflow test SQLite; SQL Server concurrency UNKNOWN |
| Dashboard/Reports | PARTIAL: ReportingStore/Service và controls; chưa có test báo cáo trong suite |
| Customer Account W09 | NOT IMPLEMENTED theo kiểm kê: tài liệu liệt kê nhưng chưa thấy action/view |
| UI Web/WinForms và SQL integration đầy đủ | PARTIAL: S29.4 HTTP smoke + LoginForm; nghiệp vụ UI chưa thử |
| AdminBootstrap S29/S29.1 | PARTIAL toàn bộ: mã/test + dry-run SQL VERIFIED; execute và đăng nhập thật chưa kiểm thử |

## 4. Database — bằng chứng kiểm kê 2026-11-02
- SELECT chỉ đọc ngoài sandbox xác nhận MSSQLSERVER Running, SQL 16.0.1000.6, PhoneStoreDB ONLINE.
- Roles = 2 (Admin/Customer); Users, Brands, Categories, Products, ProductImages, Orders, OrderDetails = 0.
- 7 khóa ngoại và 6 check constraint hiện có đều bật/trusted; chưa đối chiếu schema live từng cột/index.
- History có `20261001150857_InitialCreate` / EF 8.0.28; model khớp snapshot lúc kiểm kê.
- Không cần chạy lại migration chỉ để kiểm tra kết nối. Counts trên là lịch sử, không phải bảo đảm hiện tại.
- TLS/Windows credentials từng lỗi trong sandbox; cùng SELECT ngoài sandbox thành công.
- Không kết luận SQL Server hỏng hoặc đổi TLS chỉ từ lỗi sandbox.
- Server cũ trong CONTEXT/docs/Git: `taiiiii\SERVER`, PhoneStoreDB, Windows Authentication.
- Không tự kết nối server cũ. Database hiện tại không có dữ liệu bán hàng cũ đã được xác minh.

## 5. Khôi phục dữ liệu và CatalogImport
- 2026-11-02: chưa thấy backup SQL/ảnh catalog trong phạm vi workspace, Git local,
  thư mục người dùng thông dụng, C:\Projects và các vị trí SQL đã kiểm tra.
- Không có Windows.old/Windows.old.000; không thấy backup PhoneStore trong msdb instance hiện tại.
- Backup directory không trả file qua SQL; C:\SQL2022 bị Windows chặn, SQL không trả danh sách: UNKNOWN.
- DATA có PhoneStoreDB.mdf và PhoneStoreDB_log.ldf đang hoạt động, không phải backup cũ.
- Không có uploads/products; ảnh runtime bị Git ignore. Chưa tìm thấy bản sao ảnh cũ.
- Chưa kiểm tra ổ rời không gắn, máy cũ hoặc cloud online-only. Người dùng xác nhận chưa có nguồn backup.
- CatalogImport có 40 định nghĩa demo, 8 brand, 1 category; không khôi phục Users/Orders/lịch sử/tồn thực tế.
- Công cụ cần ảnh có sẵn; execute có thể ghi đè giá/tồn/active/ảnh chính của sản phẩm trùng.
- Chưa chạy CatalogImport trong công việc khôi phục này; không tự seed/import.

## 6. Mốc đã có bằng chứng và kiểm thử
- Kiểm kê 2026-11-02: restore/build 0 warning/error, 26/26 test SQLite pass.
- S29: AdminBootstrap độc lập, dry-run mặc định; execute xác nhận server/database và nhập mật khẩu ẩn.
- S29 test: 51/51 PASS; S29.3: 72/72 PASS (SQLite cách ly), build 0 warning/error, restore PASS.
- S29.1 dry-run SQL PASS/exit 0 trên localhost/PhoneStoreDB; không dùng execute hoặc nhập mật khẩu.
- SELECT trước/sau S29.1: Roles (1 Admin, 2 Customer), Users/AdminUsers=0, bảng nghiệp vụ khác=0,
  history chỉ InitialCreate. Trong sandbox lỗi Windows credentials; ngoài sandbox chạy thành công.
- CLI help/từ chối thiếu xác nhận hoặc mật khẩu dòng lệnh đã kiểm tra; nhập mật khẩu tương tác chưa thử.
- S29.4: migration SQL Server test, unit tests và UI smoke được ghi riêng ở mục 12; không đồng nhất phạm vi.
## 7. Rủi ro và giới hạn
- P1: Users trống theo kiểm kê, chưa có Admin để sử dụng ứng dụng quản trị.
- P1: form Admin có thể lưu lại tồn kho cũ sau checkout; ProductManagementService gán StockQuantity
  từ DTO mà chưa thấy token đối chiếu lúc mở form. Đây là rủi ro đọc mã, chưa tái hiện thực nghiệm.
- Chưa kiểm chứng SQL Server locking/concurrency, retry/commit outcome hoặc end-to-end Web–Admin.
- Doanh thu tổng/trend/average chỉ Completed; BestSellers/SalesValue gồm Confirmed/Shipping/Completed.
- Chống checkout trùng dùng memory cache trong tiến trình, chưa kiểm chứng nhiều instance/restart.
- CONTEXT/S17 là bằng chứng lịch sử; có kết luận sẵn sàng S18 mâu thuẫn báo cáo S17.3 blocked.
- Chưa thấy Word/Excel UC01–UC12/SCR01–SCR12 hoặc PROGRESS cũ; không tự suy diễn yêu cầu từ đó.
- Mất hội thoại Codex không thể phục hồi bằng Git; dữ liệu/ảnh chưa commit không có trong clone.

## 8. Git và file liên quan
- Lúc kiểm kê: main tại 9587df1, bằng local origin/main; không fetch nên remote thực tế UNKNOWN.
- Thay đổi có sẵn: global.json và 3 appsettings (SDK/local SQL); .codex/config.toml, AGENTS.md untracked.
- S29 thay đổi: PhoneStore.sln, README.md; 4 file trong tools/PhoneStore.AdminBootstrap/;
  tests/PhoneStore.Tests/{AdminBootstrapTests.cs,PhoneStore.Tests.csproj,TestDatabase.cs}.

## 9. Hiện tại, phê duyệt và bước tiếp theo
- S29.2 tạo Admin đang chờ email/tên hiển thị và xác nhận lệnh execute cụ thể; chưa tạo tài khoản.
- Preflight S29.2 (2026-11-02): kết nối localhost đến DESKTOP-ES9362P, instance mặc định;
  PhoneStoreDB ONLINE, role Admin ID 1, Customer ID 2; Users/AdminUsers = 0, các bảng nghiệp vụ khác = 0.
- Đã phê duyệt: xây dựng S29, test cách ly, README; dùng workspace-write/on-request có sandbox.
- Đã phê duyệt/thực hiện S29.1: dry-run trên PhoneStoreDB và SELECT kiểm tra trước/sau; không ghi dữ liệu.
- S29.3 được duyệt sửa code/test và tạo migration trong mã; chưa duyệt apply hoặc ghi database thật.
- S29.4 được duyệt/tạo DB test mới và apply migration test; chưa duyệt apply vào PhoneStoreDB.
- Tạo Admin thật vẫn chờ email/họ tên và xác nhận lệnh cụ thể; luôn kiểm tra lại đích trước execute.

## 10. Kết thúc mỗi nhiệm vụ
- Ghi ngày, trạng thái trước → sau và phạm vi bằng chứng; cập nhật mục liên quan thay vì nối log dài.
- Ghi file thay đổi, lệnh test/kết quả thật; nếu không chạy thì ghi rõ, không tái sử dụng PASS như kết quả mới.
- Ghi lỗi/rủi ro còn lại, công việc dở, bước tiếp theo và giới hạn phê duyệt.
- Giữ khoảng 150 dòng trở xuống, không sao chép hội thoại, mật khẩu, hash hoặc log dài.
- Không commit/push/reset/clean/history rewrite, xóa dữ liệu, restore/drop/seed/import/migrate
  hoặc thay đổi database thật nếu chưa được cho phép; không bật full access/bỏ toàn bộ approvals.
- Hướng dẫn agent không thay thế sandbox hoặc database firewall; không tuyên bố mọi lệnh nguy hiểm bị chặn.

## 11. Quy tắc chính thức: một Admin duy nhất (2026-11-02)
- Chỉ một tài khoản Admin quản trị toàn hệ thống; Customer có thể nhiều tài khoản.
- Trước bootstrap được có 0 Admin; sau khởi tạo cần 1. Admin inactive vẫn chiếm vị trí duy nhất.
- VERIFIED mã/test: đăng ký chỉ lấy role Customer từ DB, DTO không nhận role; quản lý Customer không đổi role.
- WinForms không có luồng tạo Admin; AdminAuthService chỉ xác thực/rehash, không tạo hoặc đổi role.
- Bootstrap kiểm tra mọi Admin trong transaction Serializable; test active/inactive ở cả dry-run và execute cách ly.
- S29.3 CODE VERIFIED: bỏ CreateAsync(User)/CustomerRoleIdAsync; CreateCustomerAsync chỉ nhận thông tin,
  tự tra Customer role và từ chối context có thay đổi chưa lưu. AuthService là caller đăng ký duy nhất.
- Bootstrap giữ luồng riêng và kiểm tra AdminId=1; SystemRoles ghi ánh xạ đã có từ seed/InitialCreate/snapshot.
- Migration mã nguồn: `20261102122856_EnforceSingleAdmin`, CHƯA APPLY vào PhoneStoreDB.
- Thêm UX_Users_SingleAdmin: unique RoleId WHERE RoleId=1, giữ IX_Users_RoleId hiện có.
- Thêm CK_Roles_AdminIdentity: ID 1 chỉ mang tên Admin và tên Admin chỉ ở ID 1.
- Preflight migration THROW nếu mapping sai/thiếu hoặc có >1 Admin; không tự sửa/xóa/hạ quyền dữ liệu.
- Ràng buộc bảo đảm tối đa 1, không bắt buộc luôn có 1 và không chặn mọi thay đổi role của người có quyền SQL.
- Metadata thật lần trước: AdminCount=0, chưa có singleton index/constraint mới; S29.3 không truy cập DB thật.
- Test SQLite VERIFIED: chặn insert Admin thứ hai active/inactive, promotion khi đã có Admin, đổi mapping;
  Customer nhiều tài khoản vẫn được; Bootstrap từ chối mapping cũ sai và không tự sửa.
- Script offline/model khớp snapshot; S29.4 đã apply/test trên SQL Server cách ly (mục 12).
- Kết quả S29.3 (2026-11-02): restore PASS; build --no-restore 0 warning/error;
  `dotnet test PhoneStore.sln --no-restore --no-build`: 72/72 PASS (thêm 12 ca so với 60 trước đó).
- File S29.3: AuthService, AuthUserStore, RoleConfiguration, UserConfiguration, SystemRoles, BootstrapService;
  migration + Designer + snapshot; SingleAdminConstraintTests.cs; docs/PROGRESS.md.
- Trạng thái: code/test hoàn thành; migration thật chưa apply, không seed/import/CLI execute.
## 12. S29.4 — SQL Server cách ly và chuẩn bị UI (2026-11-02)
- VERIFIED: SELECT DB_ID test=0 trước tạo; guard kiểm tra lại rồi tạo PhoneStoreDB_MigrationTest riêng.
- EF database update dùng --connection chỉ rõ DB test; InitialCreate + EnforceSingleAdmin áp dụng thành công.
- Role Admin=1; UX_Users_SingleAdmin unique/filter RoleId=1; CK_Roles_AdminIdentity bật/trusted.
- tests/sql/S29_4_EnforceSingleAdmin.sql có guard DB_NAME/Users trống, test transaction rồi ROLLBACK.
- PASS SQL: Admin đầu inactive/active đều chặn Admin thứ hai; tạo được 2 Customer; đổi tên role Admin bị chặn.
- Sau rollback test Users=0, chỉ 2 Roles; giữ database test để kiểm tra, không xóa.
- Lần SQL đầu lỗi 1934 do sqlcmd QUOTED_IDENTIFIER OFF; bổ sung SET session cần cho filtered index, chạy lại PASS.
- Build `dotnet build PhoneStore.sln --no-restore`: 0 warning/error; test --no-restore --no-build: 72/72 PASS.
- Web PID 4692: http://localhost:5015, Development; Home/Products/Account Login HTTP 200, EF SELECT thành công.
- Admin PID 8344: Responding, cửa sổ 'Đăng nhập · PhoneStore Admin' hiển thị (UI Automation Offscreen=false).
- Hai app giữ cấu hình localhost/PhoneStoreDB; không tự chuyển sang test DB. Để mở cho người dùng kiểm thử.
- Runtime: cảnh báo HTTP không xác định HTTPS redirect port; chưa thấy lỗi startup, chưa đăng nhập/ghi nghiệp vụ.
- SELECT cuối DB thật: ONLINE, history chỉ InitialCreate, index mới=0, Roles=2, Users và 6 bảng khác=0.
- S29.4 thêm script SQL/PROGRESS; log Web trong src/PhoneStore.Web/obj/S29.4.web.*.log (ignored).
- Đồng bộ tài liệu sau S29.4: AGENTS xác định PROGRESS là nguồn tiến độ chính; CONTEXT tóm tắt mới, giữ lịch sử S17.3. Không chạy lại build/test/SQL hoặc sửa code/DB.
- Bước tiếp: chờ duyệt apply migration thật/tạo Admin riêng; UI nghiệp vụ vẫn PARTIAL do chưa có tài khoản/catalog.
