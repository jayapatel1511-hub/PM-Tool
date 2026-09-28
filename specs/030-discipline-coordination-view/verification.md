# Verification: Discipline Coordination View

**Date**: 2026-09-26
**State**: Partial project Coordination view implemented in the isolated review worktree; full packet acceptance remains unproven.

Documentation validation is recorded in `docs/coordination-spec-validation.md`. The project Coordination tab now has the five fixed question headings, links to existing handoff/change/review registers, a client refresh label and explicit disclosure when a 100-row preview is partial. Handoff and open-review context rows follow the selected discipline; revision use and change cards are project-wide. Startability is explicitly unassessed rather than inferred from an open review. Frontend `npm run build` and `npm run lint` passed after self-review corrections. In the local synthetic browser, signed-in Taylor opened DEMO-101 Weekly Coordination and the five sections, scope note, register links, source-backed empty states and existing weekly sections rendered from the API. No UI mutation was exercised. The deployed browser flow, source count/export reconciliation, cross-project/default discipline scope, saved-view return, meeting actions, and AC-DCV-01 through 05 are **UNPROVEN**.

This is a staged UI increment and does not close packet 030.

## Scope and drill-down increment

The project Coordination view now exposes an owner and handoff date scope in the URL. The owner filter uses the existing project team and source register filters; handoff incoming/outgoing rows are classified by the selected discipline side or selected/current owner rather than by the API's signed-in-only direction filter. Rows open their exact source panel. Counts from truncated handoff/change previews display a `+` marker, and the view discloses the 100-row limit and that dates do not filter changes, reviews or basis uses. The source registers can have broader scope than the preview, especially for handoffs, so count/export reconciliation and saved-view return are still **UNPROVEN**. Local browser showed the owner selection and mapped change/review register URLs; no populated handoff row was available for a browser drill-down. Frontend build and lint passed with pre-existing warnings. This increment still does not satisfy AC-DCV-01 through 05.
