# S34.9E — Kiến trúc xác thực REST API PhoneStore sau khi bỏ Auth0

**Quyết định thiết kế, 19/11/2026.** Dừng kế hoạch đăng ký tenant và liên kết tài khoản Auth0. Chọn phương án B cho phiên bản đang vận hành. Khi có client cần đọc đơn qua REST, đề xuất phương án A với OpenIddict tự host, dùng tài khoản PhoneStore hiện có. Mốc chuyển từ B sang A là nhu cầu client và phê duyệt lưu trữ, HTTPS, khóa cùng môi trường nghiệm thu. Chức năng cấp token thật hiện **chưa triển khai**.

## A. Baseline

Branch `main`, HEAD `e22fc345f58272376eadd0b7082b9ab2dfaaed63`, ahead 2 theo tracking ref cục bộ. Trước audit, `docs/PROGRESS.md` đã modified và báo cáo S34.9D-01 đã untracked. Đã đối chiếu S34.3A/B, S34.9A–D-01 với source. Đếm được 23 HTTP operation trong các API controller. Kết quả 298/298 thuộc lần chạy S34.9C với issuer test. Runtime, DB và tests chưa được kiểm tra lại trong phiên tài liệu này.

## B. Xác thực và phân quyền hiện có

| Luồng | Bằng chứng trong source | Phần tái sử dụng |
|---|---|---|
| Customer Web | `PhoneStore.Web/Controllers/AccountController.cs`, `Program.cs`, `Authentication/CustomerCookieEvents.cs` | Login tạo cookie PhoneStore.Auth. Validation đối chiếu Customer active và các claim với dữ liệu hiện tại. Logout xóa cart, checkout token rồi sign out; giữ ranh giới phiên CT02. |
| Mật khẩu | `PhoneStore.Business/Authentication/{AuthService,AdminAuthService}.cs`, `PhoneStore.Data/Authentication/AuthUserStore.cs` | Hai luồng dùng IPasswordHasher<User> của ASP.NET Core Identity, kiểm tra active, role và hash. Cả hai có thể ghi hash mới khi VerifyHashedPassword trả SuccessRehashNeeded. Vì thế đăng nhập thực tế vào DB nghiệp vụ không phải phép kiểm tra chỉ đọc. |
| Admin Windows | `PhoneStore.Admin/AdminServices.cs`, `Authentication/AdminSession.cs`, `Management/CatalogManagementGateway.cs` | AdminSession giữ AdminIdentity trong tiến trình, chỉ thiết lập với UserId dương và role Admin. Có đường kiểm tra lại quyền qua IsCurrentAdminAsync. AdminSession không phải credential mạng. |
| Danh tính nội bộ | `PhoneStore.Models/Entities/{User,Role}.cs`, cấu hình User/Role trong Data | Users.UserId, RoleId, IsActive và PasswordHash là nguồn hiện hữu. Unique index có điều kiện UX_Users_SingleAdmin cùng CK_Roles_AdminIdentity giới hạn tối đa một Admin. Chưa có đầy đủ UserManager/RoleManager và bộ bảng AspNet Identity. |
| REST API | `PhoneStore.Api/Authorization/ApiAccess.cs`, `Controllers/CustomerOrdersController.cs`, `PhoneStore.Data/Purchasing/PurchaseStore.cs` | Api.Customer/Api.Admin kiểm tra active và role bằng SQL theo NameIdentifier nội bộ. Hai GET đơn hàng còn yêu cầu orders:read; truy vấn chi tiết ràng buộc đồng thời OrderId và UserId. |

## C. So sánh ba phương án

| Tiêu chí | A. Identity + authorization server OpenIddict | B. Duy trì login và trì hoãn access token | C. Tự viết issuer và quản lý refresh token |
|---|---|---|---|
| Security | Giao thức chuẩn; nhóm chịu trách nhiệm login, client registration, keys, HTTPS và cập nhật | Giữ bề mặt tấn công hiện tại, protected API đóng cho client thật | Nhóm tự bảo đảm tính đúng đắn của phát hành, rotation, chống replay, thu hồi và discovery |
| Độ phức tạp | Trung bình–cao; tích hợp kho user và giao thức | Thấp | Cao, đặc biệt khi có nhiều client |
| Bảo trì | Theo dõi bản vá thư viện, chứng chỉ, dữ liệu grant | Duy trì cookie/session và chính sách SQL hiện có | Sở hữu toàn bộ vòng đời giao thức, thư viện client và xử lý sự cố |
| Database | Thêm dữ liệu giao thức; có thể dùng lại Users/Roles | Không cần schema mới | Cần tự thiết kế grant/token, replay và audit |
| Client | Native/mobile/desktop dùng thư viện OIDC chuẩn; có thể thêm dần | Web và WinForms hoạt động; chưa có bearer cho client ngoài | JWT có thể đọc được nhưng khả năng tương thích các flow phải tự xây và kiểm thử |
| Demo offline | Khả thi với DNS, HTTPS, khóa, discovery và SQL cục bộ đã chuẩn bị | Demo được hai ứng dụng hiện tại | Khả thi nhưng phải tự vận hành mọi thành phần |
| Docker | Có thể đồng host với API; thêm tải login, mã hóa và truy vấn, cần đo | Không tăng tải do issuer | Có thể đồng host; vẫn phát sinh tải và lưu trữ token |
| Công triển khai | Vừa đến cao, chia được theo lát cắt Customer GET | Ít nhất ngay bây giờ | Cao nhất trong vòng đời; không có lợi ích đủ lớn cho đồ án |

## D. Kiến trúc đích đề xuất

1. **Authentication.** Màn hình đăng nhập của issuer trong tương lai xác minh Customer qua nghiệp vụ và hasher hiện có. OpenIddict đảm trách giao thức. Nếu cần UserManager của ASP.NET Core Identity, thiết kế adapter/store tối thiểu cho User hiện tại, kiểm thử semantics chuẩn hóa email, active, role và rehash. Không chuyển sang bộ AspNetUsers/AspNetRoles chỉ để có PKCE. Với pilot, có thể dùng trực tiếp AuthService và một scheme cookie riêng của issuer.
2. **Token issuance.** Đăng ký một public native client Customer. Browser hệ thống, Authorization Code + PKCE S256, redirect loopback đã đăng ký, state và nonce khi dùng OIDC. Issuer quyết định audience và scope orders:read. Không cấp password grant, implicit, client credentials hay quyền Admin cho client pilot. Các endpoint giao thức mới thuộc issuer, tách khỏi danh mục 23 business/health operation. [OpenIddict – chọn flow](https://documentation.openiddict.com/guides/choosing-the-right-flow.html)
3. **Token validation.** Ưu tiên thử OpenIddict Validation đồng host với server để hỗ trợ access token được mã hóa mặc định. Đánh giá lại JwtBearer nếu chọn access token chỉ ký và profile tương thích. Duyệt rõ issuer HTTPS canonical, audience, loại token, chữ ký, hạn, client/presenter và scope. Không thể chỉ thay domain trong cấu hình Auth0 rồi coi hai loại token là tương thích. [OpenIddict – signing và encryption](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html)
4. **Identity mapping.** Với duy nhất issuer nội bộ được tin cậy, issuer có thể phát sub ổn định từ UserId đã xác thực; API chuyển subject hợp lệ thành đúng một NameIdentifier dương. Điều kiện là UserId bất biến, không tái sử dụng và việc lộ ID nội bộ được duyệt. Nếu cần subject opaque, duyệt bảng ánh xạ riêng. Không lấy ID từ email, query, body hay token của issuer khác. Không còn yêu cầu liên kết Auth0 cho người dùng hiện tại.
5. **Authorization.** Sau validation, tiếp tục áp policy local, active, role và ownership. Scopes chỉ giới hạn quyền đã có. Giữ 401 cho credential thiếu/sai hoặc không thiết lập được danh tính local, 403 cho tài khoản đã nhận diện nhưng inactive/sai role/thiếu scope, 404 cho đơn ngoài ownership.
6. **Session management.** Cookie Web, cart/checkout token và WinForms AdminSession tiếp tục độc lập. Cookie của issuer cần scheme, tên và phạm vi riêng. Logout của một client xóa phiên/token local của client đó. Với pilot, access token ngắn hạn, chỉ lưu bộ nhớ, chưa yêu cầu refresh token. Nếu thêm offline_access, dùng cơ chế rolling refresh token của thư viện, lưu trạng thái, thu hồi grant/token và kiểm thử reuse, retry, logout. Chốt SLA thu hồi: JWT tự chứa có thể còn hiệu lực đến exp nếu API không kiểm token entry/introspection; kiểm active/role mỗi request vẫn cần. [OpenIddict – token storage và revocation](https://documentation.openiddict.com/configuration/token-storage.html)

## E. Tác động tới code S34.9C

Các thay đổi dưới đây dành cho giai đoạn triển khai được duyệt.

| File / cấu hình | Giữ, sửa hoặc loại bỏ dự kiến |
|---|---|
| `src/PhoneStore.Api/Authorization/Auth0Bearer.cs` | Tách phần dùng chung (principal tối giản, challenge/forbid). Thay giả định client_id, hậu tố @clients, tenant, profile và lookup Auth0 theo contract của issuer mới. Chỉ bỏ file và IVerifiedIdentityLookup sau khi có quyết định về sub và test thay thế. |
| `Authorization/ApiAuthenticationRegistration.cs`, `appsettings.json` | Giữ opt-in và validation khi startup. Rà lại ApiAuthentication:Oidc:{Enabled,Issuer,Audience,CustomerClientId}, điều kiện issuer ở root và thông báo Auth0. Duyệt chuyển cấu hình tường minh; mặc định tắt. Không tự diễn giải cấu hình Auth0 cũ thành cấu hình issuer mới. |
| `PhoneStore.Api.csproj`, `Program.cs` | JwtBearer của Microsoft là dependency chung. Chỉ gỡ nếu validator mới thay thế hoàn toàn. Giữ middleware order, API contracts và OpenAPI metadata. |
| `Authorization/{ApiAccess,UnconfiguredApiAuthentication}.cs`, `Controllers/CustomerOrdersController.cs`, `Infrastructure/AuthorizationDocumentationFilter.cs` | Giữ chính sách, scope, ownership, ProblemDetails và chế độ đóng. Products, Orders, Customers, Reporting, Catalog và Health giữ route/DTO hiện tại. |
| `tests/PhoneStore.Tests/Auth0BearerTests.cs` | Chuyển các ca chữ ký, issuer, audience, expiry, loại token, active/ownership thành test cho issuer mới; thay fixture Auth0/JWKS và quy tắc claim đặc thù. Bổ sung test code exchange thật trên host cô lập trước khi bỏ bộ cũ. |
| `ApiAuthorizationTests.cs`, `OrderApiTests.cs`, `CatalogApiTests.cs` trong tests | Giữ regression 401/403/404, default deny, owner và OpenAPI; chỉnh phần options/claims fixture nếu contract mới yêu cầu. |
| Tài liệu và Compose | S34.3A/B, 9A–D-01 lưu bằng chứng lịch sử. PROGRESS trỏ tới quyết định mới; khi triển khai mới cập nhật hướng dẫn cấu hình, khóa và rollback. S34.9E chưa cần sửa Compose. |

## F–G. Database, vận hành và rủi ro

Nếu chọn EF Core stores chuẩn, dự kiến thêm bốn nhóm bảng **applications, authorizations, scopes, tokens**. Đây là schema đề xuất; tên, kiểu khóa, indexes, retention và migration phải review trên SQL Server trước khi phê duyệt. Dữ liệu đăng ký client/scope cần provisioning có kiểm soát. Giữ nguyên các FK nghiệp vụ và constraint một Admin; không tạo bản sao user hoặc chuyển password hash. B không phát sinh bảng. [OpenIddict – EF Core](https://documentation.openiddict.com/integrations/entity-framework-core)

Ưu tiên khảo sát đồng host issuer và API để tiết kiệm RAM; đổi lại chúng chia sẻ tiến trình, quyền truy cập khóa và chu kỳ deploy. Nếu cần tách quyền ký khỏi API, đánh giá host riêng và đo RAM trước. Demo offline cần hostname issuer ổn định, chứng chỉ HTTPS được cả Windows và container tin cậy, đồng hồ đúng, discovery/keys hoạt động khi mất Internet. Giữ riêng chứng chỉ TLS và credentials ký/mã hóa bền vững qua restart; private key ngoài Git/image, phân quyền tối thiểu. Lập lịch xoay khóa với giai đoạn chồng lấn cho token đang lưu hành. Không dùng khóa phát sinh lại mỗi lần khởi động cho môi trường nghiệm thu lâu dài.

Đăng nhập issuer mới tạo thêm bề mặt dò mật khẩu và CSRF. Cần chống giả mạo ở form, giới hạn lần thử, lỗi đăng nhập không tiết lộ email, allowlist redirect URI và log không chứa credential. API chỉ cấp quyền sau xác minh tài khoản; role từ token không thay cho SQL. Đo chi phí validation/thu hồi trước khi bật refresh hay kiểm token entry trên mọi request.

## H–J. Lộ trình, nghiệm thu và phê duyệt

| Bước | Công việc sau phê duyệt | Điều kiện qua bước |
|---|---|---|
| 1. Chốt pilot | Chọn client Customer, hai GET đơn, dạng sub, host issuer, URL HTTPS, lifetime, strategy validation và ngân sách Docker | Có người vận hành khóa, cơ chế rollback, môi trường cô lập và xác nhận có cần refresh |
| 2. Prototype | Tích hợp server chuẩn và login hiện có trên test host; kiểm tra token format, client/presenter và options trước khi sửa cấu hình thực | PKCE đúng thành công; sai verifier, sai redirect, code replay, ID token, chữ ký/issuer/audience sai hoặc token hết hạn đều bị từ chối |
| 3. Duyệt storage | Review SQL migration bổ sung và provisioning, quyền truy cập DB/khóa; thử trên acceptance có phê duyệt riêng | Tài khoản và hash hiện có được bảo toàn; unique Admin còn hiệu lực; không có thao tác lên DB nghiệp vụ trong test |
| 4. Nghiệm thu client | Customer đăng nhập bằng browser hệ thống, đọc danh sách và chi tiết đơn qua REST; kiểm thử logout, inactive, role, ownership và regression Web/WinForms | Own GET 200; anonymous/invalid 401; sai role/scope/inactive 403; đơn khách khác 404. So OpenAPI đủ 23 operation hiện có. Nếu bật refresh, kiểm rotation/revoke và hành vi access token còn hạn |
| 5. Rollout có kiểm soát | Duyệt riêng deployment, thử restart/rotation khóa, offline demo và rollback trên acceptance | Tắt issuer/bearer hoặc quay về image/config đã pin khiến protected routes đóng; Web cookie, CT02, AdminSession, dữ liệu và volume an toàn. Rollback không cần drop bảng |

**Quyền phê duyệt còn thiếu:** lựa chọn triển khai A, integration trực tiếp AuthService hay Identity stores, host/key custody, subject policy, schema/provisioning và môi trường test, sau đó mới tới deployment. Không cần tạo tenant, lấy Client ID hoặc xác minh domain Auth0 để tiếp tục nhánh thiết kế này.

**Trạng thái:** hoàn tất audit và kế hoạch S34.9E. Chưa chạy build/test cho thay đổi tài liệu, chưa nghiệm thu issuer nội bộ hoặc client thật, chưa triển khai.
