# Architecture decisions

Decisions recorded while planning packet 001 (the first plan that needs them). Each open question
from §34 of the product specification lists the choice made and whether it is the specification's
default. Change a decision by editing this file, the affected packet plans, and, when behaviour
changes, `spec-parts/`.

| # | Decision | Choice | Source |
|---|---|---|---|
| Q1 | Backend stack | ASP.NET Core on .NET 10 (current LTS, supported to November 2028), EF Core 10 with Npgsql, PostgreSQL 17 | Default (§23.1; .NET 8 support ends November 2026) |
| Q2 | System roles | Entra app roles `Hub.Admin`, `Hub.Executive`, `Hub.Supervisor`, `Hub.ProjectManager`, `Hub.ReadOnly` assigned to security groups; refreshed on every sign-in as source `Group`; Admins add roles in the Hub as source `Manual` | Default |
| Q3 | Graph permissions | Adapters for `User.Read.All` directory sync and `Mail.Send` from one shared mailbox. Without them: Admin deactivation, and an SMTP relay (Azure Communication Services or corporate relay) for mail | Default |
| Q4 | Visibility | Open by default. `visibility` stored and enforced; the Restricted control shows only when the organisation setting `restricted_projects_enabled` is on | Default |
| Q5 | Project number | Manual entry, unique ignoring case and surrounding spaces, format regex in settings (default allows letters, digits and hyphens, 1–32 characters) | Default |
| Q6 | Time zone and dates | One organisation time zone, default `America/Halifax` (the pilot clock), editable in settings; ISO dates (`2026-09-15`) | Default, zone assumed |
| Q7 | Language | English at launch; all SPA text in `web/src/i18n/en.ts`, server text in `src/Hub.Api/Text.cs` | Default |
| Q8 | Thin Decision Register | Yes | Default |
| Q9 | Availability and backups | 99.5 % business hours, 14-day point-in-time restore, no zone-redundant HA (`infra/main.bicep`) | Default |
| Q10 | Browsers | Current and previous Edge and Chrome, current Safari on iPadOS, current Firefox | Default |
| Q11 | Reason for due-date change by non-PM/DL | Required | Default |
| Q12 | `allow_self_review` | false | Default |
| Q13 | Who creates projects | Project Manager system role and Admins | Default |
| Q14 | Retention | Indefinite | Default |
| Q15 | CI/CD | GitHub Actions: the repository's remote is on GitHub | Default ("whichever the organisation uses") |
| Q16 | Pilot | 3 PMs, 6 projects, 8 weeks | Default |
| Q17 | Idle sign-out | 8 hours | Default |
| Q18 | Capacity | 40 hours per week; under-assignment shown with its caveat | Default |
| Q19, Q20, Q21 | Staff scope, follow level, Time route | As decided in the specification | Decided |

## Structure

- `src/Hub.Domain`: pure code with no package references: canonical vocabulary, organisation
  settings and defaults, status machines, the permission matrix, the rules engine (indicators,
  milestone status, deliverable progress and risk, health, attention), workload, coordination
  windows and working-day calendars. Keeping it in its own assembly makes "rules are pure
  functions" structural rather than a convention (constitution II).
- `src/Hub.Api`: the single deployable. Minimal-API endpoint files per module under `Features/`,
  EF Core entities and migrations under `Data/`, cross-cutting code under `Infrastructure/`
  (authentication, authorisation, activity log, outbox and evaluation worker, jobs, email,
  directory, exports). It also serves the built SPA from `wwwroot/`.
- `tests/Hub.Tests`: xUnit. `Domain/` holds the worked examples, acceptance criteria and the
  permission matrix as pure tests; `Api/` holds integration tests against PostgreSQL through
  `WebApplicationFactory`.
- `web/`: React 19, TypeScript, Vite, Tailwind CSS 4 and shadcn/ui (Radix) components,
  TanStack Query, React Router, dnd-kit for board ordering, MSAL for Entra sign-in.
- `infra/`: Bicep for App Service, PostgreSQL Flexible Server, Key Vault and Application Insights.

## Cross-cutting choices

- **Evaluation.** Every write that can change derived state inserts an `outbox_event` in the same
  transaction and signals an in-process worker, which re-evaluates the whole project a moment
  later, writes the state tables and diffs them to raise transition notifications (§23.5). The
  request never waits for evaluation. A 15-minute job retries unprocessed events; a nightly job
  handles the date rollover, snapshots and expiries.
- **Activity log.** Written by `SaveChanges` from EF change tracking with an explicit field
  allow-list per entity, so a change cannot be saved without its entry. A database trigger rejects
  `UPDATE` and `DELETE` on `activity_log`.
- **Concurrency.** Mutable rows carry an integer `row_version`, compared with `If-Match` (or
  `rowVersion` in the body) before the change and used as the EF concurrency token.
- **Status codes as names.** Statuses are stored as their canonical display names
  (`"Ready for Review"`), so code, database, API and tests use one vocabulary.
- **Development sign-in.** Only when `ASPNETCORE_ENVIRONMENT` is `Development` or `Testing` and
  `Auth:Mode` is `Development`, the API accepts an `X-Dev-User` header naming a seeded user and the
  SPA shows a user picker. Production always validates Entra bearer tokens; the development scheme
  is not registered there.
- **No new infrastructure.** No message broker, cache server, search service or job scheduler
  library: PostgreSQL, hosted services and advisory locks cover these at the stated scale
  (constitution VII).
