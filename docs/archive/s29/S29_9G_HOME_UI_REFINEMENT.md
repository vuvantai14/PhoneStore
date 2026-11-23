# S29.9G — Home UI refinement

Ngày kiểm tra: 2026-11-02. **PASS source/build/tests/browser preview; chờ người dùng duyệt thiết kế Home.**

## Thay đổi

- Giữ phong cách xanh–trắng, thông điệp, ảnh hero và CTA `/products`.
- Hero desktop: **480 → 360px (giảm25%)**, tiêu đề tối đa46px, hai cột cân đối hơn; mobile cân bằng dòng chữ, không còn dòng cuối chỉ một từ.
- Brand cards dùng đủ tên từ `brand.Name`, giữ nguyên `brandId` và link lọc. Tám đường dẫn logo trong mapping hiện tại không có file: hiển thị tên, không dùng chữ cái hoặc tạo logo. Chỉ render logo được mapping khi file tồn tại; ảnh lỗi được ẩn, tên vẫn còn.
- Grid Home: **4 cột tại1440, 3 tại1024, 2 tại768/375**. Vẫn đúng6 sản phẩm hiện có và thứ tự từ controller; không sửa Catalog/shared ProductCard.
- Vùng ảnh desktop **230 → 280px**, mobile160 →190px. Tăng cỡ chữ brand/tồn kho, giữ đầy đủ tên/RAM/dung lượng/giá/tồn; giá và action trong cùng hàng căn đều.
- Chỉ render toàn bộ section bán chạy khi `Model.BestSellers.Count > 0`; không đổi query/thuật toán và không tạo doanh số.
- Giữ bốn dịch vụ và footer, chỉ giảm spacing Home. **Không sửa header dùng chung**: header F hiện có đã đạt kiểm tra chồng lấn ở viewport yêu cầu.

## File sửa

- `src/PhoneStore.Web/Views/Home/Index.cshtml`
- `src/PhoneStore.Web/wwwroot/css/site.css`: thêm selector Home, không đổi grid Catalog.
- `tests/PhoneStore.Tests/HomePresentationTests.cs`
- Báo cáo này, `docs/PROGRESS.md`, screenshots tại [artifacts/S29.9G](../../artifacts/S29.9G/).

## Kiểm chứng

- Build Web `--no-restore -p:OutputPath=bin/S29.9G/` và build cuối `bin/S29.9G-final/`: **0 warnings/errors**.
- `dotnet test tests/PhoneStore.Tests/PhoneStore.Tests.csproj --no-restore -p:OutputPath=bin/S29.9G-tests/ --filter FullyQualifiedName~HomePresentationTests --verbosity minimal`: **2/2 PASS**.
- Hai tests chạy Program/MVC/Razor thật với catalog fixture trong bộ nhớ, SQL provider được thay bằng SQLite memory: kiểm tra tên đầy đủ/link thương hiệu và cả nhánh bán chạy rỗng/có dữ liệu. Không mở SQL main. Lần chạy đầu fixture DI quá chặt gây500 khi auth middleware khởi tạo; đã sửa cấu hình fixture, kết quả cuối2/2PASS. Không lặp full regression.
- Edge/CDP preview tại375/768/1024/1440: grid đúng, sáu card, tám tên thương hiệu đúng dữ liệu, bốn dịch vụ; không tràn ngang, không ảnh lỗi đang hiển thị, không cắt text card, vị trí giá/action cùng hàng lệch≤1px.
- Header search/account không chồng nhau ở cả4viewport. Tab/Shift+Tab có outline; Enter ở skip link tới main, không điều hướng sang Products hoặc submit cart.
- Reduced-motion: matches=true, brand hover transform=none, transition0.00001s. Giữ tokens màu đã nghiệm thu: primary/white6.12:1, muted/white5.64:1; không tuyên bố đây là chứng nhận WCAG toàn website.
- Bằng chứng trước/sau: 14 screenshots; hero desktop trước480px/sau360px, product cards và footer sau sửa.

## Runtime, dữ liệu và Git

- **Bản để duyệt: http://localhost:5196/**, preview PID22776, binary `src/PhoneStore.Web/bin/S29.9G/PhoneStore.Web.exe`, content root Web. Dùng GET đọc dữ liệu catalog hiện có; browser cuối ở Home1440.
- Không restart hoặc thay binary Web chính5015. CSS trong source wwwroot được cả hai runtime đọc trực tiếp; **Razor Home mới chỉ có ở preview5196**, chưa coi5015 là bản G hoàn chỉnh.
- Không POST/cart/session edit, tạo/hủy đơn, SQL DML, migration/seed, thay ảnh hoặc dữ liệu. Test có dữ liệu bán chạy chỉ là fixture cách ly để kiểm tra điều kiện Razor.
- Không sửa controller/service/repository, Admin, header/layout hoặc footer markup. Không thêm dependency/framework. Skill ui-ux-pro-max áp dụng theo hướng dẫn đã đọc, giữ design system hiện có.
- HEAD `0a94f9872e6b33f6cb04cc7e982a459fc37aa78d` — `Customer Web Acceptance`; working tree sạch lúc bắt đầu G. Các thay đổi G hiện unstaged/uncommitted; không stage/commit/push.

**Dừng ở Home để người dùng duyệt. Không tự chuyển Products hoặc Admin.** Nếu chốt bản preview, việc cập nhật Razor lên5015 cần được người dùng chỉ định tiếp.
