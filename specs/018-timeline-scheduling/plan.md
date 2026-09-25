# Implementation Plan: Timeline Scheduling

**Branch**: `018-timeline-scheduling` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

Extends packet 010's project timeline: tasks as thin, collapsible bars under their deliverables (and a "Tasks without
a deliverable" row per discipline), arrows for task dependencies and for derived and explicit deliverable
dependencies (red while the predecessor is unfinished and overdue), dashed ghost bars at original dates, and dragging
a bar or milestone diamond to a new date with a confirmation. The milestone cascade to tasks already existed and is
verified here.

## Technical Context

As packet 001. No new tables: original start and due dates already exist on deliverables and tasks and are kept on
first set. One read endpoint, `GET /projects/{id}/timeline` (tasks with original dates and date-edit permissions,
dependencies with the highlight, deliverable links, deliverable permissions). A confirmed drag saves through the
existing `PATCH /tasks/{id}`, `PATCH /deliverables/{id}` or `POST /milestones/{id}/change-date`, so every existing
rule applies (T-16 reasons, T-19, correction reasons on Complete projects, M-02..M-04 preview and cascade).

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | No critical path, float, levelling, calendars, constraint types, cost, or MS Project/P6 files; successors are never auto-shifted (§12.16, §30) | Pass |
| II | The bars, arrows and highlight come from stored dates and the rules engine's overdue state | Pass |
| III | Assignees shown on task labels | Pass |
| IV | Canonical statuses colour bars | Pass |
| V | Every drag is confirmed and saved as a normal, logged edit; cancelling changes nothing; the cascade logs each shifted item | Pass |
| VI | Only people who may change an item's dates can drag it (task: change-due-date and edit rights; deliverable: edit rights; milestone: PM) | Pass |
| VII | No new infrastructure; SVG arrows drawn by the page, no Gantt library | Pass |

## Design notes

- A drag snaps to whole days and moves start and due together; a task without a start moves only its due date.
- A click without movement opens the item; a drag shows "+n d" while moving and opens the confirmation on release.
- Derived deliverable arrows show only while both deliverables are collapsed; expanded, the task arrows show the detail.
- Assignees who drag their own task are asked for the reason T-16 requires; on Complete projects everyone is.
- Milestone drags open the existing date-change dialog pre-filled with the new date, with preview, cascade to
  deliverables and optionally their tasks.

## Project Structure

```text
src/Hub.Api/Features/Timeline.cs
tests/Hub.Tests/Api/TimelineTests.cs
web/src/pages/projects/Timeline.tsx
```

## Checks

FR-001..FR-005 as API tests (timeline data, permissions, a moved task changing only itself, baselines, cascade to
tasks); bars, arrows, ghost bars and a real drag in the browser.

## Complexity Tracking

None.
