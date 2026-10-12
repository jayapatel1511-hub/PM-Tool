# Feature Specification: Staffing Signals and Trustworthy Counts

**Packet**: 036 | **Feature directory**: `specs/036-staffing-and-signal-trust` | **Date**: 2026-10-11
**Status**: In build. Jay accepted the defaults for D1–D9 on 2026-10-11 ("lets start build"; T001).
**Input**: Jay, 2026-10-11: adopt the ranked staffing and signal-quality recommendations from the October product research. The research notes are kept outside the repository.
**Source**: product specification §40 (§40.1–§40.10) in `spec-parts/15-staffing-and-signals.md`. Related, unchanged contracts: §37.6 (resource allocations, packet 029), §39 (weekly planning, packet 034; §39.12 lists role-level demand as a deferred proposal, which this packet takes up), §12.6 and D-rules (dependencies), §12.12 and A-rules (attention), §13.1 (dashboards), §13.5 (timeline), §8 (permissions).

## User outcome

A resource manager can staff a project with a role before a person is named, and every list of capacity shows that unfilled demand. A planner can express "starts together" and "finishes together" links, not only "finish then start". A PM is told when work has stopped moving even though people keep touching it. Whoever assigns work sees the person's free hours at the moment of choice. Every count on a dashboard opens exactly the rows it counts, says what it measures, and never hides unassigned or undated work. Every timeline says which dates it draws.

## Scope

**Part A: Staffing and planning signals**

1. **Open roles** (FR-ROLE): a new project-scoped record for unfilled demand, with a role label, a date range, planned hours and an optional project discipline. It is filled by proposing a normal resource allocation, so the existing proposal and confirmation flow is unchanged.
2. **Dependency types** (FR-DEPT): Start-to-Start and Finish-to-Finish next to Finish-to-Start, for task and deliverable links, with the existing working-day lag.
3. **Stalled status rule** (FR-STALE): a new attention rule, A-21, for open tasks and deliverables whose status has not changed for a configurable number of days.
4. **Capacity grouping** (FR-GRP): an optional home discipline on each person, and a group-by control on Workload and the Weekly Planner (Reports to, Office, Job title, Home discipline).
5. **Capacity at assignment** (FR-PICK): the person picker shows each candidate's remaining hours and time-away hours for the relevant week, to viewers who can already see that person's capacity.

**Part B: Trustworthy counts and dates**

6. **Count reconciliation** (FR-CNT): fix the known mismatches between dashboard counts and their lists. Add a one-line definition to every summary tile. Account for On Hold, unassigned and undated items.
7. **Timeline date source** (FR-TL): label bars as start → due, mark assumed start dates, and stop silently dropping items.

## Non-goals

- Changing packet 029's allocation model, its proposal and confirmation rules, or the allocation `PersonId` (open roles are a separate record).
- Changing packet 034's planning entries. Open roles do not appear in the Weekly Planner grid, which stays person × week.
- Start-to-Finish links, negative lag, editing a link's type in place (remove and re-add instead), dependency types in templates, and dependency-driven date cascades.
- Staleness rules for due date or owner. An unchanged due date or owner is not a problem signal.
- A dashboard or widget builder, custom fields, an automation builder, live socket updates.
- Later proposals, deliberately left out: partial/full conflict severity, a pinned status comment with seen-by, a status-mix bar, muting notifications during leave, per-person weekday hours, an intake form, and imported spent hours.

## Open decisions (defaults used until Jay decides)

| # | Decision | Default |
|---|---|---|
| D1 | How an open role is filled | Proposing a normal resource allocation that names the open role. The role becomes Filled when that allocation is created, and returns to Open if the allocation is declined or cancelled. |
| D2 | Who can create, edit or cancel open roles | Same as proposing an allocation: PM or Discipline Lead on the project (`ProposeAllocation`). |
| D3 | Where open roles appear | Project Allocations tab, and an "Unfilled roles" section on Workload. Never in a person's capacity, the Weekly Planner or readiness checks. |
| D4 | Anchor for Start-to-Start lag | A new `StartedAt`, set the first time a task or deliverable leaves Not Started, and backfilled from the activity log. |
| D5 | What Finish-to-Finish does | Never makes the successor Waiting or Blocked. It drives the date-consistency check (successor due ≥ predecessor due + lag) and warns when the successor finishes before its predecessor. |
| D6 | A-21 threshold and scope | `status_stale_days` = 15. In Progress or In Review tasks and deliverables. It does not fire when A-10 or A-11 already fired for the same item. |
| D7 | Home discipline source | Edited by an Admin on the user record. Not synced from the directory. |
| D8 | Who sees capacity in the picker | Only viewers who could already see that person in Workload or the Planner. Everyone else sees the plain picker. |
| D9 | Home "Review" lane and Dashboard "In review" tile disagree on Revision Required | Keep both rules and show each one's definition. Rename the Home lane "Review and revision". |

## User Scenarios & Testing

### Story 1: Staff a proposal before names are known (P1)

A PM adds an open role "Civil technologist, 20 h/week, 3 Nov–19 Dec" to a project. Workload shows it under Unfilled roles for those weeks. Later the PM proposes Priya for it, and the Supervisor confirms as usual.

- **AC-ROLE-01.** Creating an open role needs a label (1–80 characters), a from date and a through date, and planned hours greater than 0. It is audited and has a row version.
- **AC-ROLE-02.** Workload's Unfilled roles section shows the role's hours for each week in range, spread over working days in the same way as an allocation. No person's committed hours, percentage or indicator changes.
- **AC-ROLE-03.** Proposing an allocation with `openRoleId` creates the Proposed allocation and marks the role Filled in one transaction. A second proposal for a role that is already Filled is refused. Two simultaneous proposals give exactly one allocation.
- **AC-ROLE-04.** Declining or cancelling that allocation returns the role to Open. Completing it leaves the role Filled.
- **AC-ROLE-05.** Viewers who cannot view the project never see its open roles in any list, export, search or count.

### Story 2: Link work that starts or finishes together (P1)

A PM links "Drainage design" to start 2 working days after "Grading design" starts (Start-to-Start, lag 2), and links "Drawings" to finish with "Calcs" (Finish-to-Finish).

- **AC-DEPT-01.** The add dialog offers Finish-to-Start (the default), Start-to-Start and Finish-to-Finish. Existing links stay Finish-to-Start.
- **AC-DEPT-02.** Start-to-Start: the successor is Waiting until the predecessor has started and lag working days have passed since `StartedAt`. After that the edge is satisfied. Blocked follows the existing D-06 conditions.
- **AC-DEPT-03.** Finish-to-Finish never puts the successor in Waiting or Blocked. The date check flags successor due < predecessor due + lag. A successor that is Complete while its predecessor is not shows "Finished before predecessor".
- **AC-DEPT-04.** Start-to-Start date check: successor start < predecessor start + lag is flagged. If either start date is missing, nothing is flagged.
- **AC-DEPT-05.** Readiness, the dependency list's satisfied tick, and the deliverable dependency list all use the same satisfaction function as the evaluator. The client never recomputes it.
- **AC-DEPT-06.** Timeline and Gantt arrows run start → start for Start-to-Start, end → end for Finish-to-Finish, and end → start for Finish-to-Start. The arrow tooltip names the type and the lag.

### Story 3: See work that stopped moving (P2)

- **AC-STALE-01.** A task In Progress whose status has not changed for 15 days, but which had a comment yesterday, raises A-21 "Status not moving" with the days and the threshold in the reason.
- **AC-STALE-02.** The same task with no activity for 15 days raises A-10 only, not A-21.
- **AC-STALE-03.** A-21 can be turned off with `rule_enabled.A-21`. `status_stale_days` accepts 1–365.

### Story 4: Group capacity (P2)

- **AC-GRP-01.** An Admin sets a person's home discipline. The user record's history shows the change.
- **AC-GRP-02.** Workload and the Planner group rows by Reports to, Office, Job title or Home discipline. People with no value form a "Not set" group, never a hidden one. Group totals are the sum of their rows.

### Story 5: Assign with capacity in view (P1)

- **AC-PICK-01.** A Supervisor opening the owner picker on a task due in week 46 sees "12 h free" (and "16 h away" when time away falls in the week) next to each direct report for week 46.
- **AC-PICK-02.** For people the viewer cannot see in the Planner, no figure is shown, and the response contains no data for them.
- **AC-PICK-03.** The figure equals the Planner's remaining capacity for that person and week. The picker loads it with one request per open, not one per person.

### Story 6: Counts that match their lists (P1)

- **AC-CNT-01.** For every tile on Home, the Project Dashboard and Portfolio, the count equals the number of rows in the list it opens, under the same permissions. The Red, Yellow and Green Portfolio tiles open `computedHealth=X&status=Active`.
- **AC-CNT-02.** The Discipline "Open" count and its link use the same predicate (On Hold is excluded from both).
- **AC-CNT-03.** The deliverable Unassigned filter excludes terminal deliverables, matching the chip rule.
- **AC-CNT-04.** Upcoming Deadlines orders each type by date before truncating, so the soonest items are never dropped.
- **AC-CNT-05.** Home's status lanes account for every open task: On Hold tasks show as an "On hold" count with a link.
- **AC-CNT-06.** Each summary tile has a one-line definition, available as a tooltip and to screen readers.
- **AC-CNT-07.** When a discipline scope is selected, the decision, issue and risk tiles say "Whole project".

### Story 7: Timelines that say what they draw (P2)

- **AC-TL-01.** The legend says "Bar: start → due". A bar whose start is assumed from the creation date draws that part faded, and its tooltip and aria label say "start not set".
- **AC-TL-02.** On the project Timeline, a dated task under an undated deliverable appears as a top-level row, as it already does on Gantt.
- **AC-TL-03.** Milestones with no date are listed under Unscheduled instead of being dropped.
- **AC-TL-04.** The project Timeline uses the server's local `createdOn` instead of cutting a UTC timestamp.

## Success criteria

The full acceptance plan, with an evidence map, regression guard, test data and sign-off, is in [verification.md](verification.md).

- **SC-001**: A PM staffs a real upcoming proposal with at least 3 open roles and fills at least 1 through the normal proposal and confirmation, in under 15 minutes, without help.
- **SC-002**: Every count on Home, the Project Dashboard and Portfolio equals the rows of the list it opens, for Admin, PM and Supervisor: 0 mismatches.
- **SC-003**: On the dependency fixture, the Waiting, Blocked and date flags match a hand-worked table on three dates.
- **SC-004**: Open roles and picker capacity reveal nothing to viewers outside scope.
- **SC-005**: Packet 029 and 034 behaviour is unchanged: tests green, and Workload person rows identical before and after.

## Requirements (§40)

- **FR-ROLE-01..05**: open role record, permissions (D2), fill through allocation proposal (D1), Workload display (D3), privacy (AC-ROLE-05).
- **FR-DEPT-01..06**: type on both link kinds, `StartedAt` (D4), one shared satisfaction function, evaluator rules by type (D5), date checks by type, arrows by type.
- **FR-STALE-01..02**: rule A-21 and the `status_stale_days` setting (D6).
- **FR-GRP-01..02**: home discipline attribute (D7) and the group-by control.
- **FR-PICK-01..02**: multi-person capacity read and the picker display (D8).
- **FR-CNT-01..07**: the AC-CNT items as requirements.
- **FR-TL-01..04**: the AC-TL items as requirements.

## Wording rule

Code, comments, UI text, specification text, commit messages and documentation for this packet name no third-party products. Features are described by what they do.
