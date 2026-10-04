# Tuesday visual overhaul — handoff (2026-10-03, about 22:55 ADT)

Work in progress. **Committed locally on this branch at Jay's request (2026-10-03): the packet 034 spec, then the UI overhaul. Not pushed or deployed** (Jay: commit/push/deploy only on request).

## Where it is

- Worktree: `/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1`, branch `claude/tuesday-visual-batch1`, based on
  `codex/pm-review-release` @ `e3d6ccf8` (includes `origin/main` `49b2a4ab`). `origin/main` has since moved to `9e74863e`
  (PR #36, approved landing); its files are already copied into this worktree (see Decisions). About 110 changed or new files.
- Design contract: `/Users/jaypatel/.codex/worktrees/29d5/PM-Tool/docs/design/README.md` and `tuesday-tokens.json`.
- Rules for screen work: [2026-10-03-tuesday-migration-brief.md](2026-10-03-tuesday-migration-brief.md) (ownership, visual rules, checks).

## Decisions taken

1. **UI-only overhaul on the existing models.** Routes, APIs, calculations, permissions and ownership stay as they are.
   Allocation approval status, proposed requests and partial scope are presented with explicit labels; v1.1 planner
   concepts are not faked on the packet 029 model.
2. **Planner (Jay, 2026-10-03): "New planning layer".** v1.1's weekly planning entries become a separate record in a new
   packet 034 beside packet 029, delivered as a spec + backend + UI batch after the UI pass.
3. **Landing (Jay, 2026-10-03): use the approved Paper hero from PR #36 unchanged.** `ApprovedHero.tsx`,
   `approved-hero.css` and `web/public/landing/*` are byte-identical to `origin/main`; its `landing.*` strings are merged
   into `en.ts` at main's position; `Entrance.tsx` `LandingPage` renders `<ApprovedHero />`. Sign-in keeps the Tuesday app
   style.
4. **Open for Jay:** one brand mark (approved Terrace wordmark on the landing versus the mint "t" in sign-in and the app);
   re-capturing the hero's board image after the new UI is approved (it shows the old navy UI); behaviour items found
   during migration (no confirmation when deleting a holiday or removing a manual role; no per-notification "Mark read").

## Done

- Batch 1: tokens (`web/src/index.css`), primitives (`web/src/components/ui/*`), shared hub components, the shell
  (`Shell.tsx`: 232 px sidebar / 76 px rail / phone bottom bar, breadcrumbs, search, black quick create, density in the
  account menu), project header and tabs, and the Resources workflow (Workload, Allocations, My Staff, Reassign work).
- Shared building blocks in `components/hub/common.tsx`: `FilterBar`, `ChipToggle`, `ActiveFilters`, `Segmented`,
  `SummaryTile`, `AccentDot`, `TableRegion`, `thCls`/`tdCls`, `DesktopOnly`, `Notice`, `Missing`, density toggle.
- Screens finished by parallel writers:
  - Personal: Home, My Work, Notifications, Search, Preferences, Not found, bell, New menu.
  - Organisation: Admin, Holidays, Operations, Templates, Portfolio, Reports.
  - Sign-in and splash.
  - Projects: list, create/template/copy, dashboard, health, follow, team, settings, activity, milestones, timeline.
  - Workspace views: tasks/boards, Gantt, calendar, files, team, coordination, time, links, workspace selector/tabs.
  - Coordination: coordination page, discipline view, handoffs, reviews, changes, submissions, readiness, design basis.
  - Task workflow: task list, task panel, board, kanban, task start, comments, inline fields (every item panel), panel host.
  - Registers: risks, issues, deliverables, decisions, meetings, and their panels. Below 768 px the tables become cards.

  None of these writers opened a browser; the sweep and visual review below are what check them.
- Follow-ups from the Projects and Workspace writers (not yet done unless ticked):
  - [x] Pass `projectName` to `FollowLevelSelect`, so each select is named for its project: `Preferences.tsx` ~138 (`${f.projectNumber} ${f.name}`) and `MyWork.tsx` ~227 (`${p.projectNumber} ${p.name}`).
  - [x] Export `useIsPhone()` from `common.tsx` (`DesktopOnly` already has the media query) and use it in `Timeline.tsx` in place of its local copy.
  - [x] `Section` always renders an h2. Use h3 inside a project (the `InProject` context), as `Page` already does; Files is also a project tab.
  - [ ] `ExportMenu` is fixed at `size="sm"` (32 px) beside 40 px header buttons on Time.
  - [ ] Check that URL-bound search boxes don't drop fast keystrokes, because the router applies URL changes in a transition: `Allocations.tsx`, `Handoffs.tsx`, `Submissions.tsx`, `CoordinationRegister.tsx`, and now the register screens (`Decisions.tsx`, `Deliverables.tsx`, `Registers.tsx`). Tasks still uses the `key` remount and loses focus. Type fast in the browser, then use one approach everywhere.
  - [x] Avatar initials in `people.tsx` (confirmed 12 px).
  - [ ] Add the coordination states (Approved, Incorporated, Pending Assessment, Changes Required, Submitted, Draft, Ready, Not Met, Needs Assessment and others) to `STATUS_TONE` in `pills.tsx`. Today they show grey; `CoordStatus` in `CoordinationForms.tsx` has a local map to move.
  - [ ] Optional: a details slot on `ErrorBanner` for refusal lists, and a heading-id prop on `Section` so the `dcv-*`/`wc-h-*` cards can use it.
  - [ ] Task workflow requests:
    - `TableRegion` should accept a `ref`. The virtualised task list copies its classes inline instead.
    - In the compact `PeoplePicker`, `label` replaces the selected person's name for screen readers.
    - `ViewMenu` is also 32 px (see `ExportMenu`).
    - `DropdownMenuItem` fades disabled items to 50 %. `Tasks.tsx` overrides this locally so refusal reasons stay readable; the fix belongs in the menu primitive.
    - `WorkspaceTasks.tsx` should wrap `Swimlane` like the project board (`scroll-region` plus `w-max min-w-full`) and drop its `h-8` selects.
  - [ ] Existing task bugs, left unfixed:
    - The task search box remounts and loses focus on the first keystroke and when cleared (its `key` trick).
    - The progress slider can save twice (pointer-up, key-up and blur).
    - A save that opens a reason dialog shows "Not saved" until the confirmed save lands.
    - The inline save note briefly becomes part of the panel title heading's accessible name.
  - [ ] `task.empty` and `task.removeFilter` in `en.ts` are now unused; drop them during the fold-in.
  - [ ] Register requests:
    - `useTable` in `table.tsx` should pass column alignment (`text-right`) to its `<th>`; then delete `HeadCell` from `Decisions.tsx`.
    - Move the shared register helpers out of `Decisions.tsx` into `components/hub`: `SELECTED_ROW`, `TITLE_LINK`, `Person`, `DateText`, `GroupRow`, `RegisterCards`, `PanelHead`, `FieldGroup`. `FieldGroup` can replace TaskPanel's local `PanelSection`.
    - Choose one panel title size: TaskPanel uses 16 px because the title is edited inline; the register panels use the design's 24 px.
    - Location-kind options show raw values such as "SiteArea"; they need labels.
    - The deliverable panel's "Requires review" checkbox doesn't use `SaveStatus` yet.
    - Decisions' Clear wipes every URL parameter, including sort, columns and the open panel. Narrow it to filters.
  - Register behaviour changes to confirm:
    - Issues now has one "Clear filters", which no longer resets `group`.
    - Red row tints were replaced by explicit overdue and severity chips.
    - A failed query shows only the error banner.
  - Known gaps:
    - On phones the task list is still a scrolling table; the spec describes cards. Key and name columns don't stay pinned when the table scrolls sideways.
    - Below 768 px the coordination registers stay tables that scroll sideways.
    - The status-tone mapping and the coordination section accent colours need Jay's review.
    - Milestone strip labels truncate when the strip is narrower than about 900 px.
    - The Milestones and Team tables scroll sideways on phones instead of becoming cards.
    - Workspace lanes depend on the kanban grid's `auto-cols-[248px]` (Task workflow writer).
- Preview data: `tools/preview/seed_resources.py` and `tools/preview/seed_modules.py` (all modules, synthetic).
- Checks from batch 1 (rerun after integration): type-check, lint, build, browser suites `test:handoffs`,
  `test:coordination`, `test:design-basis`, new `test:resources`; axe at 0 violations on the Resources screens.

## Packet 034: specified, not built

No agents are running. Every screen writer and the packet 034 spec writer reported complete.

- **Written:** `spec-parts/14-weekly-planning-layer.md` (§39, plus §8.11, §10.9, §13.21 and small edits to §9.4, §13.10, §27, §29, §30) and `specs/034-weekly-planning-layer/` (spec, plan, tasks, checklist, verification).
- **New IDs:** FR-PLN-01..28, AC-PLN-01..20 and PLN-01..19. `build_spec.py --check` and `trace_spec.py --check` pass (660 IDs, 0 uncited). All application checks in `verification.md` are UNRUN.
- **Model:**
  - A person-scoped planning entry: person × ISO-week range × hours, with confidence and visibility.
  - Packet 029's Confirmed allocations appear read-only.
  - No double counting: approved hours cover a project's entries first (PLN-08).
  - Capacity matches Workload's available hours.
  - Time away is an availability override, not a separate leave record.
- **Screens:** Weekly Planner at `/planner`, and a My Week strip on My Work.
- **Open decisions:** §39.12 lists 18, each with a default the spec uses. The most consequential:
  - Supervisors see direct reports only.
  - Only a supervisor or admin records time away.
  - PMs and Discipline Leads can't create entries.
  - Entries use week grain.
  - Partial view follows Workload's rule, so supervisors never see Under-planned.
- **Plan boundary:**
  - The backend is new files plus additive edits.
  - `Workload.cs` changes three members from private to internal; no behaviour change.
  - Must not change: `Allocations.cs`, `Domain/Workload.cs`, `Readiness.cs`, `AllocationEntities.cs`, existing migrations.
- **Housekeeping:**
  - `CLAUDE.md` and `docs/SPEC-KIT-WORKFLOW.md` still say packets 001–033; Jay to approve that edit.
  - The design README in the Codex worktree needs the "Self-entered" label and a pointer to §13.21. That is outside this worktree.
  - `specs/011-hardening-and-pilot/verification.md` shows as modified because it was restored to `origin/main`'s content.

Area strings live temporarily in `web/src/i18n/en-<area>.ts` (personal, workspace, projects, work, registers,
coordination, org, entrance), merged at runtime by a glob in `web/src/lib/i18n.ts`.

## Next steps, in order

1. All areas reported complete, but none was opened in a browser. From `web/`, run `npx tsc -p tsconfig.app.json --noEmit --incremental false` and `npx oxlint`.
2. Fold `web/src/i18n/en-*.ts` into `en.ts` with no duplicate keys, delete the area files, and remove the glob from `lib/i18n.ts`.
3. Remove the unused `--frame*` tokens from `index.css` (grep for `frame` first).
4. `npm run build`, then the browser suites. Playwright 1.61 needs the cached browser:
   `CHROME_EXECUTABLE_PATH=/Users/jaypatel/Library/Caches/ms-playwright/chromium_headless_shell-1234/chrome-headless-shell-mac-arm64/chrome-headless-shell`.
   Run `test:design-basis` with `PORT=5186`, because another session's server holds 5176. `test:handoffs` needs port 5173 free, so stop the dev server first.
5. With the preview running: `CHROME_EXECUTABLE_PATH=… node tools/preview/sweep.cjs 1440,1024,768,375`. It reports page
   errors, axe (WCAG 2.0 to 2.2 AA), sideways overflow, text under 12 px, raw string keys and old palette colours for each
   route, role and width. Fix the findings.
6. Review every screen in the browser as priya, sam, lena, jordan, alex and rita.
7. Packet 034:
   - Review `spec-parts/14` and `specs/034` (`/speckit-analyze`). Use the §39.12 defaults unless Jay has answered them.
   - Implement the domain rules, the API with its EF migration, privacy filters and tests (`tests/Hub.Tests`; API tests create temporary `hub_test_*` databases in the dev Postgres on 55432).
   - Then build the planner UI and record the results in `specs/034-weekly-planning-layer/verification.md`.
8. Report to Jay, offer commit/PR. Do not deploy.

## Preview

- Database: Docker (colima) container `pm-tuesday-preview-db`, 127.0.0.1:55433, volume of the same name. Never use `pm-tool-db-1`.
- API (from the worktree root):
  `dotnet run --project src/Hub.Api --no-launch-profile -- --environment Development --urls http://localhost:5080 --ConnectionStrings:Hub "Host=localhost;Port=55433;Database=hub;Username=hub"`
  (also `.claude/launch.json` config `api-preview`). Web: `npm --prefix web run dev` (port 5173).
- Sign in at http://localhost:5173/login as a development user:
  - Sam: supervisor, partial view.
  - Priya: project manager for SYN-101.
  - Lena: executive.
  - Jordan: admin.
  - Alex: no Resources access.
  - Rita: read-only.
- Data notes:
  - Seed dates are anchored to 2026-10-03.
  - One synthetic decision link was soft-deleted to avoid the Readiness 500 (see Known issues).
  - Verification added an extra 20 h proposal for Alex on SYN-101 (Oct 5–9), moved SYN-101-T0010 to Alex, and edited the descriptions of T0009 and T0010.
  - To reset: `docker rm -f pm-tuesday-preview-db && docker volume rm pm-tuesday-preview-db`, start the container again (see `seed_resources.py`), start the API, then run both seed scripts.

## Known issues

- **Backend defect (pre-existing in `main`, not fixed in this UI branch):** `src/Hub.Api/Features/Readiness.cs:108` filters after
  projecting into the `LinkedRecord` record, which EF Core cannot translate. Any task with readiness inputs and a "blocked by
  decision" link makes the project readiness window and the discipline-coordination views return HTTP 500. A separate fix task
  was raised; fix it by filtering on the id before the projection, and add a regression test.
- Batch-1 notes still apply: an 8-week grid (API constant); partial scope is decided by role in the API; capacity values
  over 80 h get a misleading server message, which the UI now prevents; a supervisor who cannot see all of a person's
  projects cannot preview capacity, and the UI explains this.

## Restart prompt

Paste this whole block into a new session after this session's agents have stopped:

```text
GOAL: Finish the Tuesday visual overhaul and packet 034, so Jay can do one final visual review before anything is committed.

Work in /Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1 (branch claude/tuesday-visual-batch1). Read these first:
- docs/handoffs/2026-10-03-tuesday-overhaul-handoff.md: state, done list, follow-up checklist, preview commands.
- docs/handoffs/2026-10-03-tuesday-migration-brief.md: visual rules, required states, checks.
- The design contract: /Users/jaypatel/.codex/worktrees/29d5/PM-Tool/docs/design/README.md and tuesday-tokens.json.

You are done when all of these are true:
1. Every screen meets the Tuesday standard.
   - Every writer reported complete without opening a browser. Treat their work as unverified until items 3 to 5 pass.
   - Every unticked follow-up and known gap in the handoff is fixed, or deferred with a stated reason.
2. Integration is complete.
   - web/src/i18n/en-*.ts are folded into en.ts with no duplicate keys, the area files are deleted, and the glob is removed from web/src/lib/i18n.ts.
   - The unused --frame* tokens are gone from index.css.
3. These checks pass in web/:
   - Type-check: npx tsc -p tsconfig.app.json --noEmit --incremental false
   - npm run lint, with no new warnings.
   - npm run build
   - npm run test:handoffs, test:coordination, test:design-basis (PORT=5186) and test:resources. Set CHROME_EXECUTABLE_PATH as the handoff says.
4. With the preview running, node tools/preview/sweep.cjs 1440,1024,768,375 reports, for every route and role:
   - zero page errors
   - zero axe violations
   - no sideways page overflow
   - no text under 12 px
   - no raw string keys
   - no old palette colours
5. Every screen has been reviewed in the browser as priya, sam, lena, jordan, alex and rita, at desktop and phone widths.
   - States: hover, focus, selected, disabled, loading, empty, saving, failed save, conflict and read-only.
   - Edge cases: long labels, missing values, zero hours, negative capacity, and restricted or private records.
6. Packet 034 (new weekly planning layer, Jay's choice (b)) is built.
   - Run /speckit-analyze on specs/034-weekly-planning-layer and fix the gaps it finds.
   - Use the defaults in §39.12 unless Jay has answered them, and list every default you relied on in the report.
   - Implement every task in specs/034-weekly-planning-layer/tasks.md: domain rules, EF migration, endpoints, privacy filters, permissions, tests in tests/Hub.Tests, and the planner UI in the Tuesday style.
   - python3 tools/build_spec.py --check and python3 tools/trace_spec.py --check pass.
   - Results are recorded in specs/034-weekly-planning-layer/verification.md.
7. tools/preview/seed_modules.py has been read in full and reviewed, and all preview data is clearly labelled synthetic.
8. Jay has a final report covering:
   - what changed, with evidence from the checks
   - the local preview URL and the sign-in users
   - remaining limitations
   - the open decisions: brand mark, re-capturing the hero board image, confirmations for deleting holidays and removing roles, and per-notification mark-read

Rules:
- The canonical spec-parts govern scope, workflows, permissions and calculations; section 10 wins.
- Preserve unrelated changes. Read files before editing them, and never revert another writer's work.
- Outside packet 034, change the UI only:
  - Keep routes, authentication, APIs, calculations, ownership and permissions as they are.
  - Keep allocation source, confidence and visibility distinct. Never relabel packet 029 statuses as confidence or visibility.
  - Do not promote Phase 2 features into MVP. Do not weaken security for the preview.
- No AI, ML or LLM features.
- Data:
  - Use only the preview database pm-tuesday-preview-db (127.0.0.1:55433).
  - Never touch the data in pm-tool-db-1. API tests creating temporary hub_test_* databases on 55432 is expected.
- The Readiness.cs:108 backend bug is tracked as a separate task. Do not fix it here unless it blocks a check, and say so if it does.
- You may run up to 10 parallel subagents: one writer per file area, with explicit file ownership. Integrate and re-verify their work yourself.
- Do not commit, push or deploy. Keep the handoff doc current as you go, so another session can resume.
- If a goal item cannot be met, stop on that item and report the precise blocker rather than working around it.
```

## Resume audit — 3 October 2026 (Codex)

- Confirmed worktree and branch `claude/tuesday-visual-batch1`; existing dirty work preserved.
- Read handoff, migration brief and external Tuesday design contract. Previous screen completion claims remain unverified.
- Ownership: planner helper owns packet 034 analysis and backend; Task/Registers helper owns its UI; main agent owns shared components, integration, planner UI, preview, verification and this handoff.
- No commit, push or deployment authorized. Required checks and full role/width/state browser review remain pending.
