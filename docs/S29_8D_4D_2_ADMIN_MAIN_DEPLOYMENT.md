# S29.8D.4D.2/2A — Triển khai và smoke test Admin main

Ngày thực hiện: 2026-11-02 (Asia/Bangkok). Người dùng đã phê duyệt triển khai D.4B và chấp nhận known limitations, bao gồm race fresh authorization check → commit.

## 1. Kết quả triển khai

**PASS** — runtime D.4B đã được triển khai lên Admin main; không rollback.

| Hạng mục | Kết quả |
|---|---|
| Runtime nguồn | `src/PhoneStore.Admin/obj/S29.8D.4C.1-20261102_155007187/app` |
| Staging main | `src/PhoneStore.Admin/obj/S29.8D.4D.2-staging/20261102_163000_D4B-main` |
| Runtime đích | `src/PhoneStore.Admin/bin/Debug/net8.0-windows` |
| Manifest triển khai | `docs/S29_8D_4D_1_ADMIN_RUNTIME_SHA256.txt` |
| Hash runtime | **58/58 PASS** trước staging và sau copy |
| PID Admin main mới | `4592` |
| Executable | `src/PhoneStore.Admin/bin/Debug/net8.0-windows/PhoneStore.Admin.exe` |
| Database runtime | SQL session PID 4592 xác nhận `PhoneStoreDB`, open transaction 0 |
| Rollback | Không thực hiện; startup, hash và smoke đều PASS |

Không build lại và không sửa runtime D.4B. Baseline được giữ nguyên: build 0 warnings/errors, 161/161 tests PASS.

## 2. Backup và bảo toàn cấu hình

- Backup mới: `src/PhoneStore.Admin/obj/S29.8D.4D.2-binary-backups/20261102_162250451_predeploy`.
- Backup đủ 59 file runtime/config trước deploy; manifest `backup-manifest.sha256.txt` xác minh **59/59 PASS**.
- Không ghi đè backup cũ.
- `appsettings.json` main giữ nguyên SHA-256 `E6B9F351495384A52E7626E12CE10B45DB78F65B6BDA7185BEFB53E7EFAB6ABA`.
- Connection string giữ `localhost/PhoneStoreDB`; uploads resolve tới `src/PhoneStore.Web/wwwroot/uploads/products`.
- Staging main không chứa `PhoneStoreDB_StockConcurrencyTest` hoặc đường dẫn uploads test.
- Uploads main và 40 ảnh không bị sao chép, xóa hoặc ghi đè.

## 3. Smoke test WinForms thật

| Chức năng | Bằng chứng | Kết quả |
|---|---|---|
| Login | Người dùng tự đăng nhập Admin main; shell `PhoneStore Admin` mở, SQL session đúng main | **PASS WinForms thật** |
| Dashboard | Bằng chứng UI do người dùng xác nhận | **PASS WinForms thật** |
| Reports 01/11/2026–02/11/2026 | 1 đơn, 0 Completed, doanh thu 0đ, tồn 740 | **PASS WinForms thật** |
| Product list | Hiển thị đủ 40 sản phẩm | **PASS WinForms thật** |
| Tìm `OnePlus Nord 3` | Trả đúng sản phẩm | **PASS WinForms thật** |
| Từ khóa không tồn tại | Hiển thị không có kết quả | **PASS WinForms thật** |
| Xóa bộ lọc | Trở lại 40 sản phẩm | **PASS WinForms thật** |
| Brands | Bằng chứng UI do người dùng xác nhận | **PASS WinForms thật** |
| Categories | Bằng chứng UI do người dùng xác nhận | **PASS WinForms thật** |
| Customers | Bằng chứng UI do người dùng xác nhận | **PASS WinForms thật** |
| Orders list/detail | Đơn Cancelled; vivo Y17s ×1; tổng 3.490.000đ | **PASS WinForms thật** |
| Logout | Trở về LoginForm | **PASS WinForms thật** |

Smoke chỉ đọc; không bấm Save/Delete/Confirm/Cancel và không đổi trạng thái nghiệp vụ.

## 4. Hậu kiểm dữ liệu và ảnh

SELECT read-only cuối cùng trên `PhoneStoreDB`:

- Products 40; ProductImages 40.
- Tổng tồn 740.
- Một đơn `Cancelled`.
- Một Admin active.
- 40/40 ảnh khớp SHA-256 baseline.

Test DB cuối cùng giữ baseline đã ghi: Roles 2, Users 5, Brands 2, Categories 2, Products 5, ProductImages 4, Orders 10, OrderDetails 10, tổng tồn 14. Admin test PID 15548 còn chạy trong các checkpoint deploy/hậu kiểm ban đầu; tại checkpoint cuối chỉ còn PID main 4592. Không có thao tác nào của D.4D.2 gửi tới test process hoặc ghi test DB.

## 5. Known limitations còn lại

- Nội dung chính xác thông báo revoke chưa được lưu đầy đủ, dù block DML/session clear đã PASS runtime.
- Fresh-check authorization và business commit chưa cùng transaction; một write in-flight có thể hoàn tất nếu revoke xảy ra đúng khoảng race.
- Không có UI chỉnh `DisplayOrder`; thay file ảnh trực tiếp NOT SUPPORTED.
- Đóng form giữa Save và nội dung cụ thể một số Brand validation chưa có full UI evidence.
- Customer edit/lock trên Admin chưa nghiệm thu UI đầy đủ.

## 6. Kết luận

**PASS** cho S29.8D.4D.2 và S29.8D.4D.2A.

Admin main đang chạy đúng binary D.4B, đúng `PhoneStoreDB`; backup và rollback artifact hợp lệ; toàn bộ smoke bắt buộc đã PASS; database, cấu hình và ảnh main không đổi. Không migration, seed, restore, commit hoặc push.
