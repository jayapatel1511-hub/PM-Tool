# Plan: Record Recovery and Counts

Implements §49 (FR-RCV-01 to FR-RCV-16) with defaults D1 to D13. Paths and lines are at `48586029` on `claude/tuesday-visual-batch1`. No code is written by this plan.

## Approach

Reuse what exists; add only the missing joins.

- **Part A.** Copy the task restore (`src/Hub.Api/Features/Tasks.cs:583-598`) for deliverables and milestones. The save hook already turns a cleared `DeletedAt` into a "Restored" entry (`src/Hub.Api/Data/HubDb.cs:1086-1091`) and queues re-evaluation (`src/Hub.Api/Infrastructure/Audit.cs` `AffectsEvaluation`). So restore needs no audit or evaluation code. One small read endpoint lists deleted rows with `IgnoreQueryFilters()` on a plain projection, not through `TaskQueries.Apply`, whose subqueries must keep their soft-delete filters. Export reuses `ExportFile.Send` (`src/Hub.Api/Features/Reports.cs:614-617`). The permission is the existing `Permissions.RestoreItem` (`src/Hub.Domain/Permissions.cs:350`), returned as `Restore` in the project permissions block (`src/Hub.Api/Features/Projects.cs:287-305`).
- **Part B.** Count with the list's own query and return `{Value, Link}`, as `Dashboard.cs` `Count()` already does. These are new fields beside the existing ones (`issues`, `risks` and the snapshot's `deliverablesDue14` keep their shapes), so no response shape changes. Extract the risk and issue filters out of `RiskList` and `IssueList` (`src/Hub.Api/Features/Registers.cs:136-153`, `296-346`) into query functions that the list and the dashboard share; this is one shared function, not a copy. The discipline "Due ≤ 14 d" count uses `Scope.Deliverables(...)` as the Upcoming tile does. Portfolio row links and the milestone links are client links checked by tests. The new `overdueDecisions` Portfolio filter is applied to the rows in `src/Hub.Api/Features/Portfolio.cs`, and `reviewsStalled` is one more case in the `indicator` switch (`src/Hub.Api/Features/MyWork.cs:189-196`).
- **Part C.** `WorkCalendar` gains a signed step (negative counts back). `TemplatePlanner.Plan` takes an optional calendar: `null` gives today's `AddDays`. One nullable argument keeps every current caller and test unchanged. One additive column, `offset_day_kind`, has a CHECK and the default `CalendarDays`, so existing rows keep calendar days.
- **Part D.** The status segments are four `DeliverableEndpoints.Filter` counts per discipline in the dashboard response. Overdue deliverables per Portfolio row is one grouped query over the same `indicator=overdue` predicate. Grouping is client-side over rows the server already returns, like `ProjectList.tsx:44-55`.

## File boundaries

| Part | Server | Client | Tests |
|---|---|---|---|
| A | `Features/Recovery.cs` (new: list, export), `Features/Modules.cs` (map), `Features/Deliverables.cs` and `Features/Milestones.cs` (restore), `Features/Tasks.cs` (parent guard and `If-Match`, applied only while `flag.show_deleted` is on), `Features/Projects.cs` (permission flag), `Text.cs` | `components/hub/deleted.tsx` (new), `pages/projects/Tasks.tsx`, `Deliverables.tsx`, `Milestones.tsx`, `lib/types.ts`, `i18n/en.ts` | `tests/Hub.Tests/Api/RecoveryApiTests.cs` (new), `web/tests/recovery-counts.cjs` (new) |
| B | `Features/Dashboard.cs`, `Features/Registers.cs`, `Features/Portfolio.cs`, `Features/MyWork.cs`, `Features/Views.cs` | `pages/projects/Dashboard.tsx`, `pages/projects/Milestones.tsx`, `pages/Portfolio.tsx`, `pages/Staff.tsx`, `pages/Time.tsx`, `i18n/en.ts` | `tests/Hub.Tests/Api/CountLinkTests.cs` (new), `PortfolioTests.cs`, `TimeTests.cs`, `web/tests/counts.cjs`, `web/tests/recovery-counts.cjs` |
| C | `src/Hub.Domain/Calendar.cs`, `src/Hub.Domain/Templates.cs`, `Data/Entities.cs` (`ProjectTemplate`), `Data/HubDb.cs` (mapping, CHECK), one migration in `Data/Migrations/`, `Infrastructure/Audit.cs:127`, `Features/Templates.cs` (header, draft copy, read, preview `OfficeId`, three `Plan` calls) | `pages/Templates.tsx`, `pages/projects/FromTemplate.tsx`, `i18n/en.ts` | `tests/Hub.Tests/Domain/TemplatePlannerTests.cs`, `tests/Hub.Tests/Api/TemplatesTests.cs`, `web/tests/recovery-counts.cjs` |
| D | `Features/Dashboard.cs`, `Features/Portfolio.cs` (count, export columns behind the flag), `Features/Views.cs` (`group` on portfolio) | `pages/projects/Dashboard.tsx`, `pages/Portfolio.tsx`, `i18n/en.ts` | `CountLinkTests.cs`, `PortfolioTests.cs`, `web/tests/recovery-counts.cjs` |

Server paths are under `src/Hub.Api/` unless shown in full; client paths are under `web/src/`. Each flagged part adds one entry to packet 039's `src/Hub.Domain/Flags.cs` (`show_deleted`, `template_working_days`, `status_rollups`) and uses its shared server guard and client hook. Wiring `test:recovery-counts` into CI touches `.github/workflows/ci.yml`, a Tier B file under packet 039's merge rules. Only slice S07 adds a migration (one schema-lane writer).

## Constitution check

| Principle | Result |
|---|---|
| I. Coordination First | PASS. Lost work comes back, counts reconcile with their lists, template dates land on working days, and discipline status shows what is due and stuck. Nothing removed. |
| II. Deterministic and Explainable | PASS. Fixed status groups, existing indicators (G-01 Overdue) and one pure working-day function in `Hub.Domain` under the 95% branch gate. Each sum count states its definition. No AI. The existing hard-coded 14-day window is reused, not extended. |
| III. One Accountable Owner | PASS. No ownership change. |
| IV. Specification is the source | PASS. §49 governs; the amendment requests in the return value align §12.14, §13.1, §13.3, §13.6, §13.12, §19 and the `project_template` table. |
| V. Traceable by Construction | PASS. Soft delete only; restore is logged by the save hook in the same transaction; the template setting is an audited field; `If-Match` honoured. |
| VI. Secure by Default | PASS. Server-side `RestoreItem`, deny by default, 403 for the deleted list, Restricted visibility unchanged, exports logged. Authentication untouched. |
| VII. Simple Enough | PASS. No service, dependency or engine; one additive column. No complexity tracking needed. |

Open product decisions (§34): none.

## Checks to run per slice

- `dotnet test` filtered to the slice's test classes, then the full suite on the PR.
- `python3 tools/coverage_gate.py` (rules-engine branches at least 95%, services at least 70%) for S06 and every server slice.
- `npm run build` and `npm run lint` in `web/`.
- `npm run test:recovery-counts` (mocked API, axe) for interface slices; `npm run test:counts` against the local preview with the packet 036 fixtures for Part B and Part D.
- EF model-drift check and the existing migration up and rollback smoke for S07.
- Flag pair test (flag off equals today's screen) for S02, S08, S10 and S11, per packet 039.
