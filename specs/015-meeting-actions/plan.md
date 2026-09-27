# Implementation Plan: Meeting Actions

**Branch**: `015-meeting-actions` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

Meetings (title, date, type, minutes link, optional calendar event) with the actions agreed in them, each owned by a
person, a discipline or an external party; capture in Weekly Coordination meeting mode against today's coordination
meeting; routing to the owner or the discipline's lead (the PM when there is no lead) and nothing sent outside; a
"Waiting on client / external" section; converting an action to a task that it then follows; the Meeting Actions
Outstanding report.

## Technical Context

As packet 001. The schema already holds `meeting` and `meeting_action` (owner type and the three owner columns, related
task and decision), the `A` key sequence, `ActionStep`, the `converted_to_task` link relation, the `ActionAssigned`
notice (in-app only) and the rules engine's overdue-action attention routed to the lead or the PM. No migration.

New: `Features/Meetings.cs` (meetings, today's coordination meeting, actions, convert, export);
`Workflow.ActionFollowing` and `Registers.ActionOverdueDays` with worked examples; a step in the audited save that moves
converted actions with their task (`HubDb.FollowTasks`); `TaskEndpoints.NewTask` split out of task creation so
conversion applies every task rule; My Work `actions`; Weekly Coordination `waiting`; the `meeting-actions-outstanding`
report; activity names for actions and meetings. Web: `pages/projects/Meetings.tsx` (register grouped by meeting, meeting
and action forms, action panel, convert dialog), meeting-mode capture and section "Waiting on client / external" in
`Coordination.tsx`, the My Work section, and "Meeting action" in the Raise menu of task and deliverable panels.

Endpoints: `GET/POST /projects/{id}/meetings`, `POST /projects/{id}/meetings/current`, `PATCH /meetings/{id}`,
`GET /projects/{id}/actions`, `POST /meetings/{id}/actions`, `GET /actions/{id-or-key}`, `PATCH /actions/{id}`,
`POST /actions/{id}/transition`, `POST /actions/{id}/convert`, `GET /projects/{id}/actions/export`,
`GET /reports/meeting-actions-outstanding`.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Routing and status-following are fixed rules; nothing is inferred | Pass |
| II | My Work, Weekly Coordination, the register and the report read the same action rows | Pass |
| III | Every action has exactly one owner of its stated type; a discipline's action is the lead's to act on, or the PM's when there is no lead | Pass |
| IV | `ActionStep` governs manual moves; a converted action follows its task (Complete, Cancelled, else In Progress) and refuses manual moves | Pass |
| V | Creation, edits, moves, "Converted" and the follow-on status changes are logged in the same save as their cause | Pass |
| VI | Meetings: PM and leads (`RunCoordination`); actions: `RaiseRegisterItem` to add, `EditRegisterItem` to change; conversion also needs the task rules | Pass |
| VII | No new infrastructure; no message leaves the organisation for external owners (MTG-02) | Pass |

## Design notes

- Meeting mode asks for today's coordination meeting when the first action is captured and records it if missing;
  actions captured from a task or decision row are linked to it and appear in the meeting's change tray.
- The follow-on step runs inside `SaveChangesAsync`, so a task completed from its panel, the board or a bulk edit moves
  its action in the same transaction.
- A converted task's discipline is the owning discipline, else the owner's primary or led discipline on the project, else
  the related task's; the dialog asks when none applies.
- External-party actions produce no notification at all; they are waited on in section "Waiting on client / external".

## Complexity Tracking

| Deviation | Why | Simpler alternative rejected because |
|---|---|---|
| Meeting edits carry the row version in `If-Match` only | The meeting body has no version field; the web sends `If-Match` as other edits do | Adding a body version to one small form gains nothing |
