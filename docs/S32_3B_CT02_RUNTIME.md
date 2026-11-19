# S32.3B-CT02-RUNTIME — PASS tested scope / PARTIAL full matrix

Verified 2026-11-16 using real HTTP cookie authentication, browser-session semantics and antiforgery tokens against acceptance port5285. No browser UI PASS claimed.

## Deployment scope

- Built only Web image `phonestore-acceptance-s323b-web:ct02-fix01` using existing Dockerfile.
- Validated original acceptance Compose plus image-only JSON override through stdin; no configuration file changes.
- Recreated only acceptance Web with `up -d --no-deps --no-build --force-recreate web`.
- Web ID `92708e866da4` -> `84066ac4b44f`; new StartedAt `2026-11-16T09:54:30.666414313Z`, image `sha256:aeddfa741bc08488a4ead664f73033ac42f9a2287633cb9b99d89ac5fd655d42`, healthy.
- Acceptance API ID `b47fefaf6c87` / StartedAt `2026-11-16T07:15:52.693162744Z` unchanged. Business Web/API/SQL IDs and StartedAt unchanged, healthy.
- All acceptance connections verified against SQL identity `7f8d4ad3701c`, database `PhoneStoreDB_AcceptanceTest`; runtime login cannot access business database. No migration, permissions change or SQL configuration changes.

## Runtime results

| Case | Result | Evidence |
|---|---|---|
| RT01 revoke | PASS HTTP+SQL | User manually disabled synthetic Customer4 via acceptance Admin. Existing profile request redirected to Login. Anonymous and B cart count0; B token differs; A token POST rejected. SQL snapshot unchanged across HTTP requests. |
| RT02 same owner | PASS HTTP+SQL | Cart/token preserved across Home/Products/Profile/Cart requests. No SQL mutation. |
| RT03 anonymous | PARTIAL | Anonymous cart2 adopted at login and customer token available. Anonymous checkout redirects to Login, so an anonymous token cannot be obtained through normal HTTP workflow. Anonymous token rotation tested automatically, not runtime-proven. Initial harness missing empty-cart CSRF resolved by using real Product Detail form token. |
| RT04 logout | PASS HTTP+SQL | Explicit logout clears cart; loginB same session sees empty cart; token differs and old token POST rejected. No order created. |
| RT05 legacy | NOT VERIFIED | No valid fixture for injecting ownerless session without direct storage/cookie manipulation. Actual recreate discards in-memory session state, but rolling deployment/persistent ownerless migration remains unverified. |

RT01 used Customer4 (new account from A4) as A to avoid modifying baseline CustomerA/B. It was restored active by user through Admin; SELECT confirmed User4/Role2/active. No passwords/cookies/tokens recorded.

## Integrity and resources

- Acceptance eight-table count/checksum final exactly matches pre-deployment baseline after the approved Customer4 disable/enable cycle.
- Counts: Products5/Orders4/Details4/Images2/Users4/Brands4/Categories3/Roles2. No checkout succeeded, no stock changes.
- Business eight-table count/checksum unchanged; Products40/Orders15.
- Upload manifest digests unchanged: business42 files; acceptance5 files. No cleanup.
- RAM available preflight2.79GiB; checkpoints remained above2GiB; final2.39GiB. Every relevant service healthy. No stress/concurrency/fault injection.

## Remaining operation risk

At the time of this runtime test, acceptance Compose still pinned the old Web image. The previous `Compose.ps1 -Action Start` ran `compose up -d --no-build`, which could recreate Web on that old image.

S32.3B-CT02-STABILIZE pins the accepted image in `scripts/acceptance/docker-compose.yml`. `Compose.ps1 -Action Check` validates the config and prints the locally verified Web pin. `-Action Start` verifies both existing containers against their pins before starting either one, then waits for API health before starting Web. To adopt a replacement image, update the pin after acceptance and handle container replacement separately with approval.

Business port5015 still runs the prior binary. No business deployment was performed.

## Final gate

Original CT02 revoke defect reproduced previously is now PASS on acceptance runtime. Full RT01–RT05 matrix remains PARTIAL because RT03 anonymous token branch and RT05 legacy ownerless sessions are not runtime verified. Automated regression covers disabled/role/expiry and anonymous token rotation (247/247 full suite PASS in FIX01, not rerun here).

S32.3B-CT02-STABILIZE classified the RT03 anonymous token branch as NOT APPLICABLE to the HTTP workflow. Customer authorization gates the sole token-issuing action. RT05 confirms that an ownerless legacy cart remains visible as a guest cart and can be adopted at login. The legacy token is cleared on login. The current in-memory store discards sessions at process replacement. A future persistent or mixed-version rollout needs a session invalidation boundary before accepting old payloads.

EXEC-A4 CT02 known runtime defect resolved in acceptance; SE01 remains blocked without an Admin-revoke workflow, RP01 boundary-day remains partial. Stop; no EXEC-B, commit or push.
