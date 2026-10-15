# Plan: Capacity Figures and Server Values

Implements §45 with defaults D1 to D9. Spec only at this point; no application code is authorised by this plan.

## Approach

Move each rule to where it already belongs, in the smallest additive steps.

- **Rules in Hub.Domain.** The band function, the three thresholds, `Uncommitted`, `ManageOpenRole`, `ReviewDates`, the P-05 correction check and the role-label constant live in `src/Hub.Domain`, under its 95 % branch gate. Existing call signatures keep working: `Workload.OverAssigned` and `UnderAssigned` gain threshold parameters that default to today's constants, so `tests/Hub.Tests/Domain/WorkloadTests.cs` stays unchanged.
- **Values on existing responses.** Every new value is an added field on a response that exists today: Workload cells and rows, the project's `permissions`, the coordination options, task rows, planner rows and `GET /reference`. No endpoint path, table or migration is added. Settings rows come from startup seeding (`src/Hub.Api/Data/Seed.cs:38-39`). The CHECK constant keeps its SQL text identical.
- **Client reads, never derives.** Each page reads the flag, band or value and drops its own comparison. Wording changes go through `web/src/i18n/en.ts`.
- **Parity first.** Each permission-flag slice adds a role-by-status parity test before it touches the page, so any case where today's client rule differs is visible in review (FR-FIG-09, D5).
- **Kept rows are request state.** The `keep` list lives in Planner page state, not the URL, so saved views, exports and shared links never see it. It rides on every grid request, so a later live refresh (§46) keeps the rows too.

Task rows: `TaskQueries.Rows` (`src/Hub.Api/Features/TaskQueries.cs:133`) gains the actor and loads one project context per distinct project on the page, so flag cost grows with projects per page, not rows.

## File boundaries

| Area | Files |
|---|---|
| Domain | `src/Hub.Domain/Workload.cs`, `Settings.cs`, `Permissions.cs`, `Vocabulary.cs` |
| API | `src/Hub.Api/Features/Workload.cs`, `Planning.cs`, `Projects.cs`, `OpenRoles.cs`, `Changes.cs`, `TaskQueries.cs`, `Me.cs`; `src/Hub.Api/Infrastructure/Coordination.cs`; `src/Hub.Api/Data/HubDb.cs` (constant only) |
| Client | `web/src/pages/Workload.tsx`; `web/src/components/planner/PlannerGrid.tsx`, `ProjectPlanningGrid.tsx`, `ContributionList.tsx`, `labels.ts`; `web/src/pages/Planner.tsx`; `web/src/components/hub/people.tsx`; `web/src/pages/projects/` `Allocations.tsx`, `Dashboard.tsx`, `Decisions.tsx`, `DesignBasis.tsx`, `ProjectLayout.tsx`, `Readiness.tsx`, `ReadinessForms.tsx`, `Submissions.tsx`, `Tasks.tsx`, `Coordination.tsx`, `Changes.tsx`, `CoordinationRegister.tsx`, `CoordinationForms.tsx`; `web/src/lib/types.ts`; `web/src/i18n/en.ts` |
| Tests | `tests/Hub.Tests/Domain/WorkloadTests.cs`, `DomainHelpersTests.cs`, `PermissionMatrixTests.cs`, `PermissionSweepTests.cs`; `tests/Hub.Tests/Api/WorkloadApiTests.cs`, `PlanningApiTests.cs`, `ProjectsTests.cs`, `OpenRoleApiTests.cs`, `DesignBasisApiTests.cs`, `ReviewChangeTests.cs`, `TasksTests.cs`; `web/tests/resources.cjs`, `planner.cjs`, `supervisor-capacity.cjs`, `design-basis.cjs`, `reviews-changes.cjs`, `handoffs.cjs` |
| Guard | `tools/ratchet.py`, `tools/ratchet.json` (from §44.6) |
| Flag | `src/Hub.Domain/Flags.cs` (from §43.4), two entries: `figure_names` and `planner_kept_rows` |

Unrelated edits in these files are preserved.

## Constitution Check

| Principle | Result |
|---|---|
| I. Coordination first | Pass. Every change helps answer who has room and who may act. |
| II. Deterministic and explainable | Pass, and closes a gap. The 90 % band and the 110 % and 40 % thresholds become stated rules in settings, and the legend names them. No AI, ML or LLM feature. |
| III. One accountable owner | Pass. Unchanged; the open-role creator stays its owner. |
| IV. Specification is the source | Pass. §45 governs; AC-PLN-12, FR-ROLE-02, §12.15, §13.11 and §39.12 are amended by request in the same change set. |
| V. Traceable by construction | Pass. Setting changes are audited by the existing hook. Kept rows and flags write nothing. |
| VI. Secure by default | Pass. Every flag is computed by the function its endpoint enforces, and the server still enforces every command. `keep` ids pass the same visibility filters. No hidden project identity or hours are returned. |
| VII. Simple enough | Pass. No new service, dependency, table or migration. No Complexity Tracking entry is needed. |

Open product decisions this plan depends on: none of §34. Its own D1 to D9 use their defaults. Slices S01, S02, S05, S07 and S09 to S11 are Tier B (D9, §43.11).

## Checks to run

- `dotnet build Hub.slnx -c Release` and `dotnet test Hub.slnx -c Release` with coverage, then `python3 tools/coverage_gate.py coverage` (domain branches at least 95 %).
- `dotnet ef migrations has-pending-model-changes --project src/Hub.Api` after S05 reports no changes.
- `npm run build` and `npm run lint` in `web/`.
- Browser suites: `npm run test:resources`, `test:planner`, `test:supervisor-capacity`, `test:design-basis`, `test:coordination`, `test:handoffs`.
- `python3 tools/ratchet.py` (§44.6) from S08 on.
- When §44.4 has landed: the golden `openapi.json` and shape snapshot regenerated, with additions only.
- `python3 tools/build_spec.py --check` and `python3 tools/trace_spec.py --check`, run by the integrator.
