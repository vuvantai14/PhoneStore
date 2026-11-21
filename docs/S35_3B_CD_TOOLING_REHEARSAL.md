# S35.3B — Công cụ Windows pull-based CD và diễn tập cách ly

**PASS cho dry-run với release thật và diễn tập bằng test doubles. BLOCKED cho diễn tập container thật.**

## A. Baseline

`main` ở `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`. Tài liệu S35.3A và thay đổi tiến độ đã có trong working tree. Đã đối chiếu workflow phát hành, Compose business và script kiểm tra CI. Docker Desktop Engine 29.9.2 phản hồi. Kiểm tra lần này chỉ truy vấn Docker Engine và lọc tài nguyên mang nhãn project diễn tập. [Thiết kế S35.3A](S35_3A_WINDOWS_CD_DESIGN.md)

## B. File thay đổi

| File trong `scripts/cd/` | Vai trò |
| --- | --- |
| `Invoke-ReleaseRehearsal.ps1` | Entry point, guard Docker, health và journal |
| `Rehearsal.Release.ps1` | Xác minh GitHub Actions, ZIP, manifest và GHCR |
| `Rehearsal.Engine.ps1` | Triển khai tuần tự, kiểm tra invariants và rollback |
| `docker-compose.rehearsal.yml` | Topology riêng cho lần diễn tập thật sau này |
| `Test-Rehearsal.ps1` | Diễn tập quyết định và phục hồi bằng test doubles |

## C. Kiến trúc đã triển khai

Người vận hành chỉ định run, attempt, SHA và checksum ZIP lấy từ nguồn đã duyệt. Chế độ mặc định xác minh release và registry. Cờ `-ApplyApprovedRelease` là bước duyệt cục bộ rõ ràng để chạy trên project diễn tập. `-Recover` đọc giao dịch dang dở từ journal. Tác vụ định kỳ và tài khoản Windows chuyên dụng sẽ được nghiệm thu ở giai đoạn sau.

Ví dụ dry-run, có thể chạy từ thư mục gốc trong phiên PowerShell đã cho phép chạy script local.

```powershell
.\scripts\cd\Invoke-ReleaseRehearsal.ps1 `
  -RunId 44845223690 -Attempt 1 `
  -Commit 165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07 `
  -ArtifactDigest sha256:93e32c776343f5b1d86d2a711fb7aa768cb956d8b87bd5d54128de68f24c10b1
```

## D. Xác minh release

Công cụ kiểm tra đúng repository, đường dẫn workflow CI, event push, branch main, head SHA và attempt. Ba job bắt buộc phải thành công. ZIP được đọc qua Nightly.link và chỉ được phân tích sau khi SHA-256 trùng cả metadata GitHub và checksum đã duyệt. Tool đọc đúng một entry `images.json`, kiểm tra cặp reference và đối chiếu cả tag lẫn digest với GHCR `linux/amd64`. Dry-run với run thật đã PASS. [Run 44845223690](https://github.com/vuvantai14/PhoneStore/actions/runs/44845223690)

## E. Logic triển khai

Apply dùng Docker context `desktop-linux` và project cố định `phonestore-cd-rehearsal`. Guard kiểm tra nhãn Compose, mạng nội bộ, thành viên mạng, SQL volume, port loopback, mount và database đích. Trước khi thay service, engine kiểm tra cả hai app đang phục vụ và cả hai image rollback còn local. Sau đó pull từng reference theo digest. Công cụ ghi transaction kèm approval, references, image IDs và SQL snapshot vào journal trước thao tác Compose đầu tiên.

Mỗi lần gọi Compose chỉ chọn một trong `api` hoặc `web` với `--no-deps --no-build --pull never`. API phải trả HTTP 200 ở `/api/health/ready` trước khi thay Web. Web phải trả HTTP 200 ở trang chủ. Engine kiểm tra image ID và SQL snapshot, gồm ID, StartedAt, restart count, image, network và mount. Journal nằm dưới `%LOCALAPPDATA%\PhoneStore-CdRehearsal`. Khóa file độc quyền bao trùm apply và recovery của tài khoản vận hành.

## F. Rollback

Rollback đối chiếu hai image đã ghi trước triển khai và phục hồi API rồi Web nếu container đã đổi, bị dừng hoặc mất giữa chừng. Cả hai endpoint và image IDs phải qua kiểm tra lại trước khi ghi `RolledBack`. Nếu SQL invariant đổi và topology vẫn hợp lệ, engine thử phục hồi app và giữ giao dịch ở trạng thái cần xử lý. Nếu guard cách ly thất bại, mọi thao tác Compose bị chặn. Sau gián đoạn, lần apply tiếp theo bị chặn đến khi người vận hành dùng `-Recover` và kiểm tra xong. Cùng run ID hoặc run ID cũ hơn lần đã ghi nhận sẽ bị từ chối.

## G. Bằng chứng cách ly

Compose static PASS cho network nội bộ, volume `phonestore-cd-rehearsal_sql`, app ports `127.0.0.1:5385/5386`, SQL không publish port và hai app không có volume/bind mount. Database riêng theo cấu hình là `PhoneStoreDB_CdRehearsal`, với login cục bộ `cd_rehearsal`. Template đòi hỏi hai credential qua process environment và một DB fixture được provision riêng trước khi chạy. Công cụ deploy chỉ tiếp quản một stack diễn tập đã được chuẩn bị.

Docker chưa có container và volume mang nhãn project diễn tập. RAM Windows khả dụng đo được khoảng 2 GiB. Vì vậy apply, pull layers và rollback Compose thật còn **BLOCKED**. Các ca diễn tập đã dùng state trong bộ nhớ.

## H. Kết quả kiểm tra

| Kiểm tra | Kết quả |
| --- | --- |
| PowerShell parser | PASS 4 file |
| Engine test doubles | PASS 10 ca. Gồm luồng thành công, lỗi áp dụng và readiness từng app, SQL drift, gián đoạn khi Web mất, replay, thiếu image rollback và rollback health thất bại. |
| Manifest và registry | PASS cho manifest hợp lệ, sai attempt, digest sai định dạng, reference lệch và digest registry lệch. |
| GitHub/GHCR dry-run thật | PASS tại run 44845223690, attempt 1. |
| Compose static và whitespace | PASS |

Test Windows chạy bằng `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/cd/Test-Rehearsal.ps1` trong tiến trình con do policy mặc định chặn script. Policy hệ thống giữ nguyên. Chưa chạy test .NET vì thay đổi nằm trong tooling và tài liệu.

## I. Bảo mật và giới hạn

Áp dụng release cần thao tác phê duyệt cục bộ. Workflow public chỉ phát hành dữ liệu release. Public GHCR được kiểm tra bằng quyền đọc của phiên Docker hiện có. Quyền của tài khoản Windows dự kiến dùng cho CD và mô hình credential chỉ đọc của tài khoản đó còn chờ nghiệm thu. Journal chỉ chứa release metadata, IDs và snapshot đã chọn. Entry point báo lỗi tổng quát để tránh ghi secret từ response, môi trường hoặc Compose stderr. Trước khi chạy định kỳ, giới hạn ACL cho một tài khoản vận hành và bảo vệ thư mục script, Compose, approval và journal khỏi sửa đổi trái phép.

## J. GO/NO-GO cho S35.3C

GO để hoàn thiện preflight, chuẩn bị tài nguyên và provision fixture cách ly theo phê duyệt riêng. NO-GO cho business rollout trước khi có bằng chứng pull, triển khai, rollback và SQL invariants trên Docker thật, cùng điều kiện Windows và backup đã nêu tại S35.3A.
