# PhoneStore workspace instructions

Workspace: C:\PhoneStore\PhoneStore.

## Authorized routine work
- Read and edit project files needed for the requested task within this workspace.
- Run dotnet restore PhoneStore.sln, dotnet build PhoneStore.sln, and ordinary tests that use isolated test data. Do not repeatedly ask for permission for these routine actions inside the sandbox.
- Use the sandbox normally. Request a narrowly scoped exception when NuGet downloads, the SDK/package cache, or another required resource is blocked. Do not broaden filesystem or network permissions globally.
- Prefer read-only SQL checks against localhost / PhoneStoreDB with Windows Authentication.

## Actions requiring explicit user authorization
- Deleting user/source files or directories, removing data, resetting a database, or running destructive cleanup. Ordinary build-generated temporary artifacts are permitted.
- Changing schema or data in an existing database, applying EF migrations to an existing database, seeding users, or running imports against a live database. Existing PhoneStoreDB must be preserved; do not rerun migrations just to test connectivity.
- Destructive Git actions, including reset --hard, clean, history rewriting, discarding user changes, or force pushing.
- Committing, pushing, publishing, or deploying.
- Accessing unrelated paths outside this workspace, except tool/runtime files required by the approved development workflow and installed skill instructions. Any blocked access still requires sandbox approval.

Never use EnsureDeleted or database drop for environment recovery. Do not create a migration without diagnosing the need first. Do not enable danger-full-access, never approvals, or blanket shell/dotnet/sqlcmd allow rules. Keep credentials out of Git. Preserve unrelated user changes and application behavior/UI.

These are agent instructions, not a database firewall or a replacement for the filesystem sandbox. Never claim all destructive commands are technically blocked by workspace-write.

## Persistent progress and session workflow
- `docs/PROGRESS.md` is the primary source of current project progress. `CONTEXT.md` provides architecture/background and historical milestones; keep its Current Progress summary short and linked to PROGRESS.md. Neither overrides source code, actual test evidence or user authorization.
- At the start of each session, read `docs/PROGRESS.md` before planning or inspecting source files.
- Read only source/configuration files directly relevant to the current task. Do not rescan the entire repository or repeat a full inventory while PROGRESS.md remains applicable.
- Check freshness using the task context and targeted evidence. If progress conflicts with source code or actual test results, prioritize source and tests; correct the affected progress entry rather than assuming completion.
- Distinguish VERIFIED, PARTIAL, NOT IMPLEMENTED and UNKNOWN. Record the verification date and scope; historical database counts and test results are not live guarantees.
- After each completed task, update `docs/PROGRESS.md` concisely (approximately 150 lines maximum). Record the before/after status, changed files, actual test commands/results, remaining errors/risks and next step. For paused work, record the unfinished state before handing off.
- Keep only useful current facts and milestone evidence; do not copy chat transcripts, long logs, passwords, hashes or other secrets into progress notes.
- For documentation-only tasks, record that tests were not rerun. Never invent validation results or rerun database writes to refresh progress.
- No commit, push, Git reset or database change without explicit authorization. Existing safety rules above continue to apply.

## GitHub checkpoint workflow
- Work code-first: read only `docs/PROGRESS.md`, the current task report and directly affected source/configuration files. Do not repeat repository-wide audits or checks that remain valid and are unaffected by the new diff.
- Prefer targeted build/tests while iterating. Run full regression only for broad/shared changes or an important acceptance checkpoint.
- After a stable requested stage has appropriate build/tests, create a scoped Git checkpoint on the current branch and push it to the already configured verified remote. The user authorized this ongoing checkpoint workflow on 2026-11-02; report the commit hash and push result. If authentication/push fails, keep the local commit and report the error.
- Before each checkpoint, inspect status/diff and exclude unrelated user changes, secrets, runtime data, database files/backups, `bin/`, `obj/`, local tooling and caches. Never create or change a remote/repository implicitly.
- Use concise stage-based Conventional Commit messages such as `feat(ui): complete S29.9B customer web foundation`.
- Never force-push, rewrite history, reset, clean, discard changes, auto-revert or auto-rollback. A Git checkpoint replaces routine manual source/DLL/runtime backups; make SQL backups only for approved risky database operations.
- Keep completion reports short: changed files, targeted validation, remaining issue, commit hash and push outcome. Do not generate manifests, hashes or long reports unless the task or concrete risk requires them.
- This workflow does not authorize deploy/restart, database/schema/data changes, destructive operations, framework/business-scope changes, or committing secrets/runtime data. Those existing approval rules remain in force.
