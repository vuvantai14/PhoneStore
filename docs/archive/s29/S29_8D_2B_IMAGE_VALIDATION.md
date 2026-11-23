# S29.8D.2B — Nội dung ảnh và an toàn file

Ngày 2026-11-02, Asia/Bangkok. **PASS source/build/regression và các ca file/SQL cách ly đã chạy. Chưa deploy, chưa nghiệm thu thao tác WinForms thật (D.2C).**

## 1. Audit và chính sách định dạng

Đã đọc AGENTS, PROGRESS và báo cáo D.2A. Luồng hiện tại là Admin OpenFileDialog → ProductManagementService → LocalProductImageStorage → ProductManagementStore/SQL. Customer Web dùng static files và ProductImageUrl, không có endpoint HTTP upload/multipart hoặc tham số MIME từ client trong luồng này.

| Nội dung | Chính sách mới trong source |
|---|---|
| JPEG | Nhận .jpg/.jpeg, signature/EOI đầy đủ, decoder xác nhận JPEG và giải mã mọi pixel |
| PNG | Nhận .png tĩnh, kiểm bounds/CRC các chunk/IEND rồi decoder xác nhận PNG, giải mã mọi pixel |
| PNG động/APNG | Từ chối acTL/fcTL/fdAT; không để decoder chỉ đọc frame đầu nhưng browser chạy animation lớn |
| WebP mới | **Từ chối rõ ràng**, đề nghị chuyển JPEG/PNG; bỏ khỏi bộ lọc chọn file |
| WebP cũ | Không xóa/chuyển đổi hoặc sửa liên kết; regex cleanup legacy và Web không bị đổi để chặn ảnh cũ |
| GIF/BMP/TIFF/SVG/extension khác | Không nhận upload |
| Dung lượng | >0 và≤10MiB, kiểm cả kích thước ban đầu lẫn số byte đọc |
| Kích thước | Mỗi chiều≤4096, tổng≤16.000.000pixel, một frame decoder, bpp≤64 |

Decoder dùng **Windows WIC qua BitmapDecoder của Windows Desktop**, được cài sẵn trên máy cùng Microsoft.WindowsDesktop.App8.0.33. Thêm framework reference WPF vào Admin/harness để dùng decoder; **không thêm NuGet/native codec bên ngoài, không cài môi trường mới, không chuyển WinForms sang UI WPF**.

IProductImageContentValidator bắt buộc truyền vào storage; composition root Admin cấp WindowsProductImageContentValidator. Không có fallback nhận file chỉ vì chữ ký đúng. Business không phụ thuộc trực tiếp PresentationCore. Test .NET thuần dùng validator stub cho kiểm thử orchestration; decoder thật được kiểm riêng bằng harness Windows.

Decoder dùng DelayCreation/IgnoreColorProfile và kiểm dimension/pixel trước CopyPixels theo dải32hàng, không chỉ Identify metadata. Các option và cache behavior đối chiếu [Microsoft BitmapCreateOptions](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapcreateoptions?view=windowsdesktop-8.0), [BitmapCacheOption](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapcacheoption?view=windowsdesktop-8.0). PNG bounds/CRC và animation chunks tham chiếu [đặc tả W3C PNG](https://www.w3.org/TR/png-3/). Kiểm container không thay thế pixel decoder.

Web render: unit kiểm static MIME image/jpeg, image/png; browser Edge giải mã fixture JPEG/PNG4×4 trong tab data-URI riêng. WebP fixture1×1 cũng render được trong browser, nhưng upload vẫn bị từ chối theo chính sách hạ tầng decoder đã chọn. Không thêm ảnh hoặc liên kết mới vào main chỉ để thử PNG/WebP qua HTTP.

## 2. Các lỗi/thiếu sót đã sửa

1. **Chữ ký đúng nhưng nội dung hỏng:** nay validate snapshot byte bất biến bằng decoder thật trước khi tạo/publish file. Định dạng decoder phải khớp extension, PNG CRC/bounds/animation gate, JPEG EOI và PNG IEND chặn container cắt cụt hoặc dữ liệu thừa sau terminator theo chính sách strict.
2. **Thiếu giới hạn giải nén:** thêm dimension/pixel/frame/bpp và decode theo buffer nhỏ. Byte snapshot≤10MiB; không dùng stream đầu vào có thể thay đổi giữa validate và copy.
3. **Cleanup nhầm file không do lần ghi này tạo:** chỉ dọn .tmp khi CreateNew đã thành công, không xóa file có sẵn nếu tạo file thất bại. GUID tên đích/temp, File.Move overwrite=false, kiểm reparse-point/path containment giữ nguyên.
4. **TOCTOU giữa SQL reference check và xóa file:** service không tự xóa file đã publish sau unlink hoặc kết quả DB thất bại/không rõ. Trả notice giữ file, kèm ImageUrl khi cần đối chiếu; ảnh đang shared reference được giữ. File tạm lỗi được dọn ngay nếu có thể; lỗi dọn .tmp nêu rõ cần kiểm tra.
5. **Deadlock giữa thao tác ảnh ở hai Product khác nhau:** phát hiện và sửa theo mục3. Không bật retry transaction.

Thay ảnh vẫn là thêm mới→commit→chọn primary→bỏ liên kết cũ, không ghi đè/xóa ảnh cũ trước commit. Quy tắc ngừng hoạt động của Product/Brand/Category không đổi; không hard-delete dữ liệu nghiệp vụ.

## 3. Phát hiện concurrency và cách xử lý

Lần SQL đầu trước bản sửa khóa bị provider báo transient failure khi hai transaction đặt primary. Harness đã dừng, không tự replay. SELECT xác định một nhánh đã commit, nhánh còn lại chưa hoàn tất; file cũ vẫn tồn tại.

Đọc **system_health event-file** xác nhận deadlock thật trên `PhoneStoreDB_StockConcurrencyTest.dbo.ProductImages`, PK_ProductImages: hai transaction Serializable giữ **RangeS-S**, cùng đòi chuyển **X** khi UPDATE IsPrimary. Bản đọc ring-buffer ban đầu không cung cấp graph phù hợp; không suy root cause cho tới khi có event-file.

Sửa `ProductManagementStore.LockProductAsync`: trong transaction SQL Server, lấy **sp_getapplock Exclusive/Transaction** trên resource `PhoneStore.ProductImages.Write`, timeout10giây, **trước mọi Product/image lock**, rồi giữ UPDLOCK/HOLDLOCK parent như cũ. Khóa dùng chung giữa tiến trình theo database; tránh hai image-writer giữ overlapping read ranges rồi chuyển ghi. Timeout trả lỗi; không retry ngầm.

Đánh đổi: image transactions trên các Product khác nhau cũng tuần tự hóa. Phù hợp quy mô một Admin hiện tại; không tuần tự hóa việc đọc ảnh HTTP, không thêm schema, không thay khóa checkout. Không hứa tránh mọi deadlock với client SQL tùy ý bypass service.

Sau khi kiểm tra outcome và ownership prefix/IDs/no OrderDetails reference, đã dọn **chỉ fixture lỗi mới** (Products7/8, lookup3 và image rows/file thuộc D2B-prefix đó). Không reset fixture cũ. Chạy lại **fixture mới sau sửa source**, không retry transaction lỗi. Sau bổ sung PNG CRC/APNG gate, kiểm tích hợp thêm một fixture mới trên bản cuối; đều cleanup và hậu kiểm snapshot.

## 4. Bảng kiểm thử

| Ca | Kết quả / bằng chứng |
|---|---|
| JPEG/PNG thật hợp lệ | PASS WIC full decode + bytes lưu khớp source fixture |
| Header JPEG/PNG hợp lệ nhưng không có nội dung ảnh | PASS từ chối, không publish file |
| Header và terminator đúng nhưng cấu trúc giữa hỏng | PASS fixture bị từ chối |
| JPEG/PNG cắt nửa file | PASS từ chối |
| PNG CRC sai / có animation control | PASS unit, không tới decoder hoặc output |
| Extension .jpg nhưng PNG, .png nhưng JPEG | PASS từ chối |
| Giả MIME image/jpeg trong text | PASS không coi text là ảnh; luồng local-file không tin/nhận tham số MIME do caller cung cấp |
| WebP hợp lệ | PASS từ chối theo chính sách; fixture được browser decode1×1 độc lập |
| >10MiB, width4097, height4097 | PASS từ chối |
| 4001×4000 vượt16triệu pixel | PASS từ chối trước CopyPixels |
| Empty/missing/canceled | PASS regression storage đã có |
| Lỗi ghi: đường dẫn uploads bị file fixture chiếm | PASS trả lỗi, không overwrite file đó |
| Decoder ném lỗi | PASS unit, chưa tạo output; không signature-only fallback |
| GUID/path traversal/slug demo | PASS regression/file; không xóa unmanaged URL |
| Hai lần thay ảnh đồng thời dùng chung ảnh cũ | PASS SQL bản sửa: mỗi Product có1primary mới, file cũ giữ nguyên cả trong và sau thao tác |
| Hai first-upload đồng thời cùng Product | PASS SQL bản sửa: hai association, chỉ1primary |
| Lỗi DB trước commit sau đã lưu file | PASS DB insert rollback; file mới hợp lệ được giữ+notice, file cũ nguyên |
| Shared/orphan không bị tự xóa | PASS SQL và source: service không gọi TryDelete sau unlink/DB attempt |
| Cleanup có kiểm soát trong test | PASS chỉ prefix/ID mới; xác nhận không có reference trước xóa file fixture |
| WinForms OpenFileDialog/thông báo lỗi mới | NOT TESTED thao tác UI thật; source/filter/notice đã sửa, chờ D.2C |

**155/155 xUnit PASS**,0fail/skip:147baseline+8ImageStorageSafety cases mới. Có18ca file/decoder Windows riêng (thêm một dòng summary PASS trong output). SQL cuối **27assertion PASS** (28dòng PASS tính summary), không cộng vào155. Các fixtures WIC/browser/SQL không sử dụng ảnh sản phẩm main.

Đã thay một PNG base64 fixture unit D2A có checksum không hợp lệ bằng fixture4×4 thật do bộ test tạo. Đây là test fixture, không sửa ảnh demo. Build harness ban đầu có conflict WindowsBase; thêm cùng framework reference WPF vào harness, build cuối0warnings/errors. Lượt thử decode data-URI trên tab Customer chờ quá hạn đã dừng helper; kiểm lại trên tab offline riêng PASS và đóng đúng tab test, không submit/navigate form Customer.

## 5. Commands và artifact

```powershell
dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.8D.2B/
dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.8D.2B/ --verbosity minimal
dotnet build tests/PhoneStore.SqlIntegration/PhoneStore.SqlIntegration.csproj --no-restore -p:OutputPath=bin/S29.8D.2B/
tests/PhoneStore.SqlIntegration/bin/S29.8D.2B/PhoneStore.SqlIntegration.exe --check-images-s29.8d.2b
tests/PhoneStore.SqlIntegration/bin/S29.8D.2B/PhoneStore.SqlIntegration.exe --execute-approved-s29.8d.2b
git diff --check
```

Solution và harness **0warnings/errors** bản cuối; toàn bộ155tests PASS sau thay đổi cuối. Không build vào binary đang chạy.

DB test dùng lại localhost/PhoneStoreDB_StockConcurrencyTest. Trước ghi kiểm không client khác; TargetGuard kiểm connection/DB_NAME/server trước từng EF command, lease ứng dụng riêng cho lượt test. Mỗi fixture chỉ2Products/1Brand/1Category/images; không tạo/sửa test User/Order. File nằm riêng dưới harness bin/image-fixtures/D2B-<GUID>. Cleanup sau đối chiếu chỉ dữ liệu/file thuộc fixture, không drop/restore/reseed. Identity test có thể tăng.

Artifact:
`tests/PhoneStore.SqlIntegration/obj/S29.8D.2B-20261102_033917998Z`

Gồm snapshot before/final-comparison main+test, logs và attempt markers các lượt SQL lỗi/fixed/final, browser-image-decode.json. Runtime helper browser offline và fixture bytes trong harness obj/bin; không có cookie/token/password/hash credentials. Giữ markers, không chạy lại mode cũ để refresh kết quả.

## 6. File thay đổi trong D.2B

| File | Nội dung |
|---|---|
| src/PhoneStore.Business/Management/IProductImageContentValidator.cs (mới) | Hợp đồng decoder bắt buộc |
| src/PhoneStore.Admin/Management/WindowsProductImageContentValidator.cs (mới) | Decoder WIC, giới hạn và full pixel read |
| src/PhoneStore.Business/Management/LocalProductImageStorage.cs | Snapshot/limits/container check/decoder/ownership temp |
| src/PhoneStore.Business/Management/ProductManagementService.cs | Notice, giữ file sau unlink/DB failure |
| src/PhoneStore.Data/Management/ProductManagementStore.cs | Transaction application lock cho image writers |
| src/PhoneStore.Admin/AdminServices.cs | Inject decoder thật |
| src/PhoneStore.Admin/Dialogs/ProductImagesDialog.cs | Bộ lọc JPEG/PNG, không WebP mới |
| src/PhoneStore.Admin/PhoneStore.Admin.csproj | Reference framework WPF đã có |
| tests/PhoneStore.Tests/ImageStorageSafetyTests.cs (mới) | 8ca orchestration/CRC/APNG/MIME/policy |
| tests/PhoneStore.Tests/AdminCrudTests.cs | Constructor decoder stub có tên rõ, sửa fixture PNG checksum |
| tests/PhoneStore.SqlIntegration/ImageValidationChecks.cs (mới) | Ca decoder/file/SQL concurrency/cleanup |
| tests/PhoneStore.SqlIntegration/AdminCrudChecks.cs | Decoder thật; cập nhật kỳ vọng giữ file, dọn fixture riêng |
| tests/PhoneStore.SqlIntegration/Program.cs / csproj / README.md | Opt-in modes, framework reference và hướng dẫn |
| docs/S29_8D_2B_IMAGE_VALIDATION.md / docs/PROGRESS.md | Bằng chứng, giới hạn, bước tiếp tục |

LookupManagementService thay đổi từ D2A được giữ nguyên, không sửa tiếp trong D2B. Không thay Web source hoặc schema/migration.

## 7. Hậu kiểm và rủi ro còn lại

SELECT cuối **03:47:04 UTC / 10:47:04 Bangkok**: PhoneStoreDB ONLINE;40Products/40Images/tồn740/Customer1/Adminactive1/Orders1/Details1; OrderId1 Cancelled. Customer vẫn tài vũ/09032657212/AddressNULL, role/active/email và Admin nguyên.

Main **9/9 snapshot khớp trước chạy**; DB test **9/9 snapshot khớp fixture cũ sau cleanup**. Users snapshot không chứa credentials. Main Web60/60 và101preserved hashes (Admin/config/uploads, gồm40ảnh demo) giữ nguyên; Web19388/Admin10736 responsive. Không main DML/upload/migration/seed/restore/deploy/commit/push.

Rủi ro/giới hạn:

- Cố ý giữ file đã publish sau unlink/commit không rõ: có thể phát sinh orphan hợp lệ/dung lượng tăng. Chưa có job tự dọn; cần quy trình đối chiếu references và quyền phê duyệt trước xóa. File lỗi/.tmp thuộc lần ghi được dọn nếu quyền FS cho phép; khi không dọn được có thông báo.
- Dimension/byte/frame budget giảm nguy cơ decompression bomb, không phải hard memory/time sandbox cho native WIC hoặc bảo đảm không có lỗ hổng codec. Cần Windows/runtime được cập nhật và ACL upload không cho người dùng không tin cậy thay directory/reparse trong lúc thao tác.
- JPEG EOI/PNG chunk CRC/end strict có thể từ chối ảnh mà decoder/browser chịu sửa lỗi hoặc có trailing data; chủ động yêu cầu re-export JPEG/PNG sạch. Không chỉnh sửa file của người dùng, không strip metadata hoặc nhận diện nội dung/model sản phẩm.
- Chưa stress nhiều client dài hạn/kill giữa commit; không suy rollback pre-commit test thành đã kiểm mọi commit-uncertain fault. Bảo vệ giữ file vẫn áp dụng khi không xác định được outcome.
- WebP mới từ chối cho tới khi có hạ tầng decoder/chính sách được kiểm thử; không tự thêm codec lớn. WIC path chỉ phục vụ Admin Windows, không hứa tương thích Linux.

**Dừng chờ phê duyệt S29.8D.2C — nghiệm thu CRUD WinForms thật**, dùng binary/test DB/thư mục ảnh cách ly. Source D2B chưa được triển khai vào Admin chính.
