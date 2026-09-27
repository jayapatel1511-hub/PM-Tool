# Verification: Tasks and Review

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (134/134 with packet 005's tests) |
| `npm run build` (web: `tsc -b` and Vite) | Pass |
| `npx oxlint src` (web) | No errors; warnings are fast-refresh export hints and one hook-deps hint |

`TasksTests` (13, all pass):

| Test | Covers |
|---|---|
| Assigning a non-member adds them and tells the PM | AC-TEAM-01, TM-06, assignment notice |
| Assignee starts work but cannot reassign | AC-PERM-03 |
| Review flow requires reviewer, comment and counts rounds | AC-TSK-02 (Complete not offered, `review_required`), AC-TSK-03, R-01, R-03, AC-REV-02, reviewer notice |
| Self-review is refused unless allowed | AC-REV-04, R-02 |
| Changing reviewer mid-review notifies both | AC-REV-05, R-04, change logged |
| Completing sets progress and reopen needs reason and resets | AC-TSK-07, AC-TSK-08, T-14, T-20 |
| Collaborator updates progress but not due date | AC-TSK-11, T-15 |
| Stale version is refused with who and when | AC-TSK-12, G-07 |
| Assignee due change needs reason and is counted | T-16, FR-014, due-change notice |
| Bulk shift skips forbidden rows and sends one notice | FR-TSK-09, T-23, E-20, AC-NOT-06 (12 updated, 2 skipped, one notice) |
| Delete removes edges and restore brings them back | AC-TSK-09, T-08, D-09, E-04 (snapshot lists the edges) |
| Removing a member lists open items and flags left tasks | AC-TEAM-03, TM-04 (`assignee_not_on_project` after evaluation) |
| Lists filter, sort and page | FR-017 default sort, `mine`, paging, Read Only can list |

## Manual checks (browser pane, dev data on project 2026-0417)

| Check | Result |
|---|---|
| Tasks tab: grouped by deliverable (default) with group status and progress, "Add task" per group, sticky header | Pass |
| Indicator chips from derived state: Overdue 3d, Blocking 1, Blocked (manual and dependency), Waiting, Due soon, Unassigned | Pass |
| Blocked/Waiting chip opens the blockers popover without opening the panel (§13.3 UX) | Pass |
| Status menu lists only server-allowed transitions, with "via In Progress" where a path passes through it | Pass |
| On Hold from the status menu asks for a reason (confirm disabled under 5 characters); saved, and re-evaluation cleared Waiting/Blocking chips | Pass |
| Task panel from the URL: header, blockers box naming the overdue predecessor and the affected milestone, fields, dependencies, manual block, people, history | Pass |
| Chain view: predecessor above, task highlighted, successors below with states | Pass |
| Board: four lanes with counts, Done limited to 14 days, cards with key, name, exact review status, deliverable chip, initials, coloured due date, icons | Pass |
| Board drop into an allowed lane saves the transition (T0008 → In Progress, confirmed through the API) | Pass (see note) |
| Board drop refused for a Team Member on someone else's task: card snaps back with the server's reason | Pass |
| Keyboard pick-up on a card announces through the live region (i18n announcements) | Pass |

Note on board drags in the browser pane: the pane fires a trusted `visibilitychange` event while it is not in front,
which dnd-kit treats as a cancel. The drop checks above ran with that one event ignored in the page; the app code is
unchanged. Worth a real-browser drag check before release.

## Decisions and deviations

- AC-TEAM-01 says the PM assigns and the PM is notified; §17 never notifies a user about their own action, so the test
  has the Discipline Lead assign and checks the PM's notice.
- Board lanes follow §13.4/§36.3 (four lanes). FR-020's "On Hold and Cancelled collapsed" is a "Show On Hold and
  Cancelled" toggle that adds those two lanes; a count says how many On Hold tasks are hidden.
- The board's add control is on the To Do lane (creating straight into a later lane would need a create plus a
  transition). Manual card order is packet 019.
- The list loads pages of 200 in the background and renders only visible rows (E-22); group headers scroll with the
  rows rather than sticking.
- Inline edits in rows are hidden when client-side hints say the user cannot make them; the server still decides.
- Filter tokens, saved views and exports for the list belong to packets 009 and 019; this packet has the quick chips
  and basic selects.

## Deferred (cross-packet rule)

- Comments tab and document links in the task panel appear through `ItemSlots` with packet 006.
- Actual hours and Add Time plug into `TASK_SECTIONS` with packet 024.
- The review queue on My Work is packet 007.
