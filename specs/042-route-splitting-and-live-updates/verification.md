# Verification: Route Splitting and Live Updates

In progress. S01 and S04 are checked locally; other slices are not yet run.

| AC | Where it is checked | Status |
|---|---|---|
| AC-LIVE-01 | `BuildStampTests` (S01) | PARTIAL: in-process only; a real image built with `RELEASE_SHA` not checked (needs Docker or homedev) |
| AC-LIVE-02 | `web/tests/build-stamp.cjs` (S02) | UNRUN |
| AC-LIVE-03 | `scripts/test_verify_homedev_review.py`, `tools/test_pilot_operations.py` (S03) | UNRUN |
| AC-LIVE-04 | `web/tests/registrations.cjs` (S05, re-run in S07) | UNRUN |
| AC-LIVE-05 | `web/tests/registrations.cjs` (S05, re-run in S07) | UNRUN |
| AC-LIVE-06 | `web/tests/registrations.cjs` self-test (S05) | UNRUN |
| AC-LIVE-07 | `web/tests/route-splitting.cjs`, existing suites, `tools/preview/sweep.cjs` (S06, S07) | UNRUN |
| AC-LIVE-08 | `web/scripts/chunk-report.mjs` CI artifact (S07) | UNRUN |
| AC-LIVE-09 | `StaticCacheTests` (S04) | PASS (local) |
| AC-LIVE-10 | `web/tests/route-splitting.cjs` (S06) | UNRUN |
| AC-LIVE-11 | `LiveStampTests`, `web/tests/live-refresh.cjs` (S08, S13) | UNRUN |
| AC-LIVE-12 | `LiveStampTests` (S08) | UNRUN |
| AC-LIVE-13 | `web/tests/live-real.cjs` as a `nightly` line in `tools/ci_steps.txt` (S12) | UNRUN |
| AC-LIVE-14 | `LiveEventsTests` visibility sweep (S10) | UNRUN |
| AC-LIVE-15 | `LiveEventsTests` (S10, S13), `web/tests/live-events.cjs` (S11) | UNRUN |
| AC-LIVE-16 | `LiveEventsTests` (S10), `web/tests/live-events.cjs` (S11) | UNRUN |
| AC-LIVE-17 | `scripts/check-live-stream.py` on the review origin (after S12; needs an authorised activation) | UNRUN |
| AC-LIVE-18 | `web/tests/live-refresh.cjs` (S08), `web/tests/live-events.cjs` (S11, S13) | UNRUN |
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
- FAIL: the new mocked registration/route/live browser acceptance suite currently fails its Task deep-link fixture after the heading wait; its application error/fixture cause is under investigation. It is wired into CI and must be fixed before the web gate can pass. The real two-browser suite, complete route sweep and hosted/tunnel checks remain unproven.
- CI wiring includes the stable mocked suite and explicit nightly real suites. Record parity is a separate 043 handoff and is not part of this source commit.
- S13 flag retirement, hosted activation, tunnel acceptance and human review remain pending. No review/pilot flag was turned on; only disposable local test databases were changed.
