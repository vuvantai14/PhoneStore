# 01 — Tổng quan hệ thống PhoneStore

Ngày rà soát: 2026-11-13 · Phạm vi: S30.4A · Trạng thái: **READY FOR REVIEW**

PhoneStore gồm Customer Web ASP.NET Core MVC/Razor và Admin WinForms dùng chung Business, Data, Models và SQL Server. Rà soát này chỉ đọc source/schema, chạy test cách ly sẵn có và thử SELECT SQL; không chạy ứng dụng, không DML, không sửa nghiệp vụ.

Nhãn bằng chứng: **VERIFIED-AUTO** (test đang PASS), **VERIFIED-SOURCE** (đường code/schema rõ nhưng chưa nghiệm thu runtime), **VERIFIED-HISTORY** (nghiệm thu lịch sử), **PARTIAL**, **NOT TESTED/UNKNOWN**.

## Kiến trúc

| Tầng | Project | Trách nhiệm |
|---|---|---|
| Customer UI | `PhoneStore.Web` | MVC/Razor, cookie auth, session cart, checkout, order/profile |
| Admin UI | `PhoneStore.Admin` | WinForms login/session, dashboard, CRUD, order, reporting |
| Business | `PhoneStore.Business` | validation, workflow, transaction orchestration, reporting calendar |
| Data | `PhoneStore.Data` | EF Core context/configuration, stores, SQL transaction/query |
| Domain | `PhoneStore.Models` | entities, enums, DTO/contracts |
| Tests | `PhoneStore.Tests` | unit/integration cách ly cho Web, Business, Data, Admin |

Luồng chính: `UI → Business → Data → PhoneStoreDbContext → SQL Server`. Web và Admin không tạo hệ tài khoản/nghiệp vụ song song.

## Cơ chế bảo vệ chính

- Web dùng cookie `PhoneStore.Auth`, session `PhoneStore.Session`, policy `Customer`, antiforgery trên POST và tái xác minh cookie.
- Cart/checkout tải lại giá, active và tồn kho từ server; không tin giá/tổng client.
- Checkout dùng transaction serializable, application lock theo idempotency key và OrderCode duy nhất; lỗi rollback.
- Hủy đơn/hoàn kho trong transaction; `Cancelled` terminal và replay không hoàn kho lần hai.
- Admin write gateway fresh-check UserId + IsActive + Admin role và fail-closed.
- Product edit dùng optimistic concurrency theo stock gốc.
- Ảnh chỉ nhận JPEG/PNG giải mã được, tối đa 10 MiB, 4096px mỗi chiều, 16 triệu pixel; tên file an toàn.
- Reporting chỉ tính `Completed`, theo `OrderDate`, ranh giới UTC+7 `[startUtc,endExclusiveUtc)`.

## Bằng chứng S30.4A

- 15 nhóm test chức năng mục tiêu: **138/138 PASS**, 0 fail/skip.
- Không build lại vì không sửa source; DLL test S30.3D được tạo ngày 2026-11-13.
- SELECT live `PhoneStoreDB`: **UNKNOWN** do ODBC 17 lỗi encryption/SSL trước khi chạy SELECT. Không dùng số lịch sử làm kết quả live.
- Git đầu lượt: `main`, HEAD `e88625b`, working tree sạch; không commit/push.

## Kết luận

Core commerce và quản trị đã triển khai khá đầy đủ, có test tốt cho rủi ro dữ liệu chính. Toàn hệ thống **PARTIAL / READY FOR REVIEW**, chưa phải production acceptance vì UI runtime S30.3A–D, live SQL snapshot và một số tương tác WinForms/DPI vẫn cần nghiệm thu.

