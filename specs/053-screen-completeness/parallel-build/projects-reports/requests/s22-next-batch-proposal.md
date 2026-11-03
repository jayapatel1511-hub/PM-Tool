# Next bounded batch proposal — 053 S22 and Dashboard-only 052 S06

READ-ONLY proposal prepared during ALL SOURCE FROZEN generation 10. Nothing below is implemented, verified or accepted by this note. No application/test source edits, execution, generators, new goals or additional helpers/agents occurred. Root must explicitly open ownership before implementation.

## Outcome and boundary

Implement FR/AC-SCR-37 and FR/AC-SCR-38, plus only this Dashboard's FR-VSN-15 CountCard presentation. Preserve independent project_screens/visual_system/screen_patterns parts, legacy off bytes/rendering, existing counts and canonical permissions, and the already-reviewed task-name reflow repair. No additional chart, staffing command, schema, setting, migration, health input, formula or release operation.

Lane-owned maintained files: `src/Hub.Api/Features/Dashboard.cs`, `web/src/pages/projects/Dashboard.tsx`, new uniquely named `tests/Hub.Tests/Api/ProjectDashboardCompletenessTests.cs` and `web/tests/project-dashboard-completeness.cjs`. A small owned Dashboard stylesheet is needed only if existing Section/CountCard styles cannot express the layout. Do not edit Projects/Portfolio, S19/20/21 tests, Weekly Coordination, allocation commands, shared registry or theme.

Root integration is required for two bounded shared read/presentation extensions below. These are dependencies, not permission to write Capacity/Work/shared files from this lane. Locales, optional API contracts/registry/golden generation and the existing real-API counts suite remain root-owned.

## Observed source and reuse boundaries

- Dashboard's existing `Scope.Tasks`, `Scope.Deliverables`, `Count` and `DashboardCount.Link` already produce exact filtered list destinations (`Dashboard.cs:96..110, 136..191`). Decisions/registers remain explicitly project-wide under discipline selection. Keep those queries rather than recalculate counts in JavaScript.
- `ProjectListCompleteness.Enrich` is the reviewed person-count/over-capacity adapter, with an optional staffing-only call for Portfolio. It does **not** expose discipline reservation/remaining/open-role hours or arbitrary exact-date windows. Its lookahead and person counts cannot substitute for FR37.
- Capacity's reviewed `AllocationScreenReads.Compare` reuses `AllocationRules.Spread`, person calendars and explicit day overrides, unambiguous current valid work/role attribution, `Workload.Remaining`, and explicit Unattributed groups. However, its public projection buckets by whole due week, and its read endpoint is gated by capacity_screens. Summing those weeks would include days/tasks outside a submission window and would incorrectly make this project_screens feature depend on another part.
- `AllocationEndpoints.VisibleQuery` and the managed-project/PM basis at `Allocations.cs:342..355` provide the established allocation visibility/complete-versus-visible-only boundary. Do not widen it to project read access alone or infer hidden allocation counts.
- `OpenRoleEndpoints.Calendars/Spread` uses the project's office calendar and canonical allocation spread. Its catch returns an empty dictionary when a calendar removes all eligible days (`OpenRoles.cs:28..40`); FR37 must treat a positive role with an empty **full-range** spread as unavailable, not zero. A zero sum inside a clipped window can still be valid if its full-range spread is usable.
- The project attention GET calls `EvaluationEndpoints.Items` without a limit. It already returns the whole project/discipline/severity/snooze-filtered ranked list, plus Total/Snoozed/Truncated (`Evaluation.cs:33..58, 118..143`). There is no need to invent a second ranking or a capped-list kind count. `AttentionPanel` is also used by Weekly Coordination; any grouping extension must default off and be enabled only by this Dashboard.

## FR37 read contract and exact date semantics

Add only optional, null-omitted `DashboardView.Staffing` while project_screens is on. Capture a server read instant once; derive Today in org_time_zone and evaluate/read under the existing freshness/authorization boundary followed by a consistent read. Retain optional AsOf for visual_system and provide the staffing read's AsOf independently for project_screens. Do not begin a repeatable read before EnsureFresh's write boundary.

Proposed shape:

```text
Staffing {
  fromDate, throughDate?, targetKind?, targetId?, targetName?, state, asOf,
  allocationCoverage, unattributedConfirmedHours?, knownUnattributedHours,
  disciplines: [{ projectDisciplineId, name,
    remainingHours?, knownRemainingHours, unestimatedTasks, undatedTasks,
    confirmedHours?, knownConfirmedHours, unavailableConfirmed,
    openRoleHours?, knownOpenRoleHours, unavailableOpenRoles,
    excessHours?, comparisonState }]
}
```

`?` is meaningful unknown, never an omitted zero. Final DTO names are implementation choices; fields must retain these distinctions. Hidden allocation cardinalities or person/project metadata are not returned. Unattributed figures describe only visible records.

Window default: select the earliest current same-project open SubmissionPackage target, with deterministic TargetDate/key/ID ties; Draft/Checking/Ready are open, Issued/Superseded/Cancelled are excluded, and deleted/foreign references never supply metadata. The existing Project header's nextSubmission is a **milestone**, so it cannot stand in for a package. When no usable open package exists, use the existing next non-complete/non-cancelled dated milestone selector, with deterministic ties. Endpoints are inclusive. An overdue selected target yields an explicit overdue/no-forward-window state; no clamping through Today, silent switch to a later package, fabricated date, zero figures or inverted spread. No usable target yields unavailable staffing, not three zeros.

Root must settle one independent-flag detail before opening implementation: Submissions' read guard is screen_patterns. Recommended dark default is to use a clearly labelled next-milestone fallback when package reading is unavailable under that flag, without exposing hidden stored package metadata or claiming that no package exists. If the amendment instead requires a narrow package-date read independent of that guard, root should approve that exact read boundary. This is a proposed local default, not a decision-status change.

Per-discipline arithmetic:

1. Remaining: current nonterminal tasks due within `[today, target]`, using `TaskQueries` open/date/discipline scope and `Workload.Remaining(estimate, progress)`. On Hold remains open, as root already corrected for S30; Complete/Cancelled do not. Do not substitute the capacity forecast, which excludes held work, or spread a due-in-window task's estimate across weeks. Missing estimates remain unknown with a known subtotal. Undated open tasks are separately disclosed; do not imply a complete staffing assessment from the dated subset.
2. Confirmed allocated: Confirmed resource reservations only, using their full canonical person-calendar/day-override spread, clipped to these exact dates **before** aggregation. This is reservation hours, not FR-CAP-04 committed load (`max(reservation, linked forecast) + unlinked forecast`), Planner planning entries, available hours or Proposed scenarios. Preserve the reviewed current-work/purpose/person/role attribution rule. Mixed/missing/foreign/ineligible links remain Unattributed; never duplicate or arbitrarily split a reservation between disciplines. Unattributed reservations prevent an asserted complete discipline total/comparison, while known attributed and unattributed subtotals remain useful.
3. Open roles: Open status only; full canonical project-office spread, then exact-date clip and group by the role's valid project discipline. Unspecified discipline has its own labelled group. Filled/Cancelled roles are excluded. These hours never enter a person's load, the confirmed total, remaining hours, or the excess calculation.
4. Excess: only an exact fully covered comparison may state `remaining − confirmed` when positive; 120 and 80 yield 40 h, regardless of the independent 24 h role figure. Unknown/visible-only/unattributed/unestimated inputs must not produce a definitive zero/no-gap assertion. Display rounding follows decimal aggregation; no client subtraction.

Root/shared adapter request: expose a narrow exact-date read from the existing allocation comparison's canonical daily spread/attribution implementation (or factor it once under Capacity ownership), accepting the authorized allocation query and coverage as arguments. Keep S30's weekly contract/defaults and calculations unchanged. Do not call the capacity_screens HTTP endpoint from Dashboard, round a partial week to a full week, copy its eligibility/calendar rules into Dashboard, or use ProjectListCompleteness's people counts as hours. Role spread can be reused directly with the explicit empty-full-spread availability guard; no OpenRoles command change is required.

## FR38 grouped attention

Request a root-owned optional grouping presentation in `components/hub/attention.tsx`, default false. Enable it only from Dashboard when project_screens is true. Reuse its single project attention request, `AttentionRows`, SnoozeDialog, Why, current record context and source-return capture. Weekly Coordination and all other consumers keep their existing presentation.

Group by ItemType, count **attention rule firings**, not distinct record IDs. A task with two current rule firings contributes two, as the ranked endpoint does. Counts use the full same filtered response; snoozed items follow the current includeSnoozed toggle, and the selected discipline/severity never disappears. Preserve the server's rank within each kind. Show five chosen-kind rows, then `Show all (n)` for that kind, and Show fewer after expansion. Changing kind resets expansion. If the chosen kind vanishes on refresh/snooze, select the next available stable kind or show an honest empty state.

Keep chosen kind and expansion in Dashboard-only URL state (`attentionKind`, `attentionAll`) so a record open/close retains source view and scroll. These are display choices, not extra Dashboard count or attention API filters. Root may expose controlled optional props to the shared component rather than duplicate its fetch/context/snooze logic. Unknown/unsupported source kinds retain the existing unavailable context and cannot borrow metadata from another target. If Truncated ever becomes true, label partial kind counts and avoid a false complete Show all claim; the present project endpoint normally has no limit.

## Dashboard-only CountCard migration

Recommended concrete six-primary-card set uses existing DashboardCount members, with no new count formula:

| Label | Existing value | Metric tone | Whole-card destination |
|---|---|---|---|
| In progress | Tasks.InProgress | violet | `tasks?{Tasks.InProgress.Link}` |
| Ready for review | Tasks.Review | amber | `tasks?{Tasks.Review.Link}` |
| Overdue tasks | Tasks.Overdue | red | `tasks?{Tasks.Overdue.Link}` |
| Deliverables due within 14 days | Deliverables.Upcoming | amber | `deliverables?{Deliverables.Upcoming.Link}` |
| Pending decisions | Decisions.Pending | violet | `decisions?{Decisions.Pending.Link}` |
| Open issues | Registers.IssuesOpen | amber | `issues?{Registers.IssuesOpen.Link}` |

Use existing CountCard, one anchor each, keyboard/hover definition, metric icon, numeric value and where line. Task/deliverable where lines name the current project plus selected discipline when scoped; decisions/issues explicitly say whole project. Do not label Task.Total as Open tasks or All deliverables as Due soon. Red zero stays neutral; nonzero red retains square Needs attention. Use existing six/three/two layout and band overlap only in the enabled visual presentation.

Retain every remaining current count (all tasks, Complete, Blocked, Waiting, Unassigned, optional Backlog, all deliverables, At risk, Issued, Overdue decisions, High issues, High risks) as compact separate labelled links beneath the primary cards. Do not nest those links inside a CountCard anchor, silently drop them, manufacture a cross-kind summed register metric, or add a chart. The current four SummaryTile groups remain exactly the visual_system-off branch. Six primary choices are the recommended bounded presentation for root review; no new scope/requirement is inferred from the layout.

## Prepared verification plan — nothing executed

API numeric checks after root's appropriate freeze:

- Exact AC37: Civil 120 remaining / 80 confirmed / 24 open role / 40 excess; durable allocation, task, role, health and audit state unchanged by the read; no 224 h combined figure. A repeat with role 48 still has excess40.
- A partial first/last week clips daily reservations and roles before summing; explicit day overrides and holidays reconcile to canonical spread totals. Third-party planning entries and Proposed allocations do not enter confirmed80. Decimal 0.1/0.2 examples aggregate before display rounding.
- Held task adds its remaining estimate; Complete/Cancelled do not. Missing estimate, missing due date, no target, overdue target and calendar-lost-full-spread each retain unknown/coverage, rather than zero or a false no-gap result.
- Current versus superseded review/filled-role attribution, mixed links and Unattributed reservations follow S30's existing regression matrix. A restricted project/foreign discipline/source supplies no ID/name leak. Non-PM visible-only figures do not claim whole-project totals; ordinary project permission/404 outcomes remain.
- Package target wins over a later/earlier milestone according to the approved selection policy; fallback, terminal/deleted package, deterministic tie, independent flag rollback and organization-midnight clock cases have exact dates. Feature-off response remains unchanged.

Browser checks after root generation/build:

- 12 attention rows in four kinds, distribution 6/2/2/2: four chips with exact counts, selected first five, Show all6 gives six, changing to a two-row kind collapses expansion, and total kinds still sum12. Duplicate rule firings on one item are retained. Discipline/severity/snooze combinations use the same cohort. A source-panel roundtrip retains kind/expansion/scroll; context unavailable remains protected.
- All six CountCard figures follow actual positive destinations, selected discipline where appropriate, and explicit whole-project decisions/register counts. Include nonzero overdue-decision/high-issue secondary links, not only zero fixtures. Existing counts links remain and none is nested in another anchor. Root's real-API `web/tests/counts.cjs` remains the numerical destination gate; update only selectors if the enabled layout changes extraction.
- Three staffing measures and exact40 text visible independently, no combined total or false availability; keyboard/375px/800px-DPR2/text-spacing checks preserve the reviewed task-name wrapping and accessible scroll regions. Both independent flags are exercised separately and together.

Likely focused regression set: new Dashboard API/browser cases, existing AttentionContextTests, ProjectListCompletenessTests, CapacityAllocationScreenTests (only if root factors its shared daily reader), existing counts.cjs, attention-context.cjs, and project-dashboard-reflow.cjs. Root chooses the smallest appropriate set and owns all execution, generated DTOs, independent review and packet evidence. No slice is closed by this proposal.
