# 06 — Checklist nghiệm thu thủ công

Không ghi main nếu chưa có phê duyệt riêng.

## Customer Web

- [ ] Register/login/logout; duplicate email; sai password; inactive/sai role.
- [ ] Logout xóa auth/cart/token; protected route về Login.
- [ ] Home/catalog search/filter/sort/paging; ảnh/route/focus/responsive.
- [ ] Detail đúng giá/tồn/active/gallery.
- [ ] Cart trên DB test: add/update/remove/invalid/inactive/out-of-stock/server total.
- [ ] Checkout test DB: one click/replay/insufficient rollback/concurrent stock.
- [ ] History/detail ownership; sửa ID không đọc order khác.
- [ ] Cancel Pending/Confirmed; restock once; replay không cộng thêm.
- [ ] Profile email readonly, ba field cho phép, CSRF/validation/greeting.

## Admin WinForms

- [ ] Login/logout/login lại; wrong password; revoke role/active clear session.
- [ ] Dashboard KPI/donut/revenue/recent/best/low stock; loading/empty/error.
- [ ] Week/month/year/custom, prev/next, max 366 ngày, không query chồng.
- [ ] Năm bảng quản lý ID ASC sau reload/search/filter.
- [ ] Filter bars 1366×768, DPI 100/125/150%; không cắt/chồng.
- [ ] Product CRUD test DB: validation/stale stock/soft-state/reference.
- [ ] Image JPEG/PNG và reject corrupt/WebP/APNG/limit; primary/unlink.
- [ ] Brand/Category soft-state khi có Product tham chiếu.
- [ ] Customer edit/toggle; không sửa Admin duy nhất.
- [ ] Order transitions/cancel/restock/Web sync.
- [ ] Reports global filter độc lập chart-local; Completed-only; tồn kho hiện tại.

## Hậu kiểm

- [ ] Xác minh DB_NAME trước query; counts/invariants không đổi nếu read-only.
- [ ] Không migration/seed/schema drift.
- [ ] Log không unhandled exception/secret.

