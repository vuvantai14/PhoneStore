# S29.8D.1C — Nghiệm thu sửa hồ sơ Customer

Ngày 2026-11-02, Asia/Bangkok. **PARTIAL — dừng sau lần thử bấm Lưu đầu tiên vì không xác nhận được kết quả qua automation. SQL xác nhận hồ sơ chưa thay đổi.**

## Phạm vi và preflight — PASS

Đã đọc AGENTS, PROGRESS và báo cáo D.1B. Người dùng duyệt sửa tạm/khôi phục ba trường của đúng Customer UserId2 qua giao diện Web; không dùng SQL/API thay cho form.

- Web PID19388 và Admin PID10736 đúng executable, đều responsive; Web60/60 file khớp manifest D.1B, preserved101/101 hash giữ nguyên.
- localhost/DESKTOP-ES9362P/PhoneStoreDB ONLINE; Customer UserId2/tai123@gmail.com/RoleId2/active, Admin UserId1/RoleId1/active.
- Baseline Products40, Images40, tổng tồn740, Users2/Customer1, Orders1/OrderDetails1; OrderId1 Cancelled.
- SELECT lưu profile gốc và snapshot đầy đủ, sắp xếp theo khóa, của Roles/Users/Brands/Categories/Products/ProductImages/Orders/OrderDetails/migration history. Users projection không đọc mật khẩu/hash.
- Browser Customer đang đăng nhập đúng email, form GET hiển thị ba trường khớp dữ liệu gốc trước nhập.

## Kết quả trước / nhập thử / SQL sau khi dừng

| Trường | SQL trước | Giá trị đã nhập trên form | SQL sau khi dừng |
|---|---|---|---|
| FullName | tài vũ | Khách hàng Kiểm thử | tài vũ |
| Phone | 09032657212 | 0900000000 | 09032657212 |
| Address | NULL | Địa chỉ kiểm thử PhoneStore | NULL |

**Chưa có checkpoint SQL ghi thành công ba giá trị thử. Không thực hiện bước khôi phục**, vì hồ sơ trong database vẫn ở trạng thái gốc và người dùng yêu cầu dừng khi có lỗi/kết quả bất thường. Không coi việc không đổi dữ liệu là đã PASS sửa rồi khôi phục.

## Diễn biến và giới hạn bằng chứng

1. Helper CDP mở đúng `/account/profile`, xác minh email/session/giá trị gốc rồi điền ba giá trị đã duyệt.
2. Tạo marker `edit.attempt` trước thao tác; phát một cặp mousePressed/mouseReleased vào nút Lưu hồ sơ. Không gửi fetch/API/SQL ghi và không tự click lại.
3. Trong khoảng chờ15giây, helper không quan sát được trạng thái thành công thỏa điều kiện nên dừng với `Save outcome unclear/error; STOP`.
4. Đọc DOM sau lỗi: vẫn ở `/account/profile`, authenticated=true, form còn ba giá trị thử, không thấy validation error hoặc success message; greeting vẫn `Xin chào, tài vũ`.
5. SELECT xác nhận ba trường vẫn là gốc, email/UserId/RoleId/active không đổi. Không thấy câu UPDATE hoặc lỗi ứng dụng trong phần log được kiểm tra.
6. So sánh sau dừng: **9/9 snapshot giống trước**, gồm Users với các cột công khai/nội bộ không chứa credentials. Binary/hash/PID không đổi.

Chưa xác minh được thao tác mouse có thực sự kích hoạt submit hay không; metadata POST không được ghi bền trước khi helper dừng. Vì vậy **không khẳng định đã gửi POST**, cũng không kết luận service/controller lưu lỗi. Không đủ bằng chứng để tuyên bố database đã commit hay cookie sau cập nhật đã hoạt động.

Form browser hiện còn nội dung nhập thử chưa được xác nhận lưu. Không tự reload/xóa nội dung hoặc bấm tiếp sau khi đã dừng; SQL mới là bằng chứng dữ liệu đang giữ nguyên.

## Phân loại nghiệm thu

| Hạng mục | Kết quả |
|---|---|
| Đúng process, binary, database và Customer | PASS |
| Ghi nhận hồ sơ gốc bằng SELECT | PASS |
| Nhập đúng ba giá trị được duyệt vào UI | PASS |
| Lưu thành công / reload thấy dữ liệu mới / SELECT ba trường mới | PARTIAL, chưa xác minh được |
| Email, UserId, RoleId, active không đổi | PASS SELECT trước/sau |
| Cookie sau đổi tên và greeting tên mới | NOT TESTED; phiên gốc vẫn đăng nhập, greeting tên cũ |
| Khôi phục bằng Web và xác nhận sau reload | NOT TESTED; chưa thực hiện khi bước lưu đầu chưa đạt |
| Catalog, ảnh, kho, đơn Cancelled, tài khoản Admin | PASS hậu kiểm, không thay đổi |

## Bất biến và files

PhoneStoreDB ONLINE; Products40/Images40/Brands8/Category1/Users2/Orders1/Details1/tồn740; OrderId1 Cancelled, Customer1 và Admin active1. Snapshot9/9 không đổi. Không đọc/ghi password hoặc hash; không tạo Customer/đơn, sửa Admin/catalog/stock, SQL DML, migration/seed/restore, thay binary, commit/push.

Chỉ tạo báo cáo này, cập nhật PROGRESS và helper/evidence runtime trong Web/obj. Không sửa source ứng dụng. Không build/test lại vì không thay code; **129/129 PASS là baseline D.1B, không phải lần chạy mới**.

Artifact:
`src/PhoneStore.Web/obj/S29.8D.1C-20261102_031553601Z_0f70f82b55224b31a97cc4420a0835cf`

Gồm profile-before.json, profile-after-stop.json, database-before.json, database-after-stop.json và edit.attempt. Helper/state: Web/obj/S29.8D.1C-browser.mjs, S29.8D.1C-read.mjs, S29.8D.1C-state.json. Không chạy lại helper edit vì marker đã tồn tại; không xóa marker để retry.

**Dừng chờ chỉ đạo tiếp theo theo điều kiện an toàn của người dùng.** Phương án đề xuất là người dùng trực tiếp bấm Lưu trên form thật, báo kết quả để SELECT kiểm tra, rồi mới thực hiện bước khôi phục đúng giá trị gốc qua Web. Nếu chọn điều tra automation trước, chỉ đọc trạng thái/hit testing; không thử thêm thao tác ghi khi chưa có chỉ đạo tiếp tục.
