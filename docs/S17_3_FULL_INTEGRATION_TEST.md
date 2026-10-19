# S17.3 — Full System Integration Test

Ngày kiểm tra: **20/10/2026**  
Phạm vi: tích hợp Customer Web + Admin WinForms + SQL Server `PhoneStoreDB`.

## 1. Environment & Build

- Windows, .NET SDK 8.0.319, .NET 8, EF Core 8.0.28.
- SQL Server mục tiêu: `taiiiii\SERVER`, database `PhoneStoreDB`, Windows Authentication.
- `MSSQL$SERVER`: Running, Automatic.
- Web và Admin cùng dùng key `PhoneStoreConnection` và cùng connection string tới `taiiiii\SERVER/PhoneStoreDB`.
- Không đổi connection string/TLS, không sửa UI/source/schema/migration và không ghi dữ liệu thật.
- Không có SQL test database riêng được cấu hình và xác minh cách ly. Các test ghi hiện có dùng SQLite in-memory.

Kết quả lệnh trong S17.3:

```text
dotnet restore PhoneStore.sln
  PASS — all projects up-to-date

dotnet build PhoneStore.sln --no-restore
  PASS — 0 warnings, 0 errors

dotnet test PhoneStore.sln --no-restore --no-build
  PASS — 26 passed, 0 failed, 0 skipped
```

Migration trong repository:

- `20261001150857_InitialCreate.cs`
- `20261001150857_InitialCreate.Designer.cs`
- `PhoneStoreDbContextModelSnapshot.cs`

Không có migration mới. `dotnet ef migrations list` chưa chạy vì local tool `dotnet-ef` chưa được restore; không tự cài/đổi môi trường trong S17.3.

### Khả năng truy cập ứng dụng

- Admin: S17.2 ghi nhận người dùng đã mở Admin, kết nối `PhoneStoreDB` và hoàn thành A01–A10. Không chạy lại GUI Admin trong S17.3.
- Web: blocker Windows Event Log đã được xử lý trong S17.3.1 bằng logging Console + Debug chỉ ở Development. Retest Release: `/account/login` và `/images/product-placeholder.svg` trả HTTP 200; `/` và `/products` đi tới truy vấn EF rồi trả HTTP 500 do blocker TLS SQL còn lại.
- SQL client: `sqlcmd` thất bại với `Encryption not supported on the client` và `SSL Provider: No credentials are available in the security package`.

## 2. Automated Test Results

| Nhóm | Tổng | Passed | Failed | Môi trường | Giá trị bằng chứng |
|---|---:|---:|---:|---|---|
| Authentication | Theo suite 26 test | PASS | 0 | SQLite in-memory | Quy tắc hash/role/active; không phải SQL Server integration |
| Checkout | Theo suite 26 test | PASS | 0 | SQLite in-memory | UnitPrice, total, stock, rollback; không phải live Web/Admin |
| Order ownership/workflow | Theo suite 26 test | PASS | 0 | SQLite in-memory | Ownership, transition, restore stock; không phải `PhoneStoreDB` |
| Toàn solution | 26 | 26 | 0 | Isolated | Regression PASS; không được dùng để nâng INT01–INT08 thành PASS |

`TestDatabase` dùng `Data Source=:memory:`, gọi `EnsureCreatedAsync` và seed dữ liệu test riêng trong memory. Không test nào ghi vào `PhoneStoreDB`.

## 3. Database Baseline

Truy vấn chỉ đọc đã thử:

```sql
SELECT COUNT(*) FROM Roles;
SELECT COUNT(*) FROM Users;
SELECT COUNT(*) FROM Brands;
SELECT COUNT(*) FROM Categories;
SELECT COUNT(*) FROM Products;
SELECT COUNT(*) FROM ProductImages;
SELECT COUNT(*) FROM Orders;
SELECT COUNT(*) FROM OrderDetails;
```

Client không mở được kết nối do lỗi TLS/Schannel nêu trên. Không đổi connection string hoặc TLS để lách lỗi.

| Table | Baseline S17.3 | Nguồn |
|---|---:|---|
| Roles | BLOCKED | `sqlcmd` TLS error |
| Users | BLOCKED | `sqlcmd` TLS error |
| Brands | BLOCKED | `sqlcmd` TLS error |
| Categories | BLOCKED | `sqlcmd` TLS error |
| Products | BLOCKED | `sqlcmd` TLS error |
| ProductImages | BLOCKED | `sqlcmd` TLS error |
| Orders | BLOCKED | `sqlcmd` TLS error |
| OrderDetails | BLOCKED | `sqlcmd` TLS error |

Mốc S17 trước đây (41 Products, 42 ProductImages, 1 Customer, 1 Completed order) chỉ là bằng chứng lịch sử, không được ghi thành baseline hiện tại.

## 4. Integration Scenarios INT01–INT08

Quy ước: **PASS** chỉ khi kịch bản tích hợp thực đã chạy trong S17.3; **BLOCKED** khi môi trường ngăn chạy; **MANUAL PENDING** khi cần thao tác người dùng; test SQLite và kiểm tra source chỉ là bằng chứng hỗ trợ.

### INT01 — Admin → Database → Web

- Kết quả: **BLOCKED**.
- Đã xác minh: Web/Admin trỏ cùng DB; S17.2 manual Admin CRUD/validation PASS; source Web chỉ đọc sản phẩm active; 50 file ảnh local tồn tại.
- Chưa có bằng chứng S17.3: đọc cùng một product từ SQL, Admin và Web; Web request hiện bị lỗi Event Log; không thay đổi product live.
- Cần: chọn một product hiện có, ghi giá trị SQL read-only, đối chiếu Admin và Web; dùng product inactive hiện có hoặc SQL test DB cách ly để kiểm tra không mua được.

### INT02 — Web → Database → Admin

- Kết quả: **MANUAL PENDING**.
- Đã xác minh hỗ trợ: 26 test isolated kiểm tra Pending, OrderDetails, UnitPrice và TotalAmount; S17 trước đây có bằng chứng một order thật.
- Chưa chạy S17.3: không tạo checkout live; không có SQL test DB riêng.
- Cần: chỉ chạy checkout trên DB test cách ly, hoặc người dùng phê duyệt riêng cho live write; ghi counts/order/stock trước và sau, rồi đối chiếu Admin.

### INT03 — Order workflow

- Kết quả: **MANUAL PENDING**.
- Đã xác minh hỗ trợ: isolated tests PASS cho transition hợp lệ/sai và terminal states; S17.2 user-confirmed workflow PASS.
- Chưa chạy tích hợp hiện tại: không được đổi trạng thái order thật; không có test DB SQL Server cách ly.
- Cần: chạy Pending → Confirmed → Shipping → Completed và các transition sai trên order trong DB test riêng; đối chiếu Customer Web sau từng bước.

### INT04 — Stock consistency

- Kết quả: **MANUAL PENDING**.
- Đã xác minh hỗ trợ: SQLite tests PASS cho decrement, insufficient stock, rollback, cancel restore và double-restore prevention.
- Chưa có bằng chứng SQL Server integration S17.3. Không thao tác tồn kho live.
- Cần: DB test SQL Server cách ly và snapshot stock/order trước-sau cho checkout, cancel, retry và rollback.

### INT05 — Authentication & authorization

- Kết quả: **BLOCKED**.
- Đã xác minh hỗ trợ: 26 tests PASS cho role/inactive/ownership/hash; S17.2 user-confirmed Admin login/RBAC PASS; static scan không thấy PasswordHash trong DTO/grid.
- Chưa chạy đầy đủ S17.3: Web request bị lỗi môi trường; không có hai phiên Customer để kiểm tra cross-customer order trên `PhoneStoreDB`.
- Cần: Web hoạt động, Customer/Admin credentials hợp lệ và hai Customer hoặc DB test cách ly; kiểm tra access/ownership mà không chụp token/hash.

### INT06 — Dashboard & Reports

- Kết quả: **BLOCKED**.
- Đã xác minh hỗ trợ: S17.2 user-confirmed Dashboard/Reports PASS; queries chỉ tính Completed revenue, lấy best sellers từ OrderDetails và low stock threshold 5.
- Chưa có bằng chứng S17.3: baseline SQL độc lập không đọc được nên không thể đối chiếu counts, revenue, statuses, chart.
- Cần: chạy các query read-only cùng thời điểm và so với Dashboard/Reports.

### INT07 — Product images

- Kết quả: **BLOCKED**.
- Đã xác minh: thư mục Web có 50 file hỗ trợ (47 JPG, 1 JPEG, 2 PNG, 0 WebP); không tìm thấy Windows absolute path hoặc `file://` trong Web/Admin source/config; URL thiết kế dùng `/uploads/products/...`.
- Chưa có bằng chứng S17.3: không đọc được ProductImages trong DB để map URL → file; Web request không hoàn tất. Không kết luận 50 file đều được tham chiếu hoặc không hỏng.
- Cần: query toàn bộ ImageUrl, kiểm tra path tương đối, file tồn tại/signature hợp lệ và HTTP 200 trên Web.

### INT08 — Persistence & restart

- Kết quả: **BLOCKED**.
- Đã xác minh hỗ trợ: importer chỉ chạy khi gọi utility với `--execute`; Web/Admin startup không gọi demo import.
- Chưa chạy: không có baseline SQL; Web request bị chặn; không được ghi dữ liệu để kiểm tra persistence.
- Cần: lấy snapshot read-only, đóng/mở Web/Admin, lấy lại snapshot và đối chiếu; nếu kiểm tra write persistence phải dùng DB test cách ly.

### Tổng hợp

| Scenario | Status |
|---|---|
| INT01 | BLOCKED |
| INT02 | MANUAL PENDING |
| INT03 | MANUAL PENDING |
| INT04 | MANUAL PENDING |
| INT05 | BLOCKED |
| INT06 | BLOCKED |
| INT07 | BLOCKED |
| INT08 | BLOCKED |

**Integration PASS: 0/8.** Không có FAIL chức năng; tám kịch bản chưa đủ bằng chứng tích hợp thực trong S17.3.

## 5. Manual GUI Checklist

Chỉ thực hiện khi Web request và SQL read-only hoạt động. Không ghi `PhoneStoreDB` nếu chưa có phê duyệt riêng; ưu tiên DB test SQL Server đã xác minh cách ly.

| ID | Thao tác | Bằng chứng cần lưu | Trạng thái |
|---|---|---|---|
| M01 | Mở cùng product trên Admin, SQL read-only và Web | ProductId, name, price, brand, category, stock, active | MANUAL PENDING |
| M02 | Mở product inactive hiện hữu trên Web, thử add cart | 404/không mua được; không toggle live | MANUAL PENDING |
| M03 | Checkout trên DB test cách ly | Order/Details, Pending, UnitPrice, TotalAmount, stock delta | MANUAL PENDING |
| M04 | Mở order vừa tạo trên Admin | Pending và dữ liệu khớp | MANUAL PENDING |
| M05 | Chuyển workflow trên DB test | Ảnh Admin + Customer status từng bước | MANUAL PENDING |
| M06 | Cancel Pending/Confirmed trên DB test | Stock hoàn đúng một lần | MANUAL PENDING |
| M07 | Hai Customer kiểm tra ownership | Customer B nhận 404/không thấy order A | MANUAL PENDING |
| M08 | Đối chiếu Dashboard/Reports với SQL | Counts, completed revenue, statuses, best sellers, stock | MANUAL PENDING |
| M09 | Kiểm tra tất cả ImageUrl được tham chiếu | SQL URL, file, HTTP status, không broken image | MANUAL PENDING |
| M10 | Restart Web/Admin và đọc lại | Snapshot trước/sau giống nhau, không auto-import | MANUAL PENDING |

## 6. Database Final Comparison

Không có thao tác SQL ghi hoặc request nghiệp vụ ghi do S17.3 thực hiện. Tuy nhiên baseline và final đều không đọc được nên không tuyên bố database preservation đã được xác minh.

| Table | Baseline | Final | Comparison |
|---|---:|---:|---|
| Roles | BLOCKED | BLOCKED | NOT VERIFIED |
| Users | BLOCKED | BLOCKED | NOT VERIFIED |
| Brands | BLOCKED | BLOCKED | NOT VERIFIED |
| Categories | BLOCKED | BLOCKED | NOT VERIFIED |
| Products | BLOCKED | BLOCKED | NOT VERIFIED |
| ProductImages | BLOCKED | BLOCKED | NOT VERIFIED |
| Orders | BLOCKED | BLOCKED | NOT VERIFIED |
| OrderDetails | BLOCKED | BLOCKED | NOT VERIFIED |

- Schema/migration files changed by S17.3: No.
- Seed/import executed: No.
- Live business writes executed by S17.3: No.
- Database integrity verdict: **BLOCKED**.

## 7. Bugs / Risks

Không phát hiện bug chức năng mới vì các scenario tích hợp không chạy đủ để đưa ra kết luận FAIL.

Vấn đề và rủi ro:

1. `sqlcmd`/ODBC không kết nối được do TLS/Schannel, chặn baseline/final và SQL cross-check.
2. Windows Event Log từng làm ngắt mọi Web request; đã sửa và xác minh trong S17.3.1. Route dùng database vẫn bị chặn riêng bởi TLS SQL.
3. Chưa có SQL Server test database riêng đã xác minh cách ly, nên INT02–INT04 không thể chạy ghi an toàn.
4. Có 50 file trong uploads nhưng DB association hiện tại không đọc được; không kết luận file orphan/broken.
5. Bằng chứng manual S17.2 và historical S17 hữu ích để đánh giá hồi quy, nhưng không thay thế kết quả thực thi S17.3.

## 8. Final Verdict

- Build: **PASS** — 0 warnings, 0 errors.
- Automated tests: **26/26 PASS** — SQLite in-memory, không phải live SQL integration.
- Integration: **0/8 PASS**.
- Database integrity: **BLOCKED**.
- Bugs found: **0 confirmed functional bugs**; 2 environment blockers và 3 evidence/test-environment risks được ghi nhận.
- Ready for S18: **NO**.

S17.3 ở trạng thái **BLOCKED**. Để đóng S17.3 cần: khôi phục SQL read-only client/EF connectivity mà không đổi cấu hình trái phép; ghi baseline/final; và hoàn thành INT01–INT08 theo checklist, với mọi thao tác ghi đặt trên SQL Server test database đã xác minh cách ly hoặc có phê duyệt live write riêng. Web request không dùng DB hiện đã hoạt động.

## 9. S17.3.1 — Fix Integration Test Blockers

### Root cause và thay đổi Web

ASP.NET Core default logging trên Windows có Event Log provider. Khi `UseHttpsRedirection` phát cảnh báo không xác định được HTTPS port trong smoke test HTTP, provider cố ghi source `.NET Runtime`. Tài khoản chạy test không có quyền ghi Windows Event Log; exception từ logger làm đóng request, kể cả route/static file không dùng database.

Thay đổi tối thiểu trong `src/PhoneStore.Web/Program.cs`:

- Chỉ khi `builder.Environment.IsDevelopment()` mới clear default providers và thêm Console + Debug.
- Production logging pipeline không thay đổi.
- Không đổi HTTPS middleware, authentication, authorization hoặc database configuration.

Retest bằng Release build tại `http://127.0.0.1:5095`:

| Route | Result | Ý nghĩa |
|---|---:|---|
| `/account/login` | 200 | MVC request hoạt động, không còn Event Log crash |
| `/images/product-placeholder.svg` | 200 | Static file hoạt động |
| `/` | 500 | Request tới EF; lỗi còn lại là SQL TLS |
| `/products` | 500 | Request tới EF; lỗi còn lại là SQL TLS |

### SQL TLS/Schannel diagnosis

| Kiểm tra | Kết quả |
|---|---|
| SQL service | `MSSQL$SERVER` Running/Automatic |
| SQL version registry | MSSQL16.SERVER |
| ForceEncryption | 0 |
| Configured certificate thumbprint | Empty |
| TCP | Enabled, ListenOnAllIPs = 1 |
| Named Pipes | Disabled |
| TLS cipher suites visible | 28 |
| sqlcmd | 16.0.1000.6, ODBC Driver 17 |
| ODBC `taiiiii\SERVER` | Fails at TLS/pre-login |
| ODBC `.\SERVER` | Same failure |
| ODBC Named Pipes attempt | Same TLS/pre-login failure; protocol disabled |
| EF Core/Microsoft.Data.SqlClient through Web | Same error number 20 |

Exact shared error: `The instance of SQL Server you attempted to connect to requires encryption but this machine does not support it.` ODBC adds `SSL Provider: No credentials are available in the security package`.

Kết luận đã xác minh: lỗi không riêng `sqlcmd`; ODBC 17 và Microsoft.Data.SqlClient đều thất bại ở TLS/pre-login trong tiến trình kiểm thử hiện tại. `TrustServerCertificate=True` không giải quyết được vì handshake thất bại trước bước xác thực certificate. SQL Server không cấu hình `ForceEncryption`, nên cần kiểm tra ERRORLOG/certificate/service-account/TLS protocol bằng SQL Server Configuration Manager hoặc phiên quản trị. Agent hiện không có quyền đọc ERRORLOG và không thay đổi TLS hệ thống.

S17.2 có bằng chứng Admin đã kết nối trong phiên manual của người dùng. Điều này cho thấy kết nối có thể phụ thuộc phiên/identity/process environment; chưa có bằng chứng EF Core đang kết nối được trong chính phiên automation hiện tại.

### Phương thức baseline/final read-only

Phương thức ưu tiên là chạy một batch duy nhất trong SSMS/Azure Data Studio ở cùng phiên Windows đã mở Admin thành công. Batch chỉ dùng `SELECT`, xác minh đúng DB trước khi đếm:

```sql
IF DB_NAME() <> N'PhoneStoreDB'
    THROW 51000, 'Wrong database', 1;

SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
SELECT N'Roles' AS TableName, COUNT_BIG(*) AS RowCount FROM dbo.Roles
UNION ALL SELECT N'Users', COUNT_BIG(*) FROM dbo.Users
UNION ALL SELECT N'Brands', COUNT_BIG(*) FROM dbo.Brands
UNION ALL SELECT N'Categories', COUNT_BIG(*) FROM dbo.Categories
UNION ALL SELECT N'Products', COUNT_BIG(*) FROM dbo.Products
UNION ALL SELECT N'ProductImages', COUNT_BIG(*) FROM dbo.ProductImages
UNION ALL SELECT N'Orders', COUNT_BIG(*) FROM dbo.Orders
UNION ALL SELECT N'OrderDetails', COUNT_BIG(*) FROM dbo.OrderDetails;
```

Lưu output có timestamp ngay trước và ngay sau test. Cùng một batch, database và Windows identity phải được dùng cho cả hai lần. Trong automation, batch này có thể chạy bằng `sqlcmd` hoặc một EF `AsNoTracking` probe sau khi TLS connectivity được khôi phục; hiện tại cả hai đều BLOCKED.

### Đề xuất PhoneStoreTestDB — chưa thực hiện

Chỉ thực hiện sau khi người dùng phê duyệt tạo database:

1. Tạo database trống `PhoneStoreTestDB` trên instance đã xác minh; không backup/copy `PhoneStoreDB`.
2. Cấp quyền tối thiểu cho Windows identity hiện tại trên riêng test DB.
3. Dùng environment variable `ConnectionStrings__PhoneStoreConnection` cho tiến trình Web/Admin/test; không sửa connection string checked-in.
4. Trước mọi write test, guard bắt buộc `DB_NAME() = 'PhoneStoreTestDB'`; abort nếu khác.
5. Apply migration `InitialCreate` hiện có vào test DB; không tạo migration mới.
6. Xác minh `__EFMigrationsHistory`, 8 bảng nghiệp vụ và chỉ các role seed ban đầu trước khi tạo fixture test.
7. Tạo fixture chỉ trong test DB, chạy INT02–INT04, rồi giữ hoặc xóa test DB chỉ theo phê duyệt riêng.

Database chưa được tạo, migration chưa được apply và không có write nào được thực hiện trong S17.3.1.

### Commands và verification S17.3.1

- Registry read-only: SQL encryption/certificate/protocols, ODBC driver, TLS cipher inventory.
- `sqlcmd` qua named instance, local instance và Named Pipes attempt: đều thất bại trước query.
- Web Release smoke test: 2 route không DB HTTP 200; 2 route DB HTTP 500 với SqlClient TLS error.
- `dotnet build PhoneStore.sln -c Release --no-restore`: **PASS, 0 warnings, 0 errors**.
- `dotnet test PhoneStore.sln -c Release --no-restore --no-build`: **26/26 PASS**.

Debug output đang bị khóa bởi một `PhoneStore.Web.exe` đã chạy trước phiên S17.3.1; tiến trình đó không bị dừng. Release được dùng để xác minh sạch thay đổi.

### S17.3.1 verdict

- Event Log blocker: **RESOLVED**.
- SQL TLS blocker: **REMAINS BLOCKED**, root layer isolated to shared TLS/pre-login path; exact server certificate/service-account cause needs administrative inspection.
- Baseline/final method: **DEFINED**, execution BLOCKED by SQL connectivity.
- Isolated test DB: **PLAN READY, NOT CREATED**, requires explicit approval.
- Database safety: **PASS for this task** — no DB write/schema/migration/seed/reset.
- Ready to resume S17.3: **NO**, until read-only SQL/EF connectivity works; write scenarios additionally require approved isolated test DB.
