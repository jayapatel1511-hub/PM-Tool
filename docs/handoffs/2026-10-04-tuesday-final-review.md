# Tuesday overhaul and packet 034 — review report

4 October 2026. Worktree: `/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1`; branch `claude/tuesday-visual-batch1`; baseline `9d3e9b91`.

**Current status: implementation and software/browser checks PASS; release `d81c13f9084cfd4e828ab9762433d38c3849b302` is committed, pushed, deployed to the synthetic review stack and passed post-deployment safety gates. The full goal remains BLOCKED at T023's manual screen-reader validation and human acceptance.** Another session previously committed the spec and visual batch as `586fc48b` and `9d3e9b91`; Jay later authorized the reviewed synthetic release. No history was rewritten.

## Current review pointer — 4 October 2026

The current uncommitted candidate supersedes the prepared candidate `29e9b15c388415ed4ace830f5c10c37c6cec93f3` for final local review. The deployed review pointer remains `d81c13f9084cfd4e828ab9762433d38c3849b302`; this worktree must not be described as deployed or complete. The current Tuesday board capture is `93a350...` (full hash recorded in web/public/landing/capture.json). Ten fictional Tuesday personas and the full Tuesday examples are approved for hosted review.

Jay explicitly requested natural visible copy: rendered screens no longer show synthetic/sample disclaimers or badges. Fixture provenance remains private in seeds, handoffs, logs and verification records. Root verified 32 focused Python tests, spec build/trace at 660/252, Resources PASS, and Planner 14 base plus six recovery flows, 179 calls and seven axe scans; two final Design Basis resize-focus runs PASS after repairing mounted-control focus tracking. Open decisions remain the brand mark, recapturing the hero board image, confirmations for deleting holidays and removing roles, and per-notification Mark read. T023 manual validation, SC-001 and Jay's final acceptance remain open; no CI or SHA is claimed here beyond the evidence below.

## Landing follow-up — committed, CI PASS, prepared; activation pending

Jay requested a current board image, seamless background blending, then explicitly preferred the older colours over the white/mint trial. The current local hero keeps the approved pale paper, forest ink and amber/sage palette. Broad washes and a soft decorative-image mask blend the app capture into the page; the floating frame, heavy shadow and old-image fallback are removed. The unchanged 1728 × 873 JPEG comes from Priya's existing synthetic `/boards` preview, visibly labelled fictional; no records were edited. Its 102957 bytes and SHA-256 match `web/public/landing/capture.json`.

Independent source/provenance review found no actionable issue. Root inspected the final desktop and phone screenshots. Final build PASS; exact type-check and lint PASS (87 existing warnings, no new); eight cold public landing/login rechecks at 1440/1024/768/375 PASS with zero findings, replacing those cases in the retained 1956-combination evidence. This is an affected-route recheck, not another fresh full sweep. Evidence: `/private/tmp/tuesday-hero-paper-{build,sweep}.log`, `/private/tmp/tuesday-sweep-hero-paper/`. ReUI's MCP audit guidance and the business UI design skill informed the review; no new component package, image generation, authentication or app behaviour changes.

The follow-up is committed and pushed only to the existing feature branch as `29e9b15c388415ed4ace830f5c10c37c6cec93f3`. [Exact-head CI 37248697226](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37248697226) PASS: 1036 tests, zero failed/skipped, coverage and required browser gates; planner 14 base plus six recovery flows, 180 API calls, seven zero-finding axe scans. The exact tree is prepared on homedev and its JPEG hash independently matches. The active review pointer is still `d81c13f9084cfd4e828ab9762433d38c3849b302`; activation is pending Jay's own-terminal sudo. Root provided one command combining activation and the reviewed `incoming/postdeploy-29e9b15c.sh` helper, with an owner-only `data/activation-29e9b15c.log`. After Jay runs it, independently verify the log, exact image/pointer/volume, migrations, restore/persistence gates, public probes and hosted hero. The separate pilot remains unchanged. The current local result awaits Jay's visual acceptance. T023 manual screen-reader and SC-001 remain unrun. Brand mark, holiday/manual-role removal confirmations and per-notification Mark read remain open decisions.

## Current deployment evidence — verified 5 October 2026 UTC / 4 October Halifax

The review pointer is `d81c13f9084cfd4e828ab9762433d38c3849b302`; the separate pilot pointer remains `29b3d7036f529b513fdc2ad47cff62a0b75f0b59`. Draft PR #37 remains unmerged. Exact-head CI 37245644519 passed with 1,036 passed and zero failures/skips, rules branch coverage 96.8%, service line coverage 94.5%, and planner 14 base plus six recovery flows, 181 calls and seven zero-finding axe scans. Local Resources also passed because CI does not configure that suite.

The activation and `postdeploy-d81c13f9.sh` were run by Jay in his own Terminal. Independent log review confirmed both planning migrations, exact review image/volume checks, a 1255924-byte mode-0600 fresh backup, isolated restore of four projects with cleanup, and restart persistence counts `projects|people|planning = 4|3|0`. Private/public health and auth probes passed: health 200, anonymous 401, invalid private Host 400, unsafe Origin 403, wrong password 401, sign-in 204, authenticated `/me` and project list 200, sign-out 204, then `/me` 401. Existing copied-cookie replay returned 200 and remains a disclosed stateless eight-hour expiry behavior. A transient curl 52 during restart recovered before the final PASS.

The versioned hosted login loaded the new JS/CSS and versioned authenticated My Work showed the synthetic banner after initial old-HTML caching. Hosted browser review remains bounded. The landing follow-up above is locally verified, not deployed; T023, SC-001 and Jay's acceptance remain open.

## Historical deployment preparation — 4 October, evening (completed above)

Jay returned home and explicitly answered **“Yes, commit and deploy synthetic review.”** This supersedes the earlier uncommitted/no-deployment hold for the reviewed candidate. Current preparation is for the existing review stack only, preserving unrelated edits and all pilot/operational data. Host preflight verified the current review and pilot pointers at `29b3d7036f529b513fdc2ad47cff62a0b75f0b59`, review origin health HTTP 200 with the configured proxy headers, and the existing tunnel active. That release is an ancestor of the candidate. The candidate's 114 selected files and 315 frozen application/test/build sources had zero drift since final checks. GitHub authentication is valid when checked outside the network sandbox.

Prepare a reviewed commit, incorporate the approved Paper hero commit from origin/main, and pass the required draft-PR CI before transferring the exact committed tree. The approved candidate implementation is committed as `c829f0e6`. Hero merge resolution retains the approved main hero/component/assets and the reviewed sign-in/12 px fixes; two unused incoming Task translation keys are preserved, giving 3,563 unique English keys. No UI flow or authenticated API behavior changes in this integration. Interactive homedev sudo remains a user step. Deployment is not yet claimed complete. T023 stays BLOCKED/UNRUN and human acceptance/pilot remains pending; Jay authorized this synthetic review deployment with that limitation disclosed. Historical local-only status statements below describe the earlier snapshot.

## Historical local release review loop (superseded above)

The additional review/fix loop passes on the final candidate: exact type-check, lint with zero new warnings, review build, all five browser suites and local Release publish. Planner now has 14 base flows plus six recovery regressions, 180 API calls and seven axe scans with zero findings. All 96 affected six-role/four-width routes cold-rechecked successfully; the retained full sweep plus rechecks has 1,956 unique zero-finding combinations. The earlier UX final3 evidence below is retained and superseded for these repaired planner paths. CI includes planner checks, and only fictional review enables the synthetic banner in the shared review/pilot build.

**Historical checkpoint before authorization: deployment requested but not performed.** The existing path needs a committed reviewed SHA and interactive homedev sudo; those conflict with the earlier uncommitted/no-authentication instructions. No host connection or authentication was attempted. T023 remains BLOCKED/UNRUN. See [the release-loop report](../reviews/2026-10-04-tuesday-release-review-loop.md) for final commands, fixes, candidate scope and exact blockers.

## Review preview

[Open the local preview](http://localhost:5173/login). API: `http://localhost:5080`. Use the Development sign-in picker; there are no preview passwords.

| Picker | User | Role |
|---|---|---|
| priya | Priya Nair | Project Manager |
| sam | Sam Patel | Supervisor, direct-report/partial scope |
| lena | Lena Brooks | Executive |
| jordan | Jordan Lee | Admin |
| alex | Alex Chen | Individual contributor |
| rita | Rita Gomez | Read Only |

All shown records are fictional; the source and private preview provenance remain documented in the seed and verification files while the visible screens use natural product copy. Preview writes and scale fixtures use only `pm-tuesday-preview-db`, bound to `127.0.0.1:55433`. API tests use temporary `hub_test_*` databases on 55432. The preview guard verifies the named database container and exact API process before seed writes. It refuses a different port. No operational records in `pm-tool-db-1` were modified.

## What changed

The Tuesday design now carries through the application frame, project screens, Task workflow and Registers, resource views, personal pages and administrative surfaces. Shared search retains fast typing and focus while respecting URL/back/clear/saved-view state. Phone layouts use readable cards or the contract's desktop/tablet notice; internal board and planner scrolling remains contained. Keyboard focus is visible, disabled actions are readable, location kinds are named, and chips use the shared vocabulary and colours.

Task and register editors show the actual pending/save result, retain attempted values on failures and conflicts, and offer retry or explicit reload. Task progress release plus blur saves once. Preferences reset clears obsolete failed-save feedback. Project Settings links retain pending drafts and the phone form fits its main content width. Large dialogs scroll inside the phone viewport. Deliverable review, Templates and Admin settings have guarded pending actions and retained error drafts. Public landing/sign-in text is at least 12 px; phone sign-in has a contained single-column layout. The approved Paper hero composition is preserved.

Jay's additional UI/UX review repaired overlapping planner Close controls, misleading My Week count/heading, ambiguous stale filters, denied/error reads that looked empty, phone filter placement, long-dialog title/Close visibility and Time conflict recovery. Menus now wrap into extra rows as requested. Project phone tabs stay in normal flow so the tall menu cannot cover the form. Breadcrumbs wrap at tablet widths. Time Reload explicitly replaces date/hours/note with latest visible values while retaining correction reason; conflicts keep Save blocked until a successful reload, and loss of edit access removes Save. Routes, APIs, calculations and permissions outside 034 are unchanged.

The additional final-build suites and fresh complete sweep pass. All 1,956 unique route/role/width checks have zero findings, now including menu overflow. Web source hashes remained unchanged during the sweep; previous interrupted/failed runs are preserved. See the [UI/UX review](../reviews/2026-10-04-tuesday-ui-ux-review.md). No homedev connection or authentication action was performed.

Translations are consolidated into `web/src/i18n/en.ts`: 3,561 current keys, all unique under a TypeScript AST check. The area files and runtime glob are removed. The eight unused frame variables are gone.

Packet 034 adds a separate Weekly Planner and My Week. It includes pure domain rules, permission functions, additive EF migrations, versioned/idempotent commands, calendar/time-away parity, contribution explanations, qualified confidence/visibility/approval labels, search, saved views, exports, notifications and digest integration. Draft privacy is enforced before aggregation and on all read surfaces; queued notifications/email are sanitized or refused after withdrawal/access revocation. Admin correction requires a reason and is logged. Existing allocation commands, Workload calculations and Readiness behavior are guarded by unchanged tests and actual before/after response snapshots. Workload source changes only expose four internal helpers for the new layer.

The repository `/speckit-analyze` workflow was applied and its gaps remediated. All 28 functional requirements map to tasks and all 20 acceptance criteria have evidence/status rows in [packet verification](../../specs/034-weekly-planning-layer/verification.md). Canonical spec-parts and section 10 govern the implementation. No AI features or additional Phase 2 scope were introduced.

## Check evidence

| Check | Result |
|---|---|
| Full Hub.Tests | PASS: 1,036 passed, zero failed/skipped; fresh-database migration down/up included. |
| Coverage gate | PASS: Domain branches 96.6 %, Features lines 94.2 %, PlanningRules branches 100 %. |
| EF model/migrations | PASS: no pending model changes; both additive planning migrations verified applied on the preview. |
| Scale | PASS: 30 samples, 12 people × 12 weeks, 500 entries, 50 confirmed allocations; grid p95 26.32 ms and My Week p95 14.90 ms. Local macOS arm64 synthetic measurements. |
| Exact app type-check, lint, build | PASS UX final3: zero type errors; 87 existing warnings and zero new file/rule/message diagnostics. Existing Vite configuration/chunk-size notices remain. |
| Coordination, Design Basis (PORT=5186), Resources | PASS on UX final3 built assets; required Chromium executable used. |
| Handoffs | PASS UX final: create → submit → accept → incorporate, four requests, zero errors/unknown requests or form/receipt axe violations. |
| Planner acceptance | PASS UX final3: 14 flows, 150 API calls, three created entries, two notifications, five axe scans, zero errors/unknown requests. Strict four-arrow/Tab and natural-focus assertions pass after repairing Escape focus loss. |
| Full live sweep | PASS UX final2: 1,956 unique combinations at 1440/1024/768/375; zero page errors, axe violations, page/main/menu sideways overflow, sub-12 px text, raw keys, old palette flags or evaluation errors. `/private/tmp/tuesday-sweep-ux-final2/{results,summary}.json`. |
| Planner/My Work live axe | PASS in the fresh full sweep: all 96 base-route/role/width combinations, plus five stateful Planner scans. |
| Spec build, trace, whitespace | PASS after final documentation: 660 IDs, 252 sections, zero uncited/unknown. |
| Manual screen-reader | BLOCKED/UNRUN: native VoiceOver control timed out before a usable accessibility state was returned. No screen-reader result is claimed. |

Detailed commands, AC rows, log locations and defaults are in [verification.md](../../specs/034-weekly-planning-layer/verification.md). Logs/screenshots are local temporary artifacts; the maintained verification record captures their outcomes.

## Browser and state review

All six real roles were reviewed at desktop and phone widths. The normal visual review covered 108 contact sheets: 81 routes per role/width and Jordan's additional draft template. The broad state protocol covered 974 contexts with no page errors. All 101 earlier focus defects were cold-rechecked successfully. Task workflow states passed in all 12 role/width contexts.

The editor protocol passed 1,764 assertions across 364 role/device/flow observations, including 101 held saves. It covers Preferences/Reset/Preview, Time, primary register and meeting forms, Project Settings links, Templates and Admin. A further visual recheck of repaired desktop/phone captures found no actionable defect. Hover, focus, selected, disabled, loading, empty, saving, failed save, conflict and read-only were inspected where applicable; long labels, missing values, zero hours, negative remaining capacity and restricted/private records have explicit browser/API evidence.

Permissions came from each real role's read responses. Held saves and 500/409/error/long-label states were browser-only synthetic fixtures and no intercepted mutation reached the live API. Server persistence, privacy, concurrency and arithmetic have separate PostgreSQL/API evidence. The 223 explicit inapplicable editor observations are not counted as successful saves: Templates/Admin retain desktop/tablet policy; phone Milestone editing is unavailable; selected Sam Time records are read-only and Lena/Rita had no own Time records in the inspected range. Primary records and representative fields were exercised, not every possible record or every conditional workflow.

The additional dialog protocol passed 108 final observations across all six users, desktop/phone and nine create flows: 40 applicable cases and 68 explicit NAs. All applicable dialogs retained their title/Close after body scrolling, retained failed/conflict drafts and had no sideways body overflow. A separate 12-case live menu check and screenshot review covered all six users at 1440/375: selected Settings was visible, menus wrapped into rows, and page/main/menu overflow and page errors were zero. Time recovery was independently reproduced with mocked responses at both widths, including failed reload retention, correction reason retention, latest-version PATCH and permission loss. No intercepted mutation reached the live API.

The original 12 earlier findings and failed/harness observations are preserved with their successful cold repeats. The expanded menu audit also exposed 36 tablet breadcrumb overflows; the interrupted 450-row run is retained separately, followed by the repaired-source fresh 1,956-row passing sweep. The final fresh sweep added main-content overflow measurement so clipped inner forms cannot pass merely because the outer document fits. No homedev connection or authentication action was performed. No security limits were weakened for the preview or audits; live sweep requests were paced under the existing API limit.

## Remaining limitations and decisions

**T023 remains open.** Its plan Validation step 10 calls for a manual screen-reader pass at 1440/1024/375. VoiceOver control timed out, so that specific technical check cannot be asserted. Keyboard, focus and axe checks are separate evidence. [Plan requirement](../../specs/034-weekly-planning-layer/plan.md#validation). Jay's final visual approval and SC-001's actual-supervisor spreadsheet exercise are also pending human evidence; synthetic timing is not a pilot substitute.

The separate `Readiness.cs:108` decision-blocker translation defect is unchanged and did not block the required checks. One synthetic decision/task seed link was omitted to avoid that defect and is not claimed tested. `seed_modules.py` was read in full (701 lines), as were the resource seed and scale paths; module markers can skip a partially seeded existing module, so the seeds are guarded fixture helpers rather than a repair of arbitrary existing data.

Packet 029 keeps its eight-week grid, partial-scope rules, approval statuses and capacity validation. The external design README's Self-entered/pointer note is deferred because it is maintained in a separate worktree; the local interface and guide use the canonical labels and fields.

Jay's four product decisions remain explicitly deferred:

1. The unified brand mark.
2. Hero board recapture: completed locally at Jay's request; publication and visual acceptance pending as described above.
3. Confirmation behavior for deleting holidays and removing manual roles.
4. Per-notification Mark read.

These were not silently decided during the visual work. Existing product behavior remains in place pending Jay's decisions.

## Packet 034 defaults relied on

All 18 §39.12 defaults stand under the implementation instruction:

1. Keep the integer weekly capacity setting at 40 h; 37.5 h needs a separate decimal-setting proposal.
2. Use Admin settings: over 105 %, under 50 % for two weeks, stale after 28 days, maximum 80 h per entry/week, horizon 12 weeks (range 6–26).
3. Use Over-planned, Under-planned and Stale plan, distinct from Workload indicators.
4. Supervisor/direct-report or Admin records time away; self-recorded time away is deferred.
5. The same editors may clear availability overrides, with audit history.
6. A person sees approved allocations in My Week as read-only hours on visible projects, without opening allocation confirmation detail.
7. Supervisor read scope is direct reports; all-staff Supervisor read is deferred.
8. Project Managers/Discipline Leads do not create planning entries; eligible Project Managers view members of projects they manage.
9. A person cannot change or comment on a manager entry; they may add their own entry. Planning comments are deferred.
10. One owner action confirms an assignment; the person's acceptance is not required.
11. Other planners see nothing about a Private draft's existence.
12. Planning uses ISO-week ranges; resource allocations retain date grain.
13. Coverage order: confidence Confirmed, Expected, Possible; then start week, creation time and identifier.
14. Retain Workload's partial-view rule except Admin/Executive; finer restricted-existence handling is a separate privacy follow-up.
15. Project status changes do not rewrite entries; inactive projects are flagged.
16. Search includes visible entry labels and notes.
17. In-app planning notifications on, email off; stale plans appear in digest only.
18. Pointer drag is deferred; side-panel fields extend, shorten and move entries.

Restart/process commands and the original follow-up checklist remain in the [current handoff](2026-10-03-tuesday-overhaul-handoff.md).
