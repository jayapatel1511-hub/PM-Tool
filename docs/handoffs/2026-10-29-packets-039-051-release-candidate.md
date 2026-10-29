# Packets 039–051: local release candidate

**Local follow-up:** [wording and review dispositions](2026-10-29-packets-039-051-local-followup.md) resolve the two wording items and identify a P2 inactive-assignee pickup defect. Runtime code is unchanged; fix and verify that defect before enabling `flag.task_pickup`. Visual acceptance remains open.

**Release SHA:** `2dcdaec424eba81998e0c125f2e29621c8df39b0` on `codex/integrate-039-050`.

This is the last application-code commit. Later commits add verification fixtures, bounded browser condition waits, response-shape evidence, specification wording corrections, status records and this handoff. The application, dependency manifests, migrations, hosting and operational scripts remain unchanged from this SHA. The local image was built from its `git archive`, identified itself with this exact build, and passed upgrade → rollback → forward rehearsal. Nothing was pushed, merged to main or deployed to homedev. No packet is marked Accepted.

## What is in it

Packets 039–042 and 044–051 are implemented locally: delivery controls; typed contracts/registries and server figures; route splitting/live updates; operational hardening; recovery and trustworthy counts; task/timeline interactions; planning/reporting/onboarding; verifiable records; task pickup; organisation structure; and the 051 feedback workflows.

Packet 040 S15.n now covers **all 46 inventory files**. Templates, Tasks, Projects, Planning and Reports finish the remaining conversions. Generated contract/type checks and response-shape checks accompany the conversion. Packet 043 is **KILL confirmed by Jay on 2026-10-25**; its S27–S40 fallback uses the existing components and shared DataTable. The generic record engine is absent.

The merged review fixes cover Admin review visibility/validation/immutability, responsive tables, pickup visibility and withdrawal, POST audit verification, hidden internal-note values in activity, organisation-unit null uniqueness, open-role paging, data-entry recovery and draft handling, Backlog evaluation/order/export/focus, and release/backup/pruning procedures. Unknown-base save recovery requires an explicit conflict choice; automatic recovery never silently adopts a freshly fetched version. Global settings events retry when any project fails, while healthy projects continue.

The final verification pass also restored milestone advance/retarget confirmation, shortened Planner chrome without changing its word budget, and kept asynchronous people-picker placement current. Browser checks wait for actual placement and exact restored focus, retaining their geometry and accessibility assertions.

## Checks

The following preserves the 18-row shape of the 12 October release record. These are Mac-local equivalents, not a hosted CI/provenance claim. SDK **10.0.400**, Node **24.19.0**, local PostgreSQL 17 on port 55432, and the installed Chromium headless shell were used. No OS browser-dependency install, credentials or GitHub commands were needed.

| # | CI step | Local result |
|---|---|---|
| 1 | `dotnet restore Hub.slnx` | PASS |
| 2 | Release build, security analyzers enabled | PASS; 0 errors, 1,048 analyzer/test warnings |
| 3 | Full Release tests with XPlat coverage | **PASS: 1,648 passed, 0 failed, 1 opt-in scale skipped**; 1,649 total, 9 m 13 s. The skipped test passed separately |
| 4 | `tools/coverage_gate.py` | **PASS: rules branches 96.0% (gate 95%); service lines 97.8% (gate 70%)** |
| 5 | Backup/pilot helper, initializer, operations and PITR safety | PASS; part of 28 Python/spec command groups. PITR's Docker replay is opt-in; this gate tests its safety/syntax path |
| 6 | Fixture/credential wrapper unittests | PASS; maintained fixture, transport and wrapper suites |
| 7 | .NET vulnerable packages, including transitive | PASS; no vulnerable packages in all 3 projects, public NuGet source |
| 8 | `npm ci` in a fresh archive of the release | PASS; 178 packages on the Mac; separate anonymous image install also passed |
| 9 | Stamped web production build | PASS; existing bundler notices |
| 10 | Web lint | PASS; warnings, zero errors |
| 11 | Browser availability | PASS; installed headless Chromium used, no OS package changes |
| 12 | Handoffs browser suite | PASS |
| 13 | Coordination browser suite | PASS |
| 14 | Design-basis browser suite | PASS |
| 15 | Planner browser suite | PASS |
| 16 | Resources browser suite | PASS |
| 17 | `npm audit --omit=dev` | PASS; 0 production vulnerabilities |
| 18 | Trace/specification coverage | PASS; 1,343 IDs and 412 sections, zero uncited/unknown; trace/build-spec/decisions/ratchet/text-key checks and all 28 command groups |

Additional release gates:

| Check | Result |
|---|---|
| Self-contained web inventory | **39/39 command groups PASS**, including all requested recovery/Admin/Backlog/schema suites, test typecheck, keys, registrations and runner unit tests |
| Recovery-state regression | **10/10 PASS**, using the actual hook, transport and SaveNote rendering |
| Response shapes after full suite | **PASS: 1,043 shapes; 13 additive paths, zero removed paths/kinds**. Previously observed nullable/empty variants retained |
| Generated contract/types; served assets; chunk report | PASS; no generated drift, served-asset allowlist passed, 100 transitive entry sizes recorded |
| EF migration drift | PASS; no pending model changes |
| Real API smoke wrapper | **9/9 suites PASS**: smoke, two-browser live, saved views, planning/reporting, interaction, pickup, organisation, operations, Backlog |
| Real count census | **1,094 checks, 0 mismatches, 0 missing agreed tiles**; ReviewDemo project/discipline dashboards and milestones included |
| Populated SYN fixture clutter census | PASS; original thresholds retained. Empty Sam screens are reported as unmeasured for first-row height |
| Responsive UI consistency | PASS at **1440, 768, 390 and 320 px**, including 480 px height, keyboard scrolling, helper/error associations and focus restoration |
| Real verifiable records | PASS: AC-VER-02, -07, -09 and -11; tamper detection, commitment history, write-free preview and revision/baseline behavior |
| Real lifecycle | PASS: 31 matching flag-off normalized requests, 4 s pulse, 1.5 s follow-up, silent-stream close/reconnect, hidden-tab close/reopen, bounded outage backoff, connection-limit 429/recovery |
| Full record parity | **PASS: 13/13 results**, 12 persona/width pairs across 27 routes (222 role-appropriate screen comparisons), plus additions off/on; actual browser negative controls PASS |
| Opt-in scale check | **PASS: 1/1**, 500 tasks, 100 concurrent readers, 2,400 measured requests in eight phases; p95 43.2–341.6 ms (target ≤500 ms). Local warmed TestServer/PostgreSQL; excludes browser/hosted network |
| Final diff/working tree | PASS after committing these records: `git diff --check`, no application-source drift, and empty `git status --porcelain` |

A compact machine-readable [evidence record](2026-10-29-packets-039-051-evidence.json) accompanies this handoff. Raw logs, reports and screenshots remain locally under `/private/tmp/pm-tool-release-20261029/` and ignored `ci-artifacts/`; temporary paths are not permanent storage. Historical generated `ci-artifacts/integration` inputs were preserved intact under the raw-evidence folder so the default shape command reads the fresh complete run.

### Failures found and corrected

- The conversion pass exposed compile/query-translation/schema issues, an explicit-null status note omission, and a missing milestone advance dialog. These were corrected and checked before integration records were committed.
- The recovery review reproduced an unknown edit-base overwrite risk and loss of revised text during repeated expiry. Versioned explicit acceptance, retained text, Take theirs behavior and the 10-case regression now cover it.
- The global settings retry previously lost failed project work. Its regression verifies a failed project, healthy sibling progress and later recovery. The full-suite dead-letter test now isolates its own event from unrelated shared-fixture globals while retaining its assertions.
- Count reconciliation originally rejected generated query-name casing. Its matcher now keeps exact filter values while matching names correctly. Record parity similarly normalizes names/order and omits only the old unused root-Project-List `group=none`; negative controls preserve changed values, duplicate-key value order, other group values and other endpoints.
- The populated clutter run found Planner at 47 words against 40. Concise labels preserve planned work, capacity and confidence. The picker and focus checks exposed asynchronous measurement races; exact condition waits replace the premature checks, without enlarging bounds or dropping widths.
- Three dynamic My Work reason keys had relied on incidental fixture state. Real A-16/A-18 scenarios now assert their dates, inactive person and supervisor routing. The final complete suite passed with these cases; all three reason keys remain in the 1,043-shape baseline.
- Two command/setup errors were corrected: EF's `-c` selects a context, so the check uses `--configuration Release`; regenerating the Release cache after the web build removed stale compressed-asset paths. The final build and checks passed after sequencing the builds.

## Upgrade and rollback rehearsal

The release image was built from an exact archive of `2dcdaec424eba81998e0c125f2e29621c8df39b0`. The target was the explicitly disposable `pm-tool-review-db` volume restored from `pm-tool-review-db-mainstate`, containing main's **38 migrations**, 13 users, 3 projects, 6 tasks, 1 comment and 196 activity entries.

| Check | Observed result |
|---|---|
| Health/build | Healthy, exact `2dcdaec424eba81998e0c125f2e29621c8df39b0` |
| Migrations | **38 → 42**: OutboxDeadLetter, TemplateOffsetDayKind, DeliveryWorkflowRecords, AdminReview |
| Existing rows before probe writes | Every baseline table count unchanged except migration history **+4**, org settings **+53**, activity **+53 Setting Created** |
| Immutable tables | `admin_review_no_update`, `admin_review_no_truncate`, `deliverable_baseline_no_update`, `deliverable_baseline_no_truncate` present |
| Unit uniqueness | `NULLS NOT DISTINCT` index present |
| Auth/Host/Origin probes | PASS; three individual test sign-ins, signed-in reads, `/me/work`, `/me/notifications`, comments and sign-out checked. No credentials/cookies printed |
| Jobs | Seven fresh candidate-era job types **Succeeded**; timestamps retained; no failed job runs |
| Outbox | **0 dead letters** |
| Image rollback | `pm-tool-review:main-e4ca1661` runs on the migrated schema and passes probes; exact image tag verified because the old health response has no build field |
| Forward again | Exact release image passes probes again; 42 migrations retained |
| Probe writes | Three intentional test comments, one on each upgrade/rollback/forward probe; initial preservation comparison precedes them |
| Platform difference | arm64 Colima; Mac-only compose override runs the API as root for the copied key directory. The image retains user 1000:1000; Ubuntu UID/permissions remain a hosted check |
| Cleanup | Completed: stack/network stopped; two volumes, five owned image tags, rehearsal kit and fasttrack removed. All 14 named worktrees removed; original branches and detached recovery snapshots retained. Unrelated resources preserved |

The optional verifiable activity log was not activated on this review stack. Its irreversible activation remains Jay's separate choice.

## Assumptions

Packet defaults and slice-specific assumptions remain in each packet's `verification.md`. The local follow-up confirms that Admin review exports reconstruct rule text from current settings, the page exposes the latest review's export while the API accepts a recorded ID, historical pickup report rows stay with the original assignee's team, and the audit-chain already-alerted marker is retained after a cleared break. It separately identifies the live inactive-assignee pickup/assignment defect; historical report retention is not proof of live eligibility. No rates, client facts or operational data were invented; browser/rehearsal data is synthetic.

Local checks and image rollback do not prove human acceptance, hosted/tunnel behavior, real mail/directory integration, company pilot, scheduled off-host retrieval or recovery drills. The Mac-held-token exposure rule was observed; no credential-bearing remote operations were performed.

## Known limits and remaining gates

- **P2 open:** assignee deactivation blocks active teammates’ pickup and the manager pickup-assignment path. Runtime code is unchanged by the local review. Fix and verify before enabling `flag.task_pickup`; the recorded passing suites do not cover this scenario.
- Hosted activation, real tenant/provider behavior, tunnel/browser/operator checks, assistive-technology sessions and human/pilot acceptance remain open. No packet or whole specification is Accepted.
- Repository controls/Tier B review, packet 039 S18, packet 040 S16 cohort measurement/Jay's ruling, and flag retirement after activation/pilot remain open.
- The required production dependency scan is clean. The additional full npm scan reports **one high development-tool advisory for Vite**; it is recorded separately and no dependency upgrade was folded into this release.
- The exhaustive UI comparison captures controls/rows/requests/permissions/accessibility, not Jay's visual approval. The styling differences below remain for his eye.
- The rehearsal proves a local main-to-final image switch on PostgreSQL 17. It does not prove homedev permissions, installed timers, off-host backups or hosted acceptance.

## Activation steps

These are operator instructions from [the review runbook](../runbooks/homedev-review.md), **not actions performed in this session**. Jay owns host authorization and sudo. The CI-image timer stops at a contract migration until it is manually applied; do not bypass that stop or reuse exposed credentials.

1. After review/authorization, prepare the committed release from a trusted Mac checkout:
   ```bash
   scripts/prepare-homedev-review.sh 2dcdaec424eba81998e0c125f2e29621c8df39b0
   ```
2. In Jay's homedev terminal:
   ```bash
   cd /home/jaypatel04/Workspace/Projects/pm-tool/releases/2dcdaec424eba81998e0c125f2e29621c8df39b0
   bash scripts/activate-homedev-review.sh 2dcdaec424eba81998e0c125f2e29621c8df39b0 </dev/null
   ```
   The helper builds, takes the pre-upgrade dump, applies migrations on the existing volume, checks private probes and moves `current` only after success. Add `--install-timer` only when intentionally installing/proving the daily backup timer. The separate CI-image timer installation is an explicit later operator action.
3. Compare the runbook's pre-upgrade query/row counts, migration history and post-upgrade state; verify the four immutable triggers, null-equality index, jobs and dead-letter count. Retain the previous image and verified dump.
4. Run the private probe and tunnel probe, then approved review fixtures if needed:
   ```bash
   python3 scripts/verify-homedev-review.py
   python3 scripts/verify-homedev-review.py https://pm.engcalchub.com
   python3 scripts/seed-homedev-tuesday-review.py 2dcdaec424eba81998e0c125f2e29621c8df39b0
   ```
   Staffing fixtures remain opt-in (`--with-staffing-fixtures`) and change review calendar settings. Feature flags stay off until their audience and pilot are approved. Verifiable-log initialization follows the separate irreversible procedure in the runbook.
5. Record hosted/operator/human evidence separately, including real backup retrieval/restore, rather than promoting these local results to acceptance.

## Rollback

Keep the migrated schema when switching images. The rehearsed main image ran on the 42-migration schema. Prefer disabling a reversible feature flag where applicable, then use the activation helper's printed previous-image rollback command and re-run probes. Verifiable-log activation cannot be disabled this way. Never delete the live volume or run a down migration as image rollback. If data restoration is required, stop writes and restore a verified pre-upgrade dump into a **new isolated database**, validate there, then make a controlled cutover.

The local rehearsal switched to main and back; actual operator rollback must use the host's real previous release/digest and preserve its dump, keys, runtime configuration and original database.

## Commits and preservation

Integration includes the five final conversion merges and their per-slice verification records; `cdcaa65` merges the release fixes, `63893c81` preserves aliased images during pruning, and `b4b7bba` fixes settings retry. `ba005266` records packet status, `bab887e7` isolates the dead-letter test/shortens Planner chrome, and `2dcdaec4` is the final application code. Subsequent commits contain verification fixtures/waits and release evidence only.

All original named branches are retained. Detached interim/replay history is reachable as `codex/archive-interim-20261029` and `codex/archive-fasttrack-test-20261029`; dirty helper patches and the merge helper were preserved locally before cleanup. The uncommitted working files in `/Users/jaypatel/PM-Tool` were never edited.

## For Jay — current dispositions

The [local follow-up](2026-10-29-packets-039-051-local-followup.md) reviews all seven original items. The original wording is preserved in the preceding release-record commit `86c00a8c`; the current dispositions are:

1. **Resolved:** FR-BKL-08 now names the existing Task List export only; no new report.
2. **Resolved:** AC-VER-09 now uses the app's existing “over capacity” label.
3. **Agent visual review complete; Jay's acceptance open.** The three retained normal-state screenshots are readable, but header/padding/alignment/loading differences remain. Parity PASS does not prove CSS preservation. No styling changed in this follow-up.
4. **Reviewed; behavior retained.** Evaluator-relevant settings and `flag.backlog` queue reevaluation of Active projects asynchronously; Manual-order export follows board order. With `data_entry_safety` off, failed text survives blur/retry but may be lost on panel exit/unmount. Stale retries can conflict after the same actor's intervening edit. These replace the overbroad “any flag / at once” and “leaving a field” descriptions.
5. **Reviewed; display redaction retained.** Internal-note values are hidden in activity for all readers; the field's change remains visible. Stored audit data is not erased.
6. **Reviewed with one P2 gap.** Admin review exports use current settings for rule text beside recorded counts/notes (audit-fidelity limitation); only the page's latest review has an export button, while the API accepts a recorded ID. Historical pickup rows retain the original assignee's team, but live pickup/manager assignment is blocked after assignee deactivation. **Fix before enabling `task_pickup`.** Audit alerts intentionally retain the already-alerted marker after a cleared break.
7. **Activation remains separate.** Jay's authorized operator session is required on homedev; optional verifiable-log activation is irreversible. No host actions were taken.

Repository controls/Tier B, 039 S18, 040 S16, hosted/operator/human/pilot evidence, visual acceptance and later flag cleanup remain open. Local review completion does not grant acceptance or feature activation.
