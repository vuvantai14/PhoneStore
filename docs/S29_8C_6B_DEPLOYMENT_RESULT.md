# S29.8C.6B — Kết quả triển khai

Ngày: 2026-11-02, Asia/Bangkok. Được duyệt riêng DONG Y TIEP TUC TRIEN KHAI S29.8C.6B.

## Kết quả

Web PASS; Admin PASS khởi động/login/các màn hình chỉ đọc. Chưa nghiệm thu tạo đơn thật trên database chính.

- Trước copy không có process Web/Admin chính, cổng5015 trống; không đóng tiến trình test.
- Backup121file khớp hash/main cũ; staging121file khớp manifest; bộ source ở C.6-verify đã khớp staging và107tests PASS.
- Đã thay118binary/runtime files (Web60/Admin58); giữ3 appsettings. Không thay secrets/source appsettings/uploads/ảnh. SHA256118/118 sau copy PASS.
- Lệnh copy đầu dừng trước file đầu do PowerShell bọc mảng JSON; SELECT-file/hash từng dòng xác nhận staging nguyên vẹn/main chưa thay. Sửa cách đọc danh sách rồi mới copy; không rollback hoặc retry transaction/DB.

## Process và cấu hình

```text
Web PID22736:
C:\PhoneStore\PhoneStore\src\PhoneStore.Web\bin\Debug\net8.0\PhoneStore.Web.exe --urls http://localhost:5015
Content root: C:\PhoneStore\PhoneStore\src\PhoneStore.Web
Environment: Development

Admin PID10736:
C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\bin\Debug\net8.0-windows\PhoneStore.Admin.exe
```

Hai process được pin env connection localhost/PhoneStoreDB/Windows Authentication, uploads main absolute; không kế thừa connection test. SELECT sys.dm_exec_sessions xác nhận cả PID10736 và22736 dùng PhoneStoreDB, sleeping/open transaction0.

## Smoke chỉ đọc

| Hạng mục | Kết quả |
|---|---|
| Home | HTTP200/6cards |
| Products | HTTP200/40cards |
| Login, giỏ trống | HTTP200 |
| Orders, order detail, checkout anonymous | HTTP302 về login đúng ReturnUrl; không giả định authenticated Customer đã test trên main |
| Chi tiết/giá/tồn/ảnh |40/40detailHTTP200/name-price-stock-image khớp definition |
| File ảnh |40/40HTTP200 image/jpeg, hashuploads giữ nguyên |
| Admin login | Người dùng tự nhập mật khẩu và xác nhận đã đăng nhập; không gửi mật khẩu qua chat/tool |
| Dashboard |40sản phẩm/tồn740/0Customer/0Orders/0doanhthu, không lỗi |
| Sản phẩm | Grid40rows |
| Thương hiệu | Grid8rows |
| Đơn hàng | Grid0rows/trạng thái chưa có đơn đúng baseline |

Hậu kiểm main ONLINE:40Products/40ProductImages/8Brands/1Category/Users1/Roles2/Orders0/Details0/tồn740, admin@admin.com RoleId1 active. History InitialCreate+EnforceSingleAdmin giữ nguyên. Không gọi POST checkout/register/cancel/save hoặc migration/seed/import/restore.

## Hash và log

SHA256 sau triển khai:

```text
PhoneStore.Web.dll      920CF1136B73DB22997E13C8629914FA9BC6CC2136B718E7888ADCCEC67EAC8E
PhoneStore.Admin.dll    BCDA298E4BD4174F01F21E01B048DBB048097A041D71815DFC0A6F69DF350A77
PhoneStore.Business.dll EE5178DA25BB61BE87F8CBBF9C38C98219AE44315EBD0A328C3192AF6D6A30FA
```

Đầy đủ118hash tại src/PhoneStore.Admin/obj/S29.8C.6B-copy-plan.json; staging manifests/backupmanifest vẫn giữ. Process/log paths tại obj/S29.8C.6B-processes.json.
Log mỗi app: obj/S29.8C.6B-20261102_015659699Z.{stdout,stderr}.log. Không thấy fail/crit/unhandled/startup exception, stderr rỗng. Còn cảnh báo HTTPS redirect không xác định cổng do profile HTTP, không làm smoke thất bại.

Không build/test lại ở C.6B vì source/staging không đổi; evidence107/107 từ C.6 preflight. Không rollback, không thay DB test, không commit/push.

Dừng chờ duyệt riêng trước nghiệm thu tạo đơn thật. Khi cần shutdown tương lai phải xác minh console/process lại; Ctrl+Break đã hoạt động cho process cũ, không tự suy cho PID mới.
