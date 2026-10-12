# Tasks: Calm Workspace

**Input**: [spec.md](spec.md), [plan.md](plan.md). Record results in [verification.md](verification.md); a ticked box is not evidence.

## Phase 0: Gate

- [ ] T001 Record Jay's answers to E1–E4 in verification.md section 3, or a note that the defaults stand.
- [ ] T002 Confirm packet 036's T003 is done (shared base). Re-run the appendix A census on the new base and replace section 2's baseline if the numbers moved.
- [ ] T003 Write `web/tests/clutter.cjs` from appendix A with the section 4 thresholds, and add it as `npm --prefix web run test:clutter`. Run it on the base: it must FAIL there, which proves the thresholds bite.

## Phase 1: Shared layout (before packet 036's UI streams)

- [ ] T004 Turn `FilterBar` into the toolbar in `web/src/components/hub/common.tsx` (R1). Then move each page's search into the `search` prop and pass `activeCount`. Pages:
  - Time, Workload, WorkspaceCoordination, Files, Search, Portfolio, Planner, WorkspaceTasks, MyWork, WorkspaceTeam, Gantt
  - projects/Milestones, ProjectList, Handoffs, Deliverables, CoordinationRegister, Readiness, Tasks, Decisions, Submissions, DesignBasis, DisciplineCoordinationView, Timeline, ProjectActivity, Changes, Meetings, Registers
  - admin/Admin

  Check: AC-CALM-02, -03 and -07.
- [ ] T005 Apply the at-most-3 quick-filter rule (R2/E4) on each page that has more. Move the rest into Views entries with the same URL parameters. Check: AC-CALM-07; every old quick-filter URL still filters the same way.
- [ ] T006 Group the project tabs and add the second-level switch in `web/src/pages/projects/ProjectLayout.tsx` (R3/E1). Make the project header one line, with overflow (R6). Check: AC-CALM-04 and -06.
- [ ] T007 Sidebar per E2 in `web/src/app/nav.ts` and `web/src/app/Shell.tsx` (R4). Check: AC-CALM-04 for Jordan, Lena, Sam, Priya, Alex and Rita.
- [ ] T008 Shorten page descriptions to one line, with a help popover for the rest (R5), on every screen in section 2. Check: AC-CALM-05.

## Phase 2: Page passes

- [ ] T009 My Work defaults to Today, with a Views switch and a collapsed My Week summary (R10/E3), in `web/src/pages/MyWork.tsx`. Check: AC-CALM-01 for /my-work.
- [ ] T010 Project Dashboard puts the counts first (R9) in `web/src/pages/projects/Dashboard.tsx`. Check: AC-CALM-01 for /projects/:id/dashboard.
- [ ] T011 One signal per row with week labels (R7) in `PlannerGrid.tsx`, `Workload.tsx` and `indicators.tsx`. Check: AC-CALM-09.
- [ ] T012 Colour budget pass (R8) on screens over the limit. Check: AC-CALM-05's colour column.

## Phase 3: Acceptance

- [ ] T013 Run `test:clutter` and the existing browser suites (`test:planner`, `test:resources`, `test:handoffs`, `test:coordination`, `test:design-basis`), updating selectors only, plus `build` and `lint`. Record the results.
- [ ] T014 Run the route crawl (AC-CALM-06) and the click-count paths (AC-CALM-10). Record before and after.
- [ ] T015 Run the keyboard pass and axe (AC-CALM-08).
- [ ] T016 Run the human sessions SC-CALM-01 (5-second test with 2 PMs) and SC-CALM-02 (Jay's visual review) on the review stack, after Jay's go-ahead to deploy.
- [ ] T017 Run the wording check on the diff, do a trial merge with no conflicts, and commit on a packet branch. Jay signs off.
