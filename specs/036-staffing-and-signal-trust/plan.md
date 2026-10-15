# Implementation Plan: Staffing Signals and Trustworthy Counts

**Packet**: 036 | **Spec**: [spec.md](spec.md) | **Date**: 2026-10-11 | **Base**: `claude/tuesday-visual-batch1` at `bbac1f6d`

## Summary

Seven small changes, each reusing a mechanism that already exists:

- Open roles are a new table next to allocations and are filled through the existing allocation proposal.
- Dependency types use the `TaskDependency.DependencyType` column, which exists but is never read, plus one shared satisfaction function.
- The stalled-status rule uses the existing `StatusChangedAt`.
- Grouping is done on the client, over rows that are already loaded.
- The picker reads the Planner's existing grid model.
- The count and timeline fixes are one-line predicate and ordering corrections plus labels.

The packet adds no new service, job, cache or materialised table.

## Constitution Check

| Principle | Result |
|---|---|
| I. Coordination First | PASS. Open roles show unfilled demand. There is no auto-assignment and no levelling. |
| II. Deterministic and Explainable | PASS. Dependency satisfaction and A-21 are pure functions in `Hub.Domain`. The threshold is a setting. Tile definitions explain each count. |
| III. One Accountable Owner | PASS. An open role's creator owns it. The filling allocation keeps its §37.6 ownership. |
| IV. Specification Is the Source of Truth | Pending until T002 writes §40 and `build_spec`/`trace_spec` pass. |
| V. Traceable by Construction | PASS. New fields are added to `AuditRules.Fields`. Open roles are soft-deleted and carry row versions. |
| VI. Secure by Default | PASS. Open roles use the project visibility predicate. The picker reuses `Planning.People` scope, so it returns no data for people outside scope. |
| VII. Simple Enough for a Small Team | PASS. One new table, one migration, one new feature file. |

## Branch and file coordination (no merge conflicts)

Checked on 2026-10-11 against every local branch with commits not in the base:

| Branch | Files it shares with this plan | Required action before 036 starts |
|---|---|---|
| `codex/tuesday-ui-consistency` (2026-10-10, 78 files; contains `codex/tuesday-ui-inherited-planning`) | `Entities.cs`, `HubDb.cs`, `Allocations.cs`, `Planning.cs`, `Workload.cs`, `people.tsx`, `en.ts` | Merge it into the base, or record that it is abandoned. **This is the main conflict risk.** |
| Uncommitted edit in this worktree | `src/Hub.Api/Features/Planning.cs` (+27/−13) | Commit it or set it aside with a WIP commit before T003. |
| `codex/pm-*` branches (2026-10-01 to 10-03) | `Readiness.cs`, `Settings.cs`, `HubDb.cs`, `EvaluationService.cs`, `Tasks.cs`, `TaskPanel.tsx`, `en.ts` | Confirm they are superseded (the base is newer) or merge them. Do not edit those files while one is still pending. |

Rules while implementing:

- **One migration for the packet** (`<timestamp>_StaffingSignals`), generated last in Phase 2 after rebasing on the final base. `HubDbModelSnapshot.cs` is the usual conflict point, and generating once keeps it to one edit.
- **One writer per stream.** Streams own disjoint files, except `en.ts`, where each stream appends keys only inside its own named block.
  - **Stream A (backend):** Hub.Domain, Data, Features and tests.
  - **Stream B (Part A web):** `Allocations.tsx`, `Workload.tsx`, `Planner.tsx`, `people.tsx`, `TaskPanel.tsx`, `Deliverables.tsx` (dependency section), `Admin.tsx`.
  - **Stream C (Part B):** `Workspace.cs`, `Portfolio.tsx`, `Dashboard.tsx`, `Home.tsx`, `common.tsx`, `Timeline.tsx`, `Gantt.tsx`, `Timeline.cs`.
- Stream C can run in parallel with A. It also owns the arrow-by-type change in `Timeline.tsx` and `Gantt.tsx`, and makes it after A exposes `type` on the timeline payload. Stream B starts after A's API tasks pass.
- No change to existing migrations, packet 029 commands, or packet 034's planning-entry code paths.
- **Layout rules.** Streams B and C start after packet 037 Phase 1 (toolbar, navigation, header) and follow its rules R1–R10:
  - group-by goes in the toolbar's Group menu;
  - tile definitions are `(i)` tooltips, not visible text;
  - the timeline legend sits behind a Legend button;
  - picker capacity is a short right-aligned figure;
  - dependency type is a two-letter chip.

## Design

### A1. Open roles

- **Entity:** `OpenRole : CoordinationRecord`, in the new file `src/Hub.Api/Data/OpenRoleEntities.cs`:
  - `RoleLabel` (1–80)
  - `ProjectDisciplineId?`
  - `FromDate`, `ThroughDate`
  - `PlannedHours` (>0, same rules as allocations)
  - `Status` (Open | Filled | Cancelled)
  - `FilledAllocationId?`
  - `Note?`
  - soft delete and row version
- **Endpoints**, in the new file `src/Hub.Api/Features/OpenRoles.cs`, mapped from `Modules.cs`:
  - `GET /projects/{id}/open-roles`
  - `POST /projects/{id}/open-roles`
  - `PATCH /open-roles/{id}`
  - `POST /open-roles/{id}/cancel`

  Permission is `ProposeAllocation` (D2). Reads use the existing project visibility scope.
- **Fill:**
  - Add an optional `Guid? OpenRoleId` to `CreateBody` in `src/Hub.Api/Features/Allocations.cs:14`.
  - The create handler locks the open role row (the same lock pattern used for the person row), then checks that the role is Open, on the same project, and that the date range is inside the role's range. It then sets `Status = Filled` and `FilledAllocationId` in the same `SaveChanges`.
  - Decline and cancel (`Allocations.cs` :443, :459) reset a linked role to Open.

  These are the only edits to `Allocations.cs`.
- **Workload:**
  - Add an `unfilledRoles` array to the `/workload` response: role, project, and weekly hours for the same 8 weeks.
  - Weekly hours come from the existing working-day spread in `AllocationRules` (`src/Hub.Domain/Allocations.cs:36-74`), using the project office calendar.
  - These hours never enter person rows.

### A2. Dependency types

- **Vocabulary:** `DependencyTypes { FinishToStart, StartToStart, FinishToFinish }` in `src/Hub.Domain/Vocabulary.cs`.
- **Schema:**
  - Add a check constraint on `task_dependency.dependency_type`.
  - Add `DependencyType` (default FinishToStart) to `DeliverableDependency` (`Entities.cs:491`).
  - Add `StartedAt` (nullable) to `WorkTask` (`Entities.cs:444` block) and to `Deliverable` (`:299` block).
- **Setting `StartedAt`:** on the first move from Not Started to a working status (not On Hold or Cancelled), when `StartedAt` is null. Holding or cancelling unstarted work is not a start (defect 036-D2).
  - In `Tasks.cs`, in the status transition (:437).
  - In `Deliverables.cs`, in the status transition (:334) and when issuing straight from Not Started (:375).
- **Backfill (in the migration), by the same rule:**
  - `StartedAt` = the first activity-log entry whose `Changes` contains a `Status` change from `Not Started` to a status other than On Hold or Cancelled.
  - Fallback for items already started with no such entry: `StatusChangedAt`, else `CreatedAt`. Items held or cancelled straight from Not Started get no fallback and stay null.
  - Items still Not Started stay null.
- **Domain (`Evaluation.cs`):**
  - `DependencySnap` gains `string Type = FinishToStart`.
  - `TaskSnap` and `DeliverableSnap` gain `StartedOn` (the local date of `StartedAt`).
  - Add one public pure function, `DependencyRules.Satisfied(type, predStatus, predStartedOn, predCompletedOn, lag, after, today)`, which replaces the local `Satisfied` at :84-90 and the deliverable check at :254:
    - Finish-to-Start: unchanged.
    - Start-to-Start: started (or Complete/Cancelled) and `after(StartedOn, lag) <= today`.
    - Finish-to-Finish: always satisfied for start purposes.
  - D-12 at :149-156 and :262-264 branches on type:
    - Finish-to-Start: as today.
    - Start-to-Start: successor start vs predecessor start + lag.
    - Finish-to-Finish: successor due vs predecessor due + lag, plus the "finished before predecessor" note for a Complete task, or an Issued or Accepted deliverable, whose predecessor is not finished or cancelled. The note replaces the date flag.
  - D-11 and D-13 walks are unchanged, since they follow edges regardless of type.
- **Loading:** `EvaluationService.cs:43` and `:52` map `Type`. `:34-42` maps `StartedOn` through `L()`.
- **API (`Dependencies.cs`):**
  - `AddBody` and `DeliverableBody` gain an optional `Type`, validated against the vocabulary with error key `dep.type`. The duplicate rule stays one edge per (predecessor, successor).
  - `Edges()` at :166 calls `DependencyRules.Satisfied`.
  - The deliverable dependency payload (`Deliverables.cs:208-230`) returns `type` and `satisfied`.
  - Copy-structure (`Extras.cs:88-97`) copies `Type`.
- **Readiness (`Readiness.cs:77-99`):** the predecessor check calls `DependencyRules.Satisfied`. Finish-to-Finish edges are skipped for start readiness. This is the only edit to `Readiness.cs`.
- **Timeline payload:** `Timeline.cs:64-71` adds `type`.
- **Audit:** add `DependencyType` and `StartedAt` to `Audit.cs:80-87`.

### A3. A-21 "Status not moving"

- `TaskSnap` and `DeliverableSnap` gain `StatusChangedOn`, mapped in `EvaluationService.cs`.
- In `Attention()`, A-21 fires when the status is unchanged for more than `status_stale_days` AND the item had activity within `task_stale_days` (assumption A5, run 2):
  - Tasks: In Progress or a review status, `Since(StatusChangedOn) > StatusStaleDays` and `Since(LastActivity) <= TaskStaleDays`, and the A-11 review-stalled condition does not hold. It does not depend on whether A-10 or A-11 is switched on. Severity Info, or Warning when due soon, mirroring A-10.
  - Deliverables: In Progress or In Review with the same two comparisons (deliverables have no review-stalled condition).
- Add a `RuleNames` entry. Change `Rules.Ids` to `Range(1, 21)` (`Settings.cs:229`).
- Add the `status_stale_days` setting (int, default 15, range 1–365, group `attention`). It is seeded automatically, so no admin UI code is needed, only i18n keys.

### A4. Home discipline and grouping

- Add `AppUser.HomeDisciplineId?` with an FK to `Discipline`.
- Admin `PATCH /users/{id}` (`Admin.cs:144`) gains `homeDisciplineId`. Add the field to the audit list and to the Admin user editor (`Admin.tsx`).
- `/users`, the Workload person rows and the planning grid person rows return `officeId`, `jobTitle` and `homeDisciplineId` where they are missing (`Me.cs:67-76`, `Workload.cs` row projection, `Planning.cs` grid rows).
- `Workload.tsx` already groups by supervisor (:66-67). Generalise that to a `groupBy` URL parameter (none | supervisor | office | jobTitle | homeDiscipline) with a "Not set" bucket, and do the same in `Planner.tsx`. Group subtotals are summed on the client from row values. There is no server group-by.

### A5. Capacity in the picker

- `Planning.cs` `GridQuery` (:22-25) gains `PersonIds` (comma list, at most 50). It is applied inside the existing scope, so ids outside `Planning.People` are dropped silently.
- `PeoplePicker` (`web/src/components/hub/people.tsx:31-76`) gains an optional `capacityWeek` prop. When the prop is set and the viewer has `ViewWorkload`:
  - On open, it calls `planning/grid?personIds=…&from=<week>&weeks=1` once for the visible candidates.
  - It shows `remaining` as "N h free", plus "N h away" when the cell reports time away. It uses the hours the grid already returns, with no new day-count field.
  - Without data, it shows nothing.
- Pass `capacityWeek`:
  - in TaskPanel owner (`TaskPanel.tsx:127`, the week of the start date, else the due date, else the current week);
  - in the task create form (`Tasks.tsx:726`);
  - in ReassignWork (`ReassignWork.tsx:61`, the current week).

### B1. Count reconciliation

| Fix | File:line | Change |
|---|---|---|
| Portfolio health tiles | `web/src/pages/Portfolio.tsx:86-88` | Link `computedHealth=X&status=Active`. |
| Discipline Open link | `TaskQueries.cs:95` and `Dashboard.tsx:91` | Add an `onHold=false` parameter to `TaskQueries.Apply`, and use it in the link. |
| Deliverable unassigned filter | `Deliverables.cs:71` | Add `&& !terminal`. |
| Upcoming Deadlines truncation | `Workspace.cs:214, 218, 223` | `OrderBy(due)` before `Take`. |
| On Hold missing from Home lanes | `Home.tsx:100-116`, `Workspace.cs:171-175` | Return an `onHold` count and render it with a `status=OnHold` link. |
| Tile definitions | `common.tsx:185-198` | Add an optional `hint` to `SummaryTile`, shown as a tooltip and `aria-describedby`. Add one `def.*` string per tile, copying the "How this is calculated" pattern from `Workload.tsx:425-436`. |
| Discipline-scoped dashboard | `Dashboard.tsx:208-210` | Show a "Whole project" note on the decision, issue and risk tiles when a discipline is selected. |
| Home Review lane label | `en.ts` | "Review and revision" (D9). |

### B2. Timeline date source

- **Legend** (`Timeline.tsx:436-449`, shared by `Gantt.tsx`): add "Bar: start → due" and "Start not set".
- **`spanOf`** (`Gantt.tsx:66-71`, `Timeline.tsx:93`): return `assumedStart` when `startDate` is null. Draw that segment at reduced opacity, and append "start not set" to the tooltip and aria text (`en.ts:1616`).
- **Silent drop** (`Timeline.tsx:99`): render a dated task with an undated or out-of-range deliverable as a top-level row, matching `Gantt.tsx:88`.
- **Undated milestones** (`Timeline.cs:78`): return them, and list them under Unscheduled on both pages.
- **Creation date** (`Timeline.tsx:94`): use `createdOn` from the payload.

## Data model changes (single migration `StaffingSignals`)

| Table | Change |
|---|---|
| `open_role` (new) | Columns as in A1. Index on (project_id, status) filtered on `deleted_at IS NULL`. Checks: dates ordered, hours > 0, status in the vocabulary, label length. |
| `resource_allocation` | None. The link lives on `open_role.filled_allocation_id` (FK, nullable, unique when not null). |
| `task_dependency` | Check constraint on `dependency_type`. |
| `deliverable_dependency` | `dependency_type text not null default 'FinishToStart'` plus a check constraint. |
| `work_task`, `deliverable` | `started_at timestamptz null`, plus the backfill SQL. |
| `app_user` | `home_discipline_id uuid null`, FK to `discipline`. |

## Tests

| Test file | Cases |
|---|---|
| `tests/Hub.Tests/Domain/DependencyLagTests.cs` | Start-to-Start waiting → satisfied with lag; Start-to-Start with the predecessor Cancelled; Finish-to-Finish never waits; Finish-to-Finish date inconsistency; successor finished before predecessor; Start-to-Start date check with missing start; working-day lag over a holiday. |
| `tests/Hub.Tests/Domain/EvaluatorTests.cs` | AC-STALE-01, -02, -03; A-21 for a deliverable; the rule toggle. |
| `tests/Hub.Tests/Api/OpenRoleApiTests.cs` (new) | AC-ROLE-01 to -05, including the one-transaction fill, refusal when already filled, return to Open on decline, and privacy. |
| `tests/Hub.Tests/Api/EvaluationTests.cs` | Adding a link with a type; rejecting an invalid type; deliverable payload `satisfied`; Readiness skips Finish-to-Finish. |
| `tests/Hub.Tests/Api/PlanningApiTests.cs` | `personIds` filter; out-of-scope ids return nothing (AC-PICK-02); figures equal the cell endpoint (AC-PICK-03). |
| `tests/Hub.Tests/Api/WorkloadApiTests.cs` | `unfilledRoles` weekly hours; person totals unchanged (AC-ROLE-02). |
| Home, dashboard and portfolio API tests (existing files) | Upcoming Deadlines ordering with more than 101 items of one type; deliverable unassigned filter; `onHold=false`; the `onHold` count. |
| Migration test | Backfill sets `StartedAt` from the activity log, falls back to `StatusChangedAt`, and leaves Not Started items null. |

Commands:

- `dotnet build src/Hub.Api/Hub.Api.csproj --no-restore`
- `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore`
- `npm --prefix web run build`
- `npm --prefix web run lint`
- `python3 tools/build_spec.py --check`
- `python3 tools/trace_spec.py --check`
- `python3 tools/coverage_gate.py` (`Evaluation.cs` branch coverage must not drop)
