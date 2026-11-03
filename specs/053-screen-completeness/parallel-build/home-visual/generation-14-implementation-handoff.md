# Generation14 supplemental implementation: READY / held

All owned application and browser-script edits are finished and held. No C#/TS/CSS/test mutation or lane process is in flight. Root owns integration, independent review and execution. This handoff supersedes only the narrow repaired items in the earlier read-only audit; 052 S02/S07/S08 and the wider goal remain incomplete.

Generation15 independent-review repair is also READY / held. Only Shell's breadcrumb alias guard and visual-navigation/tabs scripts changed in this narrow exception. Breadcrumb aliases now require `!me.capabilities.readOnly`, matching ProjectLayout. The tab script exercises a synthetic403 embedded Vacation read and asserts no allowed-only breadcrumb or project-group selection, Projects retained as the permitted shell parent and Resources hidden. It excludes only the expected network403 for the exact request explicitly marked as denied; other console/page errors still fail. The navigation script opens global Vacation for legacy profiles; its initially incorrect own-row expectation was corrected by the generation16 script-only exception below. Root separately restored baseline planner.also=['/workload'] and moved Vacation to visualAlso; Home did not edit app/nav.ts. No permission/API/data change and no execution/check occurred here; C# FROZEN generation15 and all-source hold remain.

Generation16 script-only baseline correction is READY / held. `git show HEAD:web/src/app/nav.ts` confirms the legacy BUILT set excludes Vacation, so the old route has no Vacation row and zero current rail rows. The previous own-row assertion failed in root's `/private/tmp/tuesday-checkpoint17-browser-visual-navigation.log`; it was the wrong expectation. Only visual-navigation.cjs now asserts the verified baseline: no Vacation row/current marker, neither Resources nor Time highlighted, More retained. Enabled Resources/Time behavior is still tested exactly. No application source/BUILT was changed and no full legacy-navigation acceptance is claimed. No check/test/browser ran here; ALL SOURCE FROZEN generation16 is acknowledged after this named correction.

## Product changes

- `web/src/index.css`: `--on-navy-focus` is the explicitly specified `#9bb8ff`; its comment now acknowledges the FR-VSN-13 focus role. A selector scoped to v2 and `data-visual-decoration="brand"` sets only that surface to navy on raised. Person/section neutrality and metric/status/stored identity roles are untouched.
- `web/src/app/Shell.tsx`: marks the existing brand letter with that decoration role, retains old mint attributes/classes for off behavior, and passes pathname to root's visual-only alias callback. Breadcrumbs add the exported legacy People aliases only when visual_system is enabled. No utility-route parent or utility policy was invented.
- `web/src/pages/projects/ProjectLayout.tsx`: exports the existing Vacation URL as an enabled-only People alias used for selection/breadcrumbs, gated by its existing ReadOnly capability rule. It is not added to visible project siblings or to the legacy PROJECT_TABS inventory. The first quiet link gets a class whose auto start margin puts Activity/Settings at the right end of the desktop row; mobile order/scroll remains intact.
- `web/src/components/hub/visual-tabs.css`: adds that quiet-cluster margin only; no dimensions, link destinations or other tab styles changed in this batch.
- `web/src/components/hub/workspace.tsx`: only `/projects` → `/projects` List/Boards links retain `homePreset`/`homeProjects`, and only while home_overview is enabled. It preserves existing base filters and scalar values; it never expands a derived cohort into IDs. Portfolio and other workspace/resource destinations do not receive unsupported filters.
- `web/src/pages/Vacation.tsx`: only the embedded enabled view uses the supplied subtitle, “Availability for people you can view across projects.” It uses the existing InProject context. Global/off subtitle, request, permitted people, date range, unit filters and resource links remain unchanged; no project-specific filtering was invented.

Root applied request012 first: app/nav.ts's callback takes `(me, pathname)` and the existing permission-filtered Staff row matches only `/^\/people\/[^/]+\/reassign-work$/`; the locale `availability.projectScope` exists. Home did not edit either root-owned file. Root did not write Shell concurrently.

## Prepared verification — not executed here

Extended existing scripts only:

| Script | Added meaningful cases |
| --- | --- |
| `web/tests/visual-navigation.cjs` | Exact reassign parent for visible Staff; no selection/Staff offer when hidden; three nearby nonmatching routes; a legacy Staff profile proving off alias exclusion. Supplies valid existing Home/views fixtures and now refuses page/console errors instead of ignoring them. This is capability/selection evidence when run, not actual endpoint permission proof. |
| `web/tests/visual-tabs.cjs` | Desktop quiet-cluster geometry; enabled embedded Vacation People/breadcrumb selection without a new sibling; visible permitted-person fixture and unchanged no-project-filter API request; global/off subtitle and legacy breadcrumb/group parity. A52-row project fixture traverses List→Boards→List with exact scalar Home/base values, no derived IDs, no unsupported Portfolio keys and no Home keys when overview is off. Phone Settings is scrolled into view and opened by keyboard, preserving page containment. Existing order/deep-link/filter/loading/empty/resource cases remain. |
| `web/tests/visual-layout.cjs` | Exact navy focus RGB155/184/255,2px width and2px offset; neutral/navy brand role with unchanged32px geometry; exact captured legacy brand appearance restored after switching the flag off. Existing title/button/phone assertions remain. |
| `web/tests/visual-drawer.cjs` | Actual backward/forward boundary wrap and a complete keyboard cycle; Escape/backdrop/route-navigation opener return; separate top/bottom Search opener returns; existing search-result navigation and legacy/bottom/page-containment assertions remain. Uses the valid Home fixture for drawer→Home navigation. |

No syntax/type/lint/build/API/Chrome/axe check ran in this lane, including no node --check or git diff --check. Read-only source inspection and patch writing are the only actions. There is no passing-runtime claim.

Root should execute the four changed scripts once after its integrated final-source review/freeze and required compile/type/lint checks. `visual-layout.cjs` imports actual source modules and needs the root's Vite development harness (previously8854); the other three run against the rebuilt app preview (previously8853). Set the normal local `TUESDAY_UI_BASE_URL` and `CHROME_EXECUTABLE_PATH`; verify current endpoints rather than assuming an old server is still running. No API contract/schema migration was introduced by this batch. The broader semantic/system regressions and proportionate real-route/reflow supplements remain root's integration decision.

## Preserved boundaries and remaining work

- Utilities Search/Notifications/Preferences retain current zero sidebar selection. The unrestricted FR-VSN-16 sentence versus explicit global utilities remains a proposed-policy conflict; no spec/decision was changed here.
- Jay's rule is unchanged: Resources hidden without Workload; allowed global Vacation highlights Time. The embedded legacy URL remains under Projects with a People alias and a visible broad-people scope disclosure.
- Portfolio Home-preset and Planner home-discipline cross-reader bridges stay deferred. No unsupported scope guarantee is made.
- Root's Workspace/Home/WorkspaceTeam/Files identity edits and checkpoint16's verified Dashboard normal flow were not touched. Keep that Dashboard fix while root reconciles the formal overlap wording.
- Broader six-role/all-route loading/admission, all-screen band/header/boundary census, resize-open modal behavior, native200% zoom, pilot and human acceptance are not closed by these focused prepared scripts.
- No schema, permissions, common/Page/Sheet, registrations, generated files, shared spec ledgers or decisions were edited. No helper, goal, migration, commit, push, deployment or activation occurred.

ALL SOURCE READY / held generation14. Root may now review and freeze/integrate these exact files; no additional implementation is running here.
