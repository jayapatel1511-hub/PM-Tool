# Feature Specification: Six-View Workspace

**Feature Branch**: `022-six-view-workspace`

**Created**: 2026-09-24

**Status**: Draft (first release)

**Input**: The user confirmed all six pictured views for the first release. The six-panel image is preserved at `docs/reference/six-view-workspace.png` as feature-scope context. The downloaded Coordination Hub V2 prototype is preserved at `docs/reference/coordination-hub-v2/` solely for visual cues, not as a design or scope specification.

**Source**: Product specification §11.14, §36.1–§36.4 and §36.6–§36.9 (FR-VIS-01 through FR-VIS-05, FR-VIS-07 through FR-VIS-09; AC-VIS-01, AC-VIS-02, AC-VIS-03, AC-VIS-05, AC-VIS-06, AC-VIS-07), plus §13.0–§13.5, §13.10–§13.12, §18.4 and decision Q21; task-hour behavior (FR-VIS-10, AC-VIS-08) belongs to packet 024

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Move among the six views without losing project scope (Priority: P1)

A PM selects the projects in an Atlantic Civil workspace and moves among Projects, Board, Timeline, Calendar, Dashboard, and My Work. Every visible navigation item has a working destination, and each view shows only projects they can see.

**Independent Test**: Select two permitted and one restricted project, switch through all six views, and verify the permitted scope and no restricted disclosure.

**Acceptance Scenarios**:

1. **Given** a named workspace with three permitted projects, **When** a user switches views, **Then** the same three-project selection remains and every visible tab opens its view. *(FR-VIS-01, FR-VIS-02, AC-VIS-07)*
2. **Given** a restricted project outside the user's access, **Then** it contributes no row, card, bar, calendar entry, dashboard count, search result, or export. *(FR-VIS-02)*

### User Story 2 - Scan projects, tasks, and dates visually (Priority: P1)

The Projects board has grouped rows with PM, status, priority, due, and derived progress. The cross-project task board presents four lanes with project chips. The Gantt shows project and task hierarchy, dates, dependencies, and a today line.

**Independent Test**: Seed five Active and three Setup projects with tasks in each canonical status, then compare project counts, board lane counts, and Gantt rows with their source lists.

**Acceptance Scenarios**:

1. **Given** a project without non-Cancelled tasks, **Then** its progress shows “—”; **Given** five Active and three Setup projects, **Then** their group counts are 5 and 3. *(FR-VIS-03, AC-VIS-01)*
2. **Given** a review-required task, **When** a user moves its card toward Done, **Then** the review guard is enforced; its project label and exact review status remain visible. *(FR-VIS-04, AC-VIS-02)*
3. **Given** two projects with dated tasks, milestones, and dependencies, **Then** both expand in the Gantt with bars, diamonds, and arrows; **When** a drag is cancelled, **Then** no date changes. *(FR-VIS-05, AC-VIS-03)*

### User Story 3 - See an actionable overview and personal work (Priority: P1)

Home shows reconciled metrics, task charts, deadlines, and a personally adjustable layout. My Work shows due buckets, inbox, saved views, and guarded task actions.

**Independent Test**: Seed known project health and task states; calculate the dashboard metrics independently, then test creator versus assignee membership in My Work.

**Acceptance Scenarios**:

1. **Given** known projects and task states, **Then** each metric and chart segment opens a matching filtered list, and the date range changes Upcoming Deadlines without changing current totals. *(FR-VIS-07, AC-VIS-05)*
2. **Given** Alex created a task assigned to Jill, **Then** it appears in Alex's Created by Me and Jill's Assigned to Me, and a completion checkbox cannot skip required review. *(FR-VIS-08, AC-VIS-06)*

### User Story 4 - Open Files, Workload, and Time from the visible navigation (Priority: P2)

The Files view is a searchable library of external document links. Workload opens the person-by-week resource view for an authorised user. Time opens task-hour entry. None is a dead tab.

**Independent Test**: Open both views with a permitted and a restricted project; confirm document titles, paths, and workload records respect access.

**Acceptance Scenarios**:

1. **Given** a network folder link, **Then** Files shows Copy path, not an upload action. *(FR-VIS-09)*
2. **Given** a user without Resource access, **Then** Workload is hidden; **Given** an authorised supervisor, **Then** it opens the eight-week grid. *(FR-VIS-01, FR-VIS-09)*
3. **Given** an authenticated project member, **When** they open Time, **Then** the task-hour view opens and Add Time follows packet 024. *(FR-VIS-01, FR-VIS-09, Q21)*

### Edge Cases

- No Active projects with evaluated health yields “—” for On Track rather than 0%.
- No tasks yields “—” project progress; Cancelled tasks leave the denominator.
- A task in Revision Required stays in Review with its exact status, never in Done.
- A cross-project Gantt never draws an arrow that asserts an unsupported cross-project dependency.
- Role changes and restricted project access are rechecked when a saved view or dashboard layout opens.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Navigation, named project scope, and scope preservation MUST follow §36.1 (FR-VIS-01, FR-VIS-02).
- **FR-002**: The grouped Projects board and project priority/progress rules MUST follow §36.2 (FR-VIS-03).
- **FR-003**: The cross-project four-lane task board MUST follow §36.3 (FR-VIS-04).
- **FR-004**: The cross-project task-level Gantt and guarded date changes MUST follow §36.4 (FR-VIS-05).
- **FR-005**: The overview dashboard MUST use the metrics, chart mapping, date range, drill-downs, and personal layout controls in §36.6 (FR-VIS-07).
- **FR-006**: My Work MUST provide the due buckets, inbox, creator/assignee views, and guarded completion in §36.7 (FR-VIS-08).
- **FR-007**: Files, Workload, and Time MUST have working first-release destinations as in §36.8; task-hour behavior is owned by packet 024. *(FR-VIS-09, Q21)*

### Key Entities *(include if feature involves data)*

- **Workspace project selection**: A named set of project IDs visible to the current user; permission is enforced when read.
- **Project priority and derived progress**: Priority is stored on a project; progress is calculated from non-Cancelled tasks.
- **Personal dashboard layout**: A user's ordered and hidden state for the fixed widgets; metric definitions remain shared.
- **Saved view and card order**: Reuse packet 019, with selected project scope included.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All eight visual acceptance scenarios (AC-VIS-01 through AC-VIS-08, including the calendar scenario owned by packet 023 and Time scenario owned by packet 024) pass before first-release sign-off.
- **SC-002**: Dashboard values, project group counts, board lane counts, and Gantt rows reconcile with the underlying permitted lists in every seeded acceptance case.
- **SC-003**: Every navigation item visible at desktop and tablet widths opens a working, keyboard-operable destination.

## Assumptions

- Depends on packets 002, 004, 005, 006, 007, 009, 010, 016, 017, 018, 019, 023, and 024. Their work is part of the first release under §27 and §36, despite older packet creation text describing some as Phase 2.
- The screenshot's names, dates, figures, avatars, colours, and layout are illustrative; the six view types and behaviors in §36 are required. V2 is the sole downloaded UI visual reference, and its old Phase 2 badges or read-only Timeline do not set release scope.
- Canonical status transitions from §10 and server-side permissions from §8 remain authoritative. The four board lanes are presentation groups.

## Coordination expansion amendment — 2026-09-26

The existing workspace scope persists into the discipline coordination projection (§37.7). Approved but unimplemented packets do not create active tabs or dead controls; source records remain permission filtered.

This is approved specification scope with implementation pending in the named new packets. Historical verification for this packet does not verify the added behaviour.
