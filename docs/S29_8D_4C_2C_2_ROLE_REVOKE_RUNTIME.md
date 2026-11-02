# S29.8D.4C.2C.2 — Runtime revoke Admin bằng RoleId

Ngày nghiệm thu: 2026-11-02 (Asia/Bangkok). Chỉ `PhoneStoreDB_StockConcurrencyTest`, UserId1; không main DML.

## Precheck

- Admin test D.4B PID `15548`, executable/config đúng staging D.4C.1, test DB và uploads test.
- Fixture UserId1/`admin@s29-8b1.test` là RoleId1/Admin, IsActive1; RoleId2=Customer; không Admin khác.
- Filtered index enabled; SQL session sleeping/open transaction0; không write request khác.
- Người dùng mở sửa BrandId6, nhập tên trùng BrandId1 `S29.8B.1 Test Brand`, chưa Lưu. UI Automation xác minh đúng dialog/value/nút Lưu enabled.

## Revoke RoleId 1→2

- Dùng đúng script D.4C.2C.1 với bảy SET options; `SESSIONPROPERTY=1/1/1/1/1/1/0`.
- Guard DB_NAME, RoleId2=Customer, fixture UserId/email/RoleId1/IsActive1; Serializable transaction, rowcount1, TRY/CATCH.
- Kết quả `ROLE_REVOKE_COMMITTED`, affected rows1, exit0.
- SELECT độc lập: UserId1/RoleId2/Customer/IsActive1, PasswordHash length168 không đổi, transaction0.

## Runtime authorization

- UI Automation xác minh lại duy nhất dialog/nút rồi Invoke Lưu đúng một lần.
- Ngay sau Invoke: dialog và Admin shell đóng, LoginForm hiện; LoginButton có, LogoutButton không còn.
- BrandId1/6 SELECT khi RoleId2 vẫn hoàn toàn như baseline; không INSERT/UPDATE Brand.
- Kết quả là fresh-authorization rejection: duplicate service validation sẽ giữ dialog mở, trong khi gateway revoke clear session và đóng shell.
- Nội dung thông báo thoáng qua không còn sau transition nên không ghi nhận text; không có dữ liệu nhạy cảm trong bằng chứng.

## Restore RoleId 2→1 và login

- Trước restore: SELECT RoleId2/IsActive1, Brand unchanged, XACT_STATE0/TRANCOUNT0.
- Script restore dùng đủ SET options; guard Admin role, không Admin khác, fixture RoleId2/active; transaction/rowcount1/TRY-CATCH.
- Kết quả `ROLE_RESTORE_COMMITTED`, affected rows1, exit0.
- SELECT độc lập: UserId1/RoleId1/Admin/IsActive1, PasswordHash length168, transaction0.
- Người dùng đăng nhập lại bằng credential hiện có. UI Automation: MainForm `PhoneStore Admin`, greeting `SQL Test Admin`, LogoutButton có/LoginButton không; SQL PID15548→test DB sleeping/open transaction0.

## Hậu kiểm và phân loại

- Test DB giữ Roles2, Users5, Brands2, Categories2, Products5, Images4, Orders10, Details10, stock14; Product13/14 và Image22/24/25/26 không đổi.
- Main giữ 2/2/8/1/40/40/1/1/2, stock740, một Cancelled, một active Admin; 40/40 ảnh khớp SHA-256 baseline.
- SQL revoke RoleId: **PASS**.
- Fresh authorization: **PASS WinForms thật**.
- Session clear/shell→LoginForm: **PASS WinForms thật**.
- Explicit message text: **PARTIAL / không tồn tại sau transition**.
- Restore + login lại: **PASS**.
- Hậu kiểm: **PASS**.
- Build/tests: **không chạy lại**, không sửa source; baseline D.4B là 161/161 và build 0 warnings/errors.

Kết luận D.4C.2C.2: **PASS**. Dừng sau kịch bản RoleId; không deploy Admin chính.
