# Verification: Timeline and Extras

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (237/237) |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`ExtrasTests` (3, all pass):

| Test | Covers |
|---|---|
| Copying a structure brings everything but people and dates | §27.1 / SC-003: new project in Setup; Civil not duplicated, Electrical added, no leads; both milestones without dates; two deliverables without owners or dates, linked to the copied milestone; three tasks (the cancelled one left behind) without assignees or dates, linked to the copied deliverable, estimates kept, keys from T0001; both dependencies between the copies; eight "Copied" log rows; a source the creator cannot see is refused and no project is created |
| A leaver's work is listed and reassigned from one place | FR-ADM-02 / E-01: an Inactive user's nine open items across three projects (assignee, reviewer, deliverable owner and reviewer, decision owner, Discipline Lead; a completed task excluded); §25.7: the Supervisor may act, another Supervisor and a Standard User are refused; one item reassigned alone, the rest at once; a self-review skipped with its reason and then given to someone else; every item moved, logged with the reason, the new owner notified, each PM told |
| Moving a milestone earlier without the cascade flags late deliverables | E-09: the preview lists the deliverable now due after the milestone; after the move it is Date inconsistent (DL-03) |

The cascade itself (AC-MS-05) is covered by `MilestonesDeliverablesTests` (packet 003).

## Manual checks (browser pane, project 2026-0417)

| Check | Result |
|---|---|
| Timeline at month zoom: legend, milestone lane with labels and status colours, Civil group with the deliverable bar and progress, today and start lines, read-only note | Pass |
| Week zoom scrolls horizontally with Monday labels; the label column stays in place | Pass |
| Reassign work for Alex as Admin: items grouped by project with role, key, status and due date; select all and "Reassign to" | Pass |

## Decisions

- Cancelled deliverables and tasks are not copied: they are scope the source project dropped.
- The timeline's quarter zoom is an addition to §12.16's week and month, for projects longer than a year.
- Each affected PM receives one "work reassigned" notice per project (as a staffing change) rather than one per item.

## Deferred (cross-packet rule)

- Tasks on the timeline, dependency arrows, dragging with confirmation, baselines and multi-project groups are packet
  018; templates replace copying in Phase 2 (packet 012).
