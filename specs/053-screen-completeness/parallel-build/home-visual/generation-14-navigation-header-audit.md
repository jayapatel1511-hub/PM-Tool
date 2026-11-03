# Generation14: remaining 052 navigation and header audit

Read-only proposal, 2026-11-01. The current CONTROL permits this note only; no application/test source, shared spec ledger or decision was changed. No build, test, browser, generator, helper, goal or runtime was started. Root chooses the next bounded implementation loan. Preserve the preferred prototype's overall design.

## Evidence and completion boundary

052 S02/S07/S08 remain unchecked in `052-visual-system-and-navigation/tasks.md`; do not confuse them with the checked **053** Home S07/S08. Current `052/verification.md` records focused AC-VSN-05/08/09/10 passes while broader acceptance remains pending.

Root's checkpoint16 (`052/build-progress.md:1027` onward) records 14 Chrome suites, including visual-system/layout/tabs/title-block, and the enabled 17-screen/two-role real-API census plus reflow evidence. It does not establish six-role/all-route navigation coverage. The full census still exits1 for two preserved legacy-off MyWork first-data positions; these are not waived. DPR2 reflow equivalence is not native browser zoom or human acceptance. Navigation/drawer focused passes were recorded at earlier checkpoints; this audit did not freshly execute them.

| Requirement | Current source / existing evidence | Remaining closure |
| --- | --- | --- |
| FR-VSN-10 shell | `Shell.tsx:99–115,184–208`: permission-filtered list, required topbar control order, main scroll container. `visual-layout.css:4–20`: navy56px, desktop open240px/collapsed76px. Actual registered Search/Workspace/New/Bell slots retain their controls. | All-route shell/role census, phone extrema and input-boundary measurements. |
| FR-VSN-11 band | Shared `common.tsx:23–54` renders title/subtitle/actions/reference/tabs; nested project Page uses h2 under the project h1. Home/Portfolio overlap exists. | Full routed-page/loading/error inventory; My Staff card/overlap adoption belongs with S06. Dashboard spec conflict below. |
| FR-VSN-12 / AC05 | `ProjectLayout.tsx:119–167`: Mono number, one-line focusable full-name tooltip, status/health, PM/client/target meta. Focused long-name desktop/375px check passes in the recorded layout suite. | Long number, unbroken name, long PM/client and lifecycle/status extremes; check all information remains discoverable, not merely inside outer bounds. |
| FR-VSN-13 | Shared Input, `common.tsx:161–169,378`, actual `hub/search.tsx:82–91` use `border-input`; segmented controls expose selection. Page/button layout tests cover primary/secondary styles. | Measure necessary boundary contrast against its actual adjacent surfaces/states. Correct the exact on-navy focus token mismatch below. |
| FR-VSN-16 / AC08 | Exact main/foot ordering, capability hiding, no enabled More; Vacation fallback is implemented and focused-tested. | Canonical six-role fixtures and every permitted routed-family current-parent assertions; utility-route ambiguity and reassign alias below. |
| FR-VSN-17/18 / AC09 | Workspace eight-tab order, scoped copy link, six project groups, quiet Activity/Settings and project/resource siblings exist. Actual Reviews and selected sibling/filter tests pass. | All former project routes; legacy project Vacation; same-page List/Boards preset retention; right alignment of quiet links. |
| FR-VSN-19 / AC10 | `Shell.tsx:227–251` reuses the same lists in a modal Sheet, retains bottom MyWork/Search/Notifications, closes on navigation. Shared Sheet captures/restores the opener. Recorded375px drawer pass covers Escape, backdrop, one Tab inside, search-result navigation and page containment. | Full keyboard cycle/backward wrap, six-role drawer parity, resize and route-change focus behavior, complete phone control extrema. |

## Six-role baseline matrix

Authority: `Permissions.cs:155–162`, `Me.cs:48–61`, `app/nav.ts:32–57`; the six names come from AC-CALM-04. These are active, single-role baseline users without template-editor or unit-leader additions. The UI must use actual `/me` capabilities, not a new role-name switch.

Main8 = My Work, Home, Projects, Tasks, Resources, Time, Reports, Team. Main7 omits Resources without a gap. Foot order is Files, My Staff, Templates, Admin after permission hiding.

| Baseline role | Main | Foot | Portfolio / Workload | Global Vacation parent when permitted |
| --- | --- | --- | --- | --- |
| Admin | Main8 | Files, My Staff, Templates, Admin | Yes / Yes | Resources |
| Executive | Main8 | Files, My Staff | Yes / Yes | Resources |
| Supervisor | Main8 | Files, My Staff | Yes / Yes | Resources |
| Project Manager (system PM) | Main8 | Files | Yes / Yes | Resources |
| Team member (StandardUser) | Main7 | Files | No / No | Time |
| Read Only | Main7 | Files | No / No | No Vacation offer; evaluate denied page separately |

Template-editor adds Templates only when permitted. Section54 unit-leader reads can add workload/staff to a Standard user. Mixed roles and ReadOnly rules require capability-based fixtures. `Availability.cs:94–110` refuses ordinary ReadOnly reads, but has a separate unitRead widening path; do not generalize the baseline denial to every mixed/unit-leader identity. A hidden rail item alone never proves endpoint admission/denial.

**Jay's decided rule remains exact:** when Workload is denied, Resources stays hidden and an allowed global Vacation & Coverage route selects Time. No duplicate Vacation rail item and no new entitlement.

## Every registered shell route: current parent

Inspected `app/routes.tsx:104–172`, NAV aliases and `Shell.tsx:258` pathname matcher. Query strings do not change the global parent. This table describes current source selection, conditional on the route being permitted; it is not a claim that every role may open every route.

| Registered route family | Current visual rail selection |
| --- | --- |
| `/` | Redirects to `/my-work` → My Work |
| `/my-work`; `/home` | My Work; Home |
| `/projects` including `?view=board`; `/portfolio`; `/projects/:number` and every project child listed below | Projects (project index redirects to Dashboard) |
| `/tasks`, `/tasks/:id`, `/boards`, `/timeline`, `/calendar`, `/pickup` | Tasks; Pickup remains feature-gated |
| `/planner`, `/workload` | Resources when workload capability exposes it; denied surfaces are separate |
| `/vacation` | Resources with Workload; Time without Workload, subject to Vacation read permission |
| `/time` | Time |
| `/reports`, `/reports/:code` | Reports |
| `/team`, `/organisation` | Team; Organisation remains feature-gated |
| `/files`; `/staff` | Files; My Staff when permitted |
| `/templates`, `/templates/:id` | Templates when permitted |
| `/admin`, `/admin/reference/:kind`, `/admin/settings`, `/admin/users`, `/admin/activity`, `/admin/operations`, `/admin/holidays` | Admin when permitted |
| `/admin/structure`, `/admin/audit-chains`, `/admin/review` | Admin, also subject to their existing feature gates |
| `/people/:id/reassign-work` | **None**; breadcrumb already points to My Staff, but it does not set rail selection |
| `/notifications`, `/search`, `/preferences` | **None**; global utility routes are not aliases of a rail parent |
| `*` | No parent; classify NotFound/flag-denied/error surfaces explicitly rather than inventing a permitted destination |

Project child inventory from `ProjectLayout.tsx:33–46`:

| Selected project group / quiet destination | Registered children |
| --- | --- |
| Overview | `dashboard` |
| Work | `tasks`, `board`, `timeline`, `calendar`, `milestones` |
| Deliverables | `deliverables`, `submissions`, `reviews`, `readiness` |
| Coordination | `coordination`, `handoffs`, `changes`, `design-basis`, `decisions`, `risks`, `issues`, `meetings` |
| People | `team`, `allocations` |
| Files | `files` |
| Quiet Activity / Settings link itself | `activity`, `settings`; no six-group selection is expected on these utility tabs |
| **No project group or PROJECT_TABS label** | **`vacation`** is registered but absent from the group/breadcrumb inventory |

People's Workload link deliberately leaves the project for `/workload?projectId=<id>` and selects global Resources. Verify the canonical projectId/query and permission; do not pretend that global route is still an in-project page. PROJECT_ROUTES currently adds no extension routes.

## Prioritized next bounded loan

1. **Same-page project scope loss — concrete source gap.** `workspace.tsx:257–294` excludes `homePreset` and `homeProjects` from PROJECT_QUERY. ProjectList accepts both for its List and Boards layouts (`ProjectList.tsx:43,163–167`). Switching a Home-derived role/cohort List to Boards therefore drops supported scalar scope and can widen the rows. Preserve these two keys for the same `/projects` destination while overview is enabled; test a >50-derived-project preset without building an IDs URL. Portfolio does not currently implement these filters (`Portfolio.tsx:37–38,83`), so blindly forwarding them there would falsely promise equivalent scope. Root must choose a supported canonical bridge or explicit scope-change behavior for that distinct reader.
2. **Reassign parent — narrow canonical alias.** Request root to add the visual-only `/people/.../reassign-work` → My Staff rule through an intentional route predicate/alias, with no broad `/people` match that could claim unrelated future routes. The Staff row remains capability-gated; old off navigation stays unchanged.
3. **Legacy project Vacation — reconcile group and scope.** Keep its URL/loadability. Recommended group is People, with existing Vacation permission filtering, but first record that the current page reads its permitted people/date range rather than a project cohort. A group alias must not imply new project-specific filtering. Add the breadcrumb label and route assertion together; root decides whether this is a displayed sibling or only a legacy alias.
4. **Quiet project links right alignment.** They are direct quiet links, but `ProjectLayout.tsx:80–82` and `visual-tabs.css:114–140` place them immediately after the groups, without an auto-margin/right-side cluster. A small enabled-only alignment loan can fulfill FR-VSN-18 while retaining the same phone scroll region, order and accessible links.
5. **Neutral shell brand decoration.** `Shell.tsx:149` still uses `data-accent="mint"` plus `--acc-bg/fg` on the actual brand mark; there is no enabled neutral-decoration override there. Give only this decorative surface an intentional neutral/navy token role, retaining its letter, geometry/name and home link. Preserve v1/off mint. Do not globally strip accent attributes or recolor semantic metrics/status/identity.

Resource siblings (`workspace.tsx:258–263`) already use per-destination allowlists. Base project/person/unit/date filters have focused coverage. Home's Planner drilldown uses `homeDisciplineId` and `indicator=Above100`; Planner supports them, while Workload has no homeDisciplineId consumer. Planner's `teamProjectId` also is not a generic sibling filter. Treat cross-reader scope preservation/conversion as an explicit contract gap to resolve with Capacity/root, not permission to copy unsupported query keys or silently claim the same people/week. Retain existing workspace-tab behavior: base workspace scope is intentionally portable; Home-specific owner/discipline presets are not falsely advertised on unrelated Calendar/Files readers.

### Formal/spec conflicts for root, not silent code changes

- **Utility pages versus “every permitted route highlights one item.”** Notifications is explicitly Bell-only; Search and Preferences are global utilities. Current zero-parent utility behavior conflicts with the unrestricted FR-VSN-16 sentence. Recommended default: define explicit global-utility exceptions and their own selected Bell/account/search state, rather than inventing a misleading Tasks/Projects parent. Alternatively choose a personal parent explicitly. Record the policy before turning an exhaustive-route assertion green; keep E2 and formal navigation decisions Open.
- **Exact focus colour correction.** FR-VSN-13 expressly names `#9bb8ff` on navy. Current `index.css:125` defines `--on-navy-focus: var(--brand-tint)` (`#eaf0fd`). My earlier semantic proposal treated the blue focus colour as unlisted; that was incorrect because the spec explicitly permits it. Recommended narrow repair: set the named focus role to the specified `#9bb8ff`, preserve width2px/offset2px and assert its computed colour. Passing contrast and outline-width checks do not establish the exact hue requirement. This is an explicit approved focus role, not a new decorative palette.
- **Dashboard overlap.** FR-VSN-11 still requires Dashboard72/64px overlap. Checkpoint16 removed inherited overlap after root observed title/reference/control collisions and verified normal-flow geometry at1440/375. **Keep that verified fix.** Root should reconcile the nested project-band exception/wording or record the open decision; do not reintroduce the collision to satisfy literal text. My Staff's remaining card adoption should be dispatched with S06/Capacity ownership, not silently swept into Shell work.
- The wider colour budget, sidebar adoption and pilot activation decisions remain Open. Dark local implementation is authorized; formal approval, pilot, S10 cleanup and Accepted status are separate.

## Decoration and token evidence

`index.css:233–242` deliberately neutralizes marked person/section surfaces under v2. Section still carries a legacy data-accent attribute (`common.tsx:69`), but its rendered enabled background derives from `--raised`; this is not evidence that the old mint tint remains visible. The workspace scope label now explicitly uses named `--on-navy-secondary` (`workspace.tsx:349`, `visual-tabs.css:64–66`); its inherited muted utility class alone is not a remaining colour bug. Project header's legacy hash data-accent attribute also remains, while enabled CSS neutralizes its border/number presentation. Keep v1 selectors until authorized cleanup.

Conversely, the actual Shell brand mark still consumes legacy accent variables without the marker override. Count-card metric accents, six-domain status pills and stored project identity swatches are meaningful; they must retain their separate semantics. A named token can render the same RGB as a legacy alias, and a raw source attribute can be inert under an enabled override. Source-domain compliance and rendered-colour compliance need separate assertions; neither a text search nor a palette census alone proves both.

## Meaningful missing checks and ownership

Root owns `app/nav.ts`, route helpers/registrations, permissions/capability contracts, locales, shared common/Input/Sheet changes, shared spec/decision ledgers and every runtime/check. A future Home loan could name only `Shell.tsx`, `projects/ProjectLayout.tsx`, `hub/workspace.tsx`, the two visual CSS files and `index.css` as needed; it needs explicit exclusions around other current writers. Current generation14 additionally reserves Home.tsx/Workspace.cs/Files/WorkspaceTeam identity consumers to root. No page redesign, API widening, migration or new dependency is indicated by this audit.

Suggested checks after a new loan, executed once by root against the final integrated state:

- Extend existing `visual-navigation.cjs` rather than adding a parallel nav engine: real six-role capability baselines plus template-editor/unit-leader variants where behavior differs; exact main/foot order, no gaps/noMore, one correct parent for every **permitted** route family, named utility/error exceptions, legacy-off behavior. Use real me/API permission evidence for admission; fixtures alone cannot prove it. Existing eight profiles are capability probes, not the six-role route matrix, and only Vacation/Admin routes assert exact current parents.
- Extend `visual-tabs.cjs`: every project child above loads and selects its expected group or quiet link; legacy URLs/panel/filter links remain reachable; permitted People→Workload keeps projectId. Assert List↔Boards scalar Home cohort retention with >50 projects and old filters. Add cross-reader resource/project tests only after supported scope contracts are explicit; preserve current loading/empty/off checks.
- Extend `visual-drawer.cjs`: six-role exact list parity; repeated forward Tab and Shift+Tab through wrap; Escape/backdrop opener return; link navigation closes and gives deliberate destination focus; resize across768 while open leaves no stranded modal/focus; top and bottom Search open/close/result behavior;375px menu/scope/New/Bell/account/bottom controls and long labels stay usable. One existing Tab-inside assertion is not a full-cycle proof.
- Extend `visual-layout.cjs` source-import harness: long/unbroken names/numbers, long PM/client and status/health variants at desktop/375px, full-name keyboard discovery, visible metadata alternatives,72px bound and no clipped controls. Check actual necessary input/select/search/segmented boundaries against adjacent surfaces at3:1, exact navy focus hue/2px/2px offset and control state semantics. Measure rendered styles; do not infer ratios from a class name.
- Root's recorded source-import tests need the dev harness (previously8854); actual-app/mock tests use production preview (previously8853). Resolve current endpoints before execution. Re-run affected focused scripts and proportionate real-route/reflow supplements; do not repeat the complete census unless the shared change/risk warrants it. Native200% zoom/human acceptance remains a distinct unproven gate.

This proposal leaves all application/test sources held and checks no slice box. The immediate useful work is the small scope/parent/group/alignment/brand/focus batch after root resolves the named contract conflicts and explicitly grants ownership.
