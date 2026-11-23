# S29.9G.1 — Compact Home & 12 Latest Products

Ngày kiểm tra: 2026-11-02. Kết luận: **PASS source / PASS browser preview**.

## Thay đổi

- Home yêu cầu tối đa 12 sản phẩm bằng `GetDiversifiedNewestAsync(12)`; service, tiêu chí chọn và thứ tự không đổi.
- Hero desktop giảm từ 360px xuống 300px (16,7%), ảnh và typography được thu gọn tương ứng.
- Grid sản phẩm Home: 6 cột ở 1440px, 4 cột ở 1024px, 3 cột ở 768px và 2 cột ở 375px.
- Thu gọn ảnh, padding, khoảng cách section/card và card thương hiệu; tên thương hiệu đầy đủ vẫn được hiển thị.
- Section bán chạy tiếp tục không render khi nguồn dữ liệu rỗng; không tạo dữ liệu giả.
- Không thay đổi Catalog/Product Detail, nghiệp vụ, database hoặc dữ liệu.

## Kiểm tra thực tế

- Build Web: PASS, 0 warnings / 0 errors.
- `HomePresentationTests`: 2/2 PASS; test xác nhận Home yêu cầu đúng 12 sản phẩm và điều kiện bán chạy rỗng.
- Edge/CDP preview `http://localhost:5197/`: 12 card ở cả bốn viewport.

| Viewport | Cột | Hàng | Hero | Overflow / text clipping |
|---:|---:|---:|---:|---|
| 375px | 2 | 6 | 426.2px, bố cục dọc | Không |
| 768px | 3 | 4 | 435.6px, bố cục dọc | Không |
| 1024px | 4 | 3 | 300px | Không |
| 1440px | 6 | 2 | 300px | Không |

- 8 tên thương hiệu đầy đủ; 4 dịch vụ; không ảnh vỡ; section bán chạy rỗng không xuất hiện.
- Card hàng đầu căn giá/nút trong sai lệch tối đa 1px; focus keyboard, skip-link và reduced-motion PASS.
- Screenshot responsive được lưu trong `docs/artifacts/S29.9G/`.

## Runtime và Git

- Preview riêng PID 17352 dùng build S29.9G.1 trên cổng 5197; Web chính 5015 không restart/deploy.
- Không POST, SQL DML, migration, seed, stage, commit hoặc push.
- HEAD `0a94f9872e6b33f6cb04cc7e982a459fc37aa78d`; toàn bộ thay đổi G/G.1 còn unstaged.

**Dừng ở Home để người dùng duyệt trực tiếp; không chuyển sang màn hình kế tiếp.**
