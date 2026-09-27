# Feature Specification: Register Enhancements

**Feature Branch**: `013-register-enhancements`

**Created**: 2026-09-24

**Status**: Draft (Phase 2)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 013, register enhancements (a client-facing export of open decisions, linking a decision to many items at once, a project decision log, and the full issue history of each deliverable)."

**Source**: Product specification §11.4 (FR-DEL-08), §12.4, §12.9, §13.8, §28 (item 2), §32 (E-24)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Send the client the decisions we need from them (Priority: P1)

Before a client call, the PM exports the open decisions the client owns as a clean list: subject,
what must be decided, the date it is needed by, the impact if late, and status, without internal
notes.

**Why this priority**: The register already reads as the client-call agenda; sharing it removes
retyping.

**Independent Test**: Export the open client-owned decisions of a project and check the content.

**Acceptance Scenarios**:

1. **Given** a project with 5 open decisions, 3 owned by the client, **When** the PM exports "open decisions for the client", **Then** the file lists the 3 client-owned decisions with subject, description, required-by date, days until or overdue, impact, and status, sorted overdue first, and contains no internal notes or comments. *(§28 item 2)*

---

### User Story 2 - Link one decision to many items at once (Priority: P2)

When one decision holds up a whole package, the PM links it to many tasks, deliverables, or
milestones in one action.

**Why this priority**: Linking tasks one by one is where decision blocks get missed.

**Independent Test**: Select 12 tasks and link them to one decision as "blocked by".

**Acceptance Scenarios**:

1. **Given** 12 selected tasks, **When** the PM links them to a decision as "blocked by", **Then** all 12 links are created in one action, each is logged, and the decision's blocking count shows 12. *(§28 item 2)*

---

### User Story 3 - Read the project's decision log (Priority: P2)

A chronological log shows what was decided, when, and by whom, including deferrals and
cancellations with their reasons.

**Why this priority**: Months later, "when did the client confirm the pavement structure?" needs
an answer in seconds.

**Independent Test**: Open the decision log of a project with 8 decided decisions and 2 deferrals.

**Acceptance Scenarios**:

1. **Given** a project's decision log, **Then** it lists decided, deferred, and cancelled decisions newest first with the decision text or reason, dates, and who decided. *(§28 item 2)*

---

### User Story 4 - Keep every issue of a deliverable (Priority: P2)

Each time a deliverable is issued (Rev A, Rev B, Rev 0), the issue is kept in a list with its
date, revision, recipient, transmittal link, and note, so the full submission history is visible.

**Why this priority**: Engineering packages go through several revisions; overwriting the last
issue loses the record.

**Independent Test**: Issue a deliverable, return it for revision, and issue it again; confirm
both issues are listed.

**Acceptance Scenarios**:

1. **Given** a deliverable issued as Rev A, returned by the client, and issued again as Rev B, **Then** its issue history lists both issues with their dates, revisions, recipients, and transmittal links. *(FR-DEL-08, E-24)*

---

### Edge Cases

- An export with no open client-owned decisions says so instead of producing an empty file.
- Bulk linking skips items the user may not edit and reports them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The PM MUST be able to export the open decisions owned by the client or another chosen external party, with subject, description, required-by date, days until or overdue, impact, and status, excluding internal notes and comments. *(§28 item 2)*
- **FR-002**: Users allowed to edit a decision MUST be able to link it to many tasks, deliverables, or milestones in one action, with each link logged and unauthorised items reported. *(§28 item 2)*
- **FR-003**: Each project MUST offer a decision log listing decided, deferred, and cancelled decisions newest first with text or reason, dates, and who decided. *(§28 item 2)*
- **FR-004**: Every issue of a deliverable MUST be kept in an issue history with date, revision, recipient, transmittal link, and note; the latest issue MUST still show on the deliverable. *(FR-DEL-08, E-24)*

### Key Entities *(include if feature involves data)*

- **Deliverable issue**: One issue of a deliverable: date, revision, recipient, transmittal link, note, who issued it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A PM produces the client's open-decision list in under 1 minute.
- **SC-002**: Linking one decision to 12 tasks takes one action.
- **SC-003**: Every issue of every deliverable is retrievable from its history (100 % in testing).

## Assumptions

- The specification names these enhancements in one line (§28 item 2 and FR-DEL-08); the details above are reasonable defaults to confirm with `/speckit-clarify` before planning.
- Depends on packets 003 and 008.

## Coordination expansion amendment — 2026-09-26

Existing revision, issued-to and transmittal links remain. Packets 027/028 add explicit revision-use and immutable submission manifests (§37.4, §37.5); they do not create a document-management or transmission service.

This is approved specification scope with implementation pending in the named new packets. Historical verification for this packet does not verify the added behaviour.
