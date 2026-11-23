# S34.9C — Chuẩn bị xác thực Auth0 cho Customer Orders API

Ngày 19/11/2026. Baseline `main` tại `77fb3eb`, ahead 1 so với tracking ref cục bộ. Working tree đã có thay đổi S34.8A–9B ở API, reporting, tests và docs. SDK 10.0.403. Phạm vi nghiệm thu của giai đoạn này là source và test host cô lập.

## Cấu hình API và client thử

Mặc định `ApiAuthentication:Oidc:Enabled=false`. Cặp issuer/audience do giai đoạn trước chuẩn bị có thể tiếp tục tồn tại mà chưa kích hoạt bearer. Khi được duyệt tích hợp, cấu hình mẫu dành riêng cho **acceptance** là:

```json
{
  "ApiAuthentication": {
    "Oidc": {
      "Enabled": true,
      "Issuer": "https://<AUTH0_TENANT_DOMAIN>/",
      "Audience": "https://api.phonestore.local",
      "CustomerClientId": "<CUSTOMER_TEST_CLIENT_ID>"
    }
  }
}
```

Dùng dấu `__` thay cho `:` khi truyền biến môi trường. Thay placeholder bằng issuer khớp chính xác discovery và Client ID của ứng dụng thử. Audience là API Identifier đăng ký ở Auth0; nó không phải địa chỉ Docker để gọi HTTP. Chọn API signing algorithm RS256 và access token profile RFC 9068. Profile này phát `typ=at+jwt` và claim `client_id`. API chỉ chấp nhận client đã đăng ký. [Auth0 Access Token Profiles](https://auth0.com/docs/secure/tokens/access-tokens/access-token-profiles)

Đề xuất đăng ký **PhoneStore Customer Test Client** loại Native, public client, token endpoint authentication method None, chỉ cho phép Authorization Code + PKCE S256 trong pilot. Tắt M2M/client credentials, không cấp offline access cho lần nghiệm thu đầu. Dùng SDK OIDC chuẩn và browser hệ thống, kiểm tra state/nonce, giữ token trong bộ nhớ. OIDC scopes `openid profile email` phục vụ client đăng nhập. API permission `orders:read` phải hiện diện trong `scope` của access token để gọi hai GET. Quyền Customer/Admin do SQL quyết định. [Auth0 Authorization Code Flow with PKCE](https://auth0.com/docs/get-started/authentication-and-authorization-flow/authorization-code-flow-with-pkce)

Trong scripts/tools hiện tại chưa thấy client chạy callback PKCE. Với harness native đề xuất, bind riêng loopback, xác nhận cổng khả dụng trên máy thử và đăng ký đúng URI mà SDK phát; ví dụ `http://127.0.0.1:<PORT>/callback` và `http://127.0.0.1:<PORT>/signed-out` nếu có listener logout. Chỉ điền Allowed Callback/Logout URLs sau khi xác minh listener và chính sách tenant. Nếu chọn browser SPA, đăng ký loại SPA riêng, callback/logout HTTPS và Allowed Web Origins đúng origin thực tế. Chưa đăng ký tenant/application hoặc xác minh callback thật.

## Luồng token và quyết định HTTP

1. Khi bật, typed options kiểm tra issuer HTTPS root có dấu slash cuối, audience và Client ID. Cấu hình thiếu điều kiện khiến startup thất bại. Khi tắt, scheme PhoneStore.Api tiếp tục challenge.
2. Middleware Microsoft.AspNetCore.Authentication.JwtBearer 10.0.15 lấy metadata/JWKS qua HTTPS, kiểm tra issuer, audience chính xác, signature RS256, loại token, expiry và nbf với dung sai 30 giây. API kiểm tra client_id và subject trước khi gọi lookup. Không dùng email trong access token.
3. Chỉ khi lookup trả đúng một UserId dương mới dựng principal mới gồm NameIdentifier nội bộ và các scope đã xác minh. Provider role và các claim định danh nội bộ gửi kèm bị bỏ. Không lưu access token trong authentication properties. Route đơn hàng còn áp Api.Customer và scope orders:read; PurchaseStore lọc theo UserId của principal.

| Tình huống | Kết quả |
|---|---|
| Thiếu/sai token, sai client, ID token, thiếu sub hoặc mapping thiếu/mơ hồ | 401 ProblemDetails. Khi bật bearer có WWW-Authenticate: Bearer |
| Đã mapping nhưng inactive, sai role hoặc thiếu scope đọc đơn | 403 ProblemDetails |
| Customer đọc đơn thuộc người khác | 404 |
| Customer active, đúng mapping và scope | 200 cho danh sách và chi tiết đơn của mình |

## Mapping được xác minh thủ công

Source có Users.UserId, IsActive, RoleId và unique filtered index UX_Users_SingleAdmin. Chưa có store external identities. API chỉ định nghĩa giao diện IVerifiedIdentityLookup. Production DI chưa đăng ký implementation. Không có đường cấu hình UserId hay map từ email; ngay cả token hợp lệ cũng dừng ở 401 cho tới khi có store thật.

**Schema đề xuất, chưa áp dụng.** Bảng ExternalIdentities với Issuer, Subject, UserId, VerifiedAtUtc và thông tin audit người xác minh. FK UserId → Users.UserId với NO ACTION; unique (Issuer, Subject) dùng so sánh chính xác phân biệt hoa/thường. Chốt độ dài và kiểm tra giới hạn index SQL Server trước migration. Nếu pilot chỉ cho phép một danh tính trên mỗi tenant cho mỗi user, thêm unique (Issuer, UserId). Ghi liên kết trong một transaction và xử lý unique conflict; không ghi đè mapping cũ. Lookup đọc tối đa hai kết quả và từ chối dữ liệu mơ hồ.

**Quy trình tương lai.** Customer xác thực qua Auth0. Backend lấy iss/sub từ access token đã xác minh; nếu chưa liên kết thì business API trả 401. Customer chứng minh quyền sở hữu tài khoản PhoneStore bằng xác thực local mới trong cùng phiên liên kết giới hạn thời gian. Người được ủy quyền đối soát hai bằng chứng, chọn đúng Customer hiện hữu và ghi liên kết kèm audit. Client không tự chọn UserId. Khi gỡ liên kết, yêu cầu xác minh lại, audit và chặn quyền truy cập từ request tiếp theo. Tranh chấp liên kết phải được đối soát trước khi gán lại. Pilot chỉ liên kết Customer; cấm liên kết Admin, tạo user hoặc sửa role trong workflow này.

## Nghiệm thu, rollback và bàn giao

Test host chạy middleware thật với discovery/JWKS trong bộ nhớ, RSA key chỉ thuộc tests và dữ liệu SQLite cô lập. Phân biệt lỗi token trước lookup với lỗi mapping sau xác thực. Kiểm tra 401/403/404/200, scope, local role/active, ownership; regression Orders kiểm tra thêm pagination/filter. Các test không cần Auth0 credentials.

Release build toàn solution thành công, 0 warning/error. Lọc Auth0BearerTests, ApiAuthorizationTests và OrderApiTests: 29/29 PASS. Full PhoneStore.Tests Release: 298/298 PASS, 0 fail, 0 skip. Trong đó có 18 ca mới qua test host Auth0BearerTests. Kiểm tra whitespace bằng git diff --check.

Auth0 tenant thật **NOT TESTED**. Mapping SQL **NOT DEPLOYED**. Customer thật gọi orders **BLOCKED**. SELECT trên SQL Docker xác nhận PhoneStoreDB có 3 Users và 9 bảng gồm bảng lịch sử migration; chưa có bảng external identities. Docker báo business Web/API/SQL healthy, GET /api/health ở 5025 trả 200. Hai container acceptance Web/API ở trạng thái Exited (255), cổng 5286 chưa phản hồi. Đây là quan sát runtime hiện có; bản sửa chỉ chạy trên test host. Không restart, deploy, ghi DB hay thay đổi uploads.

**Đề xuất S34.9D.** Duyệt tenant acceptance, đăng ký Native client và xác minh vòng PKCE/callback cùng HTTPS discovery từ API. Duyệt riêng schema, store và quy trình liên kết một Customer fixture trước khi triển khai hoặc ghi mapping. Sau đó gọi GET /api/orders và GET /api/orders/{id} theo cả trường hợp sở hữu và sai chủ. Có thể tắt Enabled khi rollback cấu hình trong lần triển khai được duyệt; bên issuer thu hồi grant thử. Việc quay về scheme đóng không đòi thay đổi Users/Roles.

Commit message đề xuất: `feat(api): prepare verified Auth0 bearer authentication for customer orders`.
