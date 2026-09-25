# Feature Specification: Team Calendar

**Feature Branch**: `023-team-calendar`

**Created**: 2026-09-24

**Status**: Draft (first release)

**Input**: The user confirmed the pictured Calendar view for the first release. The six-panel image at `docs/reference/six-view-workspace.png` records feature-scope context; its visual design is illustrative. The downloaded V2 prototype is visual reference only.

**Source**: Product specification §36.5 and §36.9 (FR-VIS-06, AC-VIS-04), §8.7, §10.2, §12.11, §15 G-03

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See the team's week, month, and agenda (Priority: P1)

A team member opens Calendar for selected projects, changes between Week, Month, and Agenda, and toggles deadlines, meetings, site work, and internal task events. Due dates appear as all-day entries and timed events show their actual start and end.

**Independent Test**: Seed one of each calendar type in a week; compare all three views and each toggle against its source records.

**Acceptance Scenarios**:

1. **Given** a task due date, a deliverable due date, and a milestone date, **Then** each appears as an all-day Project Deadline and opens its source item. *(FR-VIS-06)*
2. **Given** a meeting, site visit, and internal event in one week, **Then** the Week, Month, and Agenda views show them under the right toggles, with times where applicable. *(AC-VIS-04)*
3. **Given** a user cannot view a restricted project, **Then** its events and deadlines reveal no title, time, location, or count to them. *(AC-VIS-04)*

### User Story 2 - Create and maintain a calendar event (Priority: P1)

An authorised user selects New Event, enters its type, title, start, end, owner, and optional project/location/description, and later edits or cancels it.

**Independent Test**: Create a meeting, edit its time, cancel it, and verify the calendar, permissions, and activity history each step.

**Acceptance Scenarios**:

1. **Given** an event end before its start, **When** the user saves, **Then** the form rejects it with a field error. *(FR-VIS-06)*
2. **Given** a project event, **When** its owner or project PM edits it, **Then** the change is saved and logged; another user is refused. *(FR-VIS-06)*
3. **Given** an existing deadline entry, **When** a user moves it, **Then** the source item's permission and date-change rules apply; cancelling the change preserves the source date. *(FR-VIS-06)*

### Edge Cases

- A date-only deadline is all-day and is never assigned an invented time.
- Timed events use the organisation time zone in the first release; a time zone label appears in the editor and view.
- Overlapping events both remain visible.
- A meeting event may link to a meeting record but does not silently create a meeting action, task, or recurring series.
- Cancelled events are absent from default views but retained in the audit log.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Calendar MUST provide Week, Month, and Agenda views, today and previous/next controls, selected-project scope, and the four type toggles. *(FR-VIS-06, §36.5)*
- **FR-002**: Permitted task, deliverable, and milestone due dates MUST appear as all-day Project Deadline projections and open their source items. *(FR-VIS-06)*
- **FR-003**: Users MUST be able to create, edit, cancel, and open Meeting, Site Work, and Internal Task events with the fields and permissions in §36.5. *(FR-VIS-06)*
- **FR-004**: Calendar editing of a projected deadline MUST follow its source item's permissions, reason, cascade, and logging rules. *(FR-VIS-06)*
- **FR-005**: Calendar queries, counts, and event details MUST enforce project visibility and prevent restricted data disclosure. *(AC-VIS-04, §8.7)*

### Key Entities *(include if feature involves data)*

- **Calendar event**: Type, title, project when applicable, start, end, organisation time zone, owner, optional location and description, visibility, cancellation state, and activity history.
- **Deadline projection**: A read-only calendar representation of a permitted task, deliverable, or milestone date; the source record owns that date.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The four-type acceptance scenario AC-VIS-04 passes in Week and Agenda views and each type toggle hides exactly its own entries.
- **SC-002**: A user can create a valid timed event from New Event and find it in Week, Month, and Agenda without re-entry.
- **SC-003**: Restricted event information is absent for unauthorised users in all views and direct event links.

## Assumptions

- Depends on packets 001 (users and time zone), 002 (projects and permissions), 003 and 004 (deadline sources), and 006 (activity log conventions). It is first-release scope under §27 and §36.
- This internal calendar is separate from the later personal ICS feed in §29. It does not sync to Outlook or Teams in the first release.
