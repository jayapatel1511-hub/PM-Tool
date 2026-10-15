# Tuesday local deployment candidate — 2026-10-12

**Local candidate: PASS. Hosted/company acceptance: UNPROVEN.** The remaining packet 038 implementation and source review fixes are complete and checked locally. This pass did not commit, push, merge, deploy, alter hosted configuration or mutate operational records.

## Candidate identity

- Checkout: `/Users/jaypatel/.codex/worktrees/d9b3/PM-Tool`.
- Branch: `codex/deployment-ready`; integrated baseline: `68c56f28`. Completion changes remain uncommitted.
- Built artifact: `pm-tool-review:d9b3-final`.
- Image ID: `sha256:516ce26e8c61450d77dc9202ede8a058006bd96213030c97d1abe1b6b147775f`.
- Designated application hostname: `pm.engcalchub.com`. This is the project decision, not proof of current DNS, TLS or hosting state.
- Retained local evidence: `/Users/jaypatel/.codex/visualizations/2026/10/12/01a130cd-3330-7303-acd0-54ab9a511d94/deployment-ready/`. `candidate-source-manifest.json` records SHA-256 hashes of the current source/config inputs and image identity, excluding generated build outputs and private runtime files.

The initial spec-only checkout was clean. Jay approved using the latest local Tuesday baseline; this isolated checkout was placed on `codex/deployment-ready` at `68c56f28`. Other worktrees were preserved. Parallel helpers had bounded ownership; the main agent integrated and verified the final source.

## What changed

My Staff now shows current-week capacity using the owner picker's scoped 50-person batch reads, including an accessible Partial view label and one signal per row. Jay's confirmed policy marks hidden Confirmed allocations partial consistently in Planner, My Staff and the picker; hidden records and hours remain excluded. Hidden work only in the extended lookahead suppresses Under-planned without marking the visible week partial.

Daily digests are held on working days with zero available capacity, with a default-on personal preference. Reduced days send; the weekend rule stays first. A held day does not move the last-sent marker or queue a deferred message, so the first digest after return covers the gap under existing caps. Watchdog logic shares the hold and avoids false late alerts when all sections are off. Capacity reads select no HR reason, type, balance or external reference.

The independent source review found and verified fixes for missing audit field allowlists, comment activity timestamps, optimistic concurrency across memberships/disciplines/workspaces/editable links and every caller, scope-only workspace version changes, new workspace-project insertion, the Staff response version name, Following pagination/grouping/counts and per-notification Mark read. A container startup exposed simultaneous first-use preference insertion by daily and weekly jobs; atomic creation now preserves both job markers and personal settings. No new dependency or service was introduced.

## Database and rollback

Three additive migrations follow the baseline's 33 migrations:

| Migration | Addition |
|---|---|
| `20261012155414_SupervisorCapacitySignals` | `user_setting.hold_digest_when_away`, default true for existing and new preferences |
| `20261012160119_ProjectMemberWorkspaceDocumentLinkConcurrency` | Row versions for project memberships, workspaces and editable document links |
| `20261012160644_ProjectDisciplineConcurrency` | Row version for project disciplines |

Fresh migration, preference backfill/defaults, version conflicts and EF model drift checks passed. Historical migration tests retain their original target and down/up checks. An earlier local image rollback smoke started an older image on the upgraded schema and passed basic health/authentication probes; that is bounded evidence, and was not repeated after the preference-only fix. For activation, take and verify a fresh backup first, retain the upgraded schema on image rollback, then rerun authenticated reads, jobs and browser flows. Do not execute down migrations as an image rollback shortcut.

## Executed verification

| Check | Final result | Retained evidence |
|---|---|---|
| `dotnet test Hub.slnx --no-build -c Release --collect:'XPlat Code Coverage' --results-directory /private/tmp/tuesday-final-coverage` | 1,132 passed, 0 failed/skipped | `release-tests.log` |
| `python3 tools/coverage_gate.py /private/tmp/tuesday-final-coverage` | Rules branches 96.7% / services lines 94.6%; both gates passed | `coverage-gate.log` |
| Focused supervisor/deployment regressions | 33/33 passed after final race fix | `focused-tests.log` |
| Release build, web build and lint | Passed, with existing analyzer/lint/bundle warnings | `release-build.log`, `web-build.log`, `web-lint.log` |
| Required mocked Chromium suites | Handoffs, coordination, design basis, planner, resources, supervisor capacity passed | `*-ui.log`; maintained tests under `web/tests/` |
| Supervisor suite | Capacity arithmetic display, privacy/access, batch limits, ISO week, signals, 320/390 phones, 768/1024 tablets, preferences, notification read and Following paging/grouping; zero page/axe/API surprises | `supervisor-capacity-ui.log` |
| Safety regressions | 61 passed | `safety-tests.log` |
| `python3 tools/test_pilot_pitr.py --docker` | Disposable PG17 logical/physical backup and target-paused WAL replay passed; source rows preserved; cleanup confirmed | `pitr.log` |
| Package advisories | Online npm production audit: 0 vulnerabilities; NuGet: no vulnerable packages in any project | `npm-audit.log`, `nuget-audit.log` |
| EF model drift | No pending changes | `model-check.log` |
| Container publish/startup | Production SPA and .NET publish, 36 fresh migrations, uid 1000/read-only/all-capabilities-dropped/no-new-privileges | `image-smoke-original.txt`, corrected browser evidence below |
| Private authentication | Health 200, anonymous 401, invalid Host 400, unsafe Origin 403, wrong password 401, sign-in 204, signed-in me/projects 200, sign-out 204, current browser cookie then 401 | `image-smoke-original.txt` |
| Background jobs | All seven succeeded: daily-digest, weekly-summary, directory-sync, email-send, evaluation-retry, nightly-evaluation, ops-watchdog; no duplicate-key/job-failure matches | `jobs-before-browser.txt`, `jobs-after-browser.txt` |
| Actual signed-in browser | Supervisor Sam saw three actual direct-report rows with 40 h free; desktop and phone. Employee Alex changed hold true→false and database readback was false | `staff-browser.json`, `staff-desktop.png`, `staff-phone.png`, `preferences-*.png`, `preferences-readback.txt` |
| Full-scale packet 036 API performance | Workload p95 ratio 1.028×; planning 1.058× (both ≤1.10); picker 14.79 ms (<300) | `performance.md`, `performance-samples.json` |
| Specification and diff | Canonical generated output current, traceability complete, no whitespace errors | `spec-check.log`, `trace-check.log`, final `git diff --check` |

Browser evidence correction: the initial image-smoke Staff screenshots used employee Alex and showed the expected access denial. Their original broad Staff PASS statement is superseded by the separate Supervisor Sam run, which asserted actual member rows and capacity content using real API responses. The denied screenshots are excluded from successful Staff evidence. Alex's Preferences toggle remains valid.

Local tests used macOS arm64, .NET SDK 10.0.400, Node 26.8.1 and real PostgreSQL 17. This is local verification, not a new GitHub Actions run (CI uses Node 24) or production browser acceptance. Sandbox .NET/browser/network failures were rerun successfully in the normal local environment. Tests and benchmarks used synthetic, disposable or uniquely named test databases; operational data was not substituted with fixtures.

The scale comparison used clean `9beace03` and the candidate read paths on equivalent fixtures: 600 projects, 123,540 tasks, 2,000,154 activity rows before candidate migration, 480 scale people, five warmups and 60 interleaved samples per endpoint. The subsequent atomic preference fix did not modify the measured reads. The 768/1024 checks establish the changed Staff/preferences surfaces only; they do not sign off every packet 036 tablet scenario.

## Gates before company activation

1. Review and explicitly authorize the concrete commit/release payload. Commit and run remote CI; rebuild/tag the exact release rather than treating the uncommitted image tag as a deployment identity.
2. Follow [environments](../runbooks/environments.md) and [homedev review](../runbooks/homedev-review.md), verifying current target state, fresh backup, persisted keys and exact image. Activate only with explicit authorization; verify the current populated database migration and retained-schema rollback.
3. Verify `pm.engcalchub.com` DNS, HTTPS, proxy Host/Origin protections, anonymous refusal, real account roles/project restrictions, restart persistence and browser-loaded asset identity. Recheck the changed Staff, planner, preferences and notifications through hosted sign-in.
4. Complete actual company mail/directory integration and delivery, remaining packet-wide live scenarios, non-Chromium/manual screen-reader checks, SC-001's real-supervisor session and Jay's visual acceptance. Existing packet verification records remain the authority for their unclosed scenarios.
5. Establish encrypted off-host backup retrieval and full application recovery against the required RPO/RTO. The disposable local PITR pass proves the maintained helper path; it does not close operational recovery acceptance.

The current local-password provider is supported in the reviewed Staging runtime and refuses the runtime name Production. A company production cutover requires the separate authentication/hosting acceptance described in the environment runbook. Sign-out clears the browser cookie; a copied stateless cookie remains valid until eight-hour expiry or verifier rotation/removal. This existing residual risk is recorded in the [threat model](../security/threat-model.md) and must be addressed in company acceptance.

Temporary verification stacks and credential files were removed; the final review image and bounded, secret-free evidence are retained. No passwords, cookie values, verifiers or operational database dumps are included in this handoff.

Final independent read-only review: **PASS, no outstanding actionable findings** across the source, migrations and tests for capacity/privacy, digest holds, audit history, concurrency and notifications. The reviewer inspected execution logs and the corrected desktop/phone screenshots rather than rerunning passing suites. This verdict covers the local candidate and preserves the external gates above.
