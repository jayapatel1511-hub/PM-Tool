# Local build verification — 2026-10-09

The agreed vacation, weekly allocation and personal calendar subscription scope is implemented locally. This is uncommitted work; no merge, deployment or human acceptance is implied.

## Source and environment

- Attached application worktree: `/Users/jaypatel/.codex/worktrees/finish-planning/PM-Tool`, based on `bbac1f6d`. Existing planner review fixes were preserved and the approved v1.2 specification was reconciled with the maintained application specification, including Following and assignment requirements.
- The original `a02e/PM-Tool` specification checkout and `tuesday-visual-batch1` application checkout retain their existing work. Build edits live in the attached worktree.
- .NET SDK 10.0.400, Node 24, PostgreSQL 17 in an isolated temporary container. API tests use disposable databases; the local preview uses only synthetic Development identities and data. No operational database or records were used.
- Preview: `http://127.0.0.1:5087/vacation?view=list`. The temporary preview database listens only on loopback port 55432 and uses tmpfs storage; it disappears when its container is stopped.
- Maintained files were compared byte-for-byte against the verified build replica. The SHA256 manifest is `/private/tmp/tuesday-source-manifest.json`; generated assets and dependencies are excluded.

## Results

| Check | Verdict | Evidence and limit |
| --- | --- | --- |
| Release suite and coverage gates | PASS | 1,052 passed, zero failed/skipped; 3m56s. Rules-engine branch coverage 96.8% (95% gate); service line coverage 94.6% (70% gate). Final run: `/private/tmp/tuesday-release-tests.log`, `/private/tmp/tuesday-release-coverage/47872b9c-e6d9-492c-bcba-dd246a1676d7/coverage.cobertura.xml`. |
| API builds | PASS | Debug API and Release solution builds succeeded. Final Debug log: `/private/tmp/tuesday-final-debug-build.log`; Release build log: `/private/tmp/tuesday-release-build.log`. |
| Additive migration/model | PASS | Fresh test/preview databases apply the migration. EF reports no pending model changes: `/private/tmp/tuesday-model-check.log`. An upgrade rehearsal on a copy of an operational database was not run. |
| Web build and lint | PASS | `/private/tmp/tuesday-final-web-build.log`, `/private/tmp/tuesday-final-lint.log`. Existing lint warnings and Vite config/bundle size advisories remain. |
| Planner UI regression | PASS | 20 flows; 13 accessibility checkpoints with zero axe violations, zero page errors and no unknown GET requests. Covers leave forms/import, day/year/phone views, selected copy/publish, stale reload and calendar subscription controls. API responses mocked: `/private/tmp/tuesday-final-browser-planner.log`. |
| Existing coordination UI checks | PASS | Handoff, coordination, design-basis and resource browser scripts passed. These use mocked API responses; logs are `/private/tmp/tuesday-browser-{handoffs,coordination,design-basis,resources}.log`. |
| Real API and browser workflow | PASS | The final local API/browser smoke creates synthetic weekly work, copies the selected displayed week, publishes it, verifies `Possible` confidence is retained, loads Projects directly and shows saved approved leave. Zero axe violations and page errors: `/private/tmp/tuesday-live-browser.log`. Native in-app browser also shows the saved leave record. |
| Operations/fixture regressions | PASS | Backup helper, pilot initializer and pilot operation checks passed; fixture safety suite passed 34 tests. PITR checked shell syntax only; PostgreSQL replay was not run. Logs: `/private/tmp/tuesday-python-operations.log`, `/private/tmp/tuesday-python-fixtures.log`. |
| Dependency advisories | PASS | npm audit reports zero vulnerabilities after updating the inherited `source-map-js` leaf from 1.2.1 to 1.2.2. No dependencies added. NuGet reports no vulnerable packages against current sources. Logs: `/private/tmp/tuesday-npm-audit.log`, `/private/tmp/tuesday-nuget-audit.log`. This is registry advisory checking, not a penetration test. |
| Specification/generated output | PASS | `build_spec.py --check` reports up to date; `trace_spec.py --check`: 749 IDs and 255 sections, zero uncited or unknown references. |
| Final source/whitespace | PASS | All 764 maintained source files match the build replica; `git diff --check` passes. No changes staged or committed. |

## Behavior verified by API/domain tests

Import identity mapping, permissions, expiring authenticated preview receipts, tamper/stale refusal, duplicate handling, atomic apply, explicit cancellation, unchanged reimport, source freshness, manual reconciliation and audit versions are covered. Capacity checks include office holiday/leave overlap, effective weekend schedules, partial leave with extra hours, provisional legacy absence, planner/workload consistency and reservation invalidation.

Weekly commands cover selection, ownership, private drafts, preserved confidence, atomic stale refusal and one summary per person. Calendar checks cover account/project permission scope, token hashing and rotation/revocation, stable event identity, sanitized cancellation, all-day boundaries and UTF-8 folding. Weekly hours are not converted into timed calendar events.

## Repeatable checks

Run these from the attached application worktree, with an isolated PostgreSQL test service on loopback port 55432:

```sh
dotnet test Hub.slnx --no-restore -c Release --collect:'XPlat Code Coverage' --results-directory coverage
python3 tools/coverage_gate.py coverage
python3 tools/build_spec.py --check
python3 tools/trace_spec.py --check
git diff --check
```

From `web/`, run `npm run build`, `npm run lint` and the existing `test:planner`, `test:resources`, `test:handoffs`, `test:coordination`, `test:design-basis` scripts. For the real synthetic preview only:

```sh
TUESDAY_LIVE_BASE_URL=http://127.0.0.1:5087 node tests/planning-live.cjs
```

The live script requires an explicitly supplied loopback URL and Development authentication. It creates visibly synthetic planning records in the disposable preview database. Screenshot: `web/test-results/planning-live/approved-leave.png`.

## Remaining acceptance

Actual HR export mapping, organizational permissions and Outlook subscription refresh/update/cancellation need real-system acceptance. HTTPS, designated host `pm.engcalchub.com` and proxy query redaction are deployment prerequisites; the hostname decision does not prove live DNS or hosting. Revocation stops future feed fetches and cannot erase client caches.

Manager-owned weekly entries retain packet 034 permissions: administrators and supervisors for direct reports. PMs use the existing project reservation workflow. The manager planner is desktop/tablet; phone My Week is read-only. HR approval/balances, live HR APIs, inbound free/busy and two-way calendar sync are deferred as agreed. Production release and human acceptance remain UNPROVEN.
