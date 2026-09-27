# Feature Specification: Timeline Scheduling

**Feature Branch**: `018-timeline-scheduling`

**Created**: 2026-09-24

**Status**: Draft (first release; moved by §36)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 018, timeline scheduling (tasks and dependency arrows on the timeline, dragging dates with confirmation, baseline ghost bars, and the milestone cascade reaching tasks), without becoming a scheduling engine."

**Source**: Product specification §11.10 (FR-VIEW-02), §12.3 (M-04), §12.16, §13.5, §30, §36.4

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See tasks and hand-offs on the timeline (Priority: P1)

Under each deliverable bar, the PM expands the tasks as thin bars and sees arrows for task
dependencies and derived deliverable dependencies, highlighted when a predecessor is unfinished
and overdue.

**Why this priority**: Seeing the chain in time is how PMs spot a hand-off that will miss a
submission.

**Independent Test**: Expand a deliverable with six linked tasks and confirm the bars and arrows,
including one highlighted overdue link.

**Acceptance Scenarios**:

1. **Given** an expanded deliverable, **Then** its tasks appear as thin bars from start to due date. *(FR-VIEW-02, §12.16)*
2. **Given** a dependency whose predecessor is unfinished and overdue, **Then** its arrow is highlighted. *(§12.16)*

---

### User Story 2 - Drag to change a date, with a confirmation (Priority: P1)

The PM or lead drags a bar or milestone to a new date. A confirmation shows exactly what will
change, and for milestones offers the cascade. Only dates change; nothing is rescheduled
automatically.

**Why this priority**: Re-dating visually is faster than editing fields, as long as nothing moves
without a decision.

**Independent Test**: Drag a task and a milestone; confirm the dialogs, the logged changes, and
that no other item moved.

**Acceptance Scenarios**:

1. **Given** the user drags a task bar 3 days later, **When** they confirm, **Then** only that task's dates change, the change is logged, and successors are not moved. *(§12.16)*
2. **Given** the user drags a milestone, **Then** the confirmation shows the new date and slip and offers to shift targeted deliverables and their tasks by the same number of days. *(§12.16, M-04)*
3. **Given** a user without permission to change an item's dates, **Then** its bar cannot be dragged. *(§8.5.2)*

---

### User Story 3 - Compare against the original plan (Priority: P2)

Ghost bars show each deliverable's and task's original dates behind the current ones.

**Why this priority**: Slippage over the life of a project becomes visible at a glance.

**Independent Test**: Move a deliverable's due date and confirm the ghost bar stays at the
original.

**Acceptance Scenarios**:

1. **Given** a deliverable whose due date moved by 10 days, **Then** a ghost bar marks its original dates. *(§12.16)*

---

### User Story 4 - The milestone cascade reaches tasks (Priority: P2)

When the PM shifts deliverables with a moved milestone, their tasks can shift with them, after the
same preview.

**Why this priority**: Without it, tasks fall out of line with their deliverables after every
cascade.

**Independent Test**: Cascade a 14-day milestone move to deliverables and tasks and read the log.

**Acceptance Scenarios**:

1. **Given** a cascade with "include tasks" chosen, **When** the PM confirms the preview, **Then** every targeted deliverable and its tasks shift by the same number of days, each change logged. *(M-04)*

---

### Edge Cases

- Automatic scheduling, critical path, float, resource levelling, working-time calendars, constraint types other than Finish-to-Start, cost or earned value, and Microsoft Project or P6 import and export stay out of scope. *(§12.16, §30)*
- Auto-shifting successors when a predecessor moves is never offered. *(Features That Sound Useful But Should NOT Be Built Yet)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The timeline MUST show tasks as thin, collapsible bars under their deliverables. *(FR-VIEW-02, §12.16)*
- **FR-002**: The timeline MUST draw arrows for task dependencies and derived deliverable dependencies, highlighted when a predecessor is unfinished and overdue. *(§12.16)*
- **FR-003**: Users allowed to change an item's dates MUST be able to drag its bar or diamond; a confirmation MUST show the change (and for milestones the optional cascade), and only dates MUST change. *(§12.16)*
- **FR-004**: The timeline MUST show ghost bars at original dates. *(§12.16)*
- **FR-005**: The milestone cascade MUST optionally include the tasks of shifted deliverables, previewed first and logged per item. *(M-04)*
- **FR-006**: The timeline MUST NOT compute critical paths, float, or levelling, or move any item the user did not confirm. *(§12.16, §30)*

### Key Entities *(include if feature involves data)*

- **Original dates**: The first start and due dates of each deliverable and task, kept for baselines.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A PM re-dates a task by dragging in under 10 seconds, with the change confirmed and logged.
- **SC-002**: In testing, no drag or cascade changes any item the user did not see in the confirmation (100 %).

## Assumptions

- Keeping original dates for deliverables and tasks (not only milestones) is added by this packet to support baselines.
- Depends on packets 005 and 010.
