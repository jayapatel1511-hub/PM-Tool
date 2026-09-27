# Implementation Plan: Resource and Workload View

**Branch**: `017-resource-workload-view` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

An eight-week person-by-week grid of estimated remaining hours against capacity, the unestimated count beside the
hours, Over-assigned / Under-assigned / Deadline cluster flags, a drill-down to each person's tasks by project with
reassignment, capacity overrides, export, and the Workload by Employee and by Discipline reports.

## Technical Context

As packet 001. No new tables: capacity is `app_user.weekly_capacity_hours` with the organisation default. The
calculation is a pure module in `Hub.Domain` (`Workload`) with the §12.15 worked examples as unit tests; the API
gathers tasks and people and applies it. Endpoints: `GET /workload`, `GET /workload/export`,
`GET /workload/{userId}/tasks`, `PUT /users/{id}/capacity`; reassignment uses the existing task edit.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | No forecasting beyond 8 weeks, levelling, utilisation targets, leave calendars or subtraction of logged hours (§12.15, §36.8) | Pass |
| II | One stated method, shown in a popover; the unestimated count sits beside every person's hours; rules are pure functions with worked examples | Pass |
| III | Each row is one person; reassignment names the new assignee | Pass |
| IV | Only open tasks count (not Complete, Cancelled or On Hold) on Setup and Active projects | Pass |
| V | Reassignment is the normal task edit: logged with the actor, both people notified, and the PM told when someone other than the PM or lead moves the work | Pass |
| VI | Workload for Admins, Executives, Supervisors and PMs (§8.5.1); Supervisors see their direct reports, PMs the members of projects they manage; hours only from projects the viewer can see, so Restricted work is absent (Workflow 10); capacity set by the person's Supervisor or an Admin | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- Working days are Monday to Friday (`WorkCalendar.Weekdays`); a window with no working day keeps its hours in the
  due week. The window starts at the later of the start date and today; a start after the due date uses the due day.
- Flags always use this week and next, even when the grid starts later (the "Weeks from" filter).
- Project and discipline filters count only that project's or discipline's tasks and show the people who have them;
  supervisor and office filters choose people.
- Rows are grouped by supervisor when more than one appears; default order is this week's load, highest first.

## Project Structure

```text
src/Hub.Domain/Workload.cs; src/Hub.Api/Features/Workload.cs (+ Reports.cs: workload-employee, workload-discipline; Tasks.cs: PM notice)
tests/Hub.Tests/Domain/WorkloadTests.cs, tests/Hub.Tests/Api/WorkloadApiTests.cs
web/src/pages/Workload.tsx
```

## Checks

§12.15 worked examples as domain tests; grid numbers, flags, scope, Restricted exclusion, rebalancing, capacity and
reports as API tests; the grid, drill-down and method popover in the browser.

## Complexity Tracking

None.
