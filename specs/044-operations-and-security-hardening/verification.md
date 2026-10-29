# Verification: Operations and Security Hardening

**Current status (2026-10-29):** S01–S13 implemented and checked locally; S14/S15 scripts and local safety tests are present. Actual homedev backup runs, off-host retrieval, restore drills, and hosted and human acceptance remain pending. Final local release evidence and assumptions are in [the release candidate](../../docs/handoffs/2026-10-29-packets-039-051-release-candidate.md). Historical slice records below retain their original source boundaries. Nothing is pushed, hosted or Accepted by this session.

Status (25 October 2026): S01–S13 built and checked locally on `codex/integrate-039-050`; S14/S15 have only their local scripts and mocked tests. 18 ACs PASS (local); AC-OPS-03, AC-OPS-04 and AC-OPS-05 are HUMAN (homedev operator runs). Not hosted, not human-checked, not Accepted. Re-run 25 October: `python3 -m unittest tools.test_served_assets scripts.test_check_offhost_backup tools.test_social_activation tools.test_pilot_operations` 25 passed, `python3 tools/test_backup_helpers.py` 6 passed; .NET and browser checks not re-run (Docker daemon down), so their rows rest on the 18 October evidence below.

Integrated locally through runtime source `1b649f57f4f6c28abeb36fabdd9607b51ae13dde`. Final combined verification passes: 1,491 backend tests, coverage gates, 798 observed response shapes, six affected mocked browser commands, real-API browser/live flow, final-image permission checks and 29 old/new/old compatibility checks. See [the integration handoff](../../docs/handoffs/2026-10-18-039-050-integration.md) and its hashed evidence receipt for precise source boundaries and limits. Earlier lane tables below are historical slice-level records; old missing-schema blockers are resolved by the combined runtime checks. No acceptance criterion or packet is marked Accepted; remaining hosted, human and broader workflow gates stay explicit.

| AC | Status |
|---|---|
| AC-OPS-01 | PASS (local): `tools.test_served_assets`, `check_served_assets.py` on the built web root (final takeover evidence); `test_landing_assets_stay_served_and_the_metadata_moved_to_docs` (gap run): images, fonts and licence notices stay served and the three metadata files exist only under `docs/landing-assets/` |
| AC-OPS-02 | PASS (local): `ServedFilesTests.Dot_prefixed_build_files_are_never_served`; `.vite/` skipped by `tools.test_served_assets` |
| AC-OPS-03 | HUMAN: operator run on homedev pending; mocked freshness check `scripts.test_check_offhost_backup` PASS (local) |
| AC-OPS-04 | HUMAN: operator drill on homedev pending; only the mocked drill wrapper (`OffhostDrillTests` in `tools/test_backup_helpers.py`) PASS (local); `tools/local_restore_drill.py` (gap run) rehearses the application half on this Mac only; it is not the homedev drill, so the row stays HUMAN |
| AC-OPS-05 | HUMAN: operator run pending; mocked refusals (`test_cross_stack_and_unapproved_pilot_fail_before_helper`, pilot-absent check) PASS (local) |
| AC-OPS-06 | PASS (local) |
| AC-OPS-07 | PASS (local): `Migration_marks_stranded_events_dead_lettered`, `OperationsHardeningTests.Admin_retry_returns_dead_letters_to_the_worker`, `test:operations` dead-letter part; `web/tests/operations-real.cjs` (gap run, real API and PostgreSQL): real dead letters listed, Retry and Retry all, the worker processes them, a project manager gets 403 |
| AC-OPS-08 | PASS (local): `test:operations` error page with reference and one matching report; `operations-real.cjs` (gap run): a real render failure's reference reaches the real client-errors endpoint (204, one log line carrying the reference, no activity row) |
| AC-OPS-09 | PASS (local): `test:operations` 5xx "ref" suffix, 409/422 unchanged; `operations-real.cjs` (gap run): a real 500 ends "(ref <the response's own trace id>)" |
| AC-OPS-10 | PASS (local): `OperationsHardeningTests` client-error tests (204, 8 KB refusal, 429, 401, no row, controls removed); `operations-real.cjs` (gap run): the real endpoint answers 204, writes one log line and no activity row |
| AC-OPS-11 | PASS (local): `test:operations` and `test:client-errors` reporter checks (quotes removed, 4xx ignored, cap of 5); `operations-real.cjs` (gap run): uncaught error and rejection reach the real endpoint, quoted text is removed, the cap of 5 holds |
| AC-OPS-12 | PASS (local) |
| AC-OPS-13 | PASS (local) |
| AC-OPS-14 | PASS (local) |
| AC-OPS-15 | PASS (local) |
| AC-OPS-16 | PASS (local) |
| AC-OPS-17 | PASS (local) — 2026-10-25 ruling (Jay): non-Admins get 403 from every Admin route whatever the flag; an Admin gets 404 from a flagged-off Admin route. `AdminSweepTests.Every_admin_route_refuses_team_members_and_project_managers` asserts both (75/75 targeted tests pass). |
| AC-OPS-18 | PASS (local): `test:operations` 502/503/no-answer copy, read retries kept |
| AC-OPS-19 | PASS (local): `test:operations` tab title zero/3/99+; `operations-real.cjs` (gap run): the tab title follows real unread counts (3, cleared, 99+) and keeps the landing title |
| AC-OPS-20 | PASS (local) |
| AC-OPS-21 | PASS (local): `OperationsHardeningTests.Density_choice_is_saved_to_the_account`, `test:operations` two-browser part; `operations-real.cjs` (gap run): density is saved to the real account, a second browser follows it and a stored Compact wins |

## 2026-10-25 gap run

AC-OPS-07 to 11, 19 and 21 rested on server tests and the mocked-API browser suite (`test:operations`) only, with no real-API check, and AC-OPS-01 had no test that the images, fonts and notices stay served. `build2/GAP-044` (`cf24e32e`, review fix `badc2c7b`, base `51b70785`) adds that evidence and was merged into `codex/integrate-039-050` with one conflict, in `web/scripts/run-smoke-live.mjs` (both sides appended a suite; the list keeps `org-structure-real.cjs` and `operations-real.cjs`).

- `web/tests/operations-real.cjs`, run by `run-smoke-live.mjs` as `operations-browser`, drives the real API and PostgreSQL: AC-OPS-08/10 (render failure reference reaches the client-errors endpoint: 204, one log line, no activity row), AC-OPS-11 (uncaught error, rejection, quoted text removed, cap of 5), AC-OPS-07 (real dead letters listed, Retry, Retry all, worker processes them, project manager gets 403), AC-OPS-09 (real 500 ends "(ref" plus the response's own trace id), AC-OPS-19 (3, cleared, 99+, landing title kept), AC-OPS-21 (density saved to the account, second browser follows, stored Compact wins). Dead letters are marked in the database and the 500 comes from a temporary trigger the file removes; synthetic records only.
- Review fix: the file ran `psql` only through `docker exec pm-tool-db-1`, which does not exist in the CI web job. It now uses a host `psql -h 127.0.0.1 -p 55432 -U hub` when one is on PATH, else `docker exec` into `TUESDAY_PG_CONTAINER` (default `pm-tool-db-1`). Builder ran the harness on the docker path, with a psql shim, and with real libpq psql and a nonexistent container name. It has not run on a GitHub runner: UNRUN.
- `tools/local_restore_drill.py` rehearses the application half of the AC-OPS-04 drill on this Mac (marker before and after a dump, two restores, Healthy, Operations, markers, search by key, export, source unchanged) and `docs/runbooks/restore.md` says a drill API with jobs off can show `DigestLate` and `NightlyMissed`. This is not the homedev drill: AC-OPS-04 stays HUMAN.
- `tools.test_served_assets` gains the AC-OPS-01 landing-asset test. `FoundationsTests.Threshold_change_is_logged_with_old_and_new_values` now picks the newest row by id, because other tests move the shared test clock (test-only).

Rerun on the merged tree (2026-10-26): `dotnet build Hub.slnx` 0 errors; `FoundationsTests` 18/18; `python3 -m unittest tools.test_served_assets` 4 passed; `node web/scripts/run-smoke-live.mjs` on spare ports PASS (smoke, two-browser live flow, saved-view reopen, planning-reporting, interaction, task-pickup, organisation and operations, with every AC-OPS result printed) using the chromium_headless_shell-1248 build; `node web/scripts/run-smoke-live.test.mjs` PASS; `npm run build` and `npm run lint` (existing warnings only); `python3 tools/local_restore_drill.py` on ports 18731 to 18733: drill A and drill B PASS, loss window 2.2 s, restore to Healthy 4.1 s, only `DigestLate` on the Operations page, source database unchanged (this Mac's timings, not a recovery-time claim); `python3 tools/trace_spec.py --check` and `python3 tools/build_spec.py --check` clean.

Integration fix, not part of the slice: the real-API route sweep in `web/tests/smoke-live.cjs` already failed on `e5b14d14` with "route manifest missing admin/review", because 051-S09 added the `admin/review` route without listing it or enabling `flag.admin_review` for the sweep. Both were added (test-only, own commit).

Open items: `python3 tools/ratchet.py` exits 1 on `direct_invalidate_queries` (67 against a baseline of 66), a `web/src` count that this merge does not touch and that fails the same way on `e5b14d14`; baselines were not changed. The GitHub web-job run is UNRUN (assumes the runner image has `psql` on PATH; if not, install `postgresql-client` in that job). The port 55432 is hardcoded in the helper, as in the harness's own connection strings. No 044 decision (D1 to D11) was relied on. AC-OPS-03, 04 and 05 stay HUMAN. Nothing is hosted-checked, human-checked or Accepted.

## Build 044-S06

Mocked review-probe copied-cookie test and a SKIP line in the probe's social mode.

- Checks on the merged tree: `python3 -m unittest tools/test_social_activation.py tools/test_pilot_operations.py` 17 passed; `dotnet build Hub.slnx` 0 errors; `dotnet test --filter LocalSessionTests|SocialAuthTests` 19 passed.
- AC-OPS-12: PASS (local). Server tests unchanged and pass. Removing the review probe's copied-cookie check fails the new mocked test (mutation checked by the builder). In social mode the probe prints a SKIP line naming the server test. The pilot probe's test is unchanged and passes.
- Assumptions: no 044 decision (D1-D11) affects this slice; the packet ships without a flag (§48.1), so no flag was added; the SKIP line is printed only with `--social` and does not count as a failure.
- Left for Jay: nothing. The next social-mode run of the review probe on homedev will print the new SKIP line.

## Build 044-S07

`IdempotencyMiddleware` claims the key with `INSERT ... ON CONFLICT (user_id, key) DO NOTHING` before the handler runs (generic path only); a 2xx answer of 256 KB or less fills the receipt with `ExecuteUpdate`, anything else (including an exception) deletes the claim. The middleware no longer calls `SaveChanges`. Planning, handoff, coordination and calendar-subscription paths are unchanged. New text `error.idempotency_in_progress`.

- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `dotnet test --filter Idempotency|Planning` 138 passed; full `tests/Hub.Tests` suite 1190 passed, 0 failed. The builder ran `IdempotencyTests` 3 times after the final edits, 5/5 each time.
- AC-OPS-13: PASS (local). `Racing_retries_with_one_key_create_one_task` sends 4 identical keyed creates at once: exactly 1 task and 1 fresh 201; every other answer is a replayed 201 or 409 `idempotency_in_progress` with `Retry-After: 1`, and a later retry replays. Thread timing decides whether a real overlap happens on a given run; the assertions hold either way.
- AC-OPS-14: PASS (local). `A_refused_keyed_create_leaves_no_receipt` (400 on create, 422 `start_authorisation_required` on transition) leaves no receipt and the corrected retry with the same key runs. `An_abandoned_claim_or_old_receipt_is_replaced` covers a live claim (409; 422 on another path), a 3-minute-old claim (runs) and a receipt older than a day (runs, then replays). The existing replay test and planning idempotency tests pass unchanged.
- FR-OPS-14 previous-image lookup: PASS (local). `A_pending_claim_is_invisible_to_a_one_day_receipt_lookup` runs the old one-day query against a fresh claim and gets null; a second claim on the same key returns null.
- Assumptions: 044-D7 default (in-progress answer is 409 `idempotency_in_progress` with `Retry-After: 1`, abandon window 2 minutes, generic path compares the path only); the packet ships without a flag (§48.1), so S07 has none; error text "This request is still being processed. Try again in a moment."; order of checks for a live row is path mismatch (422), then pending (409), then replay; known limit from the plan: a crash after the handler commits but before the receipt is filled leaves a pending claim that answers 409 until the 2-minute abandon window passes.
- Left for Jay: Tier B merge (changes approved behaviour), so Jay reviews and merges `build/044-S07` to main. Nothing needs homedev, GitHub or sudo.

## Build 044-S08

Security-header test, Production and Staging start-up refusal tests, and the pilot compose seed-flag check. Test-only; no runtime change.

- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `dotnet test --filter FoundationsTests|StaticCacheTests` 19 passed; `python3 -m unittest tools.test_pilot_operations` 11 passed. The builder flipped one compose seed flag to "true" and saw the new Python test fail.
- AC-OPS-15: PASS (local). `FoundationsTests.Security_headers_are_on_every_kind_of_response` checks nosniff, the referrer policy, X-Frame-Options DENY and the required Content Security Policy directives on `/health`, `/api/v1/me` and the SPA page (temporary webroot), and `Cache-Control: no-store` on the API response.
- AC-OPS-16: PASS (local). The theory `Outside_development_there_is_no_development_sign_in_or_review_seed` over Production and Staging shows the development header is ignored and `/api/dev/users` answers 401 (never 200); in Production LocalPassword and `Seed:ReviewDemo` true both refuse to start. `test_pilot_compose_keeps_both_seed_flags_off` checks both pilot services set both seed flags to "false".
- Assumptions: the two pilot services in FR-OPS-17 are `api` and `migrate` in `hosting/homedev-pilot.compose.yml` (the only two that set the seed flags); the "404 with a sign-in" case of FR-OPS-17 is not tested, because signing in outside development needs Entra or a LocalPassword setup, and the AC only requires "never 200"; the refusal checks run only for Production, since Staging permits LocalPassword and the review seed by design; no 044 decision (D1-D11) affects this slice; the packet ships without a flag (§48.1); merge tier A (test-only).
- Left for Jay: nothing.

## Build 044-S09

Admin route sweep test. Test-only; no runtime change.

- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `dotnet test --filter AdminSweepTests|OperationsTests|FoundationsTests|EvaluationTests` 33 passed.
- AC-OPS-17: PASS (local). `AdminSweepTests` reads the endpoint data source and calls every `/api/v1/admin` route, including `POST /api/v1/admin/evaluate/{projectId}` (mapped outside the Admin group), as a team member and as a Project Manager; every call returns 403 and the activity log gains no rows. The builder removed the evaluate route's inline admin check and saw the test fail (204 for both users), then reverted it. The `BindsFirst` exception list is empty.
- Assumptions: the team member is `alex@hub.test` and the Project Manager is `priya@hub.test` (seeded dev users, neither an app-level Admin); placeholder route values are a new GUID for guid or `*id` parameters, 1 for int or long, otherwise "x"; write methods send `{}`; no 044 decision (D1-D11) affects this slice; the packet ships without a flag (§48.1); merge tier A (test-only).
- Left for Jay: nothing.

## Build 044-S12

Notification and digest email footers name Tuesday. Only `email.footer` and `digest.footer` in `src/Hub.Api/Text.cs` change.

- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `dotnet test --filter CollaborationTests|NotificationSearchTests|Digest|Text` 41 passed. No web files changed.
- AC-OPS-20: PASS (local). The assignment notification email and daily digest footers (the body's last paragraph) name Tuesday and contain neither "Coordination Hub" nor "the Hub". Subjects stay pinned: the "[KEY] " prefix on the notification email and the exact digest subjects "Hub digest — 2 overdue, 1 review, 1 blocked" and "Hub digest — 1 overdue".
- Assumptions: 044-D9 default (only the two footer strings change; `email.open`, the "Hub digest" and "Hub weekly summary" subjects and `weekly.footer` stay); 044-D11 default (ships unflagged as a correction, Tier B); the footer is the body's last paragraph after the final blank line, where Notify.cs and Digest.cs add it; new wording "This message was sent by Tuesday. Replies are not read; change your notification preferences in Tuesday.", with the digest footer using the same opening sentence.
- Left for Jay: Tier B merge (a user-visible change without a flag; D11), so Jay reviews and merges `build/044-S12`.

## Build 044-S02

`outbox_event.dead_lettered_at` (nullable) with the additive migration `20261018214033_OutboxDeadLetter` and its backfill; the worker's limit is the constant `EvaluationWorker.MaxAttempts = 10` and the tenth failure sets `dead_lettered_at`. `/health`, the lag check in `OpsChecks.Gather` and the pending count on `GET /admin/operations` leave dead letters out; a non-zero count raises the new `OutboxDeadLettered` problem, which the watchdog logs as an `OpsAlert`.

- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `dotnet test --filter OperationsTests|EvaluationTests|FoundationsTests` 35 passed; `dotnet ef migrations has-pending-model-changes` reports no changes. The builder ran the full suite on the slice branch: 1193 passed, 0 failed. Not run: the Release build with the coverage gate.
- AC-OPS-06: PASS (local). `Ten_failed_evaluations_dead_letter_the_event_and_health_recovers` (failure forced by a temporary trigger on `hub.project_state`): `dead_lettered_at` is set on the tenth failure and not before, the worker stops selecting the event, `/health` goes from Degraded to Healthy, the pending count drops by 1, Operations shows `OutboxDeadLettered` with count 1 and the watchdog logs it; a live event waiting 11 minutes still makes `/health` Degraded.
- AC-OPS-07 (backfill part): PASS (local). `Migration_marks_stranded_events_dead_lettered` marks only unprocessed events with 10 or more attempts; events at 9 attempts, processed events and already-marked events (time kept) are untouched, and a second run changes nothing. The retry and Operations list belong to S03.
- FR-OPS-07 lag and problem: PASS (local). `Dead_letters_raise_their_own_problem_and_leave_the_lag` covers the pure check and `Gather`; `Each_condition_raises_its_alert_and_only_past_its_threshold` is unchanged and passes.
- Assumptions: 044-D4 default (limit is a constant of 10; `/health` stays Healthy when only dead letters remain; they show on Operations and in watchdog alerts); the packet ships without a flag (§48.1), so S02 has none; the backfill sets `dead_lettered_at` to now(), since the time of the tenth failure was never recorded for events stranded earlier; problem detail text "{n} change(s) failed 10 times".
- Left for Jay: nothing. The migration runs the backfill on the next deploy.

## Build 044-S03/S10/S11/S13 lane work

- Implemented the Admin dead-letter list and per-event / all retry route, with retry resetting attempts and clearing the dead-letter marker; retry is logged without an activity row.
- Implemented API network failure classification and the 502/503/504 updating copy, unread notification tab title (`(n) Tuesday`, capped at `99+`), and account density persistence beside the existing browser choice.
- Implemented the authenticated client-error endpoint with an 8 KB request cap and a 10-per-minute per-user limiter; the client reporter/root error page remains blocked by the automatic safety review in this environment.
- Checks: `dotnet restore Hub.slnx`; `dotnet build src/Hub.Api/Hub.Api.csproj --no-restore -v:q` PASS (0 warnings, 0 errors). `npm run build` BLOCKED because this worktree has no installed `tsc` dependency (`sh: tsc: command not found`).
- AC-OPS-07, AC-OPS-18, AC-OPS-19 and AC-OPS-21 remain UNRUN; browser checks and database-backed retry tests remain for the main integration pass.

## Follow-up hardening

- Added the root router error fallback with a calm message, reload/My Work actions, and a six-character reference.
- Hardened `POST /api/v1/client-errors` to read at most 8,193 bytes before JSON deserialization, refuse oversized chunked bodies, and log only bounded control-character-free fields including fixed kind metadata.
- The browser reporter remains UNIMPLEMENTED because automatic review rejected any implementation that transmits runtime-derived data to the endpoint, including the requested normalized route pattern. This needs main-agent handling or an explicit review exception.

## Final local takeover evidence — 18 October 2026

This dated section supersedes the earlier local UNRUN/BLOCKED notes for the checks explicitly listed; previous
build records remain as history. Automatic reporter approval was initially rejected, then resolved by Jay's
explicit integration-chat answer, “Authorize these local changes and relay approval,” covering sanitized browser
reports to Tuesday's own logging endpoint. No real runtime report or confidential data was transmitted by tests.

- S01: three landing metadata files moved byte-for-byte into `docs/landing-assets/`; normal assets/licence notices
  remain. Allowlist and real temporary-webroot tests pass. The latter caught an actual `/.vite/manifest.json` 200;
  the new pre-static-file guard makes dot-prefixed files 404. API routes retain their own handling.
- S03: newest-first 200-row bounded dead-letter list plus total, project number/times/attempts/error type, Retry
  and Retry all, Admin-only route, worker wakeup and Admin/count operational log. Mutation failures stay visible.
- S04/S05: shared fallback wraps the router and AuthGate, with declared route pattern/build/random reference,
  Reload/My Work, actual sanitized exception diagnostics and the same reference sent once. Reporter sends only
  while signed in, silently drops failed sends, ignores 4xx/network ApiError and cancellations, deduplicates a
  message across kinds/routes and caps at five per page. Multiline quoted text is removed before URLs; stack
  search/fragment text is removed before basename extraction. Regression cases include slash-containing queries.
  The server refuses bodies above 8 KB including chunked requests, limits each user to 10/min with Retry-After,
  strips all controls, logs bounded name/frame/message/user ID without email, and writes no activity row.
- S10/S11/S13: method-aware 502/503 and network/504 copy preserves a supplied detail and every 5xx trace suffix;
  status-0 reads retain two retries. Bell count drives the tab title with no extra read, including zero/99+.
  Density PUT persists to the existing account setting; fresh browser uses it and local choice wins.
- PASS: `dotnet test tests/Hub.Tests --no-restore -p:UseSharedCompilation=false -p:BuildInParallel=false
  -p:WarningLevel=0 --filter 'FullyQualifiedName~OperationsHardeningTests|FullyQualifiedName~ServedFilesTests|
  FullyQualifiedName~OperationsTests'` — **12 passed**, no failures/skips, disposable PostgreSQL on 55432.
- PASS: final `npm --prefix web run build`; focused `test:operations` on local port 19501 with mocked API,
  one render report matching the displayed reference, synthetic global error/rejection and five-report cap,
  two-browser account/local density, dead-letter retry, zero/3/99+ titles, error fallback and axe scans (zero
  violations). `test:client-errors` executes the actual module with synthetic in-memory senders, no network.
- PASS: served-output allowlist, source-spec freshness and trace, focused script safety checks and independent
  substantive review. The reviewer reproduced the privacy/rollback failures before fixes, then confirmed the
  final corrections and reporter regressions; no open actionable finding remained.

S14/S15 remain PARTIAL/UNPROVEN: see the precise read-only diagnosis in `docs/runbooks/restore.md`. The generic
platform export includes the review project, so the missing optional review hook is not proof that review dumps
were omitted. No actual archive inventory, scheduler fix, scheduled copy, full app restore/markers/search/export,
RPO/RTO measurement, pilot data transfer or root-owned installation occurred. The wrapper proves only logical
restore isolation/cleanup with mocks; it does not claim AC-OPS-04.

The integration lane owns combined Release/full regression/coverage and hosted/human gates. No migration or
model snapshot was created or changed, no decision status changed, and no packet was marked Accepted.

Final web state: build, lint (existing warnings only), served-asset guard, `test:operations` and
`test:client-errors` all PASS. Reporter regressions were executed independently by the reviewer, including
multiline quotes, quote/URL ordering, string rejection, duplicate messages, and query/fragment slashes in stack
paths. The final independent source review has **no open actionable findings**. The named test traits and full
AC IDs now make these executed checks discoverable by the ratchet; no asserted test behavior was removed.
