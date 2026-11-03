# G58 / checkpoint30 — S16 sections and collaborator contribution marks

Root-authorized dark implementation after checkpoint29 local close41/49. Basis: work-delivery/requests/g58-task-sections-contribution-plan.md plus independent source review. No decision status, Accepted gate, maintained flag, deployment or permission-role policy is changed. Root owns all integration/schema/generation/build/provider/browser and final verification. No .NET/EF/tests while C# authors write; no frontend checks/Vite while relevant app authors write; no simultaneous Vite/.NET; one Chrome lane. Preserve .review/checkpoint30-baseline and2817prior outputs.

## Source ownership

A is sole writer of:
- web/src/pages/projects/TaskPanel.tsx (S16 layout/contribution integration only; preserve all unrelated controls/rights)
- new web/src/pages/projects/task-section-navigation.tsx
- new web/src/pages/projects/task-contribution.tsx
- web/src/components/hub/fields.tsx (HistoryList/imports opt-in Task actor filter/paging only; no unrelated field/record-tab behavior edits)
- new web/tests/task-sections.cjs and task-sections.fixture.cjs
- new web/tests/task-contribution.cjs and task-contribution.fixture.cjs

B is sole writer of:
- new src/Hub.Api/Features/TaskContributions.cs
- src/Hub.Api/Features/Tasks.cs (TaskDetailCollaborator optional Contribution DTO/read attach only; old mutations/load/facts/rights unchanged)
- new tests/Hub.Tests/Api/TaskContributionTests.cs
- new tests/Hub.Tests/Api/TaskHistoryActorTests.cs

Root reserves model/HubDb/Records/Permissions, Activity.cs/ActivityScreenFields.cs, Modules, panel-host/drafts, locales/cache/generated/package/fixture-tsconfig and migration/snapshots. A/B send exact public DTO/locale/shared seam proposals early. No other existing file is loaned. C/D are next-plan documentation only.

## Seven real sections

work_screens on: Details, Dependencies, Review, Time, Comments, Links, History mount together with stable per-task IDs and real existing bodies; sticky section strip scrolls/focuses its target and observes the actual owning sheet/main scroll root without moving focus or rewriting URL during manual scrolling. Never duplicate writable review/general fields. Preserve background edit components, original versions, source filters/return chain, exact nested Person/history actor trigger and natural scroll. Pause observation under overlays/non-owning detail surfaces. Explicit taskSection targets must belong to the actual Task inspector/page; retain old S10 Comments link compatibility. Off retains old layout/tab/read behavior; people_screens/small_ux independent.

Time section moves honestly labelled existing estimated-hours controls (not progress) plus a link to the canonical own Time view /time?taskId=<actualId>. Default Time scope is own; do not send scope=all, fabricate actual totals or add a parallel time writer. TASK_SECTIONS extensions remain real extensions. Existing packet024 embedded actual-hours/Add Time gap is not claimed as fixed. Standard accepted bundle demonstrates all seven actual targets. If a module slot is unavailable, retain a labelled disabled item/notice; never call a missing module or pretend empty permission-success data.

Task History uses root's existing history reader/rendering with A opt-in prop taskActorFilter?:boolean (defaultfalse). Root flagged new GET /tasks/{id}/activity/actors returns TaskHistoryActorChoice{Id,DisplayName,IsActive}[] from DISTINCT actual visible matched history across all pages; no directory listing/System GUID. All includes System; selected person excludes it. root item-history adds raw string actorId argument, parsed/applied only for Task with work_screens on; off ignores even malformed new argument/keeps old wire behavior. Root ItemQuery reuses exact direct/same-key/comment/link/hour/dependency/privacy boundary and canonical Task key fallback where no prior parent event exists. Apply feature-kind visibility in SQL before choices/count/page. A owns scoped URL state taskHistoryActor=<taskId>:<actorId>, taskHistoryPage=<taskId>:<page>, resetpage1 on person change, real server totals/paging, viewer/project/flag query keys and current-error hiding. Root handles narrow presentation-only draft preservation for same Task/owning surface; no blanket query-navigation guard bypass.

## Contribution model and contract

Root declares TaskContribution : Audited, IAuditable (not ProjectItem/evaluation/publication): Id, CollaboratorId, TaskId, UserId, ProjectId, TaskKeySnapshot, TaskNameSnapshot, MarkedAt?, Note?, own RowVersion. Unique CollaboratorId. Composite FK(CollaboratorId,TaskId,UserId) to actual TaskCollaborator(Id,TaskId,UserId), Cascade; FK(TaskId,ProjectId) to WorkTask(Id,ProjectId), NoAction; necessary alternate keys on those existing parents. Snapshots/identity are server-derived immutable provenance. Note<=200; MarkedAt null implies Note null. No seeds/backfill/status inference. Root save guard protects immutable identity/snapshots; stored audit never rewritten.

Version0 means no state for this exact membership. First real mark explicitly creates version1 (normal Audited inserts otherwise begin0); later modifications use normal version increment. Withdrawal clears MarkedAt/Note but retains row/version, preventing ABA. Noop withdrawal on missing state/version0 creates nothing. Physical collaborator removal cascades current state only; immutable activity remains. Re-add gets new membership identity/no inherited mark. Soft task deletion/restore does not reset state; current visibility governs reads.

Top-level public generated DTOs:
TaskContributionRead(CollaboratorId:Guid,RowVersion:int,MarkedAt:DateTimeOffset?,Note:string?,CanMark:bool,CanWithdraw:bool,RefusalReason:string?)
TaskContributionSetBody(CollaboratorId:Guid,RowVersion:int,Marked:bool,Note:string?=null,Reason:string?=null)
PATCH /tasks/{id}/contribution -> TaskContributionRead. Strict unmapped-field rejection. Required JSON CollaboratorId/RowVersion/Marked; nonemptyID, version>=0. Body version is the nested contribution's version, never Task's version. Typed UI may also send same If-Match; canonical Http.CheckBodyVersion enforces body state without parent header inheritance. Do not invent generic receipt/replay/Undo semantics for this PATCH.

B appends null-omitted TaskContributionRead? Contribution=null to existing TaskDetailCollaborator, whose Id stays USER ID. Flag-on returns a batch-loaded state/read rights for every actual collaborator; flag-off omits addition entirely and performs no contribution reads. New own write is404 while off. Other-person marks are project-visible facts; command rights never follow from seeing them. Unknown/unavailable source is not fabricated zero state.

B command gates current flag/active actor/TaskEndpoints.Load visibility and canonical ProjectContext Writable. Caller comes from Access.Me.Id and must be actual current collaborator; supplied CollaboratorId must match that exact relation. No assignee/watcher/PM/Admin-on-behalf exception. Root pure Permissions.MarkTaskContribution composes active +actual own relation +existing Writable; no role expansion. Project Complete still uses separate canonical ProjectEndpoints.CorrectionReason; collaborator note is not correction reason. Task Complete/Cancelled does not autoerase or introduce a new terminal prohibition: current project Writable/visibility apply.

One existing business Tx locks/rechecks exact membership and state (membership FOR UPDATE serializes first creation/removal) before version validation/apply. Concurrent first marks: one version1, other stale409; never auto-promote version or substitute newly re-added relation. Normalize optional mark note via trim/null; max200; same desired state+normalized note at current version is true noop. Different note while already marked refuses until withdraw/re-mark. Non-null note on withdrawal refuses. Wrong type/forged user/time/task/status fields refuse. Timestamp is TimeProvider UTC, not client supplied.

One automatic registry audit per real change; B uses Audit.Note action ContributionMarked/ContributionWithdrawn with normal explicit category. Root TaskContribution record fields MarkedAt/Note, AuditType TaskContribution, project/key/name from server snapshots. No second Task log/Comment; no Task status/assignee/reviewer/progress/dates/RowVersion/LastActivity/Seen, assignment occurrences, receipt, evaluation or notifier/outbox changes. Register exact new kind under work_screens SQL visibility BEFORE all history/export counts/actor choices, not just Render.

A UI shows actual marked time/project-visible note beside collaborator name; only own actual permitted relation gets Mark/Withdraw. Original membership/version/note survives409/network/malformed/lost reply, section/Person navigation and background failure; no automatic PATCH retry/version replacement. Explicit current-read/reload may report matching CURRENT state or start a deliberate new intent; it is not an atomic receipt claim. Use genuine dirty/uncertain leave protection. No status/progress/checklist/time side effect.

## Required focused evidence

B authors source-faithful provider checks in the two new test files: note0/200/201, mark/withdraw/noop/stale/races/removal/re-add, actual Jill collaborator vsAlex owner, readOnly/inactive/hidden/on-behalf/forged fields, Complete-project correction, ownversion0->1, exact oneaudit and byte/semantic absence of parent/publication/notice effects, current DTO batching, off-hide before totals/actors/export with stored on preservation and transaction rollback. History actor cohort >200, inactive/System/privacy/dependency/same-key other-project/no prior Task event and unknown/malformed/off cases. Root owns tests for shared SQL masking if additional.

A authors strict generated fixtures and actual long inspector/standalone/375px browser flows: all seven targets, real scroll/active marker without focus stealing/observer URL writes; history actual filter/page query and Person return; retained failed inline/contribution drafts and exact original version; own mark/withdraw explicit bodies plus current refusal/off; natural keyboard/scroll and no unintended commands. No mocked application state/cache/focus/observer event or empty sections used to manufacture proof. Root runs smallest focused checks after READY hashes/all holds, then independent review/final artifact verification. S16 checkbox remains open until final provider+browser gates pass.


## Closed locally

Checkpoint30 S16 passed the final root gate on2026-11-02;42/49 slices are locally checked. All G58 sources HOLD. Read checkpoint30-findings.md and checkpoint30-runtime/verification-rollup1.json for final hashes, fixes, passing checks and limits. This does not mark the packet Accepted or authorize flag activation, commit, deployment or full-goal completion.
