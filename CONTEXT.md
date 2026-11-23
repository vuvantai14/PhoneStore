# PhoneStore Project Context

> Tài liệu mô tả bối cảnh dự án, kiến trúc hiện hành, thực thể nghiệp vụ và quy tắc phát triển cho PhoneStore.
> Trạng thái vận hành, kết quả kiểm thử và bước tiếp theo mới nhất luôn nằm tại [`docs/PROGRESS.md`](docs/PROGRESS.md); lịch sử mốc cũ (`S1–S35`) lưu tại [`docs/archive/PROGRESS_HISTORY.md`](docs/archive/PROGRESS_HISTORY.md).

## 1. Project

- **Đề tài**: Xây dựng hệ thống quản lý và bán điện thoại di động (**PhoneStore**).
- **Nền tảng đã xác minh**:
  - C# / **.NET 10** (`net10.0`, `net10.0-windows`), SDK `10.0.403` theo [`global.json`](global.json).
  - **Customer Web** (`PhoneStore.Web`): ASP.NET Core MVC, Razor Views, Cookie Authentication, Session.
  - **REST API** (`PhoneStore.Api`): ASP.NET Core Web API, Local JWT (`HS256`), Refresh Token Rotation, Swagger (`Development` only).
  - **Admin Desktop** (`PhoneStore.Admin`): Windows Forms (`net10.0-windows`), mặc định kết nối REST API (`RestApi`) và hỗ trợ dự phòng `DirectSql`.
  - **ORM & Database**: Entity Framework Core `10.0.15` + Microsoft SQL Server 2022 (`PhoneStoreDB`).
  - **Containerization & CI/CD**: Docker Compose (Linux containers cho `web`, `api`, `db`) và GitHub Actions + GHCR.

Customer Web, REST API và Admin WinForms dùng chung tầng nghiệp vụ và chung một cơ sở dữ liệu nghiệp vụ `PhoneStoreDB`.

## 2. Architecture

Solution [`PhoneStore.sln`](PhoneStore.sln) gồm 8 project (`6` ứng dụng/thư viện trong `src/`, `1` bộ test tự động, `1` công cụ bootstrap Admin), cộng 3 project phụ trợ ngoài solution (`tools/PhoneStore.CatalogImport`, `tools/PhoneStore.AcceptanceEnvironment`, `tests/PhoneStore.SqlIntegration`):

```text
PhoneStore.Models
  ↑
PhoneStore.Data (EF Core 10, PhoneStoreDbContext, Migrations)
  ↑
PhoneStore.Business (Catalog, Authentication, Purchasing, Management, Reporting, AdminApi clients)
  ↑
  ├── PhoneStore.Web    (Customer Web MVC; gọi Api cho Product Detail khi CatalogApi:Enabled = true)
  ├── PhoneStore.Api    (REST API; LocalJwt + Refresh Token + Admin/Customer/Public endpoints)
  └── PhoneStore.Admin  (WinForms; mặc định Mode = RestApi -> PhoneStore.Api; dự phòng DirectSql)
```

Quy tắc kiến trúc:
- Không circular dependency.
- Business logic dùng chung đặt trong `PhoneStore.Business`; data access đặt trong `PhoneStore.Data`.
- Trong chế độ mặc định `AdminDataAccess:Mode = RestApi`, `PhoneStore.Admin` không đăng ký `PhoneStoreDbContext` (không fallback ngầm sang SQL trực tiếp).
- Không hard-code dữ liệu nghiệp vụ; không tự thêm chức năng ngoài phạm vi được yêu cầu.
- Chi tiết kiến trúc hiện hành xem tại [`docs/architecture/SYSTEM_ARCHITECTURE.md`](docs/architecture/SYSTEM_ARCHITECTURE.md).

## 3. Actors

- **Customer (Customer Web & Customer API)**: Trang chủ, xem danh sách/chi tiết sản phẩm, tìm kiếm/lọc/sắp xếp/phân trang, đăng ký, đăng nhập, quản lý hồ sơ cá nhân, giỏ hàng, đặt hàng (Checkout), xem lịch sử/chi tiết đơn hàng, hủy đơn `Pending`, đăng xuất.
- **Admin (Admin WinForms qua REST API)**: Đăng nhập, Dashboard KPI, quản lý sản phẩm & hình ảnh, quản lý thương hiệu (`Brands`), quản lý danh mục (`Categories`), quản lý khách hàng (xem/sửa/khóa-mở khóa), quản lý đơn hàng & cập nhật trạng thái, báo cáo/thống kê doanh thu, đăng xuất.

## 4. Database

- **Cơ sở dữ liệu chính duy nhất**: `PhoneStoreDB` trên **Docker SQL Server 2022**:
  - Docker Web/API kết nối `db,1433` trên bridge network nội bộ.
  - Local Web/API (`dotnet run`) kết nối `127.0.0.1,14334` thông qua `.NET User Secrets` (ngoài Git, không lưu mật khẩu trong `appsettings.json`).
  - Windows SQL Server `localhost\PhoneStoreDB` chỉ giữ làm bản dữ liệu cũ lưu trữ (read-only archive); server lịch sử `taiiiii\SERVER` không còn tồn tại/sử dụng.
- **9 bảng trong [`PhoneStoreDbContext`](src/PhoneStore.Data/Context/PhoneStoreDbContext.cs)** (3 EF Core migrations: `InitialCreate`, `EnforceSingleAdmin`, `AddApiRefreshTokens`):
  1. `Roles` (1–N `Users`)
  2. `Users` (1–N `Orders`, 1–N `ApiRefreshTokens`; ràng buộc tối đa 1 tài khoản `Admin` qua filtered unique index `UX_Users_SingleAdmin` + `CK_Roles_AdminIdentity`)
  3. `Brands` (1–N `Products`)
  4. `Categories` (1–N `Products`)
  5. `Products` (1–N `ProductImages`, 1–N `OrderDetails`)
  6. `ProductImages`
  7. `Orders` (1–N `OrderDetails`)
  8. `OrderDetails`
  9. `ApiRefreshTokens` (lưu SHA-256 hash refresh token, `FamilyId`, rotation & reuse detection — không lưu token plaintext)
- Không thêm/sửa bảng hay áp dụng migration lên database đang vận hành khi chưa có phê duyệt tường minh.
- Chi tiết schema xem tại [`docs/architecture/DATABASE_DESIGN.md`](docs/architecture/DATABASE_DESIGN.md).

## 5. Authentication, Authorization & Business Rules

- **Xác thực & phân quyền** (chi tiết tại [`docs/architecture/AUTHENTICATION_AND_JWT.md`](docs/architecture/AUTHENTICATION_AND_JWT.md)):
  - Mật khẩu hash bằng `PasswordHasher<User>` (không dùng bảng ASP.NET Identity riêng).
  - Customer Web dùng Cookie Auth (`PhoneStore.Auth`), gắn chủ sở hữu cho `SessionCart`/`CheckoutToken`, kiểm tra tài khoản active.
  - REST API hỗ trợ `ApiAuthentication:Mode = LocalJwt` (`HS256`, `typ=at+jwt`), access token ngắn hạn (`15` phút), refresh token lưu hash có rotation & phát hiện tái sử dụng (`ReuseDetected`), kiểm tra DB role & `IsActive` trên từng request (`Api.Customer`, `Api.Admin`).
  - Quy tắc Single Admin: chỉ tối đa 1 tài khoản vai trò `Admin` (tính cả inactive); đăng ký công khai luôn tạo vai trò `Customer`.
- **Sản phẩm & ảnh**:
  - `Price > 0`, `StockQuantity >= 0`; sản phẩm thuộc 1 `Brand` và 1 `Category` hợp lệ.
  - Ưu tiên ngừng kinh doanh (`IsActive = false`, soft-deactivation) thay vì xóa vật lý khi đã có dữ liệu lịch sử.
  - Cập nhật tồn kho sản phẩm áp dụng bảo vệ tồn kho gốc (`OriginalStockQuantity` optimistic guard).
  - Ảnh sản phẩm chia sẻ qua thư mục `src/PhoneStore.Web/wwwroot/uploads/products` (Local API cấu hình tương đối qua `appsettings.Development.json`; Docker bind-mount `api` `rw` và `web` `ro`), chỉ chấp nhận JPEG/PNG hợp lệ ≤ 10 MB, tên GUID an toàn.
- **Đơn hàng & tồn kho**:
  - Chuỗi trạng thái: `Pending → Confirmed → Shipping → Completed`; hủy hợp lệ từ `Pending → Cancelled` (Customer/Admin) hoặc `Confirmed → Cancelled` (Admin); `Completed` và `Cancelled` là trạng thái kết thúc.
  - `OrderDetail.Quantity > 0`; `UnitPrice` lưu giá tại thời điểm mua (không thay đổi khi `Product.Price` đổi).
  - `TotalAmount = SUM(OrderDetail.Quantity * OrderDetail.UnitPrice)`.
  - Checkout và hủy đơn thực thi trong transaction `Serializable` kèm khóa cập nhật tồn kho nguyên tử và chống gửi trùng đơn (`Idempotency-Key` / submission guard); hủy đơn hoàn tồn đúng 1 lần.
  - Doanh thu báo cáo (`ReportingCalendar`, múi giờ nghiệp vụ UTC+7) chỉ tính đơn `Completed`.

## 6. Web Screens (`W01–W11`)

`W01 Home` · `W02 Product List` · `W03 Product Detail` · `W04 Cart` · `W05 Login` · `W06 Register` · `W07 Checkout` · `W08 Order Success` · `W09 Account/Profile` · `W10 My Orders` · `W11 Order Detail`.

## 7. Admin Screens (`A01–A10`)

`A01 Login` · `A02 Dashboard` · `A03 Product Management` · `A04 Product Add/Edit` · `A05 Brand Management` · `A06 Category Management` · `A07 Customer Management` · `A08 Order Management` · `A09 Order Detail` · `A10 Reports`.

## 8. UI/UX Direction

- Phong cách cửa hàng điện thoại hiện đại, sáng sủa (nền trắng/sáng, điểm nhấn xanh dương, phân cấp chữ rõ ràng, responsive desktop/tablet/mobile cho Web; sidebar + DataGridView + form nhất quán có hỗ trợ DPI cho WinForms).
- Mọi ảnh mẫu tham khảo UI chỉ dùng cho bố cục và ngôn ngữ thiết kế; không sao chép sản phẩm/giá/đơn hàng/thống kê giả; toàn bộ dữ liệu hiển thị phải đến từ database.
- Khi thực hiện tác vụ UI: kiểm tra các skill UI/UX sẵn có trước, không tự thiết kế lại màn hình ngoài phạm vi yêu cầu.

## 9. Coding Rules

1. Đọc [`AGENTS.md`](AGENTS.md) và [`docs/PROGRESS.md`](docs/PROGRESS.md) trước khi lập kế hoạch; đọc `CONTEXT.md` khi cần kiểm tra luật nghiệp vụ hoặc kiến trúc nền.
2. Chỉ đọc và sửa các file liên quan trực tiếp đến tác vụ hiện tại.
3. Bảo toàn code đang chạy ổn định, tái sử dụng tầng `Business`/`Data` sẵn có.
4. Không thêm tính năng, bảng DB, package hay dữ liệu giả ngoài yêu cầu.
5. Không đưa thông tin đăng nhập, mật khẩu, token hoặc connection string chứa secret vào Git.
6. Build và chạy test phù hợp sau khi sửa code; khắc phục lỗi phát sinh do thay đổi của mình rồi dừng đúng phạm vi.

## 10. Current Progress

- **Nguồn tiến độ chính**: [`docs/PROGRESS.md`](docs/PROGRESS.md) (luôn ưu tiên `docs/PROGRESS.md`, mã nguồn và bằng chứng kiểm thử thực tế thay vì ghi chú cũ).
- **Bản lưu lịch sử các mốc cũ (`S1–S35`)**: [`docs/archive/PROGRESS_HISTORY.md`](docs/archive/PROGRESS_HISTORY.md) và các báo cáo chi tiết trong [`docs/archive/`](docs/archive/) (gồm mốc kiểm thử ban đầu [`docs/archive/s17/S17_TESTING.md`](docs/archive/s17/S17_TESTING.md), [`S17_1`](docs/archive/s17/S17_1_CUSTOMER_WEB_TEST.md), [`S17_2`](docs/archive/s17/S17_2_ADMIN_WINFORMS_TEST.md), [`S17_3`](docs/archive/s17/S17_3_FULL_INTEGRATION_TEST.md)).
