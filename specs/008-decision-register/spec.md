# Feature Specification: Decision Register

**Feature Branch**: `008-decision-register`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 008, the decision register (decisions with internal or external owners and required-by dates, external parties, decisions that block linked work, and recording, deferring, cancelling, and reopening decisions)."

**Source**: Product specification §2.3, §4 principle 8, §7.8, §11.1 (FR-ORG-08), §11.7 (FR-DEC), §12.9, §13.8, §14 Workflow 11, §15.9, §27.1, §31.10

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Raise and track the decisions a project is waiting on (Priority: P1)

Anyone on the team raises a decision that must be made, by the client, an authority, management,
or the team, with an owner, a date it is needed by, and the impact if it is late. External owners
(a client's PM, a municipality) are recorded as external parties without accounts. The register
reads as the agenda for the next client call.

**Why this priority**: Unmade decisions are the most common non-task blocker in design projects.

**Independent Test**: Raise three decisions (one owned by the client), filter the register to
open decisions owned externally, and confirm the columns and sort order.

**Acceptance Scenarios**:

1. **Given** a decision raised with an external party as owner, **Then** it saves with the owner shown with their organisation, and no email is sent outside the organisation. *(AC-DEC-01, DEC-06)*
2. **Given** the register, **Then** it shows key, subject, owner (internal person, or external party with organisation), requested by, date requested, required by, days until or overdue, impact, status, the number of tasks it blocks, and linked deliverables and milestones, sorted overdue first, then by required-by date, then impact. *(FR-DEC-04, §13.8)*
3. **Given** an internal person is made owner of a decision, **Then** they receive an immediate notification. *(§17.2)*

---

### User Story 2 - A late decision visibly holds up the work that depends on it (Priority: P1)

A decision linked to tasks as "blocked by" marks those tasks Blocked once its required-by date
passes, raises a Critical attention item, and shows on the dashboard and in Weekly Coordination,
so a client delay is dated, attributed, and its impact explicit.

**Why this priority**: This turns "three tasks are quietly idle" into a visible, dated blocker with
an owner.

**Independent Test**: Link an overdue decision to two tasks and confirm the Blocked indicators,
the attention item, and the dashboard counts.

**Acceptance Scenarios**:

1. **Given** a decision required by yesterday in status Pending, **Then** it is Overdue, A-04 fires as Critical to the requester and the PM, and the dashboard's overdue decisions count is 1. *(AC-DEC-02, DEC-04)*
2. **Given** that decision is linked as "blocked by" to two tasks, **Then** both tasks show Blocked with the decision as blocker and the project's Blocked count includes them. *(AC-DEC-03, D-15)*
3. **Given** a High-impact decision due within the decision due-soon threshold, **Then** A-04 fires as a Warning. *(DEC-05)*

---

### User Story 3 - Record, defer, cancel, or reopen a decision (Priority: P2)

The owner or PM records the decision with its text and date, which releases the linked tasks and
tells their assignees. When the client commits to a new date, the PM defers the decision with the
new date and a reason; the old date stays in the history.

**Why this priority**: Closing the loop is what makes the register trustworthy, but it only
matters once decisions are being tracked.

**Independent Test**: Take an overdue decision through Deferred to Decided, then reopen it;
confirm the linked tasks, notifications, and history at each step.

**Acceptance Scenarios**:

1. **Given** the PM defers an overdue decision to next week with a reason, **Then** it is Deferred, the previous required-by date appears in its history, the linked tasks return to Waiting, and A-04 clears. *(AC-DEC-04, DEC-03)*
2. **Given** the owner records the decision with text and date, **Then** it is Decided, the linked tasks' assignees are notified, and the decision link on those tasks is satisfied. *(AC-DEC-05, DEC-02)*
3. **Given** a decision Under Review, **When** a user tries to set Decided without decision text, **Then** validation fails. *(AC-DEC-06)*
4. **Given** a Decided decision, **When** the PM reopens it with a reason, **Then** it becomes open again and re-blocks its linked tasks if it is overdue. *(DEC-07)*
5. **Given** a decision is Cancelled with a reason, **Then** its linked tasks are released with the note that the decision was cancelled. *(§14 Workflow 11)*

---

### Edge Cases

- An internal decision owner who becomes Inactive raises A-18 for the decision, and the PM reassigns the owner. *(§14 Workflow 11)*
- A decision that is not yet overdue leaves its linked tasks Waiting, not Blocked. *(D-15)*
- External parties own decisions and actions but never receive notifications; the requester and the PM get the attention items and digest lines instead. *(E-14, DEC-06)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The PM, leads, and team members MUST be able to raise decisions with subject, description, requested by (defaulting to themselves), exactly one owner (an internal person or an external party), date requested (defaulting to today), required-by date, impact if delayed (Low, Medium, or High, plus a description), and links to tasks ("blocked by" or "related"), deliverables, and milestones; owners and the PM edit decisions, and others edit only their own. *(FR-DEC-01, DEC-01, §8.5.2)*
- **FR-002**: Decision status MUST be Pending, Under Review, Decided, Deferred, or Cancelled. *(FR-DEC-02, §10.2)*
- **FR-003**: Decided MUST require the decision text and date and record who decided. *(DEC-02)*
- **FR-004**: Deferred MUST require a new required-by date later than today and a reason, keeping the previous date in the history. *(DEC-03)*
- **FR-005**: A decision past its required-by date while Pending, Under Review, or Deferred MUST be Overdue, block its "blocked by" tasks, and raise A-04 as Critical; a High-impact decision that is due soon MUST raise A-04 as a Warning. *(FR-DEC-03, DEC-04, DEC-05)*
- **FR-006**: No notification MUST go to external parties; the requester and the PM receive the attention items and digest lines. *(DEC-06)*
- **FR-007**: The PM MUST be able to reopen a Decided decision with a reason, re-blocking linked tasks if it is overdue. *(DEC-07)*
- **FR-008**: Cancelling a decision MUST require a reason and release its linked tasks with an information note. *(§14 Workflow 11)*
- **FR-009**: Any team member MUST be able to create external parties for the project (name, organisation, email, role, client flag, notes), and the PM MUST be able to edit them. *(FR-ORG-08, §12.9)*
- **FR-010**: The register MUST filter by status, owner, owner type (internal, external, client), impact, required-by range, and "blocking work"; expand rows to linked items; open the task list filtered to the tasks a decision blocks; and show external owners distinctly with their organisation. *(FR-DEC-04, §13.8)*
- **FR-011**: Recording a decision MUST happen in one dialog with the text, the date, and a "notify linked task assignees" option that is on by default. *(§13.8)*
- **FR-012**: Each decision MUST show its full history of status and date changes. *(FR-DEC-04, §20.1)*

### Key Entities *(include if feature involves data)*

- **Decision**: Subject, description, requester, internal or external owner, dates requested and required, original required-by date, impact level and description, status, decision text and date, decided by, deferral and cancellation reasons.
- **External party**: A client or third-party contact on a project who can own decisions and actions without an account.
- **Item link**: A relation between a decision and a task, deliverable, or milestone, such as "blocked by" or "related".

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A PM can list every decision the client owes, with dates, in under 30 seconds before a client call.
- **SC-002**: 100 % of overdue decisions in testing appear on the dashboard and in Weekly Coordination, with the tasks they block.
- **SC-003**: Assignees of linked tasks learn a decision was recorded within 2 minutes.
- **SC-004**: On pilot projects, every task idle because of a client decision is linked to a dated decision with an owner.

## Assumptions

- The thin register ships in the MVP (Q8 default yes). If it were cut, decisions would be tasks named "DECISION:" with a manual block of type Decision on the blocked work, losing external owners, required-by semantics, and the register view (§12.9).
- The blocking and attention behaviour is computed by packet 005 (D-15, A-04); notifications are delivered by packet 006.
- External parties are project-scoped in the MVP (§12.9 recommendation).
- A decision history view, bulk linking, and a client-facing export of open decisions are Phase 2 (packet 013).

## Coordination expansion amendment — 2026-09-26

Packet 031 may link a decision to a design-basis version (§38.1). A decision outcome and the current basis consumed by each discipline remain separate records; reopening a decision requires assessment, not silent downstream status changes.

This is approved specification scope with implementation pending in the named new packets. Historical verification for this packet does not verify the added behaviour.
