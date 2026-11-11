# Final census: 052 S02, S03, S06 and S09 · 2026-11-11

Lane b-052053-census. Worktree detached at `65cbcd64` plus uncommitted, `:root[data-visual="v2"]`-scoped CSS fixes
(listed below). Real local Development API (Debug build of `65cbcd64`) on a disposable database `hub_census_main`,
fixtures from `web/tests/visual-census-real.cjs` (SYN-101..103) plus `web/tests/visual-acceptance-real.cjs`
(SYN-104 with one task in every bucket and one On Hold, SYN-120 with a 120-character name, one approved 8 h leave day for
Alex). All eight 052/053 flags on unless stated. Synthetic data only; automated measurement, not hosted or human
acceptance; the 800 px/DPR 2 run is a reflow equivalent, not native 200 % browser zoom.

Raw evidence: `final-visual-census.json` (17-screen census off/on, the acceptance probe, the §41 `clutter.cjs` output
off/on and the flag-gated response fields) and `final-reflow-census.json` (68 reflow observations).

## Verdict per acceptance criterion (local, synthetic)

| AC | Verdict | Evidence |
|---|---|---|
| AC-VSN-01 | PASS | Home, project Tasks, Workload and Planner at 1440×900: no chromatic fill, border or text colour outside the §56 tokens; body text IBM Plex Sans, keys and mono figures IBM Plex Mono; 0 font requests off-origin (census, probe and reflow). Across all 51 probed routes the only non-token colours are the 8 stored discipline-colour swatches on `/admin` (data values, see open items). |
| AC-VSN-02 | PASS | 30 count cards on Home, Portfolio, two project Dashboards, Time and My Staff: figure colour by metric (blue, violet, red, amber, green, teal), icon chip strong-on-tint, red figure ink at 0 and red with "■ Needs attention" above 0 (Home and SYN-101 Overdue = 2). |
| AC-VSN-03 | PASS | SYN-104 drives Home Tasks by Status (To Do #94a0b4, In Progress #6a52d1, Review and revision #f0a42b, Done #2f6fe0, On Hold hatched, each named with its count in the legend), Tasks by Project bars and legend, workspace and project Board column edges; planner and Vacation cells with leave show the hatch beside "Time off 8 h". 37 lane/hatch marks, 0 wrong. |
| AC-VSN-05 | PASS | SYN-120 (120 characters) on Dashboard and Tasks: 1440 px header 64 px, number in Plex Mono, name one line with ellipsis (837 px shown), status and health pills inside, meta line whole; 375 px header 72 px with no child below it, name 269 px shown (was 44 px), Project Manager name whole (54/54 px, was 29/54), Client truncates first, Target date whole, no sideways scroll. Focus on the name shows the full name in its tooltip at both widths. |
| AC-VSN-06 | Not re-measured here | S05, closed earlier. Title blocks were present on every computed screen probed. |
| AC-VSN-07 | PASS | Every ordinary card is one link with no nested control; its destination row count equals the figure (sum-type Staff cards equal the column sum their where-line names); Home On Track is an article with two separate links (1 Green → 1 row, 4 evaluated → 4 rows) and no nesting (052-D11); the definition shows on focus for 30/30; accessible name = label, figure, where line for 30/30; Enter on Home's Overdue Tasks opens `/tasks?overdue=true` with 2 rows = 2. Cards per row: 6 at 1440 (Staff 5), 3 at 1000, 2 at 375 on Home, Dashboard and Staff, equal sizes; Portfolio shows its desktop/tablet notice at 375 (§13.0 phone scope). |
| AC-VSN-11 | PASS except one open census finding | 17 screens × 2 roles: 0 serious/critical axe issues flags on (and off); rendered text-contrast census over 51 routes: 0 text under 24 px below 4.5:1 and 0 larger text below 3:1 (one text over the On Hold hatch is unresolved by the script; ink on the hatch stripes is 6.59:1 and 12.41:1); every Tasks by Project segment is named "SYN-104 In Progress: 1 tasks". |
| AC-VSN-12 | PASS | Flags off: 17-screen census 0 serious/critical axe, all first rows ≤ 405 px; `clutter.cjs` flags off PASS (AC-CALM-01 to -05 and six-persona sidebar); no response carries `identityColour`; the only `asOf` with flags off is Home's, present at `ac2fa894` (`HomeRead.AsOf`). Every change in this lane is CSS under the v2 root attribute, so flags-off DOM and text are unchanged by construction. |

S02 band order (FR-VSN-11) over 51 routes: 49 open with the navy band (or navy project header and navy tab row),
one h1 inside it, at most one subtitle line, actions on the right, title block in the band before the tab row, and
count cards overlapping the band by 72 px on Home, Portfolio, My Staff and the project Dashboard. Exceptions are listed
under open items. Text-input boundaries (FR-VSN-13): 203 visible fields, all identified at 3:1 or more (190 by the
`--input` line or fill, 13 inline cell selects identified by their native arrow and transparent by design).

## Defects fixed (all v2-scoped CSS)

| Defect (before) | Fix | After |
|---|---|---|
| 375 px project header: name cut to 44 px ("Syn…"), Project Manager name cut to 29 of 54 px (FR-VSN-12) | `visual-layout.css`: below 768 px number and name share row 1, status/health/details row 2, meta row 3 with the PM span kept whole (max 55 %) and Client shrinking first; details button drawn 32×24 with a 44 px hit area | 72 px, name 269 px, PM whole |
| Three selects with the decorative hairline as their boundary, 1.2:1 (Preferences digest frequency and day, task page activity filter) (FR-VSN-13) | `visual-layout.css`: native fields in `main` use `--input` except disabled, invalid and transparent inline editors | all 203 fields ≥ 3:1 or arrow-identified |
| Planner, Team Planner month and Vacation figures in teal at 19 px: 4.12:1 on white, 3.91:1 on the current-week tint (FR-VSN-21, AC-VSN-11) | `planner-semantic.css`: the 19 px figure is ink; the teal Confirmed band keeps the hue | 0 contrast failures |
| Legacy derived accents in the colour inventory: identity rings and card stripes `#e3d29d #ecc3a9 #aacfb9 #b6c9ed #cbbce9` on Boards, My Work, Portfolio, Projects, Preferences and the coordination cards (FR-VSN-04/09) | `index.css`: under v2 the stripe/ring utilities and accent chips (`[data-accent].text-(--acc-fg)`) take `--line-3`/`--ink-2`/`--raised`; count tiles awaiting migration and decorated avatars keep their own rules | 0 non-token colours outside Admin |
| Translucent utilities mixing new hues: priority badge borders `border-warn/30`, `border-bad/30`; today/unread `bg-accent/40`; danger zone `border-bad/40`; callout borders; baseline marker | `index.css`: each resolves to its token role (`--border`, `--brand-tint`, `--brand`, `--ink-3`, pill tints) | gone from the census |

Known items from earlier checkpoints: Workload discipline-table keyboard scroll focus and Dashboard task-name clipping
under text spacing are closed (reflow 68/68, 0 serious axe, 0 clipped controls). My Work's first task row is 322 px
flags on and 348 px off (≤ 405); its chrome words are 28/40 (Priya) and 40/40 (Sam) flags off, but 67–70 flags on
(open, below). Legacy accent colours are closed as above.

## Open items (not fixed here)

1. **Calendar flags on** (work_screens, FR-SCR-24): the always-open filter card puts the first row at 417 px
   (AC-CALM-01), shows Person and Discipline selects (2 fields, AC-CALM-02) and 28 controls (AC-CALM-03), both roles.
   It is the only finding of `visual-census-real.cjs` (exit 1). New since checkpoint 17 (277 px). Repair belongs to the
   Calendar owner: move the type chips, Time away, Person and Discipline into the existing `FilterBar` popover with
   active chips; `tests/calendar-screen.cjs` drives the inline controls and would need the popover opened first.
2. **Team flags on**: Search plus a visible Group by select = 2 fields (AC-CALM-02); the Group control belongs behind
   §41 R1's Group button (people_screens owner).
3. **Chrome words flags on** (AC-CALM-05 word half, unchanged by FR-VSN-09) exceed the budget on Home 98/41, My Work
   67–70/40, Portfolio 75/40, Planner 50/40, Reports 233–235/40, Workload (Sam) 90/42, Calendar (Sam) 81/78, project
   Tasks 58/54, Deliverables 57/55 and Allocations 119–139/49; controls flags on exceed 25 on Home (28–32), Deliverables
   (27) and Coordination (26–27). The words come from §57 content (Home filter row and charts, group summaries,
   report descriptions, allocation explanations); no content was removed (features are additive). Needs a decision
   on the §41 word budget for §57 screens, or per-screen restructuring by their owners.
4. **`/tasks/:id`** (the task panel as a page) opens without the band; FR-VSN-11 says every screen opens with it.
   Left as a panel-page question.
5. **My Staff** puts its title block beside the title in the actions column (deliberate density choice, still in the
   band before the tabs).
6. **Admin reference data** shows stored discipline colours as swatches (8 non-token colours, data values the
   administrator edits).
7. `clutter.cjs` flags on also reports 22 exceedances that the open 052 defaults replace: project group tabs 8 > 7
   (052-D4: six groups plus Activity and Settings links), sidebar 10–12 > 8 (052-D3: eight items plus up to four in the
   foot group; navigation lane) and fills 7–11 > 6 (052-D2: token census, non-token target 0, met).
8. Timeline's dashed "Start" marker is `border-foreground/40` (ink at 40 %): grey when composited, so not a hue by the
   §41 rule, but the reflow inventory lists it as unmatched.

Flags-off findings: none. All measured flags-off output is unchanged from `65cbcd64`; no `ac2fa894` build was run
in this lane, so identity with the live release rests on the integrator's record-parity check.
