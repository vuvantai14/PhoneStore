# S31.5C2C — Docker Environment & Backup Transfer Preparation

Ngày thực hiện: 2026-11-14  
Mode: safe implementation + validation  
Trạng thái: **PARTIAL — ENVIRONMENT READY / BACKUP COPY APPROVAL PENDING**

Không start/restart container, không create/delete volume, không copy backup, không restore, migration hoặc seed.

## A. Git State

- Branch `main`, HEAD `c6de0474731a1e2397ff77f3790743cbda99c33b`.
- Bảo toàn toàn bộ thay đổi S31.5A–C2B chưa commit.
- `.env` được Git ignore; chỉ các biến không nhạy cảm được cập nhật. Không stage, commit hoặc push.

## B. Environment Readiness

Đã cập nhật trong local ignored `.env`:

| Key | Trạng thái |
|---|---|
| `COMPOSE_PROJECT_NAME` | `phonestore-restore-c2` |
| `WEB_PORT` | 5015 |
| `API_PORT` | 5025 |
| `SQL_PORT` | 14334 |
| `DB_NAME` | `PhoneStoreDB` |
| `PRODUCT_UPLOADS_PATH` | actual uploads directory trong workspace |
| `CATALOG_API_ENABLED` / timeout | configured |

Actual uploads directory tồn tại, có 42 files và đã được xác minh readable ở C1.1. Không có cấu hình ngoài phạm vi cần bảo toàn trong `.env`; môi trường Development được giữ nguyên.

Password line không được in hoặc sửa tự động. `.env.example` không chứa password và đã đồng bộ mô tả port 14334.

## C. Credential Rotation Status

**PASS.** Người dùng đã nhập credential mới trực tiếp trong `.env`; giá trị không đi qua chat/tool output.

Redacted verification:

- password set: PASS;
- length/uppercase/lowercase/digit/special complexity (≥16): PASS;
- khác historical default: PASS;
- `.env` ignored và untracked: PASS.

Quy tắc tiếp tục áp dụng cho secret mới:

- ít nhất 16 ký tự (SQL tối thiểu 8), có uppercase/lowercase/digit/special;
- không trùng password Windows SQL hoặc container cũ;
- không gửi vào chat, terminal command history, source/docs/logs;
- không đổi password Windows SQL hoặc old container.

Static Compose validation bằng `.env` thực PASS (`api`, `db`, `web`) và không in rendered config/connection string. Không có target container/volume được tạo.

## D. Backup Transfer Plan / Result

Source artifact vẫn tồn tại, đúng một verified backup set, 730,566 compressed bytes:

`C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_DOCKER_COPYONLY_20261114_014304166Z_AEB0E519BE794420B7747A5029FE36A5.bak`

**Result: NOT COPIED — awaiting separate approval.**

Kế hoạch:

1. Tạo restricted directory ngoài repository: `C:\PhoneStore\docker-transfer\S31.5C2\<run-id>`.
2. Copy source bằng một Windows manual/elevated operation được phê duyệt; không đổi ACL nguồn, không move/delete/overwrite.
3. Xác minh copied size = 730,566 bytes và SHA-256; giữ hash ngoài secret logs.
4. Nếu quyền cho phép, chạy `RESTORE VERIFYONLY WITH CHECKSUM` trên copy; nếu SQL service không đọc transfer path, không nới ACL tùy ý.
5. Restore override bind-mount transfer directory read-only → `/var/opt/mssql/backup`.
6. Không Git-add `.bak`, không bake image, không public artifact/PII.

Phương án dùng SQL Server để tạo backup thứ hai trực tiếp vào transfer directory sẽ là một BACKUP mới và cần phê duyệt riêng; không tự dùng để tránh ACL copy.

## E. Compose Isolation

`docker compose config` với secret validation tạm thời PASS:

- project: `phonestore-restore-c2`;
- services: `db`, `api`, `web`;
- ports: loopback 14334/5025/5015;
- Web/API dependencies và healthchecks đúng;
- Web uploads: bind mount read-only, source path exists;
- target volume dự kiến `phonestore-restore-c2_mssql_data` chưa tồn tại;
- old `phonestore_mssql_data` không bị tham chiếu/thay đổi.

Internal DNS giữ `db:1433`, `api:8080`; Web API base `http://api:8080/`. Không chạy `compose up` hoặc build.

## F. Resource Readiness

| Hạng mục | Kết quả |
|---|---:|
| Host free RAM | 4.69 GB |
| Docker allocation | 7.64 GB (C2B) |
| Disk C free | 305.71 GB |
| Port 14334 | FREE |
| Old PhoneStore containers | Exited |
| Old SQL volume | Exists, unchanged |

Resources hiện đạt gate ưu tiên ≥4 GB nhưng phải kiểm lại trước start.

## G. Remaining Blockers

1. Chưa có phê duyệt copy backup; Docker chưa đọc được artifact.
2. Restore override read-only backup mount chưa được tạo/duyệt.
3. Chưa có phê duyệt start target DB hoặc `RESTORE DATABASE`.
4. Old Docker DB volume recovery sau exit137 chưa kiểm; target không dùng volume này.

## H. GO / NO-GO for Docker Restore

**PARTIAL.** Environment (bao gồm rotated credential), Compose isolation, uploads và resources đều READY.

**NO-GO cho backup copy/start/restore** cho đến khi:

- người dùng phê duyệt riêng copy artifact;
- copied artifact size/hash/VERIFYONLY đạt;
- restore override/pinned SQL image được duyệt;
- start/restore được phê duyệt riêng sau final checkpoint.
