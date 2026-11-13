# 07 — Kiểm kê schema database

Nguồn: `PhoneStoreDbContext`, entity configurations, migrations `InitialCreate`, `EnforceSingleAdmin` và model snapshot.

## Xác minh SQL Server read-only S30.4A.1/A.2

Trạng thái cuối: **VERIFIED READ-ONLY**. Kết nối trong sandbox vẫn bị chặn TLS, nhưng cùng `sqlcmd` chạy ngoài sandbox bằng exception hẹp đã kết nối `PhoneStoreDB` và thực thi SELECT metadata/count thành công. Xem chẩn đoán đầy đủ tại `08-SQL-CONNECTION-DIAGNOSIS.md`.

Hai đường kết nối chỉ đọc đã được thử với Windows Authentication và database đích `PhoneStoreDB`:

1. `sqlcmd.exe` 16.0.1000.6 dùng Microsoft ODBC Driver 17: dừng trước login với lỗi encryption/SSL (`Encryption not supported on the client`, `SSL Provider: No credentials are available in the security package`).
2. Một verifier tạm thời dùng đúng `Microsoft.Data.SqlClient` từ dependency của `PhoneStore.Data`, cùng các thuộc tính kết nối của Web/Admin (`Server=localhost`, `Database=PhoneStoreDB`, integrated security, MARS và `TrustServerCertificate=True`): dừng trong `SqlConnection.OpenAsync` với SQL error 20, “server requires encryption but this machine does not support it”. Verifier chỉ chứa guard `DB_NAME()` và các SELECT metadata/count; guard chưa chạy vì kết nối chưa mở. File source tạm đã được loại bỏ sau chẩn đoán; chỉ artifact ignored còn có thể tồn tại trong `scratch`.

`TrustServerCertificate=True` chỉ bỏ kiểm tra chuỗi tin cậy chứng chỉ, không tắt TLS. Đối chiếu ngoài sandbox xác nhận SQL Server và connection string hoạt động; nguyên nhân thuộc security context `codexsandboxoffline`, không phải schema/EF model. Không thay đổi connection string, SQL Server, certificate hoặc chính sách encryption.

### Bằng chứng truy vấn đã thực thi

Chỉ chạy `DB_NAME()`, `SERVERPROPERTY`, `sys.tables`, `sys.columns`, `sys.key_constraints`, `sys.foreign_keys`, `sys.check_constraints`, `sys.indexes`, `COUNT_BIG(*)`, migration history và LEFT JOIN tìm orphan. Không có DML/DDL, migration hoặc seed.

- Bảng nghiệp vụ/count: Roles 2; Users 3; Brands 8; Categories 1; Products 40; ProductImages 40; Orders 7; OrderDetails 13.
- Migration applied: `20261001150857_InitialCreate`, `20261102122856_EnforceSingleAdmin`, EF Core 8.0.28.
- Quan hệ matched/orphan: Users–Roles 3/0; Products–BrandCategory 40/0; Orders–Users 7/0; OrderDetails–OrderProduct 13/0.
- SQL có đúng 9 PK (kể cả migration history), 7 FK và 7 check constraints như inventory.

| Bảng | PK | FK | Quy tắc |
|---|---|---|---|
| Roles | RoleId | — | RoleName unique; check RoleId=1 ↔ Admin |
| Users | UserId | RoleId→Roles | Email unique; filtered unique một Admin |
| Brands | BrandId | — | BrandName unique; soft-state |
| Categories | CategoryId | — | CategoryName unique; soft-state |
| Products | ProductId | BrandId, CategoryId | Price>0, Stock>=0; FK NoAction |
| ProductImages | ImageId | ProductId | DisplayOrder>=0; Product cascade |
| Orders | OrderId | UserId | OrderCode unique; Total>=0; status string |
| OrderDetails | OrderDetailId | OrderId, ProductId | Qty>0, UnitPrice>=0; Order cascade |

Index query/FK có trên Users.RoleId, Products.BrandId/CategoryId, ProductImages.ProductId, Orders.UserId, OrderDetails.OrderId/ProductId. Cart không có bảng vì nằm trong session. Idempotency checkout dùng OrderCode dẫn xuất token + user. Không có status history/`CompletedAt`; reporting theo `OrderDate`.

Không phát hiện schema drift giữa EF Core/migrations và SQL live trong phạm vi bảng, PK/FK, unique/check/index đã đối chiếu. `UX_Users_SingleAdmin` live là unique filtered index `[RoleId]=(1)`. Không cần migration mới. Schema **READY để thiết kế và chốt ERD**; các system/internal indexes không thuộc mô hình nghiệp vụ.

