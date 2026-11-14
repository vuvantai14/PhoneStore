# S31.5B — Docker Compose Hardening & API Integration

Ngày xác minh: 2026-11-14  
Phạm vi: implementation, static validation, targeted .NET build; **không deployment**  
Kết luận: **PASS — STATIC PASS / BUILD PASS / RUNTIME NOT TESTED**

## A. Git State

- Branch `main`, HEAD đầu/cuối task: `c6de0474731a1e2397ff77f3790743cbda99c33b`.
- Bảo toàn thay đổi tài liệu S31.5A chưa commit; không reset, clean, stash, stage, commit hoặc push.
- Không sửa database, application business source, Customer UI hoặc Admin.

## B. Files Modified

- `docker-compose.yml`
- `Dockerfile` (đã audit, không cần sửa)
- `src/PhoneStore.Api/Dockerfile` (mới)
- `.dockerignore`
- `.env.example`
- `.github/workflows/ci.yml`
- `docs/DOCKER_AND_CI_GUIDE.md`
- `docs/system-analysis/20-DOCKER-COMPOSE-HARDENING.md`
- `docs/PROGRESS.md`

Tài liệu S31.5A `docs/system-analysis/19-DOCKER-COMPOSE-PREFLIGHT.md` vẫn là file mới chưa commit từ task trước.

## C. Web Dockerfile

Root `Dockerfile` đã đáp ứng multi-stage SDK/ASP.NET Core 8, Release publish, port 8080 và project references nên không thay đổi. `.dockerignore` mới loại trừ rõ `wwwroot/uploads/products`; runtime images không còn được đưa vào build context/image một cách vô tình. `CatalogApi` được cấu hình qua Compose environment, không cần sửa Web source/appsettings local.

## D. API Dockerfile

`src/PhoneStore.Api/Dockerfile` mới:

- build context repository root để resolve Api → Business/Data/Models;
- restore theo project files trước, sau đó copy đúng bốn source projects;
- `dotnet publish -c Release`, `UseAppHost=false`;
- runtime `mcr.microsoft.com/dotnet/aspnet:8.0`, listen `0.0.0.0:8080` qua `ASPNETCORE_HTTP_PORTS`;
- không copy `.env`, uploads, tests, docs hoặc secrets.

Không thêm package và không đổi REST API logic.

## E. Compose Architecture

Compose có project name mặc định `phonestore-acceptance`, không dùng fixed `container_name`, network/volume được scope theo project:

| Service | Host mặc định | Nội bộ | Dependencies | Health |
|---|---:|---|---|---|
| `web` | `127.0.0.1:5015` | `web:8080` | DB + API healthy | HTTP `/` |
| `api` | `127.0.0.1:5025` | `api:8080` | DB healthy | HTTP `/api/health` |
| `db` | `127.0.0.1:14334` | `db:1433` | — | SQL `SELECT 1` |

Web dùng `CatalogApi__BaseUrl=http://api:8080/`; Web/API dùng `Server=db,1433`. Flag `CatalogApi__Enabled` và timeout được override qua `.env`. Cả ba host ports chỉ bind loopback và có thể đổi qua environment.

Không có migration/seed/restore startup hook. Existing `phonestore-web`, `phonestore-db`, volume và network cũ không bị thao tác.

## F. Credential Security

- Compose bắt buộc `MSSQL_SA_PASSWORD`; không còn fallback.
- `.env.example` để password trống và yêu cầu người dùng tự đặt secret mạnh.
- Docker guide không còn credential dùng được hoặc command migration chứa password.
- `.gitignore` tiếp tục ignore `.env`, certificates và database backups.
- SQL healthcheck đọc password từ container environment; không chèn giá trị Compose vào command text.
- CI tạo chuỗi kiểm tra tạm thời trong runner, không lưu secret vào repository.

Giới hạn: ignored local `.env` có thể vẫn chứa credential cũ và Git history vẫn có giá trị cũ. S31.5B không rotate container đang chạy và không rewrite history. Trước public push/deployment, người dùng phải thay local secret và đánh giá rotation/history theo quy trình riêng. Web/API vẫn dùng `sa`; least-privilege app login cần phê duyệt database riêng.

## G. Product Image Storage

Compose yêu cầu `PRODUCT_UPLOADS_PATH` tuyệt đối và bind-mount read-only vào Web `/app/wwwroot/uploads/products`. Admin Windows có thể được cấu hình trỏ cùng host directory ở bước sau. Không copy, move, rename hoặc delete 42 files hiện tại; orphan được giữ nguyên. Runtime uploads/backup/PII không được Git track.

Đường dẫn Admin chưa được đổi trong S31.5B. Việc đồng bộ config Admin là acceptance riêng, không phải source fix tự động.

## H. SQL Connectivity

- Docker Web/API: SQL authentication tới service `db,1433`, database từ `DB_NAME`.
- Local Web/API appsettings: giữ nguyên Windows/local development configuration.
- Admin WinForms: giữ nguyên PhoneStoreDB Windows; chưa chuyển sang Docker DB.
- SQL host mapping mặc định đề xuất 14334 để tránh stack cũ ở 14333, nhưng vẫn phải kiểm port ngay trước deployment.

Không tạo SQL login, không đổi quyền, không query/write database trong S31.5B.

## I. Static Validation Results

| Kiểm tra | Kết quả |
|---|---|
| `docker compose config --quiet` khi thiếu required vars | PASS: exit 1 trước deployment |
| `docker compose config --quiet` với vars kiểm thử tạm | PASS: exit 0 |
| Service discovery | PASS: `db`, `api`, `web` |
| Dependencies/healthchecks/network/volume | PASS qua rendered config |
| `git diff --check` | PASS; chỉ có cảnh báo CRLF policy của working tree |
| Web Release build `--no-restore` | PASS, 0 warnings/errors |
| API Release build `--no-restore` | PASS, 0 warnings/errors |

Docker CLI cảnh báo sandbox không đọc được user Docker config, nhưng Compose parser vẫn trả exit code đúng. Không build Docker images vì preflight ghi nhận chỉ khoảng 1 GB RAM free và task cấm các build nặng đồng thời. Không chạy `docker compose up`, không probe runtime health, không restart stack.

CI được mở rộng để build riêng Web/API images và validate Compose bằng secret tạm sinh ở runner. CI chưa chạy thực tế trong task local này.

## J. Remaining Blockers

1. Đổi ignored local `.env` sang secret mới; không dùng credential lịch sử.
2. Chốt host uploads directory an toàn, đủ 41 referenced images, và cấu hình Admin dùng chung nếu cần.
3. Kiểm lại 5015/5025/14334 và memory headroom ngay trước runtime.
4. Docker image build, actual healthcheck command và HTTP/HTTPS behavior chưa được runtime-test.
5. Docker database chưa có business data; backup/verify/restore cần phê duyệt riêng và phải bảo vệ PII.
6. `sa` còn là application credential; least-privilege login chưa được thiết kế/applied.
7. SQL image còn tag `2022-latest`; cần pin tested tag/digest trước production-like deployment.

## K. GO / NO-GO for S31.5C

**CONDITIONAL GO cho S31.5C backup/restore rehearsal plan và thao tác chỉ sau phê duyệt cụ thể.** Compose/security source đã STATIC PASS và Web/API BUILD PASS.

**NO-GO cho `docker compose up` hoặc chuyển Admin/Web/API sang Docker DB ngay lúc này.** Cần giải quyết secret local, uploads path, resources/ports và có phê duyệt backup/restore. Runtime vẫn **NOT TESTED**.
