# Verification: Operations and Security Hardening

Slice builds in progress; see the build notes below.

| AC | Status |
|---|---|
| AC-OPS-01 | UNRUN |
| AC-OPS-02 | UNRUN |
| AC-OPS-03 | UNRUN |
| AC-OPS-04 | UNRUN |
| AC-OPS-05 | UNRUN |
| AC-OPS-06 | PASS (local) |
| AC-OPS-07 | PARTIAL: backfill part PASS (local); retry and Operations list are S03 |
| AC-OPS-08 | UNRUN |
| AC-OPS-09 | UNRUN |
| AC-OPS-10 | UNRUN |
| AC-OPS-11 | UNRUN |
| AC-OPS-12 | PASS (local) |
| AC-OPS-13 | PASS (local) |
| AC-OPS-14 | PASS (local) |
| AC-OPS-15 | PASS (local) |
| AC-OPS-16 | PASS (local) |
| AC-OPS-17 | PASS (local) |
| AC-OPS-18 | UNRUN |
| AC-OPS-19 | UNRUN |
| AC-OPS-20 | PASS (local) |
| AC-OPS-21 | UNRUN |

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
