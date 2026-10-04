# Implementation Plan: Weekly Planning Layer

**Packet**: 034 | **Feature directory**: `specs/034-weekly-planning-layer` | **Date**: 2026-10-03 | **Spec**: [spec.md](spec.md)
**Status**: Locally implemented and software/browser checks recorded in `verification.md`; remaining work is uncommitted. T023 is blocked at Validation step 10's manual screen-reader pass.
**Input**: Product specification §39, §8.11, §10.9, §13.21, §13.10; packet 029 as built (`specs/029-dated-capacity-allocations/verification.md`).

## Summary

Add one person-scoped table, `planning_entry`, and one feature module, `PlanningEndpoints`, beside the built packet 029 allocation feature. The planner reads three existing sources — weekly capacity with availability overrides, Confirmed resource allocations, and the Workload task forecast — and combines them with planning entries through pure functions in `Hub.Domain/Planning.cs`. Nothing is materialised and no job is added: every figure is computed per request from the same visibility predicate, so privacy and explanation come from one place. Packet 029's model and commands, the Workload grid and packet 032's readiness checks are not modified.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`), TypeScript with React 19 and Vite.
**Primary Dependencies**: ASP.NET Core minimal APIs, EF Core 10 with Npgsql 10, TanStack Query 5, react-router 8, Radix-based `web/src/components/ui` primitives, lucide-react icons, `@tanstack/react-virtual` (already installed; used only if the Executive view needs row virtualisation).
**Storage**: PostgreSQL, schema `hub`, snake_case names (`HubDb.OnModelCreating`).
**Testing**: xUnit against an ephemeral PostgreSQL (`tests/Hub.Tests`, collection `api`); Playwright mocked-API harness (`web/tests/*.cjs`); `tools/a11y/audit.js`; `tools/coverage_gate.py`; `tools/scale/` for performance.
**Target Platform**: The existing single API and SPA.
**Project Type**: Web application (modular monolith, one database).
**Performance Goals**: AC-PLN-17 — planner grid ≤ 1.5 s p95 for 12 people × 12 weeks with 500 entries and 50 confirmed allocations; My Week ≤ 500 ms p95.
**Constraints**: §37.6, §12.15/§13.11 and §38.2 behaviour unchanged (FR-PLN-01, AC-PLN-11); server-side privacy (FR-PLN-18); thresholds only from settings (constitution II).
**Scale/Scope**: 50-person pilot; Supervisors with up to about 15 direct reports; horizon 6–26 weeks.

## Constitution Check

*Gate before design and again after it.*

| Principle | Pre-design | Post-design |
|---|---|---|
| I. Coordination First | Answers "who has room and what is planned" for project and non-project work; §30 respected: time away is a non-sensitive availability override, not leave management; no levelling or auto-assignment | PASS — one record, no workflow engine, no leave data |
| II. Deterministic and Explainable | Rules PLN-01–PLN-19 are pure functions; thresholds are §10.9 settings; contribution list and indicator explanations are the "Why?" | PASS — `PlanningRules` has no I/O; no constant threshold; AC worked examples become tests |
| III. One Accountable Owner | Each entry has one owner, its creator; approved allocations keep their §37.6 ownership | PASS — `created_by` is required; owner never changes |
| IV. Specification Is the Source of Truth | §39, §8.11, §10.9, §13.21 and the §13.10 row amended in this change; canonical names verbatim and distinct from Workload's | PASS — build/trace results recorded in `verification.md` |
| V. Traceable by Construction | Same-transaction activity rows through `HubDb.SaveChanges`; soft delete; row versions | PASS — `AuditRules.Fields` entry, explicit action notes, no hard delete |
| VI. Secure by Default | Deny by default from §8.11; one visibility predicate for every read, aggregate, export, search, notification, digest and history; Admin draft access needs a reason and is logged | PASS — full matrix, all-surface privacy and queued-email revocation tests recorded in `verification.md` |
| VII. Simple Enough for a Small Team | One table, one feature file, one domain file; no broker, job, cache or materialised table | PASS — no Complexity Tracking entry |

Open product decisions this plan depends on: Q4 (restricted projects: PLN-15 uses the Workload partial rule), Q6 (organisation time zone for ISO weeks), Q7 (externalised strings), Q18 (capacity default stays 40 h, an integer setting), Q19 (direct reports only). The §39.12 defaults are used until Jay decides otherwise; T001 records his answers.

## Design

### Read model (computed per request)

`PlanningEndpoints.Grid(query, access, db, me, store, clock)` builds every planner figure:

1. **Settings and window.** `s = store.Get(db)`; `today = clock.Today(s)`; `currentWeek = Workload.WeekOf(today)`; `first = Workload.WeekOf(from ?? today)`; `weeks = first + 7·i` for `i < clamp(weeks ?? s.PlanningHorizonWeeks, 1, 26)`.
2. **People (PLN-14).** If `Permissions.ViewWorkload(actor)`: `WorkloadEndpoints.People(db, access, me)` plus the actor; otherwise the actor only. Active users only; then the `supervisorId`, `officeId` and `personId` filters. A `personId` outside the scope returns 404.
3. **Visible entries (PLN-04).** One query: not deleted; person in scope; overlapping the window; `visibility <> 'Draft' OR created_by = actor` (or every draft when the Admin data-correction mode is on); `project_id IS NULL OR project_id IN access.VisibleProjectIds()`. `includeMyDrafts=false` also removes the actor's own drafts.
4. **Approved allocations (PLN-07).** `db.Allocations` with `Status = Confirmed`, person in scope, project in `access.VisibleProjects()` with status Setup or Active (`WorkloadEndpoints.LiveProjects`), overlapping the window; their `AllocationDayOverrides`; `AllocationRules.Spread(from, through, plannedHours, personCalendar, dayOverrides)` summed by `(person, Workload.WeekOf(day), project)` exactly as `WorkloadEndpoints.Grid` does.
5. **Capacity and time away (PLN-05, PLN-06).** Person calendar as in Workload (`WorkCalendar(holidays for null or the person's office)` when `WorkingDaysEnabled`, otherwise `WorkCalendar.Weekdays`); `AvailabilityOverrides` for the people and window; per day `AllocationRules.DailyCapacity(day, weekly, calendar, override)` and `PlanningRules.TimeAway(normal, override, category)`.
6. **Task-estimate context (FR-PLN-14).** `WorkloadEndpoints.Tasks(db, access, peopleIds, null, null)` and `Workload.Spread` per task with `Calendars.For` project calendars, summed per week; unestimated open tasks due in each week counted for the cell and for PLN-11.
7. **Combine.** Per person-week `PlanningRules.Combine(capacity, entryWeeks, approvedByProject)`; `PlanningRules.OverPlanned`; `PlanningRules.UnderPlanned(...)` with `partial = !access.SeesAllRestricted` (PLN-15); per entry `PlanningRules.Stale` and warnings (`ProjectNotActive`, `OwnerCannotManage` from `Permissions.ActOnStaff(ownerActor, person.SupervisorId)`, `AboveCapacity`).
8. **Filters and sort (FR-PLN-24).** Entry filters mark `matchesFilter` and drop people without a match; they never change cell figures. Indicator filter on computed flags; discipline filter on visible entries' project disciplines or visible open tasks. Sort by remaining capacity in `week` (default current week), name, or Over-planned first.

`Cell(personId, week)` reuses the same steps for one person and one week and also returns per-entry `countedHours`/`coveredHours`, per-day capacity rows and indicator explanations. `MyWeek` is `Grid` with `personId = actor` and `weeks = 6`.

### Commands

All commands run in `Tx.Run(db, …)`: check the §8.11 permission with the pure functions, validate with `PlanningRules`, compare the row version with `Http.CheckVersion(db, http, entry, bodyVersion)` (If-Match or body), write, add an `db.Audit.Note(...)`, send notifications through the existing `Notifier`, and `SaveChangesAsync` once. POST creates rely on the existing `IdempotencyMiddleware` (`Idempotency-Key` header); planning routes carry no `Coordination.AtomicCommand` metadata because they are not project-scoped.

- **Create / quick add**: resolve person; `self = person == actor`; `Permissions.CreatePlanningEntry(actor, personId, person.SupervisorId)`; defaults from `PlanningRules.Defaults(self, possible)`; project link checks (PLN-18): the project is in `access.VisibleProjectIds()` for the actor and passes the same open-or-member-or-Admin/Executive test for the person (a query mirroring `Access.VisibleProjects` for that user), its status is Setup, Active or On Hold, and the discipline is active and belongs to it; `last_validated_at = now`.
- **Edit**: `Permissions.ManagePlanningEntry(actor, facts)` → `(Allow, NeedsReason)`; a reason is required when `NeedsReason`; moving persons needs `CreatePlanningEntry` for the new person and is refused for self entries (`planning.self_move`) and onto the owner's own row; `last_validated_at = now`.
- **Visibility**: `PlanningRules.StepVisibility(self, from, to)` (refused for self entries: `planning.self_visibility`); `last_validated_at = now`.
- **Still valid**: manage permission; `last_validated_at = now`; note action `Validated`.
- **Delete**: manage permission; `DeletedAt/DeletedBy` (soft); note reason for Admin corrections.
- **Time away** (PLN-19): mirrors `AllocationEndpoints.SetAvailability` without changing it — `SELECT … FROM hub.app_user WHERE id = {0} FOR UPDATE`; active person or 404; `Permissions.ActOnStaff(actor, person.SupervisorId)`; the target dates are the working days of the person calendar in `[from, through]` (Record) or the dates in the range holding an Unavailable or Reduced override (Clear); for each target date the expected version is the one the caller sent for it, or 0 when the caller sent none, and it must equal the current override version (0 when no override); any mismatch → 409 with `staleDates`; Record upserts `PersonAvailabilityOverride` (new rows `RowVersion = 1`; existing rows marked modified so the version advances), Clear removes the target rows; touch `PersonDateVersion` for each changed day exactly as `SetAvailability` does; one save. The caller passes the `{ workDate, rowVersion }` rows it read from `GET /users/{personId}/availability`, so it never needs the calendar. A parity test compares the result with the single-date route.

### Notifications

New event `NotificationEvents.PlanningEntryChanged` (App on, Email off, not `ProjectScoped`). `NotifyItem(ProjectId: null, ItemType: "PlanningEntry", ItemId: entry.Id, ItemKey: null, Link: "/my-work?myWeek={startWeek}")`. Recipient: the person, only when PLN-16 applies and the person passes PLN-04 for the entry; never the actor (the existing `Notifier` rule). Titles use `planning.changed` with the visible entry label; the body carries qualified confidence and visibility. Withdrawal uses only the generic `planning.withdrawn` message and `/my-work`, with no entry identifier or details. The existing five-minute collapse key (`event:item:actor`) applies.

### Audit and history

`AuditRules.Fields[typeof(PlanningEntry)] = ["PersonId", "HoursPerWeek", "StartWeek", "EndWeek", "Label", "SourceCategory", "ProjectId", "ProjectDisciplineId", "Confidence", "Visibility", "Notes", "LastValidatedAt", "DeletedAt"]`. `LastValidatedAt` is logged so the old and new validation time is retained; Still valid also writes an explicit `Validated` action. Explicit actions: `Moved` (person change), `VisibilityChanged`, `Validated`; Admin corrections add the reason and categories `admin`, `data-correction`. No `FieldCategory` entry is added, because `PersonId` and `Visibility` are shared names with existing audited records whose categories must not change. `PlanningEntry.AuditProjectId` is always null, so project activity, Following feeds, project digests and reports never read planning rows. `Admin.cs` org activity excludes `ItemType = "PlanningEntry"`. Admin draft access writes `db.LogEvent("PlanningDraftAccess", null, "Viewed", "access", reason: reason, changes: new { personIds, from, weeks })`, which does appear in org activity.

### Search, saved views, digest, export

- Search group `planning` in `SearchEndpoints.Groups`: `PlanningEndpoints.VisibleEntries(db, access, …)` filtered by `ILIKE` on label or notes; result `{ id, name: "<person> · <label>", status: <stored visibility or Self>, dueDate: endWeek, startWeek, personId }`. The central planning-label mapping qualifies visibility. Self results link to `/my-work?myWeek={startWeek}&planningEntry={id}`; manager results link to `/planner?personId={personId}&entry={id}`.
- Saved views: `ViewEndpoints.Lists["planner"] = ["view", "from", "weeks", "week", "supervisorId", "disciplineId", "officeId", "personId", "projectId", "source", "confidence", "visibility", "indicator", "q", "sort", "includeMyDrafts"]`.
- Digest section `planning` (added to `Digest.SectionCodes` after `allocations`): the recipient's own visible entries for which `PlanningRules.Stale` is true; row link `/planner?entry={id}` for manager entries and `/my-work?myWeek={startWeek}` for self entries.
- Exports through `ExportFile.Send` with the same visible rows; more than `Export.MaxRows` rows → 422 `export_too_large`.

## File boundaries

An existing file is extended, never replaced; unrelated edits by other work are preserved. Files not listed are out of bounds.

**Backend — new**

- `src/Hub.Domain/Planning.cs` — vocabulary constants (`PlanningVisibility`, `PlanningConfidence`, `PlanningSource`, `PlanningIndicator`, `PlanningWarning`) and `PlanningRules` pure functions.
- `src/Hub.Api/Data/PlanningEntities.cs` — `PlanningEntry`.
- Additive `PlanningNotificationPrivacy` migration (+ Designer/snapshot) adds an empty-default `EmailMessage.RequiredPlanningEntryIds` array; existing email rows keep their behavior.
- `src/Hub.Api/Data/Migrations/<timestamp>_PlanningEntries.cs` and `.Designer.cs` (generated), plus the generated update to `src/Hub.Api/Data/Migrations/HubDbModelSnapshot.cs`.
- `src/Hub.Api/Features/Planning.cs` — `PlanningEndpoints`: routes, read model, commands, time-away command, `VisibleEntries`, notification helper, digest and search helpers.

**Backend — additive edits**

- `src/Hub.Domain/Permissions.cs` — `CreatePlanningEntry`, `ManagePlanningEntry`, `SeePlanningEntry`, `RecordTimeAway` (§8.11).
- `src/Hub.Domain/Settings.cs` — six `planning_*` settings (properties, `Defs` in group `planning`, `From`, per-key ranges in `Validate`); `NotificationEvents.PlanningEntryChanged`.
- `src/Hub.Api/Data/HubDb.cs` — `DbSet<PlanningEntry> PlanningEntries`, model configuration (checks, indexes, foreign keys, query filter).
- `src/Hub.Api/Infrastructure/Audit.cs` — `AuditRules.Fields` entry only.
- `src/Hub.Api/Infrastructure/Digest.cs` — `planning` section code and rows, with scoped delivery metadata.
- `src/Hub.Api/Data/Entities.cs` — additive `EmailMessage.RequiredPlanningEntryIds` metadata for planning recipients.
- `src/Hub.Api/Infrastructure/Email.cs` — rechecks current PLN-04 scope for planning-only queue items immediately before delivery; generic withdrawal checks recipient activity. Existing email branches are unchanged.
- `src/Hub.Api/Features/Notifications.cs` — planning-only current-entry authorization in the shared notification query for list, pulse, count and read commands; other event predicates are unchanged.
- `src/Hub.Api/Infrastructure/Idempotency.cs` — route only `/api/v1/planning` POSTs to the packet-specific atomic, body-bound, permission-rechecked receipt in `Planning.cs`; existing POST paths retain their baseline behavior.
- `src/Hub.Api/Features/Search.cs` — `planning` group.
- `src/Hub.Api/Features/Views.cs` — `planner` list type.
- `src/Hub.Api/Features/Admin.cs` — org activity excludes `PlanningEntry` rows (one predicate).
- `src/Hub.Api/Features/Modules.cs` — `PlanningEndpoints.Map(api);`.
- `src/Hub.Api/Features/Workload.cs` — `People`, `Tasks` and `LiveProjects` change from `private` to `internal`; the nested `TaskRow` type is also `internal` solely so the packet-034 read model can consume the existing task projection; no behavior change.
- `src/Hub.Api/Text.cs` — server text (validation, notifications, digest, export headers, search label).

**Backend — must not change** (AC-PLN-11): `src/Hub.Api/Features/Allocations.cs`, `src/Hub.Domain/Allocations.cs`, `src/Hub.Domain/Workload.cs`, `src/Hub.Api/Features/Readiness.cs`, `src/Hub.Api/Data/AllocationEntities.cs`, existing migrations, `src/Hub.Api/Infrastructure/Coordination.cs`, `src/Hub.Api/Infrastructure/Notify.cs`.

**Tests**

- New `tests/Hub.Tests/Domain/PlanningTests.cs` — every `PlanningRules` function with the cases below; settings validation ranges.
- New `tests/Hub.Tests/Api/PlanningContractTests.cs` — exact read-model totals, filtered accounting, coverage, lifecycle, concurrent receipt, private/restricted scope and packet-029 time-away parity.
- New `tests/Hub.Tests/Api/PlanningSchemaTests.cs` — each database check, index and foreign key on a fresh PostgreSQL; migration up and down.
- New `tests/Hub.Tests/Api/PlanningApiTests.cs` — AC-PLN-01, AC-PLN-03 to AC-PLN-13, AC-PLN-15, AC-PLN-16, AC-PLN-19, AC-PLN-20; commands, versions, idempotency, exports, time-away parity, no-change regression.
- New `tests/Hub.Tests/Api/PlanningPrivacyTests.cs` — AC-PLN-02 sweep: grid, cell, list, detail, history, exports, search, notifications, digest, saved-view results and Admin org activity, for owner, person, other Supervisor, Executive, Project Manager, Admin with and without data-correction mode.
- Edit `tests/Hub.Tests/Domain/PermissionMatrixTests.cs` — one theory per §8.11 row.
- Edit `tools/scale/seed.sql` and `tools/scale/measure.py` — planning entries and the two AC-PLN-17 measurements.

**Frontend**

- New `web/src/pages/Planner.tsx` — page: header, filters, Grid/List, export, saved views (`ViewMenu`), phone notice (`app.phoneNotice`), data-correction mode for Admins.
- New `web/src/components/planner/PlannerGrid.tsx` — ARIA grid, sticky 240 px person column, ≥ 144 px week columns, cells, expanded sub-rows, inline quick add, roving focus.
- New `web/src/components/planner/ContributionList.tsx` — cell breakdown in the shared accessible `ui/dialog`, with capacity days, entry metadata, coverage, approved allocations and indicator explanations.
- New `web/src/components/planner/EntryPanel.tsx` — side panel (`ui/sheet`, 560 px): fields, visibility buttons with the current value disabled, Still valid, Copy, Delete (`ConfirmDialog`), person-scoped history with loading and failed-load feedback.
- New `web/src/components/planner/TimeAwayDialog.tsx` — range form (`ui/dialog`) reading `GET /users/{id}/availability` versions first.
- New `web/src/components/planner/MyWeekStrip.tsx` — six-week strip reusing `PlannerGrid` in single-row mode.
- New `web/src/components/planner/labels.ts` — the only place that turns stored confidence, visibility and approval status into display keys (AC-PLN-14), plus shared types.
- Edit `web/src/app/routes.tsx` (route `planner`), `web/src/app/nav.ts` (item `planner`, path `/planner`, `show: m => m.capabilities.workload`, added to `BUILT`), `web/src/pages/MyWork.tsx` (strip on the user's own My Work only), `web/src/components/hub/search.tsx` (`planning` group and `itemHref`), `web/src/i18n/en.ts` (`planner.*`, `setting.planning_*`, `admin.group.planning`, `event.PlanningEntryChanged`, digest section label; follow the string-file convention current at implementation), `web/package.json` (`"test:planner": "node tests/planner.cjs"`).
- New `web/tests/planner.cjs` — mocked-API browser flows for AC-PLN-01, AC-PLN-03, AC-PLN-07, AC-PLN-08, AC-PLN-14, AC-PLN-18 with an axe scan.

**Documentation**

- New `docs/guides/planning.md`; one row in `docs/user-guide.md`; results in `specs/034-weekly-planning-layer/verification.md`; status line in `specs/README.md` when built.

## Data model

### `hub.planning_entry` (new)

| Column | Type | Null | Notes |
|---|---|---|---|
| `id` | `uuid` | no | Primary key (UUID v7 from `Entity`) |
| `person_id` | `uuid` | no | → `app_user(id)` RESTRICT |
| `hours_per_week` | `numeric(5,1)` | no | |
| `start_week` | `date` | no | ISO Monday |
| `end_week` | `date` | no | ISO Monday |
| `label` | `text` | no | |
| `source_category` | `text` | no | |
| `project_id` | `uuid` | yes | → `project(id)` RESTRICT |
| `project_discipline_id` | `uuid` | yes | → `project_discipline(id)` RESTRICT |
| `confidence` | `text` | no | |
| `visibility` | `text` | no | |
| `notes` | `text` | yes | |
| `last_validated_at` | `timestamptz` | no | |
| `created_at`, `updated_at` | `timestamptz` | no | `Audited` |
| `created_by` | `uuid` | no (check) | Owner; → `app_user(id)` RESTRICT |
| `updated_by` | `uuid` | yes | `Audited` |
| `row_version` | `integer` | no | `Audited`, incremented by `HubDb.SaveChanges` |
| `deleted_at` | `timestamptz` | yes | Soft deletion; EF query filter `deleted_at IS NULL` |
| `deleted_by` | `uuid` | yes | |

Check constraints:

- `ck_planning_owner`: `created_by IS NOT NULL`
- `ck_planning_hours`: `hours_per_week > 0 AND hours_per_week <= 168 AND hours_per_week * 2 = trunc(hours_per_week * 2)` (the lower `planning_max_hours_per_week` limit is checked by the API)
- `ck_planning_weeks`: `extract(isodow FROM start_week) = 1 AND extract(isodow FROM end_week) = 1 AND end_week >= start_week AND end_week - start_week <= 721`
- `ck_planning_label`: `char_length(btrim(label)) BETWEEN 1 AND 120`
- `ck_planning_notes`: `notes IS NULL OR char_length(notes) <= 2000`
- `ck_planning_source`: `source_category IN ('MajorProject','OtherProject','Proposal','BusinessDevelopment','Training','Admin','Supervision','InternalInitiative','FieldWork','Other')`
- `ck_planning_confidence`: `confidence IN ('Confirmed','Expected','Possible')`
- `ck_planning_visibility`: `visibility IN ('Draft','Published','Confirmed')`
- `ck_planning_self_visible`: `created_by <> person_id OR visibility = 'Confirmed'`
- `ck_planning_project_source`: `(source_category = 'MajorProject') = (project_id IS NOT NULL)`
- `ck_planning_discipline`: `project_discipline_id IS NULL OR project_id IS NOT NULL`

Indexes:

- `ix_planning_entry_person_weeks` on `(person_id, start_week, end_week)` where `deleted_at IS NULL` — grid and cell reads.
- `ix_planning_entry_owner` on `(created_by, end_week)` where `deleted_at IS NULL` — owner lists and the stale digest.
- `ix_planning_entry_project` on `(project_id)` where `project_id IS NOT NULL` — project lookups and status warnings.

That a project discipline belongs to the linked project, that a project is live and viewable, and the `planning_max_hours_per_week` limit are API validations (they depend on other rows or settings).

### Existing records used

- `person_availability_override`, `person_date_version` — written only by the time-away command, under FR-CAP-02 rules.
- `resource_allocation`, `allocation_day_override` — read only (status Confirmed).
- `app_user.weekly_capacity_hours`, `holiday`, `task`, `project`, `project_discipline`, `org_setting`, `activity_log`, `notification`, `saved_view`.

### Settings (no migration; defaults live in `OrgSettings`)

| Key | Kind | Default | Range | Property |
|---|---|---|---|---|
| `planning_horizon_weeks` | Int | 12 | 6–26 | `PlanningHorizonWeeks` |
| `planning_over_pct` | Int | 105 | 50–300 | `PlanningOverPct` |
| `planning_under_pct` | Int | 50 | 0–100 | `PlanningUnderPct` |
| `planning_under_weeks` | Int | 2 | 1–12 | `PlanningUnderWeeks` |
| `planning_stale_days` | Int | 28 | 1–365 | `PlanningStaleDays` |
| `planning_max_hours_per_week` | Int | 80 | 1–168 | `PlanningMaxHoursPerWeek` |

`OrgSettings.Validate` uses a per-key range table: `coordination_lookahead_weeks` remains 1–12 and each planning setting uses the exact range above; unrelated integer settings retain their existing validation rules.

## API contracts

Base path `/api/v1`. JSON uses camelCase; dates are `yyyy-MM-dd`; any date passed as a week is normalised to its Monday. Problem responses follow §25.6.

| Status | Code | When |
|---|---|---|
| 400 | `validation` | Field errors: `planning.quick_add_format`, `planning.hours`, `planning.week`, `planning.span`, `error.date_range`, `error.required`, `error.too_long`, `error.one_of`, `planning.project`, `coord.reference`, `error.reason_required`, `planning.self_move`, `planning.time_away_days` |
| 403 | `forbidden` | `perm.read_only`, `perm.supervisor`, `perm.owner`, `planning.owner_authority`, `planning.self_visibility`, `perm.admin` |
| 404 | `not_found` | Entry not visible to the caller (including another owner's draft); person outside the caller's scope or inactive |
| 409 | `concurrency_conflict` | Stale `rowVersion` (`extra: { changedBy, changedAt, currentRowVersion }`); time-away stale dates (`extra: { staleDates }`) |
| 422 | `idempotency_key_reused`, `export_too_large` | Existing middleware and export limit |
| 428 | `precondition_required` | Change without `rowVersion` or `If-Match` |

**Entry row** (returned by every entry endpoint):

```json
{ "id": "0192…", "personId": "…", "personName": "Yagmur Demir", "ownerId": "…", "ownerName": "Taylor Reed",
  "ownerKind": "Manager", "label": "proposal support", "sourceCategory": "Proposal",
  "projectId": null, "projectNumber": null, "projectDisciplineId": null, "disciplineName": null,
  "hoursPerWeek": 8.0, "startWeek": "2026-10-05", "endWeek": "2026-10-19",
  "confidence": "Expected", "visibility": "Draft", "notes": null,
  "lastValidatedAt": "2026-10-03T14:00:00Z", "stale": false,
  "warnings": [], "canEdit": true, "canChangeVisibility": true, "correctionOnly": false, "rowVersion": 3 }
```

`warnings` ⊆ `ProjectNotActive`, `OwnerCannotManage`, `AboveCapacity`. `correctionOnly` is true when the caller may change the entry only as an Admin data correction.

### `GET /planning/grid`

Query: `from`, `weeks` (1–26, default `planning_horizon_weeks`), `supervisorId`, `disciplineId`, `officeId`, `personId`, `projectId`, `source` (comma list), `confidence` (comma list), `visibility` (comma list of `Draft`, `Published`, `Confirmed`, `Self`), `indicator` (`OverPlanned`, `UnderPlanned`, `StalePlan`), `q`, `sort` (`remaining` default, `name`, `over`), `week` (sort week; default current week), `includeMyDrafts` (default `true`), `draftAccessReason` (Admin only; turns on data-correction mode and is logged).

```json
{ "weeks": ["2026-10-05", "2026-10-12"], "currentWeek": "2026-10-05", "evaluatedAt": "2026-10-05T13:02:11Z",
  "partialView": true, "draftAccess": false,
  "settings": { "horizonWeeks": 12, "overPct": 105, "underPct": 50, "underWeeks": 2, "staleDays": 28, "maxHoursPerWeek": 80 },
  "people": [ {
    "id": "…", "displayName": "Yagmur Demir", "supervisorId": "…", "supervisorName": "Taylor Reed", "weeklyCapacity": 40.0,
    "canCreateSelfEntry": false, "canCreateManagerEntry": true, "canRecordTimeAway": true,
    "indicators": ["OverPlanned"],
    "cells": [ { "week": "2026-10-05", "capacity": 24.0, "timeAway": 16.0, "additionalAvailability": 0.0,
      "approved": 12.0, "confirmed": 16.0, "expected": 4.0, "possible": 8.0, "remaining": 4.0,
      "taskEstimates": 14.0, "unestimatedTasks": 3, "overPlanned": false, "underPlanned": false,
      "staleEntries": 1, "ownDraftHours": 8.0 } ],
    "entries": [ { "…": "entry row", "matchesFilter": true } ],
    "approvedAllocations": [ { "allocationId": "…", "projectId": "…", "projectNumber": "1234", "projectName": "Harbour Road",
      "canOpen": true, "weeks": [ { "week": "2026-10-05", "hours": 12.0 } ] } ] } ] }
```

Errors: 404 (`personId` outside scope), 403 `perm.admin` (`draftAccessReason` from a non-Admin), 400 `error.reason_required` (reason shorter than five characters).

### `GET /planning/cells/{personId}/{week}`

Query: `includeMyDrafts`, `draftAccessReason`. 404 when the person is outside the scope.

```json
{ "personId": "…", "personName": "Yagmur Demir", "week": "2026-10-05", "partialView": true,
  "capacity": { "weeklyCapacity": 40.0, "capacityOverride": false, "workingDays": 5, "capacity": 24.0, "timeAway": 16.0, "additionalAvailability": 0.0,
    "days": [ { "date": "2026-10-05", "normal": 8.0, "available": 0.0, "category": "Unavailable", "timeAway": 8.0, "additional": 0.0 } ] },
  "approvedAllocations": [ { "allocationId": "…", "projectNumber": "1234", "projectName": "Harbour Road", "hours": 12.0,
    "approvalStatus": "Confirmed", "canOpen": false } ],
  "entries": [ { "…": "entry row", "weekHours": 16.0, "countedHours": 4.0, "coveredHours": 12.0 } ],
  "bands": { "confirmed": 16.0, "expected": 4.0, "possible": 8.0 }, "remaining": 4.0,
  "taskEstimates": { "hours": 14.0, "unestimatedTasks": 3 },
  "indicators": [
    { "code": "OverPlanned", "rule": "PLN-10", "evaluated": true, "active": false, "threshold": 105,
      "explanation": "Confirmed + Expected 20.0 h is not more than 105 % of 24.0 h (25.2 h)" },
    { "code": "UnderPlanned", "rule": "PLN-11", "evaluated": false, "active": false, "explanation": "Not evaluated: partial view" } ] }
```

### `GET /planning/entries`, `GET /planning/entries/export`

Query: `personId`, `mine` (owner = caller), `from`, `to` (overlap), `source`, `confidence`, `visibility`, `projectId`, `q`, `stale`, `includeMyDrafts`, `draftAccessReason`, `sort` (`person`, `startWeek`, `lastValidated`), `page`, `pageSize` (≤ 200), and `format` (`csv` or `xlsx`) for export. Response: `{ "items": [entry row], "page": 1, "pageSize": 50, "totalCount": 3 }`. Export columns: Person, Owner, Owner kind, Label, Source category, Project, Discipline, Hours per week, Start week, End week, Confidence, Visibility (display label), Last validated, Stale, Warnings.

### `GET /planning/grid/export`

Grid query parameters and `format`. One row per person: Person, Supervisor, Weekly capacity, Partial view, then per week: Capacity, Time away, Approved allocation, Confidence Confirmed, Confidence Expected, Confidence Possible, Remaining, Task estimates (context), Indicators. Own drafts are included only when `includeMyDrafts` is true, and that parameter is printed in the export header.

### `GET /planning/entries/{id}`, `GET /planning/entries/{id}/activity`

Entry row, or a page of rendered activity rows (`ActivityEndpoints.Render`) for `ItemType = "PlanningEntry"` and this id. Both return 404 unless PLN-04 passes; for another owner's draft an Admin must pass `draftAccessReason` (logged).

### `POST /planning/entries` (optional `Idempotency-Key`)

```json
{ "personId": "…", "hoursPerWeek": 8.0, "startWeek": "2026-10-05", "endWeek": "2026-10-19",
  "label": "proposal support", "sourceCategory": "Proposal", "projectId": null, "projectDisciplineId": null,
  "confidence": "Expected", "visibility": "Draft", "notes": null }
```

`confidence` and `visibility` default by owner kind; a self entry accepts only `visibility` `Confirmed` or none. 201 with the entry row and `Location: /api/v1/planning/entries/{id}`. Copy is a create with the copied fields.

### `POST /planning/entries/quick` (optional `Idempotency-Key`)

`{ "personId": "…", "week": "2026-10-05", "text": "8 h proposal support" }` → 201 entry row. Unparsable text: 400 on `text` with `planning.quick_add_format`.

### `PATCH /planning/entries/{id}`

Body follows the existing `Patch` convention (a present property set to null clears it): `{ "rowVersion": 3, "personId", "hoursPerWeek", "startWeek", "endWeek", "label", "sourceCategory", "projectId", "projectDisciplineId", "confidence", "notes", "reason" }`. `reason` is required when the caller acts as an Admin on an entry they do not own. 200 with the entry row.

### `POST /planning/entries/{id}/visibility`

`{ "rowVersion": 3, "visibility": "Published", "reason": null }` → 200 entry row.

### `POST /planning/entries/{id}/still-valid`

`{ "rowVersion": 3 }` → 200 entry row.

### `DELETE /planning/entries/{id}`

`If-Match: "3"` (or `?rowVersion=3`), optional `?reason=` for Admin corrections → 204. Soft deletion.

### `POST /planning/time-away` (optional `Idempotency-Key`)

```json
{ "personId": "…", "from": "2026-11-02", "through": "2026-11-06", "action": "Record",
  "availableHours": 0, "category": "Unavailable",
  "versions": [ { "workDate": "2026-11-03", "rowVersion": 2 } ] }
```

`action` is `Record` or `Clear` (Clear ignores hours and category and keeps Additional overrides). `versions` carries the override versions the caller read from the existing `GET /users/{personId}/availability?from=&through=`; a target date without an entry is expected to have no override (version 0), and entries for other dates are ignored. 200:

```json
{ "personId": "…", "days": [ { "date": "2026-11-02", "availableHours": 0, "category": "Unavailable", "rowVersion": 1, "dateVersion": 4 } ],
  "cleared": [] }
```

Errors: 400 (`through` before `from`, more than 366 days, hours outside 0–24, category not Unavailable or Reduced, a Record range with no working day: `planning.time_away_days`), 403 (`perm.supervisor`, `perm.read_only`), 404 (person not found or inactive), 409 (`staleDates`).

### Existing endpoints reused

`GET/POST/PATCH/DELETE /views?listType=planner`; `GET /search?q=&type=planning`; `GET /admin/settings`, `PUT /admin/settings/{key}`; `GET /users/{personId}/availability`; notification and preference endpoints for `PlanningEntryChanged`.

## Pure functions (`src/Hub.Domain/Planning.cs`) and test cases

```csharp
public sealed record QuickAdd(decimal Hours, string Label, bool Possible);
public sealed record EntryWeek(Guid Id, Guid? ProjectId, string Confidence, decimal Hours, DateOnly StartWeek, DateTimeOffset CreatedAt);
public sealed record EntryCount(Guid Id, decimal Counted, decimal Covered);
public sealed record WeekBands(decimal Approved, decimal Confirmed, decimal Expected, decimal Possible, decimal Remaining, IReadOnlyList<EntryCount> Entries);
public sealed record UnderWeek(decimal Capacity, decimal TimeAway, decimal Planned, bool UnestimatedDue);

public static class PlanningRules
{
    public static string? ValidateWeeks(DateOnly start, DateOnly end);            // null, "planning.week", "error.date_range" or "planning.span"
    public static string? ValidateHours(decimal hours, int maxPerWeek);            // null or "planning.hours"
    public static decimal WeekHours(DateOnly start, DateOnly end, decimal perWeek, DateOnly week);
    public static QuickAdd? ParseQuickAdd(string? text, int maxPerWeek);         // PLN-13; null when refused
    public static (string Visibility, string Confidence) Defaults(bool self, bool possible);
    public static bool StepVisibility(bool self, string from, string to);         // PLN-16
    public static (decimal TimeAway, decimal Additional) TimeAway(decimal normalDay, decimal? overrideHours, string? category); // PLN-06
    public static WeekBands Combine(decimal capacity, IReadOnlyList<EntryWeek> entries, IReadOnlyDictionary<Guid, decimal> approvedByProject); // PLN-08, PLN-09
    public static bool OverPlanned(WeekBands bands, decimal capacity, int overPct);  // PLN-10
    public static bool[] UnderPlanned(IReadOnlyList<UnderWeek> weeks, int firstEvaluable, int underPct, int underWeeks, bool partial); // PLN-11; firstEvaluable = index of the first week on or after the current week
    public static bool Stale(DateOnly endWeek, DateOnly currentWeek, DateTimeOffset lastValidated, DateTimeOffset now, int staleDays); // PLN-12
}
```

Quick-add pattern (`[GeneratedRegex]`, `IgnoreCase | CultureInvariant`): `^\s*(~)?\s*(\d{1,3}(?:\.\d)?)\s*(?:(?:h|hr|hrs|hour|hours)\b)?\s*(?:[-–—:]\s*)?(\S.*?)\s*$`; hours parsed with the invariant culture; refused when `ValidateHours` fails, the label is longer than 120 characters, or the label is only a unit word.

| Function | Input | Expected |
|---|---|---|
| `ParseQuickAdd` | `8 h proposal support` | 8, "proposal support", not Possible |
| | `8h proposal support`; `8 — proposal support`; `8 hrs - proposal support`; `8: proposal support` | 8, "proposal support" |
| | `~8 h upcoming review`; `~ 8 h upcoming review` | 8, "upcoming review", Possible |
| | `7.5h admin`; `  12 h  Hwy 7 review support ` | 7.5 "admin"; 12 "Hwy 7 review support" |
| | `8 hotels` | 8, "hotels" |
| | `8 h`; `8`; `h proposal`; `-8 h x`; `0 h x`; `8.25 h x`; `100 h x` (max 80); label of 121 characters | refused (null) |
| `ValidateWeeks` | 2026-10-05 → 2026-10-19 | valid |
| | 2026-10-06 → 2026-10-19 | `planning.week` |
| | 2026-10-19 → 2026-10-05 | `error.date_range` |
| | 2026-10-05 → +103 weeks; → +104 weeks | valid; `planning.span` |
| `ValidateHours` (max 80) | 8; 7.5; 80 | valid |
| | 0; −1; 8.25; 80.5 | `planning.hours` |
| `WeekHours` (10-05 to 10-19, 8 h) | weeks 10-05, 10-12, 10-19; 09-28, 10-26 | 8, 8, 8; 0, 0 |
| `Defaults` | self; self + `~`; manager; manager + `~` | (Confirmed, Confirmed); (Confirmed, Possible); (Draft, Expected); (Draft, Possible) |
| `StepVisibility` | manager Draft→Published, Published→Confirmed, Confirmed→Draft, Draft→Confirmed | allowed |
| | manager Draft→Draft; any → `Bogus`; self entry, any change | refused |
| `TimeAway` | normal 8: override 0 Unavailable; 4 Reduced; 10 Additional; 6 Additional; 10 Reduced; none | (8, 0); (4, 0); (0, 2); (0, 0); (0, 0); (0, 0) |
| | holiday (normal 0), override 0 Unavailable | (0, 0) |
| `Combine` (capacity 40) | no project: Confirmed 30, Expected 13, Possible 8 | Confirmed 30, Expected 13, Possible 8, Remaining −3 |
| | approved {P: 12}; P Confirmed 16 | covered 12, counted 4; Confirmed 16; Remaining 24 |
| | approved {P: 12}; P Expected 8 | covered 8, counted 0; Confirmed 12; Expected 0 |
| | approved {P: 12}; P Confirmed 8, P Expected 6 | 8/0 and 4/2; Confirmed 12; Expected 2 |
| | approved {P: 12}; P Possible 20 | covered 12, counted 8; Confirmed 12; Possible 8 |
| | approved {P: 10}; P Expected 8 starting 10-05, P Expected 8 starting 09-28 | the 09-28 entry is covered first: 8/0; then 2/6; Expected 6 |
| | approved {P: 12, Q: 0}; Q Expected 5; no-project Expected 6 | Q counted 5; Confirmed 12; Expected 11 |
| | nothing | all 0; Remaining 40 |
| | negative capacity or hours | `ArgumentOutOfRangeException` |
| `OverPlanned` | 30 + 13 at 40, 105 % | true |
| | 30 + 13 at 40, 110 %; exactly 42 at 40, 105 % | false |
| | capacity 0 with Confirmed 1; capacity 0 with only Possible 5 | true; false |
| `UnderPlanned` (50 %, 2 weeks, capacities 40) | planned 10, 10, 30, 0; `firstEvaluable` 0 | true, false, false, false |
| | same with time away in the second week | all false |
| | same with an unestimated task due in the first week | all false (the first week is no longer eligible) |
| | partial view; `underPct` 0; `firstEvaluable` 1 | all false in each case |
| `Stale` (28 days) | end in future, last validated 29 days ago | true |
| | exactly 28 days ago; end week before the current week | false |
| `Permissions` (§8.11) | person on own row; Supervisor for a direct report; Admin | create allowed |
| | Read Only; inactive; Supervisor for a non-report; Project Manager; Executive | create refused |
| | owner of self entry; Supervisor owner still supervising | manage allowed, no reason |
| | Supervisor on a self entry; former Supervisor owner | refused (`perm.owner`; `planning.owner_authority`) |
| | Admin on an entry they do not own | allowed, reason required |
| | `SeePlanningEntry`: another owner's draft; Admin without and with data-correction mode | false; false; true |

Branch coverage of `Planning.cs` must reach the §22 gate (95 %).

## Migration notes

- Generate with `dotnet tool restore` then `dotnet ef migrations add PlanningEntries --project src/Hub.Api/Hub.Api.csproj --startup-project src/Hub.Api/Hub.Api.csproj --output-dir Data/Migrations`; confirm `dotnet ef migrations has-pending-model-changes` reports none afterwards.
- Additive only: creates `planning_entry` with its checks, indexes and foreign keys. No existing table, column, constraint or row changes; no backfill. Settings and the notification event need no rows.
- Down migration drops `planning_entry`. The previous API ignores the table, so the migration may be applied before the new API is released.
- Rehearse on a fresh database and on a restored copy that already holds 029 allocations and availability overrides; compare 029 table row counts and checksums before and after (AC-PLN-11).

## Validation

Run and record in `verification.md`, all initially UNRUN:

1. `python3 tools/build_spec.py --check` and `python3 tools/trace_spec.py --check`.
2. `dotnet build src/Hub.Api/Hub.Api.csproj --no-restore`.
3. `dotnet ef migrations has-pending-model-changes --project src/Hub.Api/Hub.Api.csproj --startup-project src/Hub.Api/Hub.Api.csproj`.
4. `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter 'FullyQualifiedName~PlanningTests|FullyQualifiedName~PermissionMatrixTests'`.
5. `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter 'FullyQualifiedName~PlanningSchemaTests|FullyQualifiedName~PlanningApiTests|FullyQualifiedName~PlanningPrivacyTests'`.
6. Unchanged-behaviour guard: `dotnet test … --filter 'FullyQualifiedName~Allocation|FullyQualifiedName~Workload|FullyQualifiedName~Readiness'` with no test edits, and `git diff --stat` showing no change to the "must not change" files.
7. Full suite `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --collect:"XPlat Code Coverage"` and `python3 tools/coverage_gate.py <results>`.
8. `npm --prefix web run build`, `npm --prefix web run lint`, `npm --prefix web run test:planner`, `node tools/a11y/audit.js` on `/planner` and `/my-work`.
9. AC-PLN-17 with `tools/scale/seed.sql` and `tools/scale/measure.py`.
10. Manual keyboard and screen-reader pass at 1440, 1024 and 375 px; `git diff --check`.

## Complexity tracking

No constitution exception. Deliberate simplifications with known ceilings:

- Figures are computed per request, not materialised — ponytail: if the Executive all-staff grid misses 1.5 s p95, add people paging or a per-person weekly cache, not a nightly job.
- The time-away command repeats `SetAvailability`'s short lock/version/touch sequence instead of refactoring packet 029's file — ponytail: a parity test guards drift; extract a shared helper only when packet 029 is next changed.
- Under-planned uses Workload's coarse partial rule, so non-Admin viewers never see it — ponytail: a finer rule needs a privacy review (§39.12 decision 14).
