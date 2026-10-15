# Plan: Planning, Reporting and Onboarding

Implements §51 (FR-PRO-01 to FR-PRO-25) with defaults D1 to D19. Each slice in [tasks.md](tasks.md) is one PR that merges dark behind its §51.11 flag. Paths are from the repository root.

## Approach

Reuse first. Every addition reads records and rules that already exist; the new storage is a few additive columns and two small tables.

- **Card** (§51.2): one pure step function in `Hub.Domain` over role flags and yes-or-no facts; one endpoint gathers the facts with existence queries. Hidden state is a nullable column on `UserSetting`, beside `HoldDigestWhenAway`. The UI follows the existing one-line banner pattern of `ProjectLayout.tsx` `StatusBanner` (the Setup checklist, P-09).
- **Reports** (§51.3): two `Def` entries in the `Reports.cs` catalogue with dynamic week columns, as `workload-employee` already does. Both call the planner's own read (`PlanningEndpoints.Read`, made internal), so the figures cannot drift from the grid.
- **Route counter** (§51.4): one table keyed by day and route key, one upsert, one named rate-limit policy beside the existing ones in `Program.cs`, one catalogue report. The client sends the matched route pattern; the allowlist lives in the API.
- **Template roles** (§51.5): one `TemplateRole` table; dates come from `TemplatePlanner` (pure, under the 95% branch gate); creation reuses `OpenRole` and runs in `TemplateHook.AfterCreate`, as template design-basis suggestions already do.
- **Unfilled and unassigned** (§51.6): `WorkloadEndpoints.UnfilledRoles` serves the Planner's Projects view through an opt-in grid parameter; Unassigned work reuses `TaskQueries` rows, `Permissions.AssignTask`, the owner picker and the existing task edit.
- **Status note** (§51.7): three nullable `Project` columns written through the existing project edit; the header is computed in the existing project and Portfolio reads.
- **My Work views** (§51.8): two read endpoints over `ItemWatcher`, `Decision.RequestedById` and `CreatedBy`, loaded only when their view opens, so `/me/work` stays as it is.
- **Import** (§51.9): the leave-import pattern of `Availability.cs` (`TextFieldParser`, size and row caps, preview then commit) feeding the existing project-create rules. No preview receipt table: commit checks the file again.
- **Capacity polish** (§51.10): per-project split of the existing task forecast in the planner cell read; nullable weekday hours on `PersonSchedule` read at the one divisor in `PersonCapacity.Load` and the leave check in `Availability.cs`; the Team tab reuses `usePeopleCapacity`.

## File boundaries

| Slice | Files |
|---|---|
| S01 card server | `src/Hub.Domain/Onboarding.cs` (new), `src/Hub.Api/Features/Onboarding.cs` (new), `src/Hub.Api/Data/Entities.cs` (`UserSetting`), `src/Hub.Api/Data/HubDb.cs`, one additive migration, `src/Hub.Api/Text.cs`, the flag list from packet 039 |
| S02 card UI | `web/src/components/hub/onboarding.tsx` (new), `web/src/pages/MyWork.tsx`, `web/src/pages/Preferences.tsx`, `web/src/i18n/en.ts`, `web/tests/planning-reporting.cjs` (new), `web/package.json`, `tools/ci_steps.txt` (one line) |
| S03, S04 reports | `src/Hub.Api/Features/Reports.cs`, `src/Hub.Api/Features/Planning.cs` (visibility of `Read` only), `src/Hub.Api/Text.cs` |
| S05 route counter server | `src/Hub.Api/Features/RouteCounter.cs` (new), `src/Hub.Domain/Settings.cs` (`route_counter_enabled`), `src/Hub.Api/Data/Entities.cs`, `HubDb.cs`, one additive migration, `src/Hub.Api/Program.cs` (policy), `Reports.cs`, `Text.cs` |
| S06 route counter client | `web/src/app/routes.tsx` (one hook in the shell), `web/src/lib/route-counter.ts` (new), `web/src/pages/Preferences.tsx`, `en.ts`, `docs/pilot/pilot-plan.md` (one line) |
| S07 template roles server | `src/Hub.Domain/Templates.cs`, `src/Hub.Api/Data/Entities.cs`, `HubDb.cs`, one additive migration, `src/Hub.Api/Features/Templates.cs` |
| S08 template roles UI | `web/src/pages/Templates.tsx`, `web/src/pages/projects/FromTemplate.tsx`, `en.ts` |
| S09 unfilled group | `src/Hub.Api/Features/Planning.cs`, `src/Hub.Api/Features/Workload.cs` (visibility only), `web/src/components/planner/ProjectPlanningGrid.tsx`, `web/src/pages/Planner.tsx`, `web/src/pages/projects/Allocations.tsx` (`fill` parameter) |
| S10 unassigned work | `src/Hub.Api/Features/Workload.cs`, `web/src/pages/Workload.tsx`, `en.ts` |
| S11 status note | `src/Hub.Api/Data/Entities.cs` (`Project`), one additive migration, `src/Hub.Api/Features/Projects.cs`, `src/Hub.Api/Features/Portfolio.cs`, `src/Hub.Api/Infrastructure/Audit.cs` (or the packet 040 registry file), `web/src/pages/projects/Dashboard.tsx`, `web/src/pages/Portfolio.tsx`, `en.ts`, `Text.cs` |
| S12 My Work views | `src/Hub.Api/Features/MyWork.cs`, `web/src/pages/MyWork.tsx`, `en.ts` |
| S13 import server | `src/Hub.Api/Features/ProjectImport.cs` (new), `Text.cs` |
| S14 import UI | `web/src/pages/projects/ProjectList.tsx` (Admin overflow action and dialog), `en.ts` |
| S15 estimate vs planned | `src/Hub.Api/Features/Planning.cs` (`Cell` and the Projects view rows), `web/src/components/planner/ContributionList.tsx`, `ProjectPlanningGrid.tsx` |
| S16 weekday hours | `src/Hub.Api/Data/AvailabilityEntities.cs` (`PersonSchedule`), one additive migration, `src/Hub.Api/Features/PersonCapacity.cs`, `src/Hub.Api/Features/Availability.cs`, `Audit.cs` (or the registry file), `web/src/pages/Vacation.tsx` |
| S17 away chip | `web/src/pages/projects/Team.tsx`, `en.ts` |

Tests named per slice live in `tests/Hub.Tests/Domain/`, `tests/Hub.Tests/Api/` and `web/tests/`. Unrelated edits in shared files are kept.

## Constitution Check

| Principle | Result |
|---|---|
| I. Coordination First | PASS. Each addition answers who has room, what is unfilled or unassigned, what I asked for, and what to do first. The card avoids §30 gamification; the import is one-way and manual, not ERP functionality; no custom fields. |
| II. Deterministic and Explainable | PASS. No AI. Steps are rules over records; report figures are the PLN rules; the status note header is computed and the note feeds no rule; estimates stay context. The route counter's load-time bands are a fixed measuring scale, and nothing is derived from them, so no rule threshold is hard-coded. |
| III. One Accountable Owner | PASS. Template-created open roles are owned by the creating Project Manager (FR-ROLE-01); imported projects have one named Project Manager; the status note has an author, not a second owner. |
| IV. Specification Is the Source of Truth | PASS. §51 is added; the amendments to §20.1, §23.8, §12.14, §12.15.2, FR-CAP-02, PLN-05, FR-ROLE-04, §39.12, §40.10 and §42.5 are returned to the integrator with exact text. |
| V. Traceable by Construction | PASS. Template roles, status notes, imported projects and schedule changes write activity entries in the same transaction; the import adds a summary event. Route counts and the card's hidden time are not changes to coordinated data and are not logged, as §20.1 is amended to say. Soft delete and row versions are unchanged. |
| VI. Secure by Default | PASS. Every new read and command is authorised on the server, deny by default: reports apply PLN-04 and FR-PLN-19; My Work views apply project visibility; open roles keep FR-ROLE-05; import is Admin only with size and row caps; the route counter stores no user and is rate limited. |
| VII. Simple Enough for a Small Team | PASS. No new service, dependency or library. Two small tables and nullable columns, all additive. No complexity tracking entry is needed. |

## Checks to run

Per slice: `dotnet test` (the named classes plus the full suite), the 95% domain branch gate (`tools/coverage_gate.py`), `npm run build` and `npm run lint` in `web`, the named browser suites, and the migration lint and rollback-compatibility job from packet 039 for every slice with a migration. The calm census (`web/tests/calm-acceptance.cjs`) runs in the nightly real-API job for S02. The integrator runs `python3 tools/build_spec.py` and `python3 tools/trace_spec.py --check` after the spec PR.
