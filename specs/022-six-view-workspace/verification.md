# Verification: Six-View Workspace

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (274/274) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 97.0 % branches, services 89.5 % lines |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |
| `python3 tools/trace_spec.py --check` | Pass |

`WorkspaceTests` (8, all pass):

| Test | Covers |
|---|---|
| A named workspace keeps its scope and never discloses a restricted project | US1-1, US1-2, FR-VIS-02: a restricted project cannot join Alex's workspace; names are unique per person and workspaces personal; a scope naming the restricted project adds nothing to tasks, Home figures, deadlines, charts, calendar, timeline, files or team; when Alex loses access to a workspace project it is dropped and counted as hidden, never named |
| Projects group by lifecycle with owner, due date and derived progress | AC-VIS-01: 5 Active and 3 Setup; PM as owner; target completion as due, none shown empty; progress 33 % for 1 Complete of 3 non-Cancelled; "—" with no tasks; priority defaults to Medium |
| The cross-project board uses four lanes, guards review and keeps a personal order | AC-VIS-02: three projects in one workspace land in To Do, In Progress and Review with project labels; Revision Required stays in Review; Complete without review refused; the workspace's order is its owner's and leaves project orders alone; others' workspace ids are ignored; cards outside the workspace refused; deleting the workspace removes its order |
| The gantt shows both project groups with bars, diamonds and only each project's arrows | AC-VIS-03: two project groups with dated tasks, the milestone and one arrow inside project A, none across projects; undated tasks come back for Unscheduled; no save, no change |
| Home figures reconcile with the lists they open and the range moves only deadlines | AC-VIS-05, SC-002: Active Projects, Open and Overdue Tasks, each status bucket, each project's total, On Track (1 of 2 evaluated, 50 %) and Team Members (4, each once) equal their lists; a wider range adds deadlines and leaves every figure unchanged; an inverted range refused; no evaluated project gives "—" |
| Each person arranges their own dashboard and can restore the default | FR-VIS-07: order and hidden state saved per person, unknown widgets dropped, duplicates ignored, missing ones appended; another person keeps the default; restore |
| Created by me and assigned to me have distinct membership | AC-VIS-06: Alex's task for Jill is in Alex's Created by Me only, in Jill's Assigned to Me and My Tasks and in her Today; a collaborator sees it in My Tasks only; Complete without review refused; Completed with its date range |
| Files group links by project and item and team lists active members | FR-VIS-09, FR-VIS-01: project, deliverable and task links grouped in that order; network folder flagged for Copy path; search and type filter; Add link only for the PM; Team shows roles and led disciplines and leaves out inactive people |

## Manual checks (browser pane, as Alex)

| Check | Result |
|---|---|
| Home: tiles, Tasks by Status, Tasks by Project and Upcoming Deadlines; each link carries the scope and filter; Open Tasks (7) opens a list of 7 | Pass |
| Board: four lanes with counts, project label and exact review status on cards; manual order offered only for a named workspace | Pass |
| New workspace "Atlantic Civil" from the selector: choose a project, name, save; the URL and selector switch to it | Pass |
| Timeline tab keeps the workspace; the Gantt shows the project group, milestone diamonds, deliverable and task bars, arrows, baseline and overdue marks, today line | Pass |
| Drag a movable bar by 7 days: the preview shows old and new start and due dates; Cancel leaves the dates as they were | Pass |
| Home opened from the rail (no parameters) still shows Atlantic Civil | Pass |
| Edit Dashboard: move Tasks by Project up, hide Upcoming Deadlines, reload (kept), Restore default | Pass |
| My Work Upcoming: ticking the review-required T0003 is refused with "requires review: it is completed when Marc Dubois approves it"; it stays unticked | Pass |
| My Work Inbox lists the unread notifications | Pass |
| Files: project SharePoint link opens in a new tab; the deliverable's network folder shows Copy path; no upload anywhere | Pass |
| Team: 5 people with roles and leads, matching Home's Team Members | Pass |
| Copy link: the link keeps the view, the project ids and the workspace | Pass |
| New Item offers only Event to Alex (he cannot create projects, and has no discipline to create tasks in) | Pass |
| Tablet width (768 px): every navigation item — Home, My Work, Boards, Projects, Tasks, Calendar, Files, Time, Reports, Team, Notifications — opens its view | Pass |
| Projects drill-down from On Track shows its chosen-projects and computed-health filters as removable chips | Pass |
| A saved view on the workspace board keeps `ws` and `projects` | Pass |
| Project tabs: Files and Calendar show project 2026-0417 only (its two links; its deadlines and events in Agenda), without the workspace tabs | Pass |

## Defects found and fixed while verifying

- Home's chart reused the board's "Done (14 days)" label although it counts every Complete task; it now says Done.
- A refused tick on a review-required task said it "cannot move to Done (14 days)"; board drops and the checkbox now say
  the task is completed when its reviewer approves it.
- The workspace dialog's empty project picker read "All my visible projects"; it now says Choose projects.

## Decisions

- The default scope is My projects (PM or active member), the same default as the project list; All permitted projects
  and named workspaces are one click away.
- A shared link names its projects explicitly (and the workspace for its owner); "All permitted projects" stays relative
  to each reader.
- Milestones on the cross-project Gantt open their panel; their dates move from the project timeline, where the milestone
  rules and reasons live.
