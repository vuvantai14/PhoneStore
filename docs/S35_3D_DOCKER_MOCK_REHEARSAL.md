# S35.3D — Diễn tập CD với Docker HTTP mock

## A. Baseline

Audit ngày 2026-11-21 tại `main`, HEAD `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`. Tài liệu S35.3A–C và CD tooling S35.3B còn trong working tree. Docker Desktop dùng Linux/WSL2, Windows còn 316,45 GiB trên ổ C: và 4,55 GiB RAM lúc kiểm tra đầu. Cả ba container business healthy. [Readiness S35.3C](S35_3C_ISOLATED_DOCKER_READINESS.md)

## B. File thay đổi

- `scripts/cd/Test-DockerMockRehearsal.ps1` kiểm tra cách ly, cung cấp adapter Docker cho engine, chạy các tình huống và dọn fixture.
- `scripts/cd/mock/compose.yml`, `Dockerfile` và `readiness.py` định nghĩa fixture tách biệt.
- `scripts/cd/Rehearsal.Engine.ps1` chuẩn hóa transaction đọc từ JSON khi recovery. `scripts/cd/Test-Rehearsal.ps1` bổ sung kiểm tra qua bước serialize/deserialize.
- Báo cáo này và `docs/PROGRESS.md` ghi bằng chứng nghiệm thu.

Adapter mock gọi engine hiện có, dùng image tags local, HTTP fixture và một journal JSON trong bộ nhớ. Entry point áp dụng release GHCR giữ riêng các kiểm tra an toàn của S35.3B.

## C. Thiết kế fixture

Ba container `api`, `web`, `sentinel` chạy cùng HTTP server nhẹ trên Python Alpine ghim digest. Các image `old`, `new` và `unready` có ID khác nhau. API dùng `/api/health/ready`, Web dùng `/`, còn `/version` trả phiên bản. Image `unready` trả HTTP 503 ở đường readiness.

Sentinel đại diện tài nguyên phải giữ nguyên. Snapshot lưu ID, StartedAt và restart count của sentinel. Đây là phép thử phạm vi thao tác container. Các ca SQL invariant mismatch vẫn dùng test double riêng.

## D. Bằng chứng cách ly

Project `phonestore-cd-rehearsal` và network nội bộ `phonestore-cd-rehearsal_mock`. Gate trước khi chạy kiểm tra model Compose, tài nguyên trùng tên, listeners trên 5385/5386 và trạng thái business. Fixture không có volume, bind mount, SQL service, connection string hoặc secret. Các container chạy non-root, bỏ capability, filesystem chỉ đọc và giới hạn PID.

Kiểm tra thực tế network chỉ thấy ba thành viên fixture. Route IPv4 trong API và Web chỉ có subnet fixture, không có default route.

Compose dành loopback Windows 5385 cho Web và 5386 cho API. Lần chạy đầu cho thấy Docker Desktop không mở hai listener từ network nội bộ trong topology này, dù healthcheck trong container đã healthy. Probe sau đó chạy bằng `docker compose exec -T` đến HTTP loopback trong đúng container. Host HTTP trên 5385/5386 không thuộc bằng chứng PASS của phiên này.

Trong triển khai và phục hồi, adapter chỉ chạy Compose theo một service `api` hoặc `web`, với `--no-deps --no-build --pull never`. Cleanup dừng và gỡ ba service của fixture rồi gỡ network mang nhãn đúng project. Image mock được giữ local để chạy lại.

## Chạy lại

Từ thư mục gốc, chạy kiểm tra cấu hình trước bằng chế độ `-ValidateOnly`. Chạy bỏ cờ này sẽ build và chạy container mock trong Docker Desktop của máy. Dùng tiến trình PowerShell con nếu execution policy mặc định của máy yêu cầu.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-DockerMockRehearsal.ps1 -ValidateOnly
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-DockerMockRehearsal.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-Rehearsal.ps1
```

## E. Kết quả kiểm tra

| Ca | Mức kiểm tra | Bằng chứng |
| --- | --- | --- |
| Deploy API rồi Web | Docker thật, app mock | Hai container đổi ID. Journal ghi `Ready-api` trước `Before-web`. Hai HTTP probe xác nhận image mới. |
| API readiness lỗi | Docker thật, app mock | API trả 503. Rollback phục hồi image cũ và Web giữ ID ban đầu. |
| Web readiness lỗi | Docker thật, app mock | API mới đã healthy. Web trả 503. Cả API và Web trở lại image cũ. |
| Rollback sau cập nhật một phần | Docker thật, lỗi gọi Web được tiêm | API đổi image thật. Adapter báo lỗi trước thao tác Web. API trở về phiên bản cũ và Web giữ nguyên ID. |
| Recovery giữa deployment | Docker thật, gián đoạn được mô phỏng | API mới đã healthy. Journal JSON dang dở chặn apply. `-Recover` trả API về image cũ, giữ nguyên ID Web và xác minh HTTP 200 của cả hai. |
| SQL invariant mismatch | Test double | Engine thử phục hồi image app và giữ journal ở trạng thái cần xử lý khi SQL invariant sai. |
| Manifest không hợp lệ | Test double | Từ chối sai attempt, digest sai định dạng và reference lệch. |
| Digest mismatch | Registry test double | Descriptor của tag khác digest đã khai báo bị từ chối. |

PowerShell parser PASS 5 file. Python syntax, Compose tĩnh, `git diff --check` và whitespace của file mới PASS. Bộ kiểm thử `Test-Rehearsal.ps1` đạt 10/10 ca engine và các ca manifest/registry.

## F. Bằng chứng rollback

Với các ca lỗi readiness, journal chỉ ghi `RolledBack` sau khi image ID khớp baseline và cả hai probe trả HTTP 200. Khi Web đã được thay, engine khôi phục API trước rồi đến Web. Khi lỗi xảy ra trước thao tác Web, ID Web giữ nguyên.

Lần recovery đầu phát hiện transaction nạp bằng `ConvertFrom-Json` có thể chưa chứa thuộc tính `StepBefore`. Engine cần thêm thuộc tính này khi phục hồi. Bản sửa chuyển dữ liệu transaction thành hashtable để có thể ghi các snapshot bước rollback, giữ nguyên các guard và kiểm tra health.

## G. Bất biến business

Baseline business dùng SQL `addd99b34bf2`, API `b79d2e025e11`, Web `e55bfc7a1117`. Đối chiếu ID đầy đủ, StartedAt, restart count, health và mounts của cả ba trước và sau đạt PASS. SQL business tiếp tục dùng volume `phonestore-restore-c2_mssql_data`. Fixture dùng sentinel thay cho SQL ở các bước kiểm tra của engine. Các ca mismatch vẫn tách riêng bằng test doubles.

## H. Tài nguyên

Giới hạn mỗi service 64 MiB RAM và 0,25 CPU, tổng 192 MiB và 0,75 CPU. Các mẫu `docker stats` ghi 12,33–18,35 MiB RAM và 0,01–11,16% CPU mỗi container. Trong hai lần chạy hoàn tất từng nhóm ca, RAM Windows khả dụng khoảng 3,87–4,23 GiB. Script đo RAM host và CPU trước thao tác cập nhật, với ngưỡng dừng 2,5 GiB dành cho fixture nhẹ.

## I. Giới hạn còn lại

Các ca này dùng image HTTP local và journal mock trong bộ nhớ. Ca recovery tái tạo trạng thái Docker và journal sau bước API bằng JSON. Kịch bản ngắt tiến trình thật và đọc file journal bền vững còn chờ thử. Việc pull GHCR vào runtime, các image PhoneStore thật, Windows host-port readiness theo topology SQL rehearsal và truy vấn SQL thật cần nghiệm thu riêng.

## J. Quyết định

**PASS 5/5 ca dùng Docker với app mock. GO cho bước chuẩn bị diễn tập full-stack cách ly.** Business rollout vẫn chờ fixture SQL riêng, tài khoản tác vụ và rollout/rollback của các image thật.
