# S29.8D.4C.2B-R2 — Runtime revoke IsActive

Ngày nghiệm thu: 2026-11-02 (Asia/Bangkok). Chỉ test `PhoneStoreDB_StockConcurrencyTest`, UserId1; không RoleId change, không main DML.

## Precheck

- Admin test D.4B PID `15548`, executable/config đúng staging D.4C.1 và test DB/uploads test.
- Dialog sửa BrandId6 còn mở, `LookupNameInput=S29.8B.1 Test Brand`, đúng một `SaveLookupButton`, chưa lưu.
- Fixture `admin@s29-8b1.test`: UserId1, RoleId1/Admin, IsActive1; filtered index enabled.
- SQL session Admin sleeping/open transaction0; không user write request khác.
- Baseline test: Roles2, Users5, Brands2, Categories2, Products5, Images4, Orders10, Details10, stock14. BrandId1/6 và protected D2C fixtures đã ghi nhận.

## Revoke SQL

- Dùng script R1 với đủ bảy SET options; `SESSIONPROPERTY=1/1/1/1/1/1/0` trước UPDATE.
- Guard DB_NAME, UserId/email/RoleId/IsActive, Serializable transaction, `@@ROWCOUNT=1`, TRY/CATCH/rollback giữ nguyên.
- Kết quả: `REVOKE_COMMITTED`, affected rows 1, exit0.
- SELECT độc lập: UserId1/RoleId1/Admin/IsActive0, PasswordHash length không đổi; XACT_STATE0/TRANCOUNT0.

## Runtime authorization và session

- Helper UI đầu tiên lỗi parse PowerShell trước khi chạy, nên không có click. Helper sửa cú pháp xác minh lại dialog/value/duy nhất một nút rồi Invoke **đúng một lần thực tế**.
- Sau Invoke: dialog và Admin shell đóng; đúng một cửa sổ `Đăng nhập · PhoneStore Admin`, LoginButton hiện, LogoutButton/MainForm không còn.
- Đây là fresh-authorization rejection, không phải duplicate-name validation: theo source, duplicate service result giữ dialog mở; chỉ gateway revoke mới clear session và phát event đóng shell.
- SELECT khi còn inactive xác nhận BrandId1/6 không đổi; không INSERT/UPDATE Brand và không thay fixture nghiệp vụ.
- Nội dung thông báo Unauthorized thoáng qua không được UI Automation giữ lại sau shell close; không có log bí mật. Kết quả auth/session dựa trên UI transition + SQL unchanged + source path, không giả là đã chụp message text.

## Restore và đăng nhập lại

- Dùng script restore R1 trong transaction riêng, cùng SET options/guards.
- Trước restore: IsActive0, Brand unchanged, XACT_STATE0/TRANCOUNT0.
- Kết quả: `RESTORE_COMMITTED`, affected rows1, exit0.
- SELECT độc lập: UserId1/RoleId1/Admin/IsActive1, PasswordHash length không đổi, transaction0.
- Người dùng đăng nhập lại bằng credential hiện có; UI Automation xác minh MainForm `PhoneStore Admin`, greeting `SQL Test Admin`, LoginButton không còn, LogoutButton có.
- SQL runtime sau login: PID15548 → test DB, sleeping/open transaction0.

## Hậu kiểm và phân loại

- Test DB giữ 2/5/2/2/5/4/10/10, stock14; Brand và Product13/14, Image22/24/25/26 không đổi.
- Main giữ 2/2/8/1/40/40/1/1/2, stock740, một Cancelled, một active Admin; 40/40 ảnh khớp SHA-256 baseline.
- SQL revoke: **PASS**.
- Fresh runtime authorization: **PASS WinForms thật**.
- Session clear + shell→LoginForm: **PASS WinForms thật**.
- Explicit message text capture: **PARTIAL / không còn sau transition**, không ảnh hưởng bằng chứng chặn write.
- Restore + login lại: **PASS**.
- Main/test business invariants: **PASS**.
- Build/tests: **không chạy lại**, vì không sửa source.

Kết luận R2: **PASS**, dừng sau kịch bản A. Không thực hiện kịch bản B RoleId.
