# Verification: Screen Completeness

**Specification drafting complete; dark implementation in progress. Whole-packet acceptance remains pending.**

## Local specification checks · 2026-10-31

| Check | Result |
|---|---|
| Rebuild combined Markdown/HTML with `python3 tools/build_spec.py` | PASS |
| `python3 tools/build_spec.py --check` after rebuild | PASS: up to date |
| `python3 tools/trace_spec.py --check` | PASS: 1,545 IDs; 438 sections; zero uncited IDs/sections and zero unknown IDs |
| New FR/AC definitions across §§56–57 | PASS: 203 unique definitions, no duplicate new IDs |
| Packet length | PASS: each new `spec.md` under approximately 1,500 words |
| Research / prototype disposition | PASS: build-gaps map preserves existing ownership; five accepted patterns explicit; old §41 R8/E2 unchanged |
| Decision parity | PASS: 052-D1–D10 and 053-D1–D22 mirrored in the log, all Open |
| Competitor-name check in authored files and added generated lines | PASS: none; unchanged scheduling uses of the word “float” are ordinary domain terminology |
| Independent text review | PASS after fixes: partial availability, monthly FTE, nonblocked source use, verifier independence, next actors and filtered trends |

The new numeric examples were checked against their stated formulas: Home 529/(648−36)
rounds to 86 %, free 83 h; standard FTE 160/160 + 120/160 = 1.75; two-week 80/80 = 1.0.
This validates the written arithmetic, not a production calculation.

## Runtime acceptance

At the drafting checkpoint, all AC-SCR-01 through AC-SCR-84 were **NOT RUN**. Later local implementation evidence is recorded below. Each slice in [tasks.md](tasks.md)
names its own FR/AC pairs and must add the corresponding automated or explicitly human-only
checks when implementation is authorized. Existing packet acceptance is not reclassified
by the build-gap map.

At the specification-drafting checkpoint, not run: application build, migrations, API tests, browser/visual or accessibility checks,
notifications, hosted verification, deployment, human acceptance and pilot activation.
The HTML prototype is presentation evidence only; captured missing values remain unknown.

## Build goal started · 2026-10-31

The user authorized dark implementation after the specification. Foundation S01 is now
implemented locally in `codex/build-052-053`, with the combined flag/API and actual-browser
checks recorded in [build-progress.md](../052-visual-system-and-navigation/build-progress.md).
All remaining slices and packet-wide acceptance remain pending. Existing Open decisions
were not confirmed and no pilot flag, commit, push or deployment occurred.

## Second local build checkpoint · 2026-10-31

S01 remains locally verified. S02 now has a tested pure evaluator covering canonical
states, terminal retention, missing/inactive/multiple actors, exact input actors, stable
blocker priority, cycles, review gates and hold/resume state. It has **not** been wired into
API reads, item panels or Weekly Coordination, so AC-SCR-01 and S02 remain unaccepted.
The evaluator tests are part of the combined 40-test passing run documented in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md). No other 053
screen slice is marked complete by this checkpoint.


## Third local build checkpoint · 2026-10-31

S06 is implemented and focused checks pass for FR/AC-SCR-08, 09 and 12. Task-only S02
integration now supplies the context through its existing authorized reader and panel,
including safe input navigation and scroll return. S02 remains unchecked until all named
record readers/panels and Weekly Coordination are integrated. Other screen slices are not
accepted by these tests. Backend, final Chrome, generated-contract and review evidence,
including the corrected malformed-JSON test fixture, is recorded in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#third-batch--home-navigation-and-task-context-integration).
All new flags remain disabled by default. No live review instance was modified.


## Fourth local build checkpoint · 2026-10-31

S02 now integrates Task, Deliverable, Milestone and Decision detail context. The three new
reader/panel pairs passed focused API and actual-Chrome checks; shared Task/rule regressions,
contract generation/freshness, frontend build/lint and fixture type checks passed. Canonical
review-package and milestone prerequisites remain enforced by existing commands. The
remaining eight panel kinds, relationship completeness and Weekly Coordination keep S02
unchecked. Exact evidence, corrected test setups and the package-gate contention/performance
limitation are recorded in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#fourth-batch--deliverable-milestone-and-decision-context).


## Fifth local build checkpoint · 2026-10-31

Risk, Issue and Action join the four existing context integrations. Focused backend
checks passed **47/47**, and actual Chrome verified the three panels and the distinct
linked Task content with synthetic fixtures. Generated contracts/types, frontend build,
lint and typechecks passed. Exact evidence and the two repaired review findings are in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#fifth-batch--risk-issue-and-action-context).
S02 and whole-packet acceptance remain pending; all flags retain disabled defaults.


## Sixth local build checkpoint · 2026-10-31

Handoff and WorkConstraint are now integrated, bringing the named item-kind count to
nine of twelve. The final focused API/contract run passed **11/11**; all 15 TaskContext
API regression cases passed in the preceding run. Frontend build, lint and fixture
checks passed. Actual Chrome verified the two new dialogs, exact constraint identity,
flag gating and terminal facts; Task context regression verified linked destinations
and nonzero scroll return. Two independent-review findings were fixed and closed.
Detailed evidence, corrected fixtures and limitations are in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#sixth-batch--handoff-and-workconstraint-context).
S02, whole-packet and hosted/human acceptance remain pending. All new flags stay off
by default; no commit, push, deployment or activation occurred.


## Seventh local build checkpoint · 2026-10-31

ChangeNotice context and exact ChangeAssessment linking bring the named item-kind count
to ten of twelve. Canonical assessment/correction/verification/retention rules passed
42 pure-rule cases and 18 ChangeContext API cases; 15 TaskContext API regressions and
final contract freshness passed. Final frontend build/types/lint and actual Chrome
Change/Task/Action checks passed, including the full nested correction return chain and
160px source-list scroll restoration. Independent-review findings were repaired and closed.
Detailed results, deliberate contract-update failures, corrected browser fixtures and
remaining limits are recorded in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#seventh-batch--change-notices-and-exact-assessments).
S02, the other unchecked slices and hosted/human acceptance remain pending. New flags
remain disabled by default; no commit, push, deployment or activation occurred.


## Eighth local build checkpoint · 2026-10-31

ReviewFinding context and exact current/historical routes bring the named item-kind count
to eleven of twelve. The final focused backend run passed **76/76**; the existing exact
flag-off Reviews contract plus fresh OpenAPI check passed **2/2**. Frontend build, app /
fixture types and lint passed. Actual Chrome verified the comment actor/state matrix,
exact historical history, withdrawal acknowledgement, missing-child recovery, 160px scroll
return and round isolation between packages. The existing Change/assessment Chrome
regression also passed. Three independent-review findings were repaired and closed.
Details and limitations are in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#eighth-batch--review-comments-and-historical-round-links).
S02, other unchecked slices and hosted/human acceptance remain pending. Flags retain
false defaults; no commit, push, deployment or activation occurred.


## Ninth local build checkpoint · 2026-10-31

SubmissionPackage and exact current SubmissionCheck context bring named detail coverage
to twelve of twelve. Package readiness stays authoritative, checking ownership remains
recorded, check dates remain unknown, and evaluation coverage prevents skipped checks
from appearing Pass. Focused backend tests passed **71/71**; final optional-field API
regressions passed **25/25**, followed by **1/1** fresh schema verification. Final frontend
build, forced app/fixture types and lint passed. Actual Chrome Submission/Change/Review
checks passed, including obsolete/missing children, nested 160px background scroll return
and independent 80px dialog scroll return. A real unavailable-source count assertion also
passed **1/1**. Independent review's coverage and dialog-scroll findings were repaired
and closed. Details and
limits are in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#ninth-batch--submission-packages-and-exact-checks).
S02 and S05 remain unchecked: Weekly Coordination, full relationships and exact revision /
input-use / basis-child delivery links are still required. Flags remain false by default;
no commit, push, deployment, activation or hosted/human acceptance occurred.


## Tenth local build checkpoint · 2026-10-31

Project discipline-coordination and workspace cards now carry optional record context
from visible, permission-scoped rows. Paging/counts and flag-off byte shapes are retained;
submission readiness is reused and source/target scope is independently validated.
Verification passed **37 existing behavior/shape/rule cases**, then **8 new API cases plus
1 fresh contract check**, frontend build/forced app and fixture types/lint, and actual
Chrome weekly/submission regressions. Both weekly roots restore nonzero source scroll.
Independent source findings were repaired and closed. Evidence and limitations are in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#tenth-batch--discipline-coordination-projection-and-workspace-cards).
S02 remains incomplete for the original Weekly Coordination agenda and AttentionPanel,
as well as full explicit relationships. Flags remain disabled by default; no commit,
push, deployment, activation or hosted/human acceptance occurred.


## Eleventh local build checkpoint · 2026-10-31

Original Weekly Coordination agenda rows now consume an optional distinct kind/ID context
index, including terminal/completed, upcoming and held records. An enabled-only read
snapshot refreshes access after evaluation; original rows/counts/window math remain.
**7 focused API/contract/regression checks plus 1 exact old-response comparison passed**.
Frontend build/forced app and fixture types/lint and actual Chrome agenda + discipline /
workspace regressions passed. Independent source review found no remaining P1/P2 in this
slice. Details:
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#eleventh-batch--original-weekly-coordination-agenda).
AttentionPanel and full relationships still leave S02 incomplete. New flags remain off;
no commit, push, deployment, activation or hosted/human acceptance occurred.


## Twelfth local build checkpoint · 2026-10-31

Project attention cards now resolve current record facts after existing ranking/filtering
and preserve unavailable alerts with explicit unknown facts. Alert metadata is distinct;
My Work's shared source helper/default presentation is unchanged. **32 focused backend
cases plus 1 fresh contract passed**, along with final frontend build/forced app and
fixture types/lint, actual Chrome attention/agenda checks and independent source review.
Details:
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#twelfth-batch--project-attention-cards-and-unavailable-facts).
S02 remains unchecked pending concrete legacy related-link navigation/order/dedup closure;
S03/S05 and other unchecked slices remain in scope. Flags stay off; no commit, push,
deployment, activation or hosted/human acceptance occurred.


## Real recipient copied-link acceptance · 2026-10-31

A standalone, no-build API process using checkpoint-twelve binaries ran on localhost
8863 against a newly created disposable `hub_browser_recipient_053` database in the
existing local test container. Only development seed identities and newly created
synthetic records were used. Jobs and the evaluation background worker were disabled.
The new `web/tests/record-context-recipient-real.cjs` made real API requests with no
route mocks, created a restricted project and assigned task, and opened the exact copied
Task URL in separate PM, Alex recipient and denied Rita Chrome contexts. **PASS:** the
recipient's own session resolved the stored owner as You, displayed all four context
facts and the actual needed-by date; the disabled context was absent after reload; the
denied recipient received a 404 and no task title or record facts. No uncaught browser
page errors occurred. Evidence: `/private/tmp/tuesday-recipient-browser.log`.

This verifies one real Task copied-link path against checkpoint twelve, not every
record kind or the concurrently changing parallel batches. Source link order/dedup and
legacy navigation closure remain required before S02 can be checked. The temporary API
was stopped and its QA database dropped after the check. Review/operational databases
and their flags were untouched. No hosted/human acceptance, commit, push or deployment.


## Thirteenth local build checkpoint · 2026-10-31

The first four-chat integration closes S02's audited legacy origin/order/dedup/privacy
gaps and completes local S26 Find room and S28 discipline/scenario implementation. S07's
charts/runway are verified but its capacity-widget acceptance remains incomplete.
**22 new API cases + 85 adjacent/fresh-contract cases passed**, along with API generation,
production build, forced app/fixture types, lint, four new and three existing Chrome
scripts, final real recipient copied-Task acceptance, independent source review/re-review
and spec/trace freshness. Detailed behavior, repaired findings and evidence are in
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#thirteenth-batch--first-four-chat-integration-checkpoint).

Checkboxes denote locally implemented and verified slices; no hosted/human or full-packet
acceptance is claimed. All new defaults stay disabled. No commit, push, deployment, pilot
activation or open-decision closure occurred. The temporary real-API QA database was dropped.


## Fourteenth local build checkpoint · 2026-10-31

The second four-chat integration locally completes S07 Home layout/charts, S17/S18 project
staffing/filter/group/peek and S27 planner headers/deadlines/month views. **29 new cases +
2 fresh contract/registry checks + 109 adjacent cases pass**, alongside final production
build, forced app/fixture types, lint, SSR UI review, four new and six existing Chrome
scripts and independent source re-review. A real 68-observation census has zero serious/
critical axe findings; My Work's first task row and legacy chrome words remain explicit
failures for S04. S03 stays incomplete for normal-command readiness-source evidence; S09
capacity is verified but deadline notes remain. Detailed behavior, repairs and evidence:
[build-progress.md](../052-visual-system-and-navigation/build-progress.md#fourteenth-batch--second-four-chat-integration-checkpoint).

All flags remain disabled by default. No commit, push, deployment, pilot activation,
open-decision closure or hosted/human/full-packet acceptance. The temporary census API
and QA database were stopped/dropped after final measurement.


## Fifteenth local build checkpoint · 2026-10-31

The third four-chat integration locally completes S03/S04/S08/S09/S19/S20/S21/S30.
**69 new API/domain cases + 2 fresh contract/registry checks + 135 adjacent cases PASS**,
with final production build, forced app/fixture types, lint, SSR UI review, eight new and
seven affected existing Chrome suites, and independent source re-review. The normal-command
handoff/readiness gap is closed under Jay's explicit single-target choice; materialized
blockers and separately labelled readiness relationships remain distinct.

The all-parts real census has 68 observations, no serious/page/font issues, and all 34
enabled observations within 405px. Two legacy-off My Work rows remain explicit failures
at 558 px; full 052 S09 remains open. The 68-observation reflow/text-spacing audit passes,
with its 800px/DPR2 equivalence limitation. Portfolio's 72-project, 10-page synthetic
landscape PDF has full grouped rows/notes/repeated headers and portrait isolation after
leaving the route; root inspected the rendered pages. Detailed behavior, evidence and
limits are in [build-progress.md](../052-visual-system-and-navigation/build-progress.md#fifteenth-batch--home-trends-delivery-inputs-and-portfolio-review).

All flags remain disabled by default. No commit, push, deployment, pilot activation,
open-decision closure or hosted/human/full-packet acceptance. The temporary QA API and
only its synthetic database were stopped/dropped. Other unchecked slices remain in the
active goal; proposed next batches and exact ownership are saved in parallel-build.

## Sixteenth checkpoint — delivery, staffing and visual semantics · 2026-11-01

Locally verified dark implementation: **053 S22 is checked**. S05-A Submissions is verified but **S05 remains unchecked** until the separate full Readiness increment. Shared052 semantics/Work consumers and Dashboard CountCards are partial work toward052 S03/S06/S09; those full slices remain unchecked. No open design/activation decision was silently closed.

- **Backend PASS 103**:22 new API cases (delivery 6, Dashboard 8, exact-date reservations 6, canonical Timeline lane 2), two fresh OpenAPI/registry checks and79 adjacent workflow/scope/flag cases. Logs: `/private/tmp/tuesday-checkpoint16-api-final.log` (24/24), `...-api-adjacent.log` (79/79). Contract update was separately rerun fresh without update variables. No schema/migration added in this batch.
- **UI PASS**: production build, forced app types, fixture types, lint and SSR review. Build/analyzer warnings remain; no warning-free claim. Final product build log: `/private/tmp/tuesday-checkpoint16-ui-build-verified-2.log`; final forced/types/lint/SSR logs use `...-ui-*-verified*`.
- **Chrome PASS 14 suites**: new delivery, Dashboard completeness and Work visual consumers; updated semantic/identity component harnesses; visual system/layout/tabs/title block, Home trends/presets, attention context, weekly coordination context, Dashboard reflow, Today/reviewer regressions. Source-import layout/title/semantic/identity checks used the separate Vite development harness 8854; real app/mock fixtures used production preview 8853. Mocked browser actions are distinguished from actual API tests.
- **Real-API census**:68 on/off observations across17 screens × 2 roles, all 34 enabled first-data positions≤405 px (max 402), zero serious/critical axe/page errors/external-font requests. Overall runner exits1 for the two preserved legacy off MyWork positions558 px. These are recorded, not waived.
- **Real-API reflow**:68/68 desktop/375px/800CSSpx-DPR2 reflow-equivalent/text-spacing observations pass, with zero document overflow, sub-11 px text, clipped controls, serious violations, page errors, external fonts or restoration failures. DPR2 equivalence is not native browser 200% zoom/human acceptance.
- The full census/reflow preceded a final Dashboard-only spacing repair. Final targeted supplements pass 4/4 on/off/role census observations (enabled first data 267 px) and4/4 reflow variants. They explicitly record the selected route. Other application sources did not change after the full pass. Evidence is in052/evidence/checkpoint16-*.json.

Independent backend review found no P1/P2. UI review caught Supersede overriding a permission-disabled offer and actual SubmissionPackage targets using an unregistered panel. Both were repaired and tested through denied/allowed/partial controls and exact package navigation/return independent of screen_patterns. Runtime testing also caught print duplication while a screen legend remained open; visual-only print hiding and a print-while-open assertion fixed it. Root screenshot inspection caught Dashboard cards overlapping its title/reference/controls: removed that project page's inherited overlap prop, removed its now-unused flag local, independently reviewed the normal-flow fix and added1440/375 px geometry assertions. Root inspected final Dashboard and Board screenshots, saved in052/evidence. No automatic pass was treated as sufficient visual acceptance.

Fixture-only repairs preserved production guards: actual HealthOverride snapshot fields; explicitly labelled corrupt imported-reference setup outside normal immutable-evidence commands; null-safe Unattributed name; one-time identity-colour backfill before read-only/off baselines. Browser probe/selector/URL-render waits now retain exact values, counts and restoration expectations across modern CSS colour serialization and React commits.

All QA data was synthetic and disposable. API 5088 and dev harness 8854 were stopped after verification; hub_qa_checkpoint16 dropped and no hub_test_* database remains. Existing review DB 55433, preview 8853, prototypes and primary checkout preserved. No commit, push, deployment, pilot activation, hosted/human acceptance or whole-goal completion.


## Seventeenth checkpoint — Readiness, allocation shifts and report views · 2026-11-01

Locally verified dark implementation: **053 S05 and S31 are checked** after their full functional increments and independent source/runtime review. S05 combines checkpoint16 Submissions and checkpoint17 Readiness. S23-A catalogue/saved parameters is verified; S23 stays unchecked pending exact task-input counts/drill-downs. 052 S02/S03/S06/S07/S08/S09 remain incomplete. No decision or activation gate was closed.

- **Backend PASS227 distinct API checks**:28 focused and209 affected, with10 shared checks. The focused set includes6 Readiness,8 shift,8 report catalogue/views,4 workspace identity and2 fresh OpenAPI/registry goldens. Node QueryClient/cache suite9PASS. No migration/schema change in this checkpoint.
- **UI PASS**:production build, forced application types, fixture types, lint and SSR review. Build/analyzer warnings remain. The final browser-script-only fixes passed syntax/fixture checks without changing the built product.
- **Chrome PASS15 suites**:8 new/navigation checks and7 affected workflows, including final planning-reporting (16 mocked posts; import true; no errors/unknown requests/axe findings). These mocked interaction checks are distinct from the real API cases below.
- **Real API census**:68 observations over the standard17 routes ×2roles ×on/off. All34 enabled observations≤405px (max402); zero serious/critical axe/page/font errors. Runner still exits1 for two preserved legacy-off MyWork558px observations. That fixed-route result does not prove every report/project subroute is compact.
- **Real reflow**:68/68 observations PASS at desktop,375px,800CSSpx/DPR2 equivalent and text-spacing variant; zero overflow/sub11px text/clipped controls/serious/page/font/restore failures. Reflow equivalent is not native browser200% zoom or human acceptance.
- **Three real UI-to-API journeys PASS**, using a separate synthetic project:3 allocations shift+2weeks with task dates/person/hours/links/provenance preserved and Confirmed→Proposed;3 unassessed Readiness rows with no assessment write and expected absent-read404;Priya/Sam report rows and a personal view containing only projectId/week, invisible to Sam. Two disposable identical personal views resulted from selector retries and are disclosed. Raw evidence and9 PNGs:053/evidence/checkpoint17-real-journeys*.

Independent source review closed material issues in actual Team cache invalidation, report Views bootstrap/capture safety, Vacation capability breadcrumb and an earlier-goal legacy-off alias leak. The cancellation fixture now supplies its required ordinary reason. Browser-only repairs retain exact values and postconditions while awaiting controlled input/response settlement and decoding panel URLs.

**Visual follow-up remains required.** Root inspected the main joined screens, shift preview/after and inspector/menu captures; an independent reviewer agreed functional evidence does not close universal layout acceptance. Allocations/reports visibly put actual data below405px in these joined routes. The helper's Readiness firstWorkingRowY1611 points to a later element and is rejected as first-card evidence; its first work card is visibly much earlier but still low. These are outside what the fixed-route census establishes. Do not infer exact replacement coordinates from screenshots. Add exact card/row selectors and these joined routes to subsequent layout verification. Inspector/menu PNGs caught a transition frame, so their visual styling is UNPROVEN; settled mocked browser checks/real action+URL postconditions establish interactions, not those screenshots' final opacity. No threshold/allowlist was relaxed and052S09 stays unchecked.

Evidence originally written to an outer review folder was moved intact (10files) to the nested implementation worktree; the outer tracked tree remains untouched. Spec freshness/trace PASS1546IDs/438sections, zero uncited/unknown. QA API5088 and dev8854 stopped after all runtime actors finished; only hub_qa_checkpoint17 dropped; no hub_test_* remains. Test PostgreSQL, preview8853, prototypes and review DB55433 preserved.

Logs: /private/tmp/tuesday-checkpoint17-api-final.log, api-adjacent.log, cache-keys.log, ui-*.log and browser/regression-*.log. No commit,push,deploy,pilot activation,hosted/human acceptance or whole-goal completion.


## Eighteenth checkpoint — activity, report inputs and own-Time week · 2026-11-01

Locally verified dark implementation: **053 S32 is checked** after full FR/AC54–55 source, API, browser and real UI-to-API evidence. S10-A activity/Comments is verified, but S10 remains incomplete for seen/New and the separately scoped generic follow-up streams. S23-B verifies exact input cohorts for six task-row reports; S23 remains incomplete for the remaining Workload/Capacity/Time forms. Planner presentation is further work toward052 S03/S09, not full visual acceptance. Formal decisions/default activation remain unchanged.

- **Backend139 distinct PASS**: focused41 (31 new cases,2 fresh OpenAPI/registry goldens and8 existing catalogue/view checks) plus98 affected checks, zero overlap. Node QueryClient/cache11PASS. No schema/migration change. Logs: `/private/tmp/tuesday-checkpoint18-api-final.log`, `...-api-adjacent.log`, `...-cache-keys.log`.
- **Frontend PASS**: final production build5, forced app types before the final Time layout-only repair (build5 also typechecks it), fixture types, lint after the final layout, and SSR review. Analyzer/bundle/lint warnings remain. Logs use `/private/tmp/tuesday-checkpoint18-ui-*`.
- **Eight Chrome suites PASS**: planner semantic source harness; Work activity/Comments; own-Time (rerun after final layout); six report task inputs; existing workspace; full planner workflows; planner headers plus actual entry-list on/off supplement; report catalogue/views. These real rendered UI checks use explicit mocked APIs, separately from actual backend and joined checks. Source-import semantic harness uses dev8854; built app checks use preview8853.
- **Real Work journey PASS**: three normal seeded comments plus Priya's fourth through the actual UI, actor/count refresh in the originating project row, exact Task Comments and original filtered URL return. Real source scroll was0; nonzero scroll/focus return is established only by the mocked case.
- **Real own-Time journey PASS**: normal synthetic40h schedule and8h Wednesday ManualHR reference give32h capacity. Three assigned tasks plus preceding-week actual union deduplicate to4 rows. Blank load writes nothing; one Enter on a fully-away Wednesday records exactly one2h entry, owned by Alex, and real totals show2h/32h. This is synthetic local HR-reference fixture data, not an external-provider interaction.
- **Real report journeys PASS**: three normal tasks have null estimates. The nonfirst main-table Task returns to the exact report URL; the separate3-unestimated metric opens a3-row list, its nonfirst Task returns to the exact metric URL, and closing returns to the base report. Actual main first rowY379.5px. Real metric-return focus was not measured; the exact nonfirst-row/count focus case passes in the mocked suite. The raw followup metric firstRowBox repeats the main row coordinate and is not accepted as dialog geometry evidence.
- **Final Time visual check PASS** after root screenshot review exposed the old totals sidebar squeezing Saturday/Sunday off desktop. Own-week grid is now full width, week/filter controls share one row, and totals follow it. Independent source re-review found no P1/P2. Final real1440px region1134/1134px, first taskY399.5px;375px document375px with a1065px table inside named keyboard-scroll region341px. Same32/8/2 facts and4rows, zero errors/mutations in this final read-only check. Root inspected both settled PNGs.

Other material source/runtime findings were repaired and independently re-reviewed: actual CommentPanel project-cache refresh; Workspace page selection incorrectly resetting to page1; Planner EntryList hook scope; Time known-save/refetch double-create race; report malformed scalar GUID/overflow paging; exact main-report origin navigation; optional calendar non-working truth independent of zero hours/leave. No guard was weakened. Append-only activity fixtures clone a labelled legacy imported Task rather than deleting history. Report raw scalar parsing stays consistent with each real report parser.

Six nonempty report mock geometries are379.5px when both relevant flags are on. The preserved visual-off expanded controls remain653.5px, without a waiver. This checkpoint did not rerun the whole68-observation fixed-route census/reflow; checkpoint17 remains the latest broad scan. That route set excludes own-Time and open-report/joined project subroutes.052 S02/S03/S06/S07/S08/S09 and earlier joined Readiness/Allocations density/transition-capture limits remain open. Phone containment is not a native200%zoom/human acceptance claim.

Evidence:053/evidence/checkpoint18-real-journeys* (raw initial PARTIAL retained), checkpoint18-time-layout-final* (final settled view),052/evidence/checkpoint18-report-geometry.json. All are in the nested implementation worktree. The initial40h/0-unestimated/wrong report-return findings are superseded only by the labelled setup/repair/followup evidence, never overwritten.

Spec/trace PASS1546IDs/438sections, zero uncited/unknown. After all runtime actors finished, verified root API5088/dev8854 were stopped and only hub_qa_checkpoint18 dropped; no hub_test_* remains. Test PostgreSQL, preview8853, prototypes, reviewDB55433 and primary checkout preserved. No commit, push, deployment, pilot activation, hosted/human acceptance or whole-goal completion.053 now has21 locally checked slices; all other unchecked slices remain in the active goal.


## Nineteenth checkpoint — grouping, complete report inputs and optional person facts · 2026-11-01

**053 S23 is locally checked.** Its catalogue/views and all twelve applicable report input forms now have source, actual-database and rendered-browser evidence. S11 remains open for selectable columns/Submission date. S29 remains open for custom/native picker notice bridges; directory-backed notices and the facts editor are verified. The full goal and universal052 acceptance remain incomplete. Formal decisions and default-off flags are unchanged.

- **Backend92 distinct PASS:**42 focused (13 grouping,18 remaining report inputs,8 person facts,3 shared foundation) plus50 adjacent including fresh OpenAPI/registry goldens. Node cache12PASS. The initial privacy failure was a fixture inadvertently creating an Open project; a normal restricted-project correction repaired it without changing guards. Final single-process build passed with0errors; the earlier stalled sandbox compiler is not a pass. All542 frozen C# files remain unchanged after frontend verification.
- **Schema/contracts:** additive ScreenPersonFacts migration adds3nullable user facts and2tables, with no defaults/backfill. Independent migration review and real database constraint tests passed. Contract delta:8new paths,10new schemas, two task-list responses now ListPage, optional scoped person-facts/notices; no endpoint removal. Generated app/fixture types pass. New audit types and setting vocabulary remain flag-owned.
- **Frontend PASS:** final production build3, application types, fixture types, lint and SSR review. Existing analyzer/lint/bundle warnings remain. Independent reviews closed SQL bounded grouping, report pager validation, person-editor stale-version promotion/malformed receipts (valid child version0), unavailable Add-here and final compact report spacing. Shared FilterBar gained an optional leading control after type checking caught the missing integration.
- **Twelve Chrome suites PASS:** grouping; person facts; drawer; route matrix; layout/contrast/long names; navigation; tabs; workspace; Team; first-six task reports; remaining-six report forms; catalogue/views. The route matrix covers449 shell/group observations and9 supplied capability profiles using sentinel leaves; it does not prove actual destination admission. Person-facts checks preserve exact key/body on uncertain retries, no duplicate records, actual disabled inputs, stale-save refusal and flag independence.
- **Report density PASS:** after retaining full filters/formula/coverage and reducing compact spacing, first-six mock rows are363.5px; remaining forms are363.5/379.5/391.5/379.5/379.5/401.5px. The405 bound is unchanged. Font-ready, unscrolled stable measurement repaired false transition captures. Earlier451.5/409.5 failures are retained. Visual-off expanded controls remain653.5px, without a waiver.
- **Actual API/UI joins PASS:** four same-discipline tasks total40h/50% across two2-row server pages; normal grouped Task return preserves its exact source URL. Omar created a certification through Preferences with a real201/version0; Jill's Admin UI correction to Co-op, starts2026-12-01/ends2026-12-18 preserves activation/capacity. All-staff and the standard project-Team picker show the date notice; picker contains no certification chip. Nonfirst report metric Task returns to the exact metric query and report. Exact real metric-return focus remains unmeasured; mocked focus/scroll checks pass.
- **Final joined layout:** actual Employee/Discipline/TeamCapacity first rows379.5px; TaskHours as Alex has an actual2h Task row at363.5px. The earlier Priya Total0 capture is not counted as nonempty TaskHours evidence. All four1440/375 documents contain their width; overflow stays in named inner table regions. Root inspected final desktop TeamCapacity, actual TaskHours and phone Employee screenshots. Final read-only actor saw0mutations/console errors.

Evidence:053/evidence/checkpoint19-api-results.json, checkpoint19-real-* (initial partial records retained), checkpoint19-layout-final-build3*;052/evidence/checkpoint19-report-geometry.json and checkpoint19-header-*. Logs use/private/tmp/tuesday-checkpoint19-*.

Limits: S29 FR50 custom/native chooser coverage, S11-B columns, S10 seen/generic streams, the utility navigation policy awaiting Jay, other unchecked slices and052 broad colour/contrast/native200%zoom/human acceptance remain open. No full census rerun; checkpoint17 is still the latest broad scan with its documented exclusions. Global DL07's zero-estimate fallback discrepancy is recorded as an existing build gap; grouping follows its exact new specified formula without changing canonical evaluation.

After every actor finished, verified root API5088(PID48593)/dev8854(PID46930) were stopped and only hub_qa_checkpoint19 dropped; no hub_test_* remains. Test PostgreSQL55432, preview8853, reviewDB55433, prototypes and primary checkout are preserved. No commit, push, deployment, pilot activation, hosted/human acceptance or whole-goal completion.053 now has22 locally checked slices.

## Checkpoint20 — columns, report units and person notices locally checked

S11/S25/S29 are locally checked;053 has25 checked slices. Workspace selectable columns/current submission dates, exact report FTE/months and optional person-fact/native/custom notice coverage passed independent review and scoped verification. Staff/Time CountCards were completed for this batch with a measured Staff-only density repair.133 distinct .NET cases pass, with final30 report/catalogue/golden checks after the final unit-label repair; Node13, types/fixtures/lint/SSR/builds and12 Chrome suites pass. Real normal-command/API/browser joins substantiate dates/views, positive notices/custom verifier/HR stale409, Staff399.5px, Time4h and exact visible November1/.75/1.75 FTE with160h denominator. See [checkpoint20 findings](evidence/checkpoint20-findings.md) and its durable raw artifacts.

All new behavior stays default-off; no commit/push/deploy/activation. Physical SSE transport and native OS popup/device appearance are unmeasured; broader052 visual/CountCard acceptance, project-task density and all unchecked slices remain active. QA20 API/dev/database were cleaned up after actors finished; production preview8853 and test PostgreSQL55432 retained.


## Checkpoint21 — Board, capacity chart and Team locally checked

**S13/S24/S42 are locally checked;053 now has28 checked slices.** All source and final acceptance reviews closed. New behavior remains default-off in maintained settings; no formal decision was closed, commit/push/deployment performed or pilot activated. See [checkpoint21 findings](evidence/checkpoint21-findings.md) for self-contained evidence and preserved failures.

- Backend:120 distinct latest .NET cases pass (29 focused,91 adjacent); Node14 passes. Six new schemas/four optional properties, no route or required-field change, no migration. Final556 C# and403 web hashes match the recorded manifests. Types, fixtures, lint, SSR and production build2 pass with existing warnings.
- Browser: four focused and seven adjacent suites pass. Actual project header64px and first child364.5px ungrouped/401.5px grouped; source explanation remains complete on screen and in print. Full override/keyboard/focus/phone/off checks pass. Original report matrix379.5px remains within405.
- Real Board: five cards/12h/one canonical overdue, current Priya PM/Marc DL, actual reviewers and zero-day Ready/In Review ages; Ready timestamp survives In Review. Four project/workspace desktop/phone joins pass. Initial disabled-worker null-state evidence is preserved; enabling only the disposable evaluation worker closed that gap while scheduled jobs/email stayed off.
- Real capacity comparison: Civil40h capacity,45 Confirmed+5 Expected=50 demand, Possible20 excluded,10 shortfall; one standard40h displayed week gives FTE1/1.25/.25. Four real API/chart/table desktop/phone joins pass with no person IDs in the public comparison. Canonical historical unit membership/partial/provisional/cap semantics are independently tested.
- Real Team: unchanged4 membership IDs/2 open roles; workspace/project desktop/phone Alex Civil+Jill Not set Halifax-filtered cohorts; Diane's normal approved8h day+Expected20 gives12h free/8h away, while Omar has no scoped figure. Final option/figure rectangles fit the phone. No allocation fill or membership save occurred.

All raw initial failures and reviewed fixes remain in checkpoint21-runtime and real artifacts. Native select popup/arrow/device behavior, physical SSE transport, universal052 visual/card admission and hosted/human/pilot gates remain unproven. The utility-navigation choice remains pending. Runtime actors closed; verified owned API5088/source8854 stopped, onlyhub_qa_checkpoint21 dropped, nohub_test_* remained. TestPG55432/productionpreview8853 retained; primary/prototypes preserved. Final spec checks pass1546IDs/438sections/zero unknown; git diff whitespace check passes. Full build goal remains active for every other unchecked buildable slice; S10/S43 proposals are preparation only.

## Checkpoint22 — Timeline views, deliverable issue metadata and narrow person core

**S14/S33 locally checked;30 of49.** Independent review,126 distinct .NET,Node16,types/fixtures/lint/SSR/build2 and11 latest Chrome suites PASS. The final real joined browser report passes22/22 at1440/375 with four actors,32 screenshots,133 actual served asset comparisons and zero attempted writes. Real Timeline shared/private/source joins, issue purpose/B response/backdated identity, exact four undated records, warning-only C12, Person actual40free/0away and Rita zero hours/grid requests are recorded. Full known-ID Person coverage remains open under S43.

See [checkpoint22 findings](evidence/checkpoint22-findings.md) and linked raw runtime/real reports for final manifests, test-only repairs and preserved failed attempts. Root inspected representative final screens, verified source parity and passed spec/trace1546IDs/438sections/zero unknown. API5088 was gracefully stopped after ownership verification; only disposable hub_qa_checkpoint22 was dropped, no hub_test_* remained; PG55432/preview8853 retained. Four existing chats are restarted on disjoint g51 preparations. Full remaining052/053 scope, universal visual/native/SSE/human/pilot gates and utility policy remain open. No commit/push/deployment/activation/Accepted claim.


### Local checkpoint twenty-three — S10 verified,31/49

Independent source/acceptance review and actual runtime evidence support checking S10.117 distinct .NET,18 cache,types/fixtures,lint(withwarnings),SSR/build2 and four focused Chrome suites pass. Actual QA23 seen-state joins prove displayed-token acknowledgement, unfiltered mark-all, foreign omission and unchanged canonical records. Real Report Person return at1440/375 and client CSV/XLSX menu downloads pass; one expected private PM Project open is recorded separately. Source593C# inputs unchanged;130served files match output. See [checkpoint23 findings](evidence/checkpoint23-findings.md) for raw artifacts, repairs and limits.

S43 remains open for remaining passive User names; S34 export is verified but full atomic import is pending.052 broad visual/native/zoom gates and every other unchecked slice remain in the active goal.052-D11/D12 alone are confirmed; earlier pending utility statements are superseded by those recorded choices. No commit, push, deployment, pilot activation, decision closure or formal Accepted status.


QA23 cleanup PASS: identity-verified API77813 stopped with SIGINT; only newly created hub_qa_checkpoint23 dropped after zero-client check. Existing testPG55432/preview8853 retained. See evidence/checkpoint23-runtime/qa23-cleanup1.json (relative to packet root).


## Twenty-fourth checkpoint — atomic drawing import and remaining person surfaces · 2026-11-02

**S34 and S43 locally checked;33/49.** Independent source and final local acceptance
reviews PASS.137 distinct .NET checks (69parser,16import,6Search/Files,46adjacent),
7 Node guards, app/fixture types, lint with warnings, SSR, solution3/webbuild3 and
Search3/remaining4/import5/Pickup1 Chrome suites PASS.602 compiled C# inputs are
unchanged;129 served final build files match exactly. Prior export evidence remains
valid by source hash and eight passing adjacent export cases.

Actual QA24 CSV/XLSX API imports each create40 records/40 Created entries; previews,
refusals and retries have exact whole-hub-table parity. A valid1.27MB JSON workbook
preview succeeds while ordinary oversized creates remain413. Actual Chrome then
imports one additional corrected40-row XLSX batch and joins exact response IDs to
SQL delta40/40 CreatedImport logs/40source rows/1batch/0issues/sentinel equality.
Actual Marc inspection retains the file/hash/folds/nonzero list scroll/focus; actual
Search Alex and Files Jill return naturally. One expected private PM Project-open
acknowledgement is recorded separately. See [checkpoint24 findings](evidence/checkpoint24-findings.md).

Failed harness/compiler/golden runs are preserved and explained there; no global
cache behavior was altered to satisfy tests. QA24 cleanup was rejected by automatic
approval review because stopping its running API and dropping its database require
explicit confirmation. Nothing stopped/deleted; API4323 andhub_qa_checkpoint24 stay
preserved pending approval. No workaround, pilot activation, commit, push, deployment
or formal Accepted status. All other unchecked052/053 slices remain required.


## Twenty-fifth checkpoint — Search locally checked; review read side verified · 2026-11-02

**S46 locally checked;34/49.** Independent functional/acceptance review PASS after
closing one missing AC-test linkage finding. Search now has supported project,
status and discipline filters, actual job-title/home-discipline matches, and ten
browser/viewer-local recent searches with Clear. Filtered counts, incompatible
URLs, denied-read cache removal and exact Person return remain honest. The
cross-package open-findings register also passes R1 read-side checks; S35 stays
unchecked for response/outcome/export and external provenance.

99 distinct PostgreSQL-backed .NET cases (16Search/24register/59adjacent),25Node,
app/fixture types, lint with existing warnings, SSR, isolated solution2 and SPA1
PASS. Four functional Chrome suites PASS: Search3, register3, adjacent Search/
Person1 and finding-context1. Final desktop/375px images inspected. Chrome uses
synthetic reads against the actual production SPA; no new live Kestrel/browser
join was run. See [checkpoint25 findings](evidence/checkpoint25-findings.md) for
raw evidence, preserved38/40 and browser failures, exact repairs and limits.

606 C# inputs and443 web baseline inputs matched at browser completion except
two reviewed CJS driver repairs;129 served files match exact build bytes. A later
four-file metadata-only change adds AC79/84 citations and a test script: exact
removal reconstructs tested hashes, and the actual trace parser maps7 backend
declarations/16 cases plus the browser suite. No assertion/app/fixture/dependency
changed and no behavioral rerun is implied. Spec/trace1546IDs/438sections/zero
unknown and8 decision checks PASS. Whitespace final check recorded in runtime.

All flags remain off in maintained defaults. Other unchecked053 and broad052
visual/native/zoom gates remain required. No commit/push/deploy/pilot/formal
Accepted transition. Automatic review rejected QA24 stop/drop pending explicit
confirmation; nothing stopped/deleted. Old API4323/DB/preview8853 and sharedPG
are preserved; isolated static8854 has no API5089 and is not an unmocked review
instance. Root next follows the durable R2 response/outcome contract, not a
claim that the remaining Reviews work is complete.


## Twenty-sixth checkpoint — response outcomes and complete sheets locally verified · 2026-11-02

**R2 / FR-AC-SCR-61 local PASS; S35 still unchecked,34/49.** The prior R1
cross-package register now joins explicit response outcomes, the existing required
explanation/evidence, immutable response history and an all-round package CSV/XLSX
sheet. Legacy hashes/replays, canonical named verification, carry/reset truth and
flag-off bodies/DTOs remain intact. External-review provenance (FR/AC-SCR-62)
remains unimplemented. See [checkpoint26 findings](evidence/checkpoint26-findings.md).

149 distinct .NET checks pass:52 new validation/API/receipt/schema/export cases
and97 adjacent Reviews/Changes/permissions/context/register/plain Contract/Registry
checks. Solution2 passes0errors/1156warnings; isolated API bootstrap had7warnings.
The additive migration has exactly2nullablemax32columns and2checks, independently
reviewed and exercised by real-provider checks. Contract delta adds1route and4
optional outcome properties, with no other existing schema/path change; registry
adds one field key. App/fixture types, lint with existing warnings, SSR and final
isolated SPA2 pass. Actual trace parser links the AC61/84 tests and browser script.

Final R2 Chrome3 passes10cases/66explicit simulated commands/2downloads and strict
zero unexpected requests/errors. Long4,000+ character explanations remain whole;
uncertain/malformed replies keep the exact original request/version. Adjacent R1
and finding-context suites pass. Their prior SPA1 evidence is retained through the
exact proved removal of only the new form's invented maxlength; final full R2
suite runs on SPA2. Final read-only desktop/375px captures4 inspected;0writes.
Browser reads/commands are synthetic; actual provider/API/CSV/XLSX bytes are
separate. No live Kestrel/browser join is claimed.

Final613 C#/446webinputs/143servedfiles match;401 earlier output files and oldAPI
DLL are unchanged. Spec freshness/trace1546IDs438sections0unknown,8decisions and
whitespace pass. Compile, source-review, initial browser and screenshot-helper
failures are retained with exact repairs in the findings. No flags activated,
commit/push/deployment/pilot/formal Accepted transition, or full-goal completion.

**For Jay:** external legacy-authorship choice is pending (block unknown historical
authors versus a PM evidence/attestation workflow); no R3 code depends on an assumed
answer. QA24 cleanup is separately still awaiting explicit confirmation after
automatic approval review rejected stopping API4323/dropping onlyhub_qa_checkpoint24.
Nothing stopped/deleted; old runtime and all tested bundles remain preserved.
Other unchecked053 work and broad052 visual/native/zoom/human gates remain open.


## Twenty-seventh checkpoint — Files locally verified · 2026-11-02

**S44 / FR-AC-SCR-77 local PASS;35/49 checked.** Files adds discipline/actual-author/added-week filters, independent missing-direct-link counts and manual revision context. Original authorship, authority, inheritance, If-Match and disabled behavior remain intact. A task parent change requires explicit re-registration; no remote content is fetched or verified.

103 distinct final API tests and27 Node checks pass. API/solution/test-only rebuild have zero errors with existing warnings; bootstrap could not retrieve NuGet audit data. Migration adds exactly six nullable fields, one registration constraint and two Restrict foreign keys/indexes. Independent source/migration/coverage/final acceptance reviews pass. Generated contracts/types, fixture types, lint warnings, UI review and isolated final SPA2 pass. Files Chrome3 passes7 groups/35 reads/3 Person/1 Deliverable/zero writes; reference Chrome2 passes9 cases/23 simulated commands/10 simulated mutations. Adjacent Person and identity suites pass. Four final desktop/375px captures inspected. Browser HTTP is synthetic; actual PostgreSQL-backed API evidence is separate, with no new live Kestrel/browser join.

Final622 C#/451 web inputs/142 static files match, and715 earlier output files including the retained API DLL are unchanged. Spec freshness,1546 IDs/438 sections/zero unknowns,8 decisions and whitespace pass. [Checkpoint27 findings](evidence/checkpoint27-findings.md) preserves all initial failures, exact repairs, source hashes and evidence boundaries. Maintained flags remain off; static8857/8858 have no new API and are not unmocked review instances.

**For Jay:** external legacy-authorship choice and QA24 cleanup confirmation remain pending. Automatic approval review rejected stopping API4323/dropping hub_qa_checkpoint24; that action executed nothing. Existing runtime/data are preserved. Other unchecked053 and broad052/native/zoom/human gates remain open. No commit/push/deploy/pilot/formal Accepted or full-goal completion.


## Twenty-eighth checkpoint — metadata and template provenance · 2026-11-02

**S39/S40/S41/S48 local PASS;39/49 checked. S49 phase1 only, fullS49 OPEN.**170distinct final .NET checks,28Node client-state,5coverage-tool tests, types/fixtures/lint warnings/SSR and final A5/B3/C3/D3 synthetic Chrome suites PASS. Additive3table/8nullable-column migration and contracts reviewed/provider-exercised. Source review found and fixed audit off-flag leakage across22fields/sixnewentrykinds without changing raw audit JSON/hash or old fields. Complete83 gate intentionally refuses. Ten inspected desktop/375px captures and numbered static gallery preserve original prototypes. Final652C#/270web inputs/two143static profiles match;857earlier outputs unchanged.

[Checkpoint28 findings](evidence/checkpoint28-findings.md) records exact failures, repairs, scope and boundaries. Source flags remain off;8859/8860 are synthetic static previews with no real API5094/5095. Remaining10slices, fullS49/deactivation/policy/R3 and broader052/native/zoom/human gates remain open. QA24 rejected cleanup executed nothing; pending user choices retained. No commit/push/deploy/pilot/formalAccepted/full-goal completion.


## Twenty-ninth checkpoint — Calendar and review disciplines · 2026-11-02

**S15/S36 local PASS;41/49 checked. S12-A/S45-A partial only.**126distinct final PostgreSQL-backed API checks,29distinct client-state checks, types/fixtures/lint with warnings, reviewed additive2table/3nullable-column migration/contracts and final Calendar4/Reviews3/Bulk5/Notifications2 finite synthetic Chrome suites PASS. Independent review and browser testing found and fixed current notification-screen invalidation and unintended ErrorBanner Retry form submission; strict regression proofs preserve original versions/uncertainty and natural focus.

Eight inspected combined-design desktop/phone captures and a separate numbered [review copy](../../.review/checkpoint29-review/index.html) preserve the prototypes. Final668C#/259webinputs/143bundlefiles/142publicHTTPfiles match;1143earlier artifacts unchanged. Spec freshness,1546IDs/438sections/zero unknowns,8decision checks and whitespace evidence recorded in [checkpoint29 findings](evidence/checkpoint29-findings.md). Preview8864 is static/synthetic with no realAPI5099; provider evidence is separate from browser HTTP.

FullS12/S45/S47 and remaining8slices, legacy-authorship choice, template-policy/full83/deactivation and broader052/native/pilot/human gates remain open. QA24 rejected cleanup executed nothing; old runtime/data retained. No maintained flag activation, commit, push, deploy, formalAccepted or full-goal completion.


## Checkpoint30 — S16 local verification (generation58)

Task sections, server History actor/paging and own collaborator contribution marks are locally implemented behind work_screens. S16 is checked;42/49 screen slices are now locally checked.85 distinct provider cases and32 client-state cases pass. Application/fixture types, scoped lint with warnings, additive API/registry review and strict no-update checks pass. Independent source/model/UI reviews pass. Final Task-section and contribution suites pass on isolated SPA2; Calendar's passing long-reason regression has unchanged relevant source. Native Chrome needed actual window bounds matching its emulated viewport; the exact strict driver and nine real refresh intervals were preserved.

Final artifacts:675 compiled C# sources,271 SPA source files,143 bundle files/142 served public files match recorded snapshots.2817 earlier output hashes are unchanged. New six-addition review copy: `.review/checkpoint30-review/index.html`, loopback8868; additions05/06 are new and the earlier eight images are preserved. API5101 has no listener. Evidence: `evidence/checkpoint30-findings.md` and `evidence/checkpoint30-runtime/verification-rollup1.json`.

No commit, push, deployment, maintained flag activation, pilot or Accepted gate. Full goal and S12/S35/S37/S38/S45/S47/S49 remain open. Existing unknown-authorship/template choices and rejected QA24 cleanup remain pending; that runtime/data are retained.


## Checkpoint31 / generation59 — coordination local close

**S38 locally checked;43/49.**70 distinct selected .NET cases pass (62 provider-backed,8 pure),33 client-state cases pass, final app/fixture types and scoped lint with warnings pass. The additive private notice foundation migration and public API diff pass independent reviews; strict no-update golden/registry checks pass. Coordination-browser3 passes8 finite scenarios on final SPA2, including natural focus and actual workspace scroll restoration after a real missing-ID bug was fixed.

Final688 C# sources/274 SPA sources/143 bundle files are recorded and142 served public files match.3701 earlier output hashes and12 prior gallery images are unchanged. New separate gallery8871 adds07 linked work and08 meeting agenda; original prototypes preserved. No API5103 listener.

Notice form is compiled but unmounted and browser UNRUN. Full S45/S47 capture/delivery/reminder integration remains open. Full FR83 completeness gate refuses108 gaps; structural consistency passes. Remaining S12/S35/S37/S45/S47/S49 and existing user choices/pilot/human gates remain open. No commit/push/deployment/maintained flag activation/Accepted/full-goal completion. Evidence: `evidence/checkpoint31-findings.md` and `evidence/checkpoint31-runtime/verification-rollup1.json`.


## Checkpoint32 / generation60 — Notices locally verified

S45 +S47 now locally checked;45/49 screen slices,4remain open.127 distinct selected .NET checks (114provider-backed/13pure),19final finite browser scenarios including17receipt matrix rows, generated/strict contract, app/fixture types, scoped lint, final isolated SPA and solution build PASS. Three initial fixture failures and producer drift refusal are retained with their narrow repairs/reruns. Real transports/hosted/pilot/human acceptance/full specification acceptance remain unproven; no commit/push/deploy/maintained flags.

Evidence: [findings](evidence/checkpoint32-findings.md), [rollup](evidence/checkpoint32-runtime/verification-rollup1.json). Review copy: http://127.0.0.1:8873/#addition-09 (09Notice preferences/10Delivery history; earlier prototypes/gallery preserved). Final source/bundle/preservation/document checks follow in checkpoint32-runtime. Remaining S12/S35/S37/S49 and prior QA24 cleanup decision stay open.


## Checkpoint33 / generation61 — confirmed user choices

Jay approved the exact QA24 cleanup, chose to block issued revisions with unknown captured authors, and chose §8 template enforcement. QA24 cleanup PASS: only exact API4323 stopped and synthetic `hub_qa_checkpoint24` removed; other four database identities, shared local Colima Postgres and retained previews/output remained intact. Earlier rejected cleanup evidence is historical and unchanged. No further cleanup approval is pending.

Template-policy slice PASS: isolated solution build, 176 selected policy checks and two strict API/registry checks. System PMs may manage templates without the editor flag, with the effective Read Only veto and Admin exception preserved; project-only PMs gain no global template grant. Independent source review PASS. This does not complete FR83/S49. Unknown-authorship choice is in FR62/D16 default/source, combined spec and traceability rebuilt (1546 IDs,438 sections,zero unknown references); decision statuses remain unchanged. Evidence: `evidence/checkpoint33-runtime/template-policy-summary1.json`, `qa24-cleanup-approved1.json`, `template-policy1.json`, `contract1.json`. New navigation browser behavior was UNRUN in this slice. No commit/push/deployment/maintained flags/Accepted.

## Checkpoint34 / generation62 — external reviews in verification

Authoring implements actual issued-revision external reviews within the existing engine, immutable incoming party/code snapshots, unknown-author blocking, explicit PM verifier designation and separate verifier evidence. Model, context, frontend, shared consumers and backend source reviews found and repaired the recorded issues; all bounded repairs have source PASS. Canonical author reads are batched, actual carry/removal ancestry is checked, and external replay authority runs under the receipt lock. This is not full-screen acceptance.

Current local PASS: API-only bootstrap and migration builds each0errors/22existingwarnings;432 initial production inputs matched around bootstrap, then only the actual generated migration/designer/snapshot changed. The compiled current API has434 C# inputs. Actual EF migration is `20261103042711_ExternalIssuedReviewSources`; design generation only, no retained database apply. Up preserves NULL/Unknown legacy authors with no backfill. Reviewed Down now refuses retained issued/captured provenance before DDL and restores no fake GUID defaults. Actual provider downgrade witnesses are pending.

Browser runs1–3 retain failures from strict driver selectors/focus assertions and partial passed cases; no complete browser PASS is claimed. The actual SPA is isolated and uses finite synthetic intercepted HTTP, separate from provider persistence checks. Application/fixture types, generated contract review, provider suite, final browser/solution checks and numbered review gallery are still pending. See `evidence/checkpoint34-runtime/`, `parallel-build/NEXT-checkpoint34.md` and `g62-external-review-freeze.md`. Last full screen count stays45/49, full goal incomplete. No commit/push/deployment/maintained flags/Accepted.


## Checkpoint34 / generation62 — S35 local close

S35 locally checked;46/49.121 distinct selected .NET checks and27 finite browser scenarios pass after narrow source/fixture repairs; initial failures and expected producer refusals remain recorded. Actual issued capture/unknown blocking, clerical intake, explicit independent verifier, separate closure evidence, retained carry, atomic versions/batches, current replay authority, all-round exports and dark behavior are verified locally. Existing internal review regressions remain intact. Full solution707C#inputs, app/fixture types, scoped lint with warnings, generated/strict contracts, finalSPA4/HTTP/source/preservation/spec/trace/decision/whitespace checks pass. Actual migration Up/no-backfill and protectedDown source reviews pass, with real legacyDownUp and authorship55000/noeffects tests.

Evidence: [findings](evidence/checkpoint34-findings.md), [rollup](evidence/checkpoint34-runtime/verification-rollup1.json). Separate review copy: http://127.0.0.1:8877/#addition-11. All20 earlier gallery images and5507 earlier output files are unchanged; original prototypes preserved. S12/S37/S49 and human/pilot/hosted gates remain open. Role-explainer sources are isolated drafts only. No commit/push/deploy/maintained flags/Accepted/full-goal completion.


## Checkpoint35 / generation63 — Admin role examples and evidence gate

**S49 local implementation extended; fullS49 OPEN.46/49 checked.**83 matrix areas/257 operations/33 example conditions now use canonical supporting factors and explicit live requirements, with independent system/project memberships and S21PolicyPending. Source reviews corrected false denials and missing actual lifecycle choices. Current-authority leave replay was reproduced and fixed through canonicalAvailability.Manage. Legacy API discriminants remain compatible.

31 distinct selected backend cases (12API/contract,19domain),19 checker defect tests and9 final combined-design browser scenarios PASS. Final solution build6 has zeroerrors/1184incremental warnings;711compiledsource hashes match. Types, scopedlint withrefwarnings, strictcontracts, specfreshness/trace1546IDs438sections0unknown,8decisions andwhitespace PASS. Real provider API and synthetic browser HTTP evidence remain separate.

Format2 coverage pins realPassedcase identities, unchanged run-source snapshots and full reviewed witness fingerprints. Three meeting predicate/command mappings independently pass; otherchildren/parents/guards remainPending and strictcomplete correctlyrefuses. Candidate228method inventory is not equivalence evidence. FinalSPA2/HTTP hashes and6294earlieroutputs/previous143bundle/22galleryassets are preserved. Separate numbered review: http://127.0.0.1:8883/#addition-12.

[Checkpoint35 findings](evidence/checkpoint35-findings.md) records scope, exact failures/repairs, final checks and limits. S12/S37/S49, Undo/date/report-policy choices and broader052/human/hosted gates remainopen. No commit,push,deployment,maintainedflags,Accepted orfullgoalcompletion.


## Local checkpoint commit — report-policy decision received

Jay requested committing the completed local work. The separate [numbered HTML review copy](evidence/review/index.html) is included with the implementation and verification evidence; build caches and scratch baselines remain local. Previously passing application/test sources are unchanged.

Jay has now chosen **project membership for Standard User report exports**. This resolves the report-policy question, but its enforcement/explainer update is not implemented in this checkpoint. S21 remains PolicyPending in the current application and evidence manifest until that follow-up is implemented and verified. Undo and date-cue questions remain unanswered.46/49 locally checked; no complete-matrix, Accepted, push or deployment claim.


## Checkpoint 36 · 2026-11-04 · S12, S37 and S49 closed locally; release-candidate gates

Claude finished the remaining slices after Jay's commit of checkpoint 35 (5663eca1). All work is uncommitted on
`codex/build-052-053`, every 052/053 flag stays off, and no decision status changed except 053-D23 (Jay's own choice).

**Slices.** 49 of 49 are locally checked; cleanup slice S50 (053-D24) is open and gates pilot activation, not this release.

| Slice | What was built | Decision used | Evidence |
|---|---|---|---|
| S12 | Bulk Undo: `POST /api/v1/tasks/bulk/commands/{id}/undo` replays each changed row through the command's own validation and apply steps (permission, current version, workflow path); a row changed since or no longer permitted is refused with the reason; reversal entries carry "Undo of: …"; one Undo per command (derived receipt key), the original actor only, 10 s plus a 5 s transport allowance; hold/cancel return by the workflow step back. Single-change Undo for list, board and panel edits and status moves (`useTaskActions`) sends the same command with the earlier value against the new version. Undo toasts stay pressable over the panel that made the change. | 053-D9 default (10 s; new entries) — still Open | `TaskBulkUndoTests` (AC-SCR-19, AC-SCR-20, window, shift/hold reversal, idempotent retry, no Undo of an Undo, other actor 404); `web/tests/task-undo.cjs` (flag on: rename and status Undo with If-Match/version and reason; flag off: none); `task-cross-project-bulk.cjs` (result Undo posts the exact receipt ID) |
| S37 | Organisation list `date_reason_category` (seeded with the seven defaults; Admin → Date move reasons). With `flag.register_screens` on, a move of a dated milestone or deliverable (change-date, deliverable edit, bulk shift) needs an active category while at least one exists; the save tags the move's own activity entries `dateReason:<id>`, so the hashed entry holds category and reason. Milestone View: "Why dates moved" counts (cascades count with their source move; first dates are not moves), each opening the activity list filtered to them; "Out of sequence: before …" and "No date" cues from §12.3's order (Other excluded). Reason picker in the change-date, bulk-shift, Timeline-drag and Calendar dialogs; the deliverable panel asks on refusal. | 053-D13 default list — still Open | `DateReasonTests` (AC-SCR-65, deliverable edit/bulk, first dates, flag off, all-inactive never blocks); `web/src/lib/milestone-sequence.test.ts` (AC-SCR-66); browser check on a synthetic project |
| S21 / S49 | §8.5.1 Export reports for a Standard User now exports member projects only (active member or PM; `Permissions.ExportReportProject`); the screen and every system role keep all visible projects; the single-project activity-log export refuses a non-member with the reason. The Admin explainer reads the same predicate (S21 no longer PolicyPending). Deactivating a person who still owns open work shows the count and a Reassign work link without blocking (AC-SCR-82). | 053-D23 confirmed by Jay | `ReportExportMembershipTests`; explainer tests updated; browser check of the deactivation notice |

**Fixes to pre-existing failures found by the first full runs** (none of them product behaviour): the CI shard counter now
adds a partial test class across its files; the ExternalReview downgrade test first steps down to its own migration; two
flag-parity tests also strip the later S30/S43 additive fields; the Dashboard parity test compares activity by entry
(the identity backfill's change is visible only while `visual_system` is on); the My Work seen race test also counts the
command advisory-lock waiters that now queue behind the held personal row; the task-grouping and four work-activity
tests strip the final S10/S11/S13 row facts (`workActivity`, `seen`, compound actor fields, `board`, `submissionDate`) and read
My Work's actor from its `workActivity` fact (FR-SCR-15 is rendered through that fact); the work-seen migration test uses the
model only after upgrading to the latest schema; the last-admin test runs on its own database, because shared-collection
tests add Admins and its delete then removed the shared Admin for every later test (all of these failed on 5663eca1 itself,
which had never run the whole suite); the operations and recovery-state browser harnesses resolve
`.ts` imports and stub the navigation preserver; 14 web text keys that collided with server keys of different wording were
renamed; the project board layout uses `layout=board` (FR-PLAT-09 reserves `view`; old `?view=board` links still open it);
redundant cache invalidations go through `invalidateChange`. A drawing-import text key is now interpolated so the key check sees it.

**Two product defects found by the first whole-suite runs, both from the 052/053 build.**
- *Following unread counts were slow and scaled with followed projects.* `FollowingUnread` ran one activity query per
  followed project, and the 052/053 build made each heavier (stored-row visibility plus a per-notification project check
  that re-evaluated the visible-project subquery). With 400 followed active projects a synthetic probe measured
  `/me/following` 1,927 ms and unread count 1,777 ms (the 039–051 release: 1,003 ms and 987 ms); in the shared test
  database the calls exceeded the 100 s client timeout and failed nine tests. It is now one grouped query that joins each
  follow's read marker and checks the visible-project set as one array: 92 ms and 14 ms in the same probe, the same counts
  (183 follow, notification and collaboration tests pass).
- *Admin person-fact routes answered 404 instead of 403 to non-Admins while `flag.capacity_screens` is off*, against
  Jay's 2026-10-25 ruling. The flag check now follows the Admin demand on all five admin routes and the create replay;
  the Admin sweep now sends a JSON body on DELETE so body-bound routes reach authorisation.

**Ratchet (053-D24, Open).** The 052/053 build raised four FR-PLAT-20 counters above the 039–051 release candidate
(ac2fa894): anonymous objects 211→320, run-time text keys 230→386, raw tables 49→63, page permission derivations 2→4.
They are mostly EF projections, hash inputs and planner grids with sticky columns, which a mechanical rewrite could break,
so the baseline records the measured values and S50 restores them before any 052/053 flag goes on in pilot.

**Migrations.** 14 migrations follow the release candidate. Two contain contract operations that need Jay's `contract`
label: `NoticeDeliveryPolicyWitness` drops and re-adds two check constraints that an earlier 052/053 migration created
(additive relative to the hosted release), and `ExternalIssuedReviewSources` relaxes four NOT NULL columns to nullable
(no data loss; rolling back to 039–051 afterwards requires the pre-upgrade backup). The new `DateReasonCategories` is additive.

**Upgrade rehearsal.** A copy of the preview database (main schema, 38 migrations; 5 projects, 34 tasks, 574 activity rows,
22 users) was upgraded by this build to 56 migrations. No existing table lost a row; new tables and their backfills
appeared (79 work-assignment occurrences, 3 org levels, 6 miss categories, 7 date reasons). Against the upgraded copy
with every flag off and then every 052/053 flag on, 6 personas' workspace reads and every project screen answered 200
(permission refusals aside), identity colours backfilled 5/5 on activation, and My Work rendered with no console or server error.

**Release-candidate gates (2026-11-04, this Mac, uncommitted tree).**

| Gate | Result |
|---|---|
| `dotnet build Hub.slnx -c Release` | 0 errors, 1,184 warnings (1,206 at 5663eca1) |
| Whole backend suite, one process | 2,924 passed, 1 skipped (`HUB_SCALE` only), 1 failed: `CalendarViewReplayTests` timed out on a fixed wait under single-host load; its class then passed 3 of 3 runs alone. This is the first whole-suite run of the 052/053 line. |
| CI `web` job browser suites (38, mocked API, local headless shell) | all pass after the fixes above; `test:record-parity` is pull-request only |
| Also run: `task-cross-project-bulk`, `admin-screen`, `visual-tabs` (052/053 suites changed here) | pass |
| `npm run build`, `lint` (warnings only), `test:keys`, `typecheck:tests`, served-asset allowlist, chunk report, `npm audit --audit-level=critical` | pass; 0 vulnerabilities |
| Python `checks` job (CI runner, coverage gate, decisions, ratchet + tests, text keys, backup/pilot/PITR/social, preview fixtures, trace) | pass |
| `trace_spec.py --check` / `--ratchet --base HEAD`, `build_spec.py --check` | pass (1,546 IDs, 438 sections; 303 ACs cited, base 300) |
| `pr_checks.py migrations --base ac2fa894` | two contract migrations flagged, as described above (need Jay's `contract` label) |
| `dotnet list package --vulnerable --include-transitive` | no vulnerable packages |
| Explainer evidence checker | structural pass; 3 of 257 operations Evaluated; strict completeness still refuses (unchanged scope) |

Jay approved a local commit of checkpoint 36: `ca8fccea` (2026-11-04), not pushed or deployed.

## Prototype comparison follow-up · 2026-11-04

Comparing the build with the 2026-10-31 prototype found one 053 defect: with `flag.screen_patterns` on and
provisional or missing capacity, My Week's collapsed line read "My Week: 85 h planned, Free unavailable free".
It now reads "… planned, Free unavailable" (`mywork.weekCompactFreeUnavailable`), and
`work-delivery-today-reviewers.cjs` asserts the exact line; the pattern it used before let the doubled word pass.
The full comparison, two 052 fixes and the open observations are in
[052 verification](../052-visual-system-and-navigation/verification.md), "Prototype comparison".
