# Verification: Capacity Figures and Server Values

Runtime source `566fe5187d72be56a14fd16d0428e8b8e6e34d72` passes **1,514/1,514 backend tests** (zero failures/skips). Rules branch coverage is **3,032/3,155 (96.1%)** and service line coverage **13,084/13,665 (95.7%)**. The 799-response shape gate passes from one full-run manifest. See the [acceptance follow-up](../../docs/handoffs/2026-10-19-039-050-acceptance-followup.md) and its hashed receipt for current browser, image and source boundaries. Earlier slice runs below are historical; their missing-schema/startup blockers are resolved by the combined run. Prior compatibility evidence retains source `1b649f5`; no schema changed afterward. No packet is Accepted; hosted, pilot and human gates remain separate.

Status 2026-10-25 (`codex/integrate-039-050`): S01 to S12 built and checked locally; S13 flag cleanup waits on Jay turning on `flag.figure_names` and `planner_kept_rows`. AC-FIG-04 is PASS (local) after the 2026-10-25 gap run below. Tier B review, hosted, pilot and human checks are not done. Rerun today: domain WorkloadTests, PermissionMatrixTests and DomainHelpersTests 146 passed, 0 failed; ratchet and its self-tests pass. API tests were not rerun (local Postgres was down); their status rests on the 1,514-test combined run.

Review-fix run 2026-10-25 (Release, local; `cf3ce8ee` plus the Code review and cleanup session's uncommitted fixes; full backend 1,516 passed, 1 skipped, 0 failed): AC-FIG-08 strengthened. The DesignBasis, ReviewChange and Submission permission matrices and `TasksTests.Task_row_flags_and_real_writes…` now assert that every recorded client/server difference is "client offers, endpoint refuses" instead of only logging it; 21 differences, all in that direction; 114/114 pass. Not committed yet.

| AC ID | Slice | Status |
|---|---|---|
| AC-FIG-01 | S04, S13 | PASS (local): `planner.cjs` with the flag on: the partial person's row shows "Partial view" with the PLN-15 partial-view text as title and aria-describedby; the cell, its accessible name and the contribution list read "at most 40 h"; the page notice still shows. A person with no hidden work has no label and no "at most". Flag off: no label, no "at most", notice shows. 0 axe violations. The mocked fixture uses a person with no planned hours instead of the AC's 30 h entry; the visible value is 40 h either way. No restricted name or hours is covered by the server tests. S13 flag removal pending. |
| AC-FIG-02 | S04 | PASS (local): `PlanningApiTests.Every_free_hours_reader_marks_hidden_confirmed_work_partial` (Planner grid read and the personIds batch read used by the picker and My Staff: partial, remaining 40, no project id or number; Workload partialScope true and uncommitted null; Admin sees partial false, remaining 20, uncommitted 20); `Picker_rows_say_when_a_person_has_planned_work_the_viewer_cannot_see` and `DeploymentConformanceTests.Hidden_confirmed_allocations_mark_capacity_partial_without_returning_private_work` pass unchanged. |
| AC-FIG-03 | S03, S13 | PASS (local): Uncommitted is 12 while the Planner's remaining is 24; a partial row returns null at 12 and -3 at -3 (`WorkloadTests.Uncommitted_never_claims_spare_capacity_for_a_partial_row`, `WorkloadApiTests.Uncommitted_is_computed_on_the_server`). `resources.cjs`: flag on shows "Uncommitted −12 h" and no Workload cell says Remaining; flag off shows "Remaining N h". AC-PLN-11 regression test passes unchanged. The picker's "24 h free" is not asserted in a browser test; the API test pins its Planner value. S13 flag removal pending. |
| AC-FIG-04 | S03, S13 | PASS (local): Workload with the flag on: the grid's aria-describedby is the uncommitted basis line and the Method step names Uncommitted. My Staff: the free figure's tooltip and description are the remaining-capacity basis (`supervisor-capacity.cjs`); flag off has no tooltip or description. Person picker (gap run, `c56fb129`): `resources.cjs` opens the Workload unassigned-task picker (same `CapacityFigure` picker as assignee and reassign); flag off, the "12 h free · 8 h away" figure has no title or aria-describedby; flag on, both are "Remaining capacity in the Weekly Planner: capacity − Confirmed − Expected"; open-picker axe `pickerViolations` []. Planner: `planner.cjs` asserts the grid's description is "Planned totals include Possible work. Remaining = capacity − Confirmed − Expected." with the flag off and unchanged with it on; 17 axe checkpoints, 0 violations. `resources.cjs`, `planner.cjs` and `supervisor-capacity.cjs` report 0 axe violations. The picker fixture shows 12 h free, not the AC's 24 h; `PlanningApiTests` pins the 24 h value. Not covered: a real-API picker run; S13 flag removal pending. |
| AC-FIG-05 | S01, S02 | PASS (local): domain edges 39/40/90/91/110/111 % (S01, `WorkloadTests.Load_bands_follow_settings_and_default_to_todays_shades`); API cells carry `band`: 100 % Near, 115 % Over, no-availability with committed hours Over (`WorkloadApiTests.Cells_carry_bands_over_week_and_thresholds_from_settings`); `resources.cjs` keeps every fixture cell's shade (130, 156, 95, 50, 0 %). |
| AC-FIG-06 | S01, S02 | PASS (local): with amber 80 and over 120, 100 % and 115 % are Near, Over-assigned is false and the response thresholds are 120/80/40; restoring defaults restores bands (`Cells_carry_bands_over_week_and_thresholds_from_settings`). Each change writes one activity entry; 49, 301 and 101 and a non-Admin change are refused (`Workload_threshold_changes_are_audited_and_validated`). Legend renders the server thresholds; the browser check covers the defaults (110, 90, 40 %). |
| AC-FIG-07 | S02 | PASS (local): 100 % this week and 115 % next week give `overWeek` 2026-09-21 (`Cells_carry_bands_over_week_and_thresholds_from_settings`); `resources.cjs` checks the Over-assigned chip names the server's week. |
| AC-FIG-08 | S07, S09, S10, S11 | PASS (local): all 13 decisions covered by endpoint matrices; six diagnostic tests pass. [Recorded differences](evidence/permission-differences.json) preserve 36 legacy client-allow/server-deny cases. Existing project tests use post-authorization validation probes; new matrices use valid writes. Browser evidence is bounded to the named controls below, not every matrix cell. |
| AC-FIG-09 | S05 | PASS (local): `PermissionSweepTests.Open_role_changes_follow_the_owner_rule` and the `PermissionMatrixTests` theory `Open_role_changes_need_the_proposal_gate_and_pm_or_creator` (role created by a Discipline Lead: PM and creator allowed, another Discipline Lead gets `perm.owner`, Team Member gets the proposal refusal, all refused on Archived); the row's `canManage` calls the same `Permissions.ManageOpenRole`; OpenRoleApiTests unchanged and passing. |
| AC-FIG-10 | S05, S06 | PASS (local): server side passes (S05). Client (S06): the open-role and propose-promise forms ask for a reason exactly when `permissions.needsReason` is true; `resources.cjs` covers the role form with needsReason true and false on a Complete project, `design-basis.cjs` the propose form with needsReason true and false on an Active project. |
| AC-FIG-11 | S05, S06 | PASS (local): server side passes (S05). Client (S06): the role label maxlength comes from `/reference` `limits.roleLabel` instead of a hard-coded 80; `resources.cjs` checks maxlength 80. |
| AC-FIG-12 | S12, S13 | PASS (local): `PlanningApiTests.Kept_entries_and_rows_survive_filters_inside_visibility` and `Grid_ignores_keep_while_flag_off`. Prior source 1b649f5 Planner browser covers kept-row reset; current routes do not replace that interaction evidence. Flag retirement remains gated. |
| AC-FIG-13 | S08, S11 | PASS (local): enforced ratchets in `tools/ratchet.json`, load literals 0/0 and page permission derivations 2/2 (`python3 tools/ratchet.py`); five ratchet self-tests including injected failure controls pass (`python3 tools/test_ratchet.py`, rerun 2026-10-25). |

## Build 041-S01

Merged build/041-S01 (0496d662) into claude/build-039-050 with no conflicts. On the merged tree: `dotnet build Hub.slnx` 0 errors; WorkloadTests, DomainHelpersTests and the Setting and Admin tests 75 passed, 0 failed; `web` `npm run build` and `npm run lint` pass. Builder ran the full suite on the slice branch: 1160 passed, 0 failed. No browser suite run (no page behaviour change); coverage gate not run. AC-FIG-07 (overWeek) is S02 and stays UNRUN.

Assumptions (defaults from specs/DECISIONS.md):

- 041-D4: band names Over, Near, Within and Light; thresholds over 110, amber 90, under 40.
- 041-D9: the three threshold settings ship unflagged as a Tier B correction; no flag, no migration (startup seeding creates the setting rows).
- RPT-09: 90 % is the default amber setting.
- Setting labels 'Workload over threshold (%)', 'Workload near threshold (%)' and 'Workload light threshold (%)' were chosen by the builder; the packet gives no wording.
- The API still calls OverAssigned and UnderAssigned with default thresholds; wiring settings into the Workload response is S02.

Left for Jay: nothing for this slice.

## Build 041-S02

Merged build/041-S02 (be9fa124) into claude/build-039-050 with no conflicts. On the merged tree: `dotnet build Hub.slnx` 0 errors; Workload, Supervisor and FoundationsTests 63 passed, 0 failed; `web` `npm run build` passes and `npm run lint` exits 0 (warnings only, in other files). Builder ran `PORT=5291 node tests/resources.cjs` on the slice branch 5 times in a row with 0 axe violations (it also now waits for the Method popover to close before the axe scan); the browser suite was not re-run on the merged tree.

Assumptions (defaults from specs/DECISIONS.md):

- 041-D4: Over above 110 %, Near above 90 %, Within from 40 %, Light below.
- 041-D9: `band`, `overWeek`, the thresholds and the legend line ship unflagged (Tier B); `flag.figure_names` is not used, so the page is unchanged apart from the added legend line in the Method popover.
- `band` is sent as a lowercase string (over, near, within, light); the API has no enum converter.
- The legend gains one sentence (`workload.legend.bands`) naming all three thresholds; `workload.legend.over` keeps its wording with 110 filled in from the setting.
- Over-assigned and Under-assigned now read `workload_over_pct` and `workload_under_pct`; the export is unchanged.

Left for Jay: Tier B review. Nothing needs GitHub, homedev or sudo.

## Build 041-S03

Merged build/041-S03 (29c14945) into claude/build-039-050 with no conflicts. On the merged tree: `dotnet build Hub.slnx` 0 errors; full `dotnet test Hub.slnx` 1200 passed, 0 failed; `web` `npm run build` and `npm run lint` pass. Builder ran `resources.cjs` (3 runs) and `supervisor-capacity.cjs` on the slice branch with 0 axe violations with the flag off and on; browser suites not re-run on the merged tree. Not run: coverage gate, `npm run test:planner`, ui-review, calm-acceptance.

Assumptions (defaults from specs/DECISIONS.md):

- 041-D1: the Workload figure is named "Uncommitted".
- 041-D3: Workload keeps today's partial rule for every row (viewer cannot see every Restricted project, or a project filter is applied).
- 041-D9: only the new name and the basis lines are behind `flag.figure_names`; the server returns `uncommitted` whether the flag is on or off.
- The flag is declared as `FlagDef("figure_names", "041")`, the first declared flag; `FlagsTests` and `FlagsApiTests` now check that only declared flags are flag settings and each starts off.
- `uncommitted` is computed from the 1-decimal rounded available and committed values, matching the previous client computation (no -0).
- The wording of `workload.method5Named` was chosen by the builder.
- A Partial view figure in the picker and My Staff keeps its PLN-15 tooltip; only non-partial figures get the new basis tooltip.
- The flag-off "no at most wording" check is on the Workload page; the Planner's "at most" wording belongs to S04.

Left for Jay: turning on `flag.figure_names` in review and pilot, which S13 depends on. Nothing needs GitHub, homedev or sudo.

## Build 041-S04

Merged build/041-S04 (9e10d410) into claude/build-039-050 with no conflicts (en.ts auto-merged). On the merged tree: `dotnet build Hub.slnx` 0 errors; `dotnet test` filtered to PlanningApiTests, WorkloadApiTests and FlagsTests 29 passed, 0 failed; `web` `npm run build` and `npm run lint` pass (warnings only, in files this slice did not touch). Builder ran `planner.cjs` (15 checks, 0 axe violations, including planner-grid-partial-row and contribution-partial) and `supervisor-capacity.cjs` on the slice branch; browser suites not re-run on the merged tree. No server code changed; one new i18n key, `planner.atMost`.

Assumptions:

- No Open decision in specs/DECISIONS.md is named by this slice, so no default was needed.
- The row label's tooltip and accessible description reuse the existing `planner.partialHint` text as the PLN-15 text.
- The picker and My Staff both read through `usePeopleCapacity` (planning/grid with personIds), so the batch-read route in the test covers both readers.
- "at most" wording also applies to a negative remaining value; it is still an upper bound.

Left for Jay: turning on `flag.figure_names` in review and pilot, then the S13 flag cleanup. Nothing needs GitHub, homedev or sudo.

## Build 041-S05

Merged build/041-S05 (c66964df) into claude/build-039-050 with no conflicts. On the merged tree: `dotnet build Hub.slnx` 0 errors; PermissionMatrixTests, PermissionSweepTests, OpenRoleApiTests, DomainHelpersTests, ProjectsTests, ReviewChangeTests, AllocationApiTests and Reference tests 217 passed, 0 failed. Builder ran the full suite on the slice branch (1163 passed, 0 failed) and `dotnet ef migrations has-pending-model-changes` (none). No web change, so no web build, lint or browser suite.

Assumptions (defaults from specs/DECISIONS.md):

- 041-D1 to 041-D9 at their defaults; per 041-D9, S05 ships unflagged as a Tier B server change.
- The three extra `NeedsReason` flags in Deliverables.cs, Tasks.cs and Timeline.cs also use `ProjectStatus.NeedsCorrectionReason` (FR-FIG-11 asks for one copy of the rule; behaviour unchanged).
- The P-05 function lives on `ProjectStatus` in Vocabulary.cs, next to `IsReadOnly` and `IsLive`, rather than on `Permissions`.
- `OpenRoleLimits.Label = 80` feeds the HubDb CHECK through an interpolated string with identical SQL text, so no migration.

Left for Jay: Tier B review. The client half of AC-FIG-10 and AC-FIG-11 is slice S06.

### Build 041-S06

Merged build/041-S06 (d2052572) into claude/build-039-050 with no conflicts. On the merged tree: `dotnet build Hub.slnx` 0 errors; `npm run build` passed; `npm run lint` no errors (existing warnings only); `node tests/resources.cjs` and `node tests/design-basis.cjs` passed with no errors and no unmocked calls. Builder also ran OpenRoleApiTests (9 passed).

Assumptions:

- S06 has no flag in tasks.md or plan.md, so it ships unflagged; the client reads values the server already sends (S05).
- Until `/reference` has loaded, the role label input has no maxlength; the server still enforces 80 characters.
- No open decision in specs/DECISIONS.md applied to this slice.

Left for Jay: nothing for this slice.

## 2026-10-18 isolated takeover: S07, S09–S12

Locally implemented on `codex/capacity-planning-041-047` from `a6e0104b`. Recovered the saved b-041-S07 and b-041-S12 tracked patches, then completed project permission flags, design-basis discipline permissions, coordination row/create flags, task row flags and kept-row request state. All TaskQueries callers pass the actor; list flags include the creator edit window and assignee supervisor facts. Kept ids reset on filter change and do not enter saved URLs or exports. No review/pilot flags enabled and no Accepted status assigned.

| Check | Result / evidence |
|---|---|
| Release solution build | PASS, 0 errors (`/private/tmp/capacity-release-build.log`) |
| Focused pure-domain regression | PASS, 132 passed / 0 failed / 0 skipped; OnboardingAndRouteCounterTests, TemplatePlannerTests, WorkloadTests, PermissionMatrixTests; `/private/tmp/capacity-041-047-test-results/capacity-domain-final.trx` |
| Web build and lint | PASS; lint warnings remain (`/private/tmp/capacity-webbuild-final2.log`, `/private/tmp/capacity-weblint-final2.log`) |
| Mocked browser suites | PASS: resources, planner, supervisor-capacity, design-basis, coordination and handoffs. Final resources includes task-week picker/version-conflict/scoped-link checks; planner includes 17 empty axe checkpoints. |
| Independent source review | Latest reread: no open actionable P1/P2 in reviewed permission, kept-state, Workload and context paths. Source review is not API execution. |
| Specification / traceability | PASS: generated specification current; 1,299 IDs and 405 sections, no gaps (`/private/tmp/capacity-spec-check.log`, `/private/tmp/capacity-trace-check.log`) |
| API permission/parity tests | Authored and compiled; BLOCKED at fixture startup by missing combined 047 migration / EF PendingModelChangesWarning. Not suppressed. Integrator owns combined API run. |
| S08 ratchet / generated contract | BLOCKED on packet040 owner integration. This lane has no ratchet files. Publish load-percentage literal baseline0 and failing permission-derivation counter; final references to permissions.isPm/leadOf under web/src are0. Integrator must refresh generated response contracts. |
| Full suite / coverage gate | Not run in this isolated lane; integrator owns once combined schema/source lands. No 95% branch-coverage claim. |

The first final .NET attempt raced Vite replacing generated static assets; sequential rebuild/domain verification passed. The initial supervisor browser attempt used occupied5178; free5198 passed. Neither was accepted as an application failure. S07/S09–S12 remain pending combined verification and Tier B review; S08 and S13 are not complete. Human review, pilot decisions and cleanup remain gated.

Final companion: Planner's20flows and17axe checkpoints PASS after the optional-context guard; the exact capacity-polish context fixture and preserved kept-row scenarios share that suite. Final new API test-source Release compile PASS0errors; normalized report links and the corrected cross-project list totalCount are compiled but runtime pending combined schema. Companion browser/CI changes are published after core sourceca18b37; see lane handoff.

## Acceptance follow-up (2026-10-19)

The missing six-role by six-status endpoint matrices now pass for basis creation/naming another owner, weekly commitment proposals and readiness applicability on tasks and deliverables, task assign/edit/due changes, superseding an issued submission, source/review creation, and change-assessment management. These use valid requests and require successful responses or explicit 403 refusals. Existing `Project_permission_flags_match_their_endpoints_for_every_role_and_status` covers decisions 1, 2, 3, 5, 6 and 9. New matrix test files name their remaining decision IDs.

The basis matrix found a real false-positive `assignBasisIn` flag: its PM/lead shortcut ignored the write gate. Source commit `566fe51` computes it through `Permissions.CreateBasis` for another owner, matching the endpoint. The focused basis run passes (`ci-artifacts/integration/acceptance-followup/basis-scale-fixed.trx`). Task/submission matrices pass in `matrix-root.trx`; both coordination matrices pass in `tests/Hub.Tests/TestResults/coordination-matrix.trx`. Final combined regression passes all 1,514 tests. The six diagnostic matrix tests also pass in `ci-artifacts/integration/acceptance-followup/permission-differences.trx`; the durable difference receipt records test identities and outcomes.

## 2026-10-25 gap run

AC-FIG-04 was the only PARTIAL or UNRUN row. Its two missing local parts, a browser check of the person picker's free-hours figure and a current Planner run showing the basis line unchanged, were added to the existing mocked suites as `c56fb129` (test-only, no product code; `flag.figure_names` stays off by default and is switched on only inside the mocked suites) and merged into `codex/integrate-039-050`. The first picker axe scan reported a colour-contrast hit read mid-fade of the popover's entry animation; a scan after the animation was clean, so the helper now waits for the animation to finish before scanning, as `planner.cjs` already does.

Rerun on the merged tree (2026-10-26, Chromium headless shell 1248, mocked API): `PORT=7218 node tests/resources.cjs` passed (errors [], unmockedGets [], desktop/named/picker/panel violations []); `PORT=7219 node tests/planner.cjs` passed (flowCount 20, 17 axe checkpoints with 0 violations, errors [], unknownGets []); `SUPERVISOR_CAPACITY_TEST_PORT=7220 node tests/supervisor-capacity.cjs` passed (violations []). `dotnet build Hub.slnx` 0 errors; `dotnet test` filtered to WorkloadApiTests, PlanningApiTests, Domain.WorkloadTests and FlagsTests 44 passed, 0 failed, 0 skipped; `npm run build` and `npm run lint` (exit 0, existing warnings only) pass.

Still open: a real-API picker run seeded with the AC-FIG-03 person-week ("24 h free"), S13 flag cleanup once Jay turns on `flag.figure_names` in review and pilot, Tier B review, and hosted, pilot and human checks. No packet is Accepted.
