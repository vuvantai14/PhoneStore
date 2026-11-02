# S29.8C — Giỏ hàng, checkout, hủy đơn và quản lý đơn

Ngày: 2026-11-02, Asia/Bangkok. Các kiểm thử ghi chỉ dùng PhoneStoreDB_StockConcurrencyTest và SQLite cách ly. Không thay schema/migration, không ghi main, không triển khai binary chính.

## 1. Audit và trạng thái triển khai

- Đã đọc AGENTS.md, PROGRESS, audit S29.8A và kết quả S29.8B/B.1/B.2 trong PROGRESS cùng README harness.
- S29.8B.2 chưa triển khai: bốn DLL main không khớp staging B.2. Lúc kiểm tra chỉ còn Web PID8692 từ bin/Debug/net8.0; không thấy Admin chạy, không tự khởi động lại hoặc dừng ứng dụng.
- CartController đã có add/update/remove, kiểm quantity/stock/active và anti-forgery; CartPresenter lấy giá hiện tại từ DB. Giữ luồng này và bổ sung tests, chỉ thêm guard quantity>0 ở CartLineViewModel.Available.
- Checkout có transaction/giá snapshot nhưng chống trùng chỉ bằng memory cache, không bền qua restart. Khi thất bại sau xử lý một dòng, entity stock đã sửa còn nằm trong ChangeTracker nếu dùng lại scope; đã sửa cleanup.
- Admin đã có list/search/status filter/detail/workflow Pending→Confirmed→Shipping→Completed, Pending/Confirmed→Cancelled, terminal không chuyển tiếp. Không viết lại controls đã có.
- Customer đã có history/detail theo UserId nhưng chưa có hủy đơn. Đã bổ sung endpoint và nút tối thiểu theo workflow hiện tại.

## 2. Thay đổi

### Checkout bền vững, không thêm schema

- Web dùng CreateIdempotentAsync, bắt buộc token GUID N32 sinh trong session; không nhận giá/tổng tiền client.
- OrderCode ổn định: ORD- cộng 26 ký tự hex SHA256 từ UserId và token chuẩn hóa, vừa cột30 ký tự hiện có. Unique index OrderCode đã tồn tại. Cùng User/token luôn tra lại đơn đã commit, kể cả Cancelled; không đặt lại/trừ kho lại.
- SQL Server dùng sp_getapplock transaction-owned theo OrderCode và đọc lại đơn sau lấy lock. Ràng buộc unique là lớp bảo vệ cuối; không retry transaction tự động khi timeout/deadlock/commit không rõ.
- Controller POST replay tra cứu DB theo user/token trước khi yêu cầu token session còn tồn tại, nên replay sau clear cart/restart trả cùng receipt. Không có order đã commit thì vẫn yêu cầu token session hợp lệ trước tạo mới.
- Customer active/role Customer được kiểm tra khi tra cứu và bên trong transaction. GetCheckoutOrderId không tiết lộ đơn người khác.
- ProductsForOrderAsync khóa UPDLOCK,HOLDLOCK theo ProductId tăng dần trên SQL Server, giữ transaction Serializable và giá server snapshot. ClearTracking trong finally loại bỏ thay đổi chưa commit khỏi scope.
- CreateAsync không token vẫn dành cho caller nội bộ/legacy hiện có; Web checkout đã chuyển sang API bắt buộc token. Caller mới cần sử dụng CreateIdempotentAsync.

### Hủy đơn và quản lý đơn

- CancelForCustomerAsync kiểm active Customer và ownership trong transaction có khóa order. Customer chỉ hủy Pending/Confirmed, giống workflow Admin hiện tại; Shipping/Completed bị từ chối.
- Gửi lặp khi đã Cancelled trả thành công cho đúng owner nhưng không hoàn kho lần nữa. Admin vẫn dùng expected status và các transition cũ.
- Hủy lấy khóa sản phẩm theo thứ tự ID giống checkout, cập nhật trạng thái và hoàn kho cùng transaction. Failure trước commit rollback cả hai.
- POST /orders/{id}/cancel có Customer policy/anti-forgery. Truy cập/hủy đơn người khác trả404; detail hiển thị thông báo và nút hủy chỉ khi được phép. Receipt thêm OrderId ở DTO, không thay entity/schema.

## 3. Kết quả theo chức năng

| Nhóm | Kết quả | Bằng chứng / giới hạn |
|---|---|---|
| Cart add/update/remove | PASS | Controller tests SQLite/session cách ly và HTTP thực; không tạo order trong các thao tác giỏ |
| Quantity/active/out-of-stock/missing product | PASS | Cases0/-1/int.MaxValue/vượt stock/inactive/stock0/missing; quantity0 còn được kiểm HTTP |
| Giá/tổng tiền server | PASS | Thay giá trong DB test làm cart total đổi; HTTP gửi price/TotalAmount giả vẫn tạo UnitPrice/total từ server |
| Session ownership | PASS phạm vi session | Hai session test độc lập; anonymous được dùng giỏ, checkout/order cần Customer. Giỏ hiện không lưu theo user trong DB/cross-device |
| Checkout transaction/rollback | PASS | SQLite multiline failure không giữ tracking; SQL fault injection trước commit rollback order/stock |
| Double checkout/idempotency | PASS | SQL hai contexts cùng token cùng OrderId; replay context mới/emptycart; HTTP hai POST cùng token và replay sau clear trả cùng receipt |
| Last stock concurrency | PASS | SQL hai token khác tranh stock1: một order thành công, stock0; không âm/không retry |
| Customer cancel/ownership | PASS | Pending/Confirmed chấp nhận, Shipping/Completed từ chối ở SQLite; HTTP foreign detail/cancel404 |
| Double cancel/hoàn kho | PASS | SQL hai contexts cùng order: cả hai kết quả idempotent, stock chỉ8→9; HTTP cancel lặp không tăng thêm |
| Cancel rollback | PASS | SQL fault trước commit giữ Pending/stock0 |
| History/detail/status/price snapshot | PASS | SQLite owner khác null; HTTP history/detail Đã hủy; giá lịch sử giữ nguyên khi đổi giá SP |
| Admin list/search/filter/detail/transitions | PASS service / PARTIAL GUI | Tests list/search/status/detail/ownership/current workflow, admin reader SQL khớp Web Cancelled/total; chưa thao tác UI Admin mới |
| HTTP authentication/CSRF/logout | PASS các case đã chạy | Register/login thật DB test, anonymous orders về login, thiếu anti-forgery400, logout chặn orders |
| Triển khai Web/Admin chính | CHƯA THỰC HIỆN | Web5015 vẫn binary cũ; S29.8B.2 chưa triển khai. Không tự restart/deploy |

## 4. Kiểm thử thực tế

```powershell
dotnet build PhoneStore.sln --no-restore --no-incremental -p:OutputPath=bin/S29.8C/
dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8C/ --verbosity minimal
dotnet build tests/PhoneStore.SqlIntegration/PhoneStore.SqlIntegration.csproj --no-restore --no-incremental -p:OutputPath=bin/S29.8C/
```

Build cuối:0 warning/error. Regression cuối:105 passed/0 failed/0 skipped, tăng22 so với83 baseline. Nhóm mới: CommerceWorkflowTests (service/ownership/idempotency/workflow) và CartControllerTests (controller/session/replay).

Harness opt-in, không thuộc solution test suite, đã chạy các mode:

- --execute-approved-s29.8c: SQL concurrent checkout/cancel/last-stock/rollback, exit0.
- --execute-approved-s29.8c-http: Web test127.0.0.1:5016, HTTP cases trong bảng, exit0 ở lượt xác minh hoàn chỉnh.
- --execute-approved-s29.8c-final: kiểm SQL lại trên binary cuối sau thêm active-customer replay guard, với fixture sản phẩm/token mới, exit0. Không retry giao dịch cũ.

Lượt HTTP đầu dừng TRƯỚC checkout do assertion đọc TempData sau khi redirect đã consume. SELECT xác nhận customer@s29-8c-http.test có0order, tồn9. Sửa test đọc redirect response, dùng fixture email khác cho lượt hoàn chỉnh; không xóa dữ liệu. Đây là lỗi test, không phải lỗi giỏ hàng. Password test chỉ tạo/người dùng HttpClient giữ trong memory, không log/source/CLI.

Web test được cấu hình connection explicit; trước mỗi POST kiểm DB_NAME/server và session SQL theo PID test. Hai helper Web test đã được dừng riêng sau kết thúc; Web chính8692 giữ nguyên. Không chạy lại harness mù: guard từ chối fixture trùng.

## 5. Database và giới hạn

- Main SELECT trước/sau: PhoneStoreDB ONLINE, Products40/stock740/Images40/Brands8/Categories1/Users1/Orders0; admin@admin.com Role1 active; history InitialCreate+EnforceSingleAdmin không đổi. GET main /products200/40cards. Không có lệnh ghi main trong task.
- Test cuối giữ nguyên database: Products3 (B.1stock9, hai fixture last-stock0), Users4 (1Admin+3Customers), Orders7/Details7 (Cancelled3/Pending4), hai migrations cũ. Không DELETE/drop/truncate/restore/migration.
- Không thêm test DB mới, không sửa40 SP/ảnh/Admin main. Giữ toàn bộ fixture test, gồm Customer của lượt HTTP dừng trước checkout.
- Không coi các cases này là exhaustive stress test hoặc khả năng chạy đa vùng/mất kết nối mọi thời điểm. Snapshot tồn kho B không phát hiện mọi ABA/version của các trường khác.
- Khóa bền dùng OrderCode hash104-bit và unique index hiện có, không giữ nguyên toàn token trong DB; nếu cần thay hợp đồng này bằng bảng/token đầy đủ phải trình schema riêng.
- Cart đang thuộc session trình duyệt, không phải giỏ lưu riêng từng tài khoản trên nhiều thiết bị. Không mở rộng chức năng này trong S29.8C.

## 6. Files

- Business: Purchasing/IOrderService.cs, Purchasing/OrderService.cs; Management/IOrderManagementService.cs, Management/OrderManagementService.cs.
- Data: Purchasing/PurchaseStore.cs, Management/OrderManagementStore.cs.
- Models: Purchasing/PurchaseModels.cs (OrderReceipt.OrderId).
- Web: Program.cs; Controllers/CheckoutController.cs, OrdersController.cs; Models/PurchaseViewModels.cs; Views/Orders/Detail.cshtml.
- Tests: PhoneStore.Tests.csproj (Web reference + ASP.NET shared framework), CartControllerTests.cs, CommerceWorkflowTests.cs.
- SQL harness: Program.cs, CommerceChecks.cs, HttpCommerceChecks.cs, README.md; csproj có từ S29.8B.1.
- Docs: báo cáo này và PROGRESS.md. Không sửa appsettings/migrations/UI hình thức, không commit/push.

Bước tiếp theo: duyệt triển khai binary Web/Admin mới riêng và kiểm UI Admin orders trên môi trường test; tiếp tục các chức năng thiếu còn lại theo audit. Không thử lưu/hủy/tạo đơn trên main khi chưa có danh sách thao tác và phê duyệt riêng.
