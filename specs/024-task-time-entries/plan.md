# Implementation Plan: Task Time Entries

**Branch**: `024-task-time-entries` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

The Time route: record actual hours against tasks (work date, decimal hours, note), daily and weekly totals, owner
edits and soft deletes, PM corrections with a reason, review of permitted entries by PMs, leads and supervisors, and a
reconciling export and Task Hours report.

## Technical Context

As packet 001. `task_time_entry` exists from the initial schema (versioned, soft-deleted, logged, `hours > 0 AND
hours <= 24` check). Endpoints: `GET /time`, `GET /time/export`, `POST /time`, `PATCH /time/{id}`, `DELETE /time/{id}`;
the Task Hours report joins the packet 009 catalogue. The 24-hour day is checked under a transaction-scoped
PostgreSQL advisory lock per person and date, so concurrent saves are serialised (SC-003).

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | No timers, approvals, billing rates or payroll (§36.8) | Pass |
| II | Totals are sums of the visible, non-deleted entries; the export and report carry the same total | Pass |
| III | Every entry belongs to the person who recorded it; editing never changes its owner or its task | Pass |
| IV | No new statuses | Pass |
| V | Creation, edits, PM corrections (with reason) and soft deletion are logged against the task's key; nothing is hard-deleted | Pass |
| VI | Entering time needs a writing project member (`EnterTime`); owners edit, PMs correct with a reason (`EditTime`); review follows `ViewTimeEntry` (PM: project, lead: discipline, Supervisor: direct reports on projects they can see); Restricted projects never appear | Pass |
| VII | No new infrastructure; the advisory lock is a PostgreSQL feature | Pass |

## Design notes

- Hours are decimals with at most two places, greater than 0 and at most 24; the day total is re-checked on edits.
- Complete tasks accept late entries; Archived and Cancelled projects refuse them (`EnterTime`).
- The review list hides entries the caller may not see rather than failing, so filters never reveal counts.
- Actual hours are read by nothing else: estimates, progress, health and the workload grid do not use them (FR-005).

## Project Structure

```text
src/Hub.Api/Features/Time.cs (+ Reports.cs: task-hours)
tests/Hub.Tests/Api/TimeTests.cs
web/src/pages/Time.tsx
```

## Checks

AC-VIS-08 and SC-001..SC-003 as API tests, including four concurrent 13-hour saves; the page in the browser.

## Complexity Tracking

None.
