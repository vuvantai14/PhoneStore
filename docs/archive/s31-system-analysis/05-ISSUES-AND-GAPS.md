# 05 — Vấn đề và khoảng trống

## P0

Không phát hiện P0 mới từ source và 138 test mục tiêu. Live SQL/runtime chưa kiểm nên đây không phải bảo đảm tuyệt đối.

## P1

| ID | Vấn đề | Khuyến nghị |
|---|---|---|
| P1-01 | Live SQL UNKNOWN do TLS/ODBC 17 | Sửa client read-only rồi đối chiếu schema/count |
| P1-02 | S30.3A–D chưa nghiệm thu WinForms thật | Test 1366×768, DPI 100/125/150% |
| P1-03 | Race fresh-check → commit | Known limitation; nếu cần tuyệt đối, kiểm quyền cùng transaction/DB boundary |

## P2 / giới hạn

- Replace ảnh trực tiếp không hỗ trợ; dùng upload mới + primary + unlink.
- DisplayOrder UI, đóng dialog lúc save và đầy đủ thông báo Brand validation chưa acceptance đủ.
- Nội dung thông báo revoke chưa quan sát đầy đủ dù revoke/session clear từng PASS.
- Catalog coverage automated ít sâu hơn commerce.
- Không có `CompletedAt`; báo cáo theo `OrderDate` là quy tắc đã chốt.
- Payment online, voucher, shipping integration, password reset/email verification ngoài phạm vi hiện tại.
- `PROGRESS.md` dài hơn mục tiêu 150 dòng; nên compact bằng tác vụ tài liệu riêng, không làm trong audit để tránh mất lịch sử.

