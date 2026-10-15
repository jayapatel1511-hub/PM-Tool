# Plan: Operations and Security Hardening

Implements §48 with defaults D1 to D11. Fifteen small slices, one pull request each, ordered in `tasks.md`. No new service, package or dependency. One additive migration.

## Approach

- Fix each gap where every caller routes through it: `ApiError` and `api()` for client messages, `IdempotencyMiddleware` for keys, `ProcessOnce` and `OpsChecks` for the outbox. No per-screen patches.
- Server tests run on real PostgreSQL through `tests/Hub.Tests/Api/Factory.cs`. Client behaviour is checked by one new mocked browser suite, `web/tests/operations.cjs` (`npm run test:operations`), which each client slice extends. Script behaviour is checked by Python `unittest` files beside the scripts.
- Operator work on homedev (S14, S15) follows `docs/runbooks/restore.md` and needs Jay's sudo and approval. Agents prepare scripts and tests; they never run root commands or copy pilot data.

## File boundaries

| Slice | Files |
|---|---|
| S01 served files | `web/public/landing/capture.json`, `web/public/landing/README.md`, `web/public/landing/fonts/sources.json` moved to `docs/landing-assets/`; new `tools/check_served_assets.py`, `tools/test_served_assets.py`, new `tests/Hub.Tests/Api/ServedFilesTests.cs`; `tools/ci_steps.txt` (or `.github/workflows/ci.yml` before packet 039's list exists) |
| S02 dead-letter server | `src/Hub.Api/Data/Entities.cs` (`OutboxEvent.DeadLetteredAt`), `src/Hub.Api/Data/HubDb.cs`, new migration under `src/Hub.Api/Data/Migrations/` with its snapshot, `src/Hub.Api/Infrastructure/EvaluationService.cs`, `src/Hub.Api/Infrastructure/Operations.cs`, `src/Hub.Api/Program.cs` (`/health`), `src/Hub.Api/Features/Admin.cs` (GET operations), `tests/Hub.Tests/Api/OperationsTests.cs` |
| S03 retry UI | `src/Hub.Api/Features/Admin.cs` (retry route), `web/src/pages/admin/Operations.tsx`, `web/src/i18n/en.ts`, new `web/tests/operations.cjs`, `web/package.json`, `tools/ci_steps.txt`, `tests/Hub.Tests/Api/OperationsTests.cs` |
| S04 error page, trace ref | `web/src/main.tsx`, new `web/src/components/hub/error-page.tsx`, `web/src/lib/api.ts`, `web/src/i18n/en.ts`, `web/tests/operations.cjs` |
| S05 client-error log | `src/Hub.Api/Features/Modules.cs` (route beside `/config`), `src/Hub.Api/Program.cs` (rate-limit policy), new `web/src/lib/client-errors.ts`, `web/src/main.tsx`, `web/src/components/hub/error-page.tsx`, `tests/Hub.Tests/Api/OperationsTests.cs`, `web/tests/operations.cjs` |
| S06 probe guard | `scripts/verify-homedev-review.py` (one SKIP line), `tools/test_social_activation.py` |
| S07 idempotency | `src/Hub.Api/Infrastructure/Idempotency.cs`, `src/Hub.Api/Text.cs` (`error.idempotency_in_progress`), `tests/Hub.Tests/Api/OperationsTests.cs` (`IdempotencyTests`) |
| S08 header and start-up tests | `tests/Hub.Tests/Api/FoundationsTests.cs`, `tools/test_pilot_operations.py` |
| S09 Admin sweep | new `tests/Hub.Tests/Api/AdminSweepTests.cs` |
| S10 updating message | `web/src/lib/api.ts`, `web/src/main.tsx` (retry rule), `web/src/i18n/en.ts`, `web/tests/operations.cjs` |
| S11 tab title | `web/src/components/hub/notifications.tsx`, `web/tests/operations.cjs` |
| S12 footers | `src/Hub.Api/Text.cs`, `tests/Hub.Tests/Api/CollaborationTests.cs`, `tests/Hub.Tests/Api/NotificationSearchTests.cs` (added assertions only) |
| S13 density saved | `src/Hub.Api/Features/Notifications.cs` (PUT beside the digest preference), `web/src/components/hub/common.tsx`, `tests/Hub.Tests/Api/SupervisorCapacityTests.cs` or a new focused test class, `web/tests/operations.cjs` |
| S14 off-host copy | `docs/runbooks/restore.md`, `docs/pilot/production-readiness.md`, new `scripts/check-offhost-backup.py`, `scripts/test_check_offhost_backup.py`, `tools/ci_steps.txt`; `hosting/` units only if the diagnosis needs them |
| S15 off-host drill | new `scripts/offhost-restore-drill.sh` wrapping the existing `scripts/restore-homedev-review-drill.sh` and `scripts/restore-homedev-pilot-drill.sh`, `tools/test_backup_helpers.py`, `docs/runbooks/restore.md` (drill record) |

Notes that keep diffs small and behaviour safe:

- S02: `OpsFacts` gains `int DeadLettered = 0` as its last parameter, so existing positional test calls compile unchanged. The migration's backfill is one `UPDATE ... WHERE processed_at IS NULL AND attempts >= 10 AND dead_lettered_at IS NULL`.
- S05: Known limit: a fixed per-user window, not a token bucket. That is enough for a log-only endpoint.
- S07: the claim is `INSERT ... ON CONFLICT (user_id, key) DO NOTHING` with status 0 for pending, issued before `next(ctx)` and outside any request transaction. The receipt is filled or deleted with `ExecuteUpdate`/`ExecuteDelete`, so the middleware never flushes entities the handler left tracked. The pending claim's `created_at` is one day before the claim time, so the previous image's one-day lookup never replays it after a rollback (FR-OPS-14). Known limit: a crash after the handler commits and before the receipt is filled leaves a pending claim. The claim is replaced after the abandon window. If that ever matters, planning's single-transaction receipt is the upgrade path.
- S10: network failures become an `ApiError` with status 0 and code `network`; the retry rule in `main.tsx` treats status 0 as retriable, so read retries stay exactly as today.
- S04 and S10 both touch `api.ts`; S10 lands after S04.

## Constitution Check

| Principle | Result |
|---|---|
| I. Coordination First | PASS. Operations and trust work so coordination data stays available and believable; no new screen or concept. |
| II. Deterministic and Explainable | PASS. No AI. The dead-letter limit is an operational constant the worker already uses, not a rule threshold for a computed status. |
| III. One Accountable Owner | PASS. No ownership change. |
| IV. Specification is the source of truth | PASS. §48 written first; amendment requests for §23.5, §23.8, §24.2 and §25.10 go to the integrator. |
| V. Traceable by Construction | PASS. No audited data changes. Dead-letter retry and client errors are operational log lines (D4), like `/admin/evaluate`. The migration is additive. |
| VI. Secure by Default | PASS. Closes a pre-sign-in leak, pins headers, start-up refusals and Admin routes by test, keeps revocation gates, and the client-error route is authenticated, size-capped, rate-limited and free of item text. Local-password and social sign-in remain under the existing hosting decision; this packet does not change authentication. |
| VII. Simple Enough | PASS. No service, broker, package or new store. Complexity tracking does not apply. |

## Checks to run per slice

- `dotnet build Hub.slnx -c Release` and `dotnet test Hub.slnx -c Release` with the coverage gate (`tools/coverage_gate.py`).
- `npm run build`, `npm run lint`, then `python3 tools/check_served_assets.py src/Hub.Api/wwwroot` (from S01) and `npm run test:operations` (from S03).
- The Python safety suites already in CI, plus `tools/test_served_assets.py` and `scripts/test_check_offhost_backup.py`.
- `python3 tools/trace_spec.py --check` (run by the integrator for this spec PR).
