# Tuesday overhaul and packet 034 — review report

## Final technical deployment verification PASS — 5 October 2026

Release `bbac1f6daf595a9411908f9795201c20f812079c` is active at [Tuesday](https://pm.engcalchub.com/?review=bbac1f6d). Exact CI, full Tuesday fixture import, private and public authentication checks, all ten public identity/project/own-planning/sign-out checks, fresh backup restore and restart persistence PASS. Root directly inspected the hosted refreshed landing at desktop/375px, signed in as Priya, inspected My Work at 1440px, Tasks at 1440/375px and Weekly Planner at 1440/375px. These bounded hosted checks show no page/menu sideways overflow, no visible text below 12px, no fixture notices and no captured browser console errors. Project navigation wraps to three rows at 1440px and eight at 375px. Negative remaining capacity, time away and distinct source/confidence/visibility controls render in the desktop planner. Phone tasks use cards. The phone planner displays the desktop/tablet notice required by FR-PLN-26/AC-PLN-18; full phone planning is outside the canonical scope. Full hosted six-role/state acceptance is not claimed; the complete unchanged-source local 1,956-case sweep remains the broader evidence.

Hosted IDs: `priya`, `sam`, `lena`, `jordan`, `alex`, `rita`, `marc`, `jill`, `diane`, `omar`. Existing legacy sign-ins/credentials are preserved. Passwords remain only in the owner-only host `.runtime/tuesday-review-login-handoff.json`; the one-person temporary browser credential copy was removed, and no password/cookie/verifier appeared in logs or Git. Local preview remains [localhost:5173](http://localhost:5173/), with its Development person selector and no preview passwords. Separate pilot remains unchanged. Draft PR #37 is pushed and unmerged; post-commit evidence docs stay local and the three unrelated dirty files are preserved.

Current hosted screenshots: `/Users/jaypatel/.codex/visualizations/2026/10/04/01a104af-b670-70b0-872e-f9915f405535/tuesday-hosted-bbac1f6d.jpg`, `tuesday-hosted-bbac-phone.png`, `tuesday-hosted-mywork-bbac-desktop.png`, `tuesday-hosted-tasks-bbac-{desktop,phone,phone-content}.png` and `tuesday-hosted-planner-bbac-{desktop,phone}.png`. The temporary browser viewport was reset. Final clean public evidence is `/private/tmp/tuesday-bbac-public-users-final.log` (root serialized run, exit 0) and `/private/tmp/tuesday-bbac-public-auth.log` (exit 0); the earlier overlapped 429 probe remains failed historical evidence. No rate-limit/security workaround was used.

Overall human acceptance remains incomplete: T023 manual screen-reader, SC-001 supervisor exercise and Jay's final visual review are UNRUN. Readiness.cs:108 remains separately tracked with its decision/task seed edge omitted. Protected history/benchmark copy and copied-cookie replay/off-host backup retrieval limitations remain documented. Open decisions: brand mark, approval of the completed hero recapture/blend, holiday and manual-role removal confirmations, and per-notification Mark read. Do not mark the full original goal complete from deployment alone.

## Public verification PASS — 5 October 2026

Independent public gates PASS, including health, anonymous 401, unsafe Origin 403, wrong password 401 and legacy sign-in/sign-out. Root's serialized ten-person probe exited 0: exactly ten unique expected identities, each sign-in 204, same-person /me, project list 200, own-only My Week 200 and sign-out 204. The earlier overlapped verification log hit 429 and is retained as failed attempt evidence; no rate limit was weakened. Final clean evidence: `/private/tmp/tuesday-bbac-public-users-final.log` and `/private/tmp/tuesday-bbac-public-auth.log`; independent helper audit confirms exact SHA/statuses and no NULs. The bounded hosted signed-in browser review subsequently PASS as recorded above.

4 October 2026. Worktree: `/Users/jaypatel/PM-Tool/.claude/worktrees/tuesday-visual-batch1`; branch `claude/tuesday-visual-batch1`; baseline `9d3e9b91`.

**Current release `bbac1f6daf595a9411908f9795201c20f812079c` is committed/pushed and deployed at `https://pm.engcalchub.com`. Exact CI, full fixture import, private authentication/safety gates, fresh backup restore and restart persistence PASS. Independent public ten-person and bounded hosted signed-in browser checks PASS. T023, SC-001 and Jay's visual acceptance remain UNRUN. The separate pilot is unchanged.**

## Current deployment evidence — 5 October 2026

Root independently read the final post-deployment safety PASS. The complete three-project/ten-person dataset includes 14 tasks, six allocations, seven planning entries and the full registers/coordination fixtures. The exact review image and volume match, both planning migrations are applied, and a fresh private 1,385,277-byte backup restored seven projects in an isolated drill. Restart persistence was projects|people|planning = 7|13|7. Transient curl 56 connection resets during restart recovered before health 200 and all gates passed. Fixture manifest and sign-in handoff have mode 0600 in private mode-0700 directories. Detailed counts are in the main handoff.

The current hero has been recaptured and deployed with the approved paper/forest/amber/sage blend; it loads at 1728px natural width from its versioned URL. Root directly reviewed the hosted desktop and 375px landing: zero page/menu sideways overflow, no visible text under 12px, no fixture notice. Final visual approval remains Jay's decision; no further hero asset or deployment is implied. Full hosted six-role/state acceptance is not claimed.

Historical activation/preparation narrative: Jay activated `98ed319af5e8b48a386dbb2452710922e1f79c37` on the review volume in his own Terminal; private authentication passed and all ten review credentials were created and verified. The hosted fixture then failed before any project or resource writes when `seed_resources.py` reused its API transport variable for a client ID. The corrected fix is `bbac1f6daf595a9411908f9795201c20f812079c`, with the focused regression and CI wiring added. Exact-head CI run `37296543742` passed: 1,040 backend tests, zero failed/skipped, 96.8% rules branch and 94.5% service line coverage, 34 hosted helper tests plus 19 backup/pilot tests, and all five browser suites. The candidate was prepared with 742 regular-file hashes and both 600-mode helper hashes independently matching. The same CI run recorded spec trace 660 IDs, 252 sections and zero unknown, plus all five browser suites; Planner covered 14 base and six recovery flows with 177 API calls and seven zero-axe scans. The old “not run” wording in this historical paragraph predates current activation; current progress is recorded above. The app-only 1,956-case sweep and eight public cache rechecks remain valid because the web source hashes were unchanged; module client handling was not changed because it is local and correct.

## Historical 98ed release pointer — private auth reached; fixture aborted

The historical candidate was `98ed319af5e8b48a386dbb2452710922e1f79c37`, committed and pushed to the feature branch. It reached private auth and credential verification, then its fixture activation aborted at `seed_resources.py` before project/resource writes; it was never a completed hosted deployment. Exact-head CI run [37254797823](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37254797823) passed: 1,040 backend tests with zero failures/skips; rules branch coverage 96.8%; service line coverage 94.5%; 33 hosted fixture/credential safety tests; all five browser suites passed; and spec trace reported 660 IDs, 252 sections, zero uncited/unknown. CI recorded 87 existing lint warnings. The complete log is `/private/tmp/tuesday-ci-98ed319a.log`. CI planner evidence is 178 API calls; the local planner evidence remains 179 calls. The frozen full sweep `/private/tmp/tuesday-sweep-natural-complete/results.json` passed all 1,956 unique combinations across six roles and four widths with zero findings and exit 0. The eight public cache rechecks in `/private/tmp/tuesday-sweep-natural-public-final` also passed with exit 0. Final source verification recorded 148 unchanged hashes. The old 98ed activation is historical and its seed command is obsolete; do not rerun it.

The candidate provides the recipe for all ten approved normal-copy Tuesday personas and the full Tuesday examples; they have not yet been seeded on the currently observed host. The rendered UI uses natural product copy by Jay's explicit override; private seeds, manifests, handoffs and logs retain synthetic provenance. The local benchmark remains a separate limitation: its 12 benchmark people, 500 planning entries and 50 allocations use fixture labels/markers, and the current guarded APIs do not provide a safe copy-only normalization path without breaking seed idempotence or rewriting historical allocation reasons. This candidate does not claim that benchmark normalization.

T023 manual screen-reader validation, SC-001's actual-supervisor spreadsheet exercise and Jay's final visual acceptance remain open. The four product decisions remain open: brand mark, final approval of the current `93a350...` hero board image, confirmations for deleting holidays and removing roles, and per-notification Mark read.

## Historical current review pointer — 4 October 2026

The historical 98ed candidate superseded the prepared candidate `29e9b15c388415ed4ace830f5c10c37c6cec93f3` for final local review. The deployed review pointer remains `d81c13f9084cfd4e828ab9762433d38c3849b302`; this worktree must not be described as deployed or complete. The current Tuesday board capture is `93a350...` (full hash recorded in web/public/landing/capture.json). Ten fictional Tuesday personas and the full Tuesday examples are approved for hosted review.

Jay explicitly requested natural visible copy: rendered screens no longer show synthetic/sample disclaimers or badges. Fixture provenance remains private in seeds, handoffs, logs and verification records. Root verified 33 focused Python tests, spec build/trace at 660/252, Resources PASS, and Planner 14 base plus six recovery flows, 179 calls and seven axe scans; two final Design Basis resize-focus runs PASS after repairing mounted-control focus tracking. Open decisions remain the brand mark, recapturing the hero board image, confirmations for deleting holidays and removing roles, and per-notification Mark read. T023 manual validation, SC-001 and Jay's final acceptance remain open; no CI or SHA is claimed here beyond the evidence below.

## Historical prepared candidate — `29e9b15c...` (superseded by `98ed319a...`)

Jay requested a current board image, seamless background blending, then explicitly preferred the older colours over the white/mint trial. The current local hero keeps the approved pale paper, forest ink and amber/sage palette. Broad washes and a soft decorative-image mask blend the app capture into the page; the floating frame, heavy shadow and old-image fallback are removed. The unchanged 1728 × 873 JPEG comes from Priya's existing synthetic `/boards` preview, visibly labelled fictional; no records were edited. Its 102957 bytes and SHA-256 match `web/public/landing/capture.json`.

Independent source/provenance review found no actionable issue. Root inspected the final desktop and phone screenshots. Final build PASS; exact type-check and lint PASS (87 existing warnings, no new); eight cold public landing/login rechecks at 1440/1024/768/375 PASS with zero findings, replacing those cases in the retained 1956-combination evidence. This is an affected-route recheck, not another fresh full sweep. Evidence: `/private/tmp/tuesday-hero-paper-{build,sweep}.log`, `/private/tmp/tuesday-sweep-hero-paper/`. ReUI's MCP audit guidance and the business UI design skill informed the review; no new component package, image generation, authentication or app behaviour changes.

The follow-up is committed and pushed only to the existing feature branch as `29e9b15c388415ed4ace830f5c10c37c6cec93f3`. [Exact-head CI 37248697226](https://github.com/jayapatel1511-hub/PM-Tool/actions/runs/37248697226) PASS: 1036 tests, zero failed/skipped, coverage and required browser gates; planner 14 base plus six recovery flows, 180 API calls, seven zero-finding axe scans. The exact tree is prepared on homedev and its JPEG hash independently matches. The active review pointer is still `d81c13f9084cfd4e828ab9762433d38c3849b302`; activation is pending Jay's own-terminal sudo. Root provided one command combining activation and the reviewed `incoming/postdeploy-29e9b15c.sh` helper, with an owner-only `data/activation-29e9b15c.log`. After Jay runs it, independently verify the log, exact image/pointer/volume, migrations, restore/persistence gates, public probes and hosted hero. The separate pilot remains unchanged. The current local result awaits Jay's visual acceptance. T023 manual screen-reader and SC-001 remain unrun. Brand mark, holiday/manual-role removal confirmations and per-notification Mark read remain open decisions.

## Historical d81 deployment evidence — verified 5 October 2026 UTC / 4 October Halifax

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
