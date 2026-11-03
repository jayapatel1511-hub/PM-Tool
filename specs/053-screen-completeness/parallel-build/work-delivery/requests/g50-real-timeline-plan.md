# Checkpoint 22: S14 real Timeline QA plan

Preparation only. This file is a proposed execution plan, not a runtime receipt or an acceptance claim. Root owns the final build, schema, migrations, disposable runtime, execution and evidence. No API, database, browser, service, build or test action was performed to prepare this plan. All prior B product source remains held.

## Runtime and identity boundary

- Future target only: database `hub_qa_checkpoint22`, API `http://127.0.0.1:5088/api/v1`, final production-build preview `http://127.0.0.1:8853`. Root must verify the preview proxies to this API/database and record the final build/source identity before execution. Do not use a stale preview, API test host, mocked route or production database.
- The QA21 plan and `checkpoint21-real-run.cjs` provide conventions only. QA21 project, person, discipline, task, dependency, workspace and saved-view IDs are invalid inputs for this run. Obtain every ID from QA22 responses; do not paste IDs from this document, screenshots or historical JSON.
- Require `GET /config` to answer `authMode: "Development"` before using `X-Dev-User`. Every API path below is relative to the API base. Use the ordinary Development accounts confirmed by fresh `/me` and `/admin/users`, not role-override headers. Never change authentication mode to make a check pass.
- Expected seed identities, to resolve afresh: Jordan (`jordan@hub.test`, Admin); Priya Nair (`priya@hub.test`, PM); Marc Dubois (`marc@hub.test`, Civil lead); Alex Chen (`alex@hub.test`, ordinary member); Omar (`omar@hub.test`, other-discipline lead). Missing/inactive or differently privileged accounts are a fixture blocker, not permission to substitute Admin for the tested actor.
- Real browser sessions use the existing Development sign-in flow; the QA21 convention is a fresh context with `sessionStorage['hub.devUser']='<actor>@hub.test'` and `sessionStorage['hub.signedIn']='1'` before navigation. Verify each context's actual `/me.id` and email. Do not intercept or replace API responses. Close each context in `finally`.
- All mutations below use maintained public commands with their actual optimistic versions. No SQL, ORM fixtures, test-only endpoints, direct timestamp edits, clock overrides, source edits or schema work are part of this plan.

## Settings preflight and restoration

As Jordan, retain `GET /admin/settings`, `GET /config` and `GET /me`. As each tested persona, retain `GET /me`; the settings source is `settings.today`, `settings.orgTimeZone`, `settings.dateFormat`, with permissions in `capabilities` and flags in `flags`. Check responses and stop on a failed prerequisite rather than continuing with partial setup.

The saved-view API/UI requires `work_screens`; Timeline itself is an existing canonical reader. For the final visual-system browser pass, root may enable `visual_system`, `small_ux` and `project_screens` as the explicit QA22 runtime profile. They are not substitutes for the `work_screens` guard. Independent layer checks additionally need `schedule_baseline` and `timeline_handoffs_chain`. Do not enable unrelated feature flags.

Normal Admin change: `PUT /admin/settings/flag.<flag>` with `{"value":true}` or `{"value":false}`. For the restricted-project refusal, normal Admin change is `PUT /admin/settings/restricted_projects_enabled` with `{"value":true}`. Record every prior value and every write/status, including settings already on; verify the resulting `/me.flags` in a fresh browser context. `/config` proves authentication mode; it is not the feature-flag source.

The expected disposable start/end profile has all six named feature flags off. Root must confirm that baseline before changing them. If any is unexpectedly on, record it and resolve the runtime profile before starting; do not silently claim a full-off restoration. Always restore in `finally`, including after setup/permission/browser failure: set all six named flags back to false, restore `restricted_projects_enabled` to its recorded original value, then retain fresh Admin settings and persona `/me` reads proving the values. Keep source defaults unchanged. Optional-feature off/on comparisons use fresh reads and contexts; stale cached settings are not evidence.

## Fresh synthetic fixture through normal commands

Use a unique run suffix in project numbers, names and saved-view names, for example `QA22-TL-<run>-A/B`. Record the exact generated names and actual response IDs. A collision should stop/replan, not reuse a partially mutated historical fixture.

1. As Jordan read `GET /admin/users`; as Priya read `GET /reference` and `GET /me`. Resolve active Civil (`code == "CIV"`) and an active different discipline, active client and office from these responses. The references supply global discipline IDs; project Timeline filters and task commands require the distinct project-discipline IDs returned by the project detail. Resolve users by actual email, not list order or display name alone.
2. Let `T = Priya /me.settings.today`. Use calendar-day arithmetic on the ISO date, not the browser's local day or a hardcoded date. Let `A=T-7`, `B=T+2`, `C=T+3`, `D=T+12`, `F=T-14`, `Z=T+42`; retain the expanded ISO dates and organisation zone in the fixture manifest. These dates intentionally straddle the organisation day without needing time travel.
3. As Priya `POST /projects` with the following shape, replacing every placeholder with a fresh response ID/value:

   ```json
   {
     "projectNumber":"<unique-A>","name":"QA22 synthetic Timeline A",
     "clientId":"<client>","officeId":"<office>",
     "startDate":"<F>","targetCompletionDate":"<Z>",
     "disciplines":[
       {"disciplineId":"<global-Civil>","leadUserId":"<Marc>"},
       {"disciplineId":"<global-other>","leadUserId":"<Omar>"}
     ],
     "members":[{"userId":"<Alex>","roles":["TeamMember"],"disciplineId":"<global-Civil>"}]
   }
   ```

   Expect 201 `{id,projectNumber}`. Priya becomes the actual project PM through the command; confirm it with `GET /projects/<P1>`. Save `PDcivil` and `PDother` from detail `.disciplines[].id` by joining their actual discipline reference. If necessary, activate using Priya `POST /projects/<P1>/transition` with `{"toStatus":"Active","rowVersion":<fresh-project-version>}`; reread detail. Do not assume the creation response has a version or project-discipline IDs.
4. As Priya create three dated, ordinary tasks using `POST /projects/<P1>/tasks`, expecting 201 `{id,key,rowVersion,warnings}`:

   | Name suffix | projectDisciplineId | assigneeId | startDate | dueDate |
   | --- | --- | --- | --- | --- |
   | Civil predecessor | PDcivil | Alex | A | B |
   | Civil successor | PDcivil | Alex | C | D |
   | Other discipline | PDother | Omar | A | D |

   Body for each: `{"name":"<unique name>","projectDisciplineId":"<PD>","assigneeId":"<user>","requiresReview":false,"startDate":"<date>","dueDate":"<date>"}`. No deliverable or milestone is required; these are loose tasks. No status transition is required to exercise saved views. Retain warnings instead of hiding them.
5. As Priya `POST /tasks/<Civil-successor>/dependencies` with `{"predecessorTaskId":"<Civil-predecessor>","type":"FS","lagDays":0,"note":"QA22 synthetic Timeline join"}`. Expect 201 `{id,warnings}`. The path task is the successor when `predecessorTaskId` is supplied; do not reverse the edge. Retain dependency ID `E`.
6. As Priya `POST /projects/<P1>/milestones` with `{"name":"<unique Timeline milestone>","milestoneType":"DesignSubmission","date":"<D>","projectDisciplineId":"<PDcivil>","isClientFacing":true}`. Expect 201 and retain actual milestone ID/key. Do not assume a milestone exists merely because the layer is available.
7. Create a second Active project `P2` as Priya through the same project/transition commands with one Civil discipline led by Priya and no Marc/Alex member. Create one dated Civil task assigned to Priya. Retain P2's own project-discipline ID. This supplies an explicit two-project workspace scope and, later, an isolated permission-refusal target. Keep P2 Open until the workspace/browser checks finish.
8. As Priya `POST /workspaces` with `{"name":"<unique Timeline scope>","projectIds":["<P1>","<P2>"]}`, expecting 201 `{id,name,projectIds,hidden,rowVersion}`. Save workspace ID `W`; it is personal to Priya. `GET /workspaces` as Marc must not list W.

## Canonical reads and ID joins before any view save

Retain successful responses as Priya and Marc where permitted:

- `GET /projects/<P1>`: actual PM, project-discipline IDs and actor membership.
- `GET /projects/<P1>/tasks?pageSize=200` and `GET /tasks/<each-created-task>`: the three actual task IDs, discipline IDs, original source dates and versions.
- `GET /projects/<P1>/timeline`: `.tasks` contains all three task IDs; Civil task rows join PDcivil; `.dependencies` contains E with the exact predecessor/successor IDs, FS and zero lag; `.milestones` contains the actual created milestone. Save the canonical task/deliverable/milestone/dependency sets and immutable source fields.
- `GET /timeline?projects=<P1>,<P2>`: `.projects[].project.id` is exactly the two permitted fixture IDs; each `.data` joins that project's canonical Timeline. Compare sets/sorted stable projections, not accidental response ordering. Retain `.truncated`/`.max` rather than claiming coverage beyond the response.
- `GET /timeline?projects=mine`: retain the actual current persona's whole permitted mine set; do not assert this seed database contains only the two fixture projects.

Capture another canonical snapshot after view operations and compare within the same flag configuration and actor. View reads/saves must not change source task/dependency/milestone IDs, dates, statuses, source versions or canonical scope. Do not compare optional wire fields across different flag configurations and call legitimate flag-dependent differences a mutation.

## Shared project view: Priya saves, Marc opens

Use the real browser as Priya at `/projects/<actual-P1-projectNumber>/timeline`. Set Civil using `#tl-discipline` to PDcivil; set `#tl-from` to F and `#tl-to` to Z; choose Zoom → `Month`. Expand the Civil loose-task group so both dependency endpoints render. A stable explicit URL for the same fixture state is:

```text
/projects/<projectNumber>/timeline?disciplineId=<PDcivil>&from=<F>&to=<Z>&zoom=month&group=discipline&cols=items%2Cdates&layers=dependencies%2Cmilestones%2CoriginalDates&collapsed=none&expanded=loose%3A<PDcivil>
```

Open `getByRole('button',{name:'Views',exact:true})`, then `getByRole('menuitem',{name:'Save current view…',exact:true})`. In the visible save dialog fill `getByLabel('Name',{exact:true})`, check `Share with everyone on this project`, leave default off, and click the dialog's exact `Save` button. Observe the actual `POST /views` request and 201 response, not just the toast.

Expected request contract:

```json
{
  "name":"<unique shared Civil view>","listType":"timeline","projectId":"<P1>","scope":"Project","isDefault":false,
  "params":{"disciplineId":"<PDcivil>","from":"<F>","to":"<Z>","zoom":"month","group":"discipline","cols":"items,dates","layers":"dependencies,milestones,originalDates","hideCompleted":"false","collapsed":"none","expanded":"loose:<PDcivil>"}
}
```

Set/order normalisation is allowed; compare the meaningful sets. `GET /views?listType=timeline&projectId=<P1>` as Priya and Marc must contain the same saved ID with scope Project and valid Civil/month/layout/layer/expansion parameters, with `canEdit:true` for both PM and DL. In a fresh Marc context, open the Views menu and the menuitem named by the saved view; the active menu button becomes that view's name. Verify the resulting URL, Civil selection, Month zoom, two Civil task rows and dependency arrow E; the other-discipline task remains in the canonical API response but is absent from the filtered rendered rows. A visible menu name alone does not prove application of the definition.

The canonical project Timeline request stays `/projects/<P1>/timeline`; do not require new server filter/layer parameters. The definition is a presentation preference, not new scheduling data.

## Workspace private scope, default and explicit URL

As Priya open `/timeline?projects=<P1>,<P2>&zoom=quarter&start=<F>&group=project&cols=items%2Cdates&layers=dependencies%2CoriginalDates&collapsed=none`. Save through the real Views dialog as a private view with `Open this list in this view by default` checked. Workspace sharing must not be offered. Observe the actual POST201 and retain the view ID/version.

Expected body: `listType:"workspace-timeline"`, `projectId:null` or omitted, `scope:"Personal"`, `isDefault:true`, and params `projects:"<P1>,<P2>"`, `zoom:"quarter"`, `start:F`, `group:"project"`, `cols:"items,dates"`, `layers:"dependencies,originalDates"`, `hideCompleted:"false"`, `collapsed:"none"`. Confirm `GET /views?listType=workspace-timeline` as Priya contains it, `canShare:false`, and Marc's response does not contain its ID. Requests to update/delete Priya's private definition as Marc must return404.

In fresh Priya contexts:

1. Open bare `/timeline`: the default applies with the saved explicit two-project scope, Quarter, start and layers. Join rendered project headers and observed canonical `/timeline?projects=...` response to P1/P2.
2. Open explicit `/timeline?projects=mine&zoom=week`: explicit URL beats the default; save another private view and confirm captured `projects:"mine"`, `zoom:"week"`. Compare the rendered project set with this actor's fresh `/timeline?projects=mine` response. Do not use `?zoom=week` alone: the existing workspace preference may remember a previous project scope.
3. Open `/timeline?ws=<W>&zoom=quarter&start=<F>` and save a separate private named-scope view. Capture `ws:W` and no competing `projects` key. Reopen it and reconcile W's actual permitted project IDs with the canonical Timeline request; ws is a client scope identifier, not a new Timeline API parameter.

## Read-only UI joins, return and presentation masking

Use 1440×1000 and 375×812; add 768px for the filter/scroll transition if the final build differs there. Wait for the real expected API responses and visible fixture rows, not a fixed delay.

- Scope task-name selectors to `page.locator('[role="rowheader"] button').filter({hasText:<unique-task-name>})`; the date bar may repeat the task name in an accessible label. Do not count global buttons/listitems or match duplicate task labels across navigation and chart.
- Open the Filters button (`getByRole('button',{name:/^Filters/})`) when controls are in a phone drawer. Scope layer checkboxes to `page.locator('[data-timeline-layer-controls]:visible')`; English labels are `Dependencies`, `Milestones`, `Original dates`, and, only when independently available, `Baseline dates` and project-only `Handoffs`. Close the drawer with Escape before clicking obscured chart rows.
- Project chart: `getByRole('region',{name:'Timeline',exact:true})`. Dependency presentation: `[data-timeline-layer="dependencies"]`. Both Civil endpoints must be expanded and rendered before asserting an arrow. Loose expansion is `expanded=loose:<PDcivil>`; if a deliverable fixture is substituted, use its actual deliverable ID and its `Show the <n> tasks of <key>` expansion button instead.
- Toggle Dependencies off/on; the arrow presentation disappears/reappears, while source task rows, row counts, dates and the canonical response stay unchanged. Toggle every offered layer, then all off: captured `layers:"none"` must survive reload/save. Absence of `layers` means defaults and is not equivalent to none.
- Milestone assertions join the actual milestone ID/name/date with the rendered milestone button/diamond. Original-date spans use `[title^="Original dates"]`; baseline spans use `[title^="PM baseline"]`. Assert these only when canonical facts and their existing per-record slip visibility permit them. A fresh unshifted fixture may correctly render no original/baseline span. Do not fabricate schedule history to force an ornament.
- Click the unique Civil task-name row button, verify `panel=Task:<actual-task-id>` and real `GET /tasks/<id>`, then close the last dialog's exact Close button. The prior view/filter/scope/group/columns/layers/expansion/zoom and workspace start parameters must survive. Compare the relevant parameter map; panel and transient taskSection are not saved definition parameters.
- Verify the project's exact `Print` button still invokes the existing print path. Record an observed print event/dialog if the chosen browser harness supports it; do not infer printing from button presence. No task/date/dependency command is allowed during this check.
- Check `document.documentElement.scrollWidth <= innerWidth + 1`; chart horizontal scrolling belongs inside the Timeline region. Record viewport, route, row/header joins, overflow and a settled screenshot. Phone controls must remain reachable by keyboard.
- During read/toggle/apply/panel/print phases observe every non-GET/HEAD `/api/` request and reject or fail unexpected business writes. Permit only explicitly scheduled view saves/edits/deletes or the normal sign-in POST, with an exact allowlist by phase. Do not install a broad fallback that silently fulfils unknown commands. Record observed request counts; never print a hardcoded `businessWrites:0`.

## Permission, validation and version refusals

Perform these as ordinary API commands on the disposable fixture after positive UI joins. Record exact request/status/error and before/after view lists; a refused command must not silently create or alter a definition.

| Actor and command | Expected result / join |
| --- | --- |
| Alex POST `/views`, `listType:"timeline"`, P1, `scope:"Project"`, otherwise valid shared params | 403; Alex is a real member, not PM/DL. |
| Alex PATCH `/views/<shared-id>` with its current rowVersion and a new name | 403; list row has `canEdit:false`. |
| Marc PATCH `/views/<shared-id>` with current `rowVersion`, params `zoom:"week", layers:"none"` plus valid existing params | 200; retain new rowVersion. |
| Priya PATCH the same shared view using the previous stale rowVersion | 409; latest valid definition remains intact. Restore the desired shared state only with a newly read current version. |
| Marc PATCH or DELETE Priya's workspace-private saved view | 404; Priya's definition remains present. |
| Priya POST workspace-timeline with `scope:"Project"`, or project timeline without projectId, or workspace-timeline with projectId | 400 validation refusal; no new row. |
| Priya POST otherwise valid definition with `zoom:"year"`, `group:"assignee"`, `cols:"name,estimate"`, `layers:"madeUpLayer"`, impossible ISO date, or reversed from/to | 400 validation refusal; no new row. Run one invalid known value per request. |

For versions, either use numeric JSON `rowVersion` in the PATCH body or the header `If-Match: "<decimal rowVersion>"` from that record's latest read. The maintained parser accepts the quoted numeric header. Do not use the parent project's version for a view or a nested project discipline. Omitting a required version is a separate 428 precondition error, not the intended stale 409 check. Avoid invoking membership removal just to prove view permissions.

For a real inaccessible-project case, use P2 after workspace checks. With `restricted_projects_enabled` temporarily enabled through Admin, Priya PATCH `/projects/<P2>` with fresh P2 `rowVersion` and `visibility:"Restricted"`. Priya may first create a valid shared P2 Timeline definition. Confirm Marc has no P2 membership/lead role and is not Admin/Executive. Marc GET `/projects/<P2>/timeline` and `/views?listType=timeline&projectId=<P2>` must return404; a known view ID cannot widen project visibility. Restore P2 Open with a fresh project version before any later two-project check. This is not covered by an Admin screenshot or by a direct database visibility edit.

## Inactive discipline retention

Run after ordinary Civil-view checks to avoid corrupting their setup assumptions. Retain P1 detail and the Civil Timeline/task snapshots. As Priya PATCH `/projects/<P1>/disciplines/<PDcivil>` with `{"isActive":false}` and the Civil project-discipline's own current If-Match/version. Expect204. Reread detail and canonical Timeline: the Civil project discipline is inactive but its existing dated tasks remain, with unchanged task IDs and source dates.

Read/reopen the existing Civil shared view as Marc; `disciplineId:PDcivil` must remain valid, must not appear in `dropped`, and the retained Civil work must render. Capture a fresh view as needed through the normal save command to prove inactive-but-existing project discipline validation on write. Compare the canonical read again. Restore `isActive:true` through the same normal PATCH using a freshly read discipline version before concluding.

## Independent flags and the all-off guard

1. While work_screens remains on, enable schedule_baseline and timeline_handoffs_chain through Admin. Save a separate project definition using `layers:"baselineDates,dependencies,handoffs,milestones,originalDates"` and `chainTo:<actual-P1-milestone-id>`. Confirm the named params survive a normal read. Baseline/handoff data must only be asserted if actually present; available controls alone prove no rendered-record join.
2. Turn the two optional flags off. Fresh GET `/views?...` must omit the entire invalid `layers` key and report `"layers"` in `dropped`; it also drops/reports `chainTo`. The validator does not retain just the supported subset. The available project controls omit Baseline dates/Handoffs; workspace never offers Handoffs. Capture canonical before/after view reads within this off configuration.
3. Reenable the optional flags and read again: the stored full layers value and chainTo return. This normal off/read/on/read round trip demonstrates the read did not rewrite the definition; no DB inspection is needed. Capture canonical before/after view reads within the on configuration, not across on/off.
4. Turn work_screens off, testing both small_ux false and true in the disposable runtime. With fresh settings, GET/POST new Timeline saved-view APIs and PATCH/DELETE existing Timeline definitions must return404. Fresh project/workspace pages must show the established Timeline, no new Views/layer controls and no `/views?listType=timeline` or workspace-timeline query. `layers` and new project `expanded` URL params must not activate the new presentation path. Verify the old chart read/interaction path and canonical reads remain available; do not expect an existing Timeline itself to404.
5. Briefly reenable work_screens and read both saved-view list types: earlier definitions must still exist under their actual IDs. This is the retention join for the all-off guard, not permission to leave flags on. Then perform and record the complete final restoration described above.

## Receipts for root to produce during execution

Use new checkpoint22 S14 filenames, for example `checkpoint22-real-timeline-api.json`, `checkpoint22-real-timeline-browser.json`, `checkpoint22-real-timeline-summary.json` and settled `checkpoint22-real-timeline-{project,workspace}-{1440,375}.png` under the current checkpoint22 evidence location chosen by root. No receipt is claimed to exist by this plan.

The manifest should contain actual target/build identities, actors and capabilities, fixture IDs, expanded organisation dates, request method/path/status, command versions, saved-view definitions/dropped keys, canonical before/after projections, rendered ID joins, routes, viewport/containment, observed mutations, errors, and original/final settings. Keep credentials out. Record PASS/FAIL/BLOCKED/UNPROVEN separately for each join. Missing source history, unavailable print automation or an unrun optional rendering join stays UNPROVEN; it must not be bundled into an overall browser PASS. Human acceptance, deployment and decision closure remain outside this local QA plan.

## Maintained-source references used to prepare this plan

- `src/Hub.Api/Features/{Projects,Tasks,Dependencies,Milestones,Workspace,Timeline,Views,TimelineViewParameters,Team,Me,Modules,Admin}.cs` — ordinary DTOs, routes, guards and read contracts.
- `src/Hub.Domain/{Permissions,Settings,Flags}.cs` — share authority, restricted-project setting and feature flags.
- `web/src/pages/projects/{Timeline,timeline-view-controls}.tsx`, `web/src/pages/Gantt.tsx` — chart controls, real selectors, expansion and read paths.
- `tests/Hub.Tests/Api/TimelineSavedViewTests.cs`, `web/tests/timeline-saved-views.cjs` — authored focused acceptance contracts; reading them is not execution evidence.
- `specs/053-screen-completeness/evidence/checkpoint21-real-plan.md` and `checkpoint21-real-run.cjs` — historical disposable-runtime/auth/receipt conventions only. Their IDs and prior outcomes are not QA22 evidence.
