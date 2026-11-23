# S34.9D-01 — Chuẩn bị Auth0 tenant và PKCE cho Customer

Ngày 19/11/2026. **PARTIAL**. Đã đối chiếu source và môi trường Docker. Đăng nhập tenant thật **NOT TESTED**; Customer đọc đơn qua acceptance **BLOCKED**.

## A. Baseline

Trước khi viết tài liệu, working tree sạch. Branch `main`, HEAD `e22fc345f58272376eadd0b7082b9ab2dfaaed63`, ahead 2 so với tracking ref cục bộ. Đã đọc các báo cáo S34.9A/B/C và PROGRESS. Bằng chứng source chính nằm trong `src/PhoneStore.Api/Authorization/{ApiAuthenticationRegistration,Auth0Bearer,ApiAccess}.cs`, `Controllers/CustomerOrdersController.cs` và `scripts/acceptance/{docker-compose.yml,Compose.ps1}`.

## B–C. Checklist tenant, audience và scopes

| Thuộc tính | Giá trị cần xác nhận trước pilot | Trạng thái |
|---|---|---|
| Domain và issuer | Domain acceptance do người quản trị cung cấp. Đọc discovery qua HTTPS và so khớp nguyên văn issuer dạng `https://<DOMAIN>/`, kể cả slash cuối | Chưa có domain được cung cấp để kiểm tra |
| Custom API | Identifier đề xuất `https://api.phonestore.local`; dùng chính giá trị này làm audience khi xin token và validate | Chưa xác nhận registration |
| Profile và permission | Chọn RFC 9068, ký RS256, khai báo và cho phép Customer client xin `orders:read` | Chưa kiểm trên tenant |
| Application | PhoneStore Customer Test Client, loại Native public, token endpoint authentication None, Authorization Code + PKCE S256, không cấp grant cho máy | Chưa có Client ID được xác nhận |
| Scopes | `openid` cho đăng nhập, `profile email` nếu cần thông tin hiển thị theo thiết kế; `orders:read` là scope API bắt buộc. Không cần offline_access cho pilot | Chờ kiểm tra token cấp thực tế |
| Allowed Callback / Logout URLs | Đăng ký URI chính xác của listener đã xác minh ở mục D | Đề xuất, chưa đăng ký |
| Allowed Web Origins | Để trống cho native harness dùng system browser và đổi code trong tiến trình native. Nếu đổi sang SPA, duyệt registration cùng origin cụ thể của SPA | Không áp dụng cho pilot native |

Audience là định danh API; địa chỉ gửi GET phụ thuộc môi trường acceptance. Auth0 profile mặc định và profile RFC 9068 khác nhau ở header type và claim client ID; phải kiểm tra profile của **đúng API**. [Auth0 Access Token Profiles](https://auth0.com/docs/secure/tokens/access-tokens/access-token-profiles)

## D. Client và callback

Chọn harness native độc lập dùng thư viện OIDC, giữ login Web và AdminSession hiện tại. Trong scripts/tools chưa tìm thấy listener PKCE tương ứng. URI ứng viên là `http://127.0.0.1:7890/callback` và, nếu harness nhận logout redirect, `http://127.0.0.1:7890/signed-out`. Netstat chưa thấy TCP listener ở 7890 khi kiểm tra. Trước khi đăng ký phải thử bind loopback từ chính harness và xác nhận tenant cho phép URI này với loại Native. Port trống hiện tại chưa chứng minh callback hoạt động. [RFC 8252 §7.3](https://www.rfc-editor.org/rfc/rfc8252#section-7.3)

1. Khởi tạo listener loopback. Tạo state, nonce và verifier ngẫu nhiên dùng một lần cho từng giao dịch; giữ trong bộ nhớ.
2. Mở system browser tới authorization endpoint lấy từ discovery với client_id, audience, scopes, response_type=code, redirect_uri và code_challenge S256.
3. Tại callback, kiểm tra đúng path, state, thời hạn và giao dịch còn chờ; từ chối callback sai hoặc lặp. Đổi code qua HTTPS với cùng redirect_uri và verifier. SDK kiểm tra ID token gồm issuer, audience, chữ ký, hạn và nonce.
4. Dùng access token trong bộ nhớ gọi hai GET qua HTTPS tới acceptance đã duyệt. Dừng pilot nếu mới có HTTP thuần cho token thật. Logout xóa phiên local; đăng ký logout redirect nếu dùng logout của issuer. Không ghi token vào command line, lịch sử hay log. [Auth0 PKCE](https://auth0.com/docs/get-started/authentication-and-authorization-flow/authorization-code-flow-with-pkce)

## E. Tương thích JWT và tiêu chí nghiệm thu

Source mặc định tắt bearer. Các key dưới `ApiAuthentication:Oidc` là Enabled, Issuer, Audience, CustomerClientId. Khi bật, typed options kiểm tra issuer HTTPS root và Client ID. JwtBearer dùng discovery/JWKS tại Authority, yêu cầu RS256, at+jwt, chữ ký hợp lệ, issuer và audience khớp, exp và lifetime hợp lệ (clock skew 30 giây). Phải có đúng một client_id khớp và một sub hợp lệ. Client chỉ được cấu hình user-interactive grant.

Trình tự kiểm chứng sau phê duyệt: kiểm tra discovery/JWKS từ cả máy chạy client và mạng API; hoàn tất code exchange; kiểm tra 401 với danh tính chưa liên kết. Sau liên kết fixture đã duyệt, kỳ vọng GET /api/orders và GET chi tiết đơn của chính mình trả 200, đơn người khác 404; thiếu orders:read hoặc user inactive trả 403; token sai issuer/audience/type/hết hạn trả 401. Hai GET cùng kiểm Api.Customer và Orders.Read. Không suy quyền từ email hoặc role Auth0.

## F. Điều kiện liên kết danh tính

API chỉ khai báo IVerifiedIdentityLookup; chưa có implementation SQL đăng ký cho runtime. Đề xuất ExternalIdentities với unique (Issuer, Subject), so sánh phân biệt hoa/thường chính xác, FK UserId tới Users với NO ACTION. Nếu chỉ cho một liên kết mỗi issuer cho mỗi Customer, thêm unique (Issuer, UserId). Chốt độ dài khóa phù hợp giới hạn index SQL Server trước khi duyệt schema.

Sau JWT validation, bên xác minh đối soát iss/sub và bằng chứng Customer vừa xác thực lại tài khoản local. Chỉ người có quyền mới ghi mapping nguyên tử sau xác minh. Unique constraint xử lý cả hai yêu cầu đồng thời; từ chối khi đụng liên kết đã có. Cấm tự liên kết bằng UserId do client gửi hoặc email trùng. Chỉ liên kết Customer trong pilot. Audit sự kiện link/unlink gồm người duyệt, thời điểm UTC, UserId, mã tham chiếu và kết quả, không lưu token. Sau unlink, lookup phải từ chối từ request tiếp theo; trạng thái active và role tiếp tục đọc từ SQL. Schema, workflow và liên kết thật đều chờ phê duyệt.

## G. Docker acceptance và rollback

| Thành phần | Quan sát trực tiếp |
|---|---|
| Business | Web, API và SQL của phonestore-restore-c2 đều healthy; host có listener 14334 |
| Acceptance API | b47fefaf6c87, Exited 255, image ID bắt đầu 9b20d979aa6a |
| Acceptance Web CT02 | 84066ac4b44f, Exited 255, image ID bắt đầu aeddfa741bc0 |
| Thời điểm dừng | Cả hai kết thúc lúc 14:12:28 UTC ngày 19/11, OOMKilled=false, State.Error trống, restart policy=no. Chưa xác định nguyên nhân dừng; policy giải thích vì sao Docker không tự khởi động lại |
| Port và SQL | Chưa thấy listener 5285/5286. Cấu hình thực của cả hai container trỏ host.docker.internal,14334 / PhoneStoreDB_AcceptanceTest. Đây là đối chiếu target cấu hình, chưa thử kết nối SQL từ container đang dừng |
| OIDC và Compose | Không có key OIDC/Auth0 trong env hai container. Compose acceptance pin hai image bằng SHA-256, bind 5285/5286 vào loopback, Web mount uploads read-only; chưa cấu hình HTTPS cho bearer pilot |
| Rollback | docker image inspect xác nhận cả hai image cục bộ khớp đầy đủ với pin tương ứng. Launcher kiểm pin trước Start. Baseline API cũ có trước S34.9C; start lại cũng chưa đưa validator mới vào runtime. Cần duyệt riêng image/API acceptance và TLS; giữ pin Web CT02. Rollback dự kiến về đúng image/config API cũ hoặc tắt Enabled trên bản hỗ trợ. Chưa diễn tập |

## H–J. Kết quả, điều kiện còn thiếu và việc cần duyệt

Chỉ thay đổi tài liệu. Không chạy lại regression. Bằng chứng S34.9C là build Release sạch, 29/29 targeted và 298/298 full trên test host. Phiên này kiểm tra Git, source, cấu hình Compose, metadata container/image và listener. Chưa xác minh discovery, PKCE, callback, token, mapping hoặc API bằng tenant thật.

1. Quản trị tenant xác nhận **Domain, Client ID công khai**, API Identifier, token profile và permission đã cấu hình. Không cần cung cấp secret/token.
2. Duyệt harness Native, callback/logout sau bind thử và phương án HTTPS acceptance. Khi có domain mới đối chiếu issuer/discovery/JWKS thực tế.
3. Duyệt độc lập store mapping và quy trình liên kết một Customer fixture; duyệt image/deployment acceptance cùng rollback trước khi chạy ca 200/403/404. Không cần đụng tới Admin duy nhất hoặc dữ liệu nghiệp vụ trong bước chuẩn bị này.
