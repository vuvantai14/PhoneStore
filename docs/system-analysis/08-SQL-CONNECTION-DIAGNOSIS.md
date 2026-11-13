# 08 — Chẩn đoán môi trường kết nối SQL Server

Ngày xác minh: 2026-11-13 · Trạng thái: **S30.4A.2 — READY FOR REVIEW**

## Kết luận đã xác minh

Lỗi TLS trong S30.4A/A.1 là khác biệt của **security context Codex sandbox**, không phải sự cố chung của SQL Server, schema hay connection string PhoneStore:

- Cùng `sqlcmd`, cùng Windows Authentication, cùng `localhost/PhoneStoreDB` và không đổi cấu hình: chạy trong sandbox thất bại trước login; chạy ngoài sandbox bằng exception hẹp thì `SELECT DB_NAME(), SERVERPROPERTY('ProductVersion')` PASS.
- SQL Server default instance `MSSQLSERVER` đang `RUNNING`, auto-start delayed, PID 20228; binary SQL Server 2022 `16.0.1000.6`.
- Registry SQL Network Configuration: `ForceEncryption=0`, `ForceStrict=0`, certificate binding trống, TCP 1433. Vì vậy không có bằng chứng server ép Force Encryption hay dùng certificate cấu hình riêng.
- Codex chạy dưới `DESKTOP-ES9362P\codexsandboxoffline`; lỗi trong sandbox gồm “No credentials are available in the security package”/“machine does not support encryption”. Đây là bằng chứng trực tiếp về hạn chế credential/TLS của token sandbox.
- Customer Web PID 3772 chạy ở session người dùng, đúng binary workspace; Home và Products đều HTTP 200. Admin không chạy tại thời điểm kiểm tra, nên không có runtime Admin để so trực tiếp.

## Môi trường và client

| Thành phần | Giá trị xác minh |
|---|---|
| SQL instance | Default `MSSQLSERVER` / `localhost` |
| SQL Server | 2022 RTM, `16.0.1000.6` |
| Service account | `NT Service\MSSQLSERVER` |
| TCP | Static 1433; SQL Browser disabled (phù hợp default instance) |
| ODBC 17 | `17.4.1.1` |
| ODBC 18 | `18.6.2.1` |
| sqlcmd | `16.0.1000.6`, đang dùng ODBC 17 |
| Microsoft.Data.SqlClient | Assembly `5.1.7` (transitive qua EF Core SQL Server 8.0.28) |
| .NET | SDK 8.0.427; runtime 8.0.33; host 10.0.14 |
| Web/Admin config | `localhost`, `PhoneStoreDB`, Windows Authentication, MARS, `TrustServerCertificate=True` |
| Secret | Không có password/user SQL trong config đã đọc; không ghi token/cookie |

`TrustServerCertificate=True` không tắt mã hóa; nó chỉ bỏ xác minh chuỗi tin cậy certificate. Không có thay đổi `Encrypt`, trust, server certificate hay registry trong nhiệm vụ này.

## Kết quả read-only ngoài sandbox

- Database: `PhoneStoreDB`; server version `16.0.1000.6`.
- 9 user tables gồm `__EFMigrationsHistory` và 8 bảng nghiệp vụ.
- Counts: Roles 2, Users 3, Brands 8, Categories 1, Products 40, ProductImages 40, Orders 7, OrderDetails 13.
- Hai migrations EF Core 8.0.28 đã applied: `InitialCreate`, `EnforceSingleAdmin`.
- Tất cả quan hệ kiểm tra có 0 orphan: Users–Roles 3, Products–Brand/Category 40, Orders–Users 7, OrderDetails–Order/Product 13.
- PK, 7 FK, 7 check constraints và các unique/filter indexes khớp model snapshot. `UX_Users_SingleAdmin` tồn tại, unique, filter `[RoleId]=(1)`.

## Giả thuyết đã loại trừ hoặc chưa xác minh

- **Đã loại trừ:** SQL service dừng; sai instance/database; Force Encryption; certificate binding riêng; thiếu ODBC 18; lỗi schema/migration ngăn kết nối.
- **Chưa xác minh cấp thấp:** sandbox chặn chính xác API/credential handle nào trong SChannel/SSPI. Không có Schannel System event trong cửa sổ 2 giờ và không cần hạ bảo mật để truy sâu hơn.
- SQL Server đang ở build RTM cũ; đây là maintenance risk độc lập, không phải nguyên nhân đã chứng minh vì kết nối ngoài sandbox PASS.

## Phương án khắc phục (ưu tiên)

### 1. Dùng exception hẹp ngoài sandbox cho SELECT acceptance — khuyến nghị

- Xử lý đúng nguyên nhân: tránh token sandbox không có khả năng TLS/credential cần thiết.
- Thay đổi: không đổi hệ thống; chỉ phê duyệt prefix `sqlcmd -S localhost -E -C -b -d PhoneStoreDB` và câu SELECT cụ thể.
- Rủi ro: thấp; phải review câu SQL để bảo đảm không DML/DDL.
- Kiểm chứng: `DB_NAME()`, metadata/count/integrity SELECT; đã PASS trong S30.4A.2.
- Phê duyệt: cần exception hẹp của sandbox cho từng phạm vi lệnh; đã được phê duyệt trong lượt này.

### 2. Người dùng chạy checklist read-only trong terminal/SSMS bình thường

- Xử lý cùng khác biệt security context mà không cấp thêm quyền cho Codex.
- Thay đổi: không có; người dùng chạy script SELECT đã review bằng Windows account hiện tại.
- Rủi ro: thấp; cần tránh chọn nhầm database và không thêm DML.
- Kiểm chứng: lưu output metadata/count và `DB_NAME()`.
- Phê duyệt: người dùng chủ động thực hiện; không cần thay config.

### 3. Bảo trì/patch SQL Server và client trong change window riêng

- Xử lý rủi ro vận hành do SQL Server 16.0 RTM và ODBC 17 cũ, không phải fix bắt buộc cho lỗi sandbox.
- Thay đổi: cập nhật CU/driver theo chính sách tổ chức, có backup/change plan và restart service.
- Rủi ro: downtime/tương thích; không được thực hiện trong nhiệm vụ này.
- Kiểm chứng: test Web/Admin và read-only SQL trước/sau.
- Phê duyệt: bắt buộc phê duyệt quản trị riêng.

## Quyết định

Không cần đổi connection string, `TrustServerCertificate`, `Encrypt`, certificate hay SQL Server để hoàn tất kiểm kê hiện tại. Phương án 1 đã cho phép xác minh schema read-only thành công và không ảnh hưởng Web/Admin.

