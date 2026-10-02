# Verification: Multidisciplinary reviews and comment closure

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

- AC-MRV-01: a missing discipline decision prevents approval and required deliverable issue. Both Ready to Issue and issue recheck the current manifest; a supplied issue link must match it.
- AC-MRV-02: responding leaves a blocking finding open for separate verification. The named resolver cannot close it. Withdrawal requires the independent verifier and coordinator acknowledgement.
- AC-MRV-03/05: a new revision requires a new round and fresh decisions. Earlier approvals, comments and manifests remain readable; unresolved findings carry provenance. Discipline removal requires PM authority, reason and impact review.
- AC-MRV-04: source-owner and bulk task reassignment cannot create self-review. Named reviewers, active project participants, captured authors and current owners are checked. A response author remains ineligible to verify that response after reassignment or round carry-forward (subject to the existing explicit self-review setting).
- Scoped list/detail/options/search/export, archived writes, audit events and replay-safe project commands have automated coverage.

## Reproduce and limits

Run the repository CI workflow: `dotnet restore Hub.slnx`, Release build, `dotnet test Hub.slnx --no-build -c Release --collect:"XPlat Code Coverage" --results-directory coverage`, and `python3 tools/coverage_gate.py coverage`, with PostgreSQL on port 55432 as configured in `.github/workflows/ci.yml`. In `web/`, run `npm ci`, `npm run build`, `npm run lint`, install Playwright Chromium, then `npm run test:handoffs` and `npm run test:coordination`. Browser screenshots and JSON reports are in the [review/change artifact](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36319125624/artifacts/10932485015) and [handoff artifact](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36319125624/artifacts/10931334739).

Local checks also included 207 passing domain tests, frontend build/lint and diff/traceability checks. Full PostgreSQL and browser execution used CI. Traceability covers 612 IDs and 236 sections without gaps.

Packet 031 still supplies the design-basis/requirement adapters. Real browser-to-API scenarios, a representative populated-database migration rehearsal, manual keyboard/screen-reader and other-browser checks, concurrency/load testing, real tenant/mail, operational hardening and pilot acceptance remain unverified. No deployment or merge occurred. T006 remains open for full acceptance; implemented automated cases are recorded above.

## Verification history

On the isolated review-release branch, publishing a replacement source revision now creates a fresh Draft review round
in the same project transaction. It retains the earlier round, copies the manifest with the new revision, resets all
discipline decisions to Pending, carries unresolved findings with provenance, and notifies the named reviewers and
coordinator. The focused PostgreSQL `ReviewChangeTests` suite passed (11/11), including a newly added case starting
from an Approved round. `git diff --check` passed. This closes the automatic new-round implementation gap identified
below, while T006 stays open for real browser/API, populated migration, accessibility and pilot acceptance.

Pre-merge review found that removing a deliverable from a new round left its old required-review gate attached, so Ready to Issue and issue would search for a manifest entry that no longer existed. The new-round command now requires team-management authority and a recorded removal impact for a dropped deliverable, then clears only gates still owned by that package. A PostgreSQL API regression test covers both refusals and both resulting gates. The focused review/change and handoff API tests passed: 21 passed, zero failed or skipped.

Earlier, superseding a source revision blocked approval and issue against the old manifest until a coordinator explicitly started a new round. The automatic publication path above supersedes that historical limitation.

The first run (36318491151) passed 370 of 371 tests. A new retry test expected HTTP 400, while the existing business-rule convention correctly returned 422. Its expectation was corrected without changing the rule. Run 36318803727 passed all 372 tests, including the added handoff/change regression. Final run 36319125624 passed all 373 tests, additionally covering preserved response authorship after reassignment and scanning the active nested assessment dialog.

## Local synthetic browser-to-API review and replacement check

Against the current local API and persistent `hub_review_local` database, synthetic Jay created and started required DEMO-101-RV001 with source P01 and Project Management reviewer Yagmur. Yagmur used a separate Development-auth browser session to record an Approved decision. API readback showed the package, round and assignment Approved with one manifest entry. Jay then published replacement source P02 through the real browser. The API opened a fresh Draft round with a Pending assignment and preserved the earlier Approved assignment in the Superseded round. Browser JavaScript errors were zero. This exercises one local review/revision path; it does not prove deployed individual-password sign-in, all permission and concurrency cases, populated migration rehearsal or pilot acceptance.


## 2026-10-01 Codex recovery checkpoint

Current source/review discipline authority is enforced when setting or changing required deliverable gates. A regression refuses another discipline lead attaching a gate and confirms the command rolls back. Review/change API tests passed 19/19; mocked review browser flow and dialog axe checks passed. Company reviewer acceptance remains UNPROVEN.

## 2026-10-02 local browser-to-API acceptance rehearsal at b155601

Independent rehearsal on worktree head `b155601` against the real API and a fresh local database (`hub_agent_verify2`), synthetic project VER-201 with Survey, Civil, Structural and Electrical leads. Each named person used a separate Development-auth Chrome 154 session (Playwright 1.61.1); API calls were used for setup, readback and direct-permission probes only. Package VER-201-RV001 (coordinator Taylor, then Jay) covered survey D001 and grading plan D002 with Civil (Sam), Structural (Diane) and Electrical (Omar) reviewers. axe-core 4.13 WCAG 2.1 A/AA found zero violations in the create, decision, finding, approved-detail, round-history, new-round and stale-conflict dialogs. Console errors were only the deliberate refusals' HTTP 400/403/409 responses. Outcomes matched an earlier run on `70e47bd`.

| Scenario | Result | Evidence |
|---|---|---|
| FR-MRV-04 independent reviewer at creation | PASS | Alex (D002 owner) as Civil reviewer refused; no package created; package created with Sam |
| AC-MRV-01 Electrical pending | PASS | Civil and Structural Approved, package In Review; D002 Ready to Issue refused by the review gate |
| Blocking finding prevents approval | PASS | All three disciplines Approved, package Changes Required, register shows one blocking finding |
| AC-MRV-02 response stays open | PASS | Resolver's response leaves it Responded; resolver has no Verify control and API 403; return-to-open, second response and originator verification then Approved; Ready to Issue then succeeded |
| AC-MRV-04 reassignment to the author | PASS | Refused; Civil assignment still Sam/Approved, package still Approved |
| AC-MRV-03 new round on revision | PASS | Publishing survey C opened round 2 (Draft) with three Pending decisions; round 1 Superseded with its approvals, read-only in the round history; open advisory finding carried with provenance text |
| FR-MRV-06 issue rechecks the current round (API) | PASS | With round 4 in Draft, PM issue of D002 refused with the review-gate message |
| AC-MRV-05 removing Electrical | PASS | Coordinator Jay (discipline lead, not PM) refused 403 "Only the Project Manager can do this."; PM without impact refused; PM with reason and impact opened round 3 without Electrical; earlier Electrical approval and the verified blocking finding remain inspectable |
| FR-MRV-07 blocking withdrawal | PASS | Withdrawn blocking finding kept the package at Changes Required until coordinator Jay acknowledged it, then Approved |
| FR-MRV-03 verifier reassignment (API) | PASS | Coordinator 403; PM without reason 400; PM choosing the resolver refused as not independent; PM choosing Yagmur (project Reviewer) with reason succeeded |
| Two-context stale decision | PASS | Sam's second tab got 409 with the "This record changed…" guidance; the first tab's In Review decision and rationale were kept |

Observation: after the required package returned to Draft, D002 still displayed Ready to Issue; issue itself was correctly refused. NOT RUN: keyboard-only and screen-reader use, other browsers, load, hosted/homedev and company reviewer acceptance. T006 stays open.
