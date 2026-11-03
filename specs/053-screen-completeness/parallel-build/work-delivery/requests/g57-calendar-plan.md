# G57 proposal — S15 Calendar layers and rescheduling

2026-11-02. **READY for root review; then HOLD.** Documentation only, under root's explicit single-file loan. Checkout `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. Checkpoint28 closure remains root-owned and precedes any new source loan. No implementation, acceptance, browser or provider PASS is claimed for S15. No helper or execution was started.

## Outcome and delivery boundary

FR/AC-SCR-24 adds Time away, person/discipline filters and Calendar saved views. FR/AC-SCR-25 adds permitted deadline moves and an Undated task/deliverable tray, using Timeline confirmation/reason rules. Source authority: `spec-parts/32-screen-completeness.md:209-215,437-438`; FR-INT-11 at `spec-parts/25-task-and-timeline-interaction.md:39` requires the affected successors, deliverable children and dragged item's new inconsistencies without silently moving those related rows.

**Owner: work_screens**, Part C (`32-screen-completeness.md:49-52`). Keep existing Calendar/event/date-edit capabilities available when this addition is off. visual_system, date_context, org_structure, capacity_polish and existing working-day rules retain their separate meanings. Do not turn on flags or make Calendar require capacity_screens merely because it displays absence. No new persistent calendar/absence/date-command entity is proposed.

Build two separately reviewable loans:

1. **S15-A:** permission-scoped away projection, clearly defined filters, URL state and existing Saved Views integration. Read-only business behavior.
2. **S15-B:** dated deadline move plus Undated tray, exact current permissions/version/reason facts and canonical single-item mutations. No new bulk/reversal engine. S15-B follows A's stable scoped read contract; neither starts before checkpoint28 is closed and root freezes exact source paths/contracts.

## Observed implementation, not assumed capability

| Current source | Observed behavior / gap |
| --- | --- |
| `web/src/pages/Calendar.tsx:66-121` | Week/Month/Agenda; `mode`, `date`, `hide` in URL; workspace project scope through useScope; four type toggles; deadline clicks open their canonical panel. No away toggle, person/discipline controls, ViewMenu, undated tray or deadline drag. No Calendar-specific localStorage or Me preference is used. |
| `src/Hub.Api/Features/Calendar.cs:18-35,93-136` | Generated CalendarRange/CalendarEntry subtypes; inclusive DateOnly range, maximum92-day difference; Scope.Projects applies visible/live project scope. Deadline projection has no row version or edit/reason facts. CalendarTests.cs:111-113 pins legacy range keys. |
| `Calendar.cs:111-125` | Task cohort excludes Complete/Cancelled; Deliverable excludes Issued/Accepted/Cancelled; Milestone excludes cancelled but can include complete. Due/Date must fall in the requested range. Preserve these legacy cohorts; a complete milestone can display without being movable. |
| `Calendar.cs:64-92,127-135` | Private events belong to their owner; project events follow visibility; project-less private events remain when a project scope is chosen. Event overlap is StartAt < exclusive next-day end and EndAt > local-day start. Event editing/cancel are separate from deadline moves. |
| `web/src/pages/projects/Timeline.tsx:503-532` | MoveDialog previews task/deliverable effects through GET date-context when that existing flag is on, then PATCHes the one source item with the observed rowVersion via If-Match. A dated span moves start and due by the same calendar-day delta. No generic preview token or new commit endpoint exists. |
| `src/Hub.Api/Features/Timeline.cs:104-111` | Task span movement requires both ChangeDueDate and EditTask; DueNeedsReason is from canonical permissions. Do not derive editability from project membership or merely the ability to see a deadline. |
| `src/Hub.Api/Features/Dependencies.cs:176-238,271-282` | date_context is independently guarded. GET computes own inconsistencies, affected successors, deliverable child tasks and permitted assignee/owner availability warnings using current source data and working-day rules. It does not SaveChanges. Availability warnings currently use PlanningEndpoints.People, not every Calendar viewer. |
| `src/Hub.Api/Features/Tasks.cs:338-379` / `Deliverables.cs:319-340` | Canonical writes recheck version, project authority, correction reason and date ordering. Task due change may require its own reason; setting start also invokes EditTask. Task due-change notifications/original-date tracking stay canonical. Deliverable date edits use EditDeliverable. |
| `src/Hub.Api/Features/Milestones.cs:220-258` | Existing POST change-date has DryRun=true (DatePreview) versus real DateChanged. Dry run returns before version check, reason, assignment and SaveChanges. Commit requires ManageMilestones, an open milestone, current milestone version and a reason; optional explicit cascade exists and queries current child rows. |
| `web/src/pages/projects/Milestones.tsx:371-402` | Existing ChangeDateDialog defaults cascade/tasks off, requests typed dry run, then sends observed milestone rowVersion. Preview does not bind a child-version receipt. Calendar must not claim stronger cascade concurrency than this contract supplies. |
| `src/Hub.Api/Features/PersonCapacity.cs:27-58` | Canonical per-person/day values already distinguish Normal, Available, Holiday, Leave, Additional, Provisional and NonWorking. Leave combines current noncancelled imported records with unreconciled legacy Unavailable/Reduced overrides; zero availability alone does not establish absence. |
| `src/Hub.Api/Features/Workload.cs:115-123,140-152` / `Planning.cs:111-120,225-253` | Workload cohorts are role/current-project scoped, with PeopleScope widening. Planner adds the active viewer and supports self-only reads while broad reads require Workload or unit-leader authority. Calendar must reuse these actual authorization rules before filtering/counting. |
| `src/Hub.Api/Features/Availability.cs:98-112` | Vacation & Coverage can show all active people to a Supervisor and peers to some ordinary viewers. This is broader than the required Workload/Planner cohort. Do not reuse Availability.VisiblePeople as the Calendar authorization shortcut. |
| `Records/ScreenLists.cs`, `Features/Views.cs:30-90`, `web/src/components/hub/views.tsx` | Generic versioned personal/project Saved Views and ViewMenu exist; Calendar is not a registered list type. Current MePreferences has no Calendar fields (`Features/Me.cs:23`). |
| `web/src/lib/keys.ts:10-35,57-79` | CalendarEvent refreshes calendar. Task/Deliverable/Milestone roots and availability-change roots do not themselves include calendar; project mutation readers also have shared prefixes. S15 needs explicit root-owned invalidation for its new read and moved-item updates. |

## Proposed read/wire contract — root freezes before coding

Prefer a **separate generated typed GET `/calendar/screen`** under work_screens, leaving `/calendar` and its existing typed range/event replies unchanged. New sidecar supplies permitted choices, away days, deadline action facts and the paged Undated tray. It is not a new command endpoint. Keep these DTO names prefixed CalendarScreen to avoid generic schema-name collisions.

- Query uses the same ISO `from`, `to` and actual project scope as `/calendar`; selected person/home-discipline, showAway and explicit tray paging are additive only here. Validate dates/range/finite identifiers before work; root sets a visible bounded page size and deterministic ordering. Never fetch a huge list and cut it on the client or silently cap totals. Strict source type/ID guards; no client-supplied capability booleans.
- Response contains authorized person/home-discipline choices, exact visible tray page/count and per-source movement facts. Movement facts carry canonical sourceType/id/projectId/key, dates, **actual rowVersion**, canMove, refusal reason and reason requirement; never infer from client role names. Build facts under current permissions, not a list of whole-project permissions copied onto every row. Flag/viewer/scope partition the client key.
- Dated movement facts must match the actual Calendar deadline cohort and selected range; undated rows are exactly scoped open Tasks with DueDate=null and Deliverables with DueDate=null, using the same nonterminal exclusions above. No milestone/event in the Undated tray. Show visible noneditable rows as read-only; lack of edit permission is not hidden work or zero demand. Stable order by project number, source type and key/ID, with exact visible totals.
- Away days use authorized **Workload-or-Planner visibility**, then requested person/home-discipline filters. Reuse WorkloadEndpoints.People, PlanningEndpoints.People and PeopleScope.Widen with their route-level authority rules; helper extraction is a root seam, not a new broad permission. Admin/Executive, direct-report Supervisor, live-project SystemPM, unit leader/read-only combinations and self-only Planner must match current routes. A selectable GUID never grants people visibility.
- Reuse PersonCapacity.Load on those IDs/dates. Return only person identifier/display label, DateOnly, a minimal full/reduced/unknown state, absence hours where known, and provisional indication if retained. Never project leave reason, health data, source-system key, external ID, balances or import metadata; never put them in URL, preference, activity or saved-view payloads.
- **Away4h means absent4h**, not available4h. Do not infer Away from Normal−Available, a holiday, zero weekly capacity, nonworking day or an allocation. Full-day evidence must come from actual canonical absence data; null/partial baseline stays unknown. PersonCapacity.Day.Leave is the numeric source, with its overlap/reconciliation rules. Root must freeze the full-day/provisional presentation decisions below before claiming FR-SCR-24.
- Sidecar failure withholds enhanced actions/away facts rather than showing stale permissions; ordinary Calendar remains usable. A current query error must not reuse another viewer/flag/scope's choices, labels or capability facts.

## Date, preview and commit behavior

**Dates:** Task/Deliverable due/start, Milestone date and away days are DateOnly ISO dates; use the existing addDays/day-number helpers. Do not convert an all-day drop through the browser time zone or a UTC instant. Calendar event StartAt/EndAt remain instants and Start/End remain organization-local text; preserve the existing organization time zone and midnight overlap behavior. Test across the Halifax daylight-saving boundary with a different browser time zone.

**Dated Task/Deliverable:** proposed target day determines delta from the displayed due day; retain Timeline's paired start shift if a real start exists. Read current date-context for that exact proposal and display own warnings, affected successors and deliverable children plus their canonical reasons. Only the dragged item changes. Task span permission is the canonical intersection of due/start authority; a due-only actor must not acquire start-edit rights through Calendar. Root can separately approve a due-only action; do not silently change span semantics.

**Undated Task/Deliverable:** proposal sets dueDate to the chosen day, leaves the current start untouched, and asks the same canonical confirmation/reason questions. Do not synthesize an old due date, a zero-length span, OriginalDueDate or a new start. Existing due/start validation may refuse a day before start; show that refusal without mutation.

**Milestone:** fetch/use the actual typed MilestoneDetail/MilestoneRow and current Manage permission/version at move start. Reuse the existing change-date preview/commit contract, not PATCH date or an invented command. Calendar defaults to only the dragged item (`cascadeDeliverables=false`, `includeTasks=false`); reuse existing optional cascade only if root explicitly includes that UI scope. Do not silently enable it on drop. The separate existing milestone dialog remains unchanged for its current users.

**Preview:** Calendar sidecar and date-context reads write no business data, activity, versions, notifications/mail/outbox or Seen records. Milestone dry run likewise must not change any source row/version, child date or business side effect. Existing transport idempotency receipts, if a request explicitly uses them, are a separate existing middleware concern; do not rewrite middleware to manufacture a zero-receipt assertion. Cancel/escape/navigation from preview issues no commit.

**Commit:** retain the source version actually observed when the user began this intent. Use Task/Deliverable canonical PATCH with If-Match and Milestone POST with rowVersion/reason. Server rechecks current visibility/permission/status/version. A409 or uncertain response preserves target/reason and the original version; do not auto-adopt a refreshed version or silently resubmit. An explicit reload/cancel-and-restart creates a new preview/intent. Disable duplicate confirm while pending. PATCH retry cannot silently become an inverse/new command; POST receipt behavior remains canonical. Network uncertainty is reconciled by authoritative source read, with an explicit user decision before another write.

**Partial rights:** never loop over selected or related rows. Each Calendar action is single-source; successors/children are explanatory only. Lost authority between preview and commit gives the ordinary generic403/404 and withholds stale action facts. Existing milestone cascade does not have per-child preview versions; no stronger guarantee is claimed. A cross-project/bulk command is S12, not a Calendar implementation shortcut.

**Audit and refresh:** canonical mutations own actual changed date fields, reason, actor, OriginalDate counters and notifications. No duplicate synthetic Calendar activity. Root invalidates calendar/screen, actual source detail/list/Timeline and relevant dashboard/availability readers only after confirmed success. Permission/flag/user/member/absence/schedule/holiday changes invalidate the sidecar and safe action facts. Before/after assertions must distinguish derived evaluator reads from a business mutation.

## URL, saved views, clear and flag-off

Keep URL state authoritative: existing mode/date/hide plus projects/ws remain; propose explicit showAway/personId/homeDisciplineId keys. No new localStorage or MePreferences persistence is necessary. ViewMenu uses existing `/views` CRUD and is partitioned by viewer/list/scope. Default views use existing SavedView.IsDefault semantics; persistence occurs only through that canonical command.

Root registers a Calendar list and exact key allowlist in ScreenLists/Views. Recommend **workspace Calendar personal views only** (it has no project Calendar route today), preserving workspace scope and mode/date/type/layer/filter state. Sharing a single-project Calendar view requires an explicit projectId policy; never share a multi-project viewer-specific cohort by pretending it is a project list. Paging/drag target/reason/panel/selected source stay transient and are not saved. Validate and clean removed/unauthorized filter IDs without resolving hidden labels or probing foreign projects.

Clear filters restores all old types, Time away off, person/discipline unset and tray page1; retain view mode, anchor date and workspace selection. An explicit Reset view can have separate semantics only if approved. Applying/back/forward/default views preserves the exact calendar scope and visible date; panel routes and Person overlays use the existing reconstructible context/return mechanism.

With work_screens off: no sidecar request, away controls/rows, tray, drag affordances or Calendar ViewMenu; no new response fields or new Saved Views list behavior is exposed. Ignore/remove only addition-owned URL keys from the active view; keep old mode/date/hide/projects/ws and old event/editor behavior. Preserve stored Calendar views/data while hidden, so off→on restores them after current-rights validation. Clear never deletes unrelated saved views/preferences. Test work_screens independently from visual_system and capacity_screens.

## Interaction and phone acceptance

- Pointer movement snaps to actual date cells, with a ghost/target label; a click without drag still opens the same source panel. No business mutation on pointer-up: it opens the confirmation. Avoid treating a vertical scroll on a phone as a drag.
- Provide a keyboard/touch **Move to date** action that reaches the same proposal/confirmation path, instead of making drag the only way to meet the outcome. If root retains Timeline's no-drag-on-phone policy, keep this explicit action available only with real edit permission. Existing Calendar event create/edit remains available under its own rules.
- Use stable source-specific trigger/focus keys, not array indexes. Cancel/escape returns to the exact deadline/tray trigger; success returns to its moved occurrence/day, or a documented stable tray heading if the source disappeared. Preserve URL filters/anchor, scroll region and keyboard focus after genuine React Query refresh/remount. Navigation with a typed reason uses the existing unsaved guard; no manual history/focus injection in tests.
- At375px, filters and confirmation wrap, the tray can collapse into a labelled section, horizontal week scrolling stays inside its existing region, and the page/dialog do not overflow the viewport. Away is labelled all-day with person/date and known hours. Status, absence and proposed dates are never conveyed only by colour. Do not claim mobile drag from a desktop resize assertion.

## Bounded paths and root seams

| Owner / scope | Proposed exact paths, subject to a later source loan |
| --- | --- |
| B S15-A read/UI | New `src/Hub.Api/Features/CalendarScreen.cs`; existing `web/src/pages/Calendar.tsx`; new `web/src/pages/calendar-screen-state.ts` for finite URL/view semantics. Keep existing Calendar.cs wire unchanged. |
| B S15-B dialog adapter | New `web/src/components/hub/deadline-date-dialog.tsx` only after root approves extracting the current MoveDialog logic; generated types and canonical requests, with explicit nullable-old-due support. No new backend command engine. |
| B focused evidence sources | New `tests/Hub.Tests/Api/CalendarScreenTests.cs`, `CalendarRescheduleTests.cs`, `web/tests/calendar-screen.cjs`, `calendar-screen.fixture.cjs`. Synthetic fixture uses generated DTOs and finite routes/methods/bodies; provider tests own persistence/authority. |
| Root shared people/scope seam | Root decides helper extraction/reuse from Workload/Planning/PeopleScope and Calendar's existing source cohort, avoiding a second permission implementation or a broad Availability shortcut. Any edit to those held files needs an explicit root loan. |
| Root transport/integration seams | Program endpoint registration; ScreenLists/Views list validation and dropped parameters; generated contracts/golden shape; locale tables; cache keys/invalidation; package/fixture inventory; no schema/registry migration expected. |
| Root existing-dialog seams | Exact extraction/re-export of Timeline MoveDialog, with existing Timeline call shape/behavior preserved. Milestone dialog changes only if an explicit Calendar-safe reuse/default is approved. No full Timeline/Milestones rewrite. |

## Smallest meaningful verification after source freeze

**S15-A provider:** (1) off endpoint404 and legacy `/calendar` exact wire/cohorts unchanged; (2) active authorized full day and reduced4h produce exact date/person/hours without reason/import metadata; (3) supervisor direct report versus unrelated person, SystemPM live-project member, self-only viewer, unit leader/read-only and lost membership match actual Workload/Planner routes, with zero hidden names/counts; (4) holiday/zero baseline/nonworking/unknown/provisional/reconciled overlap cases do not become fabricated Away/free capacity; (5) finite range/ID/paging and current visible tray totals; (6) personal saved-view version/share/foreign-ID/refusal and off→on preservation/clear semantics. Reuse focused existing people/availability fixtures instead of building a second authorization model.

**S15-B provider:** (1) task dated shift with actual successor/new inconsistency; (2) deliverable with child beyond new due; (3) undated task/deliverable due set leaves start/assignee/status/other dates unchanged; (4) milestone dry run + cancel changes no business state, then exact versioned one-source commit/reason/audit; (5) real409, lost rights, closed/deleted source and due-before-start refusal leave source/children/side effects unchanged; (6) due-only versus start-edit rights, repeated pending/uncertain request and canonical retry outcomes; (7) DST/date-only invariance and preview no-business-write snapshots. Keep existing CalendarTests/TimelineTests/Milestone tests as adjacent focused regressions, not a default full-suite run.

**Actual-SPA browser:** real layer toggle/filter/view save/apply/clear/back/off→on; nonvacuous Away and Away4h with forbidden-person absence; actual dated drag opens exact current preview and requires reason, Cancel writes nothing, deliberate confirm sends one original-version canonical command; genuine409 retains target/reason/version; undated tray drop and keyboard/touch action; actual successor/child messages and current generic403/404 with stale actions hidden; natural panel/Person return and focus after a real cache refresh;375px and desktop screenshots only after those states are actually visible. No cache/history/focus injection, permissive business fallback, fake preview success or empty zero-only fixture.

Root runs only the smallest focused provider/browser set appropriate to the changed final state and records exact PASS/FAIL/UNPROVEN evidence; no S15 runtime check was run here.

## Decisions/dependencies to freeze before source authoring

1. **Filter semantics:** FR24 does not specify how a person or discipline maps to ownerless milestones and timed events. Recommend clearly labelled Time away person/**home discipline** filters in A, leaving existing project scope/deadline cohorts intact. If filters must govern every source type, root must define task assignee, deliverable owner, event owner and ownerless-milestone behavior explicitly before coding; never invent a PM/owner for a milestone. Until frozen this is an interpretation dependency, not a completed acceptance claim.
2. **Provisional/full-day absence:** PersonCapacity currently combines imported noncancelled and legacy unverified data. Freeze whether provisional days carry an explicit Unverified qualifier or are omitted from this layer; recommend matching current Planner presentation without importing private reasons. Unknown baseline stays explicit, not Full/zero. Numeric/calendar source truth must be tested before selecting the exact DTO enum/text.
3. **date_context independent flag:** do not auto-enable it. Recommend existing Timeline confirmation behavior when date_context is off, with no enhanced GET, while full AC-SCR-25 warning acceptance is exercised with it on. Root must record this combination and any stronger dependency rather than silently widening the old endpoint.
4. **Calendar milestone cascade:** recommend one dragged item only; keep cascade false for Calendar in B. If root includes the canonical optional cascade controls, widen the named acceptance scope and state its current child-version limitation explicitly.
5. **Calendar sharing:** recommend personal workspace views in A. Project sharing needs the exact scope/authority contract above before implementation; §18.4 cannot be satisfied by sharing another viewer's expanded private people scope.
6. **S12 Undo:** `work-delivery/requests/g53-s12-implementation-plan.md` owns versioned command receipts, per-row partial outcomes and append-only reversal provenance. Calendar FR24/25 is not permission to implement a competing10-second Undo engine or client-side inverse PATCH. Reuse S12 only after root supplies accepted supported-source receipt/reversal contracts; do not claim milestone/deliverable Undo from a Task-only contract. Single-source Calendar rescheduling can be scoped independently; any broader Undo acceptance remains an explicit S12 dependency.

Only this plan was written. Existing source, fixtures, runtimes, flags, screenshot hooks and checkpoint28 evidence were preserved. All proposed implementation and checks remain UNRUN.

## G57 authorized implementation integration notes (supersedes the planning-only boundary above)

Root subsequently froze `g57-calendar-freeze.md` and the exact11-path source loan. The preceding six planning decisions are now governed by that freeze: actual source filters, actual Planner cohort, both personal/shared views with explicit single-project sharing, due-only moves, no Calendar cascade and internal date-context facts independent of the old public flag. Implementation is source-authored under that loan; all execution remains root-owned. New public registration is `CalendarScreenEndpoints.Map(api)`.

### Exact root-owned locale proposal

These keys are consumed by the new source; root owns Text/i18n integration. No locale file was edited by B.

| Key | Exact text |
| --- | --- |
| calendarScreen.closed | This deadline is closed and cannot be moved. |
| calendarScreen.cannotMove | You cannot move this deadline. |
| calendarScreen.moveTitle | Move {key} |
| calendarScreen.onlyDue | Change this deadline only. Its start date and related work stay where they are. |
| calendarScreen.targetDate | New due date |
| calendarScreen.reason | Reason |
| calendarScreen.confirm | Confirm date |
| calendarScreen.saved | {key} deadline changed |
| calendarScreen.restartRequired | Your target date and reason are kept. Check the current record before starting another move. |
| calendarScreen.reloadRestart | Reload and start again |
| calendarScreen.childAfter | {key} would fall after this deadline ({date}). |
| calendarScreen.move | Move to date |
| calendarScreen.undated | Undated |
| calendarScreen.undatedCount | Undated ({count}) |
| calendarScreen.noUndated | No open undated tasks or deliverables in this scope. |
| calendarScreen.timeAway | Time away |
| calendarScreen.away | Away |
| calendarScreen.awayHours | Away {hours} h |
| calendarScreen.unavailable | Unavailable |
| calendarScreen.unverified | Unverified |
| calendarScreen.awayUnavailable | Time away is unavailable for this people scope. |
| calendarScreen.person | Person |
| calendarScreen.discipline | Discipline |
| calendarScreen.disciplineHelp | Work discipline; home discipline for time away. |
| calendarScreen.clear | Clear filters |
| calendarScreen.scopeHelp | Project scope filters work and visible events. Time away uses the people you may see in the Planner, then the selected person and home discipline. |

### Current source seams

- `CalendarEndpoints.ReadRange(from,to,projectIds,types,access,db,me,OrgSettings,explicitEmptyTypes=false)` is an internal pure extraction; the existing route retains its empty-means-all behavior, the new route passes explicitEmptyTypes=true.
- `DependencyEndpoints.ReadDateContext(type,id,startDate,dueDate,access,db,OrgSettings,clock)` is internal and pure. The old public wrapper still requires date_context; Calendar authorizes its exact current source and observed version before calling it.
- `MilestoneEndpoints.PreviewDateChange(db,Milestone,newDate,cascadeDeliverables,includeTasks)` returns `(DatePreview,List<Deliverable>,List<WorkTask>)`; the old change-date command keeps its original dry-run/version/reason/cascade/commit behavior. Calendar GET passes false/false.
- Root supplied and B consumed `PlanningEndpoints.CalendarPeople(db,access,OrgSettings)` -> `Task<IQueryable<AppUser>?>`. It returns null for an inactive actor; active viewers with current Workload/unit-leader eligibility get canonical widened Planning.People, otherwise the actual active self-only Planner cohort. This self branch is supported by the existing explicit-self Planner route, not a new fallback permission. A selected visible business owner outside that cohort still yields Away Unavailable and no absence rows.
- Root-owned Views/ScreenLists finite Calendar keys: mode/date/hide/projects/ws/showAway/personId/disciplineId. URL showAway is `true` or omitted; HTTP bool query may explicitly be false. Omitted anchor stays dynamicToday. Shared ViewMenu ProjectId comes only from one raw explicit projects GUID without ws. Current code uses generated new DTOs, not shadow wire records.

## G57 S15 authored-source handoff — READY, then HOLD

All11 loaned source/test files are now HOLD for independent root review/integration. Only those paths and this plan were edited. Checkpoint28 source, G56 driver/fixture/screenshots, other lanes' work, runtimes, flags and operational data were preserved. Source implementation is not compilation, executed verification, deployment or human acceptance.

### Final behavior and review boundaries

- Separate guarded `/calendar/screen` and GET date-preview; old Calendar DTO/endpoint semantics and off UI remain. Read-only internal Calendar range, dependency date-context and milestone preview extractions preserve old public guards and mutation commands. New Calendar previews work independently of date_context, contain current observed versions, use due-only targets and cannot cascade children.
- Repeatable-read business envelope, explicit-empty-versus-omitted types, source-specific person/global-work-discipline filters, actual Planner cohort/home-discipline away filter, typed Available/Unavailable/Hidden/Unknown distinctions. No private leave reason/HR/import fields. Full and reduced absence use actual recorded canonical leave hours, with unverified indication; holiday/nonworking/zero-baseline/overlap/reconciliation cases are authored.
- Undated metadata is batched across the full visible cohort for exact choices/count/filtering. Only selected page rows load canonical permission/Facts; no per-candidate permission hydration. Dated range rows still use current canonical source facts, without a new permission algorithm or silent result cap. A provider query-count regression grows one undated row to251 with pageSize1 and asserts unchanged command count and exact totals/choices; it is authored, not executed, and is not a latency or memory benchmark.
- New dialog retains original source version/date/reason on409 or uncertain commit; explicit reload obtains current Calendar data before closing and starting another intent. Preview errors withhold confirm. Current preview can require a stricter reason but cannot promote the source version. Canonical Task/Deliverable PATCH and no-cascade Milestone POST own audit, dates, versions, notices and outbox. No new inverse PATCH, Undo or receipt engine.
- Real native desktop date-cell drag and keyboard/touch date action use the same confirmation. Phone disables dragging while preserving the date action and ordinary event behavior. Source-specific DOM focus keys restore the exact trigger or the moved day/tray fallback after actual query refresh. Current Calendar read errors withhold cached additions. No test injects cache, focus or history.
- URL/view capture contains finite definitions only; Clear retains date/mode/workspace. Unknown/unavailable selected filter IDs get a neutral label and disable capture rather than resolve a hidden name. Off removes only addition-owned URL keys and withholds requests/UI; stored view definitions remain. Existing ViewMenu is reused; root owns its shared cache invalidation/partition behavior for identity/permission/flag changes, along with the backend Calendar view guards/ACL/drop cleaning.

### Authored acceptance sources — all UNRUN

**23 provider cases** (21 facts and one two-case theory) across the two owned test files: legacy/off shape; actual source filters; explicit empty types and exact tray pages; actual Supervisor cohort, nonempty full/reduced hours and no private metadata; zero/unknown/unverified/nonworking; overlap/reconciled legacy/office holiday; independent capacity/date_context flags; actual explicit-self Planner versus forbidden peer, Admin/global and unit/read-only-unit cohorts; personal/shared view ACL, invalid multi-project/private workspace definitions, global-discipline retention, CRUD/version/off-on preservation, current lost project visibility/drop-without-rewrite and invalid state; post-DST local event/instant versus date-only source; real successor/child warnings, due-only/undated source changes, unchanged starts/related rows, reason, stale409 and lost rights, no-cascade/no-write/keyed-GET milestone preview, unkeyed PATCH retry, completed read-only milestone and large-cohort query budget. Synthetic scale/closed fixture setup is labelled and confined to HubFactory disposable databases.

The strict generated-type actual-SPA driver authors nonempty away labels/unknown/unverified states, actual person/discipline filters and forbidden-peer-away distinction, clear/scope retention, actual desktop drag, reason/Cancel/no-write/exact focus, original-version409 and explicit reload, due-only and undated moves, read-only tray row, deliverable-child/milestone warnings/no cascade controls, real shared ViewMenu save/apply, stored off/on preservation, phone date action with no global overflow, optional natural desktop/phone screenshots and genuine15-second current404 refusal with old additions withheld. Finite route/method/source/body/query guards reject unexpected reads/commands; fixture dates/counts/relations reflect its actual selected source records.

### Frozen source hashes

| Path | SHA256 |
| --- | --- |
| src/Hub.Api/Features/CalendarScreen.cs | `0b79250755be06fc0cf73fa8cc101673dd332060716e43eb9f46725d11b293ed` |
| tests/Hub.Tests/Api/CalendarScreenTests.cs | `b2ad36ddaf67808c3bcdf9d38b57705413940b495115eeceac4a3d0508818bf7` |
| tests/Hub.Tests/Api/CalendarRescheduleTests.cs | `abca4c993ec1c2eef3a20ab8d9f0f449b607d50eefaeec601a8539b83aa72fae` |
| web/src/pages/calendar-screen-state.ts | `f3e97839730d02e5f88a29f2f1d22a5aeaf5f6110729de00878277750c2a0e7f` |
| web/src/components/hub/deadline-date-dialog.tsx | `ce25687eb4e4664c35b99faeff7b02aac1474410720b867d9e30adda094d9916` |
| web/tests/calendar-screen.cjs | `7a19f637a54ecd16eb48f95d519836526c23328d3c6e6e8cfe051e8e119df401` |
| web/tests/calendar-screen.fixture.cjs | `ea05dc2e75f3de25eb14e4d6929ba8900f41239a868ef41714ac03e23d13bc72` |
| web/src/pages/Calendar.tsx | `f5ab81e2b498731ce43954778defe600469ef0582e7b44a15348b00e6d334381` |
| src/Hub.Api/Features/Calendar.cs | `032850a2505c69479853e979fd8fb59144ea46c573287c6b6edc7ec365268542` |
| src/Hub.Api/Features/Dependencies.cs | `0d2ba9621f6f84e8431e8218bf5e59c6d3db5254d5315e2d985b502639621ea0` |
| src/Hub.Api/Features/Milestones.cs | `97ac2e887120f44315ae2dabcd3bc24f21ff0e4dd8f826b515f2d00ea3a713c6` |

Executed by B only source/spec/control/log reads, whitespace search, git diff --check and SHA256. Separate whitespace search covered untracked files; diff --check alone does not inspect them. No parser, type/lint/compiler/build/test/generator/EF/Chrome/service/helper/dependency/flag/commit/push/deploy/Accepted operation occurred.

Root next checks after all relevant authors HOLD: bounded independent source review; finish registration/people/view/locale/cache/fixture inventory seams; isolated API compile and focused `CalendarScreenTests|CalendarRescheduleTests`; adjacent existing Calendar/Timeline/Milestone/Dependencies and Calendar view shape checks as warranted; generated-contract/golden plain recheck; fixture/web type and lint; selected calendar-screen actual Chrome against an explicit isolated preview with current generated bundle and natural screenshots. Preserve prior runtimes and output directories. No source or packet acceptance checkbox was changed by B.
