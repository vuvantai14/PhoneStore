-- Run only on the newly created, isolated S29.4 database. No test rows are committed.
SET NOCOUNT ON;
-- SQL Server filtered indexes require these session options for DML (sqlcmd defaults differ).
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT OFF;
IF DB_NAME() <> N'PhoneStoreDB_MigrationTest'
    THROW 51020, 'Wrong database. This test must never run on PhoneStoreDB.', 1;
IF EXISTS (SELECT 1 FROM dbo.Users)
    THROW 51021, 'Expected empty test Users table; refusing to touch existing data.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleId = 1 AND RoleName = N'Admin')
    THROW 51022, 'Admin role mapping is incorrect.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Users')
    AND name = N'UX_Users_SingleAdmin' AND is_unique = 1 AND has_filter = 1 AND is_disabled = 0)
    THROW 51023, 'Single Admin index missing or disabled.', 1;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Roles')
    AND name = N'CK_Roles_AdminIdentity' AND is_disabled = 0 AND is_not_trusted = 0)
    THROW 51024, 'Admin identity constraint missing or untrusted.', 1;

DECLARE @FirstActive bit = 0, @DuplicateRejected bit, @RoleRejected bit, @CustomerRoleId int;
SELECT @CustomerRoleId = RoleId FROM dbo.Roles WHERE RoleName = N'Customer';
IF @CustomerRoleId IS NULL THROW 51025, 'Customer role is missing.', 1;

WHILE 1 = 1
BEGIN
    BEGIN TRY
        BEGIN TRANSACTION;
        INSERT dbo.Users (RoleId, FullName, Email, PasswordHash, IsActive)
        VALUES (1, N'Rollback-only Admin', N's29-first@example.invalid', N'non-login-test-marker', @FirstActive);
        SET @DuplicateRejected = 0;
        BEGIN TRY
            INSERT dbo.Users (RoleId, FullName, Email, PasswordHash, IsActive)
            VALUES (1, N'Rollback-only second Admin', N's29-second@example.invalid', N'non-login-test-marker', 1 - @FirstActive);
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() NOT IN (2601, 2627) THROW;
            SET @DuplicateRejected = 1;
        END CATCH;
        IF @DuplicateRejected <> 1 THROW 51026, 'Second Admin was not rejected.', 1;
        IF XACT_STATE() <> 1 THROW 51027, 'Test transaction is not committable.', 1;
        INSERT dbo.Users (RoleId, FullName, Email, PasswordHash, IsActive)
        VALUES (@CustomerRoleId, N'Rollback Customer One', N's29-one@example.invalid', N'non-login-test-marker', 1),
               (@CustomerRoleId, N'Rollback Customer Two', N's29-two@example.invalid', N'non-login-test-marker', 1);
        IF (SELECT COUNT(*) FROM dbo.Users WHERE RoleId = @CustomerRoleId) <> 2
            THROW 51028, 'Multiple Customers were not accepted.', 1;
        IF (SELECT COUNT(*) FROM dbo.Users WHERE RoleId = 1) <> 1
            THROW 51029, 'Expected exactly one rollback-only Admin.', 1;
        ROLLBACK TRANSACTION;
        PRINT CONCAT('PASS: first Admin active=', @FirstActive, '; duplicate rejected; two Customers accepted; ROLLBACK complete.');
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
    IF @FirstActive = 1 BREAK;
    SET @FirstActive = 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;
    SET @RoleRejected = 0;
    BEGIN TRY
        UPDATE dbo.Roles SET RoleName = N'Other' WHERE RoleId = 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
        SET @RoleRejected = 1;
    END CATCH;
    IF @RoleRejected <> 1 THROW 51030, 'Admin role mapping change was not rejected.', 1;
    ROLLBACK TRANSACTION;
    PRINT 'PASS: Admin role mapping protected; ROLLBACK complete.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

IF EXISTS (SELECT 1 FROM dbo.Users) THROW 51031, 'Unexpected test Users remain.', 1;
SELECT DB_NAME() AS DatabaseName, COUNT(*) AS RemainingTestUsers FROM dbo.Users;
SELECT RoleId, RoleName FROM dbo.Roles ORDER BY RoleId;
SELECT name, is_unique, filter_definition FROM sys.indexes
WHERE object_id = OBJECT_ID(N'dbo.Users') AND name IN (N'UX_Users_SingleAdmin', N'IX_Users_RoleId');
SELECT name, is_disabled, is_not_trusted FROM sys.check_constraints WHERE name = N'CK_Roles_AdminIdentity';
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
