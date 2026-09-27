# Feature Specification: Resource and Workload View

**Feature Branch**: `017-resource-workload-view`

**Created**: 2026-09-24

**Status**: Draft (first release; moved by §36)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 017, the resource and workload view (each person's estimated load over 8 weeks across projects, over-assignment, spare capacity, deadline clusters, rebalancing, and workload reports)."

**Source**: Product specification §7.5, §8.5.1, §10.4 (weekly capacity), §11.9 (FR-RES-01), §12.15, §13.11, §14 Workflow 10, §19, §34 (Q18)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See each person's load for the next 8 weeks (Priority: P1)

A Supervisor (persona Sam) sees a grid of people by week: estimated hours against capacity, with
shading and the number always visible, and how many of each person's tasks have no estimate. The
method is stated on the screen so nobody over-reads the numbers.

**Why this priority**: "Who has capacity?" is today answered by asking every PM.

**Independent Test**: With estimated tasks across two projects, check one person's weekly hours
against the stated method by hand.

**Acceptance Scenarios**:

1. **Given** an open task with a 24-hour estimate at 50 % progress, due in 10 working days, **Then** its 12 remaining hours are spread evenly over the working days from today to the due date and summed per week. *(§12.15)*
2. **Given** an overdue task, **Then** its whole remainder lands in the current week; **Given** a task without a due date, **Then** its hours sit in a "no due date" bucket and are not spread. *(§12.15)*
3. **Given** a person with 3 unestimated tasks, **Then** "3 unestimated" is shown next to their hours. *(§12.15)*

---

### User Story 2 - Spot overload, spare capacity, and colliding deadlines (Priority: P1)

The grid flags people over-assigned this week or next, people under-assigned for two weeks (only
when all their work is estimated), and deadline clusters where three or more tasks across two or
more projects fall within any three days in the next two weeks.

**Why this priority**: These flags are what the supervisor acts on.

**Independent Test**: Seed one person at 140 % next week, one at 25 % with everything estimated,
and one with three deadlines in two days across two projects.

**Acceptance Scenarios**:

1. **Given** a person loaded at 140 % next week, **Then** they are flagged Over-assigned. *(§12.15)*
2. **Given** a person below 40 % for the next two weeks with unestimated tasks, **Then** they are not flagged Under-assigned. *(§12.15)*
3. **Given** three tasks across two projects due within a three-day window in the next 14 days, **Then** the person shows a deadline cluster. *(§12.15)*

---

### User Story 3 - Rebalance work (Priority: P2)

The supervisor opens an overloaded person's tasks and reassigns one to someone with room, or sets
a person's capacity (part-time, leave); PMs can do the same within their projects.

**Why this priority**: Seeing overload is only useful if it can be fixed from the same place.

**Independent Test**: Reassign a task from Alex to Jill in the grid and watch both rows update.

**Acceptance Scenarios**:

1. **Given** Sam supervises Alex and Jill, **When** Sam reassigns one of Alex's tasks to Jill, **Then** both people and the PM are notified, the change is logged with Sam as actor, and the grid recomputes. *(§14 Workflow 10)*
2. **Given** a task on a Restricted project Sam cannot see, **Then** its row and hours are absent from Sam's grid and export, and only an authorised PM can reassign it. *(§14 Workflow 10, §36.1)*

---

### User Story 4 - Workload reports (Priority: P2)

Supervisors and Executives export workload by employee and by discipline.

**Why this priority**: Resourcing discussions happen in spreadsheets; the numbers should come from
the Hub.

**Independent Test**: Run both reports and compare them with the grid.

**Acceptance Scenarios**:

1. **Given** Workload by Employee for a supervisor and 8 weeks, **Then** it lists each person with weekly hours and capacity, open, unestimated, and overdue tasks. *(§19)*

---

### Edge Cases

- Leave and holidays are not modelled; a manual capacity override covers them until HR data is available (Phase 3). *(§12.15, §14 Workflow 10)*
- The view never forecasts beyond 8 weeks, subtracts logged actual task hours from estimates, levels resources, or sets utilisation targets. *(§12.15, §36.8)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: For each open, assigned task that is not On Hold, remaining hours MUST be the estimate times (1 minus progress); unestimated tasks MUST count separately and contribute no hours. *(FR-RES-01, §12.15)*
- **FR-002**: Remaining hours MUST be spread evenly over the working days from the later of the start date and today to the due date and summed per week; overdue work MUST land in the current week; work without a due date MUST sit in a separate bucket. *(§12.15)*
- **FR-003**: Each person-week MUST show assigned hours, capacity (personal override or the organisation default of 40 hours), load percentage, and each person's unestimated, overdue, task, and project counts. *(§12.15, Q18)*
- **FR-004**: Over-assigned MUST mean above 110 % in the current or next week; Under-assigned MUST mean below 40 % for the next two weeks with no unestimated tasks; a deadline cluster MUST mean 3 or more tasks across 2 or more projects due within any 3-day window in the next 14 days. *(§12.15)*
- **FR-005**: The grid MUST show people by week for 8 weeks with shading and numbers, expand to person, project, and task, filter by supervisor, discipline, office, project, indicator, and date range, sort by load, overdue count, or name, state its method in a help popover, and export. *(§13.11)*
- **FR-006**: Supervisors MUST be able to reassign tasks of their direct reports and set their capacity; PMs MUST be able to reassign within their projects; Admins MUST be able to set any capacity. *(§8.5.1, §13.11)*
- **FR-007**: The Workload by Employee and Workload by Discipline reports MUST be available. *(§19)*

### Key Entities *(include if feature involves data)*

- **Capacity**: A person's weekly hours, defaulting to the organisation setting.
- **Workload cell**: One person's computed hours, capacity, and counts for one week.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A supervisor sees who is overloaded and who has capacity across projects without asking any PM. *(§2.4)*
- **SC-002**: Hand calculations of 10 sampled person-weeks match the grid exactly.
- **SC-003**: Reassigning a task from the grid updates both people's rows within one evaluation cycle.

## Assumptions

- Depends on task estimates (§27.1); the view is first-release scope under §36.8 and must show missing estimates explicitly rather than implying spare capacity.
- Supervisor scope is direct reports (Q19). Showing under-assignment follows the specification's default of showing it with caveats (Q18).
- Working days are Monday to Friday until working-day calendars arrive (packet 021).
