# Verification: Revision awareness and change impact

**Date:** 2026-09-27
**State:** Foundation implemented in draft PR #3; automated checks passed. Full packet/pilot acceptance and deployment remain pending.

## Implemented

The additive `ReviewsAndRevisionChanges` migration extends the shared handoff source-revision store and starts existing projects' review/change key sequences at 1. Project-locked, version-checked commands commit evidence, activity, notifications and retry receipts together. The project screens include named actions, external source links, retained history, filters, saved views and CSV/XLSX exports. Search, scoped in-app notifications and digests reuse the existing access checks.

Review manifests are fixed per round, with fresh decisions for every new round. Reviewer independence includes original authors, current work owners and response provenance. Required packages gate both Ready to Issue and issue. Change notices explicitly supersede registered revisions, identify linked consumers, and keep acknowledgement, assessment, retention, adoption and correction verification separate.

## Actual checks

Implementation commit `d2be91138487d17cf4d903b040dd31640a14b89e` passed [CI run 36319125624](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36319125624): **373 tests, zero failures/skips, 97.6% rules branch coverage and 91.5% service line coverage**. The final commit only records verification/status documentation; it does not change the tested application code.

The successful run includes the Release build, migrations on a fresh PostgreSQL 17 test database, API/domain suites, frontend TypeScript/Vite build, lint, dependency checks and traceability. Compiler/analyser, CSS and bundle-size warnings remain; lint reported 88 warnings and zero errors. The checked NuGet and npm dependency sets reported no vulnerabilities.

The Chromium tests use mocked API responses. The handoff flow records four commands; the review/change flow records nine. There were zero JavaScript errors. axe WCAG 2.1 A/AA checks reported zero violations in the handoff draft/receipt and review creation/approval, source registration, assessment and closed-notice dialogs. These checks do not prove real browser-to-API operation or full accessibility.

## Acceptance coverage

- AC-CHG-01: three linked uses receive exactly three pending assessments; duplicate added targets are deduplicated and unlinked work is not claimed as assessed.
- AC-CHG-02: acknowledgement leaves Pending Assessment unchanged and closure is refused.
- AC-CHG-03: an evidenced Unaffected disposition and independent PM/responsible-lead retention approval keep InputUse on A while the registered head is B.
- AC-CHG-04: simultaneous retries return the same result; stale adoption and competing publication cannot overwrite a changed head. A reused request ID with different content is refused.
- AC-CHG-05: Update Required links a single-owner correction task and effort/date estimates. Completion plus independent named verification is required. Source publication never changes task dates or completion.
- AC-HND-03: an incorporated A receipt remains on A after B is published; the InputUse/handoff target receives one pending assessment and original receipt history remains intact.
- Scoped list/detail/options/input-use/search/export, immutable registrations, audit events and archived writes have automated coverage.

## Reproduce and limits

Run the repository CI workflow: `dotnet restore Hub.slnx`, Release build, `dotnet test Hub.slnx --no-build -c Release --collect:"XPlat Code Coverage" --results-directory coverage`, and `python3 tools/coverage_gate.py coverage`, with PostgreSQL on port 55432 as configured in `.github/workflows/ci.yml`. In `web/`, run `npm ci`, `npm run build`, `npm run lint`, install Playwright Chromium, then `npm run test:handoffs` and `npm run test:coordination`. Browser screenshots and JSON reports are in the [review/change artifact](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36319125624/artifacts/10932485015) and [handoff artifact](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36319125624/artifacts/10931334739).

Local checks also included 207 passing domain tests, frontend build/lint and diff/traceability checks. Full PostgreSQL and browser execution used CI. Traceability covers 612 IDs and 236 sections without gaps.

Packet 031 still supplies the design-basis/requirement adapters. Real browser-to-API scenarios, a representative populated-database migration rehearsal, manual keyboard/screen-reader and other-browser checks, concurrency/load testing, real tenant/mail, operational hardening and pilot acceptance remain unverified. No deployment or merge occurred. T006 remains open for full acceptance; implemented automated cases are recorded above.

## Verification history

The first run (36318491151) passed 370 of 371 tests. A new retry test expected HTTP 400, while the existing business-rule convention correctly returned 422. Its expectation was corrected without changing the rule. Run 36318803727 passed all 372 tests, including the added handoff/change regression. Final run 36319125624 passed all 373 tests, additionally covering preserved response authorship after reassignment and scanning the active nested assessment dialog.

## Local synthetic browser-to-API change check

Jay published DEMO-101 P02 as a replacement for P01 in a real local Chrome/API session. The resulting change notice was Open with one Pending Assessment on Taylor's linked task. The prior handoff remained Incorporated with its one submitted revision and three transition receipts. Taylor acknowledged the notice through a separate browser session; API readback showed `acknowledgedAt` set while the disposition remained Pending Assessment. Jay's attempted close returned HTTP 400 with the server's `assessments` reason and left the notice open. The shared coordination command dialog initially hid that reason behind a generic banner; it now displays the server's field messages, and the same browser refusal shows the actionable reason. Frontend build/lint passed after this fix; a settled-dialog axe WCAG 2.1 A/AA scan found zero violations or incomplete checks. Assessment disposition, retention/adoption, correction verification, a deployed browser run and full packet acceptance remain **UNPROVEN**.


## 2026-10-01 Codex recovery checkpoint

External-source supersession and publication now check authority over the existing source head, not just the discipline supplied in the request. Regression refused relabelling a Civil source by the Electrical lead and preserved the head; integrated review/change tests passed 19/19. Mocked source-replacement/assessment workflow passed; deployed acceptance remains UNPROVEN.

## 2026-10-02 local browser-to-API acceptance rehearsal at b155601

Independent rehearsal on worktree head `b155601` against the real API and a fresh local database (`hub_agent_verify2`). Taylor registered external source GEO-RPT-01 rev A (owner Jay, Civil); Alex, Omar and Diane each recorded use of A on their own tasks in separate Development-auth Chrome 154 sessions (Playwright 1.61.1); T0006 and D002 stayed unlinked. Taylor registered and published B as VER-201-CH001. API calls were used for setup, readback and direct-permission probes. axe-core 4.13 WCAG 2.1 A/AA found zero violations in the register, input-use, publish, notice, disposition, close-refusal and stale-publication dialogs. Console errors were only the deliberate refusals' HTTP 400/409 responses. Outcomes matched an earlier run on `70e47bd`.

| Scenario | Result | Evidence |
|---|---|---|
| AC-CHG-01 three linked uses | PASS | Exactly T0001, T0004 and T0005 got Pending Assessment; detection-boundary text shown; unlinked work not listed |
| AC-CHG-02 acknowledgement only | PASS | Alex's acknowledgement time recorded, status still Pending Assessment; notice owner Jay's close refused with the incomplete-assessment reason |
| Acknowledge vs close permissions | PASS | Assessor has no Close control and API close 403; unrelated Sam's acknowledge 403 and no assessment controls |
| AC-CHG-03 retain A | PASS | Diane's Unaffected disposition kept A; her own retention approval refused as not independent; close refused before approval; Taylor's approval completed it; InputUse still A, head B |
| Impact accepted: adopt B | PASS | Omar's Unaffected + Adopt new revision moved his InputUse to B and completed the assessment |
| AC-CHG-05 Update Required | PASS | Correction task, 6 h and 3 d recorded; Jay's verification refused until the task was Complete; then Resolved by Jay; no task due date changed |
| Close when complete | PASS | Jay closed CH001; register incomplete count 0 |
| Superseded revision (API) | PASS | A shown historical; a replacement for A refused with "The current revision changed…" |
| AC-CHG-04 competing publication, two contexts | PASS | C1 published; C2 publish from the second session refused with the stale-record guidance; C2 stayed Draft with no assessments; head C1 |
| Cancelled correction task (API) | PASS | Assessment stays Update Required and cannot be resolved |

At this head the correction task had never been readiness-assessed, so completing it first returned 422 `start_authorisation_required`; Jay recorded a start authorisation by API and Alex then completed it with acknowledgement and reason. Lower-severity finding: after the correction task is cancelled, resolution is refused with "The referenced work or discipline is unavailable or outside this project." and the task renders as "Unavailable or no longer a participant", which does not tell the owner that a new disposition is needed. NOT RUN: keyboard-only and screen-reader use, other browsers, load, hosted/homedev acceptance. T006 stays open.


## Hosted revision-change checkpoint — 2026-10-03 20:21–20:30 UTC

On executable `ac494a8ba70d50517f540ad7e0acced85d84897e`, three new labelled consumers exercised acknowledgement-only refusal, authorised retention of A, adoption of B, and correction completion plus independent verification. Closure was refused before correction completion and before verification; immutable A, adoption histories and due dates remained intact. Final Closed state and dispositions survived public signed-in browser reload. A separate two-connection publication race returned 200/409 from one saved head snapshot, left the losing Draft untouched and refused stale adoption without changes. These are bounded AC-CHG-01/02/03/05 and AC-CHG-04 publication/head/use slices; approval replacement under that race and exact internal server overlap remain unproven. See the [current hosted checkpoint](../../docs/reviews/2026-10-03-hosted-acceptance-ac494a8b.md).
