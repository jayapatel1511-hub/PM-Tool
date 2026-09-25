# Verification: Search, Filters, and Reports

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (234/234) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 96.8 % branches, services 85.7 % lines |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`SearchReportsTests` (5, all pass):

| Test | Covers |
|---|---|
| Key lookup, grouped results, archived toggle and visibility | AC-SRCH-02 (project, task, deliverable, milestone and decision found by one word), AC-SRCH-01 / FR-SRCH-02 (a key in any case returns the item to open; a project number opens the project; an unknown key opens nothing), exact key ranked first, cancelled projects only with the toggle, one type with counts for the results page, people with "can open My Work" for self, supervisor and Executive only |
| Restricted items never appear | §18.1 (an outsider finds neither the project, its tasks nor its number; a member does) |
| Filters combine, links reproduce, exports hold the filtered rows | §18.3 (OR within status, AND with assignee), AC-SRCH-03 (the same query string gives another user the same rows), FR-007 (CSV has exactly the two filtered rows, quotes escaped, BOM), FR-010 (XLSX with frozen header, date-typed due dates, Parameters sheet naming the assignee and the generation time), exports logged twice with no task names in the log; every other list exports |
| Reports equal their lists and respect scope | FR-RPT-01 (Overdue Tasks over two projects: columns, days overdue, blocking count, minimum days), SC-004 (single-project report total equals the list its "Open as filtered list" link opens), Tasks Blocking Others, XLSX Parameters sheet, export logged against the project, every report runs, FR-ASG-08 (Sam sees Alex's assignment with roles, open and overdue tasks and who added him; not Priya; Standard User and Supervisor "all staff" refused; Executive allowed) |
| History shows dated changes with reasons and deletion snapshots | AC-AUD-01 (Due date: 2027-01-10 → 2027-01-17 with the reason and the actor), AC-AUD-02 (deleted task snapshot with name, assignee and due date; the dependency removed with it) |

`ActivityTests` still pass after the activity export moved to the shared writer (file names now carry the date).

## Manual checks (browser pane)

| Check | Result |
|---|---|
| Typing "dundurn" shows the project and the decision grouped by type, with "See all results" | Pass |
| Typing a task key in lower case and pressing Enter opens the task panel | Pass |
| Results page selects the first type with results and shows counts on every tab | Pass |
| Reports catalogue lists the twelve reports; Overdue Tasks for one project shows parameters, rows and "Open as filtered list" | Pass (row count now singular for one row) |
| Staff Assignments as Sam lists his three reports' assignments | Pass (the "All staff" choice now appears only for Executives and Admins) |
| Decision register sorted by required-by descending from the URL; header shows the sort | Pass |

Downloads were not triggered in the browser pane; file contents are covered by the API tests.

## Decisions

- Search uses `ILIKE` with a SQL rank rather than `tsvector` and `pg_trgm`: nothing searched is long text in this
  release, and the indexes are a packet 011 change if measured latency needs them.
- People are searched by name and email only (not job title), as §18.1 lists.
- Reports that span several projects offer no "Open as filtered list" until the cross-project task list screen
  (packet 022); single-project runs do.
- Report and list exports record the report key and its parameters in the activity log's item name.

## Deferred (cross-packet rule)

- Projects At Risk and Health History (packet 016), Workload reports (017) and Task Hours (024) join the catalogue in
  their packets; the catalogue is a list those packets extend.
- Saved views (packet 019); description and comment search (packet 020).
