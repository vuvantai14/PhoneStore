# S29.8D.1 — Hồ sơ Customer

Ngày: 2026-11-02, Asia/Bangkok. **PASS source/build/tests; PARTIAL nghiệm thu runtime chính, chưa triển khai.**

## Thiết kế và phạm vi

Audit UserConfiguration xác nhận Users đã có FullName (100), Phone (20), Address (255), Email (150). Dùng User/Customer và cookie authentication hiện có; không thêm bảng, migration hoặc hệ thống tài khoản khác.

- `GET /account/profile`: hiển thị hồ sơ của Customer active đang đăng nhập.
- `POST /account/profile`: chỉ cập nhật FullName, Phone, Address; redirect về GET sau thành công.
- FullName trim, bắt buộc 2–100 ký tự; Phone tùy chọn, tối đa 20 ký tự và PhoneAttribute hợp lệ; Address tùy chọn, tối đa 255 ký tự. Phone/Address rỗng được lưu null.
- Email hiển thị chỉ đọc, lấy từ database, không bind từ form. Không cung cấp sửa email, mật khẩu, role hoặc trạng thái tài khoản.
- Liên kết “Hồ sơ của tôi” thêm vào header Customer; liên kết đơn hàng/đăng xuất/giỏ hàng giữ nguyên.

## Bảo mật và xử lý lỗi

- Controller dùng policy Customer; ID chỉ lấy từ NameIdentifier của principal, không nhận route/form/query UserId làm đích cập nhật.
- DTO/view model không có RoleId, PasswordHash, IsActive, CreatedAt hoặc UserId. Overposting các trường này bị bỏ qua; email BindNever và được tải lại từ DB.
- Data store chỉ SELECT projection các trường hồ sơ, không trả User entity/hash cho view.
- UPDATE có điều kiện UserId + IsActive + RoleName Customer, chỉ SET ba cột được phép. Không attach entity client hoặc SaveChanges làm ghi kèm entity khác.
- Server validation ở MVC và Business, POST có ValidateAntiForgeryToken; Razor encode dữ liệu hiển thị.
- Không tồn tại/inactive/không phải Customer: không cập nhật; phiên không còn hợp lệ bị cookie validation từ chối. Lỗi DB khi đọc trả thông báo 503; khi lưu trả Unavailable và thông báo rõ trên form.
- Cookie validator hiện so sánh cả FullName. Sau đổi tên, controller cập nhật Name claim, giữ identity/claims còn lại, IsPersistent và ExpiresUtc của ticket cũ để phiên không tự mất hiệu lực hoặc bị kéo dài vì sửa hồ sơ.
- Bảo vệ này không cung cấp version cho hai lần sửa hồ sơ đồng thời; ba trường hồ sơ vẫn theo lần lưu cuối. Không thay đổi logic stock/order/idempotency.

## File tạo/sửa trong nhiệm vụ

| File | Thay đổi |
|---|---|
| src/PhoneStore.Models/Authentication/CustomerProfile.cs | DTO đọc/sửa và kết quả cập nhật không chứa credentials |
| src/PhoneStore.Data/Authentication/CustomerProfileStore.cs | Projection và conditional UPDATE chỉ ba cột |
| src/PhoneStore.Business/Authentication/ICustomerProfileService.cs | Hợp đồng service |
| src/PhoneStore.Business/Authentication/CustomerProfileService.cs | Normalize, validation, active Customer, xử lý lỗi ghi |
| src/PhoneStore.Web/Controllers/ProfileController.cs | GET/POST, claims ownership, CSRF, cookie refresh |
| src/PhoneStore.Web/Models/CustomerProfileViewModel.cs | Trường form và server validation |
| src/PhoneStore.Web/Views/Profile/Index.cshtml | Form hồ sơ dùng các class hiện có |
| src/PhoneStore.Web/Views/Shared/_Layout.cshtml | Link Customer “Hồ sơ của tôi” |
| src/PhoneStore.Web/Program.cs | Đăng ký profile service/store; public partial Program cho HTTP integration tests |
| tests/PhoneStore.Tests/CustomerProfileTests.cs | 12 ca service/SQLite |
| tests/PhoneStore.Tests/CustomerProfileHttpTests.cs | 10 ca HTTP integration |
| tests/PhoneStore.Tests/PhoneStore.Tests.csproj | Microsoft.AspNetCore.Mvc.Testing 8.0.28 cùng phiên bản .NET/ASP.NET Core 8 |
| docs/S29_8D_1_CUSTOMER_PROFILE.md | Báo cáo này |
| docs/PROGRESS.md | Trạng thái và bước tiếp tục |

AGENTS.md và audit S29.8D không sửa trong D.1. Những file/tài liệu còn thay đổi từ nhiệm vụ trước được giữ nguyên, không commit.

## Kiểm thử thực tế đã chạy

```powershell
dotnet restore PhoneStore.sln
dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8D.1/
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-restore -p:OutputPath=bin/S29.8D.1/ --filter FullyQualifiedName~CustomerProfile --verbosity minimal
dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8D.1/ --verbosity minimal
git diff --check
```

- Restore PASS sau ngoại lệ network hẹp cho NuGet; lần đầu sandbox báo NU1301 repository signature. Không nới quyền toàn cục.
- Build solution PASS **0 warnings / 0 errors**; output riêng `bin/S29.8D.1/`, không ghi đè Debug của process chính.
- Profile suite **22/22 PASS**: 12 service + 10 HTTP. Toàn bộ regression **129/129 PASS**, 0 fail/skip: baseline 107 + 22 mới.
- HTTP tests dùng **Program thật, MVC/view, cookie validator, authorization và antiforgery thật**, WebApplicationFactory/TestServer và SQLite in-memory từng test, provider được assert trước tạo fixture. Không dùng SQL Server/PhoneStoreDB_StockConcurrencyTest.
- Data Protection test dùng provider ephemeral, không đọc/ghi key ring của main; không tạo database SQL hoặc browser account thật.
- Lần đầu HTTP tests bị chặn ghi key ring AppData trong sandbox; đã sửa cách ly test provider. Ca fault DB ban đầu đóng kết nối qua EF nhưng kết nối do fixture sở hữu vẫn mở; đã sửa fault injection đóng đúng kết nối SQLite riêng. Đây là lỗi thiết lập kiểm thử đã xử lý, không sửa quyền hệ thống hay database chính.
- `git diff --check` PASS; có cảnh báo chuẩn hóa LF/CRLF theo cấu hình Git, không đổi Git config.

| Ca kiểm tra | Kết quả |
|---|---|
| Đọc active Customer; ID sai/missing/inactive không đọc được | PASS service |
| Admin không được đọc/sửa như Customer | PASS service |
| Cập nhật đúng ba trường, email/role/hash/created/active và tài khoản khác giữ nguyên | PASS service + HTTP |
| Null/rỗng/name ngắn/phone sai/field quá dài/biên 100–255/clear optional | PASS service; HTTP kiểm name rỗng/phone sai |
| DB lỗi trả Unavailable | PASS service fault SQLite riêng |
| Anonymous GET/POST cần login | PASS HTTP middleware |
| GET query UserId khác vẫn chỉ hiện hồ sơ mình; route thêm ID trả 404 | PASS HTTP |
| Không render password/hash/role hoặc UserId input | PASS HTTP |
| POST giả UserId/RoleId/Email/PasswordHash/active không thay quyền hoặc tài khoản khác | PASS HTTP |
| Thiếu/giả CSRF hoặc token từ browser session khác | PASS HTTP 400, dữ liệu giữ nguyên |
| Session inactive hoặc đổi khỏi Customer sau login | PASS HTTP, không đọc/ghi được hồ sơ |
| Đổi tên xong vẫn đọc profile/orders, giữ persistent và expiry | PASS HTTP với cookie validator thật |

## Main và browser — chỉ đọc

- PID Web **22736**, Admin **10736** vẫn bản C.6B; **118/118 hash binary main không đổi**.
- SELECT hậu kiểm: DESKTOP-ES9362P/PhoneStoreDB ONLINE,40 Products/40 Images/8 Brands/1 Category/2 Users/1 Order/1 Detail/tổng tồn740; Admin duy nhất active, Customer vẫn tên tài vũ; OrderId1 Cancelled. Hai migrations không đổi.
- Trong browser Customer đang đăng nhập: trang Home không báo lỗi; GET `/account/profile` từ browser trả **404**, đúng vì binary cũ chưa có route. Không gửi POST profile hoặc thao tác ghi main.
- **Chưa nghiệm thu trực quan form mới bằng browser trên main**, chưa xác minh SQL Server provider cho UPDATE profile. HTTP/TestServer/SQLite không thay thế các bằng chứng đó. Không tự khởi động/thay binary mới để vượt giới hạn phê duyệt.
- Không migration/seed/import/restore, tạo Customer/đơn, sửa Admin/catalog/kho/ảnh, restart main, commit hoặc push.

## Bước cần phê duyệt tiếp theo

1. Duyệt riêng triển khai Web mới: kiểm source/tests/artifact, backup binary, shutdown mềm đúng Web, thay đủ binary giữ config/uploads rồi restart/smoke. Admin không cần triển khai cho chức năng này.
2. Nghiệm thu **chỉ đọc trước**: Customer đang đăng nhập mở Hồ sơ của tôi, xác nhận email/tên/phone/address đúng và không có trường quản trị; anonymous về login.
3. Nếu cần ghi main, trình trước giá trị cũ→mới của FullName/Phone/Address cho đúng UserId2 và xin duyệt riêng; dự kiến chỉ UPDATE 1 Users row, không thay email/role/hash/tài khoản khác hoặc bảng nghiệp vụ. Không tự thực hiện hoặc tự hoàn tác bằng SQL.

Dừng chờ phê duyệt; không coi source/tests PASS là đã triển khai hoặc nghiệm thu sửa hồ sơ thật.
