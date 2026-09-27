# Feature Specification: Risk and Issue Registers

**Feature Branch**: `014-risk-and-issue-registers`

**Created**: 2026-09-24

**Status**: Draft (Phase 2)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 014, risk and issue registers (risks scored on a 3 by 3 grid, issues with severity and target dates, realised risks becoming issues, and their place on the dashboard, in Weekly Coordination, in project health, and in reports)."

**Source**: Product specification §9.5, §11.7 (FR-RSK, FR-ISS), §12.10, §12.12 (A-07), §12.13 (section 10), §13.1, §13.13, §16.3 (issue input), §19

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Track risks with a simple score (Priority: P1)

The team records what might go wrong ("Environmental approval may delay fieldwork") with an
owner, a probability and impact from 1 to 3, a mitigation, how they will know it is happening,
and a date to review it. Severity is probability times impact, shown as Low, Medium, or High.

**Why this priority**: Risks seen early are cheaper than issues handled late.

**Independent Test**: Raise three risks at different scores and let one pass its review date.

**Acceptance Scenarios**:

1. **Given** a risk with probability 3 and impact 2, **Then** its severity is 6, shown as High. *(§12.10)*
2. **Given** a risk past its review date, **Then** it shows "Review overdue". *(RSK-02)*
3. **Given** a High-severity open risk, **Then** it appears on the dashboard and in Weekly Coordination. *(RSK-01)*

---

### User Story 2 - Track issues and escalate the serious ones (Priority: P1)

The team records what has gone wrong ("Survey crew cannot access site: gate locked") with an
owner, a severity, and a target resolution date. A High-severity open issue is a Critical
attention item and turns project health Red.

**Why this priority**: Live problems need an owner and a date as much as tasks do.

**Independent Test**: Raise a High issue and confirm the attention item, the health change, and
resolution rules.

**Acceptance Scenarios**:

1. **Given** a High-severity issue that is Open or In Progress, **Then** A-07 fires as Critical to its owner and the PM, and project health is Red. *(ISS-01, A-07, §16.3)*
2. **Given** an issue, **When** someone resolves it without resolution text, **Then** it is refused. *(ISS-02)*
3. **Given** an issue past its target resolution date, **Then** it is Overdue. *(ISS-03)*

---

### User Story 3 - A realised risk becomes an issue (Priority: P2)

When a risk happens, it is marked Realised and linked to a new or existing issue, keeping the
trail from prediction to problem.

**Why this priority**: The link shows whether risk management works, but the registers are useful
without it.

**Independent Test**: Realise a risk and confirm the issue link in both directions.

**Acceptance Scenarios**:

1. **Given** an open risk, **When** it is marked Realised, **Then** the Hub requires creating or linking an issue and keeps the link on both. *(RSK-03)*

---

### User Story 4 - Risks and issues where coordination happens (Priority: P2)

The dashboard counts open and High issues and High risks; Weekly Coordination has an "Open issues
and high risks" section; each register has filters and sorting; and a risk or issue can be raised
from a task or deliverable, pre-linked to it.

**Why this priority**: Registers nobody looks at go stale; these surfaces keep them in the meeting.

**Independent Test**: Raise an issue from a task panel and find it on the dashboard, in the
meeting, and in the report.

**Acceptance Scenarios**:

1. **Given** a task panel, **When** the user raises an issue from it, **Then** the issue is created linked to that task. *(§13.13)*
2. **Given** the Open Issues / High Risks report, **Then** it lists the register rows for the chosen projects and severity and exports like other reports. *(§19)*

---

### Edge Cases

- Scores are deliberately simple: no monetary value, Monte Carlo, or risk-appetite matrices. *(§12.10)*
- A Closed risk stays in the register for history but leaves the dashboard and meeting sections.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Project members MUST be able to raise risks with title, description, owner, probability (1–3), impact (1–3), mitigation, trigger indicator, review date, and links to tasks, deliverables, and milestones; severity MUST be probability times impact, banded Low (1–2), Medium (3–4), High (6–9). *(FR-RSK-01..05, §12.10)*
- **FR-002**: Probability and impact levels MUST carry plain-language anchors, such as Impact High meaning "would move a submission milestone or require rework of an issued deliverable". *(§12.10)*
- **FR-003**: Risk status MUST be Open, Monitoring, Closed, or Realised; Realised MUST require creating or linking an issue. *(§10.2, RSK-03)*
- **FR-004**: A risk past its review date MUST show "Review overdue", and High open risks MUST appear on the dashboard and in Weekly Coordination. *(RSK-01, RSK-02)*
- **FR-005**: Project members MUST be able to raise issues with title, description, raised by, owner, severity (Low, Medium, High), date raised, target resolution date, and links; status MUST be Open, In Progress, Resolved, or Cancelled. *(FR-ISS-01..05, §12.10)*
- **FR-006**: Resolved MUST require resolution text and record the resolved date; an issue past its target date MUST be Overdue. *(ISS-02, ISS-03)*
- **FR-007**: A High-severity issue that is Open or In Progress MUST raise A-07 as Critical to its owner and the PM and count towards Red project health. *(ISS-01, A-07, §16.3)*
- **FR-008**: Risks and issues MUST have keys (`{project}-R01`, `{project}-I01`), register tables with filters by status, severity, owner, and discipline, default sorts (risks by severity then review date; issues by severity then target date), detail panels with links and comments, and "raise from here" on task and deliverable panels. *(§9.5, §12.10, §13.13)*
- **FR-009**: The dashboard MUST count open and High issues and High risks, and Weekly Coordination MUST show open issues and High risks in section 10. *(§13.1, §12.13)*
- **FR-010**: The Open Issues / High Risks report MUST be available with project and severity parameters. *(§19)*

### Key Entities *(include if feature involves data)*

- **Risk**: Key, title, description, owner, probability, impact, severity, mitigation, trigger indicator, review date, status, realised issue.
- **Issue**: Key, title, description, raised by, owner, severity, dates raised and targeted, resolution, resolved date, status, originating risk.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every High open issue on a pilot project is visible on the dashboard and in the meeting within one evaluation cycle.
- **SC-002**: A team member raises a scored risk in under 1 minute.
- **SC-003**: No risk is marked Realised without a linked issue (100 % in testing).

## Assumptions

- Depends on packets 005 (attention and health), 006 (comments), and 007 (surfaces). A-07 and the issue input to health activate with this packet.
- Who may edit follows §8.5.2: the PM and leads raise and edit; team members raise and edit their own.

## Coordination expansion amendment — 2026-09-26

Packet 033 extends the same Issue ID with location/document-revision context and independent verification (§38.3). Preserve existing Issue vocabulary, permissions and audit; do not create a parallel issue register.

This is approved specification scope with implementation pending in the named new packets. Historical verification for this packet does not verify the added behaviour.
