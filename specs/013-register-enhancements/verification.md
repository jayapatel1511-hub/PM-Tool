# Verification: Register Enhancements

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (296/296) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 97.2 % branches, services 91.1 % lines |
| `dotnet build -c Release` with security analysers | Pass |
| `npm run build` and `npm run lint` | Pass |
| `python3 tools/trace_spec.py --check` | Pass |

`RegisterEnhancementsTests` (4):

| Test | Covers |
|---|---|
| The client export lists only their open decisions, overdue first, and nothing internal | US1-1, FR-001: of six decisions, the three open ones owned by the client party are listed, "Lane closures" (4 days overdue) first, then "In 6 days", then the rest; impact reads "High: Drawings wait"; the utility's, an internal owner's and a decided decision are left out; the party's internal note is absent; `partyId` picks the utility's decision; a project with none returns 422 "… nothing to export" |
| One action links a decision to many tasks and reports what it skipped | US2-1, FR-002, SC-002: Marc links 12 Civil tasks, an Electrical task and another project's task; 12 linked and logged, 2 skipped; after evaluation the decision blocks 12 tasks; repeating 2 skips both as already linked; Alex is refused (403) |
| The decision log shows each outcome newest first with text or reason | US3-1, FR-003: a deferral (reason, new date), a decision (text, date, owner, recorded by Priya Nair) and a cancellation (reason) listed Cancelled, Decided, Deferred |
| Every issue of a deliverable is kept and the latest shows on it | US4-1, FR-004, SC-003: issued Rev A, returned for revision, issued Rev B; the history lists Rev B then Rev A with dates, recipient, transmittal link, note and issuer; the deliverable shows Rev B |

## Manual checks (browser pane)

| Check | Result |
|---|---|
| Decisions → Decision log on 2026-0417: the deferral of DEC04 (reason, "Now needed by 2026-10-16"), then DEC02 and DEC01 decided with their text, dates and owners, newest first | Pass |
| Export for the client: "The client (Karen Li)" by default and every party listed; CSV and Excel list DEC05 "4 days overdue" before DEC06 "In 11 days", with impact and status, no notes; Ravi Singh shows "No open decisions are owned by Ravi Singh, so there is nothing to export." and no file | Pass |
| Decision panel as Priya → Link items: options tick with a check mark, "Link 3 items" for two tasks and a deliverable in one action; the tasks show "Blocked by it", the deliverable "Related" | Pass |
| Deliverable 2026-0501-D002 issued Rev A (18 Sep, note) and Rev B (25 Sep): the Issues tab lists both, newest first, with recipient, transmittal link and issuer | Pass |
| axe (WCAG 2.1 AA) on the decision log, the link picker, the client export dialog and the Issues tab | Pass after the fix below |

## Defects found and fixed while verifying

- The link picker lost a selection when two options were chosen before React re-rendered; selection now uses functional updates.
- The existing "Issued" line's transmittal link relied on colour alone inside text (axe `link-in-text-block`); it is now underlined.

## Decisions

- The spec's details were marked "confirm with `/speckit-clarify`"; the defaults written in the spec were built as stated.
  Jay can adjust them: who may export (built: anyone who can read the project, as with the register's own export), the
  export's columns, and whether reopenings belong in the log (built: yes, with their reason).
- Status in the client export uses the register's own words (Pending, Under Review, Deferred).
