# Reports S23–S25 — bounded implementation proposal

Generation14 read-only proposal. Application/test sources, previous Dashboard/Portfolio work and shared files remain held. This note is not implementation, verification or acceptance. No builds/tests/browser/generation/helpers/goals/migrations/commit/push/deploy/activation ran. Root must dispatch an explicit file boundary before implementation.

## Recommended order

1. **S23:** question catalogue beside the open report; named parameters and self-contained Copy link; exact missing-task counts with working, permission-preserving drill-downs. This is the next useful batch. Complete its shared cohort/view seams before advertising either feature.
2. **S25:** typed, exact period/unit transform for the four existing hour-series report forms, with matching exports and basis metadata. Prepare exact arithmetic separately from UI; preserve each report's own horizon.
3. **S24:** one optional discipline chart from the table's response. Begin with Workload by Discipline's actual task-hour rows. A paired capacity/deficit chart requires an explicit same-read capacity row contract; do not manufacture supply or join incompatible discipline meanings. Keep S24's capacity-deficit acceptance open if that adapter is not included.

All additions belong to `capacity_screens` (Part E). Existing owner gates remain: capacity_reports, org_structure, task_pickup, commitment_reliability, route_counter and role/project/people scope. visual_system and screen_patterns must not enable Report completeness. No schema, setting, permission grant, report framework or chart dependency is needed.

## Current evidence and seams

- `Reports.cs:67–138` defines25 report codes. `/reports` filters them by current actor/unit-leader eligibility and owner flags (`147–158`); `Prepare` repeats the run/export checks (`190–198`). Catalogue questions must decorate this permitted list, never an independent hard-coded menu that reveals unavailable reports.
- `ReportView` has dynamic Columns/Rows, total/lower-bound/truncated/listLink/parameters and optional visual AsOf (`36–38`). Run uses one result and shows at most2000 rows; queries retain at most Export.MaxRows+1 and oversized exports refuse (`61–62`, `160–176`). Count actual source queries, not these capped rows; label any genuinely capped chart as a visible subset.
- `Reports.tsx:36–58` has a two-column catalogue with no search; `62–100` renders an isolated report, URL parameters and ExportMenu, without ViewMenu/Copy link/adjacent catalogue. Existing labels win, including **Workload by Employee**, despite AC-SCR-39's conversational “by Person.”
- `Reports.cs:202–273` owns report projects/parameters. Explicit selected projects are intersected with current visibility; default report projects include Setup/Active/On Hold/Complete. Ordinary workspace task scope excludes Archived/Cancelled. An explicitly selected archived report project therefore cannot be assumed to reproduce in a generic workspace link.
- `Workload.cs:125–235` owns the employee report's task/person cohort. It uses visible Setup/Active projects and excludes Complete/Cancelled/**On Hold tasks**. Its discipline filter selects people who have that discipline's work, then retains those people's other visible work; it is not a blanket task-discipline predicate. Capacity and reservations remain person facts. This differs deliberately from S22's open-due-window held-task rule.
- Workload `NoDueDate` is **hours**, not a count of tasks. `Unestimated` is a task count. EmployeeRows and DisciplineRows round weekly hour output to one decimal (`403–464`); month/FTE cannot start from those rendered values. Use canonical spread/cell values before report display rounding, without changing Workload's canonical two-decimal remaining/spread rules.
- `CapacityReports.cs:16–26` uses Planning.ReportGrid, Monday normalization and settings.PlanningHorizonWeeks, with1..26 weeks. `34–98` Team Capacity has hour measures plus headcounts (over/under/partial). `100–130` Allocations by Source has a weekly-hours form and a different entries-without-project form. `Planning.cs:290–350` likewise uses a people-cohort discipline filter, includes its existing visibility/coverage/max rules and truncates internal lookahead to displayed weeks. Do not count lookahead tasks as displayed-week facts.
- `Views.cs:30–95,115–175` already implements personal/project definitions, PM/lead shared mutation, defaults, concurrency and dropped-reference reporting. Reports are absent from maintained `Records/ScreenLists.cs:All/SavedViewKeys`. ViewMenu defaults apply only to an empty URL; explicit shared parameters win (`views.tsx:22–58`). Its listType/project binding must be keyed/reset when the report changes.
- `ScreenLists.ValidReference` treats project-scoped disciplineId as a ProjectDiscipline ID, whereas Reports use the organisation Discipline ID. Reusing that validation unchanged for report views would drop a perfectly valid Civil parameter.
- `TaskQueries.cs:171–172` applies withoutDueDate only under home_overview; no unestimated filter exists. Neither feature can be used as S23's drill-down while relying on an unrelated Home flag. Huge lists of task IDs in a URL are also unsuitable.
- `ExportFile.Send` in `Reports.cs:748–768` preserves export auditing/caps and XLSX parameters. CSV receives only the optional `header`, not ordinary parameters. Existing `Infrastructure/Export.cs:32–42` already supports a safe label,value metadata block above CSV columns. S25 should pass a report-specific enabled header rather than rewrite the exporter.

## S23: exact counts, saved definitions and discoverability

### Parameter-scoped missing task facts

Propose an optional, null-omitted `ReportView.Completeness` under capacity_screens: unestimated and undated open task figures, each with exact count, complete/lower-bound state, explicit basis and a server-authored destination. For workload/capacity where scheduled figures are known subtotals, say so; a missing estimate is not zero hours. Task flags/permissions/people scope remain the source reader's.

Use a reusable report-task cohort **before count/list pagination**, not another approximation of report filters:

| Existing report code/form | Source task population for completeness |
|---|---|
| tasks-due-this-week | Existing Tasks(c, TaskFilter) with selected week, projects, discipline, mine and assignee. Date-qualified rows cannot themselves be undated; say that if the exact count is0. |
| overdue-tasks | Same source including minDaysOverdue and assignee; no broader all-overdue destination for a custom threshold. |
| blocked-tasks | Same source including blockerType; retain its pre-cap source filtering. |
| tasks-blocking-others | Existing report task population including evaluated blocking predicate. |
| stale-work | Existing cutoff, report projects and held exclusion, including custom days. |
| review-queue | Only task constituents of the existing mixed task/deliverable queue, after reviewer/project predicates; no deliverable counted as a task. |
| task-hours | Distinct task IDs referenced by the existing permitted Time.Visible entry cohort and effective date/scope/user/project parameters; count a task once despite several entries. “Open with no due date” additionally applies current canonical open status. No estimate-versus-actual comparison. |
| workload-employee | Tasks of the actual returned authorised person cohort; preserve its project, supervisor, office and discipline-as-people selection semantics. Unknown/undated context follows the existing unplannable population, not sum(NoDueDate hours). |
| workload-discipline | Its authorised office/people/visible-project task query before discipline grouping. No implied person-capacity total. |
| team-capacity; allocations-by-source weekly form | Canonical task context for the actual returned Planning.ReportGrid people, plus that report's unit/project/date/source restrictions where those truly restrict its task context. Clearly identify unknown task context as context, separate from planned-entry hours. No reliance on capacity_polish TaskContexts being present. |
| allocations-by-source entries-without-project | Report source entries have no task IDs. Show the same scoped people task-context counts with an explicit context basis, or mark not applicable; never infer a task count from entry count. See decision below. |

Other reports (deliverables, milestones, decisions, issues/risks, actions, history, pilot/route measures, staff assignments, pickup and commitment reliability) do not acquire unrelated missing-estimate cards merely because some contain a numeric column. Their own rows remain intact.

**Root seam needed:** a read-only source/cohort adapter shared by result counts and the actual task-list destination. Suggested stable locator is reportCode + missingMetric + an encoded, whitelisted report parameter map; names are a proposal, not an existing API. The existing `/tasks` page/API can accept this named cohort only under capacity_screens, re-run the originating report's owner/role checks and intersect with current task/project visibility. A report-specific cohort must supply the exact permitted source query, including an explicit archived selection when supported, rather than first applying the incompatible default workspace scope. Distinguish no eligible people/projects from an absent filter; an empty set must never widen to all.

Reject unknown metric/code, stale/hidden project/person references as appropriate, malformed/oversized locator parameters, and report-ineligible recipients. Do not persist results/IDs. Translate organisation/project discipline IDs deliberately. Reuse normal task pagination/panels and show a removable report-origin filter token. Root owns TaskEndpoints/TaskQueries/WorkspaceTasks/registry seams; this lane must not quietly edit them.

### Saved views / Copy link

Recommend one maintained listType per report, `report:<code>`, with only that report's accepted parameters. This avoids one report's default applying to another. Root owns ScreenLists registration and Views' report validation/eligibility guard on GET/POST/PATCH/DELETE; reuse SavedView storage, concurrency and existing PM/lead sharing checks. No results or new table.

The UI uses the existing ViewMenu after those registrations are saved. The URL keeps actual report parameters; UI-only catalogue search, view marker, open panel and sharing container must not be sent to Run/export or stored as unknown report filters. Preserve meaningful `history=all` for the activity-log report; current Reports chosen excludes history and materializes its default30-day range, so save/copy must not accidentally turn “all history” into “last30 days.” For a defaulted range/week, capture its applied concrete values when Save/Copy promises the same period.

Sharing project is a **view container**, not a new report-data filter. Many reports (notably Team Capacity) do not accept projectId. Provide an explicit authorised “Views for project” context selector for those; do not inject that selected ID into the report query. Existing single selected report project may preselect the container, without making the two concepts equivalent. Preserve current personal-view container behavior, and do not treat Executive as PM/lead sharing permission. Re-key ViewMenu by code/container so its one-time default application does not remain stuck on a previous report.

Copy link serializes canonical supported applied parameters and the existing report path, not saved owner/name/result data or a dependency on a personal view ID. Recipient opens the same parameters under their own present permissions. Views reference cleaning must use report dimensions (global Discipline for report disciplineId), report-known enums/ranges, owner flags and accessible references. Dropped parameters remain visibly named. Applying a view never widens visibility or suppresses a normal forbidden result.

### Catalogue with questions

Add an optional question field resolved through maintained Text metadata while enabled. Search titles/questions/descriptions locally within the already-authorised catalogue. Keep the catalogue in an adjacent aside on desktop and an accessible labelled disclosure on narrow layouts; it remains reachable while a report is open. Preserve search when opening/backtracking, but exclude it from report data/export parameters. Active report has a visible/current indicator. Existing table remains the results surface.

Suggested mapping covers all25 current codes; titles stay unchanged:

| Question | Report codes |
|---|---|
| Who has room in the coming weeks? | team-capacity, workload-employee |
| Where is work concentrated by discipline? | workload-discipline |
| Where do planned hours come from? | allocations-by-source |
| What needs action this week or is holding work up? | tasks-due-this-week, overdue-tasks, blocked-tasks, tasks-blocking-others, stale-work |
| What is due, issued or approaching submission? | upcoming-deliverables, deliverable-status-by-project, upcoming-milestones |
| What needs a decision, response or review? | open-decisions, open-issues-high-risks, meeting-actions-outstanding, review-queue |
| What changed and which projects need attention? | project-activity-log, projects-at-risk, health-history |
| Where are people assigned? | staff-assignments |
| What task time was recorded? | task-hours |
| How are pickup and commitments working? | pickup-offers, commitment-misses |
| What do pilot activity and screen measurements show? | pilot-measures, pilot-route-use |

No additional report advertised from research-only catalogue suggestions; existing owner flags decide availability.

## S25: exact periods, units and exports

Eligible forms are workload-employee, workload-discipline, team-capacity and allocations-by-source **hours-by-source**. The entries-without-project form has fixed dates/rate columns, not a week-series matrix. Pilot rows containing a week date are not hour columns; staff assignment/pilot/headcount rows never become FTE.

Use one pure server transform with typed measure descriptors, actual ISO Monday dates and canonical decimal hours **before display rounding**. Do not parse translated headings, divide every numeric column, multiply source entry rates independently of canonical coverage, or derive from screen-capped rows. Preserve the typed raw read privately inside the same report adapter so screen and export share the exact calculation.

- Default grouping week and unit hours. A week belongs to `(weekMonday.AddDays(3).Year, Month)`; use Thursday's year as well as month at New Year. A partial first/last month contains only the report's displayed weeks.
- Hour period = sum canonical visible hours for its actual k weeks. FTE period = those exact hours / (k × nullable standard DefaultWeeklyCapacityHours). All people/measures/aggregate hours use the same standard basis; a custom32-hour person does not get a32-hour denominator. Canonical utilisation percentages still use effective available capacity separately.
- Null/zero/invalid standard denominator → unavailable, not zero/divide-by-zero. Unknown inputs stay null with known subtotal/coverage. Do not turn partial scope, provisional availability or missing estimates into complete totals. Empty known cohorts can be0; unknown cohorts cannot.
- Distinguish Hours, Count, Percent, Date and Label measurements. Team Capacity over/under/partial **people counts** and unestimated/undated task counts remain counts; FTE applies only to hour measures. Totals use canonical unrounded numerator/denominator before final display rounding, never sum rounded weekly FTE. Explain the final display rounding when rounded row labels cannot arithmetically reproduce a rounded total.
- Distinguish period hours from a **weekly rate** and undated/unallocated hours as well. EmployeeRows' fixed capacity column is weekly capacity, not eight-week hours; its NoDueDate hours have no period assignment. Keep both explicitly in their original units/context rather than divide them by a month denominator. The entries-without-project hoursPerWeek rate likewise remains a rate. Monthly consolidation of weekly people counts has no defined sum/unique/max rule; recommend retaining a labelled weekly count context beside the month/hour table until root chooses the semantics below. Never sum counts and label the result distinct people.
- Workload stays exactly8 weeks (`WorkloadEndpoints.Weeks=8`) and its current-week report anchor. Do not advertise a26-week Workload horizon or make its report accept a currently ignored from/weeks parameter. Capacity reports retain settings default and existing1..26 bound. Internal Planner lookahead is not part of k. Task Hours retains its current date range and370-day guard; it does not become a weekly forecast report.
- Export Run and Export use the same period/unit function. State standard weekly hours, every period's displayed week count/dates, effective scope, missing inputs/partial coverage, grouping and unit. Pass enabled report metadata to existing ExportFile header so CSV as well as XLSX states it; retain BOM/escaping/formula safeguards/caps/export audit. Chart visibility/group presentation must not change export. Explicit month/FTE parameters intentionally change period/unit columns, consistently in both formats. Off exports remain exact.

Minimal new files may be `ReportPeriods.cs` and a named pure `ReportPeriodsTests.cs` once dispatched. Root owns or explicitly loans any thin Workload/Planning raw projection seam; this lane must not change canonical forecast, reservation coverage or person availability math.

## S24: honest optional chart

Use a small SVG/CSS bar component like the existing Home charts, with the table as its text equivalent. One chart per report, after completeness/counts and beside the table on wide layouts. No chart library and no person grouping control. Initially Workload by Discipline can chart its existing discipline task-hour rows for the selected displayed period; label visible task hours/known subtotal when inputs are missing. It has no supply rows, so it cannot claim a capacity deficit. Chart values must exactly equal the displayed table measure from that one response, including when the table is limited/partial.

To satisfy the capacity-deficit branch of AC-SCR-41, root must explicitly authorize a same-read discipline capacity table adapter. Existing `Workload.cs:260–307` DisciplineContext is the closest canonical source: supply is Planner remaining capacity by **home discipline**; demand is separate open-role/unassigned-task work discipline, with unestimated/undated coverage. It is private and currently does another Planner read. Do not graft its numbers into the current work-discipline estimate row by name, count free capacity once per project, or call the Workload API a second time from the chart.

Recommended capacity-chart follow-up: root/Capacity exposes a pure adapter accepting the already authorised canonical Planner read and selected visible demand, then report rows carry its explicit supply/demand/shortfall/coverage. Chart reads those same rows; free80/open-role24/known-unassigned90 yields short34 only with all required inputs available. Person names/series are absent. Provisional/partial supply, missing estimates/undated demand or a role calendar whose full spread is unavailable must not become short0. The current helper's empty role-spread handling/undated shortfall guard needs review before calling this complete; do not silently revise that canonical implementation in the Reports lane. If monthly shortfall aggregation is not agreed, keep capacity chart on a labelled selected week until the table's period rule is settled; do not sum gaps or net surplus weeks client-side.

## Exact ownership requested for future dispatch

| Writer | Proposed files / responsibility |
|---|---|
| Projects/reports lane | Reports.cs project report envelope/adapters; CapacityReports.cs only if explicitly loaned; Reports.tsx; new report-local presentation/period/chart helpers/CSS; uniquely named API/domain/browser tests and own notes. One writer across all report parts. |
| Root | Views.cs, ScreenLists.cs, generic ViewMenu changes if needed, task-list cohort integration/chips/keys, metadata/Text/en, generated API/registry/goldens, spec/acceptance ledgers, compile/test/browser/export execution. |
| Root or explicit Capacity loan | Workload/Planning cohort/raw exact-hours/DisciplineContext seams. Preserve D's active allocation-shift files. Publish exact method/record contract before Reports wiring; no stubs or alternate math. |

No application/test source is reopened by this proposal. Generic schema/auth/settings/feature activation stays out of scope.

## Meaningful acceptance plan for root

- S23 API: Civil4 unestimated /2 open undated with distinct IDs, default and parameter variations, a task in both cards, known0 versus unavailable, no counting hours/duplicate entries; held/terminal rules by report; exact drill-down row IDs across pagination. Use more than2000 report rows and beyond export cap to prove counts are not screen sums. Include custom minDays/blockerType/stale cutoff and mixed review queue.
- Scope tests: Lena/Sam each open the same saved Civil12-week Team Capacity parameters and get only their own people/projects; hidden sentinel name/ID/hours excluded; no-eligible-people stays empty; explicit archived report selection reproduces or refuses visibly rather than silently dropping it. Independently capacity_screens on/off with home_overview/capacity_polish off.
- Views: personal owner-only, PM/lead project sharing, member can read but cannot edit, Executive alone cannot share, stale version refusal; report type/default isolation; invalid/dropped references including global versus project discipline; owner flag off; report permission loss; history=all versus concrete default30-day period. Copy parameters work without saved-view ownership or ID.
- Catalogue browser: “room” matches permitted Team Capacity/Workload; title/question/description search; hidden report flags stay hidden; opening one leaves catalogue, query and focus usable on desktop/phone. View apply and copy preserve applied parameters without contaminating Run/export with UI-only keys.
- S25 pure/API: four40-hour-standard weeks with160 and120 hours/custom32 capacity →1 and0.75, aggregate1.75; two-week80-hour partial month →1. Weeks spanning year/month/leap boundaries use Thursday; uneven2/3-week periods and internal lookahead excluded. Add sub-tenth canonical hours so calculating from old1-decimal rows would fail. Null/zero denominator and null-hour coverage; counts/percent columns not divided. Hours/month/FTE CSV/XLSX agree with screen and carry basis/scope/missing inputs. Off body/export exact parity; Workload remains8, Capacity1/26 accepted and0/27 refused.
- S24 browser/API: bars exactly equal table rows from one report GET; no person control/series, no second data read, keyboard text equivalent/table retained, zero/negative free/unknown/partial values explicit. Capacity branch only after adapter:80/24/90→34; unknown supply or demand gives unavailable rather than definitive gap; chart toggle leaves exports unchanged. Month chart follows the table's settled aggregation, not independent arithmetic.
- Run existing focused CapacityReportsTests, SearchReportsTests report/export cases, ViewsTests and PlanningReportingWorkflowTests after new cases. Browser reuse planning-reporting scripts and root's Reports census/reflow at final changed state. Preserve export-cap/formula/privacy and all dark-flag contracts. No full suite by default.

## Decisions to settle before the dependent implementation

1. **FR39 missing-work cohort:** the text does not say whether date-qualified task reports should show exact result-cohort counts (therefore0 undated) or a broader base-population exclusion count. Recommended default: exact report cohort for row reports; canonical explicitly labelled unplannable task context for Workload/Capacity. Do not silently strip a report's date/status predicate just to make a card nonzero. For entries-without-project, show explicit scoped task context or not-applicable pending root's choice, never a fabricated entry/task relationship.
2. **Shared-view container:** support a deliberate project container independent of report parameters. No arbitrary new Team Capacity project filter and no broader sharing permission. This is a UI composition choice within FR40, not a permission change.
3. **Chart scope:** initial demand-only discipline chart is honest but does not prove the capacity-deficit branch. Recommended default: stage that branch behind the exact same-read adapter instead of claiming all S24 acceptance. A month-shortfall definition needs explicit table semantics before use; selected-week chart is the bounded default.
4. **Chart/export wording:** interpret FR41 “export unchanged” as chart-independent export. FR43 separately requires chosen month/FTE parameters and basis in export. Preserve legacy off exports and all existing underlying canonical data; do not treat this as permission for an unrelated export redesign.
5. **Non-hour weekly rows in month mode:** Team Capacity's over/under/partial counts are weekly people counts. A monthly sum is people-weeks, not distinct people; max or unique-any-week is a different defined measure. Recommended bounded default: group hour rows by month and retain the original labelled weekly count context separately, with the same distinction in export. Root may instead specify a clearly labelled distinct-people-in-any-displayed-week row computed from the same raw people flags, but must not silently choose sum/max. Fixed weekly-capacity rates and undated hour context also retain their explicit original units.

053-D22 remains Open in DECISIONS, but §57.9 explicitly says decisions use their proposed defaults until Jay decides otherwise. Implement standard-hours FTE dark under that existing default once dispatched; do not mark the decision confirmed/Accepted or ask Jay the same denominator question again. No other new unresolved policy decision was found that blocks the read-only proposal.
