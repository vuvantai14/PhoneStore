# S31.5C2D1.1 — Docker SQL Resource & Shutdown Safety

Ngày xác minh: 2026-11-14  
Kết luận: **CONFIG PASS / D2 NO-GO pending memory headroom**

Không start/stop container, restore, migration, seed, volume deletion, Docker Desktop resource change hoặc database mutation trong task này.

## A. Compose Shutdown Configuration

Only permitted source change:

```yaml
services:
  db:
    stop_grace_period: 60s
```

`docker compose -f docker-compose.yml -f docker-compose.restore.yml config --quiet` PASS. Rendered value is `1m0s`.

Unchanged and verified:

- project `phonestore-restore-c2`;
- SQL image digest `sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd`;
- port `127.0.0.1:14334→1433`;
- data volume key `mssql_data` (project-isolated actual volume);
- backup source `C:/PhoneStoreDockerTransfer/backup` → `/var/opt/mssql/backup`, read-only.

Important: the existing stopped container was created before this config change. `docker start` would not apply the new grace setting. D2 must receive explicit permission to let Compose recreate the target **container only** from the updated config while retaining `phonestore-restore-c2_mssql_data`; no volume removal.

## B. Host Memory

Current host snapshot:

- total RAM 15.77 GB;
- free RAM 4.06 GB;
- no target project container running.

Largest working sets observed:

| Process group | Representative working sets | Recommendation |
|---|---|---|
| `vmmemWSL` | ~948 MB | Keep; required by Docker/WSL |
| Chrome | ~529, 452, 225, 224 MB | User may close unused tabs/windows |
| VS Code | ~513, 393, 324, 223 MB | User may close unused windows/extensions sessions |
| Memory Compression | ~456 MB | System-managed; do not stop |
| Defender (`MsMpEng`) | ~364 MB | Security/system; do not stop |
| Explorer | ~220 MB | Keep unless user chooses otherwise |

No process was stopped automatically.

D1 evidence showed free RAM decreased from4.00 GB before target start to1.71 GB while healthy—a 2.29 GB reduction, larger than the container's reported1.201 GiB working usage due host/Docker/cache/background effects. A robust D2 gate is therefore:

- pre-start free RAM ≥5 GB;
- after SQL is healthy and settled, free RAM ≥3 GB before restore;
- stop safely before restore if either gate fails.

## C. Exit 137 Analysis

Observed prior target state:

- exit code137;
- `OOMKilled=false`;
- restart count0;
- empty `.State.Error`;
- no matching shutdown/termination/error event in available SQL log tail.

Exit137 conventionally represents process termination by SIGKILL (128+9). `OOMKilled=false` rules out Docker marking the container as an OOM kill, but does not identify who issued SIGKILL or prove exact timing. Because the container was stopped under Compose's previous default grace and SQL did not log graceful completion, exceeding grace is **consistent with** the evidence, not conclusively proven.

The 60-second grace is a preventive configuration. Its effectiveness remains NOT TESTED until a later controlled target stop; do not claim the prior issue resolved solely from static config.

## D. Docker Isolation

- target `phonestore-restore-c2-db-1`: Exited(137), not running;
- target `phonestore-restore-c2_mssql_data`: exists;
- target project network: exists;
- old PhoneStore containers remain Exited;
- old `phonestore_mssql_data` remains untouched;
- Windows `PhoneStoreDB`: ONLINE.

No conflict or unexpected running project service was found.

## E. Recommendations

1. User closes unused Chrome tabs/windows and extra VS Code windows; do not kill processes programmatically.
2. Recheck pre-start RAM; require ≥5 GB based on observed D1 drop.
3. In approved D2, use Compose to recreate only target DB container so `stop_grace_period` applies, retaining target volume/network.
4. Confirm healthy, backup mount/hash, target DB absent and post-start free RAM ≥3 GB before `RESTORE DATABASE`.
5. After restore validation/persistence acceptance, perform a controlled 60-second stop and require exit0/graceful SQL logs. If it still exits137, investigate Docker events/SQL shutdown latency before further use.

## F. GO / NO-GO for S31.5C2D2

### Configuration

**GO:** shutdown configuration and static Compose isolation are ready.

### Execution

**NO-GO now:** current 4.06 GB free does not meet the evidence-based ≥5 GB pre-start gate. The target container also needs an approved Compose recreation for the new grace period.

D2 can become conditional GO after:

- user closes unnecessary applications and RAM recheck is ≥5 GB;
- user explicitly approves recreating only target DB container (volume retained) and executing one guarded restore;
- after start, RAM remains ≥3 GB and all previous backup/target absence gates still PASS.
