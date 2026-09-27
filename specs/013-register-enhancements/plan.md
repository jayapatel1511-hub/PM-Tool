# Implementation Plan: Register Enhancements

**Branch**: `013-register-enhancements` | **Date**: 2026-09-25 | **Spec**: [spec.md](spec.md)

## Summary

The open decisions the client (or one chosen external party) owns as a clean CSV or Excel file for the client call; one
action that links a decision to many tasks, deliverables or milestones and reports what it skipped; a project decision
log read from the activity history; and every issue of a deliverable listed in its panel.

## Technical Context

As packet 001. No migration: `deliverable_issue` rows are already written on each issue (packet 003), and the decision
log reads the append-only activity log, where every decide, defer, cancel and reopen is recorded with its text or reason
(packet 008). New: `Features/DecisionExtras.cs`; `DecisionEndpoints.Open`, `Facts`, `Link` shared with it. Web:
`pages/projects/Decisions.tsx` (Register / Decision log switch, "Export for the client" dialog, multi-select link picker),
`pages/projects/Deliverables.tsx` (Issues tab).

Endpoints: `POST /decisions/{id}/links/bulk`, `GET /projects/{id}/decision-log`,
`GET /projects/{id}/decisions/client-export?partyId&format`; the deliverable detail already returns `issues`.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Filtering, sorting and counting only | Pass |
| II | The export, the log and the register read the same decisions and the same history | Pass |
| III | Decision ownership unchanged; each link keeps its decision's owner | Pass |
| IV | Only open decisions (Pending, Under Review, Deferred) are exported; statuses unchanged | Pass |
| V | Each bulk link is logged as "Linked" in the same save; exports are logged as "Exported" | Pass |
| VI | Bulk linking needs `EditRegisterItem` on the decision and the edit permission of each target (tasks `EditTask`, deliverables `EditDeliverable`, milestones `ManageMilestones`); refused targets are reported, not linked; the log and export need project read access | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- The client export defaults to every party marked as the client; a chosen party narrows it. Columns: key, subject, what
  must be decided, required by, days until or overdue, impact (level and description), status, owner. Party notes,
  comments and the decision's internal fields are never read. Overdue rows come first, then by required-by date.
- No open decisions for the party is a 422 with a sentence naming the party, shown in the dialog, instead of an empty file.
- Bulk linking checks every target, links the allowed ones in one save and returns `linked` and `skipped` with a reason
  per skipped item (other project, already linked, or the permission's own explanation); at most 500 ids per call. The
  picker groups a mixed selection by item type, one call per type.
- The log is built from activity entries ("Decided", "Deferred", "Reopened", and status changes to Cancelled) so a
  decision deferred twice and then decided shows three entries; it carries the decision text or the reason, the decision
  date or the new required-by date, who recorded it and the current owner.

## Complexity Tracking

None.
