# Verification and Acceptance Plan: Calm Workspace

**State**: Baseline measured 2026-10-11; nothing built. Every acceptance row starts UNRUN.

**Evidence rule**: PASS means `test:clutter` output, a crawl report, a click log or a screenshot from a stated commit. Nobody's opinion, including an agent's, counts as evidence.

## 1. Definition of accepted

1. AC-CALM-01 to AC-CALM-10 are PASS on one release commit, for Priya (PM) and Sam (Supervisor). AC-CALM-04 is also checked for Jordan, Lena, Alex and Rita.
2. SC-CALM-01 and SC-CALM-02 (human sessions) are PASS.
3. The existing browser suites, `build` and `lint` are green, and only selectors changed in the tests.
4. No feature, route, field, count or export was removed (AC-CALM-06).
5. The wording check is clean, the merge has no conflicts, and Jay signs off.

## 2. Baseline

**Setup**: local preview `pm-tuesday-preview-db`, fixture projects SYN-101 to SYN-103, signed in as Priya, viewport 1600×900, commit `121e3ae2`. Measured with the appendix A census.

**Columns**:
- *Chrome controls*: buttons, links and fields above the fold, outside tables and grids.
- *Filter fields*: visible filter inputs and selects with no filter active.
- *Chrome words*: words above the fold outside tables and grids.
- *First data row*: px from the top of the screen; "n/a" means the screen has no table rows, and T003 adds a page-specific data selector.
- *Fills*: distinct background colours above the fold.

| Screen | Chrome controls | Filter fields | Chrome words | First data row (px) | Fills |
|---|---:|---:|---:|---:|---:|
| /home | 30 | 2 | 114 | n/a | 12 |
| /my-work | 30 | 8 | 78 | 688 | 9 |
| /boards | 35 | 6 | 140 | n/a | 7 |
| /projects | 13 | 6 | 33 | 492 | 9 |
| /tasks | 27 | 4 | 43 | 473 | 9 |
| /calendar | 25 | 0 | 147 | n/a | 6 |
| /portfolio | 18 | 9 | 58 | 546 | 10 |
| /workload | 29 | 5 | 112 | 721 | 11 |
| /planner | 23 | 9 | 53 | 662 | 11 |
| /reports | 15 | 0 | 169 | n/a | 5 |
| /team | 11 | 1 | 16 | 425 | 7 |
| project dashboard | 49 | 1 | 168 | 1137 (below fold) | 10 |
| project tasks | 50 | 6 | 108 | 755 | 10 |
| project deliverables | 44 | 9 | 110 | 721 | 12 |
| project timeline | 47 | 5 | 185 | n/a | 12 |
| project allocations | 40 | 6 | 98 | 653 | 8 |
| project coordination | 41 | 4 | 232 | n/a | 5 |

Navigation at baseline:
- 24 project tabs in two rows.
- 14 sidebar items for Priya (up to 17 for an Admin).
- My Work has 7 view tabs and 14 section chips.

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
| AC-CALM-01 | Work is visible first | First data row top ≤ 405 px (45 % of the screen) on all 17 screens | 425–1137 px | clutter.cjs | UNRUN | |
| AC-CALM-02 | Filters are closed by default | ≤ 1 visible filter field (search) with no filter active, on all 17 screens | 0–9 | clutter.cjs | UNRUN | |
| AC-CALM-03 | Calm chrome | ≤ 25 chrome controls above the fold on every screen | 11–50 | clutter.cjs | UNRUN | |
| AC-CALM-04 | Fewer destinations | ≤ 7 project group tabs plus More; ≤ 8 sidebar items for Jordan, Lena, Sam, Priya, Alex and Rita | 24 tabs; 14–17 items | clutter.cjs | UNRUN | |
| AC-CALM-05 | Less reading, fewer colours | Chrome words above the fold ≤ 50 % of the baseline per screen; ≤ 6 fill colours above the fold | 16–232 words; 5–12 colours | clutter.cjs | UNRUN | |
| AC-CALM-06 | Nothing lost | Every route in `routes.tsx` loads for a permitted persona and highlights its sidebar item and project group; every former tab and quick filter is reachable in ≤ 2 clicks from its page; every former quick-filter URL returns the same rows as at the base | — | Crawl script plus API row-count comparison | UNRUN | |
| AC-CALM-07 | Filters behave the same | Setting a filter in the popover updates the URL exactly as before, shows a removable chip and a count on the Filters button; Clear resets all; reload restores the state | — | Browser suite on Tasks, Deliverables, Planner and My Work | UNRUN | |
| AC-CALM-08 | Keyboard and screen readers | Filters popover: Enter opens, focus moves inside, Esc closes and returns focus. More menus are operable by arrow keys. axe shows 0 serious or critical issues on the 17 screens | — | Browser and axe | UNRUN | |
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
| Browser suites | `test:planner`, `test:resources`, `test:handoffs`, `test:coordination` and `test:design-basis` green; the diff of those files changes selectors only | UNRUN |
| Build | `npm --prefix web run build` and `lint` pass | UNRUN |
| API untouched | `git diff <base> -- src/` is empty for this packet | UNRUN |
| Deep links | Every URL in the existing docs, notifications and digest templates (`git grep -o "/projects/[^ )\"']*"`) still resolves to the same page | UNRUN |
| Wording | The diff contains no third-party product names | UNRUN |
| Merge | A trial merge into the base has no conflicts | UNRUN |

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
