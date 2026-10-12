## 40. Staffing Signals and Trustworthy Counts Amendment

Approved scope: on 2026-10-11 Jay adopted the ranked staffing and signal-quality recommendations from the October product research, delivered by packet 036, and accepted the default for every open decision (§40.10). This section records the product contract; implementation and acceptance remain pending. It adds project-scoped open roles, two dependency types, one attention rule, a home discipline for grouping capacity, capacity figures in the person picker, and corrections that make every summary count and timeline bar say what it measures. §37.6 (allocations, packet 029) and §39 (weekly planning, packet 034) are unchanged apart from the two links named in FR-ROLE-03 and FR-PICK-01. Where this section adds to §12.6 (dependency types), §12.12 (attention rules), §10.4 (settings) and the §27.3 row on role-level demand, this section governs.

Every addition is deterministic and explainable: each figure, flag and count traces to a rule in this section, and the interface shows that rule. Nothing here assigns people, levels resources, reschedules work or forecasts demand.

### 40.1 Scope and boundary

Part A (staffing and planning signals) covers open roles (§40.2), dependency types (§40.3), the Status not moving rule (§40.4), capacity grouping (§40.5) and capacity at assignment (§40.6). Part B (trustworthy counts and dates) covers count reconciliation (§40.7) and the timeline date source (§40.8).

Outside this amendment: changes to §37.6's allocation model, its proposal and confirmation rules or the person named on an allocation; changes to §39's planning entries, or open roles in the Weekly Planner grid, which stays person × week; Start-to-Finish links, negative lag, editing a link's type in place, dependency types in templates and dependency-driven date cascades; staleness rules for due date or owner; a dashboard or widget builder, custom fields, an automation builder and live socket updates.

### 40.2 Open roles

- **FR-ROLE-01.** An open role MUST be a project-scoped record of unfilled demand: a role label (1–80 characters), an optional project discipline of that project, a from date and a through date (inclusive, through ≥ from), planned hours greater than 0 under the FR-CAP-03 hour rules, an optional note, and a status of Open, Filled or Cancelled. A new role is Open. Its creator is its accountable owner. It is soft-deleted only, carries a row version (G-06, G-07), and every create, change, fill, reopen and cancel writes an activity entry in the same transaction. A stale change returns a conflict and stores nothing.

- **FR-ROLE-02.** Creating, changing or cancelling an open role MUST require the same permission as proposing an allocation (§8.10: PM or Discipline Lead within the project). Everyone else is refused. On a Complete project each of these is a correction and needs a reason, as an allocation proposal does.

- **FR-ROLE-03.** An open role MUST be filled only by proposing a normal §37.6 allocation that names it. The proposal is refused unless the role is Open, is on the same project, and the allocation's date range lies inside the role's range. Creating the Proposed allocation, setting the role to Filled and recording the filling allocation happen in one transaction, so two simultaneous proposals produce exactly one allocation. Declining or cancelling that allocation returns the role to Open and clears the filling allocation; confirming, editing or completing it leaves the role Filled. The allocation keeps every §37.6 rule, including confirmation by the person's Supervisor or an Admin.

- **FR-ROLE-04.** Open roles MUST appear on the project's Allocations tab and in an "Unfilled roles" section of Workload. Workload shows each Open role's hours for every week in its range, spread over the working days of the range by the FR-CAP-03 rule using the project's office calendar (there is no person calendar). Open roles never enter a person's capacity, committed load (FR-CAP-04), Workload indicators, the Weekly Planner or readiness capacity checks (§38.2).

- **FR-ROLE-05.** An open role MUST be visible only to viewers who can view its project (§8.7). For everyone else it is absent from every list, export, search result, count, notification and history, and no total differs because of it.

### 40.3 Dependency types

- **FR-DEPT-01.** Task and deliverable dependencies MUST carry a type: Finish-to-Start (the default, and the type of every existing link), Start-to-Start or Finish-to-Finish, each with the existing working-day lag. An unknown type is refused. One link per predecessor and successor pair remains the rule (D-03); a link's type is changed by removing and re-adding the link. Copying a project's structure keeps each link's type and lag; template links are Finish-to-Start.

- **FR-DEPT-02.** Each task and deliverable MUST record `started_at`: the time it first leaves Not Started. It is set once and never cleared. Existing items are backfilled from the activity log, falling back to their last status change time when they have already left Not Started; items still Not Started have none.

- **FR-DEPT-03.** One satisfaction function MUST decide whether a link is satisfied, and the evaluator, the task and deliverable dependency lists (their satisfied mark) and the readiness predecessor check (§38.2) MUST all use it. The client never recomputes satisfaction. For Finish-to-Start, satisfaction is unchanged (D-04, with the lag). For Start-to-Start, the link is satisfied once the predecessor has a `started_at` and `lag` working days have passed since it. Finish-to-Finish links are always satisfied for starting work. A Cancelled predecessor satisfies every type (D-04).

- **FR-DEPT-04.** Waiting (D-05) and Blocked (D-06) MUST use the FR-DEPT-03 function: a Start-to-Start successor is Waiting until its link is satisfied and Blocked under the existing D-06 conditions; a Finish-to-Finish link never makes its successor Waiting or Blocked, never makes its predecessor Blocking Others (D-11), and never holds back start readiness. Chain walks (D-11, D-13) follow links of every type. Re-evaluation follows D-07 and D-08 unchanged.

- **FR-DEPT-05.** The date consistency check (D-12) MUST depend on the type and remains a warning only. Finish-to-Start: unchanged. Start-to-Start: flag a successor start earlier than the predecessor start plus `lag` working days; if either start date is missing, flag nothing. Finish-to-Finish: flag a successor due date earlier than the predecessor due date plus `lag` working days; when the successor is Complete and the predecessor is not, show the note "Finished before predecessor" instead of the flag.

- **FR-DEPT-06.** Timeline and Gantt arrows MUST run end → start for Finish-to-Start, start → start for Start-to-Start and end → end for Finish-to-Finish. The arrow tooltip and the dependency list chip name the type and the lag (for example "FS", "SS +2d", "FF").

### 40.4 Status not moving (A-21)

- **FR-STALE-01.** The attention engine (§12.12) MUST include rule A-21 "Status not moving": a task or deliverable In Progress or In Review whose status has not changed for more than `status_stale_days` days, measured as A-10 measures days, while the item has had activity within `task_stale_days` days. It never fires for an item with no activity within `task_stale_days` days (A-10's condition) or for a task whose review has stalled (A-11's condition), whether or not A-10 and A-11 are switched on; deliverables have no review-stalled condition. Severity Info, or Warning when the item is also Due Soon; routed to the assignee (task) or owner (deliverable) and the Discipline Lead. The reason states the days since the status changed and the threshold, for example "Status unchanged for 16 days (threshold 15)". It follows the existing snooze rule.

- **FR-STALE-02.** `status_stale_days` MUST be an organisation setting with default 15 and range 1–365, edited by an Admin with the other attention thresholds; A-21 can be turned off with its rule toggle (`rule_enabled.A-21`) like every other rule.

| ID | Rule | Condition (all: item not terminal; project Active) | Severity | Routed to |
|---|---|---|---|---|
| A-21 | Status not moving | Task or deliverable In Progress or In Review with `today − status_changed_at > status_stale_days` and `today − last_activity_at ≤ task_stale_days`, and, for a task, A-11's review-stalled condition does not hold; whether A-10 or A-11 is switched on makes no difference | Info; Warning if also Due Soon | Assignee or owner, DL |

| Setting key | Default | Used by |
|---|---|---|
| `status_stale_days` | 15 (1–365) | Attention rule A-21 |

### 40.5 Capacity grouping

- **FR-GRP-01.** Each user MUST have an optional home discipline (one of the organisation's disciplines), edited by an Admin on the user record and recorded in the user's history. It is not synced from the directory. An unknown discipline is refused.

- **FR-GRP-02.** Workload and the Weekly Planner MUST offer a group-by control with Reports to, Office, Job title and Home discipline; the default grouping stays as today. People with no value form a "Not set" group, never a hidden one. Each group's subtotal for a week equals the sum of its rows. The grouping is kept in the URL. Grouping changes no scope, figure or indicator.

### 40.6 Capacity at assignment

- **FR-PICK-01.** The Weekly Planner grid read MUST accept a list of at most 50 people for one week and answer inside the existing planner scope (PLN-14). People outside the viewer's scope are left out of the answer silently, so the response reveals nothing about them. The figures are the planner's own: remaining capacity (PLN-09) and time away (PLN-06), with the viewer's visibility rules (FR-PLN-18).

- **FR-PICK-02.** The person picker for a task owner, the task create form and the reassign work tool MUST show each candidate's remaining hours as "N h free", plus "N h away" when the week has time away, for the week of the item's start date, otherwise its due date, otherwise the current week. Figures appear only for people the viewer could already see in Workload or the Weekly Planner; other candidates show none, and a viewer without that access makes no capacity request. The picker loads its figures with one request per opening, not one per person. The figure is information only and blocks no choice.

### 40.7 Count reconciliation

- **FR-CNT-01.** Every summary tile on Home, the Project Dashboard and Portfolio MUST show a count equal to the number of rows in the list it opens, under the same permissions. The Portfolio Red, Yellow and Green tiles open the projects with that computed health and status Active.

- **FR-CNT-02.** The discipline "Open" count on the Project Dashboard and the list its link opens MUST use the same predicate; On Hold is excluded from both.

- **FR-CNT-03.** The deliverable Unassigned filter MUST exclude terminal deliverables (Issued, Accepted, Cancelled), matching the Unassigned chip rule.

- **FR-CNT-04.** Upcoming Deadlines MUST order each item type by date before it truncates the list, so the soonest items are never dropped.

- **FR-CNT-05.** Home's task status lanes MUST account for every open task: On Hold tasks appear as an "On hold" count with a link to exactly those tasks.

- **FR-CNT-06.** Each summary tile MUST carry a one-line definition of what it counts, available as a tooltip, reachable by keyboard and announced to screen readers. Home's Review lane is named "Review and revision", and its definition names the statuses it includes (Ready for Review, In Review, Revision Required); the Project Dashboard's "In review" tile keeps its own rule and shows its definition.

- **FR-CNT-07.** When a discipline scope is selected on the Project Dashboard, the decision, issue and risk tiles MUST say "Whole project", because those registers are not filtered by discipline.

### 40.8 Timeline date source

- **FR-TL-01.** The Timeline and Gantt legend MUST say "Bar: start → due". When a bar's start is assumed from the creation date because no start date is set, that part of the bar is drawn faded, and its tooltip and accessible label say "start not set".

- **FR-TL-02.** On the project Timeline, a dated task under an undated deliverable MUST appear as a top-level row, as it already does on Gantt.

- **FR-TL-03.** Milestones with no date MUST be listed under Unscheduled instead of being dropped (§36.4).

- **FR-TL-04.** The project Timeline MUST take an assumed start from the item's creation date in the organisation time zone, never by cutting a UTC timestamp.

### 40.9 Acceptance criteria

- **AC-ROLE-01.** Creating an open role needs a label (1–80 characters), a from date and a through date, and planned hours greater than 0. It is audited and has a row version.

- **AC-ROLE-02.** Workload's Unfilled roles section shows the role's hours for each week in range, spread over working days in the same way as an allocation. No person's committed hours, percentage or indicator changes.

- **AC-ROLE-03.** Proposing an allocation that names an open role creates the Proposed allocation and marks the role Filled in one transaction. A second proposal for a role that is already Filled is refused. Two simultaneous proposals give exactly one allocation.

- **AC-ROLE-04.** Declining or cancelling that allocation returns the role to Open. Completing it leaves the role Filled.

- **AC-ROLE-05.** Viewers who cannot view the project never see its open roles in any list, export, search or count.

- **AC-DEPT-01.** The add dialog offers Finish-to-Start (the default), Start-to-Start and Finish-to-Finish. Existing links stay Finish-to-Start.

- **AC-DEPT-02.** Start-to-Start: the successor is Waiting until the predecessor has started and lag working days have passed since `started_at`. After that the link is satisfied. Blocked follows the existing D-06 conditions.

- **AC-DEPT-03.** Finish-to-Finish never puts the successor in Waiting or Blocked. The date check flags successor due < predecessor due + lag. A successor that is Complete while its predecessor is not shows "Finished before predecessor".

- **AC-DEPT-04.** Start-to-Start date check: successor start < predecessor start + lag is flagged. If either start date is missing, nothing is flagged.

- **AC-DEPT-05.** Readiness, the dependency list's satisfied mark and the deliverable dependency list all use the same satisfaction function as the evaluator. The client never recomputes it.

- **AC-DEPT-06.** Timeline and Gantt arrows run start → start for Start-to-Start, end → end for Finish-to-Finish and end → start for Finish-to-Start. The arrow tooltip names the type and the lag.

- **AC-STALE-01.** Given a task In Progress whose status has not changed for 16 days but which had a comment yesterday, then A-21 "Status not moving" fires with the days and the threshold (15) in the reason.

- **AC-STALE-02.** Given the same task with no activity at all for 16 days, then A-10 fires and A-21 does not.

- **AC-STALE-03.** A-21 can be turned off with `rule_enabled.A-21`. `status_stale_days` accepts 1–365.

- **AC-GRP-01.** An Admin sets a person's home discipline. The user record's history shows the change.

- **AC-GRP-02.** Workload and the Weekly Planner group rows by Reports to, Office, Job title or Home discipline. People with no value form a "Not set" group, never a hidden one. Group totals are the sum of their rows.

- **AC-PICK-01.** Given a Supervisor opens the owner picker on a task due in week 46, then each direct report shows their free hours for week 46 (for example "12 h free"), and "16 h away" when time away falls in that week.

- **AC-PICK-02.** For people the viewer cannot see in the Weekly Planner, no figure is shown, and the response contains no data for them.

- **AC-PICK-03.** The figure equals the Weekly Planner's remaining capacity for that person and week. The picker loads it with one request per opening, not one per person.

- **AC-CNT-01.** For every tile on Home, the Project Dashboard and Portfolio, the count equals the number of rows in the list it opens, under the same permissions. The Red, Yellow and Green Portfolio tiles open Active projects with that computed health.

- **AC-CNT-02.** The discipline "Open" count and its link use the same predicate (On Hold is excluded from both).

- **AC-CNT-03.** The deliverable Unassigned filter excludes terminal deliverables, matching the chip rule.

- **AC-CNT-04.** Upcoming Deadlines orders each type by date before truncating, so the soonest items are never dropped.

- **AC-CNT-05.** Home's status lanes account for every open task: On Hold tasks show as an "On hold" count with a link.

- **AC-CNT-06.** Each summary tile has a one-line definition, available as a tooltip and to screen readers.

- **AC-CNT-07.** When a discipline scope is selected, the decision, issue and risk tiles say "Whole project".

- **AC-TL-01.** The legend says "Bar: start → due". A bar whose start is assumed from the creation date draws that part faded, and its tooltip and accessible label say "start not set".

- **AC-TL-02.** On the project Timeline, a dated task under an undated deliverable appears as a top-level row, as it already does on Gantt.

- **AC-TL-03.** Milestones with no date are listed under Unscheduled instead of being dropped.

- **AC-TL-04.** Given a deliverable created at 22:30 organisation time, when that is already the next day in UTC, then the project Timeline's assumed start is the organisation-time creation date.

### 40.10 Decisions and deferred proposals

Jay accepted every default below on 2026-10-11 ("lets start build").

| # | Decision | Default used |
|---|---|---|
| D1 | How an open role is filled | Proposing a normal allocation that names the role. The role becomes Filled when that allocation is created and returns to Open if the allocation is declined or cancelled. |
| D2 | Who can create, change or cancel open roles | Whoever may propose an allocation: PM or Discipline Lead within the project. |
| D3 | Where open roles appear | The project's Allocations tab and Workload's Unfilled roles section. Never in a person's capacity, the Weekly Planner or readiness checks. |
| D4 | Anchor for Start-to-Start lag | `started_at`, set the first time an item leaves Not Started and backfilled from the activity log. |
| D5 | What Finish-to-Finish does | Never makes the successor Waiting or Blocked. It drives the date check and warns when the successor finishes before its predecessor. |
| D6 | A-21 threshold and scope | `status_stale_days` = 15; In Progress or In Review tasks and deliverables with activity within `task_stale_days`; never for a task whose review has stalled (A-11's condition), whether or not A-10 and A-11 are switched on. |
| D7 | Home discipline source | Edited by an Admin on the user record; not synced from the directory. |
| D8 | Who sees capacity in the picker | Only viewers who could already see that person in Workload or the Weekly Planner. |
| D9 | Home Review lane and Dashboard In review tile disagree on Revision Required | Keep both rules, show each definition, and rename the Home lane "Review and revision". |

Open roles take up the project-scoped part of the role-level demand that §27.3 and §39.12 left out: dated demand on one project, filled by a named person. Long-term role-level forecasting stays out.

Deferred follow-up proposals, outside packet 036: partial and full conflict severity; a pinned status comment with seen-by; a status-mix bar; muting notifications during leave; per-person weekday hours; an intake form; and imported spent hours.
