# S31.5C2D1 — Isolated Docker SQL Pre-Restore Verification

Ngày thực hiện: 2026-11-14  
Mode: isolated DB start + backup verification; no restore  
Kết luận: **PARTIAL — verification PASS / D2 NO-GO due resource and shutdown gates**

## A. Docker Environment

Preflight:

- Docker Engine 29.9.2, Linux containers;
- host RAM free 4.00 GB, disk C free 305.62 GB;
- port14334 free;
- old `phonestore-web` Exited(0), old `phonestore-db` Exited(137);
- old `phonestore_mssql_data` exists and was not touched;
- target project containers/volume absent;
- backup copy 732,160 bytes and SHA-256 matched expected;
- base + restore override `docker compose config --quiet` PASS.

Only this command scope was executed: start service `db` for project `phonestore-restore-c2` with `--no-deps`. Web/API were neither started nor created.

## B. Isolated SQL Container

Compose created only:

- container `phonestore-restore-c2-db-1`;
- volume `phonestore-restore-c2_mssql_data`;
- network `phonestore-restore-c2_phonestore-net`.

The container became healthy on first poll and listened only at `127.0.0.1:14334→1433`.

Verified runtime:

- project label `phonestore-restore-c2`, service `db`;
- pinned image digest `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd`;
- target SQL Server 2022 Developer `16.0.4315.2`;
- target `DB_ID('PhoneStoreDB')` = NULL before and after verification.

No `RESTORE DATABASE`, migration, seed or target user database was created.

## C. Backup Mount Accessibility

Runtime mounts:

| Mount | Target | Mode |
|---|---|---|
| isolated named volume | `/var/opt/mssql` | RW |
| `C:/PhoneStoreDockerTransfer/backup` | `/var/opt/mssql/backup` | **read-only** |

Inside target container:

- backup readable: PASS;
- physical size: 732,160 bytes;
- SHA-256 match: PASS (`D806D221FD49FEFB9187C51D6EF0781F653312FF3606726309055F60B007D4E2`).

No backup file was modified, copied or overwritten during D1.

## D. HEADERONLY / FILELISTONLY

`RESTORE HEADERONLY` PASS:

- database `PhoneStoreDB`;
- backup type FULL/Database;
- compressed=true, COPY_ONLY=true, checksums=true, damaged=false;
- compatibility160, recovery FULL;
- source SQL 16.0.1000, target16.0.4315.2 (same major, target newer);
- backup finish `2026-11-14 08:44:18` source local time;
- backup payload6,701,056 bytes, compressed payload730,566 bytes.

`RESTORE FILELISTONLY` PASS:

| Logical name | Type | Original size | Restore target planned for D2 |
|---|---|---:|---|
| `PhoneStoreDB` | data/PRIMARY | 8,388,608 | `/var/opt/mssql/data/PhoneStoreDB.mdf` |
| `PhoneStoreDB_log` | log | 8,388,608 | `/var/opt/mssql/data/PhoneStoreDB_log.ldf` |

Windows physical paths in backup metadata are expected source paths and will not be reused on Linux; D2 must use `WITH MOVE`.

## E. VERIFYONLY WITH CHECKSUM

Target-container command `RESTORE VERIFYONLY ... WITH CHECKSUM` returned:

`The backup set on file 1 is valid.`

This closes the destination ACL gap from C2C.1. Backup is readable and valid from the exact SQL Server/container/mount intended for restore.

## F. Resource Usage

While target SQL was healthy:

- target container memory: 1.201 GiB / Docker allocation 7.639 GiB;
- host free RAM dropped from4.00 GB to1.71 GB;
- disk free remained about305.56 GB.

Because free RAM fell below the 3 GB safety gate, D1 stopped only the target DB after verification. No container/volume/network was deleted.

Post-stop:

- target container Exited(137), `OOMKilled=false`, restart count0;
- target volume/network remain;
- host free RAM recovered to2.37 GB;
- old PhoneStore containers/volume remain unchanged.

Exit137 indicates SQL did not finish inside Compose's default stop grace, not an OOM kill. Before D2/persistence testing, add/approve a longer SQL `stop_grace_period` (recommended60s) and verify graceful exit.

Source Windows `PhoneStoreDB` post-check: ONLINE; row counts unchanged Roles2/Users3/Brands8/Categories2/Products40/ProductImages41/Orders12/OrderDetails28.

## G. GO / NO-GO for S31.5C2D2

### PASS gates

- isolated target runtime/image/port/volume/network;
- backup mount read-only and hash match;
- target HEADERONLY, FILELISTONLY and VERIFYONLY WITH CHECKSUM;
- target database absent;
- source database/data unchanged;
- no Web/API startup.

### Decision

**NO-GO for D2 restore at current resource state**, despite backup verification PASS.

D2 requires separate explicit approval plus:

1. recheck host free RAM ≥3 GB, prefer≥4 GB before target start;
2. approve/apply SQL `stop_grace_period: 60s` to avoid another forced exit;
3. restart only existing isolated target DB/container and confirm healthy/mount/target DB absent;
4. execute exactly one guarded `RESTORE DATABASE ... WITH MOVE`, no `REPLACE`, only after those gates;
5. stop immediately if target DB appears unexpectedly or resource gate fails.
