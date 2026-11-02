# S29.8D.4C.2B — Runtime revoke Admin, kịch bản A

Ngày thử: 2026-11-02 (Asia/Bangkok). Phạm vi chỉ `PhoneStoreDB_StockConcurrencyTest`, UserId 1, IsActive; không chạy kịch bản RoleId.

## Precheck

- Admin test D.4B PID `15548`, executable staging D.4C.1, config đúng test DB/uploads test; một MainForm.
- Fixture đúng `admin@s29-8b1.test`, UserId1, RoleId1/Admin, IsActive1, không Order tham chiếu.
- SQL session Admin sleeping, open transaction 0; không user write request khác.
- Test snapshot: Roles2, Users5, Brands2, Categories2, Products5, Images4, Orders10, Details10, stock14.
- Main snapshot 9/9: 2/2/8/1/40/40/1/1/2, stock740, một Cancelled, một active Admin; 40/40 ảnh khớp baseline.
- Người dùng mở dialog sửa BrandId6, nhập tên trùng `S29.8B.1 Test Brand`, chưa bấm Lưu. UI Automation xác minh đúng dialog và giá trị.

## Kết quả lệnh revoke

Transaction có guard `DB_NAME`, fixture, concurrent write và `@@ROWCOUNT=1` được gọi một lần. SQL Server từ chối câu `UPDATE` trước khi thay dữ liệu:

`UPDATE failed ... incorrect settings: 'QUOTED_IDENTIFIER'` (SQL error 1934).

Nguyên nhân: bảng Users có filtered unique index `UX_Users_SingleAdmin`; session `sqlcmd` cần bộ SET options tương thích khi UPDATE. `XACT_ABORT` + TRY/CATCH đã rollback transaction. Không tự retry vì lệnh sửa khác kế hoạch đã duyệt và quy tắc yêu cầu dừng khi lỗi.

## Hậu kiểm

- UserId1 vẫn RoleId1/Admin/IsActive1; PasswordHash length không đổi. Vì revoke chưa xảy ra nên không cần chạy lệnh hoàn nguyên 0→1.
- BrandId1/6 và toàn test snapshot không đổi; SQL session Admin open transaction 0.
- Không bấm Lưu dialog; fresh authorization, session clear và quay về LoginForm **chưa được kiểm thử**.
- Main snapshot vẫn 9/9, stock740, một Cancelled, một active Admin; 40/40 ảnh SHA-256 không đổi.

## Bước cần phê duyệt mới

Nếu tiếp tục kịch bản A, chạy lại đúng một lần với các SET options bắt buộc cho filtered index trước transaction:

```sql
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
```

Sau đó giữ nguyên guard/UPDATE/rollback plan đã audit. Không thực hiện RoleId. Dialog hiện có phải được xác minh lại trước retry.

## Kết luận

- Precheck và rollback/hậu kiểm: **PASS**.
- Revoke `1→0`: **FAIL trước commit** do SET option; database không đổi.
- Runtime authorization/session/UI: **NOT TESTED**.
- Restore: **NOT REQUIRED**, fixture vẫn active.
- Tổng thể D.4C.2B: **PARTIAL / STOPPED**, chờ phê duyệt retry đã sửa SET options.
