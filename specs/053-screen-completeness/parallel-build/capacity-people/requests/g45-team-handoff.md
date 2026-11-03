# G45 D S42 — ALL SOURCE READY (held in generation46)

Only FR/AC-SCR72/73/75. All owned C#/UI/helper/script source is now held; no edit/process remains in flight. BACKEND READY45 retained; all other prior D sources stayed held. No implementation/acceptance checkbox changed.

Root's generation46 follow-up approved the shared integration requests and asked for saved-view API evidence. TeamCompletenessTests.cs now contains eight Facts: the original six plus owner-flag/list-type/personal/project/share/hidden-scope guards, and invalid group/reference cleanup/sentinel preservation with unchanged view definition after refusal. This exact test addition is complete and held again. No production/UI/script/shared source changed in the follow-up; root owns all approved shared implementation and runtime evidence.

Exact DTO signatures supplied (all public under Hub.Api.Features): `TeamScreenReads.Reference(Guid Id, string? Name)`; `TeamScreenReads.Organisation(Reference? HomeDiscipline, Reference? Office, Reference? Supervisor, Reference? Unit = null)` with Unit null-omitted; `Task<Dictionary<Guid, Organisation>> TeamScreenReads.ReadOrganisation(HubDb db, Guid[] authorisedIds, OrgSettings settings)`. ProjectTeamMember appends null-omitted optional `TeamScreenReads.Organisation? OrganisationFacts = null` after its existing PersonFacts optional field. Root's Workspace TeamPersonRow should append the same optional field; every other constructor field remains as before.

## Source supplied

- Maintained src/Hub.Api/Features/Team.cs: optional null-omitted organisationFacts after existing authorised member/certification cohort, with people_screens read snapshot; membership/roles/leads/staffing commands untouched.
- New src/Hub.Api/Features/TeamScreenReads.cs: exact supplied IDs only; home discipline/office/supervisor display references; current unit only with org_structure; off/empty performs no query. Reference identity and null unknowns preserved.
- Maintained web/src/pages/WorkspaceTeam.tsx and projects/Team.tsx: optional organisation columns, exact-ID URL filters/grouping, stable same-name reference separation and distinct Not set/Unavailable groups; saved view captures cols/group/filter and workspace scope. Existing person facts/cert filters/lead/member controls preserved. Phone members follow selected organisation columns. Off ignores new Team params and retains old storage/query keys/layout composition.
- New components/hub/team-organisation.ts and team-organisation-controls.tsx: narrow Team-only projection/controls, grouping first by actual IDs; duplicate labels include ID to remain distinct; no client authority/capacity algorithm. URL filter values are GUID/not-set/unavailable, group has none default; unit params dormant when org flag off.
- New components/hub/team-open-roles.tsx: separate authorised Open roles read on existing allocations/id/open-roles key; only Open records, with label/discipline/dates/exact canonical hours. Fill uses existing project proposeAllocation/useOffer and allocations?fill=id handoff; refusal is a disabled button with canonical reason, never a live disabled-looking link. Roles never enter members/capacity reads/counts.
- Add a person alone receives capacityWeek=weekOf(today()) while people_screens; existing PeoplePicker/usePeopleCapacity owns one current-week candidate batch, permissions and scope. No directory supplement or wider grant was added.
- New tests/Hub.Tests/Api/TeamCompletenessTests.cs: eight isolated-factory normal-command cases for project/workspace exact off parity/cohort references, home vs project primary, null/dedup, current placement/org pairs, cert/lead/hidden preservation, two separate roles, scoped free/away/outside refusal and Team-specific saved-view guards/validation/cleanup. No direct fixture qualification/staffing DB writes.
- New web/tests/team-completeness.cjs: rendered synthetic acceptance authored for Halifax home groups/Not set/same-name IDs, saved scope/filter/column URLs, current unit pairs, two role records and canonical Fill/refusal, candidate-only week/scoped/partial/absent figures, unchanged Add GUID, phone/keyboard, off/independent capacity facts. Provisional and URL-column-checkbox assertions intentionally depend on root-owned shared repairs below.

## Root integration still required

Exact contracts are requests/g45-team-contract.md. Root owns Workspace.cs TeamPersonRow optional organisationFacts decoration/snapshot after its final cohort; DTO generation; new workspace-team/project-team saved-view whitelists and return scope; Team invalidation for User/home/office/supervisor/unit/reference/flags. Include q, group, cols, all four ID filter params (including not-set/unavailable), cert and existing workspace/Home params. New keys keep old prefixes and add people_screens/org flag suffix only enabled.

Merge the 12 strings in requests/g45-team-locales.json before final typecheck. No settings/schema/migration request.

Two shared presentation issues were surfaced without changing shared source:

1. requests/g45-team-picker-provisional-observation.md: existing CapacityFigure lacks a provisional guard and can show ordinary free hours from provisional supply. Root must review/integrate the smallest uncertainty presentation while keeping selection and exact server values/permissions unchanged. S42 cannot claim the provisional case from source readiness.
2. useTable's menu checkbox ignores URL cols although the visible columns respect them. Root should derive checked from the actual current visibility/urlCols, preserving its existing toggle/storage semantics. The dedicated script asserts Office is unchecked for cols=homeDiscipline and one click shows Office. D did not edit shared table.tsx.

## Evidence and remaining checks

PASS static only: five TS/TSX files parse with zero diagnostics; node --check team-completeness.cjs; scoped tracked git diff --check; new-file whitespace; six-Fact inventory; 12-string JSON parse. The generation45 guard intentionally refused a phone-column edit when CONTROL advanced to46; after reading the retained D ownership, that edit was applied under46. No unrelated source was touched.

UNRUN: .NET compilation/tests, contract generation, types/fixturetypes/lint/build, browser/runtime, actual API joins, native device and human acceptance. Dedicated scripts are authored and unproven. Root must independently review, complete shared integration, freeze and execute before checking S42.

Suggested focused root API class: TeamCompletenessTests plus existing Team/Projects, PersonFacts, OpenRole and planner scope regressions as warranted. Browser from final built preview: TUESDAY_UI_BASE_URL=http://127.0.0.1:8853 CHROME_EXECUTABLE_PATH='/Applications/Google Chrome.app/Contents/MacOS/Google Chrome' node web/tests/team-completeness.cjs. Root owns real joined evidence, especially exact underlying scope, Fill permission, leave/free basis and current-unit names.

No S43 person panel/note, Staff, Reports, schema/registry/cache/locale/generated edits; no helper/goal/process/build/test/browser execution, commit/push/deployment/activation or decision closure in this lane.
