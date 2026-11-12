# Tasks: Screen Completeness

Each unchecked slice is one proposed implementation PR. This spec task authorizes none of
them. S01 establishes all seven disabled flags first. Every later slice depends on S01,
retains its part flag off by default, and includes both enabled behaviour and unchanged-off
checks. Existing features stay available when a flag is off.

| Slice | Outcome | Owned implementation boundary | Requirements / acceptance |
|---|---|---|---|
| S01 | [x] Shared flag foundation | src/Hub.Domain/Flags.cs; server feature guards; web flag selectors | FR-SCR-84 / AC-SCR-84 |
| S02 | [x] Exact record context | web/src/components/hub/; src/Hub.Api/Features/Tasks.cs and named record readers | FR-SCR-01 / AC-SCR-01 |
| S03 | [x] Blocking inputs and source uses | web/src/pages/projects/TaskPanel.tsx; Changes.cs; Dependencies.cs | FR-SCR-02 / AC-SCR-02 |
| S04 | [x] Today and reviewer presentation | web/src/pages/MyWork.tsx; projects/TaskPanel.tsx; review picker | FR-SCR-03 / AC-SCR-03, FR-SCR-04 / AC-SCR-04 |
| S05 | [x] Blocker-first delivery | web/src/pages/projects/Submissions.tsx; Readiness.tsx; corresponding readers | FR-SCR-05 / AC-SCR-05, FR-SCR-06 / AC-SCR-06 |
| S06 | [x] Home count context | web/src/pages/Home.tsx; src/Hub.Api/Features/Workspace.cs | FR-SCR-08 / AC-SCR-08, FR-SCR-09 / AC-SCR-09, FR-SCR-12 / AC-SCR-12 |
| S07 | [x] Home layout and charts | web/src/pages/Home.tsx; existing dashboard widgets | FR-SCR-07 / AC-SCR-07 |
| S08 | [x] Scoped Home trends | Workspace.cs; Home.tsx; snapshot model/additive migration | FR-SCR-10 / AC-SCR-10, FR-SCR-14 / AC-SCR-14 |
| S09 | [x] Home capacity and deadline notes | Home.tsx; planner read adapter | FR-SCR-11 / AC-SCR-11, FR-SCR-13 / AC-SCR-13 |
| S10 | [x] Work activity and seen state | MyWork.tsx; WorkspaceTasks.tsx; current-user seen marker | FR-SCR-15 / AC-SCR-15, FR-SCR-16 / AC-SCR-16 |
| S11 | [x] Task grouping and columns | WorkspaceTasks.tsx; projects/Tasks.tsx | FR-SCR-17 / AC-SCR-17, FR-SCR-18 / AC-SCR-18 |
| S12 | [x] Cross-project bulk and reversal | TaskQueries.cs; Tasks.cs; bulk command UI; activity writer | FR-SCR-19 / AC-SCR-19, FR-SCR-20 / AC-SCR-20 |
| S13 | [x] Board headers and review cards | projects/Board.tsx; WorkspaceTasks.tsx board view | FR-SCR-21 / AC-SCR-21, FR-SCR-22 / AC-SCR-22 |
| S14 | [x] Timeline saved views | Gantt.tsx; projects/Timeline.tsx; Views.cs | FR-SCR-23 / AC-SCR-23 |
| S15 | [x] Calendar layer and rescheduling | Calendar.tsx; Calendar.cs; existing move preview | FR-SCR-24 / AC-SCR-24, FR-SCR-25 / AC-SCR-25 |
| S16 | [x] Task sections and contribution marks | projects/TaskPanel.tsx; Tasks.cs; Activity.cs | FR-SCR-26 / AC-SCR-26, FR-SCR-27 / AC-SCR-27 |
| S17 | [x] Project staffing cue | projects/ProjectList.tsx; Projects.cs; scoped staffing adapter | FR-SCR-28 / AC-SCR-28 |
| S18 | [x] Project filters and peek | projects/ProjectList.tsx; Projects.cs | FR-SCR-29 / AC-SCR-29, FR-SCR-30 / AC-SCR-30, FR-SCR-31 / AC-SCR-31 |
| S19 | [x] Project progress and bulk | projects/ProjectList.tsx; Projects.cs; Activity.cs | FR-SCR-32 / AC-SCR-32, FR-SCR-33 / AC-SCR-33 |
| S20 | [x] Portfolio staffing and change summary | Portfolio.tsx; Portfolio.cs | FR-SCR-34 / AC-SCR-34, FR-SCR-35 / AC-SCR-35 |
| S21 | [x] Portfolio print | Portfolio.tsx; scoped print stylesheet | FR-SCR-36 / AC-SCR-36 |
| S22 | [x] Dashboard staffing and attention | projects/Dashboard.tsx; Dashboard.cs | FR-SCR-37 / AC-SCR-37, FR-SCR-38 / AC-SCR-38 |
| S23 | [x] Report parameters and catalogue | Reports.tsx; Reports.cs; Views.cs | FR-SCR-39 / AC-SCR-39, FR-SCR-40 / AC-SCR-40, FR-SCR-42 / AC-SCR-42 |
| S24 | [x] Report chart | Reports.tsx; existing report result adapter | FR-SCR-41 / AC-SCR-41 |
| S25 | [x] Report FTE and month unit | Reports.cs; Reports.tsx; named exact-arithmetic tests | FR-SCR-43 / AC-SCR-43 |
| S26 | [x] Verified Find room | Planner.tsx; Planning.cs; scoped availability reader | FR-SCR-44 / AC-SCR-44 |
| S27 | [x] Planner headers and zoom | Planner.tsx; projects/Allocations.tsx; scoped deadline adapter | FR-SCR-45 / AC-SCR-45, FR-SCR-46 / AC-SCR-46 |
| S28 | [x] Workload discipline context | Workload.tsx; Workload.cs | FR-SCR-47 / AC-SCR-47, FR-SCR-48 / AC-SCR-48 |
| S29 | [x] Optional person facts | Team.cs; Admin.cs; additive person facts migration; Staff.tsx | FR-SCR-49 / AC-SCR-49, FR-SCR-50 / AC-SCR-50 |
| S30 | [x] Allocation preview and comparison | projects/Allocations.tsx; Allocations.cs; allocation preview reader | FR-SCR-51 / AC-SCR-51, FR-SCR-52 / AC-SCR-52 |
| S31 | [x] Shift allocation dates | projects/Allocations.tsx; Allocations.cs; activity writer | FR-SCR-53 / AC-SCR-53 |
| S32 | [x] Time week grid | Time.tsx; Time.cs; availability adapter | FR-SCR-54 / AC-SCR-54, FR-SCR-55 / AC-SCR-55 |
| S33 | [x] Deliverable issue fields | projects/Deliverables.tsx; Deliverables.cs; additive issue metadata | FR-SCR-56 / AC-SCR-56, FR-SCR-57 / AC-SCR-57, FR-SCR-58 / AC-SCR-58 |
| S34 | [x] Drawing-list import and client export | Deliverables.cs; ListExports.cs; projects/Deliverables.tsx | FR-SCR-59 / AC-SCR-59, FR-SCR-60 / AC-SCR-60 |
| S35 | [x] Review response and external provenance | projects/Reviews.tsx; Reviews.cs; additive response metadata | FR-SCR-61 / AC-SCR-61, FR-SCR-62 / AC-SCR-62 |
| S36 | [x] Review discipline summaries/defaults | Reviews.cs; Reviews.tsx; Admin.cs deliverable type settings | FR-SCR-63 / AC-SCR-63, FR-SCR-64 / AC-SCR-64 |
| S37 | [x] Date reason and sequence cues | Milestones.cs; Deliverables.cs; projects/Milestones.tsx | FR-SCR-65 / AC-SCR-65, FR-SCR-66 / AC-SCR-66 |
| S38 | [x] Coordination consequences | projects/Coordination.tsx; DisciplineCoordination.cs | FR-SCR-67 / AC-SCR-67, FR-SCR-68 / AC-SCR-68 |
| S39 | [x] Decision metadata | projects/Decisions.tsx; Decisions.cs; additive metadata | FR-SCR-69 / AC-SCR-69 |
| S40 | [x] Template risk seed | Templates.cs; template snapshot instantiation | FR-SCR-70 / AC-SCR-70 |
| S41 | [x] Issue root cause | projects/Registers.tsx; Registers.cs; organisation vocabulary | FR-SCR-71 / AC-SCR-71 |
| S42 | [x] Team group and role strip | WorkspaceTeam.tsx; projects/Team.tsx; Team.cs | FR-SCR-72 / AC-SCR-72, FR-SCR-73 / AC-SCR-73, FR-SCR-75 / AC-SCR-75 |
| S43 | [x] Narrow person panel and responsibility | web/src/components/hub/ person panel; Team.cs | FR-SCR-74 / AC-SCR-74, FR-SCR-76 / AC-SCR-76 |
| S44 | [x] File-link context | Files.tsx; DocumentLinks.cs | FR-SCR-77 / AC-SCR-77 |
| S45 | [x] Notification row actions | Notifications.tsx; Notifications.cs | FR-SCR-78 / AC-SCR-78 |
| S46 | [x] Search filters and recents | Search.tsx; Search.cs; browser recent-search storage | FR-SCR-79 / AC-SCR-79 |
| S47 | [x] Notice preferences and away hold | Preferences.tsx; Notifications.cs; digest/outbox dispatch | FR-SCR-80 / AC-SCR-80 |
| S48 | [x] Template version provenance | Templates.tsx; Templates.cs; project settings provenance | FR-SCR-81 / AC-SCR-81 |
| S49 | [x] Admin person scope and role explainer | admin/Admin.tsx; Admin.cs; canonical permission description adapter | FR-SCR-82 / AC-SCR-82, FR-SCR-83 / AC-SCR-83 |
| S50 | [x] Ratchet restoration (053-D24), before any 052/053 flag goes on in pilot | the 052/053 files named in verification.md checkpoint 36 | FR-PLAT-20 counters back to the 039–051 baseline |

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


Local checkpoint thirteen checks S02, S26 and S28 after independent source review and
combined API/browser verification. See verification.md and 052 build-progress.md for
exact evidence and limits. S07 remains partial pending the capacity widget. Checked
slices are not a hosted/human acceptance or flag-activation claim.

Local checkpoint fourteen checks S07, S17, S18 and S27 after independent source review
and combined API/browser verification. S09 has its capacity requirement verified but
still needs deadline notes; S03 still needs normal-command readiness-source integration.
See verification.md and 052 build-progress.md for evidence. Checked slices denote local
implementation/verification, not hosted/human acceptance or flag activation.


Local checkpoint fifteen checks S03/S04/S08/S09/S19/S20/S21/S30 after independent source
review and API/browser/reflow/print verification. See verification.md and 052 build-progress.md
for raw evidence and limits. Checked slices are local implementation/verification, not
hosted/human acceptance, activation approval or completion of the remaining packet.


Local checkpoint eighteen checks S32 after final independent source review, real save/calendar joins, exact-once/stale/retry browser cases and settled desktop/phone layout inspection. See verification.md for evidence and limits. S10/S23 remain partial. Checked means local implementation/verification, not hosted/human acceptance or activation.


Local checkpoint nineteen checks S23 after all twelve applicable report input forms, catalogue/views, exact scoped data/paging, saved/export maps, return journeys and settled final layout passed. S11/S29 remain partial for the explicitly recorded columns/picker gaps. See verification.md; checked denotes local implementation/verification, not hosted/human acceptance or activation.

Local checkpoint twenty checks S11, S25 and S29 after independent source/acceptance review,133 distinct .NET cases,12 Chrome suites and joined real normal-command/API/browser evidence.25 slices are locally checked. SSE transport and physical native-popup limits are recorded in evidence/checkpoint20-findings.md; universal052/hosted/human/pilot acceptance remains unproven.

Local checkpoint twenty-one checks S13, S24 and S42 after independent source/acceptance review,120 distinct .NET cases,11 Chrome suites and real normal-command/API/browser joins.28 slices are locally checked. The compact project-header and actual grouped-child405px criterion also pass, while broader052 gates remain open. See evidence/checkpoint21-findings.md for exact repairs, final manifests, runtime cleanup and SSE/native-device limits. Checked denotes local verification, not hosted/human acceptance, pilot activation or full-goal completion.

Local checkpoint twenty-two checks S14 and S33 after independent source review,126 distinct .NET cases,16 Node cache cases,11 latest focused/adjacent Chrome suites and22 real API/browser join steps at1440/375.30 of49 slices are locally checked. S43 remains open for the global known-ID passive-name inventory; its core panel and note are verified. See evidence/checkpoint22-findings.md for source hashes, preserved failures, actual runtime cleanup and acceptance limits. All unchecked buildable slices remain in the active goal; no pilot, decision closure or hosted/human acceptance is implied.

Local checkpoint twenty-three checks S10 after independent source and acceptance review,
117 distinct .NET checks,18 Node cache checks,four focused Chrome suites and actual
QA23 UI/API/database joins.31 of49 slices are locally checked. S43 remains open for
remaining global names; S34 has verified client export but still requires atomic
CSV/XLSX import. See evidence/checkpoint23-findings.md for exact evidence and limits.
Checked means local implementation/verification; decisions, pilot, hosted and human
acceptance remain separate.


Local checkpoint twenty-four checks S34 and S43 after independent source and local
acceptance review,137 distinct .NET cases,7 Node guard checks,four focused Chrome
suites and actual QA24 Chrome/API/SQL joins.33 of49 slices are locally checked.
The real UI workbook creates40 records and40 activity entries; final known-User
Search/Files/Pickup paths and source return are verified. See
evidence/checkpoint24-findings.md for exact proof and preserved failures. QA24
cleanup is blocked by automatic approval review pending explicit confirmation;
API4323/databasehub_qa_checkpoint24 are preserved. All other unchecked052/053
slices remain required; no hosted/human/pilot/formal Accepted claim.


Local checkpoint27 checks S44 after independent source/migration/coverage review,103 distinct API passes,27 Node checks, final feature and adjacent Chrome suites, desktop/375px visual review and final parity/spec checks.35/49 slices are locally checked; no hosted/human/formal Accepted claim. See evidence/checkpoint27-findings.md.

Local checkpoint thirty-six (Claude, 2026-11-04) checks S12, S37 and S49: 49 of 49 slices are locally checked. S12 Undo and S37 date
reasons are built to the 053-D9 and 053-D13 defaults, which stay Open for Jay (defaults apply while work ships dark). S49 includes the
053-D23 report-export rule Jay confirmed on 2026-11-03. See verification.md "Checkpoint 36". Checked means local implementation and
verification, not hosted or human acceptance, flag activation or a decision closed.
