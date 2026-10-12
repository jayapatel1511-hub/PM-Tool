# Verification: Supervisor Capacity Signals

**2026-10-12 verdict: PASS for local implementation and deployment-candidate checks. Hosted and human acceptance remain UNPROVEN.**

Source: branch `codex/deployment-ready`, based on integrated Tuesday `68c56f28`, with uncommitted completion changes. No merge, push, deployment or operational data change occurred in this pass. Defaults D1–D7 were used under Jay's build request. Jay explicitly approved Partial view when hidden Confirmed allocations overlap the displayed weeks.

## Implemented and checked

| Contract | Evidence |
|---|---|
| AC-SUP-01–04: capacity and scope | My Staff reuses the scoped planner batch hook and accessible figure. `test:supervisor-capacity` checks 12 h free/8 h away, partial/out-of-scope suppression, 120 people in three batches of at most 50, current ISO week including Sunday/year boundary, denied capacity access, single signal with week labels, phone cards and tablet widths. Zero axe violations, page errors or unmocked API calls. |
| AC-SUP-05: visibility correction and regressions | PostgreSQL tests cover hidden allocations and entries, excluded hidden records/hours, displayed-week Partial view versus lookahead-only suppression of Under-planned. The final 1,132-test Release suite includes packet 034/036 regressions, Workload, My Week and readiness. Exports have no capacity-column change. |
| AC-DIG-01–06: hold and return | `SupervisorCapacityTests` exercise full-day leave/override/holiday hold, Reduced and nonworking days, default/opt-out, five away days and weekend, exact return-window update under existing caps, immediate notices and watchdog consistency. Existing-user migration default and concurrent first-use preference/job writes are covered. |
| Preference UI | Mocked browser PUT persistence plus real container sign-in and preference toggle. The database readback changed `hold_digest_when_away` from true to false. |
| Actual supervisor browser | The final built container, local-password sign-in as synthetic Supervisor Sam, returned three direct reports through the real API and rendered 40 h free on desktop and phone. The initial employee Alex Staff screenshots were access-denied evidence and are explicitly excluded from this PASS; Alex's separate Preferences test remains valid. |
| Review fixes | `DeploymentConformanceTests` cover audited-type fields, comment activity updates, link update/delete conflicts, workspace scope-only version changes and new child insertion, Staff version response, hidden-work privacy and Following counts beyond 1,000 events. Mocked browser tests cover personal mark-read without navigation and Following pagination/correlation grouping. |

The focused regression selection passed **33/33** after the final preference-race fix. The full final Release suite passed **1,132/1,132**, zero failures or skips. Coverage: **96.7% rules-engine branches** (gate 95%) and **94.6% service lines** (gate 70%).

## Release checks

- Release build, web production build and lint passed. Existing analyzer/lint and bundle-size warnings remain; this is not a zero-warning claim.
- All six required mocked Chromium workflow suites passed: handoffs, coordination, design basis, planner, resources and supervisor capacity. Planner reports 20 flows, seven recovery checks and 13 axe checkpoints.
- 61 backup/pilot/fixture/credential safety regressions passed. Maintained PG17 logical backup, physical backup and PITR rehearsal passed on disposable local containers, preserving source rows and pausing replay at the target (`before=1`, `after=0`).
- Online production npm audit found zero vulnerabilities; NuGet reported no vulnerable packages across the three projects. These are package advisories at run time, not a complete security assessment.
- EF reported no model drift. Three additive migrations add the preference and four row-version columns; the final image applied all 36 migrations to a fresh synthetic PostgreSQL database.
- Image `pm-tool-review:d9b3-final`, ID `sha256:516ce26e8c61450d77dc9202ede8a058006bd96213030c97d1abe1b6b147775f`, passed private authentication/Host/Origin probes, signed-in project reads and all seven background jobs with no duplicate-key/job-failure matches. Container runs as uid 1000 with read-only root filesystem, all capabilities dropped and no-new-privileges.
- Packet 036's full-scale paired API gate passed against `9beace03`: workload p95 48.15→49.49 ms (1.028×), 12-week planning grid 47.12→49.86 ms (1.058×), 30-person picker 14.79 ms (<300 ms). Same-machine, equivalent synthetic fixture: 600 projects, 123,540 tasks, approximately 2 million activity rows. The subsequent preference-only race fix touches neither measured read path.
- Canonical specification generation, traceability and whitespace checks are recorded in the final handoff.

Evidence is retained under `/Users/jaypatel/.codex/visualizations/2026/10/12/01a130cd-3330-7303-acd0-54ab9a511d94/deployment-ready/`. See [the deployment handoff](../../docs/handoffs/2026-10-12-deployment-ready.md) for exact commands, artifact identity, external gates and limits.

## Acceptance limits

The candidate is uncommitted and has no new remote CI or hosted deployment evidence. Fresh-container checks do not prove migration of the current hosted database. An earlier image rollback smoke retained the upgraded schema; it was not repeated after the preference-only fix. Hosted upgrade/rollback, TLS and exact asset identity must be verified during an authorized activation at `pm.engcalchub.com`.

Actual company mail/directory integration, full cross-browser and manual screen-reader checks, packet-wide live scenarios, SC-001's real-supervisor session, Jay's visual acceptance and company approval remain open. The disposable local PITR rehearsal does not establish off-host retrieval or the production RPO/RTO. Local-password sign-out clears the browser cookie; an already copied cookie remains usable until its eight-hour expiry or verifier rotation/removal, as documented in the threat model. Production acceptance must address that existing residual risk.
