# Verification: Risk and Issue Registers

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (310/310) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 97.3 % branches, services 90.6 % lines |
| `dotnet build -c Release` with security analysers | Pass |
| `npm run build` and `npm run lint` | Pass |
| `python3 tools/trace_spec.py --check` | Pass |

`RegistersTests` (domain, 10 cases): every grid score and its band (P3 × I2 = 6 High, 2 × 2 = 4 Medium, 1 × 2 = 2 Low),
each score in exactly one band; review overdue for Open and Monitoring risks only, not on the review date itself; issue
overdue for Open and In Progress only; High before Medium before Low.

`RegistersApiTests` (4):

| Test | Covers |
|---|---|
| Risks are scored, banded and flagged when their review is overdue | US1-1..3, FR-001, FR-004, RSK-01, RSK-02: keys R01…; sort 9, 6, 4, 1; the P3 × I2 risk is High and "Review overdue" by 4 days; a closed High risk is never overdue and leaves the dashboard (High risks 1) and Weekly Coordination; severity and indicator filters; a level of 4 refused; a read-only user refused; editing impact re-bands to Medium |
| A High open issue is a Critical attention item and turns health Red until resolved | US2-1..3, FR-005..FR-007, ISS-01..03, A-07: I01 High fires A-07 Critical routed to its owner and the PM; health Red; dashboard 2 open, 1 High; sort High then Low; overdue by 2 days; resolving without text refused with a `resolution` field error; a read-only user refused; resolved on 2026-09-14; A-07 clears and health leaves Red; reopening needs a reason and clears the resolved date |
| A realised risk needs its issue and keeps the link on both | US3-1, FR-003, RSK-03, SC-003: Realised without an issue refused and the risk unchanged; realised with a new issue, which records its origin risk while the risk records the issue; Realised offers no further steps; logged "Realised"; an existing issue can be linked, but not one that already records another risk; no realised risk without an issue |
| An issue raised from a task is linked and reported with the High risks | US4-1..2, FR-008..FR-010: the task link; the report lists the High issue, the High risk "High (6)" and the Medium issue, not the Medium risk; `severity=Medium` lists the Medium issue and risk; report and register exports; unlinking refused for a read-only user and allowed for the raiser |

## Manual checks (browser pane, as Priya on 2026-0417)

| Check | Result |
|---|---|
| Risks tab → Raise risk: choosing Likely (3) and Moderate (2) shows "Severity: High · 6"; each level shows its anchor (Impact 3: "Would move a submission milestone or require rework of an issued deliverable"); R01 raised with review date 2026-09-20 shows "Review overdue" in the table and panel | Pass |
| Mark realised → Create a new issue: title, High severity and owner carried over; I01 created, the panel shows "Realised from 2026-0417-R01", the risk shows "Realised as I01" | Pass |
| Dashboard: "Issues and risks" Open 1, High 1, High risks 0, each linking to the filtered register; the A-07 Critical item lists I01; Weekly Coordination section 10 lists I01 and opens its panel | Pass |
| Task T0003 panel → Raise → Raise issue: dialog says "Linked to 2026-0417-T0003 Drainage calculations" with Civil pre-selected; I02 created linked to the task | Pass |
| Resolve I02: the button stays disabled until a resolution is written; resolved on 2026-09-25 with the text shown in the panel | Pass |
| Reports → Open Issues / High Risks for 2026-0417: I01 listed; the key links to the Issues tab panel | Pass |
| Activity: "Realised" on R01 and "Created" on I01 show the linked item's key and title | Pass after the fix below |
| axe (WCAG 2.1 AA) on both registers, both panels, both raise dialogs, the dashboard and Weekly Coordination | Pass |

## Defects found and fixed while verifying

- Activity showed the origin risk and realised issue as raw ids; risks and issues are now named in activity.
- Report keys for risks and issues linked to the dashboard; they now open the register tab and panel.
- The probability and impact radios' labels nested their text too deep for the label check; the labels were flattened.

## Decisions

- Probability and impact anchors are plain text in the interface strings (Unlikely / Possible / Likely; Minor /
  Moderate / Major); the impact High anchor is the specification's own wording.
- A project with no open work stays Grey even with a High issue (§16.3 "Nothing to evaluate"); A-07 still fires.
- No notification event was added for a new risk or issue owner (§17.2 lists none); High issues reach the owner and PM
  through A-07.
