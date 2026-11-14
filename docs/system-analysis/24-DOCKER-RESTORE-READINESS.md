# S31.5C2B — Docker SQL Restore Readiness

Ngày xác minh: 2026-11-14  
Mode: safe preparation + read-only validation  
Kết luận: **PASS readiness audit / BLOCKED restore execution**

Không copy backup, không start/restart container, không tạo/xóa volume, không restore, không sửa `.env` và không thay database trong S31.5C2B.

## A. Backup Accessibility

Artifact S31.5C2A:

`C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_DOCKER_COPYONLY_20261114_014304166Z_AEB0E519BE794420B7747A5029FE36A5.bak`

Xác minh read-only qua SQL Server:

- file exists, parent directory exists;
- đúng một `msdb` backup set khớp path;
- finish `2026-11-14 08:44:18` local;
- COPY_ONLY=1, backup checksum=1;
- compressed size 730,566 bytes;
- C2A `RESTORE VERIFYONLY WITH CHECKSUM`, HEADERONLY và FILELISTONLY đều PASS.

Windows SQL service đọc được artifact; Codex user và SQL `OPENROWSET` không đọc raw file do OS ACL error 5. Docker chưa có mount và không được coi là có quyền đọc file này.

### Phương án transfer không đổi ACL nguồn

Sau phê duyệt riêng:

1. Tạo directory ngoài repository, ví dụ `C:\PhoneStore\docker-transfer\S31.5C2\<run-id>`; giới hạn quyền cho user thực hiện và Docker Desktop, không public/share.
2. Dùng một thao tác copy Windows được người dùng phê duyệt (manual/elevated chỉ để đọc file nguồn), không đổi ACL của SQL backup directory và không xóa/move source.
3. Tính SHA-256 trên bản copy, đối chiếu size 730,566 bytes; chạy Windows SQL `RESTORE VERIFYONLY WITH CHECKSUM` trên bản copy nếu SQL service có quyền đọc transfer directory.
4. Bind-mount **directory copy** read-only vào target DB container tại `/var/opt/mssql/backup`.
5. Không đưa `.bak` vào image, Git, uploads hoặc public directory.

S31.5C2B không tạo directory/copy/hash. Nếu copy cần nới ACL nguồn hoặc kết quả hash/size lệch, dừng.

## B. Host Resources

| Hạng mục | Kết quả |
|---|---:|
| Host RAM total/free | 15.77 / **4.68 GB** |
| Docker memory allocation | 7.64 GB |
| Disk C free | 305.72 GB |
| Port 14334 | FREE |
| Running containers | 0 |

`phonestore-web` vẫn Exited(0), `phonestore-db` vẫn Exited(137); không restart. Các Narration containers cũng Exited và ngoài phạm vi. `phonestore_mssql_data` cũ vẫn tồn tại.

RAM hiện đạt ngưỡng ưu tiên 4 GB đã đặt ở C1.1, nhưng phải đo lại ngay trước start; dừng nếu dưới 3 GB hoặc có memory pressure. Chạy tuần tự DB → restore/verify → API/Web, không build nặng song song.

## C. Environment Readiness

`.env` tồn tại nhưng **không sẵn sàng**. Không hiển thị giá trị; chỉ đánh giá match:

| Điều kiện isolated plan | Kết quả |
|---|---|
| `COMPOSE_PROJECT_NAME` present | FAIL |
| `WEB_PORT=5015` | FAIL |
| `API_PORT=5025` | FAIL |
| `SQL_PORT=14334` | FAIL |
| `DB_NAME=PhoneStoreDB` | FAIL |
| `PRODUCT_UPLOADS_PATH` configured | FAIL |
| `MSSQL_SA_PASSWORD` set/complex | PASS |
| Password khác historical default | **FAIL** |

Người dùng phải tự cập nhật `.env`, không gửi password vào chat. Giá trị không nhạy cảm được đề xuất:

- project `phonestore-restore-c2`;
- host ports 5015/5025/14334;
- target DB `PhoneStoreDB`;
- uploads path tuyệt đối `C:/PhoneStore/PhoneStore/src/PhoneStore.Web/wwwroot/uploads/products` (42 files, C1 xác minh 41 referenced + 1 orphan).

Password phải được rotate sang secret mạnh mới, không trùng Git history. S31.5C2B không sửa/đọc ra secret.

## D. Compose Isolation

Static render dùng transient values, không `.env` và không tạo Docker objects:

| Thành phần | Rendered plan |
|---|---|
| Project | `phonestore-restore-c2` |
| Services | `db`, `api`, `web` |
| DB port | `127.0.0.1:14334→1433` |
| API port | `127.0.0.1:5025→8080` |
| Web port | `127.0.0.1:5015→8080` |
| Network key | `phonestore-net`, project-scoped |
| Volume key | `mssql_data`, actual target `phonestore-restore-c2_mssql_data` |

Target volume name chưa tồn tại. Không dùng `phonestore_mssql_data` cũ và không dùng fixed `container_name`.

Local SQL image đã có digest:

`mcr.microsoft.com/mssql/server@sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd`

Image label/build đã xác minh trước là SQL Server 2022 `16.0.4315.2`, cùng major và mới hơn source `16.0.1000.6`. Restore execution nên pin digest này trong restore override để tránh `2022-latest` thay đổi ngoài dự kiến.

Restore override cần bổ sung duy nhất cho `db`:

- pinned image digest;
- read-only bind mount transfer directory → `/var/opt/mssql/backup`.

Không tạo override file trong C2B; nội dung/path phải được duyệt cùng bước copy/restore.

## E. Restore Execution Plan

Chỉ thực hiện sau phê duyệt riêng, theo checkpoint:

1. **Artifact gate:** copy được duyệt; size/hash xác minh; `.bak` readable từ Docker bind mount.
2. **Environment gate:** `.env` isolated đủ keys, rotated password, ports free, RAM ≥3 GB (ưu tiên ≥4 GB).
3. **Object gate:** project/network/volume/container target names chưa tồn tại; old volume/container không bị tham chiếu.
4. Tạo restore override và chạy chỉ service DB của project mới (`up -d db`); không start Web/API.
5. Xác minh target SQL version ≥ source, DB `PhoneStoreDB` chưa tồn tại và backup visible tại `/var/opt/mssql/backup/...bak`.
6. Chạy `RESTORE FILELISTONLY` trong target, xác nhận logical names `PhoneStoreDB`, `PhoneStoreDB_log`.
7. Chạy đúng một `RESTORE DATABASE [PhoneStoreDB] ... WITH MOVE`:
   - data → `/var/opt/mssql/data/PhoneStoreDB.mdf`;
   - log → `/var/opt/mssql/data/PhoneStoreDB_log.ldf`;
   - không `WITH REPLACE`, không migration/seed.
8. Xác minh ONLINE, compatibility160, 2 migrations, row counts 2/3/8/2/40/41/12/28, constraints và 41 image URLs.
9. Source Windows DB/counts và old Docker objects phải không đổi.
10. Persistence acceptance sau phê duyệt: controlled stop/start **target DB only**, rồi lặp ONLINE/migrations/counts. Không `down -v`.
11. Chỉ sau DB PASS mới start API rồi Web; health/connectivity acceptance là bước riêng.

Mỗi ghi/restore chạy một lần. Nếu timeout/commit outcome không rõ, SELECT target state/msdb trước; không retry mù quáng.

## F. Risks

1. `.env` dùng historical default credential — security blocker bắt buộc rotate.
2. Backup chưa được copy vào Docker-readable transfer directory.
3. Restore/start chưa được phê duyệt; không suy quyền từ task readiness.
4. Old Docker DB exit137; old volume recovery chưa kiểm, nhưng target plan không dùng volume đó.
5. Source backup chứa PII; copy cần ACL/storage hạn chế, không GitHub.
6. Docker Desktop memory và host RAM có thể thay đổi trước start.
7. Current main Compose dùng floating SQL tag; restore phải pin digest.
8. Target app vẫn dùng `sa`; least-privilege login là thay đổi DB/security riêng.

## G. GO / NO-GO

**PASS cho S31.5C2B readiness analysis và isolated restore plan.**

**BLOCKED / NO-GO cho copy, container start và RESTORE hiện tại**, vì:

- `.env` chưa isolated-ready và password trùng historical default;
- backup chưa có Docker-readable verified copy;
- chưa có phê duyệt riêng cho copy/start/restore.

Sau khi người dùng tự hoàn thiện `.env`/rotate secret và phê duyệt transfer artifact, có thể chuyển sang pre-execution checkpoint. RAM/ports/volume isolation hiện đạt nhưng phải được recheck tại thời điểm chạy.
