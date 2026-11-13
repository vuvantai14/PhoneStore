# 04 — Ma trận kiểm thử chức năng

Kết quả S30.4A: **138 PASS / 0 FAIL / 0 SKIP**.

| Nhóm | Automated | SQL live | Runtime hiện tại | Kết luận |
|---|---:|---:|---:|---|
| Account/auth/logout | PASS | NOT RUN | NOT TESTED | PASS automated |
| Profile/ownership/CSRF | PASS | NOT RUN | NOT TESTED | PASS automated |
| Catalog/Home/detail/filter | PARTIAL | NOT RUN | NOT TESTED | PARTIAL acceptance |
| Cart CRUD/isolation | PASS | NOT RUN | NOT TESTED | PASS automated |
| Checkout/rollback/concurrency | PASS | UNKNOWN | NOT TESTED | PASS isolated |
| Idempotency | PASS | UNKNOWN | NOT TESTED | PASS isolated |
| Order history/detail/ownership | PASS | NOT RUN | NOT TESTED | PASS automated |
| Cancel/restock once | PASS | UNKNOWN | NOT TESTED | PASS isolated |
| Admin auth/fresh check | PASS | NOT RUN | NOT TESTED | PASS automated |
| Admin CRUD/order workflow | PASS | NOT RUN | NOT TESTED | PASS automated |
| Image validation/cleanup | PASS | NOT RUN | NOT TESTED | PASS isolated |
| Dashboard/Reports | PASS | UNKNOWN | NOT TESTED | PASS automated |
| Management ID ASC | PASS | NOT RUN | NOT TESTED | PASS automated |
| S30.3A–D visual/DPI | N/A | N/A | NOT TESTED | PARTIAL |

Test chạy: `AuthenticationTests`, `CartControllerTests`, `CheckoutTests`, `CommerceWorkflowTests`, `OrderWorkflowTests`, `CustomerProfileTests`, `CustomerProfileHttpTests`, `SingleAdminPolicyTests`, `ProductStockConcurrencyTests`, `AdminCrudTests`, `ReportingTests`, `ManagementOrderingTests`, `ImageStorageSafetyTests`, `OrderPaginationTests`, `HomePresentationTests`.

Chiến lược: unit/integration SQLite cho validation; SQL Server test DB cho lock/concurrency; SELECT-only main cho schema/invariant; browser/WinForms theo checklist; không dùng main làm test suite.

