# Verification: Task and Timeline Interaction

Local implementation is present on `codex/recovery-interaction-045-046`, from baseline `a6e0104b0b00ea58b9098e41b2f155305063aeed`. Schema integration and acceptance remain pending. The six flags stay dark; S16 is gated on Jay's review/pilot decisions.

## Current evidence

- Implemented the project-ranked picker, task quick actions/name editor, shared date-context read/hints and drag warnings, slip threshold and PM re-baseline actions, latest-planned note, personal view copy/Staff views, grouped activity/actor, Related deliverable creation, optimistic status, allocation wording, Gantt URL collapse, Planner link, Timeline handoffs and stored milestone-chain highlighting.
- Reused the existing flag helper, status/authorisation/reason dialogs, task block/name controls, date editor, saved-view menu, export infrastructure and Related-link transaction. No dependencies added. Off-shape fields are omitted where required.
- Independent source review found no remaining actionable finding after fixes to flag boundaries, restore/delete compatibility, view copy, date drafts, shared response contracts and the re-baseline transaction. This is source review evidence, not runtime acceptance.
- PASS: `dotnet build Hub.slnx --no-restore --nologo -v quiet` (0 errors; warnings remain). PASS: `npm run build`. PASS: `npm run lint` (warnings only).
- PASS: `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-restore --filter 'FullyQualifiedName~Hub.Tests.Domain.ScheduleTests|FullyQualifiedName~Hub.Tests.Domain.TemplatePlannerTests|FullyQualifiedName~Hub.Tests.Domain.DependencyLagTests|FullyQualifiedName~Hub.Tests.Domain.PermissionMatrixTests|FullyQualifiedName~Hub.Tests.Domain.FlagsTests' --nologo -v quiet`: 176 passed, 0 failed, 0 skipped. Covers shared date bounds, lag, template stepping, slip threshold, baseline calculations, latest planned, permissions and flag metadata pairs.
- PASS: existing evaluator regression via `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Hub.Tests.Domain.EvaluatorTests' --nologo -v quiet`: 53 passed, 0 failed, 0 skipped.
- PASS: focused synthetic Chromium interaction suite through `npm run test:interaction`. Final command: `INTERACTION_TEST_PORT=19536 npm run test:interaction`. It exercises actual quick-action opacity on hover/focus, Open/deep links, archived-viewer Open-only behavior and the row flag-off shape; inline-name Escape sends no write and Enter sends exactly one PATCH; a start-authorisation refusal opens its existing dialog while status remains Not Started; TaskPanel Start-date editing sends the proposed date, retains explicit empty `startDate=` when cleared and Escape sends no write; re-baseline sends reason/current row version/clear intent; Timeline handoff navigation and chain URL/count send no writes. Timeline axe: zero violations; zero page errors/unknown API routes. Durable report: [mocked Chromium](evidence/mocked-chromium.json). It uses intercepted synthetic API responses and does not prove API permissions/transactions.
- PASS: existing settings/default/validation checks via `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Hub.Tests.Domain.DomainHelpersTests' --nologo -v quiet`: 28 passed, 0 failed, 0 skipped.
- BLOCKED: `dotnet test tests/Hub.Tests/Hub.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Baseline_requires_pm_reason_and_current_version_and_preserves_original_slip' --nologo --logger 'console;verbosity=normal'` fails at test-host startup with EF `PendingModelChangesWarning`, before application assertions. Earlier combined focused run was 145 domain passes and 16 API startup failures for the same reason. New picker/date-context/timeline/baseline/related-link/actor/saved-view API tests compile; their runtime outcome is UNPROVEN. No warning suppression, migration or snapshot was generated in this lane.

## Schema/integration contract

Model commit `ea202b19` contains nullable `Milestone.BaselineDate`, `Deliverable.BaselineStartDate` and `Deliverable.BaselineDueDate` (EF `date`), plus audited fields. The sole schema lane owns their migration/snapshot and the 048 immutable `DeliverableBaseline` history model. Deliverable re-baseline now uses `Tx.Run`, project advisory lock, reload, current-context permission/deleted/closed checks, row version and required reason. Integration must call `BaselineHistory.Capture(db, d, body.Clear == true, Check.Reason(body.Reason), access.Me.Id, clock.GetUtcNow())` immediately after the date assignments and before its final `SaveChangesAsync`, inside that same transaction. The history helper must be a no-op when its flag is off; clearing clears its pointer and keeps immutable history. This hook is not implemented here because 048 owns the entity.

## Acceptance map

“PARTIAL” means implemented with the evidence stated, with remaining checks explicitly open; it does not mean Accepted.

| AC | Current evidence and limit |
|---|---|
| AC-INT-01 | PARTIAL: domain flag metadata PASS; selected browser flag-off PASS; complete API/browser parity pending |
| AC-INT-02 | PARTIAL: actual hover/focus opacity, named Open and archived-viewer Open-only mock PASS; full role matrix pending |
| AC-INT-03 | PARTIAL: start refusal/authorisation dialog with unchanged status mock PASS; successful real authorisation pending |
| AC-INT-04 | PARTIAL: existing BlockDialog reused; save/log sequence pending |
| AC-INT-05 | PARTIAL: Escape/no write and Enter/single PATCH mock PASS; single-log API case compiled, schema blocked; conflict/empty matrix pending |
| AC-INT-06 | PARTIAL: API ranking/recent-window cases compiled, schema blocked |
| AC-INT-07 | PARTIAL: API matching/access/off cases compiled, schema blocked |
| AC-INT-08 | PARTIAL: project context supplied by layout and all three panels; full picker browser case pending |
| AC-INT-09 | PARTIAL: shared dependency-bound domain PASS; API consistency read schema blocked |
| AC-INT-10 | PARTIAL: parent/own inconsistency domain PASS; API read schema blocked |
| AC-INT-11 | PARTIAL: scoped availability/no-detail cases compiled, schema blocked |
| AC-INT-12 | PARTIAL: real date editor/request/clear/Escape mock PASS; inconsistent save/evaluation pending |
| AC-INT-13 | PARTIAL: successor/child read and dialog implemented; API tests schema blocked; drag-confirm flow pending |
| AC-INT-14 | PARTIAL: threshold domain PASS; timeline/export API cases schema blocked; ghost rendering matrix pending |
| AC-INT-15 | PARTIAL: permission matrix PASS and reason payload mock PASS; database/audit/conflict API case schema blocked |
| AC-INT-16 | PARTIAL: baseline calculations PASS; clear/original slip/export API case schema blocked; second-ghost visual check pending |
| AC-INT-17 | PARTIAL: latest planned domain PASS; current-read/health API cases schema blocked; three-surface visual check pending |
| AC-INT-18 | PARTIAL: active parameters copied as personal in existing menu; shared-copy browser sequence pending |
| AC-INT-19 | PARTIAL: personal Staff menu/API validation implemented; API case compiled, schema blocked; default replay pending |
| AC-INT-20 | PARTIAL: actor/item/burst grouping retains expandable original entries; full browser sequence pending |
| AC-INT-21 | PARTIAL: newest actor scalar projection/name fallback implemented; sort/log API case schema blocked; 500-task timing unrun |
| AC-INT-22 | PARTIAL: same-transaction Related link and unchanged task covered by compiled API case, schema blocked; panel flow pending |
| AC-INT-23 | PARTIAL: readiness guards and existing dialogs retained; full pending/refusal browser sequence pending |
| AC-INT-24 | PARTIAL: wording implemented; numeric allocation-preview browser fixture unrun |
| AC-INT-25 | PARTIAL: URL-driven collapse including `collapsed=all`; reload/back-forward browser sequence unrun |
| AC-INT-26 | PARTIAL: capability-gated Planner link implemented; allowed/denied browser pair unrun |
| AC-INT-27 | PARTIAL: handoff link mock PASS; date marks/mismatch/filter/API matrix pending |
| AC-INT-28 | PARTIAL: chain URL/count/no-write mock PASS; exact multi-task/deliverable/arrow highlight fixture pending |
| AC-INT-29 | PARTIAL: focused desktop Timeline axe PASS; complete phone/keyboard/screen-reader and §22 timing UNPROVEN |

## Open gates

Combined migration up/rollback smoke, EF drift, API runtime/regression, real packet 036 count reconciliation, rules/services coverage gate, 500-task timing, complete desktop/phone accessibility, human visual review and acceptance were not run or granted here. No flags enabled, push, PR, deployment, Tier B approval or Accepted decision. Source and test additions are local integration candidates.
