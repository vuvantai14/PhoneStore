# S35.4C — Product Management API Completion

- **Execution Date**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore` (`main`)
- **Stage Status**: **`PASS`**

---

## 1. Phạm vi & Kiến trúc triển khai

Hoàn thiện toàn bộ các nhóm endpoint Product Management, Brands/Categories Lookup CRUD, Product Editor Options, Product Images API và Public Catalog Parity trong `PhoneStore.Api` bằng cách tái sử dụng 100% nghiệp vụ `PhoneStore.Business` và `PhoneStore.Data` (không viết lại logic nghiệp vụ và không thay đổi schema database):

1. **Product CRUD (`/api/v1/admin/products` & tương thích ngược `/api/admin/products`)** — `src/PhoneStore.Api/Controllers/AdminProductsController.cs:14-101`:
   - `GET /api/v1/admin/products` (`& /api/admin/products`): phân trang + lọc `search`, `brandId`, `categoryId`, `isActive`.
   - `GET /api/v1/admin/products/{id}` (`& /api/admin/products/{id}`): trả `AdminProductDetail` kèm `OriginalStockQuantity` phục vụ optimistic concurrency.
   - `POST /api/v1/admin/products` (`& /api/admin/products`): tạo sản phẩm qua `IProductManagementService.SaveAsync(null, ...)`, kiểm tra `Price > 0` (tối đa 2 chữ số thập phân), `StockQuantity >= 0`, `BrandId`/`CategoryId` tồn tại và đang `IsActive = true`.
   - `PUT /api/v1/admin/products/{id}` (`& /api/admin/products/{id}`): cập nhật nguyên tử qua `UpdateIfStockMatchesAsync` (`OriginalStockQuantity` lệch trả `409 Conflict`, không tồn tại trả `404 NotFound`).
   - `PATCH /api/v1/admin/products/{id}/status` (`& /api/admin/products/{id}/status`): bật/ngừng hoạt động (`IsActive`).
   - **Đánh giá `DELETE` sản phẩm**: Trong domain `PhoneStore` (`src/PhoneStore.Data/Configurations/OrderDetailConfiguration.cs`), `OrderDetails` tham chiếu khóa ngoại tới `Products` để bảo toàn lịch sử đơn hàng và doanh thu báo cáo. `ProductManagementService` sử dụng cơ chế **soft-deactivation (`IsActive = false`)** thay vì xóa cứng. Vì vậy không mở endpoint hard-`DELETE` để tránh rủi ro mất toàn vẹn lịch sử; mọi thao tác ẩn/ngừng bán sản phẩm thực hiện qua `PATCH /api/v1/admin/products/{id}/status`.

2. **Brands & Categories CRUD** — `src/PhoneStore.Api/Controllers/AdminBrandsController.cs:12-76`, `src/PhoneStore.Api/Controllers/AdminCategoriesController.cs:12-76`, `src/PhoneStore.Business/Management/LookupManagementService.cs:14-73`:
   - `GET /api/v1/admin/brands`, `POST /api/v1/admin/brands`, `PUT /api/v1/admin/brands/{id}`, `PATCH /api/v1/admin/brands/{id}/status` (kèm route tương thích `/api/admin/brands...`).
   - `GET /api/v1/admin/categories`, `POST /api/v1/admin/categories`, `PUT /api/v1/admin/categories/{id}`, `PATCH /api/v1/admin/categories/{id}/status` (kèm route tương thích `/api/admin/categories...`).
   - Tái sử dụng trực tiếp `ILookupManagementService` (`LookupManagementService`): kiểm tra trùng tên không phân biệt hoa/thường (`409 Conflict`), validate độ dài tên/mô tả (`400 BadRequest`), kiểm tra tồn tại (`404 NotFound`), bảo toàn sản phẩm hiện hữu khi ngừng hoạt động Brand/Category.

3. **Product Options (`GET /api/v1/admin/products/options`)** — `src/PhoneStore.Api/Controllers/AdminProductsController.cs:41-53`:
   - Hỗ trợ `?productId={id}` (trả các Brand/Category đang hoạt động cộng với Brand/Category hiện tại của sản phẩm đang sửa kể cả khi đã ngừng hoạt động) và `?includeInactive=true` (phục vụ bộ lọc danh sách của Admin WinForms).

4. **Product Images API** — `src/PhoneStore.Api/Controllers/AdminProductsController.cs:103-216`, `src/PhoneStore.Business/Management/CrossPlatformProductImageContentValidator.cs:6-277`, `src/PhoneStore.Business/Management/LocalProductImageStorage.cs:1-145`:
   - `GET /api/v1/admin/products/{id}/images`
   - `POST /api/v1/admin/products/{id}/images` (`multipart/form-data`, giới hạn file `10 MiB`)
   - `DELETE /api/v1/admin/products/{id}/images/{imageId}`
   - `PATCH /api/v1/admin/products/{id}/images/{imageId}/primary`
   - `PATCH /api/v1/admin/products/{id}/images/{imageId}/order`
   - **Bảo mật & Tương thích Linux Docker**:
     - `CrossPlatformProductImageContentValidator` giải mã và kiểm tra cấu trúc thực tế hoàn toàn bằng managed code (tương thích cả Linux Docker và Windows): kiểm tra magic bytes, trailer `IEND`/`EOI`, CRC32 từng chunk PNG, chặn APNG (`acTL`/`fcTL`/`fdAT`), giải nén toàn bộ luồng `IDAT` bằng `ZLibStream` và xác minh từng byte filter (`0..4`) của mọi scanline; kiểm tra bảng `DQT`, `SOF0`–`SOF2`, `SOS` và dữ liệu entropy của JPEG; giới hạn kích thước `<= 4096×4096` và `<= 16,000,000` điểm ảnh.
     - Chặn `.exe`, `.webp`, path traversal (`..`), file rỗng hoặc vượt `10 MiB`; lưu tên file ngẫu nhiên `<32-hex-guid>.<ext>` qua `.tmp` + atomic move; tự động xóa file tạm upload trong khối `finally`.
     - Chỉ thao tác trên ảnh thuộc đúng `productId` (`404 NotFound` nếu `imageId` thuộc sản phẩm khác), luôn chuẩn hóa duy nhất 1 ảnh chính (`NormalizePrimary`).
     - **Đánh giá mount volume cho image uploads**: Trong Docker business hiện tại (`phonestore-restore-c2`), `web` mount `/app/wwwroot/uploads/products:ro`. Để Admin upload ảnh qua `PhoneStore.Api` trên Docker trong giai đoạn vận hành sau này, cần chia sẻ bind mount/volume `/app/wwwroot/uploads/products` (`rw` cho `api`, `ro` cho `web`). Theo đúng quy tắc an toàn Mục 7, **không** thay đổi mounts của Docker business trong giai đoạn này; kiểm thử upload chỉ chạy trên thư mục fixture cách ly.

5. **Public Catalog Parity (`/api/v1/products`)** — `src/PhoneStore.Api/Contracts/Products/ProductQueryRequest.cs:14-95`, `src/PhoneStore.Api/Controllers/ProductsController.cs:19-118`:
   - Bổ sung `brandIds[]`, `rams[]`, `storages[]` vào `GET /api/v1/products` (ánh xạ trực tiếp vào `CatalogQuery` và `ProductCatalogReader.ApplyFilters` kèm mở rộng biến thể dung lượng `8 GB`/`8GB`).
   - Bổ sung `GET /api/v1/products/{id}/related`, `GET /api/v1/products/bestsellers`, `GET /api/v1/products/newest` (tái sử dụng `IProductService.GetRelatedAsync`, `GetBestSellersAsync`, `GetNewestAsync`/`GetDiversifiedNewestAsync` đang phục vụ Customer Web).

---

## 2. Kết quả kiểm thử S35.4C

- `dotnet build PhoneStore.sln -c Release --no-restore`: **PASS (`0 Warning(s)`, `0 Error(s)`)**.
- Targeted test suite (`AdminProductApiTests`, `CatalogApiTests`, `AdminCrudTests`, `ImageStorageSafetyTests`, `ProductStockConcurrencyTests`): **PASS (`76/76`)**.
