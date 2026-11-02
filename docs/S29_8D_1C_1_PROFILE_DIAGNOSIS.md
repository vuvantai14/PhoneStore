# S29.8D.1C.1 — Chẩn đoán form Customer Profile

Ngày 2026-11-02, Asia/Bangkok. **PARTIAL: đã xác minh form/validation và trạng thái lưu thủ công; nguyên nhân chính xác của lần automation cũ vẫn UNKNOWN.** Không thử lưu, replay, reload hoặc điều hướng form main trong lượt này.

## 1. Thay đổi trạng thái so với checkpoint trước

- Báo cáo D.1C trước đó ghi SQL còn giá trị gốc sau helper timeout. Đó là checkpoint lịch sử, không phải trạng thái hiện tại.
- Quan sát browser lúc **03:22:33 UTC / 10:22:33 Bangkok** thấy đang đăng nhập tại `/account/profile`, greeting `Xin chào, Khách hàng Kiểm thử`.
- SELECT **03:22:46 UTC** thấy ba trường UserId2 đã mang giá trị kiểm thử.
- Đã hỏi và nhận xác nhận người dùng: **“Có, tôi đã tự bấm Lưu”** sau báo cáo PARTIAL. Không suy rằng helper cũ đã thành công hoặc mọi UPDATE trong log đều thuộc cùng thao tác.
- Trong lượt chẩn đoán này Codex chỉ đọc UI/SQL/log, chạy tests SQLite; không click hoặc gửi request ghi tới Web chính. Không tự khôi phục hồ sơ.

| Trường | Gốc đã ghi ở D.1C | SQL hiện tại |
|---|---|---|
| FullName | tài vũ | Khách hàng Kiểm thử |
| Phone | 09032657212 | 0900000000 |
| Address | NULL | Địa chỉ kiểm thử PhoneStore |

UserId2, email tai123@gmail.com, RoleId2 và active giữ nguyên. Phiên browser vẫn authenticated và greeting khớp tên mới. Có bằng chứng lưu thủ công đã có hiệu lực và phiên hiện tại vẫn dùng được; không khẳng định đã quan sát success message lúc người dùng lưu hoặc đã nghiệm thu bước khôi phục.

## 2. Form và validation — PASS trong phạm vi quan sát

- URL đúng `http://localhost:5015/account/profile`, document complete.
- Form action `/account/profile`, method POST, enctype application/x-www-form-urlencoded, noValidate=false.
- Input names đúng FullName/Phone/Address; nút `Lưu hồ sơ`, type submit, enabled, visible, pointer-events auto; hit-test tâm nút hiện tại đúng BUTTON.
- Antiforgery hidden input có mặt và không rỗng. Chỉ ghi boolean; không lưu/hiển thị giá trị token hoặc cookie. Không xác minh tính hợp lệ của token cho request cũ bằng replay.
- Native validity của ba field đều true, không có validationMessage; không thấy thông báo success/error trên DOM hiện tại.
- Dữ liệu thử: tên19 ký tự (server2–100), phone10 chữ số (≤20, hợp PhoneAttribute), địa chỉ27 ký tự (≤255), đều phù hợp service/model validation.
- HTML hiện tại không có cờ required native cho FullName; required/min-length vẫn được kiểm server qua DataAnnotations và service. Không coi native validity là thay thế server validation.

Source được đối chiếu: ProfileController GET/POST, CustomerProfileViewModel, Views/Profile/Index, CustomerProfileService/Store, Program, layout và site.js.

- POST có Customer policy và ValidateAntiForgeryToken; đích cập nhật từ NameIdentifier, không nhận UserId form/query.
- Email BindNever; chỉ sửa ba cột, update có điều kiện Customer active, không nhận role/hash.
- ModelState lỗi trả form; business invalid/unavailable có thông báo; thành công refresh Name claim/giữ ticket, TempData rồi redirect GET.
- Không thấy sai action/method/input name hoặc lỗi binding rõ ràng trong luồng được kiểm tra.
- site.js chỉ xử lý ảnh, filter panel và gallery; không có submit/preventDefault cho profile. Trang chỉ nạp `/js/site.js`.
- Quan sát DevTools hiện tại: không có inline onsubmit; listener submit ở form/document/window và click trực tiếp ở form/button đều0. Không thu được cached Console exception/log error qua phiên quan sát này. Điều này không chứng minh quá khứ không có lỗi hoặc không có listener ở mọi ancestor.

## 3. Có POST hay không? — phân biệt bằng chứng

| Bằng chứng | Kết luận có thể rút ra |
|---|---|
| Log Web hiện có3 câu `UPDATE [u] SET Address,Phone,FullName ...` | Có ba lệnh cập nhật profile đã được EF thực thi; log tham số bị che, không xác định từng giá trị hoặc nguồn click |
| Không có Request starting/finished hoặc `POST /account/profile` trong log | HTTP method/status của lần thử cũ UNKNOWN; cấu hình Microsoft.AspNetCore=Warning không ghi đầy đủ request thông thường |
| Log không có fail/crit/unhandled; stderr0bytes | Không thấy lỗi backend trong log hiện có; không bảo đảm mọi response4xx/validation đã được log |
| Network helper cũ chỉ giữ array trong bộ nhớ, không ghi khi timeout | Không phục hồi được request history của phiên CDP đó; không replay để đoán |
| Navigation entry hiện tại path profile/status200, không có method | Chứng minh document hiện tại200, không dùng nó để kết luận POST cũ200 hoặc302 |
| Người dùng xác nhận tự bấm Lưu + SQL/greeting đổi đúng | Hỗ trợ kết luận luồng lưu UI thủ công hoạt động; không xác định helper cũ có kích hoạt submit |

Không đọc password/cookie/token/request bodies. Chỉ quan sát Console/Log cached hiện có, không tạo request profile mới để bổ sung bằng chứng.

## 4. Phân loại nguyên nhân

| Khả năng | Phân loại |
|---|---|
| UI automation chưa kích hoạt submit | **PARTIAL / nghi vấn có căn cứ**, chưa chứng minh. Helper gọi scrollIntoView rồi đo tọa độ/click ngay; CSS html có scroll-behavior:smooth. Nếu cuộn đang chuyển động, tọa độ có thể thay đổi trước click; helper không lưu tọa độ/hit-test/event history cũ. Không cuộn/click để tái hiện trên main. |
| Client validation chặn dữ liệu thử | **Không thấy bằng chứng hiện tại**: cả3 field valid, dữ liệu hợp server rule, không thông báo. UNKNOWN cho trạng thái đúng thời điểm click cũ. |
| POST bị từ chối (auth/CSRF/4xx) | **UNKNOWN cho lần helper cũ** vì thiếu request/status history; token hiện có không chứng minh token cũ hợp lệ. |
| Backend xử lý lỗi | **Không phát hiện lỗi source/runtime trong phạm vi này**; save thủ công đã có hiệu lực và22tests PASS. Không loại trừ tuyệt đối sự cố thoáng qua cũ. |
| Kết luận root cause helper timeout | **UNKNOWN**, không đánh dấu application FAIL hoặc tự sửa/triển khai source. |

Nếu cải thiện helper sau khi được duyệt nhiệm vụ phù hợp: chờ vị trí nút ổn định/hit-test sau cuộn, lưu metadata method/path/status ngay khi nhận event trước mọi timeout, tuyệt đối không ghi body/token/cookie và không tự retry POST. Đây là đề xuất, chưa thay helper ghi hoặc source.

## 5. Kiểm thử và bất biến

```powershell
dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-restore -p:OutputPath=bin/S29.8D.1C.1-tests/ --filter FullyQualifiedName~CustomerProfile --verbosity minimal
```

**22/22 profile tests PASS**,0fail/skip; build phụ thuộc không báo warning/error. Output riêng, dùng SQLite in-memory và DataProtection ephemeral; không SQL main, không thay binary đang chạy. Không chạy lại full suite: **129/129 là baseline D.1B**.

- Web19388/Admin10736 đúng binary, main60/60 Web hash vẫn khớp D.1B.
- PhoneStoreDB ONLINE;40Products/40Images/8Brands/1Category/Users2/Customer1/Adminactive1/Orders1/Details1/tồn740; OrderId1 Cancelled.
- Đối chiếu snapshot8 bảng còn lại với D.1C nguyên vẹn. Đối với Users, chỉ trong SELECT chiếu ba trường Customer2 về giá trị gốc để so hash: khớp toàn bộ snapshot gốc. Như vậy các cột đã chọn ngoài đúng3field và tài khoản Admin giữ nguyên; credentials không được đọc/so sánh.
- Không có khôi phục DB hoặc khôi phục form. Không source/config/binary change, migration/seed/restore, tạo Customer/Order, commit/push.

Files: tạo báo cáo này, cập nhật PROGRESS. Evidence đọc nằm trong artifact D.1C: diagnostic-readonly.json, diagnostic-profile-current.json, diagnostic-database-comparison.json; helper quan sát `src/PhoneStore.Web/obj/S29.8D.1C.1-observe.mjs` chỉ đọc, không gọi lại helper edit.

## 6. Một bước tiếp theo cần phê duyệt

**Vì lưu thủ công đã xảy ra, bước phù hợp tiếp theo là người dùng khôi phục qua form Web đúng một lần** về tài vũ / 09032657212 / địa chỉ để trống (DB NULL), rồi báo kết quả để SELECT và kiểm greeting/session sau reload. Không cần bấm Lưu lại ba giá trị thử hoặc tạo request test mới.

Dừng chờ phê duyệt bước khôi phục này; xác nhận người dùng đã bấm Lưu không được coi là phê duyệt ghi thêm trong nhiệm vụ chẩn đoán chỉ đọc.
