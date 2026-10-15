## 48. Operations and Security Hardening Amendment

Approved scope: Jay approved the 14 October 2026 build-approach report and directed building all of it on 2026-10-14. Features are fixed and additive only: nothing here removes, hides, narrows or freezes a current feature, and where an item touches existing behaviour, today's behaviour stays the default. This section records the product contract for the report's roadmap items 1, 2, 6, 7, 8, 9, 11 and 17. Packet 044 delivers it. Each item closes a gap found in the code or in the hosted stack: build metadata served before sign-in, an off-host backup that has never run on its own, outbox rows that strand and keep `/health` Degraded, errors that users cannot quote, a session-revocation check that only some probes run, an idempotency receipt written after the request it protects, security behaviour that no test pins, and copy that is not true.

Where this section adds to §13.0 (density), §13.15 (Admin screens), §17.6 (email delivery), §21 (security requirements), §22 (backup and recovery), §23.5 (outbox processing), §23.6 (session management), §23.8 (observability), §24.2 (`outbox_event`), §25.6 (error handling) and §25.10 (idempotency), this section governs. It adds no business record, permission, role or report. It adds one nullable column (`outbox_event.dead_lettered_at`), two endpoints (`POST /api/v1/client-errors` and `POST /api/v1/admin/operations/outbox/retry`) and one preference write (`PUT /api/v1/me/preferences/density`) for a field that already exists.

### 48.1 Scope and boundary

Eight parts: served files (§48.2), off-host backup and a restore drill (§48.3), outbox dead-letter (§48.4), error capture (§48.5), the copied-cookie probe (§48.6), idempotency (§48.7), guard tests (§48.8) and honest copy (§48.9). Outside this amendment: the build stamp, reload toast and build in `/health` (§46, packet 042); route splitting; live updates; the aggregate route counter (§51, packet 047); third-party monitoring, visitor identifiers and usage telemetry; any change to the 15-minute RPO or 8-hour RTO in §22 until Jay decides (§48.11 D3). No part ships behind a flag: each is a fix, a guard or a copy change that is complete in its own pull request. The parts users can see (the dead-letter list with Retry, the error page and trace reference, the updating messages, the tab title count, the email footers and the saved density) are unflagged corrections under D11, so their slices are Tier B (§43.11).

### 48.2 Served files

- **FR-OPS-01.** Files that describe how the product or its landing images were built or captured MUST NOT be served. `web/public/landing/capture.json`, `web/public/landing/README.md` and `web/public/landing/fonts/sources.json` move, unchanged in content, to `docs/landing-assets/`, which the web build does not copy. Today they are copied into `src/Hub.Api/wwwroot` and served before sign-in by `UseStaticFiles` (`src/Hub.Api/Program.cs:109-110`); `capture.json` names a branch, a local database port and a development account. The landing page, its images (including the historical `board.svg` and `tuesday-board.jpg`), its fonts and their licence notices (`THIRD-PARTY-NOTICES.txt`, `fonts/*-OFL.txt`) stay served and unchanged. A request for a moved file answers 404, because the SPA fallback (`Program.cs:175`) does not match a path with a file extension.

- **FR-OPS-02.** CI MUST check the built web output (`src/Hub.Api/wwwroot`, the Vite `outDir` in `web/vite.config.ts`) after `npm run build` and fail when any file has an extension outside an allowlist (`.html`, `.js`, `.css`, `.svg`, `.jpg`, `.jpeg`, `.png`, `.webp`, `.ico`, `.woff2`, `.txt`) or when any text file contains `sourceMappingURL`. The failure names every offending path. The check skips the `.vite/` folder into which the Vite build writes its manifest (FR-LIVE-09), because the server never serves a dot-prefixed path; a test proves that `/.vite/manifest.json` answers 404. The check is a plain script with no new dependency. Any build identifier added to a served file is a commit SHA only, never a branch, host, port or account name.

### 48.3 Off-host backup and restore drill

- **FR-OPS-03.** The review and pilot dumps that `pm-tool-review-backup.timer` and `pm-tool-pilot-backup.timer` write on homedev MUST reach an encrypted off-host copy on a schedule, with no person running a command. The first step diagnoses why no scheduled off-host copy has run (`docs/pilot/production-readiness.md:53`; `docs/runbooks/restore.md:5,26`) and records the cause in the restore runbook before any fix. The fix uses the encrypted repository Jay already approved for review recovery data and the existing export path. It adds no host, service or paid storage. Review data may be copied under that existing approval. Pilot or company data enter the off-host copy only after Jay records his approval as a decision row. Review and pilot copies stay separate. The pilot drill script keeps refusing review dumps (`scripts/restore-homedev-pilot-drill.sh:10`), and the off-host drill (FR-OPS-05) refuses a pilot dump on the review path in the same way. Any change to a root-owned unit or timer is installed by Jay with his own sudo.

- **FR-OPS-04.** A freshness check MUST report, for each stack in the off-host copy, the time of the newest dump it holds and its age. It exits non-zero and names the stack when the newest copy is older than the freshness window (§48.11 D3) or when a stack that should be present is missing. It never prints a password, key or file content. It runs after each scheduled copy, where the copy is made, and its last result is recorded in the restore runbook.

- **FR-OPS-05.** A restore drill MUST prove recovery from the off-host copy, not only from the on-host dump. The drill: writes a marker task before the newest dump and a second marker after it; retrieves the newest off-host copy to a private scratch location; restores it with the existing `scripts/restore-homedev-review-drill.sh` or `scripts/restore-homedev-pilot-drill.sh` into a temporary database; starts the same release image against that database on a loopback port with seeds and background jobs off; and runs the restore checklist (`docs/runbooks/restore.md`, "Verification checklist"): `/health` answers Healthy, Admin > Operations shows no problem once evaluation settles, the first marker exists and the second does not, a known task is found by key, and a report exports. It measures the data-loss window (the time between the last change kept and the first change lost) and the time to recover (from the start of retrieval to Healthy), writes one row in the runbook's drill record, and removes the temporary database, container and scratch files. The live database and volumes are never touched. The drill repeats at the cadence §21 and §22 already set.

### 48.4 Outbox dead-letter and a /health that recovers

- **FR-OPS-06.** An outbox event whose project evaluation has failed 10 times MUST be marked dead-lettered: `outbox_event.dead_lettered_at` (new, nullable) is set to the time of the tenth failure. Ten is the worker's existing limit (`src/Hub.Api/Infrastructure/EvaluationService.cs:254,272`), kept as one named constant. The worker still selects only events below the limit, so processing is otherwise unchanged. The additive migration marks existing stranded events (unprocessed, 10 or more attempts) as dead-lettered; it is safe to re-run. The nightly job still re-evaluates every Active project, so derived state recovers even while an event waits.

- **FR-OPS-07.** `/health` (`Program.cs:128-138`), the Operations lag check and the pending-changes count (`src/Hub.Api/Infrastructure/Operations.cs:28-29,51`; `src/Hub.Api/Features/Admin.cs:96-112`) MUST leave dead-lettered events out. `/health` keeps its response shape, apart from the `build` field §46 adds (FR-LIVE-02), and its rule for live events: Degraded when the oldest live event waits more than 10 minutes. While any dead-lettered event is unretried, Operations raises a new problem kind, `OutboxDeadLettered`, with the count, and the five-minute watchdog logs it as an `OpsAlert` like every other problem. One stranded event therefore no longer keeps `/health` Degraded, and it never disappears from the operator's view.

- **FR-OPS-08.** Admin > Operations (`web/src/pages/admin/Operations.tsx`) MUST list dead-lettered events, newest first, at most 200 with the total: event type, project number when the event names a project that still exists, created time, attempts, dead-lettered time and the last error's type name (the worker stores only the exception type, never a message or item text). Each row has Retry, and the list has Retry all. Retry clears `dead_lettered_at`, sets attempts to 0 and wakes the worker, which processes the event on its next pass; an event that fails 10 more times is dead-lettered again. The retry route sits in the existing Admin group, so only Admins can call it. A retry writes one application log line with the Admin's user ID and the number of events; it changes no business data and writes no activity-log row (§48.11 D4).

### 48.5 Error page, visible trace ID and client-error log

- **FR-OPS-09.** The router's root route (`web/src/main.tsx:20`) MUST have an error element, and the same fallback MUST wrap the sign-in gate, so a screen that throws while rendering shows a calm page instead of a blank one. The page says that something went wrong on this page, shows a reference, and offers Reload and a link to My Work. The reference is the matched route pattern (never the URL or an item ID), the build identifier when the page has one (§46), and 6 random characters, for example `projects/:projectNumber/tasks · 3f9c2a1 · K7QX2M`. The page shows no stack, no error text and no item text. It meets WCAG 2.1 AA. The existing catch-all Not Found route (`web/src/app/routes.tsx:136`) is unchanged.

- **FR-OPS-10.** When the API answers 500 or above with a problem body that carries `traceId` (`src/Hub.Api/Infrastructure/Errors.cs:67-74,90`), every message the client shows for that error MUST end with `(ref <traceId>)`. This happens once, where `ApiError` builds its message (`web/src/lib/api.ts:4-15`), so toasts, inline save states and error banners all carry it. Messages for 4xx answers are unchanged. The server text already asks people to quote the trace ID (`src/Hub.Api/Text.cs:326`); this makes it visible.

- **FR-OPS-11.** `POST /api/v1/client-errors` MUST accept a browser error report from a signed-in user and only write it to the application log. The body holds the reference, the route pattern, the build identifier, the kind (render, error or unhandled rejection), the error name, a message of at most 200 characters, and stack frames reduced to file, line and column with the origin removed. The whole body is at most 8 KB; a larger body is refused with 400. The server removes newlines and control characters before logging, logs the user's ID (never the email), stores nothing in the database, and answers 204. A per-user limit of 10 reports a minute answers 429 with `Retry-After` above it. An anonymous request answers 401 like every other API route (AC-AUTH-04). The page's Content Security Policy already allows only same-origin requests, so no report leaves the application.

- **FR-OPS-12.** The client MUST report: the error element's render failure, once, with the reference it shows; uncaught errors (`window` `error` events); and unhandled promise rejections. It does not report an `ApiError` below 500, a request cancelled by the page, or a failed report; a failed report is dropped silently and is never itself reported. A page load sends at most 5 reports, and the same message is sent once. Before sending, the client replaces every quoted string in the message with `…`, so names typed by users do not reach the log.

### 48.6 Copied-cookie probe

- **FR-OPS-13.** Sign-out already revokes the server-side session (§23.6; `src/Hub.Api/Infrastructure/LocalSessionStore.cs`; migration `20261012200704_LocalAuthSessions`), and the checks that prove it MUST stay release gates: `LocalSessionTests.Logout_revokes_copied_cookie_but_leaves_another_session_usable`, `SocialAuthTests.Logout_revokes_copied_cookie_and_inactive_users_cannot_sign_in_or_use_existing_sessions`, and the hosted probe line "copied cookie after sign-out denied" in `scripts/verify-homedev-review.py:96-97` and `scripts/verify-homedev-pilot.py:112-113`. This section adds: a mocked test of the review probe, matching the pilot probe's existing test, so removing or weakening its copied-cookie check fails CI; and, in the review probe's social mode, where no password sign-in exists, an explicit `SKIP` line that names the server test covering social sign-out, instead of silence. Every post-deploy probe run, including one started by packet 039's review deploy timer, therefore either checks the replay or says why not.

### 48.7 Idempotency claimed before the handler

- **FR-OPS-14.** For a POST with an `Idempotency-Key` on the generic path (`src/Hub.Api/Infrastructure/Idempotency.cs`), the server MUST claim the key before running the request: it inserts a pending receipt for the person and key, relying on the existing unique index on (`user_id`, `key`), in its own committed statement. Then:
  - Claim made: the request runs. A 2xx answer of at most 256 KB fills the receipt with the status, content type and body. Any other answer, or an exception, deletes the claim so that a retry runs again, as today.
  - A finished receipt younger than one day for the same path: the first response is replayed with `Idempotent-Replayed: true`, as today.
  - The same key on a different path, finished or pending: 422 with `code: "idempotency_key_reused"`, as today.
  - A pending claim younger than the abandon window (§48.11 D7): 409 with `code: "idempotency_in_progress"` and `Retry-After: 1`. The request does not run.
  - A receipt older than one day, or a pending claim older than the abandon window: the old row is replaced and the request runs.

  A pending claim is stored with status 0 and a `created_at` one day before its claim time, and its age is measured from `created_at` plus one day; filling it sets `created_at` to the fill time. A lookup of receipts younger than one day, which is what the previous image's middleware runs, therefore never returns a pending claim, so after a rollback (FR-DLV-17, FR-DLV-20) the previous image never replays an empty pending row. It runs the request as it does today, and its own receipt insert meets the unique index and is dropped, as today.

- **FR-OPS-15.** Everything else about idempotency MUST stay as it is: requests without a key, keys belonging to one person, the one-day replay window, the cleanup of receipts older than two days (`EvaluationService.cs:374`), the 100-character key limit, and the paths with their own handling. Planning commands keep their atomic receipt (`src/Hub.Api/Features/Planning.cs:625-662`; FR-PLN-25, AC-PLN-13); handoff and coordination commands and the calendar subscription route keep theirs. The only change in behaviour is that two identical requests racing each other no longer both run (today's comment at `Idempotency.cs:47`).

### 48.8 Guard tests

- **FR-OPS-16.** A test MUST pin the security headers that `SecurityHeaders` sets (`src/Hub.Api/Infrastructure/Http.cs:44-60`) on three kinds of response: `/health`, an `/api/v1` route and the SPA page. Each carries `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY` and a `Content-Security-Policy` that includes `default-src 'self'`, `script-src 'self'` and `frame-ancestors 'none'`. API responses also carry `Cache-Control: no-store`.

- **FR-OPS-17.** A test over the Production and Staging environments MUST pin today's start-up refusals (§21; FR-AUTH-01): the development sign-in header is ignored and `/api/dev/users` is not mapped in either, so it never answers 200 (401 without a sign-in, 404 with one); `Auth:Mode` LocalPassword refuses to start in Production (`src/Hub.Api/Infrastructure/Auth.cs:76-77`); `Seed:ReviewDemo` refuses to start in Production (`Program.cs:182-184`). A second test MUST check that both pilot services in `hosting/homedev-pilot.compose.yml` set `Seed__ReviewDemo` and `Seed__DevUsers` to `"false"`, which is how review data is kept off the pilot database today. These tests extend `FoundationsTests.Outside_development_only_Entra_tokens_sign_in` (`tests/Hub.Tests/Api/FoundationsTests.cs:188`) and change no runtime behaviour.

- **FR-OPS-18.** A sweep test MUST list every endpoint whose route starts with `/api/v1/admin` from the application's endpoint data source, including `POST /api/v1/admin/evaluate/{projectId}`, which is mapped outside the Admin group and checks the permission inline (`src/Hub.Api/Features/Modules.cs:74-79`). It calls each one as a team member and as a Project Manager, with placeholder route values and an empty JSON body, and expects 403 with no new activity-log row. A route that cannot reach its permission check because binding fails first is named in the test with its reason; an Admin route added without an Admin check fails the test.

### 48.9 Honest copy

- **FR-OPS-19.** When a request is answered 502 or 503, which the tunnel returns while the application restarts, the client MUST show "Tuesday is updating. This change was not saved. Try again in a moment." for a change, and "Tuesday is updating. Try again in a moment." for a read, instead of "HTTP 502". When a request gets no answer, or 504, a change shows "Tuesday could not be reached. This change may not have been saved. Check before trying again." and a read shows "Tuesday could not be reached. Try again in a moment." A problem body with its own `detail` keeps that detail. Reads that fail without an answer are retried exactly as today (`web/src/main.tsx:16`). The mapping lives once in `web/src/lib/api.ts`.

- **FR-OPS-20.** While the signed-in viewer has unread notifications, the browser tab title MUST be "(n) Tuesday", and "(99+) Tuesday" above 99; at 0 it stays "Tuesday" (`web/index.html:7`). The count is the notification bell's, from the existing unread read (`web/src/components/hub/notifications.tsx:39-54`); it makes no new request and does not include the Following count. The landing page keeps its own title (`web/src/pages/ApprovedHero.tsx:16-19`).

- **FR-OPS-21.** Notification and digest email footers MUST name the product Tuesday: `email.footer` and `digest.footer` (`src/Hub.Api/Text.cs:194,310`) replace "the Coordination Hub" and "the Hub" with "Tuesday". Subjects and other server text are unchanged in this packet (§48.11 D9).

- **FR-OPS-22.** A viewer's row density choice (§13.0; `DensityToggle` and the account menu, `web/src/components/hub/common.tsx:117-140`, `web/src/app/Shell.tsx:118-121`) MUST also be saved to the account's existing `dense_rows` preference through `PUT /api/v1/me/preferences/density`, which `/me` already returns (`src/Hub.Api/Features/Me.cs:50`). A browser that has its own stored choice keeps using it, as today. A browser with no stored choice uses the account's value. A person who has never chosen still gets compact rows. A failed save leaves the browser's choice in place and is tried again on the next change.

### 48.10 Acceptance criteria

- **AC-OPS-01.** Given the web build has run, then `src/Hub.Api/wwwroot` contains no `capture.json`, landing `README.md` or `fonts/sources.json`, the served-asset check passes on it, the three files exist unchanged under `docs/landing-assets/`, and the landing page's images, fonts and licence notices are still in the output.

- **AC-OPS-02.** Given a built output that holds a `.json`, `.md` or `.map` file, or a script containing `sourceMappingURL`, when the CI check runs, then it fails and names each file; given a clean output, it passes.

- **AC-OPS-03.** Given the review stack's dump has been written, when the scheduled off-host copy next runs with no one starting it, then the off-host copy holds that dump, and the freshness check reports its time and age; given no new copy within the freshness window, the check exits non-zero naming the stack and prints no secret.

- **AC-OPS-04.** Given a marker task written before the newest dump and a second written after it, when the drill restores the newest off-host copy into a temporary database, then the application answers Healthy on it, Operations shows no problem once evaluation settles, the first marker exists, the second does not, a known task is found by key and a report exports; the drill record gains a row with the measured data-loss window and time to recover; and the live database and volumes are unchanged.

- **AC-OPS-05.** Given no recorded approval from Jay for pilot or company data, then no pilot dump is in the off-host copy; given a review dump, the pilot drill script refuses it, and given a pilot dump, the off-host drill refuses to restore it on the review path.

- **AC-OPS-06.** Given an outbox event whose project evaluation fails 10 times, then its `dead_lettered_at` is set, the worker no longer selects it, `/health` answers Healthy once no live event waits more than 10 minutes, the pending-changes count leaves it out, and Operations shows `OutboxDeadLettered` with a count of 1, which the watchdog logs; given a live event waiting 11 minutes, `/health` answers Degraded as today.

- **AC-OPS-07.** Given a dead-lettered event, when an Admin chooses Retry on Admin > Operations, then its attempts return to 0, `dead_lettered_at` clears, the worker processes it on its next pass and it leaves the list; Retry all does the same for every dead-lettered event; a Project Manager calling the retry route gets 403; and after the migration, an event that was unprocessed with 10 attempts is listed as dead-lettered.

- **AC-OPS-08.** Given a screen throws while rendering, then the page shows the calm message, a reference made of the route pattern, the build when present and 6 characters, Reload and a My Work link, and no stack or item text; exactly one client-error report carrying that reference is posted; and axe reports no violation on the page.

- **AC-OPS-09.** Given a save answered 500 with `traceId` X, then the message shown ends with "(ref X)"; given a 409 or 422 answer, the message is the same as today.

- **AC-OPS-10.** Given a signed-in user posts a client-error report, then the answer is 204, one log line holds the reference, route pattern, build and user ID with newlines removed, and no database row is written; a body over 8 KB is refused with 400; the eleventh report from the same person within a minute gets 429; an anonymous post gets 401.

- **AC-OPS-11.** Given an uncaught error and an unhandled rejection on one page, then each is reported once, with quoted strings in the message replaced; a rejected `ApiError` with status 404 is not reported; a failing report is not reported again; and a page that throws 7 distinct errors sends 5 reports.

- **AC-OPS-12.** Given a local-password or social session copied before sign-out, then after sign-out the copy gets 401 and the person's other session still works; given the review or pilot probe with its copied-cookie check removed, the mocked probe test fails; given the review probe in social mode, its output has a `SKIP` line for the copied-cookie replay naming the server test.

- **AC-OPS-13.** Given two identical task creates with one `Idempotency-Key` sent at the same time, then exactly one task exists, one answer is 201 and the other is either the replayed 201 with `Idempotent-Replayed` or 409 `idempotency_in_progress`; a later retry with that key gets the replayed 201.

- **AC-OPS-14.** Given a keyed create that is refused (400 or 422), then no receipt remains and a corrected retry with the same key runs; given a pending claim older than the abandon window, or a receipt older than one day, a request with that key runs; the existing `IdempotencyTests` and the planning idempotency tests pass unchanged.

- **AC-OPS-15.** Given responses from `/health`, `/api/v1/me` and the SPA page, then each carries the four security headers with the listed Content Security Policy directives, and the API response carries `Cache-Control: no-store`.

- **AC-OPS-16.** Given the Production or the Staging environment, then the development sign-in header is ignored and `/api/dev/users` never answers 200; given Production with LocalPassword or with `Seed:ReviewDemo` true, the application refuses to start; and both pilot compose services set both seed flags to `"false"`.

- **AC-OPS-17.** Given every route under `/api/v1/admin` and the inline-checked evaluate route, when each is called as a team member and as a Project Manager, then each answers 403 and the activity log gains no row; given a new Admin route without an Admin check, the sweep fails.

- **AC-OPS-18.** Given a save answered 502 or 503, then the person sees "Tuesday is updating. This change was not saved. Try again in a moment." and never "HTTP 502"; given a read answered 503, the read message; given a save with no answer, the "may not have been saved" message; and a read with no answer is retried as many times as today.

- **AC-OPS-19.** Given 3 unread notifications, then the tab title is "(3) Tuesday"; after Mark all read it is "Tuesday"; given 120 unread, "(99+) Tuesday"; the landing page keeps its own title; and no request is added.

- **AC-OPS-20.** Given any notification email or daily digest, then its footer names Tuesday and contains neither "Coordination Hub" nor "the Hub"; its subject is unchanged.

- **AC-OPS-21.** Given a person chooses Comfortable in one browser, then the account's `dense_rows` is false and a second browser with no stored choice opens Comfortable; a browser with its own stored Compact choice keeps Compact; and a person who never chose sees compact rows.

### 48.11 Decisions and deferred proposals

Each decision below uses its default until Jay decides otherwise. The rows are in `specs/DECISIONS.md`, which packet 039 extends (FR-DLV-26); a pending row blocks nothing in this packet's code.

| # | Decision | Default used |
|---|---|---|
| D1 | Which served files move, and the allowlist | `capture.json`, landing `README.md` and `fonts/sources.json` move to `docs/landing-assets/`. Historical landing images stay served. Extension allowlist plus a `sourceMappingURL` check; no scan of file text for host or branch names. |
| D2 | Where the off-host copy lives, and pilot data | The encrypted repository Jay approved for review recovery data, through the existing export path. No new host, service or paid storage. Pilot or company dumps join only after Jay records approval. |
| D3 | Recovery targets and the freshness window | §22's 15-minute RPO and 8-hour RTO stay the written targets. The drill records measured values against them and against the proposed 24-hour off-host RPO (`docs/pilot/production-readiness.md:77`). Jay accepts or rejects; §22 changes only by his decision. Freshness window: 26 hours. |
| D4 | Dead-letter limit, `/health` and retry audit | 10 attempts, the worker's existing limit, as a named constant rather than a setting. `/health` stays Healthy when only dead-lettered events remain; they show on Operations and in the watchdog's alerts. A retry writes an application log line, like `/admin/evaluate`, not an activity-log row. |
| D5 | Error reference and report limits | Route pattern, build when present, 6 random characters. Reports from signed-in users only; errors before sign-in are not reported. Log only; 8 KB body; 10 a minute per person; 5 a page load. |
| D6 | Hosted check of social sign-out | The server tests cover it. The hosted social replay check is a human check, listed in `specs/human-only.txt` (§43, packet 039). |
| D7 | Idempotency in-progress answer | 409 `idempotency_in_progress` with `Retry-After: 1`; abandon window 2 minutes. The generic path keeps comparing the path only, not the body. |
| D8 | Copy when the outcome is unknown | 502 and 503 say "was not saved"; 504 and no answer say "may not have been saved". |
| D9 | Product name in server text | Footers only. Email subjects ("Hub digest", "Hub weekly summary", which tests pin), "Open in the Hub" and in-app server messages that say "Hub" are unchanged in this packet. |
| D10 | Tab title and density | Notification count only, capped at "99+", no page name. The browser's stored density wins where one exists, as today; the account value applies elsewhere. |
| D11 | Visible changes without a flag | The dead-letter list with Retry (S03), the error page and trace reference (S04), the updating messages (S10), the tab title count (S11), the email footers (S12) and the saved density (S13) ship unflagged as corrections and are Tier B. The other slices change nothing users see. |

Deferred proposals, outside packet 044, each to be raised as its own addition:

- a scan of served text for internal host and branch names;
- per-page tab titles;
- renaming email subjects and the remaining "Hub" server strings;
- a runtime refusal of the review seed by stack name;
- a permission sweep over every mutating endpoint against the §8 matrix, beyond the Admin routes;
- a closed list of failure reasons for dead-lettered events;
- automatic retry of dead-lettered events;
- comparing the request body on the generic idempotency path;
- automatic retries of writes from the client, which this packet makes safe but does not add;
- a signed-out event pushed to other open tabs once live updates exist (§46);
- off-host WAL archiving toward the 15-minute RPO, which waits for D3.
