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
