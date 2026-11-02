# S29.8D.2A — Audit và kiểm thử CRUD Admin

Ngày 2026-11-02, Asia/Bangkok. **PARTIAL toàn phạm vi nghiệm thu**: source/service/SQL của các ca đã chạy đạt, chưa nghiệm thu đầy đủ thao tác CRUD qua WinForms thật; ảnh còn giới hạn kiểm tra nội dung.

## 1. Phạm vi và kiến trúc

Đã đọc AGENTS, PROGRESS và S29_8D_FUNCTIONAL_AUDIT; đối chiếu README/CONTEXT với source liên quan. D.1 Customer Profile đã PASS riêng, không sửa lại trong nhiệm vụ này.

| Luồng | UI | Business / Data |
|---|---|---|
| Sản phẩm | ProductsManagementControl, ProductEditDialog | ProductManagementService, ProductManagementStore, ProductEdit |
| Thương hiệu/danh mục | LookupManagementControl, LookupEditDialog | LookupManagementService/Store, LookupKind/LookupEdit |
| Ảnh | ProductImagesDialog, OpenFileDialog | ProductManagementService, ProductManagementStore, LocalProductImageStorage |
| Quyền | AdminAuthService, AdminSession, MainForm | CatalogManagementGateway.RequireAdmin trước resolve dịch vụ |

README yêu cầu quản lý sản phẩm/ảnh/brand/category, không có quy định đã xác minh rằng phải xóa cứng dữ liệu lịch sử. Không có rubric Word/Excel được xác minh để suy thêm yêu cầu. Hiện UI dùng **ngừng hoạt động/kích hoạt lại**, không gọi DELETE sản phẩm/brand/category. Thay ảnh bằng thêm ảnh mới → chọn primary → bỏ ảnh cũ, không ghi đè nội dung file cũ.

## 2. Quy tắc nghiệp vụ và ràng buộc

- Product.Price >0, decimal18,2; stock≥0; tên bắt buộc≤200; FK brand/category phải tồn tại; giới hạn các trường thông số có trong service/model.
- Brand/Category tên bắt buộc≤100, mô tả≤500; service trim/chặn trùng không phân biệt hoa thường, DB unique index bảo vệ thêm. ProductName không unique: các ID khác nhau có thể cùng tên hiển thị. Đây là hành vi xác minh được, chưa có yêu cầu SKU/unique-name để tự đổi schema.
- Thêm/chuyển association sang brand/category inactive bị chặn; sửa sản phẩm đã gắn association inactive hiện hữu được phép giữ association đó.
- Ngừng brand/category **không tự ngừng sản phẩm liên quan**; catalog hiện lọc Product.IsActive và danh sách lựa chọn active riêng. Cần chốt nghiệp vụ nếu muốn cascade ẩn sản phẩm, không tự áp đặt trong audit này.
- Ngừng Product giữ Orders/OrderDetails/Images; checkout kiểm active. Save dùng transaction Serializable và atomic UPDATE WHERE ProductId AND StockQuantity=OriginalStockQuantity; thiếu snapshot hoặc affected0 từ chối toàn edit.
- FK Products→Brands/Categories và OrderDetails→Products là NO_ACTION; ProductImages→Products, OrderDetails→Orders CASCADE. SELECT test metadata xác nhận các FK enabled/trusted. Không dùng cascade để dọn dữ liệu có sẵn.
- Image add/remove/primary/order khóa parent Product UPDLOCK,HOLDLOCK trong transaction. Chuẩn hóa đúng một primary nếu còn ảnh, DisplayOrder không âm. DB chưa có unique filtered primary index; quy tắc được bảo vệ ở service, không chống được SQL tùy ý bypass service.
- Storage giới hạn extension JPG/JPEG/PNG/WebP và≤10MiB, so chữ ký, copy GUID + CreateNew/temp, không overwrite; cleanup chỉ URL managed GUID trong thư mục an toàn, không xóa slug/demo hoặc path traversal. Sau commit không xóa file còn reference; lỗi cleanup có notice.
- Không có endpoint public mới. Gateway chặn session không Admin trước gọi service; service/store là tầng nội bộ tin cậy, không nhận caller identity mỗi thao tác. Desktop role snapshot và Windows SQL permission vẫn là giới hạn đã biết, không tuyên bố đã pentest.

## 3. Môi trường test và an toàn

- Dùng **localhost/PhoneStoreDB_StockConcurrencyTest** đã tồn tại ONLINE, đúng server DESKTOP-ES9362P; không tạo database hoặc apply migration.
- Trước chạy: không có user session/lock trên DB test và không có process harness/test khác; chỉ Web19388/Admin10736 chính đang hoạt động. Harness kiểm lại session trước ghi, giữ cooperative sp_getapplock riêng cho mode này.
- TargetGuard kiểm connection localhost/Windows Authentication/DB_NAME/server trước từng EF command, kể cả cleanup; không fallback connection main.
- Fixture riêng `D2A-<12hex>`:1Brand/1Category/**3Products** (có1ca tên trùng)/1CancelledOrder/1Detail; dùng Customer **test có sẵn**, không tạo hoặc sửa User/Admin. Giá/tồn không sao chép từ main.
- Test images tạo riêng2×2 JPEG/PNG, không dùng/chỉnh ảnh demo. Output riêng dưới harness bin/S29.8D.2A/crud-fixtures/<prefix>.
- Cleanup thành công có transaction và điều kiện prefix+ID: chỉ xóa Detail/Order/Images/Products/Brand/Category do lần chạy này tạo; test FK DELETE thất bại không làm mất dữ liệu. Chỉ xóa file mới trong thư mục fixture đã xác định; không recursive delete hoặc dọn fixture cũ.
- Đã cleanup, **test DB trước/sau9/9 snapshot nội dung khớp**, Users projection không credentials. Identity counters của DB test có thể tăng; không reseed identity. Đây là cleanup rows, không reset/drop/restore database.
- Cooperative lock không chặn client tùy ý không tuân thủ lock; kiểm tra session trước chạy + snapshot sau không thay thế cơ chế độc quyền lâu dài. Không để app test chạy song song trong quá trình nghiệm thu.

## 4. Bảng ca kiểm thử

PASS dưới đây là test tự động/service/SQL cách ly, không đồng nghĩa đã bấm mọi nút GUI. NOT TESTED nghĩa chưa có bằng chứng, không mặc định lỗi.

| Nhóm / thao tác | Kết quả | Bằng chứng |
|---|---|---|
| Product thêm hợp lệ | PASS SQL | Tạo3Products với brand/category riêng |
| Product đọc, search, lọc brand/category/active | PASS SQL | Kết quả đúng3fixture; kết hợp cả filter |
| Product sửa tên/giá/thông tin | PASS SQLite/SQL | GetEdit/Save cập nhật ID cũ, không tạo bản sao khi edit |
| Tên Product trùng | PASS hành vi hiện có | SQL cho phép2ID cùng tên; không unique-name schema, cần quyết định nếu muốn SKU |
| Product tên rỗng, giá0/âm, stock âm | PASS SQLite/SQL | Không phát sinh bản ghi invalid |
| Precision>2, RAM quá dài, brand không tồn tại | PASS SQLite | Service từ chối; chưa test mọi trường thông số qua GUI |
| Product ngừng/kích hoạt với OrderDetails reference | PASS SQL | History được giữ, có thể bật lại |
| Product xóa cứng reference | PASS SQL FK | DELETE test bị547; UI/service không có hard-delete |
| Product hard-delete không reference qua UI | MISSING theo thiết kế hiện tại | Chỉ soft deactivate; không kết luận thiếu bắt buộc nếu rubric không đòi hard-delete |
| Lookup thêm/sửa/search cho cả Brand/Category | PASS SQLite/SQL | Create, rename, search riêng từng loại |
| Lookup trùng tên/case | PASS SQLite/SQL | Service chặn trước ghi; không tuyên bố đã stress race insert unique |
| Lookup bắt buộc/length/missing ID | PASS SQLite/SQL một phần | Required SQL+SQLite; length101/description501/missing SQLite |
| Lookup ngừng/kích hoạt đang có Products | PASS SQL | Products/FK giữ nguyên |
| Product thêm vào lookup inactive | PASS SQL/SQLite | Bị từ chối; giữ association inactive cũ khi edit được phép |
| Lookup xóa cứng đang referenced | PASS SQL FK | Brand/Category DELETE bị547 |
| Lookup hard-delete qua UI | MISSING theo thiết kế hiện tại | Soft deactivate có confirmation giữ lịch sử |
| Ảnh JPEG/PNG hợp lệ | PASS SQL/file | Tạo fixture thật, copy/link GUID, kiểm file tồn tại |
| Định dạng/extension sai, empty, signature sai | PASS SQLite test/file + SQL | .exe/.jpg/.png/.webp sai header, không tạo uploads; SQL saiJPEG không thêm association |
| Ảnh quá10MiB, missing, canceled | PASS test/file | Từ chối trước tạo file output |
| Ảnh WebP hợp lệ end-to-end | NOT TESTED | Có allow-list/signature VP8/VP8L/VP8X, chưa dùng fixture WebP decode thật |
| Header hợp lệ nhưng payload hỏng | PARTIAL | Source chỉ so magic bytes; chưa decode pixel, không đủ để tuyên bố mọi file hỏng bị chặn |
| Hai lần upload không ghi đè source | PASS test/file | Hai GUID khác nhau, bytes khớp và source giữ nguyên |
| Primary/thay ảnh/reorder | PASS SQL | Thêm2ảnh, đổi primary, order0, bỏ ảnh cũ, còn1primary |
| DisplayOrder âm / ảnh thuộc Product khác | PASS SQL | Từ chối, không bỏ nhầm association |
| Remove last image + managed file | PASS SQL/file | Không association hoặc file upload mới còn lại |
| Path traversal/external URL/slug demo | PASS test | TryDelete trảfalse; không xóa unmanaged path |
| File còn shared reference / cleanup notice | PARTIAL | Có code bảo vệ, chưa có ca SQL shared-file/ACL denial trong lần này |
| Hai first-upload thật đồng thời | NOT TESTED mới | Parent lock có source; chưa stress concurrent primary ở D2A |
| Stale stock10→9 rồi save10 | PASS SQL + regression cũ | Save bị từ chối, kho9, không overwrite |
| Hai form snapshot cùng tồn | PASS SQL | Save đầu đổi11, save sau12 bị từ chối |
| Pre-commit lỗi khi save Product | PASS SQL fault | Thông báo lỗi; kho99 không persist, kho11 giữ |
| Pre-commit lỗi khi add ảnh | PASS SQL/file fault | DB rollback và copied file được dọn, không orphan mới |
| Lookup database exception | PASS regression mới | SQLite DB unavailable trả ManagementResult.Fail thay exception thoát |
| Gateway chưa đăng nhập / Customer role | PASS harness | Gateway từ chối trước resolve; AdminSession không cho Establish Customer |
| Full UI login/logout/CRUD permissions | PARTIAL/NOT TESTED mới | Source guard có; chưa mở full shell test cho CRUD trong D2A |
| Cleanup fixture và bảo toàn dữ liệu cũ | PASS | Test9/9snapshot; main9/9snapshot không đổi |

43assertion PASS trong harness SQL, là bằng chứng riêng ngoài147xUnit. Không đếm hard-delete cleanup là chức năng xóa cứng UI đã có. Test ngừng lookup không xác nhận Product bị ẩn; hành vi hiện tại nêu ở mục2.

## 5. Lỗi đã sửa và giới hạn còn mở

**Đã sửa nhỏ:** LookupManagementService trước đây chỉ catch DbUpdateException/SqlException; lỗi read/query provider khác như SqliteException thoát khỏi service. Hai nhánh Save/SetActive nay catch **DbException** chung (vẫn giữ xử lý riêng SQL2601/2627), trả thông báo nghiệp vụ rõ và ClearTracking. Regression DB bị đóng trên SQLite cho cả Brand/Category PASS. Không schema/logic CRUD/UI redesign.

**Còn mở P1/P2 về ảnh:** file có magic bytes đúng nhưng payload hỏng chưa được đảm bảo từ chối. Nên có decoder JPEG/PNG/WebP thực, giới hạn dimension/pixel/frame và tests malformed/truncated/decompression budget; không viết parser ảnh tự chế. Chưa thêm dependency codec/native runtime vào bộ deploy chỉ để khẳng định validation đã hoàn tất. Cần kiểm tính tương thích .NET8/Windows, policy license và bảo trì trước lựa chọn thư viện. Tài liệu decoder tham khảo: [ImageSharp loading/identifying](https://docs.sixlabors.com/articles/imagesharp/loadingandsaving.html). Chưa thay validation ảnh hiện có trong D2A; đánh PARTIAL, không PASS toàn bộ image management.

**Cần chốt nghiệp vụ:** unique ProductName/SKU; deactivate Brand/Category có cascade ẩn Product hay chỉ ngăn lựa chọn mới; có thật sự cần hard-delete ngoài soft deactivate. Không tự thêm unique index, cascade delete hoặc migration.

**Chưa nghiệm thu UI thật lần này:** Create/Edit Product dialog, lỗi validation từng control, xác nhận ngừng/bật lại, Brand/Category dialogs, OpenFileDialog/upload/primary/order/remove/notice, đóng form khi đang lưu, session logout/relogin. B.1 dialog stale conflict đã PASS lịch sử; không suy thành mọi dialog CRUD đã PASS.

## 6. Build, tests và file thay đổi

```powershell
dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8D.2A/
dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8D.2A/ --verbosity minimal
dotnet build tests/PhoneStore.SqlIntegration/PhoneStore.SqlIntegration.csproj --no-restore -p:OutputPath=bin/S29.8D.2A/
tests/PhoneStore.SqlIntegration/bin/S29.8D.2A/PhoneStore.SqlIntegration.exe --execute-approved-s29.8d.2a
git diff --check
```

- Build cuối solution **0warnings/errors**; **147/147 tests PASS**,0fail/skip, gồm129baseline+18test mới. Lần build đầu có warning async không await trong test, đã sửa test thành sync; build cuối sạch.
- Harness build0warnings/errors; chạy đúng1lần mode mới, exit0/43assertionPASS; không rerun các mode cũ, không migration hay HTTP POST main.
- TestDatabase SQLite riêng; ảnh unit-test dưới thư mục temp GUID, xóa đúng fixture files. SQL/image test nằm riêng không dùng uploads main.
- Files sửa: `src/PhoneStore.Business/Management/LookupManagementService.cs`, `tests/PhoneStore.SqlIntegration/Program.cs`, `tests/PhoneStore.SqlIntegration/README.md`, `docs/PROGRESS.md`.
- Files mới: `tests/PhoneStore.Tests/AdminCrudTests.cs`, `tests/PhoneStore.SqlIntegration/AdminCrudChecks.cs`, báo cáo này.
- Không thay DTO/schema/migration/Admin UI hoặc binary chạy; `bin/S29.8D.2A` chỉ build artifact.

Evidence: `tests/PhoneStore.SqlIntegration/obj/S29.8D.2A-20261102_033101218Z` chứa marker sql-run.attempt, sql-result.log và snapshot before/after main+test. Không lưu hash/password credential; Users chỉ projection không credentials. Không xóa marker để tự retry.

## 7. Hậu kiểm main và test

SELECT **03:32:01 UTC / 10:32:01 Bangkok**: PhoneStoreDB ONLINE;40Products/40Images/tồn740/Users2/Orders1/Details1. Customer vẫn tài vũ/09032657212/AddressNULL/Role2active; Admin admin@admin.com/Role1active; OrderId1 Cancelled.

**Main9/9snapshot trước/sau khớp**; test9/9snapshot sau cleanup khớp fixture cũ (Products3/Orders10/Images0/Users5, cùng nội dung). Main60Web+101preserved hashes khớp (Admin output/config/ảnh). Không chỉnh hồ sơ Customer hoặc Admin, sản phẩm/ảnh main, Order/Details main. Không deploy, commit/push.

## 8. Bước tiếp theo

Đề xuất **D.2B xử lý validation nội dung ảnh + các regression còn thiếu**, sau đó **D.2C nghiệm thu WinForms CRUD thật trong tiến trình/ảnh test riêng** theo ma trận trên. Chưa đủ điều kiện công bố toàn CRUD PASS UI hoặc triển khai Admin. Nếu cần thay schema, trình kế hoạch riêng.

Dừng chờ chỉ đạo; không triển khai Admin hoặc ghi PhoneStoreDB chính từ kết quả audit này.
