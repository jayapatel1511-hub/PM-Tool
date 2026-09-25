# Verification: Deliverable Dependencies, Lag, and Working Days

Date: 2026-09-25.

## Automated checks

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` | Pass (335/335) |
| `python3 tools/coverage_gate.py` | Pass: rules engine 97.5 % branches, services 90.5 % lines |
| `dotnet build -c Release` with security analysers | Pass |
| `npm run build` and `npm run lint` | Pass |
| `python3 tools/trace_spec.py --check` | Pass |

`DependencyLagTests` (domain, 6):

| Test | Covers |
|---|---|
| A deliverable waits through the lag after its predecessor is issued | US2-1, FR-002, FR-003: issued 2027-02-01 with a 10-day lag — on 02-05 the successor waits ("lag 10 days"); once its start date arrives inside the lag it is Blocked; on 02-11 nothing holds it |
| A deliverable that holds up another is Blocking Others | US1-2, FR-002: an unissued predecessor blocks a started successor and leaves a future one waiting; Blocking count 1; cancelled, it holds up nothing |
| Date checks add the lag to the predecessor due date | US2-2, D-12: a deliverable starting 10-05 before its predecessor's 10-01 + 10 days is inconsistent; a task lag of 2 after Friday 10-09 ends 10-11 in calendar days and 10-14 in working days with the Monday holiday |
| A task lag counts working days when they are on | FR-003, FR-005: after a Friday completion, a 2-day lag has passed by Tuesday in calendar days but not in working days |
| Blocked and stale days do not grow over a weekend and a holiday | SC-001, FR-005: blocked since Friday is 4 calendar days (A-02 at 3) but 1 working day (no A-02); last activity Friday is stale at 2 calendar days but not in working days |
| Workload spreads over the office's working days | FR-005, §12.15: 40 hours over Mon–Fri with a Monday holiday stay 40 hours over four days; 10 hours from Friday to Tuesday split 5 and 5, not over three days |

`PermissionMatrixTests`: deliverable links for the PM and the lead of either end; not a lead of neither, a team member,
or a read-only user.

`DependencyLagApiTests` (2):

| Test | Covers |
|---|---|
| Leads link deliverables directly with a lag and loops are refused | US1-1..3, FR-001..FR-003, D-03: a team member is refused; the Civil lead links the 85 % package to the geotechnical report with 10 days and a note; a duplicate is refused; the reverse link is refused as a loop naming both keys; one end only; another project's deliverable refused; both details show the link apart from derived ones; after evaluation the started package is Blocked by the report, which is Blocking 1; removal clears it; both changes logged |
| An office holiday counts once working days are on | US3-1..2, FR-004, FR-005: only an Admin adds a holiday; a duplicate is refused; the list by office and year; with working days on and due-soon at 1, a task due Tuesday is due soon on Friday because Monday is the office's holiday, and not once the holiday is removed |

## Manual checks (browser pane)

| Check | Result |
|---|---|
| Administration → Holiday calendars as Jordan: "Thanksgiving" 2026-10-12 added to Head Office and listed | Pass |
| 2026-0501-D017 (85 % Civil Drawing Package) → Dependencies → Linked deliverables → Depends on → Add: D022 with 10 days and "Client review of the geotechnical report"; D017 lists it with "lag 10d" and the note, and D022 lists D017 under Blocks; the reverse is not offered | Pass |
| Task T0003 → Dependencies → Add: the dialog has "Lag (days)" with its hint | Pass |
| axe (WCAG 2.1 AA) on Holiday calendars, the linked-deliverables tab and dialog, and the task dependency dialog | Pass |

## Defects found and fixed while verifying

- Deliverable rows did not carry the new blocking count, so the chip could not show; it is now in each row's state.

## Decisions

- Lag counts working days when the organisation counts thresholds in working days, and calendar days otherwise.
- Holiday changes take effect at each project's next evaluation (its next change, or overnight); no immediate
  re-evaluation of every project in the office.
- Every project has an office, so "projects without an office use the organisation calendar" does not arise; the
  organisation-wide holidays apply to all offices.
