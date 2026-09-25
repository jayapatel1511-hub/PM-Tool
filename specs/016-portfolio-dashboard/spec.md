# Feature Specification: Portfolio Dashboard

**Feature Branch**: `016-portfolio-dashboard`

**Created**: 2026-09-24

**Status**: Draft (first release; moved by §36)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 016, the portfolio dashboard (which projects need help this week and why, computed and reported health side by side, health trends, and portfolio reports)."

**Source**: Product specification §7.6, §11.9 (FR-PORT-01, FR-HLT-03), §13.12, §16.4, §16.5, §19, §36.6

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See which projects need help this week (Priority: P1)

An Executive (persona Lena) responsible for 40 to 100 projects opens the portfolio: summary
tiles, then one row per project with PM, client, office, phase, health, next milestone and
submission, overdue and blocked tasks, overdue decisions, high issues, and attention counts,
sorted worst first.

**Why this priority**: Regional managers today have no single place to see this, and PM-reported
health is optimistic.

**Independent Test**: With 40 active projects, filter to one office and to submissions within 14
days, and open a project's dashboard from the list.

**Acceptance Scenarios**:

1. **Given** the portfolio, **Then** the tiles show active projects, Red, Yellow, and Green counts, submissions in the next 14 days, and total overdue decisions. *(§13.12)*
2. **Given** filters for PM, discipline, client, office, status, phase, health, project type, and "submission within N days", **Then** only matching projects appear, sorted by health severity and then next submission. *(FR-PORT-01)*
3. **Given** a row, **When** the user opens it, **Then** they reach the project's dashboard or its Weekly Coordination. *(§13.12)*

---

### User Story 2 - Optimism is visible, not hidden (Priority: P1)

When a PM's reported health differs from the computed health, the portfolio shows both, with the
PM's note and how long ago it was set.

**Why this priority**: The value of computed health is lost if an override can quietly replace it.

**Independent Test**: Override one project's health and confirm both values and the note show in
the portfolio.

**Acceptance Scenarios**:

1. **Given** a project computed Yellow and reported Green, **Then** the row shows "Computed: Yellow · Reported: Green — note by PM, 3 days ago" with the reason available. *(§16.4, §13.12)*

---

### User Story 3 - See how health has moved (Priority: P2)

Each row shows an 8-week health trend from the nightly snapshots, so "how long has this been red?"
is answered at a glance.

**Why this priority**: Trends separate a bad week from a chronic problem, but need snapshots to
accumulate first.

**Independent Test**: With 8 weeks of snapshots, confirm the trend matches the recorded colours.

**Acceptance Scenarios**:

1. **Given** 8 weeks of snapshots for a project, **Then** its trend shows each week's computed health in order. *(FR-HLT-03, §16.5)*

---

### User Story 4 - Portfolio reports (Priority: P2)

Executives export Projects At Risk and Health History for their region.

**Why this priority**: Management reporting happens outside the Hub; the numbers should come
from it.

**Independent Test**: Run both reports for one office and compare them with the portfolio.

**Acceptance Scenarios**:

1. **Given** Projects At Risk for an office, **Then** it lists project, PM, computed and reported health with reasons, and next submission, and exports like other reports. *(§19)*

---

### Edge Cases

- Setup and On Hold projects are excluded from the colour counts; On Hold projects stay listed with an On Hold marker. *(§10.2)*
- A Restricted project appears only to viewers who may see it. *(§8.7)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The portfolio MUST show tiles for active projects, Red, Yellow, and Green counts, submissions in the next 14 days, and overdue decisions, and a table with project, PM, client, office, phase, computed and reported health with reasons, next milestone, next submission, overdue and blocked tasks, overdue decisions, high issues, Critical and Warning attention counts, and an 8-week health trend. *(FR-PORT-01, §13.12)*
- **FR-002**: The portfolio MUST filter by PM, discipline, client, office, status, phase, health, project type, and submission within N days, sort by health severity then next submission by default, and export. *(§13.12)*
- **FR-003**: Executives and Supervisors MUST see all projects they may view; PMs MUST see their own projects by default and all with a filter. *(§8.5.1)*
- **FR-004**: When reported and computed health differ, both MUST be shown with the PM's note and its age. *(§16.4)*
- **FR-005**: The Projects At Risk and Health History reports MUST be available. *(FR-RPT-01, §19)*

### Key Entities *(include if feature involves data)*

- No new entities; the portfolio reads projects, derived state, attention items, overrides, and health snapshots from earlier packets.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An Executive identifies the projects needing help this week, and why, in under 2 minutes.
- **SC-002**: Every project whose reported health differs from computed shows both in 100 % of cases.
- **SC-003**: The portfolio of 100 projects is usable within 2 seconds for 95 % of loads.

## Assumptions

- Depends on packets 005 (health, snapshots) and 007; high issues appear once packet 014 is in place.
- Cross-office comparisons, discipline throughput, and on-time submission rates are Phase 3 (§29).
