# Plan: Typed Contract and Entity Registry

Implements §44 (FR-PLAT-01 to FR-PLAT-21) with defaults D1 to D14. Base: `main` after PR #37 merges. Code citations are at `48586029`. No slice adds an endpoint path, table, column or migration.

## Approach

Replace each hand-kept copy of one contract with one source plus a test that fails when the copies would have drifted. Order: guards first, then derivations, then conversions.

1. **Ratchet first.** `tools/ratchet.py` records today's counts so nothing below can add more of the old patterns once a replacement exists.
2. **Registry, describe then derive.** Add the registry with entries copied from today's hand registries and commit JSON snapshots of their output. Then swap one hand registry per slice for its derivation, each proven equal by test. Fixes the registry exposes (`onHold`, `groupBy`, page-mode names, labels) land as named additions.
3. **Contract, scaffold then convert.** Golden `openapi.json` through the existing `HubFactory`, generated types, the typed helper and the shape recorder land with nothing converted. Open roles converts first as the proof; other feature files follow one per change.
4. **Invalidation map.** `keys.ts` becomes the body of `useProjectRefresh` with the same signature, then absorbs direct calls page by page.

Reused rather than built: the test host (`tests/Hub.Tests/Api/Factory.cs:22-51`) for the document and the recorder; `api()` (`web/src/lib/api.ts:33-55`) as the only fetch path; the existing OpenAPI mapping (`src/Hub.Api/Program.cs:80,168-169`); the `Col` record (`src/Hub.Api/Features/Reports.cs:13`); the Python test pattern already in CI (`tools/test_*.py`); Playwright and axe already installed. The only new dependency is openapi-typescript 7.13.0 (MIT, development only). No generator runs `Program.cs`, because startup migrates and seeds (`Program.cs:190-203`).

## File boundaries

| Area | Files |
|---|---|
| Registry | New `src/Hub.Api/Records/Registry.cs`, `src/Hub.Api/Records/ScreenLists.cs` and one `src/Hub.Api/Records/<Entity>.cs` per audited type. Derivation edits: `src/Hub.Api/Infrastructure/Audit.cs`, `src/Hub.Api/Features/Activity.cs`, `ListExports.cs`, `Views.cs`, `Search.cs`, and the 14 feature files with static `Col[]` arrays (`Allocations.cs`, `Changes.cs`, `DecisionExtras.cs`, `DesignBasis.cs`, `Handoffs.cs`, `ListExports.cs`, `Meetings.cs`, `Portfolio.cs`, `Readiness.cs`, `Registers.cs`, `Reviews.cs`, `Submissions.cs`, `Time.cs`, `WeeklyCommitments.cs`) |
| Registry tests | New `tests/Hub.Tests/Api/RegistryTests.cs`; snapshots under new `tests/Hub.Tests/Snapshots/registry/`; additions to `tests/Hub.Tests/Api/ViewsTests.cs` and `ActivityTests.cs` |
| Labels and text | `web/src/gen/registry.gen.ts` (generated), `web/src/i18n/en.ts`, `web/src/lib/i18n.ts`, call sites of run-time-built keys in `web/src` |
| Page modes | `web/src/pages/projects/Allocations.tsx`, `web/src/pages/Planner.tsx`, `web/src/pages/projects/Decisions.tsx`, `web/src/pages/Workload.tsx` (link at line 270), the eight page key lists named in FR-PLAT-06, `web/tests/counts.cjs` (its layout-parameter set) |
| Contract | `src/Hub.Api/Program.cs` (schema transformers only if needed), `src/Hub.Api/Features/OpenRoles.cs`, `src/Hub.Api/Features/Workload.cs`, then one `src/Hub.Api/Features/*.cs` per conversion slice with its client callers; new `tests/Hub.Tests/Api/ContractTests.cs`, `tests/Hub.Tests/Api/ShapeRecorder.cs`, `tests/Hub.Tests/Snapshots/shapes.json`; `tests/Hub.Tests/Api/Factory.cs` (adds the recorder) |
| Client contract | `web/src/gen/openapi.json`, `web/src/gen/api.d.ts` (generated), `web/src/lib/api.ts`, `web/package.json` (`gen`, `typecheck:tests`, `test:smoke-live`, `test:keys`, the devDependency), new `web/tests/tsconfig.json`, `web/tests/resources.cjs`, new `web/tests/smoke-live.cjs` |
| Invalidation | New `web/src/lib/keys.ts` and `web/src/lib/keys.test.ts`; `web/src/hooks/data.ts`; page files as their calls move |
| Tooling and CI | New `tools/ratchet.py`, `tools/ratchet.json`, `tools/test_ratchet.py`, `tools/shapes.py`, `tools/test_shapes.py`; lines in packet 039's `tools/ci_steps.txt`; `.github/workflows/ci.yml` only for the PostgreSQL service S13 adds to the `web` job; `web/src/gen/**` marked generated in the `.gitattributes` packet 039 adds |

Unrelated edits in these files are preserved. `src/Hub.Domain` is not touched.

## Constitution Check

| Principle | Result |
|---|---|
| I. Coordination first | PASS. No new product feature; existing screens keep their behaviour and gain labels and saved-view fixes. §30 is unchanged. |
| II. Deterministic and explainable | PASS. No AI, ML or LLM. No rule, threshold or calculation changes; thresholds stay in settings. |
| III. One accountable owner | PASS. No ownership model change. |
| IV. Specification is the source of truth | PASS. §44 is added in the same change set; the FR-PLN-24 wording amendment is requested, not edited here. Canonical names stay verbatim: `value.*` keys are typed against `Vocabulary.cs`. |
| V. Traceable by construction | PASS. The `SaveChanges` hook, append-only triggers and row versions are unchanged. The derived audit allow-list is proven equal to today's before the hand copy goes. |
| VI. Secure by default | PASS. The shape recorder lives in the test project only. `/api/v1/openapi.json` keeps today's authentication rule (`Program.cs:168-169`). Item-history and export-name lookups keep the visibility boundary (FR-PLAT-05). The real-API suite signs in through the development picker on loopback in CI only; no secret is added. |
| VII. Simple enough for a small team | PASS. No service, broker, datastore, engine or custom-field builder. The registry is plain typed C# that replaces hand copies, with no expression language. One development dependency, types only. Complexity Tracking does not apply; packet 043 carries the entry for generic renderers. |

## Checks to run

- `dotnet build Hub.slnx -c Release` and `dotnet test Hub.slnx -c Release` with the coverage gate (`python3 tools/coverage_gate.py coverage`).
- `python3 tools/shapes.py check` after a full test run; `python3 tools/ratchet.py`; `python3 tools/test_ratchet.py`; `python3 tools/test_shapes.py`.
- In `web`: `npm run build` (runs `gen`, then `tsc -b`), `npm run lint`, `npm run typecheck:tests`, `npm run test:keys`, the seven mocked suites in CI, and `npm run test:smoke-live` against the built API.
- `git diff --exit-code web/src/gen` after the build in CI.
- Integrator only: `python3 tools/build_spec.py --check` and `python3 tools/trace_spec.py --check`.
