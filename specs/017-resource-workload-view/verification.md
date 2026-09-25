# Verification: Resource and Workload View

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (250/250) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 96.9 % branches, services 87.7 % lines |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`Domain/WorkloadTests` (6): §12.15 worked examples — 24 h at 50 % due in ten working days gives 6 h in each of two
weeks (US1-1); overdue work lands in this week and undated work in its own bucket (US1-2); unestimated and finished
work adds nothing; the window's start (future start, long-started, start after due, weekend-only); the 110 % and
40 % edges, under-assignment never with unestimated work; deadline clusters need three tasks across two projects
within three days in the next fourteen.

`Api/WorkloadApiTests` (3):

| Test | Covers |
|---|---|
| Grid hours follow the stated method and flags fire | FR-001..FR-004: one person's 36 h and 6 h weeks computed by hand (6 + 20 + 10), 90 %, 5 h without due date, five open tasks with one unestimated and one overdue on two projects; Over-assigned at 140 % next week; Under-assigned only when everything is estimated; deadline cluster; indicator and project filters |
| Restricted work is absent and rebalancing notifies and recomputes | Workflow 10 / §36.1: 30 h on a Restricted project are missing from Sam's grid but in an Executive's; the drill-down offers reassignment; Sam's reassignment notifies both people and the PM, is logged with Sam as actor, and moves the hours; capacity 20 h gives 80 %; capacity for someone not his report refused; Standard User refused |
| Workload reports and export | FR-007: Workload by Employee with "Week of …" columns and the person's 8 h; by Discipline; grid and report exports |

## Manual checks (browser pane, as Sam)

| Check | Result |
|---|---|
| Grid for Alex, Jill and Marc with shaded cells showing hours over capacity, summaries with unestimated counts and flags | Pass (the person column header text was missing and is fixed) |
| Drill-down under Alex: project heading, tasks with status, due, estimate, hours left, per-week hours aligned with the grid, reassign picker | Pass |

## Decisions

- Tasks count on Setup and Active projects only; On Hold, Complete, Archived and Cancelled projects add no load.
- Someone with no open work shows as Under-assigned (0 %, nothing unestimated), which is the rule as written.

## Deferred (cross-packet rule)

- Working-day calendars with holidays (packet 021); logged hours stay out of the calculation (§36.8).
