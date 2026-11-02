# S29.8D.4C.2B-R1 — Chuẩn bị retry revoke IsActive

Ngày chuẩn bị: 2026-11-02 (Asia/Bangkok). Chỉ kiểm tra đọc/compile-noexec; **không UPDATE**.

## Preflight

- Admin test D.4B vẫn là PID `15548`, executable staging D.4C.1, config `PhoneStoreDB_StockConcurrencyTest` và uploads test.
- MainForm và dialog Brand vẫn tồn tại. `LookupNameInput` còn đúng tên trùng `S29.8B.1 Test Brand`; chưa bấm Lưu.
- Test DB `ONLINE`; UserId1/email `admin@s29-8b1.test` vẫn RoleId1/Admin/IsActive1; filtered unique index `UX_Users_SingleAdmin` enabled, filter RoleId1.
- SQL session Admin không có transaction mở. Lần trước SQL1934 xảy ra trước commit và fixture vẫn nguyên.

## SET options

SQL Server yêu cầu các SET options tương thích khi sửa bảng có filtered index. Một session `sqlcmd` mới được đọc trực tiếp: ANSI_NULLS/PADDING/WARNINGS/CONCAT_NULL_YIELDS_NULL/QUOTED_IDENTIFIER=1, NUMERIC_ROUNDABORT=0 nhưng ARITHABORT=0. Vì defaults có thể phụ thuộc client/session và lần lỗi báo QUOTED_IDENTIFIER, retry không dựa vào mặc định mà đặt đủ:

```sql
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
```

Sau khi SET, `SESSIONPROPERTY` đã xác minh lần lượt `1/1/1/1/1/1/0` trong cùng session.

## Script retry đã chuẩn bị

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
    IF DB_NAME() <> N'PhoneStoreDB_StockConcurrencyTest'
        THROW 51000, 'Wrong database', 1;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.Users WITH (UPDLOCK,HOLDLOCK)
        WHERE UserId=1 AND Email=N'admin@s29-8b1.test'
          AND RoleId=1 AND IsActive=1)
        THROW 51001, 'Fixture precondition failed', 1;

    UPDATE dbo.Users SET IsActive=0
    WHERE UserId=1 AND Email=N'admin@s29-8b1.test'
      AND RoleId=1 AND IsActive=1;
    DECLARE @rows int=@@ROWCOUNT;
    IF @rows<>1 THROW 51002, 'Unexpected affected rows', 1;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
```

Ngay sau commit phải SELECT đúng fixture và xác nhận IsActive0 trước khi người dùng bấm Lưu đúng một lần.

## Script hoàn nguyên bắt buộc

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
    IF DB_NAME() <> N'PhoneStoreDB_StockConcurrencyTest'
        THROW 51000, 'Wrong database', 1;
    IF NOT EXISTS (
        SELECT 1 FROM dbo.Users WITH (UPDLOCK,HOLDLOCK)
        WHERE UserId=1 AND Email=N'admin@s29-8b1.test'
          AND RoleId=1 AND IsActive=0)
        THROW 51003, 'Restore precondition failed', 1;

    UPDATE dbo.Users SET IsActive=1
    WHERE UserId=1 AND Email=N'admin@s29-8b1.test'
      AND RoleId=1 AND IsActive=0;
    DECLARE @rows int=@@ROWCOUNT;
    IF @rows<>1 THROW 51003, 'Restore affected rows invalid', 1;
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK;
    THROW;
END CATCH;
```

Hai script đã được gửi riêng qua `sqlcmd` với `SET NOEXEC ON`; cả hai exit code 0. SELECT sau no-exec xác nhận UserId1 vẫn RoleId1/IsActive1. Đây là kiểm tra compile/no-exec, không phải runtime UPDATE.

## Bảo vệ main và kết luận

- Main vẫn 2/2/8/1/40/40/1/1/2, stock740, một Cancelled, một active Admin.
- 40/40 ảnh demo khớp SHA-256 baseline.
- Không RoleId change, source/deploy/migration/restore/seed/commit/push.

Trạng thái: **READY FOR EXPLICIT RETRY APPROVAL**. Cần xác minh lại dialog, fixture, no concurrent write ngay trước khi chạy; chưa được phép UPDATE trong R1.
