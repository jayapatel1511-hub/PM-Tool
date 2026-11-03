# G51: person-name surface completion (S43)

**Inventory plus G51 implementation handoff. See the final ALL SOURCE READY section; functional checks remain unrun.**

Lane D, existing capacity/people chat. Current CONTROL has checkpoint23/generation51, sourceLoans.D empty and this exact documentationLoans.D. The existing `capacity-people/requests/` directory is used because `people/requests/` does not exist.

Build root: `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`.

## Authority and evidence boundary

This is the only file written by this turn. Product/C#/schema/generated contracts/existing tests/shared ledgers remain frozen to D. No runtime, network, browser, DB, build, generator, migration, service, helper, commit, push, deploy, flag activation or acceptance action. Proposed loans below require exact coordinator authorization.

FR-SCR-74 is `spec-parts/32-screen-completeness.md:356`; AC-SCR-74 at500 requires Lena's permitted facts/hours and Rita's facts without hours. FR-SCR-76 at367 / AC-SCR-76 at502 requires a separate <=80-character responsibility note, PM/member's actual DL authority and no role/permission gain. S43 remains unchecked at `specs/053-screen-completeness/tasks.md:52`. D21 remains formally open at spec-part541.

Existing checkpoint22 receipts cover core Person API/UI and note behavior. Coordinator's latest message reports checkpoint22 closed locally, S14/S33 checked,30of49; final real API/browser joins PASS with zero writes. That is coordinator-reported, not fresh execution here. **Core PASS does not close global names.** Remaining052/053, D21 and pending utility navigation are unchanged.

## Classification and shared contract

- **K:** actionable passive actual User ID/name. Keep in S43, including historical/inactive Users. Endpoint may correctly404. Do not exclude known-ID passive names to close scope.
- **C:** existing select/edit/destination/menu/remove control. Preserve action, add separately labelled Details for selected known person as needed; never nest controls or replace record navigation.
- **N:** current wire lacks safe typed identity. Keep text and track an additive canonical read seam; never infer by name/email/GUID/work-record ID.
- **E:** actual ExternalParty/System/unknown. Preserve typed meaning. External-party ID is not User ID. Selected person's own panel heading is already its destination; supervisor remains K.

Reuse `web/src/components/hub/person-panel-trigger.tsx:18-29`: userId/name/active/staffScope/children/className/focusKey, flag gate, stopPropagation, origin capture. Explicit key at24 hashes pathname/source panel/User/full role key into a bounded DOM ID. Every repeated/nested/remounting source needs record/role/surface key, not useId alone or name/index alone. Same User in two roles, phone/table and group/detail needs distinct suffixes.

Reuse `panel-host.tsx:32-42,54-57` and `lib/record-context-navigation.ts:43-49,53-65`. Preserve exact URL/hash/filter/page/workspace/project/source panel, content/dialog/sheet scroll, <=200-character source focus ID, parent return chain and local draft/reason/selection. Person closes to immediate origin, then origin to parent. No DOM object in history/global return cache. Prove custom-dialog remount/focus behavior instead of assuming retention.

`person-panel.tsx:21-27` scopes query by viewer/target/staff scope/page/flags, drops cached facts on error, disables capacity without valid facts. Shared capacity already suppresses errored cells. `lib/keys.ts:12-14,37-47` invalidates relevant person/team/project facts. New read-only triggers usually need no cache/write changes. Visible actor identity never grants Staff scope; only actual Staff origin supplies its validated scope.

`components/hub/registers.tsx:13-15` Person and `record-context.tsx:29-51,80` already wrap typed Users. Preserve External/Removed/Unavailable meaning and source-ready actor keys.

## Actionable inventory

Paths abbreviated below resolve under `web/src/`; backend filenames under `src/Hub.Api/Features/`. All references are current generation50 source inspections. Every K remains required; groups are implementation proposals, not independent acceptance claims.

### History, comments and feeds

| Class | Source evidence | Identity / minimal change |
|---|---|---|
| K | `components/planner/EntryPanel.tsx:23,68` | History row.actorId/name from ActivityEntry. Key `planning-entry:<entryId>:activity:<activityId>:actor`; preserve draft/version/history |
| K | `components/hub/activity.tsx:63,76`; `Activity.cs:18-20,123-124` | Group summary and flat actor have ActorId/Name/Type. Separate activityId/actor/table vs group/firstActivityId/actor keys. Split actor from summary toggle; no button inside summary |
| K | `components/hub/fields.tsx:340-347` | HistoryList actor and repeated group-summary actor have r.actorId. ItemType/itemId/firstActivityId/role keys; preserve groupChanges/disclosure |
| K | `pages/Notifications.tsx:181-194` | e.actorId/name; notification:entryId:actor; no item-open/read mutation |
| K | `components/hub/comments.tsx:38-43`; `Comments.cs:16,78,86` | AuthorId actual User FK. Key comment:type:itemId:commentId:author; preserve edit/delete/tombstones |
| K validated mention | `components/hub/richtext.tsx:6,9-12,23-43`; `comments.tsx:56,61`; `Comments.cs:15,18,78-89` | Stored UUID token plus API MentionRow.Id/Name. Source-aware render context; key commentId/block/token-position/UUID; no arbitrary UUID-text conversion |
| C | `comments.tsx:104-111,138-145` | Mention options insert token/restore caret; listbox/keyboard selection remains, no nested Details |
| N -> root read | `pages/projects/work-activity-comments.tsx:9,13-19`; `TaskQueries.cs:265-267` | lastActivityActorName has no ID on wire; latest canonical activity knows ActorUserId. Add optional ID/kind from SAME deterministically ordered row, null System/unknown, never independent mismatched latest queries |

Generic ChangeList old/new/snapshot labels (`activity.tsx:31,39-51`) are not proven Users. Never wrap every GUID: discipline/external/deleted references exist. Typed field-reference metadata is a separate seam; retain explicit residual gap where identity cannot be proved.

RichText also renders inside editable InlineText (`pages/projects/TaskPanel.tsx:242`), whose editable Display is a button (`fields.tsx:237-240`). Do not globally insert mention buttons there. Use safe separate read-only/adjacent mention presentation; preserve Markdown sanitization/edit activation. Composer remains editor.

### Coordination PersonLabel and actor fallbacks

`pages/projects/CoordinationForms.tsx:53-56` receives actual id/name or options.people but renders plain text. Add focusKey + existing trigger centrally; callers supply record/role keys. peopleChoices28/SelectField37 remain selectors. Missing name stays Unavailable, no raw UUID link or new directory/options query.

All rows below are K; filenames are `.tsx` in `web/src/pages/projects/`.

| Caller | Lines | Key identity |
|---|---|---|
| CoordinationRegister |81,109| register kind/row.id/owner-coordinator/table-card |
| Changes |103,115-116| notice/assessment ID + owner/reviewer |
| Reviews |113,123,126,140-141| package/round/assignment/finding ID + coordinator/reviewer/resolver/verifier; primary ActorList branch already wired |
| Submissions |88,93,177,184-185| package/table-card-detail; blocker group actual owner UUID; check.id/owner |
| delivery-presentation |60,99| package/coordinator; immutable issue identity/authorised-by |
| ReadinessForms |101,156-157| assessment/constraint ID + owner/removal/affected; preserve primary ActorList |
| Readiness |259,291| target/work-row ID + owner/performer |
| readiness-delivery-presentation |118| targetType/targetId/performer |
| DesignBasis |119; lookup78| basis.id/owner; actual team userId |

`readiness-delivery-presentation.tsx:96` refused candidates emit c.person.name passively. Command allowed=false is not Person-read authority. For kind User + actual id/name + Active/Inactive, reuse typed ActorList with refusal-row role key and retain refusal. ExternalParty/Removed/Unavailable retain typed fallback. Do not skip known internal Users because readiness command is refused.

### Planner/capacity dialogs and views

| Class | Source | Identity / bounded change |
|---|---|---|
| K | `components/planner/AllocationShiftDialog.tsx:65`; `AllocationShift.cs:15` | row.personId/name actual. Key allocation-shift:projectId:allocationId:preview-person; preserve selection/weeks/reason/preview/version/errors/Apply receipt; no preview/apply command on selection |
| K | `components/planner/ProjectPlanningGrid.tsx:16-33,40` | person.id exists before flattening, discarded in view row. Carry in view only, no API change; rowKind/row.key/person, entry/allocation/task-context distinct; retain entry-label action |
| K | `components/planner/ContributionList.tsx:13-18,25`; `Planning.cs:40,73-75` | d.personId and each entry.ownerId. contribution:personId:week:person vs entry:entryId:owner; accessible title/date retained |
| K | `components/planner/BulkWeekDialog.tsx:27` | e.personId/name actual EntryRow; entry.id/bulk-person; checkbox retains selection |
| K | `components/planner/FindRoomDialog.tsx:42-43`; `Planning.cs:31-33` | match/unavailable.id/displayName, distinct role keys; partial/provisional refusal and authority unchanged |
| K | `components/planner/PlannerGrid.tsx:64-65`; `PlannerMonthGrid.tsx:31`; `pages/Workload.tsx:192-197` | Supervisor group names; cohort knows supervisorId, `planner/labels.ts:35-50` loses typed ID/groups by text. Root small typed group/render seam; no label lookup; duplicate-name supervisors must not link/merge wrongly. Summary/header keys distinct |
| K | `pages/Time.tsx:181` | e.userId/e.person; time-entry:entryId:person |
| C + Details | `EntryPanel.tsx:63`; `TimeAwayDialog.tsx:13,30` | Keep picker/form action; sibling Details uses CURRENT displayed form ID/name, not stale entry/person; safe form survives |
| C + Details | `pages/projects/Allocations.tsx:139-141,494` | Existing allocation/FilledBy destination stays; adjacent Details uses personId; detail heading336 already wired |

### Work, board and project metadata

| K surface | Source | Identity / control constraints |
|---|---|---|
| Task non-managing assignee/reviewer | `pages/projects/Tasks.tsx:585-595` | r.assigneeId/reviewerId. Managing picker stays + Details; task/role/table-card keys |
| Workspace Task assignee/reviewer table/card | `pages/WorkspaceTasks.tsx:176-177,217` | Actual Task IDs; distinct responsive keys; preserve grouping/selection |
| TaskPanel disabled person/collaborators/watchers | `pages/projects/TaskPanel.tsx:189-191,464-484` | Chips are spans with separate remove, NOT picker options. Their actual User names are K; role keys, details never removes/toggles Watch |
| Shared InlinePerson | `components/hub/fields.tsx:303-316` | Disabled Display is div234-235, can trigger passive name. Enabled picker stays + sibling Details. Add caller focusKey, use kept/displayed ID/name, not stale props |
| Risk/Issue owner/raisedBy inline | `pages/projects/Registers.tsx:721,793-794`; remaining `<InlinePerson` callers | Exact item/role keys; disabled/save/reason retained; one fields writer |
| Task dependency assignee | `pages/projects/TaskPanel.tsx:339-347`; `Dependencies.cs:21-23` | e.task.assigneeId, NOT task.id/dependencyId. dependencyId/direction/assignee key; retain Task link/remove |
| Dashboard Task sublist assignee | `pages/projects/Dashboard.tsx:321-325`; `Dashboard.cs:53` | TaskQueries.Row.assigneeId; main actor/lead wiring does not cover it; section/task key |
| Board facts/footer performer/reviewer | `pages/projects/Board.tsx:330-338`; `board-screen-presentation.tsx:47-48` | Actual assigneeId/reviewerId; stop propagation; preserve drag/card/select; facts/footer keys differ |
| Board lane lead/PM | `pages/projects/board-screen-presentation.tsx:27-39` | Fact state/id/name exists. Keep consistency fingerprint, return typed Known fact rather than label only; mismatch/unavailable/unassigned never guessed |
| Coordination Who/decision/round lead | `pages/projects/Coordination.tsx:97-98,184,201,264,302-307` | Actual task/deliverable/lead IDs. Decision264 omits ID despite WeeklyDecision.OwnerUserId (`Dashboard.cs:77`); pass User-only field, null external plain; no API change |
| Discipline coordination issue owner | `pages/projects/DisciplineCoordinationView.tsx:189`; `DisciplineCoordination.cs:27` | ownerId User FK; unknown label unavailable |
| Handoff sender/receiver table/card/detail | `pages/projects/Handoffs.tsx:111,118-119,229-230` | sendingOwnerId/receivingOwnerId; retain From/To text, handoff/role/surface keys |
| Milestone expanded Deliverable owner | `pages/projects/Milestones.tsx:214,228` | d.ownerId; retain Deliverable destination/responsive keys |
| Issue raisedBy table | `pages/projects/Registers.tsx:228`; `Registers.cs:56` | raisedById; shared owner Person does not cover it |
| Discipline Action lead | `pages/projects/Meetings.tsx:59`; `Meetings.cs:26` | a.leadId/name User; discipline owner NOT User. User owner already Person70, external stays external |
| Project PM popover/meta | `pages/projects/ProjectLayout.tsx:143,174` | p.projectManagerId, distinct popover/meta keys; prove dismissal/focus return |
| Portfolio PM | `pages/Portfolio.tsx:167` | r.pm.id/displayName; project/pm/surface key; print readable |

Shared Person/RecordContext does not wire parallel plain labels automatically. Existing optional-focusKey callers need representative actual-remount/dual-DOM checks too.

### Organisation, supervisors and Vacation

| K surface | Source | Identity / minimal seam |
|---|---|---|
| Team/workspace supervisor column | `components/hub/team-organisation-controls.tsx:8-10`; `team-organisation.ts:3-16` | organisationFacts.supervisor.id/name User; office/discipline/unit NOT Users. Typed facet renderer + source member/person key |
| Team/workspace grouped/phone supervisor | `pages/projects/Team.tsx:162`; `pages/WorkspaceTeam.tsx:70`; `team-organisation.ts:38-43` | String group loses identity. DataTable.groupRow seam (`components/hub/registers.tsx:52,63-68`) with actual group ID, no label lookup; inspect custom phone cards too |
| Person supervisor | `components/hub/person-panel.tsx:39` | Typed supervisor; Person -> supervisor -> original Person page/scope -> Team |
| Organisation leader/placed/unplaced | `pages/Organisation.tsx:25,29`; `OrgStructure.cs:21-25,60-63` | u.leaderId/p.id; unit toggle retains expansion, outside names trigger; unit/person/placed keys |
| Admin User/supervisor | `pages/admin/Admin.tsx:276,282-287` | u.id/supervisorId actual names; passive names, Edit/Reassign separate |
| Admin Structure cycles/people | `pages/admin/Structure.tsx:28,31` | p.id; retain warning/home-unit selectors; leader select30 stays C |
| Vacation grid/card/quiet list | `pages/Vacation.tsx:65-66` | p.id; quiet joined text becomes keyed readable names with commas; distinct grid/card/quiet |
| Vacation schedule/reconcile/import preview | `pages/Vacation.tsx:83,100,110` | person.id/w.personId/r.personId mapped through actual identities. sourceSystem/externalId NOT Users; keep form/reason/preview/apply/confirmation, no extra HR query |

## Root read contracts and Reports

Follow-up owner is passive at `web/src/components/hub/my-work-follow-up.tsx:19`. Exact producer is **`src/Hub.Api/Features/MyWorkFollowUp.cs`, not MyWork.cs**: DTO12 OwnerId/Name; internal projections39-50; Decision53 combines OwnerUserId OR OwnerExternalPartyId; internal handoff/change/open-role55-60; merged User/External name dictionaries68-73. OwnerId alone is unsafe. Root optional flag-owned ownerKind or ownerUserId must be derived at canonical projection, not merged-name lookup. Known internal Users remain required; external text/destination/Unwatch retained. Key view/itemType/itemId/owner.

`web/src/pages/Reports.tsx:255-278` generic default emits person strings. `Reports.cs` retains TaskQueries rows453,462,470,486,639; Deliverable503,513; Decision550; Action594; ActivityEntry645-651; nested PM definition128. Known-ID variants are K. Root/shared explicit report-code/column-path -> validated User resolver from actual fields/ownerType/actorType; key code/stable record or activity ID/path/surface. Preserve units/filter/sort/views/exports/key links; no arbitrary dynamic path/GUID/name heuristic.

N report projections lose identity: under-review `Reports.cs:616,627`; TaskHoursRow685; StaffRow822; unlinked-entry `CapacityReports.cs:12,142-145`. Inventory code/path and add nullable typed User references from SAME report-authorized projection, no directory lookup. CSV/XLSX/PDF remain data; interactive HTML needs actual known User action. Keep N gaps explicit until covered/decided.

## No-ID, external and existing controls

| Class | Evidence | Disposition |
|---|---|---|
| N | TaskLastActivity above | Optional canonical actor ID/kind; explicit gap |
| N | `pages/MyWork.tsx:245`; `MyWork.cs:26,97,133,145` | Successor AssigneeName lacks AssigneeId although canonical source knows it; optional ID, never successor Task ID |
| N | `pages/projects/Decisions.tsx:304`; `Decisions.cs:38,246,255` | ItemLinkRow.Person string; targetId is work record, optional canonical typed PersonRef |
| N/C | `components/hub/search.tsx:40,44-47`; `Search.cs:37-51` | PM/author/owner snippets mostly no User ID; preserve record result action, separate Details needs typed read; no comment.id as author |
| C + Details | `search.tsx:41-42`; `Search.cs:53` | SearchPersonHit.id actual User; preserve authorized My Work action, separate Details without nested command option; palette return test |
| E | `Decisions.tsx:57-61`; `Meetings.tsx:53-56`; typed context | Keep ExternalParty/no-account meaning; discipline lead separate K |
| N/E | No-ID historical actor/System/generic snapshots/removed-unavailable | Explicit state, no guess; historical/inactive known User remains K |
| C | PeoplePicker/native select/mention option/checkbox/assignment/remove | Original action + separate selected-person Details |
| C/context | `app/Shell.tsx:217-223`; `pages/Home.tsx:252`; `MyWork.tsx:298`; `ReassignWork.tsx:55`; admin UserDialog385 | Preserve toggle/context role; passive known-person context can expose Details via actual me.id/w.person.id/person.id/user.id. Login picker `Entrance.tsx:95-96` stays unauthenticated identity selection |

Census searched Person/PersonName/PersonLabel/InlinePerson; actor/author/person/owner/assignee/reviewer/PM/lead/supervisor; sender/receiver; generic person/report cells. This is source evidence, not an exhaustive runtime census. Repeat final census and classify residuals; regex counts are not acceptance.

## Proposed disjoint loans and overlaps

1. Shared activity/comments/richtext plus HistoryList/InlinePerson hunks in fields.tsx, **one fields writer**. Author/actor needs no backend. Root labels/navigation/cache interfaces only.
2. Coordination PersonLabel/callers/Who/refused candidates, helper-first. C overlap: Reviews/Submissions/delivery/readiness. Preserve S10/S33 work, no concurrent writer.
3. Planner EntryPanel/AllocationShiftDialog/ContributionList/ProjectPlanningGrid/BulkWeekDialog/FindRoomDialog: only person render hunks; no math/command/preview/version logic. Shared grouping root-owned.
4. Tasks/WorkspaceTasks/TaskPanel/Board/helpers/DisciplineCoordination/Handoffs/Milestones/Registers/Meetings lead. Consume agreed InlinePerson interface; no second fields writer.
5. Supervisor/group render, PersonPanel supervisor, Organisation/Admin/Structure, Vacation/Time, Portfolio/ProjectLayout and Dashboard task sublist. Coordinate actual A/C/root ownership before exact hunks. No header/overlap style edits hidden in person work.
6. Root optional actor/owner-kind/secondary/report contracts, generic report resolver, shared grouping/navigation/keys/locales/generation. **MyWorkFollowUp.cs** owns polymorphic owner producer; MyWork.cs separately owns successor preview. No migration expected for read projections. Additive/flag-owned/null omission/contract review before generator.

No Deliverables.tsx name loan is needed: its shared Person handles owner/reviewer. C's isolated import/export helpers need no shared Deliverables authoring from this inventory. Reports/milestone projections have separate files. Existing Person callsite focus keys can follow demonstrated remount need. Shared files require single coordinator-owned writer. Each loan needs exact files/hunks/baseline hashes/writer/props/checks; this document authorizes no execution/helper/shared-ledger mutation.

## Focused acceptance after implementation authorization

Static: final app types/lint, affected fixture types, whitespace, JSX interactive-ancestor/unique-ID audit and final K/N/C/E census. No new write on Person selection. Avoid markup-mirroring tests.

Extend existing Person suite style with representative source classes:

1. Comment author/validated mention keyboard -> Person -> exact comments URL/nonzero dialog scroll/source focus; dirty composer/caret survive. Edit/delete/mention picker unchanged; null/deleted author and actual404.
2. Flat/group activity, HistoryList, Notifications: correct actor/disclosure, no record/read/unwatch mutation. System/unknown no trigger; duplicate User rows unique stable keys.
3. EntryPanel history/shift preview: retain draft/version/history/selections/weeks/reason/preview, zero preview/apply/PATCH/POST/DELETE. Prove actual dialog remount/focus; bounded root return-seam repair only if evidence requires.
4. Truly unmounted Review/Submission PersonLabel and refused candidate User: exact assignment/finding/check/draft return; same User coordinator/reviewer/resolver keys differ; refusals unchanged.
5. Managing picker selects, disabled/read-only name opens Person, enabled Details separate. Collaborator vs remove and watcher vs Watch independent; Board drag/card/key action preserved.
6. Person -> supervisor -> original Person page/scope -> Team; duplicate-name supervisors distinct IDs; office/unit/discipline never User links.
7. Vacation/preview, PM popover/meta/Portfolio, Time and planner project/bulk/find-room/contribution: correct User/source state; desktop and390px phone, no duplicate IDs/clipping/overlap.
8. Follow-up User vs external Decision; report known actor/assignee/PM vs label-only; preserve units/filter/sort/view/export/key navigation/Unwatch.
9. Dark people_screens: no Person buttons/reads, original text/control/destination. Rita no Workload/capacity-off: permitted facts without hours/forbidden planner request. Refreshed404 clears cached facts/cells: observe actual request AND answered error, not reopen inside staleTime.

Normal API: trigger-only source reuses core Person/responsibility receipts; no default full .NET rerun. Optional projection contract changes need focused same-row actor/System/null/inactive/deleted, polymorphic kind, dark omission and restricted project/report authority cases. No directory broadening/Staff grant. FR76 PM/DL428/409/trimmed80/no-role/no-permission invariants remain; rerun if changed command/cache/contract warrants.

Closure: every K demonstrated; N has safe typed source or explicit unresolved decision; controls preserved; final census/checks in coordinator evidence. No agent marks Accepted/closes D21/answers utility navigation/reduces052/053 scope.

## Preparation delivery

PASS: current structured CONTROL ownership checked before writing; only this document authored. Earlier guarded attempts stopped before file creation as CONTROL evolved; no product mutation. UNRUN by instruction: all runtime/browser/network/DB/build/generator/tests. READY for exact next source loan; global S43 remains OPEN.


## G51 implementation: ALL SOURCE READY / HELD

The coordinator authorized the exact structured sourceLoans.D array after the inventory review. All **33 named existing frontend files and two new browser/fixture sources** are now authored and held for root's independent review and execution. No source outside that loan was edited by D. This section supersedes the earlier preparation-only status; it does not mark S43 globally complete or Accepted.

Implemented within D:

- Actual User/Admin activity actors, comment authors and server-validated saved mentions; System/missing/external remain explicit. Raw UUID-looking description tokens have no stored validation context and remain an explicit N gap; no client-side identity guessing. Generic audit old/new/snapshot references remain N pending typed reference metadata.
- All owned PersonLabel/Who callers, record actors and by-person labels, including review/finding/constraint/prerequisite/promise history and Handoff CreatedBy/Actor. Actual User item labels in ActivityTable use User kind and exclude the existing Role-label special case; original record destination is retained with sibling Details.
- Planner history, shift preview, project grid, contributions, bulk week and room-finder names; current selected-person Details keeps the actual form ID/name. No capacity math, preview/apply/save/version ownership changes.
- Task/Workspace/Board assignees, reviewers, assignee groups, collaborators/watchers and dependencies; inline edit, picker, remove/watch, card/drag/select/fold actions remain independent. Group labels require consistent actual names, never an Unassigned fallback as a User label.
- InlinePerson optional focusKey interface and D Task/Registers role keys; separate Details for enabled pickers, passive known name for disabled branch. Existing inactive formatting preserved.
- Typed FollowUp ownerKind==='User' only; old/unknown/external remains text. Query key is original prefix/viewer/view plus people_screens/work_screens dimensions. Root owns canonical discriminator/read cache invalidation and normal-API proof.
- Root optional ItemLinkRow.personId/person consumed in the Action linked-item renderer via structural optional props; no targetId-to-user inference. Other root projections/generated files untouched.

Shared interfaces delivered:

1. `fields.tsx` exports `useRecordTab(type: string, id: string, initial: string, allowedTabs: readonly string[]): [string, (value: string) => void]`. Both flag hooks unconditional; people OR work enables exact recordTabItem Type:id + caller allowlist. Setter is stable, retains local state, ignores unsupported values and preserves location.state. Both-off keeps old local-state behavior. Removed/foreign tabs fall back safely. D Task/Action/Risk/Issue consume it; root Deliverable/Decision consumers remain root-owned.
2. Task explicit taskSection=comments behavior remains; generic manual change clears competing taskSection and retains the scoped recordTab pair. Initial taskSection=history compatibility is preserved. No utility/sidebar navigation change.
3. Native details markers are explicit stable IDs plus data-person-source-disclosure only people_screens in ActivityTable, HistoryList and Review finding history. Root capture/sanitizer/restore carries BOTH open and closed native state; React-controlled tab state uses the hook, not a false native-fold claim.
4. Root KanbanCard.overlayChildren consumed through Swimlane render(row, overlay?) and CardBody.personLinks=false. Actual card keeps stable record/role IDs, drag preview uses passive person labels. Root owns the shared Kanban component.
5. PersonLabel optional focusKey; ActionOwner optional focusKey; ActivityActor typed actorId/actorName/actorType plus required focusKey; BoardReviewer/SwimlaneFacts stable keys. Root MyWork/Deliverables callers should supply exact per-role/per-occurrence keys where they reuse these interfaces.

New browser source is `web/tests/person-surface-completeness.cjs`, with typed `person-surface-completeness.fixture.cjs`. It never starts services. Unexpected non-GET business methods return409 and are recorded (only existing synthetic sign-in and one explicitly armed shift-preview setup read allowed); unexpected GETs return501. No fallback200 masks a command. Cases cover saved author/mention, real Task and Review remount, dirty composer, answered historical404, collaborator/watch/disabled-field actions, User/Admin grouped history, actual open plus explicitly closed folds, natural exact focus/nonzero source scroll, notification actor without acknowledgement, scoped Risk comments/history and foreign/unsupported tab rejection, dirty EntryPanel, retained shift preview/reason, polymorphic FollowUp, real keyboard drag cancellation/unique IDs, phone/dark, no-Workload and both-flags-off behavior. These are authored cases, not passing receipts.

Verification boundary:

- PASS source-only: all owned paths inspected; added-line whitespace review against pre-G51 lane snapshots and new-file text. Tracked git diff --check had no diagnostics. New-file no-index status1 represented added-file differences, with no whitespace diagnostics.
- UNRUN by instruction: Node syntax parsing, app/fixture types, lint, SSR, production build, browser, API, .NET, generators, migrations, DB or service operations. Root must perform final independent source review and smallest meaningful execution after freeze.
- No helpers, staging, commit, push, deployment, flags, decision closure or acceptance. Original prototype and other lanes preserved.
- Global S43 remains OPEN across A/D/root and the explicitly tracked N cases. Other052/053 gates, D21 and utility-navigation decisions are outside this handoff; no scope reduction.

Final held source SHA-256 (root may use these for the freeze):

| File | SHA-256 |
|---|---|
| `web/src/components/hub/activity.tsx` | `2a511acfd9ae04133226ba18c1ed1f08bdcb4c96bf99374b244e1b6cadab5753` |
| `web/src/components/hub/fields.tsx` | `0d54c49deca33c64799ab37fd89b242f35c83bb06cd9e85a76b2eb5f8416dbe4` |
| `web/src/components/hub/comments.tsx` | `0830874821f6dfa07d20fe08b69e66ed8545879b812c5929da9e0ee3b45afdb3` |
| `web/src/components/hub/richtext.tsx` | `d422d388d70c844d1926f3f4216ea10907583840ba60a14dbb2d806813d22d32` |
| `web/src/components/hub/my-work-follow-up.tsx` | `a5656dc6a001d34ca718137305ca81effc3d41e6cb14e14f72e9f8b26ac93afb` |
| `web/src/pages/Notifications.tsx` | `3b282e5a9802a14b19754f20c6ea5f61f02b2b51ba79bd646977b49539aba6e5` |
| `web/src/pages/WorkspaceTasks.tsx` | `50f706c8a72bab534e42b93312d57b7dbad010fe97be7a338519acc7f9429610` |
| `web/src/components/planner/EntryPanel.tsx` | `c7d52a0649cd1bd583f4513ea6bceb58855d585180a1765887686d6ebf4f2f26` |
| `web/src/components/planner/AllocationShiftDialog.tsx` | `1f46d5b6e89452a0bed3cb72742bbcdbf9460ac7ffc8c8ad11950f70608d8441` |
| `web/src/components/planner/ProjectPlanningGrid.tsx` | `7b19c6667b858e84998e99ff749f8970665985f989950b49cfad00081d9efa1a` |
| `web/src/components/planner/ContributionList.tsx` | `a5424f306aa0f6215f820be1cd4c4016fc100eeb1b4a8fb2894b0d8308d10479` |
| `web/src/components/planner/BulkWeekDialog.tsx` | `0c2e60b2c7b34fe93292c5c612abacdf03df9f0703a9428e52ff6c58d15db67c` |
| `web/src/components/planner/FindRoomDialog.tsx` | `d91b5e5046dcb38aad116f7afbb0371a2f009441972d0c9d8c9b60d93cc0f20e` |
| `web/src/pages/projects/CoordinationForms.tsx` | `122a640f59cf8f4b80cc021fc58fcfae3d3a9ecdb6fc6b0957f7fc0771e49cd5` |
| `web/src/pages/projects/CoordinationRegister.tsx` | `3360533bcde5f362a185dede381f6fec6e1d74b7e5950e94001cf0b0c47235c4` |
| `web/src/pages/projects/Changes.tsx` | `fa1ffafcc8a0c97751904e2757a25fa1fd7e950e4c726f87b9188a1294a26744` |
| `web/src/pages/projects/Reviews.tsx` | `33848f761230d61711e57c1716663882cf531cbf445989baa7f557effd122e15` |
| `web/src/pages/projects/Submissions.tsx` | `87aa8fa193f7221b9f996c54664738c297a8e10a49742aeccc12693006ad527d` |
| `web/src/pages/projects/delivery-presentation.tsx` | `f1350e16a10cfb6ab4b0e0de454059c0dfb45be617d75a415a10e60d8a778393` |
| `web/src/pages/projects/ReadinessForms.tsx` | `f8906b536790d03513b4d6f9e5dc83539dbe0d75bd66325edf34fccab1d48e62` |
| `web/src/pages/projects/Readiness.tsx` | `ce34c0b6cddd1496073ae67253f14d7cdec89b284703ca7cff6dced94dc74d3f` |
| `web/src/pages/projects/readiness-delivery-presentation.tsx` | `9798f70c762701ca92608abba027e0d803a4e9eddc259040c9840e8e7b1cb450` |
| `web/src/pages/projects/DesignBasis.tsx` | `2b2e85853a1557e21355cf6513d43e6395069e7f5434e28646c97ede0f0d83dc` |
| `web/src/pages/projects/Tasks.tsx` | `5012260960f922d54a3fb92e3190d9ac4f1515c2e2e0040e1fbdc192cb1f616c` |
| `web/src/pages/projects/TaskPanel.tsx` | `8b6eba8db92422fb54660267bda0cbb94fc0a4e2b96ce06e3c96d1f9206152fc` |
| `web/src/pages/projects/Board.tsx` | `3603da179516fa31edda58ddd584813a0817cbb75b1a784c3df8603f1ef1597c` |
| `web/src/pages/projects/board-screen-presentation.tsx` | `695f1d40b2a8c5086adc25f18c03ba6ebf0f467193f1966477b1143f2a68e7aa` |
| `web/src/pages/projects/Coordination.tsx` | `03901210edd84a4aed6be0895d330c46b3b051a0d1658d31dcb6de0a34e1494d` |
| `web/src/pages/projects/DisciplineCoordinationView.tsx` | `8a58a6b8c9f92228c006db5569e00b4f61c3e33e69956551f3e0d60bd61876de` |
| `web/src/pages/projects/Handoffs.tsx` | `624c8b249d539e3c9aa250f0e8b13508442f19495d77a42edc5652879c32ba30` |
| `web/src/pages/projects/Milestones.tsx` | `a9e191645421409181c67544371c2d6dd73d1dce4453c3318c00421747d1305a` |
| `web/src/pages/projects/Registers.tsx` | `79905dd2ea114ac0f339b80d8c5b18fbaffe0a526b09f14a8a9fa640c4ac6c16` |
| `web/src/pages/projects/Meetings.tsx` | `a9f0247ee7a82801a34bcfcd434b713c3410b39d5a6a96c5795cba4e3ef7422d` |
| `web/tests/person-surface-completeness.cjs` | `235bce1b9cc6ab9d4f2ee0c01ee7d696220fb24b297340dc91c476739816b28a` |
| `web/tests/person-surface-completeness.fixture.cjs` | `8a5f01dcfc033af1325d3b740d49fb38332b3aee04031824e35ffd6ff05e5630` |


## Final independent-review P2 repairs: ALL SOURCE READY / HOLD (2026-11-02)

The coordinator reopened exactly Submissions.tsx, activity.tsx, fields.tsx and the two new person-surface-completeness sources for two P2 findings. These bounded repairs are authored and re-held. The useRecordTab helper and all other D application sources are unchanged by this repair.

1. **Same blocker owner in different disciplines:** Submissions now keys the PersonLabel by the complete existing blocker-group key (disciplineId + ownerId), additionally namespaced by package and owner role: `submission:${p.id}:blocker-group:${key}:owner`. The prior owner-only suffix could collide across two groups. The typed fixture supplies two real CoordinationDiscipline references and two SubmissionBlocker groups with the same User owner. The browser case uses people_screens=true/screen_patterns=false, locates both distinct discipline headings, requires different IDs and a globally unique Person-ID census before opening, then opens the SECOND trigger. It observes actual source unmount, nonzero dialog scroll and natural exact focus return to that second ID, explicitly excluding the first. It retains zero unexpected business writes.
2. **Dark grouped activity/history behavior:** The moved ActivityActor is now rendered outside the ActivityTable summary only when people_screens is on. With the flag off, the original actor-named translated summary and native click target remain. HistoryList likewise retains the actor name in its original summary when off. Dark flat/top actor text uses its original actorName/System fallback. With people on, Person actions stay outside summaries; no interactive button is nested in a native toggle.

New focused proof in the existing new browser source checks both dark ActivityTable and dark HistoryList summaries identify the actor, contain no nested button/link, and toggle their original native details open and closed on click with no Person reads. Activity's dark group also retains its details-only composition (no displaced actor span or text outside). With people on, the browser reads the native Activity group open state WHILE Person is open, before any return restoration: the outside actor action keeps a closed group closed and the inner row action keeps an open group open. Existing parent-interactivity checks and open/closed History return assertions remain.

Verification status: source-only review and ownership/hash comparison confirm only the five reopened source paths changed; all other 30 held D application files match their pre-repair hashes. No Node parsing, typecheck, lint, build, SSR, browser/API/test execution, helper, service, DB, generator, migration, commit/push/deploy/flag/acceptance action was performed. Runtime and final independent re-review remain coordinator-owned and UNRUN here. The preceding SHA-256 table has been refreshed for these five paths. Global S43 and the broader052/053 goal remain open.


## S10 visible-detail integration: ALL SOURCE READY / HOLD (2026-11-02)

The coordinator reopened only TaskPanel.tsx, Meetings.tsx, Registers.tsx, Handoffs.tsx and Changes.tsx for S10 integration. These five application files are now authored and re-held. No other application/test/shared/backend/generated/cache/locale source was edited by D in this pass. The prior Person P2 repairs had passed the coordinator's independent source review before this reopen; runtime proof remains separate.

Six detail components consume the root-owned `useWorkSeenDetailOwner` and `WorkSeenDetailNotice`, which wrap A's `useVisibleWorkDetailSeen`. Every call is unconditional before loading/error/alternate-form returns. Each receives the actual React Query result and the answer's canonical ID, rowVersion and top-level optional seen decoration (structural WorkSeenFields intersection pending root generation). A route ID is used only to establish the initial identity before an answer; no source version/decoration is invented. The shared owner also checks the canonical identity against the URL before exposing it.

| Detail owner | Canonical answer | Explicit consumer visibility |
|---|---|---|
| TaskDetail | task.id / task.rowVersion / top-level seen | Panel owner requires exact Task:id; full-page h1 owner requires exact /tasks/:id (optional trailing slash) and no panel. Blocking, deleting, pickup offer, Create Deliverable and root task-actions dialogOpen all suppress exposure. |
| ActionPanel | action.id / action.rowVersion / top-level seen | Exact Action:id and no convert/cancel step dialog. |
| RiskPanel | risk.id / risk.rowVersion / top-level seen | Exact Risk:id and no realise/close step dialog. |
| IssuePanel | issue.id / issue.rowVersion / top-level seen | Exact Issue:id and no resolve/cancel/reopen step dialog. |
| HandoffDetail | row.id / row.rowVersion / top-level seen | Exact Handoff:id, no edit replacement form and no action dialog. |
| ChangeDetail | notice.id / notice.rowVersion / top-level seen | Exact ChangeNotice:id, no selected assessment inspector, missing exact child or command dialog. Reading a containing notice for a ChangeAssessment does not acknowledge that parent. |

The real loaded detail body's div owns the shared surfaceRef and retry notice in all six consumers. Loading/error branches and the Handoff edit replacement have no acknowledged surface. The shared owner handles document visibility, fresh entry/reentry refetch and postcommit invalidateWorkSeen; the shared hook checks success/isFetchedAfterMount/isFetching/error/isPlaceholderData/dataUpdatedAt, exact answered tuple/version/tokens and real mounted visible DOM before POST. Query functions remain reads and contain no acknowledgement. Shared command validation/retry/idempotency stays root/A-owned. With work_screens off the owner issues no extra entry refetch or acknowledgement and the notice renders nothing. Existing Person links, disclosure/tab return behavior, project permissions and business commands are preserved by this bounded diff.

Root supplied `useTaskActions().dialogOpen` from the held Tasks.tsx (ask/moving/offer/starting), avoiding React-node introspection or a D write outside the five-file loan. Root also reclaimed my-work-follow-up.tsx after D's prior hold; its later bytes and the shared owner/modules/cache are coordinator-owned, not a new D revision. The historical table above refreshes only these five new D source hashes.

Verification: PASS source-only diff review against exact pre-integration snapshots at /private/tmp/pm-tool-g51-d-detail-before and scoped git diff --check (no diagnostics). The integration adds only the shared imports, structural additive query return typing, unconditional owner/visibility inputs, loaded-body refs and retry notice. UNRUN here: Node parsing, fixture/application types, lint, build, SSR, .NET, browser/API, generators, migrations and services. No helper/subagent, DB mutation, flag activation, commit/push/deploy or acceptance action occurred. Source READY is not runtime PASS or human Accepted.

Coordinator acceptance still needed: exercise each of the six real detail owners with a fresh answered token/version, validated open commit, re-read Seen and explicit failed-command retry; prove cached/loading/error/prefetch/wrong-item answers cannot acknowledge; verify hidden document and local command close/reentry needs a fresh read; prove full-page Task plus same-Task overlay has only one exposed owner and ChangeAssessment never marks its parent; verify work_screens-off has no POST or new retry UI and preserve prior people_screens return/disclosure checks. Root owns generated contracts, focused types/build and browser/API execution. Global S43 and the full052/053 goal stay open.

Current five-file source SHA-256:

| Source | SHA-256 |
|---|---|
| `web/src/pages/projects/TaskPanel.tsx` | `8b6eba8db92422fb54660267bda0cbb94fc0a4e2b96ce06e3c96d1f9206152fc` |
| `web/src/pages/projects/Meetings.tsx` | `a9f0247ee7a82801a34bcfcd434b713c3410b39d5a6a96c5795cba4e3ef7422d` |
| `web/src/pages/projects/Registers.tsx` | `79905dd2ea114ac0f339b80d8c5b18fbaffe0a526b09f14a8a9fa640c4ac6c16` |
| `web/src/pages/projects/Handoffs.tsx` | `624c8b249d539e3c9aa250f0e8b13508442f19495d77a42edc5652879c32ba30` |
| `web/src/pages/projects/Changes.tsx` | `fa1ffafcc8a0c97751904e2757a25fa1fd7e950e4c726f87b9188a1294a26744` |


## S10 private-detail cache repair: ALL SOURCE READY / HOLD (2026-11-02)

Root explicitly reopened the same five detail-owner files during the generation51 verification freeze for the confirmed private Seen cache gap. Only these five application files changed. The six detail query keys now preserve their existing prefix and ordered IDs and append the actual signed-in me.id plus independent work_screens and people_screens booleans:

| Reader | Current query key |
|---|---|
| TaskDetail | task, id, me.id, workScreens, peopleOn |
| ActionPanel | action, id, me.id, workOn, peopleOn |
| RiskPanel | risk, id, me.id, workOn, peopleOn |
| IssuePanel | issue, id, me.id, workOn, peopleOn |
| HandoffDetail | handoff-detail, projectId, id, me.id, workOn, peopleOn |
| ChangeDetail | change-detail, projectId, id, me.id, workOn, peopleOn |

Existing useMe and Task feature-flag hooks moved before their query; other detail owners add unconditional independent flag reads before their query and reuse the moved useMe result. No duplicate variables, conditional hooks, business query/command changes, visibility changes or shared cache-helper edits. Existing prefix invalidation continues to match all viewer/flag variants. Earlier hashes in this document are historical snapshots; the following five supersede them for D's current handoff.

PASS source-only: exact before/after diff against /private/tmp/pm-tool-g51-d-detail-cache-before, six-key inspection and scoped git diff --check with no diagnostics. All types/lint/SSR/build/browser/API/test execution remains UNRUN by D for this repair; the coordinator's previously reported build1 receipts predate these bytes and require proportionate final-state verification. No fixtures/tests/helpers/shared/backend/runtime/services/generators/flag/commit/deploy/acceptance action. Root retains browser hidden-tab investigation; no speculative visibility product change was made. D re-holds all sources now.

| Source | Current SHA-256 |
|---|---|
| `web/src/pages/projects/TaskPanel.tsx` | `bd42dee6d1ce8df56002a9c60f0c9d224f07f27fbecf7bfca7b27dff2f225ea7` |
| `web/src/pages/projects/Meetings.tsx` | `1b2d83b590b1990fc61e7ba637cdb15116d470ee1e0c9b67be1b32113d3cd1ac` |
| `web/src/pages/projects/Registers.tsx` | `f619a911dc4a08fa795d7a00877888d0cc267f6ab10c1057434a507a7923296c` |
| `web/src/pages/projects/Handoffs.tsx` | `6bee9393858dd8a9410d316f3e77936cec3a2ad1fa6ad1f5ba808b3ebfdaf87d` |
| `web/src/pages/projects/Changes.tsx` | `7905759789948bd2bfd7016af918805bc13e3c4bbf83423dcbdf0e1bf8d08fa0` |


## Generation52 residual S43: ALL SOURCE READY / HOLD (2026-11-02)

Only the exact activeSourceLoans.D eight-file loan was authored in this pass: six existing UI files plus the new remaining-surfaces browser and typed-fixture sources. All prior D files remain held. Root Home/Shell, B Workspace projection/API proof, the root draft hook seam, shared contracts/cache/locale/registry and drawing import UI remain outside this D revision. No helpers/subagents or source execution were used.

| Owned surface | Result and identity boundary |
|---|---|
| ProjectList.tsx:124 | PM group headings in table/cards/board use the actual PM group key and nonempty matching name, with every row agreeing on that PM. View/group/role keys are distinct even for simultaneous hidden card/table DOM. Client/office/status labels remain labels. Empty/zero PM IDs and Unassigned fallback stay plain in headings and existing row/card labels. Selection, star, Peek, navigation, grouping/sort/filter and board presentation stay independent. |
| Calendar.tsx:278,312 | Both read-only organiser sentence and owner field render actual event.ownerId/name. Event ID is used only in the source key, never as a User ID. Existing text is split at a private interpolation marker; no new locale keys. Read-only and editor actions remain. |
| Staff.tsx:264,293 | Assign and Remove titles accept ReactNode fragments around actual person.id/displayName, retaining the exact existing translated sentence. Existing Assignments scope is forwarded to Person; assign project/discipline picks, removal target/reason and membership commands are unchanged. Remove role keys include memberId. |
| TimeAwayDialog.tsx:58 | Manual leave title wraps the actual current person name, with person/record-or-new/title role key. Dates/source/reference/status/hours/version/receipt and saved browser draft remain owned by the existing form. |
| ProjectSettings.tsx:45 | Separate Details is shown only with people_screens and a known current selected PM. The real PeoplePicker callback candidate must match the selected ID; the old project PM name is never used for a different candidate. Picker, current candidate text, edit/save/reason/dirty fields and disabled behavior remain; no lookup-by-name. |
| Files.tsx:81 | Only the optional explicit addedByUser id/displayName/isActive bridge is consumed, and its name must match existing addedBy. Missing/null/zero-ID/mismatched historical metadata stays text. Link/item IDs never become User identities. Full project/item type/item ID/link ID/added-by role keys distinguish occurrences; source link/Copy path/type/search/scope actions remain separate. |

The only query-key change is coordinator-explicitly-authorized Files: `['files', scope.api, f, me.id, peopleOn]`. Existing prefix and original argument order remain, with actual viewer and independent people flag appended for the new flag-gated wire metadata. No shared invalidation/cache source changed.

A concrete dirty-memory-form gap was found during source review. Root added optional fourth preservesNavigation to useSafeForm and forwarded it to useUnsavedGuard. D consumes it only in the owned EventDialog and TimeAwayDialog. Each freezes the mounted source route plus actual event ID, or actual person ID and record/new ID. A preservation exception requires people_screens, the same mounted identity, exact pathname/hash and sorted non-Person query scope, a valid UUID Person target, and a sanitized recordContextReturn URL proving the actual open/close chain. Same-Person page/scope changes additionally need two nonnull matching return origins. Another record, page, filter, non-Person panel, explicit source close and browser unload retain the original guard. This is necessary for existing event and existing manual-leave edits, whose useSafeForm key is null; create-form browser drafts were already retained. Root owns the hook/PanelHost changes and their separate tests.

New browser source has eight consolidated residual cases, separate from the existing fifteen-scenario surface suite:

1. PM group table/cards/board, two actual User IDs with the same name, one PM repeated in two client groups, no fake Unassigned User and retained checkbox selection.
2. Calendar two owner occurrences and two event keys, actual data_entry_safety=true memory edit retained through Person without a discard question; explicit Close still offers Keep editing, actual browser navigation triggers beforeunload, then local Discard resets the independent test case.
3. Staff assign/remove title fragments retain exact all-staff scope, selected project/discipline and unsaved removal reason, with zero membership commands.
4. 375px manual-leave create proves an actual stored draft and retained dates/source/reference; existing-record edit proves memory-only dates survive Person and explicit source Cancel still asks.
5. Settings selects two actual candidates, verifies Details changes its User target and old candidate action disappears, retains unsaved project name and selected text, with no PATCH.
6. Files two author FKs, missing/null/mismatched metadata, real nonzero source scroll and filters/scope; author -> Person -> supervisor actually unmounts the original Person source, then returns naturally through both levels to the original Files trigger. A separate historical author waits for an answered narrow 404 and requires no stale successful facts. No Task/item ID opens as User.
7. Coordinator-owned Home greeting and account-name menu; natural Escape returns to the greeting or Account trigger, preserving existing menu controls.
8. Real flag-off responses at 375px keep the original labels and controls on the new surfaces, omit all Person actions/reads and account-name action, and check page overflow. Staff title off proof also uses the original desktop assignment control.

The harness guards every unexpected non-GET business command with409 and records it; it permits only the existing synthetic sign-in handshake. Unknown GETs return501, including forbidden workload/capacity reads for the no-Workload persona. It checks stable unique Person IDs and absence of nested actions, natural final focus and source scroll without forcing focus after return. Fixtures use typed canonical records and labelled synthetic data. Files metadata remains a structural additive type until root contract generation; the coordinator owns adding the new fixture to tests/tsconfig.json.

PASS source-only: structured active ownership, full six-file before/after diff against /private/tmp/pm-tool-g52-d-remaining-before, focused final source review, scoped git diff --check and new-file whitespace/final-newline inspection. UNRUN here: Node parsing, fixture/application types, lint, SSR, build, all browser/API/.NET tests, generators, migrations, DB/services and runtime probes. Eight AUTHORED cases are not eight passing receipts. Root must run the focused final-state checks and complete the global S43 source inventory; S43 and full052/053 remain OPEN, with no checkbox/decision/Accepted/commit/push/deploy/flag action by D.

Current exact eight-file SHA-256 (supersedes earlier hashes only for these newly reloaned files):

| Source | SHA-256 |
|---|---|
| `web/src/pages/projects/ProjectList.tsx` | `c1b462b2a74cd52accd486286eef08833bbbd7453cd8b2a92d94a3ba67c6a1f6` |
| `web/src/pages/Calendar.tsx` | `31090d4c355cff3003c601f489f0089eb6b583928cf0b409a369d737d23e698a` |
| `web/src/pages/Staff.tsx` | `5ac2db8e861e7a829706087dece23229fc20244cbc4e4349180a136d27423dbe` |
| `web/src/components/planner/TimeAwayDialog.tsx` | `ed16c04541c5110a1d76ae436408266f819188c4009e4f2cebbdeed841b8ef0c` |
| `web/src/pages/projects/ProjectSettings.tsx` | `cdb521ab2308589011a795134b084bef169e1d7948f269c0a17e7d26730d8222` |
| `web/src/pages/Files.tsx` | `68a55308e8e9c1df9fa7753e219522459ef65be84c22c0d03c00ce944e00b492` |
| `web/tests/person-remaining-surfaces.cjs` | `b8401b5087334868ce54cb90be792ec1c0d8abe9f902e914812451dfbc8e6a23` |
| `web/tests/person-remaining-surfaces.fixture.cjs` | `d123c5806e8854eeff4cead619967d2ec1dcfdf5ffe571cad5c79cf6d3697a50` |
