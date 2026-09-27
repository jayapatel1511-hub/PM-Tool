# Feature Specification: Timeline and Extras

**Feature Branch**: `010-timeline-and-extras`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 010, timeline and extras (the read-only timeline of milestones and deliverables, shifting deliverables with a moved milestone, copying a project's structure, and reassigning a departing person's work in bulk)."

**Source**: Product specification §11.3 (FR-MS-05), §11.10 (FR-VIEW-01), §11.12 (FR-ADM-02), §12.3 (M-04), §12.16, §13.5, §13.15, §27.1, §27.3, §32 (E-01, E-09)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See milestones and deliverables in time (Priority: P2)

The PM, leads, and Executives see a read-only timeline: milestones as diamonds on a top lane and
deliverables as bars grouped by discipline, with progress, status colour, a today line, and
overdue segments, to spot crowding before a submission.

**Why this priority**: "What is coming, in what order" is a common PM question, and a read-only
view answers it without becoming a scheduling tool.

**Independent Test**: Open the timeline of a project with 3 disciplines and 20 deliverables,
switch zoom, filter, and open an item.

**Acceptance Scenarios**:

1. **Given** a project timeline, **Then** it shows a today line, the project start and target dates, milestones coloured by status with name and date, and deliverable bars from start (or creation) to due date grouped by discipline with progress fill. *(FR-VIEW-01, §12.16)*
2. **Given** an overdue deliverable, **Then** its bar extends to today with a hatched segment; **Given** a slipped milestone, **Then** a hollow marker shows its original date. *(§12.16)*
3. **Given** the user clicks a bar or diamond, **Then** its detail panel opens. *(§13.5)*
4. **Given** the timeline, **Then** it states that it is read-only and offers no dragging. *(§13.5)*

---

### User Story 2 - Move a milestone and shift its deliverables with it (Priority: P2)

When a submission date moves, the PM can shift every deliverable targeting it by the same number
of days, after previewing the new dates.

**Why this priority**: Without it, a client extension means re-dating deliverables one by one.

**Independent Test**: Move a milestone by 14 days with the cascade on, check the preview, confirm,
and read the log.

**Acceptance Scenarios**:

1. **Given** the cascade option is chosen when a milestone moves by 14 days, **When** the PM confirms after the preview, **Then** every deliverable targeting it has its due date shifted by +14 days and each shift is logged separately. *(AC-MS-05, M-04, FR-MS-05)*

---

### User Story 3 - Start a project from another project's structure (Priority: P2)

Until templates exist, a PM starts a new project by copying an existing project's disciplines,
milestones (without dates), deliverables, tasks (unassigned), and dependencies, then adjusts them.

**Why this priority**: It is a cheap stopgap that saves hours of set-up on repeat project types.

**Independent Test**: Copy the structure of a finished project into a new one and compare the
counts of disciplines, milestones, deliverables, tasks, and dependencies.

**Acceptance Scenarios**:

1. **Given** a PM creating a project chooses to copy the structure of 1234, **Then** the new project gets 1234's disciplines, undated milestones, deliverables, unassigned tasks, and dependencies, and is created in Setup. *(§27.1)*

---

### User Story 4 - Reassign a departing person's work in one place (Priority: P2)

When someone leaves or goes on long leave, an Admin, or the person's supervisor, sees everything
they own across all projects (tasks, deliverables, decisions, reviews, and lead roles) and
reassigns it in bulk.

**Why this priority**: Turnover is certain; the alternative is reassigning items one at a time
across projects.

**Independent Test**: Deactivate a user who owns 9 items across 3 projects and reassign them all
from one screen.

**Acceptance Scenarios**:

1. **Given** an Inactive user who owns 9 open items across 3 projects, **When** an Admin opens Reassign work, **Then** every item is listed with its project and can be reassigned one by one or all at once; each change is logged and the new owners and PMs are notified. *(FR-ADM-02, E-01)*
2. **Given** a Supervisor, **Then** Reassign work is available only for their direct reports. *(§25.7)*

---

### Edge Cases

- Moving a milestone earlier without the cascade flags deliverables now due after it. *(E-09)*
- Timeline labels never overlap; long names are truncated with the full name on hover. *(§13.5)*
- Copying a structure never copies assignments, dates, comments, or history.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The timeline MUST show a time axis with week, month, and quarter zoom, a today line, and the project's start and target dates. *(FR-VIEW-01, §12.16, §13.5)*
- **FR-002**: Milestones MUST appear as diamonds on a top lane coloured by status and labelled with name and date, with a hollow marker at the original date when slipped. *(§12.16)*
- **FR-003**: Deliverables MUST appear as bars from start date (or creation date) to due date, grouped by discipline with collapsible groups, with progress fill and status colour; overdue bars MUST extend to today with a hatched segment. *(§12.16)*
- **FR-004**: Clicking a timeline element MUST open its detail panel; filters MUST cover discipline, milestone, status, hide completed, and date range; a print-friendly layout and a visible legend MUST be provided. *(§12.16, §13.5)*
- **FR-005**: The timeline MUST be read-only in this packet. *(§12.16)*
- **FR-006**: When changing a milestone date, the PM MUST be able to shift the due dates of all deliverables targeting it by the same number of days after a preview, with each shift logged separately. *(FR-MS-05, M-04)*
- **FR-007**: When creating a project, a PM MUST be able to copy another project's disciplines, milestones without dates, deliverables, tasks without assignees, and dependencies. *(§27.1)*
- **FR-008**: Admins, and Supervisors for their direct reports, MUST be able to list every open item a user owns across projects (tasks as assignee or reviewer, deliverables, decisions, and lead roles) and reassign items one by one or in bulk, with each change logged and the new owners and PMs notified. *(FR-ADM-02, E-01, §13.15)*

### Key Entities *(include if feature involves data)*

- No new entities; this packet presents and changes milestones, deliverables, tasks, dependencies, decisions, and team roles defined in earlier packets.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A PM can see which deliverables crowd the next submission within 10 seconds of opening the timeline.
- **SC-002**: A cascade of any size shifts every targeted deliverable and logs each shift (100 % in testing).
- **SC-003**: Copying a project's structure takes under 1 minute and reproduces every discipline, milestone, deliverable, task, and dependency.
- **SC-004**: All of a departing person's open work can be reassigned from one screen in one session.

## Assumptions

- These four items are MVP-Recommended (§27.2): each can be cut independently if the schedule demands.
- Tasks on the timeline, dependency arrows, dragging, baselines, and cascading to tasks follow in first-release packet 018; packet 010 delivers a read-only baseline first. Project templates replace copying structure in Phase 2 (packet 012).
- Notifications named here are delivered by packet 006.
