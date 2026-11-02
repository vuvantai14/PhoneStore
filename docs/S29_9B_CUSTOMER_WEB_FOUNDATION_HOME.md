# S29.9B — Customer Web UI/UX Foundation & Home

Ngày kiểm tra: 2026-11-02  
Phạm vi: ASP.NET Core MVC/Razor Customer Web; foundation CSS và Home.  
Kết luận: **PARTIAL — source/build/tests/HTTP PASS; browser responsive/accessibility thực tế chưa nghiệm thu**.

## Phạm vi và nguyên tắc

- Đã đọc `AGENTS.md`, `docs/PROGRESS.md`, audit S29.9A và skill `ui-ux-pro-max`.
- Skill được nhận diện trong phiên. Python launcher `py` không có Python cài đặt nên không chạy lại `search.py`; triển khai dựa trên audit S29.9A và checklist của skill.
- Giữ nguyên MVC/Razor, Razor markup, controller, service, repository, route và dữ liệu hiển thị.
- Không sửa Admin WinForms, database, runtime configuration hay uploads; không deploy/restart.

## Thay đổi

File source duy nhất: `src/PhoneStore.Web/wwwroot/css/site.css`.

- Hợp nhất foundation tokens: màu semantic, surface/border, typography nền, spacing, radius, shadow và kích thước container.
- Giữ nhận diện xanh navy/blue hiện có nhưng tăng độ tương phản và tính nhất quán.
- Tăng body text từ 15px lên 16px; cải thiện cỡ chữ Home mobile, service copy, product title/price/actions và navigation mobile.
- Chuẩn hóa touch target quan trọng về tối thiểu 44px cho CTA, link chi tiết và điều hướng trong phạm vi Home/foundation.
- Nâng card spacing/shadow/focus-within; giữ nguyên grid, dữ liệu và hành vi sản phẩm/brand/category hiện có.
- Disabled product action đổi từ contrast 2.56:1 sang 4.63:1 (`#5f697a` trên `#e6ebf2`). Các cặp primary/text/error được kiểm tra lần lượt 6.12:1, 5.64:1 và 6.53:1.
- `prefers-reduced-motion: reduce` nay vô hiệu animation/transition toàn cục trong Web và loại bỏ transform trang Home.
- Giữ focus-visible 3px, skip link, semantic navigation và responsive breakpoints hiện có.

## Kiểm tra thực tế

1. Lần build trực tiếp vào `bin/Debug` thất bại do Web đang chạy khóa DLL; không dừng tiến trình và không thay runtime.
2. Build cách ly:
   - `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.9B/`
   - PASS, 0 warnings, 0 errors.
3. Regression:
   - `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.9B/ --verbosity minimal`
   - PASS 162/162, fail 0, skip 0.
4. CSS static checks: 592 dấu `{` và 592 dấu `}`; `git diff --check` không có lỗi whitespace.
5. HTTP read-only trên runtime đang chạy: `/`, `/products`, `/account/login`, `/css/site.css` đều HTTP 200.

## Chưa nghiệm thu

- Chưa có browser automation/kiểm tra trực quan thật tại 375px, 768px, 1024px và 1440px; không tuyên bố browser PASS.
- Chưa kiểm tra keyboard tab order, screen reader, hover/focus và reduced-motion bằng browser thật.
- Các trang Products/Detail/Cart/Checkout/Auth/Profile/Orders chỉ được smoke qua ảnh hưởng foundation; tối ưu riêng thuộc các giai đoạn S29.9C trở đi.
- Source thay đổi chưa deploy; runtime không được restart theo giới hạn nhiệm vụ.

## Sẵn sàng bước tiếp theo

**READY có điều kiện cho S29.9C** sau khi nghiệm thu trực quan S29.9B trên browser thật. Nếu có sai lệch layout, chỉ chỉnh foundation/Home trước khi mở rộng sang các trang commerce.
