# Feature Specification: Search, Filters, and Reports

**Feature Branch**: `009-search-filters-reports`

**Created**: 2026-09-24

**Status**: Draft

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 009, search, filters, and reports (global search and key lookup, one filtering model for every list with shareable links, grouping and column choice, the standard reports with exports, and the Activity History view)."

**Source**: Product specification §11.10 (FR-VIEW-03), §11.11 (FR-SRCH, FR-RPT, FR-AUD-02–03), §11.13 (FR-ASG-08), §13.0 (filters, sorting, grouping, empty states), §13.14, §13.18, §18.1–§18.3, §19, §20, §31.15

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find anything by its key or name (Priority: P1)

One search box finds projects, tasks, deliverables, milestones, decisions, and people. Typing an
item key such as `1234-T0042` and pressing Enter opens that item directly.

**Why this priority**: With hundreds of projects and tens of thousands of tasks, search is basic
usability, and keys are how people refer to work in email and Teams.

**Independent Test**: Search by key, by project name, and by a person's name; confirm grouping,
ranking, permissions, and the archived toggle.

**Acceptance Scenarios**:

1. **Given** the user types `1234-T0042` and presses Enter, **Then** the task opens directly. *(AC-SRCH-01, FR-SRCH-02)*
2. **Given** the user types "Dundurn", **Then** results show the project first, then tasks, deliverables, milestones, and decisions whose names contain it, grouped by type, excluding archived projects unless the toggle is on. *(AC-SRCH-02, FR-SRCH-01)*
3. **Given** a Restricted project the user cannot see, **Then** none of its items appear in their results. *(§18.1)*

---

### User Story 2 - Slice any list the same way, and share the view (Priority: P1)

Every list offers the same filter bar: quick chips (Mine, Overdue, Blocked, Due this week,
Unassigned, and list-specific ones), a menu for every field, active filters as removable tokens,
and Clear, plus sorting, grouping, a column chooser, and export of what is on screen. The filters
live in the page address, so a copied link reproduces the view.

**Why this priority**: Every dashboard number opens a filtered list, and teams share views
constantly.

**Independent Test**: Filter the task list by status and assignee, copy the link to another user,
and confirm they see the same view; export it and open it in Excel.

**Acceptance Scenarios**:

1. **Given** a task list filtered by status and assignee, **When** the link is shared, **Then** the recipient sees the same filters applied. *(AC-SRCH-03, §18.3)*
2. **Given** filters on several fields and several values in one field, **Then** fields combine with AND and values within a field combine with OR. *(§18.3)*
3. **Given** a filtered list, **When** the user exports it, **Then** they receive the filtered rows as CSV or XLSX. *(§13.3, §22)*
4. **Given** a list with no matching rows, **Then** it says what would appear there and offers the main action. *(§13.0)*

---

### User Story 3 - Run the standard reports (Priority: P2)

People run fixed, rule-based reports with a few parameters and export them to Excel: tasks due
this week, overdue tasks, blocked tasks, tasks blocking others, upcoming deliverables,
deliverable status by project, upcoming milestones, open decisions, the review queue, stale work,
a project's activity log, and staff assignments.

**Why this priority**: People will ask for Excel on day one, but the live lists already cover the
daily need.

**Independent Test**: Run each report with parameters, open it as a filtered list, and export it;
confirm columns, scope, and the export format.

**Acceptance Scenarios**:

1. **Given** the Overdue Tasks report for two projects with a minimum of 1 day overdue, **Then** it lists key, task, project, assignee, days overdue, blocking count, and affected milestone, and offers "Open as filtered list". *(FR-RPT-01, §19)*
2. **Given** an XLSX export, **Then** it has a header row, a frozen pane, dates typed as dates, and a Parameters sheet recording the filters and when it was generated. *(§19)*
3. **Given** a Supervisor runs Staff Assignments, **Then** it lists their direct reports' project assignments (person, project, project status, roles, primary discipline, date added, who added them, open and overdue tasks there). *(FR-ASG-08)*
4. **Given** any export, **Then** the export is recorded (who exported which report, and when) but its contents are not. *(§19, §20.1)*

---

### User Story 4 - See who changed what, and when (Priority: P2)

Every project has an Activity History, and every item a History tab, listing each change newest
first with actor, action, item, the field changed from and to, and the reason where one was given.

**Why this priority**: "Who moved that date?" must always have an answer; the log itself is
recorded from packet 001, and this packet makes it readable.

**Independent Test**: Change a due date with a reason and delete a task, then check the item
History and the project Activity History.

**Acceptance Scenarios**:

1. **Given** a task's due date changes from 2027-01-10 to 2027-01-17 with the reason "Client extension", **Then** the item History shows the actor, time, "Due date: 2027-01-10 → 2027-01-17", and the reason. *(AC-AUD-01)*
2. **Given** a task is deleted, **Then** the project Activity History shows the deletion with a snapshot (key, name, assignee, status, due date) and the dependencies removed with it. *(AC-AUD-02)*
3. **Given** filters for date range, actor, item type, action type, and discipline, with "important only", **Then** only matching entries appear, and the project's history can be exported as CSV. *(FR-AUD-03, §13.14)*

---

### Edge Cases

- Search covers numbers, names, keys, subjects, and people, not descriptions or comments; that is Phase 2 (packet 020). *(§18.1)*
- Exports above 50,000 rows are split by project. *(§19)*
- CSV files are UTF-8 with a byte-order mark so Excel opens accented names correctly. *(§19)*
- Saved views are first-release scope (packet 019); bookmarked URLs remain shareable. *(§18.4, §36.7)*

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: One search box MUST search project numbers and names, clients, task, deliverable, and milestone keys and names, decision keys and subjects, and people's names and emails, showing the top 5 results per type in a dropdown and all results on a page with type tabs. *(FR-SRCH-01, §18.1)*
- **FR-002**: Entering a value that matches an item key pattern MUST open that item directly. *(FR-SRCH-02)*
- **FR-003**: Results MUST rank exact key, then project-number prefix, then name prefix, then text relevance, with recency as a tie-breaker and active projects first, and MUST include only items the user may see; archived and cancelled projects MUST appear only when requested. *(§18.1)*
- **FR-004**: Every list MUST offer quick filter chips, a menu for every filterable field, removable filter tokens, and Clear; operators MUST cover equals and in-list, date ranges, indicator flags, and free text within the list. *(§13.0, §18.3)*
- **FR-005**: Filters MUST combine across fields with AND and within a field with OR, and MUST live in the page address so links reproduce the view. *(§18.3, AC-SRCH-03)*
- **FR-006**: Lists MUST sort by clicking column headers (with due date and key as secondary sorts), group by the fields each list defines, and let the user choose columns. *(FR-TSK-09, §13.0)*
- **FR-007**: Every list MUST export its filtered rows to CSV and XLSX. *(§22)*
- **FR-008**: Empty lists MUST explain what would appear and offer the main action. *(§13.0)*
- **FR-009**: The reports catalogue MUST offer the first-release reports in §19 (Tasks Due This Week, Overdue Tasks, Blocked Tasks, Tasks Blocking Others, Upcoming Deliverables, Deliverable Status by Project, Upcoming Milestones, Open Decisions, Review Queue, Stale Work, Project Activity Log, Staff Assignments, Task Hours, portfolio and workload reports), each with its parameters, columns, and scope, respecting permissions, and "Open as filtered list" where a report maps to a list. *(FR-RPT-01, FR-ASG-08, §19, §36.8)*
- **FR-010**: XLSX exports MUST include a header row, a frozen pane, typed dates, and a Parameters sheet; CSV exports MUST be UTF-8 with a byte-order mark; exports MUST be limited to 50,000 rows per request and recorded without their contents. *(§19)*
- **FR-011**: Each project MUST have an Activity History and each item a History tab, newest first, showing time, actor or System, action, item, field changes from and to, reason, and source, filterable by date range, actor, item type, action type, and discipline, with "important only" (status, assignment, date, deletion). *(FR-VIEW-03, FR-AUD-02, §13.14)*
- **FR-012**: Deletions in the history MUST show a snapshot of the deleted item, and nothing in the history MUST be editable. *(§13.14, AC-AUD-02)*
- **FR-013**: The PM and management MUST be able to export a project's activity history as CSV. *(FR-AUD-03, §20.3)*

### Key Entities *(include if feature involves data)*

- **Search index entry**: The searchable names, keys, and subjects of projects, items, and people, with each item's visibility.
- **Report**: A named, fixed query with parameters, columns, and scope.
- **Export record**: Who exported which report or list, when, with which parameters.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Typing a key and pressing Enter opens the item in under 2 seconds.
- **SC-002**: Search results appear within 1 second for 95 % of queries.
- **SC-003**: 100 % of shared filtered links reproduce the same view for the recipient in testing.
- **SC-004**: Every report's totals equal the matching filtered list in testing.
- **SC-005**: Exports open in Excel with correct characters and date columns on the first try.

## Assumptions

- The activity log is recorded by packet 001; derived indicators used as filters come from packet 005.
- Search over descriptions and comments (packet 020) and register reports (packets 014 and 015) are Phase 2. Saved views (019), portfolio reports (016), and workload reports (017) are first-release scope under §36.
- Search uses the organisation's own data store; no external search service is assumed (§18.2).
