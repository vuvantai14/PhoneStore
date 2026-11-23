# S29.8D.4D.1 — Admin main pre-deployment readiness

Ngày kiểm tra: 2026-11-02 (Asia/Bangkok). Phạm vi chỉ đọc và chuẩn bị tài liệu; không shutdown/restart, không thay binary/config, không DML, migration, deploy, restore, commit hoặc push.

## 1. Trạng thái tiến trình và môi trường

| Thành phần | Kết quả |
|---|---|
| Admin main | **Không chạy** tại thời điểm kiểm tra; không có PID main để dừng. Binary đích dự kiến: `src/PhoneStore.Admin/bin/Debug/net8.0-windows/PhoneStore.Admin.exe`. |
| Admin test | **Đang chạy**, PID `15548`, executable `src/PhoneStore.Admin/obj/S29.8D.4C.1-20261102_155007187/app/PhoneStore.Admin.exe`. Không bị tác động. |
| Database test | `appsettings.json` cạnh PID 15548 trỏ `localhost/PhoneStoreDB_StockConcurrencyTest`. Lần lấy mẫu hiện tại không có SQL session hoạt động cho PID (ứng dụng idle); D.4C.1 và hai ca revoke trước đó đã chứng minh runtime dùng test DB. |
| Upload test | `src/PhoneStore.Admin/obj/S29.8D.2C-20261102_035250536Z/uploads`, thư mục thường, không phải link/reparse point; hiện có 5 file fixture. |
| Cấu hình main | `localhost/PhoneStoreDB`; uploads relative được resolve thành `src/PhoneStore.Web/wwwroot/uploads/products`, thư mục thường, 40 ảnh. |

Main và test có database khác nhau và đường dẫn uploads khác nhau. Rào chắn triển khai bắt buộc là **không sao chép `appsettings.json` từ staging test**. Environment override cũng phải được kiểm tra lại trong tiến trình main mới trước smoke test.

## 2. Binary D.4B và manifest

- Bản đã nghiệm thu: `src/PhoneStore.Admin/obj/S29.8D.4C.1-20261102_155007187/app` (59 file gồm cấu hình test).
- Manifest triển khai mới: [S29_8D_4D_1_ADMIN_RUNTIME_SHA256.txt](S29_8D_4D_1_ADMIN_RUNTIME_SHA256.txt), **58 file runtime**, SHA-256, cố ý loại `appsettings.json`.
- 58/58 file hiện có trong `bin/Debug/net8.0-windows` khớp hash staging D.4B trên đĩa. Đây chưa phải bằng chứng deploy/runtime main vì Admin main không chạy và chưa smoke trên `PhoneStoreDB`.
- Bản D.4B đã nghiệm thu có build 0 warnings/errors, 161/161 tests PASS và 8/8 SQL read-only checks PASS. Không chạy lại build/tests ở bước tài liệu này vì source không được sửa.
- Dependency native/managed và thư mục `runtimes/*` đều nằm trong bộ 58 file; khi deploy phải thay đủ bộ, không chỉ EXE/DLL dự án.

## 3. Backup, staging và shutdown đề xuất cho D.4D.2

Vị trí backup mới đã dành tên nhưng **chưa tạo**:

`src/PhoneStore.Admin/obj/S29.8D.4D.2-binary-backups/20261102_162250451_predeploy`

`Test-Path` tại thời điểm lập kế hoạch là false, nên không ghi đè backup cũ. Khi D.4D.2 được phê duyệt:

1. Khám phá lại PID/path/command line; không dùng PID lịch sử. Kiểm tra không có dialog đang lưu và SQL session không có transaction mở.
2. Nếu Admin main đang chạy, người dùng đóng form chính theo luồng UI bình thường; chờ process thoát. Không force-kill và không đóng PID test.
3. Backup toàn bộ thư mục runtime main hiện tại vào thư mục timestamp trên; tạo manifest SHA-256 riêng và xác minh đủ file. Chụp hash/copy cấu hình main riêng, không đụng uploads.
4. Tạo staging main từ đúng 58 file trong manifest. Giữ nguyên `appsettings.json` main; quét staging/config để chắc chắn không có `PhoneStoreDB_StockConcurrencyTest` hoặc đường dẫn uploads test.
5. Chỉ khi main đã dừng, sao chép đủ 58 file; đối chiếu 58/58 SHA-256. Xác minh lại main `appsettings.json` và 40 ảnh không đổi.
6. Khởi động đúng `bin/Debug/net8.0-windows/PhoneStore.Admin.exe`; xác minh SQL session thực tế tới `PhoneStoreDB` và upload root main trước khi đăng nhập/thao tác.

## 4. Rollback binary

Nếu khởi động hoặc smoke read-only thất bại:

1. Dừng mềm đúng Admin main mới; không tác động Admin test.
2. Xác minh không có thao tác ghi/transaction đang mở.
3. Phục hồi toàn bộ runtime từ backup D.4D.2 và kiểm manifest, đồng thời giữ/đối chiếu đúng cấu hình main đã backup.
4. Khởi động binary cũ và chạy smoke read-only.
5. Không restore database. Nếu hash/config hoặc kết quả commit không chắc chắn, dừng và xin chỉ đạo thay vì rollback tự động.

## 5. Smoke test read-only sau triển khai

- Login bằng Admin chính do người dùng tự nhập mật khẩu; không ghi credential.
- Dashboard: đối chiếu KPI với SELECT cùng thời điểm.
- Reports: kiểm khoảng có dữ liệu, cùng ngày và empty range; doanh thu/average chỉ từ `Completed`, đơn `Cancelled` không tính doanh thu.
- Products/Brands/Categories/Customers/Orders: mở danh sách, tìm/lọc/xem chi tiết, **không bấm Lưu/đổi trạng thái/upload/unlink**.
- Xác minh main runtime session dùng `PhoneStoreDB`, upload root đúng Web và không có lỗi log mới.
- Hậu kiểm lại snapshot DB và 40 SHA-256 ảnh; mọi sai lệch dừng smoke và báo cáo.

## 6. Database và schema

SELECT read-only tại thời điểm kiểm tra:

- `DESKTOP-ES9362P/PhoneStoreDB` ONLINE.
- Roles 2, Users 2, Brands 8, Categories 1, Products 40, ProductImages 40, Orders 1, OrderDetails 1.
- Tổng tồn 740; đúng một đơn `Cancelled`; đúng một Admin active.
- Hai migration: `InitialCreate`, `EnforceSingleAdmin`; source không có migration mới yêu cầu D.4B.
- 40/40 ảnh demo khớp SHA-256 baseline; không thiếu/đổi file.

**Không cần migration hoặc thay đổi schema cho D.4B.**

## 7. Known limitations phải được chấp nhận

- Nội dung chính xác của thông báo revoke chưa được giữ lại đầy đủ; việc chặn DML, clear session và quay về LoginForm đã PASS runtime.
- Fresh authorization check và commit nghiệp vụ không nằm trong cùng transaction; revoke đúng khoảng giữa hai bước vẫn có thể cho một write đang in-flight hoàn tất.
- UI chưa hỗ trợ chỉnh `DisplayOrder`; thay file ảnh trực tiếp NOT SUPPORTED (luồng hiện có: add → primary → unlink).
- Đóng form giữa lúc Save chưa có UI evidence đầy đủ; chưa có lỗi thực tế đã chứng minh.
- Text validation Brand chưa được quan sát đầy đủ dù việc từ chối dữ liệu đã được chứng minh.
- Customer edit/lock trên Admin chưa nghiệm thu UI đầy đủ; không chặn triển khai riêng các fix D.4B nhưng hệ thống tổng thể vẫn chưa nên gọi là hoàn tất tuyệt đối.

## 8. Kết luận

**READY** cho một phê duyệt triển khai riêng S29.8D.4D.2, với điều kiện người dùng chấp nhận các known limitations trên và D.4D.2 phải thực hiện fresh backup/manifest, preserve cấu hình main, shutdown mềm, kiểm hash và smoke read-only đúng kế hoạch.

Chưa có triển khai nào được thực hiện trong D.4D.1.
