# Tuesday visual overhaul — current handoff (4 October 2026)

**Locally implemented and software/browser checks PASS. The reviewed synthetic release is committed, pushed to the feature branch, deployed to the existing synthetic review stack, and passed the post-deployment safety gates. Overall goal remains BLOCKED at T023's manual screen-reader validation and human acceptance; the landing follow-up below is committed, CI-passing and prepared, awaiting activation.** Worktree `/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1`, branch `claude/tuesday-visual-batch1`, historical baseline `9d3e9b91`; deployed release `d81c13f9084cfd4e828ab9762433d38c3849b302`. Another session committed packet 034's specification (`586fc48b`) and the existing visual batch (`9d3e9b91`) during this run. Jay later explicitly authorized: “Yes, commit and deploy synthetic review.” Preserve the three unrelated dirty files and all pilot/operational data.

## Latest scope — full hosted examples, normal visible copy; activation held

Jay explicitly selected **Full Tuesday examples** for the hosted review: the ten Tuesday personas, SYN-101–103 and their tasks/registers/resources/planning. He then requested the whole app look like the real product, with no visible synthetic/sample references because all testers already know the data is fictional. This supersedes the original visible-label requirement in item 7; provenance remains in maintained fixture sources/capture metadata and private host setup records. Authentication, privacy, calculations and existing reviewer changes remain protected.

The visible shell banner and landing notices are removed, individual sign-in copy is natural, fixture recipes use normal names/descriptions, and the opt-in review seed adds the ten existing Tuesday personas without enabling DevUsers. Only exact legacy review defaults are cleaned; user edits are retained. The hero now uses an unedited 1728 × 873 browser capture of the current board without the banner, 97821 bytes, SHA-256 `93a350c50f3b0d53c19ca02f17d28231595b9a02347733dfbed709b5fce5471b`. The three local SYN project display names were changed by guarded/versioned API patches; identities and business data were retained.

Guarded hosted setup has passed independent source review and focused safety tests. It uses individual LocalPassword sign-ins and the exact review image/database only, preserves all three existing credentials, and writes new account credentials/provenance to private files. No fixture transport uses hosted X-Dev-User. A legacy-link idempotence mismatch created four duplicate local links in the first rerun; root soft-deleted only those four and fixed the marker. The second module rerun created nothing. There were no refused steps.

Targeted verification: six ReviewDemoSeed tests PASS; ten API transport guard tests, eight credential helper tests and seven operator tests PASS. Type-check/build and lint PASS (87 existing warnings, no new). Handoffs, Coordination, Resources and Planner PASS; planner covers 14 base plus six recovery flows, 179 API calls and seven zero-finding axe scans. The resize regression exposed a real Task focus loss after React removed the old layout; mounted focus tracking repairs it, and two independent final Design Basis runs PASS. An interrupted 596-observation sweep had zero findings; it is retained as partial evidence, and final route verification follows the copy cleanup. Spec build/trace checks PASS: 660 IDs, 252 sections, zero missing/unknown. Logs use `/private/tmp/tuesday-natural-*`.

The guarded normal-copy cleanup applied 116 exact legacy fixture fields through existing permissioned/versioned API calls; its dry rerun found zero remaining eligible updates. Long labels, IDs, hours, dates and roles were retained. Older comments remain subject to author/edit-window rules; source revisions, notifications and audit history remain protected. Fresh hosted recipes already use ordinary copy for those records. All 33 helper safety tests PASS, including six further action text updates (122 copy fields in total) with zero remaining eligible updates. The final full sweep is running against frozen web source and the cleaned preview; partial earlier evidence (596 observations, zero findings) is retained.

**Do not run the earlier 29e9b15c activation command.** That prepared landing-only release is superseded by the full-example candidate on the existing feature branch. Current hosted review remains d81c13f9 and the separate pilot is unchanged. The first full-example candidate is committed/pushed as `851be8ceda2883c32d1e9223619d27eca633c984`; a helper/documentation follow-up records the final action cleanup. Prepare the exact final head only after its CI and route checks; Jay performs sudo activation in his own Terminal. T023 manual screen-reader, SC-001 and final human visual acceptance remain unrun.

The hero image URL now includes its capture hash prefix (`?v=93a350c5`) so returning browsers request the new media after activation. This final one-line UI change gets eight public-route cold rechecks; non-public source remains identical during the full sweep. Activation is still held for exact final-head CI and completed route evidence.

## Copy limits retained by the existing contracts

The local performance dataset contains twelve benchmark identities, 500 planning entries and 50 allocations; it is excluded from the hosted ten-person dataset. Admin profile PATCH does not accept displayName/jobTitle, and benchmark idempotence uses exact planning/allocation markers. No API, authentication or direct SQL workaround was added. Legacy published reviews/design-basis/source-derived change titles have no general copy-only edit route; handoffs/submissions are state-governed. Older comments follow author/edit-window rules, and audit/notifications/source revisions preserve history. These local/historical references are deferred for those precise reasons; fresh hosted records use normal wording. A literal erasure of every historical occurrence is not claimed.

## Fresh completion audit

The prepared landing release has not activated: the live pointer is still `d81c13f9`, no new activation log exists, and review health is Healthy. Native VoiceOver access was retried with a bounded 20-second request and again timed out before usable state. T023 remains BLOCKED/UNRUN. The packet plan/tasks/index now reflect the committed/deployed implementation instead of the obsolete uncommitted status. The manual width/role checklist and required evidence are in `specs/034-weekly-planning-layer/verification.md`, under Current completion audit and manual pass handoff. Await actual screen-reader observations and the new reviewed release activation; do not tick T023, bypass sudo or claim the full goal complete.

## Landing follow-up — committed, CI PASS, prepared; activation pending

Jay requested a current board image, seamless background blending, then explicitly preferred the older colours over the white/mint trial. The current local hero keeps the approved pale paper, forest ink and amber/sage palette. Broad washes and a soft decorative-image mask blend the app capture into the page; the floating frame, heavy shadow and old-image fallback are removed. The unchanged 1728 × 873 JPEG comes from Priya's existing synthetic `/boards` preview, visibly labelled fictional; no records were edited. Its 102957 bytes and SHA-256 match `web/public/landing/capture.json`.

Independent source/provenance review found no actionable issue. Root inspected the final desktop and phone screenshots. Final build PASS; exact type-check and lint PASS (87 existing warnings, no new); eight cold public landing/login rechecks at 1440/1024/768/375 PASS with zero findings, replacing those cases in the retained 1956-combination evidence. This is an affected-route recheck, not another fresh full sweep. Evidence: `/private/tmp/tuesday-hero-paper-{build,sweep}.log`, `/private/tmp/tuesday-sweep-hero-paper/`. ReUI's MCP audit guidance and the business UI design skill informed the review; no new component package, image generation, authentication or app behaviour changes.

The follow-up is committed and pushed only to the existing feature branch as `29e9b15c388415ed4ace830f5c10c37c6cec93f3`. [Exact-head CI 37248697226](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37248697226) PASS: 1036 tests, zero failed/skipped, coverage and required browser gates; planner 14 base plus six recovery flows, 180 API calls, seven zero-finding axe scans. The exact tree is prepared on homedev and its JPEG hash independently matches. The active review pointer is still `d81c13f9084cfd4e828ab9762433d38c3849b302`; activation is pending Jay's own-terminal sudo. Root provided one command combining activation and the reviewed `incoming/postdeploy-29e9b15c.sh` helper, with an owner-only `data/activation-29e9b15c.log`. After Jay runs it, independently verify the log, exact image/pointer/volume, migrations, restore/persistence gates, public probes and hosted hero. The separate pilot remains unchanged. The current local result awaits Jay's visual acceptance. T023 manual screen-reader and SC-001 remain unrun. Brand mark, holiday/manual-role removal confirmations and per-notification Mark read remain open decisions.

## Current release status — verified 5 October 2026 UTC / 4 October Halifax

The exact release `d81c13f9084cfd4e828ab9762433d38c3849b302` is active at `https://pm.engcalchub.com` on the review pointer; the separate pilot pointer remains `29b3d7036f529b513fdc2ad47cff62a0b75f0b59`. Draft PR #37 is pushed and remains unmerged. Exact-head GitHub CI run 37245644519 passed: 1,036 passed, zero failed/skipped; rules branch coverage 96.8%, service line coverage 94.5%; planner 14 base flows plus six recovery regressions, 181 calls, seven axe scans, zero errors/unknowns/violations. Local Resources remains PASS because CI does not configure that suite.

Jay ran the activation and the post-deployment safety helper in his own Terminal. Root independently read the sanitized post-deployment log: both planning migrations applied; review image `pm-tool-review:d81c13f9084cfd4e828ab9762433d38c3849b302` and volume `pm-tool-review-db` were verified; the fresh backup `hub-review-20261005T002150459496724Z.dump` is 1255924 bytes with mode 0600; the isolated restore drill restored four projects and cleaned up its temporary database; restart persistence was `projects|people|planning = 4|3|0`. Health and private/public authentication probes passed, including anonymous 401, wrong-password 401, unsafe Origin 403, invalid private Host 400, sign-in 204, authenticated `/me` and projects 200, sign-out 204, and post-sign-out `/me` 401. The copied-cookie replay returned 200 as an existing stateless eight-hour expiry behavior and remains disclosed. A transient curl 52 occurred during restart and recovered before all gates passed.

The hosted browser initially served cached old HTML. The versioned `/login?review=d81c13f9` loaded the new JS/CSS; the authenticated `/my-work?review=d81c13f9` showed the synthetic banner. Hosted role/width review is bounded. The locally verified landing follow-up above is not deployed. T023, SC-001 and final acceptance remain UNRUN; off-host backup revalidation is pending.

## Historical deployment preparation — 4 October, evening (completed above)

Jay returned home and explicitly answered **“Yes, commit and deploy synthetic review.”** This supersedes the earlier uncommitted/no-deployment hold for the reviewed candidate. Current preparation is for the existing review stack only, preserving unrelated edits and all pilot/operational data. Host preflight verified the current review and pilot pointers at `29b3d7036f529b513fdc2ad47cff62a0b75f0b59`, review origin health HTTP 200 with the configured proxy headers, and the existing tunnel active. That release is an ancestor of the candidate. The candidate's 114 selected files and 315 frozen application/test/build sources had zero drift since final checks. GitHub authentication is valid when checked outside the network sandbox.

Prepare a reviewed commit, incorporate the approved Paper hero commit from origin/main, and pass the required draft-PR CI before transferring the exact committed tree. The approved candidate implementation is committed as `c829f0e6`. Hero merge resolution retains the approved main hero/component/assets and the reviewed sign-in/12 px fixes; two unused incoming Task translation keys are preserved, giving 3,563 unique English keys. No UI flow or authenticated API behavior changes in this integration. Interactive homedev sudo remains a user step. Deployment is not yet claimed complete. T023 stays BLOCKED/UNRUN and human acceptance/pilot remains pending; Jay authorized this synthetic review deployment with that limitation disclosed. Historical local-only status statements below describe the earlier snapshot.

## Historical local review/fix checkpoint — 4 October (superseded above)

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
