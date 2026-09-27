# Verification: Platform Foundations

Date: 2026-09-24. Environment: macOS, .NET SDK 10.0.400, Node 26, PostgreSQL 17 (docker compose, port 55432).

## Automated checks

| Check | Command | Result |
|---|---|---|
| Solution builds | `dotnet build Hub.slnx` | Pass, 0 warnings |
| API tests | `dotnet test tests/Hub.Tests` (FoundationsTests, 10 tests) | Pass (10/10) |
| SPA type check and bundle | `npm run build` in `web/` | Pass (bundle-size advisory only; route splitting is in packet 011) |

FoundationsTests cover: first sign-in creates a Standard User and logs "Provisioned" (AC-AUTH-01); 401
without sign-in except `/health` (AC-AUTH-04); group roles follow `X-Dev-Roles` while a Manual role
survives (AC-AUTH-03 capability, US3 scenario 2); admin routes refused to a PM; threshold change logged
with old/new values and actor type Admin (US2 scenario 3); invalid time zone refused; deactivation
reports usage and keeps the entry (FR-015, E-18); stale `If-Match` returns 409 naming who changed it
(G-07); last-admin guard and self-supervisor refusal (FR-027, FR-016); inactive users refused and
hidden from pickers (FR-005, G-11); UPDATE and DELETE on `activity_log` rejected by the database
trigger (AC-AUD-04).

## Manual checks (browser pane, dev sign-in)

| Check | Result |
|---|---|
| Dev picker lists seeded people with roles; production path uses MSAL redirect with PKCE (code review) | Pass |
| Admin sees My Work and Admin in the rail; admin sub-navigation, settings grouped with defaults, users with role sources | Pass |
| Phone width shows the bottom bar with My Work, Search, Notifications | Pass |
| `/` focuses the search box | Pass (code path; verified by keyboard in packet 011 audit) |

## Not run / deferred

- Final audit (2026-09-25): `FoundationsTests.Outside_development_only_Entra_tokens_sign_in` runs the app as Production
  with `Auth:Mode=Development` set anyway: the development header and a malformed bearer token are both refused (401)
  and `/api/v1/config` reports Entra (T-02).
- Final audit: an unknown `/api/…` path answered 200 with the application page; it now answers a 404 problem
  (`FoundationsTests.An_unknown_API_path_is_a_404_problem_not_the_application_page`).
- Real Entra ID sign-in and Graph directory sync: no tenant or app registration in this environment.
  The code paths are configuration-driven (`Auth:Entra:*`, `Graph:DirectorySync`); verify in `dev`.
- Coverage gate (`tools/coverage_gate.py`) runs in CI; measured after packet 005 adds the rules engine.
- WCAG automated scan and keyboard walk-through: packet 011.
