# G56 S49 phase1 — Admin columns and canonical role examples

Root2026-11-02. D's reviewed g56-admin-scope-role-plan.md (revised20-family inventory) is the basis. This is a bounded **partial source loan**, not S49 completion.35/49 local slices checked. All new behavior uses people_screens; old UI/DTO/key/permissions remain exact off.

## Phase1, no authorization change

Implement user columns and scope/base-capacity facts, plus a read-only role explainer with full coverage gates. NO deactivation inventory/confirmation or Reassign command changes yet: its separate exact contract remains under root review. Do not silently use responsibility rows as open-item totals. Existing deactivation behavior is left unchanged in this phase, so FR82/full S49 stays unchecked.

§8 system-PM template management differs from current Permissions.ManageTemplates. Root asked Jay which policy to keep; no answer yet. Preserve current enforcement bodies and report the existing mismatch as a build gap/known comparison, not a new grant or a complete83 PASS. Catalogue can evaluate current functions while that policy decision is pending; no default answer or source authorization change.

## User screen helper contract

Root will append ignored-null `AdminUserRow.Screen : AdminUserScreen?` and wrap the flag-on user list+facts in one RepeatableRead. D supplies public static `AdminUserScreenReads.Read(HubDb db, IReadOnlyCollection<Guid> userIds, OrgSettings settings, TimeProvider clock)` returning `Task<Dictionary<Guid,AdminUserScreen>>`. Root calls it for actual returned IDs; D cannot edit Admin.cs. Supply exact final record fields promptly; include actual userRowVersion for coherence, asOf, home-discipline descriptor/unknown, baseWeeklyCapacityHours/capacityBasis, nullable peopleScopeCount/scopeBasis. Root may refine only before integration, no shadow types.

Base/configured weekly capacity means override or configured organisation fallback, with true0 preserved. It is NOT schedule-aware effective/available/free capacity: PersonSchedule/date/leave precedence is outside this column. Exact basis is visible. Use existing stored home discipline/template-editor fields; descriptor must match actual ID, including retired label retention, no guessed name.

Only supervisors get scope counts. Structure OFF: explicitly count active direct reports by SupervisorId, not PeopleScope.Load with an Admin caller shortcut. Structure ON: use actual canonical target-person full-role/graph/indirect-setting/mixed-role/closed-unit semantics, batched facts; do not substitute the current Admin's scope or duplicate a grant algorithm. If an existing preloaded pure seam is insufficient, propose exact root-owned PeopleScope extraction with unchanged behavior; do not edit it. Inactive targets show configured scope, not current access. Count active deduplicated IDs independent of displayed list cap/filter. Explicitly document whether canonical scope includes self; match canonical result, no convenient subtraction.

UI new columns/data use actual current successful envelope only; viewer/flag partition new query dimensions, old key preserved off. Missing/refused/mismatched metadata is unknown/withheld, never invented0. Root owns general invalidation. Preserve desktop/tablet Admin and existing phone notice, role/user edit guards and Person navigation. No new private HR facts.

## Role explainer

D owns NEW `src/Hub.Domain/PermissionExplanationCatalog.cs` and `src/Hub.Api/Features/AdminRoleExplanations.cs`. Root mounts public `AdminRoleExplanations.Map(RouteGroupBuilder admin)` on existing Admin group; endpoint `/role-explanations` must require people_screens + current Admin. No request-supplied actor/project/facts, no real-person/project queries or writes. Internal synthetic IDs never go on wire.

Action/scenario metadata delegates invoke actual canonical Permissions/boolean predicates. No copied allowed-role table or manually authored grant sentences; no second evaluator/policy framework or change to authorization/§10 bodies. Output explicit action, labelled public scenario/facts, actual Allowed/NotAllowed and actual nullable Allow reason. Boolean-only predicates have no invented reason. Scenarios are conditional examples, never unconditional live grants; UI does not authorize from them.

Full required coverage: §§8.5.1/8.5.2/8.10/8.11 normalized headers/rows/cells/conditions/notes, all actual public Allow+bool method signatures/overloads, relevant compound and transport/context/feature guards. Manifest maps source bindings, never copied grant results. Changed cells/new rows/new methods/unmapped guards fail closed for complete83 claim. Predicate change changes visible computed outcomes without separately edited grant prose. Keep independent expected-result tests/seeded mapping/cell/signature defects meaningful. Known template policy discrepancy remains explicit pending Jay; no blanket ignore of new differences.

D may implement a small coverage checker/test/JSON binding manifest if necessary; no CI/dependency/framework edits. Runtime C# catalogue must not depend on an unshipped workspace file. Provide exact locale keys/text early; generated DTOs only.

## Exact phase1 loans

Existing `web/src/pages/admin/Admin.tsx`: User list/role section only. Root has already added decisionCategories/issueRootCauses to KINDS/REGISTER_KINDS; preserve those and all reference UI. No deactivation-flow changes this phase.
NEW `src/Hub.Api/Features/AdminUserScreen.cs`, `AdminRoleExplanations.cs`, `src/Hub.Domain/PermissionExplanationCatalog.cs`.
NEW `tests/Hub.Tests/Api/AdminUserScreenTests.cs`, `AdminRoleExplanationTests.cs`, `tests/Hub.Tests/Domain/PermissionExplanationTests.cs`.
NEW `web/src/pages/admin/admin-role-explanations.tsx`, `web/tests/admin-screen.cjs` and fixture.
If needed NEW `tools/check_permission_explainer_coverage.py`, `tools/test_permission_explainer_coverage.py`, `tests/Hub.Tests/Snapshots/permission-explainer-bindings.json`. Own handoff/locale proposal/inventory refinement docs allowed.

No Admin.cs/Extras/PeopleScope/Permissions/Vocabulary/schema/registry/locales/cache/generated/config/CI edits. Root owns shared hooks. No tests/build/generation/browser/EF/services/flags/helpers/dependencies/commit/push/deploy/Accepted. Author focused provider scope/basis/authority/refusal/off/read-only cases, canonical scenario/coverage proofs and strict actual-SPA synthetic columns/explainer/phone/off/error/guard checks. Deactivation7-case remains unimplemented/pending, never claim S49 complete. Return READY/HOLD source hashes/unrun checks; root later issues a separate inventory loan if resolved.
