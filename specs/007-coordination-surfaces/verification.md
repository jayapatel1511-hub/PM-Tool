# Verification: Coordination Surfaces

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (225/225) |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`SurfacesTests` (6, all pass):

| Test | Covers |
|---|---|
| Every dashboard count equals the list it opens | AC-DASH-01 (Blocked = 3 opens 3 rows), SC-004 (every task and deliverable count is replayed against its list with the link's own query string), AC-DASH-02 (one row per active discipline; open and blocked match the filtered list), discipline scope filters counts and links |
| Milestone strip shows five with overdue first | AC-DASH-03 |
| Coordination sections, scope and review marker | FR-001 (week starting Wednesday), AC-WC-02 (three tasks with one blocker), overdue and upcoming sections, AC-WC-01 (completed since review, then nothing after marking reviewed), AC-WC-05 (review logged, Team Member refused), AC-WC-07 (discipline scope) |
| My Work sections and who may see them | AC-MYW-01 (4 tasks, 2 reviews, 1 deliverable), AC-MYW-02 (blocker and its owner), AC-MYW-03 (successors and owners), AC-MYW-04 (supervisor read-only, others refused, a PM sees only their project) |
| My Staff scopes, counts and staffing | AC-ASG-07 (Sam sees Alex; Lena sees Sam not Alex; Standard User refused; all staff for Executives only), AC-ASG-08 (tasks on a Restricted project Sam cannot see are excluded from counts and assignments), AC-ASG-09 (Sam adds his report as Team Member following All activity; PM notified naming Sam; refused for someone else's report or another role) |
| Supervisors reassign their direct reports' tasks only | §8.5.1; due dates stay with the assignee and lead |

## Manual checks (browser pane, project 2026-0417)

| Check | Result |
|---|---|
| Project opens on the Dashboard: milestone strip, health/next milestone/next submission/phase tiles, counts, attention, discipline table, due this week, blocked, activity | Pass |
| Weekly Coordination in meeting mode: larger type, window and review marker, section cards with counts, attention with snooze | Pass |
| Arrow Right / Left move focus between section headers (AC-WC-03) | Pass (after making the key handler tolerate a non-element event target) |
| My Work as Alex: attention routed to him, tasks by due bucket with inline status and progress, section jump bar | Pass (task names now keep a minimum width on narrow screens) |
| My Staff as Sam: tiles, default sort, expanded assignments with Remove and Assign to project; below tablet width a read-only card list (FR-017) | Pass |

## Defects found and fixed while verifying

- `open=true` on the task list was silently ignored (only `indicator=open` worked), so counts and sections that used
  it included completed tasks. `open` is now a boolean filter like the others (caught by the discipline table test).

## Decisions

- Decision counts on the dashboard link to the decision register with `status=` and `indicator=overdue`; packet 008
  builds that register with the same filters.
- "Record decision" in meeting mode arrives with packet 008; decision rows already open their panel.
- The dashboard's Issues and Risks counts appear only when either is non-zero (Phase 2 registers, packet 014).

## Deferred (cross-packet rule)

- Portfolio and hours-based Workload are packets 016 and 017; the Staff Assignments report is packet 009.
