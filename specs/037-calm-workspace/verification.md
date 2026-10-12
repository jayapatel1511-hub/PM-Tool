# Verification and Acceptance Plan: Calm Workspace

**State**: Baseline measured 2026-10-11. Phase 1 (T002–T008) merged into `claude/tuesday-visual-batch1` on 2026-10-11 at `ceee4532`. Phase 2 (T009–T012) merged on 2026-10-12 in run 2 (`7a39e76b`, with packet 036's web streams); integration fixes in `b1a38fc6` and `caa76893`. Measured rows are from `caa76893`; AC-CALM-03 and -05 still fail on some screens, and the crawl, click-count and human rows are not run.

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
| AC-CALM-01 | Work is visible first | First data row top ≤ 405 px (45 % of the screen) on all 17 screens | 425–1137 px | clutter.cjs | PASS | `TUESDAY_UI_BASE_URL=http://localhost:5173 npm --prefix web run test:clutter` at `caa76893` (shared API on 5080 at `7a39e76b`, preview with the run 2 fixtures), exit 1, 20 thresholds exceeded (`ceee4532`: 87; base `5800e3fc`: 148). Priya: all 17 screens ≤ 405 px: /home 255, /my-work 348, /boards 317, /projects 294, /tasks 363, /calendar 319, /portfolio 405 (at the limit), /workload 377, /planner 308, /reports 231, /team 363, dashboard 270, project tasks 396, deliverables 391, timeline 348, allocations 395, coordination 386. Sam: the same where measured; /my-work, /boards, /projects, /tasks and /team are empty for Sam and unmeasured. |
| AC-CALM-02 | Filters are closed by default | ≤ 1 visible filter field (search) with no filter active, on all 17 screens | 0–9 | clutter.cjs | PASS | `TUESDAY_UI_BASE_URL=http://localhost:5173 npm --prefix web run test:clutter` at `caa76893` (shared API on 5080 at `7a39e76b`, preview with the run 2 fixtures), exit 1, 20 thresholds exceeded (`ceee4532`: 87; base `5800e3fc`: 148). At most 1 visible filter field on all 17 screens for Priya and Sam (0 on /home, /my-work, /calendar, /workload, /reports and timeline; 1 elsewhere). |
| AC-CALM-03 | Calm chrome | ≤ 25 chrome controls above the fold on every screen | 11–50 | clutter.cjs | FAIL | `TUESDAY_UI_BASE_URL=http://localhost:5173 npm --prefix web run test:clutter` at `caa76893` (shared API on 5080 at `7a39e76b`, preview with the run 2 fixtures), exit 1, 20 thresholds exceeded (`ceee4532`: 87; base `5800e3fc`: 148). Priya passes 12 of 17: /my-work 20, /projects 9, /tasks 19, /portfolio 11, /workload 24, /planner 17, /reports 1, /team 11, project tasks 25, deliverables 25, timeline 25, allocations 24. Fails: /home 36, /boards 38, /calendar 27, dashboard 38, coordination 42. Sam fails only the dashboard (36) and coordination (40). The writers trace the rest to data links that the census counts as chrome (Home and dashboard count links, board cards, calendar deadline chips) and to the 21 controls of the shared project header and tabs. |
| AC-CALM-04 | Fewer destinations | ≤ 7 project group tabs plus More; ≤ 8 sidebar items for Jordan, Lena, Sam, Priya, Alex and Rita | 24 tabs; 14–17 items | clutter.cjs | PASS | `TUESDAY_UI_BASE_URL=http://localhost:5173 npm --prefix web run test:clutter` at `caa76893` (shared API on 5080 at `7a39e76b`, preview with the run 2 fixtures), exit 1, 20 thresholds exceeded (`ceee4532`: 87; base `5800e3fc`: 148). Every project screen shows 6 group tabs plus More. Sidebar items on /my-work: Jordan 7, Lena 7, Sam 7, Priya 7, Alex 6, Rita 6. |
| AC-CALM-05 | Less reading, fewer colours | Chrome words above the fold ≤ max(40, 50 % of the baseline) per screen; ≤ 6 chromatic fill colours above the fold (HSL saturation ≥ 15 % and lightness 8–96 %, composited on white) | 16–232 words; 5–12 colours | clutter.cjs | FAIL | `TUESDAY_UI_BASE_URL=http://localhost:5173 npm --prefix web run test:clutter` at `caa76893` (shared API on 5080 at `7a39e76b`, preview with the run 2 fixtures), exit 1, 20 thresholds exceeded (`ceee4532`: 87; base `5800e3fc`: 148). A3 limits. Words fail for Priya on /home 105 (limit 57), /my-work 225 (40), /boards 256 (70), /calendar 138 (75), dashboard 143 (84) and coordination 226 (116); for Sam on /home 83 (57), /projects 49 (40, empty state), /workload 71 (56; for Sam the Unfilled roles heading and note reach above the fold), dashboard 143 and coordination 220. Fills: at most 6 on 16 of 17 screens for each persona; over by one are Priya /projects 7 and Sam /portfolio 7 (project identity dots plus status fills). |
| AC-CALM-06 | Nothing lost | Every route in `routes.tsx` loads for a permitted persona and highlights its sidebar item and project group; every former tab and quick filter is reachable in ≤ 2 clicks from its page; every former quick-filter URL returns the same rows as at the base | — | Crawl script plus API row-count comparison | UNRUN | No route changed. Writer's ad hoc crawl at `03661892` (Priya and Jordan, 51 routes, deep links with query strings) found no page errors or not-found pages, and each project page highlights its group. No crawl script or report is saved, and the API row-count comparison of the old quick-filter URLs (T014) is not run. Run 2 changed no route (`git diff 86e40ef6..HEAD -- web/src/app/` is empty); the crawl is still not run. |
| AC-CALM-07 | Filters behave the same | Setting a filter in the popover updates the URL exactly as before, shows a removable chip and a count on the Filters button; Clear resets all; reload restores the state | — | Browser suite on Tasks, Deliverables, Planner and My Work | UNRUN | Partial: My Work filters through `test:planner` (exit 0 at `ceee4532`); project Tasks by the writer's spot check at `03661892` (popover sets `?disciplineId=`, the chip sets `?dueThisWeek=true`, "Filters 2 active", reload restores, Clear resets). Deliverables and Planner filter behaviour have no recorded run. Run 2 at `caa76893`: `test:planner` (My Work at 375 px: one Filters control, the popover opens and closes, a chip clears the filter) and `test:design-basis` (basis, submissions and allocations keep URL filters and page reset) exit 0; `test:resources` drives the Workload Sort menu. Deliverables and Planner filter behaviour still has no recorded run. |
| AC-CALM-08 | Keyboard and screen readers | Filters popover: Enter opens, focus moves inside, Esc closes and returns focus. More menus are operable by arrow keys. axe shows 0 serious or critical issues on the 17 screens | — | Browser and axe | UNRUN | Partial: writer's keyboard and axe spot check at `03661892` on 9 changed screens (Filters popover Enter/Esc with focus return, project More by arrow keys, axe wcag2a/aa 0 serious or critical, popover open and closed). The 17-screen axe run (T015) is not done. `test:planner`, `test:resources`, `test:handoffs` and `test:coordination` axe checks pass at `ceee4532`. Run 2 writer checks: axe (wcag2a/aa/21a/21aa) shows 0 serious or critical issues on /home, /portfolio, the dashboard, the SYN-105 timeline, /projects and /team (`becca7b7`), and on /my-work, /reports, /calendar, /boards, /tasks and coordination apart from the app-wide aria-hidden-focus finding while any dropdown menu is open (`408977c5`); the Filters popover, the menus and the project details popover work by keyboard and return focus. Suite axe checks pass at `caa76893`. /workload, /planner, project tasks, deliverables and allocations are covered only by the suites' mocked pages, so the 17-screen run (T015) is still open. |
| AC-CALM-09 | One signal per row | No row shows two indicators at once; extras appear as "+n" with a tooltip; week-specific indicators name their week (for example Alex on the Planner) | Over- and Under-planned on one row | Browser and DOM check | PASS | Writer DOM check at `54a50d4d` (`onesignal.cjs`, before the merge; later merges did not touch these rows): at most 1 indicator chip per table row on /planner (Priya, Lena), /workload (Priya, Sam), SYN-101 tasks and deliverables and SYN-105 tasks. Extras show as '+n' with a tooltip and aria-label; Planner and Workload chips name their weeks (for example 'Over-planned · Oct 12, Oct 19' +1), so the Planner row that showed Over- and Under-planned together shows one signal. |
| AC-CALM-10 | Common tasks are not slower | Click counts for the six paths below are ≤ the baseline; recorded before and after | Measured in T002 | Click log | UNRUN | |

Threshold refinement (A3, 2026-10-12, `d48ddccf`): AC-CALM-05 counts only chromatic fills and floors the word limit at 40 words. The census in appendix A counted every non-white background, so greys, near-whites and near-blacks (surfaces, hover states, borders drawn as fills) inflated the count without adding any colour, which is what R8 limits; and half of a small baseline (for example /team 16 → 8, /tasks 43 → 21) leaves fewer words than a title, a one-line scope and a toolbar need. Section 2's Fills column was measured with the old rule, so it is not comparable with the counts below.

Paths for AC-CALM-10:
1. Open my most overdue task from My Work.
2. Show SYN-101 Civil tasks that are overdue.
3. Open SYN-101's high risks.
4. Add 8 h of planning for Alex next week.
5. Export SYN-101 deliverables.
6. Change a task's owner.

## 5. Success criteria (people)

| ID | Session | Who | Pass | Status |
|---|---|---|---|---|
| SC-CALM-01 | 5-second test: show project Tasks, My Work and the Planner for 5 seconds each, then ask "What is this page for, and what would you click first?" | 2 working PMs, not Jay | Both name the purpose and the primary action on all 3 screens | UNRUN |
| SC-CALM-02 | Visual review of the 17 screens at 1440 and 1024 widths against the baseline screenshots | Jay | Jay approves: "neat and clean, nothing missing" | UNRUN |

## 6. Regression guard

| Check | Pass condition | Status |
|---|---|---|
| Browser suites | `test:planner`, `test:resources`, `test:handoffs`, `test:coordination` and `test:design-basis` green; the diff of those files changes selectors only | Green: PASS at `caa76893`: `test:planner` (20 flows, 13 axe reports, 0 violations), `test:resources`, `test:handoffs`, `test:coordination` (0 axe violations) and `test:design-basis` (unmockedGets []) all exit 0. Selectors only: not met. Run 2 adds an open-roles mock to `resources.cjs` (036 staffing writer) and `design-basis.cjs` (integrator, `caa76893`), reaches 'Copy last week' through the More actions menu, uses the Workload Sort menu and the 'Partial view' status, and replaces the My Work phone step's assertions about the removed duplicate toggle and Sections nav with assertions about the single Filters control and the views menu. Earlier: Green: PASS at `ceee4532` (`test:planner`, `test:resources`, `test:handoffs`, `test:coordination`, `test:design-basis` all exit 0). Selectors only: not met. `design-basis.cjs`, `resources.cjs` and `planner.cjs` add steps that open the Filters popover and press Escape before using fields that moved into it; no assertion was removed. |
| Build | `npm --prefix web run build` and `lint` pass | PASS at `caa76893`: `build` exit 0; `lint` exit 0 (0 errors, 94 warnings; 89 at `86e40ef6`, the 5 new are `only-export-components` warnings in `people.tsx` and `Timeline.tsx`). Earlier: PASS at `ceee4532`: `build` exit 0; `lint` exit 0 (0 errors, 89 warnings; the writer reports one new warning, `only-export-components` for `PINNED_QUICK` in `Tasks.tsx`). |
| API untouched | `git diff <base> -- src/` is empty for this packet | PASS for Phase 2: `git diff 86e40ef6 build/037-phase2 -- src/` is empty. Earlier: PASS: `git diff 5800e3fc 03661892 -- src/` is empty. |
| Deep links | Every URL in the existing docs, notifications and digest templates (`git grep -o "/projects/[^ )\"']*"`) still resolves to the same page | UNRUN |
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

Open follow-ups found in run 2 (not built):

- The product specification still describes A-21 the old way ("not when A-10 or A-11 fired"): FR-STALE-01 and its A-21 row in `spec-parts/15-staffing-and-signals.md`, and decision D6 in packet 036 `spec.md`. Constitution IV asks for the amendment and a `build_spec.py` run in the same change; it was outside run 2's integration scope.
- The deliverable "finished before predecessor" note (D2) exists only in the evaluator result: `EvaluationService.cs` does not copy it and no API returns it, so it needs a mapping, a column and migration, and a payload field.
- A5 changes behaviour: an In Progress or In Review deliverable with no recent activity no longer raises A-21.
- `counts.cjs` defaults `COUNTS_REPORT` to a session scratchpad path (T13); set the variable when running it elsewhere.

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
