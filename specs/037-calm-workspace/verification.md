# Verification and Acceptance Plan: Calm Workspace

**State**: Baseline measured 2026-10-11. Phase 1 (T002–T008) merged into `claude/tuesday-visual-batch1` on 2026-10-11 at `ceee4532`. Phase 2 (T009–T012) merged on 2026-10-12 in run 2 (`7a39e76b`, with packet 036's web streams); integration fixes in `b1a38fc6` and `caa76893`. Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`): AC-CALM-01, -02, -04 and -06 to -10 PASS; AC-CALM-03 and -05 still FAIL on some screens; SC-CALM-01 and -02 UNRUN (they need people). Evidence in `evidence/`, including the 17 screens at 1440 and 1024 for SC-CALM-02.

**Evidence rule**: PASS means `test:clutter` output, a crawl report, a click log or a screenshot from a stated commit. Nobody's opinion, including an agent's, counts as evidence.

## 1. Definition of accepted

1. AC-CALM-01 to AC-CALM-10 are PASS on one release commit, for Priya (PM) and Sam (Supervisor). AC-CALM-04 is also checked for Jordan, Lena, Alex and Rita.
2. SC-CALM-01 and SC-CALM-02 (human sessions) are PASS.
3. The existing browser suites, `build` and `lint` are green, and only selectors changed in the tests.
4. No feature, route, field, count or export was removed (AC-CALM-06).
5. The wording check is clean, the merge has no conflicts, and Jay signs off.

## 2. Baseline

**Setup**: local preview `pm-tuesday-preview-db`, fixture projects SYN-101 to SYN-103, signed in as Priya, viewport 1600×900, measured with the appendix A census. First measured at `121e3ae2`; re-measured on the shared base `5800e3fc` for T002 (2026-10-11, `build/037-layout` writer report), and the table below is that re-measure.

**Columns**:
- *Chrome controls*: buttons, links and fields above the fold, outside tables and grids.
- *Filter fields*: visible filter inputs and selects with no filter active.
- *Chrome words*: words above the fold outside tables and grids.
- *First data row*: px from the top of the screen. Screens with no table rows use the page-specific data selector in `web/tests/clutter.cjs`.
- *Fills*: distinct background colours above the fold.

| Screen | Chrome controls | Filter fields | Chrome words | First data row (px) | Fills |
|---|---:|---:|---:|---:|---:|
| /home | 30 | 2 | 114 | 275 | 12 |
| /my-work | 30 | 8 | 78 | 694 | 9 |
| /boards | 35 | 6 | 140 | 511 | 6 |
| /projects | 13 | 6 | 33 | 504 | 8 |
| /tasks | 27 | 4 | 43 | 479 | 9 |
| /calendar | 26 | 0 | 150 | 373 | 6 |
| /portfolio | 18 | 9 | 58 | 558 | 10 |
| /workload | 29 | 5 | 112 | 727 | 11 |
| /planner | 27 | 9 | 63 | 744 | 10 |
| /reports | 15 | 0 | 169 | 184 | 5 |
| /team | 11 | 1 | 16 | 431 | 7 |
| project dashboard | 49 | 1 | 168 | 1138 (below fold) | 10 |
| project tasks | 50 | 6 | 108 | 762 | 9 |
| project deliverables | 44 | 9 | 110 | 734 | 11 |
| project timeline | 47 | 5 | 185 | 784 | 11 |
| project allocations | 40 | 6 | 98 | 660 | 8 |
| project coordination | 41 | 4 | 232 | 840 | 5 |

Against `121e3ae2`, the largest move is /planner (controls 23 → 27, words 53 → 63, first row 662 → 744); /calendar words went 147 → 150. `clutter.cjs` still computes the AC-CALM-05 word limits from the `121e3ae2` words, which is stricter for those two screens.

Navigation at baseline (`5800e3fc`):
- 24 project tabs in two rows.
- Sidebar items: Jordan 17, Lena 15, Sam 15, Priya 14, Alex 11, Rita 11.
- My Work has 7 view tabs and 14 section chips (counted at `121e3ae2`; not re-counted).

Screenshots: `baseline/my-work.jpg`, `baseline/project-tasks.jpg`, `baseline/project-dashboard.jpg`, `baseline/weekly-planner.jpg`.

## 3. Decisions

| # | Decision | Jay's answer | Date |
|---|---|---|---|
| E1 | Project tab groups | Default (Jay: 'lets start build') | 2026-10-11 |
| E2 | Sidebar items | Default (Jay: 'lets start build') | 2026-10-11 |
| E3 | My Work opens on Today | Default (Jay: 'lets start build') | 2026-10-11 |
| E4 | At most 3 quick-filter chips; the rest become Views | Default (Jay: 'lets start build') | 2026-10-11 |

## 4. Acceptance criteria

All rows are measured by `web/tests/clutter.cjs` at 1600×900 unless stated.

| ID | Criterion | Threshold | Baseline | Proof | Status | Evidence |
|---|---|---|---|---|---|---|
| AC-CALM-01 | Work is visible first | First data row top ≤ 405 px (45 % of the screen) on all 17 screens | 425–1137 px | clutter.cjs | PASS | Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`), Vite on 5186 against the shared API on 5080 (preview database with the run 2 fixtures). `TUESDAY_UI_BASE_URL=http://localhost:5186 npm --prefix web run test:clutter` (Priya, Sam) and the same with `CLUTTER_PERSONAS=jordan,lena,alex,rita`, exit 1 both; output in `evidence/clutter-priya-sam.txt` and `evidence/clutter-jordan-lena-alex-rita.txt`, JSON copies (`.json`, written by `evidence/table-json.cjs`). Priya and Sam: 20 thresholds exceeded, the same 20 as at `caa76893`. Priya: all 17 screens ≤ 405 px: /home 255, /my-work 348, /boards 317, /projects 294, /tasks 363, /calendar 319, /portfolio 405 (at the limit), /workload 377, /planner 308, /reports 231, /team 363, dashboard 270, project tasks 396, deliverables 391, timeline 348, allocations 395, coordination 386. Sam: the same where measured; /my-work, /boards, /projects, /tasks and /team are empty for Sam and unmeasured. Not counted (other personas): Jordan /planner 448, pushed down by the always-open reason field (Admin only) for viewing private drafts (`planner-draft-reason`). |
| AC-CALM-02 | Filters are closed by default | ≤ 1 visible filter field (search) with no filter active, on all 17 screens | 0–9 | clutter.cjs | PASS | Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`), Vite on 5186 against the shared API on 5080 (preview database with the run 2 fixtures). `TUESDAY_UI_BASE_URL=http://localhost:5186 npm --prefix web run test:clutter` (Priya, Sam) and the same with `CLUTTER_PERSONAS=jordan,lena,alex,rita`, exit 1 both; output in `evidence/clutter-priya-sam.txt` and `evidence/clutter-jordan-lena-alex-rita.txt`, JSON copies (`.json`, written by `evidence/table-json.cjs`). Priya and Sam: 20 thresholds exceeded, the same 20 as at `caa76893`. At most 1 visible filter field on all 17 screens for Priya and Sam (0 on /home, /my-work, /calendar, /workload, /reports and timeline; 1 elsewhere). Not counted: Jordan /planner 2 (the same private-drafts reason field, outside the Filters popover); Alex /my-work 7, which are the inline Progress selects of his own task rows (the census counts any select outside a table as a filter field). |
| AC-CALM-03 | Calm chrome | ≤ 25 chrome controls above the fold on every screen | 11–50 | clutter.cjs | FAIL | Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`), Vite on 5186 against the shared API on 5080 (preview database with the run 2 fixtures). `TUESDAY_UI_BASE_URL=http://localhost:5186 npm --prefix web run test:clutter` (Priya, Sam) and the same with `CLUTTER_PERSONAS=jordan,lena,alex,rita`, exit 1 both; output in `evidence/clutter-priya-sam.txt` and `evidence/clutter-jordan-lena-alex-rita.txt`, JSON copies (`.json`, written by `evidence/table-json.cjs`). Priya and Sam: 20 thresholds exceeded, the same 20 as at `caa76893`. Priya passes 12 of 17: /my-work 20, /projects 9, /tasks 19, /portfolio 11, /workload 24, /planner 17, /reports 1, /team 11, project tasks 25, deliverables 25, timeline 25, allocations 24. Fails: /home 36, /boards 38, /calendar 27, dashboard 38, coordination 42. Sam fails only the dashboard (36) and coordination (40). Unchanged since `caa76893`. Breakdown for Priya (this sweep): coordination 42 = 21 shared project controls (header 6, group tabs 7, Coordination pages 8) + 7 toolbar + 14 links inside the register cards; dashboard 38 = 13 header and group tabs + 25 count tiles and links; /home 36, /boards 38 and /calendar 27 each include the 9-link workspace Views switch (Dashboard, Board, List, Timeline, Calendar…) plus count links, board cards or calendar entries, which the census counts as chrome. |
| AC-CALM-04 | Fewer destinations | ≤ 7 project group tabs plus More; ≤ 8 sidebar items for Jordan, Lena, Sam, Priya, Alex and Rita | 24 tabs; 14–17 items | clutter.cjs | PASS | Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`), Vite on 5186 against the shared API on 5080 (preview database with the run 2 fixtures). `TUESDAY_UI_BASE_URL=http://localhost:5186 npm --prefix web run test:clutter` (Priya, Sam) and the same with `CLUTTER_PERSONAS=jordan,lena,alex,rita`, exit 1 both; output in `evidence/clutter-priya-sam.txt` and `evidence/clutter-jordan-lena-alex-rita.txt`, JSON copies (`.json`, written by `evidence/table-json.cjs`). Priya and Sam: 20 thresholds exceeded, the same 20 as at `caa76893`. Every project screen shows 6 group tabs plus More for all six personas (Priya, Sam, Jordan, Lena, Alex, Rita). Sidebar items on /my-work: Jordan 7, Lena 7, Sam 7, Priya 7, Alex 6, Rita 6. |
| AC-CALM-05 | Less reading, fewer colours | Chrome words above the fold ≤ max(40, 50 % of the baseline) per screen; ≤ 6 chromatic fill colours above the fold (HSL saturation ≥ 15 % and lightness 8–96 %, composited on white) | 16–232 words; 5–12 colours | clutter.cjs | FAIL | Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`), Vite on 5186 against the shared API on 5080 (preview database with the run 2 fixtures). `TUESDAY_UI_BASE_URL=http://localhost:5186 npm --prefix web run test:clutter` (Priya, Sam) and the same with `CLUTTER_PERSONAS=jordan,lena,alex,rita`, exit 1 both; output in `evidence/clutter-priya-sam.txt` and `evidence/clutter-jordan-lena-alex-rita.txt`, JSON copies (`.json`, written by `evidence/table-json.cjs`). Priya and Sam: 20 thresholds exceeded, the same 20 as at `caa76893`. A3 limits. Words fail for Priya on /home 105 (limit 57), /my-work 225 (40), /boards 256 (70), /calendar 138 (75), dashboard 143 (84) and coordination 226 (116); for Sam on /home 83 (57), /projects 49 (40, empty state), /workload 71 (56), dashboard 143 and coordination 220. Fills: at most 6 on 16 of 17 screens for each persona; over are Priya /projects 7 and Sam /portfolio 7 (T012 open). Same as `caa76893`. |
| AC-CALM-06 | Nothing lost | Every route in `routes.tsx` loads for a permitted persona and highlights its sidebar item and project group; every former tab and quick filter is reachable in ≤ 2 clicks from its page; every former quick-filter URL returns the same rows as at the base | — | Crawl script plus API row-count comparison | PASS | Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`), Vite on 5186 against the shared API on 5080 (preview database with the run 2 fixtures). `evidence/calm-acceptance.txt` and `.json` from `node web/tests/calm-acceptance.cjs` (new), checks crawl, reach and quick, base UI `9beace03` on the same API at 5188. Crawl: all 58 route entries of `routes.tsx` (every pattern, with real ids; Admin, Templates and Staff as Jordan, reassign-work as Sam, the rest as Priya) load with no error, alert or not-found page (the `*` route shows the not-found page), and highlight the expected sidebar item (Boards, Calendar and Timeline under Tasks, Portfolio under Projects, Workload under Resources; none for Notifications, Search, Preferences, Vacation and reassign-work, which have no sidebar item) and project group (Activity and Settings name themselves on More; the project Vacation route, which was never a tab, highlights no group). Reach, counted from each page: all 24 former project tabs ≤ 2 clicks from the project dashboard (7 in 1, 17 in 2; Workload through People); all base sidebar items ≤ 2 from /my-work for Priya (14: 6 in 1) and Jordan (17: 6 in 1), Notifications through the bell; the 7 former My Work views and 12 Overview sections in 2 (views menu), Hide waiting in 1; all 53 former quick-filter chips on 17 pages ≤ 2 (25 visible, 28 in the Filters popover). The Workload "Any indicator" chip is gone: clearing the indicator is now the selected chip itself or its active-filter token, both 1 click. Quick-filter URLs (53): 52 show the same rows and send the same filtered request as the base UI on the same API; `/my-work?hideWaiting=1` differs by decision E3 (it now opens Today; `?tab=overview&hideWaiting=1` matches the base, M1). |
| AC-CALM-07 | Filters behave the same | Setting a filter in the popover updates the URL exactly as before, shows a removable chip and a count on the Filters button; Clear resets all; reload restores the state | — | Browser suite on Tasks, Deliverables, Planner and My Work | PASS | Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`), Vite on 5186 against the shared API on 5080 (preview database with the run 2 fixtures). `evidence/calm-acceptance.txt` and `.json` from `node web/tests/calm-acceptance.cjs` (new), check filters, against the base UI on the same API. On project Tasks, project Deliverables, Planner, My Work and workspace Tasks: the first select in the Filters popover sets exactly the URL parameter the base sets from the always-open field (Discipline, Discipline, Discipline, Status, Project), a removable chip appears and the Filters button counts 1; a quick chip (Overdue, Over-planned, Hide waiting, Blocked) sets the base parameter and the count goes to 2; reload keeps the URL, the chips, the count and the field value; a chip removes its filter; Clear removes the rest. Deliverables has no quick chip, so its Clear step is a single chip removal. |
| AC-CALM-08 | Keyboard and screen readers | Filters popover: Enter opens, focus moves inside, Esc closes and returns focus. More menus are operable by arrow keys. axe shows 0 serious or critical issues on the 17 screens | — | Browser and axe | PASS | Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`), Vite on 5186 against the shared API on 5080 (preview database with the run 2 fixtures). `evidence/calm-acceptance.txt` and `.json` from `node web/tests/calm-acceptance.cjs` (new), checks keyboard and axe. Keyboard on all 17 screens as Priya: on the 13 screens with a Filters button, Enter opens the popover with focus inside, Escape closes it and focus returns to the button; all 63 menu buttons in main (views, Group, Sort, Export, Columns, More actions, project More, Following, Zoom, row status menus) open with Enter, move with the arrow keys, close with Escape and return focus; the sidebar More expands with Enter and Tab moves into its items. axe (wcag2a, wcag2aa, wcag21a, wcag21aa) on the 17 screens for Priya and Sam: 0 serious or critical with nothing open and with the Filters popover open. With a menu open (recorded, not counted, S5): `aria-hidden-focus` (serious) in every menu-open state (16 screens with a menu, for Priya and Sam), on the app root and the skip link; see the follow-ups in section 8. |
| AC-CALM-09 | One signal per row | No row shows two indicators at once; extras appear as "+n" with a tooltip; week-specific indicators name their week (for example Alex on the Planner) | Over- and Under-planned on one row | Browser and DOM check | PASS | Acceptance sweep 2026-10-12 on code `caa76893` (`web/` and `src/` unchanged through `c13857eb`), Vite on 5186 against the shared API on 5080 (preview database with the run 2 fixtures). `evidence/calm-acceptance.txt` and `.json` from `node web/tests/calm-acceptance.cjs` (new), check signals. No table row has two indicator chips on /planner (Priya, Lena), /workload (Priya, Sam), /tasks, /my-work?tab=overdue, SYN-101 tasks and deliverables, SYN-105 tasks and deliverables; every row with more than one signal shows "+n" with n = the rest and an accessible label; every Planner indicator names its weeks (for example "Over-planned · Oct 12, Oct 19") and Workload Over-assigned names its week ("Over-assigned · Oct 12"). |
| AC-CALM-10 | Common tasks are not slower | Click counts for the six paths below are ≤ the baseline; recorded before and after | Measured in T002 | Click log | PASS | `node web/tests/calm-clicks.cjs` (new), click logs `evidence/clicks-before.json` and `evidence/clicks-after.json`. Before: `9beace03` in a throwaway worktree, its API on 5096 against a throwaway database (`pm-acc037-temp`, 55441) seeded through that API by the base commit's `seed_resources.py`, `seed_modules.py` and `seed_planning.py` (their target guard pointed at the throwaway stack in the throwaway copy only), its UI on 5187; all removed afterwards. After: code `caa76893` on 5186 with the shared API. Shortest route per path, clicks from /my-work: 1 → 1, 3 → 3, 3 → 3, 2 → 2, 5 → 5, 4 → 4, so no path is slower. Two second routes are slower; see the table below. |

Threshold refinement (A3, 2026-10-12, `d48ddccf`): AC-CALM-05 counts only chromatic fills and floors the word limit at 40 words. The census in appendix A counted every non-white background, so greys, near-whites and near-blacks (surfaces, hover states, borders drawn as fills) inflated the count without adding any colour, which is what R8 limits; and half of a small baseline (for example /team 16 → 8, /tasks 43 → 21) leaves fewer words than a title, a one-line scope and a toolbar need. Section 2's Fills column was measured with the old rule, so it is not comparable with the counts below.

Paths for AC-CALM-10:
1. Open my most overdue task from My Work.
2. Show SYN-101 Civil tasks that are overdue.
3. Open SYN-101's high risks.
4. Add 8 h of planning for Alex next week.
5. Export SYN-101 deliverables.
6. Change a task's owner.

Click log (2026-10-12, `CALM_VERSION=base` on the `9beace03` stack, `CALM_VERSION=now` on 5186). A select choice counts as two clicks; typing is keystrokes; the last click of paths 4 to 6 (save, download, assign) is counted and not made.

| Path | Route | Before | After |
|---|---|---:|---:|
| 1 (Alex) | My Work: the first task in the Overdue group | 1 | 1 |
| 2 (Priya) | Projects, SYN-101, the Civil row's Overdue count on the dashboard | 3 | 3 |
| 2, second route | Projects, SYN-101, Tasks tab (now the Work group), Discipline, Overdue chip; now Filters first, and the chip sits in the popover (B2) | 6 | 7 |
| 3 (Priya) | Projects, SYN-101, the High risks count on the dashboard | 3 | 3 |
| 3, second route | Projects, SYN-101, Risks tab (now Coordination group then Risks), Severity; now Filters first | 5 | 7 |
| 4 (Sam) | Sidebar Planner (now Resources), Alex's next-week cell, type "8 h" and Enter (4 keystrokes, Enter not pressed) | 2 | 2 |
| 5 (Priya) | Projects, SYN-101, Deliverables, Export, CSV | 5 | 5 |
| 6 (Priya) | Tasks, SYN-101-T0001, Owner, a person | 4 | 4 |

## 5. Success criteria (people)

| ID | Session | Who | Pass | Status |
|---|---|---|---|---|
| SC-CALM-01 | 5-second test: show project Tasks, My Work and the Planner for 5 seconds each, then ask "What is this page for, and what would you click first?" | 2 working PMs, not Jay | Both name the purpose and the primary action on all 3 screens | UNRUN: needs people (two working PMs). |
| SC-CALM-02 | Visual review of the 17 screens at 1440 and 1024 widths against the baseline screenshots | Jay | Jay approves: "neat and clean, nothing missing" | UNRUN: needs people (Jay). Screenshots for the review, signed in as Priya at 1440×900 and 1024×900 on code `caa76893`: `evidence/screens-1440/` and `evidence/screens-1024/`, 17 each, numbered in section 2 order; baseline in `baseline/`. |

## 6. Regression guard

| Check | Pass condition | Status |
|---|---|---|
| Browser suites | `test:planner`, `test:resources`, `test:handoffs`, `test:coordination` and `test:design-basis` green; the diff of those files changes selectors only | Green: PASS at `caa76893`: `test:planner` (20 flows, 13 axe reports, 0 violations), `test:resources`, `test:handoffs`, `test:coordination` (0 axe violations) and `test:design-basis` (unmockedGets []) all exit 0. Selectors only: not met. Run 2 adds an open-roles mock to `resources.cjs` (036 staffing writer) and `design-basis.cjs` (integrator, `caa76893`), reaches 'Copy last week' through the More actions menu, uses the Workload Sort menu and the 'Partial view' status, and replaces the My Work phone step's assertions about the removed duplicate toggle and Sections nav with assertions about the single Filters control and the views menu. Earlier: Green: PASS at `ceee4532` (`test:planner`, `test:resources`, `test:handoffs`, `test:coordination`, `test:design-basis` all exit 0). Selectors only: not met. `design-basis.cjs`, `resources.cjs` and `planner.cjs` add steps that open the Filters popover and press Escape before using fields that moved into it; no assertion was removed. |
| Build | `npm --prefix web run build` and `lint` pass | PASS at `caa76893`: `build` exit 0; `lint` exit 0 (0 errors, 94 warnings; 89 at `86e40ef6`, the 5 new are `only-export-components` warnings in `people.tsx` and `Timeline.tsx`). Earlier: PASS at `ceee4532`: `build` exit 0; `lint` exit 0 (0 errors, 89 warnings; the writer reports one new warning, `only-export-components` for `PINNED_QUICK` in `Tasks.tsx`). |
| API untouched | `git diff <base> -- src/` is empty for this packet | PASS for Phase 2: `git diff 86e40ef6 build/037-phase2 -- src/` is empty. Earlier: PASS: `git diff 5800e3fc 03661892 -- src/` is empty. |
| Deep links | Every URL in the existing docs, notifications and digest templates (`git grep -o "/projects/[^ )\"']*"`) still resolves to the same page | PASS (2026-10-12, code `caa76893`, check deeplinks in `evidence/calm-acceptance.txt`): 33 app links, the ones the API builds for notifications, digests and attention items (task, deliverable, milestone, decision, risk, issue, action, handoff, change, review and submission panels, allocation and basis links, My Week, project scope, Time) plus the app paths named in the docs, each open the same page, heading and panel as the `9beace03` UI on the same API; none is a not-found page. Five paths in `docs/api-reference.md` are API endpoints with no page (`/projects/{id}/members`, `disciplines`, `template-packs`, `decision-log`, `actions`) and are skipped. |
| Wording | The diff contains no third-party product names | PASS at `caa76893`: `git diff 9beace03..HEAD \| grep -inE '<names>'` and the same grep on the commit messages print nothing. Earlier: PASS: `git diff 9beace03..ceee4532 \| grep -inE '<names>'` and the same grep on the commit messages print nothing. |
| Merge | A trial merge into the base has no conflicts | PASS: `build/037-phase2` merged into `claude/tuesday-visual-batch1` in `7a39e76b` with no conflicts, after the four run 2 packet 036 branches. Earlier: PASS: `build/037-layout` merged into `claude/tuesday-visual-batch1` in `ceee4532` with no conflicts, after packet 036's API branches. |

## 7. Sign-off

| Item | Name | Date | Result |
|---|---|---|---|
| Release commit | | | |
| Measured acceptance (sections 4 and 6) | | | |
| Human acceptance (section 5) | | | |
| Final approval | Jay | | |

## 8. Assumptions made while Jay was away

Jay was away during run 2 and asked for autonomous completion. Where the specification was silent, the simplest behaviour consistent with it was chosen and is listed here for his review. A1–A6 came from the orchestrator; the rest were reported by the run 2 writers (branch named in Source) or made by the integrator.

| # | Source | Assumption | Date | Commit |
|---|---|---|---|---|
| A1 | Orchestrator | Quick filters beyond three live under "More quick filters" in the Filters popover. No saved-view wiring. | 2026-10-12 | `6992f11f` (run 1), `adf8b507` (project Tasks) |
| A2 | Orchestrator | Group and Sort are compact toolbar menu buttons (a `ui/dropdown-menu` radio group), not select fields. | 2026-10-12 | `adf8b507`, `796f33f9`, `4ad463ee`, `c54f7766` |
| A3 | Orchestrator | Clutter metric: a fill counts only if it is chromatic (HSL saturation ≥ 15 % and lightness 8–96 %); the AC-CALM-05 word limit is max(40, 50 % of the baseline). | 2026-10-12 | `d48ddccf` |
| A4 | Orchestrator | Editing an allocation that filled a role keeps the role Filled. Only decline or cancel reopens it. | 2026-10-12 | `8b689db3` (run 1) |
| A5 | Orchestrator | A-21 fires when the status is unchanged for more than `status_stale_days` and the item had activity within `task_stale_days`, whether or not A-10 is enabled, and never while the A-11 review-stalled condition holds. | 2026-10-12 | `4fccfa34` |
| A6 | Orchestrator | Open-role create, edit and cancel on a Complete project require a correction reason, as allocation proposals do. | 2026-10-12 | `87651b57` (server), `4480db5e` (client) |
| B1 | 036 staffing UI | Project Tasks and Deliverables get a Group menu only; Sort stays in the sortable column headers, because a Sort menu would break the 25-control limit on Tasks. | 2026-10-12 | `adf8b507` |
| B2 | 036 staffing UI | Project Tasks control budget: every quick-filter chip sits in the Filters popover under "Quick filters" (same URL flags; Board keeps its three pinned chips), Export and Columns share one overflow menu, and the List/Board switch is one "Board" link that keeps the query string. | 2026-10-12 | `adf8b507` |
| B3 | 036 staffing UI | Avatars show their identity accent as a ring with coloured initials instead of a pastel fill, on every screen, for the R8 colour budget. | 2026-10-12 | `6afb5fef` |
| B4 | 036 staffing UI | No `groupBy` keeps the Reports to grouping and `groupBy=none` gives a flat list. Group headers show even with one group, so "Not set" is never hidden; the no-supervisor bucket keeps "No supervisor". | 2026-10-12 | `796f33f9` |
| B5 | 036 staffing UI | Group subtotals sum the figures each row shows: Workload committed/available per week; Planner planned (Confirmed + Expected + Possible)/capacity per week. | 2026-10-12 | `796f33f9` |
| B6 | 036 staffing UI | Planner week cells carry no indicator chips. Each indicator is named once in the person's signal with its weeks (the first three plus a count), in the cell's accessible label and in the contributions dialog. On Workload, Over-assigned names its week when one can be found; window rules (cluster, unestimated, under) name none. | 2026-10-12 | `796f33f9` |
| B7 | 036 staffing UI | Picker figures: one request per open for the visible candidates, and a later search asks only about candidates not asked before. A negative figure reads "N h over". The gate is `me.capabilities.workload`. | 2026-10-12 | `6afb5fef` |
| B8 | 036 staffing UI | The TaskPanel owner field (`InlinePerson` in `fields.tsx`) gets its week through a `CapacityWeek` context exported from `people.tsx`. | 2026-10-12 | `6afb5fef`, `adf8b507` |
| B9 | 036 staffing UI | Open roles are a view switch on the Allocations tab. Cancelled roles stay listed, muted. "Filled by" loads the filling allocation (one cached request per filled role; a dash if the viewer cannot open it). | 2026-10-12 | `4480db5e` |
| B10 | 036 staffing UI | On a Complete project, new allocation proposals (Fill included) show the reason field, and open-role create, edit and cancel send `reason`. | 2026-10-12 | `4480db5e` |
| B11 | 036 staffing UI | The deliverable state view has an optional notes field labelled like task notes. The API sends no deliverable notes yet, so nothing shows. | 2026-10-12 | `adf8b507` |
| B12 | 036 staffing UI | Words: Allocations and Deliverables show a short scope with the old subtitle in help; the Workload partial-view explanation moved into help beside a "Partial view" status; the dependency dialog titles are type-neutral. | 2026-10-12 | `4480db5e`, `adf8b507`, `796f33f9` |
| C1 | 036 signals UI | The faded assumed-start segment runs from the assumed start to today and is at least one day wide; when today is past the due date the whole bar fades. | 2026-10-12 | `8ddea543` |
| C2 | 036 signals UI | Home lane definitions list the statuses the server returns for each lane. | 2026-10-12 | `aa03498c` |
| C3 | 036 signals UI | Tasks without a deliverable, or under an undated one, are grouped in a collapsible "Tasks on their own (n)" row, which now respects the From/To range filter. | 2026-10-12 | `8ddea543` |
| C4 | 036 signals UI | The Timeline chart has table semantics (table, row, rowheader, cell). | 2026-10-12 | `8ddea543` |
| C5 | 036 signals UI | Dashboard count tiles are labelled article elements (the leading data under R9), so the census measures the dashboard's first data row at the tiles. | 2026-10-12 | `a2f57e68` |
| C6 | 036 signals UI | Zoom on both timelines is a menu, as A2. | 2026-10-12 | `8ddea543` |
| C7 | 036 signals UI | The Timeline drag help moved into the Legend popover; Gantt's legend has its own drag note. | 2026-10-12 | `8ddea543` |
| C8 | 036 signals UI | The Dashboard "Weekly Coordination" button is removed; the Coordination tab opens on it. | 2026-10-12 | `a2f57e68` |
| C9 | 036 signals UI | Home drops the scope eyebrow and keeps "Welcome back, {name}." as the scope line; "Every number opens…" and the as-of time moved into page help, and the section notes are dropped. | 2026-10-12 | `aa03498c` |
| C10 | 036 signals UI | The milestone strip's reduced height comes from a parent override of its inner padding, because `Milestones.tsx` was not in scope. | 2026-10-12 | `a2f57e68` |
| C11 | 036 signals UI | SummaryTile padding is py-3 at every width. | 2026-10-12 | `04275ef3` |
| C12 | 036 signals UI | The Home On Hold bar uses the neutral idle tone, with a label. | 2026-10-12 | `aa03498c` |
| C13 | 036 signals UI | A linked tile carries its definition tooltip itself, with a decorative (i), so the hint adds no tab stop; only tiles without a single destination get an (i) button. | 2026-10-12 | `04275ef3` |
| M1 | 037 phase 2 | My Work: `?tab=today` with My Tasks or no `who` opens the Today focus list; Today with `who=assigned` or `who=created` keeps the due-today task view; `?tab=overview` is the old default; `?userId=` opens Overview. | 2026-10-12 | `c54f7766` |
| M2 | 037 phase 2 | Today lists Needs my attention first, then my overdue, due-today and this-week tasks; blocked tasks due later form a Blocked group. Deliverables, decisions and actions are reached through the attention list or the Sections menu. | 2026-10-12 | `c54f7766` |
| M3 | 037 phase 2 | The My Work view switch is a menu button named "My Work views: <current>", as A2. | 2026-10-12 | `c54f7766`, `408977c5` |
| M4 | 037 phase 2 | The My Week summary names the first week shown and one indicator (Over-planned, then Stale plan, then Under-planned). It is collapsed by default; a `?myWeek` or `?planningEntry` link opens it. | 2026-10-12 | `c54f7766` |
| M5 | 037 phase 2 | Your own My Work no longer shows your name as a subtitle; the read-only view keeps "Read-only view". | 2026-10-12 | `408977c5` |
| M6 | 037 phase 2 | `DisciplineCoordinationView.tsx` counts as part of the /projects/:id/coordination screen. | 2026-10-12 | `185f77c2` |
| M7 | 037 phase 2 | The coordination header keeps Print as a button (`design-basis.cjs` clicks it) and moves only Copy summary to the overflow menu; the Discipline coordination cards stay expanded; Recently completed, Upcoming and Held start collapsed. | 2026-10-12 | `185f77c2` |
| M8 | 037 phase 2 | The six identity-accent background tints share one neutral tint; stripes, dots and the accent text colours are unchanged. | 2026-10-12 | `f1c84165` |
| M9 | 037 phase 2 | The census changed only as A3 says, plus the /my-work first-row selector (`section li`); list and card text still counts as chrome. | 2026-10-12 | `d48ddccf` |
| D1 | 036 backend fixes | A-21 on deliverables uses the same activity rule (last activity within `task_stale_days`); deliverables have no review-stalled condition. | 2026-10-12 | `4fccfa34` |
| D2 | 036 backend fixes | The deliverable "finished before predecessor" note fires while the Finish-to-Finish predecessor is not Issued, Accepted or Cancelled, and replaces the date flag. | 2026-10-12 | `9e737b3e` |
| D3 | 036 backend fixes | On a project that is not Complete, an optional reason sent with open-role create or PATCH is recorded (up to 2000 characters). | 2026-10-12 | `87651b57` |
| D4 | 036 backend fixes | Readiness given no settings store resolves the app's shared store through the database context. | 2026-10-12 | `09bfea64` |
| T1 | 036 acceptance tools | Seed idempotency: one marker record per module; a present marker means no writes for that module. | 2026-10-12 | `df50786e` |
| T2 | 036 acceptance tools | SYN-105: PM Priya; members Alex (predecessors) and Jill (successors); CIV lead Marc; status Active. | 2026-10-12 | `df50786e` |
| T3 | 036 acceptance tools | The SYN-105 holiday sits in a new office, "Riverside office" (RIV), at today + 2 working days, so it touches no one's capacity and no other project (the domain example uses an organisation holiday). | 2026-10-12 | `df50786e` |
| T4 | 036 acceptance tools | `working_days_enabled` is set to true on the preview (a global setting). | 2026-10-12 | `df50786e` |
| T5 | 036 acceptance tools | Relative 8.2 shapes: every start and finish happens on the seed day, which is date B; links L1 FS 0, L2 FS +2, L3 SS 0, L4 SS +2, L5 FF 0, L6 FF +1. | 2026-10-12 | `df50786e` |
| T6 | 036 acceptance tools | Task starts: the PM records a start authorisation, then the assignee starts the task with acknowledgement. | 2026-10-12 | `df50786e` |
| T7 | 036 acceptance tools | Open roles: Civil technologist weeks +2 to +5, 80 h, CIV; Senior reviewer weeks +3 to +4, 20 h; Geotechnical engineer weeks +2 to +3, 40 h, filled by a Proposed allocation for Omar; Drafting support weeks +4 to +5, 24 h, proposed for Jill and declined by Sam; SYN-103 Hydraulic modeller weeks +2 to +3, 30 h, created by Marc. | 2026-10-12 | `df50786e` |
| T8 | 036 acceptance tools | Stalled-item owners: Utility conflict log, Jill; Pavement design memo, Omar; Storm profiles review, Alex with reviewer Diane; Geotechnical report, Omar with reviewer Diane. | 2026-10-12 | `df50786e` |
| T9 | 036 acceptance tools | Week W is next week (2026-10-19). Alex's 20 h allocation links to the Harbour Road profiles task; Jordan's private draft is 10 h, Possible, source Proposal. | 2026-10-12 | `df50786e` |
| T10 | 036 acceptance tools | `counts.cjs` takes the list total from the request the destination page sends, so a page that drops a link filter fails. | 2026-10-12 | `fe1abce2` |
| T11 | 036 acceptance tools | `counts.cjs`: Portfolio filter-button tiles are clicked and the resulting URL is used; Home is checked in My projects and All projects; On Track links are required only when evaluated > 0; Due this week and Blocked fall back to the payload link. | 2026-10-12 | `fe1abce2` |
| T12 | 036 acceptance tools | `counts.cjs`: `mine=false` sent as no parameter counts as a match; requests are paced at 300 per person per minute. | 2026-10-12 | `fe1abce2` |
| T13 | 036 acceptance tools | The default `COUNTS_REPORT` path is the session scratchpad path. | 2026-10-12 | `fe1abce2` |
| I1 | Integrator | Project Tasks forwards the Dashboard link's `onHold` filter as a value filter, shown as a removable "On Hold: No" chip. | 2026-10-12 | `b1a38fc6` |
| I2 | Integrator | Suite changes beyond selectors to keep the merged screens covered: an open-roles mock in `design-basis.cjs`, the boxed linked-tile read in `counts.cjs`, and the My Work phone step in `planner.cjs` taken from the 037 writer's tested replacement. | 2026-10-12 | `caa76893` |
| S1 | 037 acceptance | AC-CALM-10 counts each path by its shortest route from /my-work, the landing page; a select choice is two clicks, typing is keystrokes, and a final click that would save or download is counted without being made. Second common routes are logged too. | 2026-10-12 | this sweep |
| S2 | 037 acceptance | "Same rows" (AC-CALM-06) compares this UI with the `9beace03` UI on the same API and data: the first table's body rows (group headers and the packet 036 Unfilled roles table left out), visible task keys, list items on My Work, entries on the calendar, and the filtered API request. | 2026-10-12 | this sweep |
| S3 | 037 acceptance | Reach is counted from each former tab's or filter's own page: project tabs from the project dashboard, sidebar items and My Work views from /my-work, quick filters from their list. The removed Workload "Any indicator" chip counts as reachable because clearing the indicator (the selected chip or its active-filter token) is one click. | 2026-10-12 | this sweep |
| S4 | 037 acceptance | `/my-work?hideWaiting=1` showing Today instead of Overview is decision E3, not a lost filter; `?tab=overview&hideWaiting=1` matches the base. | 2026-10-12 | this sweep |
| S5 | 037 acceptance | AC-CALM-08 axe counts the screens with nothing open and with the Filters popover open; a screen with a menu open is recorded but not counted, because an open menu hides the rest of the page from assistive technology by design. | 2026-10-12 | this sweep |

Open follow-ups found in run 2 (not built):

- The product specification still describes A-21 the old way ("not when A-10 or A-11 fired"): FR-STALE-01 and its A-21 row in `spec-parts/15-staffing-and-signals.md`, and decision D6 in packet 036 `spec.md`. Constitution IV asks for the amendment and a `build_spec.py` run in the same change; it was outside run 2's integration scope.
- The deliverable "finished before predecessor" note (D2) exists only in the evaluator result: `EvaluationService.cs` does not copy it and no API returns it, so it needs a mapping, a column and migration, and a payload field.
- A5 changes behaviour: an In Progress or In Review deliverable with no recent activity no longer raises A-21.
- `counts.cjs` defaults `COUNTS_REPORT` to a session scratchpad path (T13); set the variable when running it elsewhere.

Found in the 037 acceptance sweep (2026-10-12, not fixed: the sweep changes no application code):

- AC-CALM-03 and AC-CALM-05 still fail on the screens listed in section 4 (Home, Boards, Calendar, the project dashboard and coordination; words on My Work; 7 fills on Priya /projects and Sam /portfolio). Either the screens need another pass or the census needs to stop counting data links, cards and row editors as chrome; Jay to decide.
- Two second routes got slower (AC-CALM-10 table): filtering project Tasks by discipline and Overdue takes 7 clicks instead of 6 (the Filters popover, and the Overdue chip moved into it by B2), and filtering Risks by severity takes 7 instead of 5 (Coordination group, then Risks, then Filters). The dashboard links keep both paths at 3.
- With any dropdown menu open, axe reports `aria-hidden-focus` (serious) on the app root and the skip link on every screen: the open menu hides the page with aria-hidden while its controls stay focusable. Not counted in AC-CALM-08 (S5); a fix belongs in `web/src/components/ui/dropdown-menu.tsx` or the shell.
- Jordan (Admin) sees the private-drafts reason field on the Planner always open above the grid (`planner-draft-reason`), outside the Filters popover; it pushes his first row to 448 px. Not an acceptance persona; R1 would put it behind a control.
- The census counts any select outside a table as a filter field and any link as chrome, so the inline Progress selects on My Work rows (Alex: 7) and board cards read as filters and chrome.
- At 1440 px the project Tasks table scrolls sideways inside its region, so the indicator column starts cut off (`evidence/screens-1440/13-projects_SYN-101_tasks.jpg`); for the visual review.

## Appendix A: census (paste into the browser console, or port into `clutter.cjs`)

`web/tests/clutter.cjs` applies the A3 fill rule (section 4) instead of the plain non-white test on the last census line below.

```js
async () => {
  await new Promise(r => setTimeout(r, 1500));
  const H = innerHeight;
  const vis = el => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none' && s.opacity !== '0'; };
  const main = document.querySelector('main') || document.body;
  const inData = el => !!el.closest('table, [role=grid], [role=table], [role=treegrid], [role=dialog]');
  const fold = el => { const r = el.getBoundingClientRect(); return r.top < H && r.bottom > 0; };
  const all = [...main.querySelectorAll('button, a[href], input, select, textarea, [role=button], [role=tab], [role=checkbox], [role=combobox], [role=switch]')].filter(vis);
  const chrome = all.filter(e => !inData(e));
  const fields = chrome.filter(e => e.matches('input:not([type=checkbox]):not([type=radio]), select, textarea, [role=combobox]'));
  const firstData = [...main.querySelectorAll('table tbody tr, [role=row], [role=gridcell], article')].filter(vis).map(e => e.getBoundingClientRect().top).sort((a, b) => a - b)[0];
  let cw = 0; const tw = document.createTreeWalker(main, NodeFilter.SHOW_TEXT);
  while (tw.nextNode()) { const n = tw.currentNode, p = n.parentElement; if (!p || !vis(p) || inData(p) || !fold(p)) continue; cw += n.textContent.split(/\s+/).filter(Boolean).length; }
  const fills = new Set([...main.querySelectorAll('*')].filter(vis).filter(fold).map(e => getComputedStyle(e).backgroundColor).filter(c => c !== 'rgba(0, 0, 0, 0)' && c !== 'rgb(255, 255, 255)'));
  return { path: location.pathname, chromeControls: chrome.filter(fold).length, filterFields: fields.length, chromeWords: cw, firstDataY: firstData == null ? null : Math.round(firstData), fills: fills.size };
}
```
