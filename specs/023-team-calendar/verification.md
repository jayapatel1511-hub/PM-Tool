# Verification: Team Calendar

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (261/261) |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`CalendarTests` (3, all pass):

| Test | Covers |
|---|---|
| Week shows deadlines and events under their toggles | FR-002 (a task, deliverable and milestone date as all-day deadlines with their source ids, no time), FR-003 / AC-VIS-04 (a meeting, site work and a private internal task in one week; 09:00 in Halifax stored as 12:00 UTC and shown as 09:00), each toggle returns only its own entries, the private event only for its owner |
| Validation, editing rights and cancellation | FR-003 / US2: end before start refused on `end`; a meeting without a project refused; a non-member cannot create; the owner moves the time (logged as a date change); a lead who is not owner or PM is refused; the PM edits; the PM cancels, the event leaves the calendar, the cancellation is logged, and it can no longer be edited |
| Restricted projects reveal nothing | FR-005 / SC-003: an outsider's calendar has no entry, title or project from a Restricted project even when naming it; a direct link answers 404; a member sees it; someone else's private event answers 404 |

## Manual checks (browser pane, project 2026-0417)

| Check | Result |
|---|---|
| Week of 28 September: all-day deadlines, two overlapping meetings side by side, site work 07:30–15:00, a private internal task | Pass |
| Agenda lists each day with all-day deadlines first and events by time, with project and location | Pass |
| Opening "Client call" shows its local start and end, visibility, and Cancel event for its organiser | Pass |

## Decisions

- Deadlines show open work only (complete tasks, issued or accepted deliverables and cancelled items are left out);
  completed milestones stay, marked by their status.
- Moving a deadline is done in its source panel rather than by dragging on the calendar, so every source rule applies
  unchanged (FR-004).
