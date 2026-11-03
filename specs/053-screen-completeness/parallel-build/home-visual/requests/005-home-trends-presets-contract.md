# S08 contract request — Home trends and role presets

Generation8: precise shared-root requests before Home source implementation. Deadline notes FR13 are independent and proceed first. No shared schema/core mutation by this lane.

## Current-source facts

- `ProjectHealthSnapshot` has Id, ProjectId, SnapshotDate, health strings, Inputs and Counts JSON. Base `Entity` has **only Id**, no capture timestamp. Old Counts has evaluator-specific keys (e.g. tasksTotal/tasksComplete/tasksOverdue) and a fixed14-day deliverable count. It cannot prove Home's due-window or filtered definitions.
- The existing evaluation worker writes/overwrites Active-project daily rows around EvaluationService.cs377. Do not rewrite/interpret old Counts as Home counts, and do not silently add snapshot rows for other lifecycle states if that changes existing reports. Missing comparable rows are excluded/disclosed under FR10.
- UserSetting is the existing person preference record; no Home preset exists. `UserPreferences.GetOrCreate` is the current conflict-safe creator.
- TaskFilter already accepts **project-discipline IDs** through `disciplineId` and multiple owners through `assigneeId`. WorkspaceTasks.tsx currently drops disciplineId from its URL allowlist, so the Work owner must preserve/names these links before Home can claim exact preset drill-downs. No new canonical task rule is needed.

## Proposed minimal additive shared schema

1. Nullable jsonb `ProjectHealthSnapshot.HomeMetrics` (or root's equivalently named nullable capture field), no historical backfill. Version1 object:

```text
version: 1
capturedAt: DateTimeOffset (actual server capture instant)
orgTimeZone: string
dueWindowDays: int
projectStatus: canonical ProjectStatus
computedHealth: canonical computed health at capture
openTasks: int
overdueTasks: int
dueTasks: int
dueDeliverables: int
dueMilestones: int
accessBasis: string (internal digest, never returned raw)
```

All counts are exact nonnegative whole-project figures by the **same** open/overdue/task/deliverable/milestone definitions as Workspace.Home. Capture only under home_overview in the existing daily snapshot job, with a consistent authorized project read/transaction and same-day overwrite as current snapshot policy. Existing Counts/health/report behaviour remains. Version/basis missing or invalid means unavailable, never zero. `capturedAt` must be explicit; SnapshotDate is not an instant.

2. A `trend_compare_days` integer setting default7, range1–366 (unless root identifies an existing approved range), exposed only under home_overview, with `OrgSettings.TrendCompareDays` and standard settings serialization/validation.

3. Nullable `UserSetting.HomePreset` text, allowed saved codes ProjectManager/DisciplineLead/Supervisor/null. Only this person's explicit choice writes it, under home_overview; no default preset, no preference write during GET. Hidden while off, preserved across rollback. Workspace-owned dedicated own-person Home preference endpoints can use GetOrCreate; no need to widen Notifications' existing response.

## Safe capture/read basis

For this bounded version, support comparison only for whole-project current cohorts (including a ProjectManager preset, which only narrows project IDs). **DisciplineLead and Supervisor filtered trend chips return “Trend unavailable for this scope”.** This is the explicit FR10/AC10 fallback, not a claim of stored historic per-person/disc facts.

For unfiltered comparison, use only currently permitted selected project IDs with valid version/window/time-zone captures at or before today−N and matching current permission basis. Exclude each project from both current and historical sides if it is missing/incompatible/permission-changed; inspection discloses current/history values, included current-visible project numbers, actual per-project capture instants and excluded count. No hidden historical IDs/names appear.

`accessBasis` should conservatively invalidate capture after relevant project visibility/manager/membership changes, restricted-project policy changes, or a reader/system-role visibility change. A safe minimal digest can combine project ACL (visibility, PM, active member user/role set) with a stable global active-user/system-role visibility version/hash. Never hash LastSignIn/UpdatedAt or ordinary task activity: those do not define access. The global role hash may conservatively invalidate additional projects; unavailable is preferable to unproven historic visibility. Please own/review the exact root adapter rather than let the Home lane duplicate permission rules. Lifecycle status is a metric/filter basis, not itself an ACL hash: only include a project in a status-filtered common cohort when both sides satisfy that selected status contract.

If selected projects' latest valid captures have different instants, return a disclosed time range and per-project times; the title block can show “Trends compare with” at the earliest instant plus a second “Latest comparison” row when needed. Never label an older capture “last week” or pretend all projects share one time. Team Members has no trend.

Please reply with root adapter/capture field names before this lane compiles S08 against them. Root owns the additive migration, model/metadata/core capture and tests.

## Preset scope contract for Workspace-owned implementation

- Offer ProjectManager only for an actual system/project PM role; its projects are those the viewer manages, permission intersected.
- Offer DisciplineLead for actual current project-discipline leadership; projects are led projects, task/deliverable/deadline figures are limited to the exact led project-discipline IDs. Unscoped milestones do not acquire a made-up discipline.
- Offer Supervisor only for the held Supervisor role; all permitted projects, task/deliverable/deadline work limited to the viewer's direct people scope, or §54 scope when enabled. Milestones have no owner field and must be excluded/disclosed rather than attributed to an invented person.
- Preset is a visible scope/filter choice, not a new count definition. Persist the chosen code; resolve current permissions/roles afresh on a later visit. Explicit URL filters win; no preset applies until chosen. User loses role => no automatic use of that preset.
- Keep all resulting scope/filter values in URL chips and task/deadline drill-downs. For normal-sized cohorts, existing disciplineId/assigneeId lists reproduce the task counts; large URL cohorts need a reviewed bounded solution, not truncated IDs or a count that opens a different set. Please coordinate Work's URL handling/metadata if needed.

Acceptance focus: historical3 overdue/current1 -> green↓2, unchanged0 neutral, no Team trend; compatible common cohort only; old/malformed/version/window/access mismatch unavailable/excluded; filtered DL/Supervisor never whole-project comparison; Priya PM+DL sees exactly those two presets, explicit choice stored/restored; exact task/deadline links, flag-off shapes/behaviour preserved.
