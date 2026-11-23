# S29.9B.1 — Customer Web Browser UI/UX Acceptance

Ngày kiểm tra: 2026-11-02  
Website: `http://localhost:5015`  
Kết luận: **PARTIAL — browser thật đã phát hiện lỗi và source đã sửa; runtime dừng trước khi tái nghiệm thu**.

## Runtime và công cụ

- Microsoft Edge headless thật được dùng để render Home, không suy kết quả từ HTTP/source.
- Tại checkpoint đầu, PID 12640 lắng nghe `127.0.0.1/[::1]:5015` và HTTP CSS 200.
- SHA-256 CSS qua HTTP khớp file source tại thời điểm chụp: `478B4DD60D32C5886A82DCE8F45EA976FE55A2947448E36FBFB29945FD58B3E2` — **CURRENT tại checkpoint đầu**.
- Sau lần chụp đầu, PID 12640 tự thoát và cổng 5015 không còn listener. Không có tín hiệu shutdown/restart nào được gửi bởi nhiệm vụ này.
- Vì source đã sửa sau đó nhưng runtime không còn chạy, trạng thái CSS cuối cùng là **UNKNOWN**; không deploy/restart ngoài phạm vi phê duyệt.

## Bằng chứng browser trước sửa

Ảnh lưu tại `docs/artifacts/S29.9B.1/`:

- `home-375.png`
- `home-768.png`
- `home-1024.png`
- `home-1440.png`

Kết quả quan sát:

| Breakpoint | Kết quả | Bằng chứng |
|---|---|---|
| 375px | FAIL ban đầu | Hero bị tràn/cắt chữ sang phải; phần navigation dùng vùng cuộn ngang. |
| 768px | PARTIAL | Header/hero/CTA/brand grid không chồng chữ; logo brand hiện ảnh lỗi cùng fallback. |
| 1024px | PARTIAL | Header/hero/CTA/grid cân đối; logo brand lỗi cùng fallback. |
| 1440px | PARTIAL | Hero và product grid hiển thị cân đối; logo brand lỗi cùng fallback. |

Ảnh viewport xác minh header, hero, CTA và phần đầu nội dung. Footer/toàn bộ trang chưa có full-page screenshot nên không đánh dấu browser PASS.

## Lỗi và sửa phạm vi S29.9B

### P1 — Hero mobile bị tràn/cắt chữ

- Nguyên nhân: grid child thiếu `min-width:0`, heading 34px quá lớn ở 375px và chuỗi tiêu đề không có chiến lược wrap an toàn.
- Sửa trong `site.css`: thêm `min-width:0`, `overflow-wrap:anywhere`, giảm heading 375px xuống 30px và tinh chỉnh padding.
- Trạng thái: **FIXED IN SOURCE / chưa tái nghiệm thu browser** do runtime dừng.

### P2 — Logo thương hiệu hiển thị ảnh lỗi chồng fallback

- Nguyên nhân: `.brand-logo { display:block }` ghi đè hành vi của thuộc tính `hidden` sau `onerror`.
- Sửa trong `site.css`: thêm `.brand-logo[hidden]{display:none}`.
- Trạng thái: **FIXED IN SOURCE / chưa tái nghiệm thu browser**.

Không sửa Razor, JavaScript, controller, service, route, dữ liệu hay nghiệp vụ.

## Accessibility và regression

- Contrast token được đo: primary/white 6.12:1, muted/white 5.64:1, error/surface 6.53:1, disabled/surface 4.63:1.
- Focus-visible và reduced-motion có trong source, nhưng Tab/Shift+Tab/Enter và `prefers-reduced-motion` chưa được thao tác bằng browser sau sửa: **BLOCKED**.
- Navigation/CTA và các route Products/Detail/Login/Register/Cart/Checkout/Orders/Profile chưa được smoke đầy đủ trong lượt này vì runtime dừng: **PARTIAL/BLOCKED**. Không có form ghi dữ liệu nào được gửi.
- Build cách ly sau sửa: `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.9B.1/` — PASS, 0 warning, 0 error.
- Regression sau sửa: `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.9B.1/ --verbosity minimal` — PASS 162/162, fail 0, skip 0.
- CSS braces cân bằng và `git diff --check` không phát hiện lỗi nội dung.

## Phân loại cuối

- Runtime CSS: **UNKNOWN cuối lượt** (CURRENT trước sửa; runtime offline sau sửa).
- Browser responsive: **PARTIAL**.
- Keyboard/accessibility runtime: **BLOCKED**.
- Regression automated: **PASS**.
- S29.9B: **PARTIAL**.
- S29.9C: **NOT READY** cho đến khi khởi động Web theo phê duyệt và tái chụp bốn breakpoint, full-page/footer, keyboard/focus và reduced-motion.

Không deploy/restart, không DML database, không thay Admin, không commit/push.
