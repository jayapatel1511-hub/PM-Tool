# Generation24 current CountCard audit and next bounded proposal

Read-only application audit; source observations only. No application/C#/CSS/shared metadata write, build, test, browser, generation or helper execution. No052 slice or decision is closed. The pending utility navigation question is unrelated and unchanged.

## Governing contract

Read current `spec-parts/31-visual-system-and-navigation.md` FR/AC03–09, FR15/AC07 and `spec-parts/24-record-recovery-and-counts.md` FR-RCV-07/08. FR15 requires a metric icon, dotted definition label with hover/focus help, figure, where/basis, one whole-card destination and accessible name; red is an alert only above zero. The 6/3/2 grid and 20/16px padding are explicit. Hue meaning is domain-specific; person and project identity colours cannot substitute for metric colours.

FR-RCV-07 explicitly distinguishes cardinalities from sums. People/task/project cardinalities require matching destination row counts. Hours, assignments, stalled reviews and decisions summed over projects require a named definition and the rows they sum. Five overdue decisions over two contributing project rows is valid sum evidence, not five project rows. AC-VSN-07's literal row equality applies to Overdue Tasks. Do not change any existing value or definition to simplify a presentation migration (FR-RCV-08).

## Live shared component

`web/src/components/hub/count-card.tsx:12` and `.css` already implement the six icon/metric pairs, hidden decorative icon, dotted label, focus/hover tooltip, valueText for unknown values, explicit three-part accessible name, red-positive attention symbol/text, whole-card Link, tabular32/40 figure, 14px/1px/shadow card, 20px desktop/16px phone padding and 6/3/2 grid. Numeric red0 uses ink and has no attention meta; unknown red values also do not assert an alert. Source equality is not computed render/accessibility/contrast proof.

The component deliberately supports an article without `to`, with a definition button and optional child links, for Home's explicit ratio. This is not evidence that every consumer is a whole-card link. Nothing prevents a future caller putting interactive children inside a linked card; current linked consumers were read for that risk. `SummaryTile`'s v2 `data-count-card` marker alone is not adoption of this component or its colour/geometry/accessibility contract.

## Current consumer inventory

| Current surface | Unit / fixed meaning / destination | Current source and existing evidence boundary |
|---|---|---|
| Home Active Projects | Project cardinality / blue / Active project list with current scope/preset | `Home.tsx149`; shared card, definition, scope denominator; API predicate and source scope helpers already exist. |
| Home Open Tasks | Task cardinality / violet / scoped open Tasks | `Home.tsx151`; where is top project when overview data exists, otherwise scope. |
| Home Overdue Tasks | Task cardinality / red positive, ink0 / scoped overdue Tasks | `Home.tsx153`; existing `home-overview.cjs` checks focus/help, name substring and zero absence, but does not open this card and compare actual task rows. New bounded script fills that mock-route gap. |
| Home Due Within n Days | Mixed due-item cardinality / amber / same-date/preset/status Home deadlines-only list | `Home.tsx155–160`; actual task/deliverable/milestone split retained as additional text; same-type row count and pagination must be checked on this exact list, not on Tasks alone. `home-overview.cjs` checks destination and105-item pagination in separate cases; does not bind every card figure to a real backend cohort. |
| Home On Track | Percentage with explicit Green/evaluated counts / green / two project links plus optional critical project links | `Home.tsx161–164`; article rather than whole-card link avoids nested anchors. FR-SCR-08 explicitly requires each ratio part linked. Preserve that contract; the article is not raw one-link FR15 proof. Unknown is labelled Unavailable. |
| Home Team Members | Distinct people cardinality / teal / scoped Team | `Home.tsx165`; definition and discipline where exist; current audit does not prove deduplicated Team destination parity. |
| Portfolio Active / computed Red/Yellow/Green / submissions14 | Project cardinalities / blue/red/amber/green/amber / current Portfolio list with exact filters | `Portfolio.tsx124–145,240–242`; shared cards, where from server projects/client facts, same scope retained. Submissions preserves a tighter date filter. Health uses computed health and Active status. Existing `portfolio-completeness.cjs` checks six anchors, name IDs, one tooltip, colours by class, retained filters. Full real API row parity for every card not supplied by that browser fixture. |
| Portfolio overdue decisions | SUM overdue decisions across contributing project rows / red / Portfolio overdueDecisions=true | `Portfolio.tsx243`, `Portfolio.cs67–70`; definition explicitly names sum. Existing browser case deliberately checks5 over2projects with per-project3+2, an appropriate unit-aware acceptance. Retain it. |
| Project Dashboard six primary cards | In Progress tasks violet; review tasks amber; overdue tasks red; upcoming deliverables amber; pending decisions violet; open issues currently amber | `projects/Dashboard.tsx204–228`; shared cards use server `{value,link}` unchanged; project or discipline where plus whole-project notices retained. Open issues is open work with no due/review/risk predicate, so amber is not supported by FR04's current meaning; recommend violet, without changing its predicate/value. |
| Dashboard12 secondary counts | Cardinalities / sibling links, not CountCards | `Dashboard.tsx229–236`; intentional no nested anchors; every secondary destination remains. `project-dashboard-completeness.cjs149–164` checks all six direct href/value/where/tone/no-inner-action facts and12 sibling destinations; only first card help is checked, not all accessible names/list counts. |
| Staff5 summary tiles | Mixed PEOPLE / person-project pairs / stalled-review TASKS; legacy decorative accents | `Staff.tsx56–60,92–98`; still SummaryTile buttons. No metric chip/where/dotted label/whole-card link/CountCard grid. Header TitleBlock already exists. See detailed seam below. |
| Time Total shown | SUM recorded entry hours / should be teal / current exact time rows | `Time.tsx54–62,179–184`; still blue SummaryTile with definition but no destination/where. Own-week total is recomputed from the actual displayed week entries; other modes use the existing time-read total. Preserve both and zero/partial facts. Do not test row count equals hours. |

Existing Home, Portfolio and Dashboard work invalidates the old Home-only adoption audit. Do not repeat that claim or replace those implementations. Their scoped helpers, aggregate definitions, independent visual/domain flags, sibling controls and unknown values stay intact.

## Staff: exact units and unresolved destinations

`MyWork.cs188–248` first obtains current authorized staff/visible live projects, then office/project/discipline selection, then creates each person row. `Projects` is the DISTINCT UNION of current membership project IDs and active discipline-lead project IDs. It is not the person's count of roles or raw memberships. Tasks/reviews are bounded to the viewer's visible projects. After applying the current single `indicator`, the response computes:

| Tile field | Exact unit / formula over returned people | Valid reproduction |
|---|---|---|
| staff | PEOPLE: row count | Same returned people count |
| assignments | PERSON–PROJECT PAIRS: sum(row.projects) | Same contributing people, exact visible Projects column summed; never people row equality |
| withOverdue | PEOPLE with row.overdue>0 | Exact matching people cohort |
| withBlocked | PEOPLE with row.blocked>0 | Exact matching people cohort |
| reviewsStalled | STALLED REVIEW TASKS: sum(row.reviewsStalled) | Same contributing people with exact stalled-review column/basis exposed and summed; never total Reviews or people row equality |

`tests/Hub.Tests/Api/CountLinkTests.cs298–300` already encodes2people /3stalled reviews. Do not replace this with2. The Staff table has total `reviews`, not an explicit stalled-review column; `OneSignal` can show only another highest-priority signal, so that alone does not expose every contributing stalled count.

The existing `/staff/{id}/assignments` reader (`MyWork.cs258–284`) starts from ProjectMembers. A lead-only project can contribute to row.projects but have no such row. Do not invent an assignment-row endpoint parity claim or issue per-person requests to manufacture one. The already returned Projects column is the stable sum basis for the card's existing definition.

Current tile click replaces `indicator`, or clears it on second click; Staff and Assignments also clear it (`Staff.tsx56–60`). Yet all tile figures are computed after that current indicator. For example, in blocked scope A and B, A has overdue work: tile withOverdue=1. PersonC outside blocked scope also has overdue work. Clicking overdue replaces blocked and opens A+C=2. Clearing a selected tile similarly widens the cohort. Changing a button into a URL anchor alone preserves this mismatch.

No current multi-indicator intersection or scalar people-cohort query exists in this Staff contract. Do not invent `assigneeId`, a task-list filter, GUID list limit or permission-widening staff request. Office/discipline/inactive/scope URL state, direct/all/reports/unit authorization and visible-project boundary must stay unchanged. The current UI does not include backend-supported projectId in its f object; adding that is a separate scoped behavior change, not required for this visual audit.

## Bounded next implementation proposal — not an edit authorization

1. Root loans one shared CountCard writer only if selected-state affordance is needed; preserve its current API and legacy CSS by default. Projects changes only the Dashboard Open issues tone to violet after confirming FR04 interpretation. Existing cards receive regressions rather than a rewrite.
2. Capacity loans Staff.tsx plus a single screen-local destination/basis helper. Root must first choose the exact behavior for cardinality filter intersections and selected-clear. Recommended default: count cards reproduce their exact current cohort; keep replace/clear filter actions as separate labelled controls, with legacy off buttons unchanged. For overdue/blocked, an explicitly reviewed visual-only local view predicate over the already complete authorized people response can reproduce intersection without a new API; it must retain the server indicator in the URL and separately encode the view, support back/copy/reload/saved state, expose active selection/clear, and receive root metadata review. Do not silently ship that new URL key in this audit. If root prefers backend intersections, request a separate exact contract first.
3. Staff metric tones: Staff teal, Assignments blue (project-assignment quantity), With overdue red, With blocked red, Reviews waiting amber. Required where lines name current people scope and sum units; both aggregate cards link to their contributing people rows, where Projects and a new explicit stalled-review figure reproduce their sums. Whole-card links contain no filter button or nested anchor. Preserve existing staffing/expand/remove/current permissions and second-click clear through an explicit retained filter action.
4. Capacity loans only Time.tsx's Total shown presentation and local exact-destination helper. Use teal and a one-line sum definition/period-person-project-task basis. Link the existing exact current `/time` entry view/anchor while retaining from/to/scope/userId/projectId/taskId; do not point to a report that loses user/task selection, or equate entry count and hours. Own-week/source total logic and all existing project/task subtotals stay unchanged. No API contract is needed for a self-contained exact same-view link, though current read scope and displayed rows still need joined verification.
5. Root runs the existing Home/Portfolio/Dashboard/Time/Staff affected checks plus the new focused script, verifies all destination units from actual authorized reads, and records remaining whole052 S03/S06/S09 gaps. No broad count, schema, planner, navigation, prototype, palette or infrastructure redesign is proposed.

## New focused acceptance scope

`web/tests/count-card-contract.cjs` is a separate source-harness script, not a replacement for existing suites. It is intended to check six actual shared-component token pairs, focus/help/name, one-link/no-inner-control structure, red positive/0/unknown behavior, exact padding/radius/grid/equal heights, plus real Home→Tasks keyboard navigation with mocked API rows under the same exact project scope. The source fixture cannot prove arbitrary destination pages or server authorization; the real route leg cannot prove backend predicates because its API is mocked. Root owns parse/build/browser execution. Neither current script authorship nor prior checkpoint19 results are a runtime PASS for this new work.
