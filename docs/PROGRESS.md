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
| UI Web/WinForms và SQL integration đầy đủ | UNKNOWN ở môi trường hiện tại; không suy từ tài liệu cũ |
| AdminBootstrap S29 | PARTIAL toàn bộ: đã có mã + test; chưa xác minh dry-run/execute trên SQL Server |

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
- Kiểm kê 2026-11-02: restore/build thành công, 0 warning/error, 26/26 test SQLite pass.
- S29 sau kiểm kê: thêm công cụ độc lập `tools/PhoneStore.AdminBootstrap`, không tự chạy cùng Web/Admin.
- Mặc định dry-run chỉ đọc; execute yêu cầu xác nhận server + database thực tế.
- CLI chỉ hỗ trợ localhost/Windows Auth; nhập mật khẩu ẩn hai lần, PasswordHasher<User>.
- Từ chối email trùng, mọi Admin đã có (kể cả inactive), role thiếu và context có thay đổi chưa lưu.
- Không nâng quyền Customer, không đổi schema/migrations; execute dùng transaction Serializable.
- S29 mới nhất, 2026-11-02: `dotnet restore PhoneStore.sln` PASS.
- `dotnet build PhoneStore.sln --no-restore`: PASS, 0 warning/error.
- `dotnet test PhoneStore.sln --no-restore --no-build`: 51 passed, 0 failed, 0 skipped (26 cũ + 25 mới).
- Bootstrap tests dùng SQLite file tạm cách ly; suite cũ dùng SQLite in-memory; không ghi PhoneStoreDB.
- CLI help và từ chối tham số thiếu xác nhận/mật khẩu dòng lệnh đã kiểm tra; không kết nối DB thật.
- Không coi S29 hoàn tất vận hành: chưa chạy SQL dry-run/execute, chưa thử nhập mật khẩu tương tác thực tế.
- Task tài liệu hiện tại không chạy lại build/test; các kết quả trên là lần chạy S29 trước đó trong phiên.

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
- Task ghi nhớ này chỉ bổ sung AGENTS.md và tạo docs/PROGRESS.md; giữ các thay đổi khác nguyên vẹn.
- Không coi file đã commit chỉ vì tồn tại; dùng git status/diff có mục tiêu khi nhiệm vụ cần.

## 9. Hiện tại, phê duyệt và bước tiếp theo
- Task hiện tại: thiết lập ghi nhớ lâu dài; hoàn thành hai tài liệu, không sửa ứng dụng/database.
- Đã phê duyệt: xây dựng S29, test cách ly, README; dùng workspace-write/on-request có sandbox.
- Chưa phê duyệt: chạy AdminBootstrap trên database thật, kể cả bước dry-run đang chờ trong bàn giao S29.
- Bước tiếp theo: xin phép dry-run SQL chỉ đọc, kiểm tra đúng đích và điều kiện tạo Admin.
- Chỉ sau kết quả dry-run và phê duyệt ghi riêng mới chạy execute; người dùng tự nhập mật khẩu.
- Sau đó ưu tiên kiểm chứng rủi ro tồn kho, test SQL cách ly và UI; không tự thêm dữ liệu catalog.

## 10. Kết thúc mỗi nhiệm vụ
- Ghi ngày, trạng thái trước → sau và phạm vi bằng chứng; cập nhật mục liên quan thay vì nối log dài.
- Ghi file thay đổi, lệnh test/kết quả thật; nếu không chạy thì ghi rõ, không tái sử dụng PASS như kết quả mới.
- Ghi lỗi/rủi ro còn lại, công việc dở, bước tiếp theo và giới hạn phê duyệt.
- Giữ khoảng 150 dòng trở xuống, không sao chép hội thoại, mật khẩu, hash hoặc log dài.
- Không commit/push/reset/clean/history rewrite, xóa dữ liệu, restore/drop/seed/import/migrate
  hoặc thay đổi database thật nếu chưa được cho phép; không bật full access/bỏ toàn bộ approvals.
- Hướng dẫn agent không thay thế sandbox hoặc database firewall; không tuyên bố mọi lệnh nguy hiểm bị chặn.
