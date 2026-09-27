# Tuesday — Engineering Project Coordination Hub

An internal web application for coordinating multidisciplinary engineering projects: projects and teams, milestones,
deliverables, tasks with review, dependencies, a rules engine for indicators, health and attention items, Weekly
Coordination, decisions, risks and issues, meeting actions, templates, a workspace across projects (Home, Boards, Tasks,
Timeline, Files, Team), a team calendar, task hours, reports, notifications and an append-only activity history.

The product specification is [`Engineering-Project-Coordination-Hub-Specification.md`](Engineering-Project-Coordination-Hub-Specification.md)
(built from `spec-parts/`). It is written as 33 Spec Kit packets in [`specs/`](specs/README.md), each with its plan,
tasks and verification. What is built and what still needs a real environment to prove is in
[`docs/IMPLEMENTATION-STATUS.md`](docs/IMPLEMENTATION-STATUS.md).

## Approved coordination expansion

The nine additions approved on 2026-09-26 are captured in packets **025–033**: handoffs, multidisciplinary reviews, revision/change impact, submission readiness, dated resource allocations, discipline coordination, design basis/assumptions, ready-to-start planning/weekly commitments, and location-linked issues. Packets **025–027 now have foundation implementations** for handoffs, multidisciplinary reviews and revision/change impact. CI passed 373 tests, coverage gates and the mocked-API browser workflows/accessibility checks. Full acceptance, deployment and implementation of **028–033 remain pending**. See the [handoff](specs/025-discipline-handoffs/verification.md), [review](specs/026-multidisciplinary-reviews/verification.md) and [change-impact](specs/027-revision-change-impact/verification.md) verification records. The earlier 24-packet runtime and its verification limitations remain documented below.

Start with [DESIGN.md](DESIGN.md), the [packet index](specs/README.md), the [research rationale](docs/research/multidisciplinary-coordination.md) and the [validation record](docs/coordination-spec-validation.md). “Tuesday” is the product name in these planning documents; application branding still displays Coordination Hub until a separately implemented change.

## Repository

| Path | What |
|---|---|
| `src/Hub.Domain` | Pure rules: permissions, workflows, the evaluation engine, scoring and calendars (no I/O; unit-tested to ≥ 95 % branches) |
| `src/Hub.Api` | ASP.NET Core minimal API (.NET 10), EF Core + PostgreSQL, background jobs, and the built SPA under `wwwroot/` |
| `src/Hub.Api/Data/Migrations` | EF Core migrations, applied when the app starts |
| `web` | React 19 + TypeScript + Vite single-page application (Tailwind, shadcn/Radix, TanStack Query) |
| `tests/Hub.Tests` | xUnit domain tests and API integration tests against a real PostgreSQL |
| `infra` | Bicep for one Azure environment; `infra/env/<env>.bicepparam` per environment |
| `tools` | Specification build and traceability checks, coverage gate, accessibility audit, scale test data |
| `docs` | User and administrator guides, API conventions, runbooks, security, pilot plan |

## Local development

Prerequisites: .NET 10 SDK, Node 24, Docker, Python 3 (for the checks), and pandoc only to rebuild the specification.

```bash
docker compose up -d db
```

```bash
dotnet run --project src/Hub.Api --launch-profile http
```

```bash
npm --prefix web ci && npm --prefix web run dev
```

The API listens on http://localhost:5080 and the SPA on http://localhost:5173 (it proxies `/api`). The development
database is PostgreSQL 17 on `127.0.0.1:55432` with trust authentication and no password.

In Development, `Auth:Mode` is `Development`: there is no Entra sign-in; the SPA offers a list of seeded people and sends
`X-Dev-User: <email>`. This mode is refused in any other environment. Seeded people include jordan (Admin), lena
(Executive), priya and marc (PMs), sam (Supervisor), alex, jill, diane, omar and rita (Read Only), all `@hub.test`.

## Checks

```bash
dotnet test tests/Hub.Tests --collect:"XPlat Code Coverage" --results-directory coverage
```

```bash
python3 tools/coverage_gate.py coverage
```

```bash
npm --prefix web run build && npm --prefix web run lint
```

```bash
python3 tools/trace_spec.py --check
```

The same run in CI on every push and pull request (`.github/workflows/ci.yml`), with the vulnerable-package and
`npm audit` checks. The accessibility audit (`tools/a11y/audit.js`) runs axe-core over every screen in a signed-in
browser session; see the comment at the top of the file.

## Build for deployment

```bash
npm --prefix web ci && npm --prefix web run build
```

```bash
dotnet publish src/Hub.Api -c Release -o out
```

The SPA build writes into `src/Hub.Api/wwwroot`, so the published API serves the application, its API and its background
jobs from one App Service. Zip `out/` and deploy it; the app migrates the schema and seeds reference data on start. The
environment runbook, [`docs/runbooks/environments.md`](docs/runbooks/environments.md), gives the full order; restores are
in [`restore.md`](docs/runbooks/restore.md) and jobs and alerts in [`jobs.md`](docs/runbooks/jobs.md).

## Microsoft Entra ID

Each environment has two app registrations in the organisation's tenant:

1. **Hub API**: expose an API (`api://<api-app-id>`) with one delegated scope, `access_as_user`; set the manifest's
   `requestedAccessTokenVersion` to 2 (the API validates v2.0 tokens); define the app roles `Hub.Admin`, `Hub.Executive`,
   `Hub.Supervisor`, `Hub.ProjectManager` and `Hub.ReadOnly` (assign them to groups or people; they are synchronised at
   each sign-in).
2. **Hub SPA**: a single-page application platform with the redirect URI `https://<app-host>/` (and
   `http://localhost:5173/` only in development), API permission to the Hub API's `access_as_user`, admin-consented.

`Auth:Entra:TenantId`, `Auth:Entra:Audience` (the Hub API's client ID, which v2.0 tokens carry as their audience), `Auth:Entra:SpaClientId` and
`Auth:Entra:ApiScope` (`api://<api-app-id>/access_as_user`) come from these registrations; they are public values set in
`infra/env/<env>.bicepparam`. There are no Hub passwords; the database is reached with the app's managed identity.

## Configuration

Settings are app settings (environment variables use `__` for `:`). Nothing secret is kept in configuration files;
secrets live in Key Vault.

| Key | Default | Purpose |
|---|---|---|
| `ConnectionStrings:Hub` | development database | PostgreSQL connection; in Azure without a password, with `Db:UseManagedIdentity=true` |
| `Db:UseManagedIdentity` | `false` | Use the managed identity's Entra token as the database password |
| `Db:Migrate` | `true` | Apply migrations at start |
| `Auth:Mode` | `Entra` | `Development` (header sign-in) is accepted only in Development and Testing |
| `Auth:Entra:TenantId`, `:Audience`, `:SpaClientId`, `:ApiScope` | empty | Entra sign-in (above) |
| `Graph:DirectorySync` | `false` | Nightly leaver, title, office and manager sync from Entra ID |
| `Graph:Mail` | `false` | Send mail with Microsoft Graph from `Graph:Mailbox` |
| `Graph:Mailbox` | empty | The service mailbox Graph sends from |
| `Graph:ManagedIdentityClientId` | empty | A user-assigned identity for Graph, when one is used |
| `Email:Mode` | `Log` | `Smtp` to send through a relay (`Email:Smtp:Host`, `:Port`, `:User`, `:Password` from Key Vault); `Log` records subjects only |
| `Email:From` | empty | Sender address for SMTP |
| `Email:BaseUrl` | empty | The application's public address, used for links in emails |
| `Jobs:Enabled` | `true` | Run the background jobs in this instance |
| `Evaluation:Worker`, `Evaluation:DebounceMs` | `true`, `400` | The evaluation worker and its batching delay |
| `RateLimit:PerMinute` | `600` | Requests per person per minute |
| `Csp:ConnectSrc` | empty | Extra `connect-src` origins for the content security policy (Application Insights) |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | unset | Send requests, dependencies and logs to Application Insights |
| `Seed:DevUsers` | `true` in Development | Seed the development people |

Organisation thresholds and defaults (due-soon days, stale days, health percentages, working days, digest time and so
on) are not configuration: Admins change them in the application under **Admin → Settings**, and every change is logged.

## Rules for changes

Work follows the Spec Kit workflow in [`docs/SPEC-KIT-WORKFLOW.md`](docs/SPEC-KIT-WORKFLOW.md) and the
[constitution](.specify/memory/constitution.md): no AI or machine-learning features; one accountable owner per item;
server-side, deny-by-default permissions; an append-only activity log written in the same transaction; soft deletion of
work items; optimistic concurrency on every changeable entity; thresholds only in organisation settings; WCAG 2.1 AA.
