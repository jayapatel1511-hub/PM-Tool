# Implementation Plan: Calm Workspace

**Packet**: 037 | **Spec**: [spec.md](spec.md) | **Date**: 2026-10-11 | **Base**: `claude/tuesday-visual-batch1` after packet 036's T003 merge gate

## Summary

Most of the overload comes from three shared files, so the biggest gains come from changing those first:

| Source | Used by | Change |
|---|---|---|
| `FilterBar` (`web/src/components/hub/common.tsx:153`): an always-open bordered panel | 28 screens | Becomes a one-row toolbar. Fields move into the existing `ui/popover.tsx` (`ui/sheet.tsx` under 1024 px). Active chips are inline. |
| `NAV` (`web/src/app/nav.ts:20`) and its `primary` flag | Sidebar | Mark at most 6 items as primary. The existing "More" section collapses by default. Notifications is removed from the sidebar (the header bell already exists). |
| Project `TABS` (`web/src/pages/projects/ProjectLayout.tsx:24`): 24 flat tabs | Every project page | Grouped as in E1. Group tabs on the first row; a compact second-level switch only inside a group; More holds Activity and Settings. |

Then a few page-level passes:
- the project header;
- My Work's default view;
- the Project Dashboard order;
- the Planner and Workload one-signal rule.

All UI primitives already exist in `web/src/components/ui/` (popover, sheet, dropdown-menu, tooltip, toggle-group). No dependencies are added, and nothing changes in the API.

## Design

1. **Toolbar** (`common.tsx`):
   - `FilterBar` gains `search?: ReactNode`, `activeCount: number` and `actions?: ReactNode`.
   - It renders one row: `actions` primary, then `search`, then a **Filters** button with a count badge (which opens a popover holding `children`), then `ActiveFilters` chips inline, then the overflow area.
   - Pages pass their existing search field via `search` and keep their existing fields as children, so every URL parameter and handler is unchanged.
   - The 28 page edits are mechanical: move the search `Field` into the prop, and pass `activeCount`, which is the existing `activeTokens.length`.
2. **Quick filters** (E4): each page keeps at most 3 `ChipToggle`s in the toolbar. The rest are offered as built-in entries in the existing **Views** menu (saved-view list types already exist in `Views.cs`). They are not deleted, and their URLs still work.
3. **Project navigation** (`ProjectLayout.tsx`):
   - `TABS` becomes `GROUPS: { id, label, paths[] }[]`.
   - The active group is the one whose `paths` contains the current route segment, so old deep links stay correct.
   - The second-level switch uses `ui/toggle-group.tsx` styling as links, keeping real `<a href>` for open-in-new-tab and history.
4. **Project header**: one line, with the title truncated and the full name in a tooltip. Network folder, document library and Following move into a `dropdown-menu` overflow.
5. **Sidebar** (`nav.ts`, `Shell.tsx`): the `primary` flags follow E2. Boards, Calendar and Portfolio stay as routes, are reached from their parent page's view switch, and are listed under More.
6. **My Work** (`MyWork.tsx`): defaults to the Today view. The section chips go into a Views control. The My Week strip collapses to a one-line summary that expands.
7. **Project Dashboard** (`Dashboard.tsx`): the four count cards go first; the milestone strip goes below them with reduced height.
8. **One signal per row** (`PlannerGrid.tsx`, `Workload.tsx`, list indicator cells): show the most severe chip plus "+n", with the rest in a tooltip and the detail panel. Week-specific chips include the week label.
9. **Text**: page descriptions longer than one line move into a help popover (`(i)` next to the title).

## Ordering with packet 036 (avoids rework and conflicts)

| Step | What | Why |
|---|---|---|
| 1 | Packet 036 T003 merge gate | Shared base. `codex/tuesday-ui-consistency` touches `nav.ts`, `common.tsx` and `Planner.tsx`. |
| 2 | **037 Phase 1** (toolbar, navigation, header) | 036's UI lands on these layouts. Building 036's screens first would mean doing them twice. |
| 2 in parallel | 036 Stream A (backend) | No layout files involved. |
| 3 | 036 Streams B and C (UI) under rules R1–R10 | For example: 036's group-by goes in the toolbar's Group menu; tile definitions are `(i)` tooltips, not visible text; the timeline legend sits behind a Legend button; picker capacity is a short right-aligned figure. |
| 4 | 037 Phase 2 (My Work, Dashboard, one signal per row) | Smaller, page-local. |

## Files

- **Phase 1**:
  - `web/src/components/hub/common.tsx`
  - the 28 `FilterBar` pages (listed in tasks.md T004)
  - `web/src/app/nav.ts`, `web/src/app/Shell.tsx`
  - `web/src/pages/projects/ProjectLayout.tsx`
  - `web/src/i18n/en.ts` (a block of `calm.*` keys)
- **Phase 2**:
  - `web/src/pages/MyWork.tsx`
  - `web/src/pages/projects/Dashboard.tsx`
  - `web/src/components/planner/PlannerGrid.tsx`
  - `web/src/pages/Workload.tsx`
  - `web/src/components/hub/indicators.tsx`
- **Tests**:
  - `web/tests/clutter.cjs` (new): the census from verification.md, appendix A, with thresholds.
  - Selector updates in the existing `web/tests/*.cjs` browser suites.
