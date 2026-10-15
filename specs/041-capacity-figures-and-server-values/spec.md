# Feature Specification: Capacity Figures and Server Values

**Packet**: 041 | **Feature directory**: `specs/041-capacity-figures-and-server-values` | **Date**: 2026-10-14
**Status**: Specified, not started.
**Input**: Jay's 2026-10-14 approval of the build-approach report (kept outside the repository): "We are building everything you suggested." This packet takes its server-computed values and its roadmap items 3, 4, 5 and 15. Current features are fixed and additive only.
**Source**: product specification §45 (§45.1 to §45.10) in `spec-parts/20-capacity-figures-and-server-values.md`. It builds on §39 (PLN-09, PLN-15, FR-PLN-16, FR-PLN-18, FR-PLN-19, AC-PLN-11, AC-PLN-12), §40.6 (FR-PICK-01, FR-PICK-02), §42 (FR-SUP-01, FR-SUP-02, FR-SUP-03, AC-SUP-05), §37.6 (FR-CAP-04, FR-CAP-06, AC-CAP-04), §40.2 (FR-ROLE-01, FR-ROLE-02), §40.5 (FR-GRP-02), §12.15, §13.11, §10.4, §10.9, §8.10, P-05 and G-09. It amends AC-PLN-12, FR-ROLE-02, §12.15 step 3, §13.11 and §39.12 through the amendment requests returned with this packet.

## User outcome

A Supervisor can trust a capacity number. The Weekly Planner says on the person's own row when hidden work makes a figure an upper bound. Workload and the Planner no longer call two different figures "Remaining", and each figure says in one line how it is worked out. Workload's red, amber and calm shading comes from rules an Admin can see and set, so the amber band is no longer a hidden client rule. A button the server would refuse says why instead of failing after a click. An entry edited under a filter no longer vanishes mid-edit.

## Scope

1. **Honest free hours under partial visibility** (§45.2: FR-FIG-01, FR-FIG-02). The core fix already shipped: packet 038's approved correction counts hidden Confirmed allocations in the partial set (`src/Hub.Api/Features/Planning.cs:208-221`). The Planner grid still shows a partial person's figure with only a page notice. This packet adds the "Partial view" label on the person's row, states their remaining capacity as "at most N h", pins the Planner, picker, My Staff and Workload to one API fixture, and amends AC-PLN-12 to say so.
2. **One name and one basis for each free-hours figure** (§45.3: FR-FIG-03, FR-FIG-04). Both figures and their values stay (AC-PLN-11). The Planner's remaining capacity keeps its canonical name (§10.9). Workload's "Remaining N h" (`web/src/pages/Workload.tsx:303-304`) becomes "Uncommitted N h", computed on the server. Each figure gets a one-line basis.
3. **Load bands from the server** (§45.4: FR-FIG-05, FR-FIG-06, FR-FIG-07). One Hub.Domain band function returns Over, Near, Within or Light per Workload cell. Three new settings, `workload_over_pct` 110, `workload_amber_pct` 90 and `workload_under_pct` 40, replace the client literals (`Workload.tsx:47, 57-58`) and the domain constants (`src/Hub.Domain/Workload.cs:14`). The defaults reproduce today's shading and indicators exactly, and the 90 % band becomes a stated rule.
4. **Permission flags in place of client role checks** (§45.5: FR-FIG-08, FR-FIG-09). Thirteen client decisions built from `isPm`, `leadOf` or `manageDisciplineIds` read server flags computed by the function each endpoint enforces.
5. **One copy of each duplicated rule** (§45.6: FR-FIG-10, FR-FIG-11, FR-FIG-12). The open-role owner rule moves into `Permissions.ManageOpenRole`. The P-05 correction rule becomes one function used by four server sites, and the client reads `permissions.needsReason`. The role-label limit becomes one constant, sent to the client through `GET /reference`.
6. **Planner rows after an edit** (§45.7: FR-FIG-13, FR-FIG-14). Edited entries and their rows stay on screen, dimmed and labelled, until the filters change or the page reloads.
7. **Guard against new client copies** (§45.8: FR-FIG-15). Two counters in the §44.6 ratchet.

§45.1 sets the boundary, §45.9 holds the acceptance criteria and §45.10 the decisions.

## Non-goals

- One shared free-hours figure; that would reverse AC-PLN-11.
- Any new capacity rule, indicator or threshold beyond the three Workload settings.
- Showing hidden hours, including an "Other project work N h" total (D2).
- A per-person partial rule on Workload (D3).
- Any change to an export, report or saved view.
- Removing or renaming response fields, including `isPm`, `leadOf` and the three unused threshold fields on `/me`.
- The status-transition reason rule in `ProjectSettings.tsx:205`, deferred.
- Typed contract, registry and ratchet machinery, which belong to §44.

## Decisions (defaults apply until Jay decides)

| # | Decision | Default |
|---|---|---|
| D1 | Workload figure's name | "Uncommitted" |
| D2 | Hidden work in free hours | Partial view label and upper-bound figure; no hidden-hours line |
| D3 | Workload's partial rule | Today's, for every row |
| D4 | Band defaults | Over above 110 %, Near above 90 %, Within from 40 %, Light below |
| D5 | Control whose flag differs from today's client rule | Offer when allowed; keep visible and disabled with the server's reason when refused |
| D6 | Kept Planner rows | Until filters change or reload; at most 50 ids; counted in totals |
| D7 | Guard location | The §44.6 ratchet |
| D8 | Exports | Unchanged |
| D9 | Flags and unflagged corrections | `flag.figure_names` and `planner_kept_rows`; threshold settings and disabled controls unflagged, Tier B |

## Acceptance

- Honest free hours: AC-FIG-01, AC-FIG-02.
- Names and basis: AC-FIG-03, AC-FIG-04.
- Load bands and settings: AC-FIG-05, AC-FIG-06, AC-FIG-07.
- Permission flags: AC-FIG-08.
- One copy of each rule: AC-FIG-09, AC-FIG-10, AC-FIG-11.
- Kept Planner rows: AC-FIG-12.
- Guard: AC-FIG-13.

Existing tests named in §45.9 must pass unchanged, including the AC-PLN-11 and AC-SUP-05 regressions and `OpenRoleApiTests`.

## Depends on

- Packet 038, built and merged: the shared partial rule and the My Staff column.
- Packet 039: the flag list of §43.4, for slices S03, S04 and S12. Slices touching `Permissions.cs` or `.github/**`, and the unflagged visible corrections of D9, are Tier B under §43.11.
- Packet 040: the §44.6 ratchet, for slice S08 only. When the §44.4 golden `openapi.json` and shape snapshot exist, each slice that adds a response field regenerates them in the same pull request, and the diff shows additions only.
- No other packet depends on this one, but any later screen that shows a free-hours figure uses its §45.3 name and basis.
- Packet 043: if it converts Reviews and Changes (`CoordinationRegister.tsx`), whichever of that conversion and S10 lands second carries the other across, and the conversion's parity run runs with each flag of this packet off and on (§47.6).

## Flag

`flag.figure_names` (§45.2 row label and upper-bound wording, §45.3 names and basis lines) and `planner_kept_rows` (§45.7). With both off, every label and screen is today's. Bands, server values, permission flags and rule consolidation ship unflagged because they change nothing visible at the default settings. The three threshold settings and controls kept visible and disabled with the server's reason are unflagged corrections (D9) in Tier B slices. Slice S13 removes both flags once Jay has turned them on in review and pilot.

## Wording rule

Code, interface strings, specification text, commit messages and this packet's records name no third-party product.
