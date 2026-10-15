# Plan: Task and Timeline Interaction

Implements §50 with defaults D1 to D16. Base: `main` after PR #37 merges and after §43's flag slice. Each slice in [tasks.md](tasks.md) is one PR, merged dark behind its flag.

## Approach

Reuse first. Every part is a thin addition over code that already exists:

- Quick actions call the existing status path (`useTaskActions.move`), the manual-block dialog and `InlineText`. No new endpoint.
- The picker adds one optional parameter to the existing people read and one React context beside `CapacityWeek`, so the call sites stay as they are.
- Date context extracts the D-12 bounds that `Evaluator.Evaluate` already computes inline (`LinkDates`) into `DependencyRules`, so the evaluator and the hint share one function. Availability reuses `PersonCapacity.Load` and the planner scope check that FR-PICK-01 uses.
- Slip display, baseline and the latest-date note are pure functions in `Hub.Domain`, sent with the existing reads. The evaluator's snapshots keep `original` for slip and the Slipped indicator, and also receive `baseline`, from which the added "since re-baseline" figure is computed.
- The chain highlight reads the stored D-13 result (`TaskState.AffectedMilestoneIds`); no new graph code.
- The bundle uses the existing saved-view create, `DecisionEndpoints.NewLink` for the Related link, and the URL for every new view state.

## File boundaries

| Area | Files |
|---|---|
| Domain | `src/Hub.Domain/Evaluation.cs` (`DependencyRules` bounds, date context, slip shown, latest planned date), `src/Hub.Domain/Permissions.cs` (`Rebaseline`, PM only), `src/Hub.Domain/Settings.cs` (`slip_display_days`, `picker_recent_days`, `activity_burst_minutes`), `Flags.cs` from §43 (six names) |
| API | `src/Hub.Api/Features/Me.cs` (people read), `Dependencies.cs` (date-context read), `Milestones.cs` and `Deliverables.cs` (re-baseline, baseline fields, `fromTaskId`, related links), `Timeline.cs` (slip shown, baselines, handoffs, affected milestones, latest-date note), `Projects.cs` (latest-date note), `TaskQueries.cs` (Last activity actor), `Tasks.cs` (related deliverables on the task read), `Views.cs` (`staff` list), `src/Hub.Api/Infrastructure/EvaluationService.cs` (baseline into snapshots), `Infrastructure/Audit.cs` (baseline fields audited), `src/Hub.Api/Data/Entities.cs`, `Data/HubDb.cs`, one additive migration, `Text.cs` |
| Web | `web/src/pages/projects/Tasks.tsx`, `TaskPanel.tsx`, `Deliverables.tsx`, `Milestones.tsx`, `Timeline.tsx`, `ProjectLayout.tsx`, `Dashboard.tsx`, `Allocations.tsx`, `ProjectActivity.tsx`; `web/src/pages/Gantt.tsx`, `Staff.tsx`, `Portfolio.tsx`; `web/src/components/hub/people.tsx`, `fields.tsx`, `views.tsx`, `activity.tsx`; `web/src/i18n/en.ts` |
| Tests | `tests/Hub.Tests/Domain/ScheduleTests.cs` (new), `PermissionMatrixTests.cs`; `tests/Hub.Tests/Api/PickerRankTests.cs`, `DateContextTests.cs`, `ScheduleBaselineTests.cs` (new), `ViewsTests.cs`, `TimelineTests.cs`, `TasksTests.cs`, `MilestonesDeliverablesTests.cs`; `web/tests/interaction.cjs` (new, mocked API) with `test:interaction` in `web/package.json` and a step in the CI web job |

Schema: one migration adds nullable `milestone.baseline_date`, `deliverable.baseline_start_date` and `deliverable.baseline_due_date`. Expand only, so the previous image runs on the new schema. No backfill.

## Constitution Check

| Principle | Result |
|---|---|
| I. Coordination First | PASS. Each part answers who, what is due, or what is blocking. Hints and warnings never validate (warnings over hard validation). The one §30 change (one PM baseline beside the original date) is a returned amendment for Jay. Should-NOT auto-shift is untouched. |
| II. Deterministic and Explainable | PASS. No AI. The latest-date note, slip display, ranking and hints are stated rules; "Why?" names FR-INT-15. New thresholds live in organisation settings. |
| III. One Accountable Owner | PASS. A deliverable created from a task has one owner by today's default. |
| IV. Specification is the source of truth | PASS. §50 plus amendments to §8.5.2, §10.3, §10.4, §12.3, §12.16, §13.7 and §30. |
| V. Traceable by Construction | PASS. Re-baseline, its clearing, the Related link and the inline name edit write activity-log entries in the same transaction through the existing `SaveChanges` hook. Original dates never change. |
| VI. Secure by Default | PASS. The server decides every action. The people read refuses an unreadable project. Availability in the hint stays inside §40.10 D8 and returns no leave detail. `Rebaseline` is PM only with a matrix test. |
| VII. Simple Enough | PASS. No service, dependency, engine or broker. No complexity tracking needed. |

## Checks to run per slice

- `dotnet build -c Release` and `dotnet test -c Release` on real PostgreSQL; the 95% domain branch gate (`tools/coverage_gate.py`).
- `npm run build`, `npm run lint`, `npm run test:interaction`, plus the suites a slice's files touch (`test:resources`, `test:planner`, `test:supervisor-capacity`, `test:handoffs`, `test:ui-consistency`, `test:counts`).
- Flag pair tests (off unchanged, on works) for the slice's flag.
- Migration lint and the rollback-compatibility job (§43) for S07.
- §22 timing for the task list read with Last activity names on a 500-task project (S11) and the people read with a project (S01).
