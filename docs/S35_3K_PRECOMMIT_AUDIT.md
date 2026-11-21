# S35.3K — Pre-Commit & Pre-Push Safety Audit (`S35.3A`–`S35.3J`)

- **Audit Date**: `2026-11-21`
- **Workspace**: `C:\PhoneStore\PhoneStore`
- **Repository**: `vuvantai14/PhoneStore`
- **Branch / HEAD**: `main` (`165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`, khớp 100% với `origin/main`)
- **Audit Scope**: Toàn bộ thay đổi chưa commit của chuỗi giai đoạn `S35.3A`–`S35.3J` (và báo cáo `S35.3K` này)
- **Final Decision**: **`GO — Safe to commit/push.`**

---

## A. Git Status

| Hạng mục kiểm tra | Kết quả thực tế | Trạng thái |
|---|---|---|
| Current branch | `main` | **PASS** |
| Local `HEAD` | `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` | **PASS** |
| Remote `origin/main` | `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07` (`HEAD == origin/main`, ahead 0 / behind 0) | **PASS** |
| Staged changes (`git diff --cached --stat`) | `0` file đã stage | **PASS** |
| Unstaged tracked changes (`git diff --stat`) | `1` file (`docs/PROGRESS.md`: `+22` dòng cập nhật tiến độ `S35.3A`–`S35.3K`, `0` dòng xóa) | **PASS** |
| Deleted tracked files | `0` file | **PASS** |
| Untracked files (`git status --short -uall`) | `27` file trước khi tạo báo cáo S35.3K (`28` tổng file thay đổi như hiển thị trên GitHub Desktop); **`28` file untracked (`29` tổng file thay đổi)** sau khi thêm `docs/S35_3K_PRECOMMIT_AUDIT.md` | **PASS** |
| Whitespace & conflict markers (`git diff --check`) | `0` lỗi định dạng hoặc conflict marker | **PASS** |
| Thay đổi ngoài phạm vi `S35.3A`–`S35.3K` | `0` file (không có thay đổi nào trong `src/`, `tests/`, `.github/`, hay các file `docker-compose*.yml` ở thư mục gốc) | **PASS** |

---

## B. Changed Files Inventory

Toàn bộ **29 file** (`1` Modified + `28` Untracked) đã được đọc và kiểm tra nội dung từng file:

### 1. CD Engine, Provisioning, Readiness Probe & Business CD Scripts (`10 files` — `scripts/cd/*.ps1`)

| STT | Đường dẫn file | Trạng thái Git | Số dòng / Bytes | Vai trò & Đánh giá nội dung |
|---|---|---|---|---|
| 1 | `scripts/cd/Rehearsal.Engine.ps1` | `??` (Untracked) | `184` dòng / `9,467` B | Core transactional CD engine (`Invoke-CdDeployment`, `Invoke-CdRollback`, `Write-CdJournalRecord`, `Read-CdJournalRecords`, low-memory `PausedLowMemory` guard). **PASS** |
| 2 | `scripts/cd/Rehearsal.Release.ps1` | `??` (Untracked) | `53` dòng / `3,325` B | Xác minh GitHub Actions run, SHA-256 artifact ZIP, `images.json` và GHCR multi-arch/amd64 digests (`Get-CdRelease`, `Test-CdManifest`, `Test-CdRegistry`). **PASS** |
| 3 | `scripts/cd/Rehearsal.Probe.ps1` | `??` (Untracked) | `526` dòng / `25,350` B | Internal readiness probe (`Assert-CdInternalReadiness`, `Test-CdServiceReadinessResponse`, `Invoke-CdDockerNetworkHttpGet`), phân biệt hợp đồng JSON API vs HTML Web, từ chối mock HTTP 200. **PASS** |
| 4 | `scripts/cd/Rehearsal.SqlFixture.ps1` | `??` (Untracked) | `918` dòng / `49,597` B | Công cụ provisioning SQL fixture cách ly với 6 lớp khóa an toàn, sinh T-SQL idempotent cho 2 migrations + synthetic catalog seed + least-privilege `cd_rehearsal` lockdown. **PASS** |
| 5 | `scripts/cd/Rehearsal.ResourceGate.ps1` | `??` (Untracked) | `428` dòng / `21,135` B | Phased Tiered RAM Gates, WSL2 `MemAvailable`/`SwapUsed` telemetry, `Get-CdBusinessInvariantsSnapshot` (`-SqlOnlyInvariant`), `Invoke-CdEmergencyRehearsalStop`, `Invoke-CdSequentialBootstrap`. **PASS** |
| 6 | `scripts/cd/Invoke-ReleaseRehearsal.ps1` | `??` (Untracked) | `172` dòng / `10,129` B | CLI orchestrator cho diễn tập full-stack cách ly (`phonestore-cd-rehearsal`). Dùng `%LOCALAPPDATA%\PhoneStore-CdRehearsal`. **PASS** |
| 7 | `scripts/cd/Invoke-SqlFixtureProvisioning.ps1` | `??` (Untracked) | `22` dòng / `880` B | CLI wrapper cho `Invoke-CdSqlFixtureProvisioning` (`ValidateOnly`, `Verify`, `Provision -ConfirmFixtureProvisioning`). **PASS** |
| 8 | `scripts/cd/Business.Cd.ps1` | `??` (Untracked) | `658` dòng / `36,389` B | Module pull-based CD cho stack business `phonestore-restore-c2`: kiểm tra `approved-release.json`, `deployment.lock`, `Test-CdBusinessSqlBackup`, `Assert-CdBusinessComposeCommandSafety`, `Assert-CdBusinessHttpReadiness`, `New-CdBusinessScheduledTaskSpec`. **PASS** |
| 9 | `scripts/cd/Invoke-BusinessCd.ps1` | `??` (Untracked) | `39` dòng / `1,542` B | CLI entrypoint cho business CD (`-ValidateOnly`, `-PollApprovedRelease`, `-ApplyApprovedRelease`, `-Recover`). **PASS** |
| 10 | `scripts/cd/Register-BusinessCdTask.ps1` | `??` (Untracked) | `38` dòng / `1,899` B | CLI kiểm tra/đăng ký Windows Scheduled Task quyền tối thiểu (`-ValidateOnly` mặc định; chặn `-Register` nếu không có `-ConfirmTaskRegistration`). **PASS** |

### 2. Docker Compose & Isolated Mock Rehearsal Fixtures (`4 files`)

| STT | Đường dẫn file | Trạng thái Git | Số dòng / Bytes | Vai trò & Đánh giá nội dung |
|---|---|---|---|---|
| 11 | `scripts/cd/docker-compose.rehearsal.yml` | `??` (Untracked) | `60` dòng / `1,978` B | Compose cho full-stack rehearsal cách ly (`name: phonestore-cd-rehearsal`, internal network `isolated`, volume `phonestore-cd-rehearsal_sql`, CPU/RAM limits). Không chứa secret. **PASS** |
| 12 | `scripts/cd/mock/Dockerfile` | `??` (Untracked) | `8` dòng / `292` B | Dockerfile của mock readiness container (`FROM python:3.13-alpine@sha256:9ef1a599...`, `USER 10001:10001`). **PASS** |
| 13 | `scripts/cd/mock/compose.yml` | `??` (Untracked) | `32` dòng / `742` B | Compose của mock rehearsal (`64m` RAM limit, `read_only: true`, `cap_drop: [ALL]`, internal network `mock`). **PASS** |
| 14 | `scripts/cd/mock/readiness.py` | `??` (Untracked) | `32` dòng / `1,145` B | HTTP responder tối giản phục vụ kiểm thử mock rehearsal. **PASS** |

### 3. Automated CD Safety & Unit Test Scripts (`3 files`)

| STT | Đường dẫn file | Trạng thái Git | Số dòng / Bytes | Vai trò & Đánh giá nội dung |
|---|---|---|---|---|
| 15 | `scripts/cd/Test-Rehearsal.ps1` | `??` (Untracked) | `878` dòng / `49,968` B | Bộ unit test & test doubles cho manifest/registry, 10 ca deployment engine, persistent disk journal recovery, internal readiness probe (P1–P9), SQL fixture safety guards (S1–S10) và Phased Tiered RAM Gates (R1–R4). **PASS** |
| 16 | `scripts/cd/Test-DockerMockRehearsal.ps1` | `??` (Untracked) | `209` dòng / `11,246` B | Kiểm thử tĩnh (`-ValidateOnly`) và diễn tập Docker mock cách ly 5 kịch bản. **PASS** |
| 17 | `scripts/cd/Test-BusinessCdReadiness.ps1` | `??` (Untracked) | `318` dòng / `18,978` B | Bộ kiểm thử cho Windows Scheduled Task spec, release approval/digest verification, SQL External Backup Gate, Business Compose command safety guards, execution lock và rollback safety. **PASS** |

### 4. Documentation & Technical Reports (`12 files` — `docs/`)

| STT | Đường dẫn file | Trạng thái Git | Số dòng / Bytes | Vai trò & Đánh giá nội dung |
|---|---|---|---|---|
| 18 | `docs/PROGRESS.md` | `M` (Modified) | `1,275` dòng / `221,310` B | Cập nhật nhật ký tiến độ các mốc `S35.3A`–`S35.3K` ở đầu file. **PASS** |
| 19 | `docs/S35_3A_WINDOWS_CD_DESIGN.md` | `??` (Untracked) | `76` dòng / `13,515` B | Báo cáo thiết kế kiến trúc Windows pull-based CD. **PASS** |
| 20 | `docs/S35_3B_CD_TOOLING_REHEARSAL.md` | `??` (Untracked) | `71` dòng / `7,042` B | Báo cáo công cụ CD và dry-run release `44845223690`. **PASS** |
| 21 | `docs/S35_3C_ISOLATED_DOCKER_READINESS.md` | `??` (Untracked) | `67` dòng / `8,898` B | Báo cáo đánh giá cách ly Docker & ngân sách RAM. **PASS** |
| 22 | `docs/S35_3D_DOCKER_MOCK_REHEARSAL.md` | `??` (Untracked) | `78` dòng / `7,413` B | Báo cáo diễn tập Docker mock 5/5 kịch bản. **PASS** |
| 23 | `docs/S35_3E_FULLSTACK_READINESS.md` | `??` (Untracked) | `98` dòng / `15,317` B | Báo cáo chuẩn bị full-stack rehearsal. **PASS** |
| 24 | `docs/S35_3F_PROVISIONING_AND_PROBE.md` | `??` (Untracked) | `129` dòng / `24,161` B | Báo cáo công cụ SQL fixture provisioning, internal probe & Phased Tiered RAM Gates. **PASS** |
| 25 | `docs/S35_3G_REAL_FULLSTACK_REHEARSAL.md` | `??` (Untracked) | `162` dòng / `18,679` B | Báo cáo nghiệm thu diễn tập Docker full-stack thật. **PASS** |
| 26 | `docs/S35_3H_WINDOWS_CD_READINESS.md` | `??` (Untracked) | `110` dòng / `16,623` B | Báo cáo sẵn sàng Windows pull-based CD cho business. **PASS** |
| 27 | `docs/S35_3I_EXTERNAL_BACKUP_AND_CD_PREP.md` | `??` (Untracked) | `44` dòng / `5,857` B | Báo cáo sao chép & xác minh SQL backup ngoài Docker volume. **PASS** |
| 28 | `docs/S35_3J_REAL_BUSINESS_DEPLOYMENT.md` | `??` (Untracked) | `203` dòng / `19,479` B | Báo cáo nghiệm thu triển khai thật PhoneStore API & Web lên `phonestore-restore-c2`. **PASS** |
| 29 | `docs/S35_3K_PRECOMMIT_AUDIT.md` | `??` (Untracked) | File hiện tại | Báo cáo kiểm toán an toàn pre-commit & pre-push S35.3K. **PASS** |

- **GitHub Actions / Runtime / Generated / Binary files trong Git status**: `0` file.
- **Đường dẫn cá nhân hardcode**: Toàn bộ script thực thi (`Business.Cd.ps1`, `Invoke-BusinessCd.ps1`, `Register-BusinessCdTask.ps1`, `Invoke-ReleaseRehearsal.ps1`) đều dùng biến môi trường động (`$env:LOCALAPPDATA`, `$PSScriptRoot`, `[Security.Principal.WindowsIdentity]::GetCurrent().Name`). Chuỗi `DESKTOP-ES9362P\Admikn` chỉ xuất hiện trong dữ liệu mẫu của unit test (`Test-BusinessCdReadiness.ps1:57`) và bằng chứng log thực thi trong báo cáo Markdown (`docs/S35_3H`, `docs/S35_3J`).

---

## C. Secret Scan

Đã quét toàn bộ 29 file thay đổi/mới và cây thư mục workspace:

| Hạng mục quét | Kết quả kiểm tra | Phân loại & Đánh giá |
|---|---|---|
| Mật khẩu `MSSQL_SA_PASSWORD` thật từ `.env` | `realSaLeak = False` trên toàn bộ 29/29 file | **PASS** (0 rò rỉ) |
| Connection strings chứa mật khẩu thật | `0` kết quả | **PASS** — Chỉ có biến môi trường `${CD_REHEARSAL_APP_PASSWORD:?Required}` trong `docker-compose.rehearsal.yml` và chuỗi test giả lập `Password=Rehearsal_App_Pwd_2026!C3` trong `Test-Rehearsal.ps1` |
| GitHub PAT (`ghp_`, `github_pat_`) | `0` token thật | **PASS** — Chỉ có chuỗi giả `ghp_123456789012345678901234567890` tại `Test-BusinessCdReadiness.ps1:98` dùng cho negative test xác nhận `Test-CdBusinessApprovalRecord` từ chối file approval chứa token |
| API keys / JWT secrets / Private keys | `0` kết quả | **PASS** |
| `.env` hoặc `.env.production` trong Git status | `0` file (đã được `.gitignore` dòng 27–28 chặn: `.env`, `.env.*`, `!.env.example`) | **PASS** |
| `approved-release.json`, `ephemeral-creds.clixml`, `journal.jsonl`, `deployment.lock`, `docker-compose.cd-override.yml` | `0` file trong cây thư mục repo (lưu ngoài repo tại `%LOCALAPPDATA%\PhoneStore-BusinessCd` và `%LOCALAPPDATA%\PhoneStore-CdRehearsal`; riêng 2 file `.clixml` cũ trong `scratch/acceptance-s323b/` đã bị chặn hoàn toàn bởi `.gitignore` dòng 6 `scratch/`) | **PASS** |
| File dữ liệu/backup SQL (`*.bak`, `*.mdf`, `*.ldf`, `*.bacpac`) | `0` file trong repo (bản backup nằm tại `C:\PhoneStoreDockerTransfer\backup` ngoài repo và `.gitignore` dòng 42–45 chặn `*.bak`, `*.bacpac`, `*.mdf`, `*.ldf`) | **PASS** |

---

## D. CI/CD Review

1. **GitHub Actions Workflow (`.github/workflows/ci.yml`)**:
   - Không bị sửa đổi trong đợt thay đổi này (`0` diff so với `HEAD`).
   - Toàn bộ `14/14` lệnh `uses:` đều ghim cứng commit SHA 40 ký tự hex.
   - Quyền workflow ở mức tối thiểu: `contents: read` ở cấp workflow; chỉ job `publish-ghcr` cấp `packages: write` khi `github.event_name == 'push' && github.ref == 'refs/heads/main'`.
   - Chuỗi phụ thuộc: `publish-ghcr` có `needs: [build-and-test, docker-build]`, đảm bảo chỉ đẩy image lên GHCR khi toàn bộ build/test Windows, kiểm tra `validate_business_compose.py` và build Docker image trên Ubuntu từ context sạch (`prepare_image_context.py`) đều `success`.
2. **Windows Pull-Based CD & Safety Gates (`scripts/cd/`)**:
   - **Approved Release Validation**: Yêu cầu đồng thời file phê duyệt cục bộ `%LOCALAPPDATA%\PhoneStore-BusinessCd\approved-release.json` (`AutoDeployUnapproved = false`) và đối chiếu chéo GitHub Actions run + SHA-256 artifact ZIP + GHCR manifest digests (`Test-CdBusinessApprovalRecord`, `Get-CdRelease`, `Assert-CdBusinessReleaseMatchesApproval`).
   - **Execution Lock**: Khóa file độc quyền `deployment.lock` (`FileShare.None`); trả về `SKIPPED_LOCKED` nếu có tiến trình khác đang chạy.
   - **Phased RAM / WSL2 ResourceGate**: Kiểm tra Windows Free RAM, WSL2 `MemAvailable`, WSL2 `SwapUsed` và container invariants trước/sau từng bước; tự động dừng và ghi `PausedLowMemory` (không recreate container) nếu RAM chạm ngưỡng khẩn cấp `< 1.50 GiB`.
   - **SQL External Backup Gate**: Bắt buộc xác minh sự tồn tại, kích thước (`827,392` bytes) và SHA-256 (`AA76E082...`) của bản backup ngày 19/11/2026 tại `C:\PhoneStoreDockerTransfer\backup` trước mọi thao tác deploy business (`Test-CdBusinessSqlBackup`).
   - **Readiness Probes, Persistent Journal & Automatic Rollback**: Triển khai tuần tự `api` trước -> đợi Docker `healthy` + HTTP 200 `/api/health/live`, `/api/health/ready`, `/api/v1/products` -> triển khai `web` sau -> đợi Docker `healthy` + HTTP 200 `/`, `/products`. Ghi bền vững từng pha vào `journal.jsonl` (`Flush($true)`) và tự động rollback về baseline images nếu bất kỳ bước nào thất bại.

---

## E. Docker Safety

| Hạng mục kiểm tra | Bằng chứng kỹ thuật | Trạng thái |
|---|---|---|
| Ghim digest cho API/Web/SQL images | `Business.Cd.ps1` bắt buộc regex `^sha256:[a-f0-9]{64}$`; SQL image ghim `mcr.microsoft.com/mssql/server@sha256:11927f5edc5122658e90b3a80ee36e8aef779310bf771415c1d89271859c73fd` | **PASS** |
| Bảo vệ SQL business container & volume | `Assert-CdBusinessComposeCommandSafety` chỉ cho phép `up -d --no-deps --no-build --pull never <api\|web>`; chặn tuyệt đối target `db` và các lệnh `down`, `prune`, `rm`, `kill`, `stop`, `restart`, `volume`, `system`, `build`, `exec`, `run` | **PASS** |
| Không tự động chạy migration/seed trên business | `Business.Cd.ps1` không chứa bất kỳ lệnh SQL hay EF migration nào; `Rehearsal.SqlFixture.ps1` khóa cứng 6 cổng chỉ cho phép project `phonestore-cd-rehearsal` và DB `PhoneStoreDB_CdRehearsal` | **PASS** |
| Bảo toàn Rollback baseline images | `sha256:91b13b7108a36e3687bf6578043b4cdb1513d4e0a4fc6e962c2819c5bdf8cb6a` (`api`) và `sha256:bbcd7442e1e590e07975c0a3801a763ebe7c8c5ca1df40eb21eefac438109fb0` (`web`) được giữ nguyên trong `docker-compose.net10.yml` và local Docker image cache | **PASS** |
| Không tự chạy business deploy khi push GitHub | `.github/workflows/ci.yml` chạy trên GitHub-hosted runners (`windows-2025`, `ubuntu-24.04`), không có self-hosted runner và không gọi bất kỳ script `scripts/cd/*` nào | **PASS** |

---

## F. Validation Results

Toàn bộ các bài kiểm tra tĩnh, unit tests và read-only Compose validation đã được chạy lại trong phiên audit S35.3K:

| Bài kiểm tra | Lệnh thực thi | Kết quả |
|---|---|---|
| PowerShell AST Parser (toàn bộ `scripts/`) | `[System.Management.Automation.Language.Parser]::ParseFile(...)` trên 20 file `.ps1` (`13/13` trong `scripts/cd/`) | **PASS (`20/20` scripts, `0` lỗi)** |
| GitHub Actions SHA Pinning Audit | Quét toàn bộ `uses:` trong `.github/workflows/ci.yml` | **PASS (`14/14` actions ghim 40-hex SHA)** |
| Rehearsal CD Unit & Test-Double Suite | `./scripts/cd/Test-Rehearsal.ps1` | **PASS (100% — manifest, 10 ca engine, journal, probe P1–P9, SQL fixture S1–S10, RAM gates R1–R4)** |
| Business CD Readiness Unit & Safety Suite | `./scripts/cd/Test-BusinessCdReadiness.ps1` | **PASS (100% — parser 13/13, Scheduled Task spec, approval/digest, live SQL backup gate, Compose safety, lock & rollback)** |
| Business Docker Compose Model Validation | `python scripts/ci/validate_business_compose.py` | **PASS (`Business Compose model validated (db, api, web).`)** |
| Mock Rehearsal Compose Isolation Validation | `./scripts/cd/Test-DockerMockRehearsal.ps1 -ValidateOnly` | **PASS (`PASS Compose isolation`)** |
| Full-Stack Rehearsal Compose Validation | `docker --context desktop-linux compose --env-file NUL -p phonestore-cd-rehearsal -f scripts/cd/docker-compose.rehearsal.yml config` | **PASS (`project=phonestore-cd-rehearsal, services=api,db,web, internalNet=True`)** |
| Whitespace & Git Diff Check | `git diff --check` | **PASS (`0` lỗi)** |
| Read-Only Business Runtime Invariants Check | `Get-CdBusinessInvariantsSnapshot` | **PASS** (`db-1` `addd99b34bf2...`, `api-1` `cec828cd68c7...`, `web-1` `ed6ef0f8027d...` đều `healthy`, `RestartCount = 0`) |

---

## G. Line Ending Review

- **Cấu hình `.gitattributes` & Git**:
  - `.gitattributes` định nghĩa `* text=auto` (chuẩn hóa line endings về `LF` trong Git index/repository).
  - Cấu hình Git cục bộ có `core.autocrlf=true`.
- **Kiểm tra byte-level trên toàn bộ 29 file thay đổi/mới**:
  - `29/29` file đều là **UTF-8 không BOM (`bom = False`)** và có **line endings đồng nhất `LF` (`0` file bị `MIXED` CRLF/LF)**.
  - Cảnh báo thông tin `warning: in the working copy of '...', LF will be replaced by CRLF the next time Git touches it` là hành vi chuẩn của Git trên Windows khi `core.autocrlf=true` kết hợp với `* text=auto`; Git sẽ lưu chuẩn `LF` trong object database khi commit. Không cần và không nên chạy chuyển đổi line endings hàng loạt.

---

## H. Commit File List

### 1. Commit Message Đề xuất
```text
feat(cd): complete Docker deployment and rollback pipeline
```

### 2. Danh sách 29 File Nên Đưa Vào Commit (`S35.3A`–`S35.3K`)
- **Modified (`1` file)**:
  1. `docs/PROGRESS.md`
- **New / Untracked (`28` files)**:
  2. `docs/S35_3A_WINDOWS_CD_DESIGN.md`
  3. `docs/S35_3B_CD_TOOLING_REHEARSAL.md`
  4. `docs/S35_3C_ISOLATED_DOCKER_READINESS.md`
  5. `docs/S35_3D_DOCKER_MOCK_REHEARSAL.md`
  6. `docs/S35_3E_FULLSTACK_READINESS.md`
  7. `docs/S35_3F_PROVISIONING_AND_PROBE.md`
  8. `docs/S35_3G_REAL_FULLSTACK_REHEARSAL.md`
  9. `docs/S35_3H_WINDOWS_CD_READINESS.md`
  10. `docs/S35_3I_EXTERNAL_BACKUP_AND_CD_PREP.md`
  11. `docs/S35_3J_REAL_BUSINESS_DEPLOYMENT.md`
  12. `docs/S35_3K_PRECOMMIT_AUDIT.md`
  13. `scripts/cd/Business.Cd.ps1`
  14. `scripts/cd/Invoke-BusinessCd.ps1`
  15. `scripts/cd/Invoke-ReleaseRehearsal.ps1`
  16. `scripts/cd/Invoke-SqlFixtureProvisioning.ps1`
  17. `scripts/cd/Register-BusinessCdTask.ps1`
  18. `scripts/cd/Rehearsal.Engine.ps1`
  19. `scripts/cd/Rehearsal.Probe.ps1`
  20. `scripts/cd/Rehearsal.Release.ps1`
  21. `scripts/cd/Rehearsal.ResourceGate.ps1`
  22. `scripts/cd/Rehearsal.SqlFixture.ps1`
  23. `scripts/cd/Test-BusinessCdReadiness.ps1`
  24. `scripts/cd/Test-DockerMockRehearsal.ps1`
  25. `scripts/cd/Test-Rehearsal.ps1`
  26. `scripts/cd/docker-compose.rehearsal.yml`
  27. `scripts/cd/mock/Dockerfile`
  28. `scripts/cd/mock/compose.yml`
  29. `scripts/cd/mock/readiness.py`

### 3. Những File Cần Loại Khỏi Commit / Cần Chỉnh Sửa Trước Khi Commit
- **File cần loại khỏi danh sách 29 file trên**: **`0` file** (các file nhạy cảm/runtime như `.env`, `scratch/`, `%LOCALAPPDATA%\PhoneStore-BusinessCd\*`, `%LOCALAPPDATA%\PhoneStore-CdRehearsal\*`, `C:\PhoneStoreDockerTransfer\backup\*.bak` đều đã nằm ngoài Git status).
- **File cần chỉnh sửa trước khi commit**: **`0` file**.

---

## I. Push Impact

Khi commit trên được push lên nhánh `main` của `vuvantai14/PhoneStore`:

1. **Kích hoạt GitHub Actions (`.github/workflows/ci.yml`)**:
   - Sự kiện `push` trên `main` sẽ chạy 3 jobs trên GitHub-hosted runners:
     - `build-and-test` (`windows-2025`): build & chạy 298 .NET tests + xuất artifact `phonestore-admin-winforms-win-x64`.
     - `docker-build` (`ubuntu-24.04`): kiểm tra `prepare_image_context.py`, `validate_business_compose.py` và build thử Docker images.
     - `publish-ghcr` (`ubuntu-24.04`): build & push cặp image `ghcr.io/vuvantai14/phonestore-api:sha-<new-commit>` và `ghcr.io/vuvantai14/phonestore-web:sha-<new-commit>` cùng artifact `phonestore-images-<new-commit>-run<new-id>.1`.
   - Lưu ý: Vì commit này chỉ thêm tài liệu `docs/` và công cụ `scripts/cd/` (không nằm trong danh sách allowlist của `scripts/ci/prepare_image_context.py`), nội dung mã nguồn ứng dụng bên trong image mới là tương đương logic với commit `165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`.
2. **Không tự động triển khai hay tác động tới hệ thống Business (`phonestore-restore-c2`)**:
   - Push lên GitHub **không** gọi máy host Windows, **không** kích hoạt Windows Scheduled Task (`Registered PhoneStore scheduled tasks: 0`), và **không** thay đổi file `%LOCALAPPDATA%\PhoneStore-BusinessCd\approved-release.json` (hiện đang ghim ở `runId = 44845223690`, `commit = 165b1fce1c69cf40bc6874e0c4f7d9eb90d44f07`, trạng thái `NOOP_ALREADY_DEPLOYED`).
   - Hệ thống Docker business (`phonestore-restore-c2-db-1`, `api-1`, `web-1`) tiếp tục chạy ổn định trên bản release đã phê duyệt ở S35.3J mà không bị restart hay recreate.

---

## J. Final GO/NO-GO

- **Trạng thái kiểm toán tổng thể**: **PASS (`0` BLOCKED, `0` WARNING)**
- **Quyết định cuối cùng**:

**`GO — Safe to commit/push.`**
