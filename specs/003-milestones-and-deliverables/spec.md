# Feature Specification: Milestones and Deliverables

**Feature Branch**: `003-milestones-and-deliverables`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 003, milestones and the engineering deliverables register (milestone planning, date changes, completion, deliverable definition, the deliverable lifecycle with review and issue)."

**Source**: Product specification §9.1, §9.2, §10.2 (deliverable and milestone status), §11.3, §11.4, §12.3, §12.4, §13.6, §13.7, §14 Workflows 3 and 12, §15.4, §15.5, §31.5, §31.6, Appendix C

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The PM plans the project's milestones (Priority: P1)

The PM records the dated checkpoints that engineering projects are judged by, above all the
design submissions: name, type, date, description, an optional discipline tag, the phase it
completes, and whether it is client-facing. The original date is kept so slippage is always
visible. Only the PM changes milestone dates, with a reason.

**Why this priority**: Submissions are how clients judge engineering projects, and every
deliverable targets one.

**Independent Test**: Create four milestones including two submissions, move one date with a
reason, complete one, and cancel one; confirm slip, next milestone, next submission, and logs.

**Acceptance Scenarios**:

1. **Given** a milestone created with date 2027-01-29, **When** the PM changes the date to 2027-02-12 with a reason, **Then** the original date remains 2027-01-29, the slip shows 14 days, the change is logged with old and new values, every Discipline Lead receives an in-app notification, and any deliverable due after 2027-02-12 is flagged Date Inconsistent. *(AC-MS-04, M-02, M-03; notification verified with packet 006, flag with packet 005)*
2. **Given** a milestone with an un-issued deliverable, **When** the PM marks it Complete, **Then** a confirmation lists the deliverable, and after confirmation the milestone is Complete with today's date while the deliverable keeps its status. *(AC-MS-06, M-05, M-06)*
3. **Given** a Discipline Lead for Civil, **When** they try to change a milestone date, **Then** the action is not offered and a direct attempt is refused with a message naming the Project Manager role. *(AC-PERM-01)*
4. **Given** a milestone that completes the phase "Preliminary Design", **When** the PM completes it, **Then** the Hub asks whether to advance the project phase and never advances it on its own. *(§12.3, §9.3)*
5. **Given** a milestone dated more than 7 days in the future, **When** the PM tries to complete it, **Then** completion is refused until the date is moved. *(M-09)*

---

### User Story 2 - Discipline Leads define their deliverables (Priority: P1)

A Discipline Lead (persona Marc) lists what their discipline must produce for each submission:
drawing packages, reports, calculations, estimates, and so on. Each deliverable has a type, an
owner (the lead by default), an optional reviewer, an optional target milestone, a due date,
a priority, and whether it needs internal review before issue.

**Why this priority**: Deliverables are what the work produces and how milestone readiness is
judged.

**Independent Test**: As the Civil lead, create five deliverables targeting the 60 % submission
and confirm defaults, permissions, and the register view.

**Acceptance Scenarios**:

1. **Given** a Discipline Lead creates a deliverable with a target milestone and no due date, **Then** the due date defaults to the milestone date. *(AC-DEL-01, FR-DEL-06)*
2. **Given** a Discipline Lead for Civil, **When** they create a deliverable in Civil, **Then** it succeeds; **When** they try to create one in Electrical, **Then** it is refused. *(AC-PERM-02)*
3. **Given** a deliverable due after its milestone date, **When** it is saved, **Then** it saves with a Date Inconsistent warning rather than an error. *(DL-03)*
4. **Given** the register, **Then** deliverables are grouped by discipline by default and can be grouped by milestone, status, or owner, filtered, sorted, and expanded to show their tasks inline. *(FR-DEL-05, §13.6)*

---

### User Story 3 - Deliverables pass review and are issued (Priority: P1)

A deliverable moves from Not Started through In Progress, In Review, and Ready to Issue to
Issued, and optionally Accepted. A deliverable that requires review cannot be issued without
passing In Review. Issuing records the date, revision, recipient, and an optional transmittal
link. Issuing with open tasks needs a confirmation.

**Why this priority**: Issue is the moment that matters to the client, and review is the
engineering control before it.

**Independent Test**: Take one deliverable through review, a revision round, and issue with one
open task; confirm guards, prompts, and recorded values.

**Acceptance Scenarios**:

1. **Given** a deliverable that requires review and is In Progress, **When** the owner tries Ready to Issue, **Then** the transition is refused with the message that it must pass In Review first. *(AC-DEL-03, DL-04)*
2. **Given** a deliverable In Review, **When** the reviewer sets Revision Required with a comment, **Then** its status is Revision Required, the comment is stored as a review comment, and the owner is notified. *(AC-DEL-04; notification verified with packet 006)*
3. **Given** a deliverable Ready to Issue with 1 open task, **When** the owner issues it, **Then** a confirmation lists the open task; after confirmation it is Issued with date, revision, and recipient recorded, and the task shows "Deliverable issued with task open". *(AC-DEL-05, DL-05, DL-06)*
4. **Given** an Issued deliverable that the client returns with comments, **When** the PM or lead sets Revision Required, **Then** the issued date and revision are kept and the next issue records the new revision. *(E-24)*

---

### User Story 4 - The PM sees whether each milestone is ready (Priority: P2)

The milestone view shows every milestone on a strip and in a table with its date, original date,
slip, days remaining, and readiness: how many targeted deliverables are issued and how many of
their tasks are complete, overdue, or blocked.

**Why this priority**: Readiness turns a list of dates into an early warning, but it depends on
deliverables and tasks existing.

**Independent Test**: With a milestone targeted by five deliverables, two issued, confirm the
table shows "2 of 5 issued" and expands to the deliverables.

**Acceptance Scenarios**:

1. **Given** a milestone targeted by 5 deliverables of which 2 are Issued, **Then** its row shows "2/5 issued" and expands to each deliverable with status, owner, due date, and progress. *(FR-MS-06)*
2. **Given** a slipped milestone, **Then** the strip shows a hollow marker at the original date next to the current one. *(§13.7)*

---

### Edge Cases

- A milestone moved earlier flags every targeted deliverable now due after it. *(E-09)*
- A cancelled milestone keeps its links but leaves evaluation; its deliverables are flagged "Milestone cancelled, retarget", and the PM gets a bulk retarget dialog. *(M-07, E-17)*
- Retargeting a deliverable to another milestone is logged and re-evaluates both milestones. *(E-16)*
- A deliverable with tasks cannot be deleted; it can be cancelled with a reason, or its tasks moved first. *(DL-09)*
- Changing a deliverable's discipline moves its tasks' discipline after confirmation. *(DL-10)*
- A reopened Complete milestone clears its completed date and returns to its derived status. *(M-10)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The PM MUST be able to create milestones with name, date, type (Kickoff, Field Work, Design Submission, Client Workshop, Permit Submission, Tender, Construction, IFC, Record Drawings, Closeout, Other), description, optional discipline tag, optional completes-phase, and client-facing flag. *(FR-MS-01, §12.3, M-01)*
- **FR-002**: System MUST treat Design Submission, Permit Submission, Tender, and IFC as submissions and show the next milestone and next submission for each project. *(M-08)*
- **FR-003**: System MUST keep each milestone's original date permanently and show slip as the difference from the current date. *(FR-MS-03)*
- **FR-004**: Changing a milestone date MUST be limited to the PM, require a reason, log old and new values, and notify the PM and all Discipline Leads. *(FR-MS-04, M-02, G-09)*
- **FR-005**: Only the PM MAY mark a milestone Complete or Cancelled; completing with deliverables not Issued, Accepted, or Cancelled MUST require confirmation, default the completed date to today (editable to a past date only), and be refused more than 7 days before the milestone date. *(FR-MS-02, FR-MS-07, M-05, M-06, M-09)*
- **FR-006**: Completing a milestone that completes a phase MUST offer to advance the project phase and MUST NOT advance it automatically. *(§12.3, §9.3)*
- **FR-007**: Cancelling a milestone MUST keep its links, exclude it from evaluation, flag its deliverables for retargeting, and offer bulk retargeting. *(M-07, E-17)*
- **FR-008**: The PM MUST be able to reopen a Complete milestone with a reason. *(M-10)*
- **FR-009**: The milestone view MUST show a strip and a table with key, name, type, date, original date, slip, status with its reason, days remaining, deliverables issued out of total, task counts under them, discipline tag, and client-facing flag, expandable to targeted deliverables. *(FR-MS-06, §13.7)*
- **FR-010**: The PM and Discipline Leads (in their own discipline) MUST be able to create deliverables with name, discipline, type, owner, reviewer, target milestone, start date, due date, priority, revision, description, and a requires-review flag (on by default). *(FR-DEL-01, DL-01, §12.4)*
- **FR-011**: System MUST require discipline, type, and owner on every deliverable, default the owner to the discipline's lead, and default the due date to the target milestone's date. *(DL-01, FR-DEL-06)*
- **FR-012**: System MUST warn, not refuse, when a deliverable is due after its milestone, and MUST require the start date to be on or before the due date. *(DL-03, DL-13)*
- **FR-013**: Deliverable status MUST follow the lifecycle in §10.2 and Appendix C: Not Started to In Progress; In Progress to In Review; In Review to Revision Required or Ready to Issue; Revision Required back to In Progress; Ready to Issue to Issued; Issued to Accepted or Revision Required; any non-terminal status to On Hold with a reason and back to the previous status; any status to Cancelled with a reason by the PM or lead. *(FR-DEL-02, DL-02)*
- **FR-014**: A deliverable that requires review MUST reach Ready to Issue only from In Review, and In Review MUST require a reviewer. *(DL-04)*
- **FR-015**: Setting Revision Required MUST require a comment, stored as a review comment. *(AC-DEL-04, §12.4)*
- **FR-016**: Issuing MUST record the issued date (default today), a free-text revision, the recipient, and optionally a transmittal link and note; issuing with open tasks MUST list them and require confirmation, and those tasks stay open and flagged. *(FR-DEL-04, FR-DEL-07, DL-05, DL-06)*
- **FR-017**: Issued, Accepted, and Cancelled MUST be terminal for overdue purposes; returning an Issued deliverable to Revision Required MUST keep its issued date and revision. *(DL-11, E-24)*
- **FR-018**: System MUST refuse to delete a deliverable that has tasks and offer cancellation instead; changing a deliverable's discipline MUST move its tasks' discipline after confirmation. *(DL-09, DL-10)*
- **FR-019**: The deliverables register MUST list all deliverables with key, name, type, discipline, owner, reviewer, milestone, due date, status, progress, indicators, revision, and issued date; group by discipline, milestone, status, or owner; filter by discipline, status, milestone, owner, type, due range, indicators, and requires-review; expand to tasks inline; and support bulk milestone, due-date shift, and owner changes. *(FR-DEL-05, §13.6)*
- **FR-020**: The deliverable detail MUST show its fields, progress with task breakdown, and tabs for Tasks, Dependencies, Links, Comments, and History, with an Issue action when Ready to Issue. *(§13.6.1)*
- **FR-021**: Status guards MUST explain themselves, for example "Cannot set Ready to Issue: this deliverable requires review and has not been In Review". *(§13.6)*

### Key Entities *(include if feature involves data)*

- **Milestone**: A dated project checkpoint with type, current and original date, description, discipline tag, completes-phase, client-facing flag, completion or cancellation state, and derived status.
- **Deliverable**: An engineering product owned by one discipline and one person, optionally targeting one milestone, with type, reviewer, dates, priority, status and previous status, revision, issue details, requires-review flag, and derived progress and indicators.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A Discipline Lead can define the five deliverables for one submission in under 10 minutes.
- **SC-002**: 100 % of the lifecycle guards and transitions in this packet pass automated checks.
- **SC-003**: Every issued deliverable in testing has an issued date and revision recorded (100 %).
- **SC-004**: Every milestone date change keeps the original date and shows the slip (100 % in testing).
- **SC-005**: A PM can tell how ready a submission is (issued out of total) in under 10 seconds from the milestone view.

## Assumptions

- Derived values are specified in packet 005: milestone status On Track, At Risk, and Overdue (FR-MS-02, §16.2, AC-MS-01–03), deliverable progress and At Risk (FR-DEL-03, DL-07, DL-08, AC-DEL-02, AC-DEL-06), and the Date Inconsistent and "Issued with open work" flags (AC-DEL-07, DL-12).
- The optional cascade that shifts deliverables when a milestone moves (FR-MS-05, M-04, AC-MS-05) is specified in packet 010; multiple issue records per deliverable (FR-DEL-08) is Phase 2 in packet 013.
- Notifications named here are delivered by packet 006, following the cross-packet rule in `specs/README.md`.
- Deliverable types are the Admin-maintained list from packet 001.
