# Submission label activation — 2026-10-03

Both separate homedev runtimes now run executable `29b3d7036f529b513fdc2ad47cff62a0b75f0b59`.
Public synthetic review remains at `https://pm.engcalchub.com` on loopback `3080`;
the synthetic pilot remains private on loopback `3081`. No public route, account,
password, protection key, database volume or infrastructure was replaced.

## Code, merge and CI

[PR #34](https://github.com/jayapatel1511-hub/PM-Tool/pull/34) merged at
`fbb6f34efc55308e4a8decddb7db05ad6800ea5d`. Candidate and merge have the same tree,
`e6af2405cef3b3dff3fdc814250478df17fda139`.
[Candidate CI](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37158060280)
and [merged-main CI](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37159018378)
passed. Both CI runs passed 858 tests with zero failures/skips. Candidate CI also ran coverage gates,
private backup/pilot safety checks, frontend checks, three mocked browser suites
and specification tracing. The one-line UI change renders Unassigned for a null
blocker owner ID; a named ID that cannot be resolved retains the unavailable label.
API, readiness calculation, permissions, migrations and records did not change.

## Activation and preservation

The operator log is
`pm-tool-pilot/data/submission-label-activation-29b3d703-20261003T224023920296108Z.log`.
It records review private probes 10/10, review public probes 9/9 and pilot private
probes 10/10, each with RESULT PASS. Activation took a pre-release dump and reused
the existing volumes. Fresh pilot reads found 30 active fictional users and all
10 Setup projects. Hash comparisons preserved both runtime configurations,
all individual password verifiers and all previously present protection keys.

The first scheduled pilot dump, `hub-pilot-20261003T221503657086Z.dump`, restored
successfully into an isolated drill database with 10 projects at 22:43 UTC. The
maintained drill removed only that temporary database; the original dump remained.
This is an on-host logical restore, not off-host WAL replay or full application DR.

The wrapper stopped after these successful gates because its final sign-out check
used `/auth/sign-out` instead of the maintained `/auth/local/sign-out` route.
It did not print its final combined PASS marker. This helper defect did not undo
activation or the restore. A separate read-only completion check at 22:51 UTC
confirmed both exact current pointers, both timers active and waiting, and:

- Both original review tasks' progress/versions, the second task's dates and the original persistence marker.
- The edited SUB002 package's title, purpose, recipient, date, coordinator and milestone; Checking and manifest version 2.
- Both exported manifest versions and all 14 checks.
- All six owned promises: four Committed, one Withdrawn and one Met; frozen counts 5 and 1.
- The explicit unresolved basis conflict and both current explicit uses.
- Fresh sign-out on the correct route and an anonymous `/me` denial after discarding the cleared cookie.

Credential-free follow-up evidence is `pm-tool/data/hosted-preservation-29b3d703.json`.
The transport was corrected to honour the cleared cookie; it does not establish
rejection of a replayed old cookie. The review timer is next due October 4 at
22:00 UTC and the pilot timer at 22:15 UTC. The earlier
[scheduled recovery checkpoint](2026-10-03-scheduled-recovery-ac494a8b.md) records
both successful first automatic runs and approved encrypted review recovery.

## Browser and remaining gates

**UNPROVEN:** fresh signed-in browser verification of the deployed Unassigned label
and saved submission after full reload. The previous session expired; sign-in is
pending. The API preservation result does not replace this browser gate.

Native print remains unproven because the dialog was cancelled. The separate
sensitive pilot recovery transfer is awaiting its specific approval; no pilot dump,
runtime configuration, verifier, key, physical base or WAL was transferred to the Mac.
Automatic off-host recovery, retrieved pilot WAL replay, full application recovery,
accepted RPO/RTO, remaining full-packet/manual accessibility acceptance, company
hosting/privacy approval, real participant pilot go/no-go and production deployment
remain unproven. The private 30-person/10-project fixture is a synthetic rehearsal.
No Azure deployment or new paid service was introduced.
