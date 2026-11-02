# S29.8D.4B — Fix report timezone và Admin session authorization

Ngày thực hiện: 2026-11-02. Source/tests được sửa; không deploy, migration, restore, seed, commit/push hoặc ghi `PhoneStoreDB`.

## Thay đổi Reports

- Thêm `ReportingCalendar` với timezone nghiệp vụ cố định `SE Asia Standard Time` (Bangkok/UTC+7), không phụ thuộc timezone máy chủ.
- `DateOnly` local được chuyển đúng một lần sang UTC tại `ReportingService`; query tiếp tục dùng khoảng nửa mở `[startUtc, endExclusiveUtc)`.
- `ReportingStore` nhận offset 420 phút và group revenue trend theo `OrderDate.AddMinutes(420).Date`, được SQL Server dịch thành SQL. Không kéo Orders về memory.
- Giữ nguyên Completed-only cho revenue/average/trend và giữ nguyên `Confirmed + Shipping + Completed` cho Best sellers.
- Thêm `IReportingStore` để test ranh giới service mà không cần database ghi.

## Thay đổi Admin authorization

- `AuthUserStore.IsActiveAdminAsync` fresh-check đúng `UserId + IsActive + RoleName=Admin`; `AdminAuthService` expose qua `IsCurrentAdminAsync`.
- `CatalogManagementGateway` giữ read path hiện tại, nhưng thêm các write path riêng cho Product, Lookup, Customer và Order. Mỗi write tạo scope, fresh-check trước khi resolve/call service.
- Tất cả entry point ghi UI đã chuyển sang write path: Product save/toggle, Brand/Category save/toggle, Customer edit/toggle, Order status, ảnh add/remove/set-primary.
- Check xảy ra trước service/file side effect; upload ảnh không copy file nếu authorization thất bại.
- Revoked: clear `AdminSession`, phát `AuthorizationRevoked`; `AdminApplicationContext` dùng `BeginInvoke` để đóng shell và mở LoginForm sau khi continuation UI hiện tại unwind.
- DB/check error: fail closed, write delegate không chạy, nhưng không giả là revoke và không clear session.

Giới hạn còn lại: revoke có thể xảy ra sau fresh-check nhưng trước transaction/commit. Bản sửa giảm cửa sổ và bảo vệ mọi UI entry point, nhưng không tuyên bố loại bỏ race tuyệt đối. Muốn bảo đảm tuyệt đối phải kiểm authorization trong cùng transaction với từng write, là thay đổi kiến trúc lớn hơn.

## Tests và bằng chứng

- Unit tests mới: Bangkok same-day/multi-day/end-exclusive/no-double-convert, empty report, invalid range; fresh authorization active/inactive/role/DB unavailable.
- Gateway self-test bằng fake: active cho phép; revoked chặn action/clear session/phát event; DB error chặn action nhưng không gắn nhãn revoke.
- SQL read-only trên `PhoneStoreDB_StockConcurrencyTest`: range Bangkok/trend được SQL Server dịch; total khớp phép tính độc lập; Completed-only loại status khác; empty range 0; active Admin/invalid identity fresh-check đúng.
- Full solution build: PASS 0 warnings/errors (`bin/S29.8D.4B-final`).
- Full xUnit: PASS 161/161, 0 failed/skipped.
- SQL integration executable: PASS toàn bộ tám check read-only; không fixture/data changes.

Chưa chạy revoke role/inactive qua WinForms/SQL thật: test đó cần thay `IsActive` hoặc `RoleId` của Admin fixture đang có. Theo giới hạn nhiệm vụ, cần phê duyệt riêng trước khi sửa/khôi phục fixture D2C. Vì vậy trạng thái UI revoke là PARTIAL, không được suy từ automated test.

## Hậu kiểm

- Main: Roles2, Users2, Brands8, Categories1, Products40, Images40, Orders1, Details1, migrations2, stock740, một Cancelled, một active Admin.
- 40/40 ảnh demo khớp SHA-256 baseline.
- Test DB giữ Products5, Images4, Brands2, Categories2, Users5, Orders10, Details10; Product13/14 và Image22/24/25/26 giữ nguyên.

## Files

- Data: `AuthUserStore.cs`, `IReportingStore.cs`, `ReportingStore.cs`.
- Business: `IAdminAuthService.cs`, `AdminAuthService.cs`, `ReportingCalendar.cs`, `ReportingService.cs`.
- Admin: `AdminServices.cs`, `AdminApplicationContext.cs`, `CatalogManagementGateway.cs`, Products/Lookups/Customers controls, ProductImages/OrderDetail dialogs.
- Tests: `AuthenticationTests.cs`, `ReportingTests.cs`, SQL integration reporting/auth/gateway checks, Program/README.

## Kết luận

- Report timezone/trend: **PASS automated + PASS SQL isolated read-only**.
- Fresh authorization và fail-closed: **PASS automated/self-test**.
- Revoke đóng shell trên WinForms thật: **PARTIAL / chưa chạy**.
- Không deploy; Admin test PID cũ vẫn là binary D2C, không chứa bản sửa D.4B.
