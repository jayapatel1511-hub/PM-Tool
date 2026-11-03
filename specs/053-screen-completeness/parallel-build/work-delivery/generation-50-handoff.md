# Generation50 S14 Timeline saved views

ALL SOURCE READY / HELD. Backend API tests were returned READY; root-directed set-comparison correction was applied and C# immediately re-held. No source/test edit or lane process is in flight. No checks, compilation/types/lint/build, API/SQL/browser/runtime, generator, helper/agent, goal/status, commit/push/deploy or activation was performed here. No checkbox or decision was changed. Authored cases below are not PASS evidence.

## Owned changes

- `projects/Timeline.tsx`: work_screens-only existing ViewMenu, project personal/shared definitions, URL-backed collapsed/expanded state, exact effective capture, and bounded shown-layer controls inside the existing Filters disclosure. Existing filter queries, schedule reads, graph/date math, main bars, edit/preview/print/critical-path/panel paths remain. Layout toggles preserve URL filters, panel/hash/router state/scroll; existing filters/zoom retain their normal behavior. Off keeps original local expansion and literal original renderer.
- `Gantt.tsx`: workspace personal ViewMenu, explicit effective scope/start/zoom/hideCompleted/collapse capture and matching layer controls. Off preserves existing local/small_ux URL-collapse behavior and old canonical read. No new workspace grouping or column selector.
- `projects/timeline-view-state.ts` and `timeline-view-controls.tsx` (new): whitelist capture; canonical sets; valid zoom/calendar start parser; fixed actual group/columns; accessible presentation switches. No new HTTP read, schedule filter, business command, schema or permission.
- `TimelineSavedViewTests.cs` (new):6 Facts +2 small_ux Theory cases (8 cases), authored/unrun; root owns all required View/ScreenLists validation/guards. Root-directed repairs cover independent baseline/chain read-cleaning and a normal deactivated-discipline/retained-task saved filter, matching the actual Timeline population.
- `web/tests/timeline-saved-views.cjs` (new): actual-app/mock-API browser source, authored/unrun; existing preview/env/fixture conventions, starts no runtime. API cases own real authorisation/persistence proof; browser's two actors are presentation fixtures, not a real authentication test.
- `requests/g50-timeline-locales.json`: exact6 labels for root-owned locale merge.

No Tasks/TaskQueries/Calendar/TaskPanel/Views/ScreenLists/shared metadata/cache/generated/schema/S10 producer or S13 source was edited by B. All prior work is retained.

## Final shared contract

List types: **timeline** requires projectId; personal/shared under existing project-view policy. **workspace-timeline** is projectless personal only. New routes/mutations/reads are work_screens-gated; off UI mounts no ViewMenu and starts no new view query. Root owns these guards and registry/generated integration.

Project capture keys: disciplineId,milestoneId,status,hideCompleted,from,to,zoom,chainTo,collapsed,expanded,group,cols,layers. Workspace: projects,ws,zoom,start,hideCompleted,collapsed,group,cols,layers. Capture never includes panel/taskSection/view/page/drag/move/preview. Root validates scoped references and known statuses from the actual8-value project Timeline STATUSES, strict calendar dates/range, zoom/layer/layout enums and existing permissions. Invalid write refused; existing dropped-reference read policy names lost parameters.

Actual fixed layout is group=discipline on project, group=project on workspace, cols=items,dates in both. These are persisted truthful fixed values; there are no invented adjustable grouping/column controls. Workspace effective default scope explicitly writes projects=mine; other choices write all/none/IDs, or ws alone for named scope. Saved definitions are live views, never stored schedule results or a grant to read hidden projects.

Zoom is week/month/quarter, default month. Workspace capture stores the actual displayed start anchor (strict YYYY-MM-DD, year1–9999); ordinary existing project From/To filters remain. Project collapsed = none or comma PD IDs/handoffs; expanded = none or comma deliverable IDs/loose:<PD-ID>. Workspace collapsed = none/all/comma project IDs. Set output is sorted/deduplicated; none/all are explicit sentinels. Root's canonicalization can reorder references, so the focused API test compares set semantics where appropriate.

## Final layer guard rules

- Common IDs dependencies,milestones,originalDates; baselineDates is offered/effective only when schedule_baseline is on. Project additionally offers/effectively renders handoffs only while timeline_handoffs_chain is on; workspace never offers handoffs.
- Missing layers means all currently supported choices ON, matching the prior literal render. Explicit **layers=none** means all off; otherwise sorted/deduplicated allowed IDs. Save captures the effective shown set, so no empty value is lost by existing saved-view apply behavior.
- A saved layer never enables its independent feature flag. Current canonical record predicates still control actual marks: slipShown/baselineSlipShown, date presence/range, cancelled/completed filters, current visible/expanded dependency endpoints, permissions and supported handoff fields.
- Layer choices mask only existing graph/diamond/original/baseline/handoff presentation. Underlying source records, source totals, timeline HTTP scope, target-date/critical-path math, milestone/drop rules and edit rights are unchanged. Original/baseline geometry remains available to existing range calculation even when hidden. Unscheduled data is not converted to zero or discarded by a layer switch.
- With work_screens off all new layer/expansion semantics are ignored: the old local/canonical behavior stays intact, including the existing small_ux workspace collapse contract. No new ViewMenu query or seen-state write.

## Authored acceptance

API8 cases:

1. Priya shares a Civil/Months/dependencies project view; Marc reads the same definition and canonical Civil task IDs. Transient keys drop; grouping/columns explicit; canonical Timeline response unchanged by view-only query params.
2. Workspace private view retains explicit two-project scope/Quarter/start/layers/hide/collapse/default; another person sees none; named own workspace scope retained and sharing refused.
3. Existing member sharing/edit refusal, PM/DL shared update, stale version refusal and revoked project read404.
4. Invalid zoom/group/columns/layers/calendar dates/bool/reversed range and mismatched project requirements refuse without creating a saved view.
5. A normal team command deactivates Civil while its dated Task remains on the canonical Timeline; the shared saved discipline filter is retained for Marc and not reported dropped. No saved-view read/write changes that retained work.
6. Independent baseline/chain flags on allow those layers/chainTo; turning them off drops and names the unavailable parameters on read without rewriting the stored definition. Canonical Timeline is compared before/after view reads within each flag configuration (the flags themselves legitimately change optional fields). Re-enable restores the original definition.
7–8. Both small_ux values: new view CRUD refuses while work_screens off, definitions retained for re-enable and old canonical Timeline read unchanged.

Browser source:

- Priya selects Civil and expands the two actual tasks; dependencies visible. Original/baseline choices hide their actual marks with0 new schedule HTTP reads; sharing captures explicit Month, filters, layers, expansion and fixed layout.
- Marc reopens that shared definition with same Civil rows, Month and dependencies. Electrical rows stay excluded. Keyboard item open/close preserves exact source view URL; existing Print still calls print.
- Keyboard layer change hides arrows while task rows remain. Explicit none yields0 selected layer checkboxes. Independent baseline/handoff flags mask stale on-shaped fixture facts and show3 versus5 supported choices.
- Private workspace Quarter view captures two projects and effective start; default applies on an empty URL, explicit scope/Week URL wins. A separate `projects=mine&zoom=week` navigation explicitly selects My projects before saving, respecting the existing remembered scope.
-375/768/1280 Filters controls and page containment; both small_ux off variants retain no Views/layer UI/query and original local project expansion. A real request listener records every non-GET/HEAD API request, and a final top-level route interceptor rejects unexpected mutations before the permissive platform fallback. Assertions allow only3 intended POST /views and optional POST /me/sign-in baseline, require an empty business-mutation collection, and derive the reported count from that collection. Source schedule/permission data stays unchanged; canonical reads contain only original scope parameters.

Root next: integrate shared guards/validation/locales/generated contract, independently review exact source, freeze, run focused API/adjacent Timeline/Views checks plus required type/fixture/lint/build and new browser suite on the built preview, then join real authorised API/UI evidence. Known source/runtime limitations remain until that verification; this handoff does not check S14.
