# S29.8D.1C.2 — Khôi phục hồ sơ Customer qua Web

Ngày 2026-11-02, Asia/Bangkok. **PASS — đã khôi phục đúng ba trường, địa chỉ SQL NULL, giữ phiên đăng nhập và các bất biến.**

## Preflight và phạm vi

Đã đọc AGENTS, PROGRESS, báo cáo D.1C và D.1C.1. Phê duyệt lần này chỉ khôi phục FullName/Phone/Address của Customer UserId2 bằng giao diện `/account/profile`.

- SELECT xác minh localhost/DESKTOP-ES9362P/PhoneStoreDB ONLINE, đúng email tai123@gmail.com, RoleId2, active.
- Trước khôi phục SQL có đúng ba giá trị thử từ lần người dùng tự lưu: Khách hàng Kiểm thử / 0900000000 / Địa chỉ kiểm thử PhoneStore.
- Web PID19388 và Admin PID10736 đúng executable; không restart hoặc thay binary.
- Browser đang đăng nhập đúng Customer, form hiện tại khớp dữ liệu thử; không đọc/ghi mật khẩu hoặc cookie/token vào evidence.

## Thao tác và bằng chứng

1. Điền qua UI: FullName=tài vũ, Phone=09032657212, Address rỗng hoàn toàn.
2. Kiểm tra lại email đúng, ba giá trị chính xác, form POST đúng route, duy nhất một nút `Lưu hồ sơ` enabled và native validation hợp lệ. Chưa lưu ở bước chuẩn bị.
3. Kích hoạt **một lần native HTML button.click()** trên chính nút submit đã xác minh, giữ nguyên luồng form validation/antiforgery/cookie; không fetch/API/service call hoặc SQL ghi. Không dùng tọa độ chuột không chắc chắn, không retry.
4. Trang hiển thị **“Đã cập nhật hồ sơ của bạn.”**, greeting **“Xin chào, tài vũ”**, đúng ba giá trị và không lỗi validation.
5. Reload bằng GET: vẫn ở profile, authenticated=true, tên/điện thoại đúng, textarea địa chỉ trống, greeting tài vũ; không đăng nhập lại.
6. Network metadata ghi bền: **1 POST /account/profile → 302 → GET200**, rồi reload GET200. Không lưu header/body, cookie hoặc token.
7. SELECT hậu kiểm **03:26:20 UTC / 10:26:20 Bangkok** và so sánh snapshot gốc.

| Trường | Trước khôi phục | Sau lưu + reload + SELECT |
|---|---|---|
| FullName | Khách hàng Kiểm thử | tài vũ |
| Phone | 0900000000 | 09032657212 |
| Address | Địa chỉ kiểm thử PhoneStore | **NULL**; textarea rỗng |
| UserId | 2 | 2 |
| Email | tai123@gmail.com | giữ nguyên |
| RoleId / active | 2 / true | giữ nguyên |

`CASE WHEN Address IS NULL THEN 1 ELSE 0 END` trả **1**, không phải chuỗi rỗng. Nghiệm thu phiên/cookie ở mức hành vi: đổi tên và reload vẫn authenticated/greeting mới. Không đọc nội dung cookie; việc giữ expiry/persistent đã có bằng chứng HTTP test cách ly D.1.

## Các bất biến — PASS

- PhoneStoreDB ONLINE: Products40, Images40, Brands8, Category1, tổng tồn740.
- Users2, Customer1, Admin active1; không tạo tài khoản hoặc sửa Admin.
- Orders1, OrderDetails1; OrderId1 giữ Cancelled.
- **9/9 snapshot khớp trạng thái gốc D.1C**: Roles, Users (projection không chứa credentials), Brands, Categories, Products, ProductImages, Orders, OrderDetails, migration history. Không chỉ so số lượng; dữ liệu được SELECT thành JSON sắp xếp theo khóa để so hash.
- Web60/60 runtime hashes và preserved101/101 (Admin output/uploads/config) không thay đổi; Web19388/Admin10736 responsive.
- Không SQL DML, migration/seed/restore, deploy, sửa email/password/role, tạo Customer/Order, commit/push. Việc khôi phục chỉ qua đúng một lần lưu UI được duyệt.

## Files, tests và kết luận

Tạo báo cáo này, cập nhật PROGRESS; helper/evidence chỉ trong Web/obj. Không sửa source ứng dụng hoặc cấu hình. Không build/test lại vì không thay code:22/22 profile tests gần nhất D.1C.1 và129/129 regression D.1B là bằng chứng đã chạy trước đó, không báo thành kết quả mới.

Artifact dùng tiếp:
`src/PhoneStore.Web/obj/S29.8D.1C-20261102_031553601Z_0f70f82b55224b31a97cc4420a0835cf`

Evidence mới: restore-C2-prepared.json, restore-C2.attempt, restore-C2-network.json, restore-C2-success.json/.png, restore-C2-reloaded.json, restore-C2-profile-sql.json, restore-C2-database-comparison.json. Helper `src/PhoneStore.Web/obj/S29.8D.1C.2-browser.mjs`. Giữ attempt marker, không chạy lại bước save.

**Đề xuất đóng S29.8D.1 — Customer Profile**: source/tests, deployment, GET main, lưu thủ công đã được người dùng xác nhận và SQL kiểm chứng, khôi phục qua UI/Address NULL/session/invariants đều đạt trong phạm vi nghiệm thu. Lần click helper cũ vẫn chưa xác định nguyên nhân chính xác; không coi đó là lỗi application đã được chứng minh.

Đề xuất bước kế tiếp **S29.8D.2 — CRUD Admin**: xác định ma trận ca sản phẩm/ảnh/brand/category/Customer, dùng dữ liệu và thư mục ảnh cách ly; không tự ghi PhoneStoreDB chính hoặc bắt đầu thao tác CRUD mới khi chưa được chỉ đạo.
