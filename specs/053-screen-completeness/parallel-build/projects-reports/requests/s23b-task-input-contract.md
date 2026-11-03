# S23-B report task inputs — concrete source/read/list proposal

Read-only refinement under generation16 hold. Root accepts exact result cohorts for task-row reports, explicitly labelled canonical unplannable context for actual Workload/Capacity people scope, and scoped people task context for the entries-without-project form. Nothing here is implemented. Application/test sources remain held; no runtime, checks, build, browser, generation, helpers or goals ran. S23-B/FR39 remains incomplete independently of S23-A.

## Recommended boundary

Keep drill-down **inside Reports**. Add a report-owned paged task-input read, reusing existing source selection and TaskQueries row projection. Do not modify `/tasks`, generic TaskFilter, workspace query semantics or saved-view keys; do not put task/person ID arrays or serialized result snapshots in links.

One optional summary on the existing ReportView, two metric links and a read-only dialog/pager on the existing report page suffice. No persistence, new report type, operational command, chart, FTE or new permission. capacity_screens gates the summary and list endpoint independently of home_overview, screen_patterns and capacity_polish. Every original report owner/actor/project/people permission still applies.

## Proposed exact endpoint and UI URL

```
GET /api/v1/reports/{code}/task-inputs/{metric}?<canonical report parameters>&page=1&pageSize=50
metric = unestimated | open-undated

UI:
/reports/{code}?<canonical report parameters>&taskInput=unestimated&inputPage=1&inputPageSize=50
```

The endpoint accepts **the same scalar data keys as that report**, plus paging. Global disciplineId remains the global Discipline dimension. Actual supported from/to/week/weeks/source/confidence/mine/reviewer/custom thresholds travel unchanged after the S23-A successful canonical capture. This route accepts no invented project filter for Team Capacity and no Workload26-week horizon. Existing Http.Paging defaults50 and clamps1..200; retain that bounded convention. Metric/code are strict whitelists; unrelated report codes have no task-input route.

API route path, not query, selects the metric. UI taskInput/inputPage/inputPageSize are display state excluded from Run, export, saved parameters and Copy link, just like the existing catalogue/view/container state. Closing removes these keys and returns focus/scroll to the exact count link. Normal Task source opening captures that dialog/report origin independently of screen_patterns; close returns to the same metric/page/report parameters. Saved report views store data parameters only, never this open list/page.

The summary generates link destinations from its **actual canonical applied parameter map**, not raw browser query or a list of matching IDs. Reopen/deep links work without a stored view ID. The recipient and every page change reauthorize and rerun the source under current permissions. A later live read may legitimately have a different total/asOf; display its current count rather than claiming the initial report snapshot is immutable.

## Proposed DTOs

Names are exact proposed targets for root review, not existing types:

```csharp
public sealed record ReportTaskInputCount(
    int? Count, int KnownCount, string Href);

public sealed record ReportTaskInputs(
    string Basis, string ScopeCoverage, int UnavailableReferences,
    ReportTaskInputCount Unestimated,
    ReportTaskInputCount OpenUndated,
    IReadOnlyDictionary<string,string> Parameters,
    DateTimeOffset AsOf);

public sealed record ReportTaskInputPage(
    string Code, string Metric, string Basis, string ScopeCoverage,
    int UnavailableReferences, int? Total, int KnownTotal,
    int Page, int PageSize,
    IReadOnlyList<TaskQueries.Row> Items,
    IReadOnlyDictionary<string,string> Parameters,
    DateTimeOffset AsOf);
```

Append `ReportView.TaskInputs` as optional/null-omitted under capacity_screens only. Other reports omit it; do not give unrelated deliverable/staff/pilot/decision reports meaningless0 cards. `TaskQueries.Row` is the existing read shape; the dialog is read-only and offers existing Task source panels, no copied task commands.

Basis is a small maintained enum/string key with translated text, e.g. `ReportTaskRows`, `WorkloadPeopleContext`, `PlannerPeopleContext`, `CurrentRecordedTaskContext`. ScopeCoverage means **CompleteForVisibleParameters** or **VisibleOnly**, inherited from source scope, never a claim about all organisation work. Exact visible-query counts can still be numbers under VisibleOnly, accompanied by that scope label. Known empty eligible people/project sets yield0; an absent/unavailable source never becomes an empty all-people fallback.

Ordinary complete references use Count=KnownCount and Total=KnownTotal. Nullable totals are reserved for genuinely unavailable task-reference facts (Task Hours case below), retaining known matching counts and a disclosure. Unknown does not turn into0. Each Href opens exactly the known qualifying task list and the same unavailable-reference banner. The two metrics overlap: a task without an estimate or due date appears in both; never add the cards into a fabricated total.

## One reusable source query, used three ways

Proposed **internal-only** record/delegate in a report-owned partial/helper, not an API DTO:

```csharp
sealed record ReportTaskInputSource(
    IQueryable<WorkTask> Tasks,
    string Basis,
    string ScopeCoverage,
    int UnavailableReferences);

// Internal Ctx retains existing current Access/settings/Today/Now.
static Task<ReportTaskInputSource?> TaskInputSource(Def definition, Ctx context);
```

Null means a report definition is not task-input-applicable, not an unavailable source for an eligible report. Normal permission/owner-flag/invalid-range/read errors propagate. No stub, invented zero, independent permission model or catch-all empty query.

For task-row reports extract existing source predicates into report-local query builders and have the **existing Run**, summary and task-input list all consume those same builders. Do not independently restate min-days/blocker/stale/reviewer filtering in a new switch that will drift. In particular, preserve Blocked's existing SQL EXISTS condition for blocked_by type and blocking != false **before** any Take. The current formatted-row safety check must agree with that shared predicate; its handling of legacy missing blocking flags is retained.

Common metric functions over the distinct authorised source:

```
unestimated:  EstimatedHours == null
open-undated: DueDate == null && Status != Complete && Status != Cancelled
```

The source already owns any narrower status policy. Do not impose a new global held rule: row reports keep their own predicates; Workload/Planner canonical task context excludes On Hold via WorkloadEndpoints.Tasks. Missing-date uses the actual null field, not Home's separately gated withoutDueDate or the narrower noDueDate evaluation signal. Unestimated is a task count, never Sum(NoDueDate hours).

Execute CountAsync on the full metric query before report ScreenRows2000, Export.MaxRows+1, formatting, grouping or dialog pagination. Remove duplicate task references with SQL DISTINCT/Contains at source boundaries (especially Task Hours); a task is counted once per metric. The pager executes that identical metric query, orders by permitted project number, task Seq then Id, and only then Skip/Take. It returns live full count even on an empty high page. Do not load every task ID merely to count/page, nor change the export cap/export contents.

For a report Run use one source read/transaction/time for its rows and input context; capture current canonical parameters once. Existing owner evaluation/freshness, if any, finishes before the read snapshot; do not introduce a broad new evaluation or nested transaction in an adapter. List reads use their own consistent read/captured time and current source authorization. Root reviews any caller-owned snapshot change before dispatch.

## Source contracts by existing report

| Code/form | Shared source contract and retained predicates |
|---|---|
| tasks-due-this-week | Extract current Tasks(c,f): selected report projects, global discipline mapping, selected Monday–Sunday week, open, assignee and mine. Exact dated result cohort naturally has0 open-undated; label it as result-cohort0. |
| overdue-tasks | Same Tasks/open evaluation source plus assignee and actual `DaysOverdue >= max(minDaysOverdue,1)`. The report-owned route retains custom minima even where existing generic listLink is null. |
| blocked-tasks | Current Tasks blocked source plus exact optional blockerType JSONB EXISTS predicate, including legacy missing blocking flags; no post-cap count. |
| tasks-blocking-others | Existing blocking TaskFilter source. Successor/milestone formatting is not the population definition. |
| stale-work | Existing open source, held exclusion and exact c.Now.AddDays(-max(days,setting)) cutoff. Do not replace a custom cutoff with the ordinary stale indicator. |
| review-queue | The existing **task branch** with readyForReview/reviewer/project predicates before Take. Deliverables remain in the report but never count as tasks. Queue merging/order is unchanged. |
| workload-employee | Existing Grid once; use its final authorised returned people IDs and the canonical task query for them with the same project filter. Discipline selects the people through work, then keeps their other visible work; do not re-filter every task to Civil. Unknown/undated context follows that full canonical unplannable task population, not displayed weekly cell sums. |
| workload-discipline | Same authorised PeopleScope.Widen/office population and canonical visible Setup/Active task query before discipline grouping/one-decimal rendering. Current report has officeId, not new supervisor/project/discipline parameters. |
| team-capacity | Existing CapacityGrid once, plus the actual people represented by the report's unit rows over displayed weeks. Without an org-unit filter this is its returned people; with selected historical unit/subtree it is the union of people actually included in at least one displayed unit-week. Count each person's qualifying task once. Do not accidentally count people removed by the post-grid unit filter, or internal Planner lookahead. |
| allocations-by-source, hours-by-source | Existing CapacityGrid returned authorised people. Scope/source/confidence and counted hours are unchanged. Its Source/Confidence select planning-entry measures, not task source categories. Show explicitly labelled scoped-people task context; do not pretend tasks inherit a planning source. |
| allocations-by-source, entries-without-project | Same canonical scoped people task context, explicitly “Tasks of people in this report scope; not tasks linked to these entries.” An entry without project has no fabricated Task relationship. includeMyDrafts/discipline determine the canonical returned cohort as already implemented, not an ad hoc task filter. |
| task-hours | Distinct tasks referenced by the existing **permitted Time.Visible entry cohort**, after effective dates, project, user and mine/team permission checks. Several entries for a task count once. Current task context is separate from recorded-hour rows; see deleted-reference handling below. |

### Minimal owner seams needed

1. **Task query extraction, owned/root-approved Workload seam:** extract a pure internal `WorkloadEndpoints.TaskQuery(HubDb, Access, IQueryable<Guid> people, Guid? disciplineId, Guid? projectId) -> IQueryable<WorkTask>` from existing Tasks. Existing Tasks simply projects that query to its current TaskRow, retaining exact status/live-project/assignee/visibility policy. Report Workload and Planner contexts can query the **actual returned** person IDs without duplicating eligibility predicates. No endpoint, load arithmetic, proposed/confirmed coverage or people-permission change.
2. **Capacity report same-read context, Reports-owned if explicitly loaned CapacityReports.cs:** carry a private context alongside the existing Result (or record it on the request-local Ctx) containing the already-read grid and actual included person IDs. TeamCapacity's unit loop records union membership from its existing historical placements; do not call Planning.ReportGrid a second time for summary/pager, repeat historical unit logic separately or use display labels as IDs. Never expose that person-ID set in the DTO/URL.
3. **Workload discipline source, owner-approved:** expose its existing canonical person/task source or factor report composition to share it, without changing grid/hours/discipline grouping. Office and current people scope remain authoritative.
4. **Time:** initially reuse full permitted Time.Visible rows for distinct reference IDs (it already applies per-entry project/role checks); if a query seam is needed for scale, the owner must extract it from that actual permission path, including the per-entry ViewTimeEntry test. Do not substitute a bare TimeEntries.Where(projectVisible) query.

Returned in-memory person/task reference IDs can be used server-side to constrain an authorised SQL query when the canonical reader already returns those identities; this is not an ID-list link or a persisted scope. Prefer an existing equivalent IQueryable where available. Empty sets are explicit empty.

### Task Hours deleted-reference edge

Current Time.Visible deliberately retains recorded time for soft-deleted tasks, using IgnoreQueryFilters only inside that already permission-checked source. Generic current TaskQueries excludes deleted tasks. Blindly joining only live tasks would call their missing inputs0; broadly ignoring task filters would resurrect metadata/commands through the generic task panel.

Recommended bounded default: count/list current referenced task facts through normal current-task visibility; set Basis=CurrentRecordedTaskContext and separately disclose distinct recorded references whose current task inputs are unavailable/deleted. If any such facts are unavailable, total missing-input counts are null with KnownCount preserved, UnavailableReferences explains the limitation, and metric lists show known qualifying current rows plus that same banner. Recorded-hour table/export remains unchanged and no deleted record is recreated. Only existing permitted time-source references can contribute to that disclosure; no hidden-project/reference lookup or hidden names.

Root may instead authorise a narrowly projected deleted-reference row within this report-owned read, but that needs an explicit read-only DTO/source policy before coding. Until then do not mark Task Hours/FR39 complete by silently omitting references or treating unknown fields as zero. This is the one remaining source edge needing confirmation; it does not block the direct task-report/Workload/Capacity contracts above.

## UI and authorization behavior

- Two plain linked counts beside the report table use server values and basis; partial/known subtotal labels are explicit. Exact result-cohort zero is informative and is not replaced with a broader project count.
- Dialog is report-owned with a heading naming report/metric, full current count/coverage/asOf, labelled pager and the current report parameters. It reuses current table/panel primitives, not a general new list engine. Existing Run's table stays.
- Report eligibility/owner flags are rechecked **before** source/list projection. Shared views and links only supply parameters; they grant no task/report/project/person access. Explicit inaccessible selected filters do not become “no filter/all people.” Existing valid canonical scalar references and normal no-visible-result behavior are preserved.
- Report data/summary settle together; when report parameters change, clear the old open input list/page or explicitly reload against the new parameters. A report-owned metric URL with no valid base required project shows the same report parameter prerequisite, never an all-project fallback. A later permission loss yields ordinary403/404 rather than stale cached rows.
- Cross-project task-row reports retain explicit archived-project selection because the source is Reports. No initial workspace Scope filter silently removes it. Live Workload/Planner context retains its narrower Setup/Active policy.
- No source rows/IDs/user names are serialized into Copy links or saved views. Unknown metric/page/UI keys are not interpreted as report data filters. No task or allocation write is exposed by this feature.

## Bounded dispatch and acceptance evidence

Suggested first implementation boundary is report-owned source/query/DTO/pager for the six task-row reports, followed by explicit owner loans for Workload/Capacity/Time. Do not claim all FR39/S23 until every applicable form has evidence. C can own Reports.cs/new ReportTaskInputs.cs/Reports.tsx/report-local dialog and unique tests; root owns Workload/Time extraction seams or explicitly loans them, shared locales/generated contracts and all execution. CapacityReports.cs requires an explicit ownership loan; no edits under the present hold.

Prepared acceptance plan for root, no tests authored/executed by this note:

1. Exact result cohorts with custom minDays, blockerType including legacy JSON, reviewer and custom stale cutoff; totals and every pager ID match uncapped source. More than2000 and Export.MaxRows+1 source records prove no count from displayed/formatted rows. Duplicate time entries never double count.
2. A task missing estimate and due date appears in both cards; known0 differs from unavailable. Row date predicates produce0 undated as accepted; Workload/Planner context still exposes actual unplannable tasks. Held/terminal/backlog cases follow each canonical source, independently of home_overview/capacity_polish/screen_patterns.
3. Civil employee report4 unestimated/2 open-undated, including another discipline's task for the same canonically selected person; reporter counts/list scope match the employee source rather than a blanket discipline task filter. Empty returned people stays0.
4. Team Capacity unit filter/historical person move across displayed weeks: included union counted once, removed people excluded, lookahead not included. Entries-without-project still labels scoped people context and source/confidence never becomes a fictitious task predicate.
5. Task Hours deleted/unavailable reference banner/nullable total and known rows, permission-checked Time source, duplicate entries and no hidden/deleted metadata resurrection. Existing recorded-hour report/export unchanged.
6. Lena/Sam/members follow the same scalar link under current permissions, restricted sentinels absent, explicit archived row-report source preserved, removed/invalid scope never widens; owner flag/capacity_screens off route and field behavior exact.
7. Browser each count opens its exact metric/parameters, moves pages, opens a real Task panel and returns to same dialog/page/report query/scroll/focus; changing report params cannot retain stale list; Copy/save/export exclude dialog keys. Current count/asOf can change after an intentional intervening state update; the refreshed list states that current read rather than claiming old snapshot equality.

Root's checkpoint17 S23-A script PASS is acknowledged as reported, including radio/history action settlement and pending-listener cleanup. That evidence does not satisfy any of these S23-B cases.
