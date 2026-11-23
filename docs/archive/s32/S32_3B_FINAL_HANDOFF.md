# S32.3B — Bàn giao vòng kiểm thử PhoneStore

Ngày bàn giao 19/11/2026. Vòng kiểm thử hiện tại kết thúc theo quyết định của người dùng. Trạng thái của từng ca giữ theo bằng chứng đã có.

## A–B. Tổng hợp kiểm thử

| Giai đoạn | Trạng thái bàn giao | Bằng chứng và phạm vi |
| --- | --- | --- |
| S32.1 | Audit hoàn tất theo tiến độ đã ghi nhận | Mốc audit hệ thống. Số liệu SQL Windows của giai đoạn này chỉ dùng làm tham chiếu lịch sử. |
| S32.2 | Preflight hoàn tất theo tiến độ đã ghi nhận | Kiểm tra Docker, endpoint chỉ đọc và đích SQL của launcher Admin. Mốc SQL Docker là 15 đơn. |
| S32.3A | CLOSED theo mốc chuyển sang S32.3B-PREP | Nghiệm thu các luồng đọc Customer Web và Admin. FIX01 đã xác minh bằng HTTP trên Docker rằng bốn trang catalog trả 40 ID riêng biệt và giữ tham số tìm kiếm, lọc, sắp xếp khi đổi trang. Ghi nhận HTTP, GUI và SQL theo từng bằng chứng. |
| S32.3B ENV-SHARED | PASS | Acceptance có database, Web/API và uploads riêng. Dùng chung SQL Docker instance với business. Kiểm tra xác nhận login acceptance bị từ chối truy cập database business. |
| EXEC-A1 | PASS trong phạm vi CO01–CO05 | Checkout, giá phía server, validation, hết hàng/inactive và replay tuần tự. |
| EXEC-A2 | PASS trong phạm vi OR01–OR05 | Chuyển trạng thái, stale status, hủy và hoàn tồn, replay tuần tự, ownership. |
| EXEC-A3 | PASS trong phạm vi PR01/PR02, LK01/LK02 và IM01/IM02 | Product/Brand/Category, bảo vệ tồn kho với form cũ và kiểm tra ảnh. |
| EXEC-A4 — CT01 / AC01 | PASS trong phạm vi đã chạy | Cart có bằng chứng HTTP, rendered HTML và SQL. AC01 có bằng chứng HTTP và SQL cho đăng ký, hồ sơ và overposting. CT01 chưa có bằng chứng browser JS đầy đủ trong đợt A4. |
| EXEC-A4 — CT02 | FIX / STABILIZE PASS | Lỗi revoke đã xác minh sửa trên Web acceptance. RT03 anonymous checkout token là NOT APPLICABLE qua HTTP. RT05 xác nhận token legacy bị xóa khi đăng nhập, nhưng cart thiếu owner có thể được nhận như cart khách. Cần vô hiệu hóa session cũ trước khi dùng store persistent hoặc chạy trộn phiên bản. |
| EXEC-A4 — SE01 | BLOCKED runtime, hoãn | Không có workflow quản lý quyền Admin trong sản phẩm để chạy ca revoke hiện tại. Read gateway dựa vào session trong bộ nhớ và vẫn có khoảng giữa fresh-check và commit. Bằng chứng revoke WinForms từ S29.8D.4C thuộc môi trường lịch sử. |
| EXEC-A4 — RP01 | PARTIAL, hoãn | GUI/SQL đã khớp doanh thu Completed 2.000.000đ. Acceptance thiếu fixture tại biên 00:00 UTC+7. |
| CLOSEOUT-02A/02B và EXEC-B | DEFERRED | Kết thúc vòng hiện tại trước các bước này. Các ca ngoài phạm vi đã chạy, gồm concurrency nặng và fault injection, chưa được nghiệm thu tại đây. |

Automated gần nhất **249/249 PASS** cho `PhoneStore.Tests` tại CT02-STABILIZE. Sau đó `ReportingTests` **13/13 PASS** tại CLOSEOUT-01. Phiên bàn giao chỉ đọc tài liệu, source, Git và không chạy lại test. Mốc S32.1–S32.3A dựa trên ghi nhận bàn giao và [PROGRESS.md](../../PROGRESS.md). Chi tiết acceptance nằm trong [ENV-SHARED](S32_3B_ENV_SHARED.md), [CT02-FIX01](S32_3B_CT02_FIX01.md) và [CT02 runtime](S32_3B_CT02_RUNTIME.md).

## C. Runtime tại lần xác minh gần nhất

Tại CLOSEOUT-01 ngày 19/11/2026, **5/5 container healthy** và cả bốn endpoint Web/API trả HTTP 200. Business Web/API là `5015/5025`, acceptance Web/API là `5285/5286`. SQL Docker dùng port host `14334`. Web acceptance `84066ac4b44f` dùng image CT02 đã xác minh. Business Web chưa triển khai CT02.

Cả hai database đều ONLINE, count/checksum khớp 8/8 bảng mỗi DB. Business có 40 sản phẩm và 15 đơn. Acceptance có 5 sản phẩm, 4 đơn, 4 chi tiết và 2 ảnh. Manifest uploads khớp lần đối chiếu trước với 42 file business và 5 file acceptance. Đây là snapshot gần nhất.

## D–E. Git và checkpoint đề xuất

`main` ở `8cc7506d01f35a4b4874794c1b21308e9fc9b07c`, đồng bộ với ref `origin/main` đang lưu cục bộ. Commit này đã chứa FIX01 phân trang. Index trống.

- Modified gồm ba file Web của CT02 (`AccountController.cs`, `Program.cs`, `SessionCart.cs`), hai file regression test (`CartControllerTests.cs`, `CustomerProfileHttpTests.cs`) và `docs/PROGRESS.md`.
- Untracked gồm bốn tài liệu S32.3B tính cả báo cáo này, sáu file trong `scripts/acceptance/` và hai file source/project trong `tools/PhoneStore.AcceptanceEnvironment/`.
- Đề xuất checkpoint sau khi duyệt diff theo đường dẫn: `fix(web): isolate checkout sessions and stabilize acceptance runtime`. Gom thay đổi CT02, regression tests, launcher/config/tool acceptance và hồ sơ bàn giao. Git đã ignore `.env`, scratch acceptance và đầu ra build được kiểm tra. Loại rõ credentials, uploads, database/backup và đầu ra build khỏi checkpoint khi duyệt. Chưa stage, commit, push hay fetch trong phiên này.

## F. Lộ trình nâng cấp Customer Web

Lộ trình dùng MVC/Razor hiện có và các nguyên tắc UI UX Pro Max về phân cấp thông tin, điều hướng bàn phím, focus, vùng bấm và phản hồi lỗi. Khi triển khai từng màn hình, kiểm tra hẹp/rộng cùng các trạng thái rỗng, dữ liệu có sẵn và lỗi tương ứng.

| Thứ tự | Màn hình và source hiện có | Phạm vi nâng cấp đề xuất |
| --- | --- | --- |
| 1 | [Catalog](../../../src/PhoneStore.Web/Views/Products/Index.cshtml), [card](../../../src/PhoneStore.Web/Views/Shared/_ProductCard.cshtml), [empty state](../../../src/PhoneStore.Web/Views/Shared/_EmptyCatalog.cshtml) | Làm rõ số kết quả, bộ lọc đang áp dụng, nút xóa lọc và điều hướng trang. Tinh chỉnh mở/đóng bộ lọc mobile và focus. Giữ nguyên query, order, page size và FIX01. |
| 2 | [Chi tiết sản phẩm](../../../src/PhoneStore.Web/Views/Products/Detail.cshtml), [trang thiếu sản phẩm](../../../src/PhoneStore.Web/Views/Products/NotFound.cshtml) | Ưu tiên ảnh, giá, tồn kho và CTA. Làm rõ thumbnail được chọn, điều hướng bàn phím và trạng thái ảnh thiếu/hết hàng. Giữ nguyên route và hành vi thêm giỏ. |
| 3 | [Home](../../../src/PhoneStore.Web/Views/Home/Index.cshtml), [Brands](../../../src/PhoneStore.Web/Views/Brands/Index.cshtml), [Categories](../../../src/PhoneStore.Web/Views/Categories/Index.cshtml), [Layout](../../../src/PhoneStore.Web/Views/Shared/_Layout.cshtml) | Tinh chỉnh liên kết từ hero, thương hiệu, danh mục và 12 sản phẩm mới nhất tới Catalog. Rà lại menu mobile, tìm kiếm, focus và badge. Giữ dữ liệu và bố cục Home đã chốt làm nền. |
| 4 | [Cart](../../../src/PhoneStore.Web/Views/Cart/Index.cshtml) | Làm rõ phản hồi của từng dòng khi thay đổi số lượng, dòng hết hàng, tổng tiền và trạng thái trống. Giữ định danh ProductId, hành vi CT02 và kiểm tra server. |
| 5 | [Checkout](../../../src/PhoneStore.Web/Views/Checkout/Index.cshtml), [Success](../../../src/PhoneStore.Web/Views/Checkout/Success.cshtml) | Căn chỉnh form giao hàng và tóm tắt, đưa focus đến lỗi, hiển thị trạng thái gửi và làm rõ đường dẫn tới đơn hàng sau thành công. Giữ CSRF, token, idempotency và giá server. |
| 6 | [Orders](../../../src/PhoneStore.Web/Views/Orders/Index.cshtml), [Order Detail](../../../src/PhoneStore.Web/Views/Orders/Detail.cshtml) | Cải thiện bộ lọc trạng thái/thời gian, trạng thái trống và phân cấp mã đơn, tổng giá, giao hàng. Giữ điều kiện hủy và ownership. |
| 7 | [Login](../../../src/PhoneStore.Web/Views/Account/Login.cshtml), [Register](../../../src/PhoneStore.Web/Views/Account/Register.cshtml), [Profile](../../../src/PhoneStore.Web/Views/Profile/Index.cshtml), [Access Denied](../../../src/PhoneStore.Web/Views/Account/AccessDenied.cshtml) | Đồng bộ label, lỗi cạnh trường, thông báo lưu, bàn phím và password manager. Giữ email hồ sơ chỉ đọc, phân quyền, route và hành vi session. |

## G. Màn hình đầu tiên

Bắt đầu bằng **Catalog `/products`**, tập trung bộ lọc mobile, kết quả và phân trang. Màn hình có dữ liệu công khai và có thể nghiệm thu bằng thao tác GET. FIX01 cung cấp baseline rõ để kiểm tra việc giữ đúng tập kết quả và tham số khi đổi trang. Nghiệm thu bước đầu bằng tìm kiếm/lọc/sort, link trang 2–4, trạng thái rỗng và điều hướng bàn phím. Chuyển sang Detail sau khi Catalog được duyệt.
