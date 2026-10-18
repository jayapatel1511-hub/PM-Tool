# Verification: Capacity Figures and Server Values

Slice S01 merged into claude/build-039-050; other slices not started.

| AC ID | Slice | Status |
|---|---|---|
| AC-FIG-01 | S04, S13 | UNRUN |
| AC-FIG-02 | S04 | UNRUN |
| AC-FIG-03 | S03, S13 | UNRUN |
| AC-FIG-04 | S03, S13 | UNRUN |
| AC-FIG-05 | S01, S02 | PARTIAL: domain part passes (S01, `WorkloadTests.Load_bands_follow_settings_and_default_to_todays_shades`: 39, 40, 90, 91, 110, 111 % give Light, Within, Within, Near, Near, Over; 999 % no-availability is Over). Per-cell band from the API and the resources.cjs shade check are S02. |
| AC-FIG-06 | S01, S02 | PARTIAL: domain part passes (S01). Amber 80 and over 120 make 100 % and 115 % Near, Over-assigned fires only above 120 % (`WorkloadTests.Over_and_under_assigned_read_their_settings`); over 49 and amber 301 refused, defaults and ranges hold (`DomainHelpersTests.Workload_threshold_settings_have_defaults_and_ranges`); existing WorkloadTests unchanged. Legend wording and the audited-change API test are S02. |
| AC-FIG-07 | S02 | UNRUN |
| AC-FIG-08 | S07, S09, S10, S11 | UNRUN |
| AC-FIG-09 | S05 | UNRUN |
| AC-FIG-10 | S05, S06 | UNRUN |
| AC-FIG-11 | S05, S06 | UNRUN |
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
