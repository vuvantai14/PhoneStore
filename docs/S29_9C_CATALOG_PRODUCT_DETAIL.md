# S29.9C — Catalog & Product Detail

Ngày kiểm tra: 2026-11-02. Kết luận: **PASS**.

## Thay đổi

- Nâng cấp catalog, bộ lọc, search/sort toolbar và product cards theo design tokens S29.9B.
- Nâng cấp gallery, thông tin giá/tồn kho/thông số và CTA trên trang chi tiết.
- Bổ sung responsive 375/768/1024/1440, focus state và reduced-motion; không đổi Razor, route hay nghiệp vụ.
- Sửa hai lỗi tablet phát hiện trong nghiệm thu: action card bị chật và CTA chi tiết xuống dòng quá nhiều.

## Kiểm chứng

- Edge thật qua CDP: catalog có 40 cards, 0 ảnh vỡ; detail ProductId 25 hiển thị đúng và có 0 ảnh vỡ ở cả bốn breakpoint.
- Search `OnePlus`: 3 kết quả; search không tồn tại: empty state; BrandId 1: 6; CategoryId 1: 40; sort giá: 40; detail `/products/25`: HTTP 200.
- Không có POST, checkout hoặc thay đổi database.
- Ảnh chụp: `docs/artifacts/S29.9C/`.

- `dotnet build PhoneStore.sln --no-restore -p:OutputPath=bin/S29.9C/`: 0 warnings, 0 errors.
- `dotnet test PhoneStore.sln --no-build --no-restore -p:OutputPath=bin/S29.9C/`: 162/162 PASS.
