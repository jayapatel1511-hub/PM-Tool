# Tasks: Company Structure

Each slice is one PR, merges dark behind `flag.org_structure` and adds the flag pair test (off: identical to before; on: present), covering AC-UNT-09 for its area. Every slice needs packet 039's flags slice (S09) first; Jay approved 050-D1 on 2026-10-18.

Records status (2026-10-25): all six slices are integrated locally on `codex/integrate-039-050` and land dark behind `flag.org_structure` (flag-off checks: `OrgStructureTests.Structure_is_dark_when_flag_is_off`, `web/tests/schema-workflows.cjs`). Not pushed or merged as slice PRs; hosted and human acceptance remain.

- [x] S01 Levels, units and placement (M, Tier B: migration). (local part; slice PR and operational migration remain)
  - `OrgLevel`, `OrgUnit`, `AppUser.UnitId`; Admin endpoints to create, rename, move, close and place; cycle, level-order and close rules; manager-cycle refusal; activity entries.
  - Covers FR-UNT-01 to FR-UNT-07, FR-UNT-17, FR-UNT-20; AC-UNT-01, AC-UNT-02.
  - Tests: `tests/Hub.Tests/Domain/OrgTreeTests.cs` (cycles, level order, subtree moves); `tests/Hub.Tests/Api/OrgStructureTests.cs` (Admin only, close refused with people or open children, log entries).
- [x] S02 People scope (M). After S01.
  - `OrgTree.PeopleScope`; `include_indirect_reports` setting; every supervised-staff read uses it; no write check changes.
  - Covers FR-UNT-08 to FR-UNT-11; AC-UNT-03, AC-UNT-04, AC-UNT-05.
  - Tests in `OrgStructureTests.cs`: leader sees but cannot edit; Restricted work excluded; indirect setting on and off; cycle grants nothing.
- [x] S03 Admin structure and Organisation page (M). After S01. (local part; browser cases live in `web/tests/schema-workflows.cjs` against a mocked API, not a separate `org-structure.cjs`; live three-level headcount case unproven)
  - Covers FR-UNT-15, FR-UNT-17 (UI).
  - Tests: `web/tests/org-structure.cjs`.
- [x] S04 Scope switcher and group by unit (M). After S02. (local part; subtree subtotal browser comparison unproven)
  - My Staff switcher; Workload, Planner and Vacation group and filter; project lead unit and Portfolio filter.
  - Covers FR-UNT-12, FR-UNT-13, FR-UNT-14; AC-UNT-06.
  - Tests: subtotals add up; each person once; `web/tests/org-structure.cjs` cases.
- [x] S05 Reports and history (S). After S01. (tests: `OrgCapacityIntegrationTests`, `OrgStructureTests.Unit_filter_is_shared_by_screen_and_export_reads`)
  - Covers FR-UNT-16, FR-UNT-19; AC-UNT-08.
  - Tests: past-week placement from the log; unit columns and filter; CSV and XLSX.
- [x] S06 CSV import (S). After S01.
  - Covers FR-UNT-18; AC-UNT-07.
  - Tests: preview errors apply nothing; corrected file applies all; blanks never clear.
