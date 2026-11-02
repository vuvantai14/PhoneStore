# S29.8D.1B — Triển khai Customer Profile Web

Ngày 2026-11-02, Asia/Bangkok. **PASS triển khai và nghiệm thu chỉ đọc. Chưa nghiệm thu POST cập nhật hồ sơ thật.**

Người dùng phê duyệt riêng triển khai Web. Không thay Admin binary, không thay cấu hình/ảnh, không thực hiện thao tác ghi dữ liệu nghiệp vụ.

## Build, staging và backup

Đã đọc AGENTS, PROGRESS và báo cáo S29.8D.1; kiểm tra process, cấu hình localhost/PhoneStoreDB/Windows Authentication, source và main trước triển khai.

```powershell
dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8D.1B-build/
dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8D.1B-build/ --verbosity minimal
```

Build **0 warnings/errors**; regression **129/129 PASS**, 0 fail/skip. Build riêng không ghi đè output Debug đang chạy. Không sửa source ứng dụng trong bước triển khai này.

Artifact root:

```text
C:\PhoneStore\PhoneStore\src\PhoneStore.Web\obj\S29.8D.1B-20261102_024935357Z_9be18a0a8d6a413d9cfa63304bc5d7f7
```

| Artifact/kiểm tra | Kết quả |
|---|---|
| staging/ + staging-manifest.json | 62/62 file copy khớp build; gồm 60 runtime/binary + 2 appsettings snapshot |
| backup/ + backup-manifest.json | 62/62 file khớp Web Debug cũ, kiểm lại trước và sau triển khai |
| copy-plan.json | Chỉ 60 runtime/binary, loại appsettings khỏi danh sách copy |
| Hash sau triển khai | 60/60 khớp staging; 2 appsettings binary main khớp backup |
| preserved-files.json | 101/101 hash Admin output, uploads và appsettings source giữ nguyên |
| source-manifest.json | Source của bốn project phụ thuộc Web khớp snapshot trước copy |

SHA256 manifest staging:
`262D9693495C160A488BD4737B0E904DFA63CE648125D9CECAAB0E73C36939E3`

SHA256 manifest backup:
`B724691B8947357629438B7EF5A7E479858D43CDAE3419EF9FFC41944CDF4D68`

SHA256 Web DLL đã triển khai, khớp staging:
`B8D582F516FEE2D22CD889F1404757FE9CF2A76FA70B2D2C43A2ACAAC85991C1`

SHA256 Business DLL trong bộ Web đã triển khai, khớp staging:
`C92D65B84DD567F43499A40643C5CBCEB11C551850A79C9339EF26CA8D5DE6DD`

Manifest chứa hash từng file, không chỉ EXE. Backup trong obj cần được giữ lại; build clean có thể làm mất artifact. Backup binary không thay backup SQL/ảnh. Không tự cleanup hoặc rollback.

## Shutdown và khởi động

- Web cũ PID22736, Admin PID10736 được xác minh đúng executable/command line. Listener5015 thuộc Web cũ. SQL trước shutdown: không request SQL đang chạy/transaction mở liên quan Web.
- Helper kiểm console chỉ có Web và chính helper; không có Admin hoặc tiến trình khác. Ctrl+C đúng một lần, chờ20giây: Web còn chạy.
- Recheck PID/console/SQL rồi Ctrl+Break đúng một lần theo phê duyệt. Helper thoát mã2 sau gửi tín hiệu; kiểm tra độc lập xác nhận **Web đã thoát, listener5015=0**, log cũ ghi `Application is shutting down...`. Admin vẫn chạy. Không gửi lại tín hiệu, không force-kill.
- Chỉ sau xác nhận Web thoát/cổng trống mới thay 60 file từ staging. Cấu hình main, secrets và uploads không nằm trong thao tác ghi đè.
- Khởi động Web executable trực tiếp, working/content root `C:\PhoneStore\PhoneStore\src\PhoneStore.Web`, Development, URL5015. Connection được pin `Server=localhost;Database=PhoneStoreDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`.

| Ứng dụng | PID sau triển khai | Binary |
|---|---|---|
| Customer Web | **19388** | src/PhoneStore.Web/bin/Debug/net8.0/PhoneStore.Web.exe |
| Admin giữ nguyên | **10736** | src/PhoneStore.Admin/bin/Debug/net8.0-windows/PhoneStore.Admin.exe |

Web bắt đầu **02:53:43 UTC / 09:53:43 Bangkok**. process.json ghi PID/command context/log paths. Không sửa connection string Customer Web/Admin hoặc sao chép cấu hình test.

## Smoke GET và browser

| Kiểm tra | Kết quả |
|---|---|
| Home | HTTP200,6cards |
| Products | HTTP200,40cards |
| Login / Cart | HTTP200 |
| Chi tiết | 40/40 ProductId1–40 HTTP200 |
| Ảnh | 40/40 JPG HTTP200, Content-Type image/jpeg; file hash nguyên |
| Profile anonymous | HTTP302 → `/account/login?ReturnUrl=/account/profile` |
| Profile Customer đã login | Browser giữ session cũ hợp lệ sau restart; click link thật từ Home → `/account/profile`, hiển thị Hồ sơ của tôi |
| Hồ sơ đúng owner | Tên tài vũ/email tai123@gmail.com khớp Customer; không hiện email Admin |
| Trường chỉnh sửa | Chỉ FullName/Phone/Address; email hiển thị text, không có input email |
| Dữ liệu nhạy cảm | Không có UserId/RoleId/PasswordHash/Password/IsActive input hoặc nhãn RoleId/PasswordHash trên trang |
| Admin | PID10736 responsive, không restart; chỉ mở Dashboard,40 sản phẩm/tồn740/1Customer/1đơn/0Completed/0doanhthu |

Không bấm Lưu hồ sơ, không gửi POST profile, không tạo tài khoản hoặc đơn. Browser không cần đăng nhập lại, không đọc/gửi mật khẩu/cookie/token qua báo cáo. Đã xem ảnh chụp trang profile để kiểm tra trực quan; chưa nghiệm thu chỉnh sửa.

Bằng chứng runtime trong artifact root: http-smoke.json, browser-profile-read.json, profile-readonly.png, process.json, web.stdout.log, web.stderr.log. Helper console/browser và state nằm trong Web/obj, không đưa session/secrets vào Git.

## Hậu kiểm SQL và log

SELECT **02:56:13 UTC / 09:56:13 Bangkok**:

- DESKTOP-ES9362P / PhoneStoreDB **ONLINE**.
- Products40, Images40, Brands8, Categories1; tổng tồn **740**.
- Users2: Customer1 (`tai123@gmail.com`, tên tài vũ, RoleId2 active); Admin active1 (`admin@admin.com`, RoleId1).
- Orders1, OrderDetails1; OrderId1 **Cancelled**.
- History giữ InitialCreate + EnforceSingleAdmin; không chạy migration.
- SQL sessions Web19388 và Admin10736 cùng **PhoneStoreDB**, sleeping/open transaction0 tại lần kiểm tra.
- Log mới không có fail/crit/unhandled exception; stderr0bytes. Còn warning HTTPS redirect thiếu port do chạy HTTP, đã có từ trước.

Helper smoke đầu thiếu assembly System.Net.Http của PowerShell, đã bổ sung nạp assembly rồi chạy lại GET thành công; không phải lỗi ứng dụng. Không đổi ExecutionPolicy khi thực thi helper console nội tuyến.

## Trạng thái và bước tiếp tục

**PASS** Web deployment, profile GET anonymous/Customer, catalog/image smoke, Admin giữ nguyên và SQL baseline. **NOT TESTED trên main**: POST cập nhật hồ sơ/validation ghi, cookie rename sau lưu, SQL Server UPDATE profile. Các ca tương ứng đã PASS trong suite SQLite/HTTP cách ly, không suy thành đã nghiệm thu main.

Không migration/seed/import/restore/SQL DML, thay Admin binary, sửa ảnh/catalog/tồn/đơn/tài khoản, commit/push. Không rollback. Cập nhật PROGRESS và báo cáo triển khai này; giữ mọi thay đổi source từ D.1.

**Dừng chờ phê duyệt riêng trước POST hồ sơ thật**: cần chỉ rõ đúng Customer UserId2, giá trị cũ→mới của ba trường và một lần lưu qua Web; không đổi email/role/password hoặc dữ liệu nghiệp vụ khác.
