# Implementation Plan: Decision Register

**Branch**: `008-decision-register` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

The thin decision register: decisions with one internal or external owner and a required-by date, project external
parties, links to the tasks a decision blocks and to related deliverables and milestones, and the record, defer,
cancel and reopen steps. Blocking, overdue and attention behaviour already lives in the packet 005 evaluator
(D-15, A-04, A-18); this packet supplies the data it reads and the screens.

## Technical Context

As packet 001. No new tables: `decision`, `external_party`, `item_link` and `decision_state` exist from the initial
schema, with the "exactly one owner" check constraint. Endpoints:
`GET/POST /projects/{id}/decisions`, `GET /decisions/{id|key}`, `PATCH /decisions/{id}`,
`POST /decisions/{id}/transition`, `POST /decisions/{id}/links`, `DELETE /item-links/{id}`,
`GET/POST /projects/{id}/external-parties`, `PATCH /external-parties/{id}`.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | A register, four steps and a link list; no approval chains or client portal | Pass |
| II | The register's overdue, due-soon and blocking values come from `decision_state`, the same evaluation the dashboard and Weekly Coordination count; the dashboard links (`status=`, `indicator=overdue`) open this register | Pass |
| III | Exactly one owner (DEC-01), enforced in the API and by the database check constraint | Pass |
| IV | Canonical statuses Pending, Under Review, Decided, Deferred, Cancelled; the step table is `Workflow.DecisionStep` | Pass |
| V | Every change is logged with the actor; deferral, cancellation and reopening carry a reason; the previous required-by date stays in History; link and unlink rows appear in the decision's history | Pass |
| VI | Raise: PM, leads, team members (`RaiseRegisterItem`); edit: PM, owner, requester (`EditRegisterItem`); record, defer, cancel: PM or owner (`DecideOrDefer`); reopen: PM only; external parties created by team members, edited by the PM | Pass |
| VII | No new infrastructure; no email ever goes to an external party (DEC-06) | Pass |

## Design notes

- The register masks derived flags for closed decisions so a decision recorded a moment ago never shows as overdue
  while the evaluation catches up.
- "Blocking (n tasks)" is `decision_state.blocking_task_ids` (linked tasks that are Waiting or Blocked because of it)
  and opens the task list with `ids=`, so the count equals the rows.
- Links default to `blocked_by_decision` for tasks and `related` for deliverables and milestones; only tasks can be
  blocked by a decision; one link per item.
- Making someone the internal owner adds them to the team (as tasks and deliverables do), so the notification link
  opens for them even on a Restricted project.
- The decision-recorded notice goes to the requester and, unless unticked, the linked tasks' assignees (§17.2, FR-011).
- Weekly Coordination's meeting mode gains "Record decision" on each decision row; the change is added to the tray.

## Project Structure

```text
src/Hub.Api/Features/Decisions.cs   (+ Text.cs messages)
tests/Hub.Tests/Api/DecisionsTests.cs
web/src/pages/projects/Decisions.tsx (register, panel, record/defer/cancel/reopen, external parties)
web/src/pages/projects/Coordination.tsx (record in meeting mode), Tasks.tsx (ids filter token)
```

## Checks

AC-DEC-01..06, DEC-05, DEC-07, FR-009 and the permission rows as API tests; register, panel, dialogs and meeting mode
in the browser.

## Complexity Tracking

None.
