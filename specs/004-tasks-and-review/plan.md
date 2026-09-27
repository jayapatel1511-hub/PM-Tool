# Implementation Plan: Tasks and Review

**Branch**: `004-tasks-and-review` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)

## Summary

Tasks with one accountable assignee and one owning discipline, the T-10..T-14 workflow with independent
review (R-01..R-05), collaborators and watchers, due-date discipline (T-16, change count), manual blocks,
soft delete with restore, bulk actions with per-row permission, the task list, the task panel and full
page, and the board. Derived indicators and dependencies come from packet 005.

## Technical Context

As packet 001. Tables `task`, `task_collaborator`, `item_watcher` exist in the initial schema.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Units of coordination only; no subtasks, checklists, multiple assignees (§30) | Pass |
| II | Transitions are data (`Workflow.TaskStep/TaskPath`); no computed status stored | Pass |
| III | `assignee_id` single; collaborators and watchers do not share accountability | Pass |
| IV | Canonical task statuses verbatim; board lanes are presentations (§36.3) | Pass |
| V | Every change logged by `SaveChanges`; reasons for hold, cancel, reopen, restore, non-PM/DL due changes, bulk shifts (G-09); deletion snapshot (T-08) | Pass |
| VI | `Permissions.TaskTransition/EditTask/AssignTask/ChangeDueDate/ManualBlock/DeleteTask` per step and per bulk row (T-23) | Pass |
| VII | No new infrastructure; dnd-kit for board drag | Pass |

## Design notes

- One list query (`TaskQueries.Apply` + `Rows`) serves the project list, board, cross-project views and
  My Work, so counts reconcile with lists (§13.1 "same code path").
- A Not Started task completed from a checkbox passes through In Progress (`Workflow.TaskPath`), so no
  guard is skipped (FR-VIS-08, AC-VIS-06); resubmitting a revision does the same.
- `review_requested_at` records entry into Ready for Review for the stalled-review rule (R-05, A-11).
- Bulk actions produce one summary notification per recipient (AC-NOT-06, §17.5).
- Deleting removes dependency edges (D-09) and stores them in the snapshot so restore recreates them (E-04).
- Moving a task to In Progress moves a Not Started deliverable to In Progress (DL-02 recommendation).

## Project Structure

```text
src/Hub.Api/Features/TaskQueries.cs, Tasks.cs
tests/Hub.Tests/Api/TasksTests.cs, TestData.cs (task helpers)
web/src/pages/projects/Tasks.tsx      list, filters, create, bulk, shared task actions
web/src/pages/projects/TaskPanel.tsx  panel and full page (PANELS.Task, TASK_SECTIONS)
web/src/pages/projects/Board.tsx      board on @kibo-ui/kanban (dnd-kit)
web/src/components/hub/richtext.tsx   bold, lists and links for descriptions and comments
```

## Checks

AC-TEAM-01, AC-TEAM-03, AC-PERM-03, AC-TSK-02, AC-TSK-03, AC-TSK-07, AC-TSK-08, AC-TSK-11, AC-TSK-12, AC-REV-02,
AC-REV-04, AC-REV-05, T-16, FR-TSK-13, bulk T-23, delete/restore as API tests.

## Complexity Tracking

None.
