# Feature Specification: Meeting Actions

**Feature Branch**: `015-meeting-actions`

**Created**: 2026-09-24

**Status**: Draft (Phase 2)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 015, meetings and meeting actions (recording meetings, assigning actions to people, disciplines, or external parties, capturing them live in Weekly Coordination, routing them, and converting them to tasks)."

**Source**: Product specification §9.5, §11.7 (FR-MTG), §12.11, §12.13 (meeting mode), §13.13, §14 Workflow 9, §19

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record meetings and the actions agreed in them (Priority: P1)

The PM records a meeting (title, date, type, link to the minutes) and the actions agreed in it,
each with an owner, which can be a person, a discipline, or an external party, and a due date.

**Why this priority**: Actions agreed in meetings are the work most often forgotten.

**Independent Test**: Record a client meeting with three actions owned by a person, a discipline,
and the client.

**Acceptance Scenarios**:

1. **Given** a meeting "Client Progress Meeting #4", **When** the PM adds three actions with different owner types and due dates, **Then** each gets a key such as `1234-A07` and appears under the meeting. *(FR-MTG-01..05, §12.11)*
2. **Given** an action, **Then** its status moves Open, In Progress, Complete, or Cancelled. *(§10.2)*

---

### User Story 2 - Capture actions live in the coordination meeting (Priority: P1)

In Weekly Coordination meeting mode, the PM creates actions inline against the current meeting as
they are agreed.

**Why this priority**: Capturing actions as they are spoken is what makes them reliable.

**Independent Test**: In meeting mode, create two actions from the Blocked work section and find
them under that day's meeting.

**Acceptance Scenarios**:

1. **Given** meeting mode, **When** the PM creates an action from a row, **Then** it is attached to the current meeting record, linked to that row's item, and shown in the "Changes made in this meeting" tray. *(MTG-04)*

---

### User Story 3 - Actions go to the right place (Priority: P2)

Actions owned by a discipline appear in that discipline lead's My Work and attention. Actions owned
by an external party send nothing outside the organisation and appear in Weekly Coordination under
"Waiting on client / external".

**Why this priority**: Routing turns a list of actions into owned work.

**Independent Test**: Create a discipline-owned and a client-owned action and check the lead's My
Work and the meeting section.

**Acceptance Scenarios**:

1. **Given** an action owned by the Civil discipline, **Then** it appears in the Civil lead's My Work. *(MTG-01)*
2. **Given** an action owned by the client, **Then** no notification leaves the organisation and it appears under "Waiting on client / external". *(MTG-02)*

---

### User Story 4 - Turn an action into a task (Priority: P2)

When an action turns out to be real work, the PM converts it into a task pre-filled from the
action; the action then follows the task's completion.

**Why this priority**: Prevents the same work living in two places.

**Independent Test**: Convert an action, complete the task, and check the action.

**Acceptance Scenarios**:

1. **Given** an action, **When** the PM converts it, **Then** a linked task is created from it, and completing the task completes the action. *(MTG-03)*

---

### Edge Cases

- Agendas, minute-taking, attendance, automatic calendar synchronisation, and recurring meeting series are not built. A meeting may link to a first-release calendar event (§36.5). *(§12.11)*
- An action owned by a discipline without a lead is flagged to the PM.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The PM and leads MUST be able to record meetings with title, date, type (Coordination, Client, Design Review, Site, Other), and a link to the minutes. *(FR-MTG-01..05, §12.11)*
- **FR-002**: Actions MUST have a key, text, owner type (User, Discipline, External Party) with the matching owner, due date, status (Open, In Progress, Complete, Cancelled), optional related task and decision, and comments. *(§12.11, §9.5)*
- **FR-003**: Meeting mode MUST allow creating actions inline against the current meeting. *(MTG-04)*
- **FR-004**: Discipline-owned actions MUST route to the discipline lead's My Work and attention. *(MTG-01)*
- **FR-005**: External-party actions MUST send nothing outside the organisation and appear in Weekly Coordination under "Waiting on client / external". *(MTG-02)*
- **FR-006**: Converting an action MUST create a pre-filled task linked to it, and the action MUST follow the task's completion. *(MTG-03)*
- **FR-007**: The actions register MUST list actions by due date, grouped by meeting, with a "raise from here" option on task and deliverable panels. *(§13.13)*
- **FR-008**: The Meeting Actions Outstanding report MUST be available by project and owner type. *(§19)*

### Key Entities *(include if feature involves data)*

- **Meeting**: Title, date, type, minutes link, author.
- **Meeting action**: Key, meeting, text, owner (person, discipline, or external party), due date, status, related task and decision.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every action agreed in a pilot coordination meeting is captured in the Hub before the meeting ends.
- **SC-002**: A PM creates an action in meeting mode in under 20 seconds.
- **SC-003**: No converted action and its task disagree about completion (100 % in testing).

## Assumptions

- Depends on packets 007 (Weekly Coordination and My Work) and 008 (external parties).

## Coordination expansion amendment — 2026-09-26

Packet 030 reuses meeting actions for coordination follow-up; packet 032 adds performer-confirmed output commitments (§37.7, §38.2). A chair proposing an action does not commit work for another person, and copied meeting summaries do not change source states.

This is approved specification scope with implementation pending in the named new packets. Historical verification for this packet does not verify the added behaviour.
