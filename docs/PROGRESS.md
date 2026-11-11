# PhoneStore — Tiến độ và điểm tiếp tục

Cập nhật 2026-11-11 — S29.9H.6 **READY FOR USER TEST, sửa dứt điểm trạng thái phân trang và bộ lọc**:
- **Nguyên nhân lỗi**:
  - `ProductsController.cs` tự động ép `filter.BrandIds` chứa Apple & Samsung và `filter.InStockOnly = true` khi query string thiếu tham số (do tàn dư quy tắc cũ S29.9H.4).
  - Khi user chuyển trang (`/products?page=2`), hàm `BuildFilterUrl` tự động gắn `brandIds=1&brandIds=2&inStockOnly=true` vào URL phân trang và hiển thị các chip bộ lọc ngoài ý muốn.
- **Sửa chữa nghiệp vụ**:
  - `/products` mặc định hiển thị toàn bộ 40 sản phẩm (4 trang, 10 sp/trang). Không tự ý chọn trước bất kỳ thương hiệu nào, không tự ý lọc chỉ còn hàng.
  - `/products?page=2` chỉ chuyển sang trang 2 của toàn bộ sản phẩm, URL sạch sẽ `/products?page=2`, không tự sinh query lọc, không hiển thị chip khi chưa chọn lọc.
  - Chỉ áp dụng bộ lọc khi user chủ động tick/chọn và submit form.
  - Khi chuyển trang, giữ nguyên đúng các điều kiện lọc người dùng đã chủ động chọn.
  - Khi thay đổi bộ lọc hoặc đổi sắp xếp, form submit quay về trang 1.
  - Khi click "Đặt lại" / "Xóa bộ lọc" / "Xóa tất cả", URL trỏ thẳng về `/products`, xóa sạch toàn bộ điều kiện và về trang 1.
  - Xử lý phòng thủ `page`: Bỏ qua lỗi định dạng nếu `page` không phải số (như `page=abc` -> về trang 1 không báo lỗi); nếu `page > totalPages` thì tự động đưa về trang cuối (`totalPages`).
  - Đồng bộ `HasFilters` trong `CatalogViewModels.cs` để `InStockOnly` chỉ tính là có lọc khi bằng `true`.
- **Files đã sửa**:
  - `src/PhoneStore.Web/Controllers/ProductsController.cs`
  - `src/PhoneStore.Web/Models/CatalogViewModels.cs`
  - `src/PhoneStore.Data/Catalog/ProductCatalogReader.cs`
  - `src/PhoneStore.Web/Views/Products/Index.cshtml`
- **Tuân thủ nghiêm ngặt**: Không build/test, không chạy Web/chiếm cổng 5015, không đụng database/migration, không stage/commit/push.

Cập nhật 2026-11-11 — S29.9H.5 **READY FOR USER TEST, hoàn tất sửa giao diện bộ lọc & card sản phẩm**:
- **Sidebar bộ lọc**:
  - Đã xử lý triệt để lỗi tiêu đề nhóm bộ lọc bị biến thành thanh màu xanh navy đậm (`var(--secondary)` ở CSS cũ dòng 120); thiết kế lại theo phong cách tối giản (nền trắng nhạt `#fbfdff`, chữ xanh navy `#071d3d`, viền ngăn cách mảnh `#eaf1fa`, icon chevron xoay 180° mượt mà khi mở/đóng).
  - Thu gọn khoảng cách và padding thừa của sidebar (`.filter-body` 10px 14px 16px, `.filter-group` 8px 0, ô tìm kiếm cao 36px, nút Áp dụng 40px/Đặt lại 34px), bố cục thanh thoát, dễ thao tác.
  - Trạng thái trực quan: Chip RAM/Storage và radio khoảng giá khi được chọn nổi bật với nền xanh `var(--primary)` `#1559d6` và chữ trắng; checkbox thương hiệu/tồn kho có ô tickbox xanh sắc nét.
- **Card sản phẩm & Lưới 5 cột**:
  - Sửa lỗi nút "Chi tiết" bị rớt dòng thành 2 dòng khi hiển thị ở lưới 5 cột desktop: chuyển `.product-actions` sang `grid-template-columns: minmax(0, 1fr) auto`, thêm `white-space: nowrap !important;` và `padding: 6px 11px; flex-shrink: 0;` cho `.detail-link`.
  - Nút "Thêm vào giỏ" (`.add-cart-button`) chiếm toàn bộ không gian còn lại với `min-width: 0; display: flex; width: 100%`, icon giỏ hàng 14px cố định.
- **Phân trang & Bố cục**: Giữ nguyên 5 sản phẩm/hàng, 10 sản phẩm/trang, thanh phân trang Trước/1/2/3/4/Tiếp giữ nguyên toàn bộ query params.
- **File đã sửa**: `src/PhoneStore.Web/wwwroot/css/site.css`.
- **Tuân thủ nghiêm ngặt**: Không build/test, không chạy Web/chiếm cổng 5015, không đụng database/migration, không stage/commit/push. Chờ user nghiệm thu trực tiếp trên trình duyệt.

Cập nhật 2026-11-11 — S29.9H.4 **READY FOR USER TEST, hoàn tất nâng cấp bộ lọc sản phẩm**:
- **Sidebar bộ lọc mới**:
  - Tiêu đề "Bộ lọc sản phẩm" kèm nút "Đặt lại" góc trên; từng nhóm thu gọn/mở rộng (`details[open]`).
  - Ô tìm kiếm có icon kính lúp.
  - Checkbox 8 thương hiệu thực tế: Mặc định Apple & Samsung được chọn khi chưa có tham số thương hiệu; cho phép chọn nhiều thương hiệu (`WHERE BrandId IN (...)`).
  - Khoảng giá: Nút chọn nhanh (Tất cả, Dưới 5 tr, 5-10 tr, 10-20 tr, Trên 20 tr) + 2 ô nhập tùy chỉnh (ưu tiên giá nhập tùy chỉnh).
  - Chip RAM (`4GB`, `6GB`, `8GB`, `12GB`, `16GB`) & Chip Bộ nhớ (`64GB`, `128GB`, `256GB`, `512GB`) chuẩn hóa đa định dạng chuỗi ("4 GB", "4GB").
  - Checkbox "Chỉ hiển thị sản phẩm còn hàng": Mặc định bật (`StockQuantity > 0`); khi tắt hiển thị cả hết hàng.
  - Nút chính "Áp dụng bộ lọc" & Nút phụ "Xóa bộ lọc" (reset toàn bộ điều kiện về không giới hạn).
- **Bộ lọc đang áp dụng**: Thanh chip phía trên danh sách hiển thị các điều kiện đang chọn (`Apple ×`, `Samsung ×`, `Còn hàng ×`,...), click `×` để gỡ từng điều kiện.
- **Phân trang**: Giữ nguyên 10 sản phẩm/trang, 5 cột × 2 hàng, thanh phân trang Trước/1/2/3/4/Tiếp giữ nguyên toàn bộ query params.
- **Responsive**: Desktop sidebar cố định bên trái; Mobile có nút "Bộ lọc" trên toolbar mở modal drawer có nút đóng `&times;`.
- Tuân thủ nghiêm ngặt: Không build/test, không chạy Web, không chiếm cổng 5015, không đụng database/migration, không commit/push. Chờ user tự build và nghiệm thu.

Cập nhật 2026-11-09 — S29.9G.5.2 **PASS, chờ user duyệt 8 logo thương hiệu Home trên 5015**: bổ sung `realme.svg` (Wikimedia Commons `Realme_logo.svg`, public domain text logo/trademarked, lấy từ realme.com; giữ nguyên path wordmark, bỏ nền vàng, đơn sắc như Simple Icons); Simple Icons realme không tồn tại. Crop `viewBox` theo glyph cho oppo/vivo/samsung/realme (path giữ nguyên); nguồn/giấy phép ghi tại `wwwroot/images/brands/BRAND_LOGO_SOURCES.md`. `Index.cshtml` chỉ thêm mapping realme + khung `.brand-logo-box` 104×40 và class theo brand; `site.css` thêm khối G.5.2 cuối file: kích thước thị giác theo logo (Apple25×30, Google29, OnePlus/Xiaomi30, OPPO80×19, vivo76×20, realme84×19, Samsung100×16), `object-fit:contain`, tên `--secondary` 14px/600. Không đổi hero/card sản phẩm/header/link/controller/DB. Targeted `HomePresentationTests` 2/2 PASS (chạy ngoài sandbox vì vstest bị chặn socket nội bộ); build Web 0 warning/error. Dừng PID23476 đã xác minh (Stop-Process, không -Force), chạy lại Web 5015 (`bin/Debug/net8.0`, Production, content root Web). HTTP Home/Products/brand filter/realme.svg 200. CDP 375/768/1024/1440: 8 logo, cao card đồng đều, tên thẳng hàng, logo căn giữa, không cắt/tràn. Không DB/stage/commit/push. DỪNG ở Home.

Cập nhật 2026-11-09 — S29.9G.5.1 **PASS, chờ user duyệt thương hiệu Home trên 5015**: logo Simple Icons (7 SVG + license, brand thứ 8 hiển thị tên) và CSS `S29.9G.5 Pattern 1` đã có trong source/runtime nhưng bị khối G.1 phía sau ghi đè (card cao cố định 66/62px, logo 28px → chật/cắt). Chỉ bỏ 7 dòng brand cũ trong khối G.1 của `site.css`; không đổi Razor/card sản phẩm/font/nghiệp vụ. Build Web 0 warning/error (`-m:1`, build song song lỗi trong sandbox); CSS-only nên không chạy test. Web PID23476 (`bin/Debug/net8.0`, 5015) đã phục vụ CSS mới (wwwroot live, dll không đổi) nên không restart. HTTP: Home200/12 card/7 logo+1 tên, CSS200 rule mới, `/products`200, logo SVG200. Không DB, stage/commit/push; giữ `PhoneStore.sln`. DỪNG ở Home.

Cập nhật 2026-11-09 — S29.9G.4.1 **PASS, chờ user duyệt giá card Home**: UI UX Pro Max numeric typography; sửa lệch do giá flex center + ký hiệu `₫` 78% nhưng line-height 25px. Probe glyph xác nhận Be Vietnam Pro không hỗ trợ biến thể `tnum`, nên không dựa vào feature không tồn tại; giá Home dùng baseline, weight700, line-height1.25 và tracking chuẩn, không đổi markup/giá/định dạng/bố cục. Build Web 0 warning/error; CSS-only nên không chạy test. Xác minh PID cũ16304 đúng Web5015 rồi shutdown không `/F`; runtime mới PID24776 Debug, Home HTTP200/12card. Browser375/1440 computed PASS, nhiều độ dài giá không overflow/clipping. Không DB DML, stage/commit/push; giữ thay đổi trước và `PhoneStore.sln`. DỪNG ở Home.

Cập nhật 2026-11-09 — S29.9G.4 **PASS source/browser preview, chờ user duyệt typography**: dùng skill UI UX Pro Max; thay Segoe UI và weight tổng hợp 650/720/750/780/800 bằng Be Vietnam Pro cục bộ, đúng 4 face 400/500/600/700 + OFL, token font toàn cục và `font-synthesis:none`. Build 0 warning/error, targeted 2/2 PASS. Preview `http://localhost:5199/`: `document.fonts`/resource timing/computed styles xác nhận font thật đã load; Home 12 card 2/3/4/6 cột tại 375/768/1024/1440, card đồng đều, tiếng Việt/số/`₫` đúng, không overflow/clipping/ảnh vỡ. Smoke Home/Products/Detail/Login/Register/Cart ở 4 viewport PASS. Main không restart/deploy, không DB DML, không stage/commit/push; giữ mọi thay đổi G trước đó và `PhoneStore.sln`. Xem `S29_9G_4_TYPOGRAPHY.md`. DỪNG ở Home.

Cập nhật 2026-11-09 — S29.9G.3 **PASS source/browser preview, chờ user duyệt card Home**: partial card riêng Home theoMẫu1, khôngđổi sharedCatalog; nềntrắng/surfacexanhnhạt/shadownhẹ, brandđầyđủ, RAM+Storage2chip, giáxanhđậm, stockxanhlá, CTAchính+detailviền, khôngbadgegiả. Build0warnings/errors,targeted2/2PASS. Preview `http://localhost:5198/` PID15208: 12card, grid6×2/4/3/2; card đồngđều, khôngoverflow/clipping/ảnhvỡ/nútchồng; controls44/45px, focus/reduced-motionPASS. UI UX Pro Max áp dụng hierarchy/4-8spacing/responsive/a11y, giữ design system. Main5015 khôngrestart G.3; khôngDBDML/stage/commit/push; bảo toàn `PhoneStore.sln`. Xem `S29_9G_3_HOME_PRODUCT_CARD.md`. DỪNG ởHome.
HEAD `0a94f98` — Customer Web Acceptance — làcheckpointF; workingtree sạch trướcG, G cònunstaged. F:11màn×4viewport runtimechỉđọcPASS, WebmainPID14764/bin/S29.9F/5015; snapshotF40Products/40Images/tồn733/Orders4/Users2 là lịch sử, khôngsuy trạngtháilive. CácPOSTnghiệpvụmainchưa nghiệmthu, giữPARTIAL.
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
- [D.4C.3](S29_8D_4C_3_ADMIN_ACCEPTANCE_SUMMARY.md): Admin CONDITIONAL READY. Core CRUD/Dashboard/Reports/Auth/Orders + revoke IsActive/RoleId runtime PASS; partial DisplayOrder/close-during-save/Brand text/Customer-write và race check→commit.
- [D.4D.1](S29_8D_4D_1_ADMIN_MAIN_PREDEPLOY.md): pre-deploy READY; manifest 58 runtime loại appsettings, main/test config và uploads tách biệt, không migration.
- [D.4D.2/2A](S29_8D_4D_2_ADMIN_MAIN_DEPLOYMENT.md): deploy Admin main D.4B PASS. Backup59/59, runtime58/58, main config hash giữ nguyên; PID4592/SQLmain. WinForms thật PASS Login/Dashboard/Reports/Product list+3 search cases/Brands/Categories/Customers/Orders detail/Logout. Hậu kiểm main40/40/tồn740/1Cancelled/1Admin và40ảnhhash; không rollback/DML/migration. Test DB baseline giữ nguyên; PID test không còn ở checkpoint cuối nhưng D.4D.2 không tác động nó.
- [E.1 Customer audit](S29_8E_1_CUSTOMER_FUNCTIONAL_AUDIT.md): PARTIAL. Registration/login/catalog/cart/checkout/idempotency/concurrency/orders/cancel/profile/ownership có source và automated/SQL/browser evidence phân loại rõ; không thấy mandatory function MISSING hay P0. P1 hiện tại: Web không process/listener5015 nên HTTP unavailable. Main read-only40Products/40Images/tồn740/1Customer/1Cancelled/1Admin và40ảnhhash nguyên. Đề xuất E.2 khởi động đúng binary/config rồi browser acceptance; chưa sửa/deploy/DML.
- [E.2 browser smoke](S29_8E_2_CUSTOMER_BROWSER_SMOKE.md): PASS. Web PID14572, HTTP5015, SQL session `PhoneStoreDB`/openTx0; startup log sạch ngoài warning HTTPS-port HTTP-only. HTTP PASS Home6/Products40/search6+empty0/brand6/category40/detail25/login/register/cart/redirect protected/40ảnh. User xác nhận browser thật PASS toàn checklist. Main40/40/tồn740/1Customer/1Cancelled/1Admin và40ảnhhash nguyên; Admin4592 không ảnh hưởng. Authenticated/cart writes/checkout không thử trong E.2; đề xuất E.3 có phê duyệt riêng.
- [E.3 account acceptance](S29_8E_3_CUSTOMER_ACCOUNT_ACCEPTANCE.md): PASS browser thật. User tự login Customer; greeting/protected routes/profile readonly đúng tai123@gmail.com/tài vũ/phone/addressNULL; history/detail đúng Order1 Cancelled/vivoY17s×1/3490000, không nút cancel; cart readonly PASS; logout và protected redirect login PASS. Foreign ownership chỉ PASS automated/isolated, không thử main. Hậu kiểm40/40/tồn740/1Customer/1Cancelled/1Detail/1Admin và40ảnhhash; Web14572/Admin4592/log sạch, không DML.
- [E.4 logout cart fix](S29_8E_4_LOGOUT_CART_ISOLATION_FIX.md): root cause cookie logout không xóa SessionCart/token, gây giỏ dùng chéo browser. AccountController inject cart và clear đúng2 key trước signout; regression gọi action thật. Targeted1/1, build0warning/error, full162/162 PASS. Main40/40/tồn740/1Customer/1Cancelled/1Detail/1Admin+40hash nguyên. Chưa deploy; Web14572 vẫn binary cũ nên runtime còn lỗi đến khi duyệt deploy/restart. Known gap: cookie expiry/revoke không qua logout chưa owner-bind cart.
- [E.4A predeploy](S29_8E_4A_LOGOUT_FIX_PREDEPLOY.md): READY. Source diff đúng AccountController+CartControllerTests, output build sau source/162tests. Manifest runtime mới/current62/62; preserve2appsettings, deploy đủ60 files (8 khác hash). Web14572/SQLmain và Admin4592 giữ nguyên; main40/40/tồn740/1Customer/1Cancelled/1Admin+40hash. Backup path mới chỉ lập, chưa tạo. E.4B cần duyệt backup/shutdown/deploy và session-only add1cart→logout→empty; không checkout/SQL DML.
- [E.4B deployment](S29_8E_4B_LOGOUT_CART_FIX_DEPLOYMENT.md): deploy Web fix PASS: backup62/62, staging/runtime60/60,2config+40ảnh giữ hash; soft CtrlBreak old14572 PASS, new Web12640/5015/SQLmain/log sạch, HTTP smoke+40ảnh PASS, không rollback; Admin4592 nguyên. User checkout ngoài phạm vi tạo Order2 `ORD-D5AD063ADF220813B7EA345E99` Pending/vivoY17s×1/3490000, stock25=37,total739, Orders/Details2; agent chỉ SELECT, không sửa. User sau đó add session lại×1→logout→cart anonymous rỗng: cart isolation PASS runtime, không Order3. Token cleanup PASS automated, chưa runtime-observe riêng. Giữ Order2 chờ quyết định nghiệp vụ; không tự cancel/delete/stockfix.
- HTTPS redirect thiếu port khi chạy HTTP vẫn warning đã biết; không phát hiện runtime failure mới qua audit đọc.
- Word/Excel UC01–UC12/SCR01–SCR12/rubric chấm điểm chưa được xác minh; không tự suy đáp ứng toàn bộ tài liệu chưa có.

## 8. Nhiệm vụ hiện tại và bước tiếp theo
- [S29.9G Home](S29_9G_HOME_UI_REFINEMENT.md): chỉHomeRazor/CSS+2tests, preview5196đểuserduyệt; giữheaderF/footer, khôngghidữliệu hoặc chuyểnmànhình. Sau khi userchốt mới quyếtđịnh cậpnhật5015/checkpoint hoặc mànhìnhtiếp.
- [S29.9F runtime](S29_9F_CUSTOMER_WEB_RUNTIME_ACCEPTANCE.md): D/E đã lênmain mới, Customer tựlogin, Orders/CancelledDetail/Profile/Cart/CheckoutGETPASS. FchỉsửaCSSheader,docs/screenshots; chờ user quyết địnhcommit/push. Không thao tácAdmin ởF; READY chuẩn bị UIAdmin sauchỉđạo.
- [S29.9E Auth/Profile UI](S29_9E_AUTH_PROFILE_UI_UX.md): preview Razor không SQL PASS, summary/link/error/hint/loading/pageshow/keyboard/contrast PASS; không thay đổi auth/session/backend. Chưa nghiệm thu trên runtime main với Customer thật; không stage/commit/push. Giữ toàn bộ công việc D, không audit lại các màn hình đã PASS.
- [S29.9D UI](S29_9D_CART_CHECKOUT_ORDERS.md): 5 Razor views + CSS/JS; browser cách ly PASS (native invalid/summary/link focus/Tab/Shift+Tab/loading/pageshow/confirmation-dismiss). Main chỉ GET cart200; Razor mới chưa deploy, không ghi SQL. Skill đọc trực tiếp do `py` chưa khả dụng; script/harness tạm ở obj ignored.
- [S29.9B.2 acceptance](S29_9B_2_BROWSER_ACCEPTANCE.md) và [S29.9C](S29_9C_CATALOG_PRODUCT_DETAIL.md) đã thay trạng thái B.1 PARTIAL: Home/catalog/detail PASS Edge CDP đúng 4 viewport, hero/fallback/tablet CTA đã sửa. B/C build0warning/error,162testsPASS; không đổi nghiệp vụ.
- [S29.9A UI/UX audit](S29_9A_UI_UX_AUDIT.md): Codex nhận diện `ui-ux-pro-max` và đã audit source toàn bộ Web/Admin; không sửa source/runtime/DB. Kết luận READY có điều kiện cho S29.9B Web trước. Điểm ưu tiên: font 9–12px, disabled contrast 2.56:1, error-focus/checkout/cancel, reduced-motion chưa phủ transform, token/responsive Web; DPI/keyboard/status/chart semantics Admin. `py` hiện báo không có Python nên search design-system chưa chạy, không có output cũ; audit dùng SKILL/checklist trực tiếp. Tests/build không chạy vì documentation-only.
- S29.9-SKILL: đã kiểm tra gói chính thức `ui-ux-pro-max-cli` 2.15.0 và cài project-local vào `.agents/skills/` bằng CLI đã tải/kiểm tra, không global, không `--force`, không sửa UI/DB/runtime. `ui-ux-pro-max` có SKILL.md + 68 file (~3.57 MB); CLI chính thức còn tạo 6 skill phụ đi kèm. README nói có `--dry-run` nhưng npm 2.15.0 thực tế không khai báo option này, nên đã audit mã trước khi init. Node24.22.1/npm11.19.1/Codex0.164.0; Python3 chưa khả dụng (chỉ Windows Store alias), vì vậy search.py chưa chạy và trạng thái PARTIAL. Cần người dùng tự cài Python3 rồi mở phiên Codex mới để nhận skill và chạy smoke search; không cài Python/global CLI tự động.
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
