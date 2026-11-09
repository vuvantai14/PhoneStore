# S29.9G.4 — Typography Be Vietnam Pro

Ngày xác minh: 2026-11-09. Trạng thái: **PASS source/browser preview; chờ người dùng duyệt giao diện**.

## Kết quả

- Đã áp dụng hướng dẫn `ui-ux-pro-max`: typography hierarchy 400/500/600/700, weight thật, fallback an toàn, responsive và kiểm tra tiếng Việt/accessibility.
- Font cũ là `Segoe UI, Arial, sans-serif`; nhiều rule dùng weight không tồn tại như 650/720/750/780/800 nên trình duyệt phải tổng hợp hoặc chọn weight gần nhất, làm chữ và chữ số thiếu nhất quán giữa máy.
- Customer Web nay dùng token `--font-sans` tập trung với `Be Vietnam Pro`, fallback `Segoe UI`, `Arial`, `sans-serif`; `font-synthesis:none` ngăn tạo kiểu đậm giả.
- Font được lưu cục bộ từ bộ Google Fonts Be Vietnam Pro, kèm giấy phép OFL. Bốn face thật được dùng: Regular 400, Medium 500, SemiBold 600 và Bold 700; `font-display:swap` giữ nội dung đọc được trong lúc tải.
- Không thay bố cục, màu, dữ liệu hoặc nghiệp vụ. Home vẫn có 12 card, tương ứng 2/3/4/6 cột tại 375/768/1024/1440px.

## Phân cấp typography

| Thành phần | Weight |
|---|---:|
| Nội dung | 400 |
| Brand, RAM/bộ nhớ, tồn kho, label | 500 |
| Menu, nút, tên sản phẩm | 600 |
| Tiêu đề khu vực, giá bán | 700 |

Giá dùng chữ số tabular; chuỗi thử nghiệm `10.990.000 ₫` hiển thị đầy đủ. Toàn bộ weight CSS không chuẩn trước đó đã được chuẩn hóa về bốn face thật.

## File thay đổi trong S29.9G.4

- `src/PhoneStore.Web/wwwroot/css/site.css`
- `src/PhoneStore.Web/wwwroot/fonts/be-vietnam-pro/BeVietnamPro-Regular.ttf`
- `src/PhoneStore.Web/wwwroot/fonts/be-vietnam-pro/BeVietnamPro-Medium.ttf`
- `src/PhoneStore.Web/wwwroot/fonts/be-vietnam-pro/BeVietnamPro-SemiBold.ttf`
- `src/PhoneStore.Web/wwwroot/fonts/be-vietnam-pro/BeVietnamPro-Bold.ttf`
- `src/PhoneStore.Web/wwwroot/fonts/be-vietnam-pro/OFL.txt`
- `docs/artifacts/S29.9G/typography-g3-before-375.png`
- `docs/artifacts/S29.9G/typography-g3-before-1440.png`
- `docs/artifacts/S29.9G/typography-after-375.png`
- `docs/artifacts/S29.9G/typography-after-1440.png`

## Xác minh

- Build: `dotnet build src/PhoneStore.Web/PhoneStore.Web.csproj --no-restore -p:OutputPath=bin/S29.9G.4/` — PASS, 0 warning/error.
- Targeted test: `HomePresentationTests` — PASS 2/2.
- Browser preview `http://localhost:5199/`: PASS tại 375, 768, 1024 và 1440px.
- `document.fonts`: đúng 4 face Be Vietnam Pro ở trạng thái `loaded`; Network Resource Timing ghi nhận đủ bốn file TTF cục bộ.
- Computed styles: body 400; brand/spec/stock 500; product/menu/button 600; heading/price 700.
- 12 card đồng chiều cao ở từng breakpoint; không overflow, clipping, ảnh vỡ hoặc layout shift đáng kể.
- Smoke browser Home, Products, Product Detail, Login, Register, Cart tại bốn breakpoint: tất cả body và form controls thực dùng Be Vietnam Pro; không overflow/ảnh vỡ.
- Runtime chính không được restart/deploy; PhoneStoreDB không bị ghi.

## Kết luận

**PASS** trong phạm vi source và browser preview. Giữ preview tại `http://localhost:5199/` để người dùng duyệt; chưa stage/commit/push và chưa chuyển sang giai đoạn khác.

## S29.9G.4.1 — Căn chỉnh giá trên card Home

- Nguyên nhân: container giá dùng `align-items:center` trong khi ký hiệu `₫` nhỏ hơn chữ số nhưng dùng chung line-height 25px, tạo lệch thị giác 2–3px.
- Đo trực tiếp glyph cho thấy Be Vietnam Pro không cung cấp biến thể `tnum` (độ rộng `111111` và `000000` vẫn khác nhau; bật `tnum`/`pnum` không đổi kết quả), vì vậy không dựa vào tính năng không được font hỗ trợ.
- Đã đổi riêng giá Home sang baseline alignment; chuẩn hóa weight 700, line-height 1.25 và tracking nhẹ. Không đổi chuỗi/định dạng tiền.
- Build Customer Web PASS, 0 warning/error. Không chạy test vì chỉ thay CSS trình bày.
- Runtime chính 5015 đã restart an toàn sang binary Debug mới, PID 24776; Home HTTP 200 và đủ 12 card.
- Browser 375/1440: Be Vietnam Pro 700 và `align-items:baseline`; giá dài/ngắn cùng nhịp, dấu phân tách đều, không clipping hoặc horizontal overflow.
