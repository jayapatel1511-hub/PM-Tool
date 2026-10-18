# Verification: Route Splitting and Live Updates

In progress. S04 is checked locally; other slices are not yet run.

| AC | Where it is checked | Status |
|---|---|---|
| AC-LIVE-01 | `BuildStampTests` (S01) | UNRUN |
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
| AC-LIVE-19 | `LiveFeedTests` (S09) | UNRUN |

## Build 042-S04

- Merged into `claude/build-039-050` (slice commit a186673f). One `StaticFileOptions` instance is shared by `UseStaticFiles` and `MapFallbackToFile` in `src/Hub.Api/Program.cs` (FR-LIVE-10).
- AC-LIVE-09 PASS (local): `StaticCacheTests` uses a temporary web root; `/assets/app-<hash>.js` is `public, max-age=31536000, immutable`; `/`, `/index.html` and `/projects/P-1/tasks` are `no-cache`; `favicon.svg` keeps no Cache-Control; `/api/v1/me` stays `no-store`. The test fails with the `Program.cs` change removed.
- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `StaticCacheTests`, `FoundationsTests`, `DeploymentConformanceTests` 25/25 passed. No web change, so npm build, lint and browser suites were not run.
- Assumptions: 042-D6 default, caching ships unflagged (the packet gives S04 no flag). The test points the web root at a temporary folder through the HubFactory `webroot` setting.
- Left for Jay: after a deploy, optionally confirm the headers on the review origin with `curl -I` on an `/assets` file and on `/`.
