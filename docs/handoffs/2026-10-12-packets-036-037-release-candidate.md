# Packets 036 and 037: release candidate

**Release SHA**: `ceedcce565a90dd3860e38132487d7ba8d77421a` on `claude/tuesday-visual-batch1`.

The commits after it on the branch only record results: the two packets' `verification.md`, packet 036's `tasks.md` and this folder. `git diff ceedcce5 HEAD -- src web tests hosting scripts tools` is empty, so the image and CI are the same at the branch head. Nothing has been pushed, merged or deployed. The review stack still runs `bbac1f6daf595a9411908f9795201c20f812079c` (5 October).

## What is in it

Compared with the hosted `bbac1f6d`, the release adds three packets and two database migrations, `20261009225223_AvailabilityAndCalendarFeeds` and `20261012014621_StaffingSignals`.

**Packet 036: staffing signals and trustworthy counts**
- **Open roles.** A PM can staff a project with a role (label, dates, hours, optional discipline) before anyone is named. The role shows on the project's Allocations tab and in Workload under Unfilled roles. Filling it proposes an ordinary allocation; a decline or cancel puts it back to Open.
- **Link types.** Task and deliverable links can be Start-to-Start or Finish-to-Finish as well as Finish-to-Start, with a lag in working days. Chips and timeline arrows show the type and lag.
- **Status not moving (A-21).** Flags open work whose status has not changed in 15 days (a setting) while people are still working on it.
- **Home discipline and grouping.** An Admin can give each person a home discipline. Workload and the Weekly Planner can group people by Reports to, Office, Job title or Home discipline.
- **Free hours when assigning.** The person picker shows each candidate's free hours and time away for the task's week, but only to people who can already see that person's capacity. When part of the person's work is on a project the viewer cannot see, it shows "Partial view" instead of a number.
- **Counts that match.** Every Home, Dashboard and Portfolio tile opens exactly the rows it counts and says what it counts. On Hold, unassigned and undated work is counted, not dropped.
- **Honest timelines.** Bars run start → due. A guessed start is faded and marked "start not set". Undated milestones are listed under Unscheduled. Nothing is silently left out.

**Packet 037: calm workspace**
- On all 17 measured screens, work starts in the top 45 % of the screen.
- Filters live in one Filters popover with a count and removable chips. At most three quick filters show at a time.
- Project pages have at most 7 group tabs plus More. The sidebar has at most 8 items.
- Headers fit on one line and explanations sit in help popovers. Each row shows one signal plus "+n", and fewer colours are used.
- My Work opens on Today.
- Nothing was removed: every route, tab, filter and export can still be reached, in two clicks or fewer.

**Packet 035 (availability and weekly calendar).** This packet was merged before packets 036 and 037 and is not in the hosted `bbac1f6d`. It adds approved-leave import and manual HR records, weekly schedules, the Vacation & Coverage views and personal calendar subscriptions.

**This pass**
- `scripts/seed-homedev-tuesday-review.py --with-staffing-fixtures` is a new opt-in flag that seeds the packet 036 review data. It is off by default (`27987015`), and the runbook says when to use it (`14a85e8a`).
- Defect 036-D18 is fixed. On a new review database, the demo seed's In Progress work had no start date, so a Start-to-Start link from it would have waited for a start that had already happened (`ceedcce5`).

## Checks

Every step of `.github/workflows/ci.yml` ran on the Mac in workflow order, at `ceedcce5`. The same sequence had already passed at `14a85e8a`, before the 036-D18 fix.

| # | CI step | Result at `ceedcce5` |
|---|---|---|
| 1 | `dotnet restore Hub.slnx` | exit 0 |
| 2 | `dotnet build Hub.slnx --no-restore -c Release` | exit 0; 0 errors, 538 warnings (existing analyzer warnings) |
| 3 | `dotnet test Hub.slnx --no-build -c Release --collect:"XPlat Code Coverage" --results-directory coverage` | exit 0; 1108 passed, 0 failed, 0 skipped (4 m 42 s) |
| 4 | `python3 tools/coverage_gate.py coverage` | exit 0; rules engine branches 96.8 % (gate 95 %), service lines 94.8 % (gate 70 %) |
| 5 | Backup and pilot safety: `test_backup_helpers.py`, `test_pilot_initializer.py`, `test_pilot_operations.py`, `test_pilot_pitr.py` | exit 0; 4, 5 and 10 tests OK; the recovery script check passes its shell syntax test. It skips the full replay without `--docker`, as it does in CI |
| 6 | Fixture and credential unittest list (six files) | exit 0; 42 tests OK, including the 11 wrapper tests |
| 7 | `dotnet list Hub.slnx package --vulnerable --include-transitive`, no Critical | exit 0; no vulnerable packages in any of the 3 projects |
| 8 | `npm ci` (web) | exit 0; 147 packages |
| 9 | `npm run build` (web) | exit 0 (the existing large-chunk notice) |
| 10 | `npm run lint` (web) | exit 0; 0 errors, 92 warnings (none new) |
| 11 | Browser install (`npx playwright install chromium --only-shell`) | exit 0. Ran without `--with-deps`, which installs operating-system packages; the browser was already present |
| 12 | `npm run test:handoffs` | exit 0; 0 errors, 0 accessibility violations |
| 13 | `npm run test:coordination` | exit 0; 6 accessibility reports, 0 violations |
| 14 | `npm run test:design-basis` | exit 0; unmockedGets [] |
| 15 | `npm run test:planner` | exit 0; 20 flows, 7 recovery checks, 13 accessibility reports, 0 violations |
| 16 | `npm run test:resources` | exit 0; 0 violations, unmockedGets [] |
| 17 | `npm audit --audit-level=critical --omit=dev` | exit 0; 0 vulnerabilities |
| 18 | `python3 tools/trace_spec.py --check` | exit 0; 818 IDs, 0 not cited; 271 sections, 0 not cited; 0 unknown |

Differences from CI: the Mac uses its own toolchain (SDK 10.0.400, and the web toolchain at version 26 where CI pins 24) and the local test database server on 127.0.0.1:55432 instead of the CI service container. `vuln.txt` was written outside the repository. See packet 036's H4.

**Review image**

| Check | Result |
|---|---|
| Build | `docker build -f hosting/Dockerfile.review -t pm-tool-review:rc-ceedcce5 .`, run from a `git archive` of `ceedcce5`: exit 0 in 44 s. The image is **arm64**, because the Mac's container runtime is an arm64 VM. Homedev builds its own image from the prepared release during activation, so this one was only for the smoke test. |
| Settings | Ran with the `api` settings of `hosting/homedev.compose.yml`: Staging, local passwords, trusted proxy at the review gateway `172.30.245.1` (the network uses the review subnet), `AllowedHosts=pm.engcalchub.com`, user 1000:1000, read-only root, no capabilities, loopback port 3080. It ran against a temporary database container. The runtime files came from `scripts/init-homedev-review.py` and `scripts/bootstrap-review-credentials.py` with throwaway values. One Mac-only difference: the key ring was in a temporary volume owned by the container user (H5). |
| New database | All 33 migrations applied, the same list as the source tree, ending with `20261012014621_StaffingSignals`. The log shows no exception. |
| Runbook probes | `python3 scripts/verify-homedev-review.py`: health 200, anonymous `/api/v1/me` 401, wrong Host 400, unsafe Origin POST 403, wrong password 401, individual sign-in 204, signed-in `/me` and project list 200, sign-out 204, `/me` after sign-out 401. RESULT PASS. The copied-cookie replay after sign-out answers 200, which is already documented. |
| App served | `/` 200 `text/html` with the app root. A deep link (`/projects/SYN-101/tasks`) returns the same index, and the hashed script asset returns 200 `text/javascript`. Plain HTTP gets 307 to `https://pm.engcalchub.com/…`. An unsafe Origin on sign-out gets 403. |
| Sessions | A session cookie still works after the API container is recreated, so the key ring persists. |
| Data | On a new database, the demo seed's started work has `started_at`: 0 started items without it and 0 Not Started items with it. No links other than Finish-to-Start. |
| Upgrade from the hosted release | The hosted release's image (`bbac1f6d`, built from its own tree) created and seeded a new database with 31 migrations. The candidate then applied the two new migrations and passed the probes. Every started demo item got `started_at` equal to its last status change. Every existing table kept its row count, except `org_setting` (+2: `status_stale_days` and the A-21 switch, with their two audit rows). The new tables are empty. |
| Rollback by image | On that migrated database, the hosted release's image started with no exception, passed the probes and served signed-in project, task, deliverable and timeline reads. The candidate then started on it again and passed the same checks. |
| Clean-up | Containers, the volume, the network, both images and the throwaway `.runtime/` were removed. |

**Other checks**
- **Wrapper flag:** 11 tests in `scripts/test_seed_homedev_tuesday_review.py` pass, covering the flag off (three seeds, as before), the flag on (staffing seed last with the same transport arguments, refused if the staffing projects are missing) and the flag added to a completed release (staffing seed only, once).
- **036-D18:** the new assertion failed before the fix (3 of 6 tasks) and passes after.
- **Spec:** at the branch head after these records, `python3 tools/build_spec.py --check` reports "up to date" and `python3 tools/trace_spec.py --check` passes (818 IDs, 0 not cited).
- **Wording:** the diff and commit messages since `0c59cd1a` name no third-party product. The co-author trailer and command lines copied from the scripts are the exceptions.
- **Merge:** the remote `main` (`9e74863e`, as last fetched) is an ancestor of the branch, so the pull request has no conflicts.
- **Acceptance:** recorded in the packets' verification files. Packet 036: sections 5, 6 and 9 PASS, except SC-001, AC-CNT-06's screen-reader half, SC-005 and Performance (below). Packet 037: AC-CALM-01 to -10 PASS for all six personas. No packet 037 measurement needed re-running, because `web/` is unchanged since `a499ed72` (H10).

## Assumptions

All assumptions made while Jay was away are listed for review in [packet 036, section 14](../../specs/036-staffing-and-signal-trust/verification.md#14-assumptions-made-while-jay-was-away) and [packet 037, section 8](../../specs/037-calm-workspace/verification.md#8-assumptions-made-while-jay-was-away).

This pass added:
- **H1, H2:** the staffing flag is off by default, with these rules and timing.
- **H3:** the release SHA is the last code commit; later commits are records only.
- **H4:** the CI parity differences listed above.
- **H5, H6:** how the image smoke mirrored the compose file, and why the image's architecture doesn't matter for homedev.
- **H7:** how rollback was checked.
- **H8:** 036-D18 was fixed rather than deferred.
- **H10 to H12:** packet 037 rows stand on the unchanged `web/`, and SC-CALM-02 should look at the hosted candidate.

Earlier rows that change behaviour and need Jay's decision:
- A5, the A-21 rule.
- A6, a reason is needed to change open roles on a Complete project.
- F1, the picker's partial-view rule.
- E3, My Work opens on Today.
- 036-D2's reading of FR-DEPT-02: holding or cancelling is not a start.

## Known limits and open items

- **Human sessions are not run.** These are S1 (staffing, SC-001), S2 (dependency review), S3 (keyboard and screen reader) and S4 (Jay's visual review) in packet 036, section 10, plus SC-CALM-01 (5-second test with two PMs) and SC-CALM-02 (Jay's visual review) in packet 037, section 5. They need Jay's go-ahead to deploy, and real people. Both sign-off tables are empty.
- **Performance row is UNRUN.** There is no `hub_scale` database on the local test server, so `/workload`, `/planning/grid` and picker timings were not measured. Because of this, SC-005 stays UNRUN.
- **Partial-view follow-up (Jay to decide).** The partial flag and free hours leave out Confirmed allocations on projects the viewer cannot see, so the Planner and the picker can still overstate someone's free hours. Including them changes packet 034's AC-PLN-12.
- **Landing-polish branch.** A landing-page refresh sits on its own branch outside these packets, left for Jay (packet 036, G7).
- **Visual changes that need Jay's eye.** Avatars are rings with coloured initials instead of pastel fills (B3). Project identity dots are rings (F14). The six identity background tints share one neutral tint (M8).
- **Smaller open items.**
  - Jay has not yet confirmed the proposed severities of the S3 defects 036-D1, -D4 to -D7 and -D11 to -D18. All of them are fixed.
  - The deliverable "finished before predecessor" note is not yet returned by the API.
  - Three section 8 fixtures can't be made through the API.
  - The stored 1440 and 1024 screenshots predate the latest screen changes.
  - Two second routes take more clicks (AC-CALM-10).
  - A menu that is still closing needs a second click on its trigger.
  - The project Tasks table scrolls sideways at 1440 px.
  - A copied cookie still works after sign-out.
  - The runbook still records the off-host backup as failing.
- **Not checked from the Mac.** That the hosted database has never applied `StaffingSignals` (packet 036, G5). Behaviour on homedev's architecture. Everything the tunnel and DNS route carry.

## Activation steps

These are from `docs/runbooks/homedev-review.md`. Deploy only after Jay's go-ahead, in his own terminal; sudo prompts there.

1. **Prepare (Mac)**, from any checkout that has the commit:
   ```bash
   scripts/prepare-homedev-review.sh ceedcce565a90dd3860e38132487d7ba8d77421a
   ```
   `.runtime/` is already set up on homedev, so `init-homedev-review.py` is not needed.
2. **Activate (homedev).** This builds the image, dumps the database, starts the new API on the existing volume, prints migration and error lines, runs the private probes, and only then moves `current`. Add `--install-timer` only if the daily dump timer is not installed yet.
   ```bash
   cd /home/jaypatel04/Workspace/Projects/pm-tool/releases/ceedcce565a90dd3860e38132487d7ba8d77421a
   bash scripts/activate-homedev-review.sh ceedcce565a90dd3860e38132487d7ba8d77421a </dev/null
   ```
   The printed log should show the two new migrations applied (`AvailabilityAndCalendarFeeds`, `StaffingSignals`).
3. **Fixtures (homedev, same directory).** Run this after the private checks pass:
   ```bash
   python3 scripts/seed-homedev-tuesday-review.py ceedcce565a90dd3860e38132487d7ba8d77421a
   ```
   **On the day of S2**, and of S1 where possible, add the staffing fixtures. Either pass the flag on the first run, or run the wrapper again with it once the default run has finished:
   ```bash
   python3 scripts/seed-homedev-tuesday-review.py ceedcce565a90dd3860e38132487d7ba8d77421a --with-staffing-fixtures
   ```
   The flag turns on working days for the whole organisation and adds a Riverside office holiday. SYN-105's lag states hold on its seed day only, and it is created only once per review database.
4. **Probes:**
   ```bash
   python3 scripts/verify-homedev-review.py                            # private origin (activation runs it too)
   python3 scripts/verify-homedev-review.py https://pm.engcalchub.com  # through the tunnel
   ```
   Then open `https://pm.engcalchub.com/?review=ceedcce5` and sign in as one of the ten personas.

   To check the `started_at` backfill (this also settles G5), run this read-only query from the release directory right after activation. Expect `0|0|…` on both lines (started items without `started_at` | Not Started items with it | total):
   ```bash
   for t in task deliverable; do sudo docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml exec -T db psql -U hub_review -d hub_review -At -c "SELECT count(*) FILTER (WHERE status <> 'Not Started' AND started_at IS NULL AND NOT (status IN ('On Hold','Cancelled') AND previous_status = 'Not Started')), count(*) FILTER (WHERE status = 'Not Started' AND started_at IS NOT NULL), count(*) FROM hub.$t" </dev/null; done
   ```

## Rollback

- **Both migrations are additive.** They add nullable columns, new tables, indexes, check constraints and two settings. `StaffingSignals` also resets every task link's type to Finish-to-Start (the column was never read before packet 036) and backfills `started_at`.
- **What was tested:**
  - The `StartedAt` backfill: `StaffingSignalsSchemaTests`, the 036 sweep on a copy of the preview database, and this pass on a database created by the hosted release.
  - The down migration (sweep): it restores the earlier schema with unchanged row counts and drops `open_role`.
  - An image switch back to `bbac1f6d` on the migrated database, and forward again (this pass).
  - No migration file has changed since `5a6bf481`.
- **How:** if the probes fail, the activation script prints the rollback command. To roll back later, run this from the previous release's directory:
  ```bash
  cd /home/jaypatel04/Workspace/Projects/pm-tool/releases/bbac1f6daf595a9411908f9795201c20f812079c
  sudo env RELEASE_SHA=bbac1f6daf595a9411908f9795201c20f812079c docker compose --env-file .runtime/review.env -f hosting/homedev.compose.yml up -d --no-build </dev/null
  ln -sfn releases/bbac1f6daf595a9411908f9795201c20f812079c ../../current.new && mv -T ../../current.new ../../current
  ```
- **Keep the new schema when rolling back.** The old image runs on it. A down migration would drop open roles, link types and home disciplines. The activation dump is the restore point. Restore it only into a new isolated database, as the runbook says, and never delete the volume.
- **Caveats:**
  - Work that the old image starts gets no `started_at`. Before relying on Start-to-Start links after rolling forward, apply the migration's backfill statement again, limited to started items without one. This follows from the code and was not tested.
  - A down-and-up cycle resets typed links to Finish-to-Start.

## Commits in this pass

`27987015` wrapper flag and tests · `14a85e8a` runbook note · `ceedcce5` 036-D18 (release SHA) · `53e86832` verification records · this handoff and the pull request text.
