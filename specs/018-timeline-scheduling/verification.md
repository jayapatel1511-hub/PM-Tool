# Verification: Timeline Scheduling

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (253/253) |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`TimelineTests` (3, all pass):

| Test | Covers |
|---|---|
| Tasks, arrows and permissions for the timeline | FR-001 task dates under their deliverables; FR-002 the arrow from an unfinished overdue predecessor is highlighted, the other is not; a derived deliverable link, highlighted; FR-003 the PM may drag everything, the assignee may drag their own task (with a reason) but not others or milestones, a non-member none, and the non-member's date change is refused |
| A dragged task changes only its own dates and keeps a baseline | §12.16 US2-1 / SC-002: the saved +3 days change the task only, logged; the successor is untouched; FR-004 original dates stay for the ghost bar; US3 a deliverable moved 10 days keeps its original due date |
| The milestone cascade can include tasks | FR-005 / M-04: the preview lists both tasks; the move shifts the deliverable and its tasks by 14 days, leaves another task alone, and logs three Cascade rows |

## Manual checks (browser pane, project 2026-0417, week zoom)

| Check | Result |
|---|---|
| "Show all tasks" expands task bars under D001 and a "Tasks without a deliverable" row; hatched overdue segment on T0001 | Pass |
| Red arrow from overdue T0001 to T0002; grey arrow into T0005 | Pass |
| Dragging T0003 three days right shows the confirmation (start 2026-09-15 → 2026-09-18, due 2026-09-30 → 2026-10-03, "Only this item's dates change"); confirming saves it and a dashed ghost bar marks the original dates | Pass |

## Decisions

- Drags move a bar whole (start and due together); resizing a bar's ends is left to the item panel's date fields.
- The quarter zoom and filters from packet 010 apply unchanged; previous/next navigation and multi-project groups are
  the cross-project Gantt of packet 022.
