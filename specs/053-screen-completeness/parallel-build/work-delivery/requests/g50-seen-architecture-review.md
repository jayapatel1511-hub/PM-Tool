# S10 independent architecture review — accepted implementation direction, not built

Read-only reviewer `checkpoint22_person_backend_review` examined the g47 proposal, current audited save/idempotency/preferences/My Work paths and FR/AC-SCR-15/16. No execution or source change by reviewer. Root adopts the following bounded direction for the next S10 source loan; formal 053-D20 remains Open. S10 remains unchecked.

## Exact-token publication and acknowledgement

- Append-only `WorkAssignmentOccurrence`: token, recipient, typed item/project/role, canonical source entity version, optional exact source activity/time, provenance Published or BaselineSeed. Unique item/role/source-version (and source-activity/role for published rows). Select the current occurrence by the item's canonical version and actual current recipient/discriminator, never timestamps/UUID/audit-chain sequence. Away/back creates a new token; same-value, comments and stage-only reviewer changes create none.
- Publish alongside the exact built activity and canonical change before the same base SaveChanges commits. Failed/refused commands roll everything back. Producers do not lock or increment personal preference state. Maintain internal occurrences while public presentation is off; no public off wire/request change or fictional historical time.
- Insert-only acknowledgements unique recipient+exact token, with time and InitialBaseline/Opened/MarkAll provenance. Stale opens cannot overwrite newer acknowledgements. No canonical version/history/evaluation/notification mutation.
- One typed closed producer map: Task assignee/reviewer; Deliverable owner/reviewer; Decision internal owner; Action internal owner only when discriminator User. Census actual Risk/Issue/ChangeNotice/Handoff/project-PM rows rendered in My Work before declaring coverage. Following/requester/collaborator/affiliation/discipline lead/OpenRole are not assignments.

## Snapshot cut and first use

First successful visible own My Work visit enrolls with a token-only snapshot of already committed own occurrences, including hidden/closed historical assignments without reading or returning their item facts/IDs/counts. This suppresses historical floods and false New on later visibility/reopening. Seed current legacy assignments with truthful BaselineSeed/unknown source time, not manufactured activity. Late commits absent from the cut remain New even with earlier recorded times. Missing qualifying post-migration projection is an integrity gap, never silently Seen.

Later visits advance only first/previous/last visit facts and preserve all pending New markers. All GETs/prefetches/other-person views remain read-only and never enroll. Current read authority and current recipient predicates always govern public decoration.

## Own commands and atomic receipts

`POST /me/work/visits {requestId}`; `POST /me/work/opened {requestId,itemType,itemId,observedTokens[]}`; `POST /me/work/seen-all {requestId}`. Strict bodies, own-user only, work_screens gate. Serialize only personal commands/receipts on the caller's state row. Record receipt and acknowledgements/preferences in the same transaction; reused request ID with another action/body refuses. Existing generic idempotency receipt is written after handler return and cannot preserve a crash-gap mark-all cut.

Mark-all selects all authorised/currently eligible own occurrences across supported streams independently of UI tab/filter/page, using one database statement/snapshot after acquiring the personal lock. Separate per-kind ReadCommitted statements are not one cut. Retries replay the exact committed result and never reselect newer occurrences. No producer-to-person lock ordering is needed.

Opened token identities must come with the exact successful detail actually displayed: same read snapshot or sourceVersion <= returned canonical entity version. An unconditional later token read could attach a new assignment to stale detail. Only an actually visible successfully loaded exact detail triggers POST; no hover, prefetch, parent register, failed or cancelled detail. Recheck permission/current recipient on POST. Tokens bind viewer/item/role; stale token ack cannot clear replacements. Invalidate/refetch, never optimistically clear every badge or rely on personal revision to order producer events.

## Required verification and remaining FR15

Race/privacy pack: enrollment/mark-all versus late commit; old detail/open versus away/back; two devices old/new tokens; duplicate command after commit-before-response failure; reverse multi-item transactions; same-clock assignments; unchanged reviewer stage; hidden/revoked target; other-person view; historical reopen; off-period reassignment; unrelated preference writers; failed canonical rollback.

FR15 is separate: non-Task FollowUp/Watching/Requested rows still need a complete rendered-row census, batched exact-item latest immutable activity and live comment counts, real System actors and exact Comments navigation. Unsupported discussion kinds remain explicit. This architecture review does not close either requirement.
