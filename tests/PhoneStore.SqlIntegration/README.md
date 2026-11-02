# S29.8B.1 SQL Server / WinForms opt-in verification

This executable is separate from the solution test suite. It writes only to the approved
`localhost/PhoneStoreDB_StockConcurrencyTest`, never the application database.
Every EF command verifies the connection string, `DB_NAME()` and server before execution.
Do not run without explicit authorization for test database writes.

Prerequisites: create that NEW database only after checking it does not exist and receiving
approval; apply InitialCreate and EnforceSingleAdmin using an explicit test connection.
The executable requires empty Users/Products/Brands/Categories and refuses to reuse its fixture.
It never creates, drops, restores or migrates databases, and must not be blindly rerun.

```powershell
dotnet build tests/PhoneStore.SqlIntegration/PhoneStore.SqlIntegration.csproj -p:OutputPath=bin/S29.8B.1/
tests/PhoneStore.SqlIntegration/bin/S29.8B.1/PhoneStore.SqlIntegration.exe --execute-approved-s29.8b.1
```

It creates one test Admin, Customer, Brand, Category and Product (stock10), then uses
the real OrderService/ProductManagementService, separate SQL connections, and the real
ProductEditDialog on a Windows desktop. Random test passwords are hashed in memory;
passwords/hashes are never printed or copied from the main database.

Cases: checkout10→9/stale-save rejection, name/price preserved, two open snapshots,
two overlapping atomic conditional UPDATEs with affected rows1/0, valid save, controlled
failure before commit and rollback, then real dialog conflict label after checkout.
The deliberate pre-commit exception is an expected test injection, not an operational failure.
The test fixture remains: two checkout orders/details and final stock9. No image files are used.

The dialog test exercises the actual dialog and Save button with real SQL services;
it does not exercise login/navigation in the separately launched Admin shell.
It automatically closes only its own dialog. Existing Web/Admin processes are unaffected.

Evidence from the approved run on 2026-11-02 (Asia/Bangkok): exit0; both migrations present;
stale edits refused with stock9, concurrent UPDATE affected rows[1,0]/stock11,
rollback preserved stock10/price1000000, real conflict label visible in the dialog,
final UI case stock9/name unchanged. The database was retained.

## S29.8C commerce modes

Additional opt-in modes, only after approval for writes to the existing isolated database:
`--execute-approved-s29.8c`, `--execute-approved-s29.8c-final`,
and `--execute-approved-s29.8c-http`. Build with `-p:OutputPath=bin/S29.8C/`.
They leave their fixtures in place and refuse duplicate fixture names; never blindly rerun.
No mode migrates, drops, restores, or cleans up database records.

SQL cases cover durable checkout replay, double cancel, last-stock competition and
pre-commit rollback using real services and independent connections. HTTP mode starts
only its own test Web at 127.0.0.1:5016 with an explicit test connection, verifies its SQL
session before POSTs, uses separate in-memory credentials/cookies, and stops only that helper.
It adds a Customer and one checkout order, then cancels it. It never uses a main credential.
See docs/S29_8C_COMMERCE_REPORT.md for exact results and retained fixture counts, including
the first HTTP run that stopped on a test assertion before any checkout.

## S29.8C.5 Admin acceptance

`--execute-approved-s29.8c.5-ui` exercises the actual OrdersManagementControl and
OrderDetailDialog in an STA test host with guarded SQL services, two new orders and
process-scoped confirmation handling. It establishes a test Admin session without
using credentials, so it does not test full shell login. The fixture is retained.
`--execute-approved-s29.8c.5-http-sync` uses a new test Customer to check Admin Confirmed
is visible in Customer HTTP history/detail, then repeated cancellation and restock.
Use only after explicit approval for test writes; never blindly rerun either mode.
Results and deployment artifacts: docs/S29_8C_5_ACCEPTANCE_DEPLOYMENT.md.
