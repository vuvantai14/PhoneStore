# 02 — Danh mục chức năng

## Customer Web

| ID | Chức năng | Trạng thái | Bằng chứng |
|---|---|---|---|
| CW01 | Đăng ký Customer | VERIFIED-AUTO | Account/Auth tests |
| CW02 | Đăng nhập | VERIFIED-AUTO | active/role validation |
| CW03 | Đăng xuất, xóa cart/token | VERIFIED-AUTO | logout isolation test |
| CW04 | Chặn anonymous/sai role | VERIFIED-AUTO | policy/HTTP tests |
| CW05 | Xem hồ sơ của mình | VERIFIED-AUTO | profile tests |
| CW06 | Sửa tên/điện thoại/địa chỉ | VERIFIED-AUTO | ownership/validation/CSRF |
| CW07 | Home, brand/category navigation | VERIFIED-AUTO | presentation tests |
| CW08 | Danh sách/paging sản phẩm | VERIFIED-SOURCE | catalog reader |
| CW09 | Tìm/lọc/sắp xếp catalog | VERIFIED-SOURCE | CatalogQuery |
| CW10 | Chi tiết/ảnh/giá/tồn | VERIFIED-SOURCE | detail query |
| CW11 | Thêm giỏ | VERIFIED-AUTO | cart tests |
| CW12 | Cập nhật số lượng | VERIFIED-AUTO | quantity/server-price tests |
| CW13 | Xóa/empty cart | VERIFIED-AUTO | session tests |
| CW14 | Cách ly cart theo session/logout | VERIFIED-AUTO | regression test |
| CW15 | Checkout/shipping validation | VERIFIED-AUTO | checkout tests |
| CW16 | Tạo order/detail, giá server | VERIFIED-AUTO | integration tests |
| CW17 | Trừ tồn atomic/rollback | VERIFIED-AUTO | concurrency tests |
| CW18 | Idempotency/double submit | VERIFIED-AUTO | replay/token tests |
| CW19 | History/paging/detail ownership | VERIFIED-AUTO | order tests |
| CW20 | Cancel Pending/Confirmed, restock once | VERIFIED-AUTO | commerce tests |

## Admin WinForms

| ID | Chức năng | Trạng thái | Bằng chứng |
|---|---|---|---|
| AD01 | Login Admin active/role | VERIFIED-AUTO | auth tests |
| AD02 | Logout/login lại | VERIFIED-HISTORY | D.3 WinForms acceptance |
| AD03 | Fresh authorization trước ghi | VERIFIED-AUTO | gateway/revoke tests |
| AD04 | Dashboard 4 KPI | VERIFIED-SOURCE | reporting store/UI |
| AD05 | Donut trạng thái | VERIFIED-SOURCE | dashboard model |
| AD06 | Revenue tuần/tháng/năm/custom | VERIFIED-AUTO | 13 reporting tests |
| AD07 | Recent/best seller/low stock | VERIFIED-SOURCE | business sorting |
| AD08 | Product list/filter ID ASC | VERIFIED-AUTO | ordering test |
| AD09 | Product add/edit/validation | VERIFIED-AUTO | CRUD tests |
| AD10 | Stale-stock protection | VERIFIED-AUTO | concurrency tests |
| AD11 | Product soft-state | VERIFIED-AUTO | service tests |
| AD12 | JPEG/PNG upload an toàn | VERIFIED-AUTO | image safety tests |
| AD13 | Primary/unlink ảnh | VERIFIED-AUTO | service + history |
| AD14 | Replace file ảnh trực tiếp | NOT IMPLEMENTED | UI add/unlink/primary |
| AD15 | Brand CRUD/soft-state | VERIFIED-AUTO | CRUD/ordering tests |
| AD16 | Category CRUD/soft-state | VERIFIED-AUTO | CRUD/ordering tests |
| AD17 | Customer list/filter/detail | VERIFIED-AUTO | management tests |
| AD18 | Customer edit/active toggle | VERIFIED-AUTO | service tests |
| AD19 | Order list/filter/detail | VERIFIED-AUTO | workflow/ordering tests |
| AD20 | Pending→Confirmed→Shipping→Completed | VERIFIED-AUTO | workflow tests |
| AD21 | Admin cancel + restock | VERIFIED-AUTO | workflow tests |
| AD22 | Chặn transition sai | VERIFIED-AUTO | transition tests |
| AD23 | Reports KPI/status/top/stock | VERIFIED-AUTO | reporting tests |
| AD24 | Reports chart/filter độc lập | VERIFIED-AUTO | series tests |
| AD25 | Filter bar/DPI S30.3A–D | PARTIAL | build/test; thiếu UI acceptance |

Tổng: **45** chức năng; **35** có automated test trực tiếp, **8** có source/history nhưng chưa tái nghiệm thu, **1 PARTIAL**, **1 NOT IMPLEMENTED**. Không gắn PASS runtime mới vì S30.4A cấm chạy ứng dụng.

