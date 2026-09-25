# Verification: Decision Register

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (229/229) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 96.8 % branches, services 84.0 % lines |
| `npm run build` and `npx oxlint src` (web) | Pass, no lint errors |

`DecisionsTests` (4, all pass):

| Test | Covers |
|---|---|
| External owner, register columns, filters and sort | AC-DEC-01 (owner shown with organisation and client flag; no email to the external address), FR-DEC-04 (overdue first, then required-by; days until and days overdue), FR-010 (owner type internal/external/client, impact, required-by range, overdue indicator), §17.2 (internal owner notified and added to the team), FR-009 (team member creates a party, cannot edit it; the PM edits it) |
| Overdue decision blocks linked tasks and raises Critical | AC-DEC-02 (A-04 Critical routed to the requester and the PM; dashboard overdue decisions = 1), AC-DEC-03 (both tasks Blocked with the decision as blocker; Blocked count 2; `blocking=true` returns the two task ids), a non-owner team member cannot record |
| Defer, record, reopen and cancel follow Workflow 11 | AC-DEC-04 (defer needs a later date and a reason; tasks return to Waiting; A-04 clears; old and new dates and the reason in History), AC-DEC-06 (Decided without text refused), AC-DEC-05 (owner records; decided-by stored; assignees and requester notified; tasks released), DEC-07 (owner refused, PM needs a reason, text cleared, "Reopened" logged, re-blocks once overdue), Workflow 11 exception (cancel needs a reason; tasks released with the decision-cancelled note), Cancelled is terminal |
| High impact due soon, owners, links and edit rights | DEC-05 (A-04 Warning), DEC-01 (two owners or none refused), Read Only refused, requester edits and switches the owner to an external party, a lead who neither owns nor requested it is refused, deliverables cannot be "blocked by", duplicate link refused, cross-project link refused, unlink |

## Manual checks (browser pane, project 2026-0417)

| Check | Result |
|---|---|
| Decisions tab: register with the overdue client decision first, external owner with building icon and organisation, days to/overdue, impact, blocking count, linked keys | Pass |
| Panel: header chips, "Holding up n tasks" link, step buttons, inline fields, owner switch, linked items, history | Pass (keys no longer wrap; people pickers use the compact inline style) |
| Record decision dialog with text, date and "Notify linked task assignees", on by default | Pass |
| Raise decision: owner as external party created inline without submitting the outer form; linking a task; the new decision opens in the panel | Pass |
| Blocking count opens the task list with a "Specific tasks: 1" token | Pass |
| Weekly Coordination meeting mode: "Record decision" on each row; the change appears in the tray and under Recently completed | Pass |
| External parties dialog lists parties with client tag; Edit for the PM | Pass |

## Defects found and fixed while verifying

- A decision recorded a moment earlier still showed "Overdue" and "Holding up 2 tasks" until the background
  evaluation finished. The register and panel now mask derived flags for closed decisions.
- Counts read "1 tasks" (register blocking link, panel, task list subtitle); singular forms added.

## Decisions

- A requester may edit their decision (the "edit own" row of §8.5.2 read as the person who asked for it); owners and
  the PM also edit.
- Editing the required-by date of an open decision is allowed and logged; deferral remains the step that carries a
  reason (DEC-03). Closed decisions keep their date.
- Removing a member does not reassign their open decisions (packet 003's reassignment covers tasks and deliverables);
  the decisions stay with the person, who remains an active user.

## Deferred (cross-packet rule)

- Decision history view, bulk linking and a client-facing export of open decisions are Phase 2 (packet 013).
- The decision lines in the daily digest already come from packet 006.
