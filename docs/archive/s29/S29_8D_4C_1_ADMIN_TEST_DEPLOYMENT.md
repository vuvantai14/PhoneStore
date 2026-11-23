# S29.8D.4C.1 — Deploy D.4B vào Admin test cách ly

Ngày nghiệm thu: 2026-11-02 (Asia/Bangkok). Chỉ triển khai môi trường test; không deploy Admin chính, không DML, đổi quyền, migration, restore, seed, commit/push.

## Staging và runtime

- Admin D2C cũ PID 9840 chỉ có một MainForm, không có modal được phát hiện; người dùng đóng an toàn. Xác minh PID thoát và không còn instance trước khi mở bản mới.
- Staging: `src/PhoneStore.Admin/obj/S29.8D.4C.1-20261102_155007187/app`.
- Publish Debug có 59 file; manifest cuối 59/59 hash khớp. Bốn DLL PhoneStore khớp binary từ full build/test D.4B.
- EXE SHA-256: `68EC8EDE9070A625CA8F8A7A69322B611E6AAE18D3B3091F2DF1F477B0749664`.
- Config được sao chép từ môi trường test D2C: `localhost/PhoneStoreDB_StockConcurrencyTest`, Windows Authentication.
- Uploads: `src/PhoneStore.Admin/obj/S29.8D.2C-20261102_035250536Z/uploads`, tách khỏi 40 ảnh main.
- PID mới `15548`, executable đúng staging. Sau login có đúng một MainForm và một SQL session tới test DB, open transaction 0.

## Runtime acceptance read-only

| Chức năng | Kết quả |
|---|---|
| Login Admin fixture | PASS WinForms thật; người dùng tự nhập mật khẩu. |
| Dashboard | PASS: active products 4, stock 9, Customers 4, Orders 10/Pending 4, Completed 1/revenue 1.000.000. |
| Reports 01–02/11/2026 | PASS: 10 orders, 1 Completed, revenue/average 1.000.000, stock 9/4, out-of-stock 2. |
| Same day 02/11/2026 | PASS: 10 orders, revenue 1.000.000; dữ liệu UTC hiện có được phân đúng ngày Bangkok. |
| Empty 01/11/2026 | PASS: 0 orders/Completed/revenue/average; inventory vẫn là snapshot hiện tại. |
| Product/Brand/Category/Customer/Order lists | PASS read-only theo người dùng: 5/2/2/4/10. Không mở dialog ghi. |
| Logout | PASS: MainForm đóng, chỉ còn một LoginForm trong cùng PID. |
| Login lại | PASS: MainForm mở lại, đúng một SQL session test DB, open transaction 0. |

Không có lỗi runtime/ObjectDisposedException hoặc nhiều dialog được ghi nhận. Ca timestamp sát đúng biên 00:00 local không có fixture hiện hữu; logic đó PASS automated + SQL translation ở D.4B, không suy là đã có UI fixture-boundary test.

## Hậu kiểm

- Test DB trước/sau: Roles2, Users5, Brands2, Categories2, Products5, Images4, Orders10, Details10, migrations2.
- Product13 inactive/stock5; Product14 active/stock1. Image22/24/25/26 giữ nguyên ProductId, URL, primary và order.
- Main: Roles2, Users2, Brands8, Categories1, Products40, Images40, Orders1, Details1, migrations2; stock740, một Cancelled, một active Admin.
- 40/40 ảnh main khớp SHA-256 baseline. Staging manifest vẫn 59/59.

## Phân loại

- D.4B automated: 161/161 + gateway fake + SQL integration read-only PASS (bằng chứng trước deployment).
- D.4C.1 runtime test: Dashboard/Reports/empty/same-day/module reads/login/logout/login lại **PASS WinForms thật**.
- Fresh authorization revoke: **NOT TESTED trong D.4C.1** vì cấm đổi RoleId/IsActive; dừng trước D.4C.2.
- Admin chính không được mở, dừng hoặc thay binary trong nhiệm vụ này.
