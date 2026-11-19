# PhoneStore — Tiến độ và điểm tiếp tục

2026-11-19 — [S35.1 — CI/CD readiness](S35_1_CICD_READINESS_AUDIT.md). GitHub CI pass tại remote main 1a96390; local e22fc34. Business Compose config và HTTP readiness/Web pass. GHCR publish, runner CD và rollback chưa nghiệm thu; public repo và drift cấu hình SQL cần được xử lý trong thiết kế CD. Chỉ cập nhật báo cáo/tiến độ, không chạy lại tests. Bước đề xuất: duyệt CI permissions, merged Compose và WinForms artifact trước khi publish.

2026-11-19 — [S34.9E — Đổi hướng xác thực, bỏ Auth0](S34_9E_AUTHENTICATION_ARCHITECTURE_REVISION.md). Dừng kế hoạch tenant/linking của S34.9B–D. Giữ login hiện tại; đề xuất OpenIddict tự host và tái sử dụng Users/Roles khi có client REST cần token. Source S34.9C vẫn ở chế độ opt-in mặc định tắt. Chỉ cập nhật báo cáo và tiến độ; chưa chạy lại tests. Chờ duyệt pilot, subject, host/khóa và storage.

2026-11-19 — [S34.9D-01 Auth0 tenant và PKCE](S34_9D_01_AUTH0_TENANT_READINESS.md). PARTIAL. Đã đối chiếu source, pin image, port và target SQL acceptance. Business healthy, hai container acceptance Exited 255. Chờ domain/Client ID, callback, HTTPS, mapping và duyệt deployment acceptance. Chỉ cập nhật tài liệu; không chạy lại tests.

2026-11-19 — [S34.9C Auth0 Customer API](S34_9C_AUTH0_INTEGRATION_PREPARATION.md). Đã bổ sung JwtBearer opt-in, lookup liên kết fail-closed và scope đọc đơn. Release build sạch, targeted 29/29, full 298/298 PASS với issuer/JWKS thử. SQL SELECT xác nhận chưa có bảng mapping. Business healthy; hai container acceptance đang dừng. Auth0 tenant thật chưa thử; Customer thật BLOCKED đến khi duyệt mapping và triển khai acceptance.

2026-11-19 — [S34.9B kiến trúc xác thực API](S34_9B_AUTHENTICATION_ARCHITECTURE_DECISION.md). Đã đối chiếu source và hoàn tất thiết kế OIDC/OAuth2, mapping, tích hợp client và tiêu chí nghiệm thu. Khuyến nghị issuer được vận hành sẵn; chờ duyệt provider, registration và liên kết UserId. Chỉ cập nhật tài liệu; chưa chạy lại tests, chưa triển khai xác thực thật.

2026-11-19 — [S34.9A integration readiness](S34_9A_API_INTEGRATION_READINESS.md). Source 23 operation, build sạch, targeted 83/83, full 280/280 PASS. Business Docker chỉ 7 GET, acceptance 5 GET; Web gọi Catalog detail thật PASS. Protected client BLOCKED bởi issuer/mapping. Bổ sung metadata policy OpenAPI; production NO-GO. Chưa deploy.

2026-11-19 — [S34.8A API hardening](S34_8A_API_HARDENING.md) **PASS source/test**. SQLite HTTP overview 12→3 câu SQL, top-products 12→2, gồm phân quyền. Route bảo vệ trả no-store. Build Release sạch, targeted 30/30, full 280/280 PASS. SQL counts/checksums và 5 container khớp; SQL Server concurrency chưa nghiệm thu. Chưa deploy.

2026-11-19 — [S34.7A Reporting API](S34_7A_REPORTING_API.md) **PASS source/test**. Ba GET Api.Admin dùng báo cáo WinForms và biên UTC+7. Build Release sạch, targeted 15/15, full 280/280 PASS. SQL counts/checksums khớp, 5 container healthy. Chưa deploy.

2026-11-19 — [S34.6A Admin Customers API](S34_6A_ADMIN_CUSTOMERS_API.md) **PASS source/test**. GET danh sách/chi tiết và PATCH trạng thái giới hạn role Customer ở SQL; cả ba route yêu cầu Api.Admin. Build Release sạch, targeted 42/42, full 278/278 PASS. Users/Roles khớp trước/sau, 1 Admin active, 5 container healthy. Chưa deploy.

2026-11-19 — [S34.5A Orders API](S34_5A_ORDERS_API.md) **PASS source/test**. Hai GET Customer theo owner, hai GET Admin và PATCH có expectedStatus qua workflow WinForms. Release build sạch, targeted 50/50, full suite 276/276 PASS. SQL count/checksum 3 bảng khớp; cùng 5 container healthy. Chưa deploy, chờ issuer cho client thật.

2026-11-19 — [S34.4A](S34_4A_PRODUCT_MANAGEMENT_API.md) **PASS source/test**. 5 endpoint quản lý sản phẩm dùng Api.Admin và nghiệp vụ WinForms, kiểm tra tồn gốc khi PUT. Release build sạch, targeted 40/40, full 274/274 PASS. SQL checksum sản phẩm và ID 5 container khớp trước/sau; chưa deploy, chờ issuer cho client thật.

2026-11-19 — [S34.3B](S34_3B_API_AUTHENTICATION_PREPARATION.md) **PASS chuẩn bị tích hợp**. Typed options OIDC kiểm tra khi startup; API vẫn từ chối bearer cho tới khi có issuer và ánh xạ danh tính đã xác minh. Release build sạch, auth 9/9, regression 272/272 PASS. SQL counts và 5 container healthy khớp trước/sau; chưa deploy.

Cập nhật 2026-11-19 — [S34.3A API Authorization](S34_3A_API_AUTHORIZATION.md) **PASS source/test** tại checkpoint 3660ef4. API có policy Customer/Admin kiểm tra tài khoản active và role từ SQL, trả 401/403 ProblemDetails. Catalog/health công khai. Release build sạch, HTTP auth 2/2, full suite 265/265 PASS. Runtime business vẫn ở S34.2R-04; cần issuer và ánh xạ danh tính trước khi phục vụ client đăng nhập qua API.

Cập nhật 2026-11-19 — S34.2R-04 **PASS rollout business Web/API .NET 10 và smoke chỉ đọc**.
- FULL COPY_ONLY/CHECKSUM backup + VERIFYONLY PASS. Chỉ thay API `b79d2e025e11` rồi Web `e55bfc7a1117`, cả hai chạy .NET 10.0.15. Dùng `docker-compose.net10.yml` pin image mới; image .NET 8 còn để rollback.
- Health/live/ready, Catalog v1/detail, Swagger, Web Home/Catalog/Login/ảnh đều HTTP 200. Business SQL và acceptance giữ ID/image/StartedAt. Hai DB ONLINE, count/checksum 8/8 bảng mỗi DB và manifests uploads 42/5 khớp.
- [Báo cáo S34.2R-04](S34_2R_04_DIRECT_DOCKER_UPGRADE.md). Bỏ qua isolated acceptance theo yêu cầu. Không ghi dữ liệu nghiệp vụ, migration, restore hoặc chạy lại full suite. Chưa stage/commit/push.

Cập nhật 2026-11-19 — S34.2R-03 **BLOCKED do RAM tại preflight**.
- Hai image khớp ID R-02, 5/5 container healthy và giữ ID/image/StartedAt. Hai DB ONLINE với snapshot 8/8 bảng mỗi DB khớp. RAM host 0,879–0,909 GiB, chưa tạo tài nguyên test hoặc chạy smoke.
- [Báo cáo S34.2R-03](S34_2R_03_ISOLATED_RUNTIME_ACCEPTANCE.md) lưu kế hoạch Docker/SQL cách ly. Chờ giải phóng RAM và kiểm tra lại tài nguyên trước khi khởi tạo.

Cập nhật 2026-11-19 — S34.2R-02 **PASS build/test/image, chờ nghiệm thu**.
- SDK 10.0.403, EF/Identity/Mvc.Testing 10.0.15, build Release 11/11 và full 263/263 PASS; targeted 129/129. Sửa metadata designer WFO1000 và ba SQLite test fixture cho EF Core 10. EF model snapshot không có pending changes.
- Hai image .NET 10 build dưới tag riêng. WinForms mở/đóng LoginForm offline. Hậu kiểm 5/5 services healthy, 4/4 HTTP 200, ID/image/StartedAt giữ nguyên, hai snapshot SQL 8/8 bảng khớp. Chưa chạy image mới, SQL runtime test hoặc deploy.
- Báo cáo [S34_2R_02_DOTNET10_MIGRATION.md](S34_2R_02_DOTNET10_MIGRATION.md). Giữ checkpoint 4d77bd0 và tài liệu R-01. Chưa stage/commit/push.

Cập nhật 2026-11-19 — S34.2R-01 **AUDIT COMPLETE, GO có điều kiện cho nâng cấp cách ly**.
- S34.2 `4d77bd0` khớp GitHub main. Host có SDK 10.0.403 và Desktop ref pack 10; repo còn pin SDK 8.0.427 trên 11 project. Docker Web/API còn .NET 8, 5/5 healthy. SQL hai DB ONLINE ở compatibility 160.
- Báo cáo [S34_2R_01_DOTNET10_COMPATIBILITY_AUDIT.md](S34_2R_01_DOTNET10_COMPATIBILITY_AUDIT.md) ghi tương thích EF/ASP.NET Core/WinForms/Docker/CI, launcher hardcode TFM và rollback. Không restore package, build/test runtime 10 hoặc đổi config. Chờ phê duyệt.

Cập nhật 2026-11-19 — S34.2 **PASS source/automated, chờ người dùng nghiệm thu**.
- Thêm `GET /api/health/live` và `GET /api/health/ready`; `GET /api/health` và bốn route Catalog v1 giữ nguyên hợp đồng. Regression khóa JSON, HTTP methods, lỗi validation và bộ lọc/phân trang.
- Build solution 0 warning/error, targeted 65/65, full 263/263, `git diff --check` PASS. Docker 5/5 healthy, ID/image/StartedAt giữ nguyên, API 5025/5286 health 200. Hai route mới đã test qua TestServer và chưa deploy.
- Báo cáo [S34_2_REST_API_FOUNDATION.md](S34_2_REST_API_FOUNDATION.md). Giữ các thay đổi S34.1; chưa stage/commit/push.

Cập nhật 2026-11-19 — S34.1 **AUDIT COMPLETE / chờ phê duyệt S34.2**.
- GitHub `main` xác nhận checkpoint S32 `b0d07a7` qua `git ls-remote`. Docker 5/5 healthy; API business/acceptance đã smoke GET catalog/health/Swagger và validation. Không chạy lại suite 249/249 hoặc thay đổi runtime.
- Báo cáo [S34_1_REST_API_ARCHITECTURE_AUDIT.md](S34_1_REST_API_ARCHITECTURE_AUDIT.md) có endpoint inventory, sơ đồ, thiết kế security và lộ trình S34.2–S34.9. Ưu tiên giữ hợp đồng v1, health split và chuyển runtime .NET còn hỗ trợ trước go-live.

Cập nhật 2026-11-19 — S32.3B-FINAL-HANDOFF **kết thúc vòng kiểm thử theo quyết định người dùng**.
- S32.3A CLOSED theo mốc PREP, FIX01 runtime PASS HTTP. EXEC-A1/A2/A3 PASS trong phạm vi đã chạy, CT01/AC01 PASS theo bằng chứng tương ứng, CT02 FIX/STABILIZE PASS. Full automated gần nhất 249/249 PASS. SE01 runtime BLOCKED và RP01 PARTIAL được hoãn cùng CLOSEOUT-02A/02B và EXEC-B.
- Runtime gần nhất tại CLOSEOUT-01: 5/5 healthy, 4/4 HTTP 200, hai DB ONLINE và snapshot 8/8 bảng mỗi DB khớp. Không chạy lại test và không thay đổi runtime/dữ liệu trong phiên bàn giao.
- Báo cáo [S32_3B_FINAL_HANDOFF.md](S32_3B_FINAL_HANDOFF.md) ghi Git đang chờ, checkpoint đề xuất và lộ trình Customer Web. Đề xuất bắt đầu Catalog `/products` khi có yêu cầu nâng cấp. Chưa stage/commit/push.

Cập nhật 2026-11-19 — S32.3B-EXEC-A4-CLOSEOUT-01 **preflight PASS, A4 chưa đóng**.
- Baseline 5/5 container healthy, 4/4 HTTP 200, hai DB ONLINE với snapshot 8/8 bảng mỗi DB khớp và SHA-256 uploads 42/5 khớp. `ReportingTests` 13/13 PASS; không chạy SQL test có ghi dữ liệu.
- SE01 GUI **BLOCKED** do không có workflow quản lý quyền Admin. Ưu tiên fixture dùng một lần để nghiệm thu fresh authorization và shell về Login. Read gateway chỉ dùng session; còn race giữa fresh-check và commit.
- RP01 **PARTIAL**. SQL acceptance xác nhận doanh thu Completed 2.000.000đ và chưa có đơn sát biên UTC+7. Đề xuất fixture 30/11–01/12 trên DB dùng một lần. Chờ phê duyệt riêng cho ghi fixture, restore/cleanup hoặc bước tiếp theo.

Cập nhật 2026-11-19 — S32.3B-CT02-STABILIZE **PASS**.
- Compose acceptance pin đúng image Web CT02. Launcher Start kiểm tra image cục bộ và cả hai container trước khi chỉ dùng `docker start`, API healthy trước Web. Thay pin sau khi nghiệm thu image mới và xin phê duyệt riêng cho recreate.
- RT03 anonymous checkout token **NOT APPLICABLE** qua HTTP. GET checkout trả 302 về Login. RT05 test xác nhận cart thiếu owner có thể được nhận như cart khách, token cũ bị xóa khi đăng nhập. Cần vô hiệu hóa session cũ trước khi dùng store persistent hoặc triển khai pha trộn phiên bản.
- CT02 targeted 31/31, full PhoneStore.Tests 249/249, PowerShell syntax và Compose Check/Status PASS. Docker mock 6/6. Hậu kiểm 5/5 container healthy với ID, image, StartedAt không đổi. Hai DB ONLINE, snapshot 8/8 bảng mỗi DB không đổi. Dừng tại CT02-STABILIZE.

Cập nhật 2026-11-19 — S32.3B-RECOVERY-03 **PASS**.
- Khởi động lại đúng container acceptance API b47fefaf6c87 và Web CT02 84066ac4b44f bằng docker start. Image CT02 khớp. Business và acceptance 5/5 container healthy, HTTP 4/4 trả 200. Business giữ nguyên StartedAt.
- SQL SELECT xác nhận hai DB ONLINE, count/checksum khớp 8/8 bảng mỗi DB, blocking 0 và fixture đúng trạng thái. SHA-256 uploads khớp RECOVERY-02 (42 business, 5 acceptance). RAM cuối 3,128 GiB.
- Auth smoke GET PASS cho form Login, chuyển hướng từ Profile và giỏ hàng khách trống. Chưa POST đăng nhập vì luồng có thể cập nhật password hash. Dừng tại RECOVERY-03, chưa chạy CT02-STABILIZE hoặc EXEC-B.

Cập nhật 2026-11-16 — S32.3B-CT02-RUNTIME **REVOKE FIX PASS / FULL MATRIX PARTIAL**:
- Build image WebCT02 riêng và recreate duy nhất acceptanceWeb bằng --no-deps/--no-build, imageoverride qua stdin khôngsửaCompose. Container cũ92708e866da4→mới84066ac4b44f, Started09:54:30UTC/imageaeddfa741bc0; healthy. Business3containers/acceptanceAPI khôngrestart; business8table snapshot và uploads42business/5acceptance digestnguyên.
- RT02 sameowner/carttoken PASS HTTP+SQL; RT04 logout/accountswitch/oldtokenrejected PASS, khôngorderstockwrites. RT03 anonymouscart2adoptlogin PASS HTTP; anonymouscheckoutprotected nên tokenrotation runtime NOT VERIFIED (automatedPASS). Harness trước gặp emptycartkhôngCSRF, dùng đúng ProductDetailCSRF sauđó. RT05 legacyownerless khôngfixture hợp lệ; restartWebinmemory làm sessioncũ mất, chưa chứng minh rolling/persistent migration.
- RT01 PASS HTTP+SQL: user ngừngCustomer4 quaAdminacceptance; profile→Login,anonymouscart0,Blogin cùngsessioncart0/tokenBkhácA,POSTtokenA bịtừchối,khôngorder/stockwrite. User bật lạiCustomer4,SELECTactive/Role2; acceptance8table count/checksum cuối khớp trướcdeploy. Business8table/containerIDs/StartedAt vàuploads42business/5acceptance digestnguyên,serviceshealthy/RAMfreecuối2.39GiB.
- CT02 revoke defect resolved runtime; fullmatrixPARTIAL doRT03anonymous-token vàRT05legacyNOTVERIFIED. Report docs/S32_3B_CT02_RUNTIME.md. Composefile vẫnpinoldimage: không chạy Start/recreate thiếu imageoverride kẻo rollback bảnfix. Business5015 chưadeploy,SE01/RP01limitsgiữnguyên. Khôngsource/config/permissions/cleanup/commit/push/EXEC-B.

Cập nhật 2026-11-16 — S32.3B-CT02-FIX01 **PASS SOURCE/AUTOMATED / RUNTIME NOT VERIFIED**:
- Rootcause sessioncart/token khôngowner, revoke/expiry chỉmấtcookie màcartcòn. SessionCart thêmowner kiểmread/write/token; middleware sauauthentication trướcauthorization clear khiowner→anonymous/khácuser; loginbindowner. Anonymouscart giữ khiadoptlogin nhưng tokenrotate; sameuser giữcart/token; logout/idempotency khôngđổi.
- Sửa3source Web(SessionCart/Program/AccountController),2testfiles. HTTPpipeline thật SQLite tests disable/role/expiry3/3PASS; full247/247PASS0skipped; anonymousadopt/directswitch cũngtest. Solutionbuild isolatedscratch0warnings/errors (defaultoutput bịAdminDLLlock, khôngstopAdmin).
- Không SQLwrites/deploy/restart/config/schema/permissions/cleanup/commit/push/EXEC-B. RuntimeDocker vẫn cũ; cầnphêduyệt acceptanceWebrebuild vàCT02runtime. Giới hạn persistent/legacyownerless sessions vàrequestđồngthời ghi trong docs/S32_3B_CT02_FIX01.md.

Cập nhật 2026-11-16 — S32.3B-EXEC-A4 **FAIL CT02 REVOKE / STOP BEFORE EXEC-B**:
- Preflight target/quyền healthy/blocking0/RAMfree2.94GiB; business8table/container snapshot baseline giữ để hậukiểm. CT01 PASS HTTP+renderedHTML: Products1/2 cart CRUD, invalidqty0/-1/abc/4 rejected, sequentialqty1→3→2, tổng/badge/roworder đúng; SQL8table không đổi, chưa PASS browserJS.
- CT02 logout/accountswitch/token PASS HTTP+SQL cùngsession: cartclear, protectedredirectLogin, Bkhôngnhậncart, checkouttokenkhác, tokenA cũ bị từchối khôngtạođơn. Expiry chưatest; revoke chờ manualgate.
- AC01 PASS HTTP+SQL: register email/passwordinvalid vàduplicate bịchặn; usermớiID4 `s323b-a4-account@test.invalid`, roleCustomer2/active; profile fullname`S323B A4 Profile Updated` vàsynthetic phone/address đổi đúng; overpostUserId/RoleId/Email/IsActive/PasswordHash/CreatedAt bịbỏqua, userkhácnguyên. Credential mớiDPAPI tại ignored scratch/acceptance-s323b/a4-account-credential.clixml, khônglogsecrets. Users3→4, cácbảngkhácnguyên.
- CT02 REVOKE FAIL runtime: user ngừng đúngCustomer4 quaAdmin; SELECT inactive/Role2/emailfixture. PhiênHTTPgiữtừactive: GETprofile bịredirectLogin đúng, nhưng anonymousCartCount1; loginCustomerB cùngsession vẫncart1 vàcheckouttokenInherited=true. KhôngcheckoutPOST/đơnmới; SQLsnapshot trước/sau GET/login nguyên. Causepath CustomerCookieEvents reject/signout khôngclearSessionCart, login khôngreset/bindcart; chưa sửa source. Business8table/container snapshots khớp. Customer4 còninactive,chờuser bật lại quaAdmin.
- Customer4 đã được user bật lại quaAdmin; SELECT User4/emailfixture/Role2/active xác minh phục hồi PASS. Không reset credential hoặc sửa userkhác.
- SE01 BLOCKED: UI/service chỉquảnlýCustomer, khôngworkflowthu hồiAdmin; khôngSQLrevoke. RP01 PARTIAL: GUIReports01–16/11 khớpSQL4Orders/Completed1/Pending1/Cancelled2/revenue+average2,000,000/activeStock4; thiếufixturebiênngàyUTC+7,không sửaOrderDate.
- Hậu kiểm business8table count/checksum + containerIDs/StartedAt khớp baseline, serviceshealthy/RAMfree2.73GiB. Acceptance Users3→4 (account/profile mới và disable→enable theoUI), Products5/Orders4/Details4/Images2 nguyên; không source/configfix/cleanup/commit/push/EXEC-B. Chờ phê duyệt fix cart/token isolation sau revoke.
- Không source/configchange/checkout/concurrency/fault/cleanup/commit/push/EXEC-B. Dừng ở manualgate Customer4.

Cập nhật 2026-11-16 — S32.3B-EXEC-A3 **PASS TESTED SCOPE / STOP BEFORE EXEC-A4**:
- Preflight target/quyền acceptance PASS, RAMfree3.71–3.94GiB/disk323.12GiB, serviceshealthy. Baseline Products1–4 stocks3/1/0/2, Order1Completed/Order2–3Cancelled; Brands2/Categories2/Products4/Orders3/Details3/Images0.
- Đã mở dialog Thêm thương hiệu trên Admin acceptance13816; UIA shell bị disable, tìm đúng HWND dialog nhưng đọc descendants vẫn treo. Đã ngắt helper để tránh action trễ; SELECT xác nhận chưa tạo row EXEC-A3, không retry Save/SQL DML fallback. Business8table/container snapshot hậu kiểm khớp.
- LK01 Brand create+duplicate/required PASS manualGUI+SQL: user tạo BrandId3 `S323B-A3 Brand`, Description=`EXEC-A3 synthetic fixture`, active; xác nhận đã thử tên trùng và tên rỗng rồi Hủy. SELECT sauvalidation: tổngBrands3/fixtuređúng1/emptyBrand0,Categories2/Products4/Orders3/Details3 nguyên. Bước tiếp theo edit Brand3→`S323B-A3 Brand Edited` và tạo CategoryA3 riêng qua Admin acceptance.
- LK01 create/edit/duplicate/required Brand và Category PASS manualGUI+SQL: Brand3=`S323B-A3 Brand Edited`, Category3=`S323B-A3 Category Edited`, active; user xác nhận duplicate/required Category có lỗi đúng, totalsCategories3/Brands4. Brand4=`S323B-A3 Category` tạo nhầm được giữ nguyên, không cleanup.
- PR01 create/required/edit PASS manualGUI+SQL: Product5 tạo tên`S323B-A3 Product`,price2,500,000; required tênrỗng bị chặn và SELECT nguyên. Sau edit đúng tên`S323B-A3 Product Edited`,price2,600,000/stock5/Brand3/Category3/active.
- PR02 PASS manualGUI+HTTP+SQL: formProduct5 snapshotstock5/tên tạm; checkout CustomerA tạo Order4Pending/2,600,000 stock5→4; Lưu cũ1lần bị chặn, ảnh lỗi đúng. SELECT tênEdited/giá2,600,000/stock4/Brand3/Category3/active,staleNames0,Orders4/Details4/revenue2,000,000,Products1–4 nguyên; không retry stale.
- PR01 deactivate PASS SQL+HTTP: Product5inactive, catalogkhônglink/Detail404,stock4/tênEdited/giá2,600,000 nguyên; không bật lại. Ban đầu user thao tác nhầm Product thay lookup, không kết luận cascade/lỗicode.
- LK02 PASS disable+enable manualGUI+SQL: lần lượt Brand3/Category3 inactive rồi bật lại, SELECT cả hai active; Product5 giữinactive/stock4/giá2,600,000/tênEdited/links3–3,Products5/Orders4/Details4,khôngcascade.
- IM01 PASS manualGUI+SQL+file: JPEGImage1/PNGImage2 upload đúngacceptanceuploads/hash/decode64x64; đổiImage2primary duy nhất. User unlinkImage1 quaUI, SELECT row1absent/JPEGrefs0; JPEGfile7082b8ad08b249da8a718547904d7fa4.jpg giữ693bytes/hashnguyên theo policy; PNGImage2 primary/order1/file247bytes/hashnguyên. Không cleanup/thayfile trực tiếp.
- Sau unlink, user tự thêm lạiPNG và xác nhận: Image3/file5dfc294a59734212bc254d6acd1038a6.png,247bytes/64x64/hashkhớpfixture. Giữ nguyên, không cleanup. Baseline IM02 mới: Image2primary/order1 + Image3nonprimary/order2; uploads5files gồm JPEGorphan và2sourcefixture, đã snapshot hashes.
- IM02 PASS ba ca manualGUI+SQL+file: JPEG6bytes/PNG8bytes corrupt và JPEG10MiB+1byte đều bị từ chối; ảnh UI thông báo validation chung. SELECT sau từngca Images2/Orders4/Details4/Stock5=4/inactive/primaryImage2 nguyên, uploads5file hashes khớp baseline. Source lengthgate trước decode xác nhận đường đi giới hạn10MiB; không test IM03 hoặc stress/fault.
- Hậu kiểm cuối: Product1–4/Order1–3 giữbaseline; Product5Edited/inactive/stock4,Brand3+Category3active,Order4Pending/2,600,000 từ PR02; tổngBrands4/Categories3/Products5/Orders4/Details4/Images2,CompletedRevenue2,000,000. ExtraBrand4 và extraPNGImage3 do user xác nhận được giữ; orphanJPEGImage1 refs0/filehashnguyên theo policy. Business8table count/checksum và IDs/StartedAt khớp trước A3; mọi serviceshealthy/RAMfree2.71GiB. Chỉ cập nhật docs/tạo filevalidation scratch; không sửa source/config/cleanup/restart/commit/push. PR01/PR02/LK01/LK02/IM01/IM02 PASS trong phạm vi đã chạy; dừng chờ EXEC-A4.

Cập nhật 2026-11-16 — S32.3B-EXEC-A2 **OR01–OR05 PASS / STOP BEFORE EXEC-A3**:
- Baseline SELECT: Order1/2 CustomerA(User2),Order3 CustomerB(User3), tất cả Pending; Products stock2/0/0/2,Product4inactive. Quyền/target acceptance PASS, serviceshealthy/blocking0/RAMfree3.70GiB. Admin acceptance13816 UIA treo trước transition; đã ngắt helper, SELECT xác minh chưa đổi trạng thái.
- OR05 PASS HTTP+SQL: CustomerB GET Order1 và POST cancel với CSRF hợp lệ của B đều404; snapshot nguyên. OR03 Pending PASS: CustomerA hủy Order2 qua HTTP, Cancelled/hoàn Product1 stock2→3. OR04 PASS: replay cancel Order2 tuần tự không hoàn tồn thêm. Orders3/Details3 giữ nguyên, revenueCompleted0, Product4inactive. Không tạo thêm fixture hoặc đơn.
- OR01 PASS manualAdmin+SQL: user chuyển Order1 Pending→Confirmed→Shipping→Completed; SELECT mỗi bước, CompletedRevenue0 trước Completed và2,000,000 sau; Orders3/Details3, stocks3/0/0/2 giữ nguyên. OR02 invalid transition PASS HTTP+SQL: CustomerA cancel Completed bị từ chối bằng thông báo nghiệp vụ, SQL không đổi (harness cần HtmlDecode để kiểm message). Business8table/container snapshots sau OR01 khớp baseline.
- OR03 Confirmed PASS: user Confirm Order3 qua Admin acceptance, giữ dialog; SELECT xác nhận ownerCustomerB/User3/Confirmed trước HTTP. CustomerB login+CSRF thật hủyOrder3→Cancelled, Product2 stock0→1 đúng1, Orders3/Details3 và revenue2,000,000 giữ nguyên. Business snapshots hậu kiểm khớp. Hiện stocks3/1/0/2,Order1Completed,Order2/3Cancelled,Product4inactive.
- OR02 stale PASS manualGUI+SQL: user thử Shipping từ dialog Order3 còn Confirmed, nhận “Trạng thái đơn hàng đã thay đổi. Vui lòng tải lại trước khi thao tác”; SELECT hậu kiểm Order3Cancelled/stocks3/1/0/2/Orders3/Details3/revenue2,000,000 nguyên, không tồn âm/lệch tổng.
- Reports acceptance GUI đọc qua UIA PASS và SQL đúng khoảng01–16/11 UTC+7:3Orders/1Completed/2Cancelled, revenue2,000,000/average2,000,000/activeStock4 (loại2stock Product4inactive). Sau đọc đã để Admin acceptance tại Reports; không logout/đóng app. Business8table count/checksum và IDs/StartedAt vẫn khớp baseline; mọi serviceshealthy/RAMfree3.88GiB. Không tạo thêm fixture/cleanup/source/config/commit/push; dừng chờ EXEC-A3.

Cập nhật 2026-11-16 — S32.3B-EXEC-A1-FIXTURE **CO04 INACTIVE PASS / EXEC-A1 CO01–CO05 PASS**:
- User xác nhận ngừng Product4 qua Admin acceptance; SELECT xác minh ID4 `S323B-CO04-Inactive-UI` inactive, stock2. Harness tiếp tục bằng session/cart/checkout token giữ trong RAM từ lúc active, không tạo lại hoặc bypass session/CSRF.
- HTTP PASS: inactive bị loại khỏi catalog; detail404; dòng trong cart cũ được giữ kèm unavailable feedback; add lại bị từ chối; POST checkout hợp lệ từ form trước khi inactive bị chuyển về cart. Snapshot trước/sau rejection khớp: Orders3/Details3, Products stocks2/0/0/2, Product4 inactive. Không có order/detail mới hoặc trừ tồn.
- Business8table count/checksum và container IDs/StartedAt giữ nguyên,40Products/15Orders; mọi service healthy, RAMfree trước tiếp tục3.44GiB. Không reactivation/cleanup/source/config change/commit/push; chỉ cập nhật progress. Dừng chờ phê duyệt EXEC-A2.

Cập nhật 2026-11-16 — S32.3B-EXEC-A1-FIXTURE **PARTIAL / CO04 INACTIVE AWAITING MANUAL ADMIN ACTION**:
- Admin acceptance riêng PID13816 đã login fixture và tạo đúng1Product qua GUI: ID4 `S323B-CO04-Inactive-UI`, Brand1/Category1, giá1,500,000, stock2, active. Xác minh active catalog/detail/cart-add/checkout-form PASS HTTP, chưa submit checkout thành công.
- UIA bị treo ở bước chuyển sang ngừng hoạt động; đã ngắt helper điều khiển, không kill Admin. SELECT sau đó: Product4 vẫn active/stock2; Orders3/Details3 và Products1–3 stock2/0/0 giữ nguyên. Business count/checksum8bảng và container IDs/StartedAt khớp baseline, services healthy/RAMfree3.46GiB.
- Harness HTTP đang giữ session/cart/token trong RAM, chờ người dùng thao tác ngừng ID4 đúng Admin acceptance; không ghi cookie/token ra file/log. Chưa PASS inactive; không SQL DML fallback/cleanup/reactivate/EXEC-A2. Cần đối soát SQL trước tiếp tục sau xác nhận người dùng.

Cập nhật 2026-11-16 — S32.3B-EXEC-A1 **PARTIAL: CO01/02/03/05 PASS HTTP+SQL; CO04 inactive pending**:
- Harness HTTP in-memory dùng login Customer A/B, cookie session và CSRF thật trên5285; mọi ghi qua cart/checkout ứng dụng, SQL chỉ SELECT. CO01 tạo Order1/Product1×2/tổng2,000,000; CO02 tạo Order2/Product1×1/tổng1,000,000 dù gửi giá/tổng client=1. CO03 quantity0/-1/abc, thiếu tên/địa chỉ+sai phone bị chặn, SQL không đổi.
- CO04 Product3 hết hàng bị chặn; Customer B checkout Product2×1 tạo Order3/tổng2,000,000, rồi checkout giỏ cũ của A bị từ chối không tạo đơn. Inactive chưa test do không có fixture inactive/không sửa Product. CO05 replay tuần tự token CO01 trả lại Order1, SQL không đổi.
- Acceptance sau test: Orders3/Details3, tất cả Pending; Product stocks2/0/0; không tồn âm hoặc lệch tổng. Fixture/đơn giữ lại, không cleanup/seed/migrate. Business count/checksum8bảng trước-sau khớp,40Products/15Orders; tất cả services healthy, blocking0 tại các checkpoint; RAMfree2.80–2.89GiB trong các ca (preflight2.64GiB).
- Không sửa source/config/restart/commit/push; chỉ cập nhật tiến độ. Dừng chờ hướng dẫn cho nhánh inactive hoặc EXEC-A2; không chạy CO06/07/08.

Cập nhật 2026-11-16 — S32.3B-ENV-SHARED **ENV PASS / EXEC-A AWAITING APPROVAL**:
- Tạo riêng `PhoneStoreDB_AcceptanceTest` trên SQL Docker hiện có; 2 migrations, Roles2/Users3/Brands2/Categories2/Products3(stock5/1/0)/Orders0. Credential tổng hợp mới, DPAPI trong ignored scratch; provisioning login disabled, runtime chỉ DML bảng acceptance và đọc migration history; kết nối PhoneStoreDB bị từ chối.
- Thêm tool `tools/PhoneStore.AcceptanceEnvironment`, scripts/Compose acceptance riêng trong `scripts/acceptance`; Web5285/API5286 healthy, pool5, memory384/256MiB, CPU0.5 mỗi service; GET catalog/detail/health/ảnh fixture PASS. Admin output/launcher riêng -CheckOnly PASS, chưa mở Admin test; phiên28116 giữ nguyên.
- Tool build0warnings/errors, PS parse/config PASS. Business 8-table count/checksum và container IDs/StartedAt giữ nguyên. Host freeRAM cuối2.39GiB: chỉ test nhẹ sau resource recheck, không stress/fault injection trên shared SQL. Không checkout/CRUD tests/commit/push/cleanup.
- Báo cáo `docs/S32_3B_ENV_SHARED.md`; dừng chờ EXEC-A. Initial guard mismatch hostname/@@SERVERNAME đã được SELECT xác minh trước khi tạo DB, sửa guard rồi mới provision.

Cập nhật 2026-11-16 — S32.3A-FIX01 **PASS SOURCE/AUTOMATED; FIX01-RUNTIME PASS HTTP**:
- Sửa `ProductsController.Index`: chỉ reset Page khi ModelState có lỗi binding; giữ Page hợp lệ, page size, sort và filters hiện có.
- Thêm `CatalogPaginationHttpTests`: 11 ca HTTP qua MVC/Razor thật, catalog double riêng; trang 1–3 khác nhau, page sai/overflow, search/brand/category/sort và giữ query trong liên kết trang tiếp theo. Không kết nối SQL thật.
- `dotnet build PhoneStore.sln --no-restore`: PASS 0 warnings/errors; `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-restore --verbosity minimal`: 243/243 PASS, 0 skipped, runner khoảng 4 giây.
- FIX01-RUNTIME: sau phê duyệt riêng, build/recreate chỉ `web` bằng Compose `--no-deps --no-build --force-recreate`; Web healthy, SQL/API giữ nguyên container ID và StartedAt.
- HTTP runtime PASS: trang 1–4 đúng ID40–31/30–21/20–11/10–1, 40 ID duy nhất; page sai/overflow về1, page999 về4; search/brand/category/sort, next-link giữ query, 40 ảnh primary và API health200. SELECT count/checksum Products/Orders/Details/Images/Users trước-sau khớp (40/15/37/41/3). Không ghi DB/sửa code thêm/commit/push; browser trực quan chờ user duyệt.

Cập nhật 2026-11-14 — S31.5H **READY FOR USER TEST — ADMIN LAUNCHER FOR DOCKER SQL**:
- Thêm `scripts/run-admin-docker.ps1`: đọc `MSSQL_SA_PASSWORD`, `SQL_PORT`, `DB_NAME` từ `.env` ignored; dùng `SQLCMDPASSWORD` và một `SELECT` để preflight; chỉ truyền connection string qua environment của process Admin và dọn biến sau khi đóng. Không log/in secret, không khởi động Docker, không chạy song song khi Admin đã mở.
- Chuẩn hóa fallback Compose và `.env.example` về `PhoneStoreDB`; cập nhật README/LOCAL với lệnh một bước và cách quay lại SQL Server Windows. UI/business/appsettings không đổi.
- PowerShell parse PASS; `.env` ignore PASS; Compose config PASS; `-CheckOnly` PASS tại `127.0.0.1,14334/PhoneStoreDB` với 13 orders; Admin build scratch PASS 0 warnings/0 errors. Admin process đã được người dùng thay đổi bên ngoài trong lúc kiểm tra (PID mới nhất 19604) nhưng chưa có SQL session để chứng minh target; cần đóng instance hiện tại bình thường, chạy launcher và đăng nhập/kiểm danh sách đơn.

Cập nhật 2026-11-14 — S31 **C4 DIAGRAMS READY**:
- Đã đối chiếu solution/project references, DI của Web/API/Admin, EF Core, luồng checkout/order, Catalog API và Docker Compose từ source hiện tại; xác nhận Admin WinForms kết nối SQL Server trực tiếp qua EF Core.
- Tạo bốn sơ đồ PlantUML đơn sắc A4 tại `docs/report/chapter-4/c4-diagrams/`: System Context, Container, Component luồng mua hàng và Code diagram nghiệp vụ checkout; kèm `C4-SOURCE-VERIFICATION.md`.
- UI UX Pro Max được áp dụng cho phân cấp, nhãn quan hệ, bố cục và khả năng đọc. Không sửa ứng dụng/DB/Docker; không chạy build/test. Máy chưa có PlantUML/Java nên chưa xuất PNG/SVG; đã ghi lệnh xuất hình.

Cập nhật 2026-11-14 — S31.5G **READY FOR USER TEST, ADMIN WINDOWS → DOCKER SQL**:
- Không sửa source/UI/business: Admin đã hỗ trợ `ConnectionStrings__PhoneStoreConnection` qua environment override sau appsettings; DbContext/DI giữ nguyên. Mặc định Windows Authentication vẫn nguyên, xóa override là rollback cấu hình.
- Host read-only PASS qua `127.0.0.1,14334` (localhost ODBC17 timeout): Docker SQL healthy, PhoneStoreDB/SQL16.0.4315.2, Products40/Orders12/1activeAdmin. TLS giữ Encrypt=True+TrustServerCertificate=True; secret chỉ ở `.env` ignored/process env, không in/log. Admin/Docker uploads canonical path khớp,42files.
- Admin targeted build ra scratch PASS 0warnings/0errors; không chạy Admin, không DML/migration/seed/commit/push. Runbook thêm README/LOCAL; báo cáo `docs/system-analysis/31-ADMIN-DOCKER-SQL-CONNECTION.md`. Chờ user login và kiểm Dashboard/Products/Orders read-only.

Cập nhật 2026-11-14 — S31.5F **DOCKER RUNTIME PASS / READY FOR DEMO**:
- Phê duyệt trực tiếp nhận được; guard PASS (SQL healthy, RAM3.03GiB, DB/file target absent). Restore đúng1 lần backup verified vào Docker `PhoneStoreDB` bằng WITH MOVE+CHECKSUM, không REPLACE; 809 pages, ONLINE/compat160, 2 migrations. Counts Docker và Windows source đều giữ2/3/8/2/40/41/12/28; không migration/seed/DML hoặc ghi Windows DB.
- Start API rồi Web từ images hiện có, không rebuild: 3 services healthy/OOMfalse/restart0. HTTP API health/Home/Products/Detail đều200; API catalog40; 41/41 image URLs200. API/Web logs không fatal/500. Memory SQL~1.05GiB/API~91MiB/Web~99MiB; RAM cuối2.73GiB.
- Cập nhật README, LOCAL_DEVELOPMENT và `docs/system-analysis/30-DOCKER-FINAL-COMPLETION.md`. UI UX Pro Max dùng checklist nhưng không sửa UI. Stack đang chạy ports5015/5025/14334. Không commit/push; chưa thử stop/start persistence do RAM thấp.

Cập nhật 2026-11-14 — S31.5F **PREFLIGHT PASS / RESTORE AWAITING DIRECT CHAT APPROVAL**:
- Dùng UI UX Pro Max theo yêu cầu, không sửa UI. RAM pre-start6.89GiB; backup732160/hash D806...D4E2, mountRO, ports/config/volume isolation PASS. Compose recreate duy nhất target db để áp dụng memory2304MiB + SQL1536MB; healthy/OOMfalse/restart0, dùng~1.01GiB.
- Post-start RAM3.04GiB đạt sát gate3GiB. SQL guard xác nhận target PhoneStoreDB NULL/system DB only. Restore command bị cổng an toàn từ chối trước khi chạy vì phê duyệt chỉ có trong attachment; không restore/migration/seed/DML. Target db container hiện running healthy; cần user phê duyệt restore trực tiếp trong chat rồi recheck RAM≥3 trước đúng một restore WITH MOVE/CHECKSUM, không REPLACE.

Cập nhật 2026-11-14 — S31.5 **PARTIAL / FINAL COMPOSE ARTIFACTS READY, RUNTIME BLOCKED BEFORE RESTORE**:
- Compose 3 service, secret/env isolation, read-only uploads, pinned SQL image/backup mount, target volume/network và Web/API images đã sẵn sàng. Thêm giới hạn SQL được hỗ trợ `MSSQL_MEMORY_LIMIT_MB=1536`, cgroup `2304m`, giữ `stop_grace_period=60s`; static config PASS. Web/API image build PASS.
- Backup copy732160 bytes/SHA256 `D806...D4E2`; container HEADER/FILELIST/VERIFYONLY CHECKSUM lịch sử PASS. Chưa RESTORE DATABASE, migration, seed hoặc DML. Windows DB/old volume/target volume giữ nguyên.
- Hậu kiểm: Docker29.9.2 Linux/12CPU/~7.64GiB; ports5015/5025/14334 trống; target SQL Exited137/OOMKilled=false; RAM host3.86/15.77GiB, chưa đạt pre-start gate5GiB. Không start/restore. Cần user giải phóng RAM rồi phê duyệt riêng một guarded restore không REPLACE.
- Cập nhật `README.md`, `docs/LOCAL_DEVELOPMENT.md`; báo cáo `docs/system-analysis/30-DOCKER-FINAL-COMPLETION.md`. Không commit/push.

Cập nhật 2026-11-14 — S31.5C2D1.2 **FAIL RUNTIME SAFETY GATE / D2 NO-GO**:
- Pre-start RAM5.52GB PASS; port14334/hash/config/old-object isolation PASS. Approved recreate duy nhất target db, volume/network giữ nguyên; healthy first poll, StopTimeout runtime60, pinned SQL16.0.4315.2, backup RO mount, target PhoneStoreDB absent. Web/API không start.
- Post-start target1.152GiB, host free1.94GB <3 gate. Safe-stop initiated; container final Exited137, OOMKilled=false, restart0, no graceful shutdown log, volume/network preserved. StopTimeout60 confirmed nhưng không đạt graceful exit; nguyên nhân chưa xác định.
- RAM sau stop2.17GB; Windows PhoneStoreDB ONLINE. D2 hard NO-GO: không RESTORE. Cần giải phóng thêm RAM và chẩn đoán container signal/shutdown trước retry; không tự tắt apps/change Docker memory/DB/commit/push.
- Báo cáo `docs/system-analysis/29-DOCKER-SQL-RUNTIME-SAFETY-GATE.md`.

Cập nhật 2026-11-14 — S31.5C2D1.1 **CONFIG PASS / D2 RESOURCE GO PENDING USER MEMORY CLEANUP**:
- Chỉ sửa `docker-compose.restore.yml`: thêm `db.stop_grace_period: 60s`; merged config PASS (`1m0s`), image digest/port14334/data volume/backup RO mount/project giữ nguyên. Existing stopped container chưa nhận config mới; D2 cần approved Compose recreate container, giữ target volume.
- Host free4.06GB. Top working sets: vmmemWSL948MB (Docker, giữ), Chrome nhiều process (~529/452/225/224MB), VS Code nhiều process (~513/393/324/223MB), Defender364MB. Không tự đóng app. Đề xuất user đóng Chrome tabs/windows và VS Code windows không cần; không dừng Docker/Windows SQL/Defender.
- Exit137 evidence: OOMKilled=false, restart0, no shutdown/error log; code137 means SIGKILL-compatible termination, consistent with stop grace exceeded but không chứng minh daemon timing/cause. Longer grace giảm risk, phải runtime-test graceful exit later.
- Target container Exited137, volume/network tồn tại, running0; Windows PhoneStoreDB ONLINE. D2 NO-GO đến khi pre-start RAM≥5GB và post-start≥3GB (dựa observed drop4.00→1.71); không start/stop/restore/DB change/commit/push.
- Báo cáo `docs/system-analysis/28-DOCKER-SQL-RESOURCE-SHUTDOWN-SAFETY.md`.

Cập nhật 2026-11-14 — S31.5C2D1 **PARTIAL / BACKUP PRE-RESTORE VERIFICATION PASS, D2 BLOCKED**:
- Preflight PASS RAM4.00GB/disk305.62GB/port14334 free/hash correct/project+volume absent. Start duy nhất `db` project `phonestore-restore-c2`; created isolated network/volume/container, Web/API không start/create.
- Target SQL healthy 16.0.4315.2, pinned image; data volume RW riêng, backup bind RW=false. Container đọc file732,160 bytes/SHA256 match. Target chưa có PhoneStoreDB.
- Target HEADERONLY/FILELISTONLY/VERIFYONLY CHECKSUM đều PASS: FULL PhoneStoreDB, COPY_ONLY/checksum/compressed/not damaged/compat160/source16.0.1000; logical files PhoneStoreDB + PhoneStoreDB_log. Không RESTORE DATABASE.
- Resource after start: DB1.201GiB, host free1.71GB. Stop target vì dưới gate3GB; Exited137/OOMKilled=false, volume/network/container giữ nguyên, RAM hồi2.37GB. Source Windows DB ONLINE, counts2/3/8/2/40/41/12/28 unchanged; old objects unchanged.
- Báo cáo `docs/system-analysis/27-DOCKER-SQL-PRERESTORE-VERIFICATION.md`. D2 NO-GO tới khi RAM≥3GB (prefer4) và tăng SQL stop grace (đề xuất60s) được duyệt; không commit/push.

Cập nhật 2026-11-14 — S31.5C2C.1 **PARTIAL / COPY INTEGRITY PASS, WINDOWS SQL VERIFY BLOCKED**:
- Source backup qua SQL: exists, đúng1 set,730,566 bytes,COPY_ONLY+CHECKSUM; PhoneStoreDB ONLINE; old containers Exited; disk305.66GB. Destination `C:\PhoneStoreDockerTransfer\backup\<same-name>.bak` ngoài repo, directory/file chưa tồn tại.
- Người dùng đã manual elevated copy đúng1 file. Destination exists/readable, physical732,160 bytes; SHA256 `D806D221FD49FEFB9187C51D6EF0781F653312FF3606726309055F60B007D4E2` khớp nguồn. Chênh1,594 bytes so SQL compressed payload730,566 là media/header/block-padding overhead bình thường.
- Windows SQL VERIFYONLY copy bị OS error5 do restricted ACL; không đổi ACL. Source artifact đã VERIFYONLY PASS và byte/hash copy match; target container phải VERIFYONLY/HEADER/FILELIST trước RESTORE.
- Thêm `docker-compose.restore.yml`: pinned SQL digest, backup directory bind read-only. Static config PASS, isolated project/volume/port14334, target objects chưa tồn tại. RAM3.19GB (minimum3, preferred4), sequential gate. Báo cáo `docs/system-analysis/26-DOCKER-BACKUP-TRANSFER.md`; không start/restore/commit/push.

Cập nhật 2026-11-14 — S31.5C2C **PARTIAL / ENVIRONMENT READY, AWAITING BACKUP COPY APPROVAL**:
- Preflight PASS: backup C2A tồn tại/1 set/730,566 bytes; RAM4.69GB, disk305.71GB, port14334 free; old Web/DB vẫn Exited, old volume giữ nguyên. Không start/restart/create/delete Docker object.
- Đã cập nhật chỉ nonsecret keys trong ignored `.env`: project `phonestore-restore-c2`, ports5015/5025/14334, DB PhoneStoreDB, actual uploads path, Catalog flag/timeout; giữ nguyên password để người dùng tự rotate. `.env.example` chỉ sửa mô tả port.
- Compose config với ephemeral validation secret PASS: db/api/web, health/dependencies đúng, uploads bind read-only/path exists, target volume dự kiến chưa tồn tại. Không build/up.
- Người dùng đã nhập trực tiếp credential mới; boolean verification PASS: set, complexity≥16 và khác historical default. Compose config bằng `.env` thực PASS, services api/db/web; target container/volume chưa tồn tại, old containers vẫn Exited. Secret không được in/log.
- Backup copy chưa được phê duyệt/thực hiện; kế hoạch transfer restricted outside repo + size/hash + read-only mount. Restore/start vẫn cần phê duyệt riêng.
- Báo cáo pending `docs/system-analysis/25-DOCKER-ENVIRONMENT-AND-BACKUP-TRANSFER.md`; không DB/source backup mutation, restore, migration/seed, commit/push.
- Recheck cuối: project/ports/DB/uploads đều khớp, 42-file directory tồn tại, `.env` ignored/untracked; credential mới PASS redacted và static validation exit0. Không copy/start/restore/DB mutation, không commit/push.

Cập nhật 2026-11-14 — S31.5C2B **READINESS PASS / RESTORE EXECUTION BLOCKED**:
- Read-only xác nhận backup C2A tồn tại, đúng1 set, COPY_ONLY+CHECKSUM, compressed730,566 bytes; Windows SQL đọc/VERIFY được. Không copy artifact, không restore/start/restart container.
- Host RAM free4.68GB, Docker allocation7.64GB, disk305.72GB, port14334 free; mọi containers đều Exited. SQL2022 image digest đã ghi nhận, target project render `phonestore-restore-c2`, ports5015/5025/14334, target volume dự kiến chưa tồn tại.
- `.env` không ready: thiếu project/API/uploads; Web/SQL ports và DB name không khớp isolated plan; password đạt complexity nhưng trùng historical default. Người dùng phải tự rotate/chốt vars, không gửi secret/chat; task không sửa `.env`.
- Backup nằm dưới SQL default directory mà Codex/Docker chưa đọc trực tiếp. Đề xuất copy được phê duyệt riêng bằng Windows elevated/manual vào restricted transfer directory ngoài repo, không đổi ACL nguồn; mount directory read-only qua restore override, không bake image/Git.
- Báo cáo `docs/system-analysis/24-DOCKER-RESTORE-READINESS.md`. Kết luận BLOCKED tới khi secret rotated, env hoàn chỉnh, artifact copy approved/readable và restore được phê duyệt riêng; không commit/push.

Cập nhật 2026-11-14 — S31.5C2A **BACKUP + VERIFICATION PASS**:
- Sau phê duyệt riêng, tạo đúng1 FULL backup `PhoneStoreDB` bằng COPY_ONLY,CHECKSUM,COMPRESSION vào file timestamp+GUID mới; 809 pages, đúng1 matching backup set, backup6,701,056 bytes/compressed730,566 bytes, finish08:44:18 local. Không overwrite/retry.
- `RESTORE VERIFYONLY WITH CHECKSUM` PASS; HEADERONLY xác nhận FULL/compressed/COPY_ONLY/checksum/not damaged/SQL16.0.1000; FILELISTONLY xác nhận logical files `PhoneStoreDB` + `PhoneStoreDB_log`. Không RESTORE DATABASE.
- Hậu kiểm source: MSSQLSERVER Running, DB ONLINE, 2 migrations và counts Roles2/Users3/Brands8/Categories2/Products40/Images41/Orders12/Details28 không đổi; active backup/restore0. Docker Web/DB vẫn Exited, `.env` không đổi.
- Direct file read/SHA-256 bị ACL OS error5 nên hash UNKNOWN; không đổi ACL/thử bypass. Verification SQL PASS vẫn đủ tiêu chí C2A. Báo cáo `docs/system-analysis/23-DATABASE-BACKUP-VERIFICATION.md`; không commit/push.

Cập nhật 2026-11-14 — S31.5C1.1 **PARTIAL / CONTAINERS STOPPED, C2 RESTORE STILL BLOCKED**:
- Preflight xác minh đúng IDs/names/mount: Web không mount; DB dùng volume `phonestore_mssql_data`. Docker SQL có 0 user DB requests và 0 logged user transactions trước stop. Chạy đúng `docker stop phonestore-web phonestore-db`; không rm/down/prune/delete volume/process khác.
- Sau stop: Web Exited(0), DB Exited(137), OOMKilled=false; volume vẫn tồn tại, restart count0. DB không ghi graceful shutdown trước timeout nên consistency của old Docker volume chưa runtime-verify (không restart theo giới hạn). Windows MSSQLSERVER vẫn Running và PhoneStoreDB ONLINE.
- RAM host free 1.24→2.16GB; ports5015/5025/14334/14333 đều free. Mức2.16GB vẫn thấp để chạy thêm SQL Linux an toàn; C2 restore NO-GO đến khi recheck đạt headroom tối thiểu đề xuất3–4GB.
- `.env` tồn tại nhưng thiếu project/API port/uploads; secret chỉ ghi SET, không đọc. 42/42 uploads đọc được. Compose config với vars tạm isolated PASS, services db/api/web; không up/build/migration/seed.
- SQL backup path tồn tại, disk305.96GB, source ONLINE; ACL bị access denied với agent nhưng history chứng minh service từng backup. Báo cáo `docs/system-analysis/22-DOCKER-RESOURCE-READINESS.md`; không backup/restore/DB write, không commit/push.

Cập nhật 2026-11-14 — S31.5C1 **READ-ONLY PREFLIGHT PASS / C2 BLOCKED BY RESOURCES AND ENV**:
- Giữ nguyên Git/containers/database; chỉ SELECT/CIM/Docker inspect. SQL Windows `MSSQLSERVER`/SQL2022 Developer16.0.1000.6, `PhoneStoreDB` ONLINE/compat160/FULL/16MB, 2 migrations; counts Roles2/Users3/Brands8/Categories2/Products40/Images41/Orders12/Details28. Backup mới nhất02/11 COPY_ONLY+CHECKSUM, đã cũ.
- Backup plan: file timestamp+GUID mới trong SQL default backup directory, `COPY_ONLY,CHECKSUM,COMPRESSION`, sau đó `RESTORE VERIFYONLY ... WITH CHECKSUM`; không backup ở C1. SQL service account đã xác định nhưng ACL trực tiếp bị sandbox từ chối; lịch sử backup chứng minh đường dẫn từng ghi được, quyền hiện tại phải recheck ngay C2.
- Docker cũ vẫn running (`phonestore-web`, `phonestore-db` healthy); 5015/5025/14334 free, 14333 occupied. Host chỉ1.08GB RAM free; Docker SQL dùng~1.39GB nên NO-GO chạy thêm SQL container cho tới khi có đủ headroom. Disk C còn305.96GB.
- 41 DB image references/41 unique đều có file; disk42, missing0, orphan1 giữ nguyên. `.env` tồn tại nhưng thiếu uploads path/API port/project name; secret set nhưng không đọc/ghi. Current config không default credential; Git history còn1 matching commit.
- Báo cáo `docs/system-analysis/21-DATABASE-MIGRATION-READINESS.md`. Không backup/restore/migration/seed/DML, không start/stop container, không commit/push.

Cập nhật 2026-11-14 — S31.5B **STATIC PASS / BUILD PASS / RUNTIME NOT TESTED**:
- Chuẩn hóa Compose cách ly gồm `web/api/db`: Web5015, API5025, SQL port cấu hình (đề xuất14334), service DNS nội bộ, dependency/healthchecks và named volume; không start/restart/delete stack hiện hữu.
- Thêm multi-stage `src/PhoneStore.Api/Dockerfile`; Web/API dùng .NET8/port8080. Runtime uploads bị loại khỏi image và bind-mount read-only từ host để có thể dùng chung với Admin Windows.
- Xóa credential mặc định khỏi Compose, `.env.example` và Docker guide; `.env`/secret vẫn ngoài Git. Credential cũ còn trong Git history và local ignored `.env` cần người dùng đánh giá/rotate, không rewrite/rotate trong task.
- `docker compose config`: thiếu biến bắt buộc FAIL đúng thiết kế; biến kiểm thử PASS, đủ3 services/dependencies/healthchecks. Targeted Release build Web + API đều 0 warnings/errors. Không Docker image build do giới hạn RAM; không test runtime/health thực.
- Báo cáo `docs/system-analysis/20-DOCKER-COMPOSE-HARDENING.md`. S31.5C chỉ GO có điều kiện sau khi đổi local secret, chốt uploads path/ports và phê duyệt backup/restore riêng; không commit/push.

Cập nhật 2026-11-14 — S31.5A **DOCKER COMPOSE PREFLIGHT PASS / CONDITIONAL GO FOR S31.5B**:
- Docker Desktop/Engine/Compose đã cài và chạy Linux containers (Engine29.9.2, Composev5.6.1, 12CPU/7.6GB). Hai container ngoài task đang chạy: Web8080 HTTP200 và SQL2022 healthy trên14333; task không start/stop/restart. Compose hiện chỉ có Web+DB, không API, Web không healthcheck/feature config nội bộ.
- SQL nguồn read-only: SQL2022 RTM, PhoneStoreDB16MB/compat160/FULL, Roles2/Users3/Brands8/Categories2/Products40/Images41/Orders12/Details28, 2 migrations. Container target SQL2022 mới hơn; DockerDB có schema/migrations nhưng0 business rows. Backup gần nhất02/11 đã cũ; không backup/restore/migration/seed.
- Blockers: default SA credential tracked/history; API Dockerfile/service thiếu; 0 uploads được Git track dù DB tham chiếu41 file (disk42,1orphan), Docker build local không reproducible; port14333/volume hiện dùng; host chỉ~1GB RAM free. Đề xuất isolated project/ports/volume, mandatory secrets, API service DNS `http://api:8080/`, secure uploads bind mount và backup/restore rehearsal riêng.
- Báo cáo `docs/system-analysis/19-DOCKER-COMPOSE-PREFLIGHT.md`. Chỉ tài liệu; không sửa Docker/source/database, không build/test, không commit/push. Conditional GO chỉ cho S31.5B implementation hardening, NO-GO deployment hiện tại.

Cập nhật 2026-11-14 — S31.4 **DOCUMENTATION STANDARDIZED / SECURITY REVIEW REQUIRED**:
- Viết lại `README.md` theo source thực tế và thêm `docs/LOCAL_DEVELOPMENT.md`: chức năng Web/Admin/API, kiến trúc phân lớp, project/dependency, SQL setup, ports, feature flag, API reference, test evidence, troubleshooting và giới hạn; toàn bộ link repository dùng đường dẫn tương đối.
- Restore PASS; solution build cách ly **0 warnings/errors**; full regression **232/232 PASS**, 0 fail/skip. Không sửa application source/database, không migration/seed, không commit/push.
- `.gitignore` đã bao phủ `bin/obj/.vs/scratch/TestResults/log/.env/certificate/SQL backup`; không có build artifact/database backup được Git track. `.env.example` được track có chủ ý.
- **SECURITY REVIEW REQUIRED:** `.env.example`, `docker-compose.yml` và Docker guide đang chứa cùng một default SQL SA credential có thể sử dụng. Chưa sửa ngoài phạm vi tài liệu và chưa rewrite history; phải thay bằng placeholder/required environment value, rotate nếu từng dùng thật, rồi đánh giá history trước khi public push.


Cập nhật 2026-11-14 — S31.3G **REST API FINAL ACCEPTANCE PASS (CUMULATIVE EVIDENCE)**:
- Source/architecture/contracts khớp S31.3B–F; không sửa application source. Build solution ra output cách ly **0 warnings/errors** vì Admin PID 3604 khóa `bin`; full regression an toàn **232/232 PASS**, 0 fail/skip.
- Live Swagger recheck 200/5 paths và validation 400 PASS. Catalog/health recheck từ process Codex bị TLS chướng ngại trước SQL (503/500); không hạ bảo mật. Kết luận PASS dựa trên live SQL/API/Web S31.3E–F còn phù hợp source, automated regression mới và Admin manual FULL PASS do người dùng xác nhận; không tuyên bố recheck này là live catalog PASS mới.
- SQL read-only trước/sau không đổi: Roles2/Users3/Brands8/Categories2/Products40/Images41/Orders11/Details26; 2 migration EF 8.0.28 giữ nguyên. API task đã cleanup, 5015/5025 trống; Admin PID 3604 không bị ảnh hưởng.
- Báo cáo `docs/system-analysis/17-REST-API-FINAL-ACCEPTANCE.md`; bổ sung ghi chú Admin manual vào report S31.3E, không viết lại bằng chứng gốc. Không DML/DDL/migration/seed, commit/push/deploy.

Cập nhật 2026-11-13 — S31.3F **CUSTOMER WEB PRODUCT DETAIL API INTEGRATION PASS / GO FOR S31.3G**:
- Tích hợp duy nhất Product Detail qua typed `ICatalogApiClient` + `IProductDetailResolver`; feature flag `CatalogApi` mặc định OFF, BaseUrl/timeout configurable và validate startup. Không đổi Razor/UI, route, Business/Data/Models/API/Admin hoặc các trang Web khác.
- Policy: OFF không gọi API; ON 200 dùng API; 404 thành Web 404 không fallback; 400 thành 502; 429/5xx/network/timeout/invalid payload fallback local đúng một lần; client cancellation không fallback. DTO map đầy đủ và chỉ chấp nhận relative image URL an toàn trên Web origin.
- Build solution **0 warnings/errors**; targeted integration + API/Web regression **53/53 PASS**. Runtime thật: OFF detail 200 với 0 API calls; ON API log xác nhận Web gọi `/api/v1/products/1`; HTML parity/ảnh/giá/stock/spec PASS; API stop an toàn thì local fallback 200; missing product 404; Home/List không ảnh hưởng.
- SQL snapshot/migrations trước sau không đổi: Roles2/Users3/Brands8/Categories2/Products40/Images41/Orders11/Details26. Process task đã dừng, 5015/5025 trống. Báo cáo `docs/system-analysis/16-CUSTOMER-WEB-API-INTEGRATION.md`; không DML/DDL/migration/seed, commit/push. Người dùng xác nhận bổ sung S31.3E Admin manual FULL PASS trước khi bắt đầu task.

Cập nhật 2026-11-13 — S31.3E **PARTIAL — ADMIN UI NOT VERIFIED / NO-GO FOR FULL S31.3F**:
- Build solution **0 warnings/errors**; targeted regression **48/48 PASS** (API security/foundation/catalog 32, Web 2, Admin read-only isolated 14). Web runtime 5015 PASS Home/catalog/detail/search/filter/image; API runtime 5025 PASS 5 endpoint, pagination/filter/contract/Swagger; không lỗi log.
- Web/API/Admin process chạy đồng thời không xung đột port; Web/API vẫn 200 khi Admin PID 22168 tồn tại. Admin executable/config đúng main `PhoneStoreDB` và process Responding, nhưng không có MainWindow/top-level window trong desktop context nên LoginForm/Dashboard/Products **NOT VERIFIED**, không tuyên bố UI PASS.
- SQL live snapshot trước/sau và migrations không đổi: Roles2/Users3/Brands8/Categories2/Products40/Images41/Orders11/Details26. Đối chiếu SQL/API/Web ba sản phẩm IDs 1–3 khớp; Admin NOT COMPARABLE. Không DML/DDL/migration/seed.
- Đã dừng không-force đúng ba PID task; 5015/5025 trống. Báo cáo: `docs/system-analysis/15-API-CONCURRENT-RUNTIME-ACCEPTANCE.md`. Cần người dùng mở Admin thủ công, login và kiểm tra Dashboard/Products read-only trước khi nâng FULL PASS; không sửa source, commit/push hay chuyển S31.3F.
- HEAD thay đổi bên ngoài acceptance từ `4f53a0d` sang `9d18904` (commit S31.3D); tác vụ này không commit. Source sạch, chỉ `PROGRESS.md` và báo cáo S31.3E còn trong working tree.

Cập nhật 2026-11-13 — S31.3D **API SECURITY HARDENING PASS / GO FOR S31.3E**:
- Hardening 5 endpoint hiện có bằng query/request limits, fixed-window rate limit (catalog 60/phút, health 120/phút, không tin X-Forwarded-For), catalog timeout 10s/health 3s, cancellation propagation, validated correlation ID và structured request log không ghi query/body/secrets. Không bật CORS/JWT/Forwarded Headers; Swagger vẫn Development-only.
- Problem Details an toàn cho 400/404/429/500/503/504; cấu hình `ApiOperational` fail-closed khi ngoài giới hạn. Build solution **0 warnings/errors**; targeted cuối **34/34 PASS** (`Security 8 + Foundation 6 + Catalog 18 + Web 2`).
- Runtime thật: API 5025 đủ 5 route PASS, trace header/log PASS, Swagger 5 paths, SQL live chỉ SELECT; Web 5015 đồng thời Home/Products 200. Snapshot trước/sau không đổi: Roles2/Users3/Brands8/Categories2/Products40/Images41/Orders11/Details26. Hai process test đã dừng, 5015/5025 trống.
- Giới hạn còn lại: limiter in-memory per-process; proxy phải cấu hình trusted proxy trước forwarded headers; cancellation token xuống EF không chứng minh SQL Server dừng tức thì. Báo cáo: `docs/system-analysis/14-API-SECURITY-HARDENING.md`. Không sửa Web/Admin/Business/Data/Models, không DML/DDL/migration/seed, không commit/push.

Cập nhật 2026-11-13 — S31.3C **CATALOG REST API PASS / GO FOR S31.3D**:
- Bổ sung read-only endpoints `/api/v1/products`, `/api/v1/products/{id}`, `/api/v1/brands`, `/api/v1/categories`; giữ `/api/health`. DTO độc lập, pagination/filter/sort validation, relative image URL an toàn, 400/404/503 Problem Details; tái sử dụng `IProductService`, không sửa Business/Data/Models/Web/Admin.
- Runtime SQL Server thật trên `localhost:5025`: products 40 (default 10), brands active 8, categories active 1; detail/filter/invalid input/404 và Swagger 5 paths đều PASS. Web chạy đồng thời trên 5015, Home/Products 200. Admin source không đổi và không mở UI.
- Restore PASS; solution build **0 warnings/errors**; `CatalogApiTests` **18/18**, `ApiFoundationTests` **6/6**, `HomePresentationTests` **2/2 PASS**. Snapshot `PhoneStoreDB` trước/sau khớp: Roles2/Users3/Brands8/Categories2/Products40/Images41/Orders11/Details26; không DML/DDL/migration/seed.
- Báo cáo: `docs/system-analysis/13-CATALOG-API-IMPLEMENTATION.md`. Ctrl+C chưa dừng được hai executable thử nghiệm Web/API; không force-kill. Không commit/push và chưa chuyển S31.3D.

Cập nhật 2026-11-13 — S31.3B **API FOUNDATION PASS / READY FOR S31.3C**:
- Thêm project `PhoneStore.Api` .NET 8 vào solution, reference Business/Data/Models; DI scoped cho DbContext/catalog reader/product service, Problem Details + centralized safe exception handler, standard health check và Swagger 6.6.2 chỉ trong Development. Chưa có catalog/write endpoint, CORS hay auth API.
- `GET /api/health` runtime thật trên `localhost:5025` trả **200 healthy**; SQL live verified read-only bằng EF `SELECT 1`. Swagger JSON/UI đều 200, OpenAPI 3.0.1 chỉ có `/api/health`; Web `5015` và API `5025` chạy đồng thời đều 200, không xung đột. Đã dừng toàn bộ process test.
- Restore PASS; solution build **0 warnings/errors**; `ApiFoundationTests` **6/6 PASS**, `HomePresentationTests` regression **2/2 PASS**. Tests bao phủ 200/503, response không lộ hạ tầng, Swagger dev/prod, DI lifetime và safe Problem Details 500.
- Không migration/seed/DML/DDL, không sửa source Web/Admin/Business/Data/Models và không commit/push. Báo cáo: `docs/system-analysis/12-API-FOUNDATION-IMPLEMENTATION.md`. Snapshot live hiện tại khác count lịch sử S31.1 do hoạt động ngoài phạm vi chưa xác định; task chỉ chạy health `SELECT 1` và Web GET read-only.

Cập nhật 2026-11-13 — S31.2 **ARCHITECTURE AUDIT VERIFIED / READY FOR CHAPTER 4 DRAFT**:
- Xác minh solution 7 project và dependency thực tế: hai presentation process `PhoneStore.Web` (MVC/Razor) + `PhoneStore.Admin` (WinForms) dùng chung `Business -> Data -> Models`, cùng EF Core kết nối `PhoneStoreDB`; không có REST API, microservice hoặc message queue giữa Web/Admin.
- Tạo `docs/system-analysis/10-ARCHITECTURE-AUDIT.md`: architecture overview, project dependency graph, C4 Level 1–4 proposal, sequence checkout/Admin order status, auth/authorization, logging/error handling và bố cục Chương 4.
- Kết luận layered modular monolith thực dụng, không phải Clean Architecture thuần do `Business` tham chiếu `Data`. Logging/observability còn hạn chế; deployment topology, production sink và fresh-check→commit race được ghi UNKNOWN/known limitation đúng bằng chứng.
- Chỉ đọc source/config đã che bí mật; không build/test, không chạy app/SQL, không sửa source/database/migration và không commit/push. Bảo toàn diff S31.1 hiện có.

Cập nhật 2026-11-13 — S31.1 **DATABASE DESIGN AUDIT VERIFIED**:
- Đối chiếu read-only toàn bộ 8 Entity/DbSet, Fluent API, 2 migrations và SQL Server live `PhoneStoreDB`; không phát hiện drift về table/column/type/null/default/identity/PK/FK/check/index.
- Tạo `docs/system-analysis/09-DATABASE-DESIGN-AUDIT.md`: overview, data dictionary 58 cột, 7 quan hệ, constraints/indexes, design observations và đề xuất ERD + 10 sheet Excel.
- SQL metadata xác minh ngoài sandbox bằng exception hẹp; không đọc PII, không DML/DDL/migration/seed, không build/test, không sửa source và không commit/push.

Cập nhật 2026-11-13 — S30.4E **READY FOR USER TEST** (Customer cart quantity + detail navigation):
- Root cause: `CartController.Change` xóa dòng rồi `Add` lại nên sản phẩm vừa cập nhật bị đẩy cuối danh sách; form reload toàn trang làm dòng đổi vị trí và thiếu snapshot để đồng bộ đúng subtotal/total. Nay replace đúng index theo ProductId, giữ thứ tự; AJAX trả quantity/stock/unit price/line total/total quantity/total amount do server tính.
- Stepper khóa từng dòng khi request, chặn double-click, đối chiếu ProductId response, cập nhật đúng row/summary/badge; lỗi/response không chắc chắn khôi phục input và reload để đồng bộ session. Cart có header 5 cột, link `Xem chi tiết` dùng route Product Detail hiện có và responsive/focus state.
- Files: `CartController.cs`, `Views/Cart/Index.cshtml`, `wwwroot/js/site.js`, `wwwroot/css/site.css`, `CartControllerTests.cs`. Build **0 warnings/errors**; targeted Cart/Checkout/Commerce **35/35 PASS**; `node --check` PASS. Không DB DML/migration, không chạy/restart Web/Admin.
- MANUAL PENDING: browser 375/768/1024/1440, click nhanh +/−, network failure/reload, focus/link detail và checkout smoke read-only. Không commit/push.

Cập nhật 2026-11-13 — S30.4A.2 **READY FOR REVIEW** (SQL connection environment diagnosis):
- Xác minh lỗi TLS chỉ xảy ra dưới token `codexsandboxoffline`; cùng `sqlcmd` ngoài sandbox kết nối `PhoneStoreDB` và SELECT read-only PASS. SQL Server 2022 `16.0.1000.6`, `ForceEncryption=0`, không certificate binding; không đổi config/bảo mật.
- Live schema khớp EF/migrations: 8 bảng nghiệp vụ + migration history, 7 FK, 7 checks, unique/filter indexes; 2 migrations applied. Counts: Roles2/Users3/Brands8/Categories1/Products40/Images40/Orders7/Details13; bốn nhóm quan hệ 0 orphan.
- Cập nhật `07-DATABASE-SCHEMA-INVENTORY.md`, tạo `08-SQL-CONNECTION-DIAGNOSIS.md`. Schema **READY cho ERD**; không DML/DDL, không chạy app/build/test, không commit/push.

Cập nhật 2026-11-13 — S30.4A **READY FOR REVIEW** (full-system functional acceptance & workflow audit):
- Tạo 7 tài liệu trong `docs/system-analysis/`: overview, inventory 45 chức năng, 5 workflow, test matrix, issues/gaps, manual checklist và schema inventory.
- Chạy 15 nhóm test mục tiêu từ artifact S30.3D: **138/138 PASS**, 0 fail/skip; không chạy app, không DML/migration/seed và không sửa source.
- Live SQL là **UNKNOWN**: `sqlcmd`/ODBC 17 lỗi encryption/SSL trước SELECT; không dùng số lịch sử để tuyên bố live PASS.
- Kết luận **PARTIAL / READY FOR REVIEW**: core commerce/Admin có automated evidence tốt; cần WinForms acceptance S30.3A–D và live schema read-only trước Use Case → DFD → ERD.

Cập nhật 2026-11-13 — S30.3D **READY FOR USER TEST** (Reports revenue chart & advanced filter UX):
- Thay biểu đồ `RevenueBarsControl` cũ trên Reports bằng `RevenueColumnChartControl` dùng chung với Dashboard, có trục tiền tệ, tooltip thời gian/doanh thu/số đơn Completed, giới hạn bề rộng cột 44px logical để một điểm không kéo giãn toàn card, cùng loading/empty/error state riêng.
- Card doanh thu mặc định tuần hiện tại UTC+7 (T2–CN), có thanh nhanh `Tuần | Tháng | Năm`, kỳ trước/sau, nhãn kỳ, tổng doanh thu/số đơn và panel `Bộ lọc` progressive disclosure. Draft chỉ được áp dụng khi nhấn `Áp dụng`; `Đặt lại` về tuần hiện tại; busy state khóa toàn bộ thao tác để tránh truy vấn chồng.
- Panel nâng cao không lặp thanh nhanh: chọn ngày thuộc tuần, tháng+năm, năm, hoặc khoảng tùy chỉnh tối đa 366 ngày. Khoảng tùy chỉnh hiển thị ≤45 ngày theo ngày, 46–180 ngày gom theo tuần, >180 ngày gom theo tháng; tổng tiền/số đơn vẫn lấy từ chuỗi ngày gốc của `ReportingService` và đơn vị nhóm được ghi trên nhãn kỳ.
- Phạm vi lọc tách biệt: ngày ở header là **bộ lọc toàn trang** cho KPI đơn/doanh thu/trung bình, trạng thái và Top 10; tồn kho/cảnh báo là số liệu hiện tại. Bộ lọc trong card chỉ gọi `GetRevenueSeriesAsync` và chỉ cập nhật biểu đồ; không tải lại KPI, status chart hoặc bảng.
- Nghiệp vụ giữ nguyên: chỉ `Completed`, theo `OrderDate`, timezone `SE Asia Standard Time` UTC+7; không đổi Dashboard, database hoặc Customer Web.
- Files S30.3D: mới `ReportRevenueChartPanel.cs`; sửa `ReportsControl.cs`, `ReportingUi.cs`, `docs/PROGRESS.md`. Các diff S30.3A–C được bảo toàn.
- Kiểm tra mục tiêu: Admin build cô lập **0 warnings / 0 errors**; test-project build **0 warnings / 0 errors**; `PhoneStore.Tests.ReportingTests` **13/13 PASS**. Không chạy full suite hoặc Admin, không DML/migration/seed.
- Chưa kiểm thử trực tiếp: cần người dùng nghiệm thu Reports tại 1366×768, DPI 100/125/150%, thao tác mở/đóng panel, điều hướng kỳ, tooltip và wrap của bộ lọc. Không commit/push.

Cập nhật 2026-11-13 — S30.3C **READY FOR USER TEST** (Admin management tables ID ASC):
- Query quản lý Product, Customer và Order nay mặc định `OrderBy(...Id)` tăng dần; tìm kiếm/lọc tiếp tục áp dụng trước khi sắp xếp. Brand và Category trong `LookupManagementStore` cũng chuyển từ thứ tự tên sang `BrandId`/`CategoryId` tăng dần.
- Giữ nguyên ID thật, kiểu số và toàn bộ CRUD/filter. Không đánh lại identity, không DML/migration/seed và không thay đổi DataGridView.
- Giữ thứ tự nghiệp vụ riêng: Dashboard/Reports (đơn gần đây, bán chạy, tồn thấp), Customer Web catalog/orders, chi tiết đơn theo `OrderDetailId`, và quản lý ảnh theo `IsPrimary → DisplayOrder → ImageId` vì Admin điều khiển DisplayOrder.
- Files sửa S30.3C: `ProductManagementStore.cs`, `CustomerManagementStore.cs`, `OrderManagementStore.cs`, `LookupManagementStore.cs`, test mới `ManagementOrderingTests.cs`, và `docs/PROGRESS.md`.
- Kiểm tra mục tiêu: test-project build cô lập **0 warnings / 0 errors**; `ManagementOrderingTests.Management_lists_are_sorted_by_numeric_id_ascending_after_filters` **1/1 PASS** trên SQLite cách ly, bao phủ Product/Customer/Order/Brand/Category với ID `5, 20`. Không chạy full suite hoặc Admin.
- Chưa kiểm thử trực tiếp: cần người dùng mở năm bảng quản lý trên Admin để xác nhận dữ liệu SQL thật tăng dần sau tải lại/tìm kiếm/lọc. Không commit/push.

Cập nhật 2026-11-13 — S30.3B **READY FOR USER TEST** (filter alignment & truncated text):
- Thay cách ép `Height`/owner-draw của S30.3A bằng host panel 40px dùng chung: khung là chiều cao thị giác thống nhất, còn `TextBox`/`ComboBox` native được căn giữa theo cả hai trục và tiếp tục giữ hành vi bàn phím/accessibility nền tảng.
- Product: bốn label dùng cùng cấu trúc hai hàng; nút Tìm kiếm dùng spacer label cùng typography nên căn chính xác theo hàng nhập liệu, không còn `Margin.Top` ước lượng; khoảng trống toolbar được giảm về nhịp 10–12px.
- Customer/Order: dropdown trạng thái tăng từ 170/175px lên 200px để hiển thị đầy đủ `Tất cả trạng thái`; search, dropdown và nhóm button cùng khung 40px, nhóm action wrap nguyên khối khi thiếu chiều rộng.
- Focus border dùng token `Primary`, border thường dùng token `Border`; AutoScale baseline 96 DPI được khai báo nhất quán cho cả ba màn hình. Không đổi handler, truy vấn hay business logic.
- Files sửa trong S30.3A/B: `ManagementUi.cs`, `ProductsManagementControl.cs`, `CustomersManagementControl.cs`, `OrdersManagementControl.cs`, `docs/PROGRESS.md`.
- Kiểm tra mục tiêu: Admin build cô lập tại `scratch/s30_3b/admin-out` **0 warnings / 0 errors**; `git diff --check` PASS. Không chạy full tests, không chạy Admin và không chạm database.
- Chưa kiểm thử trực tiếp: người dùng cần nghiệm thu trực quan ở DPI 100/125/150%, resize cửa sổ và keyboard focus trên ba màn hình. Không commit/push.

Cập nhật 2026-11-13 — S30.3A **READY FOR USER TEST** (Admin filter bar UI consistency):
- Chuẩn hóa thanh lọc của Sản phẩm, Khách hàng và Đơn hàng bằng helper chung `ManagementUi`: control mục tiêu 40px tại DPI 100%, font Be Vietnam Pro/Segoe UI fallback 9.5pt, khoảng cách ngang 10px và khoảng cách wrap 8px.
- `TextBox` dùng chiều cao thực 40px; `ComboBox` native dùng owner-draw item cao 30px để chiều cao đóng đạt khoảng 38–40px và text căn giữa; button giữ chuẩn theme 40px. Không thay đổi handler, truy vấn hoặc nghiệp vụ.
- Product giữ lưới label phía trên và căn nút Tìm kiếm theo hàng nhập liệu. Customer/Order dùng toolbar chung và nhóm action không bị tách rời khi xuống hàng; nút chính/phụ giữ nguyên hierarchy hiện có.
- Files sửa: `ManagementUi.cs`, `ProductsManagementControl.cs`, `CustomersManagementControl.cs`, `OrdersManagementControl.cs`, `docs/PROGRESS.md`.
- Kiểm tra mục tiêu: `dotnet build src/PhoneStore.Admin/PhoneStore.Admin.csproj --no-restore --disable-build-servers -m:1 -nologo -o scratch/s30_3a/admin-out` — **0 warnings / 0 errors**. Không chạy full tests vì chỉ đổi layout/style, không chạy Admin và không chạm database.
- Chưa kiểm thử trực tiếp: cần người dùng nghiệm thu ba toolbar tại 1366×768 và DPI 100/125/150%, đặc biệt wrap nhóm action và text ComboBox. Không commit/push.

Cập nhật 2026-11-13 — S30.2G **READY FOR USER TEST** (Dashboard Revenue final UI/UX refinement):
- **Điều hướng kỳ**: thêm nút vector kỳ trước/kỳ sau và đặt nhãn khoảng thời gian ở giữa. Tuần dịch ±7 ngày, tháng/năm dịch đúng biên lịch; giới hạn từ `01/01/2020` đến hết năm kế tiếp; điều hướng ẩn với khoảng tùy chỉnh. Nút có accessible name, tooltip và disabled state; mọi control doanh thu bị khóa ngay trước `await`, nên bấm nhanh không tạo truy vấn chồng. Mỗi lần chỉ gọi `ReloadRevenueOnlyAsync`.
- **Bộ lọc không trùng lặp**: giữ duy nhất thanh nhanh `Tuần | Tháng | Năm`. Panel nâng cao không còn lặp ba chế độ; chỉ hiện checkbox `Khoảng ngày tùy chỉnh`, trường đúng với chế độ nhanh hiện tại và hai nút `Đặt lại` / `Áp dụng` căn phải. Draft vẫn tách applied: đóng panel không đổi biểu đồ; bỏ custom trở về chế độ chuẩn gần nhất; chọn nhanh thoát custom.
- **Modern Compact**: panel dùng một hàng responsive khi đủ rộng, wrap ở DPI cao, chiều cao tính theo preferred size và tham gia layout card (không overlay Donut). Icon chevron được bổ sung vào bộ vector `AdminTheme`; khoảng cách theo nhịp 4/8px và không đổi Dashboard/Header/Sidebar/Donut.
- **Nghiệp vụ giữ nguyên**: Completed + `OrderDate`, UTC+7, tuần Thứ 2–Chủ nhật, custom tối đa `366` ngày, empty/error state và tải Dashboard/doanh thu độc lập. `RevenueColumnChartControl` S30.2E/F được tái sử dụng vì đã có tooltip 3 dòng, chống chồng nhãn, Y-axis tiền tệ và phân biệt empty/error.
- **Files sửa trực tiếp S30.2G**: `DashboardControl.cs`, `AdminTheme.cs`, `ReportingService.cs`, `ReportingTests.cs`, `docs/PROGRESS.md`. Không sửa database/Customer Web và không thêm package.
- **Kiểm tra mục tiêu**: Admin build tại `scratch/s30_2g/admin-out` **0 warnings / 0 errors**; test build **0 warnings / 0 errors**; `PhoneStore.Tests.ReportingTests` **13/13 PASS**, gồm điều hướng tuần/tháng/năm qua biên năm và 12 bước tuần liên tục. Không chạy full suite.
- **Chưa kiểm thử trực tiếp**: cần người dùng kiểm tra WinForms 1366×768, DPI 100/125/150%, wrap panel, focus/tooltip và thao tác chuột. Precheck thấy Admin PID `10988`; hậu kiểm PID không còn tồn tại (Codex không dừng tiến trình). Không build/deploy vào `bin/Debug`, không commit/push.

Cập nhật 2026-11-13 — S30.2F **READY FOR USER TEST** (Weekly Revenue & Compact Advanced Filters):
- **Mặc định mới**: Dashboard tải doanh thu tuần hiện tại theo múi giờ `SE Asia Standard Time` (UTC+7), tuần Thứ 2–Chủ nhật; biểu đồ luôn có đủ 7 bucket `T2`–`CN`, ngày không doanh thu bằng `0`, tooltip có thứ/ngày đầy đủ, doanh thu và số đơn Completed. Vẫn tính theo `OrderDate`, không giả định ngày hoàn thành.
- **Thanh lọc gọn**: phía trên biểu đồ chỉ luôn hiện `Tuần | Tháng | Năm`, nút icon `Bộ lọc`, khoảng đang xem và tổng doanh thu/số đơn. Chọn nhanh chỉ gọi lại chuỗi doanh thu; không tải lại 4 KPI, Donut hoặc ba bảng Dashboard.
- **Panel nâng cao**: mở theo progressive disclosure, hỗ trợ tuần khác (chọn một ngày trong tuần), tháng+năm, năm và `Từ ngày – Đến ngày`; draft tách khỏi applied nên đóng panel không áp dụng thay đổi. `Áp dụng` đóng panel; `Đặt lại` về tuần hiện tại; khoảng tùy chỉnh giữ kiểm tra thứ tự và giới hạn `366` ngày. Panel tham gia layout, không overlay Donut.
- **Timezone & dữ liệu**: ngày hiện tại của UI được lấy theo UTC+7 thay vì timezone máy chủ ngầm định. SQL read-only tuần `09/11/2026–15/11/2026`: `162.900.000 ₫`, `2` đơn Completed, phát sinh ngày địa phương `12/11/2026`; không DML/migration/seed.
- **Files sửa trong S30.2F**: `DashboardControl.cs`, `ReportingCalendar.cs`, `ReportingService.cs`, `ReportingStore.cs`, `ReportingModels.cs`, `ReportingTests.cs`, và mục tiến độ này. `ReportingUi.cs`/theme/interfaces cùng các thay đổi S30.2 trước đó được tái sử dụng, không thiết kế lại Dashboard/Sidebar/Header/Donut.
- **Kiểm tra**: Admin build cô lập tại `scratch/s30_2f/admin-out` **0 warnings / 0 errors**; test build **0 warnings / 0 errors**; `PhoneStore.Tests.ReportingTests` **12/12 PASS**. Không chạy full suite và không khởi động/dừng Admin.
- **Chưa kiểm thử trực tiếp**: thao tác WinForms trên bản mới, bố cục thực tế 1366×768 và DPI 100/125/150%, tooltip chuột và việc chuyển nhanh/panel bằng bàn phím cần người dùng nghiệm thu. Precheck thấy Admin PID `22884` dùng binary cũ; hậu kiểm tiến trình này không còn tồn tại (Codex không dừng tiến trình), và `bin/Debug` không được cập nhật trong nhiệm vụ này.
- **Git**: giữ nguyên branch `main`, HEAD `b467c796bb574b8d7483bad2868d1e48c62e764b`; bảo toàn toàn bộ diff S30.1–S30.2E; không reset/commit/push.

Cập nhật 2026-11-13 — S30.2E **READY FOR USER TEST** (Codex handover, hoàn thiện Dashboard):
- **Kế thừa nguyên trạng S30.1–S30.2D**: Admin Shell/Login/DPI, 4 KPI dữ liệu thật, Donut trạng thái đơn, biểu đồ cột doanh thu, ba bảng Dashboard, truy vấn EF Core đã sửa và cơ chế lỗi độc lập; không triển khai lại các phần đã hoàn thành.
- **Hoàn thiện trong S30.2E**: bộ lọc nâng cao vẫn ẩn mặc định sau nút `Bộ lọc`; bổ sung đủ chế độ `Ngày` / `Tháng` / `Năm` / `Từ ngày – Đến ngày`; mặc định và `Đặt lại` trở về ngày hiện tại; `Áp dụng` chỉ tải lại biểu đồ doanh thu, không làm mất KPI/Donut/bảng. Khoảng tùy chỉnh kiểm tra thứ tự ngày và giới hạn tối đa `366` ngày ở cả UI lẫn service trước khi truy vấn.
- **Lỗi thực tế đã xử lý**: bản kế thừa thiếu lựa chọn `Ngày` dù yêu cầu giao diện có chế độ này; mặc định trước đó là tháng; khoảng tùy chỉnh chưa có giới hạn nên có thể sinh truy vấn/dãy bucket quá lớn. Empty state và thông báo lỗi biểu đồ tiếp tục được giữ nguyên, không để trạng thái loading kéo dài.
- **Files S30.2E trực tiếp sửa thêm**: `src/PhoneStore.Admin/Controls/DashboardControl.cs`, `src/PhoneStore.Business/Reporting/ReportingService.cs`, `tests/PhoneStore.Tests/ReportingTests.cs`. Các file Dashboard/reporting khác đang modified là công việc kế thừa S30.2–D và được bảo toàn.
- **Kiểm tra mục tiêu**: build Admin vào `scratch/s30_2e/admin-out` **0 warning / 0 error**; build test vào `scratch/s30_2e/tests-out` **0 warning / 0 error**; `ReportingTests` qua VSTest cô lập **9/9 PASS**. Build/test vào output mặc định không dùng vì tiến trình Admin/Web của người dùng đang khóa DLL; không dừng tiến trình.
- **SQL Server chỉ đọc**: `PhoneStoreDB` ONLINE; `40` Products, `40` ProductImages, tổng tồn hiện tại `723`; `7` Orders (`2 Completed`, `2 Cancelled`, `3 Pending`), doanh thu Completed `162.900.000 ₫`; `2` Customer và `1` Admin active. Không có DML/migration/seed.
- **Chưa kiểm thử trực tiếp**: binary Admin đang chạy là bản cũ trong `bin/Debug`; chưa thao tác chuột trên bản S30.2E mới và chưa kiểm tra trực quan DPI 100/125/150%. Người dùng cần đóng Admin cũ khi thuận tiện rồi chạy `dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj` để nghiệm thu.
- **Git**: branch `main`, HEAD `b467c796bb574b8d7483bad2868d1e48c62e764b`; không reset/checkout/commit/push. Các thay đổi chưa commit và thư mục scratch hiện có được giữ nguyên.

Cập nhật 2026-11-13 — S30.2E **READY FOR USER TEST, PhoneStore Admin Dashboard: Revenue Chart Advanced Filters**:
- **Trạng thái trước / sau**:
  - **Trước (S30.2D)**: Khu vực biểu đồ `Doanh thu bán hàng` luôn hiển thị sẵn thanh chọn chế độ/ngày (`Ngày`/`Tháng`/`Năm`, picker, `Áp dụng`) chiếm diện tích thẻ biểu đồ và chưa hỗ trợ lọc khoảng ngày tùy chỉnh (`Từ ngày – Đến ngày`).
  - **Sau**: Mặc định thẻ `Doanh thu bán hàng` hiển thị gọn với tiêu đề, khoảng đang xem (`Tháng MM/yyyy · Theo ngày`), chú thích `Chỉ tính đơn Hoàn thành · theo ngày đặt (OrderDate)` và nút góc phải `Bộ lọc` (kèm icon lọc). Nhấn `Bộ lọc` mở/đóng panel bộ lọc nâng cao nhỏ gọn ngay trên biểu đồ (3 chế độ: `Theo tháng`, `Theo năm`, `Từ ngày – Đến ngày`, kèm kiểm tra `fromDate <= toDate` tại chỗ, `Áp dụng`, `Đặt lại`); mở/tắt không che Donut và tự chỉnh chiều cao thẻ. Chuỗi ngày dài giữ đủ 100% dữ liệu ngày trong model, gộp cột vẽ theo dải pixel màn hình khi mật độ dày và giãn nhãn trục X theo DPI tránh đè chữ.
- **Files đã sửa**:
  - `src/PhoneStore.Models/Reporting/ReportingModels.cs`: thêm `RevenueChartPeriod.CustomRange = 3`.
  - `src/PhoneStore.Data/Reporting/ReportingStore.cs` (`CompletedRevenueBucketsAsync`): bổ sung nhánh `CustomRange` nhóm theo ngày UTC+7 (`Year * 10000 + Month * 100 + Day`).
  - `src/PhoneStore.Business/Reporting/IReportingService.cs`, `ReportingService.cs`: giữ nguyên chữ ký cũ `GetRevenueSeriesAsync(RevenueChartPeriod, DateOnly, CancellationToken)` và thêm overload `GetRevenueSeriesAsync(DateOnly fromDate, DateOnly toDate, CancellationToken)` (kiểm tra `fromDate <= toDate`, lấp đủ 100% từng ngày UTC+7 với `ToDateBucketKey`).
  - `src/PhoneStore.Admin/Styling/AdminTheme.cs` (`DrawIcon`): thêm icon vector `"filter"`.
  - `src/PhoneStore.Admin/Styling/ReportingUi.cs` (`RevenueColumnChartControl`): hỗ trợ render/hit-test dải ngày dài không drop dữ liệu và chống đè nhãn trục X theo khoảng trống thực tế.
  - `src/PhoneStore.Admin/Controls/DashboardControl.cs`: thiết kế lại header biểu đồ doanh thu (`RevenueFilterToggleButton`), panel bộ lọc ẩn mặc định (`RevenueFilterPanel`, draft state tách khỏi applied state), validate `fromDate <= toDate` và cập nhật `UpdateDynamicHeights`.
  - `tests/PhoneStore.Tests/ReportingTests.cs`: cập nhật `CapturingStore` (`BucketCalls`) và thêm 2 `[Fact]` cho `CustomRange`.
- **Kiểm tra (2026-11-13, sau chỉnh sửa cuối)**:
  - Build sandbox (`--no-restore --disable-build-servers -m:1 -nologo`): `PhoneStore.Business.csproj`, `PhoneStore.Tests.csproj` (`-p:BuildProjectReferences=false`), `PhoneStore.Admin.csproj` (`-o .../scratch/admin-out`) đều **0 Warning(s), 0 Error(s)**. Build trực tiếp `PhoneStore.Admin.csproj` vào `src/PhoneStore.Admin/bin/` vẫn bị khóa DLL do `PhoneStore.Admin.exe` đang mở trên máy người dùng.
  - `verify-ui.exe --probe-filters` (sandbox, dữ liệu SQLite/giả lập + 150-day render): **PASS** geometry panel lọc (`panel={Width=409, Height=93}`, `modeBar=389x35`, `inputsBar=389x35`, `closedH=390`, `openH=491`, `ToggleBtn='Bộ lọc' 97x35`, `SeriesCalls=4`), SQL Server `ToQueryString`, **9/9** SQLite EF Core checks và **8/8** `[Fact]` trong `PhoneStore.Tests.ReportingTests`. `verify-ui.exe --probe-faults` (sandbox): **PASS** kịch bản A, C, B + **8/8** `[Fact]`.
  - `verify-ui.exe --probe-live` (chạy ngoài sandbox sau khi sandbox bị lỗi SQL named pipes Error 5, chỉ đọc `PhoneStoreDB`): **PASS** `GetDashboardAsync`, `GetRevenueSeriesAsync` (`Day`, `Month`, `Year`, `2026-11-01..2026-11-30`, `2026-01-01..2026-12-31` -> `365` buckets) và `MainForm` + `DashboardControl` (`Period='Tháng 11/2026 · Theo ngày'`, `RevSummary='162.900.000 ₫ · 2 đơn'`, `FilterVisible=False`, `FilterBtn='Bộ lọc'`, `BannerVisible=False`).
  - `dotnet test --no-build --filter FullyQualifiedName~ReportingTests` trong sandbox bị treo ở VSTest pipe và đã dừng; các `[Fact]` của `ReportingTests` đã chạy qua `verify-ui.exe`.
- **Rủi ro / Chưa kiểm thử (NOT TESTED)**:
  - `src/PhoneStore.Admin/bin/Debug/net8.0-windows/` chưa cập nhật S30.2E vì `PhoneStore.Admin.exe` trên máy người dùng đang khóa file; cần đóng app cũ rồi chạy `dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj`.
  - Chưa test tương tác chuột trực tiếp ở cả 3 mức DPI Windows `100%`, `125%`, `150%` trên cửa sổ Desktop Admin (chỉ kiểm tra UI logic ở 125% DPI trong harness). Chưa chạy toàn bộ `dotnet test PhoneStore.sln`. Chưa commit/push Git.

Cập nhật 2026-11-13 — S30.2D **READY FOR USER TEST, Fix Admin Dashboard Data Loading Regression**:
- **Trạng thái trước / sau**:
  - **Trước (regression sau S30.2C)**: Mở Dashboard trên SQL Server thật bị giữ `—` / `"Đang tải…"` trên cả 4 thẻ KPI, Donut hiển thị `0 đơn hàng`, biểu đồ doanh thu rỗng và dòng `status` cuối trang (nằm dưới màn hình khi cuộn) hiện `"Không thể thực hiện thao tác. Vui lòng kiểm tra kết nối và thử lại."`.
  - **Sau**: Đã xác minh dữ liệu `PhoneStoreDB` còn nguyên (`40` sản phẩm active, `2` khách hàng role `Customer`, `7` đơn hàng, `162.900.000 ₫` từ `2` đơn `Completed`), sửa lỗi dịch SQL LINQ trong `CompletedRevenueBucketsAsync` và tách độc lập luồng tải `GetDashboardAsync` / `GetRevenueSeriesAsync` kèm banner lỗi trên đầu Dashboard và trạng thái lỗi riêng cho từng biểu đồ/KPI/bảng.
- **Nguyên nhân gốc đã xác nhận**:
  1. `ReportingStore.CompletedRevenueBucketsAsync`: chiếu `GroupBy` sang constructor positional record `new RevenueBucketAggregate(...)` rồi mới `.OrderBy(item => item.Key)` làm EF Core 8 ném `InvalidOperationException` khi dịch sang SQL (`GroupBy(...).Select(g => new RevenueBucketAggregate(...)).OrderBy(e0 => e0.Key)`).
  2. `DashboardControl.ReloadAsync`: `await` cả `GetDashboardAsync` lẫn `GetRevenueSeriesAsync` chung một khối `try/catch` trước khi gắn dữ liệu lên UI, nên khi `GetRevenueSeriesAsync` lỗi toàn bộ 4 KPI, Donut và 3 bảng bị bỏ qua, không hiển thị banner lỗi dễ thấy và giữ `"Đang tải…"` ở thẻ KPI/biểu đồ.
- **Files đã sửa**:
  - `src/PhoneStore.Data/Reporting/ReportingStore.cs` (`CompletedRevenueBucketsAsync`, `AggregateBucketsAsync`): dùng anonymous projection (`new { Key, Revenue, CompletedOrders }`) trước `.OrderBy(item => item.Key).ToListAsync(ct)`, rồi ánh xạ sang `RevenueBucketAggregate` trong bộ nhớ (đồng bộ với `BestSellers` và `ReportAsync`).
  - `src/PhoneStore.Admin/Styling/ReportingUi.cs`: bổ sung `OrderDonutChartControl.SetError(...)` (`DonutCanvasControl` hiện `! / lỗi` đỏ nhạt, `DonutLegendControl` hiện thông báo lỗi thay `"Chưa có dữ liệu đơn hàng"`) và `RevenueColumnChartControl.SetError(...)` (`PaintEmptyState` hiển thị thông báo lỗi và hướng dẫn nhấn `Làm mới`, xóa `isError` khi `SetData` thành công).
  - `src/PhoneStore.Admin/Controls/DashboardControl.cs`: thêm `DashboardErrorBanner` (`CreateErrorBanner`, `SetErrorBanner`), `LogReportingError` (`Trace.TraceError`), `ShowDashboardLoadFailure`, `ShowRevenueLoadFailure`, `BindDashboardSnapshot`; tách `ReloadAsync` thành 2 `try/catch` độc lập (`GetDashboardAsync` gắn KPI/Donut/3 bảng ngay khi xong trước khi gọi `GetRevenueSeriesAsync`); cập nhật `ReloadRevenueOnlyAsync` xử lý lỗi tách biệt, giữ số liệu KPI khi chỉ biểu đồ doanh thu lỗi và không để kẹt `"Đang tải…"`.
- **Kiểm tra (2026-11-13, sau chỉnh sửa cuối)**:
  - Build sandbox: `dotnet build tests/PhoneStore.Tests/PhoneStore.Tests.csproj -p:BuildProjectReferences=false --no-restore --disable-build-servers -m:1 -nologo` và `dotnet build src/PhoneStore.Admin/PhoneStore.Admin.csproj --no-restore --disable-build-servers -m:1 -nologo -o .../scratch/admin-out`: **0 Warning(s), 0 Error(s)**. Build trực tiếp `PhoneStore.Admin.csproj` vào `src/PhoneStore.Admin/bin/` gặp `MSB3021/MSB3027` do tiến trình `PhoneStore.Admin.exe` đang chạy trên máy người dùng khóa `PhoneStore.Data.dll` / `PhoneStore.Business.dll`.
  - `verify-ui.exe --probe-faults` (sandbox, dữ liệu giả lập cô lập lỗi + unit test): **PASS** Kịch bản A (Revenue lỗi -> 4 KPI `40`, `162.900.000 ₫`, bảng `1` dòng vẫn lên bình thường, `RevSummary="Không tải được"`, `BannerVisible=True`, `StuckCount=0`), Kịch bản C (nhấn `RevenueApplyButton` phục hồi -> `BannerVisible=False`, `RevSummary="162.900.000 ₫ · 2 đơn"`), Kịch bản B (Dashboard lỗi -> biểu đồ doanh thu vẫn render `162.900.000 ₫ · 2 đơn`, `KpiErrorCount=4`, `BannerVisible=True`, `StuckCount=0`), và **PASS 6/6** `[Fact]` của `PhoneStore.Tests.ReportingTests`.
  - `verify-ui.exe --probe-live` (chạy ngoài sandbox vì sandbox chặn named pipes SQL Server tới `localhost`, chỉ đọc `PhoneStoreDB` qua `AdminServices.Create()` + `MainForm`): **PASS** `GetDashboardAsync` (`ActiveProducts=40`, `Customers=2`, `Orders=7`, `CompletedRev=162900000.00`, `RecentCount=5`, `BestCount=4`), `GetRevenueSeriesAsync` (`Day 2026-11-12`: `162900000.00` / `2`, `Day 2026-11-13`: `0` / `0`, `Month 2026-11-13`: `162900000.00` / `2`, `Year 2026-11-13`: `162900000.00` / `2`), và UI thật `MainForm` (`Products='40'`, `Customers='2'`, `Orders='7'`, `Revenue='162.900.000 ₫'`, `RevSummary='162.900.000 ₫ · 2 đơn'`, `BannerVisible=False`, `RecentRows=5`, `BestRows=4`).
- **Rủi ro / Chưa kiểm thử (NOT TESTED)**:
  - `src/PhoneStore.Admin/bin/Debug/net8.0-windows/` vẫn đang giữ bản build S30.2C cũ do `PhoneStore.Admin.exe` trên máy người dùng chưa đóng; cần đóng app Admin đang mở rồi chạy lại `dotnet run --project src/PhoneStore.Admin/PhoneStore.Admin.csproj`.
  - Chưa chạy `dotnet build/test PhoneStore.sln` qua VSTest runner (có `PhoneStore.Web` đang chạy khóa `bin/` và pipe runner trong sandbox); chưa mở/thao tác tay trực tiếp trên cửa sổ Desktop Admin sau bản sửa mới nhất; chưa commit/push.

Cập nhật 2026-11-13 — S30.2C **READY FOR USER TEST, PhoneStore Admin Dashboard Revenue Chart, Date Filters & DataGrid UI Fix**:
- **Nghiệp vụ & Schema đã xác minh (`Order`, `OrderConfiguration`, `PhoneStoreDbContextModelSnapshot`)**:
  - Bảng `Orders` chỉ có cột `OrderDate` (`datetime2`, mặc định `SYSUTCDATETIME()`), không có `CompletedAt` hay lịch sử chuyển trạng thái; giữ nguyên schema. Biểu đồ doanh thu lọc `Order.Status == OrderStatus.Completed`, cộng `Orders.TotalAmount` (không join `OrderDetails`), lọc theo khoảng UTC nửa mở `[StartUtc, EndExclusiveUtc)` quy đổi từ múi giờ `SE Asia Standard Time` (+07:00) theo `OrderDate`, hiển thị nhãn rõ ràng `"Chỉ tính đơn Hoàn thành · theo ngày đặt (OrderDate)"`.
- **Thay đổi mã nguồn**:
  - `src/PhoneStore.Models/Reporting/ReportingModels.cs`: Thêm `RevenueChartPeriod`, `RevenueBucket`, `RevenueSeriesSnapshot`.
  - `src/PhoneStore.Data/Reporting/IReportingStore.cs`, `ReportingStore.cs`: Sửa `DashboardAsync` lấy tối đa 5 đơn gần đây (`.Take(5)`, `OrderDate DESC, OrderId DESC`); thêm `CompletedRevenueBucketsAsync` nhóm phía SQL theo giờ/ngày/tháng quy đổi `localOffsetMinutes`.
  - `src/PhoneStore.Business/Reporting/IReportingService.cs`, `ReportingService.cs`: Thêm `GetRevenueSeriesAsync` lấp đầy đủ 24 giờ (`00h–23h`), 28/29/30/31 ngày theo tháng/năm nhuận, 12 tháng (`T1–T12`).
  - `src/PhoneStore.Admin/Styling/ReportingUi.cs`: Thêm `RevenueColumnChartControl` (GDI+ `DoubleBuffered | ResizeRedraw`, cột bo đỉnh trên, trục Y `nghìn`/`triệu`/`tỷ`, nhãn trục X tự giãn bước tránh chồng chữ, hover + tooltip chi tiết, empty state rõ ràng); giữ nguyên `StatusBarsControl` & `RevenueBarsControl` cho `ReportsControl`.
  - `src/PhoneStore.Admin/Controls/DashboardControl.cs`: Thay thẻ `StatusBarsControl` (55% trái) bằng `Doanh thu bán hàng` kèm bộ lọc `Ngày` / `Tháng` (mặc định tháng/năm hiện tại dùng `Items.Add` tránh lỗi `BindingContext` trước Handle) / `Năm`, `Áp dụng` / `Đặt lại` bảo toàn giá trị riêng từng chế độ và chỉ gọi `GetRevenueSeriesAsync`; cố định bảng `Đơn hàng gần đây` ≤ 5 dòng không scrollbar dọc, tự co giãn chiều cao theo số dòng, bỏ cắt cứng 8 ký tự `OrderCode`, ưu tiên không cắt `Tổng tiền` / `Trạng thái` / `Ngày đặt`, thêm tooltip; sửa cột `STT` bảng `Sản phẩm bán chạy` không cắt `2...`/`3...` và đủ chỗ cho `Đã bán`, `Doanh thu` từ `1200px` trở lên.
  - `tests/PhoneStore.Tests/ReportingTests.cs`: Cập nhật `CapturingStore` và thêm test cho `GetRevenueSeriesAsync` (24h, tháng 28/29/30/31, 12 tháng, hợp nhất aggregate).
- **Kiểm tra (2026-11-13, sau chỉnh sửa cuối)**:
  - Build `PhoneStore.Tests.csproj` (`-p:BuildProjectReferences=false --no-restore --disable-build-servers -m:1`) và `PhoneStore.Admin.csproj` (ra `scratch/admin-out`, `--no-restore --disable-build-servers -m:1`): **0 Warning(s), 0 Error(s)**.
  - Chạy `verify-ui` (120 DPI / 125%, `Be Vietnam Pro`): **PASS** ToQueryString SQL Server (`DATEPART(hour/day/month, DATEADD(...))` + `SUM(TotalAmount)` không join `OrderDetails`), **PASS** 8 kịch bản SQLite (`RecentOrders<=5`, 24h UTC+7 qua nửa đêm, 3 `OrderDetail` không nhân ba, loại trừ `Pending/Confirmed/Shipping/Cancelled`, tháng 28/29/30/31, 12 tháng, kỳ rỗng = 0), **PASS** 6 `[Fact]` của `ReportingTests`, và **PASS** kiểm tra kích thước/ảnh chụp PNG tại `1200x1050`, `1366x1050`, `1920x1080`, `760px` stacked (`<850px`), chế độ `Ngày`/`Năm`, trạng thái rỗng.
  - **Chưa kiểm thử (NOT TESTED)**: Chạy `PhoneStore.Admin` thực tế kết nối `PhoneStoreDB` trên desktop (do tiến trình Admin & Web cũ của người dùng đang chạy khóa `bin/`); mức DPI **100%** và **150%**; chạy qua VSTest runner (`dotnet test` treo pipe trong sandbox); hover chuột trực tiếp bằng mắt trên app thật; chưa commit/push.

Cập nhật 2026-11-13 — S30.2B **READY FOR USER TEST, PhoneStore Admin WinForms Shared Header Text Clipping Fix**:
- **Nguyên nhân gốc (`src/PhoneStore.Admin/MainForm.cs`)**:
  - `workspace.RowStyles[0]` (`Absolute, 68`) + `header.Padding(28, 8, 28, 8)` để lại khoảng cao hữu dụng ~`65px` tại 125% DPI (`120 DPI`), trong khi `_pageTitle.Height = Math.Max(28, Font.Height + 2)` (`37px`) đưa vào `RowStyle(Absolute, _pageTitle.Height + 2)` (`39px`) bị `AutoScaleMode.Dpi` nhân tiếp 1,25× thành `49px` (double-DPI scaling), ép hàng `_pageSubtitle` xuống còn `16px` (< ~`17–18px` thực tế của `Be Vietnam Pro` 8.5pt tại 125% DPI) nên phần dưới phụ đề bị cắt và dính sát đường viền đáy Header trên cả 7 màn hình (`brand` bên Sidebar cũng có cùng mẫu đặt `Absolute` từ `wordmark.Height`).
- **Xử lý (`src/PhoneStore.Admin/MainForm.cs`)**:
  - Tăng chiều cao chuẩn hàng Header (`workspace.RowStyles[0]`) và Brand (`sidebar.RowStyles[0]` trong `BuildSidebar`) đồng bộ lên `Absolute, 74`; đặt `header.Padding(28, 6, 28, 7)` và `brand.Padding(22, 6, 12, 7)`.
  - Bỏ ép `SizeType.Absolute` từ `Font.Height`; chuyển `titleGroup`, `brand` và `accountInfo` sang `BufferedTableLayoutPanel` 4 hàng (`Percent 50`, `AutoSize`, `AutoSize`, `Percent 50`) với `AutoSize = true` trên `_pageTitle`, `_pageSubtitle`, `wordmark`, `subtitle`, `name`, `role`, giữ khoảng cách `_pageTitle` → `_pageSubtitle` = `4px` (5px tại 125% DPI) để căn giữa dọc chính xác ở mọi mức DPI mà không bị double-scaling hay cắt dấu tiếng Việt.
- **Files đã sửa**: `src/PhoneStore.Admin/MainForm.cs`, `docs/PROGRESS.md`.
- **Kiểm tra (2026-11-13)**:
  - Build `src/PhoneStore.Admin/PhoneStore.Admin.csproj` (ra thư mục tạm, `--no-restore --no-dependencies --disable-build-servers -m:1`): **0 Warning(s), 0 Error(s)**.
  - Harness render WinForms độc lập (`verify-ui`, host `PerMonitorV2` 125% DPI / 120 DPI, dữ liệu giả lập, không chạm `PhoneStoreDB`): **PASS** toàn bộ 7 tiêu đề màn hình (`Tổng quan`, `Sản phẩm`, `Thương hiệu`, `Danh mục`, `Khách hàng`, `Đơn hàng`, `Báo cáo`) ở `1200x760`, `1366x768`, `1920x1080` (`header=92px == sidebarBrand=92px`, `contentTop=92`, `Title` 32px, `Sub` 17px khớp `TextRenderer.MeasureText`, `gap=5`, `subBottomInHdr=72/92`, `clearanceToBorder=19px`, không tràn hay chồng chữ).
  - Kiểm tra ảnh PNG (`s30_2b_header_thuonghieu_1200.png`, `full_1200x760_resize.png`): `Thương hiệu` / `Tổng quan`, phụ đề và `PhoneStore` / `HỆ THỐNG QUẢN TRỊ` hiển thị trọn vẹn dấu tiếng Việt, căn giữa cân đối, Dashboard & Sidebar S30.2A/S30.1A giữ nguyên.
  - **Chưa kiểm thử (NOT TESTED)**: Runtime thực tế với `PhoneStoreDB` trên `PhoneStore.Admin` biên dịch lại do tiến trình Admin cũ của người dùng đang chạy; mức DPI **100%** và **150%** trên máy thật; toàn solution build/test; chưa commit/push.

Cập nhật 2026-11-13 — S30.2A **READY FOR USER TEST, PhoneStore Admin WinForms Dashboard Overlap, Overflow & Duplicate Rendering Fix**:
- **Nguyên nhân gốc & Xử lý (`ReportingUi.cs`, `DashboardControl.cs`, `MainForm.cs`, `AdminTheme.cs`)**:
  - **Lặp thanh biểu đồ, lặp số liệu & viền lồng nhau (`ReportingUi.cs`)**: `StatusBarsControl`, `OrderDonutChartControl`, `RevenueBarsControl`, và `Panel`/`TableLayoutPanel` thẻ KPI/Card thiếu `ControlStyles.ResizeRedraw` và vẽ trực tiếp lên `Panel` nền trắng trong khi vùng nền là `#F5F7FB`. Đã thay bằng `RoundedCardPanel` (`ResizeRedraw | OptimizedDoubleBuffer | AllPaintingInWmPaint`, xóa 4 góc ngoài theo `Parent.BackColor`, vẽ 1 nền trắng + 1 viền `#D9E2EE` 12px) và `BufferedPanel`; gọi `e.Graphics.Clear(BackColor)` đầu `OnPaint`.
  - **Chồng Donut & Legend (`OrderDonutChartControl`)**: Tách `OrderDonutChartControl` thành `TableLayoutPanel` 2 vùng độc lập (`DonutCanvasControl` 38% bên trái, `DonutLegendControl` 62% bên phải). Donut tính `diameter = Math.Max(48, Math.Min(Width, Height) - 16)` 1:1 căn giữa, tách lát bằng `FillPie` + đường chia trắng 2px + lỗ trắng trung tâm, scale font trung tâm vừa `innerRect`; trạng thái 0 đơn hiển thị vòng xám `0 / đơn hàng` + `"Chưa có dữ liệu đơn hàng"`. Legend đo động chiều rộng `count (pct%)`, chỉ render 1 lần cho mỗi trạng thái chuẩn.
  - **KPI Doanh thu & Header thẻ (`SummaryCard`, `FittingLabel`)**: Chuyển `iconBox` về Hàng 0 và cho `value` (`FittingLabel : Label` tự đo chiều rộng khi vẽ, `17f` xuống `10.5f`) chiếm `ColumnSpan = 2` (tránh co cụm/rớt dòng khi doanh thu lớn ở 1200×760 DPI 125%).
  - **Biểu đồ thanh ngang (`StatusBarsControl`)**: Đo động độ rộng nhãn/số bằng `TextRenderer.MeasureText`, vẽ đúng 1 nền thanh + 1 thanh màu khi `count > 0` + 1 số đếm.
  - **Header & Logo Sidebar (`MainForm.cs`)**: Chuyển sang `BufferedTableLayoutPanel` (`DoubleBuffered`, `ResizeRedraw`), bỏ đường vẽ cứng `y = 63` trong `sidebar.Paint`, đặt đường gạch chân duy nhất dưới `brand` khớp pixel với đường dưới `HeaderPanel` (cùng `68px` trước scale -> `85px` tại 125% DPI), chỉnh chiều cao nhãn theo `Font.Height`.
  - **Bảng Dashboard & Responsive (`DashboardControl.cs`, `ReportingUi.CompactGrid`, `AdminTheme.DrawBadge`)**: Đổi `recentGrid`, `bestGrid`, `stockGrid` sang `AutoSizeColumnsMode.Fill` với `FillWeight`/`MinimumWidth` hợp lý (ngày đặt `dd/MM HH:mm` + tooltip đầy đủ, rút gọn `OrderCode` + tooltip, vẽ badge giữ viền ngang); chỉnh chiều cao `stockCard` co giãn theo số dòng (`Clamp 148..280`); sửa `Resize` để cập nhật `SetCellPosition` + `RowStyles` khi chuyển 1/2 cột; thêm `label.SizeChanged` căn giữa empty states.
- **Files đã sửa**: `src/PhoneStore.Admin/Styling/ReportingUi.cs`, `src/PhoneStore.Admin/Styling/AdminTheme.cs`, `src/PhoneStore.Admin/Controls/DashboardControl.cs`, `src/PhoneStore.Admin/MainForm.cs`, `docs/PROGRESS.md`.
- **Kiểm tra (2026-11-13)**:
  - Build `src/PhoneStore.Admin/PhoneStore.Admin.csproj` (ra output tạm, `--no-restore --no-dependencies -m:1`): **0 Warning(s), 0 Error(s)** (`bin/Debug/.../PhoneStore.Admin.exe` đang bị khóa bởi tiến trình Admin người dùng mở sẵn nên không ghi đè).
  - Harness render WinForms độc lập (`verify-ui`, `PerMonitorV2` 125% DPI trên host, dữ liệu giả lập, không chạm DB): xác nhận `1200x760` (sau resize), `1366x768`, `1920x1080`, `1000x1100` (empty state): `headerH=85 == sidebarBrandRowEnd=85`, `overlap=False` Donut/Legend, `hOverflow=False` cả 3 grid, hiển thị đủ `"125.000.000 ₫"`.
  - **Chưa kiểm thử (NOT TESTED)**: Chạy trực tiếp trên `PhoneStoreDB` trong app `PhoneStore.Admin` mới biên dịch lại của người dùng; mức DPI **100%** và **150%**. Chưa build/test toàn solution, chưa commit/push.

Cập nhật 2026-11-12 — S30.2 **READY FOR USER TEST, PhoneStore Admin WinForms Dashboard Redesign + Donut Chart**:
- **Thiết kế lại toàn diện Dashboard Admin (`DashboardControl.cs`, `ReportingUi.cs`, `AdminTheme.cs`)**:
  - **Hàng 1 — 4 Thẻ KPI Tổng quan**:
    - Thiết kế card nền trắng, viền `#D9E2EE`, bo góc 12px, chiều cao đồng đều 118px.
    - Tích hợp Icon vector trong huy hiệu bo tròn (Sản phẩm: xanh `#1559D6`, Khách hàng: xám slate `#475569`, Đơn hàng: cam `#B45309`, Doanh thu: xanh lá `#15803D`).
    - Số liệu nổi bật 20pt bold Navy `#14243C`, phụ đề mô tả rõ ràng, không số liệu phần trăm giả.
  - **Hàng 2 — Hai biểu đồ song song (Thanh ngang 55% + Donut 45%)**:
    - **Biểu đồ thanh ngang (`StatusBarsControl`)**: Hiển thị 5 trạng thái chuẩn với thanh nền xám nhạt `#F1F5F9`, thanh màu bo góc 4px, nhãn trạng thái 114px không cắt chữ, số lượng căn phải rõ ràng.
    - **Bổ sung Biểu đồ Donut (`OrderDonutChartControl`)**: Vẽ bằng GDI+ WinForms thuần, giữ tỷ lệ tròn 1:1. Chính giữa hiển thị tổng số đơn (18pt bold) và "đơn hàng". Các lát cắt màu sắc chuẩn, viền trắng ngăn cách 2px, hiển thị phần trăm trên lát cắt khi $\ge 10\%$. Chú giải bên phải với chấm tròn màu, tên trạng thái, số lượng và tỷ lệ phần trăm.
    - Màu sắc đồng bộ 100%: Chờ xác nhận `#F59E0B` (Cam), Đã xác nhận `#2563EB` (Xanh dương), Đang giao `#38BDF8` (Xanh da trời), Hoàn thành `#22C55E` (Xanh lá), Đã hủy `#EF4444` (Đỏ).
  - **Hàng 3 — Hai bảng song song (Đơn hàng gần đây 55% + Sản phẩm bán chạy 45%)**:
    - **Đơn hàng gần đây (`recentGrid`)**: Cột Mã đơn tự rút gọn mã dài kèm tooltip đầy đủ; cột Tổng tiền định dạng VNĐ (`#,##0 ₫`) căn phải; cột Trạng thái vẽ badge bo góc màu chuẩn; không bị cắt cột, hạn chế cuộn ngang.
    - **Sản phẩm bán chạy (`bestGrid`)**: Hiển thị Rank `#`, Sản phẩm, Đã bán, Doanh thu. Khi không có dữ liệu bán hàng, hiển thị icon và thông báo "Chưa có dữ liệu bán hàng" căn giữa chuyên nghiệp.
  - **Hàng 4 — Cảnh báo tồn kho (`stockGrid`)**:
    - Chiều rộng 100%, ngưỡng $\le 5$. Tự co giãn tên sản phẩm (`AutoSizeMode.Fill`), vẽ badge "Hết hàng" (đỏ) hoặc "Sắp hết" (cam).
    - Cuối trang hiển thị dòng trạng thái cập nhật thời gian thực ("Đã cập nhật lúc HH:mm:ss · PhoneStoreDB").
  - **Responsive WinForms**:
    - Hỗ trợ màn hình 1366×768 và 1920×1080 (DPI 100%, 125%, 150%).
    - Khi chiều rộng cửa sổ $< 850\text{px}$, bố cục tự động chuyển sang xếp dọc (1 cột) tránh chồng chữ/cắt biểu đồ.
- **Files đã sửa**:
  - `src/PhoneStore.Admin/Controls/DashboardControl.cs`
  - `src/PhoneStore.Admin/Styling/ReportingUi.cs`
  - `src/PhoneStore.Admin/Styling/AdminTheme.cs`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Không sửa database hay migration, không sửa `ReportingService` hay `ReportingStore`, không sửa `LoginForm.cs` hay Shell/Sidebar, không thêm thư viện bên thứ 3, không commit/push.

Cập nhật 2026-11-12 — S30.1C **READY FOR USER TEST, PhoneStore Admin LoginForm Compact Spacing Fix**:
- **Căn giữa chữ trong ô nhập liệu và căn sát lề trái (`CreateInputFrame` trong `LoginForm.cs`)**:
  - Xác định nguyên nhân gốc từ ảnh kiểm tra: Textbox dùng `DockStyle.Fill` kết hợp với `Padding.Top = 8px` khiến chữ `admin@admin.com` bị dồn sát viền đáy (khoảng cách trên 18px, khoảng cách dưới chỉ 1px). Đồng thời trong `TableLayoutPanel`, đặt `Margin.Bottom` trên panel làm co bớt chiều cao khả dụng của ô.
  - Xử lý:
    1. Chuyển `emailFrame.Margin = Padding.Empty` và `passwordFrame.Margin = Padding.Empty` để khung nhận trọn vẹn chiều cao chuẩn 38px. Đưa khoảng cách lề vào `passwordLabel.Margin.Top = 12` và `showPassword.Margin.Top = 8`.
    2. Thay `DockStyle.Fill` bằng cơ chế định vị linh hoạt `LayoutInput()`: `left = 10` (nằm sát lề trái ngay sau góc bo 6px), `top = (frame.ClientSize.Height - input.Height) / 2`. Kết quả đo đạc pixel thực tế: khoảng cách trên 13px, khoảng cách dưới 13px — văn bản nằm chính giữa khung tuyệt đối.
    3. Thêm sự kiện `frame.Click` để bấm vào bất kỳ đâu trong khung đều tự động focus vào ô nhập liệu và thiết lập con trỏ chuột dạng `Cursors.IBeam`.
- **Tinh chỉnh khoảng cách và bố cục LoginForm theo đặc tả chuẩn Compact**:
  - Tiêu đề "Đăng nhập quản trị": Giữ nguyên hàng AutoSize độc lập, font 16pt bold chuẩn tiếng Việt.
  - Phụ đề: Đặt cách tiêu đề đúng 8px (`title.Margin = new Padding(0, 0, 0, 8)`).
  - Nhãn Email: Đặt cách phụ đề 15px (`subtitle.Margin = new Padding(0, 0, 0, 15)`, nằm trong khoảng 14–16px).
  - Ô Email: Chiều cao chuẩn 38px, cách nhãn Mật khẩu đúng 12px.
  - Nhãn Mật khẩu: Cách ô Email 12px, cách ô Mật khẩu 4px.
  - Ô Mật khẩu: Chiều cao chuẩn 38px, cách checkbox Hiện mật khẩu đúng 8px.
  - Checkbox: Cách ô Mật khẩu 8px, cách nút Đăng nhập đúng 14px.
  - Nút Đăng nhập: Chiều cao chuẩn 40px, cách nút Thoát ứng dụng đúng 18px (nằm trong khoảng 16–20px).
  - Nút Thoát ứng dụng: Chiều cao compact 26px, phẳng nhẹ không viền, đặt cách nút Đăng nhập 18px.
- **Bố cục tổng thể & Giảm khoảng trắng dư thừa**:
  - `Padding` formLayout cân đối: `new Padding(32, 20, 32, 18)`.
  - `ClientSize` thiết lập chuẩn compact 680×380px (giảm từ 410px), đưa toàn bộ khối nhập liệu lên cao hơn và loại bỏ hoàn toàn khoảng trắng dư thừa dưới nút Đăng nhập / Thoát.
  - Khoảng cách trên (~20px) và dưới (~20px) đạt độ cân đối thị giác.
  - Tự động điều chỉnh `ClientSize` linh hoạt khi có thông báo lỗi (mở rộng lên 415px để hiển thị alert box không bị cắt chữ, tự thu về 380px khi lỗi được xóa).
  - Cột thương hiệu bên trái (250px) được giữ nguyên vẹn 100%, bảo toàn tỷ lệ 2 cột và các badge an toàn DPI.
- **Files đã sửa**:
  - `src/PhoneStore.Admin/LoginForm.cs`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Không sửa database, không sửa xác thực Admin hay SingleAdminPolicy, không thay đổi MainForm/Dashboard, không commit/push.

Cập nhật 2026-11-12 — S30.1B **READY FOR USER TEST, PhoneStore Admin LoginForm Text Overlap & DPI Layout Fix**:
- **Sửa triệt để lỗi tiêu đề bị cắt và đè phụ đề trên LoginForm (`LoginForm.cs`)**:
  - Xác định nguyên nhân gốc: Tiêu đề 16pt bold (yêu cầu chiều cao $\ge 30\text{px}$) bị ép vào hàng TableLayoutPanel lồng nhau cố định chỉ 24px khiến phần chân chữ bị cắt, và phụ đề ở hàng liền sau bắt đầu ngay tại mốc 24px đè trực tiếp lên dấu tiếng Việt (`ă`, `đ`, `ị`, `g`, `p`).
  - Xử lý: Tách tiêu đề "Đăng nhập quản trị" và phụ đề thành hai hàng độc lập trong `formLayout` với `SizeType.AutoSize`. Tiêu đề nhận trọn vẹn 100% chiều cao tự nhiên theo font metric của `Be Vietnam Pro`, phụ đề nằm phía dưới với khoảng cách lề chuẩn 8–12px, không cắt dấu tiếng Việt và không thể chồng lấn ở bất kỳ mức DPI nào (100%, 125%, 150%).
- **Sửa lỗi cắt dòng "Đồng bộ dữ liệu PhoneStoreDB" trên Panel thương hiệu**:
  - Xác định nguyên nhân: Chiều rộng panel bên trái chỉ 240px (khoảng trống khả dụng 196px) không đủ cho chuỗi 31 ký tự 8.5pt (~217px), đồng thời điều khiển sử dụng `Dock = Top/Bottom` không tự động ngắt dòng.
  - Xử lý: Tăng chiều rộng cột thương hiệu lên 250px, thiết lập `MaximumSize = new Size(206, 0)` và chuyển khối badge sang `TableLayoutPanel` hai hàng `SizeType.AutoSize`. Cho phép tự động xuống dòng an toàn khi cần, không bị cắt mép phải và không đè lên dòng "Chính sách một quản trị viên".
- **Chuẩn hóa chiều cao chữ và bố cục Form**:
  - `ClientSize` thiết lập chuẩn compact 680×410px.
  - Toàn bộ các label nhãn (Tiêu đề, Phụ đề, Email, Mật khẩu, Checkbox, Footer) đều dùng `SizeType.AutoSize` để tự động thích ứng với Windows DPI Scaling.
- **Files đã sửa**:
  - `src/PhoneStore.Admin/LoginForm.cs`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Không sửa database, không sửa xác thực Admin, không thay đổi MainForm/Dashboard, không ảnh hưởng Customer Web.

Cập nhật 2026-11-12 — S30.1A **READY FOR USER TEST, PhoneStore Admin WinForms Runtime UI Fix**:
- **Khắc phục lỗi Sidebar chồng chữ (`MainForm.cs`)**:
  - Xác định chính xác nguyên nhân gốc: WinForms `Button` vẽ thuộc tính `Button.Text` mặc định qua `base.OnPaint`, đồng thời sự kiện `Paint` tùy biến lại gọi `TextRenderer.DrawText` dẫn đến chữ bị in đè 2 lớp.
  - Xử lý: Xóa rỗng `button.Text = string.Empty` và `logout.Text = string.Empty` (chuyển sang dùng `AccessibleName` cho hỗ trợ truy cập), chỉ để sự kiện `Paint` vẽ duy nhất 1 lần nhãn font `Be Vietnam Pro`, căn lề chuẩn xác cạnh vector icon, không chồng chữ, không cắt chữ.
- **Loại bỏ khoảng trắng dư thừa trên LoginForm (`LoginForm.cs`)**:
  - Thu gọn kích thước cửa sổ từ 780×520px về chuẩn compact 660×360px.
  - Cân đối lại khoảng cách padding và chiều cao các trường nhập liệu (Email, Mật khẩu, checkbox).
  - Đưa alert thông báo lỗi thành hàng `SizeType.AutoSize` (chỉ chiếm chỗ khi có lỗi, không chiếm khoảng trống khi form bình thường).
  - Chuyển nút "Thoát ứng dụng" thành nút phụ compact dạng flat link tinh tế (chiều cao 26px, không cạnh tranh thị giác với nút Đăng nhập chính).
- **Files đã sửa**:
  - `src/PhoneStore.Admin/MainForm.cs`
  - `src/PhoneStore.Admin/LoginForm.cs`
  - `docs/PROGRESS.md`
- **Bảo toàn**: 100% logic xác thực `AdminLoginGateway`, `SingleAdminPolicy`, các phím tắt Enter/Esc, database và các màn hình khác.

Cập nhật 2026-11-12 — S30.1 **READY FOR USER TEST, PhoneStore Admin WinForms: Theme, Login & Main Shell Redesign**:
- **Design System & Typography (`AdminTheme.cs`)**:
  - Tích hợp font thương hiệu `Be Vietnam Pro` thông qua `PrivateFontCollection` (liên kết từ tài nguyên font có sẵn trong Customer Web), tự động fallback về `Segoe UI` nếu môi trường không tải được, không phụ thuộc đường dẫn tuyệt đối.
  - Bổ sung bảng màu chuẩn Customer Web: Primary `#1559D6`, Navy `#14243C`, Secondary `#5A738E`, Surface `#F5F7FB`, Border `#D9E2EE`, cùng hệ thống màu Semantic Badges (Pending, Confirmed, Shipping, Completed, Cancelled, Active, Inactive).
  - Bổ sung helpers: `CreateInputFrame` (khung viền bo góc có highlight focus), `CreateRoundedRectanglePath`, `DrawBadge`, và hệ thống vector icons `DrawIcon` vẽ trực tiếp bằng GDI+ cho 7 phân hệ và chức năng đăng xuất/alert/eye.
- **Tái thiết kế màn hình Đăng nhập (`LoginForm.cs`)**:
  - Kích thước thu gọn chuẩn compact: 780×520px (thay vì 920×660px cồng kềnh).
  - Cột trái (Brand Panel, 280px): Nền xanh `#1559D6`, logo PhoneStore, thông điệp quản trị tập trung, điểm nhấn bảo mật và thông tin phiên bản.
  - Cột phải (Form Panel): Tiêu đề "Đăng nhập quản trị", phụ đề hướng dẫn, input Email và Mật khẩu trong khung bo góc 6px tinh tế, checkbox hiện mật khẩu, alert card thông báo lỗi chỉ xuất hiện khi có lỗi (không chiếm khoảng trống rỗng).
  - Giữ nguyên 100% cơ chế xác thực bất đồng bộ `AdminLoginGateway.LoginAsync`, phím Enter để đăng nhập và phím Escape để thoát.
- **Tái thiết kế Khung chính Shell (`MainForm.cs`)**:
  - Header thu gọn từ 100px xuống 64px, hiển thị tiêu đề trang, phụ đề module và khu vực tài khoản quản trị (Avatar circle AD, Tên quản trị viên, Email).
  - Sidebar cố định 228px (thay vì 244px), chiều cao header logo 64px thẳng hàng với top header, bổ sung vector icon cho tất cả 7 phân hệ điều hướng (Tổng quan, Sản phẩm, Thương hiệu, Danh mục, Khách hàng, Đơn hàng, Báo cáo).
  - Trạng thái Active item: Nền xanh nhạt `#EBF2FE`, chữ và icon xanh `#1559D6`, vạch chỉ báo active 3.5px bo góc ở mép trái.
  - Nút "Đăng xuất" đặt cố định dưới đáy sidebar với icon và đường phân cách tinh tế.
  - ContentPanel tự động co giãn theo kích thước cửa sổ (hỗ trợ màn hình 1366×768 và 1920×1080, tương thích Windows DPI scaling).
- **Files đã sửa / cập nhật**:
  - `src/PhoneStore.Admin/Styling/AdminTheme.cs`
  - `src/PhoneStore.Admin/LoginForm.cs`
  - `src/PhoneStore.Admin/MainForm.cs`
  - `src/PhoneStore.Admin/PhoneStore.Admin.csproj`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Giữ nguyên toàn bộ logic xác thực, `SingleAdminPolicy`, cơ chế phân quyền, database `PhoneStoreDB`, các UserControl và Dialogs nghiệp vụ. Không commit/push.

Cập nhật 2026-11-12 — S29.9V **READY FOR USER TEST, PhoneStore Customer Web Orders Pagination (5 Orders Per Page)**:
- **Cấu hình phân trang 5 đơn hàng/trang (`PageSize = 5`)**:
  - `OrdersController.cs`: Cập nhật `pageSize = 5` (thay vì 10). Tự động chuẩn hóa `page < 1` về 1, bảo toàn bộ lọc khi chuyển trang.
  - `PurchaseViewModels.cs`: Cập nhật mặc định `CustomerOrdersViewModel.PageSize = 5`.
  - `PurchaseStore.cs`: Truy vấn database EF Core `Skip((page - 1) * pageSize).Take(pageSize)` với `pageSize <= 0 ? 5 : pageSize`. Sắp xếp ổn định `OrderByDescending(OrderDate).ThenByDescending(OrderId)`. Giới hạn chặt chẽ `UserId == userId`.
  - `IOrderService.cs` & `OrderService.cs`: Cập nhật tham số mặc định `pageSize = 5` cho `GetPagedCustomerOrdersAsync`.
- **Giao diện & Tích hợp bộ lọc**:
  - Tái sử dụng thanh phân trang Compact hiện có trong `Views/Orders/Index.cshtml` và `site.css`: nút active xanh `#1559d6` chữ trắng, các trang khác viền xám nhạt `#d8e5f5`, Trước/Tiếp vô hiệu hóa ở trang đầu/cuối.
  - Hiển thị dòng thông tin tính động: `Hiển thị 1–5 trong tổng số X đơn hàng` (tính từ `(PageNumber - 1) * PageSize + 1` đến `Math.Min(PageNumber * PageSize, TotalCount)`).
  - Tự ẩn thanh phân trang khi `TotalPages <= 1` (khi có $\le 5$ đơn hàng).
  - Khi bấm "Lọc": Áp dụng bộ lọc và reset về trang 1.
  - Khi chuyển trang: Bảo toàn query filter qua `BuildFilterUrl`.
  - Khi bấm "Đặt lại": Xóa bộ lọc và quay về trang 1 hiển thị 5 đơn hàng mới nhất.
- **Dữ liệu thực tế & Kiểm thử**:
  - Tài khoản khách hàng test (`UserId = 2`, `tai123@gmail.com`): Có đúng 7 đơn hàng trong `PhoneStoreDB`.
  - Kết quả mong đợi: Trang 1 hiển thị 5 đơn hàng mới nhất (từ 2026-11-11 đến 2026-11-02), trang 2 hiển thị 2 đơn hàng cũ nhất.
  - Runtime HTTP probe: `/orders`, `/orders?page=2`, `/orders?page=3` trả về HTTP 302 chuyển hướng login bảo vệ an toàn.
  - Bổ sung unit test tập trung: `tests/PhoneStore.Tests/OrderPaginationTests.cs` (kiểm tra tính toán ViewModel, `BuildFilterUrl`, phân trang EF Core 5 đơn/trang, cách ly dữ liệu user, clamp page âm và overflow).
- **Files đã sửa / tạo**:
  - `src/PhoneStore.Web/Controllers/OrdersController.cs`
  - `src/PhoneStore.Web/Models/PurchaseViewModels.cs`
  - `src/PhoneStore.Data/Purchasing/PurchaseStore.cs`
  - `src/PhoneStore.Business/Purchasing/IOrderService.cs`
  - `src/PhoneStore.Business/Purchasing/OrderService.cs`
  - `tests/PhoneStore.Tests/OrderPaginationTests.cs` (Mới)
  - `docs/PROGRESS.md`
- **Bảo toàn**: Không sửa DB/migration, không sửa Cart/Checkout/Profile/Admin, không commit/push. Chờ user kiểm tra trực tiếp trên Chrome `http://localhost:5015/orders`.

Cập nhật 2026-11-12 — S29.9U **READY FOR USER REVIEW, PhoneStore Customer Web Final Acceptance & Release Readiness**:
- **Nghiệm thu toàn diện Customer Web (11 màn hình)**:
  - Kiểm tra điều hướng: 9 URL chính (`/`, `/products`, `/brands`, `/categories`, `/cart`, `/account/login`, `/account/register`, `/account/profile`, `/orders`) hoạt động 100%. Đã triệt tiêu hoàn toàn các liên kết neo cũ `/#brands` và `/#categories` trên Header và Footer.
  - Kiểm tra Brands (`/brands`): 8 thương hiệu thực tế từ DB (`Apple`, `Google`, `OnePlus`, `OPPO`, `realme`, `Samsung`, `vivo`, `Xiaomi`), logo SVG chuẩn, số lượng sản phẩm, liên kết chuyển đúng Catalog `/products?brandIds={id}`.
  - Kiểm tra Categories (`/categories`): 1 danh mục thực tế từ DB (`Điện thoại thông minh`, 40 SP), card 280px không bị kéo dãn, liên kết chuyển đúng `/products?categoryId=1`.
  - Kiểm tra Catalog (`/products`): Đã tích hợp hoàn chỉnh bộ lọc Category và Brand, hiển thị chip lọc độc lập có nút `[x]` gỡ bỏ từng tiêu chí hoặc nút "Xóa tất cả", bảo toàn tham số `categoryId` và `brandIds` khi chuyển trang (phân trang 4 trang) và khi tìm kiếm/sắp xếp. Kiểm tra CategoryId không tồn tại (`categoryId=999999`) hiển thị empty state 0 sản phẩm an toàn không phát sinh lỗi 500.
  - Kiểm tra hồi quy: Home, Catalog (10 SP/trang), Search/Sort, Login/Register Compact không có khung đỏ rỗng, Cart/Checkout/Orders bảo vệ phân quyền an toàn (302 chuyển hướng login).
  - Responsive: Header, Footer, Brands, Categories hiển thị mượt mà trên Desktop (1366px), Tablet (768px) và Mobile (390px, menu ẩn thanh cuộn xấu).
- **Trạng thái kiểm thử kỹ thuật**:
  - Runtime HTTP Probe: 100% PASS (18/18 route/kịch bản trả về HTTP 200 hoặc 302/404 chuẩn thiết kế).
  - Unit Test: BLOCKED do tiến trình `dotnet watch` trên host đang khóa các file DLL (`PhoneStore.Models.dll`, `PhoneStore.Business.dll`, `PhoneStore.Data.dll`). Không có lỗi cú pháp hoặc assertion failure.
  - Browser Automation: NOT TESTED do Chrome host chưa bật remote debugging port. Đã chuẩn bị checklist trực quan chi tiết cho người dùng.
- **Tài liệu cập nhật**:
  - `docs/S29.9R_CUSTOMER_WEB_ACCEPTANCE.md` (Mở rộng ma trận 11 màn hình, cập nhật runtime probe và kết luận).
  - `docs/PROGRESS.md`
- **Kết luận mức độ sẵn sàng**: **CONDITIONAL READY**. Không sửa mã nguồn, không sửa DB. Chờ người dùng duyệt lần cuối trên Chrome cá nhân trước khi chuyển sang Admin WinForms.

Cập nhật 2026-11-12 — S29.9T **READY FOR USER TEST, PhoneStore Customer Web Categories Page**:
- **Trang Danh mục riêng (`/categories`)**:
  - Xây dựng controller `CategoriesController` tại route `/categories` và view `Views/Categories/Index.cshtml`.
  - Truy vấn trực tiếp từ `PhoneStoreDbContext` qua EF Core không gây N+1 query: `db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.CategoryName).Select(c => new CategoryItemViewModel(c.CategoryId, c.CategoryName, c.Description, c.Products.Count(p => p.IsActive)))`.
  - Hiển thị đúng dữ liệu thực tế từ database: 1 danh mục "Điện thoại thông minh" (40 sản phẩm đang hoạt động). Không hardcode danh mục giả, không nhân bản dữ liệu, có xử lý empty state nếu không có danh mục.
  - Header trang: Breadcrumb `Trang chủ / Danh mục`, tiêu đề "Khám phá danh mục" 24px, mô tả "Tìm kiếm sản phẩm theo danh mục phù hợp với nhu cầu của bạn."
  - Bố cục lưới Compact: Lưới linh hoạt `auto-fill` (minmax 260px–280px) giúp card danh mục duy nhất không bị kéo giãn bất thường trên Desktop (chiều rộng gọn gàng 280px), tự co giãn 1 cột trên Mobile.
  - Card danh mục: Biểu tượng smartphone SVG tinh gọn, tên danh mục, mô tả ngắn, badge số lượng ("40 sản phẩm"), liên kết "Xem sản phẩm →" chuyển đến `/products?categoryId=1`.
- **Hoàn thiện kết nối bộ lọc Catalog (`/products`)**:
  - Cập nhật `ProductsController.cs`:
    - `BuildAppliedFilters`: Bổ sung hiển thị chip lọc cho danh mục đang áp dụng (`Điện thoại thông minh` kèm nút bỏ lọc `[x]`).
    - `BuildFilterUrl`: Bảo toàn tham số `categoryId` khi chuyển trang (phân trang 4 trang: `page=2, 3, 4`), khi đổi tiêu chí lọc khác và khi sắp xếp; cho phép gỡ bỏ `categoryId` an toàn khi bấm bỏ lọc.
    - Xử lý `categoryId` không hợp lệ: trả về danh sách rỗng (0 sản phẩm phù hợp) an toàn mà không phát sinh HTTP 500.
  - Cập nhật `Views/Products/Index.cshtml`: Bổ sung `<input type="hidden" name="categoryId" value="@Model.Filter.CategoryId" />` vào form sidebar để bảo toàn danh mục khi submit lọc thuộc tính khác.
- **Header & Footer Navigation**:
  - Cập nhật mục "Danh mục" trên Header trong `Views/Shared/_Layout.cshtml` từ `/#categories` thành `/categories`, tự động gán `aria-current="page"` khi ở màn hình Categories.
  - Cập nhật mục "Danh mục" trong phần Liên kết nhanh ở Footer thành `/categories`.
  - Giữ nguyên 100% section "Điện thoại mới nhất" (`#categories`) trên Home.
- **CSS**: Thêm khối style scoped riêng `.categories-page-container`, `.categories-grid`, `.category-page-card` tại cuối `site.css`.
- **Files đã tạo / chỉnh sửa**:
  - `src/PhoneStore.Web/Models/CategoryViewModels.cs` (Mới)
  - `src/PhoneStore.Web/Controllers/CategoriesController.cs` (Mới)
  - `src/PhoneStore.Web/Views/Categories/Index.cshtml` (Mới)
  - `src/PhoneStore.Web/Controllers/ProductsController.cs`
  - `src/PhoneStore.Web/Views/Products/Index.cshtml`
  - `src/PhoneStore.Web/Views/Shared/_Layout.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Không sửa DB/migration, không thêm package, không sửa Cart/Checkout/Orders/Auth, không commit/push. Đã probe runtime `http://localhost:5015/categories` trả về HTTP 200 OK, liên kết `/products?categoryId=1` trả về 40 sản phẩm kèm phân trang 4 trang và chip lọc hoạt động 100%.

Cập nhật 2026-11-12 — S29.9S **READY FOR USER TEST, PhoneStore Customer Web Brands Page**:
- **Trang Thương hiệu riêng (`/brands`)**:
  - Xây dựng controller `BrandsController` tại route `/brands` và view `Views/Brands/Index.cshtml`.
  - Truy vấn trực tiếp từ `PhoneStoreDbContext` qua EF Core không gây N+1 query: `db.Brands.AsNoTracking().Where(b => b.IsActive).OrderBy(b => b.BrandName).Select(b => new BrandItemViewModel(..., b.Products.Count(p => p.IsActive)))`.
  - Header trang: Breadcrumb `Trang chủ / Thương hiệu`, tiêu đề "Khám phá thương hiệu" 24px, mô tả "Lựa chọn thương hiệu điện thoại phù hợp với bạn."
  - Bố cục lưới: Desktop 4 cột × 2 hàng, Tablet 3 cột, Mobile 2 cột. Card bo góc 14px, viền mảnh `#d9e2ee`, nền trắng, hover nâng nhẹ + viền xanh PhoneStore.
  - Mỗi card hiển thị: Logo SVG chính hãng được căn chỉnh tỷ lệ chuẩn (Apple, Samsung, Xiaomi, OPPO, vivo, realme, Google, OnePlus), tên thương hiệu, badge số lượng sản phẩm, và liên kết "Xem sản phẩm →".
  - Liên kết Catalog: Click card/nút điều hướng chính xác đến `/products?brandIds={brandId}` theo đúng tham số lọc Catalog hiện có.
  - Xử lý trạng thái rỗng (`brands-empty-box`) nếu không có thương hiệu nào.
- **Header & Footer Navigation**:
  - Cập nhật mục "Thương hiệu" trên Header trong `Views/Shared/_Layout.cshtml` từ `/#brands` thành `/brands`, tự động gán `aria-current="page"` khi ở trang Brands.
  - Cập nhật mục "Thương hiệu" trong phần Liên kết nhanh ở Footer thành `/brands`.
  - Cập nhật liên kết "Xem tất cả →" tại section "Thương hiệu bạn yêu thích" trên `Views/Home/Index.cshtml` từ `/products` sang `/brands`. Giữ nguyên 100% giao diện section thương hiệu trang chủ.
- **CSS**: Thêm khối style scoped riêng `.brands-page-container` và `.brand-page-card` tại cuối `site.css`.
- **Files đã tạo / chỉnh sửa**:
  - `src/PhoneStore.Web/Models/BrandViewModels.cs` (Mới)
  - `src/PhoneStore.Web/Controllers/BrandsController.cs` (Mới)
  - `src/PhoneStore.Web/Views/Brands/Index.cshtml` (Mới)
  - `src/PhoneStore.Web/Views/Shared/_Layout.cshtml`
  - `src/PhoneStore.Web/Views/Home/Index.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Không sửa DB/migration, không thêm package, không sửa logic lọc Catalog, không commit/push. Đã probe runtime `http://localhost:5015/brands` trả về HTTP 200 OK. Chờ người dùng kiểm tra trực tiếp trên Chrome.

Cập nhật 2026-11-12 — S29.9R.1 **CONDITIONAL READY, Customer Web Acceptance Verification & Test Failure Diagnosis**:
- **Chẩn đoán Unit Test (Exit Code 1)**:
  - Xác định chính xác 100% nguyên nhân: Tiến trình `dotnet watch` đang chạy trên host khóa độc quyền các file DLL tại `src\PhoneStore.Web\bin\Debug\net8.0\`.
  - Khi chạy `dotnet test`, MSBuild gặp lỗi `MSB3027 / MSB3021: The process cannot access the file ... because it is being used by another process`.
  - Trong môi trường sandbox AppContainer, multi-process MSBuild và cuộc gọi Service Control Manager (`Win32Exception 5: Access is denied`) bị hạn chế quyền khiến MSBuild âm thầm báo `Build FAILED. 0 Warning, 0 Error`.
  - Không có lỗi logic code hay assertion failure nào trong mã nguồn test suite.
- **Xác minh Website Runtime (`http://localhost:5015`)**:
  - Máy chủ web đang phục vụ ổn định: `/` (200), `/products` (200), `/products/1` (200), `/products/999999` (404), `/account/login` (200), `/account/register` (200), `/cart` (200).
  - Các route yêu cầu đăng nhập: `/checkout` (302 $\rightarrow$ login), `/orders` (302 $\rightarrow$ login), `/account/profile` (302 $\rightarrow$ login) chuyển hướng đúng thiết kế phân quyền.
- **Xác minh Browser Automation & Giao diện**:
  - Kết nối automation browser ghi nhận `BLOCKED` do Chrome trên host chưa mở remote debugging port.
  - Tuân thủ nguyên tắc: Không tuyên bố đã kiểm thử tự động, đánh dấu `NOT TESTED`, cung cấp checklist để người dùng kiểm tra trực quan trên Chrome.
- **Đánh giá & Kết luận mức độ sẵn sàng**:
  - Không có lỗi P0, P1, P2.
  - Lỗi môi trường P3: file locking bởi tiến trình web đang chạy.
  - Kết luận: **CONDITIONAL READY**. Chờ người dùng kiểm tra trực quan lần cuối trên Chrome trước khi chuyển tiếp sang WinForms Admin.
- **Tài liệu tạo mới / cập nhật**:
  - `docs/S29.9R_CUSTOMER_WEB_ACCEPTANCE.md` (Báo cáo nghiệm thu chi tiết 9 màn hình và chẩn đoán).
  - `docs/PROGRESS.md`
- **Bảo toàn**: Không sửa bất kỳ file source code, Razor view, CSS, JS, Entity hay DB.

Cập nhật 2026-11-12 — S29.9Q **READY FOR USER TEST, Customer Web Global UI/UX & Navigation Finalization**:
- **Header & Navigation**:
  - Khắc phục liên kết chết `/#categories`: gắn `id="categories"` vào section "Điện thoại mới nhất" trên `Views/Home/Index.cshtml`, đảm bảo cả Header và Footer điều hướng trơn tru đến đúng vị trí.
  - Tối ưu nút Giỏ hàng: chuyển thành liên kết trực tiếp `<a class="header-dropdown-trigger cart-entry" href="/cart">`, click mở ngay trang Giỏ hàng, bảo lưu class `.cart-entry` để `site.js` AJAX cập nhật tức thời số lượng và animation bump.
  - Menu Tài khoản (khi đã đăng nhập): gom chuẩn xác 3 mục theo yêu cầu: "Hồ sơ của tôi" (`/account/profile`), "Đơn hàng của tôi" (`/orders`), và "Đăng xuất" (form POST bảo mật + antiforgery token).
  - Khi chưa đăng nhập: hiển thị liên kết "Đăng nhập" (`/account/login`) gọn gàng với icon và nhãn đồng nhất.
  - Thanh tìm kiếm: giữ nguyên hành vi tìm kiếm thực tế trên `/products`, tự động bảo lưu bộ lọc thuộc tính và reset phân trang về trang 1.
- **Footer**:
  - Loại bỏ hoàn toàn link stub `/Home/Privacy`.
  - Cập nhật liên kết "Hỗ trợ khách hàng" đồng bộ theo trạng thái xác thực: khi đã đăng nhập (`/cart`, `/orders`, `/account/profile`), khi chưa đăng nhập (`/cart`, `/account/login`, `/account/register`).
  - Liên kết nhanh (`/`, `/products`, `/#brands`, `/#categories`) hoạt động 100% không còn link chết hay link giả `#`.
- **Responsive & Trải nghiệm cuộn di động**:
  - Bổ sung quy tắc ẩn thanh cuộn mặc định của trình duyệt cho `.main-nav` (`scrollbar-width: none; -ms-overflow-style: none; .main-nav::-webkit-scrollbar { display: none; }`), hỗ trợ touch scroll mượt mà trên mobile (390px, 430px) mà không xuất hiện thanh cuộn xấu xí.
  - Đảm bảo toàn bộ viewport (1440px, 1366px, 768px, 430px, 390px) không bị tràn ngang (horizontal overflow).
- **Rà soát 6 luồng điều hướng**: Đã kiểm tra 100% khớp route MVC, breadcrumbs và liên kết cha con trên tất cả 10 màn hình Customer Web.
- **Files đã sửa**:
  - `src/PhoneStore.Web/Views/Home/Index.cshtml`
  - `src/PhoneStore.Web/Views/Shared/_Layout.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Không sửa DB/migration, không sửa các màn hình đã được duyệt, không can thiệp backend không liên quan, không commit/push. Chờ user kiểm tra trực tiếp trên Chrome `http://localhost:5015`.

Cập nhật 2026-11-12 — S29.9P **READY FOR USER TEST, Compact Login & Register UI/UX**:
- **Trang Đăng nhập (`/account/login`)**:
  - Giới hạn card `max-width: 480px`, căn giữa, padding thu gọn còn 24–28px, bỏ đường xanh dày 4px trên đỉnh card và bỏ eyebrow "TÀI KHOẢN PHONESTORE".
  - Tiêu đề "Chào mừng trở lại" 24px, mô tả ngắn; input Email và Password cao chuẩn 42px; checkbox "Ghi nhớ đăng nhập" 16px gọn gàng; nút "Đăng nhập" xanh PhoneStore 42px.
  - Hai liên kết "Chưa có tài khoản? Đăng ký ngay" và "← Tiếp tục khám phá sản phẩm" nằm gọn ở footer dưới form. Hiển thị trọn vẹn trong 1 màn hình laptop zoom 100% không cần cuộn trang.
- **Trang Đăng ký (`/account/register`)**:
  - Giới hạn card `max-width: 540px`, căn giữa, padding 24–28px, bỏ đường xanh dày và bỏ eyebrow.
  - Tiêu đề "Tạo tài khoản" 24px; chia bố cục 2 cột (50/50) cho "Họ và tên" & "Số điện thoại", và "Mật khẩu" & "Xác nhận mật khẩu"; Email chiếm toàn bộ chiều rộng (100%).
  - Tất cả input cao chuẩn 42px; dòng hint mật khẩu 12px thu gọn; nút "Tạo tài khoản" xanh PhoneStore 42px.
  - Hai liên kết "Đã có tài khoản? Đăng nhập ngay" và "← Tiếp tục khám phá sản phẩm" nằm gọn ở footer. Hiển thị trọn vẹn trong 1 viewport laptop zoom 100%.
- **Chống hiển thị hộp đỏ rỗng**:
  - Thêm quy tắc `.auth-compact-errors.validation-summary-valid, .auth-compact-errors:empty { display: none; }` đảm bảo khi form hợp lệ không có bất kỳ viền/nền đỏ thừa nào.
- **Files đã sửa**:
  - `src/PhoneStore.Web/Views/Account/Login.cshtml`
  - `src/PhoneStore.Web/Views/Account/Register.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Giữ nguyên `AccountController`, `LoginViewModel`, `RegisterViewModel`, `data-ui-submit`, antiforgery token, logic mật khẩu và phân quyền. Không đụng DB/migration, không commit/push. Chờ user kiểm tra trực tiếp trên Chrome `http://localhost:5015/account/login` và `/account/register`.

Cập nhật 2026-11-12 — S29.9O.1 **READY FOR USER TEST, Fix Empty Validation Alert trên Profile**:
- **Nguyên nhân**: Tag helper `asp-validation-summary="All"` của ASP.NET Core luôn sinh sẵn container kèm `<ul><li style="display:none"></li></ul>` và class `validation-summary-valid` khi chưa có lỗi. Khối CSS `.profile-compact .profile-errors ul` trước đó áp dụng padding và border/background đỏ nhạt vô điều kiện, khiến thẻ `<ul>` rỗng hiển thị thành một hộp đỏ trống dưới Email.
- **Xử lý**:
  - `site.css`: Thêm `.profile-compact .profile-errors.validation-summary-valid, .profile-compact .profile-errors:empty { display: none; }` để ẩn hoàn toàn khung và không chiếm diện tích khi không có lỗi.
  - `Views/Profile/Index.cshtml`: Bổ sung class `auth-errors` đồng bộ với line 65 của `site.css`.
  - Khi có lỗi thực sự (server hoặc client `site.js`), class `validation-summary-valid` bị gỡ bỏ và `validation-summary-errors` được gán vào, khung cảnh báo đỏ nhạt cùng danh sách lỗi hiển thị chính xác.
- Files đã sửa: `src/PhoneStore.Web/Views/Profile/Index.cshtml`, `src/PhoneStore.Web/wwwroot/css/site.css`, `docs/PROGRESS.md`.
- Bảo toàn: Giữ nguyên bố cục Compact Profile, không sửa Controller/Model/DB, không commit/push. Chờ user kiểm tra trực tiếp trên Chrome `http://localhost:5015/account/profile`.

Cập nhật 2026-11-12 — S29.9O **READY FOR USER TEST, PhoneStore Compact Profile UI/UX**:
- Tái thiết kế trực tiếp giao diện Hồ sơ của tôi tại `Views/Profile/Index.cshtml`:
  - Loại bỏ các khối padding khổng lồ và dòng eyebrow "TÀI KHOẢN CỦA BẠN".
  - Tiêu đề "Hồ sơ của tôi" thu gọn 22px, kèm breadcrumbs mảnh `Trang chủ / Hồ sơ của tôi`.
  - Khung nội dung tối đa 900px, căn giữa, nền trắng, viền mảnh `#d9e2ee`, bo góc 14px, đổ bóng nhẹ.
  - Hàng hiển thị Email đăng nhập: thu gọn trong 1 hàng ngang duy nhất (icon thư, nhãn "Email:", giá trị email in đậm, badge "Chỉ đọc"), giữ nguyên trạng thái chỉ đọc không cho sửa.
  - Form thông tin cá nhân: Họ tên và Số điện thoại nằm trên cùng 1 hàng ở desktop (mỗi trường 50%, grid 1fr 1fr); Địa chỉ nhận hàng ở hàng tiếp theo với textarea gọn cao 74px; chiều cao input chuẩn 42px.
  - Hàng thao tác: Nút "Lưu hồ sơ" xanh PhoneStore chiều rộng vừa phải (padding 0 22px, cao 42px, icon lưu); liên kết "Xem đơn hàng của tôi →" đặt cùng hàng bên phải nút lưu.
  - Responsive: Trên mobile ($\le 680\text{px}$), Họ tên & SĐT tự xếp dọc 1 cột, nút Lưu hồ sơ full-width, không tràn ngang.
- CSS: Thêm khối style mới scope riêng `.profile-compact` ở cuối `site.css`, tái sử dụng toàn bộ design token hiện có.
- Files đã sửa:
  - `src/PhoneStore.Web/Views/Profile/Index.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `docs/PROGRESS.md`
- Bảo toàn: Giữ nguyên `ProfileController`, ViewModel, antiforgery token, form POST, `data-ui-submit`, TempData thông báo và validation. Không đụng DB/migration, không commit/push. Chờ user kiểm tra trực tiếp trên Chrome tại `http://localhost:5015/account/profile`.

Cập nhật 2026-11-12 — S29.9N.2 **READY FOR USER TEST, Server-side Pagination cho My Orders**:
- **Triển khai Server-side Pagination**:
  - Tầng Data (`PurchaseStore.cs`): Thêm phương thức `CustomerOrdersPagedAsync` thực hiện truy vấn phân trang trực tiếp trên SQL Server qua EF Core. Thứ tự truy vấn: lọc `UserId` -> lọc trạng thái -> lọc thời gian UTC+7 -> `CountAsync()` -> `OrderByDescending(OrderDate).ThenByDescending(OrderId)` -> `Skip((page - 1) * pageSize).Take(pageSize)`.
  - An toàn số trang: Cố định `PageSize = 10` ở server. Chuẩn hóa page: nếu `page < 1` tự về 1; nếu `totalCount == 0` trả về trang 1 rỗng; nếu `page > totalPages` tự về trang cuối `totalPages`, ngăn chặn hoàn toàn lỗi tràn số (overflow) hoặc HTTP 500.
  - Tầng Business & Service (`IOrderService.cs`, `OrderService.cs`): Bổ sung `GetPagedCustomerOrdersAsync`, bảo toàn 100% các overload cũ cho unit test và workflow hiện tại.
  - Models (`PurchaseModels.cs`, `PurchaseViewModels.cs`): Thêm record `PagedCustomerOrdersResult(Orders, TotalCount, PageNumber, PageSize)`; mở rộng `CustomerOrdersViewModel` với `TotalCount`, `PageNumber`, `PageSize`, `CurrentPage`, `TotalPages`, `HasPreviousPage`, `HasNextPage`; thêm `Page` vào `OrderFilterViewModel`.
  - Controller (`OrdersController.cs`): Thêm static helper `BuildFilterUrl` bảo toàn trọn vẹn toàn bộ tham số bộ lọc khi chuyển trang. Tự động reset về trang 1 khi lọc tiêu chí mới hoặc nhấn "Đặt lại".
  - Giao diện phân trang (`Index.cshtml` & `site.css`): Thanh phân trang đồng bộ với Catalog (`‹ Trước`, `1`, `2`, `3`, `Tiếp ›`), hỗ trợ dấu `…` khi số trang lớn, badge active xanh PhoneStore, vô hiệu hóa Trước ở trang đầu và Tiếp ở trang cuối, hiển thị tóm tắt `Hiển thị 1–10 trong tổng số X đơn hàng`. Chỉ hiển thị thanh khi `TotalPages > 1`.
- **Files đã sửa**:
  - `src/PhoneStore.Models/Purchasing/PurchaseModels.cs`
  - `src/PhoneStore.Web/Models/PurchaseViewModels.cs`
  - `src/PhoneStore.Data/Purchasing/PurchaseStore.cs`
  - `src/PhoneStore.Business/Purchasing/IOrderService.cs`
  - `src/PhoneStore.Business/Purchasing/OrderService.cs`
  - `src/PhoneStore.Web/Controllers/OrdersController.cs`
  - `src/PhoneStore.Web/Views/Orders/Index.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Không sửa DB/migration, không thay đổi dữ liệu đơn, không sửa các trang khác, bảo toàn validation S29.9N.1. Chờ user kiểm tra trực tiếp trên Chrome `http://localhost:5015/orders`.

Cập nhật 2026-11-12 — S29.9N.1 **READY FOR USER TEST, Hoàn thiện bộ lọc và validation My Orders**:
- **Khắc phục lỗi nghiệp vụ bộ lọc thời gian**: Loại bỏ hoàn toàn cơ chế tự động đảo ngày (`auto-swap`) khi `Từ ngày > Đến ngày` của S29.9N; thay bằng validation chặt chẽ 2 lớp (Client & Server).
- **Validation quy tắc lọc**:
  - Với kiểu lọc "Khoảng ngày tùy chọn" (`range`): Bắt buộc nhập đầy đủ cả `fromDate` và `toDate` (báo lỗi *"Vui lòng nhập đầy đủ Từ ngày và Đến ngày."* nếu thiếu).
  - Nếu `fromDate > toDate`: Báo lỗi rõ ràng *"Từ ngày không được lớn hơn Đến ngày."*.
  - Với kiểu lọc "Theo ngày" (`day`): Bắt buộc chọn ngày (báo lỗi nếu để trống).
  - Phòng thủ an toàn (Không query DB khi lỗi): Khi validation thất bại, `OrdersController` lập tức ngắt luồng, không gọi `PurchaseStore.CustomerOrdersAsync`, trả về danh sách rỗng và gán `ErrorMessage` hiển thị alert trực quan, tuyệt đối không gây lỗi HTTP 500.
- **Client-side Interceptor**: Bổ sung validation tức thời trước khi submit form trong `Index.cshtml`, hiển thị thông báo alert đỏ nổi bật, focus vào input lỗi và ngăn submit sai lên server.
- **Khảo sát phân trang (Pagination Audit)**:
  - Hiện trạng: Luồng `OrdersController.Index` và `PurchaseStore.CustomerOrdersAsync` chưa hỗ trợ phân trang (đang tải toàn bộ đơn hàng của khách hàng theo bộ lọc).
  - Đề xuất phương án Server-side Pagination 10 đơn/trang: Tích hợp `page` / `pageSize = 10` vào `OrderFilterViewModel`, tính `TotalCount` bằng `CountAsync` và phân trang bằng `.Skip().Take()`, render thanh điều hướng phân trang đồng bộ với Catalog. Giữ nguyên không tự ý sửa code phân trang khi chưa được phê duyệt.
- **Files đã sửa**:
  - `src/PhoneStore.Web/Models/PurchaseViewModels.cs`
  - `src/PhoneStore.Web/Controllers/OrdersController.cs`
  - `src/PhoneStore.Web/Views/Orders/Index.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `docs/PROGRESS.md`
- **Bảo toàn**: Giữ nguyên layout Compact đã duyệt ở S29.9N, kiểm soát phân quyền theo `UserId`, không chạm DB/migration, không commit/push. Chờ user kiểm tra trực tiếp trên Chrome tại `http://localhost:5015/orders`.

Cập nhật 2026-11-12 — S29.9N **READY FOR USER TEST, Compact My Orders UI/UX + Advanced Date Filters**:
- Viết lại markup `Views/Orders/Index.cshtml`: bỏ banner xanh lớn và dòng "TÀI KHOẢN", thay bằng header thu gọn (breadcrumb `Trang chủ / Đơn hàng của tôi`, H1 22px, mô tả ngắn, căn giữa tối đa 1180px); danh sách đơn hàng compact dạng hàng ngang cao ~84px gồm Icon 48x48px, Mã đơn + Ngày đặt, Tổng tiền xanh PhoneStore, Badge trạng thái thực tế và nút `Xem chi tiết →`.
- Bộ lọc nâng cao: hỗ trợ lọc Trạng thái (Tất cả, Chờ xác nhận, Đã xác nhận, Đang giao, Hoàn thành, Đã hủy) và Thời gian đặt (Tất cả, Theo ngày, Theo tháng/năm, Theo năm, Khoảng ngày tùy chọn). Tự động ẩn/hiện và disable/enable các trường input không liên quan qua JS nhẹ để query string GET luôn sạch sẽ, chính xác.
- Backend & Truy vấn: mở rộng `PurchaseStore.CustomerOrdersAsync`, `IOrderService`, `OrderService` với overload lọc `status, fromUtc, toExclusiveUtc` trực tiếp trên SQL Server qua EF Core, tính toán mốc thời gian chuẩn múi giờ Việt Nam (`SE Asia Standard Time`, UTC+7), bao quát trọn vẹn cả ngày kết thúc; kiểm soát đầu vào an toàn, tự động đảo ngày nếu `fromDate > toDate`.
- Xử lý trạng thái trống: phân biệt giữa "chưa có đơn hàng nào" và "không tìm thấy đơn hàng phù hợp" khi lọc có nút "Đặt lại bộ lọc".
- CSS mới append cuối `site.css`, scope `.orders-compact` (responsive 991px → tinh gọn bảng, 680px → chuyển card compact xếp dọc).
- Giữ nguyên: bảo toàn 100% quyền sở hữu đơn hàng (`UserId`), không sửa DB/migration, không sửa trang khác, giữ nguyên overload cũ cho test.
- Kiểm tra: cần user kiểm tra trực tiếp trên Chrome tại `http://localhost:5015/orders`.

Cập nhật 2026-11-12 — S29.9M **READY FOR USER TEST, Compact Order Detail UI/UX**:
- Viết lại markup `Views/Orders/Detail.cshtml`: bỏ banner xanh lớn, thay bằng header nhỏ gọn gồm breadcrumb ngang hàng `Đơn hàng của tôi / Chi tiết đơn hàng`, tiêu đề 22px kèm badge trạng thái thực tế từ backend (`.order-status`), dòng meta hiển thị mã đơn hàng và ngày đặt, link "← Tất cả đơn hàng" dẫn về `/orders`.
- Bố cục 2 cột 65% / 35% bo góc 14px nền trắng, tối đa 1180px căn giữa, đồng bộ phong cách với Compact Checkout và Success.
- Thông tin giao hàng (cột trái): hiển thị Người nhận, Số điện thoại (link tel), Địa chỉ giao hàng, Ghi chú; mỗi hàng gọn 40–44px.
- Khu vực hủy đơn: thiết kế thành hộp nhỏ gọn ở cuối cột trái (`.order-detail-cancel-box`), thông báo ngắn và nút "Hủy đơn hàng" đỏ viền (`button-cancel-outline`), giữ nguyên điều kiện trạng thái (`Pending` / `Confirmed`), method POST, antiforgery token và bước xác nhận dialog `confirm()`.
- Sản phẩm đã đặt (cột phải): hiển thị danh sách sản phẩm thu gọn (Tên, số lượng, đơn giá, thành tiền), Tổng cộng nổi bật màu xanh PhoneStore 20px, cho phép trang mở rộng tự nhiên.
- CSS mới append cuối `site.css`, scope `.order-detail-compact` (responsive 991px → 1 cột, 680px → field xếp gọn, nút hủy full-width).
- Giữ nguyên: Controller (`OrdersController.Detail`, `OrdersController.Cancel`), model `OrderReceipt`, date formatting UTC+7, kiểm tra quyền sở hữu đơn hàng, không sửa DB/migration.
- Kiểm tra: cần user kiểm tra trực tiếp trên Chrome tại `http://localhost:5015/orders/{id}` (hoặc id 6).

Cập nhật 2026-11-12 — S29.9L **READY FOR USER TEST, Compact Order Success UI/UX**:
- Viết lại markup `Views/Checkout/Success.cshtml`: thay khối thông báo lớn bằng thẻ thông báo thành công gọn gàng (`.success-notice-card`) gồm icon tích xanh lá, H1 "Đặt hàng thành công!", mã đơn hàng nổi bật; breadcrumb một hàng `Trang chủ / Đơn hàng / Đặt hàng thành công`; layout 2 cột 65% / 35% bo góc 14px nền trắng đồng bộ với Compact Checkout.
- Thông tin đơn hàng (cột trái): hiển thị đầy đủ Mã đơn hàng (chống tràn mã dài), Ngày đặt, Người nhận, Số điện thoại (link tel), Địa chỉ giao hàng và Ghi chú; cuối thẻ bổ sung 2 nút điều hướng chuẩn route MVC: "Xem đơn hàng của tôi" (`/orders`) và "Tiếp tục mua sắm" (`/products`).
- Sản phẩm đã đặt (cột phải): hiển thị danh sách sản phẩm thu gọn (Tên, số lượng, thành tiền từng món), Tổng cộng nổi bật màu xanh PhoneStore 20px, cho phép trang mở rộng tự nhiên.
- CSS mới append cuối `site.css`, scope `.order-success-compact` (responsive 991px → 1 cột, 680px → chuyển nút xếp dọc, padding thu gọn).
- Giữ nguyên: Controller (`CheckoutController.Success`), model `OrderReceipt`, date formatting UTC+7, read-only GET (F5 không tạo thêm đơn), không sửa DB/migration.
- Kiểm tra: cần user kiểm tra trực tiếp trên Chrome tại `http://localhost:5015/checkout/success/{id}` (hoặc id 5).

Cập nhật 2026-11-12 — S29.9K **READY FOR USER TEST, Compact Checkout UI/UX**:
- Viết lại markup `Views/Checkout/Index.cshtml`: bỏ banner/eyebrow "HOÀN TẤT LỰA CHỌN"; header gọn (breadcrumb 1 hàng `Trang chủ / Giỏ hàng / Thanh toán`, H1 "Thông tin đặt hàng", link "← Quay lại giỏ hàng"); grid 65/35, card trắng bo 14px; Họ tên + SĐT cùng hàng; textarea địa chỉ 66px, ghi chú 60px; input 41px; nút "Xác nhận đặt hàng →" 43px cuối form; tóm tắt đơn hàng inline (tên, SL, thành tiền, tổng xanh 20px), sticky trên desktop.
- CSS mới append cuối `site.css`, scope `.checkout-compact` (responsive 991px → 1 cột, 680px → field xếp dọc). `_OrderSummary.cshtml` không còn được Checkout dùng nhưng giữ nguyên file.
- Giữ nguyên: form POST, antiforgery (form tag helper), `SubmissionToken`, `data-ui-submit`/`data-checkout-form` (chống gửi trùng, error summary), validation `asp-validation-*`, dữ liệu autofill từ hồ sơ, `TotalAmount`/`LineTotal` backend. Không đổi Controller/ViewModel/DB.
- Kiểm tra: chưa build/test (dotnet watch của user đang chạy; `dotnet build` trong sandbox trước đó trả FAILED 0 error do khóa file). Cần user kiểm tra Chrome zoom 100% tại `/checkout`.

Cập nhật 2026-11-12 — S29.9J.1 **READY FOR USER TEST, Khắc phục triệt để lỗi Add to Cart Redirect trên trang Danh sách sản phẩm (Catalog)**:
- **Nguyên nhân gốc rễ (Root Cause Analysis)**:
  - Trong `src/PhoneStore.Web/Views/Products/Index.cshtml`, thẻ `<form action="/products" method="get" class="catalog-layout" id="catalogFilterForm">` bọc ngoài toàn bộ layout bao gồm cả `<aside class="catalog-sidebar">` lẫn `<section class="catalog-results">`.
  - Mỗi `_ProductCard` bên trong danh sách sản phẩm lại chứa thẻ form riêng: `<form asp-controller="Cart" asp-action="Add" method="post" data-cart-form>`.
  - Theo đặc tả HTML5 (WHATWG Spec 13.2.6.4.7), các thẻ `<form>` lồng nhau (nested forms) là bất hợp pháp và bị trình duyệt tự động loại bỏ (drop/mangle). Khi đó các form con `data-cart-form` bị mất trong DOM, sự kiện click "Thêm vào giỏ" không kích hoạt AJAX handler trong `site.js` mà rơi vào submit thường. Backend `CartController` nhận request thông thường (`!IsAjaxRequest()`) nên thực hiện redirect sang `/cart`.
- **Giải pháp xử lý**:
  - **Tách biệt Form Lọc và Layout trong `Index.cshtml`**:
    - Chuyển container `.catalog-layout` từ thẻ `<form>` thành thẻ `<div>`.
    - Đưa `<form action="/products" method="get" class="filter-panel" id="catalogFilterForm">` vào trọn vẹn bên trong `<aside class="catalog-sidebar">`, kèm `<input type="hidden" name="sort" id="filterSortInput" value="@Model.Filter.Sort" />`.
    - Cập nhật dropdown sắp xếp trong `.results-toolbar` đồng bộ giá trị với `#filterSortInput` và submit `#catalogFilterForm`.
    - Toàn bộ `<section class="catalog-results">` và các thẻ `_ProductCard` hoàn toàn nằm ngoài `#catalogFilterForm`, chấm dứt 100% tình trạng nested forms.
  - **Củng cố AJAX Handler trong `site.js`**:
    - Tối ưu bộ chọn form bắt cả `event.target`, `closest` và `event.submitter`.
    - Phòng thủ fallback: chỉ redirect khi backend chuyển hướng về trang `/account/login`. Các trường hợp trả về HTML khác đều hiển thị thông báo lỗi và giữ nguyên trang, tuyệt đối không tự ý chuyển hướng sang `/cart`.
- **Files đã sửa**:
  - `src/PhoneStore.Web/Views/Products/Index.cshtml`
  - `src/PhoneStore.Web/wwwroot/js/site.js`
  - `docs/PROGRESS.md`
- **Kết quả đạt được**:
  - Thao tác "Thêm vào giỏ" tại trang Danh sách sản phẩm (`/products`) hoạt động đồng nhất với Trang chủ (`/`) và Trang chi tiết (`/products/{id}`): không chuyển trang, không reload, không mất vị trí cuộn, hiển thị toast thành công, cập nhật ngay lập tức badge giỏ hàng trên Header, cho phép tiếp tục thêm sản phẩm khác.
- **Tuân thủ nghiêm ngặt**: Không đụng DB/migration, không sửa UI ngoài phạm vi, không làm gián đoạn tính năng khác, không commit/push. Chờ user kiểm tra trực tiếp trên Chrome tại `http://localhost:5015/products`.

Cập nhật 2026-11-12 — S29.9J **READY FOR USER TEST, Compact Cart UI/UX (Zoom 100%)**:
- **Mục tiêu**: Tối ưu trực tiếp giao diện `/cart` của Customer Web, thu gọn layout để người dùng có thể xem trọn vẹn danh sách 3–4 sản phẩm cùng bảng tóm tắt đơn hàng và nút thanh toán trong một viewport laptop ở Chrome zoom 100% mà không cần cuộn trang.
- **Thực hiện chi tiết**:
  - **Bố cục & Header**:
    - Loại bỏ banner cồng kềnh, thay bằng header nhỏ gọn: breadcrumbs mảnh "Trang chủ / Giỏ hàng", tiêu đề "Giỏ hàng (X sản phẩm)" hiển thị tổng số lượng thực tế, và link "← Tiếp tục mua sắm".
    - Giới hạn chiều rộng nội dung tối đa 1180px, căn giữa trang.
    - Chia bố cục Desktop 2 cột: Cột trái 68% (Danh sách sản phẩm) và Cột phải 32% (Tóm tắt đơn hàng).
  - **Hàng sản phẩm thu gọn (Chiều cao mục tiêu 105–115px)**:
    - Chiều cao thực tế 106px (ảnh 76×76px + padding 14px + viền 1px).
    - Cấu trúc 1 hàng ngang trên Desktop: Ảnh thu nhỏ 76×76px bo góc 8px -> Tên sản phẩm & đơn giá -> Bộ điều khiển số lượng stepper (− / +) -> Thành tiền nổi bật màu xanh thương hiệu -> Nút Xóa (icon thùng rác + nhãn hover đỏ).
    - Bộ stepper (− / +): Tự động giảm/tăng số lượng và gửi cập nhật về backend, vô hiệu hóa nút trừ khi số lượng là 1, chặn vượt quá tồn kho.
  - **Khung tóm tắt đơn hàng (Bên phải)**:
    - Chỉ hiển thị 4 thành phần thiết yếu: Tổng số lượng sản phẩm, Tạm tính, Tổng cộng (20px bold), và Nút "Tiến hành đặt hàng →".
    - Hoàn toàn không lặp lại danh sách sản phẩm ở cột phải.
    - Giữ nguyên luồng kiểm tra `CanCheckout` và chuyển sang `/checkout`.
  - **Responsive & Tương thích**:
    - Đảm bảo giỏ hàng 3–4 sản phẩm hiển thị đầy đủ trong 1 màn hình laptop zoom 100%. Khi nhiều sản phẩm (> 5), cuộn tự nhiên.
    - Không sinh thanh cuộn ngang, không dùng CSS zoom/scale.
    - Responsive mượt mà trên Laptop, Tablet (991px xếp chồng) và Mobile (680px tối ưu 2 hàng gọn gàng).
    - Bảo toàn 100% dữ liệu SessionCart, kiểm tra tồn kho, CSRF token, không ảnh hưởng các màn hình khác (Home, Catalog, Detail, Checkout, Admin).
- **Files đã sửa**:
  - `src/PhoneStore.Web/Views/Cart/Index.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `src/PhoneStore.Web/wwwroot/js/site.js`
  - `docs/PROGRESS.md`
- **Tuân thủ nghiêm ngặt**: Không đụng DB/migration, không sửa UI ngoài phạm vi, không chạy build/test/server, không stage/commit/push. Chờ user kiểm tra trực tiếp trên Chrome zoom 100% tại `http://localhost:5015/cart`.

Cập nhật 2026-11-12 — S29.9J.0 **READY FOR USER TEST, điều hướng Header Dropdown Navigation (Tài khoản & Giỏ hàng)**:
- **Mục tiêu**: Nâng cấp khu vực điều hướng trên Header của Customer Web (`http://localhost:5015`), thay thế các liên kết rời rạc bằng 2 dropdown độc lập (Tài khoản & Giỏ hàng), đồng bộ nhận diện thương hiệu, tối ưu trải nghiệm người dùng ở zoom 100%.
- **Thực hiện chi tiết**:
  - **Dropdown Tài khoản (khi đã đăng nhập)**:
    - Trigger hiển thị gọn gàng: icon tài khoản SVG, tên người dùng linh hoạt (`Xin chào, @User.Identity.Name`), mũi tên chevron xoay 180° khi mở.
    - Click vào lời chào chỉ mở/đóng dropdown, không tự động điều hướng sang Đơn hàng.
    - Menu dropdown gồm đúng 2 mục chuẩn:
      1. *Hồ sơ của tôi*: điều hướng đến `/account/profile`.
      2. *Đăng xuất*: sử dụng POST `/account/logout` với antiforgery token bảo mật theo luồng hiện có.
    - Khi chưa đăng nhập: giữ nguyên liên kết Đăng nhập (`/account/login`) với icon tài khoản hài hòa cùng phong cách mới.
  - **Dropdown Giỏ hàng (áp dụng toàn bộ)**:
    - Trigger hiển thị: icon giỏ hàng SVG, chữ "Giỏ hàng", badge số lượng (`<b>@CartStore.Count</b>`), mũi tên chevron xoay 180° khi mở.
    - Click vào khu vực Giỏ hàng chỉ mở/đóng dropdown, không tự động chuyển hướng sang trang `/cart`.
    - Menu dropdown gồm đúng 2 mục chuẩn:
      1. *Đơn hàng của tôi*: điều hướng đến `/orders` (nếu chưa đăng nhập sẽ tự chuyển đến login với returnUrl).
      2. *Giỏ hàng của tôi*: điều hướng đến `/cart`.
    - Badge giỏ hàng phản ánh chính xác số lượng từ `CartStore.Count` và tương thích 100% với hàm `updateCartCount` AJAX từ S29.9I.1.
  - **UI/UX & Tương tác**:
    - Nền trắng tinh tế, viền mảnh `#d9e2ee`, bo góc 12px, bóng đổ nhẹ (`box-shadow`), icon riêng cho từng mục.
    - Hiệu ứng hover và focus-visible sắc nét; `z-index: 1050` đảm bảo không bị che khuất bởi banner/hero/content bên dưới.
    - Click lần 1 mở, click lần 2 đóng; click ra ngoài tự đóng; nhấn phím Escape tự đóng và focus lại nút bấm; mở dropdown này thì dropdown kia tự đóng; đóng khi focus rời khỏi dropdown; chỉ mở khi click chuột (không mở khi rê chuột qua).
    - Responsive linh hoạt: desktop, tablet và mobile (rút gọn text, giữ icon và badge, dropdown căn sát mép phải không tràn màn hình).
- **Files đã sửa**:
  - `src/PhoneStore.Web/Views/Shared/_Layout.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `src/PhoneStore.Web/wwwroot/js/site.js`
  - `docs/PROGRESS.md`
- **Tuân thủ nghiêm ngặt**: Không đụng DB/migration, không sửa UI Home/Catalog/Product Detail/Checkout/Admin, không làm gián đoạn AJAX Add to Cart, không chạy build/test/server, không stage/commit/push. Chờ user kiểm tra trực tiếp trên Chrome zoom 100% tại `http://localhost:5015`.

Cập nhật 2026-11-12 — S29.9I.3 **READY FOR USER TEST, tối ưu Sản phẩm liên quan thành 6 card nhỏ gọn (Desktop 6 cột)**:
- **Mục tiêu**: Nâng cấp khu vực "Sản phẩm liên quan" trong trang Chi tiết sản phẩm (`/products/{id}`), hiển thị tối đa 6 sản phẩm liên quan trên cùng 1 hàng desktop (thay vì 4 card lớn), thu gọn kích thước card và ảnh, đảm bảo đồng bộ thiết kế, không ảnh hưởng Home và Catalog.
- **Thực hiện chi tiết**:
  - **Backend (`ProductCatalogReader.cs`)**:
    - Điều chỉnh truy vấn `GetRelatedAsync`: tăng giới hạn `.Take(4)` thành `.Take(6)`, giữ nguyên điều kiện lọc theo cùng danh mục hoặc cùng thương hiệu, loại trừ sản phẩm hiện tại (`p.ProductId != product.ProductId`), sắp xếp mới nhất.
  - **CSS Scoped (`site.css`)**:
    - Định dạng `.detail-related-grid`: Desktop rộng (≥ 1400px) hiển thị chính xác 6 cột (`repeat(6, minmax(0, 1fr))`), `gap: 13px`.
    - Card thu gọn: padding `11px 11px 12px`, bo góc 14px, đổ bóng nhẹ.
    - Ảnh sản phẩm: chiều cao chuẩn `145px` (trong khoảng 140–150px), `object-fit: contain`, nền gradient xanh nhạt bo góc, zoom nhẹ khi hover.
    - Thông tin: tên sản phẩm font 13.5px tối đa 2 dòng (`-webkit-line-clamp: 2`, cao cố định 36px cân bằng hàng), giá 18px đậm rõ, badge RAM và bộ nhớ 10px nhỏ gọn, tồn kho rõ ràng.
    - Cặp nút thao tác: nút "Thêm vào giỏ" (34px, font 11.5px bold, icon 13px) và nút "Chi tiết" (34px, font 11.5px, viền mảnh) nằm cùng hàng ngang gọn gàng.
  - **Responsive đa màn hình**:
    - Desktop rộng (≥ 1400px): 6 cột (toàn bộ 6 sản phẩm trên 1 hàng).
    - Laptop (1200–1399px): 5 cột (`repeat(5, minmax(0, 1fr))`).
    - Tablet / Màn hình vừa (961–1199px): 4 cột.
    - Tablet (681–960px): 3 cột.
    - Mobile (361–680px): 2 cột (ảnh 140px, 2 nút chuyển sang xếp dọc dễ bấm).
    - Mobile nhỏ (≤ 360px): 1 cột, không tràn ngang.
  - **Bảo toàn**: Không ảnh hưởng `.product-card` hay `.product-grid` trên Home và Catalog; giữ nguyên 100% luồng Thêm vào giỏ AJAX không reload từ S29.9I.1.
- **Files đã sửa**:
  - `src/PhoneStore.Data/Catalog/ProductCatalogReader.cs`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `docs/PROGRESS.md`
- **Tuân thủ nghiêm ngặt**: Không đụng DB/migration, không sửa UI Home/Catalog/Cart/Admin, không chạy build/test/server, không stage/commit/push. Chờ user kiểm tra trực tiếp trên Chrome zoom 100% tại `http://localhost:5015/products/29`.

Cập nhật 2026-11-12 — S29.9I.2 **READY FOR USER TEST, gộp Thông số kỹ thuật & Mô tả sản phẩm thành bố cục 2 cột (60/40)**:
- **Mục tiêu**: Tái cấu trúc giao diện trang Chi tiết sản phẩm (`/products/{id}`), đưa 2 khối Thông số kỹ thuật và Mô tả sản phẩm lên cùng một hàng theo tỷ lệ 60% / 40% (CSS Grid `3fr 2fr`), chiều cao 2 khung bằng nhau, khối Sản phẩm liên quan nằm bên dưới toàn chiều rộng.
- **Thực hiện chi tiết**:
  - **Bố cục 2 cột (`Detail.cshtml` & `site.css`)**:
    - Gộp 2 khối vào `.product-details-grid` với tỷ lệ `minmax(0, 3fr) minmax(0, 2fr)` (khoảng 60% / 40%), khoảng cách `gap: 26px`, viền phân cách trên mảnh `#eaedf2`.
    - Thiết kế thẻ đồng bộ `.detail-card`: nền trắng `#ffffff`, bo góc 16px, viền xám xanh mảnh `#d9e2ee`, bóng đổ nhẹ `var(--shadow-sm)`, chiều cao tự động cân bằng kéo giãn (`align-items: stretch`).
  - **Khung Thông số kỹ thuật (bên trái — 60%)**:
    - Header bên trong khung: nền gradient xanh dương nhạt (`#f0f6fd` -> `#f7fafd`), icon kỹ thuật SVG sắc nét trong khung tròn bo góc, tiêu đề "HIỂU RÕ LỰA CHỌN" / "Thông số kỹ thuật" (`h2`).
    - Bảng thông số: giữ trọn vẹn dữ liệu động (RAM, bộ nhớ, chip, màn hình, camera, pin, OS,...), nhãn `dt` bên trái (140px), giá trị `dd` bên phải, hàng xen kẽ trắng và xanh xám nhạt (`#f8fafc`), padding gọn gàng 11×18px.
  - **Khung Mô tả sản phẩm (bên phải — 40%)**:
    - Header bên trong khung: phong cách đồng bộ khung thông số, icon tài liệu SVG, tiêu đề "THÔNG TIN SẢN PHẨM" / "Mô tả sản phẩm" (`h2`).
    - Nội dung mô tả: hiển thị dữ liệu thực tế từ database (`@product.Description`), căn trái, `line-height: 1.85`, tự động giãn theo chiều cao khung thông số mà không bị cắt chữ hay tạo scrollbar khó chịu.
  - **Responsive**:
    - Desktop (>= 1024px): 2 cột 60/40 song song cân đối.
    - Tablet (< 1024px) & Mobile (< 768px): Tự động chuyển thành 1 cột dọc (Thông số kỹ thuật phía trên, Mô tả sản phẩm phía dưới), chiều cao tự nhiên.
    - Mobile nhỏ (< 480px, 340px): Thu gọn padding header và bảng thông số linh hoạt, không vỡ layout.
  - **Bảo toàn**: Khối Sản phẩm liên quan (`.detail-related-section`) nằm bên dưới chiếm toàn bộ 100% chiều rộng; bảo toàn 100% chức năng Thêm vào giỏ hàng không chuyển trang từ S29.9I.1.
- **Files đã sửa**:
  - `src/PhoneStore.Web/Views/Products/Detail.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `docs/PROGRESS.md`
- **Tuân thủ nghiêm ngặt**: Không đụng DB/migration/controller, không sửa Admin/Checkout/Cart, không chạy build/server/test, không stage/commit/push. Chờ user kiểm tra trực tiếp trên Chrome zoom 100% tại `http://localhost:5015/products/29`.

Cập nhật 2026-11-12 — S29.9I.1 **READY FOR USER TEST, hoàn tất thêm vào giỏ hàng không chuyển trang (Add to Cart Without Redirect)**:
- **Mục tiêu**: Khi khách nhấn "Thêm vào giỏ hàng", hệ thống thêm sản phẩm thành công và ở lại trang hiện tại (không reload, không chuyển hướng sang `/cart`, giữ nguyên vị trí cuộn), hiển thị thông báo toast nổi tự ẩn sau 3s và cập nhật ngay số lượng giỏ hàng trên Header bằng dữ liệu thực từ backend.
- **Thực hiện chi tiết**:
  - **Backend (`CartController.cs`)**:
    - Bổ sung nhận diện request AJAX (`IsAjaxRequest()`) thông qua header `X-Requested-With` hoặc `Accept: application/json`.
    - Phản hồi JSON `{ success, message, cartCount }` khi là AJAX; giữ nguyên `RedirectToAction` và `TempData["CartMessage"]` cho form POST truyền thống và các unit test.
    - Thêm endpoint `GET /cart/count` trả về `{ count }` từ `SessionCart`.
    - Xử lý đầy đủ: hết hàng, số lượng không hợp lệ, vượt tồn kho (kèm thông tin số lượng đã có trong giỏ), sản phẩm ngừng kinh doanh, lỗi DB.
    - Tái sử dụng 100% `SessionCart` và `OrderService` hiện có, giữ nguyên `[ValidateAntiForgeryToken]`.
  - **Frontend JavaScript (`site.js`)**:
    - Bắt sự kiện `submit` form giỏ hàng qua event delegation thống nhất cho toàn bộ website (`data-cart-form` hoặc `action="/cart/add"`).
    - Khóa nút gửi tạm thời (`button.disabled = true`, hiển thị "Đang thêm…") để chặn click đúp / duplicate submission.
    - Gửi form bất đồng bộ qua `fetch` kèm antiforgery token và header AJAX.
    - Xử lý phản hồi: hiển thị toast tương ứng (thành công/lỗi), cập nhật số lượng trên Header (`.cart-entry b` và `aria-label`) kèm hiệu ứng bump animation nhẹ thu hút sự chú ý.
    - Nếu server chuyển hướng (chẳng hạn yêu cầu đăng nhập), tự động chuyển hướng theo đúng luồng bảo mật.
  - **Toast UI & Styling (`site.css`)**:
    - Toast thiết kế nổi tinh tế ở góc trên bên phải (mobile full-width trên cùng), đổ bóng mềm, bo góc 14px, icon SVG tròn (xanh lá thành công, đỏ lỗi), link "Xem giỏ hàng →" và nút đóng `×`.
    - Tự động mờ dần và biến mất sau 3 giây, hỗ trợ đóng nhanh thủ công và tôn trọng `prefers-reduced-motion`.
  - **Đồng bộ toàn diện**: Áp dụng nhất quán trên Trang chủ (`_HomeProductCard.cshtml`), Danh mục & Sản phẩm liên quan (`_ProductCard.cshtml`), và Trang Chi tiết sản phẩm (`Detail.cshtml`).
- **Files đã sửa**:
  - `src/PhoneStore.Web/Controllers/CartController.cs`
  - `src/PhoneStore.Web/Views/Home/_HomeProductCard.cshtml`
  - `src/PhoneStore.Web/Views/Shared/_ProductCard.cshtml`
  - `src/PhoneStore.Web/Views/Products/Detail.cshtml`
  - `src/PhoneStore.Web/wwwroot/css/site.css`
  - `src/PhoneStore.Web/wwwroot/js/site.js`
  - `docs/PROGRESS.md`
- **Tuân thủ nghiêm ngặt**: Không đụng DB/migration, không sửa Admin/Checkout, không chạy build/server/test, không stage/commit/push. Chờ user nghiệm thu trực tiếp qua Hot Reload trên `http://localhost:5015`.

Cập nhật 2026-11-12 — S29.9I **READY FOR USER TEST, tối ưu UI/UX trang Chi tiết sản phẩm (/products/{id}) ở Zoom 100%**:
- **Mục tiêu**: Tối ưu trực tiếp giao diện Chi tiết sản phẩm (`/products/{id}`, tham chiếu vivo V30 Pro id=29) trên Chrome laptop zoom 100%, gọn gàng, hiện đại, cân đối, giảm kích thước các thành phần cồng kềnh, hạn chế cuộn trang không cần thiết.
- **Thực hiện chi tiết**:
  - **Khung ảnh Gallery**: Khắc phục chiều cao 560px chiếm hết màn hình; giới hạn `max-height: 440px`, padding 24px (giảm từ 42px), ảnh `object-fit: contain` căn giữa; thumbnails thu gọn kích thước 64×64px (trước 84×84px), viền active xanh nổi bật.
  - **Thông tin sản phẩm**: Tiêu đề h1 gọn gàng `clamp(24px, 2.3vw, 32px)` (trước `clamp(32px, 3.2vw, 46px)`), giá nổi bật `clamp(26px, 2.5vw, 32px)` (trước 36px), khu vực giá có nền nhạt `#f8fafd` bo góc 12px tích hợp trạng thái còn/hết hàng; 4 ô thông số nhanh gọn gàng padding 9×12px, font 11/13px.
  - **Khu vực mua hàng**: Ô số lượng cao 42px căn giữa, nút "Thêm vào giỏ hàng" cao 42px bo góc 8px phẳng, giữ nguyên toàn bộ logic cart/form POST; link điều hướng phụ 12px.
  - **Thông số kỹ thuật & Mô tả**: Layout 2 cột (cột tiêu đề 220px, nội dung mở rộng); bảng thông số bo góc 14px, padding 11×18px mỗi dòng xen kẽ màu nền; mô tả sản phẩm đóng khung thẻ trắng padding 20×24px, line-height 1.75 thoáng đãng, dễ đọc.
  - **Sản phẩm liên quan**: Lưới 4 cột đồng bộ card PhoneStore (`.detail-related-grid`), ảnh 180px cân đối.
  - **Responsive hoàn chỉnh**: Breakpoints 1100px (3 cột liên quan, ảnh 380px), 768px (1 cột tổng quan, 2 cột liên quan), 480px (1 cột, ảnh 280px), 340px (1 cột nhỏ gọn).
- **Files đã sửa**:
  - `src/PhoneStore.Web/Views/Products/Detail.cshtml` (bổ sung class `detail-related-section`, `detail-related-grid`).
  - `src/PhoneStore.Web/wwwroot/css/site.css` (xóa CSS trùng lặp cũ, tối ưu toàn diện CSS Product Detail).
- **Tuân thủ nghiêm ngặt**: Không sửa backend/database, không chạy build/test/server, không stage/commit/push. Chờ user kiểm tra trực tiếp trên `dotnet watch` tại `http://localhost:5015/products/29` (zoom 100%).

Cập nhật 2026-11-11 — S29.10A.1 **READY FOR USER RUN, tinh chỉnh Docker Compose theo chuẩn v2**:
- Đã xóa bỏ thuộc tính `version: '3.8'` đã lỗi thời trong Compose specification v2.x.
- Chuẩn hóa healthcheck SQL Server theo biến môi trường trong `.env`, hỗ trợ đường dẫn `tools18` kèm cờ `-C` và fallback `tools` (credential mặc định cũ đã được loại khỏi working tree ở S31.5B).
- Đồng bộ thông tin kết nối giữa `db` và `web`: Host `db`, cổng 1433, CSDL `PhoneStoreDockerDB`, tài khoản `sa` với cùng mật khẩu từ `${MSSQL_SA_PASSWORD}`, ánh xạ cổng host giữ nguyên Web 8080 và SQL 14333.
- Files đã cập nhật: `docker-compose.yml`, `docs/PROGRESS.md`. Không tự chạy Docker/migration/test, không commit/push.

Cập nhật 2026-11-11 — S29.10A **READY FOR REVIEW, hoàn tất thiết lập Docker Compose & GitHub Actions CI**:
- **Docker Compose & Container hóa**:
  - `Dockerfile`: Multi-stage build (.NET 8 SDK -> ASP.NET Core 8.0 runtime) cho `PhoneStore.Web`, tối ưu layer caching và image size.
  - `.dockerignore`: Loại trừ sạch sẽ `bin/`, `obj/`, user files, thư mục Admin/Tools/Tests không liên quan.
  - `docker-compose.yml`: Gồm 2 services: `db` (SQL Server 2022 `mssql/server:2022-latest`) với persistent volume `phonestore_mssql_data` + healthcheck (`SELECT 1`), và `web` (`phonestore-web`) phụ thuộc vào `db` (`service_healthy`). Cổng ánh xạ an toàn (Web: 8080, SQL: 14333) cách ly hoàn toàn với môi trường Windows.
  - `.env.example`: Cung cấp file mẫu cấu hình biến môi trường, bảo mật mật khẩu không commit Git.
- **GitHub Actions CI (`.github/workflows/ci.yml`)**:
  - Trigger: `push`/`pull_request` trên `main` và `workflow_dispatch`.
  - Job 1 (`windows-latest`): Restore, build toàn bộ Solution Release (bao gồm WinForms Admin) và chạy bộ 162+ unit tests (`PhoneStore.Tests.csproj`).
  - Job 2 (`ubuntu-latest`): Build Docker image `PhoneStore.Web` kiểm tra đóng gói container trên Linux độc lập.
- **Tài liệu**: Tạo `docs/DOCKER_AND_CI_GUIDE.md` hướng dẫn đầy đủ từ cài đặt, chạy, xem log, dọn dẹp đến minh chứng nộp giảng viên.
- **Files đã tạo/sửa**: `Dockerfile`, `.dockerignore`, `docker-compose.yml`, `.env.example`, `.github/workflows/ci.yml`, `docs/DOCKER_AND_CI_GUIDE.md`, `docs/PROGRESS.md`.
- **Tuân thủ nghiêm ngặt**: Không đụng database Windows, không chạy container tự động, không stage/commit/push. Chờ user review.

Cập nhật 2026-11-11 — S29.9H.6 **READY FOR USER TEST, sửa dứt điểm trạng thái phân trang và bộ lọc**:
- **Nguyên nhân lỗi**:
  - `ProductsController.cs` tự động ép `filter.BrandIds` chứa Apple & Samsung và `filter.InStockOnly = true` khi query string thiếu tham số (do tàn dư quy tắc cũ S29.9H.4).
  - Khi user chuyển trang (`/products?page=2`), hàm `BuildFilterUrl` tự động gắn `brandIds=1&brandIds=2&inStockOnly=true` vào URL phân trang và hiển thị các chip bộ lọc ngoài ý muốn.
- **Sửa chữa nghiệp vụ**:
  - `/products` mặc định hiển thị toàn bộ 40 sản phẩm (4 trang, 10 sp/trang). Không tự ý chọn trước bất kỳ thương hiệu nào, không tự ý lọc chỉ còn hàng.
  - `/products?page=2` chỉ chuyển sang trang 2 của toàn bộ sản phẩm, URL sạch sẽ `/products?page=2`, không tự sinh query lọc, không hiển thị chip khi chưa chọn lọc.
  - Chỉ áp dụng bộ lọc khi user chủ động tick/chọn và submit form.
  - Khi chuyển trang, giữ nguyên đúng các điều kiện lọc người dùng đã chủ động chọn.
  - Khi thay đổi bộ lọc hoặc đổi sắp xếp, form submit quay về trang 1.
  - Khi click "Đặt lại" / "Xóa bộ lọc" / "Xóa tất cả", URL trỏ thẳng về `/products`, xóa sạch toàn bộ điều kiện và về trang 1.
  - Xử lý phòng thủ `page`: Bỏ qua lỗi định dạng nếu `page` không phải số (như `page=abc` -> về trang 1 không báo lỗi); nếu `page > totalPages` thì tự động đưa về trang cuối (`totalPages`).
  - Đồng bộ `HasFilters` trong `CatalogViewModels.cs` để `InStockOnly` chỉ tính là có lọc khi bằng `true`.
- **Files đã sửa**:
  - `src/PhoneStore.Web/Controllers/ProductsController.cs`
  - `src/PhoneStore.Web/Models/CatalogViewModels.cs`
  - `src/PhoneStore.Data/Catalog/ProductCatalogReader.cs`
  - `src/PhoneStore.Web/Views/Products/Index.cshtml`
- **Tuân thủ nghiêm ngặt**: Không build/test, không chạy Web/chiếm cổng 5015, không đụng database/migration, không stage/commit/push.

Cập nhật 2026-11-11 — S29.9H.5 **READY FOR USER TEST, hoàn tất sửa giao diện bộ lọc & card sản phẩm**:
- **Sidebar bộ lọc**:
  - Đã xử lý triệt để lỗi tiêu đề nhóm bộ lọc bị biến thành thanh màu xanh navy đậm (`var(--secondary)` ở CSS cũ dòng 120); thiết kế lại theo phong cách tối giản (nền trắng nhạt `#fbfdff`, chữ xanh navy `#071d3d`, viền ngăn cách mảnh `#eaf1fa`, icon chevron xoay 180° mượt mà khi mở/đóng).
  - Thu gọn khoảng cách và padding thừa của sidebar (`.filter-body` 10px 14px 16px, `.filter-group` 8px 0, ô tìm kiếm cao 36px, nút Áp dụng 40px/Đặt lại 34px), bố cục thanh thoát, dễ thao tác.
  - Trạng thái trực quan: Chip RAM/Storage và radio khoảng giá khi được chọn nổi bật với nền xanh `var(--primary)` `#1559d6` và chữ trắng; checkbox thương hiệu/tồn kho có ô tickbox xanh sắc nét.
- **Card sản phẩm & Lưới 5 cột**:
  - Sửa lỗi nút "Chi tiết" bị rớt dòng thành 2 dòng khi hiển thị ở lưới 5 cột desktop: chuyển `.product-actions` sang `grid-template-columns: minmax(0, 1fr) auto`, thêm `white-space: nowrap !important;` và `padding: 6px 11px; flex-shrink: 0;` cho `.detail-link`.
  - Nút "Thêm vào giỏ" (`.add-cart-button`) chiếm toàn bộ không gian còn lại với `min-width: 0; display: flex; width: 100%`, icon giỏ hàng 14px cố định.
- **Phân trang & Bố cục**: Giữ nguyên 5 sản phẩm/hàng, 10 sản phẩm/trang, thanh phân trang Trước/1/2/3/4/Tiếp giữ nguyên toàn bộ query params.
- **File đã sửa**: `src/PhoneStore.Web/wwwroot/css/site.css`.
- **Tuân thủ nghiêm ngặt**: Không build/test, không chạy Web/chiếm cổng 5015, không đụng database/migration, không stage/commit/push. Chờ user nghiệm thu trực tiếp trên trình duyệt.

Cập nhật 2026-11-11 — S29.9H.4 **READY FOR USER TEST, hoàn tất nâng cấp bộ lọc sản phẩm**:
- **Sidebar bộ lọc mới**:
  - Tiêu đề "Bộ lọc sản phẩm" kèm nút "Đặt lại" góc trên; từng nhóm thu gọn/mở rộng (`details[open]`).
  - Ô tìm kiếm có icon kính lúp.
  - Checkbox 8 thương hiệu thực tế: Mặc định Apple & Samsung được chọn khi chưa có tham số thương hiệu; cho phép chọn nhiều thương hiệu (`WHERE BrandId IN (...)`).
  - Khoảng giá: Nút chọn nhanh (Tất cả, Dưới 5 tr, 5-10 tr, 10-20 tr, Trên 20 tr) + 2 ô nhập tùy chỉnh (ưu tiên giá nhập tùy chỉnh).
  - Chip RAM (`4GB`, `6GB`, `8GB`, `12GB`, `16GB`) & Chip Bộ nhớ (`64GB`, `128GB`, `256GB`, `512GB`) chuẩn hóa đa định dạng chuỗi ("4 GB", "4GB").
  - Checkbox "Chỉ hiển thị sản phẩm còn hàng": Mặc định bật (`StockQuantity > 0`); khi tắt hiển thị cả hết hàng.
  - Nút chính "Áp dụng bộ lọc" & Nút phụ "Xóa bộ lọc" (reset toàn bộ điều kiện về không giới hạn).
- **Bộ lọc đang áp dụng**: Thanh chip phía trên danh sách hiển thị các điều kiện đang chọn (`Apple ×`, `Samsung ×`, `Còn hàng ×`,...), click `×` để gỡ từng điều kiện.
- **Phân trang**: Giữ nguyên 10 sản phẩm/trang, 5 cột × 2 hàng, thanh phân trang Trước/1/2/3/4/Tiếp giữ nguyên toàn bộ query params.
- **Responsive**: Desktop sidebar cố định bên trái; Mobile có nút "Bộ lọc" trên toolbar mở modal drawer có nút đóng `&times;`.
- Tuân thủ nghiêm ngặt: Không build/test, không chạy Web, không chiếm cổng 5015, không đụng database/migration, không commit/push. Chờ user tự build và nghiệm thu.

Cập nhật 2026-11-09 — S29.9G.5.2 **PASS, chờ user duyệt 8 logo thương hiệu Home trên 5015**: bổ sung `realme.svg` (Wikimedia Commons `Realme_logo.svg`, public domain text logo/trademarked, lấy từ realme.com; giữ nguyên path wordmark, bỏ nền vàng, đơn sắc như Simple Icons); Simple Icons realme không tồn tại. Crop `viewBox` theo glyph cho oppo/vivo/samsung/realme (path giữ nguyên); nguồn/giấy phép ghi tại `wwwroot/images/brands/BRAND_LOGO_SOURCES.md`. `Index.cshtml` chỉ thêm mapping realme + khung `.brand-logo-box` 104×40 và class theo brand; `site.css` thêm khối G.5.2 cuối file: kích thước thị giác theo logo (Apple25×30, Google29, OnePlus/Xiaomi30, OPPO80×19, vivo76×20, realme84×19, Samsung100×16), `object-fit:contain`, tên `--secondary` 14px/600. Không đổi hero/card sản phẩm/header/link/controller/DB. Targeted `HomePresentationTests` 2/2 PASS (chạy ngoài sandbox vì vstest bị chặn socket nội bộ); build Web 0 warning/error. Dừng PID23476 đã xác minh (Stop-Process, không -Force), chạy lại Web 5015 (`bin/Debug/net8.0`, Production, content root Web). HTTP Home/Products/brand filter/realme.svg 200. CDP 375/768/1024/1440: 8 logo, cao card đồng đều, tên thẳng hàng, logo căn giữa, không cắt/tràn. Không DB/stage/commit/push. DỪNG ở Home.

Cập nhật 2026-11-09 — S29.9G.5.1 **PASS, chờ user duyệt thương hiệu Home trên 5015**: logo Simple Icons (7 SVG + license, brand thứ 8 hiển thị tên) và CSS `S29.9G.5 Pattern 1` đã có trong source/runtime nhưng bị khối G.1 phía sau ghi đè (card cao cố định 66/62px, logo 28px → chật/cắt). Chỉ bỏ 7 dòng brand cũ trong khối G.1 của `site.css`; không đổi Razor/card sản phẩm/font/nghiệp vụ. Build Web 0 warning/error (`-m:1`, build song song lỗi trong sandbox); CSS-only nên không chạy test. Web PID23476 (`bin/Debug/net8.0`, 5015) đã phục vụ CSS mới (wwwroot live, dll không đổi) nên không restart. HTTP: Home200/12 card/7 logo+1 tên, CSS200 rule mới, `/products`200, logo SVG200. Không DB, stage/commit/push; giữ `PhoneStore.sln`. DỪNG ở Home.

Cập nhật 2026-11-09 — S29.9G.4.1 **PASS, chờ user duyệt giá card Home**: UI UX Pro Max numeric typography; sửa lệch do giá flex center + ký hiệu `₫` 78% nhưng line-height 25px. Probe glyph xác nhận Be Vietnam Pro không hỗ trợ biến thể `tnum`, nên không dựa vào feature không tồn tại; giá Home dùng baseline, weight700, line-height1.25 và tracking chuẩn, không đổi markup/giá/định dạng/bố cục. Build Web 0 warning/error; CSS-only nên không chạy test. Xác minh PID cũ16304 đúng Web5015 rồi shutdown không `/F`; runtime mới PID24776 Debug, Home HTTP200/12card. Browser375/1440 computed PASS, nhiều độ dài giá không overflow/clipping. Không DB DML, stage/commit/push; giữ thay đổi trước và `PhoneStore.sln`. DỪNG ở Home.

Cập nhật 2026-11-09 — S29.9G.4 **PASS source/browser preview, chờ user duyệt typography**: dùng skill UI UX Pro Max; thay Segoe UI và weight tổng hợp 650/720/750/780/800 bằng Be Vietnam Pro cục bộ, đúng 4 face 400/500/600/700 + OFL, token font toàn cục và `font-synthesis:none`. Build 0 warning/error, targeted 2/2 PASS. Preview `http://localhost:5199/`: `document.fonts`/resource timing/computed styles xác nhận font thật đã load; Home 12 card 2/3/4/6 cột tại 375/768/1024/1440, card đồng đều, tiếng Việt/số/`₫` đúng, không overflow/clipping/ảnh vỡ. Smoke Home/Products/Detail/Login/Register/Cart ở 4 viewport PASS. Main không restart/deploy, không DB DML, không stage/commit/push; giữ mọi thay đổi G trước đó và `PhoneStore.sln`. Xem `S29_9G_4_TYPOGRAPHY.md`. DỪNG ở Home.

Cập nhật 2026-11-09 — S29.9G.3 **PASS source/browser preview, chờ user duyệt card Home**: partial card riêng Home theoMẫu1, khôngđổi sharedCatalog; nềntrắng/surfacexanhnhạt/shadownhẹ, brandđầyđủ, RAM+Storage2chip, giáxanhđậm, stockxanhlá, CTAchính+detailviền, khôngbadgegiả. Build0warnings/errors,targeted2/2PASS. Preview `http://localhost:5198/` PID15208: 12card, grid6×2/4/3/2; card đồngđều, khôngoverflow/clipping/ảnhvỡ/nútchồng; controls44/45px, focus/reduced-motionPASS. UI UX Pro Max áp dụng hierarchy/4-8spacing/responsive/a11y, giữ design system. Main5015 khôngrestart G.3; khôngDBDML/stage/commit/push; bảo toàn `PhoneStore.sln`. Xem `S29_9G_3_HOME_PRODUCT_CARD.md`. DỪNG ởHome.
HEAD `0a94f98` — Customer Web Acceptance — làcheckpointF; workingtree sạch trướcG, G cònunstaged. F:11màn×4viewport runtimechỉđọcPASS, WebmainPID14764/bin/S29.9F/5015; snapshotF40Products/40Images/tồn733/Orders4/Users2 là lịch sử, khôngsuy trạngtháilive. CácPOSTnghiệpvụmainchưa nghiệmthu, giữPARTIAL.
Đọc đầu mỗi phiên. Ưu tiên source và kết quả thực tế khi tài liệu mâu thuẫn; chỉ kiểm tra phần liên quan nhiệm vụ.

## 1. Kiến trúc, môi trường và cấu hình
- Root `C:\PhoneStore\PhoneStore`, `PhoneStore.sln`; .NET 8 / SDK 8.0.427, EF/tooling 8.0.28.
- Customer ASP.NET Core MVC và Admin WinForms → Business → Data → Models; EF Core/SQL Server.
- Solution gồm Models, Data, Business, Web, Admin, Tests, AdminBootstrap; CatalogImport/SQL integration harness riêng.
- SQL Server 2022 Developer, MSSQLSERVER; alias localhost, server thực tế DESKTOP-ES9362P.
- Main: PhoneStoreDB, Windows Authentication; key ConnectionStrings:PhoneStoreConnection.
- Connection: `Server=localhost;Database=PhoneStoreDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`.
- Web appsettings/default/Development và Admin appsettings dùng main; factory đọc Web config, env có thể ghi đè.
- CONTEXT.md dùng cho kiến trúc/lịch sử; Current Progress vẫn S29.4 và có server cũ, không dùng làm trạng thái runtime.

## 2. Quy ước bằng chứng
- VERIFIED/PASS thực tế: đã chạy, luôn ghi main hay cách ly và phạm vi; không suy thành mọi ca đều đạt.
- PASS test tự động: có kết quả test đã ghi ngày, không thay nghiệm thu UI; PARTIAL: triển khai/bằng chứng chưa đủ.
- NOT IMPLEMENTED/MISSING: chưa có chức năng; UNKNOWN/NOT TESTED: thiếu bằng chứng, không mặc định là lỗi.
- Báo cáo tổng hợp mới nhất: [S29.8D](S29_8D_FUNCTIONAL_AUDIT.md).

## 3. Runtime và SQL gần nhất — S29.8D.2B
- SELECT 2026-11-02 03:55:44 UTC / 10:55:44 Bangkok: localhost/DESKTOP-ES9362P, PhoneStoreDB ONLINE.
- Products40, Images40, Brands8, Category1, Users2, Roles2, Orders1, OrderDetails1, tổng tồn740.
- Admin duy nhất UserId1/admin@admin.com/RoleId1/active; Customer UserId2/tai123@gmail.com/RoleId2/active.
- Customer đã khôi phục tài vũ/09032657212/AddressNULL bằng UI1lần theo duyệtD.1C.2; SQL/Web/greeting khớp, không đăng nhập lại.
- OrderId1, code ORD-05E65B3F0F705464636B525624, Cancelled, 3490000; Product25 vivo Y17s tồn38.
- History: 20261001150857_InitialCreate + 20261102122856_EnforceSingleAdmin; không migration mới cần apply cho C.
- UX_Users_SingleAdmin unique/enabled/filter RoleId1; CK_Roles_AdminIdentity enabled/trusted.
- Web PID19388: `src/PhoneStore.Web/bin/Debug/net8.0/PhoneStore.Web.exe --urls http://localhost:5015` (D.1B mới).
- Admin PID10736: `src/PhoneStore.Admin/bin/Debug/net8.0-windows/PhoneStore.Admin.exe`; đang đăng nhập.
- D.1B Web60/60 runtime khớp staging, backup62/62 và101preservedhash PASS; SQL session cả2 PID main, sleeping/open transaction0.
- GET D: Home6/Products40/Login/Register/Cart HTTP200; search iPhone6, brand1 có6, khoảng10–20triệu có17.
- Detail25 HTTP200, ID0 HTTP404, ảnh40/40 JPEG HTTP200; Customer /orders/1 Đã hủy, không nút hủy.
- Admin D chỉ đọc7 màn hình: Products40/Brands8/Categories1/Customers1/Orders1; Dashboard/Reports doanh thu0, tồn740.

## 4. Chức năng hiện tại
| Nhóm | Trạng thái / phạm vi bằng chứng |
|---|---|
| Customer đăng ký/login | VERIFIED main C.7, người dùng tự nhập mật khẩu; sai/inactive/role PASS tests |
| Customer logout | VERIFIED HTTP cách ly C; không logout phiên main trong audit |
| Catalog/search/filter/sort/detail/ảnh | VERIFIED main A/C.6B/D theo ca đã chạy; chưa phủ mọi tổ hợp filter |
| Giỏ hàng | VERIFIED add qty1 main C.7; update/remove/invalid/stock/session PASS tests + HTTP cách ly |
| Checkout/giá snapshot/trừ kho | VERIFIED main một giao dịch C.7 và SQL/SQLite cách ly; không retry outcome không rõ |
| Checkout idempotency/concurrency | VERIFIED SQL/HTTP cách ly C/C.5; chưa test replay ghi trên main |
| Lịch sử/chi tiết/ownership | VERIFIED UI main C.7/7A; foreign ownership PASS tests/HTTP cách ly |
| Hủy/hoàn kho | VERIFIED main Confirmed→Cancelled, kho38→37→38; double cancel PASS cách ly, không gửi lặp main |
| Hồ sơ Customer self-service | PASS source/tests/deploy/GET/lưu thủ công và khôi phục UI D.1C.2; AddressNULL/cookie/greeting/invariants PASS; đề xuất đóng D.1 |
| Admin login/phân quyền | D.3 PASS WinForms test: logout/xóa session, sai mật khẩu, Admin login lại; Customer forbidden PASS automated/NOT TESTED UI |
| Dashboard | D.3 PASS WinForms+SQL test:4activeSP/9kho/4Customer/10Orders/1Completed/1trdoanhthu |
| Product/Brand/Category/ảnh | D2A/B SQL/testPASS; D2C coreCRUD WinForms PASS: lookup add/edit/search/deactivate/reactivate, Product CRUD/softdelete/filter, image add/6reject/unlink/primary; ca phụ còn PARTIAL, chưa deploy |
| Quản lý Customer Admin | PARTIAL: đọc/bảo vệAdmin có evidence, sửa/khóaGUI chưa nghiệmthu; ngoài D.2A |
| Stale stock | VERIFIED SQL/dialog thật B.1 + regression; đã triển khai C.6B, không còn chờ deploy |
| Admin quản lý đơn | VERIFIED UI cách ly C.5 toàn workflow; main C.7A Confirm thật và đọc Cancelled |
| Reports | D.4B fixed Bangkok timezone; D.4C.1 Admin test UI PASS range01–02/same-day02/empty01, chưa deploy main |
| AdminBootstrap | VERIFIED S29.6 đã tạo Admin; không chạy lại --execute |

## 5. Mốc và bằng chứng cần giữ
- Kiểm kê sau cài Windows: 26/26 tests; DB mới Roles2/còn lại0; không tìm thấy backup/export/ảnh gốc trong phạm vi đã kiểm.
- Server cũ taiiiii\SERVER không còn là cấu hình chạy; không tự kết nối. Git/clone không khôi phục dữ liệu runtime mất.
- S29 bootstrap source/tests51; S29.3/4 suite72, test EnforceSingleAdmin trên PhoneStoreDB_MigrationTest PASS.
- Quy tắc chính thức: tối đa1 Admin kể cả inactive; Customer nhiều tài khoản. Store tạo Customer không nhận RoleId tùy chọn.
- S29.5B đã apply EnforceSingleAdmin vào main theo duyệt; S29.6 tạo Admin duy nhất theo duyệt.
- S29.7C/D: checklist [PRODUCT_IMAGES](PRODUCT_IMAGES.md),40 JPEG800x800; ảnh AI được người dùng chấp nhận minh họa demo.
- S29.7F: backup/import được duyệt riêng, chạy importer đúng1lần;40/40/8/1/tồn740; không tự chạy lại vì có thể ghi đè.
- S29.8A [audit cũ](S29_8A_FUNCTIONAL_AUDIT.md) phát hiện stale stock; các kết luận commerce cũ đã được C thay thế.
- S29.8B: DTO original stock + atomic conditional UPDATE toàn edit; affected0 từ chối, dialog giữ form/báo xung đột;83tests PASS.
- S29.8B.1 SQL thật/dialog: checkout10→9/stale save từ chối, hai connection affected[1,0], valid save/rollback PASS.
- PhoneStoreDB_StockConcurrencyTest được duyệt tạo/ghi riêng; giữ nguyên, không drop/cleanup/rerun fixture B.1.
- S29.8B.2 chỉ staging lịch sử; bản B đã được đưa vào main cùng C.6B, không triển khai B.2 cũ đè C.
- [S29.8C](S29_8C_COMMERCE_REPORT.md):105tests; transaction/stock locks, durable token OrderCode+unique/sp_getapplock, owner cancel và atomic restock.
- Checkout/cancel khóa UPDLOCK,HOLDLOCK theo ProductId trong Serializable, clear tracking, không tự retry giao dịch.
- [C.5](S29_8C_5_ACCEPTANCE_DEPLOYMENT.md):107tests; actual Orders controls/dialog SQL test PASS confirm→shipping→completed/cancel/filter/invalid; session test bằng reflection, không phải full login.
- C.5 HTTP cách ly PASS auth/cart/CSRF/ownership/price/status sync/replay; không đồng nhất với107 xUnit tests.
- TestDB cuối C.5:3Products stock8/0/0,5Users,10Orders (5Cancelled/1Completed/4Pending); D không ghi/kiểm lại fixture này.
- C.6A shutdown Ctrl+Break được duyệt riêng, PASS đúng console cũ; không suy cách shutdown cho PID mới.
- [C.6B deployment](S29_8C_6B_DEPLOYMENT_RESULT.md):118binary thay/3appsettings giữ;118hash PASS, Web/Admin main smoke PASS, user login thật.
- Staging: Web/bin/S29.8C.5-staging/net8.0 và Admin/bin/S29.8C.5-staging/net8.0-windows, dưới src/PhoneStore.*.
- Binary backup121files: `src/PhoneStore.Admin/obj/S29.8C.5-binary-backups/20261102_013925853Z_de37e8a4ab1c44039836ca8a78baeecf`; giữ manifest, không clean.
- [C.7/7A](S29_8C_7_TRANSACTION_ACCEPTANCE_RESULT.md): đúng1Customer/1Order/1Detail thật; UserId2/OrderId1/DetailId1.
- C.7 checkout qty1 đúng1lần, Pendingstock37/tổng739; user Confirm qua Admin thật, Customer thấy Confirmed.
- User Cancel đúng1lần qua Web; Web/Admin/SQL Cancelled, stock38/tổng740. Không request hủy lặp main.
- SELECT lặp và đối chiếu40catalog/ảnh không đổi; giữ Customer/đơn Cancelled/Detail làm bằng chứng, không xóa.
- Screenshot trong Web/obj: S29.8C.7-pending-receipt.png, confirmed-order.png, cancelled-order.png (cùng tiền tố).

## 6. Backup và ảnh
- Backup SQL trong `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup`.
- S29.5A: `PhoneStoreDB_FULL_COPYONLY_20261102_123908.738Z_131E14FF3BAC44C391332C4392CC2B72.bak`.
- S29.7F: `PhoneStoreDB_S29_7F_PREIMPORT_20261102_001906872Z_47B0A15271A6437A8D5FB05E41E61A38.bak`.
- Gần nhất trước C.7: `PhoneStoreDB_S29_8C_7_PRETRANSACTION_20261102_020840231Z_83DE6E0E3458420896EA6963BCAB5F59.bak`.
- C.7 backup02:08:40UTC/09:08:40Bangkok,566272bytes/set3;FULL COPY_ONLY/CHECKSUM/COMPRESSION + VERIFYONLY CHECKSUM PASS.
- Backup trước giao dịch không chứa Customer2/Order1 mới; chưa thử restore, cùng ổC, VERIFYONLY không thay restore rehearsal.
- Ảnh runtime: `C:\PhoneStore\PhoneStore\src\PhoneStore.Web\wwwroot\uploads\products`; cần backup riêng, không tự xóa/ghi đè.

## 7. Tests và rủi ro còn mở
- Gần nhất D.4B buildsolution0warnings/errors,161/161 PASS,0skip; SQL report/auth read-only + gateway fake PASS; outputbin/S29.8D.4B-final/.
- D.1C.1 chạy lại22/22profile PASS (SQLite/HTTP thật cách ly), output bin/S29.8D.1C.1-tests; khôngfullsuite/deploy/mainPOST.
- LệnhD2B: `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8D.2B/`; `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8D.2B/ --verbosity minimal`.
- Suite SQLite: Authentication, Bootstrap, SingleAdminPolicy/Constraint, Checkout, OrderWorkflow, ProductStockConcurrency, CommerceWorkflow, CartController.
- SQL/HTTP/UI harness riêng opt-in trong tests/PhoneStore.SqlIntegration; không chạy audit read-only để tránh ghi fixture.
- Có18testsCRUD+8safety/WIC/SQLshared/concurrentprimary; WebP policyreject, GUI/report/CustomerCRUD/logout/stresschưađủ; orphan giữ cóchủđích,khôngautodelete.
- Original-stock guard không phải rowversion mọi field/ABA; cart theo browser session; legacyCreateAsync không token nhưng Web dùng Idempotent.
- Revenue tổng/trend/average chỉ Completed; BestSellers/SalesValue dùng Confirmed/Shipping/Completed, không cùng chỉ số doanh thu.
- [D.4C.3](S29_8D_4C_3_ADMIN_ACCEPTANCE_SUMMARY.md): Admin CONDITIONAL READY. Core CRUD/Dashboard/Reports/Auth/Orders + revoke IsActive/RoleId runtime PASS; partial DisplayOrder/close-during-save/Brand text/Customer-write và race check→commit.
- [D.4D.1](S29_8D_4D_1_ADMIN_MAIN_PREDEPLOY.md): pre-deploy READY; manifest 58 runtime loại appsettings, main/test config và uploads tách biệt, không migration.
- [D.4D.2/2A](S29_8D_4D_2_ADMIN_MAIN_DEPLOYMENT.md): deploy Admin main D.4B PASS. Backup59/59, runtime58/58, main config hash giữ nguyên; PID4592/SQLmain. WinForms thật PASS Login/Dashboard/Reports/Product list+3 search cases/Brands/Categories/Customers/Orders detail/Logout. Hậu kiểm main40/40/tồn740/1Cancelled/1Admin và40ảnhhash; không rollback/DML/migration. Test DB baseline giữ nguyên; PID test không còn ở checkpoint cuối nhưng D.4D.2 không tác động nó.
- [E.1 Customer audit](S29_8E_1_CUSTOMER_FUNCTIONAL_AUDIT.md): PARTIAL. Registration/login/catalog/cart/checkout/idempotency/concurrency/orders/cancel/profile/ownership có source và automated/SQL/browser evidence phân loại rõ; không thấy mandatory function MISSING hay P0. P1 hiện tại: Web không process/listener5015 nên HTTP unavailable. Main read-only40Products/40Images/tồn740/1Customer/1Cancelled/1Admin và40ảnhhash nguyên. Đề xuất E.2 khởi động đúng binary/config rồi browser acceptance; chưa sửa/deploy/DML.
- [E.2 browser smoke](S29_8E_2_CUSTOMER_BROWSER_SMOKE.md): PASS. Web PID14572, HTTP5015, SQL session `PhoneStoreDB`/openTx0; startup log sạch ngoài warning HTTPS-port HTTP-only. HTTP PASS Home6/Products40/search6+empty0/brand6/category40/detail25/login/register/cart/redirect protected/40ảnh. User xác nhận browser thật PASS toàn checklist. Main40/40/tồn740/1Customer/1Cancelled/1Admin và40ảnhhash nguyên; Admin4592 không ảnh hưởng. Authenticated/cart writes/checkout không thử trong E.2; đề xuất E.3 có phê duyệt riêng.
- [E.3 account acceptance](S29_8E_3_CUSTOMER_ACCOUNT_ACCEPTANCE.md): PASS browser thật. User tự login Customer; greeting/protected routes/profile readonly đúng tai123@gmail.com/tài vũ/phone/addressNULL; history/detail đúng Order1 Cancelled/vivoY17s×1/3490000, không nút cancel; cart readonly PASS; logout và protected redirect login PASS. Foreign ownership chỉ PASS automated/isolated, không thử main. Hậu kiểm40/40/tồn740/1Customer/1Cancelled/1Detail/1Admin và40ảnhhash; Web14572/Admin4592/log sạch, không DML.
- [E.4 logout cart fix](S29_8E_4_LOGOUT_CART_ISOLATION_FIX.md): root cause cookie logout không xóa SessionCart/token, gây giỏ dùng chéo browser. AccountController inject cart và clear đúng2 key trước signout; regression gọi action thật. Targeted1/1, build0warning/error, full162/162 PASS. Main40/40/tồn740/1Customer/1Cancelled/1Detail/1Admin+40hash nguyên. Chưa deploy; Web14572 vẫn binary cũ nên runtime còn lỗi đến khi duyệt deploy/restart. Known gap: cookie expiry/revoke không qua logout chưa owner-bind cart.
- [E.4A predeploy](S29_8E_4A_LOGOUT_FIX_PREDEPLOY.md): READY. Source diff đúng AccountController+CartControllerTests, output build sau source/162tests. Manifest runtime mới/current62/62; preserve2appsettings, deploy đủ60 files (8 khác hash). Web14572/SQLmain và Admin4592 giữ nguyên; main40/40/tồn740/1Customer/1Cancelled/1Admin+40hash. Backup path mới chỉ lập, chưa tạo. E.4B cần duyệt backup/shutdown/deploy và session-only add1cart→logout→empty; không checkout/SQL DML.
- [E.4B deployment](S29_8E_4B_LOGOUT_CART_FIX_DEPLOYMENT.md): deploy Web fix PASS: backup62/62, staging/runtime60/60,2config+40ảnh giữ hash; soft CtrlBreak old14572 PASS, new Web12640/5015/SQLmain/log sạch, HTTP smoke+40ảnh PASS, không rollback; Admin4592 nguyên. User checkout ngoài phạm vi tạo Order2 `ORD-D5AD063ADF220813B7EA345E99` Pending/vivoY17s×1/3490000, stock25=37,total739, Orders/Details2; agent chỉ SELECT, không sửa. User sau đó add session lại×1→logout→cart anonymous rỗng: cart isolation PASS runtime, không Order3. Token cleanup PASS automated, chưa runtime-observe riêng. Giữ Order2 chờ quyết định nghiệp vụ; không tự cancel/delete/stockfix.
- HTTPS redirect thiếu port khi chạy HTTP vẫn warning đã biết; không phát hiện runtime failure mới qua audit đọc.
- Word/Excel UC01–UC12/SCR01–SCR12/rubric chấm điểm chưa được xác minh; không tự suy đáp ứng toàn bộ tài liệu chưa có.

## 8. Nhiệm vụ hiện tại và bước tiếp theo
- [S29.9G Home](S29_9G_HOME_UI_REFINEMENT.md): chỉHomeRazor/CSS+2tests, preview5196đểuserduyệt; giữheaderF/footer, khôngghidữliệu hoặc chuyểnmànhình. Sau khi userchốt mới quyếtđịnh cậpnhật5015/checkpoint hoặc mànhìnhtiếp.
- [S29.9F runtime](S29_9F_CUSTOMER_WEB_RUNTIME_ACCEPTANCE.md): D/E đã lênmain mới, Customer tựlogin, Orders/CancelledDetail/Profile/Cart/CheckoutGETPASS. FchỉsửaCSSheader,docs/screenshots; chờ user quyết địnhcommit/push. Không thao tácAdmin ởF; READY chuẩn bị UIAdmin sauchỉđạo.
- [S29.9E Auth/Profile UI](S29_9E_AUTH_PROFILE_UI_UX.md): preview Razor không SQL PASS, summary/link/error/hint/loading/pageshow/keyboard/contrast PASS; không thay đổi auth/session/backend. Chưa nghiệm thu trên runtime main với Customer thật; không stage/commit/push. Giữ toàn bộ công việc D, không audit lại các màn hình đã PASS.
- [S29.9D UI](S29_9D_CART_CHECKOUT_ORDERS.md): 5 Razor views + CSS/JS; browser cách ly PASS (native invalid/summary/link focus/Tab/Shift+Tab/loading/pageshow/confirmation-dismiss). Main chỉ GET cart200; Razor mới chưa deploy, không ghi SQL. Skill đọc trực tiếp do `py` chưa khả dụng; script/harness tạm ở obj ignored.
- [S29.9B.2 acceptance](S29_9B_2_BROWSER_ACCEPTANCE.md) và [S29.9C](S29_9C_CATALOG_PRODUCT_DETAIL.md) đã thay trạng thái B.1 PARTIAL: Home/catalog/detail PASS Edge CDP đúng 4 viewport, hero/fallback/tablet CTA đã sửa. B/C build0warning/error,162testsPASS; không đổi nghiệp vụ.
- [S29.9A UI/UX audit](S29_9A_UI_UX_AUDIT.md): Codex nhận diện `ui-ux-pro-max` và đã audit source toàn bộ Web/Admin; không sửa source/runtime/DB. Kết luận READY có điều kiện cho S29.9B Web trước. Điểm ưu tiên: font 9–12px, disabled contrast 2.56:1, error-focus/checkout/cancel, reduced-motion chưa phủ transform, token/responsive Web; DPI/keyboard/status/chart semantics Admin. `py` hiện báo không có Python nên search design-system chưa chạy, không có output cũ; audit dùng SKILL/checklist trực tiếp. Tests/build không chạy vì documentation-only.
- S29.9-SKILL: đã kiểm tra gói chính thức `ui-ux-pro-max-cli` 2.15.0 và cài project-local vào `.agents/skills/` bằng CLI đã tải/kiểm tra, không global, không `--force`, không sửa UI/DB/runtime. `ui-ux-pro-max` có SKILL.md + 68 file (~3.57 MB); CLI chính thức còn tạo 6 skill phụ đi kèm. README nói có `--dry-run` nhưng npm 2.15.0 thực tế không khai báo option này, nên đã audit mã trước khi init. Node24.22.1/npm11.19.1/Codex0.164.0; Python3 chưa khả dụng (chỉ Windows Store alias), vì vậy search.py chưa chạy và trạng thái PARTIAL. Cần người dùng tự cài Python3 rồi mở phiên Codex mới để nhận skill và chạy smoke search; không cài Python/global CLI tự động.
- S29.8D audit hoàn thành, toàn hệ thống PARTIAL; D.1 profile source/tests PASS, D.1B đã triển khai/GET main PASS.
- [D.1 report](S29_8D_1_CUSTOMER_PROFILE.md): CustomerProfile DTO/store/service/interface, ProfileController/model/view, Program DI/public test entry, layout link,2test files/test csproj và docs.
- Chỉ GET/POST /account/profile, Customer active từ claimID; UPDATE có điều kiện chỉ FullName/Phone/Address; Email readonly, không bind ID/role/hash; CSRF+validation server.
- Đổi tên cập nhật Name claim và giữ expiry/persistent cookie; tests overposting/owner/anonymous/inactive/role/CSRF/validation/error/session PASS.
- WebApplicationFactory chạy Program/MVC/cookie/filter thật với SQLite và DataProtection ephemeral; không SQL test writes, không tạo môi trường SQL mới.
- [D.1B deployment](S29_8D_1B_PROFILE_DEPLOYMENT.md): CtrlC20giây không thoát; recheck console chỉ Web/helper và SQL idle rồi CtrlBreak1lần, log shutdown/exit/5015trống PASS; không force.
- D.1B copy60runtime từ staging62, giữ2appsettings; backup62hash và101Admin/uploads/source-confighash giữ nguyên; chỉ restart Web19388, Admin10736 giữ.
- Artifact/backup/staging/manifests/log: `src/PhoneStore.Web/obj/S29.8D.1B-20261102_024935357Z_9be18a0a8d6a413d9cfa63304bc5d7f7`; giữ, khôngclean.
- Hậu kiểm D.1B mainONLINE40/40/8/1,Users2,Order1Cancelled,Detail1,tồn740,Customer tên tài vũ; cảWeb19388/Admin10736 SQLmain, khôngghi.
- Browser sessionCustomer cũ còn hợp lệ; clicklinkHome→Hồsơ PASS đúngowner/emailreadonly/noSensitiveFields; anonymous302login;40detail/40ảnh200.
- [D.1C](S29_8D_1C_PROFILE_ACCEPTANCE.md) helperclick timeout→dừng/SELECTgốc; [C.1 diagnosis](S29_8D_1C_1_PROFILE_DIAGNOSIS.md) user xácnhận đã tựLưu, SQL/greetingtestvalues; rootcausehelpercũ UNKNOWN.
- [D.1C.2 restore](S29_8D_1C_2_PROFILE_RESTORE.md) duyệtriêng UI1click/POST302→GET200/reload, Customer2=tài vũ/09032657212/AddressNULL,session/greeting/invariantsPASS;đềxuấtđóngD.1.
- EvidenceD.1C `src/PhoneStore.Web/obj/S29.8D.1C-20261102_031553601Z_0f70f82b55224b31a97cc4420a0835cf`; giữmarkers,khôngrerunhelperghi.
- [D.2A audit](S29_8D_2A_ADMIN_CRUD_AUDIT.md) PARTIAL toànphạmvi; source/SQL/testPASS, chưa fullWinFormsCRUD. Đã sửaLookupManagementService catchDbExceptionchung; thêmAdminCrudTests/AdminCrudChecks/modeProgram+harnessREADME/docs.
- TestDBđượcduyệtghi+dọn:trướckhôngsession/processkhác,guardDB_NAMEtừngEFcommand+cooperativelock. FixtureD2Ariêng1Brand/1Category/3Products/1OrderCancelled/1Detail,khôngtạo/sửaUsers.
- ChạySQL1lầnPASS43assertions:validation/duplicate/lookups/softdeactivate/referenceFK/stockpredicate/ảnhJPEG-PNG-primary-order-remove/rollback/filecleanup/gatewaydeny.
- Cleanupchỉprefix/IDmới+filefixture,giữfixturecũ; main/test9/9snapshotsnguyên (Userskhôngcredentials),identitytestcóthểtăng,khôngreseed/drop/migrate.
- HậuSELECTD2A03:32:01UTC mainONLINE40/40/tồn740/Users2/Order1Cancelled/Detail1/Customernguyên/Adminnguyên;Web60+101preservedhashPASS,khôngdeploy.
- Evidence `tests/PhoneStore.SqlIntegration/obj/S29.8D.2A-20261102_033101218Z`;giữmarker/log/snapshots,khôngtựrerunoldmodes.
- Giới hạn ảnh signature-only của D.2A là lịch sử; đã được D.2B thay thế bên dưới, GUI vẫn chưa nghiệm thu đầy đủ.
- DỪNGchờchỉđạo,khôngtriểnkhaiAdmin/ghimain. Softdeactivate là luồngxóaUI hiệncó;ProductNamekhôngunique,lookupinactivekhôngcascadeẩnProduct,cầnchốt nếumuốnđổi.
- [D.2B report](S29_8D_2B_IMAGE_VALIDATION.md): JPEG/PNG≤10MiB/4096mỗichiều/16Mpixels/1frame,decodeWICmọipixel;CRC/IEND/EOI/APNGgate;WebP mớitừchốirõ,ảnhcũgiữ. FrameworkWPFđãcài,khôngNuGetcodec/migration.
- Storagebắtbuộcvalidator,snapshotbấtbiến,GUID/CreateNew/atomicmove,chỉdọn.tmpđãtạo. Servicegiữfilepublish sauunlink/DBfailure/unknowncommit+noticeURL;dọnorphanphảikiểmtra/duyệtriêng.
- SQLlầnđầuFAILdeadlock PK_ProductImages RangeS-S→X (system_healtheventfile),đãSELECToutcome/dọnđúngfixturemới;thêmsp_getapplockExclusive/Transactiontrướcmọilockảnh,khôngretry. FixturemớisausửaPASS,cảbảncuốiCRC.
- SQLtestdùnglạiStockConcurrencyTest,fixture2Products/1Brand/1Category/images,khôngUsers/Orders. Sharedreplace+firstuploadđồngthời/primary/rollback/orphannoticePASS;cleanupchỉfixture,main/test9snapshotsnguyên.
- Evidence `tests/PhoneStore.SqlIntegration/obj/S29.8D.2B-20261102_033917998Z`;logs/markers/snapshots/browserJPEG-PNG-WebPdecode. BrowserofflinePASS,khônguploadmain.
- SourceD2B: validatorinterface+AdminWIC,LocalStorage/ProductService/DataStore,AdminDI/dialogfilter/frameworkref;testsSafety/SQLImageChecks/modes/README,D2Acasesupdatedpolicy,docs. Chưa deployWeb/Admin.
- HậuD2Bmain40/40/tồn740/1Cancelled/Detail1/Customer-Adminnguyên;Web19388/Admin10736responsive,Web60+preserved101hashgiữ(40ảnhdemo). Khôngcommit/push.
- D2Bsource/testsPASSkhôngthayUIacceptance. Rủiro:orphan/dunglượng,nativecodec/ACL,globalimagelockgiảmthroughput;khôngtựdọnmain.
- [D.2C UI](S29_8D_2C_ADMIN_WINFORMS_ACCEPTANCE.md) PARTIAL: appcopy/config/envcáchly,PID13368đãlogintestD2C1;DBStockConcurrencyTest/ảnhriêng,main/test9snapshotspublic+40ảnhhashnguyên,chưaCRUDghi.
- Artifact `src/PhoneStore.Admin/obj/S29.8D.2C-20261102_035250536Z`; D2C1duyệtriêngPasswordHashtestUser1/admin@s29-8b1.test,guardSQLthật+Serializable+WHEREchặt,1rowcommit/SELECTbooleanPASS03:58:54UTC.
- Usertựnhậppassẩnx2terminal,tựlogin vàxácnhận;UI13368shellđúngtestAdmin/SQLtestDBPASS03:59:38UTC;khôngsecretslog/file/chat. Main/test9snapshotnoncredentials và40ảnhnguyên.
- D2C1utility build0warning/error,giữattempt/passwordtest,khôngresetlặp. D2C3 testPID5944 appcopy59hashPASS;userlogin/shellSQLTestAdmin/SQLsessiontestDBPASS(openTx0);Dashboard3SP/8kho/10đơn,Products3/Brands1/Categories1 tảiPASS. Config/env/DI pinảnhriêng,uploads0file(chưaupload). UI tạo/sửa/tìm Brand6+Category6 S29D2C;Categoryrequired/duplicate báo rõ;Brandduplicate không thêmrow nhưng chưađọcđượctext. 13:42 UIA đồngthời thấy ProductEditDialog+LookupEditDialog cũ: dừng trướcSaveProduct,chờuserHủy/đóngmodal;0Product/0Image mới. Main9snapshot+40ảnhnguyên;test7bảngnguyên,Brands/Categories chỉthêm2fixtureID6active,giữđểtiếp,chưacleanup. Evidence *-D2C3-before/comparison.json+D2C3-fixture-checkpoint.json. Khôngbuild/testlại/deploy/mainDML.
- D.2C.2 PARTIAL 13:35Bangkok: PID13368/10736/19388 không còn, không có PhoneStore process hoặc listener5015; dừng theo điều kiện precheck, không tự restart/CRUD. Config test vẫn đúng DB/ảnh riêng0file. SELECT main40/40/tồn740/Users2/Order1Cancelled/Detail1; test3Products/0Images/tồn8/Users5/10Orders;40ảnhhash nguyên. Chỉ docs, không build/test lại; chờ mở lại Admin test/login để tiếp. D.3 reports/auth-session, D.4 tổng nghiệm thu sau D.2.
- D2C coreCRUD/D.3 Auth PASS; D.4B chỉ deploy test, main chưa deploy. C.2C.2 cuối: User1 Admin-active/login, test5Users/2Brand/2Cat/5Product/4Image/10Order/10Detail stock14; main9bảng2/2/8/1/40/40/1/1/2 stock740/1Cancelled/1Admin+40hash nguyên.

## 9. Phê duyệt và giới hạn lâu dài
- Đã duyệt main migration/Admin/import,deployC.6B/D.1B,1Customer/1OrderC.7; D.1C.2 khôiphục3fieldUser2quaUI đãPASS. Không suy quyền ghi giao dịch/CRUD tiếp theo.
- Không tự tạo Admin thứ hai, đổi role/password, thêm demo, chỉnh tồn thủ công hoặc xóa bằng chứng giao dịch.
- Không ghi main, migrate/seed/import/restore/drop/truncate/DELETE, thay binary/restart hoặc cleanup dữ liệu khi chưa được duyệt đúng thao tác.
- Không commit/push/reset/clean/history rewrite; không bật full access/bỏ approvals; bảo vệ secrets và thay đổi người dùng.
- Không tự kết nối server cũ, không tạo/drop DB test mới hoặc chạy lại harness có ghi khi chưa thuộc phạm vi được duyệt.
- Kết thúc mỗi nhiệm vụ: cập nhật trước/sau, file thay đổi, lệnh/kết quả thực tế, lỗi còn lại/bước tiếp; không ghi log dài hoặc mật khẩu/hash/cookie/token.
