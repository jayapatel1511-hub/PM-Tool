# Plan: Route Splitting and Live Updates

Implement §46 with defaults D1 to D11. Every slice is one PR off `origin/main`, in the order of [tasks.md](tasks.md). No new dependency, service, datastore or migration, except the additive index of D11 if its ordering test fails.

## Approach

- **Build stamp.** One `RELEASE_SHA` build argument feeds both halves: `VITE_HUB_BUILD` (a native Vite variable, no `define`) in the web stage and `HUB_BUILD` in the runtime stage. The server reads `HUB_BUILD` once at start (`dev` when unset), adds `X-Hub-Build` in the existing `SecurityHeaders` middleware so every response carries it, and adds `build` to `/health`. The SPA's build is one exported constant in `api.ts`, which the §48 error page can also show. The client compares the header in `api()` and shows one sonner toast with an action, the pattern already used at `web/src/pages/projects/TaskPanel.tsx:454`.
- **Registrations first, then laziness.** S05 moves the 22 registration lines into `web/src/app/registrations.tsx` with no loading change, so its boot suite proves today's behaviour before anything goes lazy. S06 and S07 then switch routes to the router's `lazy` and swap the registrations whose components sit in a lazy area for small lazy wrappers that render their own `Suspense` with `Loading`. Area modules (for example `web/src/pages/projects/area.ts`) re-export each area's pages so one dynamic import makes one chunk. The sign-in gate loads the entrance pages the same way.
- **Caching and skew.** `UseStaticFiles` and `MapFallbackToFile` get `StaticFileOptions.OnPrepareResponse`: immutable for `/assets/`, `no-cache` for `index.html`. One helper wraps every dynamic import: on failure it reloads once per build (session-storage key holds the build), otherwise rethrows to the §48 error page.
- **Project stamp.** An optional `projectId` on the existing pulse adds `projectStamp` from two indexed reads: the newest visible activity time for the project (index `ProjectId, OccurredAt`, `src/Hub.Api/Data/HubDb.cs:411`) and `project_state.evaluated_at` (primary key). The `stamp` code path is untouched.
- **Change feed.** A singleton `BackgroundService` modelled on `EvaluationWorker` (`src/Hub.Api/Infrastructure/EvaluationService.cs:227-287`), with a static `TickOnce` that tests drive directly. It runs only while a stream is open, reads `activity_log` by uuid v7 key range from a lower bound built from the last delivered id's timestamp minus the overlap, keeps a set of recent ids for de-duplication, and reads `project_state` (one row per project) for evaluation times. Each stream subscribes through a bounded channel; on overflow the stream closes and the client's reconnect refresh covers the gap.
- **Stream endpoint.** `GET /api/v1/events` in a new feature file. The handler runs in its own request scope, so it reuses `Access.VisibleProjectIdSet()` (`src/Hub.Api/Infrastructure/Access.cs:58`) to compute and recompute the viewer's set. `TypedResults.ServerSentEvents` is used if it runs as documented in the pinned SDK; otherwise the handler writes `text/event-stream` lines and flushes, which is equally built in.
- **Client stream.** `api.ts` gains one stream reader that shares `authHeaders` with `api()`, so cookie, bearer and development modes take the same path. A `useLiveEvents` hook mounted once in `Shell` reads `flag.live_events` from `/me`, applies the liveness rules, and hands each batch to the §44 map, which returns the query keys to invalidate.

## File boundaries

| Slice | Files |
|---|---|
| S01 | `hosting/Dockerfile.review`, `hosting/homedev.compose.yml`, `hosting/homedev-pilot.compose.yml`, `src/Hub.Api/Program.cs` (`/health`), `src/Hub.Api/Infrastructure/Http.cs` (`SecurityHeaders`), `tests/Hub.Tests/Api/BuildStampTests.cs` |
| S02 | `web/src/lib/api.ts`, `web/src/i18n/en.ts`, `web/tests/build-stamp.cjs`, `web/package.json`, `tools/ci_steps.txt` |
| S03 | `scripts/verify-homedev-review.py`, `scripts/verify-homedev-pilot.py`, `scripts/activate-homedev-review.sh`, `scripts/activate-homedev-pilot.sh`, `scripts/test_verify_homedev_review.py`, `tools/test_pilot_operations.py`, `tools/ci_steps.txt` |
| S04 | `src/Hub.Api/Program.cs` (static files, fallback), `tests/Hub.Tests/Api/StaticCacheTests.cs` |
| S05 | `web/src/app/registrations.tsx` (new), `web/src/main.tsx`, `web/src/app/routes.tsx`; registration lines removed from `web/src/pages/projects/{TaskPanel,Milestones,Deliverables,Decisions,Meetings,Registers,Health,Follow,CopyStructure,FromTemplate}.tsx`, `web/src/pages/admin/{Holidays,Operations}.tsx`, `web/src/components/hub/{comments,links,new-item,notifications,search,workspace}.tsx` (exports added where a component was private); `web/tests/registrations.cjs`, `web/package.json`, `tools/ci_steps.txt` |
| S06 | `web/src/app/routes.tsx`, `web/src/lib/auth.tsx` (lazy entrance pages), `web/src/lib/lazy.ts` (new), `web/src/pages/admin/area.ts` and `web/src/pages/planning-area.ts` (new), `web/vite.config.ts` (`build.manifest`), `web/scripts/chunk-report.mjs` (new), `web/tests/route-splitting.cjs`, `tests/Hub.Tests/Api/StaticCacheTests.cs`, `web/package.json`, `tools/ci_steps.txt` |
| S07 | `web/src/app/routes.tsx`, `web/src/app/registrations.tsx`, `web/src/components/hub/panel-host.tsx` (`Suspense`), `web/src/pages/projects/area.ts` (new); shared helpers moved out of `web/src/pages/projects/{Tasks,Meetings,Board,Follow,ProjectLayout,CreateProject}.tsx` and `web/src/pages/Calendar.tsx` into `web/src/components/hub/` only if D4 needs it; `web/tests/route-splitting.cjs` |
| S08 | `src/Hub.Api/Features/Notifications.cs` (pulse), `src/Hub.Domain/Flags.cs` (§43), `web/src/components/hub/notifications.tsx`, `tests/Hub.Tests/Api/LiveStampTests.cs`, `web/tests/live-refresh.cjs`, `web/package.json`, `tools/ci_steps.txt` |
| S09 | `src/Hub.Api/Infrastructure/LiveFeed.cs` (new), `src/Hub.Api/Program.cs` (hosted service), `src/Hub.Api/appsettings.json` (`Live:*`), `tests/Hub.Tests/Api/LiveFeedTests.cs` |
| S10 | `src/Hub.Api/Features/Events.cs` (new), `src/Hub.Api/Program.cs` (map), `src/Hub.Domain/Flags.cs` (§43), `tests/Hub.Tests/Api/LiveEventsTests.cs` |
| S11 | `web/src/lib/api.ts` (stream reader), `web/src/hooks/live.ts` (new), `web/src/app/Shell.tsx`, `web/src/lib/keys.ts` (§44 map, additive entries only), `web/tests/live-events.cjs`, `web/package.json`, `tools/ci_steps.txt` |
| S12 | `web/tests/live-real.cjs`, `scripts/check-live-stream.py`, `scripts/test_check_live_stream.py`, a `nightly` line in `tools/ci_steps.txt` |
| S13 | `src/Hub.Domain/Flags.cs`, `src/Hub.Api/Features/Notifications.cs`, `src/Hub.Api/Features/Events.cs`, `web/src/components/hub/notifications.tsx`, `web/src/hooks/live.ts` and the flag tests |

Unrelated edits in these files are preserved. Slices touching `hosting/**`, `scripts/activate-*` or `.github/**` are Tier B under §43, and so is S02, an unflagged change users can see (D6); S10 is security-relevant and is reviewed by Jay. New suites and checks join CI as lines in packet 039's `tools/ci_steps.txt`, not as workflow edits.

## Constitution Check

| Principle | Result |
|---|---|
| I. Coordination First | PASS. Fresher screens answer "what changed and what is stopping the project" without a reload. No feature is removed, hidden or narrowed. |
| II. Deterministic and Explainable | PASS. No AI. No rule or threshold changes; stream parameters are transport tuning held in configuration, not rule thresholds. |
| III. One Accountable Owner | PASS. No ownership change. |
| IV. Specification Is the Source of Truth | PASS. §46 is added, and §17.6 and §28 item 9 are amended in the spec PR before any slice. |
| V. Traceable by Construction | PASS. The feed only reads committed `activity_log` and `project_state` rows; it writes nothing and bypasses no hook. |
| VI. Secure by Default | PASS. Every event passes the server-side list visibility rule, recomputed on visibility changes and bounded by a 10-minute stream life; events carry no content; no token in any URL; Entra bearer mode works through `fetch`; the build identifier holds only the SHA (D1). |
| VII. Simple Enough for a Small Team | PASS. In-process `BackgroundService` and endpoint like the existing evaluation worker; no new service, broker, backplane or datastore; one feed per instance reads the shared database, so two instances need nothing more. No Complexity Tracking entry. |

Open product decisions from §34 this plan depends on: none.

## Risks

- A registration missed by S05 would hide a control once its area goes lazy; the source check and the boot suite run in every later PR.
- Cross-imports from My Work, the shell and quick create pull project modules into the first chunk; D4 allows pure moves to meet AC-LIVE-08.
- The tunnel's handling of long streams is unknown until AC-LIVE-17; the flag stays off in review until it passes, and today's refresh is unaffected either way.
- uuid v7 order in PostgreSQL is inferred until AC-LIVE-19; D11 names the fallback.

## Checks to run

- `dotnet test Hub.slnx` (full suite) with `python3 tools/coverage_gate.py coverage`; new classes `BuildStampTests`, `StaticCacheTests`, `LiveStampTests`, `LiveFeedTests`, `LiveEventsTests`.
- `npm run build`, `npm run lint`, and in `web/`: `test:build-stamp`, `test:registrations`, `test:route-splitting`, `test:live-refresh`, `test:live-events`, plus every existing mocked suite unchanged.
- The local-only suites (`calm-acceptance`, `calm-clicks`, `clutter`, `counts`, `planning-live`, `ui-consistency`) and `tools/preview/sweep.cjs` before and after S06 and S07.
- `python3 -m unittest scripts/test_verify_homedev_review.py scripts/test_check_live_stream.py` and `python3 tools/test_pilot_operations.py`.
- `web/tests/live-real.cjs` in the §43 nightly real-API job; `scripts/check-live-stream.py` against a cloudflared quick tunnel to a local stack, then against the review origin.
