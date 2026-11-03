# G51 build preparation: S10 activity and personal seen state

READY — documentation only, 2026-11-01. This is B's only new file under this loan. No app, schema, test, metadata, cache, generated, earlier QA, service or evidence-receipt file was changed. No runtime/API/browser/build/migration/generator/helper ran. Root owns checkpoint22 closure evidence and the preserved Timeline retry. Its outcomes are not S10 evidence.

This plan adopts work-delivery/requests/g50-seen-architecture-review.md and root's subsequent independent-review corrections. It supersedes g47's mutable WorkAttention sketch with append-only assignment occurrences and insert-only acknowledgements. It neither closes 053-D20 nor changes canonical §10, source statuses or workflow. Preserve root's confirmed utility-navigation choice; this slice makes no navigation edits.

## 1. Requirements and build defaults

Full requirement sources: spec-parts/32-screen-completeness.md:180 (FR-SCR-15), :182 (FR-SCR-16), :428 (AC-SCR-15), :429 (AC-SCR-16). FR-INT-19 is spec-parts/25-task-and-timeline-interaction.md:59. 053-D20 remains Open at specs/DECISIONS.md:240.

- FR15: My Work and workspace Task rows show Last activity with actual actor and live comment counts opening the exact item's Comments, read once per page. Pat's actual event2h ago and3 live comments is the acceptance example.
- FR16: textual New marks assignment/reviewer changes after the person's previous visit. Opening clears that exact occurrence; Mark all seen records now and clears its cut.
- Visits discover work; later visits do not acknowledge unopened New occurrences. First successful visible own visit acknowledges a token-only historical snapshot. No fabricated historical assignment time or hidden-item flood.
- Multiple appearances/roles share one per-item marker if any current eligible role token remains unacknowledged. Other-person reads expose no subject's private tokens, marker, times or personal revision.
- Public wire/commands/UI are work_screens-owned. Real internal assignment publication continues after migration while the flag is off. Acknowledging work changes no canonical row, version, history, derived state or notification.

## 2. Every current My Work stream

This is a source census, not a built feature claim. Preserve duplicates, scopes and existing workflow/status rules.

| Stream / reader | Current membership and evidence | Assignment and FR15 treatment |
| --- | --- | --- |
| Own Today focus / Overview tasks | GET /me/work; non-Complete/non-Cancelled Task, assignee OR collaborator; Features/MyWork.cs:81–85; UI pages/MyWork.tsx:190–206, :218–238 | Assignee qualifies; collaboration alone does not. Same Task state reused across buckets. Current Task activity/comments rendering remains covered. |
| Reviews | Task reviewer, Ready for Review/In Review; Deliverable reviewer, In Review; MyWork.cs:86–88 | Both actual reviewer roles qualify. Stage-only change with same reviewer creates0 occurrences. Deliverable row currently lacks activity/comments at MyWork.tsx:209–214. |
| Owned deliverables | Deliverable owner, excluding Issued/Accepted/Cancelled; MyWork.cs:89–90 | Owner qualifies; same item if also reviewer/follow-up. |
| Waiting / Blocking | Own/collaborated Task rows plus TaskState/people/successor labels; MyWork.cs:92–99; MyWork.tsx:240–245 | Same Task marker. Dependency routing and referenced successor/blocker text create no assignment. No per-label detail fetch. |
| Decisions | Open Pending/Under Review/Deferred; OwnerUserId OR RequestedById; MyWork.cs:101–105; MyWork.tsx:246–254 | Internal owner qualifies. Requester alone does not. External-party owner never maps to User from a GUID/name. Missing FR15 cells remain required. |
| Actions | Open/In Progress; person owner OR discipline-led/PM-without-lead fallback; MyWork.cs:107–111; MyWork.tsx:255–264 | Only OwnerType User plus OwnerUserId qualifies. Discipline/external fallback or a stale UserId under another discriminator does not. FR15 still applies to rendered rows. |
| Projects | Managed/member/followed visible project, roles/leads/follow; MyWork.cs:113–121; MyWork.tsx:265–274 | Recommend direct canonical ProjectManagerId only as personal assignment. ProjectMember PM role, other memberships, discipline leads and Follow are affiliations, not new assignments. |
| Upcoming milestones | Next30 days under those projects; MyWork.cs:123–125; MyWork.tsx:275–279 | No person assignment/reviewer field; no New. FR15 exact Milestone activity and supported comments still apply. |
| Completed | Own-assignee Complete Task completed within14 days; MyWork.cs:126–128; MyWork.tsx:280 | Existing role token; completion/reopen creates none. First-use baseline covers older/closed work too. |
| Attention | RouteToUserIds in permitted projects, display max100; MyWork.cs:129; Features/Evaluation.cs:18; MyWork.tsx:230–233 | Routing is not assignment evidence. Concrete canonical targets use actual current roles; rule/aggregate labels are not commentable items. Never limit acknowledgement to displayed100. |
| Query TaskView | GET /tasks; Upcoming/Overdue/Completed/No due date and Today assigned/created; mine is assignee/collaborator, explicit assigneeId or createdBy; MyWork.tsx:359–375, :413–423; TaskQueries.cs:193 | CreatedBy alone is not assignment. Preserve scope/date/group/page/sort. Workspace task list uses the same FR15 facts, without personal My Work enrollment. |
| Watching | Independent my_work_follow_up flag. Task, Deliverable, Milestone, Risk, Issue, Action, Decision with Manual/Mention watch, open-kind predicates; MyWorkFollowUp.cs:21–53 | Watch alone is not assignment. An actual own role may qualify. All seven kinds need FR15 cells; current FollowUpItem has none (:12), UI has none (components/hub/my-work-follow-up.tsx:16–19). |
| Requested | Decision.RequestedById; Handoff/ChangeNotice/OpenRole.CreatedBy; open or closed within14 days; MyWorkFollowUp.cs:52–65 | Requester/creator alone is not assignment. Handoff actual sender/receiver and ChangeNotice owner may qualify. OpenRole.CreatedBy does not. |
| Inbox | Unread Notifications and existing mark-read; MyWork.tsx:443–456 | Notification.ReadAt stays independent. No assignment acknowledgement on marking a notification read. Only its successfully displayed canonical destination can acknowledge an exact token. |
| Coordination tab | Shared WorkspaceCoordination, not an own-person reader; MyWork.tsx:68; WorkspaceCoordination.tsx:70–88, :162–192 | Owner/scope filter is not assignment evidence. Concrete Task/Deliverable/Handoff/ChangeNotice targets can reuse actual typed roles. Aggregate, assessment, readiness and input-use context do not acquire new roles or discussion capabilities merely by appearing under My Work. |
| Other-person view | GET /me/work?userId; current visible/person-work and PM-managed fallback; MyWork.cs:58–79, :131; UI MyWork.tsx:66 | Preserve existing scope/read-only behavior. No subject's private seen metadata or enrollment/ack writes. FR15 only for already authorized rows. |

Owner-kind seam shared with D: MyWorkFollowUp.cs:53 emits OwnerUserId OR OwnerExternalPartyId into the same OwnerId; :68–71 resolves user/external names into the same display dictionary. This is polymorphic presentation, not assignment authority. Root adds an explicit flag-owned OwnerKind (User/ExternalParty/Discipline/None, from the canonical discriminator) beside OwnerId. Preserve the existing OwnerId value/meaning rather than replacing it with a discipline ID. Recommended wire guard is either actual consuming flag people_screens OR work_screens, omitted when both are off; root reconciles the exact name/guard with D before integration. Canonical assignment logic still uses underlying typed fields even when OwnerKind is omitted. Person triggers additionally require people_screens, OwnerKind User and an actual user ID; no inference from name, Users lookup fallback, or coincident GUID. FR15's activity actor is independently typed from the actual activity event, never inferred from this owner. OpenRole's creator may be a User display actor without becoming an assignment.

## 3. Closed producer map: nine types, twelve roles

The initial six-role core is retained; the architecture-review census adds Risk/Issue, direct PM and coordination owners below. Root explicitly adopted the twelve-role map, including direct canonical ProjectManager and both Handoff owners, as dark implementation defaults, not D20 closure. It adds no new list or business role.

| Canonical role | Authoritative predicate | Audit and producer evidence |
| --- | --- | --- |
| Task Assignee / Reviewer | AssigneeId / ReviewerId | Records/WorkTask.cs:5; Tasks.cs:229–244 create, :355–383 patch, :484–489 transition reviewer, :742–747 bulk, :797–805 AssignTo; TaskPickup.cs:117, :146 reuse AssignTo. |
| Deliverable Owner / Reviewer | OwnerId / ReviewerId | Records/Deliverable.cs:5; Deliverables.cs:215–221 create, :374–375 patch, :575 bulk owner. |
| Decision InternalOwner | OwnerUserId with no external owner | Records/Decision.cs:5; Decisions.cs:185–188 create, :327–328 owner discriminator edit. RequestedBy/DecidedBy are not assignments. Invalid dual ownership fails, never guessed. |
| Action InternalOwner | OwnerType User AND OwnerUserId | Records/MeetingAction.cs:5; Meetings.cs:275–278 create, :342–346 tuple edit. User→Discipline→User reactivation is new even if a stale ID did not change. |
| Risk Owner | OwnerId | Records/Risk.cs:5; Registers.cs:235–237 create, :288 patch; Templates.cs:684 template risk. |
| Issue Owner | OwnerId | Records/Issue.cs:5; Registers.cs:453–475 NewIssue, :611 owner patch; Risk Realised calls NewIssue at :312–315. ReferenceImpact.OwnerId at :601 is another kind. |
| Handoff SendingOwner / ReceivingOwner | SendingOwnerId / ReceivingOwnerId | Records/Handoff.cs:5; Handoffs.cs:145 draft tuple, :156–157 create, :284–286 assign. Revision snapshots copy evidence, not new assignments. |
| ChangeNotice Owner | OwnerId | Records/ChangeNotice.cs:5; Changes.cs:178 canonical create. SourceHead/ChangeAssessment/IssueReferenceImpact owner copies at :175, :191–192, :210, :298 are not notice assignments. |
| Project ProjectManager | ProjectManagerId | Records/Project.cs:5; Projects.cs:286–291 create; Team.cs:147–158 PM change; ProjectImport.cs:30–31 create. ProjectMember.Roles and LeadUserId remain affiliations. |

Additional canonical producers and exclusions that endpoint hooks would miss:

- Templates.cs:593–616 materializes Deliverable owner/Task assignee. Meetings.cs:389–399 converts Action through NewTask; its Task assignment is new, Action status-following is not.
- Extras.cs:198–216 supervisor reassignment covers Task assignee/reviewer, Deliverable owner/reviewer, Decision owner. Team.cs:87–102 member removal moves Task/Deliverable owners. Capture the actual entity even when the HTTP route names a person/member.
- Extras.cs:60–83 project copy creates unassigned Deliverables/Tasks; do not inherit recipients or emit assignments. Template application publishes only actual materialized recipients.
- Seeds/ReviewDemo are fixture paths, not historical-time evidence. Legacy migration is BaselineSeed; fresh post-migration creates use normal publication.
- Current selected paths expose no raw ExecuteUpdate/SQL recipient writer bypass; Keys.cs:30 reserves sequences, not assignments. Re-run a full assignment-field/tuple/helper/raw-SQL census before source freeze; any newly found bypass must join atomic publication.
- Exclude Notification delivery, Follow.LastSeenAt, Watcher.Source, collaborator, requester/creator, membership/lead/OpenRole, assessment/review/check/readiness/source snapshot owners from this closed map. Their workflows remain independent.

## 4. Minimum root entity, EF, audit and module seams

Root retains MyWork.cs, shared DTOs, audit/model and the owner-kind bridge. Minimum shared work to unblock isolated helpers:

1. New private WorkAssignmentOccurrence: token GUID/PK; recipient; typed item/project/closed role; canonical SourceVersion; optional exact SourceActivityId/SourceOccurredAt; Published/BaselineSeed provenance; recorded-at storage time distinct from unknown assignment time. Unique item/type/role/sourceVersion, partial unique sourceActivityId/role for published rows. Index typed item/role/version-desc and recipient/token access.
2. New insert-only WorkSeenAcknowledgement: recipient+exact token unique, server time, InitialBaseline/Opened/MarkAll provenance, optional command request ID. FK binds the same recipient/token, not merely any opaque GUID.
3. New insert-only WorkSeenCommandReceipt: user+requestId unique, action, canonical payload hash, committed bounded response/time. No generic24h expiry/cleanup in this slice; root records retention separately. No hidden IDs/totals in baseline/MarkAll responses.
4. UserSetting additions: nullable MyWorkFirstVisitAt/MyWorkPreviousVisitAt/MyWorkLastVisitAt/MyWorkLastMarkAllAt and MyWorkSeenRevision default0. Private revision is NOT an EF concurrency token and UserSetting is NOT IVersioned; only personal commands increment it. Preserve independent density/digest/Home/onboarding fields and valid existing insert defaults (Entities.cs:110–123; UserPreferences.cs:8–12).
5. New private model file, DbSets, EF configuration/FKs/indexes, additive migration/snapshot; seams HubDb.cs:33, :218. New private rows are not Audited/IAuditable or business EntityRegistry entries (Records/Registry.cs:18–27; RegistryTests.cs:63). Extend published-evidence immutability guard at HubDb.cs:1059, :1122–1126.
6. Closed typed publication helper, called beside actual BuildLog at HubDb.cs:1134–1137, after canonical version at :1127–1128 and before base save :1149. Capture source event/version/recipient together; never query a guessed latest activity afterwards.
7. Own-user preference-row serialization, atomic-command middleware bypass (Idempotency.cs:30–33), module registration (Modules.cs:54–55), narrow active-self permission helper, root row/detail nullable envelopes and explicit owner kind.

Publication algorithm:

- Compare actual original/current typed role recipients including discriminators and creation. Same value, comment, review-stage-only, visibility/reopen/lead changes publish none. Removal emits no new recipient token; current-role reads suppress former recipients. Away/back emits a fresh token.
- BuildLog may classify a combined reviewer/status change as StatusChanged, not Assigned (HubDb.cs:1182–1201). Bind the actual built ActivityLog ID plus entity version; do not branch only on activity Action. A qualifying change missing expected source evidence fails its canonical transaction.
- Root forces/joins the existing transaction for all mapped producers, including reviewer-only and non-Task types. Tx.Run joins an outer transaction (Infrastructure/Keys.cs:43–52). Existing special authorship locking at HubDb.cs:1084–1101 is not complete producer coverage.
- Preserve lifecycle/detect changes at HubDb.cs:1102–1106 and normal sorted project/task locks at :1074–1083. Add occurrences before the same base save; no post-commit second save.
- Producer never locks or updates personal preference/ack/receipt state. Personal commands never lock canonical source rows. Preserve canonical conflict/deadlock refusals and atomic rollback; reverse multi-item transactions must not acquire new private lock cycles or leave partial projection.
- Private rows produce no business activity, notification, evaluation event or audit-chain entry. Publication is internal while presentation is off; public off shape remains unchanged.

Migration: root documents quiescent/locked cutover so old writers cannot mutate roles without publication. Seed current legacy mapped roles across hidden/closed/soft-deleted/archived rows with real current source version, BaselineSeed and unknown source time/activity null. No canonical version changes, fake ActivityLog, assignment endpoints or notifications. Do not lazily baseline missing post-cutover projections in GET/open.

Current-token lookup is version-bounded, NOT equality: highest role occurrence SourceVersion <= the canonical entity version actually read, also matching that read's current recipient/discriminator and viewer. Non-assignment edits advance RowVersion without a new assignment. Away/back resolves the later occurrence. No timestamp/UUID/chain/last-visit/personal-revision ordering. A qualifying role without its projection is integrity Unavailable, never silently Seen; publication parity must be tested.

## 5. Personal commands and atomic replay

New isolated feature MyWorkSeen.cs; strict bodies reject unknown fields, userId override, empty request ID, unsupported typed kinds/roles and invalid token sets:

| Own command | Body and boundary |
| --- | --- |
| POST /me/work/visits | requestId. First successful visible own visit token-baselines committed recipient history; subsequent visits update first/previous/last facts only, not pending acks. |
| POST /me/work/opened | requestId, itemType, itemId, observedTokens[]. Only exact successfully displayed detail tokens; current item authority and recipient/role binding rechecked. Never select and acknowledge replacement tokens after the detail read. |
| POST /me/work/seen-all | requestId. One authorized current eligible cohort snapshot independent of UI tab/filter/scope/group/page; exact committed cut replays without reselecting. |

Root has settled ONE reusable typed SQL/predicate contract for decoration AND the single-statement MarkAll. It is the union of the actual unfiltered streams below, intersected with current canonical direct mapped role and current read authority. Ignore UI scope/date/group/page filters, not source membership/status/independent gates. Do not substitute all readable nonTask responsibilities.

| Shared eligible-item arm | Frozen predicate before direct-role intersection |
| --- | --- |
| Own open Task | Base MyWork authorized project scope; Task not Complete/Cancelled; AssigneeId viewer OR actual Collaborator viewer (MyWork.cs:81–85). |
| Task review | Same scope; ReviewerId viewer; Ready for Review/In Review (MyWork.cs:86–87). |
| Completed Task | Workspace authorized non-Archived/non-Cancelled project scope; Status Complete; actual own-assignee OR collaborator membership. NO completion-age floor or ceiling: Completed TaskView supports arbitrary dates (MyWork.tsx:369–375; TaskQueries.cs:185–194; Tasks.cs:149–152). Viewer must still hold a direct mapped role. Collaboration/creation alone cannot yield New. The Overview-only14-day truncation is NOT this union's cutoff. |
| Deliverable owner/reviewer | Actual owner non-Issued/non-Accepted/non-Cancelled OR actual reviewer In Review (MyWork.cs:88–90). No blanket terminal-deliverable arm. |
| Base Decision / Action / Project | Decision Pending/Under Review/Deferred and owner/requester membership, then direct-owner intersection; Action Open/In Progress with actual base owner/discipline-fallback membership, then User-owner intersection; base project membership/follow predicate, then direct ProjectManager intersection (MyWork.cs:101–121). No request/fallback/affiliation assignment. |
| Attention concrete item | Uncapped actual attention RouteToUserIds viewer under base authorized project scope; typed canonical target exists and its current direct role belongs to viewer. No envelope/rule/aggregate role or display100 limit (MyWork.cs:129). |
| Watching | Only with my_work_follow_up; actual Manual/Mention watch, actual visible projects and kind-specific Open predicate in MyWorkFollowUp.cs:21–29, :35–53, then direct-role intersection. Milestone watch has no mapped role. |
| Requested | Only with my_work_follow_up; Decision.RequestedById or Handoff/ChangeNotice/OpenRole.CreatedBy viewer, and the actual open OR closed-within14-days predicate at MyWorkFollowUp.cs:52–65, then direct-role intersection. OpenRole/requester/creator alone yields no token. Preserve typed closed-time resolution. |
| Coordination concrete items | Use the existing unfiltered WorkspaceCoordination concrete target relations, not an owner scan: outgoing/incoming Handoff stage/subject predicates (DisciplineCoordination.cs:161–180), concrete blocker Handoff targets, ChangeNotice Open OR scoped Pending Assessment (:210–219), screen_patterns-owned concrete InputUse targets/current-round linked Issue rows (:237–264), and existing gated Startability Task/Deliverable targets (:307). Each still requires current direct mapped role and exact normal read visibility. Review/assessment/submission/readiness aggregate/context records themselves have no new mapped role. Preserve each real independent gate; pagination and optional user filters are not eligibility limits. |

Base MyWork and workspace Task project scopes exclude Archived/Cancelled; follow-up uses its actual VisibleProjects relation (MyWorkFollowUp.cs:35). Do not silently impose one arm's extra project/status/date cutoff on the other arms. Milestone/context/notification/Inbox records with no direct mapped role remain ineligible. If an existing reader arm cannot be expressed by the shared relation, that is an implementation gap; do not widen the relation or fall back to unrelated timestamps.

Both decoration and MarkAll call the SAME closed typed SQL cohort builder; detail/row read bounds additionally constrain token SourceVersion and returned roles. There must not be an independent endpoint-side eligibility switch or a second per-kind MarkAll implementation. Test an old Completed own Task and a collaborator+current-reviewer Completed Task beyond14 days: visible eligible New must clear with MarkAll. Test creator/collaborator-only and unrelated readable nonTask owners remain ineligible.

Transaction sequence:

1. Check active authenticated self and work_screens, including on replay. Root adopted the private-preference rule permitting active global ReadOnly users to acknowledge their own authorized work; no business/project Writable requirement. Other-person and restricted targets retain normal403/404.
2. In one transaction, conflict-safe insert UserSetting, lock only that user's row FOR UPDATE, then reload seen fields. Producers never need that row. Independent preference writers update only their own columns.
3. Under this lock find user+request receipt. Normalize a closed action/payload, GUIDs and sorted/deduplicated tokens before hashing. Same request/body replays exact committed result; another action/item/token set refuses with normal idempotency-reused semantics. Recheck current opened-target permission/recipient before replaying a retained item response.
4. First use: ONE INSERT…SELECT of committed own token IDs only for InitialBaseline, including hidden/closed historical tokens without reading/returning their item facts/counts. MarkAll: ONE SQL CTE statement joining all typed authorized/current cohorts, their read versions/current recipients, version-bounded occurrences and insert-only acknowledgements. Multiple per-kind ReadCommitted queries after a lock are not one cut.
5. Update only seen preference fields/revision, insert exact acknowledgements and immutable response receipt, commit together. Public baseline/MarkAll result contains request/time/visit facts, no hidden IDs or cohort counts. Failure before commit leaves none; commit-before-response loss replays the same cut.

Existing generic idempotency claims before handling and fills its receipt AFTER the handler (Idempotency.cs:58–59, :91–99). It cannot preserve a crash-gap MarkAll cut. Root adds MyWorkSeenEndpoints.AtomicCommand at its exact bypass seam. Reuse atomic receipt patterns from Coordination.cs:18–41 / Handoffs.cs:78–98, not their project locks/business permissions.

An open outside My Work does not enroll. It may insert only exact opened acknowledgements with visit fields still null. MarkAll before successful enrollment refuses a clear precondition; UI waits for visit success. GET/prefetch/hover/background/failed or cancelled load/parent register/another-person view writes0 enrollment/acks.

## 6. Detail freshness, UI and cache

Root adds nullable flag-owned seen envelopes to required row/detail DTOs, omitted off and in other-person mode. Suggested own states Unenrolled/Seen/New/Unavailable; no direct role means NotApplicable/omission. No other recipient's tokens, internal ack list or hidden counts.

Detail tokens bind to the canonical version actually returned: shared read snapshot, OR later occurrence query constrained SourceVersion <= returned RowVersion plus returned roles. Unconditional latest-token query after reading detail is unsafe. Task detail seams Tasks.cs:26, :257; analogous guarded details exist in Deliverables/Decisions/Meetings/Registers/Projects/Handoffs/Changes.

Visible-detail hooks run after exact successful content commits with matching type/ID/version/tokens. Normal deep links/Comments opens count; useItemPanel is not proof (panel-host.tsx:29–43). Query functions, prefetch, stale cached detail followed by403/404, cancelled navigation and nested parent detail do not count. An opened requestId belongs to ONE exact normalized itemType/itemId/observedTokens payload: reuse it only to retry that same payload. If the still-mounted panel renders a replacement token set, mint a NEW requestId; never reuse a mounted-instance ID for changed tokens. Old in-flight/replayed responses cannot optimistically clear replacement badges. Token order/duplicates normalize before deciding whether payload changed.

Own MyWork page has one visible-visit lifecycle across Today/Overview/TaskView/Watching/Requested, not a visit per row/tab. Preserve other-person mode, source URL, filters and page state; render accessible textual New.

Root cache seam web/src/lib/keys.ts:4–20, :58–79: invalidate/refetch mywork, mywork-tasks, my-work-follow-up, active typed details and any new personal key after own commands. Existing generic changes do not explicitly name follow-up; add all mapped producer/Comment/visibility/flag cases. Clear private cache on account change/sign-out; do not impersonate a business Task event to refresh.

Bounded cross-device default: refetch on mount/focus/reconnect and15s interval only while own My Work is visible, using the normal batched decorated readers. Document/test this delay. Personal revision orders personal commands only. Existing LiveFeed.cs:8–17, :36–37, :90–100 is project/evaluation activity with bounded UUID-time overlap; hooks/live.ts:49–62 handles project/evaluated. Do not stream private markers through project audiences or claim that overlap proves commit ordering. Existing live changes may accelerate refresh; added polling stops off/background.

## 7. FR15 exact activity/comment implementation

One page-level typed-key batch over final authorized row IDs, deduplicated across repeated sections:

- Latest exact ItemType+ItemId ActivityLog by OccurredAt then stable ID; return event ID/time and actual ActorType/UserId/name together.
- Live Comment count by same typed pair, DeletedAt null, only for supported kinds. No per-row history/detail/comment HTTP.
- Missing actor/time/count remains unavailable; known empty0 differs from unsupported discussion.

Reuse Activity.cs:53–76 privacy and HubDb.cs:468–475 item-history index. Do not use item History's key-prefix related-record expansion (Activity.cs:41–46) for exact-row activity. Follow-up closed-history lookup currently groups UUID alone (MyWorkFollowUp.cs:63–64); keep lifecycle behavior but make new activity keys typed.

Current mismatch seam: TaskQueries.cs:258, :265–267 reads cached Task.LastActivityAt and newest Task-log actor separately. Comment save updates Task/Deliverable cached time (HubDb.cs:1107–1115), but its immutable event is Comment with its own ID (Entities.cs:759–775). Never pair that comment time with an unrelated older Task actor. Add compound exact activity facts for the display; leave existing LastActivityAt sort unchanged (TaskQueries.cs:239). Do not relabel related Comment events as exact Task history. System label requires actual ActorType System, not missing user.

| Canonical displayed kind | Discussion census and destination |
| --- | --- |
| Task | Supported; preserve work-activity-comments.tsx:23–41 and TaskPanel.tsx:65–77 taskSection=comments path. |
| Deliverable | Supported; actual Comments tab Deliverables.tsx:362, :519. |
| Decision | Supported; Decisions.tsx:530, :611. |
| Action | Supported; ActionPanel at Meetings.tsx:414 and its CommentsSlot at :483. Open the Action thread, not its Meeting. |
| Risk / Issue | Supported; Registers.tsx:677–683, :729–731, :740–746, :817–819. |
| Milestone | Supported even without assignment; Milestones.tsx:457–463, :499–501. |
| Project | Exact Project activity yes; no generic comments capability. NotApplicable, not fake0/thread. |
| Handoff / ChangeNotice / OpenRole | Exact typed activity; not generic Commentable. Preserve route-specific detail links; no invented comments/count/button. OpenRole remains no assignment. |
| Attention/Coordination/context/notification | Only actual canonical target rows can use that target's capability. No discussion on rules/aggregate/notification envelopes or eager context-record fetch. |

Authoritative commentable list is exactly seven kinds (Domain/Vocabulary.cs:201); guards/resolution Comments.cs:30–62, :68–92. Admin may retain deleted text in audit, but deleted comments are not live count.

Root generic section seam: transient panelSection=comments for non-Task supported panels, keep existing taskSection behavior. Add to ScreenLists.NotKept (Records/ScreenLists.cs:9–15); clear on type switch/close; honor only work_screens and exact successfully loaded item. Select real Comments tab, then focus/scroll labelled region after load; preserve recordContextReturn/dirty-leave and natural trigger return with scope/filter/group/columns/page/hash. Neither section key nor seen tokens/requests enters a saved view.

## 8. Disjoint future loans

No source work starts until root explicitly opens the next generation. Root keeps MyWork.cs and all shared seams; this updates the earlier ownership sketch to avoid overlap with D.

| Owner | Exact proposed boundary |
| --- | --- |
| Root storage/integration | Data/WorkSeenEntities.cs; UserSetting; HubDb/EF/migration/snapshot and publisher hook; permissions; UserPreferences; Idempotency bypass; Modules; all shared DTO/detail/owner-kind/cache/locale/metadata/generated/ledger bridges. MyWork.cs remains root-only. |
| B backend helpers/tests only | Exact new helper/publication/eligibility/command/FR15 files and signatures below. No existing MyWork/FollowUp/Tasks/TaskQueries/Deliverables/shared model edits. BACKEND READY then hold before root compiles. |
| Existing frontend ownership | MyWork/FollowUp/shared details/cache are root/D or another explicit later loan; B's current next loan is new backend helper and focused test files only. Coordinate D's person-trigger discriminator and normalized-payload open lifecycle without file overlap. |
| Detail UI lane | Explicit separate loan for TaskPanel, Deliverables, Decisions, Meetings, Registers, Milestones, Handoffs, Changes and project detail. Root coordinates current feature-file ownership. Shared panel-host/record-context/WorkspaceTasks/activity-comments remain root unless separately loaned. Only successful-visible ack/section navigation, no redesign. |
| Independent test lane | New MyWorkSeenTests, WorkAssignmentPublicationTests, WorkActivityCoverageTests, race tests; new my-work-seen.cjs/activity-comments browser source. Root owns shared fixtures/goldens/cache tests and ALL execution. |
| Root final acceptance | Shared contract/source review, focused real PostgreSQL/race/cache/DTO checks, production build, fresh normal-command API/browser joins, evidence/slice accounting. No concurrent .NET/Vite. No deploy/activation/commit/push implied. |

Metadata: additive optional flag-owned public fields, exact own endpoint contracts, new transient section key. No private persistence in activity/search/export registry. Root regenerates normal contracts only after integration, verifies off-shape, and keeps formal D20/packet acceptance/confirmed navigation policy untouched.

### Exact new helper interface for the source loan

Root's model names are fixed: DbSets WorkAssignmentOccurrences/WorkSeenAcknowledgements/WorkSeenCommandReceipts; occurrence properties Token/RecipientId/ItemType/ItemId/ProjectId/Role/SourceVersion/SourceActivityId/SourceOccurredAt/Provenance/RecordedAt; acknowledgement RecipientId/Token/AcknowledgedAt/Provenance/RequestId; receipt UserId/RequestId/Action/PayloadHash/ResponseJson/CommittedAt. Published requires exact activity/time; BaselineSeed both null. Private values Published/BaselineSeed, InitialBaseline/Opened/MarkAll, receipt Visit/Opened/MarkAll; lowercase64hex SHA256; JSON object response; no expiry. B does not edit root model/EF files.

Proposed new files only:

- Infrastructure/WorkAssignmentPublication.cs (Hub.Api.Infrastructure): HasChanges(HubDb db) returns bool; Publish(HubDb db, EntityEntry entry, ActivityLog? sourceActivity, DateTimeOffset recordedAt) returns void. Root calls HasChanges for the transaction boundary and Publish after version/build-log binding before base save. No personal locks.
- Features/WorkSeenContracts.cs: WorkSeenKey(ItemType,ItemId); WorkSeenRoleRecipient(Role,RecipientId); WorkSeenReadBound(Key,ProjectId,SourceVersion,Roles); WorkSeenObservedToken(Token,Role,SourceVersion); WorkSeenContext(SubjectId,Today,Now); WorkSeenDecoration(State,ObservedTokens); WorkActivityFact(EventId?,OccurredAt?,ActorId?,ActorName?,ActorType?,Commentable,CommentCount?). Keys/roles/tokens use GUIDs and source version int; times DateTimeOffset, Today DateOnly, comment count nullable int. These are helper contracts, not edits to existing DTOs.
- Features/WorkSeenEligibility.cs: ReadCurrentAsync(HubDb,Access,OrgSettings,WorkSeenContext,IReadOnlyCollection<WorkSeenReadBound>,CancellationToken) returns Task<IReadOnlyDictionary<WorkSeenKey,IReadOnlyList<WorkSeenObservedToken>>>; AcknowledgeAllAsync(HubDb,Access,OrgSettings,WorkSeenContext,Guid requestId,CancellationToken) returns Task<int>. One internal cohort SQL builder is reused; acknowledge-all count is internal only, never public hidden counts. Caller already owns the personal transaction/row lock.
- Features/WorkSeenReads.cs: DecorateAsync(HubDb,Access,OrgSettings,WorkSeenContext,IReadOnlyCollection<WorkSeenReadBound>,CancellationToken) returns Task<IReadOnlyDictionary<WorkSeenKey,WorkSeenDecoration>>. Off/other-subject/no eligible role returns no private decoration. Context.SubjectId must equal access.Actor.Id. Root builds read bounds from its exact returned canonical version and role values.
- Features/MyWorkSeen.cs: MyWorkSeenEndpoints.Map(RouteGroupBuilder api) and nested sealed record AtomicCommand for root middleware bypass; only the three own commands above.
- Features/WorkActivityFacts.cs: ReadAsync(HubDb,Access,OrgSettings,IReadOnlyCollection<WorkSeenKey>,CancellationToken) returns Task<IReadOnlyDictionary<WorkSeenKey,WorkActivityFact>>. Exact typed page batch; no personal-state writes.

CancellationToken arguments default to default. All feature helper contracts use Hub.Api.Features; root shared callers can import them. New focused files only: tests/Hub.Tests/Api/WorkAssignmentPublicationTests.cs, MyWorkSeenTests.cs, MyWorkSeenRaceTests.cs, WorkSeenEligibilityTests.cs, WorkActivityCoverageTests.cs. No source implementation or test execution is authorized by this plan itself.

## 9. Acceptance before S10 closure

1. FR15 Pat2h/3 live comments across Overview/Waiting/Blocking/reviews/query TaskView/workspace tasks. All seven commentable and every Watching/Requested kind tested; four unsupported kinds remain explicit. Real System, missing actor/time, zero/deleted comments and same UUID across kinds. Bounded page queries; closed rows issue0 detail/history/comments HTTP. Sort/group/filter unchanged.
2. First visible own visit baselines truthful legacy hidden/closed/soft-deleted/archived history without source times or hidden IDs/counts in response. Hover/prefetch/background/failed/other-person GET writes0. Later visibility/reopen gives no historical flood. Repeat visit request does not advance Previous twice.
3. Twelve-role publication parity via normal create/patch/transition reviewer/bulk/pickup/supervisor/member/template/conversion/risk→issue/import/PM commands. Actual activation exactly one source-version/event-linked token. No-op/stage/comment/watch/follow/lead/member/OpenRole/unassigned copy gives0.403/404/409/validation rollback leaves0.
4. Assignment A/reviewer B after baseline; duplicate A rows reconcile. Exact visible A open clears its observed tokens everywhere, leaves B New, changes0 source versions/history/evaluation/notifications. Visit2/reload leaves B pending. Deep-link/Comments equivalent; failed/cancelled/cached403/404/parent-register opens acknowledge0.
5. MarkAll spans more than200 tasks/100 attention and current filters/tab/scope/page, but only authorized/current eligible own roles at ONE cut. Include Completed own/collaborated Tasks at arbitrary completion age with a current direct role; do not apply Overview14-day floor. Preserve nonTask closed/requested14-day/review-stage/status/independent gates; unrelated readable responsibility is excluded. Hidden/revoked items unacknowledged/undisclosed. Late commit after cut remains New even with earlier source time. No mixed per-kind snapshots.
6. Deterministic barrier races: enrollment/uncommitted assignment, MarkAll/late commit, detailV/reassignV+1, identical-clock away/back, two roles in one save, old/new devices, reverse multi-item producers, canonical conflict rollback. Assert exact token membership and no new private lock cycle; no arbitrary sleep as race proof.
7. Commit-before-response loss retries exact visit/open/MarkAll result, no newer reselect or duplicate visit/ack. Different action/body/token reuse refuses. A still-mounted panel receiving new normalized tokens mints a new opened requestId; retrying the original payload keeps the original ID. Pre-commit failure leaves no receipt/ack/preference increment. Replay after flag-off/deactivation/revocation rechecks authority without retained hidden facts.
8. Non-assignment RowVersion advance preserves current occurrence; bounded stale detail gets no future token. Removal/current discriminator suppresses old recipient; return gives fresh token. Missing qualifying projection is Unavailable/integrity failure. Verify actual source event/version, not timestamps/UUID.
9. Density/digest/Home/onboarding/job writers racing personal commands preserve every field. UserSetting is not IVersioned and private revision is not a global EF concurrency token. Active ReadOnly personal rule tested separately from business permissions. Manager/PM/Supervisor viewing another person sees no private marker/token/time. Personal revision does not disclose hidden producer counts.
10. work_screens off omits new fields/UI/queries/commands while internal publication and prior pending state persist. Off-period reassignment survives. my_work_follow_up remains independent. Reenable never rebaselines an enrolled user or expands discussion capabilities.
11. Two real authenticated contexts reconcile ack/late producer within documented visible15s/focus path. No optimistic replacement-token clear. Cache tests cover every named root and account switch. Comments destination/close/natural focus/return at375/768/1280, preserving established design/navigation.
12. Fresh/populated PostgreSQL migration, unique/FK/append-only constraints, failure atomicity and off-contract parity. Root records implemented, locally checked, committed/deployed and human-accepted distinctly. This plan or unrelated checkpoint22 passes do not close S10.

Root has adopted the twelve-role map/direct PM/both Handoff owners, exact unfiltered-stream cohort with arbitrary-age Completed exception, active-ReadOnly private acknowledgements, immutable receipts/acks without generic cleanup, and non-concurrency personal revision. Migration cutover and the visible refresh integration remain root implementation/review work. These dark implementation defaults do not close053-D20 or authorize source work beyond an explicit loan.
