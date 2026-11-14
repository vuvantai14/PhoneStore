# S31.5A — PhoneStore Docker Compose Deployment Preflight

Ngày xác minh: 2026-11-14  
Phạm vi: **read-only audit và deployment plan**  
Kết luận: **PASS preflight / CONDITIONAL GO cho S31.5B implementation; NO-GO cho deployment hiện tại**

## A. Git State

- Branch: `main`.
- HEAD khi bắt đầu: `c6de0474731a1e2397ff77f3790743cbda99c33b` (`update md`).
- Working tree sạch; tài liệu S31.3G/S31.4 đã được commit bên ngoài tác vụ trước S31.5A.
- Không stage, reset, clean, stash, commit, push hoặc đổi Git config/remote.

## B. Docker Environment

### Đã xác minh

| Hạng mục | Kết quả |
|---|---|
| Docker Desktop | 4.98.0 |
| Docker CLI / Engine | 29.9.2 / API 1.56 |
| Docker Compose | v5.6.1 |
| Container mode | Linux, `linux/amd64` |
| Engine resources | 12 CPU, 7.6 GB RAM, overlayfs |
| Host | 15.8 GB RAM; tại lúc đo chỉ còn khoảng 1 GB free |
| Ổ C | 475.8 GB, khoảng 306.1 GB free |
| Docker usage | 27 images (khoảng 5 GB), build cache khoảng 4.6 GB |

Docker CLI trong sandbox không đọc được Docker named pipe/config; cùng CLI ngoài sandbox read-only kết nối Engine thành công. Đây là khác biệt quyền môi trường Codex, không phải Docker Engine bị dừng.

### Ports và runtime hiện có

- 5015: không thấy listener.
- 5025: không thấy listener.
- 14333: đang được Docker/WSL relay giữ cho SQL container.
- Windows SQL Server service `MSSQLSERVER`: Running; ứng dụng Windows hiện dùng `localhost/PhoneStoreDB`.
- Không thấy TCP listener host 1433 trong phép đo; không được suy ra Windows SQL đã tắt vì local apps/sqlcmd vẫn kết nối bằng cơ chế Windows hiện có.

Hai PhoneStore containers xuất hiện/chạy trong lúc audit do tác động bên ngoài task:

| Container | Trạng thái | Port |
|---|---|---|
| `phonestore-db` | Running, healthy | 14333 → 1433 |
| `phonestore-web` | Running, không có container healthcheck | 8080 → 8080 |

Task không khởi động, dừng, restart hoặc xóa container/volume/network. Web hiện tại trả HTTP 200 tại port 8080, nhưng không phải acceptance của thiết kế S31.5 vì image được tạo ngày 11/11/2026 và chưa có API service.

Existing Docker objects gồm network `phonestore_network` và SQL volume `phonestore_mssql_data`. Có nhiều container/volume/network không thuộc PhoneStore; tuyệt đối không cleanup theo tên chung hoặc dùng `docker system prune`.

## C. Existing Docker Configuration

Repository hiện có:

- Một root `Dockerfile`, chỉ build/publish `PhoneStore.Web`.
- `docker-compose.yml` chỉ có services `db` và `web`.
- `.dockerignore`, `.env.example`, Docker/CI guide và GitHub Actions build Web image.

### Điểm đúng

- Web dùng multi-stage .NET 8 SDK → ASP.NET Core 8 runtime.
- SQL Server image là Linux SQL Server 2022.
- SQL dùng named volume và healthcheck `SELECT 1`.
- Web phụ thuộc DB healthy.
- Bridge network và restart policy `unless-stopped` đã có.
- Host SQL port mặc định 14333 tránh Windows SQL 1433.

### Khoảng trống

1. Không có Dockerfile/image/service cho `PhoneStore.Api`.
2. Compose không publish 5025 và không có API health dependency.
3. Web không set `CatalogApi__Enabled`, `CatalogApi__BaseUrl` hoặc timeout; mặc định flag OFF.
4. Base URL trong appsettings là localhost:5025, không đúng từ Web container. Khi ON phải dùng service DNS, dự kiến `http://api:8080/`.
5. Web container không có healthcheck.
6. Compose dùng fixed `container_name`, gây khó chạy project acceptance song song và dễ xung đột với containers hiện có.
7. CI chỉ build Web image; chưa build API image hoặc validate Compose.
8. SQL image dùng floating tag `2022-latest`; cần pin tag/digest trước deployment có kiểm soát.

## D. Web/API Container Readiness

### Web

- `PhoneStore.Web` target `net8.0`; Dockerfile copy đúng Business/Data/Models dependencies.
- Runtime image ASP.NET Core 8, internal HTTP port 8080.
- `UseStaticFiles` đã bật.
- `UseHttpsRedirection` cần được nghiệm thu với HTTP-only container/reverse proxy để tránh warning hoặc redirect sai.

### API

- `PhoneStore.Api` target `net8.0`; references Business/Data/Models.
- Có `GET /api/health` kiểm SQL và bốn endpoint catalog.
- Chưa có Dockerfile/API image hoặc Compose service.
- Swagger chỉ bật Development, đúng policy.
- API nên nghe internal 8080 và publish host 5025 trong local acceptance.
- Connection string trong container phải dùng `Server=db,1433`, không dùng localhost.

### Product images — blocker tái tạo

- Database nguồn có 41 ProductImages, 41 file unique.
- Thư mục local có 42 file (41 được DB tham chiếu, 1 orphan), tổng khoảng 3.08 MB.
- 41 references đều có file trên disk.
- **0 file upload được Git track**; toàn bộ thư mục uploads bị `.gitignore`.
- Dockerfile hiện copy source Web, nên build local có thể vô tình đóng gói runtime uploads ignored; build từ GitHub checkout sẽ không có ảnh. Đây là build không reproducible và có nguy cơ đưa dữ liệu runtime ngoài Git vào image.

Thiết kế đề xuất:

- Không bake uploads runtime vào image.
- Chuẩn bị deployment asset riêng chỉ gồm 41 file được DB tham chiếu, kiểm hash/manifest.
- Dùng host bind mount cho `/app/wwwroot/uploads/products` nếu Admin WinForms Windows vẫn quản lý file ảnh; cùng host path được cấu hình cho Admin.
- Không commit ảnh runtime/private hoặc backup database lên GitHub.

## E. SQL Server Compatibility

### Nguồn Windows

| Thuộc tính | Giá trị đã xác minh |
|---|---|
| Database | `PhoneStoreDB`, ONLINE |
| SQL Server | 2022 Developer, 16.0.1000.6 RTM |
| Compatibility | 160 |
| Collation | `SQL_Latin1_General_CP1_CI_AS` |
| Recovery model | FULL |
| Size | 16 MB (8 MB data + 8 MB log) |
| CLR user assemblies | 0 |
| Memory-optimized tables | 0 |
| Migrations | InitialCreate + EnforceSingleAdmin, EF 8.0.28 |

Current read-only counts:

| Roles | Users | Brands | Categories | Products | Images | Orders | Details |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 2 | 3 | 8 | 2 | 40 | 41 | 12 | 28 |

Counts Orders/Details cao hơn snapshot S31.3G (11/26), do thay đổi ngoài task. S31.5A không thực hiện DML.

### Container hiện có

- SQL Server 2022 Developer 16.0.4315.2 (mới hơn source cùng major).
- `PhoneStoreDockerDB` ONLINE, 16 MB.
- Hai migration đã apply, nhưng Products/Images/Orders/Details đều bằng 0.
- Named volume đang lưu schema rỗng hiện tại; không overwrite hoặc xóa volume này khi chưa có phê duyệt.

Standard EF schema không dùng CLR hoặc memory-optimized tables nên không thấy incompatibility rõ ràng với SQL Server Linux 2022. Backup từ build 16.0.1000.6 có thể restore lên target 16.0.4315.2 cùng major; vẫn phải chạy `RESTORE VERIFYONLY`, `RESTORE FILELISTONLY` và restore rehearsal để chứng minh.

Backup history nguồn ghi nhận full backup gần nhất ngày 02/11/2026, cũ hơn dữ liệu hiện tại. Không dùng backup đó để tuyên bố migration data hoàn chỉnh.

## F. Database Portability

Phương án ưu tiên:

1. Tạo **full backup mới** từ `PhoneStoreDB` nguồn sau checkpoint read-only và phê duyệt riêng.
2. Lưu backup ngoài repository, ở thư mục riêng có ACL phù hợp.
3. Verify backup, mount thư mục backup read-only vào SQL container và chạy `RESTORE FILELISTONLY`.
4. Restore vào database/volume acceptance mới với `MOVE` tới Linux data path.
5. Không copy trực tiếp MDF/LDF đang hoạt động.
6. Đối chiếu schema, migrations, counts, constraints, orphan checks và 41 image references.
7. Restart container có kiểm soát để chứng minh named-volume persistence.
8. Chỉ chuyển Web/API/Admin sang DB restored sau nghiệm thu và phê duyệt riêng.

Để tránh ghi đè database/volume hiện có, rehearsal nên dùng Compose project, host SQL port và named volume mới. Không dùng `WITH REPLACE`, drop database hoặc `docker compose down -v` nếu chưa được duyệt rõ.

Database chứa Users, recipient phone/address và Orders nên có dữ liệu cá nhân. Backup thật không được public. Nếu cần portfolio/demo public, phải tạo dữ liệu demo đã ẩn danh bằng quy trình được phê duyệt riêng; không sửa trực tiếp backup production-like để “làm sạch” tùy ý.

## G. Credential Security

**SECURITY BLOCKER từ S31.4 vẫn tồn tại:**

- Compose có default SQL SA password.
- `.env.example` và Docker guide chứa cùng default credential.
- Giá trị đã tồn tại trong Git history từ commit Docker trước đó.
- Web hiện dùng SA trong application connection string.

S31.5A không in credential vào báo cáo, không rotate và không rewrite Git history.

Điều kiện S31.5B:

1. Compose dùng required interpolation như `${MSSQL_SA_PASSWORD:?required}`, không có fallback.
2. `.env.example` chỉ chứa placeholder/blank và hướng dẫn tạo secret mạnh.
3. Docker guide không chứa password dùng được hoặc command line lộ password.
4. App Web/API không nên dùng SA lâu dài. Tạo SQL login/user app least-privilege là thay đổi security/database cần phê duyệt riêng.
5. Nếu credential mặc định từng được dùng ngoài máy local, rotate trước khi chia sẻ.
6. Lập kế hoạch xử lý Git history riêng; không force-push/rewrite nếu chưa phối hợp.

Local Compose development có thể dùng ignored `.env`, nhưng environment vẫn nhìn thấy qua container inspect với người có Docker access. Production cần secret manager/orchestrator phù hợp.

## H. Proposed Compose Architecture

```text
Browser
  ├─ 127.0.0.1:5015 → web:8080
  │                       │ Product Detail, flag ON
  │                       ▼
  └─ 127.0.0.1:5025 → api:8080
                          │
                          ▼
                       db:1433 → named SQL volume

Admin WinForms (Windows)
  └─ localhost:<isolated SQL host port> → db:1433
     + shared host uploads directory
```

### `db`

- SQL Server 2022 Linux, preferably pinned tag/digest.
- Required SA secret only for initialization/restore.
- Named volume for `/var/opt/mssql`.
- Healthcheck `SELECT 1`.
- Loopback host binding và configurable port; 14333 hiện bị container cũ chiếm.
- Restart `unless-stopped` cho local demo.

### `api`

- New multi-stage .NET 8 image.
- Internal port 8080; local host mapping 5025.
- Connection string uses `db,1433`.
- Depends on DB healthy; `/api/health` used for readiness.
- Production environment; Swagger chỉ dùng Development local khi chủ động bật.

### `web`

- Existing multi-stage image sau hardening.
- Internal 8080; local host mapping 5015.
- DB connection uses `db,1433`.
- `CatalogApi__Enabled=true|false`.
- `CatalogApi__BaseUrl=http://api:8080/`.
- API dependency/readiness phải giữ được fallback policy.

### Admin Windows

- Dùng environment/config tạm trỏ `localhost,<isolated-port>`; không overwrite main appsettings.
- Dùng port song song như 14334 khi 14333 đang bận, sau khi kiểm listener.
- Product image path trỏ cùng Windows host directory bind-mounted vào Web.
- Read-only smoke trước; CRUD cần phê duyệt ghi riêng.

## I. Risks & Blockers

| Mức | Vấn đề | Điều kiện xử lý |
|---|---|---|
| Blocker | Không có API Dockerfile/service | Implement và build API image ở S31.5B |
| Blocker | Default SA credential tracked/history | Remove defaults, require secret, rotate if used |
| Blocker | 0 product images tracked; local image build không reproducible | Secure asset deployment/mount + manifest |
| Blocker | Existing containers/volume/port 14333 active | Isolated project/ports/volume hoặc xin phép shutdown rõ ràng |
| High | Docker DB chỉ có schema, không business data | Backup/restore rehearsal có phê duyệt |
| High | Web/API dùng SA | Least-privilege app login hoặc ghi rõ local-only limitation |
| High | Host free RAM chỉ khoảng 1 GB lúc đo | Tăng memory headroom trước build/restore |
| Medium | Web không có healthcheck | Chọn health probe khả dụng trong runtime image |
| Medium | SQL tag floating; CI chỉ build Web | Pin image và mở rộng CI/Compose validation |
| Medium | HTTP/HTTPS proxy behavior chưa nghiệm thu | Kiểm forwarded scheme và local HTTP behavior |
| Data | Source chứa PII, backup cũ | Backup mới ngoài Git; anonymized demo cho public portfolio |

## J. Implementation Plan S31.5B–E

### S31.5B — Compose/security implementation

- Không đụng stack đang chạy.
- Loại default credential khỏi Compose/example/docs.
- Thêm API Dockerfile/service và safe service DNS config.
- Thêm configurable loopback ports, unique project compatibility, healthchecks.
- Thiết kế uploads bind mount/asset manifest.
- Mở rộng CI build Web + API và `docker compose config`.
- Build images/config validation; chưa restore main data.

### S31.5C — Database transfer rehearsal

- Xin phê duyệt full backup mới.
- Verify backup; restore vào volume/database acceptance mới.
- Không overwrite existing volume/database.
- Đối chiếu schema, migrations, counts, constraints và image references.
- Ghi rõ PII handling; không commit backup.

### S31.5D — Concurrent runtime acceptance

- Chạy isolated `web/api/db`.
- Verify container health, API endpoints, Web feature flag OFF/ON/fallback, static images.
- Xác minh Admin Windows read-only kết nối Docker DB bằng config tạm.
- Kiểm restart persistence; không CRUD nếu chưa có phê duyệt ghi.

### S31.5E — Final hardening/documentation

- Regression tests, logs, limits, health/readiness và failure scenarios.
- Git secret/artifact audit.
- Runbook backup/restore/start/stop; không dùng `down -v` như thao tác thường.
- Chốt portfolio/demo data policy, known limitations và deployment evidence.

Validation checklist:

- [ ] Web/API images build từ clean Git checkout.
- [ ] `docker compose config` không missing variables hoặc secret fallback.
- [ ] DB/API/Web health đạt.
- [ ] SQL restore + migrations/schema/counts khớp.
- [ ] 41 referenced product images truy cập được.
- [ ] Feature flag OFF/ON và API fallback đúng.
- [ ] API validation/rate limit/timeout/Problem Details.
- [ ] Admin read-only kết nối đúng container database.
- [ ] SQL data/uploads tồn tại sau controlled restart.
- [ ] Tests không ghi PhoneStoreDB nguồn.
- [ ] Không secret/backup/runtime uploads trong Git.

## K. GO / NO-GO for S31.5B

**PASS S31.5A / CONDITIONAL GO chỉ cho S31.5B implementation hardening.**

Điều kiện:

1. Không đụng containers/volume hiện có.
2. Sửa credential blocker trước build/deployment.
3. Thêm API service và internal DNS config.
4. Chốt uploads deployment strategy.
5. Dùng isolated project/ports/volume và kiểm memory headroom.

**NO-GO cho triển khai Compose ba service hoặc chuyển Admin sang Docker DB ở trạng thái hiện tại.**

S31.5A không sửa source/Docker config, không build image, không tạo backup, không restore/migrate/seed, không thay database/container và không commit/push.
