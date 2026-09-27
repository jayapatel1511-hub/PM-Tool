# Implementation Plan: Six-View Workspace

**Branch**: `022-six-view-workspace` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

One project scope shared by the workspace views — My projects, all permitted projects, a chosen set, or a user-owned
named workspace — and the views that read it: Home (overview dashboard with a personal layout), Boards (cross-project
four-lane board), Tasks (cross-project list), Timeline (cross-project Gantt), Calendar (packet 023, now scoped), Files
(document-link library) and Team; My Work gains Today, Upcoming, Overdue, Completed and Inbox with the three membership
views. The top bar gains the workspace selector and New Item; every workspace view has view tabs, a plus-tab view
switcher and a share control.

## Technical Context

As packet 001. `workspace`, `workspace_project`, `board_order.workspace_id` and `dashboard_layout` exist from the initial
schema; no migration. New endpoints: `GET/POST /workspaces`, `PATCH/DELETE /workspaces/{id}`,
`PUT /workspaces/{id}/board-order`, `GET /home`, `PUT/DELETE /me/dashboard-layout`, `GET /files`, `GET /team`,
`GET /timeline`. Extended: `GET /tasks` (`projects` scope, `workspaceId` for the owner's manual order, `completedTo`),
`GET /calendar` (`projectIds` takes the same scope values), `GET /projects` (`computedHealth`), `GET /me`
(`capabilities.createTask`), saved-view list types `workspace-tasks`, `workspace-board`, `mywork`.

The scope is one server helper (`Scope.Projects`): `mine` (PM or active member, the project list's own default), `all`,
or ids, always intersected with the caller's visible projects and excluding Archived and Cancelled. Every workspace
endpoint starts from it, so a named set, saved view or shared link can only narrow what a reader sees. In the browser the
URL carries the scope (`ws` or `projects`) and the last one shown is kept per viewer, so switching views keeps it.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | No AI; figures are counts and ratios of existing records; no new statuses or workflow | Pass |
| II | Every Home figure is the count of the list its link opens (same `TaskQueries.Apply` filter, same project query); snapshot totals say they are current; only Upcoming Deadlines follows the range | Pass |
| III | Owners, assignees and roles unchanged; the board and checkbox call the existing guarded transitions | Pass |
| IV | Lanes are presentation groups of canonical statuses (§36.3); On Hold and Cancelled only by explicit filter | Pass |
| V | Work-item changes still go through their own endpoints and logs; workspaces and dashboard layouts are personal settings and are not logged, like saved views and preferences | Pass |
| VI | Scope re-applies `VisibleProjects` on every read; workspaces, layouts and workspace card orders are owner-only (others get 404); project links in Files keep `EditProject`; New Item shows only creatable types | Pass |
| VII | No new infrastructure or dependencies; charts are CSS bars with text | Pass |

## Design notes

- Progress (FR-VIS-03) is the existing `ProgressPct` of the project state: whole-percent floor of Complete over
  non-Cancelled tasks, null ("—") without any; the list already groups Active and Upcoming (Setup).
- On Track counts Green computed health over Active projects with evaluated (Green, Yellow or Red) health; the drill-down
  uses the new `computedHealth` filter so a reported override never counts as Green there.
- Team Members counts distinct active people with an active membership in the scope; the Team view lists the same people.
- A cross-project Gantt builds each project exactly as its own timeline (same code), so arrows never join two projects;
  it draws at most 25 projects and says so beyond that. Milestones open from the Gantt; they move on the project timeline.
- Manual card order: a named workspace keeps its owner's own order (`board_order.workspace_id`); an ad-hoc set offers no
  manual order; the project board keeps the shared project order.
- Inside a project the same Files and Calendar views run with the scope forced to that project (project tabs), and
  Workload opens filtered to it; the workspace tabs are hidden there.
- Completion checkbox and board drops share one hook (`useLaneDrop`): it asks the server which transitions are allowed,
  so review-required tasks cannot be ticked off without review.

## Project Structure

```text
src/Hub.Api/Features/Workspace.cs (Scope, workspaces, home, layout, files, team)
src/Hub.Api/Features/Timeline.cs (shared Build + /timeline), Tasks.cs, TaskQueries.cs, Calendar.cs, Projects.cs, Me.cs, Views.cs
tests/Hub.Tests/Api/WorkspaceTests.cs
web/src/components/hub/workspace.tsx (scope, selector, dialog, view tabs, share), new-item.tsx
web/src/pages/Home.tsx, WorkspaceTasks.tsx (list and board), Gantt.tsx, Files.tsx, WorkspaceTeam.tsx, MyWork.tsx, Calendar.tsx
```

## Checks

AC-VIS-01, 02, 03, 05, 06 and FR-VIS-02/09 as API tests; AC-VIS-07 and the drag, layout and scope behaviour in the browser.

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| `workspace` and `dashboard_layout` rows carry no row version | Each belongs to one person and only that person changes it; last write is what they last chose, as with notification preferences | A version check would only ever conflict with the same person's other tab and protects no one else's data |
