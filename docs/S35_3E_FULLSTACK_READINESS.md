# S35.3E — Full-stack isolated rehearsal readiness

**PASS cho cấu hình tĩnh. BLOCKED cho chạy full-stack.** Audit ngày 2026-11-21 trên Windows. Cấu hình rehearsal sẵn sàng để kiểm tra tĩnh với image PhoneStore thật. Tài nguyên Windows, provisioning SQL fixture và probe của công cụ CD còn là điều kiện trước khi chạy.

## A. Baseline

`main` và ref theo dõi `origin/main` cùng ở `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`. Index sạch. Working tree đã có cập nhật `PROGRESS.md`, bốn báo cáo S35.3A–D và chín file tooling/fixture chưa commit. Phiên này chỉ cập nhật Compose rehearsal hiện có, báo cáo và tiến độ.

S35.3D đã đạt 5/5 ca trên Docker với ứng dụng HTTP mock, trong đó recovery dùng journal JSON mô phỏng gián đoạn. Engine đã chuẩn hóa transaction đọc từ JSON. Entrypoint vẫn giữ các guard về Compose project, network, SQL volume, cấu hình kết nối và đích image. Bộ test doubles 10/10, các ca manifest/registry và cú pháp PowerShell 5/5 đạt khi chạy lại. [Thiết kế Windows CD](S35_3A_WINDOWS_CD_DESIGN.md) · [Tooling S35.3B](S35_3B_CD_TOOLING_REHEARSAL.md) · [Readiness S35.3C](S35_3C_ISOLATED_DOCKER_READINESS.md) · [Diễn tập mock S35.3D](S35_3D_DOCKER_MOCK_REHEARSAL.md)

## B. Tài nguyên

| Hạng mục | Số đo và đánh giá |
| --- | --- |
| Windows | RAM tổng 15,77 GiB. Ba mẫu cách nhau khoảng 3 giây còn 3,42 / 3,50 / 3,55 GiB. CPU i5-11400H, 6 lõi, 12 luồng, tải snapshot 6%. Ổ C: còn 315,71 GiB. |
| Docker Desktop/WSL2 | Engine 29.9.2, Compose v5.6.1, Linux kernel WSL2 6.18.58.1. Docker thấy 12 CPU và 7,64 GiB RAM. WSL còn khoảng 5.706–5.713 MiB khả dụng, swap chưa dùng. Không thấy `.wslconfig` của tài khoản hiện tại. |
| Business | SQL 1,185 GiB và 1,77% CPU. API 100,1 MiB và 0,51% CPU. Web 138,5 MiB và 0,01% CPU. Container đang chạy báo `HostConfig.Memory=0`, nên các số đo không phải mức tài nguyên tối đa của business. |
| Full-stack rehearsal | Giới hạn SQL 2.304 MiB, 2 CPU. Web/API mỗi service 512 MiB và 0,5 CPU. Cộng 3.328 MiB (3,25 GiB) và 3 CPU. Probe ngắn hạn dự kiến 64 MiB và 0,25 CPU. SQL Server yêu cầu tối thiểu 2 GB để khởi động. Giới hạn cgroup và mức SQL nội bộ 1.536 MiB cần xác minh qua cold start trong lần thực thi. |

RAM Windows và WSL thuộc cùng host. Duy trì ngân sách kế hoạch S35.3C là khoảng 3,5 GiB cho cold start và 1,5 GiB dự phòng. Cả ba mẫu Windows đều dưới 5 GiB. CPU và đĩa đạt preflight, RAM là yếu tố chặn. Đo lại trước pull/bootstrap và giữa hai lần thay container. Nếu thiếu headroom trước rollout hoặc business health suy giảm, dừng phát sinh bước mới và xử lý trạng thái rehearsal theo journal. [Yêu cầu SQL Server trên Linux](https://learn.microsoft.com/en-us/sql/linux/install-upgrade/setup?view=sql-server-ver17) · [MSSQL_MEMORY_LIMIT_MB](https://learn.microsoft.com/en-us/sql/linux/configure/environment-variables?view=sql-server-ver17)

## C. SQL fixture

`scripts/cd/docker-compose.rehearsal.yml` dùng project `phonestore-cd-rehearsal`. Service `db` là SQL Server Developer, `linux/amd64`, pin image `mcr.microsoft.com/mssql/server@sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd`. Volume project `phonestore-cd-rehearsal_sql` là mount ghi duy nhất tại `/var/opt/mssql`. SQL không publish port. Network nội bộ `phonestore-cd-rehearsal_isolated` chỉ cho phép thành viên fixture. SQL healthcheck dự kiến chạy `SELECT 1` qua `sqlcmd` trong container và đọc mật khẩu từ môi trường tiến trình.

Phương án provisioning cho giai đoạn thực thi, sau khi được duyệt:

1. Khởi tạo SQL container riêng với `CD_REHEARSAL_SQL_PASSWORD` mới. Xác minh container ID/label, network, volume, image và endpoint ngay trước provisioning. Chạy client qua loopback bên trong `db` hoặc trên network fixture với DNS `db,1433`. Sử dụng tài khoản provisioning riêng cho schema và seed. Kiểm tra `DB_NAME()` đúng `PhoneStoreDB_CdRehearsal` trước các lệnh schema/seed và dừng khi sai mục tiêu.
2. Chuẩn bị offline SQL idempotent từ migrations hiện có của `PhoneStore.Data` để áp vào fixture trong lần thực thi. Migrations quan sát được là `20261001150857_InitialCreate` và `20261102122856_EnforceSingleAdmin`. InitialCreate đã tạo Roles mặc định. Xác minh `__EFMigrationsHistory` và cấu trúc trước seed. Tool acceptance S32.3B có tên database và tài khoản hardcode cho môi trường khác, nên không gọi trực tiếp trong flow này.
3. Seed một brand và category active, một sản phẩm active mang marker `S353E-FIXTURE-PHONE` với giá dương và tồn 5, và tùy chọn một sản phẩm inactive để kiểm tra lọc catalog. Ghi lại ID từ fixture, không giả định identity. Dùng placeholder của Web nếu thiếu ảnh. Bảng ảnh, Users, Orders và OrderDetails có thể để rỗng trong lần đọc đầu. Trang chủ vẫn cần schema của Orders/OrderDetails vì luồng best sellers có truy vấn nhóm theo đơn.
4. Tạo login ứng dụng `cd_rehearsal` với `CD_REHEARSAL_APP_PASSWORD` khác secret provisioning. Chỉ ánh xạ user vào fixture và cấp SELECT đúng bảng catalog, ảnh và đơn hàng mà luồng đọc cần. Kiểm tra quyền DDL/DML không có bằng truy vấn metadata. Xác minh runtime SQL login đọc được marker, hai bảng Users/Orders không chứa bản ghi. Không nhập account, orders, uploads, backup hoặc data volume từ business.

## D. Image thật và cấu hình

Hai reference của run `44845223690`, attempt 1, đã được truy vấn lại bằng `buildx imagetools inspect`. Cả hai root digest khớp và chứa descriptor `linux/amd64`. Đây là kiểm tra metadata, chưa pull layers hoặc khởi chạy. [Run phát hành GHCR](https://github.com/vuvantai14/PhoneStore/actions/runs/44845223690)

| Service | Reference |
| --- | --- |
| Web | `ghcr.io/vuvantai14/phonestore-web@sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76` |
| API | `ghcr.io/vuvantai14/phonestore-api@sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050` |

`CD_REHEARSAL_API_IMAGE` và `CD_REHEARSAL_WEB_IMAGE` là selectors bắt buộc khi render, có thể chọn image ID rollback cho bootstrap và reference GHCR theo digest cho deploy. Policy fixture là `never`. Chuẩn bị image cục bộ bằng pull rõ digest ở bước được duyệt, không để Compose tự tìm tag. Hai app chạy `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_HTTP_PORTS=8080`. Cả hai nhận `ConnectionStrings__PhoneStoreConnection` trỏ `db,1433/PhoneStoreDB_CdRehearsal`, TLS mã hóa với trust chỉ dành cho chứng chỉ tự ký của fixture, pool tối đa 5. Web bật `CatalogApi__Enabled` và gọi `CatalogApi__BaseUrl=http://api:8080`. Secret đưa qua tiến trình cục bộ. Chỉ render bằng biến tổng hợp khi in hoặc kiểm tra cấu hình. [Compose fixture](../scripts/cd/docker-compose.rehearsal.yml)

- **API:** `/api/health/live` kiểm tra tiến trình. `/api/health/ready` và `/api/health` dùng `CanConnectAsync` tới DB và có thể trả 503. Readiness không đủ chứng minh schema, nên đối chiếu marker qua `/api/v1/products` và `/api/v1/products/{id}`. [HealthController](../src/PhoneStore.Api/Controllers/HealthController.cs)
- **Web:** HTTP `/` là smoke cho trang chủ và truy vấn SQL. `/products` cần có marker để chứng minh catalog fixture. `/products/{id}` có thể dùng API khi feature flag bật và fallback SQL khi API lỗi. Nếu cần chứng minh Web đã gọi API, đối chiếu dấu vết request của detail hoặc quan sát fallback riêng. HTTP 200 của detail một mình không đủ chứng minh API đã phục vụ. [ProductsController](../src/PhoneStore.Web/Controllers/ProductsController.cs) · [ProductDetailResolver](../src/PhoneStore.Web/CatalogApi/ProductDetailResolver.cs)

## E. Cặp rollback

Image inspect xác nhận hai image local có sẵn, chạy `linux/amd64` và khớp ID đang phục vụ business. Chỉ lấy image ID bất biến làm baseline ứng viên. Container config, credentials, volumes và network fixture được tạo riêng. `/api/health/ready` trên API baseline business hiện trả 200, còn readiness/SQL compatibility của cặp này **trên fixture** phải được chứng minh trước khi thay bất kỳ service nào.

| Service | Image ID ứng viên |
| --- | --- |
| Web | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` |
| API | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` |

Khi thực thi, xác minh khác biệt giữa image ID đang phục vụ fixture và image ID pull cho release mới. Lưu cặp trước/sau vào journal. Chặn apply nếu image cũ thiếu, không vượt readiness/catalog hoặc không còn tái sử dụng được dưới tài khoản tác vụ. Rollback khôi phục API trước, sau đó Web nếu đã thay, rồi xác minh image, HTTP và SQL snapshot. [Bằng chứng baseline .NET 10](S34_2R_04_DIRECT_DOCKER_UPGRADE.md)

## F. Bằng chứng cách ly

Compose render với credential tổng hợp và `--env-file NUL` đã PASS cho project/service, volume đúng project, internal network, không có mount Web/API, không có publish SQL, hai app chỉ khai báo bind loopback và giới hạn CPU/RAM. Không thấy container, volume hoặc network rehearsal tồn tại. Cổng Windows `5385/5386` chưa có listener và không rơi vào dải port IPv4 bị loại trừ tại thời điểm đo. Kết quả này là kiểm tra tĩnh, cần lặp lại khi bootstrap. [Docker Compose internal network](https://docs.docker.com/reference/compose-file/networks/)

| Business | Container ID rút gọn | StartedAt UTC | Mount và health |
| --- | --- | --- | --- |
| SQL | `addd99b34bf2` | `2026-11-21T00:27:43.48526107Z` | `phonestore-restore-c2_mssql_data` ghi được tại `/var/opt/mssql`. Không có backup bind trong snapshot. Healthy, restart count 0. |
| API | `b79d2e025e11` | `2026-11-21T00:27:43.503227531Z` | Không có mount. Healthy, restart count 0. |
| Web | `e55bfc7a1117` | `2026-11-21T00:27:43.55036162Z` | Product uploads bind read-only. Healthy, restart count 0. |

Các ID và StartedAt khớp S35.3A và bất biến đã nghiệm thu ở S35.3D. Với lần chạy thật, đối chiếu đủ ID, StartedAt, restart count, image, ports, health, mạng và chi tiết mount trước/sau từng pha. SQL fixture cũng phải có snapshot riêng. Chỉ quan sát SQL business qua Docker metadata.

## G. Kế hoạch diễn tập

Health chính dùng HTTP từ network fixture đến `http://api:8080` và `http://web:8080`. Có thể dùng container probe Python local đã có từ S35.3D, chạy dưới project/network đã đối chiếu, không publish port, mount hoặc truyền secret. Chỉ chạy ngắn hạn, kết thúc probe trước khi engine kiểm tra membership snapshot tiếp theo. Lấy status, JSON kỳ vọng và marker từ response. Không dựa vào curl có sẵn trong image .NET.

Kiểm tra Windows host trong cột kết quả riêng qua `127.0.0.1:5386/api/health/ready` và `127.0.0.1:5385/`, so khớp body với probe nội bộ nếu kết nối được. S35.3D cho thấy Docker Desktop có thể không mở listeners của internal network. Entrypoint hiện chỉ dùng host HTTP trong `Assert-CdHealth`, nên phải bổ sung probe nội bộ có kiểm soát trước diễn tập hoặc chứng minh host binding hoạt động khi preflight. Giữ nguyên network cách ly và ghi `HOST NOT REACHABLE` nếu Windows không kết nối. Chuẩn hóa config-file path Windows trước khi so sánh nhãn Compose để tránh sai khác dấu phân cách.

| Ca | Điều kiện đạt |
| --- | --- |
| 1. Deploy thật | Cặp rollback đạt baseline. API đổi sang image mới và readiness PASS trước khi Web đổi. Image IDs và journal đúng thứ tự. |
| 2. API kết nối SQL fixture | Live/ready đạt từ mạng fixture. API trả marker và ID đã seed. Kết nối của app được xác minh tới fixture với user đã giới hạn. |
| 3. Web truy vấn catalog | `/`, `/products` và detail truy cập được từ network fixture. Danh sách chứa marker fixture. Không có dữ liệu người dùng hoặc đơn hàng thật. |
| 4. API readiness lỗi | Dùng fault fixture đã duyệt, ví dụ cấp sai password chỉ cho container API mới với cùng server/database/login. Live vẫn lên, readiness báo lỗi, Web giữ ID. Trả config đúng trước khi rollback rồi kiểm tra cả cặp. |
| 5. Web readiness lỗi | Khi API mới healthy, áp fault tương tự chỉ cho Web mới hoặc tiêm timeout vào probe Web bằng harness riêng. Kiểm tra probe API giữ 200. Phục hồi cấu hình và cặp image trước khi xác nhận rollback. Ghi rõ lỗi do probe mô phỏng hay HTTP thật. |
| 6. Rollback sau cập nhật một phần | Tiêm lỗi thao tác Web sau API ready. API trở về image cũ, Web giữ baseline nếu chưa bị thay. Health và marker đều khớp. |
| 7. Recovery sau gián đoạn | Ngắt tiến trình riêng sau khi journal đã flush ở bước API. Apply tiếp bị chặn. Dùng `-Recover` đọc file thật, khôi phục image/health và xác minh journal. |
| 8. Business invariants | So sánh snapshot metadata business trước/sau từng ca, đồng thời đối chiếu snapshot SQL fixture. Giữ nguyên ID, StartedAt, health và mounts của business. |

Fault fixture phải được triển khai qua harness hoặc override được duyệt riêng với phạm vi một app và cùng endpoint SQL cách ly. Chưa có fault injector full-stack ở phiên readiness. Khi thay app, giữ lệnh Compose giới hạn `api` hoặc `web` với `--no-deps --no-build --pull never`.

## H. Điều kiện còn thiếu

1. Headroom Windows thấp hơn ngân sách. Kiểm tra lại RAM Windows/WSL và business health trước bootstrap.
2. Chưa provision SQL fixture, runtime login, schema, marker và baseline healthy của hai image rollback. SQL healthcheck và resource caps mới chỉ được render tĩnh.
3. Chưa pull image release về host. Chưa xác minh account tác vụ, host-port behavior hoặc triển khai probe nội bộ cho đường apply/recover. Chưa thử file journal trên full-stack thật.

## I. Quyết định

**NO-GO cho full-stack execution hiện tại. GO cho chuẩn bị provisioning và probe cách ly.** Phiên này xác minh metadata GHCR/Docker, HTTP readiness business và kết quả static Compose. Không chạy full-stack, pull image, migration, seed hoặc thay đổi SQL/volume. `git diff --check` đạt, không chạy bộ test .NET vì chỉ chỉnh cấu hình triển khai và tài liệu.
