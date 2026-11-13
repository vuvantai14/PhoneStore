# S31.3E — PhoneStore SQL Server & Concurrent Runtime Acceptance

Ngày xác minh: 2026-11-13  
Kết luận: **PARTIAL — ADMIN UI NOT VERIFIED / NO-GO cho FULL S31.3F acceptance**

## A. Preflight & Git State

- Branch `main`, HEAD `4f53a0d406cb15c18c7169676b3e171bee665337`.
- Bảo toàn diff S31.3D đang có. Task này không sửa source, config, database; không commit/push.
- Trong lúc acceptance, HEAD được cập nhật bên ngoài tác vụ lên `9d189045b079489d1b9dbe0289c8df8396076478` (`PhoneStore REST API Security & Operational Hardening`); Codex không tạo commit này. Hậu kiểm source S31.3D sạch, chỉ tài liệu S31.3E còn thay đổi.
- Cả ba cấu hình hiệu lực trên đĩa dùng key `PhoneStoreConnection`, Windows Authentication và database `PhoneStoreDB`. Admin uploads vẫn trỏ về thư mục ảnh Web hiện có.
- Port 5015/5025 trống trước khi khởi động; không có process PhoneStore cũ gây xung đột.

## B. Database Identity & Baseline

- SQL Server live read-only: `PhoneStoreDB`.
- Baseline: Roles 2, Users 3, Brands 8, Categories 2, Products 40, ProductImages 41, Orders 11, OrderDetails 26.
- Migration history: `20261001150857_InitialCreate`, `20261102122856_EnforceSingleAdmin`, cùng EF Core 8.0.28.
- Không migration, seed, DML, DDL, backup hay restore.

## C. Customer Web Runtime

Customer Web PID 15524, executable đúng `src/PhoneStore.Web/bin/Debug/net8.0/PhoneStore.Web.exe`, port 5015:

- Home 200; Products 200; Product detail ID 1 200.
- Search `iPhone 12`, brand filter ID 1, category filter ID 1 đều 200.
- Ảnh `/uploads/products/iphone-12.jpg` trả 200, 70,070 bytes.
- Không đăng nhập, cart, checkout, đăng ký hoặc thao tác ghi.
- Runtime log không có `fail`, unhandled exception hay exception bất thường.

Kết quả: **PASS runtime read-only**.

## D. REST API Runtime

API PID 17024, executable đúng `src/PhoneStore.Api/bin/Debug/net8.0/PhoneStore.Api.exe`, port 5025:

- Health, products, product detail 1, brands, categories: 200.
- Default products: page 1, pageSize 10, totalItems 40, totalPages 4.
- Filter brand 1 + category 1 + price ascending + pageSize 5: 5/6 item; first ID 1.
- API detail IDs 1/2/3 khớp SQL về name, brand, category, price, stock và relative image URL.
- Public response không có `isActive`, `passwordHash`, `createdAt`, `orderDetails`.
- Swagger Development 200; không có lỗi 500/log exception.

Kết quả: **PASS runtime read-only**.

## E. Admin WinForms Runtime

- Khởi động đúng executable `src/PhoneStore.Admin/bin/Debug/net8.0-windows/PhoneStore.Admin.exe`, PID 22168, cùng config `PhoneStoreDB`.
- Process tồn tại, cùng desktop session và báo `Responding=True`, nhưng sau hơn 8 giây không có `MainWindowHandle` hoặc top-level window thuộc PID; LoginForm không quan sát được.
- Không nhập credential và không thực hiện thao tác ghi.
- Vì không có UI thật để quan sát, không thể xác minh login, Dashboard/catalog hoặc kết nối SQL qua chính process Admin.

Kết quả: **ADMIN UI NOT VERIFIED**. Đây có thể là giới hạn desktop launch context của Codex; chưa có đủ bằng chứng để kết luận lỗi ứng dụng.

Checklist thủ công còn lại:

1. Chạy Admin từ desktop user session bằng lệnh ở mục L.
2. Xác nhận LoginForm xuất hiện.
3. Tự nhập credential Admin; mở Dashboard và Products chỉ đọc.
4. Đối chiếu sản phẩm IDs 1/2/3 và không bấm Save/Delete/Confirm/Cancel.

## F. Concurrent Runtime Results

- Web 5015, API 5025 và Admin PID 22168 cùng tồn tại đồng thời; không xung đột port.
- Trong lúc Admin process mở, Web/API tiếp tục trả 200 và SQL SELECT hoàn tất.
- Không thấy DI/DbContext lifetime error, SQL connection-pool error, lock bất thường hoặc runtime exception trong log Web/API.
- Admin UI responsiveness và SQL read qua Admin: **NOT VERIFIED** do không có window.

Kết quả: **PARTIAL** — process concurrency PASS, full three-UI concurrency chưa chứng minh.

## G. Data Consistency Matrix

| Dữ liệu | SQL | API | Web | Admin |
|---|---|---|---|---|
| Product IDs 1/2/3 | VERIFIED | VERIFIED | VERIFIED | NOT COMPARABLE |
| Name | VERIFIED | khớp | xuất hiện đúng detail | NOT COMPARABLE |
| Brand/category | VERIFIED | khớp | presentation hoạt động | NOT COMPARABLE |
| Price/stock | VERIFIED | khớp | detail đọc cùng nguồn | NOT COMPARABLE |
| Image URL/file | VERIFIED | relative URL khớp | ảnh HTTP 200 | NOT COMPARABLE |

Ba mẫu: iPhone 12 / 13 / 14, Apple, danh mục `Điện thoại thông minh`, giá lần lượt 12,990,000 / 15,990,000 / 18,990,000 và stock 12 / 18 / 14.

## H. Resilience Verification

- Invalid query 400 và missing product 404: live/API regression PASS.
- Rate limit 429: isolated controlled test PASS, không spam API live.
- Timeout 504 và client cancellation: isolated automated PASS.
- Database unavailable 503: test double PASS; không tắt SQL Server main.
- Swagger Production 404: automated PASS.
- Live health với SQL hoạt động: 200.

## I. Automated Regression

| Hạng mục | Kết quả |
|---|---|
| Solution build | PASS — 0 warnings, 0 errors |
| API Security/Operational | 8/8 PASS |
| API Foundation | 6/6 PASS |
| Catalog API | 18/18 PASS |
| Customer Home/Catalog presentation | 2/2 PASS |
| Admin read-only reporting/management ordering | 14/14 PASS |
| Tổng targeted | **48/48 PASS**, 0 fail, 0 skipped |

Tests dùng doubles/SQLite cách ly hoặc TestServer; không ghi PhoneStoreDB main. Không chạy full suite.

## J. Database Snapshot Comparison

| Bảng | Trước | Sau |
|---|---:|---:|
| Roles | 2 | 2 |
| Users | 3 | 3 |
| Brands | 8 | 8 |
| Categories | 2 | 2 |
| Products | 40 | 40 |
| ProductImages | 41 | 41 |
| Orders | 11 | 11 |
| OrderDetails | 26 | 26 |

Hai migration ID và ProductVersion trước/sau không đổi. Task không thay database.

## K. Process Cleanup

- Dừng không-force đúng PID do task tạo: API 17024, Web 15524, Admin 22168.
- Hậu kiểm: không còn listener 5015/5025 và không còn ba PID task.
- Không dừng SQL Server hoặc process ngoài task.

## L. Remaining Risks & Blockers

- Blocker cho FULL PASS: Admin LoginForm/Dashboard/Products chưa được quan sát trong desktop UI thật.
- Không có bằng chứng Admin UI khớp ba sản phẩm, dù source/config/build và SQL độc lập đều hợp lệ.
- Không chạy stress/load test; concurrent read chỉ ở mức kiểm soát.

Lệnh để người dùng chạy Admin thủ công:

```powershell
& "C:\PhoneStore\PhoneStore\src\PhoneStore.Admin\bin\Debug\net8.0-windows\PhoneStore.Admin.exe"
```

## M. GO / NO-GO for S31.3F

**NO-GO cho FULL S31.3F acceptance tại thời điểm này.** Web/API/runtime/SQL/targeted regression đều PASS và database không đổi, nhưng tiêu chí bắt buộc Admin WinForms UI runtime chưa được xác minh. Sau khi người dùng hoàn thành checklist Admin read-only, có thể nâng S31.3E lên FULL PASS nếu không phát hiện lỗi; chưa tự chuyển sang S31.3F.
