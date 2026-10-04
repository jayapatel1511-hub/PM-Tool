# Verification: Weekly Planning Layer

**Date**: 2026-10-04
**State**: Locally implemented after `9d3e9b91`, uncommitted. Backend, model, performance, required web suites, complete route sweep, keyboard and visual/state checks PASS. **T023 remains BLOCKED/UNRUN for its manual screen-reader validation:** native VoiceOver control timed out before usable accessibility state. No commit, push, deployment or human acceptance is implied.

## Deployment authorization — 4 October, evening

Jay returned home and explicitly answered **“Yes, commit and deploy synthetic review.”** This supersedes the earlier uncommitted/no-deployment hold for the reviewed candidate. Current preparation is for the existing review stack only, preserving unrelated edits and all pilot/operational data. Host preflight verified the current review and pilot pointers at `29b3d7036f529b513fdc2ad47cff62a0b75f0b59`, review origin health HTTP 200 with the configured proxy headers, and the existing tunnel active. That release is an ancestor of the candidate. The candidate's 114 selected files and 315 frozen application/test/build sources had zero drift since final checks. GitHub authentication is valid when checked outside the network sandbox.

Prepare a reviewed commit, incorporate the approved Paper hero commit from origin/main, and pass the required draft-PR CI before transferring the exact committed tree. Interactive homedev sudo remains a user step. Deployment is not yet claimed complete. T023 stays BLOCKED/UNRUN and human acceptance/pilot remains pending; Jay authorized this synthetic review deployment with that limitation disclosed. Historical local-only status statements below describe the earlier snapshot.

## Latest release review loop

PASS for local software checks after independent review/fixes: exact app type-check, required lint with 87 baseline/current warnings and zero new diagnostics, synthetic frontend build, Handoffs/Coordination/Design Basis (5186)/Resources/Planner and local Release publish. Planner: 14 base flows plus six recovery regressions, 180 API calls, three created entries, two notices, seven axe scans, zero errors/unknown requests/violations. The regression repairs failed conflict reload draft loss, immediate stale-save lock, metadata action draft preservation, edited failed-save retry, permission revocation and time-away fresh-version/idempotency recovery. Final independent source review found no actionable defect in that bounded scope.

All 96 affected Planner/My Work six-role/four-width routes cold-rechecked PASS; preserved full UX final2 pass plus replacements has 1,956 unique combinations and zero findings. This is an affected-route recheck, not a second fresh full sweep. All 315 application/test/build source hashes remained unchanged during final verification. Logs: `/private/tmp/tuesday-*-release-final.log`; evidence `/private/tmp/tuesday-sweep-release-final/`. CI now includes the planner suite. Review/pilot synthetic-banner build separation is verified with dummy-only Compose configuration; no hosted auth settings changed.

Prior full backend/migration/coverage/scale evidence remains unchanged; no backend edit or database write occurred during this loop, so the full database suite was not repeated. The earlier application-check rows below are retained historical evidence for UX final3; new planner checks above supersede those repaired paths. Final spec/trace/whitespace checks pass after this evidence update. T023's manual screen-reader validation stays BLOCKED/UNRUN and unticked; human acceptance/pilot remains pending. Deployment is BLOCKED/NOT ATTEMPTED at the unresolved uncommitted instruction and interactive homedev sudo. No host authentication was attempted. See [the final release-loop report](../../docs/reviews/2026-10-04-tuesday-release-review-loop.md).

## Open decisions (T001)

PASS — the §39.12 defaults stand under Jay's instruction to implement this packet; no alternate answers were supplied. All 18 defaults relied on are reproduced below.

| # | Decision | Default used |
|---|---|---|
| 1 | Default weekly capacity: v1.1's 37.5 h or the existing 40 h | Keep `default_weekly_capacity_hours` at 40 h (§10.4, Q18). 37.5 h needs a decimal setting kind; propose it separately. |
| 2 | Planner thresholds | v1.1's values as Admin settings (§10.9): over 105 %, under 50 % for 2 weeks, stale after 28 days, at most 80 h per entry week, 12-week horizon (6–26). |
| 3 | Indicator names | Over-planned, Under-planned and Stale plan, distinct from Workload's Over-assigned and Under-assigned. |
| 4 | Who records time away | Supervisor (direct reports) or Admin, as availability overrides (FR-CAP-02 unchanged). Self-recorded time away is deferred because it would change FR-CAP-02's editors. |
| 5 | Whether the time-away command may clear overrides | Yes, for the same editors, audited. |
| 6 | Whether the allocated person sees their own approved allocations | Yes, in My Week, as read-only hours on projects they can view (AC-PLN-20); the §37.6 detail stays closed to them. |
| 7 | Supervisor read scope | Direct reports only (Q19); v1.1's all-staff read for Supervisors is deferred. |
| 8 | Project Managers and Discipline Leads creating planning entries | No; they propose resource allocations (§37.6). Project Managers with the system role can view members of projects they manage. |
| 9 | Person changing or commenting on a manager entry | No change; add an own entry. Comments on planning entries are deferred. |
| 10 | Person accepting Confirmed assignment | Not required; one owner action. |
| 11 | Other planners seeing that a Private draft exists | Nothing is visible. |
| 12 | Week or date grain | Planning entries use ISO-week ranges; resource allocations keep date grain; weekly sums reconcile them. |
| 13 | Coverage order | Confidence Confirmed, Expected, Possible, then start week, creation time, identifier. |
| 14 | Partial-view rule | Workload's rule: partial unless the viewer is an Admin or Executive. A finer rule that never leaks restricted existence is a follow-up. |
| 15 | Project status change | Entries are untouched and flagged "Project not active"; v1.1's unlink on archive is not adopted. |
| 16 | Global search | Includes visible entries by label and notes. |
| 17 | Notification channels | In-app on, email off; Stale plan in the digest only. |
| 18 | Pointer drag of planner blocks | Deferred; side-panel fields provide extend, shorten and move. |

Constitution review: PASS for the implemented scope. Pure `PlanningRules`, one accountable creator, externalized settings/copy, deny-by-default privacy and additive migrations are retained. The unchanged-behaviour guard passes; existing allocation, readiness and coordination sources/tests were preserved. The additive queued-email privacy guard prevents a previously visible planning record from leaking after withdrawal or access revocation.

## Spec Kit analysis and remediation

The repository's `/speckit-analyze` workflow was applied to this packet, using its prerequisite resolver, `spec.md`, `plan.md`, `tasks.md`, canonical §39/§8.11/§10.9 and the constitution. No extension hooks are installed. Jay explicitly authorized fixing the findings in the goal.

| Finding | Severity | Resolution |
|---|---|---|
| Premature verification/status wording in the plan and packet | Medium | Replaced with observed results and explicit pending human evidence. |
| Plan notification key/category and contribution presentation drift | Medium | Aligned to `planning.changed`, generic withdrawal without an identifier, `admin`/`data-correction` audit categories, contribution dialog and actual UI primitives. No canonical scope change. |
| Per-setting validation ranges needed exact correspondence | Medium | Each of the six planning settings has its own range tests; coordination lookahead remains 1–12. |
| SC-001 pilot and screen-reader evidence cannot be inferred from automated checks | Medium | SC-001 explicitly UNRUN human evidence. Manual screen-reader is a separate BLOCKED technical validation; T023 stays open. |
| AC-PLN-18 keyboard test initially omitted arrow/Tab/natural-focus assertions | Medium | Added strict assertions, reproduced Escape focus loss and repaired it by allowing the mounted-cell effect to restore focus; final test PASS without test-side refocusing. |

Coverage: 28/28 FR-PLN identifiers map to implementation tasks; all 20 AC-PLN criteria have evidence rows below. No unmapped requirement, uncovered implementation task, constitution exception, or remaining critical/high cross-artifact inconsistency was found. SC-002 reconciliation and SC-003 privacy have executable protocols. SC-001's actual-supervisor pilot is a human outcome.

## Application checks

Commands ran in this worktree. Browser suites use the handoff's `CHROME_EXECUTABLE_PATH`; PostgreSQL tests use temporary `hub_test_*` databases on 55432. Preview and scale data use only `pm-tuesday-preview-db` on 55433.

| Check | Result and evidence |
|---|---|
| API build | PASS; compiled successfully before the final full test run. |
| EF pending model changes | PASS — “No changes have been made to the model since the last migration.” `/private/tmp/tuesday-ef-model-final.log`. |
| Domain, permission, settings, schema, API and privacy cases | PASS in the final full suite, including migration down/up on a fresh database. |
| Full `Hub.Tests` suite with XPlat coverage | PASS — **1,036 passed, 0 failed, 0 skipped**, 5 m 25 s. `/private/tmp/tuesday-full-suite-final-isolated.log`. |
| `tools/coverage_gate.py` | PASS — Hub.Domain branches **96.6 %**, Features lines **94.2 %**. PlanningRules branch coverage **100 %**. `/private/tmp/tuesday-final-coverage-isolated/0b1433d3-cf64-4cd2-ae3f-f25665ab4dae/coverage.cobertura.xml`. |
| Existing-behaviour guard | PASS — existing Allocation/Workload/Readiness tests unchanged and pass in full suite; snapshot test compares actual workload JSON/CSV, allocation list and readiness before/after planning commands. Protected source/migration diff is empty; existing Workload edits are access modifiers only. |
| Preview migrations | PASS — `20261004022246_PlanningEntries` and `20261004035357_PlanningNotificationPrivacy` applied, verified in `hub.__ef_migrations`. Before the first migration, preview backup and packet-029 row-count/checksum comparison were captured; unchanged at that boundary. Later synthetic scale allocations intentionally add records. |
| Scale, 30 samples on local macOS arm64 | PASS — 12 people × 12 weeks, 500 entries, 50 confirmed allocations: grid p95 **26.32 ms** (limit 1,500); My Week p95 **14.90 ms** (limit 500). `/private/tmp/tuesday-planning-scale-final.json`. |
| Exact app type-check | PASS — `npx tsc -p tsconfig.app.json --noEmit --incremental false`. `/private/tmp/tuesday-tsc-ux-final3.log`. |
| `npm run lint` | PASS — 87 baseline warnings, 87 current, **0 new** compared by file/rule/message with `9d3e9b91`. `/private/tmp/tuesday-lint-ux-final3.log` and `.json`. |
| `npm run build` | PASS — `/private/tmp/tuesday-build-ux-final3.log`. Existing Vite native-loader and chunk-size notices remain. |
| Required handoffs, coordination, design-basis (5186), resources browser suites | PASS on UX final3 assets; required Chromium executable used. `/private/tmp/tuesday-handoffs-ux-final.log` and `/private/tmp/tuesday-{coordination,design-basis,resources}-ux-final3.log`. |
| `npm run test:planner` | PASS — 14 flows, 150 API calls, 3 created entries, 2 notifications; 5 axe scans with zero violations, zero page errors, zero unknown requests. Exact retained-body/idempotency retry, old-version conflict + explicit reload, and ordinary-user My Week/self-entry assertions included. `/private/tmp/tuesday-planner-ux-final3.log`. |
| Planner/My Work live axe audit | PASS in the fresh UX full sweep — all 96 base-route/role/width combinations, zero violations/page errors. The separate 36-case final12 baseline audit is retained in `/private/tmp/tuesday-a11y-final12.json`. |
| Complete live route/role sweep | PASS — fresh UX final2 has 1,956 unique combinations and zero findings. Measures page errors, axe, document/main/menu horizontal overflow, sub-12 px text, raw keys and four known obsolete palette colours at 1440/1024/768/375. Web source hashes stayed unchanged during this run. `/private/tmp/tuesday-sweep-ux-final2/{results,summary}.json`. Historical final7/final8 and interrupted UX failures are retained separately, not substituted for this fresh pass. |
| Broad desktop/phone state review | PASS — 974 role/desktop/phone contexts for normal, loading, empty, failed load, hover, focus, selected, disabled and read-only. All 101 earlier focus defects cold-rechecked PASS. 108 normal visual contact sheets reviewed. Editor protocol PASS: 364 observations, 1,764 assertions, zero final failures/errors; 101 held-save observations and 223 explicit NAs. Task protocol PASS in all 12 role/width contexts. Browser-only synthetic mutation responses; all live writes blocked. Evidence in `/private/tmp/tuesday-state-review-final2/`, `tuesday-focus-final/`, `tuesday-task-states-v2/`, `tuesday-editors-review-merged/`, and the two visual-review reports. |
| Manual screen-reader pass | BLOCKED/UNRUN — plan Validation step 10 remains unmet. Native VoiceOver app control timed out before usable accessibility state. Keyboard assertions, visible focus and axe are tested separately; no screen-reader acceptance is claimed. |
| Supervisor spreadsheet pilot (SC-001) | UNRUN — needs an actual supervisor and their existing spreadsheet; synthetic timing is not a substitute. |
| `build_spec.py --check`, `trace_spec.py --check`, `git diff --check` | PASS after final evidence updates. Source/trace: 660 IDs, 252 sections, zero uncited/unknown. Final local logs `/private/tmp/tuesday-{spec,trace}-ux-close.log`. |

## Additional UI/UX follow-up

Jay requested overlap/button placement/usability review, then explicitly requested wrapped menu rows and no homedev authentication while away. Project, Search, Admin and shared tabs now wrap; tablet breadcrumbs wrap complete label/chevron groups. Planner has one Close control, distinct stale labels and truthful denied/failed read states. My Work has one My Week heading without a false count and a phone filter disclosure that retains active tokens. Long dialogs keep title/Close outside the scrollable body. Time conflict recovery explicitly reloads the latest visible row, retains correction reason and blocks Save until reload succeeds; failed reload retains the draft, and no-longer-editable rows remove Save. Outside 034 these are UI-only changes.

Final UX type-check/lint/build and all five browser suites pass. Lint remains 87 baseline/current warnings and zero new diagnostics. Parent reviewed all 12 desktop/phone role-specific menu screenshots: zero page/main/menu overflow or page errors. The final dialog protocol has 108 records, 40 applicable and 68 NAs; all applicable cases retain draft/conflict values and title/Close after scrolling, with no sideways dialog overflow. Time recovery was independently reproduced with mocked 1440/375 responses and no live writes. An independent source review found no actionable defect in the UI changes; later breadcrumb geometry was verified by the parent.

The first expanded menu sweep was deliberately interrupted after 450 rows when it found 36 tablet breadcrumb overflows. Original evidence is preserved in `/private/tmp/tuesday-sweep-ux-interrupted/`. The repaired-source fresh full sweep completed all 1,956 unique combinations with zero findings in `/private/tmp/tuesday-sweep-ux-final2/`; historical final7/final8 evidence is not substituted for it. Full details and limitations are in [the additional UI/UX review](../../docs/reviews/2026-10-04-tuesday-ui-ux-review.md).

## Acceptance criteria

PASS below means the stated software behaviour was observed in automated tests or agent-operated browser checks; Jay's final visual acceptance is still pending.

| Criterion | Status | Evidence |
|---|---|---|
| AC-PLN-01 | PASS | API quick-add defaults/grammar and keyboard quick-add browser flow create exactly one weekly entry. |
| AC-PLN-02 | PASS | `PlanningPrivacyTests` and contract all-surface sweep: drafts alter no nonowner counts/totals/list/detail/history/exports/search/notifications/digest/activity; reason-required Admin correction is logged. |
| AC-PLN-03 | PASS | Atomic publish/withdraw/delete notice tests and browser publish/withdraw flow; withdrawal removes identifiers/details and sanitizes queued email. |
| AC-PLN-04 | PASS | Domain/contract exact 30 + 13 versus 40 arithmetic, thresholds 105/110 %, Possible not subtracted; negative capacity rendered in contribution browser check. |
| AC-PLN-05 | PASS | Domain deterministic coverage and API approved-coverage/calendar reconciliation; contribution shows original, covered and counted hours. |
| AC-PLN-06 | PASS | Domain availability rules plus API calendar parity; time away reduces capacity once. |
| AC-PLN-07 | PASS | Self-entered visibility/owner constraints and direct-report browser flow using a user with no system roles; manager entry stays read-only to its subject. |
| AC-PLN-08 | PASS | API range update and browser extend assert one entry spanning weeks, with matching row version. |
| AC-PLN-09 | PASS | Domain stale boundaries and API Still valid/history/digest tests; stale hours remain counted. |
| AC-PLN-10 | PASS | API task-estimate context derivation/reconciliation; contribution dialog labels context separately from remaining capacity. |
| AC-PLN-11 | PASS | Full unchanged suite + before/after actual-read snapshot; protected implementation files unchanged. |
| AC-PLN-12 | PASS | Restricted project and revoked-access tests filter before aggregation; partial label and suppressed Under-planned tested. |
| AC-PLN-13 | PASS | Concurrent receipt/idempotency tests; stale save and exact old-version conflict browser assertions; explicit reload fetches new values and version. |
| AC-PLN-14 | PASS | Single qualifier mapping; browser label scan and qualified Confidence/Visibility/Approval status labels, including self-entered and search links. |
| AC-PLN-15 | PASS | Permission matrix + API refusal tests; Read Only veto, owner authority, Supervisor scope and Admin correction reason enforced server-side. |
| AC-PLN-16 | PASS | Atomic range/stale/missing-version tests, parity with packet 029 rows/date versions and no-op handling; browser time-away dialog. |
| AC-PLN-17 | PASS | Synthetic scale measurements above, well within both p95 limits; environment and sample size recorded. |
| AC-PLN-18 | PASS | Strict browser four-arrow navigation, Enter quick add/save, Escape cancel and natural cell focus, Tab entry-panel access and exact-opener focus after close. 1440 editable, 1024 read-only, 375 notice; text/symbols supplement colour; axe zero. Separate plan screen-reader pass is BLOCKED/UNRUN. |
| AC-PLN-19 | PASS | Archive/unlink/move contract test keeps entry and flags Project not active; no lifecycle rewrite of ownership/history. |
| AC-PLN-20 | PASS | Own row without workload permission, safe read-only approved-allocation projection, no export permission escalation; exact own six-week My Week query tested. |

## Browser evidence boundaries

All six actual sign-in roles were inspected at desktop and phone widths. Save/failure/conflict protocols exercised representative editable fields and primary records on the real role-specific baseline; no permission was invented to force an inapplicable state. Templates and Admin retain their canonical desktop/tablet policy, and phone Milestone editing is intentionally unavailable. Sam's selected Time records are read-only; Lena and Rita had no own Time records in the inspected range. Those cases are explicit NAs, not successful saves. Parent Settings loading/error/retry was separately checked in four Priya/Jordan width contexts. The final cold visual recheck found no actionable defect in the repaired flows.

Held saves, long-label and server-error/conflict responses were synthetic browser fixtures; they verify interface state handling. Server persistence, privacy, concurrency and atomicity are established separately by the PostgreSQL/API tests. The audit does not claim every possible record or every conditional workflow was exercised. Original failed and superseded harness evidence is retained; only successful final cold records are counted.

## Success criteria and evidence limits

- **SC-001: UNRUN human pilot.** Software AC evidence is above; the under-one-hour spreadsheet exercise and Jay's approval still need people.
- **SC-002: PASS sampled software reconciliation.** Pure arithmetic cases, API exact-band/coverage/calendar/task-context tests, plus the visible Alex Oct 5 contribution reconcile normal 40, actual 32, away 8, additional 0, Confirmed 60 + Expected 17 and remaining −45. The contribution lists each day's capacity and approved/planning coverage; grid/cell use the same Under-planned lookahead.
- **SC-003: PASS adversarial software protocol.** Six-role matrix checks all read surfaces before and after visibility/access changes, including queued email delivery and saved-view/export results. No private existence/count leak was observed. This is scoped to the tested revision and roles.

All preview records are fictional, visibly labelled Synthetic preview. `seed_modules.py` was read in full and reviewed; its guard refuses non-preview targets. The separate `Readiness.cs:108` decision-blocker defect remains unchanged and did not block required checks; the omitted seed link is not claimed as tested. The four product decisions (brand mark, hero recapture, holiday/role removal confirmations, per-notification mark-read) remain with Jay.
