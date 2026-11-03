# S29 next-batch proposal and precise schema request

Status: READ-ONLY PROPOSAL, prepared under ALL SOURCE FROZEN generation10. No source/test edits, builds, tests, generation, migration, helpers or goals. Root must assign the next editing window and approve the shared schema contract before implementation. This document does not close 053-D11/D18/D21 or mark S29 implemented/accepted.

## Contract and owning flag

Scope is **FR/AC-SCR-49 and FR/AC-SCR-50 only**, packet053 S29. §57.1 places these requirements in PartE, so their owner is **`capacity_screens`**, including the small Team/Admin/picker integrations. `people_screens` does not become a second enabling requirement. With capacity_screens on and people_screens/visual_system off, S29 still works. The later S43 person panel may consume these facts only when capacity_screens is also on; building that panel is outside this batch.

Only certification tags with optional expiry, the three fixed person types, and optional start/end dates are introduced. No responsibility note (FR76), free-form skills profile, proficiency/score, issuer, licence number, attachment, employment percentage/FTE, salary, qualification verification, approval workflow, or full Person Profile.

## Root-owned additive schema request

| Maintained model/table | Exact business additions | Null/default and constraints |
|---|---|---|
| `AppUser` / `hub.app_user` | `string? PersonType` → `person_type`; `DateOnly? PersonStartDate` → `person_start_date`; `DateOnly? PersonEndDate` → `person_end_date` | All nullable, no backfill/default. Type is null or exactly Employee / Co-op / Contractor (053-D11). Either date may be independently null; if both exist, start≤end. Dates do not require a type. Preserve unknown rather than assume Employee. |
| New `CertificationTag : RefData` / `hub.certification_tag` | Standard reference `Name`, `IsActive`, `SortOrder` only | Reuse RefData identity/audit/rowVersion infrastructure; no extra business facts. Name required/trimmed/max120 as existing Admin reference names, case-insensitive unique. Admin-managed organisation vocabulary, as required by §57's vocabulary boundary. No seeded person qualifications. |
| New `UserCertification : Audited, IAuditable` / `hub.user_certification` | `Guid UserId` FK AppUser; `Guid CertificationTagId` FK CertificationTag; `DateOnly? ExpiresOn` | A person may have zero records; expires_on nullable. Unique(user_id,certification_tag_id), restrictive FKs, normal audit stamps/actor/rowVersion. No status or verified flag; expiry state is derived. Add/change/remove retains before/after evidence in same-transaction activity. |

New child rows necessarily have non-null identity/FKs; **all existing-person business columns are nullable**. Do not add placeholder certification rows or synthesise expiry/type/dates from job title, HR IDs, schedules, assignments or old prototypes. Tag deactivation preserves existing assignments/history; new assignment requires an active configured tag, while an unchanged inactive tag can retain its recorded expiry and be removed. Do not expose people-per-tag counts, per-person certification counts or ranking via a new usage/summary surface.

Root exclusively owns `Data/Entities.cs` (or a narrow new person-facts entity file), `Data/HubDb.cs`, the one sequential additive migration/designer/snapshot, `Records/AppUser.cs`, new record audit metadata as needed, generated registry/API, shared settings, Admin, permission helpers and locale/keys integration. Migration adds only the above and normal reference/audit plumbing; existing rows remain valid and empty/unknown. No need to edit capacity, allocation or planning schema/rules.

## Settings request

Add `certification_warn_days` (default30) and `availability_window_days` (default60), owning flag capacity_screens. Validate as nonnegative integer warning windows using the existing setting contract; do not introduce fractional/negative days. Derive via DateOnly day differences, avoiding date addition overflow. Settings are offered/read/writable through new behaviour only while the owner flag is on; flags stay defaultfalse. Do not change default_weekly_capacity_hours or org-time-zone rules.

Centralise deterministic display derivation in a narrow person-facts reader using `clock.Today(settings)`:

- Certification expires_on null: expiry not recorded; no Valid/lifetime claim. Earlier than today: Expired. Today through warn_days inclusive: Expires in n days (including0). Later: ordinary tag plus its recorded date, without a warning or asserted qualification validity.
- Show Starts <date> only for a recorded start later than today. Show Ends <date> for a recorded end from today through availability_window_days inclusive. Past end retains the recorded fact but does not invent an Ended employment/workflow status. These are proposed exact display-boundary defaults for the reader, not a decision-status change.
- Null type/date remains unrecorded. Zero certification records means no certifications recorded, not proof the person has no skills. An inaccessible/not-returned person yields no fact object and no inferred empty set. Derived warning state/days are response-only and never stored.

## Commands, permissions, concurrency and activity

Use small new routes/sections rather than silently widen the legacy Admin user patch. Every new route checks capacity_screens server-side; off→404.

- Self certification list/editor: GET `/me/certifications`; POST `/me/certifications`; PATCH/DELETE `/me/certifications/{id}`. Target comes only from authenticated me. Active self may edit unless ReadOnly. ReadOnly self can read their own recorded facts but has no edit affordance and write is403. The Admin exception is enforced by the shared permission contract, not a Supervisor/PM proxy rule.
- Admin corrections: the same narrow certification editor against `/admin/users/{userId}/certifications` and its child route, under existing Admin authentication. Admin may correct another/inactive person's facts; require the existing `Check.Reason` contract for administrative corrections and record the reason with the change. Self routes retain the self-edit rule. Supervisor, unit leader, PM or lead status alone grants no certification/type editing authority.
- Type/date editor: PATCH `/admin/users/{userId}/person-facts`, whitelist only PersonType / PersonStartDate / PersonEndDate plus correction reason. Existing AppUser If-Match/rowVersion contract; no edits to user activation, roles, capacity or HR source fields through this route.
- Certification create validates active vocabulary ID and uniqueness; no client-supplied UserId reassignment. Child edit/delete uses the existing If-Match/rowVersion contract. Duplicate create and stale edit must not overwrite existing facts; failed/denied commands produce no fact/audit mutation. All successful writes and activity are one transaction, with immutable actor and prior expiry evidence. Root decides the exact route wiring/shared helper implementation; these are fixed facts, not a new role system.

No command enables/disables a user, changes person selection, creates project membership, alters notifications to others, publishes data, or contacts an issuer/provider. A future start or past end never removes a person or changes their capacity/eligibility.

## Permission-scoped read integrations and file ownership

Current-source seams matter: there is no Staff.cs, standalone admin/Users.tsx or generic people feature to replace.

| Integration | Exact maintained location | Next-batch writer/request |
|---|---|---|
| Own reusable read/commands | New `Features/PersonFacts.cs` (or two narrow helper/endpoint files) | Capacity after root approves schema/permission contract and supplies module registration. Reader receives already-authorised IDs; no arbitrary unscoped batch facts endpoint. |
| My Staff DTO/read | `Features/MyWork.cs`: MyStaffPerson and Staff | **Work-owned file**: root applies an exact additive reader/DTO patch or explicitly loans only these sections. Preserve all staffing, work, count and partial-scope calculations. Join facts after StaffScope and row filters. |
| Workspace Team DTO/read | `Features/Workspace.cs`: TeamPersonRow / Team | **Home/root-owned file**: root applies or explicitly loans exact sections. Preserve Home preset/status bridge, selected project scope and grouping. Filter by `certificationTagId` within that scope before forming returned people. |
| Project Team DTO/read | `Features/Team.cs`: ProjectTeamMember / GET project/team | Capacity when reopened; add scoped facts and the same tag selector for current members. Existing access.Project/member checks and role/staffing commands remain. |
| Directory picker DTO/read | `Features/Me.cs`: PickerUser / GET users | Root-owned exact patch. Attach only the type/date display notices required by FR50 to the already-authorised candidate set. Preserve active/ids/project access rules, ordering/ranking/limit and membership logic. No new skill search or wider candidates. |
| Admin rows/editor/vocabulary/settings | `Features/Admin.cs`; `web/src/pages/admin/Admin.tsx` Users/UserDialog and reference/settings UI | Root exclusively. Small person-facts section and shared certification editor; no replacement of legacy Users/roles/capacity UI. |
| Staff rendering | `web/src/pages/Staff.tsx` | Capacity: chips in both card and table representations using one narrow new `components/hub/person-facts.tsx`. Existing counts and signals stay. |
| Team rendering/filter | `web/src/pages/WorkspaceTeam.tsx`; `web/src/pages/projects/Team.tsx` | Capacity **only after root releases its current WorkspaceTeam loan**. A URL tag-ID filter and removable token preserve project scope/Home filters. No per-tag counts or tag-based ordering. |
| Picker presentation | `web/src/components/hub/people.tsx` | Capacity: quiet exact date notices on candidates; same selection/disabled/capacity semantics. Custom candidate arrays missing notices stay unknown; do not fetch arbitrary people to fill them. Root/lane should inspect typed candidate callers for required FR50 coverage before READY. |
| Self-edit entry point | `web/src/pages/Preferences.tsx`, narrow reusable certification editor | Capacity, S29-only bounded section accessible to self; no new profile route or broader Preferences/S49 work. |
| Shared caches, locale, module/auth registration, generated fixtures | Existing keys/text/registry/API infrastructure | Root request only. Invalidate relevant people/admin/staff/team reads after fact change; preserve query-key scope. No broad package/framework changes. |

Existing responses append a nullable, null-omitted fact/notice object only when capacity_screens is on. Off keeps legacy property names/order/values and ignores the additive tag filter, matching earlier dark selectors. Both flags matrixes matter: people_screens alone must not expose S29 facts, and capacity_screens alone must supply them. New fact metadata must not leak into generic lists/exports/search while owner flag is off. The later person panel is deferred.

## Acceptance checks to prepare once authorised

Narrow new files: `tests/Hub.Tests/Api/CapacityPersonFactsTests.cs` and `web/tests/capacity-person-facts.cjs`; root adds schema/permission/registry/flag unit checks in its own files. No tests are created or executed during this freeze.

1. **AC49 real data path:** Admin configures Confined space tag; eligible Omar self records expiry today+20. Authorised My Staff card/table and Team row show the same tag + Expires in20days; tag filter returns the same-scoped rows. No certification count, people comparison/rank or new summary appears.
2. **Expiry boundaries:** null expiry, today−1, today, warn_day boundary, one day beyond, changed org time zone and changed warning setting derive correctly from server Today. Unknown never gets a green Valid result. Deactivated vocabulary preserves recorded chips and rejects a new assignment.
3. **AC50 exact fixture:** Admin sets Jill Co-op ending2026-12-18. At2026-10-31 the marker reads Ends2026-12-18 (48days away). Test 61/60/0/−1 day boundaries and future start. Staff, Team and standard picker notices agree. No implicit Employee default on old rows.
4. **Display-only proof:** compare raw canonical capacity/planning/allocation reads, schedule/availability/version rows, membership, activation, picker IDs/order and normal selection before/after future-start and past-end facts. Hours and eligibility are unchanged, including reduced-capacity/zero-capacity fixtures. No HR/leave/private planning content is attached.
5. **Permission/concurrency:** active self succeeds on own certifications; ReadOnly, inactive self, unrelated user, Supervisor/PM/unit leader acting for another fail. Admin correction needs reason and logs actor/before/after in the same transaction. Stale certification/type row versions and invalid/duplicate/tag/date inputs leave values and activity unchanged. Expired certification is valid to record; it is not silently dropped.
6. **Scope/non-leakage:** Staff direct/indirect/unit/all boundaries, visible workspace project scope + Home preset, project members, removed/inactive people and current directory candidate rules remain intact. Hidden person/fact never enters rows, filter matches, option-derived counts or raw payloads. Tag vocabulary choices are configured names, not inferred from hidden people's assignments.
7. **Flag matrix/off bytes:** new routes/settings/editors/facts/filter effects absent off, legacy read shape retained, no migration backfill; capacity_screens on + people_screens/visual_system off works. People_screens on + capacity_screens off omits S29 objects. Saved data survives disabling the flag without a visible leak.
8. **Rendered workflow:** self editor and Admin correction, Staff cards/table, workspace/project Team filter + return URL, picker keyboard selection, stale-edit error, unknown/expired text,375px reflow and accessible chip/filter names. Synthetic UI fixtures and real-API Chrome checks reported separately.

## Sequencing and handoff

Root reviews this exact contract first, then writes sequential schema/settings/permission/Admin/metadata glue while granting the narrow source loans. Capacity implements only the approved feature/UI/test files. Stop at READY with exact shared-locale/reader requests; root freezes all writers and runs one migration/contract/build/test/browser lane. Proposed test commands are the new API class filter and new CJS script, plus focused existing AdminContract/staffing/team/picker checks. No runtime, migration or human acceptance is implied by this proposal.
