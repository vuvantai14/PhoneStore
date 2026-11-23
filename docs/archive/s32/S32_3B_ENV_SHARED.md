# S32.3B-ENV-SHARED — ENV PASS

Verified 2026-11-16. Preparation only: no checkout/CRUD/concurrency/fault-injection tests executed.

## Isolation and resources

- Existing SQL Docker identity: `7f8d4ad3701c`, host `127.0.0.1:14334`. New database: `PhoneStoreDB_AcceptanceTest`.
- New Compose project: `phonestore-acceptance-s323b`; only Web/API, no SQL service or named volume creation.
- Acceptance Web: <http://localhost:5285>; API: <http://localhost:5286/api/health>.
- Web/API connect to `host.docker.internal,14334` with the acceptance database and runtime login. Web API BaseUrl: `http://api:8080/` on a separate Compose network.
- Web limit 384 MiB/0.5 CPU, API 256 MiB/0.5 CPU; pool limit 5 per application. Both healthy, OOMKilled=false.
- Host available RAM approximately 3.00 GiB before setup and 2.39 GiB at final check. Suitable only for small sequential tests; recheck resources before EXEC-A. No stress/fault testing on the shared instance.
- New database files only: data initial32/max256 MiB; log initial16/max128 MiB; growth8 MiB. Existing SQL configuration/volume mount unchanged.

## Credentials and permissions

- `ps_s323b_provision`: db_owner only in acceptance; disabled after migration/fixture initialization.
- `ps_s323b_runtime`: SELECT/INSERT/UPDATE/DELETE on the eight acceptance business tables; SELECT-only migration history. No database ALTER or sysadmin privilege.
- Runtime connection to business `PhoneStoreDB` rejected. Checks used permissions metadata and attempted connection, never business DML.
- Credentials generated randomly; persisted only as Windows DPAPI-encrypted SecureString values in ignored `scratch/acceptance-s323b/secrets.clixml`. Directory ACL restricted to current Windows SID, SYSTEM, Administrators. No SA credential persisted there.
- Do not print secrets or run verbose Compose config/inspect environment output. Docker environment credentials remain readable to users with Docker administrator access.

## Fixtures

- Two migrations applied, two schema Roles, one Admin and two Customers.
- Synthetic accounts: `admin@s323b.test`, `customer-a@s323b.test`, `customer-b@s323b.test`. No real customer/hash copied.
- Two Brands, two Categories, three active Products (IDs1/2/3): prices1,000,000/2,000,000/3,000,000; stocks5/1/0.
- Orders0, image rows0. Two synthetic64x64 JPEG/PNG fixture files live in separate uploads, not yet linked to a Product.
- Upload root: `scratch/acceptance-s323b/uploads`; Web bind is read-only, Admin acceptance writes only to this test root.

## Admin

- Separate output: `scratch/acceptance-s323b/admin`; existing Admin PID28116 and its output left untouched.
- Program/ApplicationContext source has no global single-instance mutex; the original launcher's blanket process check is not an application constraint. Dedicated launcher checks the acceptance executable path.
- `scripts/acceptance/Run-Admin.ps1 -CheckOnly` PASS. It verifies SQL permissions and output, without launching an Admin.
- Interactive acceptance Admin has NOT been started or logged in. When authorized, run the launcher without `-CheckOnly`; use fixture identity only. Its environment overrides are process-local and restored after launch.

## Validation and production integrity

- Tool build PASS:0 warnings/errors; PowerShell parse PASS; Compose static config PASS.
- GET Home/Products/Detail1/Login, fixture PNG/JPEG and API health:HTTP200.
- Web catalog IDs3,2,1; API returns exactly three fixture products with expected stock/price.
- Runtime/provision permission checks PASS; provisioning login disabled; business database access denied for runtime.
- Business count/checksum snapshots matched for all eight tables. Counts:Products40/Orders15/Details37/Images41/Users3/Brands8/Categories2/Roles2.
- Business Web/API/SQL container IDs and StartedAt unchanged; all healthy.
- Initial provisioning guard stopped before database creation because container hostname differed from persisted SQL @@SERVERNAME. SELECT confirmed database/login absence; guard corrected to audited SQL identity before proceeding. No blind migration replay.
- NuGet vulnerability lookup was blocked in sandbox initially; scoped network retry succeeded. Final tool build has no warnings.

## Operations

From repository root:

```powershell
# Read-only configuration/status/permission verification:
.\scripts\acceptance\Compose.ps1 -Action Check
.\scripts\acceptance\Compose.ps1 -Action Status
.\scripts\acceptance\Run-Admin.ps1 -CheckOnly
```

`Initialize-Environment.ps1` is one-shot and refuses existing local state; do not rerun provisioning. No automatic cleanup/drop/reset is provided. Keep acceptance resources for the next approved step.

READY for planning/approval of lightweight EXEC-A. No automatic transition to business write acceptance.
