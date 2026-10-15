## 47. Record Engine Amendment

Approved scope: Jay approved the build-approach report and directed building all of it on 2026-10-14; features are fixed and additive only. Packet 043 delivers this section. It takes the report's generic list and panel renderers (option 2), keeps them to list- and panel-shaped screens as the report's hybrid does, proves them first with a parity spike on Risks and open roles, adopts them one screen at a time only if the spike passes, and falls back to a shared table if it does not. This section records the product and build contract; nothing is built yet. Under constitution VII the renderers count as speculative infrastructure, so the plan's Complexity Tracking entry needs Jay's approval before any engine code is written (D1).

Where this section adds to §23.4 (the shared data table, side panel and filter bar), §13.0 (global UI conventions), §13.13 (Risk Register), §18.4 (saved views), §40.2 (open roles on the Allocations tab) and the entity registry of §44, this section governs. It changes no endpoint path, payload, permission, rule, status name or count. It removes, hides, narrows or freezes no screen, column, filter, action, saved view, export or route. No existing screen is put behind a flag: a conversion replaces a component outright, and only after a parity run proves the screen unchanged. Surfaces the spike adds to a screen that lacked them ship dark behind `flag.record_engine_additions` (§43), so today's screens stay the default.

### 47.1 Scope and boundary

In scope: one set of definitions in the §44 registry read by two client renderers, RecordList and RecordPanel (§47.2, §47.3); a real-API parity run that compares a screen before and after a change (§47.4); the spike on Risks and open roles with measured pass and kill rules (§47.5); adoption on list- and panel-shaped screens after a pass (§47.6); and the shared-table fallback with a raw-table ratchet (§47.7).

Outside this section:

- Hybrid screens: the Tasks list, the WorkspaceTasks and My Work task lists, Issues, the Decisions register views, Handoffs, Meetings, Submissions, Design basis, the allocation detail sheet and the Task panel. Each keeps its own components.
- Bespoke screens: Planner, Workload (including its Unfilled roles week grid), Gantt, Timeline, Calendar, the Coordination views, Readiness, Dashboard, Home, Board, Reassign work, the template and project setup flows, Preferences, Operations, Search and Reports (which is already server-described).
- Generic create or update endpoints. Every write stays on the entity's existing endpoint.
- Custom fields, custom statuses, user-defined lists or rules, a runtime expression language, a new service, a new runtime package and a database table of definitions (§30).
- New search groups (deferred, §47.9), the label families of §44, counts that open their lists (§49), row-hover quick actions (§50) and live updates (§46).
- Any AI, machine-learning or LLM feature (constitution II).

### 47.2 One registry, read by the renderers

- **FR-REC-01.** The record engine MUST read its list and panel definitions only from the §44 entity registry (`src/Hub.Api/Records/`, one plain C# file per entity). It adds display parts to those same files. It never keeps a second registry, a JSON or YAML definition file, or a database table of definitions. A list definition names the existing endpoint that returns its rows, its existing export endpoint if it has one, its columns, its filters, its default sort and tie-break, its grouping, its create action and its row actions. A column carries its row path, header text key, renderer, whether it is sortable, whether it is optional or fixed, and the browser storage key the screen's column chooser uses today. A filter carries its URL key, type, label key and options source. A panel definition names the header facts, the field groups and their fields, the tabs (links, comments, history) and the transitions with any dialog they need. A field carries its key, type, editor and the body key of the entity's existing update endpoint. Definitions are developer-written data plus named renderers and slots (FR-REC-07). No definition holds a string that is evaluated at runtime. No user can add a field, status, list or rule.

- **FR-REC-02.** `GET /api/v1/meta` MUST return every list and panel definition as data: no rows, no permissions and no personal data. Its ETag is the build identifier (§46). The client reads it once per build, keyed by the identifier compiled into the app, and caches it. A converted screen makes the same row, detail and permission requests as before, plus at most that one cached read.

- **FR-REC-03.** `GET /api/v1/records/{type}/{id}` MUST return, for a type that has a panel definition, the entity's existing detail payload unchanged (for a type without a detail endpoint, such as an open role, the row its list endpoint returns) plus a descriptor. For each panel field the descriptor says whether this viewer may edit it and, if not, the reason text. For each transition it gives the target status, whether it is allowed, the reason if not, and the dialog slot it needs, if any. The descriptor is computed by the same `Hub.Domain` permission and workflow functions the existing endpoints call: for a risk, `Permissions.EditRegisterItem` and `Workflow.RiskStep`. The open-role panel is read-only: its descriptor marks every field not editable and lists no transitions, because editing and cancelling stay row actions with their existing dialog. The endpoint makes the same access check as the existing read and answers a viewer who cannot view the project with the same status code, so §8.7 and FR-ROLE-05 hold.

- **FR-REC-04.** RecordList MUST read rows from the list's existing endpoint and export through the list's existing export endpoint. Paths, parameters and payloads stay as they are. A list with no export today MAY gain one through `GET /api/v1/lists/{code}/export`. That endpoint runs the list's existing query function with the request's filters and the viewer's access, and writes the declared columns through the existing export writer (`ExportFile.Send`) under the existing row cap. Such an export is an addition under FR-REC-12.

### 47.3 The renderers

- **FR-REC-05.** RecordList MUST be built only from components the app already has: `useTable` and `Column` (`components/hub/table.tsx`); `RegisterCards`, `GroupRow`, `SELECTED_ROW` and `TITLE_LINK` (`components/hub/registers.tsx`); `FilterBar`, `ActiveFilters`, `ChipToggle`, `TableRegion`, `Empty`, `Loading`, `ErrorBanner` and `Page` (`components/hub/common.tsx`); `ViewMenu` (`components/hub/views.tsx`); and `ExportMenu` (`components/hub/export.tsx`). It works as a whole page and as a section inside a page that keeps its own toolbar. It keeps every §13.0 and §41 behaviour the screen has today: one toolbar row, the Filters popover with removable chips, filter state and sort in the URL, the column choice in the URL and in the same browser storage key, `aria-sort` on sortable headers, a screen-reader caption, phone cards below 768 px, the selected-row cue, empty states with their action, skeleton rows, and error banners with retry. It adds no interface library.

- **FR-REC-06.** RecordPanel MUST be built from `PanelHead` and `FieldGroup` (`components/hub/registers.tsx`), `FieldRow`, `InlineText`, `InlineDate`, `InlineSelect`, `InlinePerson`, `TabBar` and `HistoryList` (`components/hub/fields.tsx`), and the comments slot. It picks each field's editor from the field type. It disables a field the descriptor marks not editable and shows the reason as its title. It saves one changed field at a time through the entity's existing update endpoint with the row version (G-06, G-07), and shows a version conflict exactly as the screen does today. It runs a transition through the entity's existing transition endpoint, or through the dialog slot the descriptor names. Each converted type is registered in `PANELS` (`components/hub/panel-host.tsx`) by one explicit line in the §46 eager registrations module (`web/src/app/registrations.tsx`), so `?panel=<Type>:<id>` deep links keep working from every screen.

- **FR-REC-07.** Named cell renderers and named slots MUST be the only escape hatch. A renderer draws one cell type, such as a key, a date, a person, a status pill or a severity chip. A slot is a component one screen needs, such as a create form, a dialog, a row action or a panel section; the page that uses the list or panel passes its slots. Renderer names are a closed C# enumeration in the registry, emitted to the client with the §44 generated files (`web/src/gen/`), and the client's renderer map is typed as exhaustive over it, so a missing renderer fails `npm run build`. A declared slot that the page does not pass shows an error banner in its place, which the parity run reports as a difference.

- **FR-REC-08.** After a write, RecordList and RecordPanel MUST refresh through the §44 invalidation map (`keys.ts`) and add no invalidation of their own. The new endpoints return typed results, appear in the §44 golden `openapi.json`, and are read through the §44 typed helper.

### 47.4 The parity run

- **FR-REC-09.** A real-API parity run MUST compare a base build and a candidate build of the web app, served against the same API and the same database, and fail on any difference. Its records are created through the API, as the §44 real-API suite creates its own. It signs in as a Project Manager, a Discipline Lead, a Team Member, a Read Only user and a user who cannot view a Restricted project, at 1600 px and 375 px wide. It compares:
  - the accessible tree of the screen and its panel: headings, caption, column headers, row text, link and button names, disabled states and their titles, `aria-sort` and `aria-current`;
  - every filter field, the URL it writes and the rows returned;
  - the saved-view menu, saving a view and applying it;
  - the export file in CSV and Excel: header row and data rows;
  - the keyboard path: Tab order through the toolbar, Enter opens the Filters popover and the panel, Esc closes each and returns focus, arrow keys move in menus;
  - each panel field's editability, each transition button and its reason, and each tab;
  - the API requests made while the screen loads, apart from the one cached `/meta` read.

  An automated accessibility scan reports no serious or critical issue on either build. The run saves screenshots of both builds for the reviewer; they are evidence, not a pass condition. The run executes in CI on every PR that converts a screen, with `flag.record_engine_additions` off, and the PR cannot merge while it fails.

### 47.5 The spike on Risks and open roles

- **FR-REC-10.** The spike MUST convert three screens, each in its own PR: the Risks register list (`RisksTab`), the Risk panel (`RiskPanel`, both in `pages/projects/Registers.tsx`) and the open-roles list on the project's Allocations tab (the `OpenRoles` table in `pages/projects/Allocations.tsx`). Each PR passes the parity run and deletes the component it replaces, so no screen ever has two live implementations. The Realise dialog and the Close confirmation with its optional reason become slots of the Risk panel. The create forms (`RiskForm`, `RoleDialog`), the Fill flow, the Cancel confirmation and the Filled-by cell keep their components and are passed as slots. Risks keeps its columns, sorts, filters and indicator chip; open roles keep their rows, actions and empty state.

- **FR-REC-11.** The spike's verdict MUST be measured, not judged. It is PASS only if all of these hold:
  - (a) the parity run passes for all three conversions;
  - (b) every existing test passes with no assertion changed; the only allowed test edits are added `/meta` fixtures in suites that mock the API;
  - (c) the new hand-written lines for Risks (definitions plus any new slot code) are fewer than the TSX lines the two Risk PRs deleted, counted without blank lines;
  - (d) the Risk panel needs at most 3 slots and no Risk-specific CSS;
  - (e) the descriptor reproduces every field and transition reason through existing permission and workflow functions, with no function added for the engine;
  - (f) the open-roles conversion, with its additions under FR-REC-12, touches at most 10 files and at most 300 hand-written lines, tests excluded;
  - (g) those additions come from definitions alone, with no screen-specific client code.

  Any miss is KILL. The verdict, with each measured value, is recorded in the packet's `acceptance.md` (§43). Jay confirms it by setting the status of decision 043-D3 in `specs/DECISIONS.md`.

- **FR-REC-12.** The spike MUST also add, behind `flag.record_engine_additions` (a §43 flag declared in `src/Hub.Domain/Flags.cs` with packet 043, returned on `/me`, off by default), the surfaces these screens lack. The Risks register gains saved views: a `risks` saved-view list and the Views menu in its toolbar row, as the Issues register already has. Open roles gain column-header sorting, a read-only detail panel opened from the role label with the role's fields and a History tab, and Excel and CSV export offered in the Allocations page's Export menu after the existing entries. FR-ROLE-05 applies to each addition: a viewer who cannot view the project gets none of them, and no total differs. The open-role panel's `PANELS` entry names the flag, and the panel host treats an entry whose flag is off as absent, so a `?panel=OpenRole:<id>` link does nothing while the flag is off, as today. With the flag off the screens are exactly as before. When Jay has turned the flag on in review and pilot, the §43 cleanup removes the flag check and keeps the additions. A KILL verdict keeps them too (FR-REC-14). Adoption PRs (§47.6) add no surface.

### 47.6 Adoption per screen

- **FR-REC-13.** After a PASS verdict that Jay confirms (D3), list- and panel-shaped screens MUST convert one screen per PR in the order of D5. Each PR passes the parity run, deletes the component it replaces and adds no surface the screen lacked. Each PR also meets four rules: the screen's new hand-written lines are fewer than the lines it deletes; it needs at most 3 slots; it adds no screen-specific CSS; and it needs no new permission function. A screen that misses any rule stays as it is: its PR is closed unmerged, and the adoption table in the packet's `acceptance.md` names the rule it missed. A bespoke screen may place a RecordList where it shows a plain list, under the same rules. A conversion carries every flagged addition already merged on that screen as a slot, and its parity run runs with each such flag off and on. This holds for other packets' additions too, for example the Team tab away chip (§51.10), the Portfolio status note and grouping (§51.7, §49.6), the Portfolio Planner link (§50.6), the Allocations `fill` parameter (§51.6) and the Reviews and Changes server flags (§45.5).

### 47.7 Fallback and ratchet

- **FR-REC-14.** If the verdict is KILL, one PR MUST revert the spike conversions and the engine's `/api/v1/meta` and `/api/v1/records/{type}/{id}` endpoints, restoring the components the parity run had matched, and pass the parity run against the pre-spike build with `flag.record_engine_additions` off. The FR-REC-12 additions are kept: the same PR rebuilds them on the restored components behind the same flag, keeping the open-roles export endpoint, so nothing a user could reach is removed. If Jay declines D1, nothing is reverted because nothing was built. In either case the fallback follows. The `RegisterTable` function in `pages/projects/Registers.tsx` moves to `components/hub/registers.tsx` as a shared `DataTable` over the existing `useTable` and `Column`, with no change to its output. Hand-rolled list tables then move onto it one table per PR, each under the parity run, deleting the old markup in the same PR. The fallback adds no panel renderer, no metadata endpoint and no surface a screen lacked beyond the kept FR-REC-12 additions.

- **FR-REC-15.** The §44 ratchet already counts raw `<table>` elements outside `web/src/components/ui/table.tsx` and only reports that count until a shared list lands. The first adoption PR after a confirmed PASS, or the `DataTable` PR on the fallback path, MUST turn that counter into a failing check, so the count only goes down from then on. Every later conversion lowers it. The counter stays report-only until then, so the revert of FR-REC-14 never trips it.

### 47.8 Acceptance criteria

- **AC-REC-01.** Given the Risk and open-role definitions, when `GET /api/v1/meta` is read, then the `risks` list has today's 9 columns in today's order (key, title, severity, status, owner, discipline, review date, mitigation, realised as), with key and title fixed, "realised as" optional, the same sortable set and the storage key `hub.riskColumns`, and today's filters (`q`, `status`, `severity`, `ownerId`, `disciplineId`, `indicator`); the open-roles list has today's 6 columns; the response's ETag is the build stamp and a repeat read with that ETag returns 304; and both new endpoints appear in the golden `openapi.json`.

- **AC-REC-02.** Given each persona of FR-REC-09 and a risk on an Active and on a Complete project, when `/api/v1/records/Risk/{id}` and `/api/v1/risks/{id}` are read, then each field's editable flag and reason equal `permissions.edit`, the transitions equal `permissions.transitions` in order and in reason, and a viewer who cannot view the project gets the same status code from both.

- **AC-REC-03.** Given an Open and a Filled open role, then the descriptor carries the row the open-roles list returns for that role, marks every field not editable and lists no transitions; and a viewer who cannot view the project gets the same status code as from `GET /api/v1/projects/{id}/open-roles` (FR-ROLE-05).

- **AC-REC-04.** Given the Risks register converted, when the parity run compares the base and candidate builds, then it reports no difference for any persona at either width, including each filter, each column sort, the column chooser, both export formats, the keyboard path and the accessibility scan; and `RisksTab` no longer exists in `web/src`.

- **AC-REC-05.** Given the Risk panel converted, then the parity run reports no difference in the header facts, field values, editability and reasons, the Realise, Close and Reopen steps, the Links, Comments and History tabs, an inline edit that saves one field with If-Match and then shows the new value in the list, and the conflict message on a stale row version; `?panel=Risk:<id>` opens it from My Work; and `RiskPanel` no longer exists.

- **AC-REC-06.** Given the open-roles list converted, then the parity run reports no difference in rows, dates, hours, status pills, Filled by, the Fill, Edit and Cancel actions for each persona, the correction reason on a Complete project, the empty state, and the Allocations page's Views and Export menus; and the `OpenRoles` table function no longer exists.

- **AC-REC-07.** Given any spike, adoption or fallback PR, then every existing API and browser test passes with no assertion changed, and the only test edits are added `/meta` fixtures in suites that mock the API.

- **AC-REC-08.** Given a definition that names a renderer with no client component, then `npm run build` fails; given a page that does not pass a declared slot, then the screen shows an error banner in its place and the parity run fails.

- **AC-REC-09.** Given `flag.record_engine_additions` off, then Risks shows no Views menu, open roles show no sort buttons, no panel link and no open-roles export entries, and `?panel=OpenRole:<id>` opens nothing. Given it on, then a Risks saved view saves and re-applies its filters, sort and columns; open-role headers sort with `aria-sort`; the role label opens a panel whose History tab lists the role's activity entries; and the Export menu's open-roles entries download the project's roles with the list's columns in CSV and Excel. A viewer who cannot view the project gets none of these, and the export answers that viewer with the same status code as the open-roles list endpoint.

- **AC-REC-10.** Given the spike's PRs merged, then the packet's `acceptance.md` lists rules (a) to (g) of FR-REC-11, each with its measured value, and states PASS or KILL, and decision 043-D3 in `specs/DECISIONS.md` points to it.

- **AC-REC-11.** Given a converted screen, then loading it makes the same API requests as the base build, apart from at most one `/meta` read per build, which the next screen serves from cache.

- **AC-REC-12.** Given an adoption PR, then it shows a passing parity run, deletes the component it replaces, adds no surface the screen lacked, and its new hand-written lines are fewer than the lines it deletes. Given a screen that misses a rule, then its PR is closed unmerged and the adoption table in `acceptance.md` has a row naming the rule.

- **AC-REC-13.** Given a KILL verdict, then one PR restores `RisksTab`, `RiskPanel` and the `OpenRoles` table, removes `/api/v1/meta`, `/api/v1/records/{type}/{id}` and the renderers, keeps the FR-REC-12 additions behind `flag.record_engine_additions`, passes the parity run against the pre-spike build with the flag off, and passes the AC-REC-09 checks with it on.

- **AC-REC-14.** Given the fallback, then `DataTable` renders Risks and Issues with no difference in the parity run, and each later fallback PR moves one hand-rolled table onto it with a passing parity run and deletes the old markup.

- **AC-REC-15.** Given the counter switched to failing, then a PR that adds a raw `<table>` outside `web/src/components/ui/table.tsx` fails CI, and each later conversion PR lowers the committed baseline.

- **AC-REC-16.** Given the packet's PRs, then `web/package.json` and the `src/**/*.csproj` files gain no runtime package, no new service is started, and no database table or migration stores list or panel definitions.

### 47.9 Decisions and deferred proposals

Each decision uses its default until Jay decides otherwise. D1 is a constitution VII approval; Jay gave it on 2026-10-14 by directing that everything in the report be built.

| # | Decision | Default |
|---|---|---|
| D1 | Approve the Complexity Tracking entry in packet 043's plan for the spike and, on a confirmed PASS, adoption | Decided by Jay 2026-10-14: approved for the spike; adoption beyond the spike screens follows only a confirmed PASS verdict (D3). |
| D2 | Which screens the spike converts | The Risks register list and panel, and the open-roles list on the Allocations tab. |
| D3 | How the verdict is reached | Measured by FR-REC-11 and recorded in the packet's `acceptance.md`; Jay confirms. PASS starts adoption; KILL starts the revert, which keeps the flagged additions, and the fallback. |
| D4 | What the spike adds | Behind `flag.record_engine_additions`, off: Risks saved views; open-role sorting, panel with History, and export. Adoption PRs add nothing. On KILL the additions are kept on the restored screens (FR-REC-14). If D1 is declined, the spike never runs and the open-role export returns to §51.13's deferred list. |
| D5 | Adoption order | Milestones list; Milestone panel; Holidays; reference data; Users; project Team; WorkspaceTeam; Vacation; the Allocations list; Reviews and Changes; Deliverables list; Deliverable panel; Decision panel; Meeting action panel; Project List; Portfolio list; project Activity; org Activity. |
| D6 | Hybrid and bespoke screens | Not converted (§47.1). |
| D7 | Generic create and update endpoints | Not built. Panels save through each entity's existing endpoints. |
| D8 | Fallback table and order | `RegisterTable` becomes `DataTable` in `components/hub/registers.tsx`. Order: open roles, Allocations list, project Team, WorkspaceTeam, Holidays, reference data, Users, Vacation, Milestones, Project List, Portfolio, Reviews and Changes. |
| D9 | How panels are registered | One explicit `PANELS` line per converted type in `web/src/app/registrations.tsx` (§46); no automatic fallback for a type without one. |
| D10 | Merge tier of conversion PRs | Spike PRs are Tier B: Jay merges them. Adoption and fallback PRs whose parity run passes with no test assertion changed are refactors with unchanged tests, so Tier A (§43); the parity screenshots go with each PR. |

Deferred follow-up proposals, outside packet 043:

- search groups for risks, issues, meeting actions and open roles (left to §47 or later by §44);
- generic create and update endpoints per entity;
- converting hybrid screens, each with a parity checklist built from its FR and AC IDs;
- per-field descriptors for the Task panel;
- export, saved views and a column chooser for adopted lists that lack them;
- the Workload Unfilled roles week grid and its export;
- virtualised rows or a larger table library, only if a list outgrows `useTable`;
- a generated client file of list codes and label keys.
