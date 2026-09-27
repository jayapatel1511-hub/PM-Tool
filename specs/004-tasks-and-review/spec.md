# Feature Specification: Tasks and Review

**Feature Branch**: `004-tasks-and-review`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 004, tasks and review (tasks with a single accountable assignee, the task workflow with independent technical review, collaborators, manual blocks, bulk actions, the task list, the task panel, and the board)."

**Source**: Product specification §7.3, §7.4, §10.2 (task status), §10.5, §11.5, §12.2 (TM-04, TM-06), §12.5, §13.3, §13.3.1, §13.4, §14 Workflows 4, 5, 7, 8, §15.6, §15.7, §31.7, §31.8, Appendix C

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Assign and track the units of work (Priority: P1)

A PM or Discipline Lead creates tasks under a deliverable (or directly against a milestone, or
standalone), each with exactly one assignee, an owning discipline, a due date, a priority, an
optional estimate, and an optional reviewer. Team members see their tasks in the task list and
open any task in a side panel without losing their place.

**Why this priority**: Tasks are the core unit of coordination; everything else rolls up from
them.

**Independent Test**: Create ten tasks across two deliverables, assign them, and confirm the
list, grouping, sorting, inline edits, and the task panel.

**Acceptance Scenarios**:

1. **Given** a PM assigns a task to a user who is not on the team, **When** the assignment saves, **Then** the user is added as a Team Member and the PM receives an in-app notification. *(AC-TEAM-01, TM-06)*
2. **Given** a Team Member assigned to a task, **When** they set it to In Progress, **Then** it succeeds; **When** they try to reassign it to someone else, **Then** it is refused. *(AC-PERM-03)*
3. **Given** a task list for a project, **Then** tasks are grouped by deliverable by default (also by discipline, milestone, assignee, status, or due bucket), sorted by due date with empty dates last and then priority, and status, assignee, due date, priority, and progress can be edited in the row. *(FR-TSK-09, §13.3)*
4. **Given** a user opens a task, **Then** its details open in a side panel with a link to the full page, and the address can be shared so others open the same task. *(§13.3.1, §13.0)*

---

### User Story 2 - Work moves through a workflow with independent review (Priority: P1)

A task moves from Not Started to In Progress and then either to Complete or, when it requires
review, to Ready for Review, In Review, and Complete or Revision Required. Reviewers approve or
send work back with a comment; every return increments the review round. Done can never quietly
mean "done but unreviewed".

**Why this priority**: Independent technical review is structural in engineering work and is
one of the product's main promises.

**Independent Test**: Take a task that requires review through two review rounds to Complete,
then reopen it; confirm every guard, prompt, and log entry.

**Acceptance Scenarios**:

1. **Given** a task that requires review, **When** the assignee tries to move it from In Progress to Complete, **Then** Complete is not offered and Ready for Review is. *(AC-TSK-02, R-01)*
2. **Given** a task that requires review and has no reviewer, **When** the assignee sets Ready for Review, **Then** a reviewer must be chosen before it saves. *(AC-TSK-03)*
3. **Given** a task In Review, **When** the reviewer requests revision without a comment, **Then** it is refused; with a comment, **Then** the status is Revision Required, the review round becomes 1, the comment is posted as "Review, round 1", and the assignee is notified. *(AC-REV-02, R-03)*
4. **Given** self-review is not allowed, **When** a user makes themselves both assignee and reviewer, **Then** validation fails with the stated reason. *(AC-REV-04, R-02)*
5. **Given** the reviewer is changed while a task is In Review, **Then** both the old and new reviewers are notified and the change is logged. *(AC-REV-05, R-04)*
6. **Given** an assignee sets progress to 100 on a task that does not require review, **Then** the Hub offers to mark it Complete; on acceptance it is Complete, its completion time is recorded, and its deliverable's progress is recalculated. *(AC-TSK-07, T-20)*
7. **Given** a Complete task, **When** a Discipline Lead reopens it with a reason, **Then** it is In Progress at 90 %, its completion time is cleared, its successors are re-evaluated, and the reopen is logged with the reason. *(AC-TSK-08, T-14, E-10)*

---

### User Story 3 - Collaborate and keep dates honest (Priority: P2)

Collaborators help with a task and watchers follow it, while the assignee stays accountable.
Due-date changes by anyone other than the PM or lead need a reason, and repeated changes become
visible. When work genuinely cannot proceed for a reason outside the Hub, the assignee sets a
manual block with a type and reason.

**Why this priority**: These keep the task record trustworthy without adding owners.

**Independent Test**: Add a collaborator and a watcher, change a due date three times, and set
and clear a manual block; confirm permissions, reasons, and counters.

**Acceptance Scenarios**:

1. **Given** a collaborator on a task, **When** they update progress and post a comment, **Then** both succeed; **When** they try to change the due date, **Then** it is refused. *(AC-TSK-11, T-15)*
2. **Given** an assignee changes their task's due date, **Then** they must give a reason, the change is logged, and the reviewer is notified. *(T-16)*
3. **Given** a task whose due date has changed three times, **Then** the task shows "Due moved x3". *(FR-TSK-13)*
4. **Given** an assignee sets a manual block of type Client with a reason, **Then** the block, its reason, and when it was set are recorded, and clearing it removes them. *(FR-TSK-07, D-14; the Blocked indicator is specified in packet 005)*

---

### User Story 4 - Change many tasks at once, safely (Priority: P2)

PMs and leads select many tasks and assign, re-date, re-prioritise, move, hold, or cancel them
in one action. Every row is checked separately, and the result says what changed and what was
skipped. Deleting is rare and recoverable; cancelling is preferred once work has started.
Concurrent edits never overwrite each other silently.

**Why this priority**: Re-planning after a slip touches many tasks at once, and data loss would
destroy trust.

**Independent Test**: Bulk-shift 14 tasks where the user lacks permission on 2; delete a task;
edit one task from two sessions at once.

**Acceptance Scenarios**:

1. **Given** a PM selects 14 tasks and shifts their due dates by 7 days with a reason, **When** they lack permission on 2, **Then** 12 are updated, 2 are skipped with the reason, one summary is shown, and each affected person receives one summary notification. *(FR-TSK-09, T-23, E-20)*
2. **Given** a task with comments, **When** a user chooses to delete it, **Then** the Hub offers Cancel first; a confirmed delete is soft, logged with a snapshot, and restorable. *(T-08, T-09, FR-TSK-11)*
3. **Given** two users edit the same task, **When** the second saves an out-of-date version, **Then** the save is refused and they see who changed it and when, with an option to reload. *(AC-TSK-12, G-07)*

---

### User Story 5 - Discipline teams work from a board (Priority: P2)

Leads and teams who think in columns use a board with one column per status, optional swimlanes
by discipline, deliverable, or assignee, and drag-and-drop that follows the same workflow rules
as the list.

**Why this priority**: Teams expect a board, and it costs little on top of the list.

**Independent Test**: Scope the board to one deliverable, drag a card through allowed and
disallowed moves, and switch swimlanes.

**Acceptance Scenarios**:

1. **Given** the board, **When** a user drags a card to a status they may not set, **Then** the card returns to its column with the reason shown. *(FR-TSK-10, §13.4)*
2. **Given** a move that needs a reason (On Hold), **When** the card is dropped, **Then** the reason dialog opens before the move saves. *(§13.4)*
3. **Given** a board scoped to more than 500 cards, **Then** the Hub suggests filtering. *(E-22)*

---

### User Story 6 - Removing a member keeps their work owned (Priority: P3)

When the PM removes a team member who owns open tasks, the Hub asks whether to reassign them or
leave them with a visible "assignee not on project" indicator.

**Why this priority**: Membership changes are common, but orphaned work is the failure the
product exists to prevent.

**Independent Test**: Remove a member who owns 5 open tasks and choose each option in turn.

**Acceptance Scenarios**:

1. **Given** a member owns 5 open tasks, **When** the PM removes them from the team, **Then** the PM is prompted to reassign or leave the tasks; if left, the tasks show "assignee not on project". *(AC-TEAM-03, TM-04)*

---

### Edge Cases

- A task that spans disciplines keeps one owning discipline; other disciplines join as collaborators, or the work is split into linked tasks. *(E-06)*
- Several people on one task: one assignee plus collaborators; separable work is split into tasks under the deliverable. *(E-07)*
- A lead who must review their own work on a small project: the PM becomes reviewer, or the organisation enables self-review, which is logged. *(E-08)*
- A task moved to another deliverable keeps its dependencies and takes the new deliverable's milestone; tasks cannot move to another project. *(T-17, T-18, T-21)*
- A task's discipline must be one of the project's active disciplines, and its start date must be on or before its due date. *(T-19, T-22)*
- Projects with thousands of tasks keep lists responsive through virtualised rows and paging. *(E-22)*
- Standalone tasks with no deliverable or milestone ("Book kickoff room") are allowed, grouped under "Other tasks", and left out of milestone readiness. *(E-15)*
- A changed due date is logged with its reason, notifies the assignee and reviewer when someone else changed it, and re-evaluates the task and its successors. *(E-03)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Authorised users MUST be able to create tasks with name, description (bold, lists, and links only), discipline (required), deliverable (optional), milestone (only when there is no deliverable), assignee, reviewer, requires-review flag, priority, start and due dates, progress, and estimated hours. *(FR-TSK-01, §12.5)*
- **FR-002**: Every task MUST have a priority of Low, Medium (default), High, or Critical, which affects sorting and attention severity but never changes rules (§10.5), and at most one assignee, and exactly one owning discipline from the project's active disciplines. *(T-01, T-02, T-22)*
- **FR-003**: Assigning a task or review to someone not on the team MUST add them as Team Member or Reviewer and notify the PM. *(FR-TEAM-03, TM-06)*
- **FR-004**: Task status MUST follow the transitions in §12.5 and Appendix C, offering each user only the transitions their role and ownership allow; starting a blocked task MUST warn, not refuse. *(FR-TSK-02, T-10 to T-14)*
- **FR-005**: A task that requires review MUST NOT go from In Progress to Complete, and Ready for Review MUST require a reviewer. *(FR-TSK-03, R-01)*
- **FR-006**: The reviewer MUST NOT be the assignee unless the organisation allows self-review (off by default). *(FR-TSK-04, R-02, Q12)*
- **FR-007**: Ready for Review MUST notify the reviewer and place the task in their review queue; the reviewer MUST be able to move it to In Review and then Complete or Revision Required. *(FR-REV-01, FR-REV-02)*
- **FR-008**: Revision Required MUST require a comment, posted as a review comment with the round number, and MUST increment the review round. *(FR-REV-02, FR-REV-03, R-03)*
- **FR-009**: Changing the reviewer while a task is Ready for Review or In Review MUST notify both reviewers. *(R-04)*
- **FR-010**: Progress MUST be a whole number from 0 to 100 in steps of 10, editable by the assignee and collaborators; Complete MUST force 100; progress above 0 on a Not Started task MUST offer In Progress; 100 MUST offer Complete or Ready for Review. *(FR-TSK-05, T-20)*
- **FR-011**: On Hold and Cancelled MUST require a reason; leaving On Hold MUST restore the previous status; only the PM MAY restore a Cancelled task, with a reason. *(§12.5, G-09)*
- **FR-012**: Reopening a Complete task MUST be limited to the PM, the lead, and the reviewer, require a reason, clear its completion time, set progress to 90, and re-evaluate its successors. *(FR-TSK-12, T-14)*
- **FR-013**: Users MUST be able to add collaborators, who may update status, progress, and description but not the assignee or due date, and watchers, who only receive notifications. *(FR-TSK-08, T-15)*
- **FR-014**: Due-date changes by anyone other than the PM or lead MUST require a reason and MUST notify the assignee and reviewer when made by someone else; the Hub MUST count due-date changes and show the count from 3. *(T-16, FR-TSK-13, Q11)*
- **FR-015**: Users MUST be able to set and clear a manual block with a type (Client, External Party, Internal, Decision, Information, Other), a reason, and the time it was set. *(FR-TSK-07, D-14)*
- **FR-016**: Deletion MUST be soft (G-06), logged with a snapshot, and restorable; the Hub MUST offer Cancel before Delete once a task has progress or comments. *(FR-TSK-11, T-08, T-09)*
- **FR-017**: The task list MUST show key, name, deliverable, discipline, assignee, reviewer, status, indicators, priority, start, due, progress, estimate, and last activity; support grouping, sorting on any column, a column chooser, inline edits, and virtualised rows for large projects. *(FR-TSK-09, §13.3)*
- **FR-018**: Bulk actions (assign, set or shift due dates, set priority, set deliverable, put On Hold with a reason, cancel) MUST check permission per row, never let one failure stop the rest, and report updated and skipped counts. *(FR-TSK-09, T-23, E-20)*
- **FR-019**: The task panel MUST show header, blockers, fields, description, dependencies, manual block, collaborators and watchers, document links, actual-hour total and Add Time (packet 024), and Comments and History tabs, saving each field on its own; reviewers MUST see Approve and Request revision when the task is In Review. *(§13.3.1, §36.8)*
- **FR-020**: The board MUST show one column per status (Complete limited to the last 14 days, On Hold and Cancelled collapsed), cards with key, name, assignee initials, due date, indicator icons, deliverable, and priority, swimlanes by none, discipline, deliverable, or assignee, and drag-and-drop that follows FR-004 and asks for reasons where required. *(FR-TSK-10, §13.4)*
- **FR-021**: Saving an out-of-date version of a task MUST be refused with who changed it and when, and an option to reload and compare. *(G-07, AC-TSK-12)*
- **FR-022**: Removing a member who owns open tasks MUST prompt the PM to reassign them or leave them flagged "assignee not on project", and log the choice. *(TM-04)*
- **FR-023**: Moving a task to another deliverable MUST keep its dependencies and change its milestone context; moving it to another project MUST be refused; its start date MUST be on or before its due date. *(T-17, T-18, T-19, T-21)*

### Key Entities *(include if feature involves data)*

- **Task**: A unit of work with key, name, description, discipline, deliverable or milestone, assignee, reviewer, requires-review flag, priority, dates, status and previous status, progress, estimate, manual block, reasons, review round, due-date change count, last activity, completion time, and order.
- **Task participant**: A collaborator or watcher on a task.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A team member can update a task's status or progress in under 10 seconds from the list or the panel.
- **SC-002**: 100 % of workflow and review guards pass automated checks, and no task that requires review can be completed without review.
- **SC-003**: A bulk change to 50 tasks completes with an accurate summary of updated and skipped rows.
- **SC-004**: A task list of 500 tasks is usable within 2 seconds for 95 % of loads. *(§22)*
- **SC-005**: New users complete "update a task", "add a comment", and "mark ready for review" after a 30-minute walkthrough. *(G6)*

## Assumptions

- Derived indicators are specified in packet 005: Overdue, Due Soon, Waiting, Blocked, Blocking Others, Stale, Unassigned, No Due Date, and Date Inconsistent (FR-TSK-06, T-03, T-05, T-06, T-07, R-05, AC-TSK-01, AC-TSK-04, AC-TSK-05, AC-TSK-06, AC-TSK-10, AC-REV-03), as are dependencies and deleting a task that has them (AC-TSK-09, D-09).
- Notifications named here, including the reviewer's notification (AC-REV-01), are delivered by packet 006; the review queue on My Work is in packet 007; the filter bar and exports for the list are in packet 009.
- Kanban manual ordering within columns is Phase 2 (packet 019).
