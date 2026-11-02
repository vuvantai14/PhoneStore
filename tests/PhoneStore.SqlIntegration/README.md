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

## S29.8D.2A Admin CRUD (opt-in, writes and scoped cleanup)

`--execute-approved-s29.8d.2a` uses ONLY the existing
`localhost/PhoneStoreDB_StockConcurrencyTest`, guarded before each EF command.
Requires explicit authorization for fixture writes AND deleting only newly created
fixture rows/files. Checks that no other database session is using the target before
starting and holds a cooperative application lock for this mode.

Creates a unique D2A prefix: one Brand, one Category, three Products, one Cancelled
Order/Detail using an existing test Customer, and separate generated PNG/JPEG files.
No Customer/Admin creation or changes, no migrations or main database connections.
Tests lookup/product validation/search/soft deactivate/reactivate, referenced FK
delete rejection, stale stock, images/primary/order/cleanup, pre-commit rollback,
and gateway/session role rejection. This is service/SQL testing, not full WinForms UI.

On success, deletes only this run's rows by prefix/IDs in a transaction and removes
its own files. Existing fixtures are retained; identity counters may advance.
On failure it stops without automatic retry or broad cleanup; inspect retained
fixtures before deciding the next action. Do not rerun older destructive modes.

Example after approval, build to a separate output then invoke its executable:

```powershell
dotnet build tests/PhoneStore.SqlIntegration/PhoneStore.SqlIntegration.csproj --no-restore -p:OutputPath=bin/S29.8D.2A/
tests/PhoneStore.SqlIntegration/bin/S29.8D.2A/PhoneStore.SqlIntegration.exe --execute-approved-s29.8d.2a
```

Results/remaining coverage: docs/S29_8D_2A_ADMIN_CRUD_AUDIT.md.

## S29.8D.2B content validation and file safety

New uploads accept JPEG/PNG only, decoded by Windows Desktop WIC; WebP uploads are
rejected explicitly (existing files/associations are retained). No external decoder
package is added. Admin and harness reference the installed Windows Desktop WPF
framework for BitmapDecoder, without replacing the WinForms UI.

File-only opt-in: `--check-images-s29.8d.2b` creates unique generated fixtures,
checks real decoding, malformed/truncated/oversized images, dimensions/pixels,
write failure and safe paths; removes only its own fixtures. No SQL connection.

SQL opt-in: `--execute-approved-s29.8d.2b` requires approval for test writes and
owned-fixture cleanup. Uses the existing StockConcurrencyTest database with command
guards and a test-run application lock. Creates only two Products + one Brand and
Category, image associations and generated files. Tests shared old files, concurrent
replacements/first uploads, primary rules and rollback, then cleans only its fixtures.
Never touches test Users/Orders or main. On error, inspect outcome before cleanup or
starting a fresh fixture; no transaction replay or automatic retry.

Image-write transactions now acquire a database application lock before row locks
to avoid Serializable range-lock conversion deadlocks between different Products.
After unlink or a DB failure/uncertain commit, valid files are retained with a notice;
do not expect production cleanup to delete these immediately. Controlled test cleanup
verifies no references after removing only its fixture rows.

Report: docs/S29_8D_2B_IMAGE_VALIDATION.md. D2A expectations were updated to this
retention policy; its historical result remains evidence for the older implementation.

## S29.8D.2C.1 scoped existing test Admin credential

This mode requires separate explicit approval to reset only existing test UserId1,
admin@s29-8b1.test, active Admin on localhost/PhoneStoreDB_StockConcurrencyTest.
It never accepts target overrides or a password argument, and never starts CRUD/UI tests.

- `--preflight-test-admin-s29.8d.2c.1`: read-only actual server/database/identity guard.
- `--reset-test-admin-approved-s29.8d.2c.1`: interactive console only; hidden password
  and confirmation, PasswordHasher<User>, Serializable transaction/row lock, strict
  identity WHERE, PasswordHash-only assignment, exactly one affected row.

Do not redirect or record the interactive terminal. Password/hash never go to status
files; post-commit SELECT reports only a boolean match. An attempt marker prevents
blind replay; on any failure check read-only outcome, never automatically retry.
The new credential replaces an unknown old fixture password and is retained.
User login to the real test Admin window is a separate manual step. Main is forbidden.
Evidence and pending CRUD matrix: docs/S29_8D_2C_ADMIN_WINFORMS_ACCEPTANCE.md.
