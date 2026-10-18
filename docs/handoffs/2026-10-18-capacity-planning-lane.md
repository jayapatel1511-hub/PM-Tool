# Capacity/planning lane handoff — 2026-10-18

Source: `codex/capacity-planning-041-047`, isolated worktree `/Users/jaypatel/.codex/worktrees/capacity-planning-041-047/PM-Tool`, baseline `a6e0104b0b00ea58b9098e41b2f155305063aeed`. Local source/testing only. No push, PR, deployment, pilot enablement, decision confirmation or Accepted status.

## Adoption

- Model commit `bb8c95a5587f1c6eb7cec93331ee411ce649ae0c` plus required alignment `939a6b7`: original schedule CHECK retained, one canonical ProjectTemplate.OffsetDayKind, no per-role kind columns. Integration already aligned these at8c1ae565; do not reapply the early model uncorrected.
- Reviewed core source commit: `ca18b3732bb3607a8e2b218870fc7d6dacf93016`. It excludes migrations/snapshot and can apply after the integrated model. The companion test/CI commit is published in `/private/tmp/tuesday-039-050-coordination/capacity-planning.md`; adopt both before running combined regression.
- Recovered b-041-S07 and b-041-S12 tracked.patch content, including baseline status_rollups registry reconciliation. Original snapshots were not cleaned or rewritten by this lane.

## Implemented

041 S07/S09–S12: project, basis, coordination and task server permission values and client consumers; kept Planner rows.047 S01–S17: onboarding, capacity/source reports, anonymous daily route totals, template roles, unfilled/unassigned demand, status note, My Work follow-ups, create-only project CSV import, per-project estimate context, weekday schedule capacity and Team away chip. Additions remain behind their declared dark flags. Existing response/export shapes stay unchanged when flags are off.

## Checks

Release solution compile PASS0errors; focused domain132PASS/0FAIL/0SKIP; web build/lint PASS with warnings; focused mocked resources/planner/supervisor-capacity/design-basis/coordination/handoffs and planning-reporting PASS. Specification freshness and traceability PASS. Independent source review has no remaining actionable P1/P2 in latest reviewed paths. See both packet verification files for evidence and limitations.

API regressions are authored and compile but runtime is BLOCKED at fixture migration startup by the missing combined schema. No warning suppression or substitute schema used. Full suite, coverage, golden contract refresh and combined API execution belong to integration after schema adoption.

## Integration-owned seams

1.040/041 S08: ratchet is absent from this lane. Add load-percentage literal baseline0 and failing permission-derivation counter/seeded tests in the040 owner implementation. Final permissions.isPm/leadOf references under web/src are0. Refresh generated contracts for optional taskContexts/unassigned/roles and permission fields; adopt new audited fields into040 registry.
2.045: retain canonical template offset kind and recovery editor/API behavior while merging Templates.cs and Templates.tsx; no per-role day-kind fields.
3.050: apply OrgStructureReports.At historical week-end scope to TeamCapacity where required, not current user UnitId.
4.Shared migration, API/full-coverage regression, human Tier B review, pilot choices and flag cleanup remain open. Do not mark041/047 Accepted from this handoff.

Browser data is synthetic and mocked. It does not prove real API privacy, database migration, Entra, hosted, native CAD or human acceptance.

## Final companion evidence

Built preview planning-reporting suite PASS16posts/no errors/no unknown calls/axe clean, including Draft role edit/add/remove and editor preview summary. Planner PASS20flows/17empty axe checkpoints; exact P8planned/10estimate and Q0planned/4estimate context. Supervisor suite PASS12-member one-batch partial/out-of-scope scenario plus original120-member/three-batch coverage. Resources PASS task-week capacity/assignment conflict/row version/scoped link. API source Release compile PASS0errors after fixture review; API runtime remains unproven. See packet records for precise limits, including wizard skip-text and no-access Team browser cases not fully covered.
