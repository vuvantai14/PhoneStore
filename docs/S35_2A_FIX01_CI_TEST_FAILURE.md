# S35.2A-FIX01 — Ổn định kiểm thử từ chối cấu hình OIDC khi startup

## Bằng chứng và nguyên nhân

Baseline main tại `86315dc94e74defcec77f8c599e58791d1e3fcd8`, working tree ban đầu sạch. Workflow thực hiện Release build rồi chạy PhoneStore.Tests với --no-build trên windows-latest. Trong run push 44552851822, Windows build thành công, tests đạt 297/298; ca lỗi có issuer HTTPS và thiếu audience. Artifact upload bị bỏ qua. [Windows job](https://github.com/vuvantai14/PhoneStore/actions/runs/44552851822/job/135660903072)

Test cũ tạo WebApplicationFactory, thêm cấu hình in-memory bằng WithWebHostBuilder rồi gọi CreateClient để khởi động API qua entry point. Cặp issuer/audience thiếu một vế bị IsValid từ chối, kể cả khi bearer tắt. ValidateOnStart yêu cầu kiểm tra trong lúc host khởi động. Log ghi “Hosting failed to start” cùng OptionsValidationException. Stack của **exception mà assertion nhận được** đi qua CreateClient → CreateHost → DeferredHost.StartAsync → GetRequiredService → ObjectDisposedException (IServiceProvider). Đây là lần truy cập provider sau khi host đã được giải phóng trong đường khởi động hoãn của fixture. Stack không thể hiện OptionsValidationException được bọc bên trong ObjectDisposedException; xUnit báo sai loại exception.

Trước sửa, ba lượt chạy riêng theory cũ trên Windows local đều đạt 6/6. Mỗi ca có factory và cấu hình riêng. Log full suite của runner có các test class khác chạy xen kẽ. Điều này có thể tác động lịch thực thi, nhưng không chứng minh xung đột dữ liệu giữa tests. SDK hai bên cùng 10.0.403. Local Windows x64 build 26200 chạy .NET 10.0.14; log xUnit trên Windows Server 2025 ghi .NET 10.0.15. Không đủ bằng chứng quy nguyên nhân cho patch runtime.

## Phạm vi bản sửa

- `tests/PhoneStore.Tests/ApiAuthorizationTests.cs`: từng case tạo generic host độc lập, gọi đúng AddPhoneStoreApiAccess của production với cấu hình in-memory, build host, rồi await StartAsync. Test yêu cầu chính xác OptionsValidationException với OptionsType bằng ApiOidcOptions. Bỏ ValidateOnStart hoặc cho phép cặp issuer/audience sai sẽ làm test này thất bại. Host chỉ dispose sau assertion.
- `tests/PhoneStore.Tests/Auth0BearerTests.cs`: chuyển ca bật bearer nhưng thiếu issuer sang cùng theory vì có cùng dependency vào startup thất bại. Sáu case ban đầu và case này đều được thực thi. Các ca HTTP kiểm middleware, policy và token vẫn dùng WebApplicationFactory.
- `src/PhoneStore.Api/PhoneStore.Api.csproj`: thêm friend assembly PhoneStore.Tests để test dùng trực tiếp registration và kiểu options nội bộ; không sao chép validator hoặc mở public API.
- Tài liệu: báo cáo này và `docs/PROGRESS.md`. Không thay đổi workflow, cấu hình issuer, chính sách phân quyền hay mã xử lý request.

## Gate nghiệm thu

Kết quả trên Windows local (Release, SDK 10.0.403):

| Lệnh / phạm vi | Kết quả |
|---|---|
| dotnet build PhoneStore.sln --no-restore -c Release | PASS, 0 warning, 0 error |
| dotnet test tests/PhoneStore.Tests -c Release --no-build --filter FullyQualifiedName~Invalid_oidc_configuration_rejects_startup | 7/7 PASS sau sửa, lặp lại 7/7 sau final build |
| Cùng lệnh test, filter `FullyQualifiedName~ApiAuthorizationTests\|FullyQualifiedName~Auth0BearerTests` | 27/27 PASS |
| dotnet test tests/PhoneStore.Tests -c Release --no-build | 298/298 PASS, 0 fail/skip |
| git diff --check | PASS |

Kết quả local chỉ áp dụng cho runtime đã cài. Cần một run GitHub mới trên đúng SHA chứa FIX01 sau khi được duyệt commit/push. Điều kiện đóng gate là job Windows xanh, full suite đạt, job Ubuntu xanh và artifact đúng SHA tải/mở được. Chạy lại SHA cũ không nghiệm thu bản sửa.
