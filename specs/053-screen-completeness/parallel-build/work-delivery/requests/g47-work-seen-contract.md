# S10 next-batch contract: activity coverage and persistent New state

CONTRACT READY — proposal only, 2026-11-01. Prepared under generation47's explicit documentation-only loan. This file is the only change. Product/test/schema/cache/metadata/locale/generated sources and all earlier Work/S13 sources remain held. No checks, runtime, build/test/browser/process, agent/helper, goal or status mutation was performed. Nothing below is implemented or verified runtime evidence.053-D20 remains Open.

## 1. Required behavior and the decision to preserve

`spec-parts/32-screen-completeness.md:180–184` requires Last activity/comments on My Work/workspace rows (FR15), and New text for items assigned to the viewer or on which they became reviewer after the previous My Work visit (FR16). Opening clears the individual marker; Mark all seen records now. AC15/16 at427–428 explicitly require Pat/2h/3comments and New until opened or Mark all seen. `specs/DECISIONS.md:240` records the same recommended definition as053-D20, Open. FR-INT19 (`spec-parts/25-task-and-timeline-interaction.md:59`) uses the newest exact task activity-log entry, including actual System actor, read once per page; sort is unchanged.

**Recommended interpretation:** a visit discovers/reconciles assignments but is not an acknowledgement. Once an assignment occurrence is New, repeated visits, reloads, filters, tab changes and background reads leave it New until that occurrence is opened or included in Mark all seen. Last visit stays a separate preference fact. Replacing the comparison floor on every visit would erase unopened markers on visit2 and contradict AC16. This is an explicit implementation assumption for root to record; it does not close the product decision.

Recommended first-use default: establish an acknowledged baseline of historical assignments, record the first real visible My Work visit, and produce no historical New flood. Only subsequent real assignment occurrences become New. This default is not specified in AC16 and must be settled by root before a schema/source loan. Never invent historical assignment times to implement it.

## 2. Current source evidence

| Maintained source | Observed fact | Contract consequence |
|---|---|---|
| `Data/Entities.cs:108–121` UserSetting | Home, onboarding, digest and density fields; no My Work visit, opened-item or all-seen state. | New persistence is needed. Preserve unrelated preferences and their independent writers. |
| `Infrastructure/UserPreferences.cs` | Conflict-safe INSERT then tracked row, specifically for racing unrelated saves. | Reuse the creation boundary; seen commands update only their own fields. Do not replace/deserialise a whole preference object. |
| `Data/Entities.cs:481–515`; `Features/Tasks.cs:533–536` | AssigneeId/ReviewerId exist. ReviewRequestedAt is review-stage timing, preserved through In Review and cleared outside review. No assignment-specific timestamp/epoch exists. | Review wait age, CreatedAt, UpdatedAt, StatusChangedAt and LastActivityAt cannot establish a new assignee/reviewer occurrence. S13 age facts are not a seen-state substitute. |
| `Records/WorkTask.cs:5`; `HubDb.BuildLog:1130–1199` | Actual AssigneeId/ReviewerId changes and creation values enter immutable activity. Changes contain field/old/new; action may be StatusChanged when a transition also chooses reviewer. | Capture the actual typed recipient change, not Action==Assigned, actor/recorder ID or latest activity timestamp. Same-value saves do not create an occurrence. |
| `HubDb.cs:443–452,1031–1038,1090–1129` | Indexed item activity by item type/ID/time; published log changes are refused; audited save builds log rows and saves them with business changes/outbox. | An additive recipient projection can be linked to that exact source activity; do not edit history. Transaction/lock integration still needs root source review, especially multiple saves per outer command. |
| `Data/Entities.cs:773–803` ActivityLog | UUID ID, OccurredAt and structured changes; nullable JSON-ignored ChainId/ChainSeq. | No proven global commit-ordered assignment cursor exists. UUID order, nullable chain sequence and Max(OccurredAt) alone do not solve late-commit or acknowledgement races. |
| `Tasks.cs:215–244,355–383,484–489,742–747,797–805` | Creation, normal assign/reviewer patches, transition-time reviewer choice, bulk direct assignment and shared AssignTo/pickup are real paths. | Instrumenting normal Patch alone misses required events. A closed typed assignment map in the audited-save boundary is preferable to duplicated endpoint hooks. |
| `MyWork.cs:58–87` | Own/person scope uses existing visible projects and person-work permissions; `userId` permits another person's scoped read. | Another-person mode must not read or write that person's private seen state. Current authenticated user is the only write subject. Preserve existing scope predicates. |
| `MyWork.cs:20–28,81–110,118–142` | Tasks include assignee/collaborator; reviews include Tasks and Deliverables; owned Deliverables, owner/requester Decisions, person/discipline Actions, project memberships/leads, upcoming Milestones and completed Tasks also appear. | A Task-only badge cannot close FR16/S10. Eligibility must come from an explicit actual person-assignment role, not merely appearing in a stream. |
| `web/src/pages/MyWork.tsx:64–67,138,193–206,359–376,423` | Overview and query-backed TaskView are separate readers (`mywork` vs `mywork-tasks`); task activity/comments already rendered. Watching/Requested are a third multi-kind component. | One server-owned per-item state must reconcile repeated rows/tabs. No browser-only Set, per-component visit floor or per-row seen GET. |
| `TaskQueries.Rows`; `projects/work-activity-comments.tsx:12–52`; `WorkspaceTasks.tsx:183–185,221` | Existing actor/count projection and exact Comments action are reused, including source URL/state/return and unavailable actor/time/count. | Retain these verified S10-A/S11 behaviors, existing sort and actual actor facts. Seen writes must not alter Comments navigation or business commands. |
| `MyWorkFollowUp.cs:10–13,32–69`; `components/hub/my-work-follow-up.tsx` | FollowUpItem has ID/type/title/project/status/date/owner only. Watching/Requested rows span several kinds, without activity/comment/seen facts or cells. | This remains an FR15 census/build gap. UpdatedAt/current owner is not a replacement for actual activity/assignment evidence. |
| `Notifications.cs:18–33,94–97,187,233`; Entities Notification/ProjectFollow | Notification.ReadAt and ProjectFollow.LastSeenAt implement other concepts. | Do not repurpose bell, Inbox, Following or notification delivery/aggregation for New. Channel settings/muting must not change assignment markers. |
| `components/hub/panel-host.tsx:29–47` | useItemPanel sets route state; it does not prove the requested detail loaded. | Do not acknowledge in the click helper or an HTTP GET. Acknowledge exact successful visible detail, including deep links and Comments opens. |
| `web/src/lib/keys.ts:10–20,58–78` | Existing task/business events refresh mywork, mywork-tasks and related roots. There is no seen command or personal seen-state key. | New acknowledgement commands need an explicit personal invalidation contract; do not fabricate a Task business change to force refresh. |

No assignment-history retention/backfill completeness, per-user commit ordering, new indexes, producer transaction coverage or scale has been proved by this read-only pass. A few existing source files were read at bounded ranges; this is not a repository-wide role/command audit.

## 3. Recommended durable state: assignment occurrence, then acknowledgement

Use existing PostgreSQL and root-owned additive schema, not a new service. Proposed names below are concrete design names, not claims that these entities exist.

**UserSetting additions:** MyWorkFirstVisitedAt, MyWorkPreviousVisitedAt, MyWorkLastVisitedAt, MyWorkLastMarkAllSeenAt, and a private monotonic personal revision. A nullable first-visit value means unenrolled. All times come from one server clock per command; revision/events, not time alone, decide acknowledgement order. A retry does not turn one visit into two visits or move PreviousVisitedAt again.

**WorkAttention row**, unique `(UserId, ItemType, ItemId, AssignmentRole)`:

- ProjectId for efficient filtering, always re-authorised against the canonical record.
- Current assignment occurrence token, exact SourceActivityId when available, SourceField and SourceOccurredAt when evidenced.
- AcknowledgedOccurrenceToken and AcknowledgedAt; a nullable/different token means pending.
- Explicit baseline provenance for old records whose assignment event cannot be established. No fabricated SourceOccurredAt and no attribution to a convenient latest actor.

The token is an opaque occurrence identity, not a row version/modified time/order assumption. Each real null/other-person→viewer role change creates a distinct occurrence. Away→back creates a fresh one, even at the same timestamp. One source activity can yield several typed role occurrences; recipient, kind, ID and role disambiguate them. Same-value saves, status-only transitions, review requests with unchanged reviewer, comments, watches and pings do not create an assignment occurrence.

Capture both normal creation assignment and actual changed recipients once, in the same transaction as their source activity/business change. Failed/refused/stale commands create zero attention changes. Retried source activity must not create a second epoch or overwrite an acknowledged matching epoch. Role removal makes the old occurrence ineligible through the canonical current-recipient predicate; historical personal state may be retained, with no item/business log rewrite.

New for a visible item is `any currently eligible viewer-role occurrence whose token != acknowledged token`. Multiple roles/duplicate sections still render one New marker for that item. Having no audited evidence must not be silently represented as Seen: baseline records are explicitly baseline; a genuinely inconsistent projection is Unavailable and is a build/recovery gap.

**Source producer choice:** prefer a closed typed role adapter in the audited save, linked to the exact just-built ActivityLog row, rather than re-reading the first100 history entries or scattering timestamp writes across commands. Role adapters must inspect typed GUID/current role discriminators, not generic name matching or every field ending in OwnerId. Root owns the precise integration and regression review; this proposal does not grant a HubDb edit.

## 4. Transaction and concurrency contract

Personal visit/acknowledgement and assignment publication need one consistent per-user serialization boundary. Recommended implementation uses a small dedicated lock/state row per user (or an equivalent proven lock on the preference row), not a Task/Project row version. Producers and personal commands participate in the same boundary.

- A producer publishes occurrence changes atomically with its normal business transaction. A visit/Mark-all cannot pass a producer and then acknowledge an occurrence that commits after its own cut.
- First visit and a racing assignment serialize: an assignment published before enrollment belongs to the acknowledged historical baseline; one published after enrollment remains New. A transaction that started earlier but commits later must not disappear because its OccurredAt precedes the visit time.
- Multi-recipient source commands acquire all affected user-state locks in deterministic user-ID order. Root must inspect whole outer transactions, including multiple SaveChanges calls, before selecting the lock placement. Sorting one inner save alone is not a deadlock proof.
- Personal commands lock only personal state and read authorisation/canonical items without acquiring canonical project/task mutation locks. Avoid reverse lock order with assignment commands. No canonical status/version/notification/history changes result from personal acknowledgement.
- Acknowledgement is conditional on the exact observed occurrence token. An old panel acknowledgement cannot acknowledge a replacement occurrence. CAS/upsert is idempotent, and older tab responses cannot replace newer personal revisions.
- Prefer READ COMMITTED with the lock acquired before the authorised cut/read, or another explicitly proved transaction strategy. Reusing a pre-lock RepeatableRead snapshot can miss a source commit it waited for. Root must prove, not assume, this integration.

These are acceptance constraints, not a claim that new locking is trivial. If root chooses audit replay instead, it must supply a commit/visibility-safe cursor plus durable per-occurrence acknowledgement with the same race guarantees; Max(time), GUID ordering or nullable audit ChainSeq alone are insufficient. No global cutoff should skip an in-flight transaction simply because it allocated an older sequence/time.

## 5. Exact read and command contracts

### Visit — `POST /me/work/visits`

Body `{requestId}`. No target user, project filters, selected tab or client time. Called only after authenticated own My Work successfully renders visible content; one logical navigation/return visit has one request ID. Background/refetch/filter/page changes reuse that visit or do not issue another. A failed load/prefetch/hidden tab/another-person view creates zero visits. Mount effects in several child views must not independently count visits.

Return `{visitId, previousVisitedAt, visitedAt, personalRevision}`. Store previous/last visit with preferences; first successful call establishes enrollment/baseline. Later visits change only visit metadata/reconciliation, never acknowledge pending items. A duplicate request returns its original result and does not advance previous/last timestamps again. A reload can be a new visit; it still clears zero markers.

### Own attention read — `GET /me/work/attention?items=…` or a batched response decorator

Preferred integration is an optional batched decorator on the existing own Work/TaskView/FollowUp response, so one page read provides all required facts and no row GETs are added. Exact route name is secondary to that batching rule. For each visible typed item return `state: New | Seen | NotApplicable | Unavailable` plus an opaque observed-assignments token for the viewer's eligible roles. Omit the entire personal decoration in another-person mode and when work_screens is off. Cache scope includes authenticated viewer and personal revision; never cache one user's markers as another's.

Every GET/detail/prefetch remains read-only: zero GetOrCreate insert, visit advance, seen row, baseline enrollment or canonical evaluation writes. Before enrollment, enabled UI may read an explicit Unenrolled top-level state and issue the visit command after a successful visible render; it must not treat an arbitrary missing row as a visit.

### Successful exact open — `POST /me/work/opened`

Body `{requestId,itemType,itemId,observedToken}`. The token is server-validated, bound to viewer/kind/ID and the exact eligible assignment occurrences returned with the successfully loaded authorised detail. No user ID/client timestamp/Mark-all flag. Detail GET itself is not the acknowledgement.

The UI sends it only after that exact detail is loaded and actually visible, including a deep link, Comments-targeted open, normal panel, or the record's own detail page. Opening a parent/project register is not opening a selected child. A failed404/403, hover, cancelled load or prefetched detail acknowledges zero items. Opening an eligible item from a project page also clears its own My Work marker; this is not restricted to clicking from My Work.

Conditionally acknowledge only captured occurrences. If reassignment/reviewer change occurs after the read, the new token stays pending. A stale request can harmlessly acknowledge its old occurrence but must return the current own state/revision without clearing a replacement event. No optimistic permanent removal before success; expose retry on failure, keep the pending marker and preserve URL, section, scroll and return state. Multiple role occurrences actually observed for the same item may be acknowledged together; unrelated items are untouched.

Return `{itemType,itemId,state,personalRevision}` with no other person's state. Re-check visibility/role relationship at write time; unavailable/hidden/invalid typed record is not a personal-state escape hatch. No task edit permission is required to acknowledge one's visible item, and no acknowledgement grants a business command.

### Mark all — `POST /me/work/seen-all`

Body `{requestId}` only. Under the personal serialization boundary, select all currently visible **own My Work-eligible** assignment occurrences across tabs/projects and current supported history horizon, independent of browser search/page/scope chips. Acknowledge the exact selected tokens and store MyWorkLastMarkAllSeenAt=server now atomically. Hidden/revoked-project occurrences are not read out or acknowledged through a caller-supplied list; they remain inaccessible/pending until legitimately visible.

Return `{seenAt,acknowledgedVisibleItemCount,personalRevision}`; count distinct visible items, not roles/hidden records. Repeated request IDs return the same operation result. An assignment published after this command's cut remains New. Older Mark-all/open/visit responses never rewind state or override a newer occurrence. Mark all is not a business bulk edit and sends no assignment/review notifications.

## 6. Role and rendered-stream census

Use this closed initial map, then verify every matching producer/read/detail route before claiming S10 complete:

| Kind/stream | Concrete proposed eligible role | Exclusions / remaining evidence |
|---|---|---|
| Task, including completed/query-backed/attention rows | AssigneeId, ReviewerId (`Records/WorkTask.cs:5`) | Creation, patches, bulk, pickup and transition reviewer choice all covered. Collaborator/creator/watcher alone is not assignee/reviewer. |
| Deliverable ownership/reviews | OwnerId, ReviewerId (`Records/Deliverable.cs:5`) | Retain reviewer-independent commands/permissions; review-stage changes with unchanged reviewer are not new assignment. |
| Decision owner/requester | OwnerUserId (`Records/Decision.cs:5`) | Requester/external-party owner alone is not a person assignment. |
| Action | OwnerUserId only while OwnerType is User (`Records/MeetingAction.cs:5`) | Discipline-owned actions shown because of a lead/PM role require a separate explicit policy; becoming lead is not an item reassignment. Do not substitute the effective actor from readiness. |
| Watching Risk/Issue/ChangeNotice | Actual person OwnerId (`Records/Risk.cs:5`, Issue.cs:5, ChangeNotice.cs:5) | Only a real AppUser matching the viewer, not an external owner; presence in Watching/Requested alone creates no New event. |
| Watching/Requested Handoff | Explicit SendingOwnerId / ReceivingOwnerId (`Records/Handoff.cs:5`) | Root must confirm both assigned-owner roles versus current workflow responsibility. Recommendation: actual explicit owner change qualifies; stage/actor change alone does not. Preserve canonical single-target Handoff. |
| Project rows | Direct ProjectManagerId is a candidate explicit assignment | Root must verify the Project audit field and intended FR16 scope before enabling. Member/discipline-lead affiliation alone does not assign every project item. Opening uses a successful authorised project page, not the generic panel helper. |
| Milestone / OpenRole | No person assignee/reviewer in inspected registry fields (`Records/Milestone.cs:5`, OpenRole.cs:5) | Recommend NotApplicable, not fabricated lead/PM ownership or a synthetic person for an open role. |
| Other heterogeneous Attention rows | Map actual stored person roles per kind | Census is not complete in this pass. Do not silently suppress qualifying assignment markers or claim complete coverage from Task tests. |

Root must resolve the explicit Action-discipline/Handoff/Project/Attention boundaries and record them as implementation assumptions/open decisions. Task-first is an acceptable bounded implementation batch only with FR16/S10 still partial; it is not permission to redefine the full requirement.

FR15 also still needs a full rendered-row census: generic Watching/Requested lacks all three facts; non-Task Overview rows/attention/project/milestone rows have not been proved complete. For each supported discussion kind, batch latest exact-item activity (OccurredAt then stable ID) and live comment count under existing privacy/access rules. Existing TaskLastActivity/TaskCommentCount remains the reference behavior. System is only an actual system-authored activity. Missing actor/count is unavailable, not guessed System/zero; unsupported discussion capability is explicit NotApplicable. Reuse exact typed panel/detail navigation and Comments section where actually supported. Do not route every kind to a Task panel or fetch100 histories per closed row.

## 7. Flag, migration and recovery choices

Public off behavior: omit new response properties, suppress New/Mark-all UI, personal commands unavailable/flag-refused, no new personal GETs/enrollment/acknowledgement writes and existing activity/comments/notification/follow semantics unchanged. Flags are not activated by this work; prior persisted personal state is retained, not erased.

For reliable off/on history, **recommended internal producer is durable after migration regardless of the presentation flag**, so real assignment occurrences are never lost while the UI is dark. Before enrollment they are baseline-acknowledged; after enrollment pending assignments survive the UI flag being off. This adds a bounded internal projection write to actual assignment changes even when presentation is off. That is a deliberate new storage/transaction cost, not existing off write parity. Root must accept this producer boundary before opening the loan. If strict zero additional off-command work is required, root needs a proven audit catch-up strategy and acceptance for late commits/off-period reassignments; silently rebaselining an enrolled user's pending items is not acceptable.

Root-owned additive migration creates personal state and unique lookup/idempotency indexes; initial historical current-role rows are acknowledged baseline with truthful provenance and nullable unknown source times. Prefer typed set-based backfill; do not call business assignment commands, alter canonical rows/versions/history or send notifications. Missing historical audit does not become a fresh assignment. Backfill coverage, recipient producer parity, retention/cleanup rules, SQL plans and locking under multi-save transactions remain UNPROVEN until implemented/reviewed. No new public assignments API or replica is implied.

## 8. Narrow next implementation ownership

Root first settles first use, role census and durable-off producer/transaction strategy. Root owns schema/migration/HubDb/audit role capture, UserPreferences additions, new own-user commands/decorator, auth/registry/wire descriptions, all kind-specific detail tokens/loaded lifecycle seams, shared panel host, locales/cache/live-event routing/generated contracts and evidence ledgers. One writer per shared file, no concurrent producer/command edits.

B can own MyWork.tsx, a tiny New marker/Mark-all helper, MyWorkFollowUp UI, and focused new API/browser acceptance sources under an explicit loan. WorkspaceTasks changes, if needed for FR15 generic reuse, must be named separately and preserve S11 chooser/date/group/live/view behavior. New badges are text, repeated item instances reconcile through one server state, and neither collaborator contribution markers nor S12 Undo/bulk/Timeline/Calendar are bundled here. S13 source remains held.

Seen commands use a new own-person revision/invalidation root, with both Overview and query-backed MyWork tabs plus FollowUp refreshed after successful acknowledgement. Other devices need an authenticated user-scoped event or an explicitly documented refresh path. Do not broadcast hidden IDs/roles/seen status to project subscribers or manufacture a Task business event/notification. Existing known task/role changes must invalidate seen decoration consistently; polling/pulse alone is not proof of cross-device behavior.

## 9. Concrete acceptance before S10 closure

1. First visible visit09:00 establishes an acknowledged baseline of old assigned C. Normal TaskA assignment09:05 and TaskB reviewer change09:06 yield exactly2 distinct New items on visit09:10. A repeated in2 sections still counts once. C receives09:07 comment only and remains Seen. Failed/hidden/prefetch/other-person GETs write0 visit/attention rows.
2. Opening A after its exact successful read clears A everywhere, leaves B New, and changes0 task/review versions/state/activity/notifications. Reload/next visit leaves unopened B New. Mark all at09:12 clears all visible eligible markers regardless of current page/filter/tab; D assigned09:13 remains New.
3. Assignment away→back produces a fresh token; same-value save/status-only review request/comment produces none. Assignee and reviewer changes in one StatusChanged activity both work. Normal create, bulk, pickup and transition-time reviewer choice all produce exact source-linked occurrences, never a guessed actor time.
4. Race initial enrollment versus uncommitted assignment; late-commit timestamp before visit; same-time events; open read versus replacement assignment; Mark-all versus producer; reversed tabs/devices and retried request IDs. No lost assignment, new-event over-acknowledgement, clock-order dependency or preference overwrite; root demonstrates deterministic locking/retry behavior.
5. Manager viewing Alex never sees Alex's private marker/read time or advances it. Restricted project removal suppresses facts/counts/actions; hidden events cannot be inferred from response counts or opaque personal revisions. Role removals, inactive/missing references and later access return are explicitly tested without inventing assignments.
6. All agreed eligible non-Task roles receive normal-command tests and successful exact-open paths. New affiliation/watch/request/discipline lead alone does not create a false event. Unresolved census cases remain an open build gap, not silently Seen.
7. Off/on with existing pending B, an off-period reassignment and cold browser storage preserves the accepted producer policy and old public off shape. Ordinary density/digest/Home saves racing visits/opens retain every unrelated preference. No browser clock/localStorage seen authority.
8. FR15 Pat/2h/3comments in every required stream, including generic follow-up and non-Task cells; zero closed-row detail/history/comment HTTP requests. Exact Comments section/return survives scopes/filter/sort/group/cols/page at375/768/1280 and keyboard use. Verify the final agreed kind census, not just a Task screenshot.

No app/tests/schema/cache/status/ledger changes are authorised by this document. CONTRACT READY; all product sources remain held for coordinator review and the next exact implementation loan.
