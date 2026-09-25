# Verification: Milestones and Deliverables

Date: 2026-09-24.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (69/69 at the end of this packet) |
| `npx tsc -b` (web) | Pass |

`MilestonesDeliverablesTests`: date change keeps the original date, requires a reason, logs old/new, notifies the PM and
every Discipline Lead, previews and applies the cascade with one log entry per shifted deliverable, and the DL is refused
with a message naming the PM (AC-MS-04, AC-MS-05, AC-PERM-01); completion refused more than 7 days early (M-09), needs
confirmation with the list of un-issued deliverables (M-05, AC-MS-06), defaults the completed date to today (M-06) and
leaves the deliverable's status alone; deliverable due date defaults to the milestone (AC-DEL-01), owner defaults to the
discipline lead (DL-01), DL refused in another discipline (AC-PERM-02), due-after-milestone warns (DL-03), start after
due refused (DL-13); lifecycle guard message for Ready to Issue (AC-DEL-03), In Review needs a reviewer (DL-04),
Revision Required needs a comment stored as a Review comment and notifies the owner (AC-DEL-04), issue with an open task
needs confirmation and records revision and recipient (AC-DEL-05), client return keeps issued date and revision with one
issue-history row (E-24), delete refused with tasks (DL-09); discipline change moves tasks after confirmation (DL-10).

## Manual checks (browser pane)

| Check | Result |
|---|---|
| Milestones tab: strip with today marker and diamonds, table with original date, slip, status, readiness | Pass |
| Header next milestone with countdown; Setup checklist updates when a milestone and submission deliverable exist | Pass |
| Deliverable side panel opens from the URL (`?panel=Deliverable:<id>`), inline fields, tabs | Pass |

## Deferred (cross-packet rule)

- Milestone status (On Track / At Risk / Overdue), deliverable progress, At Risk and Date Inconsistent flags show once
  packet 005 evaluates the project; until then milestones show "Not evaluated".
- Comments and document links tabs appear with packet 006.
