# Verification: Visual System and Navigation

Specification drafting completed on 2026-10-31. The dark build is complete locally (S01–S09, 2026-11-11; S10 follows pilot activation); local implementation evidence appears below. Open pilot/design decisions remain Open.

## Specification checks run on 2026-10-31

The final local specification checks below were run during completion of the draft. They do not constitute runtime acceptance.

| Check | Result |
|---|---|
| `python3 tools/build_spec.py` then `python3 tools/build_spec.py --check` | PASS: combined Markdown and HTML rebuilt with pandoc and up to date |
| `python3 tools/trace_spec.py` then `python3 tools/trace_spec.py --check` | PASS: 1,545 IDs and 438 sections; zero uncited or unknown IDs in the combined specification |
| Every ID referenced in §56 exists in the specification or is defined in §56 | PASS |
| No other company's product name in the files this packet adds or changes | PASS |
| Contrast of the §56 tokens computed with the WCAG 2.1 relative-luminance formula | PASS: `#646d81` is 5.19:1 on white and 4.64:1 on `--side`; strong metric hues on white are Blue 4.70:1, Violet 5.61:1, Red 4.74:1, Amber 3.30:1, Green 4.00:1 and Teal 4.12:1; status-pill text on its stated tint is 5.66:1–6.48:1; `#9bb8ff` on `--navy` is 7.19:1. FR-VSN-21 therefore limits smaller coloured text to pairings that pass 4.5:1 and treats the other strong hues as large figures or non-text marks. |

These are token-pair calculations, not an implementation or browser audit. Final acceptance must measure the actual rendered foreground/background pair, including overlays, hover/focus states and responsive layouts.

## Acceptance

| AC ID | Area | Slice | Status |
|---|---|---|---|
| AC-VSN-01 | Token census, fonts, no third-party font request | S01, S09 | PASS locally 2026-11-11: fonts self-hosted; rendered token census 0 non-token fills outside Admin discipline data |
| AC-VSN-02 | Count-card colours by metric | S03 | PASS locally 2026-11-11: count-card figure colours by metric on every count-card screen |
| AC-VSN-03 | Lane colours and hatch | S03 | PASS locally 2026-11-11: lanes and hatch across 37 lanes in the acceptance probe |
| AC-VSN-04 | Project identity colour | S04 | PASS locally with synthetic API/browser data; no pilot/human acceptance |
| AC-VSN-05 | One-line project header within 72 px | S02 | PASS locally 2026-11-11: band order on 51 routes; header 72 px with a 120-character name, desktop and 375 px, with and without a health override; inputs 3:1 |
| AC-VSN-06 | Title block from the response time | S05 | PASS locally: response clock, organisation zone, shifted browser clock |
| AC-VSN-07 | Count card link and definition | S06 | PASS locally 2026-11-11: 30 count cards each one link equal to its list; On Track two links (052-D11); definition on focus |
| AC-VSN-08 | Sidebar list and foot group per role | S07 | PASS locally 2026-11-11: route matrix 62 routes × 9 profiles; one highlight per content route, none on utility pages (052-D12) |
| AC-VSN-09 | Workspace and project tab rows | S07 | PASS locally 2026-11-11: workspace and project tab rows; Notifications and Admin tab rows in the band; former project routes load |
| AC-VSN-10 | Phone drawer and bottom bar | S08 | PASS locally 2026-11-11: drawer focus trap, Escape and return; bottom bar kept; 744 real page loads with no sideways scroll |
| AC-VSN-11 | Accessibility scan and contrast census | S09 | PASS locally 2026-11-11: 17 screens off/on 0 serious/critical axe, 0 contrast failures; reflow 68/68 |
| AC-VSN-12 | Flag off changes nothing | S01 to S09 | PASS locally 2026-11-11: flags-off census and route matrix match `ac2fa894`; record parity 13/13 |

At the specification-drafting checkpoint, all runtime checks were unrun. The later build evidence below supersedes that historical state; whole-packet acceptance is still pending.

## Build goal started · 2026-10-31

The user authorized dark implementation after the specification. Foundation S01 is now
implemented locally in `codex/build-052-053`, with the combined flag/API and actual-browser
checks recorded in [build-progress.md](../052-visual-system-and-navigation/build-progress.md).
All remaining slices and packet-wide acceptance remain pending. Existing Open decisions
were not confirmed and no pilot flag, commit, push or deployment occurred.

## Second local build checkpoint · 2026-10-31

S04 and S05 are implemented and locally verified; S02/S03 remain partial. The combined
40-test backend run, generated contract/registry checks, no-model-drift result, frontend
build/lint/type checks and five focused Chrome scripts are recorded in
[build-progress.md](build-progress.md). The header/buttons passed independent targeted
re-review. Remaining census, charts/cards, navigation, accessibility and pilot/human gates
are not covered by these checks. No field or flag was enabled in a live review instance.


## Third local build checkpoint · 2026-10-31

Home now uses the new count cards; sidebar/tab rows and phone menu/search are locally
implemented. Focused Chrome checks pass for tooltip/name/zero behavior, responsive cards,
capability-filtered rail profiles, actual parent/project tab links, Board empty/loading
states, Portfolio scope forwarding, phone focus and search completion. The full route,
contrast/token and accessibility censuses remain pending; the Vacation parent follows Jay’s explicit Time fallback for users without Workload
access. S02/S03/S06/S07/S08 are not reclassified as whole-packet acceptance.
See [build-progress.md](build-progress.md#third-batch--home-navigation-and-task-context-integration)
for exact evidence, retained limits and the On Track multi-link wording note.


## Checkpoint-thirteen real census baseline

A real API/Chrome 17-screen × PM/Supervisor × off/on sweep produced 68 observations.
The baseline is **FAIL / PARTIAL**, with actionable flag-on text contrast defects on
five routes and first-data positions over405px on six routes. Flag-off populated routes
had no serious/critical axe or first-row failures. My Work has no assigned fixture work
for those two personas and remains unmeasured. No page errors or external font requests.
Raw JSON and interpretation are saved under evidence/checkpoint13-visual-census.json and
evidence/checkpoint13-visual-findings.md. The one-list/tab/fill raw counts must also be
compared with §56/open decisions, rather than used to hide required destinations.
Census QA API/database were stopped/dropped. S09 remains unchecked; no whole-packet
accessibility/contrast/token, zoom/text-spacing or hosted/human acceptance follows.


## Checkpoint-fourteen real census

All six baseline flag-on page-position defects and the measured text-contrast defects
are fixed. Final compiled-state evidence has 68 observations with zero serious/critical
axe violations, uncaught page errors or external font requests. My Work now has actual
assigned task fixtures: first task row is 558px off and 518px on, exceeding405px in both
modes. The legacy sweep also records43 chrome words above its40-word limit there. These
remain explicit S04 work. Full S09 still needs rendered token census, zoom/text-spacing
and all newly enabled part states; raw colour/tab/navigation counts do not decide the
open proposals. Evidence: evidence/checkpoint14-visual-census.json and
evidence/checkpoint14-visual-findings.md; combined checks in build-progress.md.


## Broader all-flags reflow baseline after checkpoint fourteen

A second68-observation real-API sweep enabled all eight amendment flags only in a fresh
disposable synthetic database. Desktop/375px/800px-DPR2/text-spacing variants have no
document horizontal overflow, visible text below11px or page errors. Workload's new
discipline table has a serious keyboard-scroll focus finding in threevariants; four
Dashboard task-name buttons clip under text spacing and need inspection/repair. Owners
are addressing both in generation8. The800px/DPR2 mode is labelled a reflow equivalent,
not proof of a real200% browser zoom setting. Rendered colour inventory also leaves
legacy accent/token-source cleanup open. Raw evidence and limits are in
evidence/checkpoint14-reflow-census.json and evidence/checkpoint14-reflow-findings.md.
The synthetic API/database were stopped/dropped after saving evidence. No full052S09
acceptance follows. Generation8 source changes are not included in these binaries.


## Checkpoint29 combined-design review · 2026-11-02

Four new feature surfaces were captured with synthetic visual_system plus their owning screen flag on the final local SPA5: Calendar, discipline reviews, cross-project task commands and Notifications. Eight desktop/phone images were inspected; all four framing runs made zero business writes. The separate numbered copy is [.review/checkpoint29-review/index.html](../../.review/checkpoint29-review/index.html). Original prototypes and previous output are preserved. This adds visual smoke evidence only: the broader052/zoom/native/pilot/human gates are not closed. Source flags remain off, with no commit/push/deployment/Accepted. See053checkpoint29 findings for provider/browser boundaries and repairs.


## Checkpoint30 — Task surfaces in the visual system

The S16 Task sections/History and contribution UI retain the navy band, Plex typography and one-list navigation. Four new combined-design desktop/phone captures were inspected in the separate `.review/checkpoint30-review/` gallery; the earlier eight images and original prototypes are preserved. New numbered additions05/06 are visible at loopback8868.

The associated053 slice passes85 focused provider cases,32 client-state cases, application/fixture types and scoped lint with warnings. Final Task-section and contribution browser suites pass on isolated SPA2; the unchanged Calendar/source regression also passes. Shared portals respect opt-in current Task availability without changing their flag-off props. The strict native-visibility driver kept nine real refresh intervals; its Chrome window bounds were corrected after preserved diagnostic failures.

Evidence: `../053-screen-completeness/evidence/checkpoint30-findings.md` and `../053-screen-completeness/evidence/checkpoint30-runtime/verification-rollup1.json`. Packet053 is42/49 locally checked. Full acceptance, remaining work, maintained activation and deployment remain open; nothing committed or pushed.


## Checkpoint31 — Readiness links and meeting agenda

Four final SPA2 combined-design desktop/phone captures retain the existing navy band, Plex typography and navigation. Separate review copy `.review/checkpoint31-review/index.html` adds numbered07/08 while preserving12 earlier images and all original prototypes. The actual S38 browser suite passes8 scenarios; source/served hashes match. This adds visual smoke evidence, not broader052/zoom/native/pilot/human acceptance. Packet053 is43/49 locally checked;70 selected .NET and33 client-state cases pass. Notice form remains unmounted and full delivery behavior open. See `../053-screen-completeness/evidence/checkpoint31-findings.md`. No maintained activation, commit, push or deployment.


## Checkpoint32 / generation60 — Notices locally verified

S45 +S47 now locally checked;45/49 screen slices,4remain open.127 distinct selected .NET checks (114provider-backed/13pure),19final finite browser scenarios including17receipt matrix rows, generated/strict contract, app/fixture types, scoped lint, final isolated SPA and solution build PASS. Three initial fixture failures and producer drift refusal are retained with their narrow repairs/reruns. Real transports/hosted/pilot/human acceptance/full specification acceptance remain unproven; no commit/push/deploy/maintained flags.

Evidence: [findings](../053-screen-completeness/evidence/checkpoint32-findings.md), [rollup](../053-screen-completeness/evidence/checkpoint32-runtime/verification-rollup1.json). Review copy: http://127.0.0.1:8873/#addition-09 (09Notice preferences/10Delivery history; earlier prototypes/gallery preserved). Final source/bundle/preservation/document checks follow in checkpoint32-runtime. Remaining S12/S35/S37/S49 and prior QA24 cleanup decision stay open.

## Prototype comparison · 2026-11-04 · after commit ca8fccea

Jay asked for the build to be checked against the mockup. Reference: the 33-screen prototype of 2026-10-31
(`~/PM-Tool/.review/2026-10-31-unslop-home/proto/`, untracked, 052-D7) and its own desktop/phone shots.
Method: a copy of the same preview database (5 projects, 34 tasks) upgraded from 38 to 56 migrations in a
scratch database, all eight 052/053 flags on there only, each screen opened as the person the prototype shows
(Lena Brooks, Priya Nair, Alex Chen, Jordan Lee) at 1440×900 and 375×812, workspace screens at the
prototype's "All permitted" scope, and each pair read side by side. The day differs (prototype 2026-10-31,
app 2026-11-04), so dates and relative times differ.

All 33 screens render with no console error and no sideways scroll at either width. Band, title block,
count-card overlap, the eight-item sidebar with its foot group, the one-line project header, the six project
groups with Activity and Settings as links, and the workspace tab row match the prototype's direction and
FR-VSN-10 to FR-VSN-19.

Fixed in this pass (flag-off output unchanged):

| Defect | Where | Fix |
|---|---|---|
| Board column headers cut off at the top on long columns (To Do 23, In Progress 7) | Boards and project Board, 1440×900 | The shared kanban header no longer shrinks in its height-capped column (`shrink-0`) |
| Person names on the navy band at 2.21:1 contrast (AA needs 4.5:1) | Home "Welcome back, Lena Brooks"; the Project Manager name on all 13 project pages | On the band, person-name buttons take the band's text colour (14:1) and the navy focus ring |
| My Week's collapsed line read "Free unavailable free" | My Work with `flag.screen_patterns`, provisional or missing capacity | Own text "My Week: {planned} planned, Free unavailable"; the suite now asserts the exact line instead of a pattern |

A text-contrast scan of all 33 screens after the fix finds no failure on the band; its remaining hits are
overlay layers the scanner cannot resolve, and those rows are white in the screenshots.

Differences that follow the specification or an open decision, not defects: My Week collapsed below the
queue (053 accepted pattern); register, milestone, deliverable, review, time, template and team-matrix
"moment" charts are prototype ideas outside 053-D2's default; count-card figures take their metric colour
(AC-VSN-02) where the prototype used ink; application labels win (052-D8: "Red", "Team Planner",
"Owner (PM)"); the prototype's two-line project name is not adopted (FR-VSN-12).

Open observations for Jay (no change made):

- Tab rows sit on the light page instead of the band on Notifications (Notifications / Following) and Admin
  (17 tabs over two rows); FR-VSN-11 and the prototype put them in the band. Part of S07, still open.
- Home's capacity card: with 2 of 22 people unverified, the headline reads "Percentage unavailable" at hero
  size and the card carries about eight lines of explanation; the prototype shows one percentage and a bar.
- Every Home count card carries a two-line "Trend unavailable for this scope" box; the prototype shows a
  small trend chip.
- At 375 px the project header truncates the name to a few letters and hides the Project Manager's name.
- The project identity swatch is a 2 px ring, fainter than the prototype's filled swatch (FR-VSN-07 says
  "swatch"); a filled circle would read as a task mark (FR-VSN-08).
- FR-SCR-66 flags a client workshop between the 60 % and 90 % submissions as "Out of sequence", because
  Design Submission precedes Client Workshop in the §12.3 order. The build follows the rule; the rule may
  need a series exception.
- The Projects list at 1440 px is 1,834 px of 18 columns inside a 1,134 px card, so Next milestone and the
  attention columns start off-screen and rows grow to 141 px from the Staffing cell; the prototype fits one
  compact row per project. Columns can be hidden; no default changed.
- Home and Projects open on "My projects", so a Supervisor who is on no project sees empty pages until
  choosing All permitted; the prototype assumed All permitted. Existing behaviour, outside 052/053.

Checks after the fixes (committed locally as `98ed6fd3` on `codex/build-052-053`; flags stay off):

| Check | Result |
|---|---|
| `npm run build`, `lint`, served-asset allowlist | pass |
| CI web job suites (38 scripts) and `typecheck:tests` | 38 of 39 pass; `test:supervisor-capacity` failed only because the local Docker (Colima) port forward holds port 5178 here and passes on `SUPERVISOR_CAPACITY_TEST_PORT=5198` |
| `test:record-parity`, base `ca8fccea`, candidate `98ed6fd3` | pass: self-check, all 12 persona/width sets (6 personas × 1600 and 375 px), AC-REC-09 additions flag off and on |
| 053 suites the fixes touch: `work-delivery-today-reviewers` (exact My Week line), `work-board-completeness`, `visual-system`, `visual-navigation`, `visual-layout` (on the Vite dev server it requires), `home-visual-layout`, `project-dashboard-completeness` | pass |
| `visual-route-matrix` (not in CI) | fails waiting for `/projects/NAV-001` to settle on its dashboard; fails the same way on Codex's `5663eca1`, last recorded pass checkpoint 19. The running app redirects `/projects/250101` to its dashboard; the harness expectation needs updating (open) |
| `ratchet.py`, `text_keys.py`, `trace_spec.py --check`, explainer evidence checker | pass |
| Contrast scan, 33 screens at 1440 px, flags on | no band failure after the fix |

Side-by-side gallery of all 33 pairs, desktop and phone: `~/PM-Tool/.review/2026-11-04-mockup-check/index.html`
(untracked). The scratch database `hub_mockup` was dropped afterwards.


## Build completion · 2026-11-11 · S02, S03, S06, S07, S08 and S09

Jay asked to finish the rest of the build. Two lanes measured and finished the open slices with `flag.visual_system` and the
053 screen flags on, keeping flags-off output unchanged; Claude merged them on `codex/build-052-053` with the 053 S50 work and
fixed three defects their censuses found. Details, gates and the independent review are in
[053 verification](../053-screen-completeness/verification.md), "Build completion · 2026-11-11".

**Navigation (S07, S08).**
- The route matrix (`web/tests/visual-route-matrix.cjs`) failed because it seeded the project into the wrong cache slot; the app's
  `/projects/<n>` redirect was right. It now covers all 62 router routes × 9 role profiles at 1440 and 375 px, flags on and off:
  every content route highlights exactly one sidebar item and Search, Notifications and Preferences highlight none (052-D12);
  the flags-off pass matches the `ac2fa894` source exactly.
- Notifications (Notifications / Following) and Admin now put their tab rows in the band (FR-VSN-11). §56 does not say how
  Admin's 19 tabs fit on desktop; they are one band row that scrolls sideways like the band's other rows, with the current tab
  scrolled into view (a presentation choice, not a decision). Band tab rows take the navy focus colour; the inherited ring was
  2.2:1 on navy.
- Escape from a focused drawer link closed a hidden tooltip instead of the drawer; drawer links no longer carry a tooltip, and
  `visual-drawer.cjs` checks it.
- A real-API sweep of 744 page loads (6 roles, 2 widths, every flag on) found no sideways scroll, page error or second selected
  item. Evidence: `evidence/final-navigation-matrix.json`, `evidence/final-navigation-real-sweep.json`.

**Census (S02, S03, S06, S09).** Fixes are CSS under `:root[data-visual="v2"]`:
- 375 px project header: the 120-character name showed 44 px and the Project Manager was cut; the name now shows 269 px and the
  Project Manager whole, within 72 px (FR-VSN-12).
- Three selects drew a 1.2:1 hairline as their boundary; native fields now use the 3:1 control line (FR-VSN-13).
- Planner, Team Planner month and Vacation figures in teal at 19 px were 4.12:1; they are ink (FR-VSN-21).
- Legacy accent rings, stripes and chips and translucent status colours now resolve to §56 tokens; the only non-token colours
  left are the stored discipline swatches on Admin reference data, which are data values.
- Count cards: all 30 figures equal their destination list counts, each ordinary card is one link and On Track keeps its two
  links (052-D11); six per row at 1440 px, three at 1000 px and two at 375 px.
- `/tasks/:id` now opens with the band (FR-VSN-11): the task key and name in the band, the task card below (Claude, this pass).
Evidence: `evidence/final-visual-census.json`, `evidence/final-reflow-census.json`, `evidence/final-findings.md`.

Defaults used (decisions stay Open): 052-D2 colour budget (non-token fills target 0, met outside Admin data), 052-D3 one sidebar
list of eight with a foot group, 052-D4 six project group tabs with Activity and Settings links, 052-D10 bottom bar kept with the
drawer.

**Final census, `88b33103`.** The 17-screen census (all eight flags off and on, PM and Supervisor) has 68 observations and no
finding: zero serious or critical axe violations, every first data row within 405 px of the top, no page error and no external font.
Calendar's first row is at 277 px with no visible field and 21 controls; Team has one field. The acceptance probe found the band
on all 51 routes, 30 count cards matching their lists and 197 inputs at 3:1 or identified by their arrow. Reflow passes 68 of 68
(375 px, the 800 px DPR 2 equivalent of 200 % zoom, and text spacing). With flags on, `clutter.cjs` reports only the counts the 052
defaults replace (sidebar items, project group tabs, fills) and §57's words and controls (open, 053 record). Acceptance-table
status: AC-VSN-01, 02, 03, 05, 07, 08, 09, 10, 11 and 12 pass locally; AC-VSN-04 and 06 passed earlier; none has pilot, hosted
or human acceptance.

**Open.** AC-CALM-05's word half is exceeded on ten screens by §57 content (needs Jay's decision, see the 053 record). My Staff puts
its title block in the actions column (still in the band before the tabs). S10 flag cleanup follows pilot activation. Pilot,
hosted and human acceptance are not covered by these local checks.
