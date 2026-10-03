# Completion audit and release checkpoint — 2026-10-03

This record supersedes any claim that `26c0ae1` completes packets 025–033. That candidate passed its bounded CI/image checks and PR #24 merged, but subsequent source/specification review found required behavior still missing. Its activation is held. The live synthetic review release is now `211bd88649e288484901aba6946fdd0e2e29dd17`, activated on 2026-10-03 at 14:06:40 UTC. The 06:55 checkpoint below is historical; the hosted checkpoint records subsequent evidence. A newer implementation commit is not evidence of deployment or company acceptance.

## Current synthetic pilot checkpoint — 2026-10-03 18:24 UTC

The current private pilot rehearsal uses release `3bfea2328aae1421300f81979ceebc7d5be6b6b1`. PR #30 merged at `d0c2ab7179eba47014e821f13b5fbd19bcaeb06d`, with candidate and merged trees equal. Candidate CI `37139948986` and merged-main CI `37140933823` passed 858 tests. This is a synthetic pilot result, not a code bug-fix claim, public review upgrade, real-company acceptance, or production deployment.

The latest operator log is `data/synthetic-pilot-fixture-30-20261003T181646351856362.log`. It records 30 fictional users, 10 Setup projects, all 30 individual sign-ins and assigned-project checks, preservation of the existing six passwords, local logical restore of 10 projects, and native pilot PITR replay paused with `before=1` and `after=0`, passed at 18:24 UTC. The roster is Admin 1, PM 3, CIV 4, STR 2, GEO 4, MAT 4, MEC 4, ELE 4 and ENV 4. Categories are MunicipalInfrastructure (existing), Road/Drainage, Water/Wastewater, CommercialBuilding, IndustrialRetrofit, LandDevelopment, Geotechnical, MaterialTesting, EnvironmentalRemediation and EnergySite.

The private pilot remains on `3081`; public review release `211bd88649e288484901aba6946fdd0e2e29dd17` remains on `3080`. The pilot backup timer is active for 22:15 UTC on October 3, but `LastTriggerUSec` is empty, so the first automatic run is **UNPROVEN**. The latest bounded result at 18:24:29 UTC reports WAL archived 8 and failed 0. Bounded off-host WAL transfer/retrieval/replay, full-application DR, and accepted RPO/RTO remain **UNPROVEN**. Credentials remain outside Git. The earlier encrypted snapshot `2582b812` is historical review evidence only: 27 migrations, 3 projects and 3 users.

Real company participants, the 8-week / 50-user human-acceptance process, privacy and hosting approvals, and sponsor go/no-go remain external gates. Azure and new paid services are not prerequisites for the stated homedev direction.

## Completed implementation slices

| Required behavior | Implemented evidence | Verification scope |
|---|---|---|
| Local-password sign-in access audit | `294d23b`: valid server sign-in persists one event before cookie issuance; local browser callback cannot duplicate it | 13 focused LocalAccounts checks; wrong-password and inactive refusal included |
| Pending coordination digest and exception assignment | `783e544`: submissions, allocations, basis impacts, constraints, promises, issue verifications and exception-verifier work reuse existing access and preferences | 24 focused digest/readiness/collaboration checks at that checkpoint |
| Independent issue verification after owner replacement | `2054286`, `d3e1f5b`: edit, appointment and resolution recheck current independence with the explicit self-review setting | 15 issue checks; legacy inconsistent owner assignment cannot resolve |
| Submission live-basis guard and adoption recovery | `1f128a7`, `fd037c9`: current basis/impact/conflict evidence participates in readiness and issue fingerprint | 19 combined issue/submission checks at that checkpoint |
| Readiness source records | `e0a700e`: actual IDs, keys and statuses link to contributing records; allocation visibility uses existing permission query | 24 focused readiness checks and one private-allocation visibility regression; frontend build |
| Manual issue-reference provenance | `7a76414`: source system, available stable ID, registrant/time and manual-registration label | 36 focused issue/change/concurrency checks; additive migration 28; frontend build |
| Allocation inactive-person recovery | `94eb9ea`, `e72bccb`: confirmation refuses ineligible people; authorised edit returns to Proposed; lists/detail/export surface the warning and history remains | 10 allocation checks on the final list increment; frontend build/lint |
| Submission Fail and actual A-to-B issue history | `c279137`, `ef4859f`: required failures block, optional failures remain recorded without gating, manual source labels and immutable snapshots retained | Included in the 52 combined issue/submission/allocation/change checks below |
| Unavailable issue-reference recovery | `86b133d`: authorised versioned retry-safe replacement preserves original reference and invalidates earlier verification | 52 combined checks; frontend build/lint; Release EF reports no pending model changes; additive migration 29 |
| One notification per command and recipient/item | `6ced0d7`: current-context and persisted notification/email deduplication | 10 collaboration checks; regression first failed on duplicate rows |
| Owner replacement recovery | `f75a742`, `4e6bf11`, `6153cb1`, `71d426e`: new owner re-records with a reason and current assessment version; active owned work follows the replacement while historical approvals and frozen promises remain | 9 focused recovery/DCV/action checks; aggregate version guard and replay cases |
| Coordination context and full print | `169d9ab`, `7ba46e0`, `71d426e`: evaluated blockers, source links, promised/needed dates, bounded rows with exact totals and complete capture context; Print fetches all permitted rows | Included in 9 focused checks; frontend build/lint; all three required Chromium mocked suites passed locally; paged fifth row and repeated Print refresh included |
| Provisional basis confirmation dates | `26ed8b2`, `f3e9860`, `ffdfd92`: new proposals require dates; historical/template unknown dates remain explicit until authorised editing | 14 combined basis/submission checks after correcting the old fixture; template-copy regression |
| Source invalidation notices | `59052d5`, `d086ba3`: basis, review, publication and handoff changes route submission/check-owner notices transactionally | 77 focused workflow/collaboration checks; publication-notice regression first failed on missing notices; retry preserves counts |

The combined local suite at `26ed8b2` ran 857 checks: 856 passed and one old submission fixture failed because it omitted the newly required provisional date. `ffdfd92` corrected that fixture; the affected 14 basis/submission checks then passed. Coverage from the combined run met the gates (96.7% rules branches, 94.0% service lines). Final candidate and merged-main CI subsequently passed 858/858; see the final checkpoint below. Backup/pilot helper regressions passed 14/14; traceability covered 612 IDs and 236 sections with no gaps.

Raw evidence remains under `/private/tmp/pm-*`; credentials, dumps and runtime files are not committed. Counts describe the named runs and are not a new combined full-suite claim.

## Historical review code and image checkpoint — 2026-10-03 06:55 UTC

Reviewed candidate **`211bd88649e288484901aba6946fdd0e2e29dd17`** merged through
[PR #25](https://github.com/jayapatel1511-hub/PM-Tool/pull/25) as
**`328e88a43cb769a77db1cffc889fd6794f9b9902`**. Both trees equal
`4a84fce40aa43fdb2d6ab8651218e6e0143da089`. The two independent reviewers reported no
further confirmed findings in their bounded changed paths after correction. This is not a claim that every
packet acceptance scenario has been performed by a company user.

- [Candidate CI](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37103065653) and
  [merged-main CI](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37103869911) both PASS:
  858 tests, zero failures/skips, required coverage gates, three mocked Chromium suites, 14 backup/pilot helper
  regressions, dependency/build/lint checks and complete traceability. Candidate coverage was 96.7% rules branches
  and 94.2% service lines. Existing build/lint warnings remain.
- The exact archive image passed an isolated local Colima smoke test with a fresh, seed-free pilot configuration:
  29 migrations, one bootstrap Admin, no fictional users/projects, Host/Origin/anonymous/wrong-password denial,
  individual sign-in, HTTP 307 to HTTPS, container hardening, and cookie/key persistence after API recreation.
- A dump from the approved encrypted review recovery snapshot was restored only into an isolated test database.
  Migration 27 to 29 and new-image → old-live-02ca7cd-executable → new-image startup preserved the hashes of
  23 existing business tables. This proves compatibility for that restored synthetic snapshot, not rollback after
  new business writes. Do not downgrade the schema or overwrite current data with an old dump.
- The exact candidate is staged in both review and separate pilot release directories. Review runtime/data links
  are retained. Pilot runtime is empty; no company accounts, database or service were provisioned.

## Remaining hosted and company acceptance at 06:55 UTC (superseded)

- Operator activation of **211bd88**, private/public probes and final browser checks remain pending. Homedev still
  selects `02ca7cd`; the held `26c0ae1` must not be activated. The prepared hosted read checks refuse to run until
  `current` selects the final candidate.
- The pre-activation public task read at 06:33 UTC retained the original persistence marker, progress 70% and
  row version 1. Compare against current records after activation; preserve any deliberate concurrent changes.
- Final hosted paging/action context, native print, keyboard and narrow-screen checks remain required. Mocked UI
  tests and image health do not close packet T006 or prove company acceptance.

## Historical hosted synthetic checkpoint — 2026-10-03 15:25 UTC

The operator activated executable **211bd88** at **14:06:40 UTC**. The exact image, retained
`pm-tool-review-db` volume, private probes **10/10** and public probes **9/9** passed. The fresh pre-activation dump
is private `data/backups/hub-review-20261003T140623986900866Z.dump`; the previous image remains available.
The original T0001 comment marker, progress **70%** and row version **1**, and original T0002 dates/status/version
were preserved in public API comparisons after activation and hosted fixture creation. The marker is a comment,
not the task description. No original task, staffing, calendar or permission records were edited.

All new hosted fixture records use `HOSTED-211bd88-20261003` labels. These are observed API/browser slices,
not a blanket PASS for all 45 acceptance criteria or company pilot acceptance:

| Packet | Observed hosted PASS | Remaining limits |
|---|---|---|
| 025 | Undated submit refused atomically; date mismatch exposed; source/proxy/recipient authority enforced; two independent receipts; accepted is distinct from incorporated; corrected B incorporated while head A; later publication reuses B and creates only the pending A-consumer assessment | Full restricted-viewer acceptance remains unproven |
| 026–028 | Independent reviewer closes a finding after resolver response; self-review/reassignment denied; B supersedes round A; unissued stale A returns to Checking; issue B supersedes issued A while the complete immutable A record/export stays equal; stale issue fingerprint returns 409 without issue creation | Full access, checklist waiver, impact/adoption and browser matrices remain unproven |
| 029 | New Production/Review reservations yield committed delta **19 h** (`max(12,8)+3+max(4,3)`); unknown estimate stays null; overload without a reason is refused without mutation; cancellation removes reservations but retains **11 h** forecast and unchanged task records; date-only edit withdraws confirmation, preserves old provenance and requires reconfirmation; stale person/date confirmation returns 409 unchanged, then refreshed confirmation passes | Availability overrides, simultaneous competition, full native UI and restricted-project matrix remain unproven |
| 030 | Exact three-task drill-down; PM scope labels and saved private view; repeated meeting capture reuses one action with four links; Mark as reviewed changes review metadata without changing work/sign-offs; refreshed view reflects incorporated input | Native print/PDF and full export parity remain unproven |
| 031–032 | Numeric basis without units refused; independent confirmation; two old-version consumers retained with two pending impacts after B; bounded assumption permission; independently verified constraint removal; performer signature and chair snapshot; withdrawal stays in frozen denominator | Actual expiry, five-item denominator and full conflict/adoption/browser matrices remain unproven |
| 033 | Two disciplines see one issue/location ID; reverse station range and coordinate without CRS refused unchanged; independent evidence required before resolution; publishing B preserves closed issue and its A-document reference with pending impact | All location forms, multi-location grouping/export and product reference-kind decision remain unproven |

Signed-in native browser checks passed for task dates at **1440, 390 and 320 px**: associated native inputs,
32 px clear controls inside the viewport, Escape closes the picker/panel and restores task-title focus. No task values
were changed. Taylor performed Accept/Incorporate and the coordination drill-down/saved view/action reuse in the
browser. The submission register and B detail render the actual Superseded/Checking/Issued states and B manifest.
Print fetches the complete context, but the native print preview could not be inspected with the available app
controls; no print PASS is claimed.

Private evidence: `data/hosted-acceptance-journal-211bd88.jsonl`, `hosted-original-records-211bd88.json`,
`hosted-handoffs-211bd88.json`, `hosted-coordination-browser-211bd88.json`,
`hosted-reviews-submissions-issues-211bd88.json`, `hosted-basis-readiness-211bd88.json`,
`hosted-weekly-readiness-211bd88.json`, and `hosted-capacity-211bd88.json`. Partial harness runs are retained;
use their journal and final successful evidence rather than treating corrected harness projection errors as app defects.
These reports contain synthetic records only. Working credentials/session cookies, runtime files and dumps stay private.

Jay requested synthetic users/projects instead of supplying real company participants. A **separate synthetic pilot
rehearsal** was prepared at the pilot base, with its own Admin, private password/keys and port **3081**, no review seed
or copied review accounts/data. At this historical checkpoint its interactive sudo activation was pending; the current
activated pilot result is recorded at the top of this audit. The public review route remains on **3080**.
This preparation cannot establish company participation or business acceptance.

## Historical pilot security candidate checkpoint — 2026-10-03 (not deployed)

After 211 activation, T-09 preparation adds a separate runtime DB password/file/role and a bounded one-shot
migration service. It does not change or activate staged 211. Ordinary API configuration excludes the database
owner/bootstrap settings. Actual isolated PG17/Staging image checks passed: 29 migrations with no web host,
8 denied DDL/role/audit mutations, individual sign-in, ordinary insert/update plus audit inserts, only
`hub_pilot_app` sessions, no owner credential in the API and cookie/key persistence after restart.
The apparent restart timeout was a checker error: Docker reassigned the ephemeral published port; refreshing that
port made the unchanged image pass. Independent review identified candidate-stop and migration-container cleanup
failures; both are corrected, a verifier-rejection stop regression is retained and failure-path re-review PASS. All 19 backup/pilot helper tests
and 3 LocalAccounts tests passed at this checkpoint; exact new-candidate CI is still required.

This addresses preparation for the database role gate; it does **not** implement PITR, a bounded encrypted off-host
copy schedule or the 15-minute recovery target. Those remain explicit production gates.

## Historical review release gates — 2026-10-03

| Gate | Verdict | Limit |
|---|---|---|
| Corrected code and required CI | PASS, bounded | Candidate/main CI 858/858; latest documentation follow-up and its merged-main CI also PASS; subsequent pilot database-role work is a separate uncommitted candidate |
| Merge | PASS | PR #25 merged at `328e88a`; PR #26 documentation merged at `5b2631a`; executable remains 211bd88 |
| Exact image and migration rehearsal | PASS locally | Fresh seed-free configuration and restored synthetic review snapshot in Colima |
| Current review deployment | PASS for 211bd88 | Private/public probes and populated hosted slices above; all packet/company/native-print acceptance is not closed |
| Encrypted off-host recovery | PASS, manual | Approved review dump/config/verifiers/keys retrieved and restored in isolation; earlier snapshot, not automatic execution or latest 29-migration retrieval |
| Scheduled recovery | UNPROVEN | First automatic review backup due 2026-10-03 22:00 UTC; daily dumps FAIL the 15-minute/PITR requirement |
| Monitoring | Prepared only | Specific Uptime Kuma save approval remains pending |
| Synthetic pilot rehearsal | Prepared only | Separate private runtime initialized; operator activation and fictional participant/project provisioning pending |
| Company pilot acceptance | UNPROVEN | Synthetic rehearsal does not supply real participants, hosting/privacy approval or sponsor go/no-go |
| Production | Prepared only | Company approval, recovery targets, hosted restricted database role, operations and cutover remain open |

Homedev and individual passwords remain the chosen hosting/authentication direction. Azure and new paid services are not prerequisites. Never populate company pilot or production by restoring the review database.

## Historical pilot operations and recovery checkpoint — 2026-10-03 17:15 UTC

Pilot migration/runtime separation merged through PR #27 (`9437f5e`); candidate `17d1b48` passed all CI gates.
Homedev bootstrap stopped before migration because Compose 5.5.1 rejects `run --no-build`. PR #28 replaced it
with `--pull never`; candidate `a532f8e` and merged-main CI passed. The retry then stopped at the preserved-database
backup: PostgreSQL treats a trailing `-` as a filename. PR #29 corrected both pilot stdin calls and added
`--exit-on-error`; candidate `90ad077` passed 858/858, coverage 96.7% rules branches/94.2% service lines, browser
regressions and all trace gates, then merged as `0fa478c`. Real local maintained logical backup/restore passed.
Public review stays on `211bd88`; neither failed pilot attempt establishes deployment or acceptance.

Jay requested fictional pilot people/projects. The fixture runner passed real local API checks for six synthetic
participants, one Setup project, one milestone, two draft deliverables and three Not Started tasks. The unknown
estimate remained null. Independent review corrected archived-project refusal and Admin-ID stdin isolation.
Separate private operator helpers are staged on homedev; actual provisioning and five new individual sign-ins
remain pending. No existing review people/projects/verifiers are copied to the pilot.

Pilot-only PITR preparation is a separate uncommitted patch at this checkpoint: durable archive helper, five-minute
WAL timeout, separate base/WAL volumes, verified physical backup on the existing daily service, and bounded isolated
replay drill. Independent review caught timestamp, history-file, durability, shell and stdin defects. Actual replay
then caught a Mac bind mount resolving to a directory and the image lacking `su-exec`; the mount now fails closed
and native Docker user selection is used. Final local PostgreSQL 17 rehearsal PASS in 7.3 seconds: verified base,
replay paused at the UTC target, post-base before-marker present/after-marker absent, daily physical command,
archive history/duplicates/collision refusal. All 19 existing helper checks pass. Final independent source re-review found no actionable findings. Homedev PITR, off-host schedule,
retrieval and full RPO/RTO remain unproven; no acceptance criterion or company decision is fabricated.
