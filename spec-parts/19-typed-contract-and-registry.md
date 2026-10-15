## 44. Typed Contract and Entity Registry Amendment

Approved scope: on 2026-10-14 Jay approved the build-approach report and directed building all of it. Features are fixed and this work is additive only: nothing here removes, hides, narrows or freezes a feature, screen, route, report, setting or rule. Packet 040 delivers this section. It finishes two promises the specification already makes, a typed client generated from the OpenAPI document (§23.4, §25.1) and an explicit allow-list per audited entity (§20.4). It replaces hand-kept copies of one contract with one source and a test, because seven of packet 036's eighteen defects were seams between such copies (036-D1, 036-D3, 036-D5, 036-D8, 036-D9, 036-D10 and 036-D14 in `specs/036-staffing-and-signal-trust/verification.md:379-396`), against two rule errors.

Where this section adds to §13.14 (Activity History), §18.4 (saved views), §20.4 (audit implementation), §22 (localisation), §23.4 (frontend architecture) and §25.1 (API conventions), this section governs. Every screen, endpoint path, response field, label text, saved view, export column and search group that exists today keeps working as it does. Where this section touches existing behaviour, today's behaviour stays the default. Nothing here ships behind a flag. The visible changes (the added Activity History labels, the saved-view key fixes and the page-mode parameter names) are corrections of existing behaviour that ship unflagged under D14, and their slices are Tier B (§43.11). Code citations are at `48586029` of `claude/tuesday-visual-batch1`.

### 44.1 Scope and boundary

Five parts: one entity registry (§44.2), the seam fixes it exposes (§44.3), a typed contract between server and client (§44.4), one cache-invalidation map (§44.5), and a ratchet with a stop rule (§44.6). This section adds no endpoint path, database table, column or migration, and changes no rule, threshold, permission or calculation.

Outside this section: server-computed load bands and permission flags (§45); route splitting, the build stamp and live updates (§46); generic list and panel renderers (§47); CI sharding, feature flags, PR size limits, the test-count ratchet and the record format (§43).

### 44.2 Entity registry

- **FR-PLAT-01.** There MUST be one entity registry written in plain C# under `src/Hub.Api/Records/`, one file per audited entity type, discovered in one place. Each entry declares:
  - the entity type and its stored item type name (`IAuditable.AuditType`, `src/Hub.Api/Data/Entities.cs:23-30`);
  - its audited fields;
  - a project lookup for item history, or a stated reason it has none (an organisation-level record, a child row shown in its parent's history, a personal record, or no item-history surface today);
  - a name lookup, where activity summaries or export parameters name it;
  - its search group, if any;
  - the lists it owns. Each list gives its saved-view keys, the reference type each id key is checked against, and the page keys a saved view does not keep, each with a reason. A list that exports also gives its export columns.

  Lists that span entities (My Work, Workload, Weekly Planner, the coordination views, readiness, Portfolio and the board variants) are declared in one screen-lists file. Entries are typed records and lambdas only: no expression strings, configuration files or rules evaluated at run time.

- **FR-PLAT-02.** These hand-kept copies MUST be derived from the registry, each in its own change: `AuditRules.Fields` (`src/Hub.Api/Infrastructure/Audit.cs:34-128`, 78 types); `ActivityEndpoints.ItemProject` and `ActivityEndpoints.Names` (`src/Hub.Api/Features/Activity.cs:86-103` and `:150-183`); the export parameter names `ListExports.VisibleNames` (`src/Hub.Api/Features/ListExports.cs:73`); `ViewEndpoints.Lists` and the reference checks in `Clean` (`src/Hub.Api/Features/Views.cs:23-48` and `:167-201`); the 21 static export column arrays (`static readonly Col[]` in 14 feature files, the `Col` record at `src/Hub.Api/Features/Reports.cs:13`); and `SearchEndpoints.Groups` (`src/Hub.Api/Features/Search.cs:14-15`). Columns built per request (`src/Hub.Api/Features/Workload.cs:275,295`, `src/Hub.Api/Features/Availability.cs:127`, `src/Hub.Api/Features/Planning.cs:585`) and the report definitions (the `Def` record at `src/Hub.Api/Features/Reports.cs:24` and the `Def[] All` registry at `:35`, already one registry) stay where they are. For every existing entry each derivation MUST return exactly what the hand copy returns today, in the same order and format. Export parameter names keep their rule: a record is named only when its project is visible to the caller, and people and disciplines keep their own visibility rules unchanged. The `SaveChanges` audit hook, the append-only activity log and its triggers do not change.

- **FR-PLAT-03.** Before each derivation lands, a JSON snapshot of the hand copy's output MUST be committed. A test MUST fail when a derived list lacks any snapshot entry: an audited field, an item-history type, a named type, a saved-view key, an export column or a search group. Export columns and search groups also keep their order, and new ones are appended after them. The snapshot update path refuses to drop an entry. A derived list adds entries only where this section or a later amendment names the addition.

- **FR-PLAT-04.** A reflection test MUST fail when: an `IAuditable` type has no registry entry, or two; an entry names an audited field the type does not have; an entry has neither a project lookup nor a reason; or a list code accepted by `/views` has no declaration. Gaps found on the first run that §44.3 does not fix go into a known-gaps file that may only shrink.

- **FR-PLAT-05.** The derived item-history lookup MUST resolve exactly today's 14 types, Project included (`Activity.cs:86-103`). A type gains a project lookup only in the change that gives it an item-history surface, and only where `ActivityEndpoints.Visible` (`Activity.cs:53-69`) already applies its privacy boundary. Personal records (planning entries and drafts, availability overrides, approved leave, schedules, calendar subscriptions and time entries) carry a reason instead, unless that change adds the privacy rule and a test for it. The same bound applies to derived export parameter names: no personal record type is named unless today's lookup names it. A viewer who cannot see a record gets the same 404 from its item history as today.

- **FR-PLAT-06.** The registry MUST emit `web/src/gen/registry.gen.ts` with literal unions of the item types, the audited field names, the vocabulary values of `src/Hub.Domain/Vocabulary.cs`, the list codes and each list's saved-view keys. The client text table MUST hold an `itemType.*` key for every item type and a `field.*` key for every audited field; a missing one fails the web build. `value.*` keys are typed against the vocabulary but need not be complete, because `tv()` already falls back to the canonical stored name (§10, `web/src/lib/i18n.ts:14`). Where a page that shows saved views keeps its filter keys in a list (`web/src/pages/projects/Tasks.tsx:309`, `web/src/pages/Workload.tsx:40`, `web/src/pages/Planner.tsx:28`, `web/src/pages/Portfolio.tsx:24`, `web/src/pages/WorkspaceTasks.tsx:28`, `web/src/pages/projects/Decisions.tsx:55`, `web/src/pages/projects/ProjectList.tsx:26`, `web/src/pages/projects/Registers.tsx:73`), that list is typed against its saved-view keys plus its declared not-kept keys, so a page key the save path would silently drop fails the web build. Typing them exposes keys the save path drops today (for example Portfolio `computedHealth` and Planner `stale`); each is added to its saved-view keys or declared as not kept (D13). The file is rewritten by the registry test in update mode, and CI fails when the committed file is stale.

### 44.3 Seam fixes

- **FR-PLAT-07.** The task lists' saved-view keys (`Views.cs:18-20`, shared by tasks, board, workspace-tasks and workspace-board) MUST include `onHold`, which `src/Hub.Api/Features/TaskQueries.cs:39` already parses and `web/src/pages/projects/Tasks.tsx:308` already keeps in the URL. A view saved with it reopens with it. Today the save leaves it out (`Views.cs:147-156`).

- **FR-PLAT-08.** Workload and Weekly Planner saved views (`Views.cs:40-41`) MUST keep `groupBy`, the grouping parameter both pages read (`web/src/pages/Workload.tsx:86`, `web/src/pages/Planner.tsx:48`) under FR-GRP-02. A view without it opens with today's default grouping, Reports to.

- **FR-PLAT-09.** The URL parameter `view` carries the saved-view id (`web/src/components/hub/views.tsx:20-38`), and no page that shows the saved-view menu may use it for its own state. Three pages do today: Allocations (`?view=roles`, `web/src/pages/projects/Allocations.tsx:69,90`), the Weekly Planner (`?view=list` or `?view=projects`, `Planner.tsx:36,59`) and Decisions (`?view=log`, `web/src/pages/projects/Decisions.tsx:126,149`). They MUST use `tab=roles`, `layout=list` or `layout=projects`, and `tab=log`. Old links keep working: a legacy `view` value that is one of the page's own modes opens that mode, and the address is rewritten to the new name without adding a history entry. A saved-view id is never read as a mode. Without a mode parameter each page opens as today (Allocations list, Planner grid, Decisions register). Allocations and Planner saved views keep the mode (new keys `tab` and `layout`), so applying a saved view opens the mode it was saved in. Today applying any view returns these pages to their default mode, because the view id overwrites the mode. In-app links move to the new names (`Workload.tsx:270`). A check MUST fail CI when a page that renders the saved-view menu reads or writes `view` for its own state.

- **FR-PLAT-10.** Every audited item type and field MUST have its client label (FR-PLAT-06). A scripted read found 24 item types and 79 field names without one; Activity History shows the raw key or a split field name for them (`web/src/components/hub/activity.tsx:16,68`). The missing English texts are added. The text of every existing key stays unchanged.

- **FR-PLAT-11.** The client text table (`web/src/i18n/en.ts:2`, typed today as `Record<string, string>`) MUST be typed by its own keys, and `t()` MUST accept only a known key or a key of a generated family. Keys built at run time outside those families go through one named function, which the ratchet counts (219 template-built calls today). For keys present in both `src/Hub.Api/Text.cs` and `en.ts`, a check MUST fail on any new difference in text. Today's differences (23 of 79 shared keys; for example `itemType.Action` is "Action" on the server and "Meeting action" in the client) are recorded with their current texts and stay as they are (D4).

### 44.4 Typed contract

- **FR-PLAT-12.** Handlers MUST return public C# records through typed results (`TypedResults` and `Results<...>`), so the OpenAPI document carries their response schemas. Today 79 handlers return `Task<object>`, 89 return `Task<IResult>` and none use typed results (50 files in `src/Hub.Api/Features`, 357 endpoint mappings). Conversion goes one feature file per change. Every converted route returns the same JSON as before (FR-PLAT-16); only added fields are allowed. An endpoint written after the typed helper exists is typed when written. The first conversion is `src/Hub.Api/Features/OpenRoles.cs` (its private `Row` record, line 15) with the unfilled-roles payload of `src/Hub.Api/Features/Workload.cs:208-226`. It passes only if every existing test and recorded shape is unchanged; renaming `plannedHours` in the server record fails the web build in `Allocations.tsx` and the typed mock; removing OpenRole's project lookup fails the registry test; deleting `itemType.OpenRole` fails the web build; and the conversion fits one change under the §43 size limit. If the suite calls fewer than about 60% of mapped endpoints, uncovered endpoints get tests before their file converts (D7). If the generated document needs hand edits for `DateOnly`, `decimal` or nullable values, schema transformers are added beside `AddOpenApi` (`src/Hub.Api/Program.cs:80`). The 23 merge-patch `JsonElement` request bodies stay hand-typed on the client.

- **FR-PLAT-13.** A test MUST fetch `/api/v1/openapi.json` through the existing test host (`tests/Hub.Tests/Api/Factory.cs:22-51`), normalise it (the servers block removed, keys in a stable order) and compare it with the committed `web/src/gen/openapi.json`; update mode rewrites the file. No build-time generator that starts the application is used, because startup migrates and seeds the database (`Program.cs:190-203`). Publication at `/api/v1/openapi.json` is unchanged (`Program.cs:168-169`).

- **FR-PLAT-14.** openapi-typescript (pinned at 7.13.0, MIT, a development dependency only) MUST generate `web/src/gen/api.d.ts` from the committed document as the first step of `npm run build`. CI MUST fail when a committed generated file differs from a fresh generation.

- **FR-PLAT-15.** `web/src/lib/api.ts` MUST gain one helper that takes a method, a path from the generated types, its parameters and its body, and returns the generated response type. It calls the existing `api()` (`api.ts:33-55`), so the source header, If-Match, Idempotency-Key, the warnings toast, `ApiError` and the 401 handling stay in one place. `get`, `post`, `patch`, `put` and `del` keep working unchanged.

- **FR-PLAT-16.** A response-shape recorder MUST live in the test project and be added by the test host, so no application build contains it. For each method, route template and status, it records the union of JSON key paths and value kinds across the full suite; the merged result is committed as `tests/Hub.Tests/Snapshots/shapes.json`. A check over a full run MUST fail when a recorded path disappears or changes kind, and when a new path appears until the snapshot is regenerated in the same change. Null merges with any kind, and an array seen only empty in a run does not count as a removal. Every operation with a declared response schema MUST have a recorded shape, so converted endpoints are always exercised. The check also lists the mapped endpoints no test calls.

- **FR-PLAT-17.** Mocked browser suites MUST type the payloads of converted endpoints against the generated types (`// @ts-check` with JSDoc imports), checked by a type check over `web/tests` in CI. The first is the open-roles mock that returns `[]` (`web/tests/resources.cjs:56`). A suite's other mocks are typed when their endpoints convert.

- **FR-PLAT-18.** One browser suite MUST run in CI against the built application and the CI PostgreSQL service, with no mocked route, signed in through the development picker on a loopback address. It creates visibly synthetic records through the API and covers at least: creating an open role on Allocations and finding it in the list and in Workload's Unfilled roles (FR-ROLE-04, AC-ROLE-01); the legacy links of FR-PLAT-09 opening their modes; and zero axe violations and zero page errors on each page it opens.

### 44.5 Invalidation map

- **FR-PLAT-19.** `web/src/lib/keys.ts` MUST hold the query-key roots and one map from item type to the query keys a change to it affects. `useProjectRefresh` (`web/src/hooks/data.ts:15-28`) keeps its signature and routes through the map, including its second pass 1.5 s after a save, which stays; §46 adds change events beside it and never removes it. The map only adds: for every item type it includes every key that today's call sites invalidate for it, so a change can cause an extra refetch but never a missing one. The 57 direct `invalidateQueries` calls move into the map page by page. A unit test asserts the superset for each item type.

### 44.6 Ratchet and stop rule

- **FR-PLAT-20.** `tools/ratchet.py` MUST count, against a committed baseline `tools/ratchet.json`: calls of the `api.ts` helpers without a type argument; handlers returning `Task<object>` and untyped `Results.*` responses in `src/Hub.Api/Features`; anonymous objects (`new {`) there; direct `invalidateQueries` calls outside `keys.ts`; run-time-built text keys (FR-PLAT-11); `<table>` elements outside `web/src/components/ui/table.tsx`; and client re-derivations of permissions. A counter whose replacement exists MUST fail CI when it rises. Until its replacement lands (the typed helper here, the shared list in §47, server permission flags in §45) a counter only reports. The update option lowers the baseline and refuses to raise it. The same script runs the FR-PLAT-09 reserved-name check and the FR-PLAT-11 text check as hard checks. The ratchet changes no product behaviour.

- **FR-PLAT-21.** After the first five features built on the typed contract and registry, the figures for each MUST be recorded against packet 036 (about 35 hand-written files; 18 sweep defects, 7 of them seams): hand-written files changed, generated files and tests excluded, and sweep defects found. If neither median has dropped below packet 036's figure, dedicated conversion changes stop. Ratchets, the registry and every conversion already made stay, and new endpoints are still typed. Jay rules on the recorded figures (D8).

### 44.7 Acceptance criteria

- **AC-PLAT-01.** Given the registry, when an `IAuditable` type's entry is deleted, a second entry names the same type, or an entry has neither a project lookup nor a reason, then the registry test fails. With the registry complete it passes, and the known-gaps file lists only gaps §44.3 does not fix.

- **AC-PLAT-02.** Given the derived audit allow-list, then it equals today's for all 78 types, and the activity rows written for a seeded task edit, risk edit and open-role cancel are identical to those written before the change. Removing `PlannedHours` from OpenRole's entry fails the superset test.

- **AC-PLAT-03.** Given the derived item-history lookup, then `/items/{type}/{id}/activity` resolves the same project as today for Task, Deliverable, Milestone, Decision, Risk, Issue, Action, ReviewPackage, ChangeNotice, ReviewFinding, ChangeAssessment, Handoff, OpenRole and Project, and any other type still returns 404. Removing OpenRole's project lookup fails the registry test (the 036-D1 class).

- **AC-PLAT-04.** Given a seeded fixture, then Activity History summaries and names and the export Parameters sheet are byte-identical before and after the derivation. An id of a record in a project the caller cannot see stays a raw value in the Parameters sheet. A person who cannot see a private calendar event or another person's time entry gets the same responses as today.

- **AC-PLAT-05.** Given the snapshots, then the derived saved-view keys contain every key each list accepts today, each export's columns are today's columns in today's order with any new ones after them, and the search groups are today's 16 in today's order. Deleting any one entry from a derivation fails CI.

- **AC-PLAT-06.** Given a project Tasks view saved with `onHold=false`, when it is reopened, then the address and the list request both carry `onHold=false`; the same holds for board, workspace-tasks and workspace-board views. Removing `onHold` from the server's task keys fails the web build (the 036-D3 class). Every key in the eight typed page lists of FR-PLAT-06 is either kept by a saved view or declared as not kept with its reason.

- **AC-PLAT-07.** Given Workload and Weekly Planner views saved with `groupBy=office`, when reopened, then each groups by Office. A view saved without `groupBy` opens grouped by Reports to.

- **AC-PLAT-08.** Given `/projects/{n}/allocations?view=roles`, then Open roles opens and the address becomes `?tab=roles` without a new history entry; a view saved on Open roles reopens on Open roles, and one saved on the list reopens on the list. Given `/planner?view=list` or `?view=projects`, then that layout opens as `?layout=list` or `?layout=projects`; a Planner view saved in List reopens in List, and one saved in Grid opens the grid. Given `/projects/{n}/decisions?view=log`, then Log opens as `?tab=log`. Without a mode parameter each page opens as today. The existing planner and resources suites, which use the old links, pass unchanged.

- **AC-PLAT-09.** Given a page that renders the saved-view menu and reads or sets `view` for its own state, then the reserved-name check fails. After FR-PLAT-09 no page does.

- **AC-PLAT-10.** Given the registry, then every audited item type and field has an `itemType.*` or `field.*` text and Activity History shows no raw key for any of them. Deleting `itemType.OpenRole` from `en.ts` fails `npm run build` (the 036-D5 class). The diff of `en.ts` adds keys and changes the text of no existing key.

- **AC-PLAT-11.** Given `t('no.such.key')` in a page, then the web build fails. Given a key shared by `Text.cs` and `en.ts` whose text changes on one side only, then the text check fails. The 23 recorded differences keep their current texts. The count of run-time-built keys never rises.

- **AC-PLAT-12.** Given the test host, then the normalised document equals `web/src/gen/openapi.json`. Given a new endpoint without the regenerated file, the golden test fails; update mode rewrites the file. No build step starts the application.

- **AC-PLAT-13.** Given a fresh checkout, then `npm run build` regenerates `api.d.ts` identical to the committed file, and CI fails when either generated file is stale. A request made through the typed helper carries the same `X-Hub-Source`, If-Match and Idempotency-Key headers and raises the same `ApiError` as one made through `api()`.

- **AC-PLAT-14.** Given the converted `OpenRoles.cs` and unfilled-roles payload, then their recorded shapes are unchanged, every existing test passes, and the document carries their record schemas. Renaming `plannedHours` in the server record fails `npm run build` in `Allocations.tsx` and fails the type check of `resources.cjs` (the 036-D10 class).

- **AC-PLAT-15.** Given the recorded shapes, when a handler drops a response field or changes its kind, then the shape check fails; when it adds a field, the check fails until `shapes.json` is regenerated in the same change. A list that returns an empty array in one run does not fail the check. No application build contains the recorder.

- **AC-PLAT-16.** Given an operation with a declared response schema that no test calls, then the shape check fails. The check prints the mapped endpoints no test calls.

- **AC-PLAT-17.** Given a typed mock that returns a field the generated type does not have, or lacks a required one, then the `web/tests` type check fails in CI.

- **AC-PLAT-18.** Given CI, then the smoke suite runs against the built application and PostgreSQL with no mocked route, creates a synthetic open role, finds it on Allocations and in Workload's Unfilled roles, opens the three legacy links of AC-PLAT-08 in their modes, and reports zero axe violations and zero page errors.

- **AC-PLAT-19.** Given `keys.ts`, then for every item type the map includes each key that today's call sites invalidate for it, `useProjectRefresh` callers are unchanged, and a save still triggers the second invalidation pass after 1.5 s. Removing a key from the map fails the unit test.

- **AC-PLAT-20.** Given the baseline, when a change raises a counter whose replacement exists, then CI fails; when it lowers one, CI passes and the update option lowers the baseline; the update option refuses to raise a counter. Report-only counters print without failing.

- **AC-PLAT-21.** Given five features built on the platform, then each one's hand-written files and sweep defects are recorded against packet 036's figures, and Jay's ruling on the long tail is recorded with them. This is a human check.

### 44.8 Decisions and deferred proposals

Each decision uses its default until Jay decides otherwise.

| # | Decision | Default |
|---|---|---|
| D1 | How the registry finds its entries | Discovery by reflection in one place; each entity file adds itself, so parallel changes never edit one shared list. |
| D2 | New names for page modes | Allocations `tab=roles`, Weekly Planner `layout=list` or `layout=projects`, Decisions `tab=log`. |
| D3 | Legacy `view=` mode links | Accepted indefinitely and rewritten in place; never removed. |
| D4 | Text that differs today between server and client for one key | Both keep their current text. Aligning a pair is a copy change Jay rules on key by key. |
| D5 | Text of the added labels | Plain English from the names the specification uses for the entity and field; Jay may change any. |
| D6 | New response fields | Allowed, with the shape snapshot regenerated in the same change so the addition shows in review. |
| D7 | Endpoint coverage below about 60% | Tests for uncovered endpoints come before their file converts; conversion continues file by file where covered. |
| D8 | Pace of the long tail | Run it on the ratchet now, with the stop rule, rather than wait for pilot evidence. |
| D9 | Sign-in for the real-API suite | The development picker on loopback against the CI database. |
| D10 | Runner for the `keys.ts` unit test | Node's built-in test runner with type stripping (expected to work on Node 24, not yet tried); if it cannot load the file, the same assertions run in a mocked browser suite. |
| D11 | Item-history lookups for further types | None added here; each comes with the change that gives the type a history surface, under FR-PLAT-05. |
| D12 | Converting files a feature change touches | Allowed within the files a change already touches (the boy-scout rule, FR-DLV-29) once constitution 1.1.0 is in effect (§43.14 D11); until then conversions are changes of their own. |
| D13 | Page keys a saved view drops today | A filter, sort, grouping, column or mode key is added to the saved-view keys. Paging, panel and access-reason keys (for example Planner `page` and `draftAccessReason`) are declared as not kept. |
| D14 | Visible changes without a flag | The added Activity History labels (FR-PLAT-10), the saved-view key fixes (FR-PLAT-07, FR-PLAT-08, D13) and the page-mode names with old links kept (FR-PLAT-09) ship unflagged as corrections of existing behaviour. Their slices (S05, S06, S09) are Tier B. |

Deferred follow-up proposals, outside packet 040: search groups for risks, issues, meeting actions and open roles; and unfilled roles in the Workload export (`src/Hub.Api/Features/Workload.cs:49-55`, which exports the people rows only). Saved views for Risks are added by §47 behind its flag (FR-REC-12). The registry makes each one a declaration plus its screen; each remaining one is proposed with a later packet. A TypeScript port of the server stays deferred until a client-side what-if cannot be served by a server dry run in about 250 ms through the tunnel, local-first use becomes a requirement, or measured .NET agent friction remains after CI sharding; it would also need a constitution VII entry Jay approves. The generated document and types are internal and are not a public API; §30 is unchanged.
