# Plan: Record Engine

Implements §47 (packet 043). No slice is authorised by this plan; each slice is one PR that Jay or the §43 merge rules accept. Engine slices (S02 to S07, S08 to S26) also wait for Jay's approval of the Complexity Tracking entry below (D1).

## Approach

Build the smallest engine that can replace a real screen, prove it on Risks and open roles, and keep it only if the numbers say it is simpler.

- **Server.** No new registry. Display parts go into the §44 per-entity files under `src/Hub.Api/Records/`, and the renderer and slot enumerations sit beside them, emitted to the client with the §44 generated files. One new feature file, `src/Hub.Api/Features/RecordEndpoints.cs`, maps three read endpoints in `src/Hub.Api/Features/Modules.cs`: `GET /api/v1/meta`, `GET /api/v1/records/{type}/{id}` and `GET /api/v1/lists/{code}/export`. The descriptor calls the functions the existing endpoints call: `Permissions.EditRegisterItem` and `Workflow.RiskStep` as in `RegisterEndpoints.GetRisk` (`src/Hub.Api/Features/Registers.cs:209-229`). The open-role panel is read-only and returns the row `OpenRoleEndpoints.List` builds (`src/Hub.Api/Features/OpenRoles.cs:53-60`), so its row actions keep today's `canManage` (`OpenRoles.cs:41-42`). The export endpoint reuses `ExportFile.Send` (`src/Hub.Api/Features/Reports.cs:614`) and the `Col` record (`Reports.cs:13`). Rows never pass through a new endpoint: RecordList calls each list's existing path (`/projects/{id}/risks`, `Registers.cs:50`; `/projects/{projectId}/open-roles`, `OpenRoles.cs:20`).
- **Client.** Two new files. `web/src/components/hub/records.tsx` holds `useMeta`, a `useRecordList` hook shaped like today's `useTable` (`web/src/components/hub/table.tsx:15`, returning the toolbar parts and the body so a page can keep its own toolbar), the `RecordList` page wrapper and the renderer map. `web/src/components/hub/record-panel.tsx` holds `RecordPanel`. Both compose existing parts only: the RisksTab and RiskPanel code they replace (`web/src/pages/projects/Registers.tsx:189-229`, `:712-766`) is already made of them. The renderer map is typed `Record<Renderer, ...>` over the generated enumeration, so `tsc -b` fails on a missing renderer. Pages pass their slots as props, which avoids import cycles between `components/hub` and `pages`.
- **Tests.** `web/tests/record-parity.cjs` follows the two-build comparison already used by `web/tests/calm-acceptance.cjs` (`CALM_BASE_UI_URL`) and runs beside the §44 real-API browser suite, creating its own records through the API. `tests/Hub.Tests/Api/RecordEngineTests.cs` covers the endpoints against real PostgreSQL through the existing `HubFactory` (`tests/Hub.Tests/Api/Factory.cs`).
- **Fallback.** `RegisterTable` (`Registers.tsx:162-187`) already wraps `useTable`, `RegisterCards`, `GroupRow` and `TableRegion`. Moving it to `components/hub/registers.tsx` as `DataTable` is the whole shared table.

## File boundaries

| Area | Files |
|---|---|
| Server, new | `src/Hub.Api/Features/RecordEndpoints.cs` |
| Server, changed | `src/Hub.Api/Features/Modules.cs` (one `Map` line); `src/Hub.Api/Records/` files for each converted entity (display parts only) and the renderer and slot enumerations |
| Client, new | `web/src/components/hub/records.tsx`, `web/src/components/hub/record-panel.tsx` |
| Client, changed (spike) | `web/src/pages/projects/Registers.tsx` (RisksTab, RiskPanel removed; slots passed), `web/src/pages/projects/Allocations.tsx` (OpenRoles table removed; slots passed), `web/src/components/hub/export.tsx` (extra entries, S06), `web/src/app/registrations.tsx` (`PANELS` lines, §46), `src/Hub.Domain/Flags.cs` (one entry, S06), `web/src/components/hub/panel-host.tsx` (a flag-named entry counts as absent while its flag is off, S06), `web/src/i18n/en.ts` (added keys only) |
| Client, changed (adoption or fallback) | One page file per slice, as named in tasks.md; `web/src/components/hub/registers.tsx` (`DataTable`, fallback only) |
| Tests, new | `tests/Hub.Tests/Api/RecordEngineTests.cs`, `web/tests/record-parity.cjs`, `web/package.json` script `test:record-parity` |
| Tests, changed | Mocked browser suites (`web/tests/resources.cjs` and any suite that loads a converted screen): an added `/meta` fixture only; `tools/ci_steps.txt` (the parity line beside the §44 real-API suite); `tools/ratchet.py` and `tools/ratchet.json` (the `<table>` counter turned to failing, S08 or S28) |

Unrelated edits in these files are preserved. No migration, no new package, no change to any existing endpoint body.

## Constitution Check

| Principle | Result |
|---|---|
| I. Coordination First | PASS. The screens keep answering who owns which risk and which roles are unfilled. Additions are small and serve the same lists. |
| II. Deterministic and Explainable | PASS. No AI. Field and transition reasons come from the existing `Hub.Domain` functions; no rule or threshold moves to the client. |
| III. One Accountable Owner | PASS. No ownership change. |
| IV. Specification Is the Source of Truth | PASS. §47 is written before code; every slice cites FR-REC and AC-REC IDs; behaviour of existing screens is unchanged and proved by the parity run. |
| V. Traceable by Construction | PASS. Every write still goes through the entity's existing endpoint, `Http.CheckVersion` and the `SaveChanges` audit hook. The open-role History tab reads the existing log (`Activity.cs:100` already resolves OpenRole). |
| VI. Secure by Default | PASS. The server computes the descriptor and applies the same access checks; `/meta` holds no data; FR-ROLE-05 holds for every addition. |
| VII. Simple Enough for a Small Team | CONDITIONAL. Still one monolith, one database, no new service or package. Generic renderers driven by server-described definitions are speculative infrastructure, so the entry below needs Jay's approval before S02 (D1). The fallback path does not. |

Open §34 decisions: none.

## Complexity Tracking

| Addition | Why it is needed | Simpler alternative, and why it is not enough | Bounds |
|---|---|---|---|
| RecordList and RecordPanel driven by display parts in the §44 registry, with `GET /api/v1/meta`, `GET /api/v1/records/{type}/{id}` and `GET /api/v1/lists/{code}/export` | Each list and panel hand-keeps its columns, filters, export and permission display. Seven of packet 036's 18 defects were seams between such copies, and open roles took about 35 files yet never got export, sorting or History. | `DataTable` over `useTable` (the fallback, FR-REC-14) shares the table markup only. Columns, filters, panels, exports and permission display stay hand-kept per screen. It is the path taken if the spike fails or this entry is declined. | Developer-written C# only; no runtime expression strings; no user-defined fields, statuses, lists or rules (§30); no service, package, migration or definitions table; no generic writes; list- and panel-shaped screens only; each conversion shorter than what it deletes; KILL rules in FR-REC-11; reverting the spike PRs removes it. |

## Checks to run

- `dotnet test` (full suite; `RecordEngineTests` new; `RegistersApiTests`, `OpenRoleApiTests`, `ViewsTests`, `ActivityTests`, `PermissionMatrixTests`, `PermissionSweepTests` unchanged), with `tools/coverage_gate.py`.
- `npm run build` (includes the exhaustive renderer map) and `npm run lint` in `web`.
- `npm run test:record-parity` on every conversion PR, flag off; with the flag on in S06.
- The CI mocked browser suites and the §43 nightly real-API suites (`calm-acceptance`, `calm-clicks`, `counts`, `clutter`).
- `tools/ratchet.py` (§44) and the §43 flag retirement and pair checks.
- `python3 tools/build_spec.py --check` and `python3 tools/trace_spec.py --check` are run by the integrator.
