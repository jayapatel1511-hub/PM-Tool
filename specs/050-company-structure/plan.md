# Plan: Company Structure

Implements §54 (FR-UNT-01 to FR-UNT-20) with defaults D1 to D8. Each slice in [tasks.md](tasks.md) is one PR that merges dark behind `flag.org_structure`.

## Approach

Reuse first. New storage is two tables and two nullable columns.

- **Tables:** `OrgLevel(id, name, order)` and `OrgUnit(id, parent_id, level_id, name, leader_id, discipline_id, office_id, closed_on)`, following the `RefData` pattern in `src/Hub.Api/Data/Entities.cs`. Cycle and level-order checks are pure functions in a new `src/Hub.Domain/OrgTree.cs`.
- **Columns:** `AppUser.UnitId` and `Project.LeadUnitId`, both nullable.
- **People scope:** one function, `OrgTree.PeopleScope(viewer)`, returns the set of user IDs (direct reports, optional indirect reports, led subtrees). Every read that today asks "supervised staff" (`src/Hub.Api/Features/Me.cs`, `Workload.cs`, `Planning.cs`, `PersonCapacity.cs`, `Availability.cs`) calls it instead when the flag is on. Write checks in `src/Hub.Domain/Permissions.cs` are not touched.
- **History:** placement changes are already written to `ActivityLog`; a past-week read takes the last `unit_id` change at or before the week end.
- **Import:** a parse-and-preview endpoint and an apply endpoint in one transaction, reusing the CSV preview pattern of the HR leave import (§37.6 / `Availability.cs`).

## File boundaries

| Slice | Files |
|---|---|
| S01 levels, units, placement | `src/Hub.Domain/OrgTree.cs` (new), `src/Hub.Api/Data/Entities.cs`, `src/Hub.Api/Data/HubDb.cs`, one additive migration, `src/Hub.Api/Features/Admin.cs`, `src/Hub.Domain/Flags.cs`, `src/Hub.Api/Text.cs` |
| S02 people scope | `src/Hub.Domain/OrgTree.cs`, `src/Hub.Domain/Settings.cs`, `src/Hub.Api/Features/Me.cs`, `Workload.cs`, `Planning.cs`, `PersonCapacity.cs`, `Availability.cs` |
| S03 Admin and Organisation UI | `web/src/pages/admin/` (new Structure tab), `web/src/pages/Organisation.tsx` (new), `web/src/i18n/en.ts`, `web/tests/org-structure.cjs` (new) |
| S04 My Staff, Workload, Planner, Portfolio | `web/src/pages/Staff.tsx`, `Workload.tsx`, `Planner.tsx`, `Vacation.tsx`, `Portfolio.tsx`, `web/src/pages/projects/ProjectList.tsx`, `ProjectSettings.tsx`, `src/Hub.Api/Features/Projects.cs`, `Portfolio.cs` |
| S05 reports and history | `src/Hub.Api/Features/Reports.cs`, `src/Hub.Api/Features/ListExports.cs` |
| S06 CSV import | `src/Hub.Api/Features/Admin.cs`, `web/src/pages/admin/` |

## Risks

- **Scope reads in many places:** S02 must route every supervised-staff read through one function; a missed read shows a leader too little, never too much, because edits are untouched.
- **Large trees:** the scope set is computed per request with one recursive query; cache per request only.
