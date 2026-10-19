# Verification: Task and Timeline Interaction

Runtime source `566fe5187d72be56a14fd16d0428e8b8e6e34d72` passes **1,514/1,514 backend tests** (zero failures/skips). Rules branch coverage is **3,032/3,155 (96.1%)** and service line coverage **13,084/13,665 (95.7%)**. The 799-response shape gate passes from one full-run manifest. See the [acceptance follow-up](../../docs/handoffs/2026-10-19-039-050-acceptance-followup.md) and its hashed receipt for current browser, image and source boundaries. Earlier slice runs below are historical; their missing-schema/startup blockers are resolved by the combined run. Prior compatibility evidence retains source `1b649f5`; no schema changed afterward. No packet is Accepted; hosted, pilot and human gates remain separate.

## Historical lane evidence — superseded by current crosswalk below

- Implemented the project-ranked picker, task quick actions/name editor, shared date-context read/hints and drag warnings, slip threshold and PM re-baseline actions, latest-planned note, personal view copy/Staff views, grouped activity/actor, Related deliverable creation, optimistic status, allocation wording, Gantt URL collapse, Planner link, Timeline handoffs and stored milestone-chain highlighting.
- Reused the existing flag helper, status/authorisation/reason dialogs, task block/name controls, date editor, saved-view menu, export infrastructure and Related-link transaction. No dependencies added. Off-shape fields are omitted where required.
- Independent source review found no remaining actionable finding after fixes to flag boundaries, restore/delete compatibility, view copy, date drafts, shared response contracts and the re-baseline transaction. This is source review evidence, not runtime acceptance.
- PASS: `dotnet build Hub.slnx --no-restore --nologo -v quiet` (0 errors; warnings remain). PASS: `npm run build`. PASS: `npm run lint` (warnings only).
- PASS: `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter 'FullyQualifiedName~Hub.Tests.Domain.ScheduleTests|FullyQualifiedName~Hub.Tests.Domain.TemplatePlannerTests|FullyQualifiedName~Hub.Tests.Domain.DependencyLagTests|FullyQualifiedName~Hub.Tests.Domain.PermissionMatrixTests|FullyQualifiedName~Hub.Tests.Domain.FlagsTests' --nologo -v quiet`: 176 passed, 0 failed, 0 skipped. Covers shared date bounds, lag, template stepping, slip threshold, baseline calculations, latest planned, permissions and flag metadata pairs.
- PASS: existing evaluator regression via `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Hub.Tests.Domain.EvaluatorTests' --nologo -v quiet`: 53 passed, 0 failed, 0 skipped.
- PASS: focused synthetic Chromium interaction suite through `npm run test:interaction`. Final command: `INTERACTION_TEST_PORT=19536 npm run test:interaction`. It exercises actual quick-action opacity on hover/focus, Open/deep links, archived-viewer Open-only behavior and the row flag-off shape; inline-name Escape sends no write and Enter sends exactly one PATCH; a start-authorisation refusal opens its existing dialog while status remains Not Started; TaskPanel Start-date editing sends the proposed date, retains explicit empty `startDate=` when cleared and Escape sends no write; re-baseline sends reason/current row version/clear intent; Timeline handoff navigation and chain URL/count send no writes. Timeline axe: zero violations; zero page errors/unknown API routes. Durable report: [mocked Chromium](evidence/mocked-chromium.json). It uses intercepted synthetic API responses and does not prove API permissions/transactions.
- PASS: existing settings/default/validation checks via `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Hub.Tests.Domain.DomainHelpersTests' --nologo -v quiet`: 28 passed, 0 failed, 0 skipped.
- BLOCKED: `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Baseline_requires_pm_reason_and_current_version_and_preserves_original_slip' --nologo --logger 'console;verbosity=normal'` fails at test-host startup with EF `PendingModelChangesWarning`, before application assertions. Earlier combined focused run was 145 domain passes and 16 API startup failures for the same reason. New picker/date-context/timeline/baseline/related-link/actor/saved-view API tests compile; their runtime outcome is UNPROVEN. No warning suppression, migration or snapshot was generated in this lane.

## Historical schema/integration contract — integrated

Model commit `ea202b19` contains nullable `Milestone.BaselineDate`, `Deliverable.BaselineStartDate` and `Deliverable.BaselineDueDate` (EF `date`), plus audited fields. The sole schema lane owns their migration/snapshot and the 048 immutable `DeliverableBaseline` history model. Deliverable re-baseline now uses `Tx.Run`, project advisory lock, reload, current-context permission/deleted/closed checks, row version and required reason. Integration must call `BaselineHistory.Capture(db, d, body.Clear == true, Check.Reason(body.Reason), access.Me.Id, clock.GetUtcNow())` immediately after the date assignments and before its final `SaveChangesAsync`, inside that same transaction. The history helper must be a no-op when its flag is off; clearing clears its pointer and keeps immutable history. This hook is not implemented here because 048 owns the entity.

## Current acceptance crosswalk (2026-10-19)

Local PASS covers only the named checks. It is not human acceptance. Backend evidence is the full 1,514-test TRX; browser evidence is `ci-artifacts/integration/interaction-final-566fe51.log` (both scripts pass).

| AC | Current evidence and limit |
|---|---|
| AC-INT-01 | PASS bounded API/mock flag pairs; complete flag retirement and pilot parity remain gated. |
| AC-INT-02 | PASS desktop mock hover/focus, named Open, archived viewer Open-only; server role/status matrix passes. |
| AC-INT-03 | PASS mock start-authorisation refusal with unchanged status; backend authorization regressions pass. |
| AC-INT-04 | PASS mock Block dialog requires reason and sends expected payload; backend lifecycle regressions pass. |
| AC-INT-05 | PASS empty/Escape no-write, Enter single PATCH, 409 saved name recovery and rejected-draft retention; `ScheduleBaselineTests.Task_name_edit_logs_once_and_newest_actor_is_flagged_without_changing_sort` passes. |
| AC-INT-06–07 | PASS all six PickerRank tests: ranking, recent window, search, access and flag-off shape. |
| AC-INT-08 | PASS browser Task and Deliverable project query and On this project marker; Portfolio query has no project context. Milestone provider wiring is source evidence only (`Milestones.tsx:479`): no PeoplePicker consumer exists in that panel, so no milestone picker interaction is claimed. |
| AC-INT-09–11 | PASS all 11 DateContext tests: evaluator bounds, own inconsistencies, availability scope and flag-off behavior. |
| AC-INT-12 | PASS mock proposed/cleared date requests, Escape no write, inconsistent-date save; API bounds/evaluation tests pass. |
| AC-INT-13 | PASS mock new inconsistency/successor/child warnings and cancel without write; API descendant read and drag regressions pass. |
| AC-INT-14 | PASS mock two-day/no-ghost and four-day/original-plus-baseline ghosts; API threshold/filter/export cases pass. |
| AC-INT-15–16 | PASS all four ScheduleBaseline cases plus reason/current version payload, denied controls and second ghost. `BaselineHistory.Capture` is integrated inside the deliverable transaction; baseline/history/audit/conflict checks pass. |
| AC-INT-17 | PASS API current values/three surfaces/status/health invariants and mock latest-planned Why title. Human visual review remains separate. |
| AC-INT-18–19 | PASS shared view copied as Personal with identical parameters, no source PATCH and default false; Staff default replay, explicit URL precedence and manual restoration. All 12 Views tests pass. |
| AC-INT-20 | PASS four entries grouped within ten minutes, separate eleven-minute entry and expand-all originals in Chromium. |
| AC-INT-21 | PASS actor, sort and log API behavior. LIMIT: task-list p95 is not consistently within 500 ms; see measurement below. |
| AC-INT-22 | PASS mock create-from-task prefills/type/default owner/reciprocal link and denied action; API preserves original task and rejects cross-project source. |
| AC-INT-23 | PASS pending Complete/Saving state then refusal rollback to In Progress; existing guards remain. |
| AC-INT-24 | PASS actual allocation dialog says 2 of 5 dates and renders five date rows; zero axe violations after animation settles. |
| AC-INT-25 | PASS global Gantt collapse, URL reload/back and all-collapsed state. |
| AC-INT-26 | PASS allowed/denied Portfolio-to-Planner capability pair. |
| AC-INT-27 | PASS handoff date marks, mismatch/overdue days, navigation, incorporated/discipline filters and no-write controls; API visibility/status cases pass. |
| AC-INT-28 | PASS exact one-of-two chain highlight, dimming and clear; API uses stored affected milestones without recalculation. |
| AC-INT-29 | PASS bounded desktop Timeline/Gantt/allocation axe checks. Phone, keyboard/screen-reader completeness, human review and representative network timing remain UNPROVEN. |

## Performance and remaining gates

[The measurement receipt](evidence/scale-measurement.json) preserves the 500-task, 100-concurrent-user, page-size-100, pool-20 samples. Uninstrumented task-list p95 is **912.2 ms flag off (FAIL 500 ms target)** and **392.2 ms flag on**; picker p95 is 68.6/108.8 ms. Fixed off-then-on order prevents a speedup or regression claim. Coverage-instrumented task-list timings also exceed 500 ms. Functional TRX success does not assert latency. One subsequent coverage-disabled OFF→ON→ON→OFF control used the same load, two full-concurrency warmup batches per phase/path and three measured batches. Task-list phase p95 values are 340.7, 366.5, 350.4, 315.9 ms; aggregate OFF 338.4 / ON 362.4 ms (600 samples each). Picker aggregate OFF 57.2 / ON 103.7 ms. This single warmed local control passes 500 ms, while preserving the earlier failure; it does not establish statistical significance or corporate-network/pilot performance.

Combined schema startup, API regression and coverage blockers are resolved. Previous-image compatibility remains the recorded 29-check source-1b649f5 result; no later schema change occurred. Full phone/keyboard/screen-reader and human acceptance, flag retirement and hosted/pilot operation remain separate. No push, deployment, pilot flag change or Accepted status.
