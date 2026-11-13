# S31.3C — Catalog REST API Implementation

Ngày xác minh: 2026-11-13  
Trạng thái: **PASS — GO cho S31.3D**

## A. Phạm vi và an toàn

- Mở rộng project `PhoneStore.Api` của S31.3B; không tạo API project thứ hai.
- Chỉ bổ sung catalog read-only. Không có endpoint ghi, authentication API, CORS hay thay đổi database.
- Tái sử dụng `IProductService` và catalog reader hiện có; không sửa Business, Data, Models, Customer Web hoặc Admin WinForms.
- Không migration, seed, DML/DDL, commit hoặc push.

## B. Files triển khai

### API

- `src/PhoneStore.Api/Controllers/ProductsController.cs`
- `src/PhoneStore.Api/Controllers/BrandsController.cs`
- `src/PhoneStore.Api/Controllers/CategoriesController.cs`
- `src/PhoneStore.Api/Contracts/CatalogOptionResponse.cs`
- `src/PhoneStore.Api/Contracts/Products/ProductQueryRequest.cs`
- `src/PhoneStore.Api/Contracts/Products/ProductSummaryResponse.cs`
- `src/PhoneStore.Api/Contracts/Products/ProductDetailResponse.cs`
- `src/PhoneStore.Api/Contracts/Products/PagedProductsResponse.cs`
- `src/PhoneStore.Api/Mapping/CatalogContractMapper.cs`
- `src/PhoneStore.Api/Infrastructure/ApiExceptionHandler.cs`
- `src/PhoneStore.Api/Program.cs`

### Tests

- `tests/PhoneStore.Tests/CatalogApiTests.cs`

## C. Endpoint contract

| Method | Route | Kết quả |
|---|---|---|
| GET | `/api/health` | Health/readiness từ foundation S31.3B |
| GET | `/api/v1/products` | Danh sách sản phẩm active có phân trang, tìm kiếm, lọc và sắp xếp |
| GET | `/api/v1/products/{id}` | Chi tiết một sản phẩm active; 404 nếu không tồn tại/inactive |
| GET | `/api/v1/brands` | Danh sách thương hiệu active |
| GET | `/api/v1/categories` | Danh sách danh mục active |

Query của products: `search`, `brandId`, `categoryId`, `minPrice`, `maxPrice`, `sort`, `page`, `pageSize`, `inStockOnly`.

- `page` mặc định 1; `pageSize` mặc định 10 và tối đa 50.
- `search` tối đa 100 ký tự.
- `sort`: `newest`, `price-asc`, `price-desc`, `name-asc`.
- Giá không âm; `minPrice <= maxPrice`; ID bộ lọc phải dương.
- Input sai trả 400 `application/problem+json`; product không có trả 404 Problem Details.

## D. DTO và bảo vệ dữ liệu

- Response dùng DTO API độc lập, camelCase; không serialize EF entity.
- Không lộ `IsActive`, `CreatedAt`, password/hash, connection string hay đường dẫn vật lý.
- Image URL chỉ giữ relative URL hợp lệ. Absolute URI, UNC/double slash, backslash và path traversal bị loại bỏ.
- Giá, tồn và filter được lấy/xử lý phía server thông qua business service hiện có.

## E. Dependency và xử lý lỗi

- Controller nhận scoped `IProductService`; service tiếp tục dùng catalog reader/DbContext scoped của S31.3B.
- `DbException` và `DbUpdateException` được ánh xạ thành 503 Problem Details an toàn; lỗi khác thành 500.
- Log lỗi chỉ ghi loại exception và trace identifier; không trả stack trace hay chi tiết hạ tầng cho client.

## F. Build và automated tests

| Kiểm tra | Kết quả |
|---|---|
| `dotnet restore PhoneStore.sln` | PASS |
| `dotnet build PhoneStore.sln --no-restore` | PASS — 0 warnings, 0 errors |
| `CatalogApiTests` | **18/18 PASS** |
| `ApiFoundationTests` | **6/6 PASS** |
| `HomePresentationTests` | **2/2 PASS** |

Catalog tests bao phủ default pagination, filter/sort/page, query validation, detail found/404/invalid ID, active option lists, empty lists, URL ảnh an toàn, lỗi database 503, OpenAPI paths và catalog reader SQLite cách ly loại trừ dữ liệu inactive.

## G. Runtime SQL Server thực

API chạy thử tại `http://localhost:5025` với `PhoneStoreDB` hiện có:

- `/api/health`: 200 `healthy`.
- `/api/v1/products`: 200, `totalItems=40`, mặc định trả 10 item.
- Product detail ID 40: 200, `OnePlus Nord 3`, một relative image URL.
- `/api/v1/brands`: 200, 8 item active.
- `/api/v1/categories`: 200, 1 item active.
- Filter `pageSize=5&sort=price-asc&inStockOnly=true`: 200, 5 item trong tổng 40.
- `pageSize=51`: 400 Problem Details.
- ID không phải số: 400 Problem Details; ID không tồn tại: 404 Problem Details.
- Swagger UI/JSON: 200 trong Development, OpenAPI 3.0.1 và đúng 5 route trên.

Customer Web chạy đồng thời trên 5015: Home và Products đều HTTP 200; API health/products vẫn 200, không xung đột port hoặc database. Admin source không thay đổi; solution build PASS nhưng không mở UI Admin trong bước này.

## H. Snapshot database read-only

Snapshot trước và sau runtime giống nhau:

| Chỉ số | Trước | Sau |
|---|---:|---:|
| Roles | 2 | 2 |
| Users | 3 | 3 |
| Brands | 8 | 8 |
| Categories | 2 | 2 |
| Products | 40 | 40 |
| ProductImages | 41 | 41 |
| Orders | 11 | 11 |
| OrderDetails | 26 | 26 |
| Active products | 40 | 40 |
| Active brands | 8 | 8 |
| Active categories | 1 | 1 |

Các truy vấn API là SELECT; không phát hiện thay đổi dữ liệu. Snapshot hậu kiểm được lấy độc lập bằng Windows Authentication.

## I. Giới hạn và rủi ro còn lại

- API catalog hiện public và chưa có rate limiting/authentication; phù hợp read-only catalog nhưng cần quyết định riêng trước endpoint ghi hoặc dữ liệu riêng tư.
- Image URL là relative contract; consumer phải ghép đúng origin/static host của Customer Web.
- Brand/category ID dương nhưng không tồn tại hiện cho danh sách rỗng 200, không phải 404; đây là hành vi filter thông thường.
- API và Web dùng chung SQL database/account theo kiến trúc hiện tại; chưa có deployment/secret isolation production.
- Hai process thử nghiệm Web/API vẫn còn listener sau Ctrl+C; không force-kill theo quy tắc an toàn. Đây là trạng thái runtime cục bộ cần người dùng đóng mềm nếu không còn dùng.

## J. Kết luận

**S31.3C PASS.** Catalog API đáp ứng contract read-only, validation, Problem Details, Swagger, runtime SQL Server thực và regression mục tiêu. Database không đổi. Trạng thái **GO cho S31.3D**, nhưng chưa tự chuyển giai đoạn.
