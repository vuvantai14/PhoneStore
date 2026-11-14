# S31.5C2D1.2 — Docker SQL Runtime Safety Gate

Ngày thực hiện: 2026-11-14  
Kết luận: **FAIL runtime safety gate / NO-GO for D2 restore**

Task recreated and ran only isolated service `db`; no restore, migration, seed, Web/API startup, source DB change, volume deletion or Git operation.

## A. Pre-start RAM

- First measurement attempt used an invalid CIM property and returned0; gate stopped before Docker action. This was a measurement error, not actual RAM.
- Correct measurement: **5.52 GB free**, meeting the ≥5 GB pre-start gate.
- Disk/port/config gates: port14334 free, backup hash correct, Compose config PASS.
- Old PhoneStore containers Exited; old volume retained.
- Target volume/network existed from D1, target container stopped, no target service running.

## B. Container Recreate

User authorized recreation of only target service `db`. Command scope used Compose `--no-deps --force-recreate db`:

- recreated `phonestore-restore-c2-db-1` only;
- retained `phonestore-restore-c2_mssql_data` and project network;
- did not create/start Web or API;
- retained pinned SQL image and read-only backup bind mount.

No target user database existed before/after startup (`DB_ID('PhoneStoreDB') = NULL`).

## C. SQL Health

- Target became `running|healthy` on first poll.
- SQL connection PASS; version `16.0.4315.2` Developer.
- `OOMKilled=false`, restart count0 while running.
- Backup mount remained read-only; data volume remained isolated RW.

No backup metadata/restore command was needed because D1 already verified it and this task was a runtime resource gate.

## D. Runtime Shutdown Configuration

Runtime inspect confirmed `StopTimeout=60`, so the recreated container received the new configuration.

When post-start RAM failed, a Compose stop was initiated. The tool session detached while Docker still showed the container running. A direct target-only Docker stop with 60-second timeout was then used to complete the required safe-stop path; no other container was targeted.

Final state:

- Exited(137);
- `OOMKilled=false`;
- restart count0;
- no `.State.Error`;
- no clear graceful SQL shutdown record in available log tail.

Exit137 is compatible with SIGKILL termination, but available evidence does not identify the sender or prove whether Docker's timeout, entrypoint signal handling, or SQL shutdown latency is the root cause. The fact that runtime StopTimeout=60 was present but exit remained137 means static grace configuration alone is insufficient evidence of safe shutdown.

## E. Post-start RAM

| Checkpoint | Host free RAM |
|---|---:|
| Before recreate | 5.52 GB |
| Target healthy | **1.94 GB** |
| After target stopped | 2.17 GB |

Target SQL reported about1.152 GiB memory, but host free RAM fell3.58 GB due combined Docker/WSL/cache/background effects. The mandatory ≥3 GB post-start gate failed.

## F. Resource Safety

- Target container stopped; target volume/network/container retained.
- No target project service running.
- Windows `PhoneStoreDB` remains ONLINE.
- Old PhoneStore containers/volume remain untouched.
- No automated user-app shutdown or Docker Desktop memory change.

User can close additional unused Chrome/VS Code windows, but the observed drop shows pre-start5 GB may still be insufficient. A future retry should require materially higher headroom and repeat both pre/post gates.

## G. GO / NO-GO for S31.5C2D2

**NO-GO.** Two independent safety conditions failed:

1. post-start free RAM1.94 GB < required3 GB;
2. target SQL still exited137 despite runtime StopTimeout60 and no OOM marker.

Before any restore approval/retry:

- free substantially more host RAM; target ≥6–7 GB pre-start based on this observed3.58 GB drop, then require ≥3 GB after healthy;
- diagnose target image PID1/stop signal and capture Docker events/full SQL logs during a controlled no-restore start/stop test;
- require graceful exit0 or documented SQL shutdown completion before restoring user data.

Do not RESTORE DATABASE while either blocker remains.
