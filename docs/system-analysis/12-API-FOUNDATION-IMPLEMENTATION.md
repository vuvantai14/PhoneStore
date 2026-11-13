# S31.3B — PhoneStore REST API Foundation Implementation

Ngày thực hiện: 2026-11-13  
Kết luận: **PASS — READY FOR S31.3C**

## A. Preflight & Working Tree

- Branch: `main`.
- Trước thay đổi đã có diff người dùng: `docs/PROGRESS.md` modified; báo cáo S31.1/S31.2 untracked. Tất cả được bảo toàn.
- Solution ban đầu có 7 project; chưa có API, Swagger, CORS, health checks hoặc Problem Details configuration.
- Source khớp kết luận S31.3A: catalog service hiện hữu có thể tái sử dụng; không cần migration/schema change.

## B. Files Created/Modified

Tạo mới:

- `src/PhoneStore.Api/PhoneStore.Api.csproj`
- `src/PhoneStore.Api/Program.cs`
- `src/PhoneStore.Api/appsettings.json`
- `src/PhoneStore.Api/Properties/launchSettings.json`
- `src/PhoneStore.Api/Controllers/HealthController.cs`
- `src/PhoneStore.Api/Contracts/HealthResponse.cs`
- `src/PhoneStore.Api/Health/HealthCheckNames.cs`
- `src/PhoneStore.Api/Health/IPhoneStoreReadinessProbe.cs`
- `src/PhoneStore.Api/Health/PhoneStoreReadinessProbe.cs`
- `src/PhoneStore.Api/Health/PhoneStoreDatabaseHealthCheck.cs`
- `src/PhoneStore.Api/Infrastructure/ApiExceptionHandler.cs`
- `tests/PhoneStore.Tests/ApiFoundationTests.cs`
- tài liệu này.

Sửa:

- `PhoneStore.sln`: thêm `PhoneStore.Api` dưới solution folder `src`.
- `tests/PhoneStore.Tests/PhoneStore.Tests.csproj`: thêm reference tới API.
- `docs/PROGRESS.md`: cập nhật kết quả S31.3B.

Không sửa source Customer Web, Admin WinForms, Business, Data, Models, migration hoặc database.

## C. API Project & Dependency Graph

`PhoneStore.Api` là ASP.NET Core Web API .NET 8 độc lập:

```text
PhoneStore.Api
  -> PhoneStore.Business
  -> PhoneStore.Data
  -> PhoneStore.Models
```

Không có dependency ngược/circular, Docker, Redis, message queue, JWT hoặc write endpoint. S31.3B chỉ xuất bản `GET /api/health`.

Swagger dùng `Swashbuckle.AspNetCore` version cố định `6.6.2`.

## D. DI & Configuration

- `AddControllers()` và controller routing.
- `AddProblemDetails()` với `traceId` an toàn.
- `AddExceptionHandler<ApiExceptionHandler>()`.
- `PhoneStoreDbContext`: scoped, SQL Server provider.
- `ProductCatalogReader`: scoped.
- `IProductService -> ProductService`: scoped.
- `IPhoneStoreReadinessProbe -> PhoneStoreReadinessProbe`: scoped.
- Standard `AddHealthChecks()` với database health check.
- Endpoint API mặc định chạy `http://localhost:5025`, không xung đột Web `5015`.
- Dùng key `ConnectionStrings:PhoneStoreConnection`; ASP.NET Core configuration vẫn hỗ trợ environment variable override.
- Local config dùng Windows Authentication, không có password; không sửa Web/Admin config.
- Không bật CORS và không gọi `UseHttpsRedirection`.
- Development dùng Console/Debug logging để tránh Windows Event Log permission làm hỏng request/test.

Tests xác minh DbContext, Reader và ProductService cùng instance trong một scope nhưng khác instance giữa hai scope; không có singleton lifetime sai.

## E. Health Endpoint

`GET /api/health` gọi standard `HealthCheckService`. Real probe dùng:

```text
PhoneStoreDbContext.Database.CanConnectAsync(cancellationToken)
```

- Healthy: HTTP 200, `{"status":"healthy"}`.
- Unavailable: HTTP 503, `{"status":"unhealthy"}`.
- Response không có server/database/connection/exception detail.
- CancellationToken được truyền xuyên controller -> health service -> EF connection probe.
- Runtime live ngoài sandbox: HTTP 200; EF log chứng minh query duy nhất là `SELECT 1`.
- Test unavailable dùng test double, không làm hỏng SQL Server thật.

## F. Exception Handling & Swagger

### Exception handling

- Central `ApiExceptionHandler` trả HTTP 500 theo Problem Details.
- Client chỉ nhận title/detail chung, request path và trace ID.
- Không trả stack trace, SQL error hoặc exception message.
- Server log chỉ ghi exception type + trace ID, không ghi message/connection string/PII.
- Health 503 được controller trả trực tiếp và không bị đổi thành 500.

### Swagger/OpenAPI

- Development: Swagger JSON và Swagger UI được map.
- Production: không map Swagger middleware; automated test xác minh cả JSON/UI trả 404.
- OpenAPI runtime hợp lệ `3.0.1`, có đúng một path `/api/health`.
- Chưa có Products/Brands/Categories endpoints, đúng phạm vi S31.3B.

## G. Build & Test Results

| Kiểm tra | Kết quả |
|---|---|
| `dotnet restore PhoneStore.sln` | PASS; API/test restored |
| `dotnet build PhoneStore.sln --no-restore` | PASS, 0 warning, 0 error |
| `ApiFoundationTests` | 6/6 PASS |
| `HomePresentationTests` regression | 2/2 PASS |

Sáu API tests bao phủ:

1. Startup + healthy 200.
2. Unavailable database test double -> 503, không lộ hạ tầng.
3. Swagger JSON/UI Development + health path.
4. Swagger không public trong Production.
5. DI scoped lifetimes.
6. Exception handler trả Problem Details 500 an toàn.

Lần build đầu phát hiện test thiếu namespace `Environments`; đã sửa trong test. Lần test đầu cũng phát hiện Windows Event Log provider không dùng được trong sandbox; API Development logging được cấu hình giống Web bằng Console/Debug. Các lần build/test cuối đều PASS.

## H. Runtime Verification

Runtime cuối dùng đúng binary vừa build:

| Hạng mục | Kết quả |
|---|---|
| API listen `localhost:5025` | PASS |
| `GET /api/health` | 200, healthy |
| Swagger JSON | 200 |
| Swagger UI | 200 |
| OpenAPI version/path | 3.0.1; `/api/health` |
| Web `localhost:5015` chạy đồng thời | 200 |
| API health khi Web chạy đồng thời | 200 |
| Port conflict Web/API | Không |

Sandbox runtime trả 503 fail-closed do security context không kết nối SQL, đúng chẩn đoán S30.4A.2. Cùng API chạy ngoài sandbox trả 200 và `SELECT 1`, do đó **SQL live connection VERIFIED**.

Tất cả process API/Web do task khởi tạo đã được dừng; cổng 5015/5025 không còn listener của task.

## I. Database Safety Verification

- Không migration, seed, DML hoặc DDL.
- API chỉ có health GET; runtime EF command là `SELECT 1`.
- Web concurrent smoke chỉ GET Home và log các SELECT catalog.
- Tests dùng readiness test double; không mở `PhoneStoreDB`.
- Read-only hậu kiểm xác nhận database đích là `PhoneStoreDB` và snapshot hiện tại: Roles 2, Users 3, Brands 8, Categories 2, Products 40, ProductImages 41, Orders 11, OrderDetails 26.

Snapshot hiện tại khác số liệu lịch sử S31.1 ở Categories/Images/Orders/Details. Audit này không có pre-runtime live count để xác định thời điểm các thay đổi lịch sử đó; không gán chúng cho S31.3B. Không có code path/log bằng chứng DML trong build/test/runtime của task này.

## J. Remaining Risks

- S31.3C phải thêm validation/upper bound `pageSize`; Data hiện chưa giới hạn phía trên.
- Cần chốt contract relative image URL trước catalog endpoint.
- Swagger chỉ được kiểm bằng automated + local Development smoke, chưa có deployment production.
- Health hiện là combined process/database readiness; chưa tách liveness/readiness.
- Không có CORS/auth/rate limiting vì V1 foundation chưa cần; phải đánh giá lại trước khi public API.
- API dùng cùng database/account configuration với Web/Admin; least-privilege read-only SQL principal vẫn là hardening tương lai.

## K. GO / NO-GO for S31.3C

**GO.** API đã chạy thật, SQL Server live đã kết nối read-only, Swagger/OpenAPI hoạt động, exception/health/DI có automated evidence và Web/Admin source không bị thay đổi.

S31.3C nên chỉ triển khai catalog read-only DTO/endpoints, pagination/query validation và contract tests. Không tự thêm write API, authentication hoặc Customer Web integration.
