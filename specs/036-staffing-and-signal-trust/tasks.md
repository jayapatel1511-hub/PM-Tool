# Tasks: Staffing Signals and Trustworthy Counts

**Input**: [spec.md](spec.md), [plan.md](plan.md). Record every result in [verification.md](verification.md), which is the acceptance plan; a ticked box is not evidence by itself.

`[A]`, `[B]` and `[C]` name the plan's streams; each has one writer. `[P]` marks a task that can run alongside the previous one.

## Phase 0: Gate (no application code)

- [x] T001 Get Jay's answers to decisions D1–D9 (or a note that the defaults stand). Record them in verification.md section 2. Done 2026-10-11: D1–D9 all Default (Jay: 'lets start build'), recorded in verification.md section 3.
- [x] T002 Write §40 in `spec-parts/15-staffing-and-signals.md` from spec.md's requirements, and add packet 036 to `specs/README.md`. Check: `python3 tools/build_spec.py --check` and `python3 tools/trace_spec.py --check` pass. Done in `5800e3fc`; both checks pass at the merge commit (verification.md section 9).
- [x] T003 (done 2026-10-12: `codex/tuesday-ui-consistency` merged in `4218baec`; the 5 October leftovers, including `Planning.cs`, committed in `7f058aed`; old branches reviewed in run 1; 25 superseded; codex/landing-polish is a real landing refresh, outside this packet, left for Jay. Base: `9beace03`, verification.md E2) Clear the conflict risks listed in the plan's branch table:
  - merge `codex/tuesday-ui-consistency` into the base, or record that it is abandoned;
  - commit the pending `Planning.cs` edit;
  - confirm the `codex/pm-*` branches are superseded.

  Record the final base commit in verification.md. Check: `git rev-list --count <base>..codex/tuesday-ui-consistency` is 0, or the branch is recorded as abandoned.

## Phase 1: Domain (pure, Stream A)

- [x] T004 [A] Add `DependencyTypes` to `src/Hub.Domain/Vocabulary.cs`. Add `DependencyRules.Satisfied` and the type-aware D-12 checks to `src/Hub.Domain/Evaluation.cs`. Extend `DependencySnap` (`Type`) and `TaskSnap`/`DeliverableSnap` (`StartedOn`, `StatusChangedOn`) with defaults, so existing callers compile unchanged. Requirements: FR-DEPT-03, -04. Done on `build/036-domain` (`24e1995d`), merged 2026-10-11.
- [x] T005 [A] Add the A-21 rule, its `RuleNames` entry, `Rules.Ids` 1–21 and the `status_stale_days` setting in `src/Hub.Domain/Evaluation.cs` and `src/Hub.Domain/Settings.cs`. Requirements: FR-STALE-01, -02. Done in `24e1995d`. The setting sits in the existing `thresholds` group.
- [x] T006 [A] Add the dependency cases to `tests/Hub.Tests/Domain/DependencyLagTests.cs` and the A-21 cases to `tests/Hub.Tests/Domain/EvaluatorTests.cs`, as in the plan's test table. Check: `dotnet test … --filter 'FullyQualifiedName~DependencyLagTests|FullyQualifiedName~EvaluatorTests'` passes, and `coverage_gate.py` holds. Done in `24e1995d`; coverage gate 96.6 % (writer run on `build/036-domain`).

## Phase 2: Data (Stream A)

- [x] T007 [A] Add `OpenRole` in `src/Hub.Api/Data/OpenRoleEntities.cs`. Add `StartedAt` (task and deliverable), `DeliverableDependency.DependencyType` and `AppUser.HomeDisciplineId` in `src/Hub.Api/Data/Entities.cs`. Add the DbSet, checks, indexes, FKs and query filter in `src/Hub.Api/Data/HubDb.cs`. Add the audit fields in `src/Hub.Api/Infrastructure/Audit.cs`. Done in `299e8be3`.
- [x] T008 [A] Rebase on the T003 base. Then generate the single `src/Hub.Api/Data/Migrations/<timestamp>_StaffingSignals.cs` with the `StartedAt` backfill SQL. Check: `dotnet ef migrations has-pending-model-changes` reports none; existing migrations are unchanged; the up/down migration test passes on fresh PostgreSQL, including the backfill cases. Done in `299e8be3`: one migration, `20261012014621_StaffingSignals`, on base `5800e3fc` (T003 is only partly done, so no later base existed).

## Phase 3: API (Stream A)

- [x] T009 [A] Implement `src/Hub.Api/Features/OpenRoles.cs` and register it in `Modules.cs`. Add `OpenRoleId` to allocation create and the reset on decline/cancel in `src/Hub.Api/Features/Allocations.cs`, with no other edits there. Add `unfilledRoles` to `/workload` in `src/Hub.Api/Features/Workload.cs`. Add text keys in `src/Hub.Api/Text.cs`. Write `tests/Hub.Tests/Api/OpenRoleApiTests.cs`. Requirements: FR-ROLE-01–05. Check: OpenRole, Allocation and Workload tests pass. Done on `build/036-api-roles` (`8b689db3`, `0a815c5a`, `913f0123`), merged 2026-10-11.
- [x] T010 [A] Make the following changes, all listed in the plan's A2 section: Done: the EvaluationService mapping in `0e368af2`, the rest on `build/036-api-deps` (`daa98739`, `7a190db0`), merged 2026-10-11.
  - Map `Type`, `StartedOn` and `StatusChangedOn` in `src/Hub.Api/Infrastructure/EvaluationService.cs`.
  - Set `StartedAt` in `Tasks.cs` and `Deliverables.cs`.
  - Add the type field and shared satisfaction to `Dependencies.cs`.
  - Return `type` and `satisfied` in the deliverable dependency payload in `Deliverables.cs`.
  - Copy `Type` in copy-structure (`Extras.cs`).
  - Use the shared function in the predecessor check in `Readiness.cs`.
  - Add `type` to the payload in `Timeline.cs`.

  Requirements: FR-DEPT-01, -02, -05. Check: the `EvaluationTests`, `Readiness` and `Timeline` test filters pass.
- [x] T011 [A] [P] Add `homeDisciplineId` to the admin user PATCH (`Admin.cs:144`). Return `officeId`, `jobTitle` and `homeDisciplineId` on `/users`, the Workload person rows and the planning grid rows. Add `PersonIds` to `GridQuery` in `Planning.cs`. Add the `PlanningApiTests` and `WorkloadApiTests` cases. Requirements: FR-GRP-01, FR-PICK-01. Check: the Planning, Workload and Admin test filters pass, and the privacy tests are unchanged. Done on `build/036-api-roles` (`0a815c5a`, `02a4c2df`, `52a0e3e9`).

## Phase 4: Web, Part A (Stream B, after T009–T011)

- [x] T012 [B] Open roles in `web/src/pages/projects/Allocations.tsx`:
  - list, add, edit and cancel open roles;
  - "Fill" opens the existing allocation create form prefilled with the role's dates and hours, and sends `openRoleId`.

  Add the Unfilled roles section to `web/src/pages/Workload.tsx`. Requirements: AC-ROLE-01–04. Done on `build/036-ui-staffing` (`4480db5e` open roles with a view switch on the Allocations tab, `796f33f9` Unfilled roles on Workload), merged 2026-10-12 in run 2.
- [x] T013 [B] Dependency type:
  - add a select (default Finish-to-Start) and a type chip to the dependency section of `web/src/pages/projects/TaskPanel.tsx`, with hint text by type;
  - make the same change in `ExplicitDeps` in `web/src/pages/projects/Deliverables.tsx`, using the server's `satisfied` value instead of the client rule at :465.

  Requirements: AC-DEPT-01, -05. Done in `adf8b507` (shared `DepTypeField` and `DepTypeChip` in `indicators.tsx`; the client rule is deleted), merged 2026-10-12.
- [x] T014 [B] Add the `groupBy` control with a "Not set" bucket to `web/src/pages/Workload.tsx` and `web/src/pages/Planner.tsx`. Add the home discipline field to the user editor in `web/src/pages/admin/Admin.tsx`. Requirements: AC-GRP-01, -02. Done in `796f33f9` (Group menu, `groupPeople` in `planner/labels.ts`, per-week subtotals) and `7a544d18` (Admin editor), merged 2026-10-12.
- [x] T015 [B] Add `capacityWeek` to `PeoplePicker` in `web/src/components/hub/people.tsx`, and pass it from `TaskPanel.tsx`, `Tasks.tsx` (create form) and `ReassignWork.tsx`. Requirements: AC-PICK-01–03. Done in `6afb5fef` and `adf8b507` (the TaskPanel owner field gets the week through a `CapacityWeek` context), merged 2026-10-12.

## Phase 5: Part B (Stream C, can start after T003)

- [x] T016 [C] (Server part done on `build/036-api-deps` in `f41ccc2e`: `TaskQueries.cs` `onHold`, `Deliverables.cs` unassigned filter, `Workspace.cs` ordering and `onHold`, with API tests. Web half done on `build/036-ui-signals` in `937822e9` (Portfolio), `a2f57e68` (Dashboard `onHold=false`) and `aa03498c` (Home On Hold row), merged 2026-10-12; the project Tasks page forwards `onHold` since the integration fix `b1a38fc6`.) Make the count fixes from the plan's B1 table:
  - `Portfolio.tsx`
  - `TaskQueries.cs` (`onHold`) and `Dashboard.tsx`
  - `Deliverables.cs:71`
  - `Workspace.cs` (ordering and `onHold` count)
  - `Home.tsx`

  Add the API tests. Requirements: AC-CNT-01–05, -07.
- [x] T017 [C] Add the `SummaryTile` `hint` in `web/src/components/hub/common.tsx`, and `def.*` strings for every Home, Dashboard and Portfolio tile. Requirements: AC-CNT-06. Done in `04275ef3` (the hint is an (i) tooltip read through `aria-describedby`; the strings are `sig.def.*`), merged 2026-10-12.
- [x] T018 [C] Make the timeline changes from the plan's B2 list in `Timeline.tsx`, `Gantt.tsx` and `Timeline.cs`. Requirements: AC-TL-01–04. (Server part done in `e1ffbb01`: undated milestones in `Timeline.cs`. Web part done in `8ddea543`, merged 2026-10-12.)
- [x] T019 [C] After T010, draw arrows by dependency type in `Timeline.tsx` and `Gantt.tsx`, with the type and lag in the tooltip. Requirements: AC-DEPT-06. Done in `8ddea543` and `becca7b7`, merged 2026-10-12; explicit deliverable-link tooltips name their lag since the run 2 fix pass (036-D10, `447afefe`), and a link derived from task links names the type only (F9).

## Phase 6: Acceptance (see verification.md)

- [ ] T020 (all but three fixtures done 2026-10-12: `seed_staffing.py`, its tests and `counts.cjs` in `df50786e` and `fe1abce2`; the section 8.2 example was encoded in run 1; 036-D4 fixed in `325fb7fa`, and a fresh database seeded through the `79882c20` API loads every module, the template path included, with zero refused steps (verification.md E4). Three section 8 fixtures cannot be made through the API: an unowned deliverable, backdated ages and an undated milestone on an existing project; the API tests cover them, and accepting that is for Jay (needs Jay). `test:counts` passes at `a499ed72`: 288 counts, 0 mismatches) Write `tools/preview/seed_staffing.py` with the section 8 fixtures (SYN-104, SYN-105, stalled items, capacity week W, count and timeline edge cases), seeded through the API as the acting persona. Encode the section 8.2 worked example in `DependencyLagTests`. Add `web/tests/counts.cjs` (an `npm run test:counts` script) that clicks every Home, Dashboard and Portfolio tile as Admin, PM and Supervisor and compares the count with the rows (SC-002).
- [ ] T021 (open: the Performance row needs the `hub_scale` fixture database, which does not exist on the local test server; every other section 9 row PASS. Release candidate pass at `ceedcce5`: full suite, coverage, web, spec, wording and merge re-run, and Migration up and the rollback checked on the review image (036-D18 found and fixed). Earlier: re-run in the finisher pass at `a499ed72` except Coverage. Partly done 2026-10-12: the full checks ran at `caa76893`, see verification.md section 9; the `/workload` JSON diff and the evaluator output diff are not run; acceptance sweep 2026-10-12 at `4760e2c4`: every section 9 row PASS, the `/workload` and evaluator diffs included, except Performance, UNRUN because there is no `hub_scale` database) Run the full checks and the regression guard (verification.md section 5), including the before/after `/workload` JSON diff and the evaluator output diff on the fixture. Record each result with the commit.
- [ ] T022 (open: tablet widths are not run; the screen-reader half needs a person, booked by Jay (needs Jay). Partly done 2026-10-12: rows filled from the run 2 writer browser passes and the integrator's runs; tablet and the SC-003 chip pass are not run; acceptance sweep 2026-10-12 at `4760e2c4`: every B row has a desktop browser pass with screenshots in `evidence/`, the SC-003 and SC-004 checks included; tablet widths and the VoiceOver half are not run) Fill in the evidence map (verification.md section 4) for every AC through automated tests or an agent browser pass (desktop, tablet, keyboard), with screenshots. Run the SC-003 and SC-004 browser checks.
- [x] T023 Run the wording check: `git diff <base> -- . | grep -inE '<competitor product names>'` returns nothing (weekday names used in date rules are fine). Do a trial merge into the base with no conflicts. Commit on a packet branch. Done 2026-10-12: the five run 2 branches merged into `claude/tuesday-visual-batch1` with no conflicts (`7a39e76b`); the wording grep on `git diff 9beace03..HEAD` and the commit messages prints nothing.
- [ ] T024 (needs Jay: the deploy and the human sessions need his go-ahead and real people. Release candidate `ceedcce5` is ready: activation steps in `docs/handoffs/2026-10-12-packets-036-037-release-candidate.md`; add `--with-staffing-fixtures` to the fixture wrapper on the day of S2.) With Jay's go-ahead, deploy to the synthetic review stack. Run the human sessions in verification.md section 7: the SC-001 staffing exercise, the dependency review, the screen-reader pass and Jay's visual review. Book the people when T001 is answered, not at the end.
- [ ] T025 (needs Jay.) Jay signs off in verification.md section 8. Update `specs/README.md` status. Any failed row goes back to its task; a new idea goes to a follow-up proposal, not into this packet.
