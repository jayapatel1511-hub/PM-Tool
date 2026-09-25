# Verification: Notification and Search Enhancements

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (327/327) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 97.4 % branches, services 90.4 % lines |
| `dotnet build -c Release` with security analysers | Pass |
| `npm run build` and `npm run lint` | Pass |
| `python3 tools/trace_spec.py --check` | Pass |

`NotificationSearchTests` (6):

| Test | Covers |
|---|---|
| The weekly summary matches each dashboard and can be turned off | US1-1..2, FR-001, SC-001: for Marc's three new projects the summary's overdue, blocked and overdue-decision counts and computed and reported health equal each dashboard's; on Monday the job queues one email naming each project with "Overdue tasks: 1 · Blocked tasks: 1 · Overdue decisions: 0", a second run sends nothing more, a read-only user who manages nothing gets none; once switched off, the next Monday sends nothing |
| The weekly summary goes on the earliest coordination day | Wednesday and Tuesday → Tuesday; a project without a day → Monday; Sunday and Friday → Friday; a person who manages nothing gets an empty preview |
| A switched-off digest section is left out with its count | US2-1, FR-002: with Project updates on, the subject has "project update" and the body its section; switched off, the subject reads "Hub digest — 1 overdue" and the section is gone; an unknown section code is refused |
| The pulse changes when a notification arrives or is read | US3, FR-003: steady while idle, changes on an assignment and again when all are read |
| Search finds descriptions and live comments within what the person may see | US4-1, FR-004, §18.1: the comment's task, key and snippet; a deleted comment never appears; a deliverable description snippet; a name match carries no snippet; once the project is restricted a non-member finds nothing and a member still does |
| A snippet shows the text around the match with mentions as names | Leading ellipsis, "@Diane Roy" for the mention token, null when absent |

## Manual checks

| Check | Result |
|---|---|
| Search page for "hydro pole relocation": Comments (1) with the task T0003, the phrase marked, and a link to the task panel; quick search for "Hydro One" shows the decision and the comment with the words marked | Pass |
| Bell as Alex with the tab visible: an assignment made through the API appears after 4.1 s (target 5 s); in a hidden tab polling pauses and the count catches up on focus | Pass |
| Preferences as Priya: nine digest sections; unticking "My staff" stores it and ticking it again clears it; "Weekly summary of my projects" with Preview showing "Hub weekly summary — 1 project · 1 Red · 1 overdue", the next submission and the attention items | Pass after the fix below |
| axe (WCAG 2.1 AA) on Preferences, the weekly preview and the search page | Pass |
| Search timing on the scale database (247,080 comments, 74,000 task descriptions): rare phrase 0.05 s, a phrase in every comment 0.33 s, a word in every description 0.59 s (SC-003: under 2 s) | Pass |

## Defects found and fixed while verifying

- The summary said "1 projects"; singular and plural now read correctly.
- The digest contents heading was announced twice; it is now the fieldset's visible legend.

## Decisions

- The spec's summary timing (earliest coordination day, digest time) was built as its stated default for Jay to confirm.
- Changes in the summary count everything done by others on the project since the previous summary, grouped like the
  digest's project updates.
- Synthetic comments and descriptions were added to the local `hub_scale` database only, to time search.
