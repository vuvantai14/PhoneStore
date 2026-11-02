# S29.8D.2C — Nghiệm thu Admin WinForms cách ly

Ngày 2026-11-02, Asia/Bangkok. **D.2C vẫn PARTIAL: login Admin test đã PASS sau reset D.2C.1; chưa thực hiện CRUD ghi hoặc cleanup cuối.**

## 1. Precheck và build — PASS

Đã đọc AGENTS, PROGRESS, báo cáo D2A/D2B. AdminServices đọc appsettings tại AppContext.BaseDirectory rồi AddEnvironmentVariables; connection key ConnectionStrings:PhoneStoreConnection, ảnh ProductImages:UploadsDirectory. Có thể tách hoàn toàn file cấu hình và biến môi trường của tiến trình test.

- localhost/DESKTOP-ES9362P/PhoneStoreDB_StockConcurrencyTest ONLINE, schema hiện có; trước chuẩn bị không có session/process khác dùng DB test.
- DB test có UserId1/admin@s29-8b1.test/RoleId1/active,3Products/5Users/10Orders từ fixture cũ. Không sửa/reset/xóa fixture cũ.
- SQL source B.1 xác nhận mật khẩu Admin test được tạo ngẫu nhiên chỉ trong memory, không lưu; không có password để tự login và không tìm cách phục hồi hash/password.
- Main Web19388/Admin10736 giữ nguyên và responsive. PhoneStoreDB ONLINE,40Products/40Images/tồn740/1Customer/1Adminactive/Order1Cancelled/Detail1.
- Ghi9snapshot main/test không chứa credentials và SHA256 của40ảnh demo trước chuẩn bị.

```powershell
dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8D.2C/
dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8D.2C/ --verbosity minimal
```

Kết quả mới: **0warnings/errors;155/155 tests PASS**,0fail/skip. Build riêng, không thay Debug/appsettings của ứng dụng chính. Không sửa source trong D2C tới checkpoint này.

## 2. Tiến trình Admin test thật đã mở

Artifact root:
`C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8D.2C-20261102_035250536Z`

| Hạng mục | Giá trị |
|---|---|
| Test PID | **13368** |
| Binary | artifact root/app/PhoneStore.Admin.exe, copy đủ output build mới |
| File config test | artifact root/app/appsettings.json |
| Database pin | localhost/PhoneStoreDB_StockConcurrencyTest, Windows Authentication |
| Uploads test | artifact root/uploads (riêng, hiện0file) |
| Ảnh đầu vào fixture | artifact root/input-images (5file tự tạo, không dùng ảnh demo) |
| Cửa sổ | **TEST — PhoneStoreDB_StockConcurrencyTest — Đăng nhập Admin** |

Connection và thư mục ảnh được đặt trong **config bản sao test + environment riêng của process**; không thay cấu hình global/main. Gắn nhãn cửa sổ bằng handle đúng PID13368 để người dùng phân biệt, không sửa UI source. Chỉ điền email qua EmailInput; không lấy/điền password.

SELECT session thực tế xác nhận PID13368 dùng PhoneStoreDB_StockConcurrencyTest, sleeping/open transaction0. Chưa có authenticated shell/navigation. Không dựa riêng vào tên window để khẳng định đích database.

Checkpoint chuẩn bị: người dùng chọn tự login rồi hỏi mật khẩu test; credential fixture ngẫu nhiên không còn lưu. Lúc đó chưa đổi mật khẩu, không thiết lập AdminSession bằng reflection. Việc reset test sau này được duyệt riêng và ghi tại mục6; main không đổi.

## 3. Phân loại nghiệm thu hiện tại

| Thao tác | Trạng thái / bằng chứng |
|---|---|
| Mở binary D2B mới/cấu hình cách ly/LoginForm | PASS thực tế, PID13368 |
| Login Admin test | PASS thực tế D.2C.1: user tự nhập password, xác nhận login; shell PID13368 hiển thị đúng test Admin, SQL đúng DB cách ly |
| Sai mật khẩu/Customer forbidden/logout qua LoginForm | NOT TESTED mới; tests/service trước đó không thay UI evidence |
| Product thêm/sửa/tìm/ngừng/bật lại qua UI | NOT TESTED D2C; D2A SQL/service đã PASS riêng |
| Brand/Category thêm/sửa/trạng thái qua UI | NOT TESTED D2C; D2A SQL/service đã PASS riêng |
| Upload/thay/primary/order/remove qua OpenFileDialog | NOT TESTED D2C; D2B file/SQL đã PASS riêng |
| Ảnh hỏng/WebP/APNG/thông báo qua UI | NOT TESTED D2C;5input fixture chỉ được chuẩn bị, không gọi upload |
| Tham chiếu/FK/validation qua UI | NOT TESTED D2C; D2A DB evidence không thay thao tác GUI |
| Stale stock qua dialog mới | NOT TESTED D2C; B.1 SQL/dialog và regression có bằng chứng lịch sử |
| Main/DB test/ảnh không thay đổi | PASS checkpoint9/9snapshot mỗiDB +hash40ảnh |

Fixture ảnh gồm JPEG/PNG4×4 thật, JPEG hỏng, WebP sample và PNG có animation-control chunk cho ca từ chối. PNG-control chưa được coi là fixture APNG chuẩn đầy đủ; chưa chạy ca này qua UI nên không công bố PASS.

## 4. Cleanup và hậu kiểm checkpoint

- Chưa tạo Product/Brand/Category/Image/User/Order bằng UI hoặc SQL; không có business fixture rows cần xóa ở giai đoạn này.
- Thư mục uploads test trống. Input ảnh/config/appcopy được giữ để tiếp tục sau login; không xóa hoặc chép vào uploads chính.
- **Chưa đóng PID13368** vì đang chờ người dùng đăng nhập. Bước đóng test/cleanup cuối nhiệm vụ vẫn pending, không tuyên bố đã hoàn tất nghiệm thu.
- Hậu kiểm9/9snapshot main và test khớp trước chuẩn bị (Users projection không credentials). MainWeb60+preserved101 hash nguyên, gồm Admin/config/uploads/40ảnh demo. Main Web19388/Admin10736 responsive.
- Main giữ40Products/40Images/tồn740/1OrderCancelled/1Detail/1Customer/1Adminactive. Không migration/seed/restore/deploy/commit/push hoặc thay mật khẩu.

Evidence trong artifact root: snapshots before/waiting-comparison, main-images-before.json, test-app-manifest.json, waiting-login-ui.json, stdout/stderr test. State `src/PhoneStore.Admin/obj/S29.8D.2C-state.json` ghi PID/config paths không chứa secrets.

## 5. Phương án credential cần duyệt riêng nếu không có mật khẩu cũ

Chưa thực hiện phương án này. Chỉ áp dụng nếu người dùng phê duyệt rõ việc đổi mật khẩu **tài khoản test hiện có**, vì nó thuộc fixture B.1 chứ không phải fixture mới sẽ tạo/dọn ở D2C.

1. Guard connection localhost/DB_NAME=PhoneStoreDB_StockConcurrencyTest/server DESKTOP-ES9362P; xác minh đúng UserId1/email admin@s29-8b1.test/RoleId1/active, không client ghi khác.
2. Người dùng nhập mật khẩu test mới ẩn trong terminal, xác nhận lần hai; không CLI/log/chat/file. Hash bằng PasswordHasher<User> đúng .NET8/hệ thống hiện có.
3. Chỉ đổi **PasswordHash** của đúng row test trên; không đổi email/role/active, không tạo Admin thứ hai, không dùng PhoneStoreDB chính. Sau phê duyệt mới triển khai/chạy công cụ scoped cho thao tác này.
4. Mật khẩu cũ không có để khôi phục; mật khẩu mới trở thành credential test thay thế. Không cam kết reset rồi tự khôi phục credential cũ. Ghi ngoại lệ này riêng khỏi snapshot các trường không credentials.
5. Người dùng tự login trên cửa sổ TEST, rồi tiếp tục ma trận UI từng bước/SELECT, cleanup chỉ fixture mới. Giữ Admin test có sẵn.

Phương án trên đã được duyệt riêng và hoàn tất tại mục6. Không đánh dấu toàn D2C PASS hoặc triển khai main từ bằng chứng login.

## 6. S29.8D.2C.1 — Reset credential test PASS

Ngày2026-11-02, người dùng duyệt riêng đúng UserId1/admin@s29-8b1.test trên PhoneStoreDB_StockConcurrencyTest, không tạo Admin mới. Đã đọc lại AGENTS/báo cáo và kiểm tra SQL thực tế trước nhập.

- Công cụ opt-in trong `tests/PhoneStore.SqlIntegration/TestAdminCredentialChecks.cs`, Main chỉ nhận mode cố định; không nhận server/database/user/email hoặc mật khẩu qua CLI.
- Connection hard-code localhost/testDB/Windows Authentication; TargetGuard kiểm @@SERVERNAME=DESKTOP-ES9362P và DB_NAME thực tế trước đọc/ghi. SELECT bắt buộc đúng UserId1/email so BIN2/RoleId1/RoleNameAdmin/active; sai bất kỳ điều kiện nào thì dừng.
- Build harness riêng `bin/S29.8D.2C.1/` **0warnings/errors**. Preflight sandbox lần đầu không kết nối được, chạy lại SELECT có ngoại lệ SQL hẹp PASS; không có lần UPDATE thất bại hoặc retry.
- Mở terminal tương tác PID10052, không redirect output/input và không ghi transcript. Người dùng nhập mật khẩu hai lần bằng Console.ReadKey(intercept:true),8–128ký tự; mật khẩu/confirmation chỉ trong memory, không CLI/chat/file. Không đọc nội dung mật khẩu từ UI.
- Dùng **PasswordHasher<User>** mặc định như AdminAuthService, kiểm hash trong memory trước ghi. Không tự tạo thuật toán.
- Transaction Serializable, SELECT đúng row với UPDLOCK/HOLDLOCK, rồi UPDATE **chỉ PasswordHash** với WHERE UserId1+emailBIN2+RoleId1+active+roleAdmin. Yêu cầu affected rows=1, nếu không rollback; không có metadata khác cần cập nhật trong schema User hiện có.
- Marker CreateNew chống chạy lại mù; sau commit SELECT chỉ trả boolean hash đã lưu khớp credential mới, không xuất hash. Lỗi/commit không rõ thì SELECT kiểm outcome rồi dừng, không tự UPDATE lần hai.
- Kết quả **CommittedAndVerified, affected rows1**, thời điểm **03:58:54 UTC / 10:58:54 Bangkok**. Không ghi password/hash/token vào evidence.
- Người dùng xác nhận **“Đã đặt mật khẩu và đăng nhập Admin test”**. Kiểm UI đọc đúng PID13368: authenticated shell/navigation hiện, tên SQL Test Admin và email admin@s29-8b1.test. SQL session PID13368 dùng đúng testDB, sleeping/open transaction0.
- Hậu SELECT **03:59:38 UTC / 10:59:38 Bangkok**: UserId1/email/name/RoleId1/active giữ nguyên; main và test **9/9 snapshot không credentials** khớp checkpoint trước. Đây là xác nhận các cột không credentials, không khẳng định PasswordHash test không đổi: thay đổi đó chính là thao tác đã duyệt.
- **40 ảnh main giữ hash**; main Web19388/Admin10736 và test13368 responsive. Main40Products/40Images/tồn740/Order1Cancelled/Detail1/Customer-Admin nguyên theo snapshot.

Evidence mới trong artifact D2C: D2C1-reset-process.json, D2C1-login-verification.json, các `*-D2C1-comparison.json`; status/attempt ở harness bin/S29.8D.2C.1. Status chỉ chứa target/flags/row count/boolean/thời điểm, không chứa hash hoặc mật khẩu. Không xóa attempt marker để chạy lại.

Files D2C.1: thêm TestAdminCredentialChecks.cs; sửa Program.cs/README của harness, báo cáo này và PROGRESS. Không sửa ứng dụng hoặc binary đang chạy. Không chạy lại155tests vì chỉ thêm utility opt-in không nằm trong suite;155/155 ở bước chuẩn bị D2C là baseline trước đó, build utility mới đã PASS.

**Dừng sau reset và xác minh login; không bắt đầu CRUD.** Mật khẩu mới giữ làm credential test, không biết mật khẩu cũ để khôi phục. Chờ chỉ đạo tiếp tục ma trận UI D2C; không migration/seed/restore/mainDML/deploy/commit/push.

## 7. S29.8D.2C.2 — PARTIAL, dừng tại precheck

Kiểm tra ngày 2026-11-02 khoảng **13:35 Asia/Bangkok** sau yêu cầu nghiệm thu CRUD UI:

- Get-CimInstance Win32_Process xác nhận không còn tiến trình PhoneStore; PID test13368, main Admin10736 và Web19388 không còn chạy. Cổng5015 không có listener. Các dotnet còn chạy thuộc VS Code/MSBuild, không phải ứng dụng PhoneStore. Chưa xác định nguyên nhân/thời điểm các ứng dụng thoát.
- File cấu hình bản sao test vẫn trỏ localhost/PhoneStoreDB_StockConcurrencyTest và artifact/uploads riêng; uploads test0file. Không có process hiện hành để xác minh cấu hình runtime hoặc phiên đăng nhập.
- SELECT thực tế: server DESKTOP-ES9362P; cả hai DB ONLINE. Main Products40/Images40/tồn740/Users2/Orders1Cancelled/Details1. Test Products3/Images0/tồn8/Users5/Orders10, trong đó5Cancelled/Details10. Đây là kiểm tra số lượng, không phải đối chiếu đầy đủ mọi cột.
- Đối chiếu40file ảnh demo với main-images-before.json: **40/40 khớp**, không đổi file.
- Theo điều kiện dừng precheck của yêu cầu, **không tự khởi động lại ứng dụng, không tạo fixture, không thao tác CRUD hoặc cleanup**. Không ghi SQL, đổi mật khẩu, sửa source, deploy, migration, seed, restore, commit/push. Không có fixture mới của D.2C.2 cần dọn.

| Phạm vi D.2C.2 | Kết quả |
|---|---|
| Admin test đang chạy/đăng nhập | FAIL điều kiện khởi đầu: tiến trình không còn; không kết luận lỗi chức năng |
| Cấu hình test trên đĩa/ảnh cách ly | PASS kiểm tra tĩnh; runtime chưa xác minh |
| CRUD thương hiệu/danh mục/sản phẩm/ảnh và stale stock qua UI | NOT TESTED ở lượt này |
| Main counts và hash40ảnh | PASS kiểm tra chỉ đọc nêu trên; Admin chính không còn chạy |
| Build/regression | Không chạy lại vì chưa thay source;155/155 là baseline lịch sử |

**Bước tiếp theo:** mở lại đúng binary Admin test với cấu hình/env cách ly và để người dùng đăng nhập bằng credential test đã đặt; xác minh PID mới/session SQL rồi tiếp tục CRUD. Cần xác nhận tiếp tục sau sai lệch precheck này; không suy rằng login PID cũ còn hợp lệ. Nếu muốn khởi động Web/Admin chính, cần chỉ đạo riêng, không dùng main để nghiệm thu ghi.

## 8. S29.8D.2C.3 — Khởi động lại test PASS, chờ đăng nhập

2026-11-02 **13:37–13:38 Asia/Bangkok**: trước mở không có PhoneStore.Admin.exe khác; kiểm **59/59 file** bản sao test khớp test-app-manifest.json, gồm executable/dependencies/config. Không build, thay binary hoặc khởi động main.

- PID mới **5944**, executable `C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8D.2C-20261102_035250536Z\app\PhoneStore.Admin.exe`.
- Config appcopy và environment riêng lúc tạo process cùng pin `localhost/PhoneStoreDB_StockConcurrencyTest`; uploads tuyệt đối `C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8D.2C-20261102_035250536Z\uploads`. AdminServices dùng config/env này để đăng ký storage, không dùng uploads main. Chưa upload nên chưa có bằng chứng file ghi runtime.
- LoginForm thật PID5944 xuất hiện, gắn nhãn TEST và điền admin@s29-8b1.test; không đọc/nhập mật khẩu. Đã yêu cầu người dùng tự nhập mật khẩu hiện có và đăng nhập, chưa thực hiện CRUD.
- SELECT hiện chưa thấy session SQL của PID5944 trước login: **database runtime UNKNOWN**, không khẳng định PASS chỉ từ cấu hình. Phải kiểm session thật sau login trước bất kỳ CRUD nào.
- Chụp9bảng mỗiDB bằng SELECT, Users projection loại PasswordHash, lưu `PhoneStoreDB-D2C3-before.json` và `PhoneStoreDB_StockConcurrencyTest-D2C3-before.json` trong artifact. Guard server DESKTOP-ES9362P và DB_NAME thực tế PASS. 40/40 ảnh main vẫn khớp baseline, uploads test0file.
- Evidence launch: `D2C3-launch.json`; state cập nhật TestAdminPid5944. Login/Dashboard/Products/Brands/Categories đang chờ; **CRUD vẫn PARTIAL/NOT TESTED UI**. Không reset password, SQL ghi, migration, deploy hoặc commit/push. Không chạy lại regression vì không đổi source;155/155 là baseline trước đó.

Sau user xác nhận login: xác minh shell đúng tài khoản, SQL runtime đúng testDB và thư mục ảnh cách ly; đọc các màn hình rồi tiếp tục đúng fixture CRUD đã được duyệt nếu mọi kiểm tra PASS. Nếu UI không chắc chắn, dừng để user thao tác; không dùng SQL ghi thay UI.

### 8.1. Sau xác nhận đăng nhập — login/runtime PASS, CRUD PARTIAL

Ngày2026-11-02 khoảng13:39–13:42Bangkok, người dùng báo đã đăng nhập. UI PID5944 hiển thị **SQL Test Admin / admin@s29-8b1.test**. SELECT sys.dm_exec_sessions xác nhận host_process_id5944 dùng **PhoneStoreDB_StockConcurrencyTest**, sleeping/open_transaction_count0, server DESKTOP-ES9362P. Dashboard hiển thị3Products/tồn8/4Customers/10Orders/1Completed; Products3, Brands1, Categories1 tải được qua NavigationButton1–3.

Ảnh vẫn được pin trong config/env tiến trình và DI storage của binary đã kiểm hash; chưa upload nên chưa có bằng chứng file ghi runtime. Không sử dụng thư mục main. Có lần cửa sổ bị minimized, UIA không thấy controls; chỉ restore đúng handle5944 rồi mới thao tác, không gửi lặp lệnh Lưu.

| Thao tác UI thật | SELECT/kết quả | Đánh giá |
|---|---|---|
| Thêm Brand `S29D2C Brand UI 20261102`, mô tả Fixture CRUD UI D2C.2 | BrandId6, active | PASS |
| Tìm exact tên Brand, mở sửa, đổi thành `S29D2C Brand UI Edited 20261102` | Grid1row, dialog tên cũ khớp, SQL tên mới/active | PASS |
| Blank/duplicate Brand trong dialog thêm, sau đó Hủy | Duplicate chỉ1row; AccessibleName che text status nên chưa đọc được thông báo cụ thể | PARTIAL validation UI, không coi thông báo đã PASS |
| Blank Category trước tạo | Text native: “Vui lòng nhập tên danh mục.” | PASS |
| Tạo `S29D2C Category UI 20261102`, tìm kiếm, sửa thành `S29D2C Category UI Edited 20261102` | CategoryId6, active; Grid1row; SQL khớp tên cũ/mới | PASS |
| Duplicate Category mới cùng tên đã sửa | “Tên danh mục đã tồn tại. Vui lòng chọn tên khác.”; Hủy dialog | PASS |
| Mở AddProduct dialog | Chưa điền/lưu Product; phát hiện cây UIA có cả ProductEditDialog và LookupEditDialog thương hiệu cũ | PARTIAL, dừng vì chưa chắc modal đích |
| Product validation/edit/stale stock, ngừng hoạt động lookups/product, ảnh | Chưa thực hiện ở checkpoint này | NOT TESTED UI |

Các lần ghi thành công đều qua nút Lưu WinForms thật, mỗi thao tác bấm một lần và SELECT sau lưu; không INSERT/UPDATE/DELETE bằng SQL. Chưa xác định dialog thương hiệu cũ là modal thật còn mở hay cây accessibility lưu trạng thái; **không kết luận lỗi source**. Đã yêu cầu user chỉ Hủy/đóng dialog thêm sản phẩm và báo còn dialog thương hiệu phía sau hay không; chưa tự tiếp tục thao tác ghi.

Hậu kiểm13:42: main **9/9 snapshot nguyên**,40/40ảnhhash nguyên. Test7/9bảng nguyên; Brands/Categories chỉ thêm đúng2rowID6 trên, loại2row đó thì snapshot hai bảng còn lại khớp hoàn toàn baseline. Products/Images/Orders/OrderDetails/Users/Roles/history không đổi. Uploads test0file. Không tạo Product/Image/User/Order mới, không reset password.

Fixture được giữ **active**, chưa cleanup do dừng tại thao tác chưa chắc chắn; không xóa cứng hoặc ngừng hoạt động bằng SQL. Giữ PID5944 để user xử lý modal và tiếp tục đúng ID6, không tạo lại fixture trùng. Main Web/Admin vẫn không được mở lại; không thể tuyên bố Admin chính đang hoạt động. Không thay source/binary, không build/test lại;155/155 là baseline trước đó.

Evidence: `*-D2C3-before.json`, `*-D2C3-comparison.json`, `D2C3-fixture-checkpoint.json` trong artifact. Kết luận **PASS khởi động/login/kết nối SQL**, **PARTIAL CRUD**, chờ user phản hồi modal trước khi tiếp tục.

### 8.2. Mở lại sau khi người dùng đóng Admin test — PASS chỉ đọc

2026-11-02 **13:45 Asia/Bangkok**: kiểm Win32_Process xác nhận PID5944 đã thoát, không có Admin instance khác. Đã kiểm lại59/59file manifest và cấu hình, mở đúng executable appcopy cũ, không build/deploy hoặc mở main. PID mới **9840**, đường dẫn executable vẫn `C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8D.2C-20261102_035250536Z\app\PhoneStore.Admin.exe`.

- Khi tạo process pin riêng ConnectionStrings__PhoneStoreConnection=localhost/testDB và ProductImages__UploadsDirectory=`C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8D.2C-20261102_035250536Z\uploads`; file config cùng giá trị, binary/DI đã kiểm hash. Đường dẫn storage cấu hình hiệu lực là thư mục test này; chưa ghi file để thử nên không tuyên bố đã quan sát I/O upload thực tế. Thư mục vẫn0file.
- User tự nhập mật khẩu và xác nhận đăng nhập. UI đọc PID9840 khớp SQL Test Admin/admin@s29-8b1.test; SELECT session thực tế **host_process_id9840 → PhoneStoreDB_StockConcurrencyTest**, sleeping/open_transaction_count0. **PASS login và DB runtime** lúc13:45:42.
- Đã mở màn hình quản lý sản phẩm bằng NavigationButton1, status **3 sản phẩm**; không bấm Thêm hoặc Lưu.
- SELECT BrandId6=`S29D2C Brand UI Edited 20261102` và CategoryId6=`S29D2C Category UI Edited 20261102`, cả hai active. Products chỉ IDs1/2/3 là fixture cũ B.1/C, stock8/0/0; **không có Product mới ngoài dự kiến**. Không reset hoặc tạo Admin.
- Evidence `D2C-reopen.json`, state TestAdminPid9840. Không SQL ghi, cleanup, migration, restore, commit/push. Không sửa source/binary nên không chạy lại build/tests;155/155 vẫn là baseline cũ. CRUD toàn bộ vẫn **PARTIAL**; giữ2lookupfixture và chờ user xác nhận riêng trước khi tiếp tục.

## 9. S29.8D.2C.5 — Product CRUD thật: PARTIAL, các thao tác ghi đã PASS

2026-11-02 **13:47–13:50 Asia/Bangkok**. Đọc AGENTS/PROGRESS/báo cáo; xác minh PID9840 đúng executable test, session SQL thật testDB/open transaction0, config/env ảnh test riêng. Brand6/Category6 active; chưa có Product mang tên dự định. Không có dialog cũ tại precheck. Chụp Products/Brands/Categories trước thử trong `D2C5-before-*.json`.

Mở AddProduct đúng một lần: kiểm theo **ControlType.Window** có đúng1 ProductEditDialog; MenuBar cùng AutomationId là thanh menu của dialog, không tính thành form thứ hai. Không có LookupEditDialog như checkpoint trước.

| Thao tác WinForms thật | Bằng chứng / kết quả | Kết luận |
|---|---|---|
| Điền trước tạo | Tên S29D2C Product UI; chọn tên Brand6/Category6 đối chiếu SQL; Price1230000, Stock5, ActiveOn, mô tả Fixture WinForms D2C.5; đọc lại tất cả trước Save | PASS |
| Bấm Save một lần | SELECT đúng1Product **ID13**, Brand6/Category6/Price1230000/Stock5/IsActive1, mô tả khớp | PASS thực tế |
| Tìm exact fixture rồi mở sửa | Status1sản phẩm/Grid1row; dialog tên đúng fixture | PASS thực tế |
| Sửa tên/giá/mô tả và Save một lần | Tên **S29D2C Product UI Edited**, Price1250000, Description **Edited through WinForms D2C.5**; SQL Stock5, associations6/6 giữ nguyên | PASS thực tế |
| Tìm tên đã sửa | Grid1row | PASS thực tế |
| Nhập Price=-1 và Stock=-1, rời ô nhập, không Save | NumericUpDown đưa về0.00/0; đây là validation/control bound, không phải bằng chứng server nhận và từ chối giá âm | PASS phạm vi UI; server validation vẫn dựa test trước |
| Tên trống, Save một lần | ValidationMessage/nativeText “Vui lòng nhập tên sản phẩm.”; Hủy dialog; SELECT giá1250000/kho5/tên đã sửa nguyên | PASS thực tế, không commit các giá trị thử |
| Ngừng hoạt động | Grid chỉ1fixture, mở ToggleProductActiveButton; kiểm confirmation đúng tên đầy đủ rồi Yes một lần | PASS thực tế |
| SELECT soft delete | ID13 tồn tại, **IsActive=false**, Stock5, Price1250000, Brand6/Category6; không xóa cứng | PASS thực tế |
| Lọc Đang hoạt động/Ngừng hoạt động | Đọc được3option Tất cả/Đang/Ngừng. Khi chọn, helper nhận phần tử không chắc là ListItem và dừng **trước selection/apply**; chưa có kết quả lọc | PARTIAL, không tự retry |
| Ảnh/stale stock mới | Không thực hiện; ảnh cần phê duyệt riêng, stale stock có bằng chứng lịch sử B.1/D2A/B | NOT TESTED ở bước này |

Không thấy ObjectDisposedException hoặc runtime exception ứng dụng trong các thao tác đã xác minh; lỗi **“Uncertain filter”** là guard của helper UIAutomation, không kết luận lỗi bộ lọc của ứng dụng. Sau đó chỉ SELECT hậu kiểm, không tiếp tục UI/Save. Selector trạng thái có thể đang mở; người dùng có thể đóng bằng Esc trước thao tác tiếp theo.

**Hậu kiểm13:50:** Products test loạiID13 khớp toàn bộ snapshot3fixture cũ; Brands/Categories test khớp trước thử, không sửaID6. Product13 có0ảnh, uploads test0file. Main9/9snapshot không credentials khớp D2C3;40Products/40Images/tồn740/1Cancelled/1Customer/1Adminactive,40/40ảnhhash nguyên.

Giữ fixture **ID13 inactive, tồn5** làm bằng chứng; tổng stock test tăng5 hợp lệ từ8→13 do fixture mới, không sửa3sản phẩm cũ. Không cleanup bằng SQL. Không upload ảnh, sửa source, build/test lại hoặc deploy; baseline tự động155/155 là kết quả trước đó. Không migration/seed/restore/commit/push.

Evidence artifact: `D2C5-before-Products.json`, `D2C5-before-Brands.json`, `D2C5-before-Categories.json`, `D2C5-product-created.json`, `D2C5-product-edited.json`, `D2C5-product-final.json`, `D2C5-main-comparison.json`.

**Dừng trước ảnh.** Product create/edit/search/soft-deactivate và required validation PASS thực tế; toàn phạm vi PARTIAL vì filter chưa xác minh. Bước tiếp theo là kiểm filter thủ công trên đúng PID9840/fixture13, rồi xin phê duyệt upload/thay ảnh riêng; không tạo lại sản phẩm.

## 10. S29.8D.2C.6 — Quản lý ảnh UI: PARTIAL, giữ ảnh người dùng vừa thêm

2026-11-02 khoảng **13:52–13:55 Asia/Bangkok**. Người dùng xác nhận ở đầu yêu cầu rằng filter trạng thái D2C.5 đã kiểm thủ công PASS; ghi riêng bằng chứng người dùng, không coi là automation mới.

### Precheck và mở quản lý ảnh — PASS

- PID9840 còn chạy đúng executable test; SQL session thực tế PhoneStoreDB_StockConcurrencyTest, sleeping/open transaction0. File config vẫn đúng DB và uploads riêng; kiểm từng thư mục cha không có ReparsePoint/junction/symlink. Không có Admin instance khác.
- Chụp9bảng mỗiDB không credentials trước thử: `*-D2C6-before.json`; main40ảnhhash nguyên. SELECT Product13 inactive/tồn5/0ảnh; Brand/Category fixture giữ nguyên.
- Ban đầu danh sách0row do filter Đang hoạt động; chuyển **ListItem** đúng Ngừng hoạt động, tìm tên exact trả1row. Nút ManageProductImagesButton enabled; mở ProductImagesDialog đúng tên Product13, status chưa có ảnh. **Không cần tạo thêm Product hoặc kích hoạt lại ID13**.
- Dùng lại đúng2ảnh fixture đã chuẩn bị ngoài ảnh main: artifact/input-images/fixture.jpg4×4/644byte và fixture.png4×4/123byte, cả hai giải mã System.Drawing thành công. Chưa gửi các file này vào ứng dụng.

### Thao tác ảnh quan sát được và điểm dừng

Automation bấm **AddImageButton một lần**, OpenFileDialog thật xuất hiện, có File name input1148. Trước khi helper điền fixture.jpg, control1148 đã không còn; helper báo Missing control và **không bấm Open/không retry upload**. Chỉ đọc UI/SELECT để xác minh outcome.

SELECT thấy **ImageId22/ProductId13**, ImageUrl **`/uploads/products/f228a76df5904468bdc0df8ea5477e0b.png`**, IsPrimary=true, DisplayOrder=0; UI status1ảnh. Người dùng sau đó xác nhận **“tôi vừa thêm ảnh”**, nên đây là thao tác WinForms thủ công của người dùng, không phải upload JPEG tự động thành công.

File thực tế:
`C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8D.2C-20261102_035250536Z\uploads\f228a76df5904468bdc0df8ea5477e0b.png`

- PNG1920×1080,250287byte; giải mã được, RawFormatPNG. **PASS file lưu thực tế đúng uploads test**, chứng minh vị trí lưu runtime khi người dùng thêm ảnh; không chỉ dựa config.
- Không khớp hash PNG fixture4×4 đã chuẩn bị. Người dùng chưa cung cấp tên/đường dẫn nguồn; nội dung/nguồn ảnh và tiêu chí không thông tin cá nhân **UNKNOWN**, không tự suy là ảnh fixture đơn giản. Không mở/ghi nội dung ảnh vào báo cáo.
- Giữ nguyên Image22/file đang được tham chiếu; không thay, unlink hoặc cleanup ảnh này khi nguồn chưa rõ. Không tạo file fixture bổ sung hoặc thử validation sau điểm dừng.

| Ca nghiệm thu D2C.6 | Kết quả |
|---|---|
| Quản lý ảnh Product inactive13 | PASS UI thật |
| Thêm PNG qua người dùng + SQL/file/primary/order/vị trí lưu | PASS thực tế phạm vi đã xác minh; nguồn/nội dung ảnh UNKNOWN |
| Upload JPEG fixture | NOT TESTED UI, chưa bấm Open |
| Thay PNG/primary/unlink và giữ ảnh cũ | NOT TESTED UI |
| JPEG/PNG hỏng, WebP, APNG, >10MiB, dimensions/pixels | NOT TESTED UI lượt này; kết quả tự động D2B trước đó không thay UI evidence |
| Cleanup | Không xóa file referenced; giữ1ảnh/1row hợp lệ, không có file tạm mới do helper |

Hậu kiểm **13:55**: main9/9snapshot khớp trước thử,40Products/40Images/tồn740/1Cancelled/1Customer/1Adminactive;40/40ảnhhash nguyên và tổng file ảnh main vẫn40, không phát sinh ảnh mới main. Test7bảng Roles/Users/Brands/Categories/Orders/OrderDetails/history nguyên; Products chỉ Product13.UpdatedAt đổi từ06:49:19.7593792UTC thành06:54:26.8187171UTC, các cột khác và3fixture cũ nguyên; ProductImages thêmImage22. Uploads test1filePNG. Không SQL ghi thay UI hoặc thay source/build/deploy;155/155 là baseline trước đó, không chạy lại.

Evidence: `*-D2C6-before.json`, `*-D2C6-comparison.json`, `D2C6-file-checkpoint.json`. **Kết luận PARTIAL**. Dừng vì OpenFileDialog thay đổi khi người dùng thao tác; cần xác định file nguồn và thống nhất dùng ảnh fixture chuẩn trước khi tiếp tục JPEG/thay ảnh/validation. Không tự triển khai Admin mới, migration/seed/restore hoặc commit/push.

## 11. S29.8D.2C.6A — Fixture chuẩn và WinForms validation

2026-11-02 **13:57–14:03 Asia/Bangkok**. Kết luận **PARTIAL toàn phạm vi**, **PASS thực tế thêm JPEG/PNG và6ca từ chối**. Không thay/xóa ảnh, không triển khai main.

### A. Nguồn Image22 — UNKNOWN, file đích VERIFIED

Guard xác minh PID9840 đúng binary test/config, SQL runtime PhoneStoreDB_StockConcurrencyTest. Image22 vẫn thuộc Product13, ImageUrl `/uploads/products/f228a76df5904468bdc0df8ea5477e0b.png`, primary=true/order0. ProductImages chỉ có5cột ImageId/ProductId/ImageUrl/IsPrimary/DisplayOrder; **không lưu original filename/source path/CreatedAt**. Thuộc tính CreatedAt=null trong JSON projection utility không phải metadata cột database.

- File đích trong uploads test: PNG1920×1080/250287bytes, giải mã được.
- SHA-256 **6B8F576C67A6BE2F7AEB6A0A73320071D82EF69332DF3FFCB7DCB0071A57D373**.
- Không khớp hash file nào trong artifact/input-images đã chuẩn bị. OpenFileDialog đang hiện tên `Screenshot (1)`; chỉ là dấu vết tên hiện tại, không chứng minh đường dẫn hoặc nội dung nguồn của lần upload22. Không quét thư mục cá nhân không liên quan hoặc suy nguồn từ tên.
- Không sửa/unlink/đổi primary hoặc xóa Image22. Hậu kiểm row và hash file22 giữ nguyên.

### B. Manifest fixture mới

Root tuyệt đối:
`C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8D.2C-20261102_035250536Z\D2C6A-fixtures-593bd0a3772c4af9950df4cc97e18053`

**manifest.json** trong root chứa từng path tuyệt đối, extension, bytes, dimensions, kết quả giải mã System.Drawing, SHA-256 đầy đủ và expected accept/reject. Tên thư mục GUID mới, không ghi đè fixture cũ. JPEG/PNG chuẩn24×24 chỉ hình chữ nhật màu tự tạo, không nội dung cá nhân. Không dùng ảnh main hoặc ảnh22 để tạo fixture.

| Tên file trong root | Bytes | Dimensions | Expected / căn cứ |
|---|---:|---|---|
| accept-standard.jpg |708|24×24|ACCEPT, giải mã được |
| accept-standard.png |173|24×24|ACCEPT, giải mã được |
| reject-truncated.jpg |40|không decode|REJECT, cắt container JPEG |
| reject-truncated.png |40|không decode|REJECT, cắt container PNG |
| reject-valid.webp |38|fixture1×1 lịch sửD2B|REJECT, WebP không hỗ trợ; copy đúng fixture đã có browser decode evidenceD2B, không tuyên bố System.Drawing decode đượcWebP |
| reject-animation.png |231|24×24|REJECT, APNG1frame với acTL/fcTL đúng CRC trướcIDAT; frame giải mã được |
| reject-over10MiB.jpg |10485761|24×24|REJECT, JPEG có dữ liệu padding tới10MiB+1; kiểm dung lượng, không phải ca chỉ vượt size nhưng strict-container hoàn toàn hợp lệ |
| reject-width4097.png |146|4097×1|REJECT, PNG giải mã được nhưng vượt chiều4096; không tạo bitmap16triệu pixel không cần thiết |

Giới hạn tổng16triệu pixel chưa nghiệm thu UI riêng lần này; nguồn/testD2B đã có ca4001×4000, không đồng nhất với UI evidence. Fixture WebP/APNG được giữ rõ nguồn tạo/giới hạn kiểm chứng trong bảng.

### C. WinForms thật — lựa chọn chính xác và gửi một lần

User chọn JPEG chuẩn theo hướng dẫn, automation đọc cả2control file-name đều khớp tuyệt đối. Guard lúc chuẩn bị Open không thấy SQL sessionPID9840 (UNKNOWN tạm thời; không kết luận saiDB). **Không gửi file khi guard chưa PASS**: hủy file selector, đóng dialog ảnh, refresh Products chỉ đọc; SELECT xác nhận lại session9840→testDB/sleeping/openTx0. Mở lại ảnh/selector, điền path JPEG bằng Edit1148 duy nhất, đọc lại exactpath và hashmanifest; nútOpen native classButton/AutomationId1 được xác định trong đúng file dialog. Parent ImageDialog và file selector là luồng modal cha/con; không có thêm ProductEdit/LookupEditDialog không liên quan.

Mỗi ca sau đều kiểm guard/path/hash trước **một lần Open**; Open là thao tác gửi file của UI hiện có, không có nútSave ảnh riêng. Không retry request lỗi hoặc kết quả commit không rõ.

| Ca UI | SELECT / file / thông báo | Kết quả |
|---|---|---|
| Thêm JPEG accept-standard.jpg | Image23/Product13, primary=false/order1; file đích byte/hash exactmanifest | PASS thực tế |
| Thêm PNG accept-standard.png | Image24/Product13, primary=false/order2; file đích byte/hash exactmanifest | PASS thực tế |
| reject-truncated.jpg | Thông báo từ chối; Product13/ảnh/3file vàhash nguyên | PASS thực tế |
| reject-truncated.png | Như trên, kiểm riêng sau ca | PASS thực tế |
| reject-valid.webp | Như trên; thông báo nêu WebP chưa hỗ trợ | PASS thực tế |
| reject-animation.png | Như trên; thông báo policy chung, không nêu APNG riêng | PASS thực tế từ chối; thông báo chi tiết PARTIAL |
| reject-over10MiB.jpg | Như trên; không tạo file/row | PASS thực tế từ chối fixture; không cô lập duy nhất lý do size vì có padding |
| reject-width4097.png | Như trên; không tạo file/row | PASS thực tế |

Thông báo native/UI của6ca: “Không thể thêm ảnh. Chỉ tải lên JPEG/PNG giải mã được, tối đa 10 MB, mỗi chiều 4096 và 16 triệu điểm ảnh. WebP chưa được hỗ trợ. Kiểm tra tệp và thư mục lưu ảnh.” Không có stack trace/ObjectDisposedException mới quan sát được trong các ca đã chạy; không suy mọi lỗi runtime đều được phủ.

### D. Ảnh thêm / ảnh thay / bảo toàn

Tất cả file dưới uploads test tuyệt đối:
`C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8D.2C-20261102_035250536Z\uploads`

| ImageId / ProductId | File trong uploads test | SHA-256 | Thao tác |
|---|---|---|---|
|22 /13|f228a76df5904468bdc0df8ea5477e0b.png|6B8F576C67A6BE2F7AEB6A0A73320071D82EF69332DF3FFCB7DCB0071A57D373|Giữ nguyên row/file, primary/order0 |
|23 /13|f6c5c92593d8466b98cc25b0e6a9003b.jpg|00EA5F065FD00386ABA59BC166079324750DA600D695F423731A80307D619147|Thêm JPEG chuẩn mới, primaryfalse/order1 |
|24 /13|249827219a56454bb7c26b075378a19b.png|E462BAF47DD2318F0A3FC3F38630B3D997B4A10F3A1E4641DCC483024E18D4FD|Thêm PNG chuẩn mới, primaryfalse/order2 |

**Chưa có ảnh nào được thay.** Source/UI hiện có Add/SetPrimary/Remove, không có thao tác Replace trực tiếp; thay đầy đủ là thêm mới→chọn primary→bỏ liên kết ảnh cũ. Điều kiện người dùng yêu cầu dừng trước xóa ảnh/fixture khác và giữ22 được tôn trọng: không bấmSetPrimary hayRemove; không đồng nhất Add với Replace. Bước nghiệm thu primary/unlink/cleanup còn PARTIAL, cần duyệt riêng thao tác cụ thể lên fixture23/24 và ảnh22 nếu bị ảnh hưởng. Không tự cleanup các row/file đã lưu hoặc fixture đầu vào.

### E. Hậu kiểm và điểm tiếp tục

**14:03:29 Bangkok**: main ONLINE,9/9snapshot không credentials khớp trước nhiệm vụ;40Products/40Images/tồn740/1OrderCancelled/1Customer/1Adminactive. 40ảnh nguyênhash, tổng file ảnhmain vẫn40. Test7bảng Roles/Users/Brands/Categories/Orders/OrderDetails/history nguyên; Products chỉ Product13.UpdatedAt thay đổi, các field nghiệp vụ và3fixture cũ không đổi. ProductImages loại23/24 khớp baseline hoàn toàn, gồm22. Uploads test3file, tất cả giải mã được; không file invalid/.tmp phát sinh qua6ca.

Evidence artifact: `D2C6A-image22.json`, `D2C6A-fixture-directory.txt`, manifest8file, `*-D2C6A-before.json`, `D2C6A-after-jpeg.json`, `D2C6A-after-png.json`, `D2C6A-case-*.json`, `D2C6A-final-files.json`, `*-D2C6A-final-comparison.json`.

Không sửa source ứng dụng, không build/test lại;155/155 là baseline lịch sử. Không SQLghi thay UI, không mainwrite/migration/restore/deploy/commit/push. Giữ testPID9840 và3ảnh làm bằng chứng. **Dừng trước xóa/bỏ liên kết/thay hoàn chỉnh hoặc triển khai Admin chính**; nguồn22 vẫn UNKNOWN, cần user cung cấp file nguồn nếu muốn chốt nguồn.

## 12. S29.8D.2C.6B — Precheck PASS, chờ unlink thủ công

2026-11-02 **14:05–14:07 Asia/Bangkok**. PID9840 đúng binary test/config/runtime SQLtestDB. Manifest8fixture nguyên hash, uploads không reparsepoint; Product13 inactive/1250000/tồn5, ảnh22primary/order0,23nonprimary/order1,24nonprimary/order2. MỗiURL có tổng1row tham chiếu, không rowkhác tham chiếu. Hash3file khớp6A, xem `D2C6B-preflight-images.json` và `D2C6B-preflight-files.json` trong artifact.

Đã kiểm source và UI thật: có AddImageButton, SetPrimaryImageButton, RemoveImageButton và ApplyImageOrderButton. **Thay file trực tiếp NOT SUPPORTED**, không có nút/service replace-file; không đánh đồng Add hoặc SetPrimary với thay file. Source NormalizePrimary khi chọn23/24 sẽ đổi22.IsPrimary thànhfalse, nên không được chạy trong điều kiện giữ nguyên row22. RemoveImageAsync commit bỏrowliênkết rồi trả notice giữ file, không tự xóa published file; cleanup physical tự động không được thực hiện theo chính sách hiện có.

Đã trình hai xung đột precondition và user xác nhận riêng: **“Đồng ý chỉ bỏ liên kết23, giữ file và ảnh22/24”**. Phạm vi đã làm rõ: trước unlink23 kiểm không có rowKHÁC tham chiếu; sau unlink phải0tham chiếu; khôngxóafile tay, khôngđổiprimary22. Không còn yêu cầu0tổngthamchiếu trước unlink vì row23 chính là liên kết sẽ bỏ.

UIAutomation đọc được cellID23/URL/giá trị bằng ValuePattern; thử kiểm Legacy pattern bị helper TypeNotFound trước thao tác, sau đó SelectionItemPattern của cell không hỗ trợ. **Không chọn hàng, không bấmRemove/confirmation bằng automation**; đây là hạn chế helper/UIA, không phải lỗi runtime WinForms hoặc bằng chứng unlinkPASS. Đã SELECT lại lúc14:07:24: row22/23/24 vàhash nguyên, OtherRefs0. Yêu cầu user chọn đúng23 theoID+filename rồi Xóa ảnh/Yes đúng1lần, báo thông báo UI; chờ phản hồi trước khi kiểm outcome, khôngretry.

Main lúc14:06 ONLINE,9/9snapshot khớp6A;40Products/40Images/740/1Cancelled/1Adminactive/1Customer,40ảnhhashnguyên. Snapshot9bảng mỗiDB tại `*-D2C6B-before.json`. Chưa ghi database hoặc file ảnh trong checkpoint này; khôngbuild/test lại/source/migration/deploy/commit/push.

Trạng thái hiện tại: **PARTIAL**, unlink23 pending thao tác người dùng; primary đổi bị giới hạn giữ22, replace-file NOT SUPPORTED; khôngcleanup physical hoặc triển khai main. Sau user phản hồi phải SELECT kiểm row23mất/URL0refs,22/24 nguyên,3file/hash nguyên vànoticefilegiữ, rồi đối chiếu snapshotmain/test trước khi kết luận.

### 12.1. Sau người dùng bỏ liên kết23 — PASS thực tế

Người dùng xác nhận **“đã bỏ liên kết23”** trên WinForms thật. Hậu kiểm chỉ đọc **2026-11-02 14:08:56–14:09:14 Asia/Bangkok**:

- SELECT **ImageId23 không còn**, URL `/uploads/products/f6c5c92593d8466b98cc25b0e6a9003b.jpg` có **0row tham chiếu trong toàn ProductImages**, không chỉ Product13.
- UI ImageStatusLabel: **“Đã bỏ liên kết ảnh; tệp được giữ để tránh xóa nhầm khi có thao tác đồng thời. Cần đối chiếu liên kết trước khi dọn tệp: /uploads/products/f6c5c92593d8466b98cc25b0e6a9003b.jpg”**.
- Image22 giữ nguyên5cột: Product13/URLcũ/primarytrue/order0. Image24 giữ nguyên5cột: Product13/URLcũ/primaryfalse/order2. Đã kiểm từng cột với snapshot trước, lưu `D2C6B-surviving-image-columns.json`; không chỉ dựa serialization cả mảng JSON. Product13 còn2ảnh/đúng1primary; không tự renumberorder2 thành1.
- Ba file vẫn tồn tại, byte/hash khớp toàn bộ `D2C6B-preflight-files.json`. **Không file nào bị thay/xóa**:

| Image / file trong uploads test | Trạng thái SQL sau UI | SHA-256 trước = sau |
|---|---|---|
|22 / f228a76df5904468bdc0df8ea5477e0b.png|Row nguyên, primarytrue/order0,1thamchiếu|6B8F576C67A6BE2F7AEB6A0A73320071D82EF69332DF3FFCB7DCB0071A57D373|
|23 / f6c5c92593d8466b98cc25b0e6a9003b.jpg|Row đã bỏ,0thamchiếu; file giữ|00EA5F065FD00386ABA59BC166079324750DA600D695F423731A80307D619147|
|24 / 249827219a56454bb7c26b075378a19b.png|Row nguyên, primaryfalse/order2,1thamchiếu|E462BAF47DD2318F0A3FC3F38630B3D997B4A10F3A1E4641DCC483024E18D4FD|

Root file vẫn là `C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8D.2C-20261102_035250536Z\uploads`. FileJPEG23 là **orphan có chủ đích đã đối soát**, giữ theo chính sách/concurrency notice và phê duyệt; không phải lỗi commit chưa rõ. Không tự xóa tay hoặc cleanup dữ liệu đã lưu.

| Phạm vi nghiệm thu | Kết quả cuối |
|---|---|
| Bỏ liên kết23 qua WinForms thật | **PASS** người dùng thao tác + SELECT + noticeUI |
| Giữ file sau unlink, thông báo cần đối soát | **PASS** đúng chính sách hiện tại |
| Không xóa ảnh/file còn tham chiếu22/24 | **PASS** row/file/hash nguyên |
| Chọn primary23/24 | **PARTIAL/không thực hiện**, sẽ đổi22.IsPrimary trái giới hạn; đã được user đồng ý bỏ qua khi làm rõ phạm vi |
| Thay file ảnh trực tiếp | **NOT SUPPORTED**, không có UI/service replace-file; không giả lập SQL |
| Xóa file orphan vật lý | Không thực hiện, không tự cleanup; file23 giữ để chờ chỉ đạo |

Hậu so sánh9bảng main **9/9 nguyên**,40Products/40Images/tồn740/Orders1Cancelled/Users2 gồm1Customer+1Adminactive;40ảnhmain nguyênhash. Test chỉ bỏ rowImage23 và đổi Product13.UpdatedAt,7bảng khác nguyên; các field nghiệp vụProduct13 và3fixture cũ không đổi, Brands/Categories6không đổi. Evidence `*-D2C6B-final-comparison.json` và kiểm cột ảnh còn lại. PID9840 vẫn đúng executable test.

**S29.8D.2C.6B tổng PARTIAL** vì chưa nghiệm thu đổi primary do bảo vệ22; unlink/retention đã PASS, replace-file không thuộc chức năng UI hiện có. Không phát hiện lỗi mới trong thao tác đã đối chiếu; không sửa source/build/test lại,155/155 là baseline trước. Không SQLghi thay UI/mainwrite/migration/restore/deploy/commit/push. **Dừng chờ chỉ đạo, không triển khai Admin chính.**

## 13. S29.8D.2C.6C — Primary cách ly và tổng kết CRUD

2026-11-02 **14:11–14:14 Asia/Bangkok**, checkpoint chờ người dùng đặt primary. Đã đọc hướng dẫn/tiến độ/báo cáo; PID9840 vẫn đúng appcopy test, SQL runtime testDB và uploads riêng. Product13 inactive/giá1250000/tồn5; Image22primary/order0, Image24nonprimary/order2, mỗi file1thamchiếu; fileJPEG23 vẫn còn, hash nguyên. Source NormalizePrimary xác nhận đổi primary13 sẽ đổi22, nên **không ghi lên Product13 hoặc Image22/24**.

Tạo đúng **1Product mới ID14** qua AddProduct dialog thật, chỉ1modal; tên **S29D2C Primary UI 6C**, Brand6/Category6, active, Price100000, Stock1, mô tả Isolated primary acceptance fixture. Đọc lại tên/lookup/numeric/active trước Save1lần, SELECT khớp. Không sửa3fixture cũ hoặc lookup6.

Tìm exact tên, filterActive1row, mở ảnh đúngProduct14/chưaảnh; upload từng JPEG/PNG manifest6A với path/hash verified và Open1lần. Không có lỗi commit/timeout hoặc retry:

| Image / Product | URL | Primary trước đổi | Order | Bytes / dimensions | SHA-256 |
|---|---|---|---:|---|---|
|25 /14|/uploads/products/be7b667388e74ddbbd23537cbaf200f3.jpg|true|0|708 /24×24|00EA5F065FD00386ABA59BC166079324750DA600D695F423731A80307D619147|
|26 /14|/uploads/products/86e9342714ee4bc083075367332e66f1.png|false|1|173 /24×24|E462BAF47DD2318F0A3FC3F38630B3D997B4A10F3A1E4641DCC483024E18D4FD|

File dưới uploads test đã ghi trong các mục trước, hash khớp `D2C6A-fixtures-593bd0a3772c4af9950df4cc97e18053/manifest.json`. UI hiện chỉ1ProductImagesDialog, SetPrimary enabled; Grid SelectionPattern không hỗ trợ nên **không tự bấm primary khi chưa xác minh được lựa chọn**. Đã yêu cầu user chọn đúng26/filePNG trênProduct14 và bấm Đặt ảnh chính1lần, chờ xác nhận rồi SELECT trước khi kết luận PASS.

Evidence mới: `*-D2C6C-before.json`, `D2C6C-created-product.json`, `D2C6C-after-jpeg.json`, `D2C6C-before-primary.json`, `D2C6C-primary-fixture-files.json`. Haiảnh mới được giữ, không cleanup. Source/build/tests không thay đổi;155/155 là baseline cũ.

### Tổng hợp bằng chứng CRUD hiện có

| Nhóm | PASS WinForms thực tế | PASS source/test cách ly | Tồn đọng |
|---|---|---|---|
| Brand | Thêm/sửa/tìm Brand6, SELECT khớp | SQLite/SQL create/edit/CIduplicate/validation/softdeactivate/reference/errorhandlingD2A | Ngừng/bật lại khi referenced chưa UI; text lỗi blank/duplicateBrand chưa ghi đủ → PARTIAL |
| Category | Thêm/sửa/tìm Category6; required và duplicate có thông báo thật | SQLite/SQL lookup/reference/deactivateD2A | Ngừng/bật lại khi referenced chưa UI → PARTIAL |
| Product | Product13 create/edit/search/softdeactivate/required/negativecontrol; filter user xác nhận | Atomic originalstock/regression/SQL/dialog B.1; transaction/referenceD2A | Không suy đã kiểm mọi field/filter; kích hoạt lại và đóng form giữa save chưa UI ởD2C; chức năng đã thực hiện PASS phạm vi |
| Ảnh | AddJPEG/PNG/hash/6reject/unlink23+retentionnotice PASS; ảnhinactive hỗ trợ; primary26 trênProduct14 UIuser+SQL PASS14:16 | D2B decode/size/pixels/APNG/concurrency/rollback/sharedreference | Displayorder edit chưa UI; tổngpixel16triệu chỉ test trước; nguồn22UNKNOWN |
| Thay file trực tiếp | NOT SUPPORTED | UI/service không replace-file | Không tự bổ sung; Add/SetPrimary/Remove là các thao tác riêng |

Nguồn ảnh22 vẫn UNKNOWN, được giữ nguyên theo yêu cầu; JPEG23 orphan có chủ đích. Không coi giữ orphan là cleanup physical đã thực hiện; không xóa chúng để làm test PASS. Soft delete là nghiệp vụ hiện có, không phải thiếu hard-delete. **Toàn Admin CRUD vẫn PARTIAL**, không tự đóng mọi ca UI chưa nghiệm thu hoặc triển khai main.

Hậu kiểm bảo vệ lúc **14:15:42Bangkok**, trong khi chờ user phản hồi: main9/9snapshot và40ảnhhash nguyên; Products/ProductImages test loạiProduct14 khớp baseline hoàn toàn, gồmProduct13/Image22/24; file22/24/orphan23hashnguyên,URLorphan23vẫn0refs. Product14 có1primary nhưng chưa khẳng định đã đổi sang26 khi chưa đối chiếu kết quả thao tác user. Evidence `D2C6C-main-comparison.json`. Không ghi thêm hoặc xóa fixture; chờ xác nhận để kiểm SQL25false/26true rồi cập nhật kết luận primary.

### 13.1. Primary đã nghiệm thu PASS — kết quả cuối S29.8D.2C.6C

Người dùng xác nhận **“đã đặt26 làm ảnh chính”** trên WinForms thật. SELECT kiểm **2026-11-02 14:16:34 Asia/Bangkok** đúnglocalhost/DESKTOP-ES9362P/PhoneStoreDB_StockConcurrencyTest:

| ImageId / ProductId | Primary trước → sau | DisplayOrder trước → sau | URL/file/hash |
|---|---|---|---|
|25 /14|true → **false**|0 →0|be7b667388e74ddbbd23537cbaf200f3.jpg, SHA00EA5F065FD00386ABA59BC166079324750DA600D695F423731A80307D619147, nguyên |
|26 /14|false → **true**|1 →1|86e9342714ee4bc083075367332e66f1.png, SHAE462BAF47DD2318F0A3FC3F38630B3D997B4A10F3A1E4641DCC483024E18D4FD, nguyên |

Product14 có đúng2ảnh và **đúng1primary là26**; không yêu cầu hoặc tự sửaDisplayOrderprimary thành0 vì đây là hai thuộc tính riêng trong nghiệp vụ hiện có. Kết quả là **đổi primary**, không thay file ảnh. Haiảnh25/26 nằm trong uploads test đã nêu, khớp manifest chuẩn; không ghi đè hoặc xóa file.

- Products vàProductImages test loạiProduct14 khớp baseline hoàn toàn: Product13/ảnh22/24 và3fixture cũ giữ nguyên. Bảy bảng Roles/Users/Brands/Categories/Orders/OrderDetails/history test nguyên, lookup6 không sửa.
- 5filetest giữ nguyênhash:25/26 +22/24 +JPEGorphan23. Orphan23 vẫn0thamchiếu, không bị xóa; nguồn22 vẫnUNKNOWN và không bị tác động.
- Main **9/9snapshot nguyên**,40ảnhhash nguyên; counts40Products/40Images/740/1Cancelled/2Users(1Customer/1Adminactive) giữ từsnapshot đã đối chiếu, không ghi main.
- Evidence `D2C6C-after-primary.json`, `D2C6C-main-final-comparison.json`, `D2C6C-primary-fixture-files.json`; không automationretry thao tác ghi, user tự bấm primary đúngfixture đã chỉ định.

**S29.8D.2C.6C PASS trong phạm vi nghiệm thu primary cách ly và tổng kết**. **Tổng Admin CRUD vẫn PARTIAL**: Brand/Category ngừng-bật lại cóSQL/testPASS nhưng chưaWinForms; thông báo validationBrand chưa đủUIevidence; imageDisplayOrder edit và đóng form khi save chưaUI. ProductCRUD vàimageadd/6reject/unlink/primary đạt ở các ca thực tế đã thử; không suy mọi trường hợp đềuPASS. Thayfiletrựctiếp NOT SUPPORTED, không coi là lỗi hoặc tự thêmchức năng.

Giữ Product14active/ảnh25-26 và các fixture có sẵn làm bằng chứng; stocktest tăng1 hợp lệ doProduct14. Không tựcleanup, sửa source, build/test lại(155/155baselinecũ), deploy, migration, restore, commit/push. **DỪNG trước triển khai Admin chính**, đề xuất tiếp theo nghiệm thu ngừng/bật lạiBrand/Category cách ly để đóng các ca UI còn thiếu.

## 14. S29.8D.2C.7 — Brand/Category ngừng và bật lại PASS

Ngày2026-11-02 **14:18–14:21 Asia/Bangkok**. PID9840 đúng binary test. Khi session SQL idle biến mất trước thao tác đầu, helper không ghi; đóng dialog ảnh/chuyển Brand/làm mới chỉ đọc rồi guard lại session9840→PhoneStoreDB_StockConcurrencyTest trước confirmation. Config uploads test riêng giữ nguyên.

Source/service/store xác nhận SetActiveAsync chỉ đổi `IsActive` của đúng Brand/Category rồi SaveChanges; không sửa/cascade Product/Image. SQL testDB không có DML trigger enabled trên Brands/Categories. Precheck: Brand6/Category6 active, mỗi lookup có2Productrefs là13/14. UI confirmation ghi rõ sản phẩm liên quan và lịch sử được giữ.

| Thao tác WinForms thật | Bằng chứng UI/SQL | Kết quả |
|---|---|---|
| Tìm Brand6 | Grid1row, ID6/tên/mô tả/Đang hoạt động khớp | PASS |
| Ngừng Brand6 | Confirmation đúng tên+nêu giữ liên quan, Yes1lần; SELECT false | PASS |
| Bật lại Brand6 | Search1row, nút/confirmation Kích hoạt lại đúng tên; SELECT true | PASS |
| Tìm Category6 | Grid1row, ID6/tên/mô tả/Đang hoạt động khớp | PASS |
| Ngừng Category6 | Confirmation đúng tên+nêu giữ liên quan, Yes1lần; SELECT false | PASS |
| Bật lại Category6 | Search1row, confirmation đúng tên; SELECT true | PASS |
| Bộ lọc trạng thái Lookup | UI chỉ có tìm theo tên, không có status filter riêng | NOT SUPPORTED |

Hậu kiểm: Brand6 vàCategory6 cuối cùng active=true, mỗi lookup2refs. Product13 vẫn inactive/giá1250000/tồn5; Product14 active/giá100000/tồn1. Ảnh22primary/24secondary của13 và25secondary/26primary của14 giữ nguyên ImageId/ProductId/URL/primary/order. JPEGorphan23 vẫn0refs. Snapshot test trước/sau **9/9khớp** vì trạng thái lookup được khôi phục;5filetest hash nguyên.

Main ONLINE và **9/9snapshot khớp**;40Products/40Images/tồn740/1Cancelled/1Adminactive/1Customer,40ảnhmain hash nguyên. Evidence: `PhoneStoreDB*-D2C7-before.json`, `D2C7-files-before.json`, `D2C7-products-after-brand.json`, `D2C7-test-final-comparison.json`, `D2C7-main-final-comparison.json`.

### Kết luận Admin CRUD D2C

**S29.8D.2C.7 PASS. Core Admin CRUD WinForms trong phạm vi nghiệp vụ hiện có đã nghiệm thu thực tế:** Brand/Category add/edit/search/deactivate/reactivate; Product create/edit/search/filter/soft-deactivate/validation cơ bản; image add JPEG/PNG, invalid rejection, primary, unlink và retention notice. SQL/service/regression cách ly bổ sung concurrency, rollback, FK/reference và decoder validation.

**Toàn báo cáo D2C vẫn PARTIAL theo chuẩn phân loại nghiêm ngặt** vì các ca phụ chưa có UI evidence: sửa DisplayOrder, đóng form trong khi save, và thông báo blank/duplicate Brand được ghi đầy đủ. Replace-file trực tiếp **NOT SUPPORTED**, không phải FAIL; nguồn Image22 **UNKNOWN** và giữ nguyên. Core CRUD acceptance có thể đóng; giữ các tồn đọng phụ trong backlog.

Không sửa source ở D2C.7 nên không build/test lại; **155/155** là baseline gần nhất, không phải kết quả mới. Không SQLghi thay UI/mainwrite/migration/restore/seed/deploy/commit/push. Đề xuất chuyển S29.8D.3 sau phê duyệt riêng; chưa triển khai Admin chính.
