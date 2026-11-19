# S34.9A — Nghiệm thu và chuẩn bị tích hợp REST API

## A. Baseline và mức nghiệm thu (2026-11-19)

Nhánh main tại 77fb3eb, ahead 1 so với origin/main lưu cục bộ. S34.8A đã có trong working tree, chưa commit; báo cáo đánh giá source gồm các thay đổi này. Không fetch, stage, commit hoặc push.

| Mức | Kết luận | Bằng chứng/giới hạn |
|---|---|---|
| Source | PASS build Release | Toàn solution trên .NET 10; 23 operation MVC. |
| HTTP cách ly | PASS theo các ca đã chạy | WebApplicationFactory, SQLite trong bộ nhớ hoặc test doubles. Danh tính hợp lệ đến từ scheme riêng trong test host. |
| Docker business | PASS cho 7 operation công khai | OpenAPI thực tế có 7 GET. Cả 7 GET trả 200. Các operation bảo vệ chưa deploy. |
| Docker acceptance | PARTIAL | OpenAPI của image .NET 8 có 5 GET công khai; live/ready trả 404. |
| Client thật | PASS cho luồng Web → Catalog detail đã quan sát; BLOCKED cho protected API | GET Web /products/39 trả 200; cùng cửa sổ thời gian có 2 log outbound Web và 1 log API /api/v1/products/39 thành công. |
| Production | NOT READY | Thiếu issuer/mapping, xác minh SQL Server concurrency, hiệu năng thực tế, triển khai và diễn tập rollback. |

## B. Inventory

Một dòng là một operation. Cột bằng chứng ghi theo mã test và trạng thái deployment. **P** = hiện diện trong Swagger business 5025 và acceptance 5286; **B** = chỉ business; **N** = thiếu trong cả hai document runtime. Trạng thái deployment không khẳng định mọi nhánh lỗi đã được gọi. Client **Catalog** là consumer read-only; Web hiện tích hợp trực tiếp chi tiết sản phẩm. **Customer** là client khách hàng dự kiến; **Admin** là client quản trị dự kiến. WinForms đang dùng Business/Data. **Ops** là công cụ vận hành.

Các mã test tương ứng: H = ApiFoundationTests, C = CatalogApiTests, P = AdminProductApiTests, O = OrderApiTests, U = AdminCustomerApiTests, R = AdminReportApiTests; ApiAuthorizationTests bao phủ nền tảng chung. Cột mã HTTP ghi thành công và lỗi nghiệp vụ chính. Mọi operation có policy còn có 401/403; lỗi hạ tầng chung có thể trả 500/503. Catalog và Health có thêm 429/504 theo policy rate-limit/timeout. Lỗi validation chủ yếu là ValidationProblemDetails; các lỗi khác dùng ProblemDetails. Các response 204 không có body.

| Method và route | Policy | Request | Response | HTTP chính | Client | Test / Docker |
|---|---|---|---|---|---|---|
| GET /api/health | Public | — | HealthResponse | 200,503 | Ops | H/P |
| GET /api/health/live | Public | — | HealthResponse | 200 | Ops | H/B |
| GET /api/health/ready | Public | — | HealthResponse | 200,503 | Ops | H/B |
| GET /api/v1/products | Public | ProductQueryRequest (query) | PagedProductsResponse | 200,400 | Catalog | C/P |
| GET /api/v1/products/{id} | Public | id:int | ProductDetailResponse | 200,400,404 | Web/Catalog | C/P |
| GET /api/v1/brands | Public | — | CatalogOptionResponse[] | 200 | Catalog | C/P |
| GET /api/v1/categories | Public | — | CatalogOptionResponse[] | 200 | Catalog | C/P |
| GET /api/admin/products | Api.Admin | AdminProductQuery | AdminProductPage | 200,400 | Admin | P/N |
| GET /api/admin/products/{id} | Api.Admin | id:int | AdminProductDetail | 200,404 | Admin | P/N |
| POST /api/admin/products | Api.Admin | ProductWriteRequest | AdminProductId và Location | 201,400,503 | Admin | P/N |
| PUT /api/admin/products/{id} | Api.Admin | id + ProductUpdateRequest | — | 204,400,404,409,503 | Admin | P/N |
| PATCH /api/admin/products/{id}/status | Api.Admin | id + ProductStatusRequest | — | 204,400,404,503 | Admin | P/N |
| GET /api/orders | Api.Customer | CustomerOrderQuery | OrderPage | 200,400 | Customer | O/N |
| GET /api/orders/{id} | Api.Customer | id:int | OrderDetailResponse | 200,404 | Customer | O/N |
| GET /api/admin/orders | Api.Admin | AdminOrderQuery | OrderPage | 200,400 | Admin | O/N |
| GET /api/admin/orders/{id} | Api.Admin | id:int | OrderDetailResponse | 200,404 | Admin | O/N |
| PATCH /api/admin/orders/{id}/status | Api.Admin | id + OrderStatusChange | — | 204,400,404,409,503 | Admin | O/N |
| GET /api/admin/customers | Api.Admin | AdminCustomerQuery | CustomerPage | 200,400 | Admin | U/N |
| GET /api/admin/customers/{id} | Api.Admin | id:int | CustomerDetail | 200,404 | Admin | U/N |
| PATCH /api/admin/customers/{id}/status | Api.Admin | id + CustomerStatusRequest | — | 204,400,404,503 | Admin | U/N |
| GET /api/admin/reports/overview | Api.Admin | ReportRange | ReportOverview | 200,400,503 | Admin | R/N |
| GET /api/admin/reports/revenue | Api.Admin | ReportRange | RevenueReport | 200,400,503 | Admin | R/N |
| GET /api/admin/reports/top-products | Api.Admin | ReportRange | TopProductsReport | 200,400,503 | Admin | R/N |

## C. Authentication

ApiAuthentication:Oidc:Issuer và Audience được kiểm tra theo cặp khi startup; chúng không kích hoạt JwtBearer. Scheme hiện trả NoResult. Không có bằng chứng về issuer đáng tin, kiểm tra chữ ký, thời hạn, audience hoặc ánh xạ thực (iss, sub) → UserId. Không suy UserId từ email/sub dạng số. Khi có danh tính nội bộ đáng tin, policy truy vấn SQL cho từng request để xác minh active và role. Customer ownership áp dụng thêm trong truy vấn đơn. Web vẫn dùng cookie/session; WinForms vẫn dùng AdminSession. Mobile/WinForms tương lai nên đăng ký native client dùng trình duyệt hệ thống, authorization code + PKCE, access token đúng audience; giữ bí mật máy chủ ngoài ứng dụng desktop. Tham khảo [RFC 8252](https://www.rfc-editor.org/rfc/rfc8252).

## D. OpenAPI

Đã kiểm tra document sinh qua HTTP trên test host và document thực ở hai cổng. Swagger mở theo Development. Metadata cũ ghi 401/403 nhưng từng operation chưa chỉ ra policy cụ thể. Test kiểm tra policy thiếu đã thất bại trước sửa. Operation filter chỉ bổ sung mô tả và extension x-phonestore-policy từ authorization metadata, phân biệt Api.Admin/Api.Customer; không quảng bá bearer security scheme chưa hoạt động. Thay đổi này chỉ thuộc tài liệu OpenAPI và test, chưa xuất hiện ở Docker.

Catalog v1 giữ đường dẫn/shape; phiên bản document v1 không thêm prefix vào /api/admin hay /api/orders. Catalog pageSize mặc định 10, tối đa 50; management/orders mặc định 20, tối đa 100. PATCH trạng thái đơn yêu cầu expectedStatus/targetStatus, PUT sản phẩm yêu cầu originalStockQuantity. ReportRange nhận fromDate/toDate dạng ngày YYYY-MM-DD bao gồm ngày cuối, tối đa 366 ngày, quy đổi UTC+7 sang nửa khoảng UTC. CustomerOrderQuery nhận timestamp fromUtc/toExclusiveUtc. Cần contract test với SDK client được chọn để xác nhận enum, decimal, nullability, ngày và hiển thị ProblemDetails/ValidationProblemDetails; Swagger hợp lệ chưa chứng minh client deserialize đúng.

## E. SQL Server và concurrency

Mã quản lý đơn kiểm tra expectedStatus dưới transaction Serializable, dùng UPDLOCK/HOLDLOCK và khóa sản phẩm theo ID khi hoàn tồn. SQLite đã nghiệm thu hủy đồng thời chỉ hoàn tồn một lần. SQL Server thực còn cần đo chờ khóa, deadlock, tính nguyên tử giữa trạng thái và stock, và hành vi sau lỗi/timeout. UPDATE sản phẩm kiểm tra tồn gốc; hai form cùng tồn gốc vẫn có thể ghi đè tên/giá. Cần thử trường hợp này, ghi nhận hoặc duyệt thay đổi versioning riêng. Reporting dùng GROUP BY, range OrderDate UTC và ranking hiện hữu. Đo trước ở SQLite: overview 3 và top-products 2 câu SQL tính cả authorization. Chưa có kết luận latency hay plan SQL Server. Không chạy các ca ghi trên PhoneStoreDB.

## F. Docker

- Business Compose ba file config --quiet thành công. SQL, API, Web business và hai container acceptance healthy. Cổng loopback business 5015/5025/14334, acceptance 5285/5286. Business API b79d2e025e11, ASP.NET Core 10.0.15; acceptance API b47fefaf6c87, ASP.NET Core 8.0.34. Business API Development; Web bật CatalogApi và gọi http://api:8080/. Hai API có key cấu hình connection string; không thấy tên biến OIDC/issuer/audience trong kiểm tra env. Không xuất giá trị bí mật.
- GET /api/admin/customers trả 404 trên cả hai runtime và route vắng trong cả hai spec. Đây là image cũ; chưa thể dùng 404 này để nghiệm thu authorization. Port 5286 đã smoke health, listing, brands, categories; detail có trong spec, chưa gọi trong phiên này.
- Trong tối đa 500 dòng log cuối mỗi API trong 30 phút kiểm tra, không thấy dòng mở đầu fail:/crit:. Đây là mẫu log giới hạn, không phải lịch sử sự cố toàn hệ thống.
- Image rollback lịch sử .NET 8 API 9b20d979aa6a, Web 75bfbc07a0ab còn cục bộ. Đối với lần nâng cấp kế tiếp, baseline quay về phải là **.NET 10 hiện chạy** (API 91b13b7108a3, Web bbcd7442e1e5). Ghim đúng digest trước thay đổi. Backup COPY_ONLY R-04 còn ở volume SQL, file 827392 byte, VERIFYONLY WITH CHECKSUM đã PASS tại R-04. SELECT msdb xác nhận full backup gần nhất hoàn tất 2026-11-19 04:42:01 theo đồng hồ SQL, copy_only=1, has_backup_checksums=1; PhoneStoreDB ONLINE. Chưa xác minh khả năng phục hồi độc lập/off-host hoặc diễn tập restore. Chi tiết đường dẫn trong [R-04](S34_2R_04_DIRECT_DOCKER_UPGRADE.md).

## G. Automated verification

- dotnet build PhoneStore.sln -c Release --no-restore: PASS, 0 warning/error.
- dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj -c Release --no-build --filter FullyQualifiedName~Api: 83/83 PASS.
- Full PhoneStore.Tests Release tuần tự: 280/280 PASS, 0 skipped, 3 phút 9 giây. git diff --check đạt (chỉ có cảnh báo chuyển LF/CRLF).
- Phần sửa trong phiên: Swagger operation filter, đăng ký filter trong Program.cs, assertion trong CatalogApiTests.cs, báo cáo này và PROGRESS.md. Không đổi endpoint, runtime authentication hay schema. Không ghi PhoneStoreDB, không deploy/commit/push.

## H. Kế hoạch nghiệm thu có điều kiện

| Bước | Điều kiện trước | Thao tác và kết quả mong đợi | Rủi ro và rollback | Tiêu chí |
|---|---|---|---|---|
| 1. Identity | Duyệt issuer, audience, native client, nguồn mapping ổn định và quy trình liên kết tài khoản | Kiểm tra discovery/JWKS, chữ ký, iss/aud/expiry; chỉ subject đã liên kết mới nhận UserId; active/role từ SQL | Sai mapping cấp nhầm quyền; rollback về scheme từ chối toàn bộ, thu hồi credential thử ở issuer | BLOCKED hiện tại; PASS khi cả ca hợp lệ và từ chối chạy với issuer thật |
| 2. Protected runtime | Image ứng viên được duyệt, runtime acceptance cách ly và target đã kiểm tra | Trên acceptance kiểm spec đủ 23 operation; GET bằng token thật, 401/403/ownership 404; tác vụ ghi chỉ trên fixture được duyệt | Nhầm đích; dừng ngay, trả acceptance về image/config cũ | PASS khi có bằng chứng request, response, target và DB trước/sau |
| 3. SQL concurrency | SQL Server và dữ liệu tổng hợp cách ly, được duyệt; thống nhất mức tải | Hai request cùng hủy; transition stale; cùng sản phẩm ở hai đơn; cập nhật stock/giá từ hai form; đối chiếu stock, order, lock/timeouts | Deadlock hoặc ảnh hưởng shared instance; dừng writer, giữ evidence, chỉ rollback fixture theo phê duyệt | PASS khi tối đa một lần hoàn tồn, không ghi dở và rủi ro lost-update được chấp nhận/xử lý |
| 4. Reporting | Fixture có đủ trạng thái và biên ngày, kế hoạch và ngưỡng hiệu năng thống nhất | Đối chiếu API/WinForms với SELECT; lấy execution plan, logical reads, latency p95; xác nhận Completed và top-10 đúng | Range lớn gây contention; dừng đo và dùng image cũ nếu regression | PASS khi số liệu khớp, biên UTC+7 đúng, ngưỡng tải đã duyệt đạt |
| 5. Client | Bước 1–4 đạt cho lát cắt cần dùng, callback và quyền client được đăng ký | WinForms hoặc mobile đăng nhập qua browser + PKCE, gọi API, xử lý expiry, 401/403/409; kiểm cookie Web/AdminSession | Lộ token hoặc ảnh hưởng login cũ; thu hồi token thử, tắt tích hợp client | PASS khi phiên thật hoàn tất luồng được phép và từ chối đúng luồng sai quyền |
| 6. Deploy & rollback | Phê duyệt riêng, pin candidate/baseline, backup phù hợp RPO, xác minh môi trường và secrets | Diễn tập rollout/rollback trên acceptance, kiểm health, spec, Catalog client, dữ liệu và volume; chỉ sau đó xét business | Sai image/schema/volume; quay về đúng image/config đã ghi, restore DB cần duyệt riêng | PASS khi diễn tập rollback thành công và có quyết định phát hành; hiện NO-GO production |

## I–J. Blockers và bước tiếp theo

Ưu tiên duyệt một issuer, một audience và cách liên kết (iss, sub) với UserId cho một client thử. Sau đó mới triển khai validator và tích hợp trên acceptance được duyệt. SQL Server concurrency, performance SQL thật, cơ chế secrets/TLS/quyền DB tối thiểu cho production, backup độc lập và diễn tập rollback còn mở. Không triển khai các bước kế hoạch trong S34.9A.
