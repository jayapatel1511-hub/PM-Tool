# Hosted synthetic acceptance checkpoint — 2026-10-03

Both isolated homedev runtimes use executable `ac494a8ba70d50517f540ad7e0acced85d84897e`.
Public review is served at `https://pm.engcalchub.com` through the existing tunnel to loopback `3080`;
the separate 30-person/10-project synthetic pilot remains private on loopback `3081`.
The source location fix merged in [PR #31](https://github.com/jayapatel1511-hub/PM-Tool/pull/31).
The subsequent documentation-only checkpoint merged in
[PR #32](https://github.com/jayapatel1511-hub/PM-Tool/pull/32) at
`c8fe1f6be791d8d747e8f0a2cef35fadd29b2821`; its candidate and
[merged-main CI](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37150309474) passed.
These acceptance checks changed only labelled synthetic records and did not change application code,
credentials, project membership, base capacity or calendars. No new activation was required.

## Observed checks on the public review

The bounded API checks used fresh individual sessions through public HTTPS. Credentials stayed on homedev;
the browser checks used the existing signed-in review account in temporary tabs, preserving the user's task tab.
New review/change records were confined to `SYNTH-GATES-1003`
(`01a1032a-ced6-7870-b921-b2bb1cb20388`). They are fictional acceptance fixtures, not company work.

| Criterion | Result | Observed behavior and limit |
|---|---|---|
| AC-CAP-02 daily availability/privacy slice | PASS | Browser saving 4 hours on 2026-11-03 changed weekly availability from 40 to 36 hours and survived full reload. Another Supervisor's detail read returned 404 and attempted edit returned 403 without changing the row. Partial-scope spare capacity remained explicitly unknown. Normal effective capacity was restored and fresh HTTPS reads verified 40 hours/week. Two new neutral override rows remain for traceability; existing overrides were untouched. |
| AC-CHG-01/02/03/05 three-consumer slice | PASS | Exactly three linked tasks received Pending Assessment. Acknowledgement did not settle the assessment and closure was refused. One task retained A with independent Taylor approval; one adopted B; one adopted B and completed a correction independently verified by Jay, who was neither its creator nor owner. Closure was refused before correction completion and again before verification. Old source A, adoption histories and task due dates remained unchanged. Final notice Closed and all three dispositions were visible after a signed-in browser reload. |
| AC-CHG-04 publication/head/use slice | PASS | Two distinct publication calls started together on separate HTTPS connections from the same saved head snapshot returned one 200 and one 409 `concurrency_conflict`. The loser remained the unchanged Draft with no assessments; the winner held exactly the expected A-linked assessment and source head. Stale adoption returned 409 `source_changed`, leaving full notice details, source snapshots, uses and history unchanged. The fixture ended with the winning notice Closed and the losing Draft Cancelled with a reason. Exact overlapping execution inside the server, multi-instance/stress behavior and approval replacement under this race remain UNPROVEN. |
| AC-MRV-05 fresh-round/removal slice | PASS | A new source A review received independent Civil and Electrical approvals. Publishing B reset the package to Draft with a new Pending round. Removing a required discipline without a reason or impact was refused without changing package, rounds, assignments or manifest. A justified removal created a third Draft round with one Pending Civil assignment, while original approvals and the immutable A manifest remained intact. The browser showed current source B/Pending and the historical A approvals; full reload returned the current round. |
| AC-RDY-04 and chair/performer slice | PASS | Taylor's five new proposals stayed Proposed until Jay confirmed them. The Tuesday week beginning 2026-09-29 was frozen with exactly five commitments. A stale withdrawal was refused without adding history; a fresh-version withdrawal changed one promise to Withdrawn while preserving all five promise IDs/content and the immutable denominator of five. The other four stayed Committed; task due dates, original dates, progress and completion status did not change. The signed-in browser showed the original count of five and one later withdrawal after full reload. The actual CSV response retained all five exact outputs, criteria, dates, readiness and states. This does not prove native download or complete AC-RDY-05 permission/notification acceptance. |
| AC-BAS-04 unresolved-conflict/current-use slice | PASS | Two independently confirmed fictional narrative assumptions in the same Civil scope retained their own confirmed version, one explicit unresolved conflict and one exact-version consumer use each. The A use created before B confirmation remained unchanged; the Electrical consumer explicitly linked B. Both consumers' derived Basis checks were applicable and unsatisfied, with whole assessments Needs Assessment because other inputs remained unknown. Task dates and completion did not change. The signed-in register and inspector showed both conflicting values, the conflict count and current original use; full reload preserved both entries and counts. This is not evidence for replacement/adoption, template copying, complete readiness or company engineering acceptance. |

## Evidence and boundaries

Credential-free results are owner-only files under the review runtime's `data/` directory:

- `hosted-availability-ac494a8b.json` (restoration checked at 20:04 UTC)
- `hosted-three-consumer-change-ac494a8b.json` (20:21 UTC)
- `hosted-review-round-ac494a8b.json` (20:28 UTC)
- `hosted-publication-race-ac494a8b.json` (20:30 UTC)
- `hosted-weekly-five-ac494a8b.json` (20:47 UTC), plus `hosted-weekly-export-ac494a8b.json` (21:00 UTC)
- `hosted-basis-conflict-ac494a8b.json` (20:52 UTC)

The run journals retain safe synthetic mutation identifiers and reject blind repeats. Working passwords,
cookies, database dumps, runtime configuration and protection keys are excluded from Git and these result files.
Earlier location/API/CSV/XLSX and preservation checks remain scoped to their recorded source and timestamps.
The weekly helper initially used a version from before snapshot capture. The server correctly refused that stale
command; the targeted retry used the journal's existing five promises and a freshly read version. It created no
duplicate tasks, promises or snapshot. This was an acceptance-helper correction, not an application fix.

Native print remains **UNPROVEN**: desktop automation could not control the print dialog and the user cancelled it;
no current PDF was saved. Actual export HTTP responses were checked, but the native browser download event was
not confirmed. First automatic backup executions remain unproven until the scheduled timers run and their fresh
dumps are verified. The prepared encrypted pilot logical recovery transfer is awaiting its specific approval;
off-host WAL replay, full application recovery and accepted 15-minute RPO/8-hour RTO remain unproven.

These are bounded synthetic acceptance observations. They do not close all packet scenarios, manual accessibility,
real company hosting/privacy approval, the 50-user/8-week pilot acceptance process or production deployment.
Review, pilot and production preparation remain on homedev with individual passwords; no Azure or new paid
service was introduced.
