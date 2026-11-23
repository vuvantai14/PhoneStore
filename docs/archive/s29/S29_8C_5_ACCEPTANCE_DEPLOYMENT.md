# S29.8C.5 — Nghiệm thu Admin Orders và chuẩn bị triển khai

Ngày 2026-11-02, Asia/Bangkok. Trạng thái: PASS các ca SQL/UI/HTTP đã chạy; CHƯA TRIỂN KHAI main.

## Nghiệm thu

| Hạng mục | Kết quả / bằng chứng |
|---|---|
| Danh sách Admin | PASS OrdersManagementControl thật hiển thị đủ test orders; refresh giữ các dòng fixture |
| Tìm kiếm | PASS nhấn nút Tìm kiếm với receiver fixture, đúng2 đơn |
| Lọc trạng thái | PASS combo Pending và nút tìm, đúng2 đơn đang Pending |
| Chi tiết/khách hàng | PASS OrderDetailDialog thật:1 item, email Customer và shipping receiver đúng |
| Chuyển hợp lệ | PASS nút Xác nhận→Chuyển sang giao hàng→Hoàn thành với confirmation thật |
| Chuyển sai | PASS Pending không có nút Completed; Shipping không có nút hủy; terminal không có actions; backend từ chối Shipping→Pending |
| Hủy/hoàn kho | PASS nút Hủy đơn, order Cancelled; tồn hoàn1, lần hủy Admin stale bị từ chối/Customer repeat thành công nhưng không hoàn thêm |
| Customer không hủy Shipping/Completed | PASS service gọi sau thao tác UI thực, trạng thái/tồn không đổi |
| Đồng bộ Customer | PASS receipt reader thấy UI Completed/Cancelled; HTTP test riêng thấy Confirmed do Admin service ghi, rồi Cancelled ở history/detail |
| Khóa checkout | PASS hai GUID độc lập tạo2OrderId; replay cùng token trả OrderId cũ; token của hai user không tái sử dụng đơn của nhau; token mới sau clear khác token cũ |

Harness `--execute-approved-s29.8c.5-ui` chạy actual OrdersManagementControl/OrderDetailDialog trên desktop qua gateway/scoped SQL services với guard connection/DB_NAME/server trước mỗi EF command. Test session Admin được thiết lập bằng reflection, không đăng nhập bằng password hoặc bypass auth trong source ứng dụng. Vì vậy đây là nghiệm thu module quản lý đơn, không phải kiểm thử full shell login. Reflection cũng dùng truy cập control fields cho assertion. Confirmation chỉ được bấm Yes khi thuộc đúng PID harness; không tác động app chính.

Mode UI exit0, tạo2 đơn mới (CompletedId10/CancelledId11) và final stock8 từ baseline9. Mode HTTP sync exit0 dùng Web helper127.0.0.1:5016/testDB, thêm1Customer/1Order rồi cancel; guard SQL sessionPID trướcPOST, chỉ dừng helper riêng. Không chạy lại fixture cũ hoặc cleanup bằng DELETE.

Không phát hiện lỗi chức năng mới, không sửa source ứng dụng/UI trong C.5. Thêm AdminOrdersUiChecks.cs/mode opt-in, mở rộng HttpCommerceChecks để nhận email fixture và kiểm Confirmed, thêm2 regression cases vào CommerceWorkflowTests/CartControllerTests. Các thay đổi source nghiệp vụ S29.8C có trước và được giữ nguyên.

Build solution/harness 0 warnings/errors; `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8C.5/ --verbosity minimal` PASS107/107,0fail/skip. `git diff --check` PASS. SQL UI/HTTP là bằng chứng riêng ngoài107 tests.

Test DB giữ nguyên: PhoneStoreDB_StockConcurrencyTest ONLINE;3Products tồn8/0/0,5Users (1Admin/4Customers),10Orders (5Cancelled/1Completed/4Pending). Không tạo DB/schema mới, không drop/restore/delete fixture.

## Main và schema

- Chỉ Web PID8692 chạy: C:\PhoneStore\PhoneStore\src\PhoneStore.Web\bin\Debug\net8.0\PhoneStore.Web.exe; chưa thấy Admin process. Không tự đóng/mở/restart app chính. Các DLL main khác bản S29.8C, chưa deploy.
- Main SELECT trước/sau: PhoneStoreDB ONLINE;40Products/tồn740/40Images/8Brands/1Category/1User(admin@admin.com Role1 active)/0Orders. Hai migrations InitialCreate+EnforceSingleAdmin không đổi. GET localhost:5015/products200/40cards.
- OrderCode nvarchar30, unique IX_Orders_OrderCode enabled. Source C dùng các cột hiện có, không model/schema change, không migration cần apply. SQL test đã chạy hai migrations giống main.
- Config Web lấy appsettings tại content root src/PhoneStore.Web; Admin lấy appsettings tại AppContext.BaseDirectory. Cả hai localhost/PhoneStoreDB/Windows Authentication; không sửa các config chính.
- Ảnh: C:\PhoneStore\PhoneStore\src\PhoneStore.Web\wwwroot\uploads\products; staging Admin resolve relative path về đúng thư mục này. Không copy/xóa/thay ảnh runtime.

## Artifact đã chuẩn bị

Binary mới đã sao chép từ output solution vừa chạy107 tests, kiểm SHA256 từng file:

```text
Web:
C:\PhoneStore\PhoneStore\src\PhoneStore.Web\bin\S29.8C.5-staging\net8.0\PhoneStore.Web.exe
Admin:
C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\bin\S29.8C.5-staging\net8.0-windows\PhoneStore.Admin.exe
```

Web62 file, Admin59 file (gồm dependencies/native runtimes). Mỗi staging có deployment-manifest.json. Không chỉ thay EXE: nghiệp vụ nằm trong DLL. Staging là bản sao riêng, không phải output có thể bị MSBuild clean khi đổi OutputPath. Web appsettings snapshot có trong staging nhưng triển khai sẽ giữ config main hiện có.

Đã backup chỉ đọc/copy toàn bộ binary Debug hiện tại,121file/33.427.203byte, kiểm hash từng file:

```text
C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\obj\S29.8C.5-binary-backups\20261102_013925853Z_de37e8a4ab1c44039836ca8a78baeecf
```

Có Web/, Admin/, backup-manifest.json. Đây là backup binary, không thay thế SQL backup hoặc backup ảnh. Snapshot trong obj chỉ phù hợp bước triển khai hiện tại, có thể mất khi clean; trước deploy kiểm tồn tại/hash và sao giữ ngoài thư mục build nếu cần theo phạm vi được duyệt.

## Kế hoạch thực hiện SAU phê duyệt riêng

1. Recheck process paths/PIDs, hashes source/staging/backup và Web/main config/DB metadata. Nếu có process mới, giao dịch ghi hoặc form chưa lưu thì dừng trao đổi; không force-close.
2. Người dùng kết thúc các form chưa lưu. Xác định cách Web hiện được quản lý (terminal/process trực tiếp); yêu cầu shutdown có kiểm soát để Kestrel ngừng nhận request và chờ request hiện tại hoàn tất. Không taskkill/Stop-Process-force. Nếu không điều khiển shutdown mềm được, báo trước thay vì tự cưỡng bức.
3. Dừng mềm đúng Admin nếu có và đúng Web sau khi được duyệt. Chờ exit hoàn toàn; không copy đè file đang dùng. Không dừng SQL Server hoặc đụng database test.
4. Chép trọn bộ staging Web vào src/PhoneStore.Web/bin/Debug/net8.0 và Admin vào src/PhoneStore.Admin/bin/Debug/net8.0-windows; giữ appsettings main, không copy manifest/config snapshot đè cấu hình; không chép wwwroot runtime.
5. So hash bộ executable/DLL/deps/runtimeconfig/native runtimes với manifest; kiểm connection và ảnh. Khởi động Web trong content root src/PhoneStore.Web với Development/localhost:5015 và pin env main, Admin working directory chính với env main/uploads rõ ràng. Không dùng env của test helper.
6. Smoke CHỈ ĐỌC: Home/products200,40cards, vài details/giá/tồn và40ảnh200; anonymous checkout/orders→login; SQL counts/history/singleAdmin không đổi; Admin login window responsive. Người dùng tự nhập mật khẩu nếu cần; không test checkout/cancel/save trên main.
7. Chỉ mở lại thao tác ghi khi người dùng nghiệm thu. Không suy phê duyệt deploy là phê duyệt tạo đơn hoặc sửa catalog.

## Rollback binary nếu cần

- Chỉ sau khi có phê duyệt rollback cụ thể: dừng mềm đúng process mới, kiểm không có request ghi đang chạy, khôi phục bộ binary cũ tương ứng từ backup, giữ config/ảnh/database, kiểm hash rồi restart/smoke chỉ đọc. Không rollback migration/restore DB.
- Nếu lỗi deployment sau copy một phần: không khởi động bộ DLL lẫn version; báo tình trạng và xin hướng dẫn, không tự sửa schema hoặc retry checkout.
- Rollback về code cũ làm mất bảo vệ idempotency/stale stock/hủy Customer mới. Nếu đã có yêu cầu ghi trên bản mới, phải đánh giá outcome trước rollback; không replay yêu cầu lên bản cũ vì có thể tạo đơn trùng. Ưu tiên chỉ đọc cho tới khi nghiệm thu hoàn tất.

Rủi ro còn lại: kiểm thử hữu hạn, chưa exhaustive stress/deadlock/mất kết nối; snapshot stock không bảo vệ mọi field/ABA; full shell login chưa nằm trong UI harness. Không có lý do đổi schema ở bước này. Dừng chờ duyệt triển khai; không commit/push.
