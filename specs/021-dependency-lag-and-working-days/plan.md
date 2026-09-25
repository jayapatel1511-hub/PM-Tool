# Implementation Plan: Deliverable Dependencies, Lag, and Working Days

**Branch**: `021-dependency-lag-and-working-days` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

Explicit Finish-to-Start links between deliverables with an optional lag, shown apart from derived links, with loops
refused; lag on task dependencies; Waiting, Blocked and Blocking Others on deliverables; and working-day counting with
office holiday calendars that Admins maintain, used by thresholds, lags, blocked days and workload spreading.

## Technical Context

As packet 001. Already present: `deliverable_dependency` and `task_dependency.lag_days`, the `holiday` table, the
`working_days_enabled` setting, `WorkCalendar`, calendar-aware "within N days" and "for N days" in the rules engine,
task lag in the satisfied check, and explicit deliverable links producing Waiting and Blocked. One migration,
`DeliverableBlockingCount` (`deliverable_state.blocking_count`).

New in the rules engine: lag on deliverable links (from the predecessor's issue date), D-12 for deliverables with lag,
lag and blocked days counted in working days when on, and Blocking Others for deliverables; `DeliverableSnap.IssuedDate`;
`Workload.Spread` takes the project's calendar; `Permissions.ManageDeliverableDependency`. API:
`POST /deliverables/{id}/dependencies`, `DELETE /deliverable-dependencies/{id}`, explicit links and a link permission in
the deliverable detail, `blockingCount` in deliverable rows, `GET/POST/DELETE /admin/holidays`, `Calendars.For` for
per-project calendars in workload views. Web: linked deliverables in the deliverable panel with a link dialog, lag in the
task dependency dialog and on each link, the "Blocking N" chip on deliverables, and Administration → Holiday calendars.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Date arithmetic over fixed calendars; nothing inferred | Pass |
| II | One calendar function serves thresholds, lag, blocked days and workload; worked examples in `DependencyLagTests` | Pass |
| III | No ownership change | Pass |
| IV | Overdue still means the due date is before today (G-01); working days change thresholds and spreads only | Pass |
| V | Links, removals and holiday changes are logged in the same save | Pass |
| VI | Deliverable links: the PM or the lead of either deliverable's discipline (D-10 by analogy); holidays: Admins | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- A deliverable link is satisfied when the predecessor is Cancelled, or Issued or Accepted once the lag after its
  issue date has passed; until then the successor waits, and is Blocked once its start date arrives, it is due soon,
  it has started, or the predecessor is overdue — the task rule (D-06).
- Links are added and removed, never edited, like task dependencies; changing a lag is remove and add.
- A lag counts working days when the organisation counts them, so "10 days of client review" skips weekends and
  holidays consistently in the waiting check and in D-12.
- A project's calendar is its office's holidays plus the organisation-wide ones. Holiday changes apply from the next
  evaluation of each project (on its next change, or overnight).

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| Deliverable links carry no row version | They are created and removed, never edited, like task dependencies | A version would guard an edit that does not exist |
