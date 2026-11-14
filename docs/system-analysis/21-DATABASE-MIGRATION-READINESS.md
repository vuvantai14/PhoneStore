# S31.5C1 — PhoneStore Database Migration Readiness

Ngày xác minh: 2026-11-14  
Mode: **read-only preflight + backup/restore plan**  
Kết luận: **PASS preflight / BLOCKED thực thi C2 ở trạng thái tài nguyên và `.env` hiện tại**

Không có backup, restore, migration, seed, DML, container/volume lifecycle hoặc thay đổi `.env` trong C1.

## A. Git State

- Branch `main`, HEAD `c6de0474731a1e2397ff77f3790743cbda99c33b`.
- Working tree giữ nguyên toàn bộ S31.5A/B: sáu tracked files đang sửa và ba files mới (`19`, `20`, API Dockerfile) trước khi thêm báo cáo C1.
- Không reset, clean, stash, stage, commit hoặc push.

## B. Docker & Host Resources

### Docker đã xác minh read-only

- Docker Engine/CLI `29.9.2`, Compose `v5.6.1`, Linux `amd64`, 12 CPUs, Docker memory 7.639 GiB, storage `overlayfs`.
- `phonestore-web`: running, port host 8080, khoảng 80 MiB.
- `phonestore-db`: running/healthy, port host 14333, khoảng 1.394 GiB.
- Không start, stop, restart hoặc xóa container/network/volume.

### Host

| Hạng mục | Giá trị tại thời điểm đo |
|---|---:|
| RAM tổng | 15.77 GB |
| RAM free | **1.08 GB** |
| Docker memory allocation | 7.639 GiB |
| Disk C free | 305.96 GB |
| Windows SQL private / working set | 582.1 / 154.5 MB |

Ports 5015, 5025, 14334 và 1433 không có TCP listener; 14333 đang do stack cũ sử dụng. Port free chỉ là snapshot, phải kiểm lại ngay trước C2.

**Resource blocker:** 1.08 GB free thấp hơn mức an toàn để chạy thêm SQL Server Linux bên cạnh Windows SQL và container cũ. C2 không được start container mới cho đến khi RAM free/headroom được cải thiện và đo lại. Không tự dừng process/container để giải phóng RAM.

## C. Source SQL Snapshot

Thông tin sau được xác minh trực tiếp bằng Windows Authentication và SELECT metadata:

| Thuộc tính | Kết quả |
|---|---|
| Instance/service | default `MSSQLSERVER`, Running |
| Service identity | `NT Service\MSSQLSERVER` |
| SQL Server | 2022 Developer 64-bit, `16.0.1000.6` |
| Database | `PhoneStoreDB`, ONLINE |
| Compatibility / recovery | 160 / FULL |
| Size | 16.00 MB (data 8 MB, log 8 MB) |
| Logical data file | `PhoneStoreDB` |
| Logical log file | `PhoneStoreDB_log` |
| EF migrations | `20261001150857_InitialCreate`, `20261102122856_EnforceSingleAdmin`, EF 8.0.28 |

Business row counts:

| Roles | Users | Brands | Categories | Products | ProductImages | Orders | OrderDetails |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 3 | 8 | 2 | 40 | 41 | 12 | 28 |

### Backup history

Latest known full backup finished `2026-11-02 09:08:40`: type full, COPY_ONLY, CHECKSUM, 5.39 MB logical / 0.54 MB compressed. Hai full backups trước đó cũng COPY_ONLY+CHECKSUM. Backup mới nhất đã cũ hơn live snapshot nên không phù hợp để chuyển dữ liệu hiện tại.

## D. Backup Readiness

SQL reports default backup directory:

`C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup`

Directory tồn tại và backup history chứng minh SQL service từng ghi thành công. Agent không đọc được ACL hiện tại (`UnauthorizedAccessException`), vì vậy quyền ghi **chưa được chứng minh lại trực tiếp**. C2 phải xác nhận SQL service identity/path và file đích chưa tồn tại trước BACKUP; không thử tạo file bằng account Codex.

Kế hoạch C2 sau phê duyệt:

1. Chụp lại source snapshot/counts và kiểm tra không có backup/restore đang chạy.
2. Sinh tên mới có UTC timestamp + GUID, ví dụ `PhoneStoreDB_DOCKER_COPYONLY_<timestamp>_<guid>.bak`; xác minh không tồn tại.
3. Chạy đúng một full backup: `COPY_ONLY, CHECKSUM, COMPRESSION`; không dùng `INIT`, không overwrite file cũ.
4. Xác minh `msdb` ghi đúng một backup set mới và file size hợp lý.
5. Chạy `RESTORE VERIFYONLY ... WITH CHECKSUM`, sau đó `RESTORE HEADERONLY` và `RESTORE FILELISTONLY` read-only.
6. Không thay đổi source database và không dùng backup cũ nếu backup mới/verify thất bại.

Developer Edition hỗ trợ backup compression. Với database 16 MB và lịch sử compressed dưới 1 MB, disk 305.96 GB không phải blocker; vẫn dành tối thiểu 100 MB cho artifact/metadata và không đặt backup trong Git workspace.

## E. Docker Restore Plan

Thiết kế độc lập, không dùng lại `phonestore-db`, port 14333, network hoặc volume cũ:

- Compose project riêng, ví dụ `phonestore-migration-c2`.
- SQL host bind loopback `127.0.0.1:14334:1433`, kiểm lại port trước start.
- Named volume mới, scoped theo project; tuyệt đối không `external` tới volume cũ.
- SQL Server Linux 2022 build **cùng major và không cũ hơn** source 16.0.1000.6. Trước C2 cần pin tag/digest đã kiểm; stack cũ từng xác minh target 16.0.4315.2 tương thích theo hướng upgrade.
- Database target ban đầu không tồn tại; restore thành `PhoneStoreDB`, không `WITH REPLACE`.

### Đưa backup vào Linux container

Sau VERIFYONLY và phê duyệt copy artifact, copy file backup sang thư mục transfer riêng ngoài repository, ví dụ `C:\PhoneStore\docker-transfer\S31.5C\<run-id>`. Bind-mount directory đó read-only vào `/var/opt/mssql/backup`. Không mount backup vào image và không commit artifact.

Trong container, chạy `RESTORE FILELISTONLY`; dùng logical names đã xác minh:

- `PhoneStoreDB` → `/var/opt/mssql/data/PhoneStoreDB.mdf`
- `PhoneStoreDB_log` → `/var/opt/mssql/data/PhoneStoreDB_log.ldf`

Restore dùng `WITH MOVE`, không `REPLACE`. Sau restore: kiểm ONLINE/compatibility/migrations/counts/constraints; có thể chạy `DBCC CHECKDB ... WITH NO_INFOMSGS` sau khi đánh giá tài nguyên. Restart-persistence test chỉ thực hiện ở acceptance sau và không xóa volume.

Nếu bind mount từ `Program Files` bị Docker Desktop/ACL chặn, dừng và dùng transfer directory được phê duyệt; không nới ACL SQL Server hoặc copy tùy ý.

## F. Product Images

Đối chiếu SQL + disk:

- ProductImages rows: 41; unique URLs: 41.
- Disk directory: `src/PhoneStore.Web/wwwroot/uploads/products`.
- Disk files: 42.
- Referenced files missing: 0.
- Orphan disk files: 1; giữ nguyên, không xóa/move/rename.

Compose S31.5B bind-mount host uploads read-only vào Web. `.env` hiện chưa có `PRODUCT_UPLOADS_PATH`, nên Docker Web mới chưa thể start đúng cấu hình.

Portable demo package đề xuất (không tạo trong C1): sanitized/demo database backup + đúng 41 referenced files + manifest relative path/SHA-256/size + restore/runbook. Orphan để ngoài package nhưng ghi inventory. Package chứa PII hoặc backup thật phải ở storage riêng, không GitHub; nếu public cần quy trình anonymization được phê duyệt.

## G. Environment Variables

`.env` local tồn tại và không được đọc ra báo cáo. Trạng thái redacted:

| Variable | Trạng thái |
|---|---|
| `MSSQL_SA_PASSWORD` | SET |
| `DB_NAME` | SET |
| `WEB_PORT` | SET |
| `SQL_PORT` | SET |
| `PRODUCT_UPLOADS_PATH` | **MISSING** |
| `API_PORT` | **MISSING** |
| `COMPOSE_PROJECT_NAME` | **MISSING** |

Compose source yêu cầu password và uploads path, Web/API dùng `db,1433`, Web gọi `http://api:8080/`. Local appsettings/Admin vẫn giữ cấu hình Windows hiện tại. C1 không sửa `.env` hoặc connection strings.

Trước C2 người dùng phải tự bổ sung các biến thiếu, chọn project name/ports độc lập và xác nhận password không phải credential lịch sử. Không hiển thị password qua `docker compose config` hoặc logs.

## H. Security Risks

- Working tree Compose/example/guide không còn default credential (0 matching current files).
- Git history vẫn có một commit matching cấu hình credential cũ: `6dad07dc111d...`; không rewrite history trong C1.
- Nếu credential lịch sử từng được dùng thật, rotate cho stack **mới** trước deployment. Không rotate container cũ trong C1.
- Backup nguồn chứa Users, recipient address/phone và Orders; không lưu trong repository/public artifact.
- `sa` vẫn là app credential trong Compose; least-privilege SQL login là thay đổi database cần phê duyệt riêng.

## I. C2 Execution Checklist

### Gate trước backup

- [ ] Git diff và source DB snapshot được ghi nhận; không có DML/migration.
- [ ] RAM free đủ theo ngưỡng đã chốt; không còn resource blocker.
- [ ] SQL service/DB ONLINE; không có backup/restore operation khác.
- [ ] Unique backup filename không tồn tại; SQL service có thể ghi default backup directory.
- [ ] User phê duyệt rõ BACKUP và artifact copy ngoài workspace.

### Backup/verify

- [ ] FULL `COPY_ONLY, CHECKSUM, COMPRESSION` hoàn tất đúng một lần.
- [ ] `RESTORE VERIFYONLY WITH CHECKSUM`, HEADERONLY, FILELISTONLY PASS.
- [ ] Backup path/hash/size/time được ghi, không lộ PII/credential.

### Gate trước container/restore

- [ ] `.env` mới/isolated đủ required vars; password rotated nếu cần.
- [ ] 5015/5025/14334 free; unique Compose project/network/volume confirmed.
- [ ] SQL image tag/digest pinned và target version không cũ hơn source.
- [ ] Transfer directory bind read-only, backup readable trong container.
- [ ] User phê duyệt start container và RESTORE vào volume mới.

### Restore/post-check

- [ ] Target database không tồn tại; restore không dùng `REPLACE`.
- [ ] `WITH MOVE` dùng đúng logical names/Linux paths.
- [ ] ONLINE, migrations, eight row counts, constraints và image references khớp.
- [ ] Source DB, old containers/volume và 42 source image files không đổi.
- [ ] Không chạy app write acceptance trước phê duyệt riêng.

### Stop conditions

Dừng ngay nếu RAM/headroom không đủ, port/object name xung đột, backup verify fail, target SQL cũ hơn source, file logical names lệch, target DB/volume đã tồn tại, commit outcome không rõ, hoặc source counts thay đổi ngoài checkpoint.

## J. GO / NO-GO

**PASS cho S31.5C1 read-only readiness và kế hoạch kỹ thuật.**

**NO-GO cho thực thi C2 ở trạng thái hiện tại**, vì:

1. host chỉ còn 1.08 GB RAM free, không an toàn để chạy thêm SQL container;
2. `.env` thiếu uploads path/API port/project name cho stack isolated;
3. ACL ghi backup chưa được kiểm trực tiếp ở phiên này;
4. cần phê duyệt riêng cho BACKUP, artifact copy, start container và RESTORE.

Khi bốn gate trên được xử lý, kế hoạch chuyển thành **CONDITIONAL GO** cho một run C2 tuần tự, có checkpoint và stop conditions rõ ràng.
