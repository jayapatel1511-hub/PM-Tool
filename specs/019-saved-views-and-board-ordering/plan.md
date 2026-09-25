# Implementation Plan: Saved Views and Board Ordering

**Branch**: `019-saved-views-and-board-ordering` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

Saved views for every list whose state lives in the address (tasks, board, deliverables, decisions, projects,
portfolio, workload): personal views with one default per list, project views shared by the PM and leads, and
"left out" notices when a stored reference no longer exists. The project board gains a Manual order whose card
positions are shared by the project's members.

## Technical Context

As packet 001. `saved_view` and `board_order` exist from the initial schema; migration `SavedViewConcurrency` makes
saved views versioned (created/updated by, row version) because project views are edited by several people.
Endpoints: `GET/POST /views`, `PATCH/DELETE /views/{id}`, `PUT /projects/{id}/board-order`; the task list accepts
`sort=board`. Lists now also read their columns from `cols=` in the address, so a view or link carries them.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Views store list parameters only; no view builder or calculated columns | Pass |
| II | A view reopens the same list query; results are always current (FR-003) | Pass |
| III | Project views show who created them | Pass |
| IV | No new statuses | Pass |
| V | Views and card order are personal or team preferences, not logged work changes (§20.1 "not logged" preferences); deleting a view removes it outright because it is not a work item | Pass |
| VI | Personal views are the owner's alone; project views are seen by members and changed only by the PM and leads (`ManageSavedProjectView`); arranging the board needs a writing member (`ArrangeBoard`: PM, leads, Team Members, Reviewers) | Pass |
| VII | No new infrastructure; one additive migration | Pass |

## Design notes

- A view holds the list's own query parameters; unknown keys are refused on save and dropped (and named) on open,
  as are references to deleted or inactive items, people and reference data.
- The default view applies only when a list opens without parameters, so shared links always win.
- Board order: dropping a card within its lane (Manual order chosen) rewrites the positions of that lane's shown cards;
  cards without a position follow by due date. Named workspaces' personal order is packet 022.

## Project Structure

```text
src/Hub.Api/Features/Views.cs; Data/Entities.cs (SavedView : Audited) + migration; Features/TaskQueries.cs (sort=board)
src/Hub.Domain/Permissions.cs (ArrangeBoard)
tests/Hub.Tests/Api/ViewsTests.cs, Domain/PermissionSweepTests.cs
web/src/components/hub/views.tsx (+ view menus on the lists), table.tsx and Tasks.tsx (cols= from the address), Board.tsx (Manual order)
```

## Checks

FR-001..FR-004 as API tests, the permission rows as a domain sweep; saving and reopening a view and a manual reorder
in the browser.

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| `board_order` rows carry no row version | Positions are rewritten for a whole lane on each drop; last write is what "manual order" means, and a version check would reject unrelated concurrent moves | A version per row would turn every concurrent rearrangement into a conflict without protecting any data |
