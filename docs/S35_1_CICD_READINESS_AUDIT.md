# S35.1 — Khả năng triển khai PhoneStore bằng GitHub Actions

Ngày 19/11/2026. **CI hiện hữu đã chạy thành công trên GitHub. CD vào máy Windows hiện tại: NO-GO.** Bước đầu tiên đáng làm là xuất hai image lên GHCR từ commit đã qua CI. Việc cấp quyền triển khai lên Docker Desktop cần quyết định riêng vì repository công khai và máy chạy cả SQL nghiệp vụ.

## A. Baseline

- Workspace ở `main`, HEAD `e22fc345f58272376eadd0b7082b9ab2dfaaed63`, ahead 2 theo tracking ref. Origin fetch/push là `https://github.com/vuvantai14/PhoneStore.git`. GitHub API công khai trả repository public, default branch main, remote HEAD `1a9639079b0911ea758a6aa6ed9329699b5938de`, trường `protected=false`.
- Trước audit: `docs/PROGRESS.md` đã modified, hai báo cáo S34.9D-01 và S34.9E untracked. Chưa có kết quả CI remote cho HEAD local trong bằng chứng thu được.
- SDK thực tế 10.0.403; global.json chọn 10.0.403 và latestPatch. Solution có tám project: Web MVC, Api, Business, Data EF Core/SQL Server, Models, Admin, AdminBootstrap và PhoneStore.Tests. CatalogImport, AcceptanceEnvironment, SqlIntegration là ba project bổ sung ngoài solution. Admin và SqlIntegration target net10.0-windows; chín project còn lại net10.0.

## B. Workflow và quyền hiện có

Chỉ thấy `.github/workflows/ci.yml`. Trigger gồm push main, PR nhắm main và workflow_dispatch. Hai job độc lập. Windows checkout, setup SDK theo global.json, cache NuGet, restore/build Release và test; Ubuntu dùng Buildx build hai image với push=false rồi validate **base Compose** bằng credential tạm sinh trong job. Lần chạy CI mới nhất GitHub trả về là push thành công trên remote HEAD. [Run 44465033640](https://github.com/vuvantai14/PhoneStore/actions/runs/44465033640)

Workflow chưa khai báo permissions tường minh, nên quyền GITHUB_TOKEN thực tế phụ thuộc settings của GitHub và loại event. Chưa xác minh settings quản trị, quyền Packages, runner group hoặc environment reviewer. Chưa có publish GHCR, lưu WinForms artifact, job deploy hay health gate sau deploy.

## C. Docker readiness

| Thành phần | Cấu hình và trạng thái đã đối chiếu |
|---|---|
| Engine | Docker 29.9.2 Linux, kernel microsoft-standard-WSL2, distro docker-desktop Running version 2; Compose v5.6.1. Kết quả thuộc tài khoản tương tác hiện tại. |
| Images | Web dùng `Dockerfile`, API dùng `src/PhoneStore.Api/Dockerfile`; context đều là root. Multi-stage SDK 10.0.403-noble → aspnet:10.0-noble, publish Release, port 8080. CI đặt tag cục bộ phonestore-web:ci / phonestore-api:ci. Base runtime tag có thể đổi theo servicing. |
| Business Compose | Project thực tế phonestore-restore-c2. File theo thứ tự: docker-compose.yml, docker-compose.restore.yml, docker-compose.net10.yml. Lệnh config --quiet với đúng project và cả ba file trả 0. Base có build; override net10 chọn **local image ID** API 91b13b7108a3, Web bbcd7442e1e5. Những ID này không phải pull reference của GHCR. |
| Network, port, health | Bridge phonestore-restore-c2_phonestore-net. Loopback Web 5015→8080, API 5025→8080, SQL 14334→1433. Web gọi api:8080, Web/API gọi db:1433. SQL health SELECT 1; API Compose kiểm /api/health; Web kiểm /. Tại audit cả ba healthy; GET / ở 5015 và /api/health/ready ở 5025 trả 200. |
| Dữ liệu | Volume thực tế phonestore-restore-c2_mssql_data gắn RW vào /var/opt/mssql. Web có bind /app/wwwroot/uploads/products read-only; dữ liệu ảnh được loại khỏi build context. Giữ nguyên đường dẫn uploads mà Admin Windows đang dùng. |
| Drift SQL | DB báo tag 2022-latest nhưng RepoDigest khớp digest 11927f5e… trong restore override. Runtime chỉ có mount /var/opt/mssql, HostConfig.Memory=0 và StopTimeout chưa đặt. Restore override có thêm backup mount RO, mem_limit 2304m, stop_grace_period 60s. Compose hợp lệ về cú pháp vẫn có thể recreate SQL khi áp toàn stack. |
| Acceptance | scripts/acceptance/docker-compose.yml pin riêng image API và Web CT02, 5286/5285, target DB acceptance. Hai container Exited 255. Tách project này khỏi CD business. |

Các biến host chính: COMPOSE_PROJECT_NAME, SQL_PORT, WEB_PORT, API_PORT, DB_NAME, PRODUCT_UPLOADS_PATH, MSSQL_SA_PASSWORD, ASPNETCORE_ENVIRONMENT, CATALOG_API_ENABLED và CATALOG_API_TIMEOUT_SECONDS. Base có fallback tên project khác business và environment Development. CD phải chỉ rõ project, bộ file và các giá trị dự kiến. Giữ .env ở host, không kết xuất compose config đã nội suy vào logs.

## D. CI và registry đề xuất

1. **PR:** giữ build solution + test Windows và build hai image Linux trên GitHub-hosted runner. Chỉ cấp contents:read. Validate cả ba file Compose bằng giá trị giả và config --quiet. Kiểm tra service, port và mount mục tiêu trên model đã lọc secrets. Không cấp publish/deploy cho PR.
2. **Push main đã được duyệt:** job publish phụ thuộc kết quả build/test và kiểm tra Docker thành công cho cùng SHA. Buildx dùng cùng context/Dockerfiles cho linux/amd64. Login ghcr.io bằng GITHUB_TOKEN với contents:read và packages:write riêng cho job này. Push `ghcr.io/vuvantai14/phonestore-web:sha-<commit>` và `ghcr.io/vuvantai14/phonestore-api:sha-<commit>`. Ghi **digest registry** của cả hai vào manifest cùng commit/run ID; chỉ phát hành manifest khi cả hai push thành công. CD pull theo digest. [GitHub – Publishing Docker images](https://docs.github.com/en/actions/tutorials/publish-packages/publish-docker-images)
3. **WinForms:** job Windows tạo artifact Admin Release riêng nếu duyệt phân phối. Giữ job Ubuntu giới hạn Web/API. Có thể giữ CI độc lập với việc máy Windows demo đang bật hay tắt.

## E. CD Windows và rollback đề xuất

**Biên tin cậy:** giữ CI trên GitHub-hosted. Với public repo, ưu tiên workflow CD trong repo triển khai private hạn chế người sửa; runner Windows chỉ đăng ký cho repo/group CD tin cậy. Dùng workflow_dispatch với manifest của run publish đã xác minh cho pilot; sau khi bảo vệ nhánh, nghiệm thu rollback và duyệt liên kết sự kiện mới cân nhắc kích CD tự động khi publish thành công. Không cấp runner có Docker pipe/quyền tới dữ liệu cho mã PR. Nhãn runner chỉ giúp chọn máy. [GitHub – Secure use](https://docs.github.com/en/actions/reference/security/secure-use)

**Chuẩn bị host.** Runner cần kết nối outbound tới GitHub/GHCR, quyền đọc manifest/uploads, quyền cập nhật file release, quyền điều khiển Docker context đúng và pull package (packages:read nếu private). Chưa xác minh runner đã đăng ký/online; kiểm tra local không tìm thấy service tên actions.runner*. Chế độ service và tài khoản đăng nhập tương tác có thể thấy context/named pipe khác nhau. Phải thử bằng chính tài khoản runner sau khi Windows reboot, kiểm tra Docker Desktop/WSL2 sẵn sàng, RAM/disk đủ cho image mới và cũ. Không lấy phiên người dùng hiện tại làm bằng chứng runner sẽ deploy được. [Docker Desktop WSL2](https://docs.docker.com/desktop/features/wsl/)

**Trình tự đề xuất, chưa chạy:**

1. Serialize deployment bằng concurrency group theo host/project, không hủy giữa chừng. Kiểm tra SHA/run/digest được duyệt và allowlist đúng hai GHCR package. Dùng thư mục release ổn định ngoài runner _work, ví dụ C:\PhoneStoreDeploy\business. Tách .env, uploads và trạng thái release khỏi checkout tạm.
2. So đúng project hiện có, network, volume, bind source, DB target, port, SQL healthy, container IDs/StartedAt, file backup và dung lượng. Ghi cặp digest/config của release đang hoạt động. Với release đầu tiên phải giữ được hai local image ID hiện tại để rollback offline. Có sai lệch ngoài Web/API hoặc thiếu rollback image thì dừng.
3. Tạo override release đã duyệt chỉ thay image cho api/web bằng `ghcr.io/...@sha256:...`. Dùng project cố định và các file Compose ở mục C, thêm override release **cuối cùng**. Chạy config --quiet và kiểm tra model chọn đúng digests. Pull cả hai image trước khi thay container. Dùng `up -d --no-deps --no-build --pull never api` cho API, chờ healthy + GET readiness; sau đó dùng cùng cờ cho web. Hai lệnh đều cần cùng project/file flags. Không đưa db vào tập service triển khai.
4. Trong timeout hữu hạn, kiểm Docker health, HTTP API /api/health/live và /api/health/ready, Web / và /products, một ảnh sản phẩm hiện có, và đọc Catalog v1. Đối chiếu ID/StartedAt SQL cùng volume và uploads. Ghi kết quả và hai digest thực chạy. Chỉ đánh dấu release thành công sau smoke read-only.
5. Khi lỗi, dừng rollout; dùng cặp image/config trước và khôi phục riêng service đã bị thay đổi theo đúng dependency API rồi Web. Kiểm lại health/readiness, Catalog, ảnh và ID SQL. Rollback image không xử lý được schema thiếu tương thích; mọi migration hoặc restore DB là quy trình riêng cần phê duyệt.

## F. Build/test .NET và WinForms artifact

Các lệnh thực có trong CI:

```powershell
dotnet restore PhoneStore.sln
dotnet build PhoneStore.sln --no-restore --configuration Release
dotnet build tools/PhoneStore.CatalogImport/PhoneStore.CatalogImport.csproj --configuration Release
dotnet build tools/PhoneStore.AcceptanceEnvironment/PhoneStore.AcceptanceEnvironment.csproj --configuration Release
dotnet build tests/PhoneStore.SqlIntegration/PhoneStore.SqlIntegration.csproj --configuration Release
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-build --configuration Release --verbosity normal
```

Chọn Windows hosted runner cho solution và Windows Desktop targeting packs. Cross-compilation từ Linux có thể cần EnableWindowsTargeting và không cung cấp WinForms runtime/GUI; không cần thêm con đường build đó. SqlIntegration chỉ **build** trong CI. Artifact Admin đề xuất dùng dotnet publish trên Windows, framework-dependent với .NET 10 Desktop Runtime ở máy nhận; sau khi duyệt kiến trúc máy mới chọn RID cụ thể. Đóng gói output publish, ghi SHA/build, retention và kiểm nội dung cấu hình; không kèm .env, backup hoặc dữ liệu. Nghiệm thu GUI riêng trên Windows.

## G–H. An toàn dữ liệu và quyền

- S34.2R-04 ghi full COPY_ONLY 827.392 bytes, VERIFYONLY WITH CHECKSUM thành công, lưu cùng volume SQL. Đây là bằng chứng lịch sử. Chưa xác minh backup mới nhất, bản độc lập ngoài host hoặc restore drill. Duyệt RPO, bản sao ngoài volume, bảo vệ backup và khả năng phục hồi trước business CD. Kiểm riêng backup uploads và đường dẫn host; SQL backup không chứa file ảnh.
- Kiểm tra entrypoint Web/API không thấy tự gọi Migrate/EnsureCreated. CD không chạy AdminBootstrap, CatalogImport, SqlIntegration, migration hoặc seed. Không dùng down -v, prune hoặc tự recreate db. Không thay đổi SQL Windows, SQL Docker Developer hoặc stack acceptance.
- .env được Git ignore, .dockerignore loại env/runtime uploads. Web/API hiện nhận credential SA và TrustServerCertificate=True từ Compose. Docker pipe có thể đọc env và mount: tài khoản runner có quyền thực tế rất cao. Duyệt login ứng dụng ít quyền, TLS phù hợp, ACL/thư mục host và environment release trước CD; không truyền SQL password vào CI. Kiểm tra hành vi cookie/Data Protection và session khi Web recreate, đặc biệt giỏ hàng đang mở.
- Cần duyệt branch protection/ruleset, PR review cho workflow và deploy manifests, actions pin bằng commit SHA, quyền Packages giới hạn từng job, environment reviewer nếu plan hỗ trợ và audit deployment. Không chạy mã từ fork trên runner CD, không dùng input tùy ý làm image/command. Visibility GHCR và khả năng pull từ host chưa xác minh. Loopback endpoints phù hợp demo tại host; công khai ra Internet cần quyết định HTTPS/network riêng.

## I–J. Lộ trình và quyết định cần duyệt

| Giai đoạn | Bằng chứng hoàn thành | Quyết định |
|---|---|---|
| S35.2 CI | PR build/test + hai Dockerfile + merged Compose pass, không quyền ghi; Admin artifact khởi chạy trên Windows | Cho sửa workflow, thống nhất runner và cách phát hành artifact |
| S35.3 GHCR | Hai image cùng SHA, hai digest pull được, manifest đầy đủ; PR không publish | Cho publish, chọn visibility, owner và retention |
| S35.4 CD pilot | Runner tin cậy chạy đúng context sau reboot, có TLS/secrets/backup phù hợp, dry-run chứng minh chỉ thay api/web; diễn tập rollback trên target được duyệt | Chọn repo/group CD, tài khoản, thư mục deploy, environment gate, lịch chạy, target pilot và cấp quyền cài runner |
| S35.5 Business | Release và rollback có bằng chứng HTTP, SQL/volume/uploads nguyên vẹn; có người vận hành khi host offline | Duyệt riêng lần deploy đầu và điều kiện tự động hóa các lần sau |

**Kết luận:** CI Build/Test và Docker build có bằng chứng trên remote main. Publish registry, CD self-hosted, deployment health gate và rollback qua Actions chưa nghiệm thu. S35.1 chỉ đọc cấu hình/metadata, GET health và viết tài liệu. Không chạy build/test lại, đăng ký runner, publish, deploy hay ghi SQL.
