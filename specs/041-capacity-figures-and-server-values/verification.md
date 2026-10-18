# Verification: Capacity Figures and Server Values

Slice S01 merged into claude/build-039-050; other slices not started.

| AC ID | Slice | Status |
|---|---|---|
| AC-FIG-01 | S04, S13 | UNRUN |
| AC-FIG-02 | S04 | UNRUN |
| AC-FIG-03 | S03, S13 | UNRUN |
| AC-FIG-04 | S03, S13 | UNRUN |
| AC-FIG-05 | S01, S02 | PASS (local): domain edges 39/40/90/91/110/111 % (S01, `WorkloadTests.Load_bands_follow_settings_and_default_to_todays_shades`); API cells carry `band`: 100 % Near, 115 % Over, no-availability with committed hours Over (`WorkloadApiTests.Cells_carry_bands_over_week_and_thresholds_from_settings`); `resources.cjs` keeps every fixture cell's shade (130, 156, 95, 50, 0 %). |
| AC-FIG-06 | S01, S02 | PASS (local): with amber 80 and over 120, 100 % and 115 % are Near, Over-assigned is false and the response thresholds are 120/80/40; restoring defaults restores bands (`Cells_carry_bands_over_week_and_thresholds_from_settings`). Each change writes one activity entry; 49, 301 and 101 and a non-Admin change are refused (`Workload_threshold_changes_are_audited_and_validated`). Legend renders the server thresholds; the browser check covers the defaults (110, 90, 40 %). |
| AC-FIG-07 | S02 | PASS (local): 100 % this week and 115 % next week give `overWeek` 2026-09-21 (`Cells_carry_bands_over_week_and_thresholds_from_settings`); `resources.cjs` checks the Over-assigned chip names the server's week. |
| AC-FIG-08 | S07, S09, S10, S11 | UNRUN |
| AC-FIG-09 | S05 | PASS (local): `PermissionSweepTests.Open_role_changes_follow_the_owner_rule` and the `PermissionMatrixTests` theory `Open_role_changes_need_the_proposal_gate_and_pm_or_creator` (role created by a Discipline Lead: PM and creator allowed, another Discipline Lead gets `perm.owner`, Team Member gets the proposal refusal, all refused on Archived); the row's `canManage` calls the same `Permissions.ManageOpenRole`; OpenRoleApiTests unchanged and passing. |
| AC-FIG-10 | S05, S06 | PASS (local): server side passes (S05). Client (S06): the open-role and propose-promise forms ask for a reason exactly when `permissions.needsReason` is true; `resources.cjs` covers the role form with needsReason true and false on a Complete project, `design-basis.cjs` the propose form with needsReason true and false on an Active project. |
| AC-FIG-11 | S05, S06 | PASS (local): server side passes (S05). Client (S06): the role label maxlength comes from `/reference` `limits.roleLabel` instead of a hard-coded 80; `resources.cjs` checks maxlength 80. |
| AC-FIG-12 | S12, S13 | UNRUN |
| AC-FIG-13 | S08, S11 | UNRUN |

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
