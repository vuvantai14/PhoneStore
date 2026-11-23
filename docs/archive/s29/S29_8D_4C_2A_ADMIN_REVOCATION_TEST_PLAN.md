# S29.8D.4C.2A — Kế hoạch kiểm thử thu hồi quyền Admin

Ngày chuẩn bị: 2026-11-02 (Asia/Bangkok). Giai đoạn này chỉ đọc và lập kế hoạch; **chưa chạy UPDATE**, chưa thay RoleId/IsActive và chưa thao tác ghi nghiệp vụ.

## Preflight đã xác minh

- Admin test D.4B vẫn chạy: PID `15548`, executable thuộc staging D.4C.1; cấu hình dùng `localhost/PhoneStoreDB_StockConcurrencyTest` và uploads test D2C.
- SQL runtime: server `DESKTOP-ES9362P`, database test `ONLINE`; đúng một session của PID 15548, trạng thái sleeping, open transaction 0.
- Fixture đích duy nhất: `Users.UserId=1`, email `admin@s29-8b1.test`, RoleId `1`/Admin, IsActive `1`; không có Order tham chiếu và không có Admin test khác.
- Roles: `1=Admin`, `2=Customer`. `FK_Users_Roles_RoleId` và `FK_Orders_Users_UserId` đều NO ACTION. Không có trigger bảng nào trong database test.
- `UX_Users_SingleAdmin` unique, enabled, filter `RoleId=1`; chuyển fixture sang RoleId 2 cho phép tạm thời không có Admin. Hoàn nguyên về RoleId 1 chỉ an toàn nếu chưa xuất hiện Admin khác.
- Session giữ `AdminIdentity` trong memory. Trước mỗi write, gateway fresh-check `UserId + IsActive + RoleName=Admin`; khi false, clear session, phát `AuthorizationRevoked`, đóng shell bằng `BeginInvoke` và hiện LoginForm. Read path không fresh-check.
- Snapshot test: Roles2, Users5, Brands2, Categories2, Products5, Images4, Orders10, Details10, stock14. Protected Product13/14 và Image22/24/25/26 đã ghi nhận.
- Main: 2/2/8/1/40/40/1/1/2, stock740, một Cancelled, một active Admin; 40/40 ảnh khớp SHA-256 baseline.

## Phép thử ghi không gây tác dụng phụ nghiệp vụ

Trước mỗi revoke, mở dialog sửa BrandId 6 và chuẩn bị tên trùng chính xác BrandId 1 (`S29.8B.1 Test Brand`) nhưng **chưa Lưu**. Sau revoke mới bấm Lưu một lần. Gateway phải kiểm quyền trước service và từ chối, clear session, đóng dialog/shell, về LoginForm.

Tên trùng là lớp bảo vệ thứ hai: nếu fresh-check không hoạt động, validation duplicate trong service vẫn phải từ chối trước SaveChanges. Đối chiếu SQL phải xác nhận BrandId 6, hai Product13/14 và toàn snapshot không đổi. Không dùng Product Save vì một lần lưu hợp lệ có thể đổi UpdatedAt; không upload ảnh hoặc đổi trạng thái Order.

## A — IsActive true → false

Checkpoint trước: xác minh DB_NAME, fixture đúng email/RoleId1/IsActive1, không transaction mở từ Admin và snapshot nghiệp vụ khớp.

Lệnh revoke dự kiến (chỉ chạy sau phê duyệt riêng):

```sql
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRAN;
IF DB_NAME() <> N'PhoneStoreDB_StockConcurrencyTest' THROW 51000, 'Wrong database', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK,HOLDLOCK)
               WHERE UserId=1 AND Email=N'admin@s29-8b1.test' AND RoleId=1 AND IsActive=1)
    THROW 51001, 'Fixture precondition failed', 1;
UPDATE dbo.Users SET IsActive=0
WHERE UserId=1 AND Email=N'admin@s29-8b1.test' AND RoleId=1 AND IsActive=1;
IF @@ROWCOUNT <> 1 THROW 51002, 'Unexpected affected rows', 1;
COMMIT;
```

Sau thao tác UI, SELECT xác minh IsActive0, session bị clear/LoginForm, không DML nghiệp vụ. Hoàn nguyên ngay, trước mọi thử nghiệm khác:

```sql
SET XACT_ABORT ON;
BEGIN TRAN;
IF DB_NAME() <> N'PhoneStoreDB_StockConcurrencyTest' THROW 51000, 'Wrong database', 1;
UPDATE dbo.Users SET IsActive=1
WHERE UserId=1 AND Email=N'admin@s29-8b1.test' AND RoleId=1 AND IsActive=0;
IF @@ROWCOUNT <> 1 THROW 51003, 'Restore precondition failed', 1;
COMMIT;
```

Hậu kiểm A: fixture trở lại RoleId1/IsActive1; PasswordHash và mọi cột khác không được cập nhật; người dùng đăng nhập lại bằng mật khẩu hiện có. Chỉ khi login và snapshot PASS mới sang B.

## B — Admin → non-Admin

Checkpoint mới: Admin đã đăng nhập lại, fixture RoleId1/IsActive1, không Admin khác, role 2 đúng `Customer`, snapshot khớp. Revoke dự kiến:

```sql
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRAN;
IF DB_NAME() <> N'PhoneStoreDB_StockConcurrencyTest' THROW 51000, 'Wrong database', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WITH (HOLDLOCK) WHERE RoleId=2 AND RoleName=N'Customer')
    THROW 51004, 'Non-admin role missing', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK,HOLDLOCK)
               WHERE UserId=1 AND Email=N'admin@s29-8b1.test' AND RoleId=1 AND IsActive=1)
    THROW 51001, 'Fixture precondition failed', 1;
UPDATE dbo.Users SET RoleId=2
WHERE UserId=1 AND Email=N'admin@s29-8b1.test' AND RoleId=1 AND IsActive=1;
IF @@ROWCOUNT <> 1 THROW 51002, 'Unexpected affected rows', 1;
COMMIT;
```

Sau UI/SQL checkpoint, hoàn nguyên có bảo vệ unique-single-Admin:

```sql
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRAN;
IF DB_NAME() <> N'PhoneStoreDB_StockConcurrencyTest' THROW 51000, 'Wrong database', 1;
IF EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK,HOLDLOCK) WHERE RoleId=1 AND UserId<>1)
    THROW 51005, 'Another Admin exists; stop', 1;
UPDATE dbo.Users SET RoleId=1
WHERE UserId=1 AND Email=N'admin@s29-8b1.test' AND RoleId=2 AND IsActive=1;
IF @@ROWCOUNT <> 1 THROW 51003, 'Restore precondition failed', 1;
COMMIT;
```

Hậu kiểm B giống A và đăng nhập lại không reset mật khẩu. Nếu revoke, UI, restore hoặc checkpoint nào không rõ kết quả: dừng, chỉ SELECT xác minh; không chạy lại UPDATE và không chuyển sang trường hợp tiếp theo.

## Phê duyệt cần thiết

1. Phê duyệt **A**: đúng hai thay đổi có điều kiện `IsActive 1→0→1` trên UserId1 trong test DB, cùng một lần Save dialog duplicate để kích hoạt fresh-check.
2. Sau khi A khôi phục và PASS, phê duyệt **B** riêng: `RoleId 1→2→1` trên cùng fixture, cùng một lần Save dialog duplicate.
3. Mọi lệnh phải chạy riêng từng checkpoint, kiểm `DB_NAME()` và `@@ROWCOUNT=1`; không batch cả A+B, không reset mật khẩu, không chạm main.

Trạng thái: **PREPARATION PASS / RUNTIME REVOKE NOT TESTED**. Dừng chờ phê duyệt thay đổi fixture test.
