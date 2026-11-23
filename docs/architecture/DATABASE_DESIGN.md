# PhoneStore — Thiết kế cơ sở dữ liệu hiện hành

> Tài liệu mô tả lược đồ cơ sở dữ liệu thực tế hiện hành (`9` bảng, `3` EF Core migrations) đối chiếu trực tiếp từ [`PhoneStoreDbContext.cs`](../../src/PhoneStore.Data/Context/PhoneStoreDbContext.cs) và `src/PhoneStore.Data/Configurations/`.
> Báo cáo kiểm toán thiết kế gốc nằm tại [`archive/s31-system-analysis/09-DATABASE-DESIGN-AUDIT.md`](../archive/s31-system-analysis/09-DATABASE-DESIGN-AUDIT.md).

## 1. Nguồn dữ liệu thống nhất

Từ mốc `S-DB.UNIFY.1B` (2026-11-23), toàn bộ môi trường phát triển và chạy nghiệp vụ của PhoneStore dùng chung **một cơ sở dữ liệu duy nhất**:

| Môi trường / Instance | Địa chỉ kết nối | Database | Vai trò hiện tại |
|---|---|---|---|
| **Docker SQL Server 2022** (`phonestore-restore-c2-db-1`) | Docker nội bộ: `db,1433`<br>Local host: `127.0.0.1,14334` | `PhoneStoreDB` | **Nguồn dữ liệu nghiệp vụ chính duy nhất** cho cả Docker Business (`web`/`api`) và Local Dev (`dotnet run` qua `.NET User Secrets`) |
| **Windows SQL Server** (`localhost`) | `localhost` (Windows Auth) | `PhoneStoreDB` | Bản lưu trữ cũ (`read-only archive`), không ghi nghiệp vụ mới, không xóa/restore đè |
| **Swagger Dev cách ly** (`phonestore-dev-swagger`) | Nội bộ stack `5026` | `PhoneStoreDB_SwaggerDev` | Database tách biệt riêng cho thử nghiệm Swagger |

## 2. Lịch sử EF Core Migrations

Tất cả migrations đặt tại [`src/PhoneStore.Data/Migrations/`](../../src/PhoneStore.Data/Migrations/):

1. `20261001150857_InitialCreate` — Khởi tạo 8 bảng nghiệp vụ cốt lõi (`Roles`, `Users`, `Brands`, `Categories`, `Products`, `ProductImages`, `Orders`, `OrderDetails`), seed 2 vai trò `Admin` (`RoleId = 1`) và `Customer` (`RoleId = 2`).
2. `20261102122856_EnforceSingleAdmin` — Bổ sung ràng buộc tầng DB cho quy tắc **Single Admin** (`CK_Roles_AdminIdentity` trên `Roles` và filtered unique index `UX_Users_SingleAdmin` trên `Users`).
3. `20261121044020_AddApiRefreshTokens` — Bổ sung bảng `ApiRefreshTokens` phục vụ Local JWT Refresh Token Rotation, thu hồi theo session family và phát hiện tái sử dụng (`ReuseDetected`).

> **Nguyên tắc an toàn**: Không tự động chạy `dotnet ef database update`, không drop/recreate hay sửa dữ liệu trên `PhoneStoreDB` đang vận hành nếu chưa được người dùng phê duyệt tường minh.

## 3. Lược đồ 9 bảng và quan hệ

```text
Roles (1) ───────────< (N) Users (1) ───────────< (N) Orders (1) ───────────< (N) OrderDetails
                             │                                                       │
                             └────< (N) ApiRefreshTokens                             │ (N)
                                                                                     │
Brands (1) ──────────< (N) Products (1) ─────────────────────────────────────────────┘
                             │   │
Categories (1) ──────< (N) ──┘   └────< (N) ProductImages
```

### Chi tiết từng bảng (đối chiếu Fluent API trong `src/PhoneStore.Data/Configurations/`)

| # | Bảng | Khóa chính | Cột chính & Kiểu/Giới hạn | Khóa ngoại & Quy tắc xóa | Ràng buộc & Index quan trọng |
|---|---|---|---|---|---|
| 1 | `Roles` | `RoleId` | `RoleName` (`nvarchar(50)`, required) | — | `UQ(RoleName)`; `CK_Roles_AdminIdentity`: `(RoleId = 1 AND RoleName = 'Admin') OR (RoleId <> 1 AND RoleName <> 'Admin')`; seed `1=Admin`, `2=Customer` |
| 2 | `Users` | `UserId` | `FullName (100)`, `Email (150)`, `Phone (20)`, `PasswordHash (500)`, `Address (255)`, `IsActive (default true)`, `CreatedAt (SYSUTCDATETIME())`, `RoleId` | `RoleId -> Roles` (`NoAction`) | `UQ(Email)`; `IX(RoleId)`; **`UX_Users_SingleAdmin`** (`UNIQUE` filtered `WHERE [RoleId] = 1`) |
| 3 | `Brands` | `BrandId` | `BrandName`, `IsActive`, ... | — | Tên thương hiệu duy nhất/chuẩn hóa ở tầng nghiệp vụ |
| 4 | `Categories` | `CategoryId` | `CategoryName`, `IsActive`, ... | — | Tên danh mục duy nhất/chuẩn hóa ở tầng nghiệp vụ |
| 5 | `Products` | `ProductId` | `ProductName (200)`, `Price decimal(18,2)`, `StockQuantity`, `RAM (50)`, `Storage (50)`, `Chip (150)`, `Screen (200)`, `Camera (250)`, `Battery (100)`, `OperatingSystem (100)`, `Description`, `IsActive (default true)`, `CreatedAt`, `BrandId`, `CategoryId` | `BrandId -> Brands` (`NoAction`); `CategoryId -> Categories` (`NoAction`) | `CK_Products_Price` (`[Price] > 0`); `CK_Products_StockQuantity` (`[StockQuantity] >= 0`) |
| 6 | `ProductImages` | `ImageId` | `ProductId`, `ImageUrl (500)`, `IsPrimary (default false)`, `DisplayOrder` | `ProductId -> Products` (`Cascade`) | `CK_ProductImages_DisplayOrder` (`[DisplayOrder] >= 0`) |
| 7 | `Orders` | `OrderId` | `OrderCode (30)`, `UserId`, `ReceiverName (100)`, `ReceiverPhone (20)`, `ShippingAddress (300)`, `Note (500)`, `OrderDate (SYSUTCDATETIME())`, `TotalAmount decimal(18,2)`, `Status (string(20), default Pending)` | `UserId -> Users` (`NoAction`) | `UQ(OrderCode)`; `CK_Orders_TotalAmount` (`[TotalAmount] >= 0`) |
| 8 | `OrderDetails` | `OrderDetailId` | `OrderId`, `ProductId`, `Quantity`, `UnitPrice decimal(18,2)` | `OrderId -> Orders` (`Cascade`); `ProductId -> Products` (`NoAction`) | `CK_OrderDetails_Quantity` (`[Quantity] > 0`); `CK_OrderDetails_UnitPrice` (`[UnitPrice] >= 0`) |
| 9 | `ApiRefreshTokens` | `ApiRefreshTokenId` | `UserId`, `FamilyId (32)`, `TokenHash (64)`, `CreatedAtUtc`, `ExpiresAtUtc`, `RevokedAtUtc`, `RevokedReason (64)`, `ReplacedByTokenHash (64)` | `UserId -> Users` (`Cascade`) | `UX_ApiRefreshTokens_TokenHash` (`UNIQUE`); `IX_ApiRefreshTokens_UserId`; `IX_ApiRefreshTokens_FamilyId` |

## 4. Cơ chế toàn vẹn giao dịch & chống tranh chấp dữ liệu

1. **Đặt hàng (Checkout) và Hủy đơn (Cancel)**:
   - Thực thi trong transaction mức cô lập **`Serializable`** kết hợp khóa cập nhật tồn kho nguyên tử (`UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE ProductId = @id AND StockQuantity >= @qty`).
   - Tránh đặt trùng đơn (submission guard ở Web / `Idempotency-Key` ở API).
   - Hủy đơn chỉ hợp lệ từ `Pending → Cancelled` (Customer/Admin) hoặc `Confirmed → Cancelled` (Admin), hoàn trả tồn kho đúng 1 lần duy nhất; không cho hủy lại đơn đã `Cancelled` hoặc `Completed`.
2. **Bảo vệ tồn kho khi Admin chỉnh sửa sản phẩm**:
   - Dùng `OriginalStockQuantity` làm điều kiện lạc quan (`optimistic guard`) khi cập nhật tồn kho sản phẩm, ngăn ghi đè nhầm khi có đơn hàng vừa trừ tồn kho song song.
3. **Khóa ứng dụng cho thao tác ảnh sản phẩm**:
   - Các giao dịch cập nhật/gắn ảnh dùng khóa ứng dụng theo phạm vi DB để tránh deadlock chuyển đổi range-lock trong `Serializable`.
4. **Refresh Token lưu an toàn**:
   - Chỉ lưu **SHA-256 hex (64 ký tự)** của refresh token vào `ApiRefreshTokens.TokenHash`, không bao giờ lưu chuỗi token gốc trong database.
   - Mọi thao tác rotate/revoke chạy trong transaction có khóa hàng (`FindByHashForUpdateAsync`), đánh dấu `RevokedReason` (`Rotated`, `Logout`, `ReuseDetected`, `Expired`, `AccountInactive`).
5. **Báo cáo doanh thu theo múi giờ nghiệp vụ**:
   - `OrderDate` lưu UTC (`SYSUTCDATETIME()`), tầng báo cáo (`ReportingCalendar`) quy đổi biên ngày theo **UTC+7 (`Asia/Bangkok`)** và chỉ cộng dồn doanh thu từ các đơn `Status = Completed`.
