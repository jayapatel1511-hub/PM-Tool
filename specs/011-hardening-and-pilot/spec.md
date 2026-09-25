# Feature Specification: Hardening and Pilot

**Feature Branch**: `011-hardening-and-pilot`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 011, hardening and pilot (accessibility, performance at scale, reliability and recovery, security readiness for organisation-wide rollout, support material, and the eight-week pilot with go/no-go)."

**Source**: Product specification §2.4, §5.1, §5.2, §21, §22, §31, §33, §34 (Q9, Q10, Q14, Q16), §35.1 (stages 6 and 7), §36.9

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pilot PMs run real projects in the Hub (Priority: P1)

Three motivated PMs run six real projects in the Hub for eight weeks. They run coordination
meetings from it, their teams work from My Work, and thresholds are tuned weekly from what the
attention lists show. At the end, the business decides whether to roll out.

**Why this priority**: The product succeeds only if coordination happens in it; the pilot is the
evidence.

**Independent Test**: Run the pilot and collect the measures for goals G1–G6.

**Acceptance Scenarios**:

1. **Given** the pilot's week 4, **Then** "task has no owner" and "task has no due date" items on pilot projects are trending towards zero. *(G2)*
2. **Given** pilot PMs, **Then** they report that Weekly Coordination replaced their status spreadsheet or email for the meeting. *(G1)*
3. **Given** new pilot users after a 30-minute walkthrough, **Then** they update a task, add a comment, and mark work ready for review unaided. *(G6)*
4. **Given** the weekly pilot review, **When** an attention rule proves too noisy or too quiet, **Then** its threshold is adjusted in settings and the change is logged. *(§33)*
5. **Given** the end of the pilot, **Then** a go/no-go decision is recorded against the success metrics. *(§35.1, Q16)*
6. **Given** first-release readiness, **Then** AC-VIS-01 through AC-VIS-08 pass on the integrated workspace, including calendar visibility, reconciled metrics, and task-hour permissions and totals. *(§36.9)*

---

### User Story 2 - Everyone can use the Hub (Priority: P1)

Every screen works with the keyboard alone, with screen readers, at high contrast, and without
relying on colour, in the supported browsers.

**Why this priority**: The audience is every employee; accessibility is a baseline, not a feature.

**Independent Test**: Run automated accessibility checks on every screen and a manual audit of
the core flows before general availability.

**Acceptance Scenarios**:

1. **Given** each MVP screen, **Then** it passes automated WCAG 2.1 AA checks and a manual audit before general availability. *(§22)*
2. **Given** the supported browsers (current and previous Microsoft Edge and Google Chrome, current Safari on iPad, current Firefox), **Then** the core flows work in each. *(§22, Q10)*
3. **Given** a user who prefers reduced motion or high contrast, **Then** the Hub follows the operating-system setting. *(§13.0)*

---

### User Story 3 - The Hub stays up and never loses work (Priority: P1)

The Hub is available during business hours across the organisation's time zones, recovers from
failure without losing more than a few minutes of work, and its restore procedure is proven
before go-live and every quarter after.

**Why this priority**: People stop using a coordination tool they cannot trust to be there with
their data.

**Independent Test**: Run a restore drill to a chosen point in time and measure the data loss and
the time to recover.

**Acceptance Scenarios**:

1. **Given** a restore drill, **Then** data is recovered to within 15 minutes of the chosen point and the service is back within 8 hours. *(§22, Q9)*
2. **Given** an error-rate spike, a failed nightly job, delayed evaluation, or a digest not sent by 08:00, **Then** operators are alerted. *(§22, §23.8)*
3. **Given** the environments are lost, **Then** they can be rebuilt from their definitions in under one day. *(§22)*

---

### User Story 4 - It is safe to roll out organisation-wide (Priority: P2)

Before the pilot, the team walks through the threats; automated checks watch dependencies and
code continuously; before organisation-wide rollout, the Hub passes a penetration test if the
organisation requires one.

**Why this priority**: The Hub holds internal-confidential project data for every employee.

**Independent Test**: Complete the threat-model walkthrough and the automated scans, and review
the security requirements below against the running system.

**Acceptance Scenarios**:

1. **Given** the pilot start, **Then** a threat-model walkthrough is complete and its findings are resolved or accepted. *(§21)*
2. **Given** a critical vulnerability in a dependency, **Then** it is acted on immediately; routine dependency updates happen monthly. *(§21)*
3. **Given** logs and error messages, **Then** they contain no secrets, tokens, or comment text, and identify users by internal ID rather than email where possible. *(§21)*

---

### User Story 5 - It performs at full scale (Priority: P2)

At the expected scale (300–500 users, 300–600 active projects, 100,000 open tasks, 2 million
activity entries a year) the Hub meets its response targets, and it can grow to five times that
without redesign.

**Why this priority**: The pilot is small; general availability is not.

**Independent Test**: Load synthetic data at full scale, including 5,000-task projects, and
measure the targets below.

**Acceptance Scenarios**:

1. **Given** full-scale synthetic data and 100 concurrent users, **Then** My Work, the Project Dashboard, and a 500-task list are usable within 2 seconds at the 95th percentile, and list and item reads answer within 500 milliseconds at the 95th percentile. *(§22)*
2. **Given** a 2,000-task project, **Then** a change is reflected in its derived state within 2 seconds; **Given** 500 active projects with 100,000 tasks, **Then** the nightly run finishes within 30 minutes. *(§22)*

---

### User Story 6 - The Hub can be run and supported (Priority: P3)

Administrators, users, and operators have what they need: an admin guide, a one-page user guide,
a technical reference for the Hub's interfaces, runbooks for background jobs and restores, and a
support process for rollout by office or group.

**Why this priority**: Needed for general availability, not for the pilot's first weeks.

**Independent Test**: Have an administrator and an operator complete common tasks using only the
written material.

**Acceptance Scenarios**:

1. **Given** the admin guide and runbooks, **Then** a new administrator can deactivate a leaver, reassign their work, and restore a test environment using only the documents. *(§22)*

---

### Edge Cases

- The bilingual decision (Q7) made late would force retrofitting; interface text is kept separate from code from the first packet, so a second language costs translation, not rework. *(§33)*
- Notification fatigue during the pilot: defaults are digest-first and are adjusted if pilot users switch notifications off. *(§33)*
- Management over-reading health colours: computed and reported health always appear together, and training explains that health summarises indicators and does not predict. *(§33)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The pilot MUST run with the agreed group (default: 3 PMs, 6 projects, 8 weeks), measure goals G1–G6 weekly, and end with a recorded go/no-go decision. *(§5.1, §35.1, Q16)*
- **FR-002**: Every MVP screen MUST pass automated WCAG 2.1 AA checks in every change and a manual audit before general availability. *(§22)*
- **FR-003**: The core flows MUST work in the supported browsers. *(§22, Q10)*
- **FR-004**: The service MUST be available at least 99.5 % of business hours each month across the organisation's time zones. *(§22, Q9)*
- **FR-005**: No committed change may be lost on failure beyond 15 minutes of data, and service MUST be restorable within 8 hours; restores MUST be tested before go-live and every quarter. *(§21, §22, Q9)*
- **FR-006**: Operators MUST be alerted on a high error rate, a failed nightly job, delayed evaluation, and a digest not sent by 08:00. *(§22, §23.8)*
- **FR-007**: Development, test, and production MUST be separate, production data MUST NOT be used in development, and environments MUST be rebuildable from their definitions in under one day. *(§21, §22, T4)*
- **FR-008**: All connections MUST be encrypted, and unencrypted requests MUST be redirected. *(§21)*
- **FR-009**: Secrets MUST be kept in a managed secret store, never in code, configuration files, logs, or error messages, and MUST be rotated on a schedule. *(§21)*
- **FR-010**: The Hub MUST reach other services with the least privilege needed, and the permission to write activity-log entries MUST NOT include changing or deleting them. *(§21)*
- **FR-011**: A threat-model walkthrough MUST be completed before the pilot; dependency and code scanning MUST run on every change, with critical findings acted on immediately and routine updates monthly; a penetration test MUST precede organisation-wide rollout if the organisation requires one. *(§21)*
- **FR-012**: Each user MUST be limited to a reasonable request rate so a runaway client cannot degrade the Hub for others. *(§21)*
- **FR-013**: The response and evaluation targets in §22 MUST be met at full synthetic scale, and the design MUST reach five times that scale without architectural change. *(§22)*
- **FR-014**: An admin guide, a user quick guide, a technical interface reference, and runbooks for background jobs and restores MUST exist before general availability, so a team of 2 to 4 developers can maintain the Hub. *(§22, T3)*
- **FR-015**: Project data and activity history MUST be retained according to the organisation's retention policy (default: indefinitely). *(§20.3, Q14)*
- **FR-016**: The first-release six-view acceptance scenarios MUST be verified before the pilot starts. *(§36.9)*

### Key Entities *(include if feature involves data)*

- **Pilot measure**: A weekly reading of one goal (G1–G6) on the pilot projects.
- **Restore drill record**: When a restore was tested, the point restored to, data loss, and time to recover.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Pilot PMs run their coordination meetings from the Hub, and every "why is this blocked?" question can be answered by clicking the indicator. *(G1, §2.4)*
- **SC-002**: Pilot users update task status at least weekly without being chased. *(G4)*
- **SC-003**: No submission on a pilot project is a surprise: each was At Risk at least two weeks ahead if it was not on course. *(G5)*
- **SC-004**: Availability is at least 99.5 % of business hours in each month after go-live.
- **SC-005**: Every restore drill recovers to within 15 minutes of the chosen point, within 8 hours.
- **SC-006**: 100 % of MVP screens pass the accessibility audit before general availability.

## Assumptions

- Open decisions use the specification's defaults: 99.5 % business-hours availability, 14 days of point-in-time restore, and no zone-redundant high availability (Q9); the browsers listed above (Q10); indefinite retention (Q14); a pilot of 3 PMs, 6 projects, and 8 weeks (Q16). Log retention of 30 to 90 days is still to be decided.
- Whether a penetration test is required before rollout is an organisation decision (§21).
- This packet hardens the first-release set in `specs/README.md`, including packets 016–019 and 022–024; it adds no new product behaviour.
