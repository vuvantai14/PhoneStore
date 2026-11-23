# S29.9G.2 — Hoàn thiện Trang chủ PhoneStore

Ngày kiểm tra: 2026-11-09. Kết luận: **PASS runtime chính**.

## Nguyên nhân

- Source đã được sửa ở G.1 để gọi `GetDiversifiedNewestAsync(12)`, nhưng Web chính cổng 5015 vẫn chạy binary Debug cũ nên HTML chỉ có 6 card.
- `ProductService` truyền nguyên tham số xuống `ProductCatalogReader`; ViewModel và Razor không cắt danh sách.
- Thuật toán hiện có vẫn lấy ứng viên active theo `CreatedAt DESC, ProductId DESC`, sau đó phân bổ đa dạng theo thương hiệu. G.2 không thay đổi truy vấn hoặc thứ tự này.

## Thực hiện

- Giữ thay đổi giới hạn Home từ 6 thành tối đa 12 trong `HomeController`.
- Giữ nguyên toàn bộ giao diện G.1: hero, header, thương hiệu, card, dịch vụ, footer và điều kiện ẩn bán chạy rỗng.
- Build riêng `bin/S29.9G.2`, shutdown mềm runtime cũ bằng Ctrl+C sau khi xác minh đúng PID/console, rồi chạy runtime mới trên 5015 với cấu hình Development hiện hành.
- Không sửa database, Catalog, Product Detail, Admin hoặc nghiệp vụ cart/order.

## Kiểm thử

- Build Web: PASS, 0 warnings / 0 errors.
- Targeted `HomePresentationTests`: 2/2 PASS; test xác nhận Home yêu cầu đúng 12 sản phẩm.
- SQL chỉ đọc: PhoneStoreDB có 40/40 sản phẩm active và 40 ProductImages.
- Runtime chính: PID 16304, `bin/S29.9G.2/PhoneStore.Web.exe`, HTTP 200, 12 card.
- Product IDs hiển thị: `40,37,33,29,24,18,12,6,39,36,32,28`.

| Viewport | Card | Grid | Overflow / clipping |
|---:|---:|---:|---|
| 375px | 12 | 2 cột | Không |
| 768px | 12 | 3 cột | Không |
| 1024px | 12 | 4 cột | Không |
| 1440px | 12 | 6 cột × 2 hàng | Không |

- Không ảnh vỡ; giá và nút trong từng hàng căn đều; section bán chạy rỗng không render.
- Keyboard focus, skip-link và `prefers-reduced-motion` PASS.

## Áp dụng UI UX Pro Max

Đã dùng `.agents/skills/ui-ux-pro-max/SKILL.md`: giữ design tokens hiện có, breakpoint 375/768/1024/1440, không horizontal scroll, phân cấp/spacing nhất quán, target tương tác tối thiểu, focus rõ và reduced-motion. Không tạo design system mới.

## Git và an toàn

- HEAD `0a94f9872e6b33f6cb04cc7e982a459fc37aa78d`; thay đổi G/G.1/G.2 còn unstaged.
- `PhoneStore.sln` có thay đổi ngoài phạm vi từ trước và được bảo toàn, không chỉnh sửa.
- Không SQL DML, migration, seed, stage, commit hoặc push.

**Dừng ở Home để người dùng duyệt và chốt.**
