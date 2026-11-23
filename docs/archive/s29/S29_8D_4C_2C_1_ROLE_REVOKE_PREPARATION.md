# S29.8D.4C.2C.1 — Chuẩn bị revoke Admin bằng RoleId

Ngày chuẩn bị: 2026-11-02 (Asia/Bangkok). Chỉ đọc và compile/no-exec; **chưa UPDATE RoleId**.

## Runtime và fixture

- Admin test D.4B vẫn chạy PID `15548`, đúng executable staging D.4C.1.
- Config runtime: `localhost/PhoneStoreDB_StockConcurrencyTest`; uploads là thư mục test D2C, tách ảnh main.
- MainForm đang mở và đã đăng nhập lại sau R2.
- Fixture: UserId1, `admin@s29-8b1.test`, RoleId1/Admin, IsActive1, PasswordHash length168 như các checkpoint trước; không có Order tham chiếu.
- RoleId2 tồn tại với RoleName `Customer`, là non-Admin. FK Users→Roles NO ACTION chấp nhận RoleId2.
- `CK_Roles_AdminIdentity` chỉ ràng buộc định danh hàng Roles và đang enabled/trusted; không cấm User tham chiếu Customer.
- `UX_Users_SingleAdmin` unique/enabled/filter RoleId1: chuyển 1→2 làm số Admin tạm thời bằng 0, hợp lệ. Restore 2→1 chỉ chạy khi không có User khác RoleId1.
- SQL session PID15548 sleeping/open transaction0; không user write request khác. Test snapshot 2/5/2/2/5/4/10/10, stock14.

## A — Script revoke RoleId 1→2

```sql
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRY
    BEGIN TRAN;
    IF DB_NAME()<>N'PhoneStoreDB_StockConcurrencyTest'
        THROW 51000,'Wrong database',1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WITH(HOLDLOCK)
                   WHERE RoleId=2 AND RoleName=N'Customer')
        THROW 51004,'Non-admin role missing',1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WITH(UPDLOCK,HOLDLOCK)
                   WHERE UserId=1 AND Email=N'admin@s29-8b1.test'
                     AND RoleId=1 AND IsActive=1)
        THROW 51001,'Fixture precondition failed',1;

    UPDATE dbo.Users SET RoleId=2
    WHERE UserId=1 AND Email=N'admin@s29-8b1.test'
      AND RoleId=1 AND IsActive=1;
    DECLARE @rows int=@@ROWCOUNT;
    IF @rows<>1 THROW 51002,'Unexpected affected rows',1;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
```

Sau commit phải dùng connection độc lập SELECT đúng UserId/email/IsActive1/RoleId2/RoleName Customer và transaction0 trước thao tác UI.

## B — Script restore RoleId 2→1

```sql
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

BEGIN TRY
    BEGIN TRAN;
    IF DB_NAME()<>N'PhoneStoreDB_StockConcurrencyTest'
        THROW 51000,'Wrong database',1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Roles WITH(HOLDLOCK)
                   WHERE RoleId=1 AND RoleName=N'Admin')
        THROW 51006,'Admin role missing',1;
    IF EXISTS (SELECT 1 FROM dbo.Users WITH(UPDLOCK,HOLDLOCK)
               WHERE RoleId=1 AND UserId<>1)
        THROW 51005,'Another Admin exists; stop',1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WITH(UPDLOCK,HOLDLOCK)
                   WHERE UserId=1 AND Email=N'admin@s29-8b1.test'
                     AND RoleId=2 AND IsActive=1)
        THROW 51003,'Restore precondition failed',1;

    UPDATE dbo.Users SET RoleId=1
    WHERE UserId=1 AND Email=N'admin@s29-8b1.test'
      AND RoleId=2 AND IsActive=1;
    DECLARE @rows int=@@ROWCOUNT;
    IF @rows<>1 THROW 51003,'Restore affected rows invalid',1;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
```

Sau commit phải SELECT độc lập UserId1/RoleId1/Admin/IsActive1, PasswordHash length không đổi, transaction0; sau đó người dùng đăng nhập lại bằng credential hiện có.

Hai script đã compile bằng `SET NOEXEC ON`, exit0; SELECT sau kiểm tra vẫn RoleId1/Admin/IsActive1. Không có DML trong bước chuẩn bị.

## Kịch bản UI và bằng chứng

Trước revoke: mở sửa BrandId6, nhập tên trùng chính xác BrandId1 `S29.8B.1 Test Brand`, chưa Lưu; xác minh đúng một dialog/nút Save. Sau revoke RoleId2, Invoke Save đúng một lần.

Tên trùng là bảo vệ thứ hai: nếu fresh-check hỏng, service duplicate phải từ chối trước SaveChanges. Kết quả mong đợi fresh-check: Brand không đổi, session clear, dialog/shell đóng, LoginForm hiện. Restore RoleId ngay dù UI PASS/FAIL/UNKNOWN; không retry write. Thu thập UI transition, SELECT Brand trước/sau, RoleId trước/revoke/restore, session transaction và login lại. Không dùng Product/Order/Image write.

## Main và kết luận

- Main: 2/2/8/1/40/40/1/1/2, stock740, một Cancelled, một active Admin.
- 40/40 ảnh demo khớp SHA-256 baseline.
- Build/tests không chạy lại; baseline D.4B vẫn 161/161, build 0 warnings/errors.

Trạng thái: **READY**, với điều kiện xác minh lại PID/dialog/fixture/no concurrent write ngay trước UPDATE; cần phê duyệt riêng RoleId `1→2→1`.
