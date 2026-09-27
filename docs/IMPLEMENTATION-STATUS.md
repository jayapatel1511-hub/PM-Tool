# Implementation status

As of 2026-09-25. Everything below was checked on a development Mac against PostgreSQL 17 in Docker; nothing has been
deployed, pushed or committed. "Verified" means an automated test and, for screens, a check in a real browser; the
last section lists what cannot be proven without the organisation's tenant, Azure environment, people or time.

## Overall

All 24 Spec Kit packets are built: the 18 first-release packets (including the six confirmed workspace views in 022
and task-hour entry under Time in 024) and the 6 Phase 2 packets. Each packet folder in [`specs/`](../specs/README.md)
has its `spec.md`, `plan.md` (with the constitution check), `tasks.md` (every task ticked) and `verification.md`
(automated and browser checks, defects found and fixed, decisions). No Phase 3 capability was added.

| Check | Result |
|---|---|
| `dotnet test tests/Hub.Tests` (xUnit domain tests and API integration tests on PostgreSQL) | 339 of 339 pass |
| `python3 tools/coverage_gate.py` | Rules engine 97.5 % branches (gate 95 %); application services 90.9 % lines (gate 70 %) |
| `dotnet build -c Release` with every .NET security analyser rule as an error | Pass |
| `dotnet list package --vulnerable --include-transitive`; `npm audit --omit=dev` | None found |
| `npm run build` and `npm run lint` (TypeScript, oxlint with jsx-a11y) | Pass |
| `python3 tools/trace_spec.py --check` | 496 of 496 specification IDs and every section traced to packets |
| axe-core, WCAG 2.1 A and AA, every screen (`tools/a11y/audit.js`) | 46 routes as Admin, and the main routes as PM, team member and read-only: no violations |

## Against the goal

| Deliverable | Where | Status |
|---|---|---|
| Frontend | `web/` React SPA: My Work, Home, Boards, Tasks, Timeline, Calendar, Files, Team, Time, Reports, Portfolio, Workload, My Staff, Templates, project tabs (Dashboard, Weekly Coordination, Tasks, Board, Deliverables, Milestones, Timeline, Decisions, Risks, Issues, Meetings & Actions, Files, Calendar, Team, Activity, Settings), Administration | Verified |
| API | `src/Hub.Api`, 231 operations on 182 paths in `/api/v1/openapi.json`; conventions in [`api-reference.md`](api-reference.md) | Verified |
| Database and migrations | PostgreSQL schema `hub`; eight EF Core migrations applied at start (InitialSchema, ProjectLinksAsDocumentLinks, SavedViewConcurrency, SnapshotCounts, SearchTrigramIndexes, SearchTextIndexes, DeliverableBlockingCount, IdempotencyRecords) | Verified (each test run migrates a fresh database) |
| Entra sign-in | MSAL redirect sign-in in the SPA, JWT bearer validation of v2.0 tokens in the API, just-in-time provisioning, app-role sync at each sign-in | Built; the development sign-in and its refusal outside Development are tested; **real tenant sign-in unverified** |
| Role and project permissions | `Hub.Domain/Permissions.cs`, checked server-side on every request | Verified (permission matrix and sweep tests, per-endpoint refusals, restricted-project tests) |
| Engineering workflows | Projects, milestones and cascade, deliverables with review and issue, tasks with review, dependencies with lag, decisions, risks, issues, meeting actions, templates | Verified |
| Notifications | In-app notifications, email queue, daily digest with per-section choices, weekly PM summary, following feed, live counts | Verified locally with the log mail sender; **real mail delivery unverified** |
| Reports | 20 reports with CSV/XLSX export, list exports on every register | Verified |
| Audit history | Append-only activity log in the same transaction as each change (database triggers refuse update, delete and truncate), project and organisation views, export | Verified |
| Accessibility | WCAG 2.1 AA: axe on every screen, jsx-a11y lint in CI, keyboard operation of boards, calendars and dialogs | Verified automatically; **assistive-technology testing unverified** |
| Deployment-ready documentation | [`README.md`](../README.md) (setup, configuration, Entra registrations, build), [`runbooks/environments.md`](runbooks/environments.md), [`runbooks/jobs.md`](runbooks/jobs.md), [`runbooks/restore.md`](runbooks/restore.md), [`admin-guide.md`](admin-guide.md), [`user-guide.md`](user-guide.md), [`security/threat-model.md`](security/threat-model.md), [`pilot/pilot-plan.md`](pilot/pilot-plan.md), `infra/main.bicep` | Written; **the Bicep deployment itself is unverified** |

## Packets

| Packet | Delivers | Automated tests | Browser checks |
|---|---|---|---|
| 001 Platform foundations | Sign-in, users and roles, reference data, settings, application frame, activity log | FoundationsTests (12), PermissionMatrixTests, DomainHelpersTests | Yes |
| 002 Projects and teams | Projects and lifecycle, roles, teams and leads, keys, project list, resume date review (E-05) | ProjectsTests (12) | Yes |
| 003 Milestones and deliverables | Milestones, date changes, deliverables with review and issue | MilestonesDeliverablesTests (5), WorkflowTests | Yes |
| 004 Tasks and review | Tasks, review workflow, collaborators, blocks, bulk actions, list, panel, board | TasksTests (13), WorkflowTests | Yes |
| 005 Dependencies and rules engine | Dependencies; indicators, milestone status, progress, health with override, attention | EvaluationTests (9), EvaluatorTests (44), EvaluatorEdgeTests (21) | Yes |
| 006 Collaboration and notifications | Comments, document links, notifications, email, digest, following | CollaborationTests (8) | Yes |
| 007 Coordination surfaces | Dashboard, Weekly Coordination with meeting mode, My Work, My Staff | SurfacesTests (6) | Yes |
| 008 Decision register | Decisions, external owners, decision blocks, record, defer, cancel, reopen | DecisionsTests (4) | Yes |
| 009 Search, filters, reports | Search and key lookup, filter bar, reports and exports, activity history | SearchReportsTests (5), ActivityTests (4) | Yes |
| 010 Timeline and extras | Baseline timeline, cascade, copy structure, reassign work | ExtrasTests (4) | Yes |
| 016 Portfolio dashboard | Portfolio, both health values, trends, portfolio reports | PortfolioTests (3) | Yes |
| 017 Resource workload view | 8-week grid, flags, rebalancing, workload reports | WorkloadApiTests (3), WorkloadTests (6) | Yes |
| 018 Timeline scheduling | Tasks and arrows on the timeline, drag with confirmation, baselines | TimelineTests (3) | Yes |
| 019 Saved views and board ordering | Personal and project views, manual card order | ViewsTests (4) | Yes |
| 023 Team calendar | Week, month and agenda, deadlines, events, visibility | CalendarTests (3) | Yes |
| 024 Task time entries | Time route, task-hour entry, corrections, totals and export | TimeTests (5) | Yes |
| 022 Six-view workspace | Home, Boards, Tasks, Timeline, Files, Team across chosen projects | WorkspaceTests (8) | Yes |
| 011 Hardening and pilot | Accessibility, scale, reliability, security readiness, support material, pilot measures | OperationsTests (3), ExportSafetyTests (7), IdempotencyTests (1), scale runs | Yes |
| 012 Project templates | Templates, versioning, instantiation wizard, discipline packs | TemplatesTests (4), TemplatePlannerTests (4) | Yes |
| 013 Register enhancements | Client export of open decisions, bulk linking, decision log, issue history | RegisterEnhancementsTests (4) | Yes |
| 014 Risk and issue registers | Risks on a 3 × 3 grid, issues, A-07, Red health, raise from here, report | RegistersApiTests (4), RegistersTests | Yes |
| 015 Meeting actions | Meetings, actions for people, disciplines and external parties, capture in meeting mode, convert to task | MeetingActionsTests (4), RegistersTests | Yes |
| 020 Notification and search enhancements | Weekly PM summary, digest sections, live counts, search in descriptions and comments | NotificationSearchTests (6) | Yes |
| 021 Dependency lag and working days | Deliverable links, lag, working days, holiday calendars | DependencyLagApiTests (2), DependencyLagTests (6) | Yes |

## Found and fixed in the final audit

- E-05 (recommended): the "Date review" after an On Hold project resumes had been deferred from packet 002 and never
  built. Built and tested (`ProjectsTests`, packet 002 verification).
- §25.10: 429 responses now send `Retry-After`, and creates accept `Idempotency-Key` (packet 011 verification).
- §25.6: an unknown `/api` path answered 200 with the application page; it now answers a 404 problem (packet 001).
- The global calendar's empty week grid could not be scrolled from the keyboard; read-only people saw "New task" on My
  Work and "Add time" on Time (the server already refused both).
- E-15: tasks without a deliverable are grouped under "Other tasks", as specified; E-06 guidance is in the user guide.

## Unverified: needs a real environment, people or time

| Item | Why it is unverified | What would verify it |
|---|---|---|
| Entra ID sign-in end to end | No tenant or app registrations here; MSAL redirect, tenant signing keys and real app-role claims were never exercised | Register the two apps per the README in the dev tenant; sign in as each role |
| Microsoft Graph directory sync and Graph or SMTP mail | Only the log mail sender ran; the sync job ran against no directory | Enable `Graph:DirectorySync` and a mail route in `dev`; disable a test account; watch Admin → Operations |
| Azure deployment | `infra/main.bicep` has not been deployed (not requested); managed-identity database access, Key Vault, Application Insights and alert rules are untried | Follow `docs/runbooks/environments.md` for `dev` |
| Performance in Azure | Measured locally only: nightly evaluation of 500 projects and 123,540 tasks in 74 s, search 0.03–0.6 s over 247,080 comments, API load results in packet 011 | Repeat `tools/scale` against the Azure database |
| Backup restore | Procedure and drill record only | The first drill before go-live (`restore.md`) |
| Screen readers and other browsers | Checked with axe and keyboard in Chromium only | NVDA and VoiceOver passes; Firefox, Safari on iPadOS |
| Email rendering | Plain-text emails asserted in tests, not seen in Outlook | Receive a digest and a weekly summary in `dev` |
| The eight-week pilot and its measures | Needs real teams | `docs/pilot/pilot-plan.md`; Reports → Pilot measures |
| Threat model walkthrough | Not held; T-09 (migration role), T-12 (mail secret rotation), T-16 (Graph mailbox policy), T-18 (penetration test) open | The walkthrough record in `threat-model.md` |
| Phase 2 defaults | 013, 020 and 021 were specified as defaults "to confirm with /speckit-clarify"; built as written | Jay's confirmation; details in each packet's verification Decisions |

## Not built, by scope

- Phase 3 (§29): Teams and SharePoint integration, ERP and financial display, utilisation planning, external access
  for clients, advanced portfolio reporting and administration, ICS calendar feeds, localisation.
- Deliberately never built (§30): AI or machine-learning features of any kind, financial or timesheet systems.
- E-12: a "Compare with template" view is deliberately not built.
- Continuous deployment: CI runs on every push; deployment is the documented manual `az` sequence until the
  organisation chooses its release tooling (§23.7 TBD).
