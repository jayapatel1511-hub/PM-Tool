# Tasks: Screen Completeness

Each unchecked slice is one proposed implementation PR. This spec task authorizes none of
them. S01 establishes all seven disabled flags first. Every later slice depends on S01,
retains its part flag off by default, and includes both enabled behaviour and unchanged-off
checks. Existing features stay available when a flag is off.

| Slice | Outcome | Owned implementation boundary | Requirements / acceptance |
|---|---|---|---|
| S01 | [ ] Shared flag foundation | src/Hub.Domain/Flags.cs; server feature guards; web flag selectors | FR-SCR-84 / AC-SCR-84 |
| S02 | [ ] Exact record context | web/src/components/hub/; src/Hub.Api/Features/Tasks.cs and named record readers | FR-SCR-01 / AC-SCR-01 |
| S03 | [ ] Blocking inputs and source uses | web/src/pages/projects/TaskPanel.tsx; Changes.cs; Dependencies.cs | FR-SCR-02 / AC-SCR-02 |
| S04 | [ ] Today and reviewer presentation | web/src/pages/MyWork.tsx; projects/TaskPanel.tsx; review picker | FR-SCR-03 / AC-SCR-03, FR-SCR-04 / AC-SCR-04 |
| S05 | [ ] Blocker-first delivery | web/src/pages/projects/Submissions.tsx; Readiness.tsx; corresponding readers | FR-SCR-05 / AC-SCR-05, FR-SCR-06 / AC-SCR-06 |
| S06 | [ ] Home count context | web/src/pages/Home.tsx; src/Hub.Api/Features/Workspace.cs | FR-SCR-08 / AC-SCR-08, FR-SCR-09 / AC-SCR-09, FR-SCR-12 / AC-SCR-12 |
| S07 | [ ] Home layout and charts | web/src/pages/Home.tsx; existing dashboard widgets | FR-SCR-07 / AC-SCR-07 |
| S08 | [ ] Scoped Home trends | Workspace.cs; Home.tsx; snapshot model/additive migration | FR-SCR-10 / AC-SCR-10, FR-SCR-14 / AC-SCR-14 |
| S09 | [ ] Home capacity and deadline notes | Home.tsx; planner read adapter | FR-SCR-11 / AC-SCR-11, FR-SCR-13 / AC-SCR-13 |
| S10 | [ ] Work activity and seen state | MyWork.tsx; WorkspaceTasks.tsx; current-user seen marker | FR-SCR-15 / AC-SCR-15, FR-SCR-16 / AC-SCR-16 |
| S11 | [ ] Task grouping and columns | WorkspaceTasks.tsx; projects/Tasks.tsx | FR-SCR-17 / AC-SCR-17, FR-SCR-18 / AC-SCR-18 |
| S12 | [ ] Cross-project bulk and reversal | TaskQueries.cs; Tasks.cs; bulk command UI; activity writer | FR-SCR-19 / AC-SCR-19, FR-SCR-20 / AC-SCR-20 |
| S13 | [ ] Board headers and review cards | projects/Board.tsx; WorkspaceTasks.tsx board view | FR-SCR-21 / AC-SCR-21, FR-SCR-22 / AC-SCR-22 |
| S14 | [ ] Timeline saved views | Gantt.tsx; projects/Timeline.tsx; Views.cs | FR-SCR-23 / AC-SCR-23 |
| S15 | [ ] Calendar layer and rescheduling | Calendar.tsx; Calendar.cs; existing move preview | FR-SCR-24 / AC-SCR-24, FR-SCR-25 / AC-SCR-25 |
| S16 | [ ] Task sections and contribution marks | projects/TaskPanel.tsx; Tasks.cs; Activity.cs | FR-SCR-26 / AC-SCR-26, FR-SCR-27 / AC-SCR-27 |
| S17 | [ ] Project staffing cue | projects/ProjectList.tsx; Projects.cs; scoped staffing adapter | FR-SCR-28 / AC-SCR-28 |
| S18 | [ ] Project filters and peek | projects/ProjectList.tsx; Projects.cs | FR-SCR-29 / AC-SCR-29, FR-SCR-30 / AC-SCR-30, FR-SCR-31 / AC-SCR-31 |
| S19 | [ ] Project progress and bulk | projects/ProjectList.tsx; Projects.cs; Activity.cs | FR-SCR-32 / AC-SCR-32, FR-SCR-33 / AC-SCR-33 |
| S20 | [ ] Portfolio staffing and change summary | Portfolio.tsx; Portfolio.cs | FR-SCR-34 / AC-SCR-34, FR-SCR-35 / AC-SCR-35 |
| S21 | [ ] Portfolio print | Portfolio.tsx; scoped print stylesheet | FR-SCR-36 / AC-SCR-36 |
| S22 | [ ] Dashboard staffing and attention | projects/Dashboard.tsx; Dashboard.cs | FR-SCR-37 / AC-SCR-37, FR-SCR-38 / AC-SCR-38 |
| S23 | [ ] Report parameters and catalogue | Reports.tsx; Reports.cs; Views.cs | FR-SCR-39 / AC-SCR-39, FR-SCR-40 / AC-SCR-40, FR-SCR-42 / AC-SCR-42 |
| S24 | [ ] Report chart | Reports.tsx; existing report result adapter | FR-SCR-41 / AC-SCR-41 |
| S25 | [ ] Report FTE and month unit | Reports.cs; Reports.tsx; named exact-arithmetic tests | FR-SCR-43 / AC-SCR-43 |
| S26 | [ ] Verified Find room | Planner.tsx; Planning.cs; scoped availability reader | FR-SCR-44 / AC-SCR-44 |
| S27 | [ ] Planner headers and zoom | Planner.tsx; projects/Allocations.tsx; scoped deadline adapter | FR-SCR-45 / AC-SCR-45, FR-SCR-46 / AC-SCR-46 |
| S28 | [ ] Workload discipline context | Workload.tsx; Workload.cs | FR-SCR-47 / AC-SCR-47, FR-SCR-48 / AC-SCR-48 |
| S29 | [ ] Optional person facts | Team.cs; Admin.cs; additive person facts migration; Staff.tsx | FR-SCR-49 / AC-SCR-49, FR-SCR-50 / AC-SCR-50 |
| S30 | [ ] Allocation preview and comparison | projects/Allocations.tsx; Allocations.cs; allocation preview reader | FR-SCR-51 / AC-SCR-51, FR-SCR-52 / AC-SCR-52 |
| S31 | [ ] Shift allocation dates | projects/Allocations.tsx; Allocations.cs; activity writer | FR-SCR-53 / AC-SCR-53 |
| S32 | [ ] Time week grid | Time.tsx; Time.cs; availability adapter | FR-SCR-54 / AC-SCR-54, FR-SCR-55 / AC-SCR-55 |
| S33 | [ ] Deliverable issue fields | projects/Deliverables.tsx; Deliverables.cs; additive issue metadata | FR-SCR-56 / AC-SCR-56, FR-SCR-57 / AC-SCR-57, FR-SCR-58 / AC-SCR-58 |
| S34 | [ ] Drawing-list import and client export | Deliverables.cs; ListExports.cs; projects/Deliverables.tsx | FR-SCR-59 / AC-SCR-59, FR-SCR-60 / AC-SCR-60 |
| S35 | [ ] Review response and external provenance | projects/Reviews.tsx; Reviews.cs; additive response metadata | FR-SCR-61 / AC-SCR-61, FR-SCR-62 / AC-SCR-62 |
| S36 | [ ] Review discipline summaries/defaults | Reviews.cs; Reviews.tsx; Admin.cs deliverable type settings | FR-SCR-63 / AC-SCR-63, FR-SCR-64 / AC-SCR-64 |
| S37 | [ ] Date reason and sequence cues | Milestones.cs; Deliverables.cs; projects/Milestones.tsx | FR-SCR-65 / AC-SCR-65, FR-SCR-66 / AC-SCR-66 |
| S38 | [ ] Coordination consequences | projects/Coordination.tsx; DisciplineCoordination.cs | FR-SCR-67 / AC-SCR-67, FR-SCR-68 / AC-SCR-68 |
| S39 | [ ] Decision metadata | projects/Decisions.tsx; Decisions.cs; additive metadata | FR-SCR-69 / AC-SCR-69 |
| S40 | [ ] Template risk seed | Templates.cs; template snapshot instantiation | FR-SCR-70 / AC-SCR-70 |
| S41 | [ ] Issue root cause | projects/Registers.tsx; Registers.cs; organisation vocabulary | FR-SCR-71 / AC-SCR-71 |
| S42 | [ ] Team group and role strip | WorkspaceTeam.tsx; projects/Team.tsx; Team.cs | FR-SCR-72 / AC-SCR-72, FR-SCR-73 / AC-SCR-73, FR-SCR-75 / AC-SCR-75 |
| S43 | [ ] Narrow person panel and responsibility | web/src/components/hub/ person panel; Team.cs | FR-SCR-74 / AC-SCR-74, FR-SCR-76 / AC-SCR-76 |
| S44 | [ ] File-link context | Files.tsx; DocumentLinks.cs | FR-SCR-77 / AC-SCR-77 |
| S45 | [ ] Notification row actions | Notifications.tsx; Notifications.cs | FR-SCR-78 / AC-SCR-78 |
| S46 | [ ] Search filters and recents | Search.tsx; Search.cs; browser recent-search storage | FR-SCR-79 / AC-SCR-79 |
| S47 | [ ] Notice preferences and away hold | Preferences.tsx; Notifications.cs; digest/outbox dispatch | FR-SCR-80 / AC-SCR-80 |
| S48 | [ ] Template version provenance | Templates.tsx; Templates.cs; project settings provenance | FR-SCR-81 / AC-SCR-81 |
| S49 | [ ] Admin person scope and role explainer | admin/Admin.tsx; Admin.cs; canonical permission description adapter | FR-SCR-82 / AC-SCR-82, FR-SCR-83 / AC-SCR-83 |

Additional dependencies: S03/S04/S05 use S02’s context contract; S07 uses S06 counts;
S08 uses the exact same scope contract; S27 uses S26’s header inputs; S43 uses S29
person facts. Person-panel fields whose owning flag is off are omitted. S35 establishes
review response metadata before S36 metrics. Freeze exact file paths when a slice is
authorized; these are bounded feature lanes, not permission to edit every file in a folder.

Each PR includes server authorization/refusal, concurrency/activity where mutable,
unknown/partial-view and named numeric AC checks. Only additive nullable/schema changes
listed in the slice may enter its migration. Schema-lane changes run sequentially.
One owner per shared file; coordinate overlapping slices instead of writing concurrently.
Changes to canonical §10 require an explicit amendment before implementation.

All prototype/build-only gaps in [build-gaps.md](build-gaps.md) retain their existing packet
ownership. They are linked prerequisites or existing acceptance work, not extra features
inside a slice. No slice turns on a pilot flag, closes a decision or claims acceptance.
