# Feature Specification: Dependencies and Rules Engine

**Feature Branch**: `005-dependencies-and-rules-engine`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 005, dependencies and the deterministic rules engine (task dependencies, waiting, blocked, and blocking indicators, all derived indicators, milestone status, deliverable progress and risk, project and discipline health with override, attention items with snooze, and timely re-evaluation)."

**Source**: Product specification §5.2, §10.3, §10.4, §11.6, §11.9 (FR-ATT, FR-HLT), §12.6, §12.12, §14 Workflow 6, §15.1, §15.8, §15.11, §15.12, §16.1–§16.7, §23.5 (behaviour only), §31.9, §31.12

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record cross-discipline hand-offs as dependencies (Priority: P1)

Leads and assignees record that one task cannot start until another finishes ("Civil detailed
grading depends on Geotechnical pavement recommendations"). The Hub refuses loops and shows the
full chain of predecessors and successors around any task.

**Why this priority**: Invisible hand-offs are the main source of delay the product exists to
expose.

**Independent Test**: Link four tasks in a chain, try to close a loop, open the chain view, and
delete a linked task; confirm the refusal, the chain, and the clean-up.

**Acceptance Scenarios**:

1. **Given** tasks A and B in the same project, **When** a user adds "B depends on A", **Then** A shows "Blocks B" and B shows "Depends on A". *(AC-DEP-01)*
2. **Given** A to B to C exists, **When** a user tries to add "A depends on C", **Then** the save is refused and the loop A to B to C to A is displayed. *(AC-DEP-02, D-03)*
3. **Given** A to B to C to D, **When** the user opens the chain view on B, **Then** A is shown above and C and D below, each with its state, up to the depth limit. *(AC-DEP-07, FR-DEP-07)*
4. **Given** a task with two successors, **When** the PM deletes it, **Then** a confirmation lists the two dependencies to be removed; after confirmation the task is soft-deleted with a snapshot, the dependencies are removed, and both successor assignees and the PM are notified. *(AC-TSK-09, D-09, E-04)*

---

### User Story 2 - Waiting, blocked, and blocking appear by themselves (Priority: P1)

Nobody types "blocked". A task is Waiting when a predecessor is unfinished but nothing is wrong
yet, and Blocked when it should have started, is due soon, is being worked, or its predecessor is
overdue; a manual block or an overdue decision also blocks it. Predecessors show how many tasks
they are holding up, and the milestones at risk. The same engine computes every other indicator:
Overdue, Due Soon, Stale, Unassigned, No Due Date, Date Inconsistent, Slipped, and Inactive Owner.

**Why this priority**: Answering "what are we waiting for" without status meetings is the core
value of the product.

**Independent Test**: Reproduce the three worked examples in §15.12 and confirm each indicator,
blocker list, and affected milestone.

**Acceptance Scenarios**:

1. **Given** B depends on A, A is In Progress and due in 10 days, and B starts in 12 days, **Then** B shows Waiting, not Blocked, and no attention item fires for B. *(AC-DEP-03, D-05)*
2. **Given** B depends on A and A becomes Overdue, **Then** within one evaluation cycle B shows Blocked with A as its blocker, A shows "Blocking 1 task", A-03 fires as Critical for A, A-02 fires for B, and both list the affected milestone. *(AC-DEP-04, D-06, D-11, D-13)*
3. **Given** B is Blocked by A, **When** A is marked Complete, **Then** B's Blocked indicator clears right after evaluation and B's assignee receives an "unblocked, you can start" notification. *(AC-DEP-05, D-07)*
4. **Given** B is Blocked by A and A is Cancelled, **Then** B is neither Waiting nor Blocked and shows the note "Predecessor cancelled". *(AC-DEP-06, D-04)*
5. **Given** B depends on A and B's start date is earlier than A's due date, **Then** B shows Date Inconsistent naming the pair. *(AC-DEP-08, D-12)*
6. **Given** a manual block of type Client with a reason, **Then** the task is Blocked with the reason and "blocked for n days"; clearing the block removes the indicator. *(AC-DEP-09, D-14)*
7. **Given** a task due yesterday and In Progress, **Then** it is Overdue with "Overdue 1d" and counted in the project's overdue count; **When** it is put On Hold with a reason, **Then** it leaves the overdue count and appears among items held past their due date. *(AC-TSK-04, AC-TSK-05, G-01, T-06)*
8. **Given** a task created without an assignee whose start date is today, **Then** it shows Unassigned and A-08 fires to the lead and the PM. *(AC-TSK-01)*
9. **Given** a task In Progress with no change or comment for 11 days (threshold 10), **Then** it shows Stale and A-10 fires as Info to the assignee and the lead. *(AC-TSK-06, T-07)*

---

### User Story 3 - The PM gets a ranked list of what needs attention (Priority: P1)

Twenty fixed rules (A-01 to A-20) watch every Active project and raise attention items with a
severity, the people to route them to, and a sentence saying exactly why. Items rank by severity,
then days overdue or blocked, then priority, then due date. A PM or lead can snooze an item with
a note; nobody can dismiss one, because it disappears only when the condition clears.

**Why this priority**: The attention list is what makes dashboards and meetings act on the data.

**Independent Test**: Seed a project with conditions for eight rules, confirm the items, their
ranking and reasons, then snooze one and watch it return.

**Acceptance Scenarios**:

1. **Given** a task Overdue 6 days and blocking another, **Then** the top attention item is A-03 Critical for that task, naming the successor and the days overdue. *(AC-ATT-01)*
2. **Given** a PM snoozes an item for 7 days with a note, **Then** it leaves the default list, appears under Snoozed, is logged, and returns after 7 days if the condition persists. *(AC-ATT-02, ATT-03)*
3. **Given** a snoozed Warning item whose condition becomes Critical, **Then** it returns immediately. *(AC-ATT-03)*
4. **Given** a project in Setup, **Then** no attention items are produced. *(AC-ATT-04, ATT-01)*
5. **Given** a task assigned to an Inactive user, **Then** A-18 fires as Critical to the lead, the PM, and the user's supervisor. *(AC-ATT-05)*
6. **Given** an attention item, **When** the user asks why, **Then** the exact rule, the threshold value, and the item values that satisfied it are shown. *(AC-ATT-06)*
7. **Given** rule A-10 is disabled in settings, **Then** no Stale attention items are produced while the Stale indicator still shows. *(AC-ATT-07)*
8. **Given** the project's PM changes, **Then** items routed to the PM go to the new PM within one evaluation cycle. *(AC-ATT-08, ATT-05)*
9. **Given** a task Ready for Review for 6 days (threshold 5), **Then** A-11 fires as Warning to the reviewer, the lead, and the PM. *(AC-REV-03, R-05, FR-REV-04)*
10. **Given** a task whose due date has changed three times, **Then** A-20 fires as Info. *(AC-TSK-10)*

---

### User Story 4 - Milestones, deliverables, and projects get an honest status (Priority: P1)

Milestone status (On Track, At Risk, Overdue), deliverable progress and risk, discipline status,
and project health (Green, Yellow, Red, Grey) are all computed from stated inputs, each with a
"Why?". A PM may override the reported health with a note for a limited time; management always
sees both values.

**Why this priority**: Health reported by hand is optimistic and inconsistent; computed health
with visible reasons is what executives need.

**Independent Test**: Build the project from AC-HLT-01 and AC-MS-01, confirm the colours and
reasons, then set and expire an override.

**Acceptance Scenarios**:

1. **Given** a milestone dated 2027-01-29 with 5 targeted deliverables, 3 Issued and 2 In Progress, **When** today is 2027-01-16 (13 days out, threshold 14), **Then** the milestone is At Risk and the reason lists "2 of 5 deliverables not issued"; **Given** all 5 Issued, **Then** it is On Track. *(AC-MS-01, AC-MS-02)*
2. **Given** a milestone dated yesterday and not complete, **Then** it is Overdue, A-14 fires as Critical, and project health is Red. *(AC-MS-03)*
3. **Given** a deliverable with 8 tasks of which 6 are Complete and 1 Cancelled, **Then** progress shows 85 % and "6/7 tasks". *(AC-DEL-02, DL-07)*
4. **Given** a deliverable due in 7 days (threshold 10) at 30 % progress, **Then** it is At Risk and A-06 fires as Warning to the owner, the lead, and the PM; **Given** a deliverable due after its milestone, **Then** it shows Date Inconsistent and A-17 fires. *(AC-DEL-06, AC-DEL-07, DL-08)*
5. **Given** an Active project with 41 open tasks of which 7 are Overdue and no other conditions, **Then** computed health is Yellow and "Why?" shows "Overdue tasks 7 of 41 (17 %), at least 10 %"; **Given** 11 Overdue (27 %), **Then** it is Red. *(AC-HLT-01, AC-HLT-02)*
6. **Given** Red computed health, **When** the PM overrides it to Green with a note, **Then** the dashboard shows Reported Green with Computed Red beside it, the project list shows both, and after 14 days the system removes the override, logs it, and A-15 informs the PM. *(AC-HLT-03, FR-HLT-02)*
7. **Given** a project with no milestones and no open tasks, **Then** health is Grey with the reason "Nothing to evaluate". *(AC-HLT-04)*
8. **Given** the nightly run, **Then** every Active project has exactly one health snapshot for that date. *(AC-HLT-05)*

---

### User Story 5 - Everything stays current without anyone refreshing it (Priority: P2)

Indicators, statuses, health, and attention items update shortly after any change, never delay
the person saving, and roll over at midnight so things become overdue without anyone touching
them. Projects that are not Active are not evaluated.

**Why this priority**: A flag that lags reality all day erodes trust; one that slows saving
erodes adoption.

**Independent Test**: Change a predecessor's status and time how long its successor takes to
update; advance the date past a due date and confirm the overnight changes.

**Acceptance Scenarios**:

1. **Given** a project with 2,000 tasks, **When** a user saves a change, **Then** the save completes immediately and the project's derived state reflects it within 2 seconds. *(§22)*
2. **Given** a task due today, **When** the organisation's date rolls over, **Then** it becomes Overdue and appears in the next morning's digest without any user action. *(§16.7)*
3. **Given** an Active project put On Hold, **Then** none of its items count as Overdue, Blocked, or Stale, it produces no attention items, and its health is Grey with the reason "Project on hold". *(AC-PRJ-03, G-05, E-05)*

---

### Edge Cases

- A predecessor put On Hold stays unsatisfied; its successors keep their indicators and the blockers list shows the predecessor as On Hold. *(§12.6)*
- A predecessor moved to another deliverable keeps its dependency edges. *(§12.6)*
- A dependency on an already Complete predecessor is allowed and has no effect; one on an already Complete successor is allowed with a warning. *(D-16, D-17)*
- Reopening a Complete predecessor re-blocks its successors and notifies their assignees and the PM. *(D-08)*
- A deleted predecessor can be restored, which recreates its edges from the snapshot. *(E-04)*
- Milestones with no deliverables and no tasks, such as Kickoff, stay On Track until Overdue, except submission milestones with nothing planned inside the approaching window. *(§16.2)*
- "Today" is the organisation's calendar date, so users in other time zones may see an item turn overdue an hour early or late. *(E-21, G-03)*
- Projects with more than 5,000 tasks still finish evaluation within seconds. *(E-22)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to add Finish-to-Start dependencies between tasks in the same project, limited to the PM, a lead with either task in their discipline, and the successor's assignee, with an optional note. *(FR-DEP-01, D-01, D-02, §8.5.2)*
- **FR-002**: System MUST refuse self-dependencies, duplicates, and loops, show the loop's path, and list loop-creating tasks as disabled when searching for a predecessor. *(FR-DEP-02, D-03)*
- **FR-003**: A dependency MUST be satisfied when its predecessor is Complete or Cancelled; a Cancelled predecessor MUST show "Predecessor cancelled" on the successor. *(D-04)*
- **FR-004**: A successor that is not terminal or On Hold and has an unsatisfied predecessor MUST be Waiting unless a Blocked condition holds. *(FR-DEP-03, D-05)*
- **FR-005**: A successor MUST be Blocked by a dependency when it has an unsatisfied predecessor and its start date has arrived, the predecessor is Overdue, it is due within the task due-soon threshold, or it is In Progress or later. *(FR-DEP-03, D-06)*
- **FR-006**: When a predecessor becomes satisfied, System MUST re-evaluate its successors immediately and notify each successor's assignee that the task is no longer waiting; reopening a predecessor MUST re-block its successors and notify their assignees and the PM. *(FR-DEP-05, D-07, D-08)*
- **FR-007**: Deleting a task MUST remove its dependency edges, list them in the logged snapshot, re-evaluate successors, and notify their assignees and the PM; restoring it MUST recreate the edges. *(D-09, E-04)*
- **FR-008**: Removing a dependency MUST be limited to the PM, a lead with either end in their discipline, and the successor's assignee, and MUST be logged. *(D-10)*
- **FR-009**: A task MUST show "Blocking n tasks" with the list when successors are Waiting or Blocked because of it; an overdue task that blocks others MUST be flagged as blocking. *(FR-DEP-04, FR-DEP-06, D-11)*
- **FR-010**: System MUST flag Date Inconsistent when a successor starts before, or is due before, its predecessor's due date, when a task is due after its deliverable, and when a deliverable is due after its milestone, naming the pair. *(D-12, T-04, DL-03)*
- **FR-011**: For an Overdue or Blocked task, System MUST list the affected milestones: its own and those of all transitive successors, up to the chain depth limit. *(FR-DEP-06, D-13)*
- **FR-012**: A manual block MUST make the task Blocked with its type, reason, and days blocked until cleared. *(D-14)*
- **FR-013**: A task linked as "blocked by" to a decision that is Pending, Under Review, or Deferred and past its required-by date MUST be Blocked with the decision as blocker, and Waiting when the decision is not yet overdue. *(D-15)*
- **FR-014**: System MUST derive deliverable-to-deliverable dependencies from task dependencies and show them read-only on both deliverables. *(FR-DEP-08, D-18)*
- **FR-015**: The chain view MUST show transitive predecessors and successors with their states up to the configured depth (default 10). *(FR-DEP-07)*
- **FR-016**: An item MUST be Overdue when its due, milestone, or required-by date is before today, it is not in a terminal status or On Hold, and its project is Active; Due Soon when due within its type's threshold. Terminal statuses are Complete and Cancelled for tasks; Issued (when Accepted is unused), Accepted, and Cancelled for deliverables; Complete and Cancelled for milestones; Decided and Cancelled for decisions. *(G-01, G-02, G-04, §10.3)*
- **FR-017**: System MUST also derive Stale (no activity beyond the stale threshold while In Progress or in review), Unassigned, No Due Date, Slipped (days from the original date), Inactive Owner, "Issued with open work", and "held past due date" for tasks On Hold past their due date. *(§10.3, T-03, T-06, T-07, DL-12)*
- **FR-018**: System MUST evaluate "today" as the calendar date in the organisation time zone and MUST NOT evaluate indicators, attention, or health for projects in Setup, On Hold, Complete, Archived, or Cancelled, showing a banner that explains why. *(G-03, G-05)*
- **FR-019**: System MUST evaluate attention rules A-01 to A-06 and A-08 to A-20 exactly as defined in §12.12 (conditions, severities, and routing), each with an enabled flag in organisation settings. *(FR-ATT-01, §12.12)*
- **FR-020**: An attention item MUST be identified by rule and item, persist with its first-detected time while the condition holds, disappear when it clears, and never be dismissible. *(ATT-02, §12.12)*
- **FR-021**: Attention items MUST rank by severity, then days overdue or blocked, then priority, then due date. *(ATT-04)*
- **FR-022**: Every attention item, status, and health colour MUST offer "Why?" showing the rule, the threshold, and the item values that triggered it. *(AC-ATT-06, §4 principle 6)*
- **FR-023**: The PM or a lead MUST be able to snooze an attention item for 1 to 30 days with a note; snoozes MUST be logged and end at expiry or when the item's severity rises. *(FR-ATT-02, ATT-03)*
- **FR-024**: Routing MUST use the roles current at evaluation time. *(ATT-05)*
- **FR-025**: Milestone status MUST follow §16.2: Overdue when the date has passed; At Risk when a targeted deliverable is Overdue, a task under it is Overdue or Blocked, it is within the approaching threshold with unfinished deliverables, it is a submission with nothing targeted inside that threshold, or a targeted deliverable is due after it; otherwise On Track. *(FR-MS-02, §16.2)*
- **FR-026**: Deliverable progress MUST be complete tasks over non-cancelled tasks rounded down to 5 %, weighted by estimated hours when every task has one, shown as "—" without tasks, and never entered by hand. *(FR-DEL-03, DL-07)*
- **FR-027**: A deliverable MUST be At Risk under the conditions in DL-08. *(DL-08)*
- **FR-028**: Project health MUST follow §16.3 (Grey, then Red, then Yellow, else Green) from the inputs listed there, with "Why?" listing every non-zero input and the colour it contributes. *(FR-HLT-01, FR-PRJ-05, §16.3)*
- **FR-029**: The same rules scoped to one discipline's items MUST give each discipline a status, and each project MUST have a per-discipline summary of open, overdue, blocked, and waiting tasks, deliverables due within 14 days, next due item, lead, and status colour. *(FR-TEAM-05, §16.6)*
- **FR-030**: The PM MUST be able to override reported health to Green, Yellow, or Red (not Grey) with a mandatory note; the override MUST expire after the configured days, be removed and logged by the system, raise A-15, and wherever health is shown beyond project members both values MUST appear when they differ. *(FR-HLT-02, §16.4)*
- **FR-031**: System MUST store one health snapshot per Active project per night with computed and reported health and the inputs. *(FR-HLT-03, §16.5)*
- **FR-032**: Derived state MUST be recomputed after every relevant change, on demand at most 60 seconds stale, and nightly after midnight in the organisation time zone; changes in derived state MUST be signalled once to notifications (became blocked, became unblocked, milestone At Risk, new Critical item). *(FR-HLT-01, §16.7, §23.5)*
- **FR-033**: Given the same data and settings, evaluation MUST always produce the same result. *(§4 principle 6)*

### Key Entities *(include if feature involves data)*

- **Task dependency**: A directed Finish-to-Start link from predecessor to successor in one project, with an optional note.
- **Derived state**: The current indicators for each task and deliverable, status for each milestone, and health and counts for each project and discipline, with reasons.
- **Attention item**: A current rule firing for one item: rule, severity, message with values, people routed to, first-detected time, rank.
- **Attention snooze**: Who snoozed which item, until when, and why.
- **Health override**: The PM's reported health, note, author, time, and expiry.
- **Health snapshot**: One project's computed and reported health and inputs for one date.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every worked example in §15.12 and every acceptance scenario in this packet passes as an automated check (100 %).
- **SC-002**: After a change, affected indicators update within 2 seconds for projects up to 2,000 tasks, and saving is never delayed by evaluation. *(§22)*
- **SC-003**: Date-driven changes (newly overdue items, milestones turning At Risk) are visible for every Active project within 30 minutes after midnight in the organisation time zone. *(§22)*
- **SC-004**: 100 % of attention items, statuses, and health colours in testing show a reason that names the rule, threshold, and values.
- **SC-005**: On pilot projects, blocked work is visible within one day of becoming blocked with nobody typing it. *(G3)*
- **SC-006**: On pilot projects, the milestone At Risk rule fires at least 14 days before a submission that is not on course. *(G5)*

## Assumptions

- Thresholds, rule enabled flags, and the organisation time zone come from packet 001's settings; calendar days are used (working-day calendars are Phase 2, packet 021).
- A-07 (high-severity open issue) and the issue input to health are Phase 2 (packet 014). Displaying health trends is first-release scope (packet 016, §36).
- Notifications signalled here are delivered by packet 006; the surfaces that show these results are in packet 007. Decision blocks need packet 008.
- The worked examples in §15.12 are part of this packet's acceptance tests, and rules-engine coverage follows the constitution's quality gates (T1).
- Cross-project dependencies are Phase 3 (FR-DEP-10) and not part of this set of packets.
