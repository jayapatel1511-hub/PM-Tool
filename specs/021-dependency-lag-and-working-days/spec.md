# Feature Specification: Deliverable Dependencies, Lag, and Working Days

**Feature Branch**: `021-dependency-lag-and-working-days`

**Created**: 2026-09-24

**Status**: Draft (Phase 2)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 021, deliverable dependencies, lag, and working days (explicit deliverable-to-deliverable dependencies, waiting time on a dependency, and thresholds counted in working days with office holiday calendars)."

**Source**: Product specification §10.4, §11.6 (FR-DEP-09), §12.6, §12.15, §28 (items 10 and 11)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Link deliverables directly (Priority: P1)

A lead records that one deliverable needs another first ("the 85 % Civil package needs the
Geotechnical Investigation Report issued") without linking individual tasks. The link shows on
both deliverables alongside the ones derived from tasks.

**Why this priority**: Many hand-offs are between packages, not tasks, and linking every task
pair is tedious.

**Independent Test**: Link two deliverables directly and confirm both show the dependency and the
derived-versus-explicit distinction.

**Acceptance Scenarios**:

1. **Given** a lead links deliverable B to depend on deliverable A, **Then** both show the dependency, marked as explicit, next to any dependencies derived from their tasks. *(FR-DEP-09)*
2. **Given** A is not Issued and B's start date has arrived, **Then** B shows Blocked with A as its blocker, following the same rules as task dependencies. *(D-06)*
3. **Given** a link that would create a loop between deliverables, **Then** it is refused and the loop is shown. *(D-03)*

---

### User Story 2 - Add waiting time to a dependency (Priority: P2)

A dependency can carry a lag in days, for example 10 days of client review between issuing a
package and starting the next one, instead of modelling the wait as a task.

**Why this priority**: Client review periods are the most common waits in design projects.

**Independent Test**: Add a 10-day lag and confirm when the successor stops Waiting and how date
checks use the lag.

**Acceptance Scenarios**:

1. **Given** B depends on A with a 10-day lag and A completes on 2027-02-01, **Then** B stays Waiting until 2027-02-11 and becomes Blocked if its start date arrives before that. *(§12.6)*
2. **Given** a lag, **Then** the Date Inconsistent check compares B's start date with A's due date plus the lag. *(D-12)*

---

### User Story 3 - Count working days, not calendar days (Priority: P2)

Thresholds such as "due soon in 5 days" and "stale after 10 days" count working days, using each
office's calendar of statutory holidays, which Admins maintain.

**Why this priority**: Calendar days make Monday mornings noisy and holidays misleading.

**Independent Test**: With a holiday on a Monday, confirm Due Soon and Stale use working days.

**Acceptance Scenarios**:

1. **Given** working-day counting and a holiday on Monday, **When** a task is due next Tuesday, **Then** Due Soon counts the working days between today and Tuesday, skipping the weekend and the holiday. *(§10.4)*
2. **Given** an Admin adds a holiday to the Hamilton office calendar, **Then** projects led from that office use it from then on. *(§10.4)*

---

### Edge Cases

- Overdue still means the due date is before today; working days change only thresholds and spreads, not what "overdue" means. *(G-01)*
- A project uses its lead office's calendar; projects without an office use the organisation calendar.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Leads MUST be able to add explicit Finish-to-Start dependencies between deliverables in the same project, shown separately from derived ones, with loops refused. *(FR-DEP-09, D-03)*
- **FR-002**: Explicit deliverable dependencies MUST produce Waiting, Blocked, and Blocking Others indicators on deliverables using the same conditions as task dependencies, with "satisfied" meaning Issued, Accepted, or Cancelled. *(§12.6)*
- **FR-003**: Dependencies MUST accept an optional lag in whole days; a successor MUST stay Waiting until the predecessor is satisfied and the lag has passed, and date checks MUST include the lag. *(§12.6)*
- **FR-004**: Admins MUST be able to maintain holiday calendars per office, and the organisation MUST be able to switch thresholds to working days. *(§10.4)*
- **FR-005**: With working days on, due-soon, stale, review-stale, blocked-days, and approaching thresholds and workload spreading MUST skip weekends and the project office's holidays. *(§10.4, §12.15)*

### Key Entities *(include if feature involves data)*

- **Deliverable dependency**: An explicit Finish-to-Start link between two deliverables.
- **Dependency lag**: Days of waiting added to a dependency.
- **Holiday calendar**: An office's statutory holidays.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With working days on, no Due Soon or Stale flag changes solely because of a weekend or holiday (100 % in testing).
- **SC-002**: A client review wait is modelled with a lag instead of a placeholder task on every pilot project that needs one.

## Assumptions

- The specification names these items briefly (§28 items 10 and 11, §10.4 "TBD: statutory holiday calendar by office"); the details above are defaults to confirm with `/speckit-clarify`.
- Depends on packet 005.
