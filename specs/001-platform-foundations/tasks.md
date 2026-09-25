# Tasks: Platform Foundations

**Input**: [plan.md](plan.md), [spec.md](spec.md)

## Phase 1: Setup

- [x] T001 Solution, projects, tool manifest, docker-compose, .gitignore (plan structure)
- [x] T002 [P] Web app scaffold: Vite, Tailwind, shadcn/ui, path alias, API proxy in `web/vite.config.ts`

## Phase 2: Foundational

- [x] T003 Canonical vocabulary and settings defaults in `src/Hub.Domain/Vocabulary.cs`, `Settings.cs` (§8.2, §10.4, FR-014)
- [x] T004 Entities and `HubDb` with snake_case mapping, row versions, soft-delete filters in `src/Hub.Api/Data/` (§24.1)
- [x] T005 Activity log written from `SaveChanges` with per-entity allow-lists, correlation IDs, actor type and source; append-only trigger in migration (FR-018–FR-020, §20)
- [x] T006 Problem-details errors, `If-Match` concurrency helper, security headers, rate limit in `Infrastructure/Errors.cs`, `Http.cs` (§21, §25.6, §25.8)
- [x] T007 Authentication: Entra JWT bearer; Development/Testing header scheme; JIT provisioning with group roles (`Infrastructure/Auth.cs`) (FR-001–FR-004, FR-007)
- [x] T008 Health endpoint `/health` (database, outbox lag) (FR-007, §22)
- [x] T009 Job host with advisory-lock single instance and `job_run` records (`Infrastructure/Jobs.cs`) (§23.5)

## Phase 3: US1 Sign in (P1)

- [x] T010 `GET /api/v1/me` returns user, roles and preferences; sign-in logged once per session start (AC-AUTH-01, §20.1)
- [x] T011 SPA auth: MSAL redirect with PKCE and silent renewal, dev user picker, 8-hour idle sign-out (FR-006)
- [x] T012 Tests: first sign-in creates user with Standard User; no token → 401 except `/health` (AC-AUTH-01, AC-AUTH-04)

## Phase 4: US2 Reference data and settings (P1)

- [x] T013 Admin CRUD for disciplines, clients (with Internal / TBD), offices, deliverable types, phases (ordered defaults), project types; deactivate with usage count (FR-008–FR-015, E-18)
- [x] T014 Settings endpoint for thresholds, rule flags, notification defaults, project-number format, self-review, time zone, date format, digest time (FR-014)
- [x] T015 Admin screens `web/src/pages/admin/*` (FR-008–FR-015)
- [x] T016 Tests: non-admin refused; threshold change logged with old/new (US2 scenarios 3, 4)

## Phase 5: US3/US4 Roles, supervisors, leavers (P2)

- [x] T017 Users admin: roles with source, supervisor (not self), activate/deactivate, "No supervisor" filter, last-admin guard (FR-004, FR-016, FR-017, FR-027)
- [x] T018 Directory sync job via Graph when configured (FR-005)
- [x] T019 Tests: project-create permission from role (AC-AUTH-03 at the API in packet 002), manual role survives group change, last admin refused

## Phase 6: US5 Activity log (P1)

- [x] T020 Organisation activity history endpoint and Admin screen with filters (FR-021)
- [x] T021 Test: UPDATE/DELETE on `activity_log` rejected by the database (AC-AUD-04)

## Phase 7: US6 Frame (P3)

- [x] T022 Shell: role-filtered rail, top bar (search, quick-create, bell, user menu), `/` shortcut, tablet icon rail, phone bottom bar, visible focus (FR-022–FR-025)
- [x] T023 Strings in `web/src/i18n/en.ts` via `t()` from the first component (FR-026)

## Phase 8: Polish

- [x] T024 CI workflow `.github/workflows/ci.yml` (build, test, web build) and `infra/main.bicep` skeleton (Q15, §23.7)
- [x] T025 Record results in `verification.md`
