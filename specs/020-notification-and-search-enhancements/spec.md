# Feature Specification: Notification and Search Enhancements

**Feature Branch**: `020-notification-and-search-enhancements`

**Created**: 2026-09-24

**Status**: Draft (Phase 2)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 020, notification and search enhancements (a weekly PM summary email, digest content preferences, live unread counts, and search over descriptions and comments)."

**Source**: Product specification §17.4, §17.6, §18.1, §28 (items 9 and 12)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A weekly summary for each PM (Priority: P1)

Once a week, before their coordination meetings, each PM receives one email summarising every
project they manage: health (computed and reported), next submission, overdue and blocked counts,
overdue decisions, the top attention items, and what changed since last week.

**Why this priority**: It gives PMs a portfolio view of their own projects without opening each
one.

**Independent Test**: For a PM with three projects, generate the weekly summary and compare it
with each project's dashboard.

**Acceptance Scenarios**:

1. **Given** a PM of three Active projects, **When** the weekly summary is sent, **Then** it contains one section per project with the values above, and its numbers match the dashboards. *(§28 item 9)*
2. **Given** a PM turns the weekly summary off, **Then** they no longer receive it. *(§17.4)*

---

### User Story 2 - Choose what the digest contains (Priority: P2)

Each person switches digest sections on or off, for example keeping overdue and review sections
but dropping project updates.

**Why this priority**: Different roles value different sections; all-or-nothing leads to people
switching the digest off.

**Independent Test**: Turn off Project updates and confirm the next digest omits it.

**Acceptance Scenarios**:

1. **Given** a user turns off the Project updates section, **Then** their next digest has no Project updates section and its subject omits the update count. *(§28 item 9)*

---

### User Story 3 - Unread counts update live (Priority: P2)

The notification bell and the Following tab update as soon as something arrives, instead of every
minute.

**Why this priority**: Faster feedback during meetings and reviews.

**Independent Test**: Assign a task to a signed-in user and time how long their bell takes to
update.

**Acceptance Scenarios**:

1. **Given** a signed-in user, **When** they are assigned a task, **Then** their unread count updates within 5 seconds without reloading the page. *(§17.6)*

---

### User Story 4 - Search descriptions and comments (Priority: P2)

Search also finds words in task and deliverable descriptions and in comments, with results showing
the matching text and respecting permissions.

**Why this priority**: "Where did we discuss the hydro pole relocation?" is a common question.

**Independent Test**: Search for a phrase that appears only in a comment and open the result.

**Acceptance Scenarios**:

1. **Given** a comment containing "hydro pole relocation", **When** a user who can see that project searches for it, **Then** the comment's item appears with the matching text highlighted; deleted comments never appear. *(§18.1)*

---

### Edge Cases

- A PM with no Active projects receives no weekly summary.
- Search over comments respects Restricted projects exactly as other search results do. *(§18.1)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each PM MUST receive a weekly summary email, which they can turn off, covering every Active project they manage with computed and reported health, next submission, overdue and blocked counts, overdue decisions, top attention items, and changes since the previous summary. *(§28 item 9)*
- **FR-002**: Users MUST be able to switch individual digest sections on or off. *(§28 item 9)*
- **FR-003**: Unread counts MUST update within 5 seconds of a notification or feed entry arriving, without a page reload. *(§17.6)*
- **FR-004**: Search MUST include task and deliverable descriptions and non-deleted comment text, permission-filtered, showing the matching text. *(§18.1)*

### Key Entities *(include if feature involves data)*

- **Digest preference**: A person's on or off choice for each digest section and for the weekly summary.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Weekly summary numbers match the project dashboards (100 % in testing).
- **SC-002**: Unread counts update within 5 seconds for 95 % of arrivals.
- **SC-003**: A phrase from a comment is found in under 2 seconds.

## Assumptions

- The specification names the weekly PM summary without detail (§28 item 9); its content and timing above (sent on the morning of each PM's earliest coordination day, default Monday 07:00) are reasonable defaults to confirm with `/speckit-clarify`.
- Per-project mute already exists in the MVP as the Muted follow level (packet 006).
- Depends on packets 006 and 009.
