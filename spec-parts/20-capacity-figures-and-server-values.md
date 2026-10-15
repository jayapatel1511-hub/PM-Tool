## 45. Capacity Figures and Server Values Amendment

Approved scope: on 2026-10-14 Jay approved the build-approach report (kept outside the repository) and directed building all of it. Features are fixed and additive only: nothing in this section removes, hides, narrows or freezes a feature, screen, route, report, export, setting or rule, and no existing feature is put behind a flag. Packet 041 delivers this section. It covers the report's server-computed values and its roadmap items 3, 4, 5 and 15. Every capacity figure says what it counts and when it is incomplete. Workload's load bands and thresholds move into Hub.Domain and organisation settings. Client copies of permission and validation rules give way to values the server computes. An edited Weekly Planner row stays on screen. Where an addition touches existing behaviour, today's behaviour is the default: the new settings default to today's thresholds, both free-hours figures keep their values, the new names and wording show only with `flag.figure_names` on, and a request without the new parameter returns today's rows.

Where this section adds to §8.10 (open-role permissions), §10.4 (organisation settings), §12.15 and §13.11 (Workload), §39 (PLN-15 and AC-PLN-12), §40 (FR-ROLE-01, FR-ROLE-02 and FR-PICK-02) and §42 (FR-SUP-01 and FR-SUP-03), this section governs. It changes no calculation of §37.6, §39 or §42 and no export. Any later screen that shows either free-hours figure uses its §45.3 name and basis.

### 45.1 Scope and boundary

Part A, capacity figures: honest free hours under partial visibility (§45.2), one name and one basis for each free-hours figure (§45.3), load bands from the server (§45.4) and Planner rows that stay after an edit (§45.7). Part B, server values: permission flags in place of client role checks (§45.5), one copy of each duplicated rule (§45.6) and a guard against new copies (§45.8).

Outside this amendment: merging the Planner and Workload figures into one, which would reverse AC-PLN-11; any new capacity rule, indicator or threshold beyond the three settings of §45.4; showing hidden hours in any form, including a total of hidden work; a per-person partial rule on Workload; load bands, uncommitted hours or kept rows in any export; removing or renaming existing response fields, including `isPm`, `leadOf` and the unused threshold fields on `/me`; and the typed contract, registry and ratchet of §44, which this section uses when they exist.

### 45.2 Honest free hours under partial visibility

- **FR-FIG-01.** In the Weekly Planner grid, each person whose totals are partial under PLN-15, as corrected by §42 (hidden planning entries or Confirmed allocations on projects the viewer cannot view, overlapping the displayed weeks), MUST carry the "Partial view" label on their own row (behind `flag.figure_names`, FR-FIG-04), with the PLN-15 text as its tooltip and accessible description. The page notice shown today stays. Each remaining capacity figure for that person MUST read as an upper bound, "at most N h", in the cell, its accessible name and the contribution list, because hidden work can only lower it. The value, sort order, indicators and export are unchanged. The client reads the server's per-person `partial` flag (`src/Hub.Api/Features/Planning.cs:208-221, 285-288`) and works out nothing about hidden work itself.

- **FR-FIG-02.** A person's free hours MUST come only from readers that apply a partial rule. The planner grid read applies PLN-15, and the person picker (FR-PICK-01, FR-PICK-02) and My Staff (FR-SUP-02, FR-SUP-03) reuse it. Workload keeps today's scope rule: every row is partial for a viewer who cannot see every restricted project, or under a project filter (`src/Hub.Api/Features/Workload.cs:183`), and a partial row never shows a non-negative uncommitted figure (§45.3). None of them returns a hidden project's identity, detail or hours (FR-PLN-19, FR-CAP-06). One API test pins every reader to one fixture.

### 45.3 One name and one basis for each free-hours figure

- **FR-FIG-03.** The two free-hours figures MUST keep their separate rules and values (FR-PLN-16, AC-PLN-11) and MUST NOT share a name.
  - Remaining capacity (PLN-09, §10.9) is capacity − Confirmed − Expected from the Weekly Planner. It keeps its names: "Remaining capacity" in the Weekly Planner and its contribution list, and "N h free" in the person picker and My Staff (FR-PICK-02, FR-SUP-01).
  - Uncommitted is the new name of the Workload figure: available capacity − committed load (FR-CAP-04). Workload labels it "Remaining N h" today (`web/src/pages/Workload.tsx:288, 303-304`, key `workload.remaining`); with `flag.figure_names` on it becomes "Uncommitted N h". The server MUST return it per cell as `uncommitted`, computed in Hub.Domain, and MUST return null when the row is partial and the value is not negative, which is today's display rule (`Workload.tsx:304`). The client stops computing available − committed.

- **FR-FIG-04.** Each figure MUST carry a one-line basis wherever it is shown.
  - Weekly Planner: the existing line above the grid (`planner.remainingBasis`, `web/src/components/planner/PlannerGrid.tsx:46`), unchanged.
  - Workload: one line above the grid, linked to the grid as its accessible description in the same way: "Uncommitted = available − committed load (confirmed reservations and forecast task work). Planning entries are not counted." The Method popover's committed-load step (`workload.method5`) names the figure.
  - Person picker and My Staff: the figure's tooltip and accessible description, "Remaining capacity in the Weekly Planner: capacity − Confirmed − Expected" (`web/src/components/hub/people.tsx:63-72`, where today only a partial figure has one).
  Only labels and descriptions change. No value, sort, filter, indicator or export changes. The new name, the basis lines, the Partial view row label and the "at most" wording of FR-FIG-01 ship behind `flag.figure_names` (§43.4). While it is off, Workload shows "Remaining N h" and every label, tooltip and description is today's; the server still returns `uncommitted` and `band`.

### 45.4 Load bands from the server

- **FR-FIG-05.** Workload's load band for each person-week MUST be computed by one Hub.Domain function (`src/Hub.Domain/Workload.cs`) from organisation settings and returned per cell as `band`. Bands are tested in this order:

| Band | Condition | Shade today |
|---|---|---|
| Over | load_pct > `workload_over_pct` | Red |
| Near | load_pct > `workload_amber_pct` | Amber |
| Within | load_pct ≥ `workload_under_pct` | Calm |
| Light | Otherwise | Light |

A week with no availability and committed hours has load 999 % (`Workload.Pct`) and is Over, as today. With the default settings every cell keeps today's shade (`web/src/pages/Workload.tsx:57-60`). The 90 % amber band, which exists today only in the client, becomes a stated rule (constitution II). The client maps the band to a shade and never compares a percentage with a number.

- **FR-FIG-06.** Over-assigned and Under-assigned (§12.15 step 3) MUST read `workload_over_pct` and `workload_under_pct` instead of the fixed constants at `src/Hub.Domain/Workload.cs:14`, which remain as the defaults. The server MUST also return per person `overWeek`: the first of the current and next week whose band is Over. The client derives it today with `c.pct > 110` (`Workload.tsx:47`). The Workload response MUST carry the three thresholds in use, so the legend and the Method text name them instead of the fixed "110 %" in `workload.legend.over`. Deadline cluster is unchanged.

- **FR-FIG-07.** The three thresholds MUST be organisation settings in the thresholds group, edited by an Admin on the existing settings screen and audited like every other setting (`src/Hub.Api/Features/Admin.cs:82-92`, `src/Hub.Api/Infrastructure/Audit.cs:48`). Startup seeding creates their rows (`src/Hub.Api/Data/Seed.cs:38-39`), so there is no migration. Because bands are tested in order, every combination of values gives exactly one band; an amber value at or above the over value leaves no Near band. The Weekly Planner's settings (§10.9) are unchanged.

| Setting key | Default | Allowed | Used by |
|---|---|---|---|
| `workload_over_pct` | 110 | 50 to 300 | Over band, Over-assigned, `overWeek` |
| `workload_amber_pct` | 90 | 0 to 300 | Near band |
| `workload_under_pct` | 40 | 0 to 100 | Light band, Under-assigned |

### 45.5 Permission flags in place of client role checks

- **FR-FIG-08.** Each client decision in the table below MUST read a flag that the server computes with the Hub.Domain function its endpoint enforces, instead of rebuilding the decision from `permissions.isPm`, `permissions.leadOf` or `manageDisciplineIds`. Every flag is an added field. Project-level flags use the existing `{ok, reason}` shape on the project's `permissions` (`src/Hub.Api/Features/Projects.cs:287-306`). Row flags are booleans, like the existing `canEdit` and `canManage`. In the table, client files are under `web/src/pages/projects/` and server files under `src/Hub.Api/Features/`.

| # | Client decision today | Server flag | Computed by |
|---|---|---|---|
| 1 | `Allocations.tsx:64` propose an allocation: PM or any managed discipline | `permissions.proposeAllocation` | `Permissions.ProposeAllocation` |
| 2 | `Dashboard.tsx:49` snooze an attention item: PM or Discipline Lead | `permissions.snooze` | `Permissions.Snooze`, enforced at `Evaluation.cs:28` |
| 3 | `Decisions.tsx:723` edit external parties: PM and edit | `permissions.manageExternalParties` | `Permissions.ManageExternalParties`, enforced at `Decisions.cs:55` |
| 4 | `DesignBasis.tsx:78-81` create a basis entry, and in which disciplines; name another owner | `permissions.createBasisIn` and `permissions.assignBasisIn`, discipline lists like `createTaskIn` | `Permissions.CreateBasis` with the actor, or another person, as owner (`DesignBasis.cs:117`) |
| 5 | `ProjectLayout.tsx:57` date review banner: PM or Discipline Lead | `permissions.reviewDates` | New `Permissions.ReviewDates`: PM or any Discipline Lead, today's rule |
| 6 | `Readiness.tsx:123` capture the weekly plan: PM and can write | `permissions.captureWeeklyPlan` | `Permissions.ManageCoordination` as `WeeklyCommitments.cs:147` enforces |
| 7 | `Readiness.tsx:205-206` work offered for a weekly commitment: owner or managed discipline | `canCommit` on each task and deliverable of the coordination options | As `WeeklyCommitments.cs:50-51` enforce |
| 8 | `ReadinessForms.tsx:68` assess readiness: can write and managed discipline | `canManage` on each task and deliverable of the coordination options | `Permissions.ManageCoordination` for the work's discipline (`Readiness.cs:667, 698`) |
| 9 | `Submissions.tsx:43` create a submission: PM or any managed discipline | `permissions.createSubmission` | `Permissions.CreateSubmission`, enforced at `Submissions.cs:172` |
| 10 | `Submissions.tsx:145` supersede an Issued package: authoriser or any managed discipline | `permissions.createSubmission` | `Permissions.CreateSubmission`, which the supersede path enforces |
| 11 | `Tasks.tsx:131-139` task hints (manage, edit, due), also used at `Coordination.tsx:78` | `can` with `assign`, `edit` and `changeDue` on each task row | `Permissions.AssignTask`, `EditTask` and `ChangeDueDate` |
| 12 | `Changes.tsx:99` manage a change assessment: managed discipline | `canManage` on the work item (row 8) | `Permissions.ManageCoordination`, enforced at `Changes.cs:204, 210` |
| 13 | `CoordinationRegister.tsx:41` create in a register: can write and (changes, or any managed discipline) | `canCreate` per register kind on the coordination options | The function its create endpoint enforces |

The coordination options come from `src/Hub.Api/Features/Changes.cs:250-262`; task rows from `src/Hub.Api/Features/TaskQueries.cs:133`. The `isPm` and `leadOf` fields stay on the response.

- **FR-FIG-09.** Each flag MUST equal its endpoint's own decision for every role and project status. Where a flag differs from today's client rule, the endpoint already refuses the call (403) or already allows it; a parity test lists every such case. A control the endpoint allows is offered. A control today's client offers but the endpoint refuses stays visible, disabled, with the server's reason as its title (D5), so no control a user sees today disappears. The server still enforces every command (§8.9, constitution VI).

### 45.6 One copy of each duplicated rule

- **FR-FIG-10.** The rule for changing or cancelling an open role MUST be one Hub.Domain function, `Permissions.ManageOpenRole`: the actor may propose an allocation on the project (FR-ROLE-02) and is a PM of the project or the role's creator, its accountable owner (FR-ROLE-01). Today it is written inline twice (`src/Hub.Api/Features/OpenRoles.cs:41-42` for the row's `canManage`, `:99-100` for edit and cancel). Both use the function. Refusals keep their reasons in today's order: the proposal refusal first, then `perm.owner`. The permission matrix and sweep tests cover it (`tests/Hub.Tests/Domain/PermissionMatrixTests.cs`, `PermissionSweepTests.cs`).

- **FR-FIG-11.** The P-05 correction rule, that a change to a Complete project needs a reason, MUST be one Hub.Domain function. `ProjectEndpoints.CorrectionReason` (`src/Hub.Api/Features/Projects.cs:313`), the open-role reason check that re-implements it (`OpenRoles.cs:48`), the coordination command gate (`src/Hub.Api/Infrastructure/Coordination.cs:32-36`) and the project's `permissions.needsReason` (`Projects.cs:305`) all use it. The client MUST read `permissions.needsReason` where it compares the status itself today (`web/src/pages/projects/Allocations.tsx:72`, `Readiness.tsx:178`). Reason checks are unchanged: at least 5 characters where a reason is required (G-09), and today's length limit on an optional reason.

- **FR-FIG-12.** The open-role label limit of 1 to 80 characters (FR-ROLE-01) MUST be one Hub.Domain constant. The endpoint check (`OpenRoles.cs:64`) and the database CHECK definition (`src/Hub.Api/Data/HubDb.cs:732`) use it, and the CHECK's SQL text stays identical, so there is no migration. `GET /reference` MUST return it as `limits.roleLabel`, and the role form reads its maximum length from there (`Allocations.tsx:363`).

### 45.7 Planner rows after an edit

- **FR-FIG-13.** The planner grid read MUST accept `keep`, a list of at most 50 ids of planning entries and people. Each id passes the same visibility rules as every other read (FR-PLN-18, FR-PLN-19) or is ignored. A kept entry that does not match the filters is returned with `matchesFilter: false`, as non-matching entries are today. A person who owns a kept entry, or whose id is kept, is returned even when the entry, indicator or discipline filter would drop them (`src/Hub.Api/Features/Planning.cs:276-283`), with `kept: true` on the row. Without `keep`, every existing field keeps today's value. Exports and saved views ignore `keep`.

- **FR-FIG-14.** After the viewer creates, changes, moves or changes the visibility of an entry, or records time away, in the Weekly Planner grid or Projects view while a filter is active, the client MUST send those ids in `keep` until the filters change or the page reloads. A kept entry or row that no longer matches MUST stay on screen, dimmed and labelled in text "No longer matches the filters", never by colour alone (§22). It stays reachable by keyboard and opens as before. Entries that were not edited keep today's behaviour: hidden when they do not match (`web/src/components/planner/PlannerGrid.tsx:68`, `ProjectPlanningGrid.tsx:6`). Totals and group subtotals sum the rows on screen (FR-GRP-02). This addition ships behind the flag `planner_kept_rows` (§43.4); while it is off, `keep` is ignored and `kept` is absent.

### 45.8 Guard against new client copies

- **FR-FIG-15.** The §44.6 ratchet MUST fail CI when web/src compares a load percentage with a numeric literal (a new counter with baseline 0), and when its count of client permission re-derivations in `web/src/pages` rises. The second counter counts `permissions.isPm`, `permissions.leadOf`, `manageDisciplineIds.length` and `manageDisciplineIds.includes(`. It becomes a failing check once the first §45.5 change has landed, as §44.6 provides once a replacement exists. Each later §45.5 change lowers its baseline. It ends at the two choice-list filters that read a server-computed list (`web/src/pages/projects/Reviews.tsx:34`, `Changes.tsx:35`).

### 45.9 Acceptance criteria

- **AC-FIG-01.** Given Jill has a 30 h Expected planning entry on a Restricted project that Sam cannot view, overlapping week W, and capacity 40 h with nothing else planned, when Sam opens the Weekly Planner, then Jill's row shows "Partial view" with the PLN-15 text as its tooltip and accessible description, her week W remaining capacity reads "at most 40 h" with the value 40 as today, the page notice still shows, and no restricted project name or hours appear. Alex, with no hidden work, shows his figure without the label or the "at most" wording. With `flag.figure_names` off, Jill's row shows no label and no "at most" wording, as today.

- **AC-FIG-02.** Given a person with a hidden Confirmed allocation overlapping the displayed week, then the planner grid read, the picker's batch read and the My Staff request mark the person partial and return no hidden identity or hours; the same viewer's Workload row is partial and its `uncommitted` is null while not negative; an Admin sees the person complete with the full figures. The existing tests `Picker_rows_say_when_a_person_has_planned_work_the_viewer_cannot_see` and `Hidden_confirmed_allocations_mark_capacity_partial_without_returning_private_work` pass unchanged.

- **AC-FIG-03.** Given a person-week with capacity and availability 40 h, 28 h of forecast task work, no allocations, and planning entries of 10 h Confirmed and 6 h Expected, then Workload shows "Uncommitted 12 h" and returns `uncommitted` 12, the Weekly Planner shows remaining capacity 24 h, and the picker shows "24 h free". With `flag.figure_names` on, no Workload cell uses the word "Remaining"; with it off, Workload shows "Remaining 12 h" as today. On a partial Workload row the value is null at 12 h and −3 at −3 h, shown as "Uncommitted −3 h". The AC-PLN-11 test `Existing_workload_and_allocation_reads_are_unchanged_by_planning_commands` passes unchanged.

- **AC-FIG-04.** Given Workload with `flag.figure_names` on, then the grid's accessible description is the uncommitted basis line and the Method popover's committed-load step names "Uncommitted". Given the person picker or My Staff showing "24 h free", then the figure's tooltip and accessible description are the remaining-capacity basis. The Weekly Planner's basis line is unchanged. With the flag off, every description and tooltip is today's. The resources, planner and supervisor-capacity browser suites report 0 axe violations.

- **AC-FIG-05.** Given the default settings, then cells with load 39, 40, 90, 91, 110 and 111 % return Light, Within, Within, Near, Near and Over; a cell with no availability and committed hours returns Over; and the resources browser suite renders the same shade for every fixture cell as before the change.

- **AC-FIG-06.** Given an Admin sets `workload_amber_pct` to 80 and `workload_over_pct` to 120, then a 100 % cell is Near, a 115 % cell is Near, Over-assigned fires only above 120 %, the legend names 80 % and 120 %, and each change writes an activity entry. Values outside the allowed ranges, such as `workload_over_pct` 49 or `workload_amber_pct` 301, are refused. Restoring the defaults restores today's bands and indicators. The existing domain checks in `tests/Hub.Tests/Domain/WorkloadTests.cs` pass unchanged.

- **AC-FIG-07.** Given a person at 100 % load this week and 115 % next week, then the response's `overWeek` is next week's Monday and the Over-assigned chip names that week, as today.

- **AC-FIG-08.** For each of the 13 decisions in §45.5, a parity test evaluates every combination of Admin, project PM, Discipline Lead, Team Member, Read Only and Executive with Setup, Active, On Hold, Complete, Archived and Cancelled, and asserts that the flag equals the endpoint's decision. Every case where today's client rule differs is listed, and its endpoint call is refused today. The existing browser suites show their fixture users the same controls as before, each enabled, or disabled with the server's reason where the endpoint refuses it.

- **AC-FIG-09.** Given an open role created by a Discipline Lead, then a PM may change and cancel it, its creator may, another Discipline Lead of the project is refused with `perm.owner`, a Team Member is refused with the proposal refusal, and on an Archived project everyone is refused. The row's `canManage` agrees in every case. The existing `OpenRoleApiTests` pass unchanged.

- **AC-FIG-10.** Given a Complete project, then creating, changing or cancelling an open role and proposing an allocation without a reason are refused with today's error, and each is accepted with a 5-character reason. On an Active project a reason is optional and a 2,001-character open-role reason is refused, as today. The Allocations and readiness forms ask for a reason exactly when `permissions.needsReason` is true. `Changing_roles_on_a_complete_project_needs_a_correction_reason` passes unchanged.

- **AC-FIG-11.** Given an 81-character role label, then the endpoint refuses it with today's error. The CHECK definition text and the EF model are unchanged, with no new migration. `GET /reference` returns `limits.roleLabel` 80, and the role form's maximum length is 80.

- **AC-FIG-12.** Given the Weekly Planner filtered by source Proposal, when Sam changes Jill's only Proposal entry to Training, then Jill's row and that entry stay on screen, dimmed, labelled "No longer matches the filters" and reachable by keyboard. Changing a filter or reloading removes them. An entry quick-added under the filter that does not match stays in the same way. A grid request without `keep` returns today's people, entries and values. A `keep` id for an entry Sam cannot see changes nothing. With the flag off, the Planner behaves as today.

- **AC-FIG-13.** Given one added comparison of a load percentage with a number in web/src, or one added `permissions.isPm` in `web/src/pages`, then the ratchet fails CI. On the tree after each §45.5 change it passes.

### 45.10 Decisions and deferred proposals

Each decision below uses its default until Jay decides otherwise.

| # | Decision | Default used |
|---|---|---|
| D1 | Name of the Workload figure | "Uncommitted". Alternatives: "Spare"; or rename the Planner figure "Free to plan" and keep "Remaining" on Workload, which would change PLN-09's display name. |
| D2 | Hidden work in free hours | The Partial view label and an upper-bound figure. No "Other project work N h" line, because it would reveal how much hidden work exists. |
| D3 | Workload's partial rule | Today's: every row partial for a viewer who cannot see every restricted project. A per-person rule like the Planner's is deferred. |
| D4 | Band names and default thresholds | Over above 110 %, Near above 90 %, Within from 40 %, Light below. Jay confirms 90 % as the default amber setting. |
| D5 | A control whose flag differs from today's client rule | Offer it where the endpoint allows it. Where today's client shows it and the endpoint refuses, keep it visible and disabled with the server's reason. Alternative: hide it. |
| D6 | Kept Planner rows | Kept until the filters change or the page reloads; at most 50 ids; counted in totals and subtotals. |
| D7 | Where the guard lives | Two counters in the §44.6 ratchet. If that ratchet has not landed, the load-percentage check waits for it; it is not built twice. |
| D8 | Exports | Unchanged. No band, uncommitted figure or kept row appears in any export. |
| D9 | Flags and unflagged corrections | `flag.figure_names` covers the Workload name, the basis lines, the Partial view row label and the "at most" wording; `planner_kept_rows` covers kept rows. The three threshold settings and the legend that names them (§45.4) and controls kept visible and disabled with the server's reason (FR-FIG-09) ship unflagged as corrections; their slices are Tier B (§43.11). |

Deferred follow-up proposals, outside packet 041: a per-person partial rule on Workload; bands and uncommitted hours in exports; server-sent label keys for the figures; the status-transition reason rule in `web/src/pages/projects/ProjectSettings.tsx:205`; and a per-week rather than per-person partial flag.
