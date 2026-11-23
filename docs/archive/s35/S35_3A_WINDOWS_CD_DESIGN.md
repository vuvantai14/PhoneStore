# S35.3A — Kiến trúc và an toàn Windows CD

Audit chỉ đọc ngày 2026-11-21. **GO cho S35.3B triển khai công cụ kiểm tra và diễn tập cách ly. NO-GO cho rollout business cho tới khi đạt các điều kiện ở mục H.**

## A. Runtime hiện tại

`main` và ref theo dõi `origin/main` ở `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`. Working tree sạch trước audit. Docker Desktop Engine 29.9.2 chạy Linux trên WSL2. Project business là `phonestore-restore-c2`. Model hợp nhất của `docker-compose.yml`, `docker-compose.restore.yml` và `docker-compose.net10.yml` đã qua `python scripts/ci/validate_business_compose.py`.

| Service | Container ID rút gọn | Image ID đang chạy | Port và health |
| --- | --- | --- | --- |
| `api` | `b79d2e025e11` | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` | `127.0.0.1:5025`. Healthy. `/api/health`, `/live` và `/ready` dưới `/api/health` trả HTTP 200. |
| `web` | `e55bfc7a1117` | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` | `127.0.0.1:5015`. Healthy. `/` trả HTTP 200. Bind uploads vào `/app/wwwroot/uploads/products` ở chế độ chỉ đọc. |
| `db` | `addd99b34bf2` | `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | `127.0.0.1:14334`. Healthy qua SQL probe `SELECT 1`. Volume `phonestore-restore-c2_mssql_data` ghi được tại `/var/opt/mssql`. |

Đọc lại được hai descriptor GHCR theo digest đã nghiệm thu. Web là `sha256:7e2970be3e8cc14e548ee58b03ac48652ed3cd2a41fbc62304b7ac988c69de76`, API là `sha256:bf98725379dfb4c3103fa2f4d3e198f9dc5aa74735e2eb86a64ccae2b1919050`. Đây là kiểm tra metadata bằng tài khoản Windows hiện tại. Quyền của tài khoản tác vụ CD và tải layers còn chờ diễn tập. [Run GHCR 44845223690](https://github.com/vuvantai14/PhoneStore/actions/runs/44845223690)

## B. So sánh kiến trúc

| Tiêu chí | A. Self-hosted Windows runner | B. Windows chủ động pull |
| --- | --- | --- |
| Repository public | GitHub có thể giao workflow cho runner. Cần cô lập runner khỏi code PR và giới hạn workflow được quyền điều phối. | GitHub-hosted CI xuất manifest. Máy Windows chỉ nhận dữ liệu release đã duyệt qua kết nối đi ra. |
| Quyền Docker | Tài khoản runner có quyền Docker cũng có khả năng tác động SQL cùng host. | Tài khoản tác vụ cục bộ có quyền Docker. GitHub runner chỉ có quyền build/publish và không điều khiển Docker của host. |
| Chi phí và vận hành | Điều phối trực tiếp trên Actions. Cần duy trì runner biệt lập, quyền đăng ký, cập nhật và thông tin xác thực. | Dùng Windows Task Scheduler và công cụ cục bộ. Cần duy trì bộ kiểm tra release, trạng thái và nhật ký triển khai. |
| Tự động hóa | Job triển khai được xếp hàng ngay sau khi điều kiện workflow và approval đạt. | Tác vụ phát hiện release đã duyệt theo chu kỳ. Có độ trễ và có thể chờ khi máy hoặc Docker Desktop offline. |
| Rollback và SQL | Có thể dùng cùng quy trình rollback. Khả năng bảo vệ SQL phụ thuộc cách cô lập runner. | Rollback dựa vào nhật ký và image local, có thể xử lý khi GitHub mất kết nối. Công cụ cố định giới hạn mục tiêu ở Web/API. |

## C. Kiến trúc đề xuất

Chọn **B** cho PhoneStore. GitHub-hosted Actions tạo và kiểm tra release. Một tác vụ Windows do người vận hành quản lý đọc run và artifact được duyệt, xác minh dữ liệu, tải image theo digest rồi gọi quy trình cố định tại máy triển khai. Giai đoạn đầu dùng duyệt cục bộ cho bộ `run ID + attempt + commit + checksum ZIP`. Sau khi diễn tập thành công có thể chuyển sang đọc một nguồn phê duyệt được bảo vệ. Giữ công cụ cục bộ và cấu hình triển khai trong vị trí có ACL riêng. Artifact chỉ được đọc như JSON và tác vụ không checkout hoặc thực thi script lấy từ run.

## D. Chuỗi triển khai dự kiến

1. Giữ khóa độc quyền cục bộ cho project và kiểm tra nhật ký lần chạy trước. Xác minh workflow path `.github/workflows/ci.yml`, repository, event push, branch main, SHA được duyệt, run/attempt, ba job CI/publish thành công và artifact còn hạn. Chỉ chọn artifact duy nhất đúng tên và đúng run, kiểm tra checksum ZIP theo metadata GitHub trước khi đọc `images.json`. Nếu dùng API tải ZIP yêu cầu xác thực, giới hạn quyền đọc Actions cho repository. Tách credential đọc artifact và credential registry.
2. Kiểm tra JSON và chỉ nhận hai repository image đã định, tag đầy đủ chứa đúng SHA, digest SHA-256 hợp lệ và reference khớp. Đối chiếu descriptor theo tag và digest, xác nhận `linux/amd64`. Từ chối artifact sai nguồn, thiếu trường, release cũ chưa có phê duyệt rollback riêng, hoặc attempt sai. Release đã triển khai với cùng cặp digest là thao tác lặp không đổi trạng thái.
3. Kiểm tra identity Docker Engine/context, project, container SQL, volume, mạng, port, bind, tình trạng health và cấu hình Web/API đang chạy so với baseline cục bộ đã duyệt. Chụp image ID của hai service, thứ tự Compose và mô hình hiệu lực đã lọc bí mật. Dừng trước khi thay đổi nếu phát hiện drift ngoài baseline hoặc thiếu image rollback.
4. Dùng chính ba file Compose theo thứ tự base, restore, net10 và chỉ thêm override cuối dành cho image của `api` và `web`. Override net10 hiện dùng image ID cục bộ, nên GHCR reference phải nằm sau nó trong thứ tự merge. So sánh model đã resolve để chỉ cho phép thay hai giá trị image, giữ đúng project, env-file, ports, network, uploads và định nghĩa `db`. Kiểm tra schema ứng dụng tương thích database hiện tại và đường startup của image trước triển khai.
5. Pull rõ từng reference `name@sha256:…` trước khi thay container và ghi image ID local thực tế. Sau đó mỗi lệnh Compose chỉ nhận đúng một service, với project `-p phonestore-restore-c2`, các file cố định và dạng `up -d --no-deps --no-build --pull never api` rồi tương tự cho `web`. `web` khai báo phụ thuộc `api` và `db`, còn `api` phụ thuộc `db`. `--no-deps` ngăn Compose khởi động hoặc reconcile dependencies khi thao tác service đích. Xác minh kế hoạch trên môi trường cách ly trước khi cho phép lệnh thật.
6. Đợi API healthy và `GET /api/health/ready` HTTP 200 trong timeout sau khi xác nhận image ID của container. Sau đó cập nhật Web, đợi Docker healthy, kiểm tra HTTP 200 tại `/`, catalog đọc được và ảnh mẫu đọc được. Dùng một catalog GET bổ sung để kiểm chứng đường dữ liệu, vì health của Web là kiểm tra trang chủ. Đối chiếu SQL sau từng bước, kiểm tra cả hai service lần cuối rồi lưu release thành công. [Docker Compose up](https://docs.docker.com/reference/cli/docker/compose/up/)

## E. Rollback

Ghi bền trạng thái trước mỗi lần thay container. Giữ hai image ID cũ còn dùng được tại host và giữ bản cấu hình chạy trước đó trong suốt cửa sổ rollback. Lưu cùng một cặp image, kể cả khi chỉ một service cần hoàn nguyên. Phục hồi từng service bằng override dùng image ID cũ, cùng scope Compose và `--pull never`.

| Điểm lỗi | Phục hồi |
| --- | --- |
| Manifest, baseline hoặc pull lỗi trước khi thay service | Kết thúc lần chạy và giữ cặp đang phục vụ. |
| API cập nhật lỗi khi Web còn phiên bản cũ | Khôi phục API cũ, kiểm tra API readiness và xác minh lại Web. |
| API mới đã healthy nhưng Web deploy/readiness lỗi | Khôi phục API cũ trước để Web cũ có backend tương thích. Nếu Web đã bị thay hoặc lần cập nhật dừng giữa chừng, khôi phục tiếp Web cũ. Kiểm tra lại image ID và health của cả hai service. |
| Rollback hoặc kiểm tra bất biến SQL lỗi | Khóa lần triển khai tiếp theo và chuyển xử lý cho người vận hành. Giữ nhật ký và trạng thái thực tế để đối chiếu. |

Trong cả deploy và rollback có thể xuất hiện cặp Web/API khác phiên bản trong thời gian ngắn. Chỉ chấp nhận release có contract tương thích giữa hai phiên bản liền kề hoặc có cửa sổ bảo trì đã được duyệt. Khi tiến trình bị gián đoạn, đọc nhật ký và đối chiếu container thực tế trước khi tiếp tục hoặc phục hồi.

## F. Bảo vệ SQL

SQL runtime có container ID đầy đủ `addd99b34bf2f3b68811f75f8da49f3cb67fb665b3722dbaeb4309436abc50a6`, `StartedAt=2026-11-21T00:27:43.48526107Z` và nhãn config files chỉ ghi file base. Cấu hình merge thêm SQL image pin, giới hạn tài nguyên và backup bind read-only tại `/var/opt/mssql/backup`. Runtime SQL chỉ có data volume. Ghi nhận sự khác biệt này trong baseline đã duyệt và kiểm tra runtime theo baseline đó. Reconcile cả stack có thể đưa cấu hình SQL đã merge vào container mới.

Trước triển khai, sau mỗi service và sau rollback, đối chiếu SQL container ID, StartedAt, restart count, image ID, project/service label, mạng, bind port, kiểu/tên/nguồn/đích/quyền của các mount và health. Đối chiếu DB đích bằng kiểm tra đọc `DB_NAME()` hoặc readiness trên connection đã xác minh. Có thể đối chiếu ID và StartedAt của các container ngoài hai service được phép thay. Lệnh CD chỉ cho phép `api` hoặc `web`. Các tác vụ DDL, migration, seed, restore, thao tác volume và SQL lifecycle nằm ngoài quy trình.

File FULL COPY_ONLY CHECKSUM `/var/opt/mssql/data/PhoneStoreDB_S34_2R04_COPYONLY_20261119T044154834Z_6448f0d2.bak` vẫn có trên runtime, 827.392 byte. Đây là file `.bak` duy nhất thấy tại thư mục dữ liệu đã kiểm tra. Báo cáo S34.2R-04 ghi `RESTORE VERIFYONLY WITH CHECKSUM` thành công lúc tạo. Phiên này xác minh sự tồn tại và kích thước. Việc kiểm lại checksum, lịch sử backup bên ngoài thư mục và bản sao độc lập còn chờ xác minh. Backup nằm cùng volume dữ liệu, cần có bản sao đã kiểm chứng ngoài volume trước lần rollout business. [Bằng chứng backup S34.2R-04](../s34/S34_2R_04_DIRECT_DOCKER_UPGRADE.md)

## G. Kiểm soát bảo mật và quyền

- Duy trì CI pull request trên GitHub-hosted. Bảo vệ main và thay đổi workflow bằng review. Giới hạn quyền publish ở job GHCR. Nếu sau này chọn A, cô lập runner trong host riêng, giới hạn workflow tin cậy, dùng approval cho environment và chỉ cho phép branch đã duyệt. Label runner dùng để định tuyến, quyền thực thi cần được kiểm soát riêng. [GitHub Actions secure use](https://docs.github.com/en/actions/reference/security/secure-use) · [Deployment environments](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments)
- Tác vụ B dùng quyền tối thiểu trên Windows và ACL cho script, Compose, thông tin xác thực và state. Chỉ cấp quyền đọc GHCR nếu package yêu cầu. Dùng credential store và xác minh Docker context dưới chính tài khoản tác vụ. Database credentials chỉ có trong cấu hình runtime local. Quyền Docker là quyền nhạy cảm trên host dùng chung.
- Dùng một khóa cục bộ xuyên suốt deploy và rollback. Lưu lịch sử SHA, run attempt, cặp digest và trạng thái đã áp dụng. Từ chối replay hoặc hạ phiên bản nếu thiếu phê duyệt riêng. Kiểm tra artifact schema và digest dù run đã PASS để dữ liệu đầu vào không thể đổi tên service, đường dẫn hoặc tham số lệnh.

## H. Điều kiện để triển khai business

1. Thử dưới tài khoản tác vụ thật với Docker Desktop đang hoạt động, sau restart Windows và khi chưa đăng nhập nếu đó là chế độ vận hành dự kiến. Xác minh truy cập GitHub artifact và GHCR bằng quyền chỉ đọc, cùng khả năng pull cả cặp theo digest trên môi trường cách ly.
2. Lập baseline runtime đã duyệt, xác minh kế hoạch Compose chỉ thay đổi service đích, và diễn tập lỗi API, lỗi Web sau khi API thành công, điều kiện mất tiến trình và rollback. Xác minh health và bất biến SQL sau mỗi tình huống.
3. Có backup đã kiểm chứng ngoài volume, image rollback còn truy cập được và cơ chế phê duyệt release cho business.

## I. Quyết định

S35.3B có thể xây công cụ kiểm tra manifest và Compose, nhật ký cục bộ và diễn tập với mục tiêu cách ly. Business rollout chờ hoàn tất các điều kiện mục H. S35.3A chỉ kiểm tra Git, Docker metadata, Compose tĩnh, HTTP health, metadata GHCR và sự tồn tại của backup.
