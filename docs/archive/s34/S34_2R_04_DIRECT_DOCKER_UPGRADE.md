# S34.2R-04 — Triển khai trực tiếp .NET 10 cho Docker business

Ngày 19/11/2026. **PASS cho rollout Web/API và smoke chỉ đọc.**

## A. Preflight

Git `main` ở `4d77bd0`, giữ diff R-01/R-02/R-03. Xác minh hai image .NET 10 từ R-02 và hai image .NET 8 rollback đều có cục bộ. Cả năm container healthy, API/Web business và acceptance đều trả HTTP 200. Database đích `PhoneStoreDB` trên container SQL `addd99b34bf2`, volume `phonestore-restore-c2_mssql_data`, ONLINE. Môi trường Compose khớp cấu hình Web/API đang chạy, bao gồm SQL target, cổng loopback và bind mount uploads read-only. RAM mẫu đầu 3,033–3,361 GiB.

## B. Backup Verification

Một FULL `COPY_ONLY`, `CHECKSUM`, `COMPRESSION`, `NOINIT` vào đường dẫn mới trên volume SQL đang dùng.

- File trong container `/var/opt/mssql/data/PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak`.
- Hoàn thành lúc 04:42:01 theo đồng hồ SQL. 842 trang, metadata backup full loại D, `is_copy_only=1`, `has_backup_checksums=1`.
- `RESTORE VERIFYONLY WITH CHECKSUM` xác nhận backup set hợp lệ. File đọc được trong container, 827.392 bytes, mode 640.
- SHA-256 `aa76e08284c319312b301af267562400cf5abc4926e16e57e3cc9d190580a706`.
- Backup nằm trên cùng volume SQL. Chưa xuất bản sao độc lập hoặc chạy restore.

## C. Deployment

Business project `phonestore-restore-c2`. Thêm [override image .NET 10](../docker-compose.net10.yml), dùng cùng `docker-compose.yml` và `docker-compose.restore.yml`. Mô hình Compose đã resolve chỉ thay hai image selector. Rollout lần lượt API rồi Web với `--no-deps --no-build --pull never --force-recreate`. RAM ngay trước API/Web lần lượt 3,613/3,524 GiB. Sau recreate đã đối chiếu lại đích SQL, cấu hình môi trường, cổng, network và mounts.

| Service | Container cũ → mới | Image .NET 8 → .NET 10 |
| --- | --- | --- |
| API | `a9e6d10bb415` → `b79d2e025e11` | `sha256:9b20d979aa6af3ff2866f2611d05cd404d1d8b600f66f3165804c812f565f894` → `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` |
| Web | `eea0f96f38eb` → `e55bfc7a1117` | `sha256:75bfbc07a0abfd0370e75c0166bce6dac4d005bdd7dc29d0ecf322415ce4ff3d` → `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` |

## D. Web/API Runtime

Cả hai container báo `Microsoft.AspNetCore.App 10.0.15` và `Microsoft.NETCore.App 10.0.15`, Docker healthy.

| Scope | HTTP GET đã PASS |
| --- | --- |
| API port 5025 | `/api/health`, `/api/health/live`, `/api/health/ready`, Catalog list 40 sản phẩm/trang 1 có 10 mục, product detail ID 40, brands, categories, Swagger UI và JSON đều 200. |
| Web port 5015 | Home, Catalog page 2, Product Detail ID 40, Login và ảnh `oneplus-nord-3.jpg` đều 200. |

## E. Database Integrity

SQL `addd99b34bf2` giữ image, StartedAt và volume. `PhoneStoreDB` ONLINE, 40 Products, 15 Orders, 37 OrderDetails, 41 ProductImages. Count/checksum khớp 8/8 bảng; blocking 0 lúc đo. SHA-256 manifest 42 file uploads business khớp trước/sau.

## F. Acceptance Integrity

API `b47fefaf6c87` và Web CT02 `84066ac4b44f` giữ ID, image, StartedAt, healthy và HTTP 200. `PhoneStoreDB_AcceptanceTest` ONLINE, count/checksum khớp 8/8 bảng, gồm 5 Products, 4 Orders, 4 OrderDetails và 2 ProductImages. SHA-256 manifest 5 file uploads acceptance khớp. Cả năm container healthy ở hậu kiểm. RAM cuối 3,431 GiB.

## G. Rollback Readiness

Hai image .NET 8 còn cục bộ. Override rollback đã được kiểm tra static config và chỉ đổi image selectors. File local bỏ qua bởi Git là `scratch/s34-r04-rollback-6a40cf1695cf4c829465d93a0d1e58ae.json`. Nếu cần rollback, dùng hai file Compose business và override rollback, thay riêng `api` rồi `web` với các cờ đã nêu, xác minh healthy từng service. Rollback chưa chạy vì rollout PASS. Giữ SQL và dữ liệu, xử lý restore backup theo phê duyệt riêng.

## H. Remaining Risks

Người dùng bỏ qua isolated acceptance R-03. Phiên này chỉ smoke GET, chưa nghiệm thu đăng nhập, session qua restart, checkout, CRUD, reporting/WinForms trên provider mới. Không chạy full suite lại; bằng chứng gần nhất là **263/263** ở R-02. Web thay process nên session in-memory có thể mất. Khi recreate business về sau, dùng kèm override .NET 10 để giữ image đã nghiệm thu. Không thay đổi Admin, SQL credentials, schema hoặc acceptance.

## I. Suggested Commit Message

`chore(runtime): migrate PhoneStore to .NET 10 LTS`. Chưa stage, commit hoặc push. Dừng tại R-04.
