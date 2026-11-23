# S34.9B — Quyết định kiến trúc xác thực REST API

Ngày 19/11/2026. Thiết kế để duyệt. Khuyến nghị dùng nhà cung cấp OIDC được vận hành sẵn cho client API đầu tiên. Trong thời gian chờ duyệt, Web và Admin tiếp tục đăng nhập như hiện tại, API bảo vệ tiếp tục từ chối client thật.

## A. Kiến trúc đã xác minh

| Thành phần | Bằng chứng trong repository | Hành vi |
|---|---|---|
| Customer Web | `src/PhoneStore.Web/Program.cs`, `Controllers/AccountController.cs`, `Authentication/CustomerCookieEvents.cs` trong project Web | Đăng nhập bằng cookie. ValidatePrincipal đọc lại Customer active, kiểm tra role, email, tên và từ chối phiên mất hiệu lực. Giỏ hàng/session CT02 thuộc Web. |
| WinForms | `src/PhoneStore.Admin/AdminServices.cs`, `Authentication/AdminSession.cs` trong project Admin, `src/PhoneStore.Business/Authentication/AdminAuthService.cs` | Xác thực tài khoản local, kiểm tra mật khẩu và duy trì AdminSession trong tiến trình. |
| Tài khoản | `src/PhoneStore.Business/Authentication/AuthService.cs`, `src/PhoneStore.Models/Entities/User.cs`, `Role.cs`, các cấu hình User/Role trong Data | Web và Admin dùng PasswordHasher<User> của ASP.NET Core Identity. Ứng dụng tự quản lý Users/Roles và login. Schema hiện tại chưa có mapping OIDC. UX_Users_SingleAdmin và CK_Roles_AdminIdentity bảo vệ quy tắc tối đa một Admin. |
| REST API | `src/PhoneStore.Api/Authorization/{ApiAuthenticationRegistration,UnconfiguredApiAuthentication,ApiAccess}.cs` | Options nhận cặp issuer HTTPS/audience. Scheme hiện trả NoResult. Policy Api.Customer/Api.Admin đòi đúng một NameIdentifier nội bộ hợp lệ và truy vấn SQL kiểm tra IsActive cùng role. |
| Dữ liệu cá nhân | `src/PhoneStore.Data/Purchasing/PurchaseStore.cs` | Truy vấn chi tiết đơn khách hàng giới hạn đồng thời OrderId và UserId. |

Baseline Git là main tại 77fb3eb, ahead 1 so với origin/main lưu cục bộ. Working tree có sửa đổi S34.8A/S34.9A. Theo lần đo S34.9A, source có 16 operation bảo vệ nhưng các image đang đo chỉ công bố Catalog/health. 280/280 là kết quả lịch sử. Phiên này chỉ đối chiếu source và tài liệu.

## B–C. Các phương án và tiêu chí lựa chọn

| Tiêu chí | A. OIDC được vận hành sẵn | B. OIDC chuẩn tự host | C. Duy trì đăng nhập hiện có |
|---|---|---|---|
| Bảo mật | Provider vận hành khóa, có thể yêu cầu MFA cho Admin. Nhóm chịu trách nhiệm cấu hình tenant, audience và linking | Kiểm soát tenant và khóa. Nhóm chịu trách nhiệm vá, xoay khóa, bảo vệ quản trị và phục hồi | API bảo vệ tiếp tục đóng. Web và WinForms vẫn có ranh giới xác thực hiện tại |
| Độ phức tạp | Vừa. Đăng ký client, mapping, validator và kiểm thử | Cao. Thêm toàn bộ vòng đời dịch vụ định danh | Thấp. Trì hoãn tích hợp |
| Chi phí hạ tầng | Không cần thêm container issuer. Phải xác nhận hạn mức, chi phí tenant và Internet khi demo | Cần RAM, TLS, lưu trữ bền vững, backup và môi trường thử | Không thêm tài nguyên |
| Tương thích | Thêm client dần, giữ login cũ | Tương tự A nếu provider đáp ứng profile token | Tương thích hoàn toàn với các client hiện tại |
| Bảo trì | Quản lý registration, quyền tenant, giới hạn dịch vụ | Thêm monitoring, nâng phiên bản, diễn tập sự cố | Duy trì hai luồng local; client API bảo vệ vẫn bị chặn |
| Triển khai | HTTPS, kết nối mạng tới discovery/JWKS, cấu hình API riêng từng môi trường | Thêm issuer với DNS ổn định, vận hành riêng dữ liệu/khóa và TLS | Không cần deployment |
| Phù hợp đồ án | Ưu tiên nếu có tenant, ngân sách và mạng demo xác nhận được | Phù hợp khi có yêu cầu tự chủ/offline cùng người chịu trách nhiệm vận hành | Hợp lý cho giai đoạn hiện tại; chưa đạt mục tiêu trình diễn protected API |

## D. Kiến trúc khuyến nghị

Chọn hướng giao thức OIDC cho đăng nhập và OAuth 2.0 access token cho API. Đề xuất A để giảm chi phí vận hành trên máy đang chạy SQL/Web/API. B là lựa chọn nếu demo đòi hỏi issuer nội bộ. C là trạng thái chuyển tiếp có thể duy trì. Chưa chọn nhà cung cấp, tenant hay tài khoản thử.

Lát cắt đầu tiên nên dùng client acceptance gọi một GET đơn hàng của Customer và một GET quản trị bằng hai danh tính riêng. Điều này kiểm tra cả ownership và quyền Admin trước khi thay đổi một client đang vận hành.

## E. Liên kết danh tính với Users hiện có

1. Xác minh token trước, sau đó tra cặp (iss, sub) đúng từng ký tự. Xác nhận provider phát subject người dùng ổn định trong access token dành cho API; subject pairwise có thể khác giữa client. Mỗi liên kết bổ sung đều cần xác minh độc lập. Email, tên, role claim và sub dạng số không được dùng để suy UserId. [OIDC Core §8, §5.7](https://openid.net/specs/openid-connect-core-1_0.html)
2. Đề xuất bảng liên kết bổ sung sau phê duyệt schema, với FK đến Users.UserId, unique (Issuer, Subject) và so sánh phân biệt hoa thường chính xác trên SQL Server. Một danh tính ngoài chỉ trỏ tới một tài khoản local. Tạo liên kết nguyên tử, từ chối xung đột kể cả hai yêu cầu đồng thời. Khi đụng liên kết cũ, yêu cầu đối soát thủ công.
3. Customer xác thực lại tài khoản local và hoàn tất đăng nhập OIDC mới trong cùng giao dịch liên kết, có state, nonce và thời hạn ngắn. Admin cần phê duyệt thủ công và MFA trước khi liên kết đúng UserId Admin hiện hữu. Không tự tạo Users/Roles, nhập password hash sang issuer hoặc đổi quyền trong bước liên kết.
4. Thu hồi liên kết có audit người thực hiện, thời điểm và kết quả. Chặn yêu cầu tiếp theo nếu liên kết đã gỡ hoặc tài khoản mất hiệu lực. FK hạn chế xóa user còn liên kết; quy trình xử lý tài khoản đã xóa không tự tái liên kết bằng email. Role và active luôn lấy từ SQL.

## F. Tích hợp từng client

| Client | Hành vi đề xuất |
|---|---|
| Customer Web | Duy trì cookie, validation và CT02. Catalog public tiếp tục hoạt động. Khi cần gọi protected API thay mặt khách, bổ sung OAuth authorization code + PKCE phía server, gắn phiên token với đúng UserId, lưu token phía server, xóa khi logout/đổi tài khoản. |
| WinForms Admin | Giữ AdminSession cho chức năng hiện có. Với chức năng API thử nghiệm, đăng ký native public client, mở browser hệ thống, code + PKCE S256, callback loopback trên máy người dùng. Kiểm tra danh tính local trả về tương ứng Admin đang thao tác trước khi kích hoạt chức năng. |
| Mobile | Public client riêng, browser hệ thống, code + PKCE S256, redirect URI gắn với ứng dụng. Lưu refresh token trong kho bí mật của hệ điều hành nếu cần phiên dài. |
| Swagger/manual | Client riêng chỉ dành cho acceptance, callback đăng ký chính xác, PKCE, không client secret/persist authorization. Cho phép hai tài khoản thử gọi các GET đúng quyền. Chỉ mô tả bearer trong OpenAPI khi validator thực sự hoạt động. |

Đăng ký redirect URI chính xác; với loopback native cho phép cổng động theo chuẩn. Dùng thư viện client chuẩn kiểm tra state, nonce và callback. WinForms/mobile không mang client secret. Ưu tiên access token ngắn hạn trong bộ nhớ. Nếu cần offline access, dùng refresh token rotation có phát hiện tái sử dụng hoặc ràng buộc token với client; WinForms dùng kho bí mật Windows. Khi logout, xóa token local, thu hồi refresh token tại issuer nếu có và xử lý kết thúc phiên SSO theo nhu cầu. Access token tự chứa có thể còn hiệu lực đến exp; API vẫn kiểm tra mapping/active/role mỗi request. [RFC 8252](https://www.rfc-editor.org/rfc/rfc8252) · [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700)

## G. Quyết định bảo mật API

API nhận access token dành cho PhoneStore qua HTTPS. Dùng middleware chuẩn, khóa ký từ JWKS của issuer đã duyệt, allowlist thuật toán, so khớp tuyệt đối issuer, kiểm tra audience, signature, exp, nbf nếu có và clock skew hữu hạn. Chỉ nhận token đại diện người dùng đúng profile đã duyệt; loại ID token, token app-only và token cho tài nguyên khác. Với profile RFC 9068 kiểm tra cả typ. [RFC 9068 §4](https://www.rfc-editor.org/rfc/rfc9068)

Sau mapping, dựng principal chỉ chứa một NameIdentifier nội bộ đáng tin; bỏ các claim định danh/quyền do token mang vào có thể xung đột. Áp Api.Customer hoặc Api.Admin, cộng scope theo client nếu được duyệt. Trả 401 + Bearer challenge khi thiếu/sai token hoặc không lập được danh tính local. Trả 403 khi đã xác định tài khoản nhưng inactive/sai quyền. Giữ ProblemDetails và ownership 404; sự cố SQL trả lỗi hạ tầng an toàn và không chạy action. Log traceId và loại kết quả, không log token hoặc bí mật.

## H. Docker, mạng và rollback

Mapping cấu hình dự kiến dùng ApiAuthentication__Oidc__Issuer và ApiAuthentication__Oidc__Audience. Trong phiên bản hiện tại, hai giá trị này chỉ được validate. Bản tích hợp tương lai cần có cách chủ động bật validator sau khi xác minh cấu hình. Issuer là URL HTTPS canonical truy cập được từ browser và container, discovery trả đúng issuer, JWKS dùng chứng chỉ được tin cậy. Kiểm tra DNS, egress, đồng hồ, rotation khóa, timeout và cache metadata. Khi thiếu khóa tin cậy, từ chối token. Public client và API kiểm chữ ký bất đối xứng không cần giữ private signing key.

Môi trường acceptance dùng issuer/audience và registration riêng, không tin khóa test tại production. Đề xuất triển khai trên acceptance trước. Rollback API về scheme hiện tại hoặc image/config đã xác minh, dừng client pilot và thu hồi grant thử. Có thể giữ mapping bổ sung ở trạng thái bất hoạt; rollback không đòi xóa tài khoản hay restore DB.

## I. Tiêu chí nghiệm thu đề xuất

| Ca | Bằng chứng phải thu |
|---|---|
| Token hợp lệ cho Customer đã liên kết | GET danh sách đơn trả 200 và chỉ chứa đơn của Customer |
| Hết hạn; sai issuer; sai audience; sai chữ ký | Bốn ca độc lập, mỗi ca trả 401 ProblemDetails và không chạy action |
| Thiếu token; ID token; subject chưa liên kết | Mỗi ca trả 401, không phát sinh tài khoản/liên kết |
| User đã disable sau khi nhận token | Request tiếp theo trả 403 với cùng token |
| Customer gọi route Admin; Admin gọi route chỉ dành cho Customer | 403 dù token có claim role khác với SQL |
| Customer truy cập đơn người khác | 404 như ID không tồn tại, không lộ chi tiết |
| Admin active đã liên kết | GET quản trị trả 200; gỡ liên kết thì request tiếp theo trả 401 |
| Tính ổn định | Kiểm thử duplicate mapping, logout/đổi tài khoản, expiry/refresh, luân chuyển khóa, issuer gián đoạn và rollback; cookie/CT02/AdminSession vẫn đạt regression |

Ba mức bằng chứng cần ghi riêng: test HTTP cô lập dùng issuer/khóa chỉ dành cho test; runtime acceptance dùng flow PKCE thật và tenant thử; production chỉ được nghiệm thu sau deployment riêng được duyệt. Test scheme hiện tại trả 401 cho mọi bearer chưa chứng minh validator mới phân biệt được các lỗi. Đợt pilot chỉ cần GET và dữ liệu thử đã duyệt.

## J. Quyết định cần phê duyệt

1. Chọn A hoặc B, tenant/issuer, người quản lý, yêu cầu MFA, ngân sách và điều kiện mạng khi demo. Có thể giữ C trong lúc chờ.
2. Chốt audience, profile access token, client đầu tiên, scopes, redirect URI, thời hạn token và chính sách refresh/logout.
3. Duyệt kho mapping, schema và quy trình chứng minh hai danh tính, xử lý liên kết Admin, thu hồi và khôi phục truy cập.
4. Duyệt môi trường, danh tính fixture và quyền triển khai acceptance riêng. Chỉ xét production sau khi đã có bằng chứng client thật và rollback.

Trạng thái S34.9B: hoàn tất thiết kế. Tích hợp client thật còn bị chặn bởi issuer, registration và mapping chưa được phê duyệt.
