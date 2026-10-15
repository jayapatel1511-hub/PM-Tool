# Feature Specification: Typed Contract and Entity Registry

**Packet**: 040 | **Feature directory**: `specs/040-typed-contract-and-registry` | **Date**: 2026-10-14
**Status**: Specified, not started.
**Input**: Jay approved the 14 October build-approach report on 2026-10-14 and directed building all of it. This packet is items 2 and 3 of the report's hybrid (report section 4.3) and the seam defects of report section 2.3. Features are fixed; this packet adds and never removes, hides, narrows or freezes anything.
**Source**: product specification §44 (§44.1 to §44.8) in `spec-parts/19-typed-contract-and-registry.md`. It finishes promises already in §23.4 (generated typed client, per-project cache keys) and §25.1 (OpenAPI document generated from code), and derives the per-entity allow-lists of §20.4. Related contracts: §13.14 and FR-AUD-01, FR-AUD-02 (Activity History); §18.4, §36.7, FR-VIEW-04 and FR-VIS-08 (saved views); §40.2 and §40.5 with FR-ROLE-04, FR-ROLE-05 and FR-GRP-02 (open roles, grouping); FR-PLN-24 (planner saved views); FR-SRCH-01 and FR-RPT-01 (search groups, export columns); §22 (localisation). Code citations are at `48586029`.

## User outcome

People see the same screens, plus three fixes: Activity History names every item type and field instead of showing raw keys; saved views keep the On Hold filter, the Workload and Planner grouping and the Allocations and Planner mode; and old links keep opening the same mode. Agents and Jay get a build where a missing registration, label or response field fails CI instead of surfacing in an acceptance sweep. Seven of packet 036's eighteen defects were of that kind.

## Scope

1. **One entity registry** (§44.2: FR-PLAT-01, FR-PLAT-02, FR-PLAT-03, FR-PLAT-04, FR-PLAT-05, FR-PLAT-06). Plain C#, one file per audited entity under `src/Hub.Api/Records/`. It becomes the source of `AuditRules.Fields` (`src/Hub.Api/Infrastructure/Audit.cs:34-128`), the Activity item-history lookup and names (`src/Hub.Api/Features/Activity.cs:86-103,150-183`), export parameter names (`src/Hub.Api/Features/ListExports.cs:73`), saved-view keys (`src/Hub.Api/Features/Views.cs:23-48`), export columns and search groups (`src/Hub.Api/Features/Search.cs:14-15`). Superset snapshot tests mean a derived list can never lose an entry. A reflection test fails on a missing entry, field, lookup or reason. The registry emits typed key unions for the `itemType.*`, `field.*` and `value.*` label families and each list's saved-view keys.
2. **Seam fixes** (§44.3: FR-PLAT-07, FR-PLAT-08, FR-PLAT-09, FR-PLAT-10, FR-PLAT-11). `onHold` in task saved views; `groupBy` in Workload and Planner saved views; Allocations `tab=roles`, Planner `layout=list|projects` and Decisions `tab=log` in place of `view=`, with old links still accepted and a reserved-name check; the missing item-type and field labels; `en.ts` typed by its keys; and a check that stops new text drift between `Text.cs` and `en.ts` while keeping today's texts.
3. **Typed contract** (§44.4: FR-PLAT-12, FR-PLAT-13, FR-PLAT-14, FR-PLAT-15, FR-PLAT-16, FR-PLAT-17, FR-PLAT-18). Typed results one feature file per change, starting with open roles; a golden `openapi.json` produced through the existing test host; openapi-typescript types; one typed helper over the existing `api()`; a response-shape recorder in the test project; typed mocks; and one real-API browser smoke suite in CI.
4. **Invalidation map** (§44.5: FR-PLAT-19). `web/src/lib/keys.ts` maps item types to query keys; `useProjectRefresh` keeps its signature and its 1.5 s second pass; the map only adds invalidations.
5. **Ratchet and stop rule** (§44.6: FR-PLAT-20, FR-PLAT-21). `tools/ratchet.py` keeps legacy-pattern counts from rising once their replacement exists; after five features on the platform, the figures decide whether the long tail continues.

## Today's behaviour stays the default

- Every derivation returns what the hand copy returns today, in the same order and format. Activity rows, export columns, search groups and response JSON are compared by test.
- The item-history lookup resolves exactly today's 14 types, Project included. Further types come only with a history surface and its privacy rule.
- Saved views without the new keys open as today: default grouping, Allocations list, Planner grid, Decisions register.
- Existing label texts are unchanged; only missing labels are added. Differing server and client texts stay as they are.
- `useProjectRefresh` callers are unchanged and the 1.5 s pass stays. Invalidations only grow.
- No endpoint path, table, migration, rule, threshold or permission changes. Nothing is put behind a flag.

## Non-goals

- Server-computed load bands, `can` flags and the open-role owner rule in `Permissions.cs` (packet 041, §45).
- Route splitting, the build stamp and live updates (packet 042, §46). The 1.5 s pass stays; §46 does not remove it.
- Generic list and panel renderers, and the shared table (packet 043, §47). The registry leaves room for them and builds neither.
- CI sharding, feature flags, PR size limits, the test-count ratchet and the record format (packet 039, §43).
- Search for risks, issues, meeting actions and open roles, and unfilled roles in the Workload export: recorded as deferred proposals in §44.8. Saved views for Risks are added by §47 behind its flag (FR-REC-12, packet 043).
- A TypeScript port of the server: deferred behind the triggers in §44.8, and it would need a constitution VII entry.
- A public API. The generated document and types are internal; §30 is unchanged.
- Any AI, ML or LLM feature (constitution II).

## Decisions (defaults apply until Jay decides)

| # | Decision | Default |
|---|---|---|
| D1 | How the registry finds entries | Reflection in one place; each entity file adds itself. |
| D2 | New page-mode names | Allocations `tab=roles`; Planner `layout=list` or `layout=projects`; Decisions `tab=log`. |
| D3 | Legacy `view=` mode links | Accepted indefinitely, rewritten in place. |
| D4 | Server and client texts that differ today | Both kept; aligning a pair is Jay's copy ruling per key. |
| D5 | Text of added labels | Plain English from the specification's names; Jay may change any. |
| D6 | New response fields | Allowed; shape snapshot regenerated in the same change. |
| D7 | Endpoint coverage below about 60% | Tests first for uncovered endpoints; convert file by file where covered. |
| D8 | Pace of the long tail | Ratchet now, with the stop rule. |
| D9 | Real-API suite sign-in | Development picker on loopback against the CI database. |
| D10 | `keys.ts` test runner | Node's built-in runner; mocked browser suite as fallback. |
| D11 | More item-history lookups | None here; each with its history surface. |
| D12 | Converting files a feature change touches | Allowed within touched files once constitution 1.1.0 is in effect. |
| D13 | Page keys saved views drop today | Filters, sort, grouping, columns and modes are kept; paging, panel and access-reason keys are declared not kept. |
| D14 | Visible changes without a flag | Labels, saved-view key fixes and mode names ship unflagged as corrections; S05, S06 and S09 are Tier B. |

## Acceptance

AC-PLAT-01 to AC-PLAT-21, as written in §44.7:

- Registry: AC-PLAT-01, AC-PLAT-02, AC-PLAT-03, AC-PLAT-04, AC-PLAT-05.
- Seam fixes: AC-PLAT-06, AC-PLAT-07, AC-PLAT-08, AC-PLAT-09, AC-PLAT-10, AC-PLAT-11.
- Typed contract: AC-PLAT-12, AC-PLAT-13, AC-PLAT-14, AC-PLAT-15, AC-PLAT-16, AC-PLAT-17, AC-PLAT-18.
- Invalidation map: AC-PLAT-19.
- Ratchet and stop rule: AC-PLAT-20, AC-PLAT-21 (human check).

The open-roles conversion is the first proof: seeded defects of the 036-D1, 036-D3, 036-D5 and 036-D10 kinds must each fail the build or CI (AC-PLAT-03, AC-PLAT-06, AC-PLAT-10, AC-PLAT-14) while every existing test and recorded shape stays identical.

## Depends on

- PR #37 merged into `main`, so this work lands on the one line of record.
- No slice waits on packet 039. Where packet 039's CI jobs exist, the new steps join the matching job, and the shape check runs after all API test shards. The size limit cited in FR-PLAT-12 is packet 039's (§43).
- Packet 041 edits `OpenRoles.cs` for the owner rule; whichever of 041's slice and this packet's S12 lands second rebases on the other.
- Packets 042 and 043 build on this packet: §46 plugs live updates into `keys.ts`, and §47 renders lists and panels from the registry.

## Flag

None. The visible changes are corrections of existing behaviour that ship unflagged under D14, in Tier B slices; existing features are never flagged.

## Wording rule

Code, interface text, specification text and commit messages name no third-party product.
