# Implementation Plan: Team Calendar

**Branch**: `023-team-calendar` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

A Calendar with Week, Month and Agenda views, today and previous/next, project scope and four type toggles: all-day
Project Deadline projections of open tasks, deliverables and milestones (opening their source item), and Meeting,
Site Work and Internal Task events created, edited and cancelled in the calendar.

## Technical Context

As packet 001. `calendar_event` exists from the initial schema (versioned, logged, cancellation fields). Endpoints:
`GET /calendar?from&to&projectIds&types`, `POST /calendar/events`, `GET/PATCH /calendar/events/{id}`,
`POST /calendar/events/{id}/cancel`. The API takes and returns local date-times in the organisation's time zone and
stores UTC.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | No recurrence, invitations, Outlook/Teams sync or ICS feed (§29); events never create tasks or meetings | Pass |
| II | Deadlines are projections of the source dates, never copies; editing them happens in the source item | Pass |
| III | Every event has one owner | Pass |
| IV | Event types are the canonical three; cancellation is a state, not a delete | Pass |
| V | Event creation, edits and cancellation are logged (allow-list includes times, type, visibility, cancellation); cancelled events leave the default views but stay in history | Pass |
| VI | Creating a project event needs a writing project member (`CreateProjectEvent`); editing needs the owner or a project PM (`EditEvent`); Private events are the owner's alone; nothing from a project the viewer cannot see is returned, and a direct link answers 404 | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- Meeting and Site Work require a project; an Internal Task without a project is always Private.
- Deadlines: open tasks, open deliverables and non-cancelled milestones in the range; date-only, never given a time.
- Week view: an all-day row and a 06:00–20:00 hour grid; overlapping events share the column side by side.
- Month view: six weeks with three entries a day and "+n more" opening that week; Agenda: the next 30 days.
- A deadline opens its task, deliverable or milestone panel, where the source's date rules (reasons, cascade) apply.
- The view mode lives in `mode=` so the `view=` parameter stays free for saved views.

## Project Structure

```text
src/Hub.Api/Features/Calendar.cs
tests/Hub.Tests/Api/CalendarTests.cs
web/src/pages/Calendar.tsx
```

## Checks

FR-001..FR-005 and AC-VIS-04 as API tests; the three views, toggles and the event dialog in the browser.

## Complexity Tracking

None.
