# Implementation Plan: Platform Foundations

**Branch**: `001-platform-foundations` (no Git branch; Spec Kit does not switch branches here) | **Date**: 2026-09-24 | **Spec**: [spec.md](spec.md)

## Summary

Stand up the modular monolith and everything later packets rely on: corporate sign-in with
just-in-time provisioning and system roles, reference data and organisation settings, the
immutable activity log, the application frame, and the jobs, health check, security headers and
string externalisation. Stack and §34 decisions are recorded in
[docs/decisions.md](../../docs/decisions.md).

## Technical Context

**Language/Version**: C# 14 on .NET 10 LTS; TypeScript 6 with React 19

**Primary Dependencies**: ASP.NET Core minimal APIs, EF Core 10 + Npgsql, JwtBearer (Entra), Azure.Identity (managed identity for Graph); Vite 8, Tailwind 4, shadcn/ui (Radix), TanStack Query, React Router, MSAL Browser

**Storage**: PostgreSQL 17 (local: `docker compose up -d db` on port 55432)

**Testing**: xUnit + `WebApplicationFactory` against PostgreSQL; coverlet for coverage; TypeScript build as the SPA type check

**Target Platform**: Azure App Service (Linux) + Azure Database for PostgreSQL Flexible Server

**Project Type**: Web application (SPA + API)

**Performance Goals**: §22 (screens usable ≤ 2 s p95, API reads ≤ 500 ms p95)

**Constraints**: WCAG 2.1 AA, strings externalised, no secrets in code or config, Entra-only sign-in

**Scale/Scope**: 300–500 users (§22)

## Constitution Check

| Principle | Check | Result |
|---|---|---|
| I Coordination first | Only identity, reference data, settings, audit, frame | Pass |
| II Deterministic, no AI | No AI; thresholds live in `org_setting` with §10.4 defaults in `Hub.Domain/Settings.cs` | Pass |
| III One owner | No owned items in this packet | n/a |
| IV Spec is source | Canonical role names verbatim (`Hub.Domain/Vocabulary.cs`) | Pass |
| V Traceable | Activity log written inside `SaveChanges`; DB trigger blocks UPDATE/DELETE; sign-ins, exports, admin changes logged | Pass |
| VI Secure by default | Entra bearer tokens only in production; deny-by-default fallback policy; server-side role checks; security headers; secrets via Key Vault references | Pass |
| VII Simple | One API, one database, hosted services, advisory locks; no broker | Pass |

Re-checked after design: no violations; Complexity Tracking empty.

## Decisions assumed (§34)

Q1, Q2, Q3, Q6, Q7, Q15 and Q17 as recorded in [docs/decisions.md](../../docs/decisions.md).
The last-administrator guard (FR-027) is the spec's stated default.

## Project Structure

```text
Hub.slnx, dotnet-tools.json, docker-compose.yml, .gitignore
docs/decisions.md
src/Hub.Domain/         Vocabulary.cs, Settings.cs
src/Hub.Api/            Program.cs, Text.cs, appsettings*.json
  Data/                 Entities.cs, HubDb.cs, Seed.cs, Migrations/
  Infrastructure/       Auth.cs, Access.cs, Audit.cs, Errors.cs, Jobs.cs, Directory.cs, Http.cs
  Features/             Me.cs, Admin.cs, Activity.cs
tests/Hub.Tests/        Api/Factory.cs, Api/FoundationsTests.cs
web/                    src/main.tsx, src/app/*, src/lib/*, src/i18n/en.ts, src/pages/admin/*, src/components/*
infra/main.bicep, .github/workflows/ci.yml
```

**Structure Decision**: web application with a pure domain assembly (see docs/decisions.md).

## Design notes

- `app_user` keyed by Entra object ID; email updated from the token (edge case: email changes).
- `user_system_role` unique on (user, role, source) so a Manual grant survives the group being
  removed (US3 scenario 2). This refines §24's unique(user, role).
- Settings: one `org_setting` row per key with a JSON value; defaults applied when missing.
- Notification defaults are stored as organisation settings and copied into a user's
  `notification_preference` rows the first time they are needed, so later default changes never
  overwrite existing choices (US2 scenario 5).
- Directory sync (`DirectorySyncJob`) runs daily when Graph is configured; otherwise Admins
  deactivate manually.
- Idle sign-out: the SPA tracks activity and signs out after `idle_timeout_hours` (default 8).
- Frame: nav items computed from system roles and first-release routes; items whose packet is not
  built yet are hidden rather than dead links (FR-VIS-01).

## Checks

- `dotnet build`, `dotnet test` (FoundationsTests: AC-AUTH-01, AC-AUTH-03, AC-AUTH-04, admin-only,
  last admin, settings change logged, deactivation count, activity log immutable at the database).
- `npm run build` in `web/` (type check and bundle).
- Browser walk-through: sign in as a Standard User and an Admin; compare navigation; `/` focuses search.

## Complexity Tracking

None.
