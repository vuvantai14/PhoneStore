# S31.5C2A — PhoneStore Safe Database Backup

Ngày preflight: 2026-11-14  
Mode: backup only + verification  
Trạng thái: **PASS — BACKUP CREATED / VERIFYONLY + HEADERONLY + FILELISTONLY PASS**

Backup được thực hiện sau phê duyệt riêng. Không có `RESTORE DATABASE`, DML, migration, seed, container action hoặc thay đổi `.env`.

## A. Preflight

| Hạng mục | Đã xác minh read-only |
|---|---|
| SQL service | `MSSQLSERVER` Running, account `NT Service\MSSQLSERVER` |
| SQL version | 2022 Developer 64-bit, `16.0.1000.6` |
| Database | `PhoneStoreDB`, ONLINE |
| Size / compatibility / recovery | 16.00 MB / 160 / FULL |
| Migrations | InitialCreate + EnforceSingleAdmin, EF 8.0.28 |
| Active BACKUP/RESTORE requests | 0 |
| Disk C free | 305.71 GB |
| Docker old stack | Web Exited(0), DB Exited(137); không restart |

Row-count checkpoint trước backup:

| Roles | Users | Brands | Categories | Products | ProductImages | Orders | OrderDetails |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 3 | 8 | 2 | 40 | 41 | 12 | 28 |

## B. Backup Destination

SQL Server default backup directory:

`C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup`

SQL service xác nhận parent directory tồn tại và nhìn thấy 3 files `.bak`. `msdb` có 3 successful full backup sets; mới nhất hoàn thành 2026-11-02 09:08:40, COPY_ONLY + CHECKSUM, compressed 0.54 MB.

Agent không đọc được Windows ACL trực tiếp (`Access is denied`) và không thay ACL/chạy probe file bằng Administrator. Khả năng SQL service liệt kê thư mục cùng successful backup history là bằng chứng gián tiếp; lệnh backup được phê duyệt sẽ là kiểm chứng write permission thực tế. Nếu SQL trả access denied, task phải dừng, không nới ACL tự động.

Tên đích đã reserve và xác minh `File Exists=0`, parent exists:

`C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_DOCKER_COPYONLY_20261114_014304166Z_AEB0E519BE794420B7747A5029FE36A5.bak`

Tên dùng UTC timestamp + GUID, không trùng file cũ. Không dùng `INIT`, `FORMAT` hoặc overwrite.

## C. Backup Execution

Đã chạy đúng một lệnh FULL backup, không retry:

- `COPY_ONLY` — không ảnh hưởng differential base;
- `CHECKSUM` — checksum pages/backup;
- `COMPRESSION` — Developer Edition hỗ trợ dù server default hiện là off;
- `NOINIT` mặc định tới unique new file;
- `STATS` chỉ để quan sát tiến độ, không thay dữ liệu.

Kết quả SQL Server:

- 809 pages processed (808 data + 1 log);
- hoàn thành thành công lúc `2026-11-14 08:44:18` theo giờ SQL Server;
- đúng một matching backup set tại file đích;
- logical backup size: `6,701,056` bytes;
- compressed backup size: `730,566` bytes;
- `IsCopyOnly=1`, `HasBackupChecksums=1`.

Không overwrite file cũ, không thay database schema/business data.

## D. Verification

| Kiểm tra | Kết quả |
|---|---|
| `RESTORE VERIFYONLY ... WITH CHECKSUM` | PASS: “The backup set on file 1 is valid.” |
| `RESTORE HEADERONLY` | PASS: database `PhoneStoreDB`, FULL, compressed, COPY_ONLY, checksum, not damaged, compatibility 160, source SQL 16.0.1000 |
| `RESTORE FILELISTONLY` | PASS: `PhoneStoreDB` (data/PRIMARY) và `PhoneStoreDB_log` (log) |
| `msdb` path/set count | PASS: đúng một set khớp file mới |
| File size | 730,566 compressed bytes theo HEADERONLY/msdb |
| SHA-256 | UNKNOWN: direct user read và SQL `OPENROWSET` đều bị OS error 5 Access denied |

Không thay ACL hoặc dùng quyền Administrator để bypass. SHA-256 không nằm trong acceptance bắt buộc; integrity đã được SQL Server xác minh bằng backup checksum và VERIFYONLY. Không chạy `RESTORE DATABASE`.

## E. Source Integrity

Hậu kiểm sau verification:

- SQL service Running; `PhoneStoreDB` ONLINE;
- 2 EF migrations không đổi;
- counts không đổi: Roles2, Users3, Brands8, Categories2, Products40, ProductImages41, Orders12, OrderDetails28;
- active backup/restore requests: 0;
- hai Docker containers vẫn Exited.

Không phát hiện external row-count change trong cửa sổ backup.

## F. Backup Artifact Location

Artifact nằm tại:

`C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_DOCKER_COPYONLY_20261114_014304166Z_AEB0E519BE794420B7747A5029FE36A5.bak`

File nằm ngoài Git workspace. C2A không copy sang repository/public directory, không upload GitHub và không thay `.env`.

Việc copy sang Docker transfer directory chỉ được thực hiện ở bước sau với phê duyệt riêng, sau khi VERIFYONLY PASS.

## G. Remaining Risks

1. Backup chứa PII (Users, phone/address/orders); phải bảo vệ ACL và không public.
2. SHA-256 chưa tính được do ACL; không giảm độ tin cậy của SQL checksum/VERIFYONLY nhưng cần xử lý khi copy artifact được phê duyệt.
3. Host free RAM vẫn thấp cho restore container; không liên quan Windows backup nhưng vẫn chặn Docker restore.
4. Docker DB old volume recovery chưa kiểm sau exit 137; không ảnh hưởng source backup nhưng cần ghi nhận.

## H. GO / NO-GO for Docker Restore

Fresh backup đã **VERIFYONLY PASS** nên điều kiện artifact đạt.

**NO-GO cho Docker restore cho đến khi có phê duyệt riêng.** Các gate còn lại: artifact transfer được duyệt, RAM headroom đủ, isolated `.env`/project/volume, target image/version được chốt và quyền start/RESTORE được cấp rõ ràng.
