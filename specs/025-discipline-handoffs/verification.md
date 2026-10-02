# Verification: Discipline Handoffs and Acceptance

**Date**: 2026-09-27
**State**: Initial handoff increment implemented and automated checks passed. Full packet acceptance, deployment and pilot acceptance remain pending.

## Verified implementation

The increment adds the project handoff workflow, immutable submission/source snapshots, receiver evidence, owner reassignment, transactional request receipts, project UI, search, filters, saved views, CSV/XLSX export, audit history and scoped notifications/digests. The additive `DisciplineHandoffs` EF migration starts existing projects' handoff sequence at 1. Receiving work retains its own status and dates.

The tested implementation commit is `0c2d37b4b90c693167a8a44ddbd22f5184d813f0`. [CI run 36288680255](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36288680255) completed successfully. Later documentation-only commits record these results; they do not change the tested application code.

| Check | Actual result |
|---|---|
| Backend Release build | Passed on .NET 10; existing test-analyser warnings remain |
| PostgreSQL integration and domain suite | 360 passed, zero failed, zero skipped; fresh database migrated by HubFactory |
| Rules engine branch coverage | 97.6%, above the 95% gate |
| Feature service line coverage | 91.6%, above the 70% gate |
| Frontend build and lint | Passed; 78 lint warnings, zero errors; existing bundle-size and CSS-selector warnings remain |
| Chromium handoff workflow | Create → submit → accept → incorporate passed with mocked API responses; four distinct command IDs, zero JavaScript errors and zero unexpected API routes |
| Dialog accessibility | axe WCAG 2.1 A/AA checks passed on the populated draft and final receipt dialogs; zero violations |
| Dependency checks | NuGet scan and npm audits reported no vulnerabilities in the checked dependency sets |
| Specification traceability | 612 IDs and 236 sections cited, zero gaps or unknown IDs |
| EF model drift | Local `migrations has-pending-model-changes`: no changes since the migration |
| Diff hygiene | `git diff --check` passed |

Browser screenshots and the JSON report are available in the [handoff-browser-evidence artifact](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36288680255/artifacts/10921072293). Reproduce with `npm ci`, `npm run build`, `npx playwright install --with-deps chromium --only-shell`, and `npm run test:handoffs` in `web/`.

## Acceptance evidence and limits

- AC-HND-01/02: separate needed/promised dates, visible mismatch, named-person acceptance, separate incorporation and unchanged receiving task state/date passed in API tests.
- AC-HND-04: required return/resubmission reasons, preserved revisions, named actors and protection against self-receipt through reassignment passed. A prior submitter remains ineligible after another sender resubmits.
- AC-HND-05: independent receipts, restricted-project list/detail/options/search/export filtering and permitted list/export reconciliation passed.
- Concurrent duplicate create/submit commands return one result and create one receipt/notification; changed-payload request-ID reuse and stale versions are refused. Cross-project targets, unavailable owners, immutable-record edits, lifecycle restrictions and delivery-time access revocation passed.
- **AC-HND-03 is now covered by packet 027** on the review/change branch: after B is published, incorporated A and its receipt history remain unchanged, exactly one pending assessment links the InputUse/handoff target, and receiving task state is unchanged. This passed in implementation commit `d2be91138487d17cf4d903b040dd31640a14b89e`, [CI run 36319125624](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/36319125624), with 373 total tests passing. Earlier handoff-only results above remain historical.

The UI test uses mocked API responses. It does not verify browser-to-real-API integration or every UI path. Full keyboard/screen-reader review, additional browsers, concurrency load, performance, migration against an existing populated deployment, operational hardening, real tenant sign-in/mail and pilot acceptance remain unverified. No deployment or production-readiness claim is made.

## Verification history

Local .NET SDK 10.0.401/runtime 10.0.12 compilation and 193 domain tests passed, as did the Node 24.19 frontend build/lint. Local PostgreSQL was unavailable and Chromium downloads returned truncated archives; PostgreSQL and browser execution therefore used CI.

The first CI run (36287819927) passed 358/360 tests. Two new tests had incorrectly passed visibility to a project-create endpoint that does not accept that field. The fixture now sets and asserts restricted visibility. The second run (36288410995) passed 359/360; the email fixture could sit beyond the worker's first 100 queued messages in the shared test database. It now sorts first so the worker executes its suppression check. The original security assertions were retained, and the final run passed all 360 tests.

The documentation-only checks in `docs/coordination-spec-validation.md` remain historical; they are not runtime evidence for this increment.

## Local synthetic browser-to-API check on the review branch

Against the current local API and persistent `hub_review_local` database, a real Chrome session signed in as synthetic Jay through Development auth and created DEMO-101-H001 from the existing P01 source deliverable to Taylor's coordination task. Jay submitted it; a separate Taylor browser session accepted it with criteria evidence and then recorded incorporation. API readback showed Incorporated, three transition receipts, one retained submitted revision and an incorporated revision link. The browser reported zero JavaScript errors. After the detail dialog reached full opacity, an axe WCAG 2.1 A/AA scan found zero violations or incomplete checks. This confirms the exercised local path with synthetic data. It does not verify interim password sign-in, homedev deployment, other packet scenarios, source replacement, manual assistive-technology use or company acceptance.


## 2026-10-01 Codex recovery checkpoint

The integrated handoff browser workflow (create, submit, accept, incorporate) passed with mocked API responses, no page errors and no settled-dialog axe violations. The harness now supports installed Chrome and waits for dialog animations before contrast checks. This does not prove the current homedev release.

## 2026-10-02 local browser-to-API acceptance rehearsal at b155601

Independent rehearsal against the real API on worktree head `b155601` (fresh local database `hub_agent_verify2`, synthetic projects VER-201 open and VER-202 restricted, seeded Development users). Separate Development-auth Chrome 154 sessions (Playwright 1.61.1) acted as each named person; API calls were used only for setup, readback and direct-permission probes. axe-core 4.13 (WCAG 2.1 A/AA, injected by init script) found zero violations in every handoff screen and dialog scanned. Its colour-contrast "needs review" results came from aria-hidden status glyphs or from scans taken 0.4 s after a dialog opened; re-scans of the handoff and review detail and review-create dialogs after 1.2 s reported none. Browser console errors were limited to the HTTP 400/403/404/409 responses of deliberate refusals; no page errors. The same scenarios run earlier on `70e47bd` gave identical outcomes.

| Scenario | Result | Evidence |
|---|---|---|
| Create from source deliverable; draft without promise | PASS | Revision, sender, receiver and needed-by (task due) prefilled; draft saved |
| AC-HND-01 submit needs promise; promised after needed | PASS | Submit refused while undated; both dates and "Promised after needed date" shown to sender and receiver; task dates unchanged |
| AC-HND-04 sender accepts / return without reason | PASS | Sender's Accept disabled with reason, API 403; empty return blocked in browser and API 400 |
| FR-HND-02/03 return, resubmit with response and revision B, clarification round | PASS | Actor and reasons recorded; revisions A and B both preserved |
| AC-HND-02 accept is not incorporate | PASS | Accepted by Alex with criteria outcome; target task still Not Started |
| FR-HND-06 blocker removal vs dependency | PASS | Blocker group disappears on acceptance; downstream task still blocked by its task dependency |
| Incorporate the accepted, resubmitted revision B | **FAIL** | Refused: "Retaining the older revision requires a PM or responsible discipline lead to approve a reason." See defect below |
| AC-HND-05 two receivers, restricted project | PASS | Civil accepted, Structural still Submitted; PM list and CSV export 2/2; non-members Taylor and Priya: UI not-found, search and coordination aggregate omit VER-202, API list/detail/export/transition 404 |
| Unrelated member (Sam) and Read Only viewer (Rita) | PASS | Actions hidden or disabled with reason; API 403 for incorporate, cancel and create |
| Two-context stale draft edit | PASS | Second editor sees "changed by Jill Martin… Reload"; his typed text stays in the form; Jill's saved values kept |
| PM cancellation; reassignment to sender | PASS | Cancel needs a reason (browser and API 400) and keeps history; self-receipt reassignment refused |
| FR-HND-07 replay (API) | PASS | Two concurrent and one later submit with one request ID: one result, one history event, one revision, one receiver notification |
| FR-HND-04 change after acceptance (VER-201-H004) | PASS | After survey C was published the receipt stayed Accepted with unchanged history; a pending assessment was listed; incorporation of B was refused until Omar's evidenced Unaffected disposition and Taylor's retention approval, then succeeded with InputUse = B and head = C |
| Removed owner (API) | PASS | Receipt flags `ownersAvailable=false`; reassignment to the removed person refused |
| Lifecycle (API): On Hold, Complete, Archived, Cancelled | PASS | On Hold suppresses overdue and allows receipt; Complete refuses non-PM writes and PM writes without reason; Archived and Cancelled refuse all writes, reads still allowed |

**Defect (high): a corrected resubmission cannot be incorporated.** Repro: Draft from VER-201-D001 rev A → submit → receiver returns with reason → sender sets the deliverable revision to B, edits the draft to declared revision B and a new link, resubmits with a response → receiver accepts (the observed run also had one clarification round) → Record incorporation. Expected: Incorporated with revision B. Actual: HTTP 400 with the retention message above. The resubmission snapshot creates an unpublished, non-head SourceRevision for B because `Coordination.Snapshot` (`src/Hub.Api/Infrastructure/Coordination.cs`) only adds a source head when none exists, and the Incorporated branch of `HandoffEndpoints.Move` (`src/Hub.Api/Features/Handoffs.cs`) treats any non-head receipt as a retained older revision. Registering B later in Changes then asks the receiver to assess "B replaced by B".

**Lower-severity findings.** The handoff detail's change-assessment list includes notices for unrelated sources on the same receiving task (VER-201-H004 listed the geotechnical notices CH001 and CH002 beside the survey notice); the query in `HandoffEndpoints.Detail` matches on `TargetId` alone. The refused undated submission says only "One or more fields are invalid. Required." without naming the promised date.

NOT RUN: AC-HND-03 incorporated-A-then-B (covered by the earlier local evidence), keyboard-only and screen-reader use, other browsers, load, hosted/homedev and company acceptance. T006 stays open.
