# S31.5C1.1 — Docker Resource Cleanup & Environment Readiness

Ngày thực hiện: 2026-11-14  
Mode: safe execution + verification  
Kết luận: **PARTIAL — stop PASS, volume preservation PASS, C2 restore BLOCKED**

Người dùng phê duyệt dừng đúng `phonestore-web` và `phonestore-db`. Task không xóa container/volume, không chạy Compose, không thay database hoặc `.env`.

## A. Git State

- Branch `main`, HEAD `c6de0474731a1e2397ff77f3790743cbda99c33b`.
- Bảo toàn toàn bộ working tree S31.5A/B/C1; C1.1 chỉ thêm tài liệu và progress.
- Không reset, clean, stash, stage, commit hoặc push.

## B. Container State Before / After

### Preflight

| Container | ID rút gọn | Image | Trạng thái | Storage |
|---|---|---|---|---|
| `phonestore-web` | `25356d995e14` | `phonestore-web` | running | không mount |
| `phonestore-db` | `d3d6f67871b1` | SQL Server 2022 (`16.0.4315.2`) | running/healthy | volume `phonestore_mssql_data` → `/var/opt/mssql` RW |

Cả hai có Compose project label `phonestore`; không phải container hệ thống khác. Trước stop, truy vấn read-only trong Docker SQL ghi nhận:

- other user-database requests: 0;
- user-database transactions có log records: 0.

Chỉ chạy:

```text
docker stop phonestore-web phonestore-db
```

Không chạy `rm`, `down`, `volume rm`, `system prune` hoặc dừng container/process khác.

### Sau stop

| Container | Kết quả |
|---|---|
| `phonestore-web` | Exited (0), container vẫn tồn tại |
| `phonestore-db` | Exited (137), `OOMKilled=false`, restart count 0, container vẫn tồn tại |

Exit 137 cho thấy DB không thoát trong grace period và bị stop cưỡng bức sau timeout; không có bằng chứng OOM. Log tail không ghi nhận graceful shutdown tại thời điểm stop. Volume vẫn tồn tại, nhưng consistency/recovery của **Docker DB cũ** chưa được runtime-verify vì task không cho restart. Không suy đoán data loss và không tự restart.

Windows SQL Server không bị dừng: service `MSSQLSERVER` vẫn Running và source `PhoneStoreDB` ONLINE.

## C. RAM Before / After

| Thời điểm | Host free | Docker Web | Docker DB |
|---|---:|---:|---:|
| Trước stop | 1.24 GB | 80.82 MiB | 1.394 GiB |
| Sau stop | 2.16 GB | stopped | stopped |

RAM free tăng khoảng 0.92 GB nhưng 2.16 GB vẫn là headroom thấp cho một SQL Server Linux mới cùng Windows SQL, Docker Engine và Web/API. Ngưỡng vận hành đề xuất trước C2 là tối thiểu 3 GB, ưu tiên 4 GB free tại checkpoint; đo lại ngay trước start và dừng nếu memory pressure tăng.

## D. Docker Volume Safety

- `phonestore_mssql_data` vẫn tồn tại, driver `local`, mountpoint Docker giữ nguyên.
- Không tạo/xóa volume hoặc network.
- Container DB cũ vẫn tham chiếu đúng volume.
- Chưa kiểm SQL recovery của volume sau exit 137; việc restart old DB cần phê duyệt riêng và không phải điều kiện để tạo target volume mới, nhưng phải được ghi nhận nếu old stack còn cần dùng.

## E. Port Availability

Sau stop, các port sau đều không có listener:

- 5015 — Web mới;
- 5025 — API mới;
- 14334 — SQL target mới;
- 14333 — port stack cũ đã giải phóng.

Port state là snapshot; C2 phải kiểm lại ngay trước `compose up`.

## F. Environment Readiness

`.env` local tồn tại, không bị sửa và không hiển thị giá trị. Trạng thái redacted:

| Variable | `.env` |
|---|---|
| `MSSQL_SA_PASSWORD` | SET |
| `DB_NAME` | SET |
| `WEB_PORT` | SET |
| `SQL_PORT` | SET |
| `COMPOSE_PROJECT_NAME` | MISSING |
| `API_PORT` | MISSING |
| `PRODUCT_UPLOADS_PATH` | MISSING |

`.env.example` có đầy đủ keys và không có default password. Trước deployment, người dùng phải tự chốt project name, API port và uploads path; đồng thời xác nhận secret không phải credential lịch sử. C1.1 không tự thêm/sửa `.env`.

Static validation dùng transient environment riêng (`phonestore-acceptance`, 5015/5025/14334, DB `PhoneStoreDB`, random non-persistent validation secret, actual uploads path):

- `docker compose config --quiet`: exit 0;
- services: `db`, `api`, `web`.

Không chạy build hoặc `compose up`.

## G. Uploads Readiness

- Actual directory: `C:\PhoneStore\PhoneStore\src\PhoneStore.Web\wwwroot\uploads\products`.
- 42 files; 42/42 mở read-only thành công.
- C1 trước đó xác minh 41 referenced files, missing 0, orphan 1.
- Compose có thể bind-mount directory này read-only vào Web, nhưng `.env` chưa trỏ tới path.

Để clone sang máy khác, không phụ thuộc ignored repository folder: dùng asset package riêng gồm đúng 41 referenced files + relative-path/hash manifest; không public backup/PII. C1.1 không copy/move/delete file.

## H. SQL Backup Readiness

- Windows `MSSQLSERVER`: Running dưới `NT Service\MSSQLSERVER`.
- `PhoneStoreDB`: ONLINE, compatibility 160, recovery FULL.
- SQL Server `16.0.1000.6` Developer.
- Default backup directory tồn tại: `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup`.
- Disk C free: 305.96 GB.
- Latest full backup 2026-11-02 là COPY_ONLY + CHECKSUM, compressed 0.54 MB; đã cũ.

Agent vẫn không đọc được ACL backup directory (`Access is denied`). Backup history chứng minh SQL service từng ghi được, nhưng current write permission phải được xác nhận bằng chính BACKUP mới ở C2 sau phê duyệt; không tạo probe file bằng user account và không nới ACL.

Kế hoạch C2 giữ nguyên: unique timestamp/GUID filename, FULL `COPY_ONLY, CHECKSUM, COMPRESSION`, không overwrite, rồi `RESTORE VERIFYONLY WITH CHECKSUM`, HEADERONLY và FILELISTONLY.

## I. Remaining Blockers

1. Host free RAM chỉ 2.16 GB; chưa đạt headroom đề xuất 3–4 GB.
2. `.env` thiếu `COMPOSE_PROJECT_NAME`, `API_PORT`, `PRODUCT_UPLOADS_PATH`.
3. Cần xác nhận local SA secret đã rotate nếu credential lịch sử từng được dùng.
4. Backup directory ACL chưa đọc trực tiếp được; backup mới cần phê duyệt riêng.
5. Docker DB cũ exit 137; old volume recovery chưa được kiểm vì không restart.
6. Docker image build/runtime healthchecks và target SQL restore vẫn NOT TESTED.

## J. GO / NO-GO for S31.5C2

### Đã PASS

- Đúng hai old containers đã dừng và vẫn tồn tại.
- Volume cũ còn nguyên; không destructive cleanup.
- Windows source SQL/PhoneStoreDB vẫn Running/ONLINE.
- Target ports free.
- Upload files readable.
- Compose static validation với isolated transient variables PASS.

### Quyết định

**PARTIAL / NO-GO cho full C2 restore hiện tại** do RAM free còn thấp và `.env` chưa hoàn chỉnh.

Có thể chuyển sang **GO backup-only** sau một phê duyệt riêng vì backup dùng Windows SQL và disk đủ. Chỉ GO start/restore container khi:

1. người dùng hoàn thiện `.env` (không gửi password vào chat);
2. RAM free được đo lại đạt tối thiểu 3 GB, ưu tiên 4 GB;
3. backup mới VERIFYONLY PASS;
4. unique Compose project/volume và SQL image version/digest được xác nhận;
5. có phê duyệt rõ cho start container và RESTORE vào volume mới.
