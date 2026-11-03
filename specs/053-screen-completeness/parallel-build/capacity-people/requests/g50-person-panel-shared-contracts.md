# G50 D S43 exact early shared contracts

Approved basis: g47-person-panel-contract.md + generation50-brief.md. D owns new PersonPanel.cs/MemberResponsibility.cs, Team.cs, Team pages/new UI/helpers/tests only. Root owns every shared seam below; no independent generators/execution. Prior S42 base is preserved.

## Root model / permission / scope / module requests

1. Add only `ProjectMember.ResponsibilityNote: string?` (nullable varchar80, no default/backfill) and the additive mapping/migration/snapshot. Add ResponsibilityNote to existing ProjectMemberRecord audit allow-list, retaining history=false. Root owns field/activity labels; normal version/audit/evaluation hooks remain.
2. Exact permission helper requested: `Permissions.EditMemberResponsibility(Actor actor, ProjectContext project, Guid? primaryDisciplineId, bool primaryDisciplineActive): Allow`. Apply existing Writable first, then existing IsPM OR (primary nonnull, active true, IsDL(project,primary)). D ensures primary belongs to this same project in the DB predicate. Existing Admin-as-PM and Complete/ReadOnly/Archived/Cancelled behavior stays canonical; no generic member-PATCH widening. Existing `perm.pm_or_dl` may supply the refusal text.
3. Exact Staff authority seam requested in MyWork.cs: `internal static Task<bool> CanReadPanelStaffPerson(HubDb db, Access access, Guid personId, string? scope, SettingsStore store)`. Reuse/factor existing StaffScope with showInactive:true, guard actual ViewStaff/unit-leader access first. No Staff permission => false without a forbidden all-directory query. A supplied scope must be validated through the existing all/reports/unit authority; no synthetic scope grant. D only invokes it when needed for Staff fallback or when an explicit staffScope was supplied. Explicit supplied staffScope must include the target or panel404; self/Admin/visible-current-member authorisation handles ordinary calls without that context.
4. Exact full-facts date seam requested: public/internal `PersonFacts.Read(HubDb db, Guid[] authorisedIds, OrgSettings settings, DateOnly today)` delegating to existing ReadAt behind capacity_screens. Existing overloads/expiry math unchanged. D captures one organisation day/UTC instant in PersonPanel read snapshot.
5. Register `PersonPanelEndpoints.Map(api)` and `MemberResponsibilityEndpoints.Map(api)` in the existing module mapping. D supplies only these two new static classes/Map methods. Routes: GET `/people/{userId:guid}/panel` (optional staffScope, projectsPage=1, projectsPageSize=25 max100); PATCH `/projects/{projectId:guid}/members/{memberId:guid}/responsibility` (If-Match or body rowVersion). Both require people_screens. Root regenerates API contracts after backend READY.

## D DTOs / Team bridge

D's new `MemberResponsibilityEndpoints.ResponsibilityRead(string? Note,bool CanEdit,string? EditReason)` is appended to ProjectTeamMember as optional null-omitted Responsibility. Existing outer member RowVersion remains the note edit baseline. D will include ResponsibilityNote in private MemberRead and derive permission from same-project active discipline metadata after the unchanged member/cert cohort. All generic role/primary commands stay held.

Note body will reject unmapped fields and require the responsibilityNote property (nullable explicit clear), with optional RowVersion/Reason. Saved acknowledgement returns member ID/current member RowVersion/normalised note. Normal correction reason only where ProjectStatus.NeedsCorrectionReason. No roles/staffing/capacity/follow mutation.

PersonPanelRead fields: Id,DisplayName,JobTitle?,IsActive,OrganisationFacts,optional PersonFacts,Today,Week,AsOf,Projects (Items/Page/PageSize/Total). Project items: MemberId,ProjectId,ProjectNumber,ProjectName,ProjectStatus,Roles,PrimaryDiscipline reference?,LeadOf reference[],ResponsibilityNote?. No contact/HR/private allocation fields. Only Access.VisibleProjects current memberships; true visible count and stable project-number/ID paging. Root may request a naming refinement before generation; no old required shape changes.

Optional root Workspace.TeamProjectRow bridge: append null-omitted ResponsibilityNote only while people_screens on, from the exact member row already used in each visible Team project reference. This lets the workspace show the same project-scoped note alongside its roles without another endpoint. It never joins notes across projects or changes people/counts. D can omit workspace inline note until bridge is provided, but panel and project Team are required.

## Shared UI / A Staff signature

D supplies `web/src/components/hub/person-panel.tsx` export `PersonPanel({id,onClose}:PanelProps)` and `person-panel-trigger.tsx` export:

```ts
PersonPanelTrigger({ userId, name, active = true, staffScope, className, children })
// userId:string; name:string; active?:boolean; staffScope?:string|null;
// className?:string; children?:ReactNode. Off => original children/name, no button/fetch.
```

This signature is ready for A's passive Staff card name at Staff.tsx current line154. A alone owns Staff.tsx. The existing desktop Staff name is already a My Work link (current line189): preserve that navigation; add a separately labelled person-details trigger beside it if root approves, never nest buttons or replace its existing destination. Default trigger shows name/inactive text; supplied children preserve passive formatting. Team/WorkspaceTeam supply exact userId, not memberId/email. Picker entries and editable owner controls keep their existing selection/edit actions.

ROOT REQUEST: add PANELS.Person lazy registration for this export behind people_screens, no fullPage route. Root supplies `itemType.Person` text. Extend `useItemPanel` with optional fourth argument `{ personStaffScope?: string }` used only for Person opens; set/remove the namespaced personStaffScope and reset only personProjectsPage/personProjectsPageSize on a new Person selection. Existing three-argument callers remain unchanged. PersonPanel reads these namespaced values; preserve all underlying route scope/list params.

ROOT RETURN REQUEST: existing captureRecordContextOrigin should optionally capture a real trigger element ID as focusId; extend safe recordContextReturn/Shell restore to focus that existing element (getElementById), otherwise safe page/list content. D trigger assigns useId() to its button and calls existing capture function. Do not put DOM references in history state. Direct Person close clears only panel + personStaffScope/personProjectsPage/personProjectsPageSize; origin-backed close restores its exact prior URL/state/scroll/nested chain. Root owns shared panel-host/record-context-navigation/Shell changes. No Person full-profile route or new permission-bearing ItemType entity.

## Cache / metadata / locale requests

D panel key: `['person-panel', viewerId, personId, staffScope??null, projectsPage, projectsPageSize, capacityFlag, orgFlag]`, enabled only people_screens. Existing candidate capacity key/hook for [personId] + response.Week; no capacity request without workload capability, no fallback directory fill.

Root refreshes person-panel alongside Team for Member/note/primary/lead/Project visibility/status/User/reference/placement/facts/cert/settings/flag changes, preserving exact actor/context. Responsibility note editor invalidates project Team + workspace Team + person-panel on success only; capacity is not recomputed client-side from a note. Access404/error must not continue displaying stale query data. Panel-specific query keys remain transient and must not become saved list filters. Root owns keys/registry/record metadata; D will submit locales JSON before frontend READY.

Normal note-only member save still triggers existing Member audit/live/evaluation hooks. Verify canonical values unchanged rather than asserting no generic outbox/evaluation work. All S29/S42 sources and tests retained; no migration/model/permission/module/registration/cache/locale edits by D.


## Root seam alignment after initial response

D uses root's exact `Permissions.SetMemberResponsibility(Actor,ProjectContext,bool)` and `MyWorkEndpoints.StaffScope(db,access,scope,true,store,snapshotSettings)` signatures. The latter's return type `PersonRow` must also be internal (currently private sealed record), otherwise its new internal method has inconsistent accessibility and D cannot inspect authorised IDs. New panel validates direct/all/reports/unit syntax and org/indirect flags before using StaffScope; legacy Staff callers are unchanged.

Endpoint classes/Map are exactly PersonPanelEndpoints.Map(api) and MemberResponsibilityEndpoints.Map(api). Team optional DTO is exactly ResponsibilityRead(string? Note,bool CanEdit,string? EditReason). Workspace note bridge remains null-omitted ResponsibilityNote. UI exports/signature above unchanged. Module root registration can proceed.


## Frontend review seam and remaining passive-name inventory

Shared `usePeopleCapacity` currently builds cells from query data even if that query errors. Root should omit errored query data when assembling candidate cells, so a failed403/404 after planner scope changes cannot display a cached figure. D does not edit shared people.tsx. Panel's own q.error already clears all previously cached person facts and disables the capacity read.

Wired in D: project Team desktop+phone names and passive discipline-lead names (editable PeoplePicker unchanged); workspace Team name and same-project note bridge. A has wired Staff mobile name and separate desktop details icon while preserving My Work link. Remaining exact passive surfaces are loans, not claims of completion:

- Workload.tsx current line218: p.displayName with p.id.
- PlannerGrid.tsx current line72: person.displayName with person.id; preserve adjacent expand button.
- Planner.tsx EntryList current line140: r.personName/r.personId and ownerName/ownerId; preserve entry-label editor.
- project Dashboard.tsx current line104: d.leadId/name, currently inside A-owned layout; current line134 activity actor name/actorId requires only actual known internal-user IDs.
- project Allocations.tsx current lines138/333/493: a/row/q.data.personId and personName; preserve allocation dialog/open/edit control, only passive name text can use trigger.
- record-context.tsx ActorList/owner: only Person.kind User with known nonnull id, never external/unassigned labels; root shared ownership.
- project TaskPanel.tsx current line466 is within an existing picker/edit control; do not wrap or replace it. Tasks.tsx current line589 likewise needs inspection of the existing assignee control before any passive-name loan.

These candidates have not been changed by D. S43 global all-name acceptance must remain open unless root grants/integrates the remaining exact loans and verifies them. Root may count the narrow Team+Staff implementation as only a partial S43 slice.
