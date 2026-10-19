# Verification: Typed Contract and Entity Registry

Integrated locally through runtime source `1b649f57f4f6c28abeb36fabdd9607b51ae13dde`. Final combined verification passes: 1,491 backend tests, coverage gates, 798 observed response shapes, six affected mocked browser commands, real-API browser/live flow, final-image permission checks and 29 old/new/old compatibility checks. See [the integration handoff](../../docs/handoffs/2026-10-18-039-050-integration.md) and its hashed evidence receipt for precise source boundaries and limits. Earlier lane tables below are historical slice-level records; old missing-schema blockers are resolved by the combined runtime checks. No acceptance criterion or packet is marked Accepted; remaining hosted, human and broader workflow gates stay explicit.

| Acceptance criterion | Slice | Status |
|---|---|---|
| AC-PLAT-01 | S02 | PARTIAL: completeness/snapshot checks pass; explicit negative deletion controls not all recorded |
| AC-PLAT-02 | S03 | PARTIAL: original 78-type ordered snapshots pass; before/after seeded audit bytes not separately compared |
| AC-PLAT-03 | S04 | PARTIAL: original 14 history mappings preserved; full endpoint sweep belongs to integration |
| AC-PLAT-04 | S04 | PARTIAL: original name/export query snapshots preserved; binary before/after comparison unproven |
| AC-PLAT-05 | S06, S07 | PASS local: snapshot supersets and generated saved-view keys |
| AC-PLAT-06 | S06 | PARTIAL: four-list onHold=false API round trips and typed page negative control pass; browser reopen unproven |
| AC-PLAT-07 | S06 | PARTIAL: groupBy=office API round trips pass; grouped browser reopen unproven |
| AC-PLAT-08 | S09 | PASS local: legacy-link real browser smoke and saved-mode API round trips |
| AC-PLAT-09 | S09 | PASS local: reserved page-mode guard and unit tests |
| AC-PLAT-10 | S05 | PASS local: generated label completeness and missing-label compiler control |
| AC-PLAT-11 | S05, S08 | PASS local: strict key and shared-text checks; missing-key compiler control |
| AC-PLAT-12 | S10 | PASS local: golden contract checks |
| AC-PLAT-13 | S10, S12 | PASS local: regeneration/build and typed PATCH transport browser checks |
| AC-PLAT-14 | S12, S15.n | PASS local checkpoint: typed caller/fixture negative controls and 798-response observed shape gate pass; later S15 slices require their own checks |
| AC-PLAT-15 | S11 | PASS local: recorder and shape merger regression controls |
| AC-PLAT-16 | S11, S15.n | PASS local checkpoint: one final full-run capture; all declared JSON responses observed, 381/429 mapped endpoints called (88.8%) |
| AC-PLAT-17 | S12 | PASS local: actual JSDoc fixture rejects renamed DTO property |
| AC-PLAT-18 | S13 | PASS local: final owned synthetic PostgreSQL/API/browser smoke and two-browser live flow |
| AC-PLAT-19 | S14 | PASS local: invalidation map tests including delayed legacy refresh |
| AC-PLAT-20 | S01 | PASS local: seeded ratchet regression controls; all enforced combined-lane counters pass original limits, then baselines lowered |
| AC-PLAT-21 | S16 (human check) | UNPROVEN: qualifying five-feature cohort not established; measurement and subsequent Jay ruling remain pending |


## Local implementation handoff — 2026-10-18

The isolated `codex/platform-039-050` checkout implements the registry consumers, original-contract snapshots, generated key families and strict text keys, saved-view key derivation, legacy page-mode migration, golden OpenAPI/types, shared typed request transport, response-shape recorder, query invalidation map and the OpenRole/Workload proof. This is local source, not Accepted or deployed. Packet 043 is deliberately excluded from this handoff.

- PASS: standalone ContractTests/RegistryTests, 22 tests; combined scoped contract/registry/live/coverage checks, 40 tests. Earlier full response capture ran 1,240 tests: 1,239 passed and one newly added LiveStamp assertion failed; that assertion was corrected and the focused live checks then passed. This is not a claim that the final full suite ran.
- PASS: standalone TypeScript and production build. Generated contract has no packet-043 meta/record endpoints. The resources browser suite retains its existing assertions and adds typed PATCH If-Match/UI-source header checks; zero page errors, unmocked GETs and axe violations.
- PASS: real Development API/browser smoke across Allocations, Workload and three legacy links, zero page errors/serious axe violations. The required web-job PostgreSQL service and owned synthetic API/preview runner are wired; the runner itself passed on spare local ports.
- PASS: query-key/page-mode tests, shape-merger tests, seeded ratchet regressions, build-aware activation/probe tests and local SSE server probes. Counts and exact commands are retained in `ci-artifacts/platform/` locally.
- Shape snapshot: original response run `710901ff78a2432ea74c8fda62def872`, with only the 19 missing typed operations filled from the successful coverage fixture run `3c8b8de6bdc944368ab0fcc3e2fbdf54`. Original operation shapes were not replaced. Full final shape-gate and shard coverage remain UNPROVEN until integration runs the full API suite.
- Registry snapshots preserve all original types and each original field in order while permitting explicitly authorized additions from the other lanes. History remains exactly the original 14 types. Integration owns the explicit 82-type/late-field union.
- FR-FIG-15 counters: direct load-percentage literal comparisons baseline 0; exact page permission expressions baseline 25 from a6e0104b. Generic task progress percentages are outside the load counter. Other initial counters are not silently raised at integration.
- Remaining gates: eight page key-list typing and added onHold/groupBy/mode saved-view round-trip scenarios need completion; negative compiler controls and final full CI/coverage are unproven. S15 feature conversions and S16 fifth-feature human ruling are incomplete. No packet or decision is marked Accepted.

Independent final handoff review found and resolved dynamic GUID dictionary keys in response shapes. The recorder/merger now use a stable `{id}` segment while preserving child fields and kinds; C# regression 1/1 and Python shapes regressions 5/5 pass. Initial snapshot normalization changed only those dictionary keys.

## Followup verification — 18 October 2026

Eight page filter declarations now satisfy generated SavedViewKeys. Their positive build exposed two previously omitted saved filters, projects.ids and workspace tasks/board.createdBy; these are now retained. PlatformViewKeysTests passes 3/3 against real PostgreSQL. The combined focused registry/contract/view/record run passes 44/44; this count includes separate packet 043 tests and is not the full API suite.

Actual TypeScript CompilerHost controls compile the current project graph without diagnostics, then virtually remove a text key, field.RoleLabel, tasks.onHold, or rename OpenRoleRow.plannedHours. Each fails at its actual page or typed fixture. Maintained source bytes are unchanged by these controls. Detailed local logs and the reproducible control script are under ci-artifacts/platform-recovery/negative-compiler-controls.*.

The synthetic smoke runner refuses an occupied port before starting an API and terminates only its own process groups. Its small occupied-port regression passes and is wired into the required web job. No operational database, hosted flag, push or deployment was changed.
