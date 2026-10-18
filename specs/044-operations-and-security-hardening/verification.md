# Verification: Operations and Security Hardening

Slice builds in progress; see the build notes below.

| AC | Status |
|---|---|
| AC-OPS-01 | UNRUN |
| AC-OPS-02 | UNRUN |
| AC-OPS-03 | UNRUN |
| AC-OPS-04 | UNRUN |
| AC-OPS-05 | UNRUN |
| AC-OPS-06 | UNRUN |
| AC-OPS-07 | UNRUN |
| AC-OPS-08 | UNRUN |
| AC-OPS-09 | UNRUN |
| AC-OPS-10 | UNRUN |
| AC-OPS-11 | UNRUN |
| AC-OPS-12 | PASS (local) |
| AC-OPS-13 | UNRUN |
| AC-OPS-14 | UNRUN |
| AC-OPS-15 | PASS (local) |
| AC-OPS-16 | PASS (local) |
| AC-OPS-17 | PASS (local) |
| AC-OPS-18 | UNRUN |
| AC-OPS-19 | UNRUN |
| AC-OPS-20 | UNRUN |
| AC-OPS-21 | UNRUN |

## Build 044-S06

Mocked review-probe copied-cookie test and a SKIP line in the probe's social mode.

- Checks on the merged tree: `python3 -m unittest tools/test_social_activation.py tools/test_pilot_operations.py` 17 passed; `dotnet build Hub.slnx` 0 errors; `dotnet test --filter LocalSessionTests|SocialAuthTests` 19 passed.
- AC-OPS-12: PASS (local). Server tests unchanged and pass. Removing the review probe's copied-cookie check fails the new mocked test (mutation checked by the builder). In social mode the probe prints a SKIP line naming the server test. The pilot probe's test is unchanged and passes.
- Assumptions: no 044 decision (D1-D11) affects this slice; the packet ships without a flag (§48.1), so no flag was added; the SKIP line is printed only with `--social` and does not count as a failure.
- Left for Jay: nothing. The next social-mode run of the review probe on homedev will print the new SKIP line.

## Build 044-S08

Security-header test, Production and Staging start-up refusal tests, and the pilot compose seed-flag check. Test-only; no runtime change.

- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `dotnet test --filter FoundationsTests|StaticCacheTests` 19 passed; `python3 -m unittest tools.test_pilot_operations` 11 passed. The builder flipped one compose seed flag to "true" and saw the new Python test fail.
- AC-OPS-15: PASS (local). `FoundationsTests.Security_headers_are_on_every_kind_of_response` checks nosniff, the referrer policy, X-Frame-Options DENY and the required Content Security Policy directives on `/health`, `/api/v1/me` and the SPA page (temporary webroot), and `Cache-Control: no-store` on the API response.
- AC-OPS-16: PASS (local). The theory `Outside_development_there_is_no_development_sign_in_or_review_seed` over Production and Staging shows the development header is ignored and `/api/dev/users` answers 401 (never 200); in Production LocalPassword and `Seed:ReviewDemo` true both refuse to start. `test_pilot_compose_keeps_both_seed_flags_off` checks both pilot services set both seed flags to "false".
- Assumptions: the two pilot services in FR-OPS-17 are `api` and `migrate` in `hosting/homedev-pilot.compose.yml` (the only two that set the seed flags); the "404 with a sign-in" case of FR-OPS-17 is not tested, because signing in outside development needs Entra or a LocalPassword setup, and the AC only requires "never 200"; the refusal checks run only for Production, since Staging permits LocalPassword and the review seed by design; no 044 decision (D1-D11) affects this slice; the packet ships without a flag (§48.1); merge tier A (test-only).
- Left for Jay: nothing.

## Build 044-S09

Admin route sweep test. Test-only; no runtime change.

- Checks on the merged tree: `dotnet build Hub.slnx` 0 errors; `dotnet test --filter AdminSweepTests|OperationsTests|FoundationsTests|EvaluationTests` 33 passed.
- AC-OPS-17: PASS (local). `AdminSweepTests` reads the endpoint data source and calls every `/api/v1/admin` route, including `POST /api/v1/admin/evaluate/{projectId}` (mapped outside the Admin group), as a team member and as a Project Manager; every call returns 403 and the activity log gains no rows. The builder removed the evaluate route's inline admin check and saw the test fail (204 for both users), then reverted it. The `BindsFirst` exception list is empty.
- Assumptions: the team member is `alex@hub.test` and the Project Manager is `priya@hub.test` (seeded dev users, neither an app-level Admin); placeholder route values are a new GUID for guid or `*id` parameters, 1 for int or long, otherwise "x"; write methods send `{}`; no 044 decision (D1-D11) affects this slice; the packet ships without a flag (§48.1); merge tier A (test-only).
- Left for Jay: nothing.
