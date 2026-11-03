# Checkpoint21 — Board, capacity chart and Team locally verified

2026-11-01. **053 S13, S24 and S42 are locally checked; 28 of 49 slices are now checked.** The full 052/053 build goal remains active. This is local implementation and verification, not formal Accepted status, hosted deployment, pilot activation or human acceptance. No commit, push or deployment occurred. The primary checkout and prototypes were preserved.

## Implemented behavior

- **S13:** Review cards use the persisted Ready-for-Review timestamp through In Review, name the recorded reviewer, and show organisation-local waiting days. The configured threshold adds a triangle/text/amber presentation without changing canonical stalled-state arithmetic. Missing/future timestamps remain unavailable. Lane/swimlane figures describe actual loaded cards, preserve zero versus missing estimates, and name the actual current discipline lead or project manager without a fallback. The existing 500-card cap is disclosed.
- **S24:** Team Capacity adds one typed home-discipline comparison, a matching numeric table and one React-owned DOM chart from the same cached canonical grid and historical per-week membership. Capacity already includes time away; Confirmed already includes approved reservations; demand is Confirmed + Expected, with Possible separate. Monthly deficit is the sum of positive weekly group shortages. FTE uses the standard hours for the displayed weeks. Partial/provisional inputs leave deficit value and evidence unknown. The 2,000-expanded-row budget retains complete groups and distinguishes returned versus total groups. UI period/page focus preserves Copy/Back/Task-return context while staying out of canonical saved/export parameters. The original matrix and exports remain.
- **S42:** Both Teams group/filter by exact home-discipline, office and supervisor IDs, and by current unit only while its owning flag is enabled. Not set and unavailable remain distinct; duplicate names do not merge IDs. Existing authorised membership/certification cohorts remain. Open roles show separately with canonical Fill permission and never become people or capacity. Add-a-person uses the current organisation week and existing candidate-scoped planner facts; partial/provisional/outside-scope figures remain honest. Saved views have separate, flag-guarded project/workspace contracts.
- **052 project-header subset:** Both 120-character name forms fit a 64px header with full hover/focus text. Active health overrides retain both labelled pills and full Details/Why content. Target dates remain complete with long PM/client values. Grouped project Tasks retain full source provenance inline and in print, with the actual child at 401.5px rather than substituting its group summary. This does not close all of 052 S02/S09.

## Final verification

| Check | Final result | Durable evidence |
|---|---|---|
| Backend build | PASS, zero errors; existing warnings remain | `checkpoint21-runtime/backend-build.log`, `backend-build-repair2.log` |
| Focused .NET | 29 PASS: Board10, Team8, capacity comparison11 | `checkpoint21-runtime/focused-summary.json` |
| Adjacent .NET | 91 distinct latest outcomes PASS | `checkpoint21-runtime/adjacent-summary.json` |
| Distinct .NET total | **120 PASS**, no skipped | Focused and adjacent summaries have no overlapping cases |
| Cache | **14 PASS**, actual QueryClient invalidation/refetch | `checkpoint21-runtime/node-cache-summary.json` |
| App/fixture types, lint, SSR | PASS | `app-types-repair1.log`, `fixture-types.log`, `frontend-lint.log`, `ui-review.log` in the runtime folder |
| Production frontend | Build2 PASS | `checkpoint21-runtime/frontend-build2.log`, `production-build2-manifest.json` |
| Focused Chrome | **4/4 PASS** | `checkpoint21-runtime/browser-focused-final-summary.json` |
| Adjacent Chrome | **7/7 PASS** | `checkpoint21-runtime/browser-adjacent-results.json` |
| Backend source | 556 hashes match final freeze | `checkpoint21-runtime/csharp-freeze-repair2.json` |
| Web source | 403 hashes match final freeze | `checkpoint21-runtime/web-source-freeze-repair4.json` |
| Contract | Six new schemas/four optional properties; no path or required-field change | `checkpoint21-runtime/contract-delta.json`; normal golden checks passed in adjacent91 |

The focused browser suites cover project-header geometry/print/keyboard/off restoration; Board reviewer ages/leads/loaded totals/201 and 501 cohorts/drag transition/manual ordering; Team ID groups/views/columns/roles/scoped and uncertain picker values; and chart/table values, deficits, periods/FTE/unknowns/truncation/copy/save/export/Task return/phone/off behavior. These suites use controlled API fixtures and are separate from the real joins below.

The seven adjacent suites cover task grouping, workspace columns, report units, capacity report task inputs, person facts, Staff/Time count cards and project Dashboard completeness. The unchanged 405px limits remain. Project Tasks measure the actual child at 364.5px ungrouped and 401.5px grouped; the original report matrix measures 379.5px in the real chart join.

## Real normal-command/API/browser joins

- **Board:** `checkpoint21-real-board-final.json` passes four project/workspace, discipline/project, desktop/phone views. Five actual cards total12h, with one canonical overdue task, actual Priya/Marc IDs and two current review cards showing Marc and zero days. The earlier helper captured Ready entry and proved In Review retained its timestamp. Six-day waiting is proven by controlled API tests, not browser time travel.
- **Capacity:** `checkpoint21-real-capacity.json` passes four actual desktop/phone Hours and FTE/month views. Civil capacity40h, Confirmed45h + Expected5h = demand50h; Possible20h is excluded; shortfall10h. The single displayed ISO week's standard denominator40 gives FTE1/1.25/.25. Rendered bars/table cells join the actual matching response. No person ID appears in the public comparison. Root inspected desktop and scrolled phone-chart screenshots.
- **Team:** `checkpoint21-real-team-positive.json` records normal office/home corrections, unchanged four project-member IDs/two open roles, and workspace desktop/phone Alex Civil + Jill Not set filtered by the QA Halifax office. A normal approved8h day and Published Expected20h give Diane effective32h and remaining12h, provisional false; Omar is absent from the forced scoped grid. `checkpoint21-real-team-final.json` has two final project desktop/phone rows and zero failures: exact group/filter/Fill links, Diane12h free/8h away, Omar without a figure, and option/capacity rectangles within the viewport. No membership or allocation fill was saved. Root inspected final Board and Team screenshots.

All fixture data is synthetic and confined to `hub_qa_checkpoint21`. The original QA API had both jobs and evaluation worker disabled; same-day project freshness did not process later task outbox events, so initial task states were null. That partial evidence remains. Root restarted only the disposable API with the **normal evaluation worker enabled and scheduled jobs/email dispatch still disabled**. All five canonical states and one overdue task then materialised, followed by the final joined browser pass. No SQL manufactured state or timestamps.

## Repairs and preserved failures

Initial focused29 passed. Adjacent91 initially passed89; two older whole-envelope expectations were updated to explicitly validate new Board/organisation facts before preserving full legacy-envelope and off-byte comparisons. A local test identifier collision was corrected; its failed build log remains. The intentional golden-refresh run failed by design before review and normal passing checks.

Initial browser failures are preserved. Product repairs addressed grouped-child spacing, long metadata date shrink, health-override phone composition, printable source provenance, an optional Copy callback access, and the chart's separate accessible label/control association. Fixture repairs retained exact assertions while correcting the real Details label, excluding Shell list items from empty-board counts, waiting for URL-driven rows/columns, and closing each nested popover before testing natural focus return.

The Board diagnostic proved a keyboard drag was still active when the fixture began its mouse gesture. The installed sensor defers its key listener by one event-loop turn. The final fixture waits for actual activation/cancellation and that turn; actual POST/PUT bodies and order remain asserted. Native Team selection uses real type-ahead with exact before/after values, supported by checkpoint20's isolated headless-Chrome evidence. No programmatic selection or focus supplies a passing result.

## Remaining boundaries

Physical server-to-browser SSE transport, native OS popup/arrow/device behavior, 200% native zoom and human acceptance remain unproven. Cache14 and actual application live-event paths are narrower evidence. Scheduled jobs/email and hosted operation were intentionally outside this QA runtime.

052's universal band/overlap, CountCard admission, route/colour/contrast/density censuses and remaining navigation/drawer criteria remain unchecked. The utility-navigation none-versus-My Work question is still pending; the existing policy remains. The previous broad census remains checkpoint17 with its recorded exclusions. Formal decisions remain Open, and pilot cleanup is a later authorised gate.

The other 21 unchecked053 slices remain required work. Documentation-only S10 seen-state and S43 person-panel proposals are preparation for the next batch, not implementation. Runtime cleanup and final spec/hash checks are recorded in `parallel-build/checkpoint21-state.json` and the packet verification ledger.
