# Feature Specification: Saved Views and Board Ordering

**Feature Branch**: `019-saved-views-and-board-ordering`

**Created**: 2026-09-24

**Status**: Draft (first release; moved by §36)

**Input**: User description: "Convert the Engineering Project Coordination Hub specification into Spec Kit: packet 019, saved views and board ordering (personal and project-shared views of any list, and manual card order on the board)."

**Source**: Product specification §11.10 (FR-VIEW-04), §13.4, §18.4, §36.3, §36.7

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Save my own view of any list (Priority: P1)

A user saves the filters, sort, columns, and grouping of a list under a name, switches between
saved views, and marks one as their default for that list.

**Why this priority**: People rebuild the same filters every day; bookmarks only partly cover it.

**Independent Test**: Save two views of the task list, set one as default, and reopen the list.

**Acceptance Scenarios**:

1. **Given** a filtered, grouped task list, **When** the user saves it as "Civil overdue", **Then** it appears in their view switcher and restores the same filters, sort, columns, and grouping. *(FR-VIEW-04, §18.4)*
2. **Given** a view marked as default, **When** the user opens that list, **Then** it opens in that view. *(§18.4)*

---

### User Story 2 - Share project views with the team (Priority: P2)

A PM or lead saves a view for the whole project, such as "Blocked by discipline", which every
member sees in their switcher.

**Why this priority**: A team working from the same view has the same conversation.

**Independent Test**: As a lead, save a project view and confirm a team member sees it and cannot
change it.

**Acceptance Scenarios**:

1. **Given** a lead saves a project view, **Then** every project member sees it; only the PM and leads can change or delete it. *(§18.4)*

---

### User Story 3 - Order cards by hand on the board (Priority: P2)

On the board, a team drags cards within a column into their preferred order, and that order is
kept for everyone on the project.

**Why this priority**: Teams that plan in columns want their own order within a status.

**Independent Test**: Reorder three cards in a column, reload, and confirm the order holds for
another member.

**Acceptance Scenarios**:

1. **Given** the board sorted manually, **When** a user drags a card above another in the same column, **Then** the new order persists for that lane, shared for a single-project board or personal for a named workspace. *(§13.4, §36.3)*

---

### Edge Cases

- Views store definitions, not results, so they always show current data. *(§18.4)*
- A view that refers to a field or value that no longer exists opens with that filter dropped and says so.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to save named views of any list (tasks, deliverables, decisions, projects, and other lists) with filters, sort, columns, and grouping, switch between them, and mark one default per list. *(FR-VIEW-04, §18.4)*
- **FR-002**: PMs and leads MUST be able to save project views visible to all project members. *(§18.4)*
- **FR-003**: Views MUST store definitions, not results. *(§18.4)*
- **FR-004**: The board MUST offer a manual sort in which card order within a lane persists per project for its members or per user-owned named workspace for that user. *(§13.4, §36.3)*

### Key Entities *(include if feature involves data)*

- **Saved view**: Owner, scope (personal or project), project, list type, name, filters, sort, columns, grouping, default flag.
- **Card order**: The manual position of a task within a status lane for one project (shared) or one user-owned named workspace (personal).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Opening a saved view takes one click and restores the view exactly (100 % in testing).
- **SC-002**: Manual board order survives reloads and is the same for every member.

## Assumptions

- Depends on packets 004 and 009.
