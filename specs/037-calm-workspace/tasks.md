# Tasks: Calm Workspace

**Input**: [spec.md](spec.md), [plan.md](plan.md). Record results in [verification.md](verification.md); a ticked box is not evidence.

## Phase 0: Gate

- [x] T001 Record Jay's answers to E1–E4 in verification.md section 3, or a note that the defaults stand. Done 2026-10-11: E1–E4 all Default (Jay: 'lets start build'), recorded in verification.md section 3.
- [x] T002 Confirm packet 036's T003 is done (shared base). Re-run the appendix A census on the new base and replace section 2's baseline if the numbers moved. Done 2026-10-11: 036 T003 is only partly done (the `codex/pm-*` branches are unreviewed); the census re-run on `5800e3fc` moved, and section 2 now holds it.
- [x] T003 Write `web/tests/clutter.cjs` from appendix A with the section 4 thresholds, and add it as `npm --prefix web run test:clutter`. Run it on the base: it must FAIL there, which proves the thresholds bite. Done in `8fab3350`; on the base it fails with 148 thresholds exceeded (Priya and Sam).

## Phase 1: Shared layout (before packet 036's UI streams)

- [x] T004 Turn `FilterBar` into the toolbar in `web/src/components/hub/common.tsx` (R1). Then move each page's search into the `search` prop and pass `activeCount`. (Done in `6992f11f` for all 28 pages. Its checks are not met yet: AC-CALM-02 and -03 FAIL and AC-CALM-07 is only partly run; see verification.md section 4.) Pages:
  - Time, Workload, WorkspaceCoordination, Files, Search, Portfolio, Planner, WorkspaceTasks, MyWork, WorkspaceTeam, Gantt
  - projects/Milestones, ProjectList, Handoffs, Deliverables, CoordinationRegister, Readiness, Tasks, Decisions, Submissions, DesignBasis, DisciplineCoordinationView, Timeline, ProjectActivity, Changes, Meetings, Registers
  - admin/Admin

  Check: AC-CALM-02, -03 and -07.
- [x] T005 Apply the at-most-3 quick-filter rule (R2/E4) on each page that has more. Move the rest into Views entries with the same URL parameters. Check: AC-CALM-07; every old quick-filter URL still filters the same way. Done in `6992f11f`. Deviation: the extra chips moved into the Filters popover ("More quick filters", same URL flags, 2 clicks), not into Views entries, because `views.tsx` was outside the task.
- [x] T006 Group the project tabs and add the second-level switch in `web/src/pages/projects/ProjectLayout.tsx` (R3/E1). Make the project header one line, with overflow (R6). Check: AC-CALM-04 and -06. Done in `252993fb` (grouping), `037e070c` (group pages beside the tabs) and `2c91e79b` (one-line header, 65 px).
- [x] T007 Sidebar per E2 in `web/src/app/nav.ts` and `web/src/app/Shell.tsx` (R4). Check: AC-CALM-04 for Jordan, Lena, Sam, Priya, Alex and Rita. Done in `19e0ad9b`.
- [x] T008 Shorten page descriptions to one line, with a help popover for the rest (R5), on every screen in section 2. Check: AC-CALM-05. Done in `03661892`: the Timeline and discipline coordination descriptions were the only ones longer than one line.

## Phase 2: Page passes

- [x] T009 My Work defaults to Today, with a Views switch and a collapsed My Week summary (R10/E3), in `web/src/pages/MyWork.tsx`. Check: AC-CALM-01 for /my-work. Done on `build/037-phase2` in `c54f7766` and `408977c5`, merged 2026-10-12 (`7a39e76b`): first data row 348 px (base 694).
- [x] T010 Project Dashboard puts the counts first (R9) in `web/src/pages/projects/Dashboard.tsx`. Check: AC-CALM-01 for /projects/:id/dashboard. Done on `build/036-ui-signals` in `a2f57e68`, merged 2026-10-12: first data row 270 px (base 1138).
- [x] T011 One signal per row with week labels (R7) in `PlannerGrid.tsx`, `Workload.tsx` and `indicators.tsx`. Check: AC-CALM-09. Done on `build/036-ui-staffing` in `adf8b507`, `796f33f9` and `54a50d4d` (`OneSignal` in `indicators.tsx`), merged 2026-10-12.
- [x] T012 (done 2026-10-12: one neutral tint behind every identity accent in `f1c84165`, avatars as rings in `6afb5fef`, project identity dots as rings in `9a640417`; at `a499ed72` every screen shows at most 5 chromatic fills for all six personas, verification.md AC-CALM-05) Colour budget pass (R8) on screens over the limit. Check: AC-CALM-05's colour column.

## Phase 3: Acceptance

- [x] T013 Run `test:clutter` and the existing browser suites (`test:planner`, `test:resources`, `test:handoffs`, `test:coordination`, `test:design-basis`), updating selectors only, plus `build` and `lint`. Record the results. Done 2026-10-12 at `caa76893`; results in verification.md sections 4 and 6.
- [x] T014 Run the route crawl (AC-CALM-06) and the click-count paths (AC-CALM-10). Record before and after. Done 2026-10-12 on code `caa76893`: `web/tests/calm-acceptance.cjs` (crawl, two-click reach, quick-filter rows against the `9beace03` UI) and `web/tests/calm-clicks.cjs` (before on a throwaway `9beace03` stack, after on the preview); results in verification.md section 4 and `evidence/`.
- [x] T015 Run the keyboard pass and axe (AC-CALM-08). Done 2026-10-12 on code `caa76893` with `web/tests/calm-acceptance.cjs` (keyboard on the 17 screens, axe for Priya and Sam with nothing open and with the Filters popover open); results in verification.md section 4.
- [ ] T016 (needs Jay: his go-ahead to deploy, two working PMs and his own review.) Run the human sessions SC-CALM-01 (5-second test with 2 PMs) and SC-CALM-02 (Jay's visual review) on the review stack, after Jay's go-ahead to deploy.
- [ ] T017 (wording check, merge and commit done in the finisher pass, verification.md section 6; needs Jay: his sign-off.) Run the wording check on the diff, do a trial merge with no conflicts, and commit on a packet branch. Jay signs off.
