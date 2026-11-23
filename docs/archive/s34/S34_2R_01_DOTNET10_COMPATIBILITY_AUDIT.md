# S34.2R-01 — Audit tương thích .NET 10 LTS

Ngày 19/11/2026. **GO cho nâng cấp cách ly sau phê duyệt. NO-GO cho triển khai runtime hiện tại.** Audit xác nhận điều kiện nền, chưa restore package mới, retarget, build hoặc chạy ứng dụng .NET 10.

## A. Current Framework Inventory

`main` ban đầu sạch tại `4d77bd0a3ab619fae70d17a2f750d74233082e9e`. S34.2 đã commit, và `git ls-remote --exit-code origin refs/heads/main` xác nhận GitHub main trỏ đúng SHA. Kết quả **263/263** là baseline S34.2 trên .NET 8.

Máy Windows 11 x64 build `26200` (25H2) có SDK **8.0.427** và **10.0.403**. `global.json` chọn `8.0.427`, `rollForward: latestPatch`. Host có NETCore, ASP.NET Core và Windows Desktop runtime `8.0.33` và `10.0.14`, cùng Windows Desktop reference pack `10.0.14`. Việc có SDK 10 không tự đổi SDK repo đang chọn.

| Vị trí | Project | TFM hiện tại → dự kiến |
| --- | --- | --- |
| Solution | Api, Business, Data, Models, Web | `net8.0` → `net10.0` |
| Solution | Admin | `net8.0-windows` → `net10.0-windows` |
| Solution | PhoneStore.Tests, AdminBootstrap | `net8.0` → `net10.0` |
| Ngoài solution | CatalogImport, AcceptanceEnvironment | `net8.0` → `net10.0` |
| Ngoài solution | PhoneStore.SqlIntegration | `net8.0-windows` → `net10.0-windows` |

Tổng **11 project**. App build riêng dùng project graph hiện có. Các app và tool `net8.0` không thể tham chiếu thư viện chia sẻ đã đổi sang `net10.0`, nên đổi TFM đồng bộ trong nhánh nâng cấp. [global.json](../../../global.json) · [Solution](../../../PhoneStore.sln) · [Windows SDK và runtime](https://learn.microsoft.com/en-us/dotnet/core/install/windows) · [Chính sách hỗ trợ .NET](https://dotnet.microsoft.com/en-us/platform/support/policy)

## B. SDK and Package Compatibility

| Nhóm | Phiên bản hiện tại | Ưu tiên khi nâng |
| --- | --- | --- |
| SDK và Desktop | Pin SDK `8.0.427`, SDK `10.0.403` đã có | Chọn và pin patch SDK 10 còn hỗ trợ. Đồng bộ Windows, CI và SDK Docker. Xác minh IDE đích, ref pack và Desktop Runtime trên máy chạy Admin. |
| EF Core và tools | Core/SqlServer/Design trong Data, Sqlite trong Tests, `dotnet-ef` **8.0.28**. Web Design **8.0.11** | Đồng bộ EF Core/SqlServer/Sqlite/Design và tool manifest lên cùng một patch 10.0.x. Kiểm tra design-time context và pending model changes trên checkout cách ly. |
| SqlClient và SQLite native | Assets hiện resolve SqlClient **5.1.7**, SNI **5.1.2**, Sqlite.Core **8.0.28**, SQLitePCLRaw.bundle_e_sqlite3 **2.1.6** | Lấy graph transitive từ EF 10 đã chọn. Kiểm tra SqlClient broker/native deps, TLS/Windows authentication, SQL login Docker và SQLite fixture/native load. Không pin driver riêng nếu chưa có lý do. |
| ASP.NET Core testing và Identity | Mvc.Testing và Identity.Core **8.0.28** | Chuyển sang patch 10.0.x phù hợp để TestServer và PasswordHasher được kiểm tra cùng host mới. Giữ password hash cũ tương thích. |
| Microsoft.Extensions trên Admin | Configuration.Json/EnvironmentVariables **8.0.0**, DI **8.0.1** | Đồng bộ các references Microsoft.Extensions trực tiếp với 10.0.x. Kiểm tra thứ tự ưu tiên appsettings và biến môi trường trong launcher. |
| Swagger | Swashbuckle.AspNetCore **6.6.2**, Microsoft.OpenApi transitive **1.6.14** | Kiểm tra build/smoke với graph hiện tại trước nếu restore/audit cho phép. Tách bước nâng Swashbuckle khi cần. Khi lên v10, nhà duy trì khuyến nghị đi qua 9.0.6 và OpenAPI.NET v2 có thay đổi namespace/API. Giữ spec/UI path và Development-only. |
| Test runner | Test SDK **17.12.0**, xUnit **2.9.2**, VS adapter **2.8.2** | Thử discovery và full suite với runner hiện có. Nâng Test SDK/adapter tương thích khi có lỗi hoặc cần servicing, giữ xUnit v2 trong phạm vi migration. |

Phiên bản ở cột hiện tại lấy từ csproj, tool manifest và assets đã restore. Phiên bản đích chính xác chờ resolve, audit transitive và test. [EF providers](https://learn.microsoft.com/en-us/ef/core/providers/) · [EF 10 breaking changes](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/breaking-changes) · [Swashbuckle migration guide](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/migrating-to-v10.md) · [Test SDK](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk)

## C. Breaking Changes

| Thành phần | Tác động dự kiến và kiểm tra bắt buộc |
| --- | --- |
| MVC/cookie | .NET 10 mặc định trả 401/403 cho endpoint được nhận diện là API khi dùng cookie. API v1 PhoneStore hiện không cấu hình cookie, còn Web dùng MVC với policy Customer. Kiểm tra thực tế 302 Login/AccessDenied với Profile, Orders và Checkout, hành vi XHR, CSRF, login/logout và revoke. Chỉ cân nhắc override từng endpoint nếu test xác nhận regression contract. |
| Session | Cart và checkout token nằm trong session memory. Xác minh ownership CT02, guest-cart adoption, logout, revoke và token replay sau login. Process thay thế sẽ mất session in-memory. Kiểm tra khả năng đọc cookie giữa phiên bản/image bằng key ring hiện có và xác định cửa sổ re-login; tránh chạy trộn phiên bản khi chưa xác minh trạng thái phiên. |
| REST API/JSON/error | ASP.NET Core 10 thay đổi diagnostics mặc định khi `IExceptionHandler` đã xử lý lỗi. Giữ handler/log có trace ID và kiểm tra lỗi không bị mất log hoặc lộ bí mật. So sánh JSON catalog v1, validation errors, 404/429/503/504, OpenAPI và ba health routes S34.2. Kiểm tra cảnh báo obsolete của test host nếu phát sinh. |
| EF Core/SQL | EF9 coi pending model changes là lỗi khi apply migration. Tool acceptance gọi `MigrateAsync`, cần kiểm tra snapshot và tool riêng. EF10 đổi chiến lược dịch parameterized collections, có thể đổi execution plan của lọc catalog và truy vấn tồn kho trong `PurchaseStore`. Đo cả SQL semantics và plan. Kiểm tra báo cáo UTC+7, update tồn có điều kiện, transaction checkout, unique constraints và replay trên SQL thật cách ly. |
| EF provider/TLS | SQL Docker thực tế **SQL Server 2022 16.0.4315.2**, business và acceptance **ONLINE, compatibility 160**. Không có yêu cầu đổi schema hoặc server version được xác định ở audit. Chốt provider SQL dialect cho 160 nếu truy vấn cần, giữ mức compatibility của DB. Kiểm tra TLS/driver và Windows Authentication theo môi trường, giữ policy encrypt/certificate hiện có trong bước tương thích. |
| Test & serialization | SQLite và test double xác minh logic/contract, không chứng minh SQL Server translation. Giữ exact snapshot field/numeric/null, route và method. Không dựa trên test count đơn thuần nếu test discovery hoặc test skipped thay đổi. |

Nguồn: [ASP.NET Core 10 breaking changes](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/overview?view=aspnetcore-10.0) · [Cookie 401/403](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/cookie-authentication-api-endpoints?view=aspnetcore-10.0) · [EF 9](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/breaking-changes) · [EF 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/breaking-changes) · [SQL provider](https://learn.microsoft.com/en-us/ef/core/providers/sql-server/) · [S34.2 baseline](S34_2_REST_API_FOUNDATION.md).

## D. WinForms Compatibility

Admin và SQL harness đều dùng `UseWindowsForms` và tham chiếu `Microsoft.WindowsDesktop.App.WPF`. Admin thực sự dùng WPF `BitmapDecoder` để kiểm tra JPEG/PNG, cần giữ Desktop references. .NET 10 bổ sung các kiểu menu WinForms có thể trùng tên với WPF. Chưa thấy xung đột kiểu đó qua lượt tìm source, nhưng cần build cả Admin lẫn SQL harness. Kiểm tra output và fonts, launcher `-CheckOnly`, login/fresh authorization, CRUD chỉ trên fixture được duyệt, báo cáo/ảnh và scaling 100%/125%/150%. Smoke kết nối SQL Windows và Docker theo target cấu hình riêng. [WinForms breaking changes](https://learn.microsoft.com/en-us/dotnet/core/compatibility/10#windows-forms) · [SDK/MSBuild/Visual Studio](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs)

## E. Docker Compatibility

Hai Dockerfile đang dùng `sdk:8.0` và `aspnet:8.0`, publish framework-dependent và bind cổng 8080. Docker Engine **29.9.2**, Compose **v5.6.1**. Tại audit, 5/5 container healthy và 4/4 HTTP GET Web/health trả 200. Cả bốn container Web/API chạy ASP.NET Core và NETCore **8.0.34**.

| Stack | Web image | API image |
| --- | --- | --- |
| Business | `75bfbc07a0ab…` | `9b20d979aa6a…` |
| Acceptance | CT02 `aeddfa741bc0…` | `9b20d979aa6a…` |

SQL dùng image `11927f5edc51…`. S34.2 có trên Git, các container giữ image cũ. Tag .NET 10 mặc định chuyển từ Debian sang Ubuntu 24.04. Ở bước build cách ly, chọn tag distro rõ ràng, kiểm thử timezone/culture, CA, healthcheck Bash, 8080, quyền mount ảnh và SQL TLS. Đồng bộ Docker SDK với pin repo; Dockerfile hiện restore bằng SDK của build image vì không copy `global.json` trước restore. Pin image mới riêng, không ghi đè tag/pin CT02 đang dùng và không đổi SQL image hoặc volume. [Base image change](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/10.0/default-images-use-ubuntu) · [Web Dockerfile](../../../Dockerfile) · [API Dockerfile](../../../src/PhoneStore.Api/Dockerfile)

## F. CI/CD Compatibility

Workflow hiện dùng Windows để restore, build Release và chạy PhoneStore.Tests với setup-dotnet `8.0.x`. Ubuntu đã có job build Web/API image và validate Compose. Khi nâng cấp, cài SDK theo `global.json` và in `dotnet --version` trong job. Build riêng ba project ngoài solution. Giữ SQL integration ghi dữ liệu ngoài CI mặc định cho tới khi có disposable SQL target được duyệt. Xác minh Linux image, smoke endpoints, test discovery và artifacts. Workflow hiện không deploy production. [CI workflow](../../../.github/workflows/ci.yml) · [setup-dotnet](https://github.com/actions/setup-dotnet/blob/main/README.md)

## G. Recommended Upgrade Sequence

1. **Chuẩn bị.** Giữ checkpoint S34.2 và mốc 263 tests. Chọn patch .NET 10/EF 10 rồi đồng bộ SDK, tool manifest và package chính. Kiểm tra assets transitive. Dùng nhánh nâng cấp cách ly khi được phê duyệt.
2. **Retarget đồng bộ.** Đổi 11 TFM theo bảng. Cập nhật `scripts/acceptance/common.ps1` vì đang gọi DLL từ đường dẫn `bin/Debug/net8.0`. Giữ cơ chế chọn image CT02 cho runtime đang dùng.
3. **Build/test.** Build Release toàn solution và ba project ngoài solution. Chạy targeted catalog/health/cookie/CT02/ordering trước full suite. Kiểm tra model snapshot và script migration theo thiết kế, giữ schema hiện tại. Nếu có pending model changes hoặc DDL mới, dừng để phân tích.
4. **Smoke ba host.** Web MVC/catalog/ảnh, cookie và session. API catalog v1, ba health routes, Swagger Dev và errors. WinForms publish thư mục riêng, chạy qua launcher kiểm tra SQL target và UI trên Windows.
5. **Docker và CI.** Build Web/API riêng bằng .NET 10, đưa tag/digest riêng vào stack cách ly theo phê duyệt. Chạy job Windows và Linux cùng pin SDK. Kiểm tra mounts/ports, startup, log và health.
6. **SQL cách ly.** So sánh SQL generated và kết quả catalog, reporting, checkout/idempotency, tồn kho và quyền. Nếu cần fixture ghi, dùng target disposable đã duyệt. Tất cả kiểm thử regression không dùng business/acceptance hiện tại làm DB ghi.
7. **Triển khai theo phê duyệt riêng.** Chỉ đề xuất rollout khi hợp đồng v1 ổn định, SQL schema giữ nguyên, full suite và smoke đủ bằng chứng, image IDs và kế hoạch rollback được chốt.

## H. Risks and Rollback

- **Cao:** lệch dependency graph, EF snapshot drift, SQL translation/transaction, cookie/session qua restart, Docker base image/TLS. Kiểm tra các nhóm này trước rollout. Dừng nếu cần thay schema, thay đổi contract hoặc can thiệp business DB.
- **Vừa:** Swagger/OpenAPI namespace, runtime WinForms/WPF, font/DPI, launcher output và CI SDK pin. Tách package lớn thành checkpoint riêng khi có thể.
- **Rollback:** giữ checkpoint source, image IDs Web/API và bộ binaries WinForms đã xác minh. Nếu có regression sau deploy được duyệt, phục hồi cả cấu hình và artifact tương thích bằng phê duyệt riêng, kiểm tra lại health, target DB, API v1 và đăng nhập. Rollback artifact giữ schema/dữ liệu. Ảnh hưởng session phải được đánh giá riêng vì in-memory session không phục hồi bằng việc đổi image.

## I. Estimated Scope

Phạm vi nền khoảng **17 file source/build/config** gồm 11 csproj, global.json, tool manifest, 2 Dockerfile, workflow CI và helper launcher. Tài liệu và test tương thích bổ sung theo lỗi tìm thấy. Source nghiệp vụ chỉ cần sửa khi build/test chỉ ra một khác biệt có thể tái hiện. Chia thành ba checkpoint đề xuất gồm retarget và tests, Docker/CI cách ly, rồi rollout riêng.

## J. Go/No-Go Recommendation

**GO có điều kiện** cho spike nâng cấp .NET 10 trong môi trường cách ly sau khi người dùng phê duyệt. Máy và SQL Server có nền tảng phù hợp, chưa có blocker buộc thay schema từ audit. **NO-GO cho triển khai** trước khi xác minh restore, build, full test suite, Windows Desktop, Docker .NET 10 và SQL runtime cách ly. Phiên audit chỉ kiểm tra source, Git, metadata SQL và Docker/HTTP chỉ đọc. Không thay source/config, package, image, container hay database.
