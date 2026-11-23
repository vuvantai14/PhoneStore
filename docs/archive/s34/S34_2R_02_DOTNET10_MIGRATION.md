# S34.2R-02 — Nâng cấp PhoneStore lên .NET 10 LTS

Ngày 19/11/2026. **PASS cho build, automated tests và build image cách ly. Chờ nghiệm thu và phê duyệt riêng trước rollout.**

## A. Baseline

`main` ở checkpoint S34.2 `4d77bd0a3ab619fae70d17a2f750d74233082e9e`. Working tree ban đầu chỉ có báo cáo audit R-01 và ghi chú tương ứng trong PROGRESS. Hai thay đổi được giữ. SDK `10.0.403` có sẵn. Baseline .NET 8 là 263/263 tests. Docker business/acceptance có 5/5 container healthy, API health 200, và snapshot chỉ đọc cho hai DB khớp baseline.

## B. Framework Migration

Pin `global.json` ở SDK `10.0.403`, giữ `latestPatch`. Retarget 11 project sau, giữ project references:

- `net10.0` gồm Api, Business, Data, Models, Web, PhoneStore.Tests, AdminBootstrap, CatalogImport và AcceptanceEnvironment.
- `net10.0-windows` gồm Admin và PhoneStore.SqlIntegration, giữ WinForms và WPF reference.

Đổi TFM output trong `scripts/acceptance/common.ps1` sang `bin/Debug/net10.0`. Giữ nguyên cấu hình kết nối và pin image acceptance. Ngoài 11 csproj và global.json, các file sửa gồm `.config/dotnet-tools.json`, hai Dockerfile, `.dockerignore`, workflow CI, helper acceptance, `ReportingUi.cs`, ba HTTP test fixtures, báo cáo này và PROGRESS.

## C. NuGet / EF Core

- **10.0.15** cho EF Core/Design/SqlServer/Sqlite, Mvc.Testing, Extensions.Identity.Core, Configuration.Json, Configuration.EnvironmentVariables, DependencyInjection và `dotnet-ef`. EF Design ở Web được đồng bộ từ 8.0.11.
- Graph resolve Microsoft.Data.SqlClient **6.1.7**, SNI runtime **6.0.3**, Microsoft.Data.Sqlite.Core **10.0.15** và SQLitePCLRaw bundle **2.1.13**.
- Giữ Swashbuckle **6.6.2**, OpenAPI.NET transitive **1.6.14**, Microsoft.NET.Test.Sdk **17.12.0**, xUnit **2.9.2** và adapter **2.8.2** sau khi build và test xác nhận tương thích.
- `dotnet ef migrations has-pending-model-changes` với design-time target offline xác nhận **không có model changes**. Không tạo hoặc áp dụng migration.

## D. Web / API Compatibility

`CustomerProfileHttpTests.cs`, `HomePresentationTests.cs` và `CatalogPaginationHttpTests.cs` gỡ cấu hình provider SQL Server trước khi đăng ký SQLite. EF Core 10 giữ riêng registration này, nên chỉ gỡ DbContext/options như trước có thể tạo hai provider trong cùng test host. Production DI giữ nguyên.

Automated tests qua TestServer xác nhận startup Web/API, Catalog v1, ba health routes S34.2, Swagger Development, JSON/status, ProblemDetails, cookie redirect/antiforgery và CT02. Không đổi API contract hoặc auth/session implementation.

## E. WinForms Compatibility

.NET 10 báo WFO1000 cho ba thuộc tính card trong `ReportingUi.cs`. Khai báo `DesignerSerializationVisibility.Hidden` cho các thuộc tính được cấu hình bằng code, giữ nguyên giá trị và cách vẽ. Admin và SQL harness build thành công. Đã xác minh tiến trình WinForms .NET 10 riêng mở được LoginForm và đóng exit 0 với SQL offline. Chưa đăng nhập Admin hoặc chạy Gateway/Reports trên SQL thật với runtime mới. [Microsoft WFO1000](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/compiler-messages/wfo1000)

## F. Automated Test Results

| Kiểm tra | Kết quả |
| --- | --- |
| Restore solution, ba project ngoài solution, `dotnet tool restore` | PASS |
| Build Release solution và ba project ngoài solution, `--no-restore -m:1` | **11/11 PASS**, 0 warning, 0 error |
| Targeted API/Web/auth/session/reporting sau sửa fixture | **129/129 PASS**, 0 skipped |
| Full `PhoneStore.Tests` Release `--no-build --no-restore -m:1` | **263/263 PASS**, 0 skipped, chạy `net10.0` |
| EF model snapshot, PowerShell parse và `git diff --check` | PASS |

SQL integration harness build-only. Test có ghi dữ liệu dùng fixture SQLite riêng.

## G. Docker Build Results

Hai Dockerfile dùng SDK `10.0.403-noble`, runtime `aspnet:10.0-noble` và copy global.json trước restore. Build context loại scratch và .env lồng nhau. Compose business và launcher acceptance `Check` đều validate thành công. Hai image Linux/amd64 build dưới tag riêng:

| Tag | Local image ID |
| --- | --- |
| `phonestore-api:s34-2r02-net10` | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` |
| `phonestore-web:s34-2r02-net10` | `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` |

Chưa chạy container từ hai image mới. [Thay đổi base image .NET 10](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/10.0/default-images-use-ubuntu)

## H. CI/CD Changes

Workflow Windows lấy SDK từ global.json và build thêm ba project ngoài solution. Job Linux giữ bước build Docker image và validate Compose. Chưa chạy GitHub Actions cho thay đổi local.

## I. Runtime Integrity

5/5 business/acceptance container healthy và khớp **ID, image ID, StartedAt** trước/sau. Bốn HTTP endpoint Web/API trả 200. Hai database ONLINE, snapshot count/checksum **8/8 bảng mỗi database** khớp, SQL blocking 0 tại hai thời điểm đo. Không đổi Compose, deploy, restart, recreate, volume, SQL schema hay dữ liệu.

## J. Remaining Risks and Rollback

Hậu kiểm trước rollout gồm smoke hai image .NET 10 trên stack cách ly, SQL Server provider/TLS/WinForms reporting và nghiệp vụ ghi với fixture disposable đã được phê duyệt. Kiểm tra lại chuyển tiếp cookie/session vì session in-memory mất khi thay process. Nếu cần rollback trước deploy, dùng checkpoint S34.2 để hoàn tác có chủ đích phần retarget/package/build mà vẫn giữ tài liệu R-01. Nếu rollback sau deploy được phê duyệt, dùng image/binary và cấu hình đã xác minh trước rollout, đối chiếu lại catalog/health/SQL. Kế hoạch không cần rollback dữ liệu.

## K. Suggested Commit Message

`chore(runtime): migrate PhoneStore to .NET 10 LTS`

Chưa stage, commit, push hoặc deploy. Dừng tại S34.2R-02.
