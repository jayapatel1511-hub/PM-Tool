# S29 ALL SOURCE READY — generation21 C# freeze retained

FR/AC-SCR49–50 bounded source, eight dedicated API Facts and synthetic browser script are authored and held. No D edit/process in flight. All earlier capacity source remains held; Person Profile/S43 and all unrelated utilities remain outside this loan. Root owns generation/migration/execution/acceptance.

## Maintained files and behavior

- Backend: new PersonFacts.cs, narrow existing Team.cs and new CapacityPersonFactsTests.cs. See `s29-backend-handoff.md` for exact contract and eight normal-command cases. C# remained frozen after BACKEND READY; no C# repair was made by D during compilation. Root reports independent backend review no production P1/P2, and owns correcting the test's old Deleted expectation to canonical **Removed with prior Snapshot**, strengthening actor/user/tag/expiry checks after its compiler exits. Production audit behavior is unchanged.
- New hub/person-facts.tsx: flag-owned presentation and configured vocabulary filter. Server-derived exact Starts/Ends/expiry state/days are rendered without client warning-date inference. Null expiry is "Expiry not recorded", known expiry is Recorded/ExpiresIn/Expired; inactive tags remain explicit. No skill count, validity/rank/proficiency or employment-status inference. Empty unrecorded facts add no empty layout gap.
- New hub/person-facts-editor.tsx: active self certification editor; narrow Admin type/date/reason correction and certification editor; small dedicated tag vocabulary dialog. Parent/child versions and immutable target paths retained; strict dates/type rules remain server-owned. Create validates UUID/integer version before acknowledging, then protects the known saved ID until authoritative read, with Reload after failure. Parse/shape/transport uncertainty keeps exact body/key locked for retry. Stale edits reload the current record; explicit removal uses canonical delete dialog/child version. Admin legacy version advances only when its baseline equals the captured prior version actually used by the successful facts PATCH; a newer child editor cannot bypass the older legacy form's stale-version guard. Tag editor uses only GET/POST/PATCH, no usage/count prompt. Exact P2 repair/regressions: s29-frontend-review-repair.md.
- Staff.tsx: same chips/notices in desktop table and phone cards; existing work/count/scope/capacity logic unchanged.
- WorkspaceTeam.tsx: chips plus URL certificationTagId filter and removable token, preserving project/Home preset/status scope. Existing authorised cohort is supplied by root's reader; no per-row facts reads.
- projects/Team.tsx: chips in table/cards, same explicit tag filter, existing lead label retained through the enabled LeadName projection even if the filter excludes that lead. Original role/staffing controls and permission logic unchanged.
- hub/people.tsx: candidate type/date notice only from optional server personNotices; no certification fetch/search/ranking or eligibility change. Custom candidates without notices stay unrecorded. Original name DOM/key retained off; enabled picker query appends capacity_screens so off-cache rows do not suppress new notices.
- Preferences.tsx: S29-only self certification section, no new profile or broader Preferences work.
- admin/Admin.tsx: bounded UserDialog facts section, dedicated configured-tag dialog entry, and only the two new warning-setting callbacks additionally invalidate PersonFacts. Existing user editor/roles/capacity behavior retained.
- New capacity-person-facts.cjs: synthetic rendered flows below. It does not establish real permission/schema/math evidence.

## Root seams and exact keys

Public interfaces fixed: PersonFacts.Read/ReadNotices/Facts/Notices/Map; response properties personFacts/personNotices. EditorRead's own Facts remains facts. Exact contract: `s29-reader-contract.md`.

Queries/invalidation: `s29-ui-cache-contract.md` is authoritative. Editor `['person-facts',targetId,'admin'|'self']`; configured choices `['certification-tags']`; Admin vocabulary `['admin','certificationTags']`; existing staff/wteam/p/people prefixes retained. After mutations await UserCertification/PersonFacts/CertificationTag invalidation respectively. Root reports all three mappings and the narrow certificate/tag pre-replay guards merged. D never edited shared cache/middleware/schema/metadata/registration/locales/generated files.

Latest `s29-ui-locales.json` requests **35** new personFacts keys (includes responseUnknown from the exact review repair). Root's eight audit/settings labels are excluded. Server5 messages were already reported merged. Root still generates final migration/OpenAPI/types/registry and checks contracts after reviewed freeze.

## Authored browser cases — runtime UNRUN here

1. people_screens alone does not mount/read editor; capacity_screens alone does. Self records Confined space +20 with stable create key; no Admin type/date form on self.
2. Same Expires in20 chip on Staff table and375px card; scoped Workspace Team filter retains project/Home preset/status; project Team filter retains lead label and unrelated URL state.
3.409 expiry correction requires Reload, then targets the same current child version; clearing expiry displays unknown. Expired EIT record with lost create response retries identical body/key once, not a second record.
4. Explicit EIT removal targets exact ID/version, leaves the other record; ReadOnly self has no add/edit controls.
5. Admin Jill Co-op ending Dec18: exact reason/versioned body, Ends notice; subsequent legacy user Save uses the acknowledged updated version. Tag deactivation preserves inactive person chip; configured tag creation never requests /usage.
6. Picker shows Ends date, has no certifications, and keyboard selection still executes the ordinary permitted command. Preferences375px containment; owner flag off removes editor/facts/new reads while retaining data.

The script arms response waits before actions and does not swallow a failed acceptance wait. Strict page errors/unexpected writes/unknown GETs remain empty assertions. Synthetic response math only tests rendering; API Facts/root foundation cases own actual calendar, role, FK, atomic audit and capacity invariance evidence.

## Static verification actually run

- `node --check web/tests/capacity-person-facts.cjs` — PASS.
- TS createSourceFile.parseDiagnostics on all eight owned changed/new TSX files — PASS syntax only.
- Scoped tracked `git diff --check` — PASS.
- Requested personFacts locale keys versus all eight source files —34 requested, no missing keys.

No D typecheck, compilation, tests, build, browser, generator, migration, helper, goal, commit, push, deployment or activation ran. Root must run final typecheck/build, new8+foundation+adjacent Admin/Staff/Team/picker/version/receipt/capacity tests, the new Chrome script and meaningful real-API role/flag/record evidence, plus final phone/geometry review as appropriate. Local implementation/source review are distinct from runtime verification, deployment, human review and Accepted status. No AC/task/packet checkbox was changed here.
