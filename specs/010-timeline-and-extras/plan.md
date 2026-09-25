# Implementation Plan: Timeline and Extras

**Branch**: `010-timeline-and-extras` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

A read-only project timeline (milestone lane, deliverable bars by discipline, today, start and target lines, hatched
overdue segments, hollow original-date markers); copying another project's structure when creating a project; and a
"Reassign work" screen that lists everything one person owns across projects and reassigns it in bulk. The milestone
cascade (US2, AC-MS-05) was built and tested with packet 003's milestone date change; this packet adds the E-09 check.

## Technical Context

As packet 001. No new tables. The timeline reads the existing milestone and deliverable lists. Copying runs as an
`IProjectCreateHook` inside the create transaction, taking item keys in blocks (`Keys.Reserve`). Endpoints:
`GET /users/{id}/open-work`, `POST /users/{id}/reassign`; `POST /projects` accepts `copyFromProjectId`.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | The timeline is read-only (dragging and baselines are packet 018); no scheduling engine | Pass |
| II | Bars and markers come from the same lists and derived state as the registers | Pass |
| III | Copies are unassigned by design (assignments are never copied); reassignment names one new owner per item | Pass |
| IV | Copied items start Not Started; canonical statuses throughout | Pass |
| V | Copies are logged as "Copied" with the source; every reassignment is logged with the reason "Work reassigned from …", notifies the new owner and tells each affected PM once | Pass |
| VI | Copy only from a project the creator can see (otherwise refused, nothing created); Reassign work for Admins, and Supervisors for direct reports only; only projects the caller can see | Pass |
| VII | No new infrastructure or dependencies; the @kibo-ui Gantt was reviewed and not used (drag-first, no milestone lane, hatching or original-date markers, four new dependencies), as §12.16's implementation note prefers a purpose-built component | Pass |

## Design notes

- Bars run from start date (or creation date) to due date; deliverables without a due date are listed under
  "Unscheduled". Zoom is week, month or quarter; filters, zoom and scope live in the URL; print uses the browser.
- Milestone labels are packed into up to three rows so they never overlap, and truncate with the full name on hover.
- Copy structure: active disciplines (no leads), milestones without dates, non-cancelled deliverables and tasks
  without people or dates (keeping type, discipline, milestone and deliverable links, priority, review flag and
  estimates), and the dependencies between copied items. Disciplines already chosen in the create form are not
  duplicated.
- Reassign work covers task assignee and reviewer, deliverable owner and reviewer, decision owner and Discipline
  Lead roles on projects that are not Archived or Cancelled. A reviewer who would review their own task is skipped
  with the reason (R-02). New owners join the team as usual.

## Project Structure

```text
src/Hub.Api/Features/Extras.cs (CopyStructureHook, ReassignEndpoints), Infrastructure/Keys.cs (Reserve)
tests/Hub.Tests/Api/ExtrasTests.cs
web/src/pages/projects/Timeline.tsx, CopyStructure.tsx; web/src/pages/ReassignWork.tsx (+ links from Admin Users and My Staff)
```

## Checks

§27.1 / SC-003, FR-ADM-02 / E-01 / §25.7 and E-09 as API tests; the timeline, copy source and Reassign work screen in
the browser.

## Complexity Tracking

None.
