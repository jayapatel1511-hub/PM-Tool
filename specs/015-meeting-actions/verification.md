# Verification: Meeting Actions

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (321/321) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 97.4 % branches, services 90.2 % lines |
| `dotnet build -c Release` with security analysers | Pass |
| `npm run build` and `npm run lint` | Pass |
| `python3 tools/trace_spec.py --check` | Pass |

Domain (`RegistersTests`): action overdue days (Open and In Progress only, not on the due date); a converted action
follows its task — Complete → Complete, Cancelled → Cancelled, In Progress, Not Started, In Review and On Hold → In Progress.

`MeetingActionsTests` (4):

| Test | Covers |
|---|---|
| A meeting records actions for a person, a discipline and the client | US1-1..2, FR-001, FR-002: a team member cannot record a meeting; a script link is refused; A01…A03 with three owner types listed under the meeting by due date with owner names; two owners refused; read-only refused; the owner moves their own action to Complete; cancelling needs a reason; the discipline's action is moved by its lead, not a team member |
| Meeting mode captures actions against today's coordination meeting | US2-1, FR-003, MTG-04: "Weekly Coordination — 2026-09-14" recorded once and shared with the lead; a team member cannot start it; two actions linked to the blocked task listed under it |
| Actions reach the owner, the discipline lead or the PM, and nothing goes outside | US3-1..2, FR-004, FR-005, MTG-01, MTG-02: the Civil action is in Marc's My Work with an ActionAssigned notice; the client's action has no notification at all and is in "Waiting on client / external"; once overdue it is an attention item routed to Marc; a discipline without a lead warns and puts the action in the PM's My Work flagged "no lead" |
| A converted action becomes a task and follows its completion | US4-1, FR-006, FR-008, MTG-03, SC-003: the task takes the action's text, owner, Civil and due date, and names the action; the action is In Progress with no manual moves (422) and cannot convert twice (409); task In Progress → action In Progress, Complete → Complete, reopened → In Progress; logged "Converted"; the report lists the open action and filters by owner type |

## Manual checks (browser pane, as Priya on 2026-0417)

| Check | Result |
|---|---|
| Meetings & Actions → Record meeting: "Client Progress Meeting #4", Client, minutes link, calendar event "Client call" chosen from the project's events near the date | Pass |
| Actions for Civil (overdue 2 days, showing its lead Marc Dubois), the client Karen Li (marked external) and Alex (added through the form's person picker) list by due date under the meeting | Pass |
| Weekly Coordination: "Waiting on client / external" lists A02; in meeting mode the action button on task T0003 opens the form linked to the task with its assignee as owner and "Today's coordination meeting"; A04 is added, the tray shows it, and the meeting "Weekly Coordination — 2026-09-25" now exists | Pass |
| Marc's My Work lists the Civil action A01 | Pass |
| Convert A03: with no discipline to derive the dialog asks for one; choosing Civil creates T0009 assigned to Alex and opens it | Pass after the fix below |
| axe (WCAG 2.1 AA) on the Meetings tab, meeting form, action form, action panel, Weekly Coordination in meeting mode and My Work | Pass |

## Defects found and fixed while verifying

- Converting an action whose owner had no primary discipline on the project failed with "Required."; the server now
  also tries the discipline the owner leads and the related task's, and the dialog pre-selects the suggestion or asks.

## Decisions

- Actions reuse the existing overdue attention item (routed to the owner, the lead, or the PM when there is no lead) rather
  than a new rule id; §12.12 lists no separate action rule.
- A deliverable raised "from here" is linked to the action through the generic link table; tasks and decisions use the
  action's related-task and related-decision fields.
