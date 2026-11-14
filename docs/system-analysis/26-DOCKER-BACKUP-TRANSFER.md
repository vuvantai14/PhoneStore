# S31.5C2C.1 — Safe Backup Transfer

Ngày preflight: 2026-11-14  
Mode: controlled backup copy + verification  
Trạng thái: **PARTIAL — COPY INTEGRITY PASS / WINDOWS SQL VERIFY BLOCKED BY DESTINATION ACL**

## A. Preflight

- Branch `main`, HEAD `c6de0474731a1e2397ff77f3790743cbda99c33b`; giữ nguyên toàn bộ changes S31.5A–C2C.
- Source backup qua SQL Server: exists, đúng một backup set, compressed 730,566 bytes, COPY_ONLY=1, checksum=1.
- Windows `PhoneStoreDB`: ONLINE.
- Old `phonestore-web`: Exited(0); old `phonestore-db`: Exited(137); không restart.
- Disk C free: 305.66 GB.
- Không copy, restore, start container, migration/seed, volume mutation, commit/push trong preflight.

## B. Source and Destination

Source:

`C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup\PhoneStoreDB_DOCKER_COPYONLY_20261114_014304166Z_AEB0E519BE794420B7747A5029FE36A5.bak`

Destination proposed outside repository:

`C:\PhoneStoreDockerTransfer\backup\PhoneStoreDB_DOCKER_COPYONLY_20261114_014304166Z_AEB0E519BE794420B7747A5029FE36A5.bak`

Destination directory and file do not exist. Source raw-file read by current user returns AccessDenied; SQL Server can read/VERIFY the source. No source ACL will be changed.

## C. Copy Approval

**APPROVED by user.** Approved method:

1. Launch one narrowly scoped elevated PowerShell operation.
2. Create only `C:\PhoneStoreDockerTransfer\backup`.
3. Restrict destination ACL to current Windows user, `SYSTEM`, and local Administrators; do not touch source ACL.
4. Copy exactly the source to the exact destination with overwrite disabled (`File.Copy(..., false)`).
5. Do not move/delete/rename source and do not copy any other file.

Required privilege: Administrator read access to SQL default backup directory plus create/write/ACL permission on the new transfer directory. Docker Desktop runs under the current user, which retains read access to the destination.

## D. Transfer Result

Initial automated copy safely stopped before mutation because the tool process lacked an Administrator token. The user then performed the approved manual elevated single-file copy.

Final destination:

`C:\PhoneStoreDockerTransfer\backup\PhoneStoreDB_DOCKER_COPYONLY_20261114_014304166Z_AEB0E519BE794420B7747A5029FE36A5.bak`

Post-check after manual copy:

- destination directory/file exist: true;
- destination readable by current user: true;
- source physical bytes reported by user's elevated copy: 732,160;
- destination physical bytes: 732,160;
- source/destination hash match: true;
- destination SHA-256: `D806D221FD49FEFB9187C51D6EF0781F653312FF3606726309055F60B007D4E2`;
- SQL Server still sees source file: true;
- source `PhoneStoreDB`: ONLINE;
- old Docker containers: still Exited.

No source ACL/content change, overwrite, move or delete was performed.

## E. Backup Verification

### A. Backup integrity

- Physical file size: 732,160 bytes, matches source physical size.
- Destination SHA-256 matches source: PASS.
- Current-user read test: PASS.

SQL `compressed_backup_size=730,566` is not expected to equal the physical `.bak` length. It measures compressed backup payload; the physical media file additionally contains media/header, block framing and alignment/padding. Difference: 1,594 bytes. The 732,160-byte physical file is 512-byte aligned, consistent with SQL backup media blocks; this is not evidence of corruption.

### B. SQL verification

Windows SQL `RESTORE VERIFYONLY WITH CHECKSUM` on the destination returned OS error 5 Access denied. Per policy, ACL was not changed and HEADERONLY/FILELISTONLY were not attempted against the inaccessible copy.

Evidence chain remains strong but not complete at destination SQL layer:

1. source artifact VERIFYONLY/HEADERONLY/FILELISTONLY PASS in C2A;
2. manual source/destination physical sizes match;
3. SHA-256 hashes match exactly;
4. destination is readable by Docker Desktop user.

Target SQL container must run `RESTORE VERIFYONLY WITH CHECKSUM`, HEADERONLY and FILELISTONLY from `/var/opt/mssql/backup/...bak` before any `RESTORE DATABASE`. Failure is a hard stop.

### C. Backup metadata comparison

Metadata from verified source C2A remains applicable because physical size and SHA-256 are identical:

- database `PhoneStoreDB`, FULL backup;
- compressed=true, COPY_ONLY=true, checksums=true, damaged=false;
- source SQL `16.0.1000`, compatibility160, recovery FULL;
- backup start/finish `2026-11-14 08:44:18` local;
- logical files `PhoneStoreDB` (data) and `PhoneStoreDB_log` (log);
- compressed payload730,566 bytes; physical media732,160 bytes.

## F. Restore Override Readiness

Created `docker-compose.restore.yml` containing only DB restore-specific overrides:

- pinned SQL image digest `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd`;
- bind mount `C:/PhoneStoreDockerTransfer/backup` → `/var/opt/mssql/backup`, read-only.

Base + override static validation PASS:

- project `phonestore-restore-c2`;
- isolated volume `phonestore-restore-c2_mssql_data`, not yet created;
- loopback SQL port `127.0.0.1:14334→1433`;
- backup source exists and mount is read-only;
- target containers count 0;
- services `db`, `api`, `web` render correctly.

No container/image build/volume creation occurred.

## G. Resource Readiness

- Host free RAM: 3.19 GB (minimum gate3 GB; preferred4 GB).
- Disk C free: 305.63 GB.
- Port14334: free.
- Old PhoneStore containers: Exited.
- Target project containers: 0; target volume absent.

Recheck immediately before start; if RAM drops below3 GB or target port/object appears, stop.

## H. Remaining Risks

1. Windows SQL service cannot read restricted destination; container-side verification is mandatory.
2. Docker bind-mount readability is only statically configured, not runtime-proven.
3. Backup contains PII and must remain outside Git/public paths.
4. Host free RAM is 3.19 GB: above minimum 3 GB but below preferred 4 GB; recheck immediately before start and run sequentially.

## I. GO / NO-GO for S31.5C2D

**PARTIAL / CONDITIONAL GO for S31.5C2D pre-restore runtime verification only.**

C2D may start the isolated target DB only after separate approval and resource/port/object recheck. It must first prove the read-only mount and run target-container `VERIFYONLY WITH CHECKSUM`, HEADERONLY and FILELISTONLY. **NO-GO for `RESTORE DATABASE` until all three target checks PASS.**
