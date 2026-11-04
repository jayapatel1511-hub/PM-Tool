# Verification: Visual System and Navigation

Specification drafting completed on 2026-10-31. The later user-authorized dark build is in progress; local implementation evidence appears below. Open pilot/design decisions remain Open.

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
| AC-VSN-01 | Token census, fonts, no third-party font request | S01, S09 | Font/switch checks PASS locally; complete token census pending |
| AC-VSN-02 | Count-card colours by metric | S03 | Home focused checks PASS; full-screen census pending |
| AC-VSN-03 | Lane colours and hatch | S03 | Home chart/runway focused checks PASS; other surfaces pending |
| AC-VSN-04 | Project identity colour | S04 | PASS locally with synthetic API/browser data; no pilot/human acceptance |
| AC-VSN-05 | One-line project header within 72 px | S02 | Focused desktop/phone check PASS; wider S02 census pending |
| AC-VSN-06 | Title block from the response time | S05 | PASS locally: response clock, organisation zone, shifted browser clock |
| AC-VSN-07 | Count card link and definition | S06 | Home focused checks PASS; full slice remains partial |
| AC-VSN-08 | Sidebar list and foot group per role | S07 | Focused role/parent checks PASS locally; decision gates remain Open |
| AC-VSN-09 | Workspace and project tab rows | S07 | Focused actual links/scope checks PASS; whole-route acceptance pending |
| AC-VSN-10 | Phone drawer and bottom bar | S08 | Focused focus trap/return/search and 375px checks PASS; all-screen census pending |
| AC-VSN-11 | Accessibility scan and contrast census | S09 | Visual/Home pair: 68 observations with zero serious/critical axe; all-flags reflow finds Workload focus issue; full acceptance pending |
| AC-VSN-12 | Flag off changes nothing | S01 to S09 | Focused shape/navigation/browser checks PASS; packet-wide parity pending |

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

Checks after the fixes (uncommitted on `codex/build-052-053`; flags stay off):

| Check | Result |
|---|---|
| `npm run build`, `lint`, served-asset allowlist | pass |
| CI web job suites (38 scripts) and `typecheck:tests` | 38 of 39 pass; `test:supervisor-capacity` failed only because an SSH tunnel holds port 5178 here and passes on `SUPERVISOR_CAPACITY_TEST_PORT=5198`; `test:record-parity` needs a clean tree and a PR base, so it runs once these fixes are committed |
| 053 suites the fixes touch: `work-delivery-today-reviewers` (exact My Week line), `work-board-completeness`, `visual-system`, `visual-navigation`, `visual-layout` (on the Vite dev server it requires), `home-visual-layout`, `project-dashboard-completeness` | pass |
| `visual-route-matrix` (not in CI) | fails waiting for `/projects/NAV-001` to settle on its dashboard; fails the same way on Codex's `5663eca1`, last recorded pass checkpoint 19. The running app redirects `/projects/250101` to its dashboard; the harness expectation needs updating (open) |
| `ratchet.py`, `text_keys.py`, `trace_spec.py --check`, explainer evidence checker | pass |
| Contrast scan, 33 screens at 1440 px, flags on | no band failure after the fix |

Side-by-side gallery of all 33 pairs, desktop and phone: `~/PM-Tool/.review/2026-11-04-mockup-check/index.html`
(untracked). The scratch database `hub_mockup` was dropped afterwards.
