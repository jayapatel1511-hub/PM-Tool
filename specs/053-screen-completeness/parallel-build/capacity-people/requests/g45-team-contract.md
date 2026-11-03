# G45 D S42 stable Team contract / root integration requests

Scope FR/AC72/73/75 only. Project/workspace grouping/filtering is a client projection over each existing complete authorised Team cohort, after its existing certification/Home scope filters. No candidate/person population expansion or new staffing/capacity arithmetic.

## DTO and workspace bridge (root owns Workspace.cs)

D will author a narrow new TeamScreenReads.cs helper with public `TeamScreenReads.Reference(Guid Id, string? Name)` and `TeamScreenReads.Organisation(Reference? HomeDiscipline, Reference? Office, Reference? Supervisor, Reference? Unit)` where null Unit is null-omitted. `ReadOrganisation(HubDb db, Guid[] authorisedIds, OrgSettings settings)` returns `Dictionary<Guid, Organisation>`; off/empty returns empty without reads. Only those supplied people are queried; reference display names are resolved for their actual home-discipline, office, supervisor and current unit. Unknown IDs with absent names remain identified Unavailable, never merged with Not set. Unit is populated only with org_structure.

D appends null-omitted optional `OrganisationFacts` to ProjectTeamMember in Team.cs and reads it after existing canonical certification/member filters. Snapshot enabled when people_screens OR capacity_screens; off byte shape preserved. No permission or membership command edits.

ROOT REQUEST: append the same null-omitted optional `TeamScreenReads.Organisation? OrganisationFacts = null` to WorkspaceEndpoints.TeamPersonRow; after its final existing `people` cohort, call ReadOrganisation once and decorate exact IDs. Add people_screens to its existing snapshot predicate. Preserve ActiveMembers/dedup/Home scopes/cert filters, counts/order/references. No extra API route or schema migration.

## URL/view/cache contract (root owns metadata/keys/generated)

Two ViewMenu list types requested: `workspace-team` (/team, scope-aware) and `project-team` (/projects/:number/team, projectId). Team display params: `group` = homeDiscipline|office|supervisor|unit (none default), `homeDisciplineId`, `officeId`, `supervisorId`, `unitId` (GUID or `not-set` or `unavailable`), existing `sort`, `cols`, q and certificationTagId. Workspace preserves its Home preset/status/scope params. Off ignores all new Team params; unit params ignored with org_structure off. No URL cleanup that destroys prior scope. Root registers safe whitelist and saved-view scope; these params are presentation filters, never API staffing predicates. Missing whole organisationFacts is Unavailable, distinct from an authoritative null reference (Not set).

Current query prefixes: project `['p',projectId,'team',...]`, workspace `['wteam',scope.api,...]`. D adds a people_screens suffix only when enabled, to avoid reusing a fresh old missing-facts response; old keys unchanged off. Root invalidation should refresh Team on person home-discipline/office/supervisor/unit reference edits, reference rename, flags and org placement while preserving existing PersonFacts/cert invalidations. Root regenerates DTOs and list keys after C# freeze.

Shared table presentation seam found during source review: useTable's visible columns respect URL cols, but its menu checkbox reads only local hidden state. A saved/shared Team cols=homeDiscipline view would hide Office while incorrectly checking Office in the menu; its first click then has the wrong effect. Root-owned shared integration request: derive checkbox checked from current visible columns (or the existing urlCols branch), preserving the existing toggle/local-storage logic. Dedicated browser acceptance asserts the URL Office-hidden checkbox is unchecked, and one click shows Office. D has not edited table.tsx.

## UI contracts

D helper groups by reference ID, with a separate Not set sentinel and a visible ID suffix only for duplicate names. Null is not-set; named/unavailable IDs are distinct. Grouping sorts groups without changing stable person references; filters exact IDs. Columns use the current useTable/cols contract; no new shared table dependency. Shared presentation helper/control component is Team-local. Existing member editing/lead/certification controls remain.

Project Unfilled roles strip reuses GET projects/{id}/open-roles with current `['allocations',id,'open-roles']` key, displays only Open rows with label/discipline/dates/hours. Fill link uses canonical project.permissions.proposeAllocation through useOffer and the existing `/projects/:number/allocations?fill=:roleId` handoff (confirmed in Allocations.tsx). A refused action is a disabled button with its canonical reason, never a clickable disabled-looking link. Roles do not join members, counts or any capacity read. Read errors/empty are explicit.

Only AddMember's PeoplePicker receives `capacityWeek={people_screens ? weekOf(today()) : undefined}`. Existing shared PeoplePicker/usePeopleCapacity/CapacityFigure determines permissions, candidate-only planner batches, partial/unknown handling and absent outside-scope figures. No alternate directory/capacity fetch or wider grant. Root owns any required shared picker change if existing canonical semantics need adjustment; D does not edit it.

No S43 panel/note, Staff/Reports/shared schema/registry/keys/PeoplePicker edit, execution/helpers/goal/commit/push/deploy/activation. New tests: TeamCompletenessTests.cs and team-completeness.cjs. The 12 exact locale strings are now supplied in g45-team-locales.json for root merge. Shared picker semantics are reused unchanged; actual keyboard/device/partial/provisional behavior remains for root runtime verification, not a lane claim.
