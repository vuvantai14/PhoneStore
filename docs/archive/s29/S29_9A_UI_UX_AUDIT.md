# S29.9A — Audit UI/UX PhoneStore bằng UI UX Pro Max

Ngày kiểm tra: 2026-11-02 (Asia/Bangkok)  
Phạm vi: source-only, không sửa source, không chạy/deploy/restart ứng dụng, không ghi database.

## 1. Kết luận

**READY có điều kiện cho S29.9B (Customer Web trước).** Codex đã nhận diện `ui-ux-pro-max` trong phiên hiện tại và đã đọc `SKILL.md`. Audit dùng checklist/accessibility/design intelligence của skill và source thực tế. `py` hiện trả về `No installed Python found!`, nên chưa tái tạo được design-system bằng search script; không có kết quả cũ để tái sử dụng. Đây là giới hạn công cụ, không chặn việc sửa UI dựa trên audit, nhưng phải ghi rõ là chưa có bằng chứng search CLI.

Không phát hiện yêu cầu đổi MVC/Razor, WinForms, controller, service, repository, API hoặc database. Không đề xuất thêm màn hình, KPI hay chức năng.

## 2. Bằng chứng và phương pháp

- Skill: `.agents/skills/ui-ux-pro-max/SKILL.md`, 739 dòng; Codex liệt kê skill trong catalog phiên hiện tại.
- Web: toàn bộ Razor trong `src/PhoneStore.Web/Views`, `wwwroot/css/site.css`, `wwwroot/js/site.js`.
- Admin: `LoginForm`, `MainForm`, các `Controls`, `Dialogs`, `Styling` liên quan các màn hình được yêu cầu.
- Không kiểm thử browser/WinForms trực quan trong bước này; kết luận màn hình là **source audit**, không phải runtime acceptance.
- Kiểm tra tương phản tĩnh trên các cặp màu chính bằng công thức WCAG relative luminance.

## 3. Design system đề xuất

Định hướng: **Modern trustworthy electronics retail** — sạch, kỹ thuật, dễ so sánh sản phẩm, ưu tiên thông tin và khả năng mua hàng; không dùng hiệu ứng trang trí làm lu mờ giá/tồn/trạng thái.

### Token nền tảng

| Nhóm | Đề xuất |
|---|---|
| Primary | Giữ `#1559D6`; hover `#1046AD`; phù hợp nhận diện hiện có |
| Text | `#14243C`; muted `#637086`; không dùng muted cho chữ nhỏ trên nền màu |
| Surface | White / `#F5F7FB`; border `#E1E7EF` |
| Success | `#246546` trên `#EFF8F3` |
| Danger | `#A52F3A` trên `#FFF8F8` |
| Radius | 8px control, 12–16px card/dialog; hero có thể 20–28px |
| Spacing | Thang 4/8: 4, 8, 12, 16, 24, 32, 40, 48, 64 |
| Typography Web | Segoe UI/system; body tối thiểu 16px, phụ 14px; tránh nội dung 9–12px |
| Typography Admin | Segoe UI 10–11pt; heading 18–24pt; status/help tối thiểu 9.5–10pt |
| Motion | 150–200ms cho hover/focus; chỉ opacity/color/transform; tắt khi reduced-motion |
| Focus | Outline 3px primary + offset; WinForms phải giữ focus cue mặc định/rõ ràng |

Không bắt buộc dùng nguyên palette do skill tạo. Giữ xanh hiện tại giúp giảm rủi ro và duy trì nhận diện giữa Web/Admin.

### Tương phản WCAG tĩnh

| Cặp màu | Ratio | Đánh giá |
|---|---:|---|
| `#1559D6` / white | 6.12:1 | PASS AA normal text |
| `#637086` / white | 5.01:1 | PASS AA normal text |
| `#637086` / `#F4F8FD` | 4.70:1 | PASS AA sát ngưỡng |
| white / `#1559D6` | 6.12:1 | PASS AA |
| `#D4E1F4` / `#061A38` | 13.09:1 | PASS AAA |
| `#94BDFF` / `#061A38` | 9.06:1 | PASS AAA |
| `#A52F3A` / white | 6.85:1 | PASS AA |
| `#8994A6` / `#E6EBF2` (disabled) | 2.56:1 | **FAIL** nếu cần đọc; chỉ chấp nhận khi control disabled có ngữ nghĩa rõ và nội dung không thiết yếu |

Cần kiểm tra lại bằng browser/Windows rendering thực tế vì font smoothing, gradient và overlay có thể thay đổi kết quả cảm nhận.

## 4. Customer Web audit

| Màn hình | Hiện trạng tốt | Vấn đề/rủi ro | Mức ưu tiên |
|---|---|---|---|
| Layout/Header/Footer | Skip link, `main` focus target, landmarks, `aria-current`, search label, touch target 44px | Mobile nav dùng horizontal scroll nhưng không có chỉ dấu; header có nhiều breakpoint chồng nhau; chữ action 11–12px | P1 |
| Home | Hero rõ CTA, hierarchy tốt, ảnh có kích thước, service semantics, responsive grids | Hai hệ token/hero cũ–mới cùng trong một CSS; product card 11–12px; hover translate chưa được vô hiệu hóa trong reduced-motion; 6 cột dày | P1 |
| Products | Filter dùng fieldset/legend, responsive panel, sort/search rõ | Filter state/chip chưa hiển thị rõ; desktop summary bị vô hiệu pointer; label/result nhỏ; cần focus/keyboard test details/summary | P1 |
| Detail | Breadcrumb, gallery buttons có aria-label/pressed/controls, ảnh alt, layout responsive | Cần live announcement khi đổi ảnh; thumbnail selected state cần không chỉ dựa màu; spec 2 cột có thể chật ở zoom 200% | P1 |
| Cart | Label ẩn cho quantity, nút xóa có accessible name, tổng server-side hiển thị rõ | Update và Remove gần nhau; cần confirm/undo hoặc khoảng cách danger; tổng/metadata 12–13px | P1 |
| Checkout | Layout summary + form rõ, server-side validation hiện có | Cần error summary focus sau submit, trạng thái loading/disable chống bấm lặp và thông báo tiến trình; kiểm tra autocomplete | P0/P1 |
| Login | Label thật, autocomplete, role=alert, remember checkbox, focus rõ | Chưa có show/hide password trên Web; lỗi summary cần focus; chữ helper nhỏ | P1 |
| Register | Label, input type/autocomplete, hint password, validation link qua aria-describedby | Cần show/hide password, mô tả required nhất quán, error summary focus | P1 |
| Profile | Email readonly theo nghiệp vụ, form tối giản | Validation span chưa có id/aria-describedby rõ như Auth; textarea thiếu min-height; success message cần live status | P1 |
| Orders list | Empty state và hierarchy rõ | Trạng thái nên có text + icon, không chỉ màu; bảng/card cần kiểm tra zoom/mobile | P1 |
| Order detail | Trạng thái/mã/giá rõ, action hủy có điều kiện nghiệp vụ | Hủy là destructive nhưng button chưa có class danger/confirmation UI rõ; mã dài cần wrap/focus | P0/P1 |

### Web — vấn đề xuyên suốt

1. **P0 accessibility/transaction:** đảm bảo focus chuyển tới error summary; loading/disabled rõ khi checkout; confirmation rõ cho hủy đơn.
2. **P1 typography:** tăng body từ 15px lên 16px; loại bỏ chữ nội dung 9–12px, đặc biệt card, dịch vụ, filter và header actions.
3. **P1 motion:** reduced-motion hiện chỉ tắt transition của `a,button`; phải tắt transform/transition trên brand cards, hero và mọi animation khác.
4. **P1 consistency:** gom token `:root` đang khai báo hai lần; dùng một scale radius/spacing/type cho Home, catalog, auth và commerce.
5. **P1 responsive:** kiểm tra 320/375/768/1024/1440px, zoom 200%, keyboard-only; tránh header/nav quá dày và card 2 cột quá chật ở 375px.
6. **P2 polish:** thống nhất empty state, badge trạng thái, button hierarchy và skeleton/loading; không thêm chức năng mới.

## 5. Admin WinForms audit

| Màn hình | Hiện trạng tốt | Vấn đề/rủi ro | Mức ưu tiên |
|---|---|---|---|
| Login | Tab order, Accept/Cancel, focus ban đầu, accessible names, busy state, DPI scale | Fixed two-column layout cần test 125–200% DPI; status cần AccessibleRole.Alert/live announcement; password toggle target/label | P1 |
| Shell/Main | Sidebar nhất quán, active state có màu + description, DPI scale | Sidebar cố định 244px, minimum 1000×680; active state vẫn thiên về màu; keyboard focus cue cần kiểm tra runtime | P1 |
| Dashboard | Card/KPI hierarchy và read-only đã có | Không thêm KPI; cần kiểm tra chart/card text scaling, empty state và tab traversal | P1 |
| Products | Toolbar wrap, accessible search/input, grid numeric alignment | Toolbar có thể vỡ ở DPI cao; DataGridView cần header contrast, row focus/selection không chỉ màu, keyboard action | P1 |
| Images | Grid, accessible DisplayOrder, confirmation xóa | Nhiều action ảnh cần state/selection rõ; file validation message cần focus/alert; preview/primary không chỉ dựa màu | P0/P1 |
| Brands | Search/toolbar/confirmation có sẵn | Validation Brand từng là limitation; status text cần Alert và focus; hành động active/inactive cần label động rõ | P1 |
| Categories | Cùng pattern Brands, giúp nhất quán | Cùng rủi ro DPI, status announcement, selection/action | P1 |
| Customers | Grid + confirmation trạng thái | Trạng thái active cần text/icon; thao tác nhạy cảm cần button danger và selection confirmation rõ | P0/P1 |
| Orders | Grid, dialog chi tiết, confirmation transition | Nút trạng thái phải thể hiện workflow, disabled reason, không chỉ màu; chi tiết 900×700 cần test DPI | P0/P1 |
| Reports | Filter ngày, cards/charts/tables và empty state | Chart custom cần legend/text summary/table alternative; màu series phải phân biệt không chỉ màu; 4 cards/2 columns có thể chật DPI | P1 |

### Admin — vấn đề xuyên suốt

1. Chuẩn hóa `AdminTheme`, `ManagementUi`, `ReportingUi` thành nguồn token duy nhất; không rải `Color.FromArgb` và font tùy ý.
2. Thiết lập AccessibleName/Description/Role cho status, grid, chart và action; đưa focus về lỗi/row liên quan.
3. Đảm bảo mọi thao tác chính dùng keyboard, Enter/Escape hợp lý, tab order theo thị giác; tránh shortcut xung đột.
4. Test DPI 100/125/150/200%, Windows text scaling và 1024×768; fixed sizes là rủi ro chính.
5. Với chart: thêm textual summary/legend trong màn hình hiện có, không thêm KPI.
6. Không dùng animation phức tạp trong WinForms; ưu tiên phản hồi trạng thái tức thời, không block UI thread.

## 6. Danh sách file dự kiến sửa

### S29.9B — Customer Web trước

1. `src/PhoneStore.Web/wwwroot/css/site.css` — token, type scale, spacing, focus, reduced motion, responsive, danger/status.
2. `src/PhoneStore.Web/wwwroot/js/site.js` — gallery/live status và hành vi focus tối thiểu nếu cần.
3. `Views/Shared/_Layout.cshtml`, `_ProductCard.cshtml`, `_OrderSummary.cshtml` — navigation, semantics, card consistency.
4. `Views/Home/Index.cshtml`, `Views/Products/Index.cshtml`, `Views/Products/Detail.cshtml`.
5. `Views/Cart/Index.cshtml`, `Views/Checkout/Index.cshtml`, `Views/Checkout/Success.cshtml`.
6. `Views/Account/Login.cshtml`, `Register.cshtml`, `Views/Profile/Index.cshtml`.
7. `Views/Orders/Index.cshtml`, `Views/Orders/Detail.cshtml`.
8. Tests UI/HTML/accessibility hiện có hoặc test mới chỉ cho markup/behavior; không đổi nghiệp vụ.

Thứ tự: token/layout → catalog/detail → cart/checkout/orders → auth/profile → responsive/accessibility regression.

### S29.9C — Admin sau Web

1. `Styling/AdminTheme.cs`, `ManagementUi.cs`, `ReportingUi.cs`.
2. `LoginForm.cs`, `MainForm.cs`.
3. `Controls/DashboardControl.cs`, `ProductsManagementControl.cs`, `LookupManagementControl.cs`, `CustomersManagementControl.cs`, `OrdersManagementControl.cs`, `ReportsControl.cs`.
4. `Dialogs/ProductEditDialog.cs`, `ProductImagesDialog.cs`, `LookupEditDialog.cs`, `CustomerEditDialog.cs`, `OrderDetailDialog.cs`.
5. Automated tests cho accessibility properties/layout invariants nếu phù hợp; nghiệm thu WinForms thật ở DPI/keyboard riêng.

Thứ tự: theme/shared controls → login/shell → data grids/toolbars → dialogs/destructive states → reports/charts → DPI/keyboard acceptance.

## 7. Tiêu chí nghiệm thu S29.9B

- Không đổi route, action, model binding, controller/service/repository hoặc database.
- Contrast AA: text thường ≥4.5:1, component/focus ≥3:1; trạng thái không phụ thuộc màu.
- Keyboard đi được toàn bộ luồng; focus rõ; error summary nhận focus sau submit lỗi.
- `prefers-reduced-motion` tắt mọi motion không thiết yếu.
- Không horizontal overflow ở 320/375px và zoom 200%; desktop giữ hierarchy ổn định.
- Cart/checkout/order actions giữ nguyên nghiệp vụ và anti-double-submit hiện hành.
- Build/test regression và browser acceptance trước deploy; triển khai cần phê duyệt riêng.

## 8. Trạng thái

- Codex skill recognition: **PASS**.
- Skill file/resources: **PASS**.
- Python search command: **BLOCKED** (`py`: No installed Python found).
- Source UI audit Web/Admin: **PASS (source-only)**.
- Runtime visual/accessibility acceptance: **NOT TESTED trong S29.9A**.
- S29.9B readiness: **READY có điều kiện**; có thể bắt đầu sửa Customer Web dựa trên audit, đồng thời nên cài Python 3 trước nếu muốn bắt buộc có design-system output từ search tool.
