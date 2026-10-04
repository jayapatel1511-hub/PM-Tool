# Tuesday visual overhaul — current handoff (4 October 2026)

**Locally implemented and software/browser checks PASS. Overall goal BLOCKED at T023's manual screen-reader validation. Jay has now explicitly authorized committing and deploying the reviewed synthetic-review candidate; release preparation/CI and interactive sudo remain pending.** Worktree `/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1`, branch `claude/tuesday-visual-batch1`, current baseline `9d3e9b91`. Another session committed packet 034's specification (`586fc48b`) and the existing visual batch (`9d3e9b91`) during this run. Jay explicitly answered: “Continue from 9d3e9b91; leave remaining work uncommitted.” Preserve those commits and all unrelated edits.

## Deployment authorization — 4 October, evening

Jay returned home and explicitly answered **“Yes, commit and deploy synthetic review.”** This supersedes the earlier uncommitted/no-deployment hold for the reviewed candidate. Current preparation is for the existing review stack only, preserving unrelated edits and all pilot/operational data. Host preflight verified the current review and pilot pointers at `29b3d7036f529b513fdc2ad47cff62a0b75f0b59`, review origin health HTTP 200 with the configured proxy headers, and the existing tunnel active. That release is an ancestor of the candidate. The candidate's 114 selected files and 315 frozen application/test/build sources had zero drift since final checks. GitHub authentication is valid when checked outside the network sandbox.

Prepare a reviewed commit, incorporate the approved Paper hero commit from origin/main, and pass the required draft-PR CI before transferring the exact committed tree. Interactive homedev sudo remains a user step. Deployment is not yet claimed complete. T023 stays BLOCKED/UNRUN and human acceptance/pilot remains pending; Jay authorized this synthetic review deployment with that limitation disclosed. Historical local-only status statements below describe the earlier snapshot.

## Review/fix/release follow-up — 4 October

Jay requested another review/fix loop and deployment. **Local review/fixes/checks PASS; deployment BLOCKED, not attempted.** Independent review found and repaired planner draft loss on failed reload, metadata-action draft loss, time-away conflict recovery and stale Retry after editing. Exact type-check, lint (87 baseline/current; zero new), synthetic build, all five browser suites and local Release publish pass. Planner: 14 base flows plus six recovery regressions, 180 API calls, three entries, two notices, seven axe scans, zero errors/unknown requests/violations. All 96 affected six-role/four-width route cases cold-rechecked; retained full first pass plus rechecks has 1,956 unique combinations and zero findings. Source hashes stayed frozen. English remains 3,561 unique keys.

Planner tests now run in CI. The shared Dockerfile's synthetic flag defaults false, with review-only Compose true; company pilot remains false and LocalPassword/auth/seed boundaries unchanged. Dummy-only configuration checks pass. No Docker image or exact-commit hosted CI/runtime was claimed verified. Full backend evidence is retained unchanged; no database suite repeat or seed write in this loop.

Deployment cannot proceed under the earlier uncommitted/no-homedev-authentication instructions. The established runbook transfers only a committed Git tree, and activation calls interactive sudo in Jay's own terminal. Do not transfer baseline 9d3e9b91: it omits the current implementation/repairs. No SSH/authentication/host data action was attempted. Await clarification for a local reviewed commit; host activation stays held while Jay is away. T023 manual screen-reader remains BLOCKED/UNRUN, separate from pending human acceptance/pilot.

See the [release-loop evidence and exact blockers](../reviews/2026-10-04-tuesday-release-review-loop.md). Latest logs use `/private/tmp/tuesday-*-release-final.log`; sweep evidence is `/private/tmp/tuesday-sweep-release-final/`. The local preview is running again on 5173 with the synthetic flag (session 78090), API remains on 5080.

## Additional UI/UX pass — completed

Jay requested overlap, button placement and usability review, then explicitly requested menus wrap into extra rows instead of horizontal scrolling. No homedev access or authentication actions are permitted while he is away. All work stays on the local synthetic preview.

Confirmed, verified repairs: duplicate planner Close controls (overlapping measured rectangles), misleading My Week count/heading, ambiguous stale filters, forbidden/error reads shown as empty results, mobile My Work filter disclosure, wrapped Project/Search/Admin/shared tab menus, and persistent dialog titles/Close while long forms scroll. Time conflict recovery is implemented and independently checked, including failed Reload retention, reason retention, latest rowVersion and loss of edit access. The expanded menu sweep exposed 36 tablet breadcrumb overflows; the 450-row interrupted run is preserved in `/private/tmp/tuesday-sweep-ux-interrupted/`. Breadcrumb groups now wrap, and the repaired-source fresh full replacement sweep completed **1,956 unique combinations with zero findings**, including page/main/menu overflow, in `/private/tmp/tuesday-sweep-ux-final2/`. Web source hashes stayed unchanged during the sweep. Required web checks and all five browser suites pass on the final build (UX final3; Handoffs UX final). All 12 live desktop/phone menu captures were reviewed; 108 focused dialog observations include 40 applicable passes and 68 explicit NAs. The [additional UI/UX report](../reviews/2026-10-04-tuesday-ui-ux-review.md) records repairs, evidence and deferred refinements. Earlier final12/final7–8 evidence remains retained; it is not claimed as verification of these new changes. The current blocker remains T023 manual screen-reader validation; no attempt requiring homedev authentication will be made.

## Authority and decisions

- Canonical `spec-parts/`, section 10 first. Packet 034 is the approved new planning layer beside packet 029; its confidence and visibility do not replace allocation approval status.
- Outside 034: UI only. No changes to routes, authentication, APIs, calculations, ownership or permissions. No AI features, no Phase 2 promotion.
- Read the [migration brief](2026-10-03-tuesday-migration-brief.md), external `/Users/jaypatel/.codex/worktrees/29d5/PM-Tool/docs/design/README.md` and `tuesday-tokens.json`.
- Approved Paper landing from PR #36 remains the visual authority for the hero. Jay's four open decisions remain: unify the brand mark; re-capture the hero board image after UI approval; confirm holiday deletion/manual-role removal behavior; per-notification Mark read. These require Jay's product decisions and are deferred, not silently changed.
- Packet 034 uses all 18 §39.12 defaults. They are reproduced in [verification.md](../../specs/034-weekly-planning-layer/verification.md). Comments, self-recorded time away, all-staff Supervisor access and pointer dragging remain deferred by the canonical MVP contract.
- The external design README's Self-entered/pointer addition is deferred: that contract is maintained in a separate worktree. Local planner labels and the planning guide implement the canonical distinction and pointer here.

## Follow-ups from the previous handoff

- [x] Follow selectors have project-specific accessible names; shared `useIsPhone`; project Sections use h3.
- [x] ExportMenu and ViewMenu use 40 px header actions.
- [x] URL-bound searches use one optimistic input that retains fast typing/focus and honors URL Clear/back/saved views. Direct browser typing verified on Tasks and Allocations; broad state checks pass.
- [x] Coordination tones moved to shared pills; readable disabled dropdown items; PeoplePicker compact name includes selected person.
- [x] ErrorBanner details slot; Section headingId; TableRegion accepts ref/props; table headers honor numeric alignment.
- [x] Workspace board has internal sideways lane scrolling and normal-height controls.
- [x] Task duplicate slider saves guarded; reason dialog waits before reporting failure; title accessible name excludes save feedback.
- [x] Register helpers shared; TaskPanel uses the same 24 px title; location kinds labelled; deliverable review checkbox SaveStatus; Decisions Clear preserves sort/columns/panel.
- [x] Unused `task.empty`/`task.removeFilter` strings removed.
- [x] Phone cards implemented for Tasks, Team, Milestones/targeted deliverables, Handoffs, Reviews, Changes and Submissions. Desktop task key/name pinned. Milestone strip uses short keys; full labels are visible in the adjacent list.
- [x] Issues Clear retains grouping; overdue/severity use explicit chips; failed queries show ErrorBanner. These UI behavior choices stand pending Jay's final review.
- [x] Nine area translation files folded into `en.ts`, 3561 current keys, all unique (TypeScript AST check); 3417 at fold time; area files and runtime glob deleted.
- [x] Eight unused `--frame*` variables removed after checking usages.

## Current evidence and work remaining

Backend verification is final: 1,036/1,036 Hub.Tests pass, no failures/skips; domain branch coverage 96.6 %, feature line coverage 94.2 %, PlanningRules branches 100 %. EF reports no pending model changes. Both additive preview migrations are applied. The 30-sample planning scale check passes (grid p95 26.32 ms; My Week 14.90 ms). Existing packet 029 and Readiness command/calculation sources remain unchanged; Workload edits expose internal helpers only.

Retained frontend checkpoint UX final3: exact type-check PASS; lint PASS with 87 baseline/current warnings and zero new file/rule/message diagnostics; build PASS. Handoffs, Planner, Coordination, Design Basis (5186) and Resources suites PASS on the final local build. Handoffs port 5173 was freed for its suite and the synthetic Vite preview has been restarted.

Historical evidence retained: the fresh final7 live sweep measured **1,956 combinations**, including six users, 81 routes each plus Jordan's actual draft template, four widths and public landing/sign-in: zero errors, axe violations, document/main horizontal overflow, text below 12 px, raw keys or old palette flags. A stronger keyboard test then exposed the planner's Escape focus-loss bug. It is repaired and the strict keyboard test passes. **Final8 cold-rechecked all 96 affected Planner/My Work route-role-width combinations; the complete combined 1,956-row result still has zero findings.** Both the unmodified zero-finding first pass and fresh rechecks are retained in `/private/tmp/tuesday-sweep-final8/`. Earlier failed evidence remains in final5; deliberately interrupted final6 is not claimed complete.

Broad state audit PASS: **974 desktop/phone route-role combinations**, browser-only loading/empty/failed-load fixtures, no runtime errors, all live writes blocked. All 101 initial focus captures affected by the old outline utility ordering were cold-rechecked and now show solid keyboard focus. Full normal visual review completed all 108 contact sheets, with 81 routes per role/width and Jordan 82. See `/private/tmp/tuesday-visual-final-review.md`.

Task state audit PASS for all 12 user/width contexts: real role permissions; editable Priya/Jordan/Alex and read-only Sam/Lena/Rita; long labels, missing/zero values, history failure/retry, pending/500/409 drafts and changed-server values. Date/select/person failures retain the attempted input, and progress release+blur issues one command with actual outcome feedback. Evidence `/private/tmp/tuesday-task-states-v2/results.json`.

Retained UX final3 Planner acceptance PASS: 14 flows, 150 API calls, 3 created entries, 2 notices, five axe scans and zero errors/unknown requests. Exact same-body/idempotency retry after 500, original-version conflict followed by explicit reload, ordinary-user self entry and exact six-week My Week query are asserted. The stronger keyboard protocol asserts all four arrows, Enter quick add, natural cell focus after Escape/save, Tab into the entry panel, and natural exact-opener focus after close. Latest live six-role Planner/My Work axe checks PASS in the fresh UX full sweep: all 96 base-route/role/width combinations, zero page errors/violations. The earlier separate 36-case audit is retained. Human supervisor timing and VoiceOver remain UNRUN.

- [x] Final backend full suite, coverage, EF model and scale checks.
- [x] Full planner stateful acceptance assertions implemented; parent independently checked exact request bodies.
- [x] Fresh UX final2 six-role/four-width sweep, 1,956 unique checks with zero findings including page/main/menu overflow; final7/final8 retained. Six-role desktop/phone normal visual review plus additional menu/dialog UX checks.
- [x] Broad loading/empty/failure/focus/selected/disabled/read-only states and Task workflow edge-state review.
- [x] Editor saving/failure/conflict review: **364 observations, 1,764 assertions, zero final failures or page errors**, all six roles at 1440/375. Includes Preferences/Reset/Preview, Time, primary register and meeting forms, Templates, Admin, Project Settings links, and four parent-provider loading/error/retry cases. 101 held-save observations and 223 explicit inapplicable states are distinguished; real role permissions were retained and live mutations blocked. Fixed draft/pending feedback, phone dialogs and Settings internal overflow. Final read-only visual review of repaired captures PASS. Evidence: `/private/tmp/tuesday-editors-review-merged/` and `/private/tmp/tuesday-final-repair-visual-review.md`.
- [x] Final Handoffs/live axe reruns; final web checks and task/AC evidence update. Final source/trace/whitespace results are recorded in verification.md.
- [x] [Jay review report](2026-10-04-tuesday-final-review.md) and running synthetic preview. All remaining work is uncommitted.
- [ ] **BLOCKED: T023 manual screen-reader pass at 1440/1024/375.** The native VoiceOver control request timed out before usable accessibility state. Keyboard/axe evidence does not replace this plan Validation step 10. T023 remains unticked; human supervisor pilot and Jay acceptance remain UNRUN separately.

## Preview and data safety

Only Docker/Colima **`pm-tuesday-preview-db`**, bound `127.0.0.1:55433`, database `hub`, is authorized for preview. Never mutate `pm-tool-db-1`. API tests may create temporary `hub_test_*` databases on 55432.

API command from target worktree:

```sh
dotnet run --project src/Hub.Api --no-launch-profile -- --environment Development --urls http://localhost:5080 --ConnectionStrings:Hub "Host=localhost;Port=55433;Database=hub;Username=hub"
```

Web command: `VITE_SYNTHETIC_PREVIEW=true npm --prefix web run dev -- --port 5173 --strictPort`. Preview URL: `http://localhost:5173/login`. Development picker users: Priya (PM), Sam (Supervisor/partial scope), Lena (Executive), Jordan (Admin), Alex (individual contributor), Rita (Read Only).

API on 5080 is running the final integrated planner build from `src/Hub.Api` with Development mode and the exact 55433 preview connection. Vite 5173 is running with the synthetic flag. Both additive migrations are applied. All five final web browser suites passed; Vite 5173 is running again. Final API remains on 5080. Native screen-reader validation is the remaining T023 blocker. Stop Vite only if repeating the handoff suite which needs 5173. `.claude/launch.json` sets the synthetic flag. Shell banner explicitly labels all shown preview records fictional.

`seed_modules.py` **read in full (701 lines)** and reviewed. Records use Synthetic preview prefixes and `.test` evidence. Module marker checks skip an existing module and may leave a partially seeded module incomplete; refusal aborts subsequent writes. `seed_resources.py` also read. Both now call `preview_target.py` before any writes: exact local API command/port and named Docker mapping must match. Current-target PASS; wrong-port refusal verified. Scale fixture has a separate `planning_only` branch and named-preview guard.

Seed dates anchor 2026-10-03. Pre-existing preview adjustments: extra 20 h proposal for Alex Oct 5–9, T0010 moved to Alex, T0009/T0010 descriptions edited. One decision/task link was omitted to avoid the separate Readiness defect; do not present that omitted edge as tested.

## Known limitations

`Readiness.cs:108` has a pre-existing EF translation failure for a task linked as blocked by a decision. Separate fix task exists. Do not fix unless a required check blocks, and report if it does. It has not yet blocked this continuation's checks.

Packet 029 retains its 8-week grid, role-based partial scope, and existing capacity validation. Do not retrofit planner concepts into it. Human pilot timing (SC-001, an actual supervisor's existing spreadsheet) and final Jay acceptance cannot be inferred from automation; record unperformed human evidence honestly.

Required browser executable:

```sh
CHROME_EXECUTABLE_PATH=/Users/jaypatel/Library/Caches/ms-playwright/chromium_headless_shell-1234/chrome-headless-shell-mac-arm64/chrome-headless-shell
```

`test:handoffs` needs 5173 free; `test:design-basis` uses `PORT=5186`. Final sweep command: `CHROME_EXECUTABLE_PATH=… node tools/preview/sweep.cjs 1440,1024,768,375`.

## Restart prompt

The technical blocker to resolve is the manual screen-reader pass in packet 034 plan Validation step 10. All other final evidence above is recorded; preserve it and rerun checks only if source changes or the evidence no longer describes the running revision. The full original goal below remains the acceptance contract. Paste this block into a new session after this session's agents have stopped:

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
