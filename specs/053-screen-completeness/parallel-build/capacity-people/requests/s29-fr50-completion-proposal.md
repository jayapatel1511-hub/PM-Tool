# S29 FR50 completion — exact next-generation proposal, notes only

Status: **PROPOSAL READY / ALL SOURCE HELD under generation22**. No implementation, C#/app/script/test edits, execution, helper, generator or migration. Root accepted the coverage inventory and requested this completion plan. The PersonFacts Chrome PASS and closed editor/callback/version0 source review are coordinator-reported evidence; they do not close the remaining FR50 paths. Full S29 remains unchecked.

## Accepted completion scope

Interpret "person pickers" as actual person-selection controls, including person-valued owner/role/filter dropdowns. Cover the one custom PeoplePicker and all actual native chooser families in `s29-fr50-picker-coverage-inventory.md`. Include the Admin HR-identity chooser, using recorded PersonFacts dates only and retaining its existing Admin-only audience. Plain person references/read-only actor rosters and non-person assumption/version/project/discipline selectors are outside this specific requirement.

Add only display labels. Do not change candidate IDs, order/rank, active/role/team/project scope, search predicates, self-review exclusions, current selection, disabled/allowed state, request GUIDs, field versions or canonical command rules. No new rights, qualification/certification data, counts, profiles, capacity/workload/HR inference, arbitrary fill-in GET or general people batch endpoint. Existing containing-feature guards remain in force; the notice addition itself belongs only to capacity_screens, independently of people_screens/visual_system.

## One root-owned authorised-ID bridge pattern

Reuse **PersonFacts.ReadNotices(db, authorisedIds, settings, clock)** unchanged as the sole fact derivation. No alternate date calculation or new person-lookup policy is needed.

For each source below, root keeps its existing access demand, complete candidate predicates, order and any cap; materialises that exact result first; passes only those returned IDs to ReadNotices once; then decorates rows without sorting, filtering, adding or dropping candidates. Empty stays empty. ReadNotices contains only nullable PersonType/StartDate/EndDate and server-derived Starts/Ends; no certifications. Use capacity-only RepeatableRead for same-read cohort/notices consistency, reusing an existing transaction rather than nesting. Capture settings/time consistently with the existing route.

Append this optional property **after existing DTO fields**, so legacy off keys/order/values remain identical:

```csharp
[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
PersonFacts.Notices? PersonNotices = null
```

Off returns the original DTOs with the property omitted and performs no additional person-fact lookup. On, a missing read result remains null/unknown, never an inferred Employee/start/end. Unknown date facts have no warning; future start/present-window end use the existing reader rules. A past end must not become an employment status or remove eligibility.

### Six root-owned DTO/read seams

| Exact file / DTO | Existing authorised source to preserve | Narrow root change |
|---|---|---|
| Features/Changes.cs — CoordinationPerson / Options | access.Project plus Coordination.People(db,p), existing display-name order; original options permissions/work/source references | Append PersonNotices, inject current settings/clock, decorate only final People IDs. This covers the custom Issue verifier array automatically because its .filter preserves object fields, as well as native coordination option sources. |
| Features/TaskPickup.cs — Candidate / Detail | existing canAssign, active-user, current-assignee exclusion and SameTeam eligible array; existing offered-task/project access | Decorate eligible IDs only after the current gates. canAssign=false still returns an empty candidate list. No new picker ranking or changed assignment permission. |
| Features/Handoffs.cs — HandoffPerson / Options | existing project access, Coordination.People, original order and handoff rules | Append/decorate the final People list. Keep sender/receiver/coordinator command checks unchanged. |
| Features/OrgStructure.cs — OrganisationPersonRow / GET organisation | existing org feature gate, active-user filter and PeopleScope-selected returned people | Append/decorate only the final People list, not all unit placements/managers used in separate calculations. Do not change headcounts, units, cycles, scope or leadership rules. |
| Features/DisciplineCoordination.cs — WorkspaceOwnerChoice / workspace read | existing selected visible projects, actual current-member/PM ownerIds and original display-name order | Append/decorate the final Owners list. Never widen to everyone with recorded dates or silently turn empty project scope into all owners. |
| Features/Availability.cs — HrIdentity / GET planning/hr-identities | existing Permissions.Administer demand followed by current active users in display-name order | Append/decorate the already-Admin-authorised returned IDs. No wider HR source/key exposure, no HR-derived person type/start/end, no new HR permission and no identity/leave/mapping writes. Ordinary non-Admin/ReadOnly users keep the existing refusal. |

Already-covered DTOs require no new server bridge: AdminUserRow.personFacts.notices for supervisor controls, ProjectTeamMember.personFacts.notices for the project discipline-coordination owner filter, and PickerUser.personNotices for the42 standard shared PeoplePicker sites. Native consumers must read only the notices subsection, never add certification labels to a picker.

Root exclusively owns these C# seams, shared metadata/contracts/cache/locales/generation and the execution lane. This proposal requests no schema/migration, permission, receipt, settings/default, registry resource or formal decision-status change.

## Single plain native-option formatter

Proposed new small shared module `web/src/components/hub/person-option-label.ts`, assigned to D only if root opens the next window. It takes the existing displayName, optional notices and an **explicit enabled boolean**. Returns the original name byte-for-byte when disabled/missing; otherwise joins name and the same optional type/Starts/Ends words used by PersonDateNotice. Reuse current personFacts.starts/ends translations and fmtDate. It must consume server Starts/Ends booleans, not compute day windows, capacity or employment state.

A tiny shared notice-word function may serve both native labels and the existing React PersonDateNotice to avoid copy drift. No component/framework replacement. Native Choice remains value/label and option value remains the original person GUID. Put names first, retain order/search/exclusions, and do not sort on the appended notices.

The explicit enabled input is essential: stale cached on-data must not leak date labels after the flag turns off. Pure helpers such as peopleChoices cannot call hooks. Thread the flag from the owning component through any pure field-builder functions, preserving stable hook ordering. Do not default to enabled merely because a notice object is present. The shared PeoplePicker already implements this flag check and needs no wider lookup.

## Exact frontend ownership/edits to request

All paths below are future named loans, not currently reopened. One writer per file. Root coordinates release of Work/Projects-owned files before D changes labels; no command, grouping, report, S10/S11 or previous layout work is loaned by implication.

| File | Exact bounded change |
|---|---|
| components/hub/person-option-label.ts (new); person-facts.tsx (notice words only) | Pure enabled native label formatter / optional reuse in current notice component. No reader, qualification or policy logic. |
| pages/projects/CoordinationForms.tsx | peopleChoices accepts the enabled flag and formats final candidate labels; keep its value/label contract. |
| pages/projects/Changes.tsx, Reviews.tsx, Submissions.tsx, CoordinationRegister.tsx | Pass the flag through existing peopleChoices calls and pure command-field builders. Preserve reviewer guidance/excluded IDs and owner/coordinator/resolver/verifier filters and all command payloads. |
| pages/projects/DesignBasis.tsx | Replace only five direct people label maps at current187/190/299/301/360 with the common formatter after existing filters. |
| pages/projects/Allocations.tsx | Replace only person filter/proposal choice labels at155/263. Do not change proposal capacity previews, links, shifts, confirmations or candidate eligibility. |
| pages/projects/ReadinessForms.tsx | Replace only removal-owner and exception-verifier label maps at222/283 after their current owner/actor exclusions. Assumption/version candidates remain untouched. |
| pages/projects/Registers.tsx | Expected **no implementation edit**: custom verifier's existing filtered CoordinationOptions objects flow into PeoplePicker once the DTO bridge is supplied. Only a separately approved type seam if generation exposes one; no verification commands or exclusions change. |
| pages/projects/Handoffs.tsx | PersonSelect labels only, consuming HandoffPerson.personNotices and explicit flag. |
| pages/projects/TaskPanel.tsx | Native pickup assignment option labels only. Preserve allowed.assign/candidates/value/write behavior and unrelated comments/activity slots. |
| pages/Pickup.tsx | **Type interface only if necessary**: add optional notices to the local PickupOffer.candidates declaration (or use the generated Candidate type). Its page does not render the candidate list; no page redesign. |
| pages/admin/Admin.tsx | Add/create-user and edit-user supervisor option labels from user.personFacts?.notices. Preserve active/self exclusions, dirty fields and the repaired captured-prior-version baseline guard. Do not update a dirty legacy form's version just because notices refresh. |
| pages/admin/Structure.tsx | Unit leader labels from the newly decorated organisation people only; no organisation commands, placement/count/tree changes. |
| pages/projects/DisciplineCoordinationView.tsx | Owner filter labels from member.personFacts?.notices; keep returned Team cohort and owner-ID URL exactly. |
| pages/WorkspaceCoordination.tsx | Owner filter labels from decorated WorkspaceOwnerChoice; keep current workspace/project/discipline/date scope and URL/payload IDs. |
| pages/Vacation.tsx | Admin HR identity selector labels from decorated HrIdentity only. Preserve chosen identity/version/source/key inputs and mapping/leave guards. A notice refresh must not grant a new version to dirty old HR mapping fields. |

Existing standard PeoplePicker call sites need no per-page rewrites. No extra API call may be made to supplement the custom verifier, pickup, handoff, organisation or HR arrays.

## Root cache and generated seams

Existing keys observed: `coord-options`, `issue-verifier-eligible`, `pickup-offers` (TaskPanel detail and pickup page), `handoff-options`, `organisation`, `workspace-coordination`, `hr-identities`, plus existing `admin`, project `p`, and standard `people`.

Root must ensure PersonFacts/User date/type changes and availability_window_days/timezone/owner-flag changes refresh the relevant mounted notice sources, while preserving each query's existing scope. Use the current key registry; no lane edits keys.ts. Notice-only sources should not require certification-tag/record data. Existing certificate/Team/Admin invalidations may remain for their original consumers; do not invent a new aggregate or per-person lookup. Admin warning-setting callback already conditionally invalidates PersonFacts. Any enabled-only query-key suffix must retain the exact old key off and keep prefix invalidation working.

Root regenerates OpenAPI/typed DTOs and affected contracts after backend freeze, then final typecheck/lint/build/Chrome after frontend freeze. New fields are read-only/null-omitted, and display text is excluded from save/export/filter identity. Reuse existing locale keys unless an actual new label is necessary; send JSON for root merge. No migration is requested.

## Dedicated meaningful checks to author in the next window

New API file: `tests/Hub.Tests/Api/CapacityPersonPickerNoticeTests.cs`. New browser file: `web/tests/capacity-person-picker-notices.cjs`. Preserve the existing PersonFacts/Chrome suites; do not replace/weaken them. Use normal facts/Admin/team/role commands and clearly labelled synthetic UI responses.

1. **Six-source cohort invariants:** record each route's candidate IDs/order/allowance/version/source fields before/after dates; only the optional notice changes. Include empty scope, hidden/restricted project, removed/inactive membership, current-owner exclusions, canAssign=false, same-team/indirect/org-structure boundaries and no all-people fallback. Durable state equality proves pure reads.
2. **Owner flag matrix:** capacity on with people/visual off still decorates an available containing feature; capacity off omits exact new DTO keys and retains legacy labels/order even when the client cache contains old notice objects. Existing org/pickup/other containing-feature guards stay independent. No field appears merely because people_screens is on.
3. **Canonical derivation reuse:** future start, endtoday/60/61/past/null, changed window0 and one Halifax/UTC boundary through the new bridges. Reuse existing S29 boundary evidence; add cross-source assertions rather than duplicate the date algorithm in tests. Unknown never becomes a default Employee or an Ended/Available status.
4. **Custom verifier:** notices render on the already filtered options array with zero `/users` fill-in requests. Self-review off excludes owner/raiser as today; on preserves existing relaxation. Appointment sends the same allowed verifier ID/reason, with no new rights from type/dates.
5. **Native option families:** exercise every actual direct-map family and each common peopleChoices consumer (Changes/Reviews/Submissions/register filters), including allocations, basis and readiness. Assert accessible label has the correct notice while option value, choice order, URLs, saved/filter identity and real command GUID remain unchanged. Missing notices keep original labels.
6. **Specialised sources:** pickup uses only its actual eligible same-team candidates, handoff keeps current permitted people, organisation leader uses only scoped active people, workspace owner keeps selected-project owners, project owner reuses Team notices. Dates never enable/disable/hide these people.
7. **Admin/HR cases:** supervisor choices render available notices without changing the repaired legacy version behavior. HR identity chooser remains Admin-only, shows recorded dates without HR inference, preserves source/key/version and writes nothing by rendering/selecting a label. Non-Admin and non-Admin ReadOnly refusal remains exact; no HR metadata enters ordinary pickers. Preserve dirty mapping/legacy versions across date refreshes.
8. **Display-only math:** capacity/activation/roles/membership/candidate IDs/ranking and existing allocation/planner readings are unchanged by future-start/past-end facts. Existing actual S29 proof is retained; new bridge reads add no audit, date versions or outbox writes and never fetch certifications.
9. **Actual UI and phone:** one real-API date fixture across the custom verifier and native chooser families, keyboard selection, unchanged request IDs, cached flag-off labels, null facts and375px containment. Native OS popup appearance is a separate device limitation; Chrome DOM/keyboard evidence must not be called every-device acceptance. No added band/banner/density workaround is needed for suffix labels.

Root should re-run a static actual-person-selection inventory after integration to catch missed direct maps/typed aliases, then independent source review, focused new/adjacent API/contracts/cache checks, final generated typecheck/build and the dedicated browser cases. Broaden only for actual failures/risk. Preserve snapshots by comparing unchanged-off behavior; never update expected cohorts merely to accept a widened list.

## Dispatch and acceptance gate

Proposed sequencing: root opens only these six backend bridges and typed contracts; D receives only named formatter/presentation/type/test loans, publishes the exact interface/key plan, authors tests and reports backend READY; all C# freezes before root compilation/generation. Then D completes labels/browser script and reports ALL SOURCE READY, after which root freezes all app files and runs the single UI lane. Existing source remains held until that explicit dispatch.

FR50/full S29 may be checked only after the remaining custom/native controls have implemented and verified notice mapping, current scope/permission/order/eligibility invariants, honest unknowns and unchanged off behavior. A directory case or source inventory alone is insufficient. Only Jay owns Accepted status; no checkbox, decision, deployment, flag activation or operational data was changed by this proposal.
