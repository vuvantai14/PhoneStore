# S29.9G.3 — Product Card Home, Mẫu 1

Ngày kiểm tra: 2026-11-09. Kết luận: **PASS source / PASS browser preview**.

## Phạm vi và thiết kế

- Tạo partial card riêng cho Home; shared card của Catalog/Product Detail không đổi.
- Mẫu 1 giữ design system xanh–trắng: card nền trắng, border xanh nhạt, bo góc mềm và shadow nhẹ.
- Ảnh căn giữa trên nền xanh nhạt, `object-fit: contain`, hover scale nhẹ và bị vô hiệu khi `prefers-reduced-motion`.
- Hiển thị tên thương hiệu đầy đủ; không thêm logo giả.
- Tên sản phẩm được wrap tự nhiên, không line-clamp/cắt bất hợp lý.
- RAM và bộ nhớ là hai chip nhỏ lấy từ dữ liệu thật; không thêm nhãn Mới/Yêu thích/khuyến mãi.
- Giá xanh đậm, tồn kho xanh lá có dấu chấm và chữ; CTA chính xanh dương, Chi tiết dạng viền.
- Card dùng chiều cao đồng đều, giá/tồn kho/nút được căn theo cùng nhịp dọc.

## Kiểm thử

- Build Web: PASS, 0 warnings / 0 errors.
- Targeted `HomePresentationTests`: 2/2 PASS; xác minh partial Home, chip RAM/bộ nhớ và không có nhãn giả.
- Preview: `http://localhost:5198/`, PID 15208; HTTP 200, 12 card và 24 chip cấu hình.

| Viewport | Grid | Card height | Kết quả |
|---:|---:|---:|---|
| 375px | 2 cột | 452.6px, đồng đều | PASS |
| 768px | 3 cột | 434.6px, đồng đều | PASS |
| 1024px | 4 cột | 444.6px, đồng đều | PASS |
| 1440px | 6 cột × 2 hàng | 424.6px, đồng đều | PASS |

- Không horizontal overflow, text clipping, ảnh vỡ hoặc CTA overlap.
- Nút Thêm vào giỏ 44px, Chi tiết 45px; focus outline PASS.
- Skip-link và keyboard focus PASS; reduced-motion tắt transform card/ảnh PASS.
- Ảnh có `object-fit: contain`; screenshot cập nhật trong `docs/artifacts/S29.9G/`.

## UI UX Pro Max

Đã đọc và áp dụng `.agents/skills/ui-ux-pro-max/SKILL.md`: visual hierarchy, spacing 4/8px, semantic color hiện có, responsive 375/768/1024/1440, target tối thiểu 44px, focus rõ, không chỉ dùng màu để biểu đạt tồn kho và reduced-motion. Không tạo design system/phong cách mới.

## An toàn và Git

- Không sửa controller, service, database, số lượng 12 sản phẩm, cart/order hoặc màn hình khác.
- Không POST, SQL DML, migration, seed, stage, commit hoặc push.
- Web chính 5015 không restart trong G.3; preview riêng dùng để người dùng duyệt.
- HEAD `0a94f9872e6b33f6cb04cc7e982a459fc37aa78d`; bảo toàn thay đổi ngoài phạm vi `PhoneStore.sln`.

**Dừng tại Home để người dùng trực tiếp xem và duyệt card.**
