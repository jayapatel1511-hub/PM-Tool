# Verification: Saved Views and Board Ordering

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (258/258) |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`ViewsTests` (4, all pass):

| Test | Covers |
|---|---|
| Personal views restore their definition and one is default | FR-001 / US1-1 (discipline, overdue, grouping, sort and columns come back; the open panel is not saved), FR-003, US1-2 (default moves when another is chosen), another person sees none and cannot delete it |
| Project views are shared and changed only by the PM and leads | FR-002 / US2-1: a lead's view is seen by a Team Member with the owner's name, read-only; the member cannot share, edit or delete; the PM edits; a stale edit is refused with a conflict (G-07) |
| A view whose references are gone opens without them | Spec edge case: a deleted deliverable and a retired parameter are dropped and named; the rest of the view still applies |
| Manual board order is shared by the team | FR-004 / US3-1 / SC-002: a Team Member's order is what the lead sees; a later partial reorder keeps the rest; a Viewer is refused; tasks from another project are refused |

`PermissionSweepTests.The_team_arranges_the_board_but_viewers_and_outsiders_do_not` covers `ArrangeBoard` for every
persona and an Archived project.

## Manual checks (browser pane, project 2026-0417)

| Check | Result |
|---|---|
| Task list filtered by overdue and grouped by assignee saved as "Overdue by person"; the menu shows it as active | Pass |
| Opening the plain list and choosing the view restores overdue, grouping and every column | Pass |
| Board with Manual order: dragging T0007 below T0004 in To Do saves the order, and another user's list returns T0002, T0004, T0007 | Pass (a drop on the lane itself, which the keyboard produced, was first ignored and now moves the card to the lane's end) |

## Decisions

- Milestones keep their filters in page state rather than the address, so they have no saved views yet.
- Deleting a view is a hard delete: views are preferences, not work items (constitution: work items are soft-deleted).
