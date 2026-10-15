# Feature Specification: Record Engine

**Packet**: 043 | **Feature directory**: `specs/043-record-engine` | **Date**: 2026-10-14
**Status**: Specified, not started. Engine slices also wait for Jay's constitution VII approval (D1).
**Input**: Jay's 2026-10-14 instruction to build everything in the 2026-10-14 build-approach report (kept outside the repository): its option 2 (one registry driving generic lists and panels) as narrowed by its hybrid, item 5: generic RecordList and RecordPanel for list- and panel-shaped screens only, after a parity spike on Risks and open roles, with a shared table as the fallback.
**Source**: product specification §47 (§47.1 to §47.9) in `spec-parts/22-record-engine.md`. Related contracts: §23.4 (shared data table, side panel, filter bar), §13.0 (global UI conventions), §13.13 (Risk Register), §18.4 (saved views, FR-VIEW-04), §40.2 (open roles, FR-ROLE-01 to FR-ROLE-05), §41 (calm layout rules), §44 (entity registry and typed contract, packet 040), §43 (flags, `specs/DECISIONS.md`, merge rules, packet 039) and §46 (build stamp and eager registrations, packet 042).

## User outcome

Nobody sees a change when a screen moves to the engine: the Risks register, the Risk panel and the open-roles list look, read, filter, sort, export and respond to the keyboard exactly as before, for every role. What changes is how they are built. One declaration per entity drives the list and the panel, so a later feature no longer needs a hand-kept copy of its columns, filters, export and permission display in each screen. When Jay turns the packet's flag on, the Risks register gains saved views, and open roles gain sorting, a detail panel with History, and export. If the spike shows the engine is not simpler than what it replaces, the engine is reverted, the flagged additions are kept on the restored screens, and lists move onto one shared table instead.

## Scope

1. **One registry, read by the renderers** (§47.2, FR-REC-01, FR-REC-02, FR-REC-03, FR-REC-04): display parts added to the §44 per-entity C# files; `GET /api/v1/meta` with the build identifier as ETag; `GET /api/v1/records/{type}/{id}` returning the existing detail plus a per-field and per-transition descriptor from the existing permission and workflow functions; rows and exports from each list's existing endpoints; `GET /api/v1/lists/{code}/export` only for a list with no export today.
2. **The renderers** (§47.3, FR-REC-05, FR-REC-06, FR-REC-07, FR-REC-08): RecordList built only from `useTable`, `RegisterCards`, `GroupRow`, `FilterBar`, `ViewMenu`, `ExportMenu` and the other existing parts; RecordPanel built from `PanelHead`, `FieldGroup`, `FieldRow`, the inline editors, `TabBar` and `HistoryList`; named renderers (a closed enumeration, checked by the build) and named slots as the only escape hatch; refresh through the §44 `keys.ts` map.
3. **The parity run** (§47.4, FR-REC-09): a real-API comparison of a base build and a candidate build on one API and database, for five personas at desktop and phone widths, covering the accessible tree, filters, saved views, export files, keyboard path, panel editability and transitions, API requests and the accessibility scan. It gates every conversion PR.
4. **The spike** (§47.5, FR-REC-10, FR-REC-11, FR-REC-12): Risks list, Risk panel and open-roles list converted in three PRs, each deleting the old component; a measured PASS or KILL verdict against seven rules; additions behind `flag.record_engine_additions`.
5. **Adoption** (§47.6, FR-REC-13): after a confirmed PASS, one list- or panel-shaped screen per PR in the D5 order, parity-only, under four size and slot rules; a screen that misses a rule stays as it is.
6. **Fallback and ratchet** (§47.7, FR-REC-14, FR-REC-15): on KILL, one revert PR that keeps the flagged additions, then `RegisterTable` becomes a shared `DataTable` and hand-rolled tables move onto it one per PR; the §44 ratchet's `<table>` counter becomes a failing check, so the count only goes down.

Acceptance criteria are in §47.8; decisions and deferred proposals are in §47.9.

## Non-goals

- Converting hybrid screens (Tasks, WorkspaceTasks and My Work task lists, Issues, the Decisions register views, Handoffs, Meetings, Submissions, Design basis, the allocation detail sheet, the Task panel) or bespoke screens (Planner, Workload and its Unfilled roles grid, Gantt, Timeline, Calendar, Coordination views, Readiness, Dashboard, Home, Board, Search, Reports and the setup flows). See §47.1.
- Generic create or update endpoints. Every write stays on the entity's existing endpoint (D7).
- Custom fields, custom statuses, user-defined lists or rules, any runtime expression language, a new service, a new runtime package, or definitions stored in the database (§30, constitution VII).
- New surfaces on adopted screens beyond the spike's flagged additions (D4).
- New search groups (deferred in §47.9), the §44 label families, counts that open their lists (§49), row-hover quick actions (§50), live updates (§46), and any AI feature (constitution II).
- Putting any existing screen behind a flag. A conversion replaces the component outright once the parity run proves it unchanged.

## Decisions (defaults)

| # | Decision | Default |
|---|---|---|
| D1 | Approve the plan's Complexity Tracking entry (constitution VII) | Not approved: S01 proceeds; engine slices wait; the fallback starts only on an explicit decline. |
| D2 | Spike screens | Risks list and panel; open-roles list on the Allocations tab. |
| D3 | Verdict | Measured by FR-REC-11, recorded in `acceptance.md`; Jay confirms by setting 043-D3's status. |
| D4 | Spike additions | Behind `flag.record_engine_additions`, off: Risks saved views; open-role sorting, panel with History, export. Kept on KILL; if D1 is declined, open-role export returns to §51.13's deferred list. |
| D5 | Adoption order | Milestones list, Milestone panel, Holidays, reference data, Users, project Team, WorkspaceTeam, Vacation, Allocations list, Reviews and Changes, Deliverables list, Deliverable panel, Decision panel, Meeting action panel, Project List, Portfolio list, project Activity, org Activity. |
| D6 | Hybrid and bespoke screens | Not converted. |
| D7 | Generic writes | Not built. |
| D8 | Fallback table | `RegisterTable` becomes `DataTable` in `components/hub/registers.tsx`; order as listed in §47.9. |
| D9 | Panel registration | One explicit `PANELS` line per converted type in `web/src/app/registrations.tsx` (§46). |
| D10 | Merge tier | Spike PRs Tier B (Jay merges); adoption and fallback PRs Tier A when parity passes with no test assertion changed. |

Report decision 11 ("run the record-engine spike") becomes D1 here, because constitution VII requires Jay's approval of the Complexity Tracking entry before any engine code. RPT-11 follows it: the spike is not run until D1 is approved.

## Acceptance

AC-REC-01 to AC-REC-16 (§47.8):

- Definitions and descriptor: AC-REC-01, AC-REC-02, AC-REC-03.
- Spike parity: AC-REC-04 (Risks list), AC-REC-05 (Risk panel), AC-REC-06 (open roles), AC-REC-07 (existing tests unchanged), AC-REC-08 (renderer and slot names), AC-REC-11 (requests).
- Spike additions and verdict: AC-REC-09, AC-REC-10.
- Adoption: AC-REC-12.
- Fallback and ratchet: AC-REC-13, AC-REC-14, AC-REC-15.
- Boundaries: AC-REC-16.

Existing contracts that must still pass unchanged on the screens they govern: FR-ROLE-01..05 with AC-ROLE-01, AC-ROLE-02, AC-ROLE-03, AC-ROLE-04 and AC-ROLE-05, FR-VIEW-04, AC-CALM-06, AC-CALM-07 and AC-CALM-08, RSK-02 and RSK-03, and the §13.0 keyboard, filter, sorting and responsive conventions.

## Depends on

- **Packet 039 (§43)**: protected trunk and merge tiers (D10), `src/Hub.Domain/Flags.cs` with the flag-off and flag-on checks, `specs/DECISIONS.md`, `acceptance.md` and the human-only list, the parallel CI jobs and the real-API nightly run.
- **Packet 040 (§44)**: the registry entries for Risk and OpenRole in `src/Hub.Api/Records/` with their superset and completeness tests, the typed contract, golden `openapi.json`, `web/src/gen/` files and typed helper (before S02); `keys.ts` (before S03); the real-API browser suite that S01 sits beside; Allocations `tab=roles` with old `view=roles` links still accepted (before S05); the ratchet's `<table>` counter that S08 or S28 turns into a failing check.
- **Packet 042 (§46)**: the build identifier used as the `/meta` ETag (before S02); `web/src/app/registrations.tsx` with its boot suite and deep-link test (before S04).
- **Packet 041 (§45)**: server can-flags are read where a converted screen has them; not a blocker.
- Adoption (S08 onward) starts only after a PASS verdict that Jay confirms. The fallback (S27 onward) starts only on KILL or if Jay explicitly declines D1.

## Flag

`flag.record_engine_additions` (§43, declared in `src/Hub.Domain/Flags.cs`): off by default; gates only the spike's additions (FR-REC-12). The conversions themselves are not flagged. Cleanup after Jay turns it on in review and pilot is slice S26.

## Wording rule

Code, interface strings, specification text, commit messages and this packet's records name no third-party product.
