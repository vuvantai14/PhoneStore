# S29.8D — Audit chức năng sau nghiệm thu S29.8C.7

Ngày kiểm tra: **2026-11-02, Asia/Bangkok**. Kết luận: **PARTIAL toàn hệ thống**; luồng bán hàng cốt lõi đã nghiệm thu, chưa đủ bằng chứng để công bố mọi chức năng hoàn tất. Không sửa source hoặc thực hiện thao tác ghi database trong audit này.

## 1. Quy ước và nguồn bằng chứng

- **PASS thực tế**: đã chạy qua HTTP/UI/SQL; ghi rõ main hay database cách ly và phạm vi ca kiểm tra.
- **PASS test tự động**: có ca test đạt trong lần chạy được báo cáo; không suy thành nghiệm thu toàn bộ UI.
- **PARTIAL**: có triển khai/bằng chứng một phần, còn thiếu ca kiểm tra hoặc kết luận nghiệp vụ.
- **MISSING**: chưa có triển khai trong các đường chức năng liên quan đã kiểm tra.
- **NOT TESTED**: chưa có bằng chứng đã thực hiện ca cụ thể; không đồng nghĩa lỗi.

Nguồn: AGENTS.md, PROGRESS.md, README.md, CONTEXT.md; [audit cũ](S29_8A_FUNCTIONAL_AUDIT.md), [commerce](S29_8C_COMMERCE_REPORT.md), [C.5](S29_8C_5_ACCEPTANCE_DEPLOYMENT.md), [C.6B](S29_8C_6B_DEPLOYMENT_RESULT.md), [C.7/7A](S29_8C_7_TRANSACTION_ACCEPTANCE_RESULT.md); source liên quan và kiểm tra chỉ đọc hiện tại.

Các tài liệu S17 là lịch sử trước khôi phục môi trường, không dùng số liệu 41 sản phẩm/đơn Completed cũ làm bằng chứng cho database hiện tại. CONTEXT.md vẫn có mục Current Progress S29.4 và server cũ; cấu hình thực tế/PROGRESS mới được ưu tiên. Không sửa CONTEXT ngoài phạm vi lượt này.

## 2. Trạng thái runtime và SQL đã kiểm tra lại

| Hạng mục | Bằng chứng mới trong S29.8D |
|---|---|
| Customer Web | PID **22736**, `src/PhoneStore.Web/bin/Debug/net8.0/PhoneStore.Web.exe --urls http://localhost:5015` |
| Admin | PID **10736**, `src/PhoneStore.Admin/bin/Debug/net8.0-windows/PhoneStore.Admin.exe`; phiên Admin đang đăng nhập |
| Phiên bản | **118/118** file trong copy-plan C.6B khớp SHA256; không build/thay binary |
| SQL | SELECT lúc **02:27:26 UTC / 09:27:26 Bangkok**: DESKTOP-ES9362P, PhoneStoreDB ONLINE; session cả hai PID dùng PhoneStoreDB, sleeping/open transaction 0 tại lần kiểm tra |
| Catalog | Products **40**, ProductImages **40**, Brands **8**, Categories **1**, tổng tồn **740** |
| Tài khoản | Users **2**; đúng một Admin `admin@admin.com`, RoleId 1, active; Customer `tai123@gmail.com`, UserId 2, RoleId 2, active; Roles 2 |
| Giao dịch giữ lại | Orders **1**, OrderDetails **1**; OrderId 1, `ORD-05E65B3F0F705464636B525624`, Cancelled, 3.490.000 VND; ProductId 25 tồn **38** |
| Schema | InitialCreate + EnforceSingleAdmin; UX_Users_SingleAdmin unique/enabled/filter RoleId=1; CK_Roles_AdminIdentity enabled/trusted |
| Ảnh | GET **40/40** file JPG HTTP 200, Content-Type image/jpeg; không sửa ảnh |
| Báo cáo hiện tại | SELECT doanh thu Completed **0**; Dashboard/Reports thật hiển thị 1 đơn, 0 hoàn thành, doanh thu 0, tồn 740 |

HTTP mới: Home 200/6 cards; Products 200/40; search iPhone 200/6; brandId=1 200/6; khoảng giá 10–20 triệu 200/17; sort price-asc 200/40; detail 25 HTTP 200; ID 0 HTTP 404; Login/Register/Cart HTTP 200. HTTP 200 của sort chứng minh route chạy; tính đúng thứ tự đã có bằng chứng audit A, chưa viết bộ assertion mới ở D.

Customer browser đang đăng nhập: GET/read `/orders/1` thấy **Đã hủy**, đúng mã đơn, không còn nút hủy. Admin chỉ điều hướng đọc bảy màn hình: Dashboard; Products 40; Brands 8; Categories 1; Customers 1; Orders 1; Reports. Không bấm lưu, đăng xuất hoặc chuyển trạng thái.

## 3. Customer Web

| Chức năng | Phân loại | Bằng chứng và giới hạn |
|---|---|---|
| Đăng ký Customer | **PASS thực tế — main** | C.7 người dùng nhập mật khẩu trực tiếp Web; tạo đúng UserId 2, RoleId 2. AuthenticationTests/SingleAdminPolicyTests kiểm normalize, hash, email trùng và không chọn Admin. Không đăng ký lại trong D. |
| Đăng nhập | **PASS thực tế — main** | Người dùng xác nhận login C.7; hiện đọc được đơn thuộc mình. Sai mật khẩu/inactive/role có PASS test tự động; RememberMe hết hạn dài ngày chưa nghiệm thu đầy đủ. |
| Đăng xuất | **PASS thực tế — HTTP cách ly** | HTTP commerce harness đã kiểm đăng xuất; AccountController POST có Authorize/anti-forgery. Không logout phiên main trong D. |
| Danh sách, tìm kiếm, lọc, sắp xếp | **PASS thực tế — main, phạm vi đã chạy** | ProductsController/ProductService/catalog store; audit A và GET D nêu trên. Chưa có suite catalog riêng phủ mọi tổ hợp filter, biên giá, active brand/category. |
| Chi tiết, ảnh, giá/tồn | **PASS thực tế — main** | C.6B kiểm 40 trang và mapping; D đọc detail 25/ID sai và 40 ảnh. Không suy thành đã thử mọi biến thể ảnh thiếu/sản phẩm inactive trên main. |
| Thêm giỏ | **PASS thực tế — main** | C.7 thêm ProductId 25, qty 1; giá server 3.490.000. SessionCart/CartPresenter/CartController. |
| Cập nhật/xóa dòng giỏ | **PASS thực tế — HTTP cách ly; PASS test tự động** | HTTP harness C và CartControllerTests: add/update/remove, server price, session isolation; không thử ghi thêm main. |
| Giỏ: sai số lượng, hết hàng, ngừng bán | **PASS test tự động / HTTP cách ly** | CartControllerTests và HTTP harness chặn quantity sai/vượt kho/missing/inactive. Giỏ lưu browser session, không phải bảng Cart hoặc giỏ đồng bộ nhiều thiết bị. |
| Checkout, snapshot giá, trừ kho | **PASS thực tế — main** | C.7 một POST form: đúng 1 Order/1 Detail, qty 1, Pending; tồn 38→37, tổng 740→739. CheckoutTests/SQL tests phủ thêm ca lỗi. |
| Replay/double checkout | **PASS test tự động + SQL/HTTP cách ly** | C/C.5: cùng token trả đơn cũ, token độc lập tạo đơn độc lập, scope theo user, replay sau context mới; không gửi checkout lặp trên main. |
| Lịch sử/chi tiết/trạng thái | **PASS thực tế — main** | C.7/7A history/detail và trạng thái Pending→Confirmed→Cancelled; D vẫn thấy Cancelled. |
| Quyền sở hữu đơn | **PASS test tự động + HTTP cách ly** | OrdersController/PurchaseStore giới hạn CustomerId; foreign view/cancel bị từ chối. Main chỉ có một Customer nên không tạo Customer thứ hai để thử. |
| Hủy đơn/hoàn kho | **PASS thực tế — main** | Người dùng hủy Confirmed qua Web thật C.7A, tồn 37→38/tổng 739→740; Web/Admin/SQL đồng nhất. |
| Hủy lặp, Shipping/Completed không được hủy | **PASS test tự động + SQL/UI cách ly** | CommerceWorkflowTests, OrderWorkflowTests, C.5. Main không thử request hủy lặp; SELECT lặp không thay thế test replay ghi. |
| Hồ sơ Customer tự xem/sửa | **MISSING** | CONTEXT actor có Tài khoản/W09; AccountController chỉ register/login/logout/access-denied, chưa có profile action/view. `/account/profile` hiện 404. Admin sửa Customer không thay thế self-service. |

## 4. Admin WinForms

| Chức năng | Phân loại | Bằng chứng và giới hạn |
|---|---|---|
| Đăng nhập Admin | **PASS thực tế — main** | Người dùng login bản C.6B; D shell hiện admin@admin.com. AdminAuthService/AuthenticationTests kiểm active, password và Customer bị chặn. |
| Phân quyền | **PASS test tự động; PARTIAL toàn phiên desktop** | AdminSession/MainForm/CatalogManagementGateway.RequireAdmin kiểm role; Customer không được login Admin. Gateway dùng session snapshot, chưa revalidate DB từng thao tác; chưa test logout/session hết hiệu lực đầy đủ qua GUI. |
| Đăng xuất | **NOT TESTED qua UI hiện tại** | AdminApplicationContext.OnLogout có clear session/dispose shell/mở login; không bấm để giữ phiên người dùng. |
| Dashboard | **PASS thực tế — dữ liệu hiện có** | D mở thật: 40 sản phẩm, kho 740, 1 Customer active, 1 đơn, 0 Completed/0 doanh thu; chưa bao phủ mọi phân bố trạng thái/doanh thu dương. |
| Danh sách/tìm kiếm sản phẩm | **PASS thực tế — phạm vi đọc** | Grid 40; audit A tìm iPhone được 6. Bộ lọc brand/category/active có source, chưa nghiệm thu mọi combo trên GUI. |
| Tạo/sửa/ngừng hoạt động sản phẩm | **PARTIAL** | ProductsManagementControl/ProductEditDialog/ProductManagementService/store có triển khai và validation. Chưa nghiệm thu toàn bộ CRUD ghi qua UI cách ly. Không gọi soft-deactivate là thiếu hard-delete khi nghiệp vụ không yêu cầu xóa lịch sử. |
| Bảo vệ stale stock khi sửa | **PASS test tự động + SQL/dialog thật cách ly** | S29.8B/B.1: conditional UPDATE theo ID + original stock, affected rows; checkout 10→9 rồi save snapshot 10 bị chặn, hai form/rollback PASS. Đã triển khai cùng C.6B, không còn lỗi cũ chưa sửa. |
| Quản lý ảnh | **PARTIAL** | ProductImagesDialog + Add/Remove/Primary/Order; GUID copy, giới hạn 10MB/signature, lock/transaction/cleanup có code. 40 ảnh GET tốt không chứng minh upload/xóa/đổi primary/reorder/rollback file đã nghiệm thu. |
| Thương hiệu | **PASS thực tế đọc / PARTIAL ghi** | Grid 8; LookupManagementService/Store thêm/sửa/trạng thái, chặn tên trùng; chưa nghiệm thu write/concurrency UI đầy đủ. |
| Danh mục | **PASS thực tế đọc / PARTIAL ghi** | Grid 1, dùng lookup service/store; cần các ca validation/write như thương hiệu. |
| Khách hàng | **PASS thực tế đọc / PARTIAL sửa, khóa** | Grid 1 hiện đúng Customer; CustomerManagementService/Store giới hạn Customer, không đổi role. Tests bảo vệ Admin có; chưa nghiệm thu edit/disable/re-enable và hiệu lực session qua UI. |
| Danh sách/tìm kiếm/lọc đơn | **PASS thực tế — main đọc + UI cách ly** | Main thấy 1 đơn; C.5 controls thật đã tìm/filter đúng fixture. |
| Chi tiết đơn/khách hàng/giá lịch sử | **PASS thực tế — main và cách ly** | C.7A người dùng đối chiếu order code/sản phẩm/giá trước Confirm; C.5 detail dialog và snapshot price. |
| Chuyển trạng thái hợp lệ | **PASS thực tế** | Main Pending→Confirmed qua nút/confirmation thật; C.5 test DB Pending→Confirmed→Shipping→Completed qua dialog thật. Không tuyên bố main đã Shipping/Completed. |
| Chặn chuyển sai/hủy Admin/hoàn kho | **PASS test tự động + UI/SQL cách ly** | C.5 thiếu nút bất hợp lệ, backend từ chối reverse, terminal không actions, hủy hoàn một lần. Main đơn cuối Cancelled do Customer. |
| Báo cáo/thống kê | **PASS thực tế đọc hiện tại / PARTIAL toàn nghiệp vụ** | Reports D đúng 1 Cancelled/0 Completed/0 tiền; ReportingStore lọc Completed cho tổng/trend/average. Chưa có regression suite báo cáo và nghiệm thu doanh thu dương trên môi trường hiện tại. |
| Tồn kho/cảnh báo | **PASS thực tế số liệu hiện tại / PARTIAL quy trình quản trị** | Dashboard/report tồn 740, cảnh báo ngưỡng ≤5; stale-stock đã kiểm. Chưa có yêu cầu được chốt cho sổ biến động kho/nhập hàng độc lập; không tự thêm hệ thống kho mới. |

C.5 UI harness sử dụng controls/dialog thật nhưng gán AdminSession test bằng reflection; đó là nghiệm thu module Orders, không phải full-shell login. Main login/Confirm được người dùng xác nhận riêng C.6B/C.7A.

## 5. Backend, SQL và tích hợp

| Hạng mục | Phân loại / nhận định |
|---|---|
| Kiến trúc | Có Business/Data dùng chung cho Web MVC và WinForms; EF Core 8, SQL Server. Không cần dựng thêm REST API để đáp ứng kiến trúc hiện tại. |
| Giá/tổng tiền | PASS test + main một giao dịch: đọc Product.Price trong checkout, lưu UnitPrice snapshot, không tin tổng client. |
| Checkout transaction/không âm kho | PASS SQLite + SQL Server cách ly: Serializable, UPDLOCK/HOLDLOCK theo ProductId; tạo Order/Details và trừ kho atomic, tranh đơn vị cuối chỉ một thắng. Main chỉ nghiệm thu case qty1. |
| Rollback lỗi giữa chừng | PASS test/SQL cách ly; dispose transaction không commit và ClearTracking; không tiêm lỗi trên main. |
| Idempotency bền vững | PASS test/SQL/HTTP cách ly: OrderCode từ user+token, unique index hiện có, sp_getapplock Transaction và readback; không còn chỉ dựa memory cache như audit A. Không tự retry outcome commit không rõ. |
| Atomic hủy/hoàn kho | PASS test/SQL/UI cách ly và main một lần: lock order + product, validate owner/status, cập nhật trạng thái/kho cùng transaction; repeat Cancelled không cộng lại. |
| Admin duy nhất | PASS tests + SQL constraint isolation S29.4; main unique filtered index/check đang enabled/trusted. Bootstrap riêng, kể cả inactive vẫn chặn Admin thứ hai; không thử INSERT trên main. |
| Customer session/CSRF | CustomerCookieEvents đọc active account; POST ghi có anti-forgery và policy. HTTP harness kiểm auth/ownership/CSRF; controller tests riêng không chứng minh toàn middleware. |
| Đồng bộ Web–Admin | PASS thực tế C.7A: Confirm trên Admin→Customer thấy Confirmed; Customer Cancel→Admin refresh thấy Cancelled. Dialog đang mở là snapshot, phải refresh/mở lại; không mặc định realtime push. |
| Validation CRUD | Có service constraints/DTO/UI và DB constraints; PARTIAL độ phủ, đặc biệt ảnh/lookup/customer/report. Không kết luận PASS mọi thông báo chỉ từ việc có try/catch. |
| Ngày báo cáo | PARTIAL: ReportingService diễn giải DateOnly thành UTC midnight; UI chọn ngày local. Cần chốt timezone nghiệp vụ và test sát nửa đêm trước sửa. |
| Chịu tải/mất kết nối | NOT TESTED đầy đủ: stress nhiều instance, deadlock dài hạn, timeout đúng lúc commit, khôi phục app/session; hữu hạn SQL tests không bảo đảm mọi kịch bản. |

Giới hạn đã biết: original-stock comparison không phải rowversion cho mọi field/ABA; legacy CreateAsync nội bộ không nhận token, Web đang dùng CreateIdempotentAsync; cart theo browser session chưa có quy tắc chuyển tài khoản/persistence nhiều thiết bị. Đây là phạm vi cần đánh giá, chưa phải lỗi runtime mới đã chứng minh.

## 6. Đối chiếu yêu cầu đồ án

Phạm vi xác định từ README, CONTEXT và yêu cầu người dùng: C#/.NET, MVC Customer, WinForms Admin, tầng Business/Data, SQL Server; quản lý catalog/Customer/đơn/báo cáo và bán hàng. **Chưa có bộ tiêu chí chấm điểm hoặc Word/Excel UC01–UC12/SCR01–SCR12 được xác minh trong kiểm kê trước**, không khẳng định đáp ứng toàn bộ tài liệu chưa có.

| Nhóm yêu cầu có căn cứ | Mức đáp ứng |
|---|---|
| Công nghệ/kiến trúc và dữ liệu chung | Có triển khai, runtime Web/Admin/SQL đã xác minh |
| Customer mua hàng và quản lý đơn của mình | Luồng chính PASS thực tế; lỗi biên/concurrency PASS tests/cách ly trong phạm vi đã chạy |
| Tài khoản Customer (CONTEXT W09 và yêu cầu audit) | MISSING self-service profile; cần chốt trường được sửa và validation |
| Admin quản lý dữ liệu | Đọc và đơn hàng đã nghiệm thu; CRUD sản phẩm/ảnh/lookup/Customer còn PARTIAL |
| Báo cáo doanh thu/tồn | Có triển khai, đọc đúng ca Cancelled; thiếu bộ kiểm thử dữ liệu Completed và biên thời gian |
| Tính toàn vẹn/phân quyền | Có tests và constraints; không tuyên bố đã security/stress audit toàn diện |

Quên/đổi mật khẩu, xác thực email, thanh toán online, vận chuyển ngoài hệ thống, export Excel/PDF, nhà cung cấp/nhập kho và realtime push chưa được coi là điều kiện bắt buộc nếu chưa có yêu cầu chính thức. UI/UX thực hiện sau chức năng, trừ lỗi ngăn sử dụng.

## 7. Lỗi, thiếu sót và thứ tự xử lý

Không phát hiện P0 mới hoặc lỗi runtime mới trong các thao tác đọc đã chạy. Không đưa stale stock/hủy Customer/idempotency cũ trở lại danh sách lỗi mở: các mục này đã sửa, kiểm thử cách ly và triển khai.

| Ưu tiên | Phát hiện | Loại / hướng xử lý |
|---|---|---|
| P1 — hoàn thiện scope | Thiếu hồ sơ Customer tự xem/sửa | MISSING; bổ sung tối thiểu endpoint/view/service/DTO cho chủ tài khoản, không cho client sửa RoleId/Email/active ngoài chính sách đã duyệt; ưu tiên dùng schema có sẵn. |
| P1 — bằng chứng nghiệm thu | CRUD sản phẩm/ảnh/lookup/Customer chưa đủ write acceptance | PARTIAL, không phải FAIL; thêm isolated tests cho validation/rollback/file lifecycle/soft-deactivate và tương tác UI. Dùng DB test hiện có nếu được duyệt fixture, ảnh test riêng; không ghi main. |
| P2 — báo cáo | Thiếu tests Completed/Cancelled/Shipping, lọc ngày, average/best sellers | Thêm regression dữ liệu cách ly; phân biệt revenue Completed với SalesValue của Confirmed/Shipping/Completed. Chốt timezone trước sửa biên ngày. |
| P2 — auth/session | Admin logout chưa nghiệm thu; session role snapshot; RememberMe chưa đủ ca | Test login sai/logout/role/active/session trên môi trường cách ly trước quyết định thay đổi service. |
| P2 — độ bền | Chưa bao phủ stress, commit không rõ, mọi trường concurrency | Kịch bản fault có kiểm soát, không tự retry ghi; không vội thêm schema/rowversion khi chưa chứng minh nhu cầu. |
| P2 — tài liệu | CONTEXT Current Progress/server còn cũ | Đánh dấu trong audit/PROGRESS; cập nhật tài liệu gọn ở nhiệm vụ kế tiếp, không coi server cũ là cấu hình runtime. |

Giai đoạn đề xuất: **D.1 hồ sơ Customer** → **D.2 kiểm thử/bổ sung tối thiểu CRUD quản trị** → **D.3 báo cáo và auth/session** → **D.4 nghiệm thu tổng hợp cách ly** → tính năng mở rộng được duyệt → UI/UX. Không viết lại các luồng commerce đã PASS.

## 8. Kiểm thử và phạm vi thay đổi

- Baseline có bằng chứng gần nhất: **107/107 PASS, build 0 warnings/errors**, từ C.5/C.6 preflight; lệnh được ghi: `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8C.5/ --verbosity minimal`. **Không build/test lại trong D**, không gắn kết quả cũ thành lần chạy mới.
- Suite hiện có: AuthenticationTests, AdminBootstrapTests, SingleAdminPolicyTests, SingleAdminConstraintTests, CheckoutTests, OrderWorkflowTests, ProductStockConcurrencyTests, CommerceWorkflowTests, CartControllerTests. TestDatabase dùng SQLite cách ly. SQL/HTTP/UI harness là bằng chứng riêng, không cộng vào 107.
- Thiếu suite chuyên biệt cho catalog/filter permutations, CRUD lookup/ảnh/Customer, report/timezone và desktop session; profile chưa có chức năng để nghiệm thu. SQL integration opt-in dùng PhoneStoreDB_StockConcurrencyTest, không chạy lại trong D và không tạo database mới.
- Lệnh SELECT doanh thu lần đầu dùng nhầm enum số trong khi Status lưu chuỗi, bị lỗi conversion; đã sửa truy vấn chỉ đọc thành `Status=N'Completed'`, kết quả 0. Đây là lỗi truy vấn audit, không phải lỗi ứng dụng hoặc thay đổi dữ liệu.
- Chỉ tạo báo cáo này và cập nhật PROGRESS. Không POST form, INSERT/UPDATE/DELETE, migration/backup/restore/import, restart/copy binary, sửa ảnh/source, commit/push. Số liệu main giữ nguyên so với kết thúc C.7A.
- Backup gần nhất được ghi nhận là bản **trước** giao dịch C.7, không chứa Customer/Order mới; VERIFYONLY không thay full restore rehearsal. Không tạo backup mới trong D.

**Dừng chờ người dùng duyệt giai đoạn sửa đầu tiên.** Đề xuất D.1: kiểm tra User/AuthService, thêm service/DTO/profile action/view và tests ownership/validation, dùng schema hiện có; không đổi mật khẩu, role hoặc ghi main trong quá trình phát triển.
