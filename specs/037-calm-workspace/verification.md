# Verification and Acceptance Plan: Calm Workspace

**State**: Baseline measured 2026-10-11. Phase 1 (T002–T008) merged into `claude/tuesday-visual-batch1` on 2026-10-11 at `ceee4532`; Phase 2 (T009–T012) is not built, so several rows still fail by design.

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
| AC-CALM-01 | Work is visible first | First data row top ≤ 405 px (45 % of the screen) on all 17 screens | 425–1137 px | clutter.cjs | FAIL | `npm --prefix web run test:clutter` at `ceee4532` (local preview with the `StaffingSignals` migration applied), exit 1, 87 thresholds exceeded (base `5800e3fc`: 148). Priya passes 9 of 17 (base 3): /home 275, /boards 317, /projects 294, /tasks 363, /calendar 373, /reports 184, /team 363, project tasks 396, project deliverables 390. Fails: /my-work 578 and dashboard 940 (Phase 2), /portfolio 412, /workload 611, /planner 550, timeline 444, allocations 462, coordination 542. Sam: same numbers; /boards, /projects, /tasks and /team are empty for Sam and unmeasured. |
| AC-CALM-02 | Filters are closed by default | ≤ 1 visible filter field (search) with no filter active, on all 17 screens | 0–9 | clutter.cjs | FAIL | `npm --prefix web run test:clutter` at `ceee4532` (local preview with the `StaffingSignals` migration applied), exit 1, 87 thresholds exceeded (base `5800e3fc`: 148). Priya: at most 1 field on 14 of 17 (base 5). Fails: /home 2 (Home widget date range), /planner 3 (sort select and sort week in the Planner view row), allocations 6 (`Allocations.tsx` was not in the T004 list). |
| AC-CALM-03 | Calm chrome | ≤ 25 chrome controls above the fold on every screen | 11–50 | clutter.cjs | FAIL | `npm --prefix web run test:clutter` at `ceee4532` (local preview with the `StaffingSignals` migration applied), exit 1, 87 thresholds exceeded (base `5800e3fc`: 148). Priya passes: /projects 8, /tasks 19, /portfolio 9, /workload 23, /planner 19, /reports 15, /team 11, deliverables 23, allocations 25. Fails: /home 30, /my-work 26, /boards 26, /calendar 26, dashboard 37, project tasks 29 (base 50), timeline 45, coordination 36. |
| AC-CALM-04 | Fewer destinations | ≤ 7 project group tabs plus More; ≤ 8 sidebar items for Jordan, Lena, Sam, Priya, Alex and Rita | 24 tabs; 14–17 items | clutter.cjs | PASS | `npm --prefix web run test:clutter` at `ceee4532` (local preview with the `StaffingSignals` migration applied), exit 1, 87 thresholds exceeded (base `5800e3fc`: 148). Every project screen shows 6 group tabs plus More (base 24 tabs). Sidebar items on /my-work: Jordan 7, Lena 7, Sam 7, Priya 7, Alex 6, Rita 6. |
| AC-CALM-05 | Less reading, fewer colours | Chrome words above the fold ≤ 50 % of the baseline per screen; ≤ 6 fill colours above the fold | 16–232 words; 5–12 colours | clutter.cjs | FAIL | `npm --prefix web run test:clutter` at `ceee4532` (local preview with the `StaffingSignals` migration applied), exit 1, 87 thresholds exceeded (base `5800e3fc`: 148). Words within 50 % only on project tasks (48, limit 54) and deliverables (55, limit 55). Fills of 6 or fewer only on /boards 6, /calendar 6 and /reports 5. The colour budget is T012 (Phase 2). |
| AC-CALM-06 | Nothing lost | Every route in `routes.tsx` loads for a permitted persona and highlights its sidebar item and project group; every former tab and quick filter is reachable in ≤ 2 clicks from its page; every former quick-filter URL returns the same rows as at the base | — | Crawl script plus API row-count comparison | UNRUN | No route changed. Writer's ad hoc crawl at `03661892` (Priya and Jordan, 51 routes, deep links with query strings) found no page errors or not-found pages, and each project page highlights its group. No crawl script or report is saved, and the API row-count comparison of the old quick-filter URLs (T014) is not run. |
| AC-CALM-07 | Filters behave the same | Setting a filter in the popover updates the URL exactly as before, shows a removable chip and a count on the Filters button; Clear resets all; reload restores the state | — | Browser suite on Tasks, Deliverables, Planner and My Work | UNRUN | Partial: My Work filters through `test:planner` (exit 0 at `ceee4532`); project Tasks by the writer's spot check at `03661892` (popover sets `?disciplineId=`, the chip sets `?dueThisWeek=true`, "Filters 2 active", reload restores, Clear resets). Deliverables and Planner filter behaviour have no recorded run. |
| AC-CALM-08 | Keyboard and screen readers | Filters popover: Enter opens, focus moves inside, Esc closes and returns focus. More menus are operable by arrow keys. axe shows 0 serious or critical issues on the 17 screens | — | Browser and axe | UNRUN | Partial: writer's keyboard and axe spot check at `03661892` on 9 changed screens (Filters popover Enter/Esc with focus return, project More by arrow keys, axe wcag2a/aa 0 serious or critical, popover open and closed). The 17-screen axe run (T015) is not done. `test:planner`, `test:resources`, `test:handoffs` and `test:coordination` axe checks pass at `ceee4532`. |
| AC-CALM-09 | One signal per row | No row shows two indicators at once; extras appear as "+n" with a tooltip; week-specific indicators name their week (for example Alex on the Planner) | Over- and Under-planned on one row | Browser and DOM check | UNRUN | |
| AC-CALM-10 | Common tasks are not slower | Click counts for the six paths below are ≤ the baseline; recorded before and after | Measured in T002 | Click log | UNRUN | |

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
| Browser suites | `test:planner`, `test:resources`, `test:handoffs`, `test:coordination` and `test:design-basis` green; the diff of those files changes selectors only | Green: PASS at `ceee4532` (`test:planner`, `test:resources`, `test:handoffs`, `test:coordination`, `test:design-basis` all exit 0). Selectors only: not met. `design-basis.cjs`, `resources.cjs` and `planner.cjs` add steps that open the Filters popover and press Escape before using fields that moved into it; no assertion was removed. |
| Build | `npm --prefix web run build` and `lint` pass | PASS at `ceee4532`: `build` exit 0; `lint` exit 0 (0 errors, 89 warnings; the writer reports one new warning, `only-export-components` for `PINNED_QUICK` in `Tasks.tsx`). |
| API untouched | `git diff <base> -- src/` is empty for this packet | PASS: `git diff 5800e3fc 03661892 -- src/` is empty. |
| Deep links | Every URL in the existing docs, notifications and digest templates (`git grep -o "/projects/[^ )\"']*"`) still resolves to the same page | UNRUN |
| Wording | The diff contains no third-party product names | PASS: `git diff 9beace03..ceee4532 \| grep -inE '<names>'` and the same grep on the commit messages print nothing. |
| Merge | A trial merge into the base has no conflicts | PASS: `build/037-layout` merged into `claude/tuesday-visual-batch1` in `ceee4532` with no conflicts, after packet 036's API branches. |

## 7. Sign-off

| Item | Name | Date | Result |
|---|---|---|---|
| Release commit | | | |
| Measured acceptance (sections 4 and 6) | | | |
| Human acceptance (section 5) | | | |
| Final approval | Jay | | |

## Appendix A: census (paste into the browser console, or port into `clutter.cjs`)

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
