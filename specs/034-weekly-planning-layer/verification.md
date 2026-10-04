# Verification: Weekly Planning Layer

**Date**: 2026-10-03
**State**: Specified and planned. No application code exists for this packet; every application check below is **UNRUN**. Documentation checks for the specification change are recorded separately and are not evidence that the feature works.

## Open decisions (T001)

UNRUN — Jay has not yet confirmed the §39.12 defaults. Until he does, the defaults in §39.12 apply.

## Documentation checks (this specification change)

These ran when §39, §8.11, §10.9, §13.21 and packet 034 were written; they cover the documents only.

| Check | Result |
|---|---|
| `python3 tools/build_spec.py` then `python3 tools/build_spec.py --check` | PASS — rebuilt, then "up to date" |
| `python3 tools/trace_spec.py` then `python3 tools/trace_spec.py --check` | PASS — 660 IDs, 0 not cited; 252 sections, 0 not cited; 0 unknown |

## Application checks

| # | Check | Covers | Result |
|---|---|---|---|
| 1 | `dotnet build src/Hub.Api/Hub.Api.csproj --no-restore` | T002–T013 | UNRUN |
| 2 | `dotnet ef migrations has-pending-model-changes --project src/Hub.Api/Hub.Api.csproj --startup-project src/Hub.Api/Hub.Api.csproj` | T006 | UNRUN |
| 3 | `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter 'FullyQualifiedName~PlanningTests\|FullyQualifiedName~PermissionMatrixTests'` | T003–T005; AC-PLN-04, AC-PLN-05, AC-PLN-06, AC-PLN-09, AC-PLN-15 | UNRUN |
| 4 | `dotnet test … --filter 'FullyQualifiedName~PlanningSchemaTests'` (fresh PostgreSQL; migration up and down) | T007 | UNRUN |
| 5 | `dotnet test … --filter 'FullyQualifiedName~PlanningApiTests\|FullyQualifiedName~PlanningPrivacyTests'` | T009–T014; AC-PLN-01 to AC-PLN-13, AC-PLN-15, AC-PLN-16, AC-PLN-19, AC-PLN-20 | UNRUN |
| 6 | Unchanged-behaviour guard: `dotnet test … --filter 'FullyQualifiedName~Allocation\|FullyQualifiedName~Workload\|FullyQualifiedName~Readiness'` with no test edits; snapshot regression; `git diff --stat` of the "must not change" files is empty | T015; AC-PLN-11 | UNRUN |
| 7 | Full suite with coverage, then `python3 tools/coverage_gate.py <results>` (Hub.Domain branches ≥ 95 %, Features lines ≥ 70 %) | All backend tasks | UNRUN |
| 8 | `tools/scale` seed and measurement (grid ≤ 1.5 s p95; My Week ≤ 500 ms p95) | T016; AC-PLN-17 | UNRUN |
| 9 | `npm --prefix web run build` and `npm --prefix web run lint` | T017–T020 | UNRUN |
| 10 | `npm --prefix web run test:planner` (mocked API, axe) | T021; AC-PLN-01, AC-PLN-03, AC-PLN-07, AC-PLN-08, AC-PLN-14, AC-PLN-18 | UNRUN |
| 11 | `node tools/a11y/audit.js` on `/planner` and `/my-work` | T021; FR-PLN-28 | UNRUN |
| 12 | Supervised browser session with synthetic people (Supervisor, direct report, Executive, Project Manager, Admin) | T023; all AC-PLN | UNRUN |
| 13 | Manual keyboard and screen-reader pass at 1440, 1024 and 375 px | AC-PLN-18 | UNRUN |
| 14 | `git diff --check` | All tasks | UNRUN |

## Acceptance status

| Criterion | Status |
|---|---|
| AC-PLN-01 to AC-PLN-20 | UNPROVEN — no implementation |

A ticked checklist item or a passing documentation check is not evidence that the planner works.
