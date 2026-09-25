# Implementation Plan: Coordination Surfaces

**Branch**: `007-coordination-surfaces` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

The four screens people work from: the Project Dashboard (the project's home tab), Weekly Coordination with meeting
mode, My Work, and My Staff with supervisor staffing. All four read the derived state from packet 005 and the
notifications from packet 006; none computes a rule of its own.

## Technical Context

As packet 001. No new tables: `project.coordination_day`, `last_coordination_reviewed_at/by` and the state tables
already exist. Endpoints: `GET /projects/{id}/dashboard`, `GET /projects/{id}/coordination`,
`POST /projects/{id}/coordination/reviewed`, `GET /me/work?userId=`, `GET /staff`, `GET /staff/{userId}/assignments`.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | No charts for their own sake, no hours on My Staff (§13.1, §13.19); decision recording in meeting mode waits for packet 008 | Pass |
| II | Every dashboard number is the count of the list query its link opens (`TaskQueries.Apply`, `DeliverableEndpoints.Filter`), so numbers and lists reconcile (SC-004) | Pass |
| III | My Work and My Staff show named owners, blockers and successors | Pass |
| IV | Canonical statuses; lanes and buckets are presentation | Pass |
| V | Mark as reviewed and supervisor staffing are logged with the actor; meeting-mode edits go through the normal endpoints (logged and notified as usual) | Pass |
| VI | My Work of another person: self, supervisor of a direct report, Executive, Admin, or a PM for their own projects only; My Staff for Supervisors (direct reports) and Executives/Admins (all); Restricted projects the viewer cannot see are excluded everywhere | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- "This week" in Weekly Coordination starts on the project's coordination day (Monday if unset); the task list's
  "Due this week" chip keeps a Monday week. Since last review falls back to the last 7 days.
- Blocked work is grouped by blocker on the client from each task's stored blockers, so a task with two causes
  appears under both.
- Meeting mode: larger type, arrow keys step through section headers, inline status/due date/reassignment through
  the shared task actions, which report each saved change to the "Changes made in this meeting" tray (including
  changes completed in a reason dialog); "hide items already discussed" hides rows changed in this session.
- Copy summary is built on the client from the same payload (sections 1, 3, 5, 6, 7 with keys).
- Supervisors may reassign tasks their direct reports own (§8.5.1): `TaskFacts.AssigneeSupervisorId`.
- The digest gains the My staff section for supervisors (FR-016).

## Project Structure

```text
src/Hub.Api/Features/Dashboard.cs, MyWork.cs     (+ Deliverables.Filter, TaskFilter.Of, Digest "staff" section)
tests/Hub.Tests/Api/SurfacesTests.cs
web/src/pages/projects/Dashboard.tsx, Coordination.tsx
web/src/pages/MyWork.tsx, Staff.tsx
```

## Checks

AC-DASH-01..03, AC-WC-01/02/03/05/07, AC-MYW-01..04, AC-ASG-07..09 as API tests; meeting-mode keys and layout in the
browser.

## Complexity Tracking

None.
