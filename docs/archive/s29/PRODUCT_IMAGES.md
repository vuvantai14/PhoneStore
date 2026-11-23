# Ảnh cho 40 sản phẩm PhoneStore

Đối chiếu trực tiếp `tools/PhoneStore.CatalogImport/Program.cs`, ngày 2026-11-02.
Danh sách này là checklist chuẩn bị ảnh; chưa nhập sản phẩm hoặc liên kết database.

## Thư mục chính xác

```text
C:\PhoneStore\PhoneStore\src\PhoneStore.Web\wwwroot\uploads\products
```

Web phục vụ file từ `wwwroot` qua `UseStaticFiles`, URL tương ứng `/uploads/products/<tên-file>`.
Admin hiện dùng `ProductImages:UploadsDirectory = ../../../../PhoneStore.Web/wwwroot/uploads/products`,
resolve từ `src/PhoneStore.Admin/bin/Debug/net8.0-windows/` về cùng thư mục trên.
Cấu hình source và bản build giống nhau; chưa thấy biến môi trường `ProductImages__UploadsDirectory` ghi đè trong phiên kiểm tra.
Không dùng thư mục `bin/.../wwwroot` hoặc `database/` làm nơi chứa ảnh.

## Danh sách cần chuẩn bị

Tên gốc lấy chính xác từ `ImageBase` trong CatalogImport; đuôi `.jpg` là đề xuất thống nhất.
Có thể dùng `.jpeg`, `.png` hoặc `.webp` với **cùng tên gốc**, nhưng chỉ một file được hỗ trợ cho mỗi tên gốc.
Ví dụ chọn `iphone-12.png` thì không đặt thêm `iphone-12.jpg`: importer sẽ báo trùng.
Không đổi đuôi PNG/WebP thành JPG bằng cách rename; giữ đúng định dạng thực hoặc tự chuyển đổi bằng phần mềm ảnh.

| STT | Tên sản phẩm | Thương hiệu | Tên file ảnh đề xuất | Thư mục (từ root dự án) | Trạng thái ảnh |
|---|---|---|---|---|---|
| 1 | iPhone 12 | Apple | `iphone-12.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 70070 byte; nội dung PARTIAL |
| 2 | iPhone 13 | Apple | `iphone-13.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 60725 byte; nội dung PARTIAL |
| 3 | iPhone 14 | Apple | `iphone-14.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 63754 byte; nội dung PARTIAL |
| 4 | iPhone 14 Pro Max | Apple | `iphone-14-pro-max.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 65702 byte; nội dung PARTIAL |
| 5 | iPhone 15 | Apple | `iphone-15.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 62233 byte; nội dung PARTIAL |
| 6 | iPhone 15 Pro Max | Apple | `iphone-15-pro-max.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 61578 byte; nội dung PARTIAL |
| 7 | Samsung Galaxy A15 | Samsung | `samsung-galaxy-a15.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 64464 byte; nội dung PARTIAL |
| 8 | Samsung Galaxy A25 | Samsung | `samsung-galaxy-a25.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 64212 byte; nội dung PARTIAL |
| 9 | Samsung Galaxy A35 | Samsung | `samsung-galaxy-a35.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 60612 byte; nội dung PARTIAL |
| 10 | Samsung Galaxy A55 | Samsung | `samsung-galaxy-a55.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 67800 byte; nội dung PARTIAL |
| 11 | Samsung Galaxy S23 | Samsung | `samsung-galaxy-s23.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 56216 byte; nội dung PARTIAL |
| 12 | Samsung Galaxy S24 Ultra | Samsung | `samsung-galaxy-s24-ultra.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 69012 byte; nội dung PARTIAL |
| 13 | Xiaomi Redmi Note 13 | Xiaomi | `xiaomi-redmi-note-13.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 78816 byte; nội dung PARTIAL |
| 14 | Xiaomi Redmi Note 13 Pro | Xiaomi | `xiaomi-redmi-note-13-pro.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 76014 byte; nội dung PARTIAL |
| 15 | Xiaomi Redmi Note 13 Pro+ | Xiaomi | `xiaomi-redmi-note-13-pro-plus.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 64379 byte; nội dung PARTIAL |
| 16 | Xiaomi 13T | Xiaomi | `xiaomi-13t.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 70242 byte; nội dung PARTIAL |
| 17 | Xiaomi 14 | Xiaomi | `xiaomi-14.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 64240 byte; nội dung PARTIAL |
| 18 | POCO X6 Pro | Xiaomi | `poco-x6-pro.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 81248 byte; nội dung PARTIAL |
| 19 | OPPO A58 | OPPO | `oppo-a58.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 82879 byte; nội dung PARTIAL |
| 20 | OPPO A79 | OPPO | `oppo-a79.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 85456 byte; nội dung PARTIAL |
| 21 | OPPO Reno10 | OPPO | `oppo-reno10.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 84908 byte; nội dung PARTIAL |
| 22 | OPPO Reno11 | OPPO | `oppo-reno11.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 82597 byte; nội dung PARTIAL |
| 23 | OPPO Reno11 F | OPPO | `oppo-reno11-f.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 89249 byte; nội dung PARTIAL |
| 24 | OPPO Find N3 | OPPO | `oppo-find-n3.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 76495 byte; nội dung PARTIAL |
| 25 | vivo Y17s | vivo | `vivo-y17s.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 78614 byte; nội dung PARTIAL |
| 26 | vivo Y36 | vivo | `vivo-y36.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 77200 byte; nội dung PARTIAL |
| 27 | vivo V29 | vivo | `vivo-v29.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 75762 byte; nội dung PARTIAL |
| 28 | vivo V30 | vivo | `vivo-v30.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 81263 byte; nội dung PARTIAL |
| 29 | vivo V30 Pro | vivo | `vivo-v30-pro.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 83621 byte; nội dung PARTIAL |
| 30 | realme C55 | realme | `realme-c55.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 88888 byte; nội dung PARTIAL |
| 31 | realme C67 | realme | `realme-c67.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 78863 byte; nội dung PARTIAL |
| 32 | realme 11 Pro | realme | `realme-11-pro.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 96906 byte; nội dung PARTIAL |
| 33 | realme 12 Pro+ | realme | `realme-12-pro-plus.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 86807 byte; nội dung PARTIAL |
| 34 | Google Pixel 7 | Google | `google-pixel-7.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 64064 byte; nội dung PARTIAL |
| 35 | Google Pixel 7 Pro | Google | `google-pixel-7-pro.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 64638 byte; nội dung PARTIAL |
| 36 | Google Pixel 8 | Google | `google-pixel-8.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 64200 byte; nội dung PARTIAL |
| 37 | Google Pixel 8 Pro | Google | `google-pixel-8-pro.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 66984 byte; nội dung PARTIAL |
| 38 | OnePlus 11 | OnePlus | `oneplus-11.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 65776 byte; nội dung PARTIAL |
| 39 | OnePlus 12 | OnePlus | `oneplus-12.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 63197 byte; nội dung PARTIAL |
| 40 | OnePlus Nord 3 | OnePlus | `oneplus-nord-3.jpg` | `src/PhoneStore.Web/wwwroot/uploads/products/` | Đã có; PASS kỹ thuật: JPEG 800×800, 67274 byte; nội dung PARTIAL |
## Tự sao chép bằng File Explorer

1. Mở File Explorer, dán đường dẫn tuyệt đối ở trên vào thanh địa chỉ và nhấn Enter.
2. Bật View → Show → File name extensions để tránh tên như `iphone-12.jpg.jpg`.
3. Chuẩn bị ảnh thật đúng mẫu điện thoại; giữ chất lượng phù hợp, nên tối đa 10 MB/file để tương thích luồng Admin.
4. Đổi tên file theo checklist ở thư mục nguồn của bạn, giữ đúng đuôi định dạng; dùng Copy/Paste vào thư mục đích.
5. Nếu Windows hỏi thay thế file, chọn bỏ qua và báo lại; không ghi đè ảnh đã có.
6. Khi đủ ảnh, xác nhận với Codex để kiểm tra tên, số lượng, trùng và định dạng trước khi xin phép import/liên kết.

Chỉ copy ảnh chưa tạo Product hoặc ProductImages; Web chưa có lý do hiển thị các file này thành sản phẩm.
Ảnh runtime ở thư mục này bị Git ignore: cần giữ bản sao riêng. Không thêm placeholder thay cho ảnh thật.

## Quy trình Admin hiện có (đọc source, chưa thao tác ghi)

Sau khi được phép có sản phẩm: đăng nhập Admin → Sản phẩm → chọn sản phẩm đã lưu → Quản lý ảnh → Thêm ảnh.
`OpenFileDialog` chọn JPG/JPEG/PNG/WebP; `LocalProductImageStorage` kiểm tra dung lượng >0 và ≤10 MB,
kiểm tra signature đúng đuôi, rồi **copy** vào thư mục đích bằng tên GUID mới, không giữ tên checklist.
Service thêm ProductImages với ProductId/ImageUrl/DisplayOrder/IsPrimary trong transaction; không sửa file nguồn.
Vì vậy upload qua Admin tạo bản copy GUID, không thay thế quy trình chuẩn bị file theo slug cho CatalogImport.
Không bấm Thêm ảnh lúc này: hành động đó sẽ copy file và ghi database.

## Web đọc ảnh và bước liên kết sau này

`ProductCatalogReader` đọc ProductImages theo ProductId, ưu tiên IsPrimary rồi DisplayOrder/ImageId;
card lấy một ảnh, trang chi tiết lấy danh sách ảnh. URL đi qua `ProductImageUrl` và thẻ ảnh trong view.
Web có fallback placeholder trong mã hiện tại, nhưng checklist này không tạo/copy hoặc dùng placeholder làm ảnh thật.

Sau khi người dùng thêm đủ ảnh và phê duyệt riêng, chọn một luồng:
- CatalogImport: kiểm tra chỉ đọc trước; khi được phép mới tạo/cập nhật Products và ProductImages,
  dùng URL `/uploads/products/<ImageBase>.<đuôi-thực>`, primary và display order theo công cụ.
- Admin: chọn ảnh cho từng Product đã lưu; service tạo liên kết tới bản copy GUID.

CatalogImport có thể ghi lại giá/tồn kho/active và ảnh chính của sản phẩm trùng; không tự chạy chỉ vì đã đủ ảnh.
Không tạo ProductImages khi chưa có ProductId tương ứng. Không chèn SQL thủ công ở bước chuẩn bị này.
## S29.7D — kết quả kiểm tra 2026-11-02

**Tổng thể: PARTIAL. Kỹ thuật: PASS 40/40. Nội dung/độ đúng sản phẩm: chưa được duyệt.**

- Thư mục có 41 file: 40 JPG/JPEG thực, 0 PNG, 0 WebP và 1 `HUONG_DAN.txt` không phải ảnh.
- 40/40 tên file khớp checklist; thiếu 0, ảnh thừa 0, sai tên 0, trùng tên gốc 0.
- SHA256 có 40 giá trị khác nhau: không có file trùng byte; không đồng nghĩa mọi hình khác nhau về nội dung.
- Cả 40 file giải mã và render được bằng System.Drawing; signature JPEG khớp đuôi .jpg.
- Tất cả 800×800 px, dung lượng 56.216–96.906 byte/file, tổng 2.906.958 byte; đều >0 và ≤10 MiB.
- Không có yêu cầu minimum resolution khác trong source; 800×800 không chứng minh chi tiết gốc sắc nét.
- GET từng `/uploads/products/<tên-file>` tại localhost:5015: 40/40 HTTP 200 và Content-Type image/jpeg.
- Đã xem trực tiếp toàn bộ 40 ảnh; caption phía dưới tương ứng tên model/file, nhưng không xác thực được ngoại hình thật.
- `HUONG_DAN.txt` mô tả đây là ảnh minh họa AI tách từ bảng, không phải ảnh chính thức. Đây là thông tin nguồn, không phải chứng nhận.

### Điểm cần xử lý/duyệt thủ công

| Ảnh/phạm vi | Quan sát trực tiếp | Đánh giá |
|---|---|---|
| Toàn bộ 40 ảnh | Dạng card có caption, chi tiết/nhãn/logo mềm hoặc méo; nhiều nét chữ trên thân/màn hình không rõ | Chưa đạt xác minh ảnh sản phẩm thật; cần duyệt nếu chỉ dùng demo |
| `poco-x6-pro.jpg` | Cạnh phải máy bị cắt sát khung hình bên trong | Cần ảnh đủ khung hoặc chấp nhận hạn chế demo |
| `realme-12-pro-plus.jpg`, `oneplus-11.jpg`, `oneplus-12.jpg`, `google-pixel-7.jpg`, `google-pixel-7-pro.jpg`, `google-pixel-8.jpg`, `google-pixel-8-pro.jpg` | Mép trên sát/cắt vào vùng ảnh điện thoại | Cần kiểm tra bản gốc/crop thủ công |
| `vivo-v30-pro.jpg` | Caption dưới ghi Vivo V30 Pro nhưng chữ trên màn hình chỉ đọc được dạng “VPro”, không rõ V30 | Nhãn không đủ tin cậy, cần đối chiếu ảnh gốc/model |
| `oppo-reno11-f.jpg`, `oneplus-11.jpg`, `oneplus-nord-3.jpg` | Chữ nhỏ cạnh model có dấu hiệu biến dạng | Cần kiểm tra chữ/logo, không tự coi là nhãn chính xác |

Các file còn lại không có lỗi tên/giải mã/dung lượng được phát hiện, nhưng vẫn cần xác nhận model/màu/camera so với nguồn đáng tin cậy.
Không kết luận ảnh chính hãng hoặc đúng cấu hình chỉ từ caption. Không dùng kiểm tra tự động để phê duyệt tính xác thực.

### Điều kiện hiển thị và bước tiếp theo

File tĩnh đã truy cập được, nhưng catalog chỉ hiện khi Product tồn tại và IsActive=true;
ProductImages phải có ProductId đúng và ImageUrl `/uploads/products/<tên-file>` đúng tên/đuôi.
Card chọn primary rồi DisplayOrder/ImageId, chi tiết đọc danh sách; CSS object-fit:contain không sửa phần đã bị cắt trong file.

Chưa chạy import, tạo ProductImages hoặc thay đổi database/Admin. Sau khi người dùng chấp nhận bộ minh họa AI cho demo
hoặc tự thay bằng ảnh sản phẩm phù hợp, kiểm tra lại file thay đổi rồi xin phê duyệt riêng kế hoạch import/liên kết.
`HUONG_DAN.txt` được giữ nguyên; importer hiện liệt kê nó trong Unsupported nhưng chỉ dừng với ảnh thiếu/trùng.
