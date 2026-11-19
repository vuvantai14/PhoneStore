# S34.3B — Chuẩn bị tích hợp xác thực REST client

## Baseline

Ngày 2026-11-19, main tại 3660ef4. S34.3A còn unstaged. Source API, cấu hình ứng dụng và tên biến môi trường của hai API Docker được kiểm tra không cho thấy issuer OIDC đã cấu hình. Năm container healthy ở preflight. Phạm vi thay đổi gồm Program.cs, Authorization/ApiAuthenticationRegistration.cs, ApiAuthorizationTests.cs, báo cáo này và PROGRESS.md.

## Client và cơ chế xác thực

| Client | Hiện tại | Khi cần gọi API có phân quyền |
|---|---|---|
| Customer Web | Cookie MVC, session CT02, Catalog công khai | Nếu cần đăng nhập liên kết, dùng OIDC authorization code ở server, giữ cookie cho browser và giữ access token phía server. |
| Admin WinForms | AdminSession và truy cập Business/Data | Nếu chuyển sang API, đăng ký native client, mở browser hệ thống, dùng authorization code + PKCE. |
| Mobile hoặc external | Chưa xác định client triển khai | Mobile tương tác dùng code + PKCE, lấy access token có audience của API. Client máy-máy cần xác định quyền riêng trước khi cân nhắc client credentials. |

Cơ sở thiết kế: [OpenID Connect Core](https://openid.net/specs/openid-connect-core-1_0.html), [RFC 8252 cho ứng dụng native](https://www.rfc-editor.org/rfc/rfc8252).

## Cấu hình và ranh giới tin cậy

ApiAuthentication:Oidc:Issuer và ApiAuthentication:Oidc:Audience là hai typed options tùy chọn theo cặp. Có thể bỏ cả hai. Khi khai báo, issuer phải là HTTPS tuyệt đối, không chứa thông tin đăng nhập, query hoặc fragment. Audience phải có giá trị. Binder từ chối khóa lạ trong section; validation chạy khi khởi động. Hai tham số hợp lệ chỉ mô tả provider dự kiến. API không truy vấn discovery hoặc kích hoạt bearer. Scheme và các policy S34.3A vẫn áp dụng.

Model User hiện có UserId/RoleId/IsActive và chưa có liên kết danh tính OIDC. Trước khi nhận token thật cần chọn issuer, audience, đăng ký client và phê duyệt nguồn ánh xạ duy nhất (iss, sub) → UserId. Không suy UserId từ email hay sub dạng số. Sau xác minh chữ ký, issuer, audience, lifetime và ánh xạ, mới chuyển UserId cho policy kiểm tra Users/Roles. Nếu cần lưu mapping bằng schema mới, phải phê duyệt riêng.

## Bằng chứng và giới hạn

- Test HTTP xác nhận 401 ProblemDetails khi thiếu token, token malformed và JWT fixture có hạn đã hết, sai issuer, sai audience hoặc có issuer/audience khớp cấu hình dự kiến. Khóa fixture sinh trong bộ nhớ của test. Các ca này chứng minh scheme hiện tại đóng; validation chữ ký/lifetime bởi provider và ánh xạ OIDC thành công còn chờ tích hợp thực.
- Test danh tính nội bộ kiểm tra 403 ProblemDetails khi sai role hoặc tài khoản bị disable giữa các request. Sáu cấu hình issuer/audience không hợp lệ khiến startup thất bại. Các controller probe chỉ thuộc test host.
- Release build solution 0 warning/error; targeted ApiAuthorizationTests 9/9 PASS.
- Regression Release: 272/272 PASS, 0 skipped. Trước/sau: cùng 5 container healthy, PhoneStoreDB ONLINE, Users/Roles/Products/Orders = 3/2/40/15 qua SELECT. Không deploy hoặc ghi database nghiệp vụ.
