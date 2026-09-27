# Implementation Plan: Milestones and Deliverables

**Branch**: `003-milestones-and-deliverables` | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)

## Summary

Milestones with types, original date and slip, date changes with reason and notification, completion
with confirmation and phase suggestion, cancellation with retargeting, reopen; the deliverables register
with the Appendix C lifecycle and guards, review, issue with confirmation for open tasks, derived
defaults, bulk actions, and detail panels. Derived status, progress and indicators come from packet 005.

## Technical Context

As packet 001. Tables `milestone`, `deliverable`, `deliverable_issue` exist in the initial schema.

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I | Submission readiness only | Pass |
| II | No derived values computed here; status "why" comes from `milestone_state` (packet 005) | Pass |
| III | Every deliverable has exactly one owner (DL-01), defaulting to the discipline lead | Pass |
| IV | Milestone types, deliverable statuses verbatim (§10.2, §12.3) | Pass |
| V | Date changes, status changes, issue, retarget and cascade shifts logged per item with one correlation ID (M-04, G-10) | Pass |
| VI | `Permissions.ManageMilestones`, `CreateDeliverable`, `EditDeliverable`, `DeliverableTransition`, `DeleteDeliverable` | Pass |
| VII | No new infrastructure | Pass |

## Design notes

- Structural steps in `Workflow.DeliverableStep`; guard messages from `Workflow.DeliverableGuard` (§13.6).
- Issue writes the deliverable's latest issue fields and appends a `deliverable_issue` row, so packet 013's
  history needs no migration.
- Revision Required stores the mandatory comment as a Review comment (C-07) directly in `comment`.
- The milestone cascade (M-04, packet 010) is part of `change-date` with `dryRun` preview; `includeTasks`
  (packet 018) shifts the targeted deliverables' tasks too.
- Milestones keep a nullable `date` so template and copied milestones can be undated (§12.14 step 2);
  manual creation and edits require it (M-01).

## Project Structure

```text
src/Hub.Api/Features/Milestones.cs, Deliverables.cs
tests/Hub.Tests/Api/MilestonesDeliverablesTests.cs
web/src/pages/projects/Milestones.tsx, Deliverables.tsx, web/src/components/hub/panels/*
```

## Checks

AC-MS-04..06, AC-PERM-01/02, AC-DEL-01, AC-DEL-03..05, M-05, M-09, DL-03, DL-09, DL-10, E-24 as API tests.

## Complexity Tracking

None.
