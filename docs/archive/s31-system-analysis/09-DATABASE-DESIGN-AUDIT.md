# S31.1 — PhoneStore Database Design Audit

Ngày xác minh: 2026-11-13 · Phạm vi: read-only · Database: `PhoneStoreDB`

## A. Database Overview

| Thuộc tính | Kết quả |
|---|---|
| SQL Server | Microsoft SQL Server 2022, `16.0.1000.6` |
| Instance | Default instance `MSSQLSERVER` tại `localhost` |
| Database | `PhoneStoreDB` |
| Database collation | `SQL_Latin1_General_CP1_CI_AS` (case-insensitive) |
| Ứng dụng | Web/Admin cùng dùng connection key `PhoneStoreConnection` |
| Authentication | Windows/Integrated Security; không có SQL password trong cấu hình đã đọc |
| EF Core | SQL Server provider/migrations 8.0.28 |
| Applied migrations | `20261001150857_InitialCreate`; `20261102122856_EnforceSingleAdmin` |
| Bảng nghiệp vụ | 8; thêm bảng hạ tầng `__EFMigrationsHistory` |
| Trạng thái đối chiếu | **VERIFIED LIVE** bằng `sys.*` metadata và source EF Core |

Kết nối trong sandbox bị giới hạn TLS/credential, nhưng truy vấn SELECT ngoài sandbox bằng exception hẹp đã PASS. Không có DML/DDL, migration hoặc đọc dữ liệu cá nhân. Connection string được mô tả theo thuộc tính, không sao chép secret.

## B. Danh sách bảng nghiệp vụ

| Bảng | Chức năng |
|---|---|
| `Roles` | Danh mục vai trò hệ thống: Admin, Customer |
| `Users` | Tài khoản đăng nhập, hồ sơ Customer/Admin và trạng thái hoạt động |
| `Brands` | Danh mục thương hiệu sản phẩm, hỗ trợ soft-state |
| `Categories` | Danh mục loại sản phẩm, hỗ trợ soft-state |
| `Products` | Thông tin điện thoại, cấu hình, giá, tồn kho và trạng thái bán |
| `ProductImages` | Danh sách ảnh, ảnh chính và thứ tự hiển thị của sản phẩm |
| `Orders` | Header đơn hàng, người nhận, tổng tiền, trạng thái và thời điểm đặt |
| `OrderDetails` | Các dòng sản phẩm của đơn; lưu snapshot đơn giá tại lúc checkout |

`__EFMigrationsHistory` là bảng hạ tầng EF Core, không phải bảng nghiệp vụ.

## C. Chi tiết cấu trúc từng bảng

Quy ước: `PK`, `FK`; `ID` = identity `(1,1)`; `NN` = NOT NULL; default hiển thị theo SQL Server live.

### Roles

| Cột | SQL type | Null | Key/Identity | Default | Ý nghĩa |
|---|---|---:|---|---|---|
| RoleId | int | NN | PK, ID | — | Mã vai trò |
| RoleName | nvarchar(50) | NN | Unique index | — | Tên vai trò |

### Users

| Cột | SQL type | Null | Key/Identity | Default | Ý nghĩa |
|---|---|---|---|---|---|
| UserId | int | NN | PK, ID | — | Mã người dùng |
| RoleId | int | NN | FK→Roles | — | Vai trò |
| FullName | nvarchar(100) | NN | — | — | Họ tên |
| Email | nvarchar(150) | NN | Unique index | — | Email đăng nhập |
| Phone | nvarchar(20) | NULL | — | — | Điện thoại |
| PasswordHash | nvarchar(500) | NN | — | — | Hash mật khẩu, không phải mật khẩu rõ |
| Address | nvarchar(255) | NULL | — | — | Địa chỉ hồ sơ |
| IsActive | bit | NN | — | `1` | Trạng thái hoạt động |
| CreatedAt | datetime2(7) | NN | — | `SYSUTCDATETIME()` | Thời điểm tạo UTC |

### Brands

| Cột | SQL type | Null | Key/Identity | Default | Ý nghĩa |
|---|---|---|---|---|---|
| BrandId | int | NN | PK, ID | — | Mã thương hiệu |
| BrandName | nvarchar(100) | NN | Unique index | — | Tên thương hiệu |
| Description | nvarchar(500) | NULL | — | — | Mô tả |
| IsActive | bit | NN | — | `1` | Soft-state |
| CreatedAt | datetime2(7) | NN | — | `SYSUTCDATETIME()` | Thời điểm tạo UTC |

### Categories

| Cột | SQL type | Null | Key/Identity | Default | Ý nghĩa |
|---|---|---|---|---|---|
| CategoryId | int | NN | PK, ID | — | Mã danh mục |
| CategoryName | nvarchar(100) | NN | Unique index | — | Tên danh mục |
| Description | nvarchar(500) | NULL | — | — | Mô tả |
| IsActive | bit | NN | — | `1` | Soft-state |
| CreatedAt | datetime2(7) | NN | — | `SYSUTCDATETIME()` | Thời điểm tạo UTC |

### Products

| Cột | SQL type | Null | Key/Identity | Default | Ý nghĩa |
|---|---|---|---|---|---|
| ProductId | int | NN | PK, ID | — | Mã sản phẩm |
| ProductName | nvarchar(200) | NN | — | — | Tên điện thoại |
| BrandId | int | NN | FK→Brands | — | Thương hiệu |
| CategoryId | int | NN | FK→Categories | — | Danh mục |
| Price | decimal(18,2) | NN | — | — | Giá bán hiện tại |
| StockQuantity | int | NN | — | — | Tồn kho |
| RAM | nvarchar(50) | NULL | — | — | RAM |
| Storage | nvarchar(50) | NULL | — | — | Bộ nhớ |
| Chip | nvarchar(150) | NULL | — | — | Vi xử lý |
| Screen | nvarchar(200) | NULL | — | — | Màn hình |
| Camera | nvarchar(250) | NULL | — | — | Camera |
| Battery | nvarchar(100) | NULL | — | — | Pin |
| OperatingSystem | nvarchar(100) | NULL | — | — | Hệ điều hành |
| Description | nvarchar(max) | NULL | — | — | Mô tả dài |
| IsActive | bit | NN | — | `1` | Trạng thái bán |
| CreatedAt | datetime2(7) | NN | — | `SYSUTCDATETIME()` | Thời điểm tạo UTC |
| UpdatedAt | datetime2(7) | NULL | — | — | Lần cập nhật cuối |

### ProductImages

| Cột | SQL type | Null | Key/Identity | Default | Ý nghĩa |
|---|---|---|---|---|---|
| ImageId | int | NN | PK, ID | — | Mã ảnh |
| ProductId | int | NN | FK→Products | — | Sản phẩm sở hữu |
| ImageUrl | nvarchar(500) | NN | — | — | Đường dẫn ảnh |
| IsPrimary | bit | NN | — | `0` | Ảnh chính |
| DisplayOrder | int | NN | — | — | Thứ tự hiển thị |

### Orders

| Cột | SQL type | Null | Key/Identity | Default | Ý nghĩa |
|---|---|---|---|---|---|
| OrderId | int | NN | PK, ID | — | Mã nội bộ |
| UserId | int | NN | FK→Users | — | Customer đặt hàng |
| OrderCode | nvarchar(30) | NN | Unique index | — | Mã đơn/idempotency |
| ReceiverName | nvarchar(100) | NN | — | — | Người nhận snapshot |
| ReceiverPhone | nvarchar(20) | NN | — | — | Điện thoại nhận hàng |
| ShippingAddress | nvarchar(300) | NN | — | — | Địa chỉ giao hàng |
| Note | nvarchar(500) | NULL | — | — | Ghi chú |
| OrderDate | datetime2(7) | NN | — | `SYSUTCDATETIME()` | Ngày đặt UTC |
| TotalAmount | decimal(18,2) | NN | — | — | Tổng tiền server tính |
| Status | nvarchar(20) | NN | — | `N'Pending'` | Trạng thái đơn |

### OrderDetails

| Cột | SQL type | Null | Key/Identity | Default | Ý nghĩa |
|---|---|---|---|---|---|
| OrderDetailId | int | NN | PK, ID | — | Mã dòng đơn |
| OrderId | int | NN | FK→Orders | — | Đơn hàng |
| ProductId | int | NN | FK→Products | — | Sản phẩm |
| Quantity | int | NN | — | — | Số lượng mua |
| UnitPrice | decimal(18,2) | NN | — | — | Đơn giá snapshot |

## D. Quan hệ PK/FK

| Bảng cha | Bảng con | Cardinality | FK | ON DELETE | Xác minh live |
|---|---|---|---|---|---|
| Roles | Users | 1–N | Users.RoleId | NO ACTION | Có |
| Brands | Products | 1–N | Products.BrandId | NO ACTION | Có |
| Categories | Products | 1–N | Products.CategoryId | NO ACTION | Có |
| Users | Orders | 1–N | Orders.UserId | NO ACTION | Có |
| Products | ProductImages | 1–N | ProductImages.ProductId | CASCADE | Có |
| Orders | OrderDetails | 1–N | OrderDetails.OrderId | CASCADE | Có |
| Products | OrderDetails | 1–N | OrderDetails.ProductId | NO ACTION | Có |

Suy ra quan hệ nghiệp vụ Orders–Products là N–N qua `OrderDetails`; đây không phải FK N–N trực tiếp. Kiểm tra live cho bốn nhóm quan hệ chính cho kết quả 0 orphan.

## E. Constraints và indexes

### Primary keys

Mỗi bảng nghiệp vụ có một **clustered PK** theo cột `...Id`; tất cả PK nghiệp vụ được SQL live xác nhận là `int IDENTITY(1,1)`. Các index còn lại là nonclustered.

### Unique

SQL live không có đối tượng `UNIQUE_CONSTRAINT` riêng ngoài PK. Tính duy nhất được EF Core tạo bằng **unique index**:

- `IX_Roles_RoleName` (`RoleName`)
- `IX_Users_Email` (`Email`)
- `UX_Users_SingleAdmin` (`RoleId`) với filter `[RoleId]=(1)`
- `IX_Brands_BrandName` (`BrandName`)
- `IX_Categories_CategoryName` (`CategoryName`)
- `IX_Orders_OrderCode` (`OrderCode`)

### Check constraints

- `CK_Roles_AdminIdentity`: RoleId 1 phải là Admin và tên Admin không được dùng cho role khác.
- `CK_Products_Price`: Price > 0.
- `CK_Products_StockQuantity`: StockQuantity >= 0.
- `CK_ProductImages_DisplayOrder`: DisplayOrder >= 0.
- `CK_Orders_TotalAmount`: TotalAmount >= 0.
- `CK_OrderDetails_Quantity`: Quantity > 0.
- `CK_OrderDetails_UnitPrice`: UnitPrice >= 0.

### Non-unique indexes

- `IX_Users_RoleId`
- `IX_Products_BrandId`, `IX_Products_CategoryId`
- `IX_ProductImages_ProductId`
- `IX_Orders_UserId`
- `IX_OrderDetails_OrderId`, `IX_OrderDetails_ProductId`

## F. Khác biệt và điểm cần lưu ý

### Đối chiếu Entity ↔ Fluent API ↔ Migration ↔ SQL live

- Không phát hiện schema drift về bảng, cột, type/length, nullable, identity, default, PK/FK, check hay index.
- Hai migrations trong source đều xuất hiện trong `__EFMigrationsHistory` với EF Core 8.0.28.
- Enum `OrderStatus` được lưu dạng `nvarchar(20)` đúng Fluent API; default live là `Pending`.
- `Description` của Products là `nvarchar(max)` đúng convention khi Fluent API không giới hạn độ dài.

### Design observations, không phải drift

- Database chưa có CHECK giới hạn `Orders.Status` vào tập enum; validation hiện thuộc ứng dụng.
- Database chưa có unique `(OrderId, ProductId)`; service checkout chủ động từ chối dòng sản phẩm trùng.
- Database chưa enforce “mỗi Product chỉ có một IsPrimary=1” hoặc unique DisplayOrder; service quản lý ảnh đảm nhiệm.
- Không có `CompletedAt` hay bảng lịch sử trạng thái; báo cáo doanh thu dùng `OrderDate` theo nghiệp vụ hiện tại.
- Không có bảng Cart vì giỏ hàng nằm trong session; đây là thiết kế hiện hành, không phải bảng thiếu.
- Email/name uniqueness theo collation live `SQL_Latin1_General_CP1_CI_AS`, do đó so sánh duy nhất không phân biệt hoa/thường theo collation mặc định.

Không tự đề xuất migration cho các observation trên. Mọi thay đổi schema cần quyết định nghiệp vụ và phê duyệt riêng.

## G. Đề xuất ERD và Excel Database Design

### ERD

Nên trình bày 8 entity theo ba cụm:

1. **Identity:** Roles → Users.
2. **Catalog:** Brands → Products ← Categories; Products → ProductImages.
3. **Commerce:** Users → Orders → OrderDetails ← Products.

Trên mỗi relationship ghi rõ cardinality, FK và `ON DELETE`. Đánh dấu `OrderDetails.UnitPrice` là snapshot giá; `Orders.Receiver*` là snapshot giao hàng. Không đưa `__EFMigrationsHistory` vào ERD nghiệp vụ, có thể ghi chú riêng trong technical appendix.

### Workbook Excel đề xuất

| Sheet | Nội dung |
|---|---|
| `00_Overview` | DBMS, database, phiên bản, phạm vi, quy ước |
| `01_Table_List` | Bảng, mô tả, loại master/transaction/detail |
| `02_Column_Dictionary` | Table, ordinal, column, type, length, precision/scale, nullable, PK/FK, identity, default, mô tả |
| `03_Relationships` | Parent/child, cardinality, FK, ON DELETE |
| `04_Constraints` | PK, unique indexes, check constraints, biểu thức |
| `05_Indexes` | Tên index, bảng, cột, unique/filter, mục đích |
| `06_Domain_Rules` | OrderStatus, role mapping, stock/price/quantity rules |
| `07_Migrations` | MigrationId, EF version, mục đích |
| `08_ERD_Legend` | Ký hiệu ERD, snapshot fields, soft-state fields |
| `09_Verification` | Nguồn EF/SQL, ngày xác minh, query read-only, kết quả drift |

Nên dùng một dòng cho mỗi cột ở `02_Column_Dictionary`, không gộp ô; bật filter/freeze header để thuận tiện chấm đồ án.

## Phân loại bằng chứng

- **VERIFIED SQL SERVER:** database/version, tables/columns/types/default/identity, PK/FK/delete rules, checks, indexes, migrations.
- **VERIFIED SOURCE:** ý nghĩa entity, navigation, enum conversion và nghiệp vụ snapshot/session.
- **SUY RA CÓ KIỂM SOÁT:** cardinality N–N Orders–Products qua OrderDetails và cách nhóm ERD.
- **CHƯA XÁC MINH:** mức độ tối ưu index theo workload/Execution Plan thực tế; không ảnh hưởng tính đúng của schema inventory hiện tại.

