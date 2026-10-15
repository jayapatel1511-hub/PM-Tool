# Feature Specification: Operations and Security Hardening

**Packet**: 044 | **Feature directory**: `specs/044-operations-and-security-hardening` | **Date**: 2026-10-14
**Status**: Specified, not started.
**Input**: Jay's 2026-10-14 instruction to build everything in the build-approach report, additive only. This packet takes the report's roadmap items 1, 2, 6, 7, 8, 9, 11 and 17.
**Source**: product specification §48 (§48.1 to §48.11) in `spec-parts/23-operations-and-security-hardening.md`. Related contracts: §13.0 (density), §13.15 (Admin screens), §17.6 (email), §21 (security), §22 (backup and recovery targets), §23.5 (outbox), §23.6 (session management), §23.8 (observability), §24.2 (`outbox_event`), §25.6 (errors), §25.10 (idempotency), §31.1 (AC-AUTH-04), FR-AUTH-01, FR-PLN-25 and AC-PLN-13.

## User outcome

Nothing about how Tuesday was built is served to the public. A backup leaves homedev on its own schedule and has been restored from where it lands. One failed background event no longer keeps `/health` Degraded, and an Admin can retry it. A tester who hits an error sees a page with a reference they can quote, and every server error shows its trace ID. A retried create cannot make two items. Security behaviour that matters is pinned by tests. The copy tells the truth: "Tuesday is updating" instead of "HTTP 502", the unread count in the tab, Tuesday in email footers, and a density choice that follows the person.

## Scope

1. **Served files** (§48.2; FR-OPS-01, FR-OPS-02): move `capture.json`, the landing `README.md` and `fonts/sources.json` out of `web/public`; a CI allowlist check on the built output.
2. **Off-host backup and restore drill** (§48.3; FR-OPS-03, FR-OPS-04, FR-OPS-05): diagnose why the scheduled off-host copy never ran, fix it in the existing encrypted repository, add a freshness check, and run a measured drill from the off-host copy.
3. **Outbox dead-letter** (§48.4; FR-OPS-06, FR-OPS-07, FR-OPS-08): mark an event after 10 failures, leave it out of `/health` lag, show it on Admin > Operations with Retry and Retry all, and alert on it.
4. **Error capture** (§48.5; FR-OPS-09, FR-OPS-10, FR-OPS-11, FR-OPS-12): a root error page with a reference, "(ref traceId)" on 5xx messages, and a log-only, rate-limited `POST /api/v1/client-errors` fed by the error page and window listeners.
5. **Copied-cookie probe** (§48.6; FR-OPS-13): keep the existing server tests and probe lines as gates; add a mocked test of the review probe and an explicit SKIP line in its social mode.
6. **Idempotency** (§48.7; FR-OPS-14, FR-OPS-15): claim the key before the handler runs; replay, reuse refusal and planning's atomic path stay as they are.
7. **Guard tests** (§48.8; FR-OPS-16, FR-OPS-17, FR-OPS-18): security headers, Production and Staging start-up refusals with the pilot seed flags, and an Admin route sweep.
8. **Honest copy** (§48.9; FR-OPS-19, FR-OPS-20, FR-OPS-21, FR-OPS-22): the updating message, "(n) Tuesday" in the tab title, Tuesday in email footers, and density saved to the account.

§48.1 sets the boundary; §48.10 holds the acceptance criteria; §48.11 holds the decisions and deferred proposals.

## Where today's behaviour is kept

- The landing page, its images, fonts and licence notices stay served.
- `/health` keeps its shape, apart from the `build` field §46 adds (FR-LIVE-02), and its 10-minute rule for live events. Only dead-lettered events leave the lag.
- Idempotency keeps replay, the 422 reuse refusal, the one-day window and planning's own receipt. Only the race changes.
- 4xx messages, the Not Found route and query retries are unchanged.
- Density stays compact by default, and a browser's stored choice still wins in that browser.
- Email subjects and in-app server text are unchanged.
- No runtime start-up rule changes; the guard tests pin today's.

## Non-goals

- The build stamp, reload toast and build in `/health`, route splitting and live updates (§46, packet 042).
- The aggregate route counter (§51, packet 047).
- Third-party monitoring, visitor identifiers, usage telemetry or any external error service.
- Changing §22's 15-minute RPO or 8-hour RTO before Jay decides (D3).
- The deferred proposals in §48.11: host and branch text scan, per-page titles, renaming subjects, a stack-name seed refusal, a full mutating-endpoint permission sweep, failure-reason lists, automatic dead-letter retry, body comparison on the generic idempotency path, client write retries, a signed-out push to other tabs, and off-host WAL archiving.

## Decisions (defaults apply until Jay decides; rows are in `specs/DECISIONS.md`, which packet 039 extends)

| # | Decision | Default |
|---|---|---|
| D1 | Files that move; allowlist | Three metadata files to `docs/landing-assets/`; extension allowlist plus `sourceMappingURL`; no text scan. |
| D2 | Off-host location; pilot data | The encrypted repository Jay approved for review data; pilot or company dumps only after his recorded approval. |
| D3 | Recovery targets; freshness window | §22 targets unchanged; drill measures against them and the proposed 24 h; Jay decides. 26-hour window. |
| D4 | Dead-letter limit; `/health`; retry audit | 10 attempts as a constant; Healthy when only dead letters remain, which show on Operations and alert; retry logs a line, no activity row. |
| D5 | Error reference; report limits | Route pattern, build when present, 6 characters; signed-in only; 8 KB, 10 a minute, 5 a page. |
| D6 | Hosted social sign-out check | Server tests; hosted check is human-only. |
| D7 | Idempotency in progress | 409 `idempotency_in_progress`, `Retry-After: 1`; abandon after 2 minutes; path-only comparison kept. |
| D8 | Unknown outcome copy | 502/503 "was not saved"; 504 and no answer "may not have been saved". |
| D9 | Product name in server text | Footers only. |
| D10 | Tab title; density | Notification count, "99+" cap, no page name; stored browser density wins. |
| D11 | Visible changes without a flag | S03, S04, S10, S11, S12 and S13 ship unflagged as corrections and are Tier B. |

## Acceptance

AC-OPS-01, AC-OPS-02 (served files); AC-OPS-03, AC-OPS-04, AC-OPS-05 (backup and drill); AC-OPS-06, AC-OPS-07 (dead-letter); AC-OPS-08, AC-OPS-09, AC-OPS-10, AC-OPS-11 (error capture); AC-OPS-12 (copied cookie); AC-OPS-13, AC-OPS-14 (idempotency); AC-OPS-15, AC-OPS-16, AC-OPS-17 (guard tests); AC-OPS-18, AC-OPS-19, AC-OPS-20, AC-OPS-21 (honest copy). Text in §48.10. AC-OPS-03, AC-OPS-04 and AC-OPS-05 are checked by an operator run on homedev and go in `specs/human-only.txt` (§43, packet 039); their scripts also have mocked tests. Every other AC is cited by an automated test in the slice that adds it.

## Depends on

- No packet blocks this one. Each slice starts from `origin/main` after PR #37 merges.
- Packet 039: where the new CI steps sit (the current single job, or lines in 039's `tools/ci_steps.txt` once it exists), `specs/human-only.txt`, `specs/DECISIONS.md` (rows `044-D1` to `044-D11`) and the merge tiers. Not a blocker.
- Packet 042: its S06 writes the Vite manifest into `wwwroot/.vite/`, which S01's check skips (FR-OPS-02).
- Packet 042: its build stamp, once landed, fills the build part of the error reference. Not a blocker.
- Packet 040: if its typed helper lands first, the `api.ts` changes stay inside `api()`, which the helper calls.
- Slices S14 and S15 need Jay's sudo for any root-owned unit and his recorded approval before pilot data leaves homedev.

Unblocks: packet 042's change-event flag is not turned on until S02 lands, since a stranded event would silence live refresh (§46); its lazy-route slice comes after S04, so a failed chunk load shows the error page.

## Flag

None. Every slice is a fix, a guard or a copy change complete in its own pull request; no existing feature is flagged. The slices users can see ship unflagged as corrections under D11 and are Tier B.

## Wording rule

Code, interface strings, specification text and commits name no third-party product.
