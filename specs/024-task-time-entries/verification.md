# Verification: Task Time Entries

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (266/266) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 97.0 % branches, services 88.8 % lines |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`TimeTests` (5, all pass):

| Test | Covers |
|---|---|
| Entries add up and the day stays within 24 hours | AC-VIS-08 US1-1 (2.5 + 1.25 = 3.75 for the day, task and project), US1-2 (23 h then +2 refused with how much still fits; nothing partial saved), zero, negative, over-24 and three-decimal values refused, FR-005 (estimate and progress unchanged) |
| Concurrent saves cannot both break the day | SC-003: four simultaneous 13-hour saves for one person and date; exactly one succeeds |
| Complete tasks accept late hours but closed projects do not | US1-3: a late entry on a Complete task accepted; a Viewer refused; after the project is Cancelled, refused |
| Owners edit, PMs correct with a reason, and deletion is soft | US2-1: another member cannot reach the entry; the owner edits; the PM needs a reason and it is logged with the old and new hours; the task cannot be changed; US2-2: the owner's soft delete leaves the totals, is logged as Deleted, and the row is kept |
| Review scopes, totals and exports reconcile | US3-1 / SC-002: the PM sees 5 h on the project, the Supervisor only his report's 3 h, an unrelated member nothing; the CSV total row and the Task Hours report both say 5; US3-2: the report's hours, task and note on a Restricted project are absent from the Supervisor's list and export |

## Manual checks (browser pane, as Alex)

| Check | Result |
|---|---|
| Add time: choose T0006, 2.5 h, note "Site notes"; the week shows 2.5 h on Friday, the entry under its date, and totals by project and task | Pass |

## Defects found and fixed while verifying

- A deleted entry still appeared in the list: the task lookup inside the query used `IgnoreQueryFilters()` (so hours on
  deleted tasks keep their key), and EF applies that to the whole query, which switched off the entries' own
  soft-delete filter. The list now tests `DeletedAt` explicitly. No other query in the codebase uses
  `IgnoreQueryFilters()` inside a larger query.

## Decisions

- Entries on a task that is later deleted keep counting and keep the task's key; they belong to the person, not the task.
