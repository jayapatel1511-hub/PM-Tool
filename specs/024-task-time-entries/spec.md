# Feature Specification: Task Time Entries

**Feature Branch**: `024-task-time-entries`

**Created**: 2026-09-24

**Status**: Draft (first release)

**Input**: The user confirmed that the Time navigation entry in the six-view image should let staff enter hours against tasks.

**Source**: Product specification §3.2, §6, §8.5.2, §11.14, §13.20, §19, §27, §30, §34 (Q21), §36.8–§36.9 (FR-VIS-10, AC-VIS-08)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record my task hours (Priority: P1)

A project member opens Time, chooses a permitted task, enters a work date and decimal hours with an optional note, and sees their daily and weekly totals. They can record separate sessions on the same task and date.

**Independent Test**: Enter 2.5 and 1.25 hours on one task for one date; verify two rows, a 3.75-hour day, and matching task/project totals.

**Acceptance Scenarios**:

1. **Given** two permitted entries of 2.5 and 1.25 hours, **Then** Time shows two rows and totals 3.75 hours for that day and task. *(FR-VIS-10, AC-VIS-08)*
2. **Given** a user's entries already total 23 hours on a date, **When** they add 2 more, **Then** the save is refused and no partial entry is recorded. *(FR-VIS-10, AC-VIS-08)*
3. **Given** an entry on a Complete task in an editable project, **Then** it is accepted; **Given** an Archived or Cancelled project, **Then** it is refused. *(FR-VIS-10)*

### User Story 2 - Correct entries without losing their history (Priority: P1)

The entry owner fixes or soft-deletes an error. A PM may correct a project entry with a reason. The activity log retains the actor, prior value, new value, and reason.

**Independent Test**: As the owner edit one entry; as the PM correct another with a reason; as another member try to edit it; then soft-delete and inspect totals and history.

**Acceptance Scenarios**:

1. **Given** Alex's entry, **When** Jill attempts to edit it, **Then** the action is refused; **When** the PM corrects it, **Then** a reason is required and logged. *(AC-VIS-08)*
2. **Given** a deleted entry, **Then** it leaves visible totals but remains in the immutable activity history. *(FR-VIS-10, AC-VIS-08)*

### User Story 3 - Review and export permitted actual hours (Priority: P2)

A PM or lead reviews entries in their scope; a supervisor reviews direct reports within projects they may see. Filters and the Task Hours report yield the same totals and never expose restricted project data.

**Independent Test**: Seed entries in one open and one restricted project, then compare the list, task total, project total, and export as a PM, supervisor, and unrelated user.

**Acceptance Scenarios**:

1. **Given** filtered task-hour entries, **Then** the displayed and exported totals match the sum of visible non-deleted entries. *(FR-VIS-10, §19)*
2. **Given** a restricted project the supervisor cannot view, **Then** its entry's task, project, note, and hours are absent from their Time list and export. *(FR-VIS-10, §8.7)*

### Edge Cases

- Zero, negative, non-numeric, over-24-hour, and daily-total-over-24-hour values are refused.
- Simultaneous entry saves cannot both pass if together they exceed a user's 24-hour day.
- Changing a task's assignee never changes an existing time entry's owner.
- Actual hours never silently update the task estimate, progress, health, workload forecast, invoice, or payroll.
- An entry cannot be moved to a task in another project by editing its task ID; corrections are audited.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Time MUST open a working first-release task-hour view with Add Time, dated entries, daily and weekly totals, filters, and export. *(FR-VIS-01, FR-VIS-09, FR-VIS-10, §13.20)*
- **FR-002**: An entry MUST identify one task, its project, the signed-in owner, work date, positive decimal hours no more than 24, and an optional note; a user's non-deleted entries MUST total no more than 24 hours per date. *(FR-VIS-10)*
- **FR-003**: An owner MUST be able to edit or soft-delete their own entry while the project is editable; a PM correction MUST require a reason; every change MUST be attributed and logged. *(FR-VIS-10)*
- **FR-004**: PM, lead, and supervisor review MUST follow §36.8 and project visibility; totals and exports MUST use only permitted, non-deleted entries. *(FR-VIS-10, §8.7, §19)*
- **FR-005**: Actual hours MUST remain separate from estimated hours, progress, health, workload forecast, billing, and payroll. *(FR-VIS-10, §30)*

### Key Entities *(include if feature involves data)*

- **Task time entry**: Project, task, owner, work date, decimal hours, optional note, creation and update timestamps, soft-deletion state, and version.
- **Task/project time total**: A permission-filtered sum of non-deleted entries, not an independently edited field.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: AC-VIS-08 passes, including arithmetic, permissions, correction reason, daily cap, audit, and estimate separation.
- **SC-002**: Visible list, task, project, and export totals reconcile exactly for each tested permission scope.
- **SC-003**: No over-24-hour day can be created by concurrent saves in the acceptance test.

## Assumptions

- This is task effort capture, not an official payroll or billing timesheet. No timer, approval, billing rate, or payroll submission is specified.
- Depends on packets 001 (identity and audit framework), 002 (project lifecycle and permissions), 004 (tasks), and 009 (filters and export). Packet 011 hardens and verifies it before the pilot.
- Q21 is decided: the Time route is functional in the first release. Any future finance integration requires a separate requirement and review.

## Coordination expansion amendment — 2026-09-26

Packet 029 allocations and packet 032 weekly commitments remain separate from actual-hour entry (§37.6, §38.2). No automatic deduction of actual hours from reservations/estimates or payroll approval is introduced.

This is approved specification scope with implementation pending in the named new packets. Historical verification for this packet does not verify the added behaviour.
