# S35.2A — CI và artifact Admin WinForms

Ngày 19/11/2026. **PASS local; workflow mới chưa chạy trên GitHub.**

## A–C. Baseline, file và permissions

Branch main, HEAD e22fc345f58272376eadd0b7082b9ab2dfaaed63. git ls-remote trả remote main 1a9639079b0911ea758a6aa6ed9329699b5938de, khớp tracking ref; local ahead 2, behind 0. Trước khi sửa, PROGRESS.md đã modified, các báo cáo S34.9D-01, S34.9E và S35.1 đã untracked.

Thay đổi của S35.2A gồm `.github/workflows/ci.yml`, `scripts/ci/Publish-AdminArtifact.ps1`, `scripts/ci/validate_business_compose.py`, báo cáo này và `docs/PROGRESS.md`. Workflow khai báo `contents: read` cho GITHUB_TOKEN, không cấp quyền Packages hoặc deployment. Cả hai checkout dùng persist-credentials=false. Giữ ba trigger push main, PR vào main và workflow_dispatch.

## D. Artifact Windows

Sau khi test Windows thành công, script publish Admin Release net10.0-windows trên host Windows x64, framework-dependent, dùng build đã kiểm thử. Artifact chứa apphost, DLL, manifests, font và runtime dependencies. Script yêu cầu destination mới, thay riêng cấu hình trong output bằng mặc định Windows Integrated Security và đường dẫn ảnh tương đối `uploads/products`, kiểm tra các file bắt buộc và từ chối loại file ngoài danh sách cho phép trước upload. Không lấy .env, uploads thực, backup hoặc credential Docker. Source appsettings của Admin giữ nguyên.

Tên artifact: `PhoneStore.Admin-net10-win-x64-<commit>-run<run_id>.<attempt>`. Khi job Windows chạy thành công trên GitHub, mở Actions → chọn run → Artifacts. Retention yêu cầu 14 ngày. Giải nén toàn bộ thư mục trên Windows x64 có .NET 10 Desktop Runtime. Mặc định kết nối localhost/PhoneStoreDB qua tài khoản Windows. Nếu dùng SQL Docker và thư mục ảnh hiện có, cấp `ConnectionStrings__PhoneStoreConnection` và `ProductImages__UploadsDirectory` cục bộ cho tiến trình Admin trước khi mở EXE. Không phân phối credential cùng artifact.

## E. Kiểm tra Compose business

Script chạy docker compose config --format json với project phonestore-restore-c2 và các file theo thứ tự base → restore → net10, dùng input tổng hợp, bỏ qua .env local, không log model. CI kiểm chính xác service db/api/web, volume RW phonestore-restore-c2_mssql_data tại /var/opt/mssql, SQL image digest đã duyệt, backup bind C:/PhoneStoreDockerTransfer/backup → /var/opt/mssql/backup RO, mem_limit 2304 MiB, SQL memory limit 1536 MB và stop_grace_period 60s. Kiểm tra các probe SQL SELECT 1, API GET /api/health, Web GET /. Đổi digest/chính sách có chủ đích cần cập nhật assertion cùng PR. Đây là kiểm tra **model merge**; khác biệt giữa SQL runtime và model đã ghi ở S35.1 vẫn là điều kiện cần xét trước CD.

## F. Bằng chứng local

| Kiểm tra | Kết quả |
|---|---|
| dotnet restore PhoneStore.sln; dotnet build PhoneStore.sln --no-restore -c Release | PASS, build 0 warning/error |
| Build Release bổ sung CatalogImport, AcceptanceEnvironment, SqlIntegration | 3/3 PASS; SqlIntegration chỉ build |
| dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-build -c Release | 298/298 PASS, 0 fail, 0 skip |
| Publish-AdminArtifact.ps1 | PASS trên Windows x64, publish vào thư mục tạm mới, xác nhận Integrated Security và không có user/password trong cấu hình. Không mở GUI |
| python scripts/ci/validate_business_compose.py | PASS static cho ba file merge |
| docker buildx build --output=type=cacheonly -f Dockerfile . và file src/PhoneStore.Api/Dockerfile | Cả hai Linux build PASS; không tag/push, không sử dụng container nghiệp vụ |
| Workflow YAML, ba trigger, permissions, git diff --check | PASS local. Dùng YAML parser của Compose cho kiểm tra cú pháp; cú pháp expression và artifact download sẽ do Actions xác minh ở run thật |

## G–H. Phần còn lại

Chờ một run GitHub của đúng revision để xác nhận upload/download và smoke WinForms trên máy nhận. S35.2B đề xuất duyệt quyền publish riêng cho trusted push, naming/visibility và kiểm chứng hai GHCR digest của cùng commit. CD cần duyệt runner, quyền truy cập, protection, rollback và ranh giới chỉ thay api/web. Phiên này không publish GHCR, deploy, commit/push, chạy migration hoặc ghi SQL nghiệp vụ.
