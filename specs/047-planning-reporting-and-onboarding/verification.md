# Verification: Planning, Reporting and Onboarding

Status 2026-10-25: S01–S17 integrated locally on `codex/integrate-039-050`; S18 flag cleanup pending Jay's flag decisions. Current local evidence is the [19 October follow-up](../../docs/handoffs/2026-10-19-039-050-acceptance-followup.md) at runtime source `566fe518`: full Release backend 1,514/1,514 PASS, which runs every 047 API test named below against real PostgreSQL. Targeted tests were not rerun on 2026-10-25 (local Docker was down); this table cites recorded runs. Not Accepted; hosted, human and pilot checks remain open.

Integrated locally through runtime source `1b649f57f4f6c28abeb36fabdd9607b51ae13dde`. Final combined verification passes: 1,491 backend tests, coverage gates, 798 observed response shapes, six affected mocked browser commands, real-API browser/live flow, final-image permission checks and 29 old/new/old compatibility checks. See [the integration handoff](../../docs/handoffs/2026-10-18-039-050-integration.md) and its hashed evidence receipt for precise source boundaries and limits. Earlier lane tables below are historical slice-level records; old missing-schema blockers are resolved by the combined runtime checks. No acceptance criterion or packet is marked Accepted; remaining hosted, human and broader workflow gates stay explicit.

| AC ID | Area | Slice | Status |
|---|---|---|---|
| AC-PRO-01 | Getting-started card | S02 | PARTIAL: mocked `planning-reporting` onboarding scenario PASS (local); nightly real-API calm check with the card and manual screen-reader review not done (HUMAN) |
| AC-PRO-02 | Getting-started card | S01 | PASS (local): `OnboardingAndRouteCounterTests` role catalogue cases; `OnboardingApiTests` read-only and flag-gated read |
| AC-PRO-03 | Getting-started card | S01, S02 | PASS (local): `OnboardingApiTests` hide/show persists without activity; mocked hide and restore scenario |
| AC-PRO-04 | Team Capacity report | S03 | PASS (local): `CapacityReportsTests.Team_capacity_reuses_twelve_week_planner_scope_drafts_and_exports` |
| AC-PRO-05 | Report privacy | S03, S04 | PASS (local): `CapacityReportsTests` draft scope and source report cases; full persona privacy matrix not run |
| AC-PRO-06 | Allocations by Source report | S04 | PASS (local): `CapacityReportsTests.Source_hours_apply_approved_coverage_and_unlinked_list_filters` |
| AC-PRO-07 | Report exports | S03, S04 | PASS (local): `CapacityReportsTests` CSV/XLSX export cases |
| AC-PRO-08 | Route counter | S05, S06 | PASS (local): `RouteCounterTests` band, key and rate-limit cases; mocked route counter scenario (no raw URL or query) |
| AC-PRO-09 | Route counter off and notice | S05, S06 | PASS (local): `RouteCounterTests.Both_gates_off_store_nothing` and setting-hidden case; mocked notice |
| AC-PRO-10 | Template roles | S07 | PASS (local): `TemplatePlannerTests`; `TemplatesTests.Template_roles_preview_copy_preserve_and_instantiate_only_for_whole_projects_when_enabled` |
| AC-PRO-11 | Template role skips | S07, S08 | PARTIAL: skip reasons PASS in domain tests and editor scenario; wizard skip reasons source-reviewed only |
| AC-PRO-12 | Unfilled roles in the Planner | S09 | PARTIAL: `PlanningApiTests` includeOpenRoles flag pair PASS (local); planner browser scenario for the Unfilled roles group not written |
| AC-PRO-13 | Unassigned work under Workload | S10 | PASS (local): `WorkloadApiTests.Unassigned_tasks_are_dark_and_the_scoped_show_all_count_matches`; mocked `resources` assign and stale-version scenario |
| AC-PRO-14 | Project status note | S11 | PASS (local): `PlanningReportingWorkflowTests.Project_note_is_versioned_audited_permission_checked_and_never_changes_health`; mocked status-note scenario |
| AC-PRO-15 | Status note on Portfolio and permissions | S11 | PASS (local): same test covers Portfolio row and permissions; Portfolio export column with flag off not separately asserted |
| AC-PRO-16 | My Work Watching | S12 | PASS (local): `PlanningReportingWorkflowTests.Follow_up_views_track_own_open_watches_and_requested_records_without_writes`; mocked my-work views scenario |
| AC-PRO-17 | My Work Requested by me | S12 | PASS (local): same follow-up test (requested records); mocked view switch |
| AC-PRO-18 | Project import | S13, S14 | PASS (local): `ProjectImportTests` preview, commit, idempotency cases; mocked project import scenario |
| AC-PRO-19 | Project import limits | S13 | PASS (local): `ProjectImportTests.Oversized_and_duplicate_rows_are_rejected_or_classified_as_errors` |
| AC-PRO-20 | Estimate versus planned | S15 | PASS (local): `PlanningApiTests.Capacity_polish_contexts_reconcile_project_plans_and_task_estimates_without_changing_capacity`; mocked planner contexts |
| AC-PRO-21 | Per-weekday hours | S16 | PASS (local): `PlanningReportingWorkflowTests` weekday 20 h and zero-vector cases; SupervisorCapacity 0 h weekday case not written |
| AC-PRO-22 | Away chip on the Team tab | S17 | PASS (local): mocked `supervisor-capacity` 12-member team tab scenario |
| AC-PRO-23 | Flags off and on | every slice | PASS (local): flag-off/flag-on cases in the API tests above; S18 flag cleanup pending Jay |

## 2026-10-18 evidence and remaining gates

- PASS: `dotnet build Hub.slnx -c Release --no-restore` (0 errors). API regression sources compile with the solution.
- PASS: focused pure-domain run132/132, zero failed/skipped, covering onboarding catalogue, strict route keys/bands, role dates/skips, workload and permission matrix. Evidence: `/private/tmp/capacity-041-047-test-results/capacity-domain-final.trx`.
- PASS: final web build and lint (warnings only). New `planning-reporting` mocked suite is registered in package.json and tools/ci_steps.txt; observed15posts, import success, zero page errors/unmocked calls/axe violations. This covers selected onboarding/restore, route-gate, status-note, import and follow-up flows; it is not every server acceptance case.
- PASS: mocked resources assignment uses the task week, sends the row version, shows stale-version feedback, saves and removes the unassigned row, and keeps the scoped Show-all URL. Mocked planner verifies contribution/project context including a project with no planning entry. Existing resources/planner/supervisor-capacity/design-basis/coordination/handoffs workflows pass.
- PASS: generated specification current; traceability1,299IDs/405sections, no gaps; git diff --check.
- Independent source review repaired dark-field omissions, optional discipline roles, retained-role FK guards, one template-level offset kind, actual report horizon/date links, import activity shape, route timing/UUID privacy, legacy calendars and task-only project context. Latest reviewed paths have no open actionable P1/P2.
- Resolved 2026-10-19 (was BLOCKED): the combined migration landed and the full backend run at `566fe518` passes these API tests. Original note: real PostgreSQL API execution. The OnboardingApiTests probe compiled and reached startup, then both tests failed at Program migration with EF PendingModelChangesWarning because this lane deliberately owns no migrations/snapshot. Do not suppress the warning. Schema lane owns the combined additive migration; integrator owns API/full-coverage execution after adopting it.
- Model commits: `bb8c95a` followed by required correction `939a6b7`; the latter restores the original schedule CHECK and removes per-role day-kind fields to align reviewed integrated model8c1ae565. Save of an all-zero weekday vector preserves a valid nonempty legacy WorkingDays representation; effective days come from vector values. No migration or snapshot changed here.
- New API regressions cover capacity reports/draft scope/export/coverage, strict route aggregation/rate limit, onboarding hide/show/read-only/flag pair, template preview/preservation/draft-copy/instantiation, unassigned scope/count/export parity, status note/version/reason/audit, weekday20h/zero-vector/legacy schedule, follow-up reads and create-only import/idempotency/limits. They are authored/compiled, not runtime PASS.
- Integration seams:040 must refresh generated contracts and adopt audited fields into its registry;045 supplies the canonical offset-day API/editor;050 TeamCapacity unit scope must use OrgStructureReports.At at week-end, owned by the integrator. No current UnitId substitute.
- UNPROVEN: full95% domain branch coverage, full acceptance matrix/privacy persona cases, real-API browser/nightly calm checks, manual keyboard/screen-reader review, hosted/native/Entra behavior. S18 flag cleanup requires Jay's review/pilot choices and remains pending.

Raw logs live under `/private/tmp/capacity-*`. The durable handoff is `docs/handoffs/2026-10-18-capacity-planning-lane.md`. No API test failure has been relabelled PASS; the table remains blocked until integrated execution records replace it.

### Final companion checks

Built-preview `PORT=19513 npm run test:planning-reporting`: PASS16posts, import success, no page errors/unmocked calls/axe violations (`/private/tmp/capacity-browser-planning-reporting-preview.log`). Draft template roles are edited/added/removed with optional discipline and milestone anchors; editor preview verifies its milestone/undated summary. Wizard role dates and named skip reasons remain source-reviewed rather than fully browser-covered. `PORT=5194 npm run test:planner`: PASS20flows, no page errors/unmocked calls, axe clean; exact P8planned/10estimate and Q0planned/4estimate contexts asserted. Supervisor suite at5198: PASS12-member one-batch scenario with partial/out-of-scope handling, preserving120-member/three-batch coverage. These synthetic checks do not replace API execution.

Final API test-source Release compile: PASS0errors (`/private/tmp/capacity-tests-supplement-build3.log`). Test review corrected the linked-entry source to MajorProject, isolated the own-draft report fixture to a dedicated supervisor, used the list DTO's totalCount, covered500-character status notes, and added normalized report-link/default-horizon scope checks. Counter assertions use before/after values where other test rows may exist. Runtime still BLOCKED pending combined schema. All browser test changes and the planning-reporting CI registration are in the companion commit listed by the lane coordination record.
