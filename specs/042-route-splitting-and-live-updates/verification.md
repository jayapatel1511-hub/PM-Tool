# Verification: Route Splitting and Live Updates

**Current status (2026-10-29):** Implemented and checked locally (S01–S12), including the real-API lifecycle checks. S13 flag cleanup, hosted/tunnel checks and human acceptance remain pending. Final local release evidence and assumptions are in [the release candidate](../../docs/handoffs/2026-10-29-packets-039-051-release-candidate.md). Historical slice records below retain their original source boundaries. Nothing is pushed, hosted or Accepted by this session.

**Status (2026-10-25, branch `codex/integrate-039-050`):** S01–S12 built locally with passing local checks; S13 flag cleanup waits on Jay turning both flags on. AC-LIVE-05, AC-LIVE-16 and AC-LIVE-18 moved to PASS (local) in the 2026-10-25 gap run below. Hosted, tunnel and human checks not run. Not Accepted. Records check on 2026-10-25 re-ran `scripts/test_check_live_stream.py` and `scripts/test_verify_homedev_review.py` (OK); backend tests were not re-run because local Postgres would not start, so backend rows rely on the 566fe51 full run below. The planned `build-stamp.cjs`, `live-refresh.cjs` and `live-events.cjs` were built as one `web/tests/live-browser.cjs` (with `registrations-browser.cjs` and `route-splitting.cjs`, run by `test:browser-verification`).

Review-fix run 2026-10-25 (local; `cf3ce8ee` plus the Code review and cleanup session's uncommitted fixes; fresh production build). `live.ts` now stops on 4xx refusals (the 429 cap keeps today's refresh), refreshes the open project once after each successful reopen, resets backoff after a stream that lived at least 60 s, and no longer reconnects on project navigation. Browser results: registrations.cjs PASS (new single-declaration guard), registrations-browser PASS (7 panels), route-splitting PASS (3 routes, 122 chunks, 0 failed), live-browser PASS including a new 429 case (one /events request, no retry over 4 s). Browsers ran on the installed headless Chromium build 1248 instead of the 1228 build Playwright 1.61.1 expects. Not committed yet.

Runtime source `566fe5187d72be56a14fd16d0428e8b8e6e34d72` passes **1,514/1,514 backend tests** (zero failures/skips). Rules branch coverage is **3,032/3,155 (96.1%)** and service line coverage **13,084/13,665 (95.7%)**. The 799-response shape gate passes from one full-run manifest. See the [acceptance follow-up](../../docs/handoffs/2026-10-19-039-050-acceptance-followup.md) and its hashed receipt for current browser, image and source boundaries. Earlier slice runs below are historical; their missing-schema/startup blockers are resolved by the combined run. Prior compatibility evidence retains source `1b649f5`; no schema changed afterward. No packet is Accepted; hosted, pilot and human gates remain separate.

| AC | Where it is checked | Status |
|---|---|---|
| AC-LIVE-01 | `BuildStampTests` (S01) | PASS (local): current 566fe51 image OCI revision/HUB_BUILD match; health build verified; hosted image activation remains unproven |
| AC-LIVE-02 | `web/tests/live-browser.cjs` (S02) | PASS (local): stamped A→B→A browser notice, one per new build |
| AC-LIVE-03 | `tools/test_social_activation.py` (`test_review_probe_checks_the_expected_build`, which replaced the source-text check `scripts/test_verify_homedev_review.py` on 2026-10-25), `tools/test_pilot_operations.py` (S03) | PASS (local): Python activation/probe controls; hosted execution unproven |
| AC-LIVE-04 | `web/tests/registrations.cjs` (S05, re-run in S07) | PASS (local): all seven Task, Milestone, Deliverable, Decision, Action, Risk and Issue cold deep links render their own headings; Task comments/links also checked |
| AC-LIVE-05 | `web/tests/registrations.cjs` (S05, re-run in S07), `web/tests/live-lifecycle-real.cjs` (GAP-042) | PASS (local): real-API new tab on `/my-work?panel=Task:<id>` opens the Task panel over My Work and "Open full page" goes to `/tasks/<id>` (2026-10-26 run below); all plugin combinations not swept |
| AC-LIVE-06 | `web/tests/registrations.cjs` self-test (S05) | PASS (local): seeded registration guard controls |
| AC-LIVE-07 | `web/tests/route-splitting.cjs`, existing suites, `tools/preview/sweep.cjs` (S06, S07) | PASS (local): 59 representative real-API routes including all 24 project tabs, plus five legacy URL/mode checks; no failed API/chunk transport, error banners or serious/critical axe violations. This is route-pattern coverage, not every parameter/role/filter combination |
| AC-LIVE-08 | `web/scripts/chunk-report.mjs` CI artifact (S07) | PASS (local): raw entry closure 45.06% smaller; gzip 35.33% |
| AC-LIVE-09 | `StaticCacheTests` (S04) | PASS (local) |
| AC-LIVE-10 | `web/tests/route-splitting.cjs` (S06) | PASS (local): aborted route chunk reaches error boundary; storage-denied loop guard verified |
| AC-LIVE-11 | `LiveStampTests`, `web/tests/live-browser.cjs` (S08, S13) | PASS (local): project pulse mocked browser and scoped API tests |
| AC-LIVE-12 | `LiveStampTests` (S08) | PASS (local): scoped project-stamp access checks |
| AC-LIVE-13 | `web/tests/live-real.cjs` as a `nightly` line in `tools/ci_steps.txt` (S12) | PASS (local): current 566fe51 real two-browser Risk/Task changes arrive within 2 seconds; no hosted activation |
| AC-LIVE-14 | `LiveEventsTests` visibility sweep (S10) | PASS (local): focused API visibility checks |
| AC-LIVE-15 | `LiveEventsTests` (S10, S13), `web/tests/live-browser.cjs` (mocked) and `web/tests/live-real.cjs` (real two-browser) (S11) | PASS (local), bounded: scoped API/stream and real two-browser Risk/Task updates pass; full hosted lifecycle remains unproven |
| AC-LIVE-16 | `LiveEventsTests` (S10), `web/tests/live-browser.cjs` (mocked), `web/tests/live-real.cjs` (real two-browser) (S11) and `web/tests/live-lifecycle-real.cjs` (GAP-042) | PASS (local): real 25 s heartbeat, 60 s silence then 1 s reconnect and one project refresh, hidden-tab close and reopen, host stop and 1/2/4/8/16/30 s backoff, sixth-stream 429 (2026-10-26 run below); the hidden tab is simulated through `document.visibilityState`; hosted run unproven |
| AC-LIVE-17 | `scripts/check-live-stream.py` on the review origin (after S12; needs an authorised activation) | HUMAN: hosted/tunnel run needs an activation Jay authorises; not run |
| AC-LIVE-18 | `web/tests/live-browser.cjs` (S08), `web/tests/live-browser.cjs` (mocked), `web/tests/live-real.cjs` (real two-browser) (S11, S13) and `web/tests/live-lifecycle-real.cjs` with `web/scripts/build-before-live.mjs` (GAP-042) | PASS (local): flags off, the scripted session makes the same 30 requests as the before build, none to `/api/v1/events`, with the 1.5 s follow-up and 4 s pulse; flags on, both still run (2026-10-26 run below); S13 flag removal pending |
| AC-LIVE-19 | `LiveFeedTests` (S09) | PASS (local) |

## Build 042-S01

- Merged into `claude/build-039-050` (slice commit 0dcf17a0). `hosting/Dockerfile.review` takes `RELEASE_SHA` (default `dev`), exposed as `VITE_HUB_BUILD` in the web stage and `HUB_BUILD` at runtime; both compose files pass `RELEASE_SHA: ${RELEASE_SHA:-dev}`. `SecurityHeaders` in `src/Hub.Api/Infrastructure/Http.cs` adds `X-Hub-Build` to every response; `/health` returns `build` beside `status` with `Cache-Control: no-store` (FR-LIVE-01, FR-LIVE-02).
- AC-LIVE-01 PARTIAL: `BuildStampTests` checks the header on an API call, a static file, a 404 and `/health`, and the `/health` body and `no-store`, for `dev` (unset) and a configured 40-character SHA. The hosted half (a real image built with `RELEASE_SHA=<sha>`) was not run.
- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `BuildStampTests`, `StaticCacheTests`, `FoundationsTests`, `OperationsTests`, `DeploymentConformanceTests` 34/34 passed; `tools/test_backup_helpers.py`, `tools/test_pilot_pitr.py`, `tools/test_pilot_operations.py` OK. No web change, so npm build, lint and browser suites were not run.
- Assumptions: 039-D15 default (RELEASE_SHA becomes HUB_BUILD; this slice owns the argument and compose wiring). No flag, Tier B, as the packet says. A review build without a SHA reports `dev`; the pilot file already refuses to start without `RELEASE_SHA`. The pilot migrate service reuses the api image tag, so only services that build an image got the argument.
- Left for Jay: refresh the root-owned compose copies on homedev before the §43 review timer deploys again (FR-DLV-18); then check a real image built with `RELEASE_SHA=<sha>` (`/health` returns that build and `X-Hub-Build` matches). The §43 CI image must pass the commit it builds as `RELEASE_SHA`.

## Build 042-S04

- Merged into `claude/build-039-050` (slice commit a186673f). One `StaticFileOptions` instance is shared by `UseStaticFiles` and `MapFallbackToFile` in `src/Hub.Api/Program.cs` (FR-LIVE-10).
- AC-LIVE-09 PASS (local): `StaticCacheTests` uses a temporary web root; `/assets/app-<hash>.js` is `public, max-age=31536000, immutable`; `/`, `/index.html` and `/projects/P-1/tasks` are `no-cache`; `favicon.svg` keeps no Cache-Control; `/api/v1/me` stays `no-store`. The test fails with the `Program.cs` change removed.
- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `StaticCacheTests`, `FoundationsTests`, `DeploymentConformanceTests` 25/25 passed. No web change, so npm build, lint and browser suites were not run.
- Assumptions: 042-D6 default, caching ships unflagged (the packet gives S04 no flag). The test points the web root at a temporary folder through the HubFactory `webroot` setting.
- Left for Jay: after a deploy, optionally confirm the headers on the review origin with `curl -I` on an `/assets` file and on `/`.

## Build 042-S09

- Merged into `claude/build-039-050` (slice commit c0fd9b66). `LiveFeed` in `src/Hub.Api/Infrastructure/LiveFeed.cs` is a singleton hosted service registered in `src/Hub.Api/Program.cs`; `Live:*` settings added to `appsettings.json` (FR-LIVE-15).
- AC-LIVE-19 PASS (local): `LiveFeedTests` 7/7 (uuid v7 ordering, a late-committing row delivered exactly once, evaluated rows once per evaluation, no rows written, outbox untouched, subscribers). The review fix prunes the de-duplication set against the same millisecond-rounded lower bound as the query; `Row_in_the_boundary_millisecond_is_delivered_once` fails without it.
- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `LiveFeedTests` 7/7; full `dotnet test Hub.slnx` 1197/1197 passed (`Program.cs` auto-merged). Builder also ran `tools/coverage_gate.py` (rules engine branch 96.6%, service line 94.7%). No web change, so npm build, lint and browser suites were not run.
- Assumptions: no Open decision from `specs/DECISIONS.md` relied on. The feed stays idle until a stream subscribes (S10), so behaviour is unchanged until `flag.live_events` lands with S10. The prune bound is rounded down instead of rounding the public `Cursor` at construction.
- Left for Jay: nothing new; pushing, the pull request and merging remain his.


## Local implementation handoff — 2026-10-18

Recovered b4541e89 as ccdddc85 and retained the saved 042-S10 follow-up. Existing flags were preserved. The follow-up adds User visibility reload and actor-only routing; the invisible-batch heartbeat starvation defect found in review is fixed with a monotonic heartbeat budget and a regression.

- PASS: focused LiveEvents/LiveStamp checks in the 40-test contract/registry/live handoff run; query-prefix tests preserve existing refreshes and the 1.5-second follow-up, and cover reconnect/open-panel and child-record invalidations.
- PASS: production build, eager registration source guard and scoped Python probe regressions. The retry marker is persisted before an automatic chunk reload; denied storage reaches the error page without an automatic reload loop.
- PASS (raw bytes): manifest entry plus all transitive static imports for `/my-work`: verified a6e0104b baseline 2,233,051 bytes, candidate 1,226,771 bytes, a 45.06% reduction. Gzip totals are 572,815 and 370,455 bytes (35.33% reduction); D4 is stated in uncompressed bytes. Both local reports are under `ci-artifacts/platform/chunks-*.json`.
- Routes use React Router lazy descriptors; My Work stays eager. Entrance and the Entra provider load only when needed. Panel and shell slot suspensions have local fallbacks. The reviewed 044 error page/client reporter files were adopted from 0535311e; route reporting retains the route pattern.
- HISTORICAL FAIL (resolved below): the new mocked registration/route/live browser acceptance suite failed its Task deep-link fixture after the heading wait; its application error/fixture cause is under investigation. It is wired into CI and must be fixed before the web gate can pass. The real two-browser suite, complete route sweep and hosted/tunnel checks remain unproven.
- CI wiring includes the stable mocked suite and explicit nightly real suites. Record parity is a separate 043 handoff and is not part of this source commit.
- S13 flag retirement, hosted activation, tunnel acceptance and human review remain pending. No review/pilot flag was turned on; only disposable local test databases were changed.

## Followup browser verification — 18 October 2026

The missing mocked DTOs were views, Task links and comments, planning grid, project task pages, project health/date review and admin users. Each now has an explicit shape; unknown GETs return 501 and fail every new browser acceptance test. Registration diagnostics retain body/page/console/request evidence.

PASS: registration deep link; normal route loading; exactly one aborted route chunk reaches the error boundary; authenticated live stream without URL token; open-project pulse; zero unknown GETs/page errors/serious or critical axe violations. A production build stamped platform-local-spike passes the actual A→B→A notice sequence. The fixture changes its stable build only on navigation, avoiding background /me refresh races. The web job now stamps its build with github.sha so this branch runs in CI. These are local production-preview checks, not a hosted release or complete route/panel sweep.

The previously recorded mocked-browser FAIL is resolved by this followup. Existing real-API smoke evidence remains valid for the foundation; final synthetic runner and existing browser suite reruns are tracked in the final handoff. No hosted flags were activated.

### Real local browser followup

The maintained live-real.cjs now exercises the actual Development picker (pre-seeding auth had redirected away before the picker), and targets visible Risk row buttons. A fresh owned synthetic API18512/preview19512 run passes: two browser contexts, four queryless streams with Alex's header, Risk and Task changes visible within 2 seconds, zero browser errors. Both flags are restored in finally and owned ports are closed. This proves those local flows only; role revocation, restricted-project changes, outage/silence/hidden-tab timing and hosted/tunnel behavior remain unproven.

Fresh owned real-API smoke on18522/19522 also passes all five route/mode checks, with zero axe violations and page errors. Existing suites handoffs, sign-in, coordination, design-basis, planner, resources, supervisor-capacity and ui-review pass against the candidate. Design-basis needed only the allowed meta fixture for the converted Allocations page; supervisor capacity used its explicit spare-port setting5278 because another chat owned5178.

The required web-job owned-service runner now executes smoke-live.cjs and live-real.cjs sequentially against its one fresh synthetic database. A final invocation on18542/19542 passes both. This closes the previous source-only nightly wiring gap for those covered live scenarios; the optional nightly command also now uses the actual LIVE_UI_URL/LIVE_API_URL variable names. The occupied-port regression remains PASS and cleanup kills only the owned process groups.

## Acceptance follow-up (2026-10-19)

The current full backend run contains 10 LiveEvents, three LiveStamp and two BuildStamp passes. All seven panel registrations and the bounded three-route chunk test pass (122 asset responses, zero failed chunks). The aggregate mock command then failed because its invocation omitted `VITE_HUB_BUILD` despite using a stamped production build; the separate corrected live command passes A→B→A, authenticated queryless stream, project pulse and axe. This invocation failure is retained, not reported as an aggregate PASS.

The strengthened owned real-API runner passes all 59 representative routes, five legacy URL/mode flows and the two-browser live flow with four streams, Risk/Task updates within 2 seconds and zero browser errors. Its source 566fe51 health stamp is recorded. The runner waits for sign-in to finish, catches API/chunk transport failures and rejects rendered ErrorBanner content. The sole expected HTTP failure is 404 for the seeded task with no pickup offer. It initializes audit chains only in migration-only mode on its newly generated synthetic database. No hosted/pilot flags were changed.

## GAP-042 real-API lifecycle run (2026-10-26)

- `web/tests/live-lifecycle-real.cjs` (`npm run test:live-lifecycle`) owns an API, a stall/outage proxy and a preview on a fresh synthetic database (`hub_live_57718_1793053655002`, ports 18562/18563/19562). It first builds the "before" SPA with `web/scripts/build-before-live.mjs`: today's `web/` source with two in-memory edits (Shell.tsx without `useLiveEvents()`, useUnread with the plain pulse from 46dc7685^); the script fails if either edit no longer applies. The AC-LIVE-18 comparison is no longer optional: every run records the before session and asserts the candidate's flags-off log equals it. `TUESDAY_LIVE_BEFORE_DIST` only reuses an existing before build.
- Review fix: the first recorded run (GAP-042 commit 18223c10) had no before build, skipped the comparison and still printed PASS, so it did not prove AC-LIVE-18's main clause.
- Run on review-fix source after `npm run build` (candidate `src/Hub.Api/wwwroot`), headless Chromium build 1248: PASS. The before bundle has the plain pulse only, no event-stream reader and no `live_project_stamp`; preview log shows two starts (before, then candidate). Before session: pulse gaps 4,022–4,038 ms, follow-up 1,503 ms. Flags off: pulse 4,012–4,024 ms, follow-up 1,503 ms, no `/api/v1/events`, pulse without a project id, and the same 30 non-pulse requests as the before session (`sameAsBefore: 30`). Flags on: pulse 4,014–4,036 ms, follow-up 1,503 ms, project pulse and stream present. Silence: silent stream closed after 60,997 ms, reopened 1,001 ms later, two project reads (one refresh) after the reopen, heartbeats 25,000 ms apart. Hidden tab (simulated through `document.hidden`/`visibilityState`): stream closed in 103 ms, none while hidden, one reopened 3 ms after visible with one project refresh. Host stop: exit in 21 ms, held stream ended 1 ms after the stop, backoff 1,004/2,004/4,005/8,003/16,002/30,004 ms, one project refresh after the reopen. Cap: sixth stream 429, 200 after closing one. Deep link: `/my-work?panel=Task:<id>` in a new tab opened the panel over My Work; "Open full page" went to `/tasks/<id>`. Zero browser errors. Evidence in `ci-artifacts/live-lifecycle/evidence.json` (untracked).
- Not run: hosted/tunnel lifecycle (AC-LIVE-17), S13 flag removal. `test:live-lifecycle` is not wired into `tools/ci_steps.txt`.
- Assumptions: no Open decision from `specs/DECISIONS.md` relied on; the test changes no product code, so flag-off behaviour is unchanged.

## 2026-10-25 gap run

AC-LIVE-05, AC-LIVE-16 and AC-LIVE-18 were PARTIAL. `build2/GAP-042` (`d50b79c8`, base `0f28826d`) adds `web/tests/live-lifecycle-real.cjs` (`npm run test:live-lifecycle`) and `web/scripts/build-before-live.mjs`, and was merged into `codex/integrate-039-050`. The only conflict was `web/package.json` scripts; both `test:record-parity` and `test:live-lifecycle` were kept. No product code or flag changed, so flags-off behaviour is unchanged.

Rerun on the merged tree (2026-10-26): `dotnet build Hub.slnx` 0 errors; `npm run build` and `npm run lint` (exit 0, existing warnings only); `npm run test:live-lifecycle` on headless Chromium build 1248, fresh synthetic database `hub_live_60695_1793054023274`, ports 18562/18563/19562: PASS. Before build: pulse gaps 4,010–4,034 ms, follow-up 1,508 ms. Flags off: pulse 4,011–4,020 ms, follow-up 1,503 ms, `sameAsBefore: 30`. Flags on: pulse 4,011–4,028 ms, follow-up 1,504 ms. Silence: closed after 60,998 ms, reconnect 1,003 ms, two project reads after the reopen, heartbeats 25,000 ms apart. Hidden tab (simulated): close 104 ms, reopen 5 ms after visible. Host stop: exit 26 ms, held stream ended 1 ms after, backoff 1,004/2,002/4,010/8,012/16,004/30,003 ms. Cap: sixth stream 429, 200 after closing one. Deep link: `/my-work?panel=Task:<id>` opened the panel; "Open full page" went to `/tasks/<id>`. `tools/trace_spec.py --check` and `tools/build_spec.py --check` pass.

Still open: AC-LIVE-17 hosted/tunnel run (HUMAN), S13 flag removal, wiring `test:live-lifecycle` into `tools/ci_steps.txt`, and hosted checks. No acceptance criterion or packet is Accepted.

## 2026-10-28 release review

- `web/tests/live-lifecycle-real.cjs` counted project reads only 1.3 to 2.4 s after the stream reopened, so an extra refresh outside that window would have passed. It now counts every project read from the reopen: one `refreshProject` is one read at once plus its 1.5 s follow-up (for health the server is still evaluating), and the test requires exactly that, for the tab becoming visible and for the reconnect after the outage. Local real-API run 2026-10-28: PASS; reads at 3 and 1505 ms (visible) and 1 and 1504 ms (outage reopen), recorded in the run's evidence as `projectReadsMs`.

## 2026-10-29 combined lifecycle rerun

The S15 conversion removed the legacy get import used by the in-memory pre-live client builder. The adapter now calls typedRequest with required options while retaining the original pulse paths and 4-second cadence. The first two attempts timed out before the pulse after an incomplete adapter update; the final real-API run passes. Failure diagnostics now retain rendered state and partial request logs before stack cleanup. Evidence: 31 identical normalized flags-off requests; pulse gaps about 4.0 seconds; 1.5-second follow-up reads; silent stream closed at 60,998 ms and reconnected in 1,003 ms; hidden-tab stream closed/reopened, outage backoff 1/2/4/8/16/30 seconds and one read plus follow-up; sixth connection refused 429 and accepted after another closed. No hosted/tunnel acceptance. Assumption: the pre-live comparison changes only transport behavior; typed helper paths/headers preserve the same logical reads.


### Final local release evidence, 2026-10-29

Final real lifecycle gate PASS on the final application assets: 31 matching baseline/off reads, pulse ~4 seconds, follow-up ~1.5 seconds, silent stream close 60,998 ms/reconnect1,002 ms, hidden-tab close/reopen, 1/2/4/8/16/30-second outage backoff, sixth connection429 then200 after closing. No hosted/tunnel claim. See [the release handoff](../../docs/handoffs/2026-10-29-packets-039-051-release-candidate.md) and its machine-readable evidence. No packet is marked Accepted.
