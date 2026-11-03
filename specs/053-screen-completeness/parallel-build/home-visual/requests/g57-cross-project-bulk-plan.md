# G57 final proposal — S12-A cross-project task bulk

Status: **REVISED READY / freeze HOLD. Documentation only.** This revision replaces the earlier per-item/orchestrated architecture. Root has selected the minimum S12-A architecture below; exact source ownership and contracts still need root freeze. No application files, tests, fixtures, schema, configuration or shared services are edited by this plan.

## Scope and acceptance boundary

S12-A implements FR-SCR-19 / AC-SCR-19 from spec-parts/32-screen-completeness.md:188–194,432: select explicit task rows across projects, preview per-row eligibility, apply the project Task List's seven existing bulk operations, preserve each project's permissions and submitted row version, list every refusal, write activity for changed tasks, and open this list from the departing-person tool filtered to that person's open assigned tasks.

The seven operations are **assign, setDueDate, shiftDueDates, setPriority, setDeliverable, hold, cancel** (web/src/pages/projects/Tasks.tsx:443). Maximum 200 explicit unique tasks per command. No generic edit, arbitrary transition, Undo route/offer, per-item command tables, pending-row orchestration, global command engine or new service in this slice. Existing legacy project bulk remains unchanged while work_screens is off.

**S12 overall stays open.** FR-SCR-20 / AC-SCR-20 and Open decision 053-D9 govern later S12-B task-surface Undo; S12-C is the non-task breadth inventory below. A passing reassignment case does not complete even S12-A: all seven operations and the departure entry point require evidence.

## Current source evidence

All source paths are relative to /Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053. These are source observations, not runtime proof.

| Source | Current truth / gap |
| --- | --- |
| web/src/pages/WorkspaceTasks.tsx:123 | Workspace Tasks has 100-row paging, filters, grouping, phone cards and desktop table; no selection or bulk toolbar. TaskQueries.cs:127 already returns RowVersion. |
| web/src/pages/projects/Tasks.tsx:544,677,812 | Project selection holds only IDs. Existing dialog shows selected count without eligibility preview; result toast truncates skipped reasons to six. Seven toolbar operations are narrower than the API's additional transition operation. |
| src/Hub.Api/Features/Tasks.cs:44,728–815 | Legacy Bulk has no per-row observed version. It uses one route project, omits missing/foreign IDs, catches item ApiException with task/start-authorisation reload, and saves changed rows then summary notices. That reload is not a safe new transaction-isolation strategy. |
| Tasks.cs:333–449,479–573; Infrastructure/Http.cs:18–31 | Canonical edits/transitions enforce row versions, correction/date reasons, field-specific rights, active-person/self-review, structure, dates and workflow. Transitions can affect timestamps, review history and parent deliverables. Legacy bulk field logic differs from Edit; do not copy its missing correction-reason check or silently change its off contract. |
| Tasks.cs:171,820; Hub.Domain/Permissions.cs:295–339 | Facts include current supervisor/collaborators/creator window. AssignTask, EditTask, ChangeDueDate and TaskTransition have different rights. AssignTo invokes active-person/self-review checks and EnsureMember. Current supervisor assignment authority can disappear after successful reassignment. |
| Features/Team.cs:20–78,139–142 | EnsureMember can add/merge membership and assignment follow, guard restricted-project team authority, notify the new member, notify their supervisor via StaffAssignment and notify PM via MemberAutoAdded. These obligations cannot be dropped merely to achieve one summary per recipient. |
| Infrastructure/Notify.cs:19–100 | Existing notifier applies actor/active/setup/current-project access/mute/direct-assignment/preferences/channel policy. Its five-minute collapse and 24-hour email heuristic do not establish command deduplication. It stages in-app and email rows. |
| Infrastructure/Idempotency.cs:39–119; Infrastructure/Keys.cs:43 | General POST idempotency lacks a body hash and stores a receipt after the handler, separately from task commits. Tx.Run already supports one database transaction; new atomic task receipt bypass must be root-owned. |
| Data/HubDb.cs:1273; Infrastructure/Audit.cs:6; Records/WorkTask.cs:5 | Canonical save hook writes immutable activity with old/new changes. Record activity IDs from this save; do not fabricate separate success logs or edit previous logs. Existing audit fields are not a complete future inverse payload. |
| web/src/pages/ReassignWork.tsx:27–55; app/routes.tsx:120 | Existing departure tool handles task/reviewer/deliverable/decision work kinds using its own endpoint. Add a task-list entry point; preserve other kinds and use the existing /tasks route. |

## Minimum architecture: one receipt and one bounded transaction

Recommend ONE task-specific TaskBulkCommandReceipt table, with unique (ActorId, RequestId), server CanonicalHashVersion, CanonicalIntentHash, CommandId, Operation, CompletedAt, bounded typed terminal row results, typed before/after field facts and actual task/side-effect activity IDs. Store stable summary channel IDs/suppression facts as part of this same bounded receipt. No per-item receipt table or global ActivityLog schema change. Max200 bounds the row collection; before/after maps have an explicit operation-specific allow-list, not arbitrary JSON snapshots. Do not duplicate free-form task bodies/reasons across 200 rows. Root freezes concrete model/JSON schema/indexes/migration/retention.

**One Tx commits the receipt, all valid row mutations, canonical membership/follow effects, task and side-effect activity, and coalesced notifications together.** AC-SCR-19's six valid plus two terminal refused rows are one committed result. An infrastructure error rolls back the WHOLE attempt, including otherwise-valid changes, logs, notification rows and the new receipt. There is no pending row, independently committed item, batch-resume orchestration or partial infrastructure success contract.

Recommended command sequence:

1. Require current work_screens and authenticated actor; validate the whole envelope before writes: RequestId, exact operation/typed params, ≤200 rows, no duplicate task IDs, valid per-project deliverable mapping, supported schema version.
2. Begin one Tx; serialize actor/request identity with the root-approved receipt lock/unique-key strategy. Check an existing receipt BEFORE current mutation feasibility. Identical intent reads a sanitized terminal result; changed intent is refused. A racing identical command waits/returns the same committed receipt; if the first attempt rolls back, a later retry can execute the entire original intent.
3. In deterministic project/task order, load actual visible project contexts, task facts/versions and referenced people/deliverables. Validate ALL rows without writes. Cache the original current-actor context for each project: planned membership additions must never grant the actor extra permission for another row. Check current row-level permission, submitted version, correction/date reason, target validity, self-review/date/workflow rules and EnsureMember feasibility. Unknown, deleted, foreign-project or hidden targets have no existence-disclosing detail.
4. Build typed internal staged plans for eligible changed rows, no-ops and refusals. Stage exact before/after fields and side-effect/notification intents; do not change tracked entities, reserve keys, call mutation-capable EnsureMember or emit logs/notices during validation. Per-item domain refusal is terminal; it does not abort other validated rows.
5. Apply only validated plans under the same transaction and current concurrency guards; run canonical mutation/membership/follow helpers with notification capture active. Save through the canonical audit hook and collect actual activity IDs. No-op/refused rows must have no changed task version, activity, membership/follow, evaluation or notification effect. Zero-day/no-value-change inputs must be genuine no-ops, not fake LastActivity/counter changes.
6. Aggregate canonical notification obligations into one count-only summary per recipient; stage channel rows and receipt result in the SAME transaction, then commit. Return only the sanitized projection below. A lost committed reply is recovered by identical retry, with no repeated mutations or summaries.
7. A newly discovered domain or provider failure during apply that staging cannot safely isolate rolls back the attempt; do not catch it and reload only the task. Prefer complete validation and whole-attempt rollback. If root finds savepoints necessary for a specific domain refusal, freeze full DbContext tracker/audit-note/notification/correlation/outbox cleanup first, including all membership/follow/parent/comment/authorisation state. A savepoint alone does not rewind EF or AuditContext. No savepoint shortcut is authorized by this proposal.

## Early wire contract for root freeze

All additions are server/client gated by existing work_screens. New preview/apply are distinct from legacy Bulk:
- POST /api/v1/tasks/bulk/preview — TaskBulkIntent → TaskBulkPreview.
- POST /api/v1/tasks/bulk/commands — TaskBulkCommandBody → TaskBulkCommandResult.
- No additional execute, generic edit, arbitrary transition, receipt polling or Undo endpoint.

Root owns a custom atomic-command marker and generic IdempotencyMiddleware bypass. Preview MUST bypass the middleware before its key claim/cache even when Idempotency-Key is present, and must have no receipt/activity/notification/evaluation writes. Apply owns its actor/request hash receipt; the generic middleware must neither claim nor replay this path. The body RequestId is the one identity; do not add an independent mutable retry key.

| DTO | Required shape / semantics |
| --- | --- |
| TaskBulkIntent | schemaVersion=1; items ordered array of {taskId, projectId, rowVersion}; operation one of seven; params typed discriminated operation params; reason nullable string validated by the existing relevant rule, with no invented reason maxlength. |
| TaskBulkCommandBody | requestId plus the complete immutable TaskBulkIntent. |
| assign params | assigneeId nullable; clearing is explicit null. No reviewer/general field patch. |
| setDueDate params | dueDate nullable DateOnly; clearing is explicit null. |
| shiftDueDates params | days integer under canonical date validation; preserve nullable dates. |
| setPriority params | priority canonical enum. |
| setDeliverable params | targets array exactly one {projectId, deliverableId nullable} per selected project. Reference must be in that project; no name matching or foreign-ID reuse. Clearing is explicit. Setting a deliverable retains canonical coupled MilestoneId clearing. |
| hold / cancel params | empty typed object; canonical TaskTransition checks/reason/lifecycle for that operation only. |
| Visible preview row | index, taskId, projectId, key, outcome eligible/unchanged/refused, stable code and safe reason. Never a stored receipt or durable authorization. |
| Visible terminal result row | index, taskId, projectId, key, outcome changed/unchanged/refused, safe code/reason, actual resulting rowVersion and task activity IDs where appropriate. No per-item receipt/inverse fields. Before/after and side-effect ancestry remain internal receipt evidence unless root separately approves a visible projection. |
| Unavailable row, preview or replay | EXACTLY {index, outcome:unavailable}. No true changed/refused/no-op outcome, ID/name/key, version, reason revealing project existence, activity, receipt, before/after or inverse fields. |
| Preview/result counts | eligible or changed, unchanged and refused counts computed ONLY from currently visible rows; unavailableCount separate. Do not include an unsanitized updated/total-success count, hidden success flag or hidden warning. |
| TaskBulkCommandResult | opaque commandId for this original actor's command; completedAt; visible/unavailable rows in input order; sanitized counts and visible-only warnings. No pending/completion-progress state or Undo offer. |

CanonicalIntentHash version1 is computed server-side from the validated typed operation, ordered item identities/versions, normalized typed params and canonical trimmed reason. Preserve input row order because response indexes use it; normalize deliverable mappings by project ID and object property order deterministically. Hash does not depend on raw JSON property ordering; presence/null semantics remain explicit. Store the hash version and reject unsupported input versions/unknown operations/params. Changed canonical intent under a reused actor/request identity is refused.

Receipts persist no-op/refused outcomes too. Later permission, version or target changes must NEVER revive a terminal row on retry. A new request ID means a new explicit command, not recovery. UI keeps the complete original intent/RequestId/selected versions locked while the result is uncertain; do not promote versions or turn Retry into a new command.

## Current-visibility terminal projection

Identical retry checks current work_screens, authenticated ORIGINAL actor, matching actor/request/hash and CURRENT project/task visibility, but does not require mutation rights already lost after a successful assignment. Existing terminal rows are read, never executed again. Current write authority is required for a fresh command's validation, not to recover its historical visible result.

Re-project stored results on EVERY return/replay. A task/project now hidden/deleted/unavailable becomes only the exact unavailable-row shape above. Suppress its actual terminal outcome, names/IDs, versions, activity/before-after, warnings and all receipt/inverse detail. Counts derive only from visible rows, plus separately labelled unavailableCount. Never return the stored raw result JSON and never authorize receipt disclosure through old receipt authority or generic cached responses. A foreign actor cannot read another actor's receipt.

## Root-owned notification capture and ACL contract

The seven-operation mutation service must not invoke single Edit once per item and emit 200 single-task messages. Root owns Notifier, TeamService notification seams, notification schema/ACLs, channel policy and generic middleware. API author consumes a narrow capture seam rather than editing those shared files.

Recommended interface to freeze BEFORE parallel authoring:
- BeginTaskBulkCapture(commandId, originalActorId) returns a request-scoped capture context; no static/global mute.
- Existing canonical Notifier.Send calls inside that scope stage typed TaskBulkNoticeIntent values instead of directly staging Notification/EmailMessage.
- Intent carries eventType, recipient, sourceProjectId, sourceTaskId when applicable, obligation kind (assignment/date/member-added/supervisor/PM), and canonical direct-assignment policy facts. Coalescer receives the changed-task set and actual membership/follow effects; it never infers success from selected IDs.
- StageTaskBulkSummaries(capture, tx) applies canonical actor suppression, active recipient, project lifecycle/setup, current access, mute/direct-assignment, preferences and channel choices, then adds at most ONE in-app summary and ONE email per eligible recipient. Return actual stable summary/channel IDs or terminal suppression facts for the receipt. No flush outside the command Tx.

Preserve assignment/date notice families AND all EnsureMember obligations: AddedToProject to a newly added person, StaffAssignment to their supervisor and MemberAutoAdded to PM. Preserve canonical membership/role/follow/audit effects. Coalesce those obligations when the same person has several roles or appears across three projects; do not drop them or send three project emails. Bulk does not gain unrelated single-edit reassigned-away/reviewer/general-status notice families. Root freezes any existing supervisor-reassignment PM obligation applicable to this command; capture must preserve it where canonical policy requires it.

Summary body is count-only, with no source task/project/person names or raw reasons. Link is the EXISTING authorized /tasks route; no new recipient receipt view or unbounded IDs URL. RequiredProjectIds is the distinct union of source projects contributing to that recipient's included summary. Avoid attributing refused/no-op rows to a recipient or creating notices for an assignment/member addition that never committed.

Root will add nullable INTERNAL Notification.RequiredProjectIds. For new non-null values, require ALL projects currently visible to the recipient BEFORE query paging, unread count, pulse and mark-read. Empty/invalid project sets must not accidentally widen visibility; root freezes the valid new-row invariant. Legacy null rows retain old behavior. EmailMessage already has RequiredProjectIds and send-time access checks; populate the union, preserving its existing delivery guard. No client-supplied required-project list is trusted.

Dedup identity is stable (commandId, recipientId, channel) and persisted with the command's terminal receipt in the SAME Tx. Serial actor/request receipt execution and atomic notification/receipt commit ensure identical retries cannot create or increment summaries again. Do not use five-minute unread collapse, 24-hour dedup or per-project summaries as the dedup contract. Delivery transport remains the existing email worker; exactly-once notification enqueue is not a claim of exactly-once external email delivery.

## Minimal exact ownership proposal (not permission to edit)

| Owner | Exact paths / scope |
| --- | --- |
| A backend author | NEW src/Hub.Api/Features/TaskBulkCommands.cs for seven-operation DTOs/preview/validation/staged apply/receipt projection; bounded route/helper seams ONLY in src/Hub.Api/Features/Tasks.cs; NEW tests/Hub.Tests/Api/TaskBulkCommandTests.cs. No generic command engine or unrelated Task refactor. |
| UI author, after DTO freeze | NEW web/src/components/hub/task-bulk-command.tsx; web/src/pages/WorkspaceTasks.tsx selection/cards/table integration; web/src/pages/projects/Tasks.tsx flag-on bulk integration while preserving off path; web/src/pages/ReassignWork.tsx departure link. No TaskPanel/MyWork/board Undo changes in S12-A. |
| Browser author, after fixture/DTO freeze | NEW web/tests/task-cross-project-bulk.cjs and task-cross-project-bulk.fixture.cjs. Deterministic synthetic transport data; root executes. |
| Root shared integration | Data/Entities.cs, HubDb.cs, sequential new migration/snapshot for ONE task receipt plus nullable internal Notification.RequiredProjectIds; Infrastructure/Idempotency.cs, Notify.cs, Features/Team.cs capture seam and notification query/ACL paths; Program.cs route integration if needed; locales, keys/cache, generated DTOs/contract/inventory. Exact shared loans remain root-owned. No permission/workflow expansion or global ActivityLog schema. |

Earliest integration deliverables are operation DTO discrimination, receipt JSON/result projection and the notification capture/stage interface. Backend/UI must not invent incompatible shapes in parallel. Root freezes concrete shared names/path ownership before source loans. No helper or execution is started here.

## UI behavior

Selection is an explicit task/project/observed-version snapshot. Select-all selects only the current page; never all filtered results. Retain selection across page/sort, show selected total/outside-page count, cap at200, and clear with an announced reason when actor, filters or project scope changes. Refetch never silently upgrades selected versions; explicit deselect/reselect is new intent.

Preview is read-only and shows visible eligible/refused/no-op rows plus unavailable count and complete accessible refusal details. Confirmation states eligible count and explains apply revalidates versions/rights. A failure after preview can be a normal refused row; an infrastructure failure rolls back the entire attempt and is retryable with the original immutable request, not a false partial success.

Completed result shows changed/no-op/refused/unavailable counts under current visibility, every safe refusal, and an accessible details view beyond the toast. Keep filters/URL, phone layout, focus/scroll and actual task/activity/planning/readiness consumers fresh through root-owned canonical invalidation. Do not show an Undo action in S12-A.

Departure tool adds a work_screens-gated link to /tasks filtered by that user's open assigned tasks using existing supported assignee/open query semantics and appropriate current project scope. Its reviewer/deliverable/decision work kinds remain in the existing tool. Verify the destination filters and data, not merely the link's existence.

## S12-B/C remain open

S12-B must separately implement task-field/status/reassignment/date/bulk Undo across project/workspace lists, boards, My Work and TaskPanel, including canonical reasons, current rights/version checks, immutable activity ancestry, ten-second timing and original notifications remaining factual. No capability, route, deadline or inverse payload is promised by S12-A's evidence receipt.

Composite inverses are NOT automatically “canonically impossible.” Restoring a deliverable may need the coupled prior MilestoneId; date shifts need both dates; completion may require canonical reopen plus a permitted original-progress edit; parent transitions and review state may require several guarded commands and dependent versions. Later S12-B analysis must distinguish a missing composite implementation from an actually prohibited workflow, preserve history/comments/counters, and make any true policy conflict explicit. It cannot simply call all unsupported inverses domain refusals.

Concrete non-task inventory for root breadth decision (not implemented by this task receipt):
- projects/Deliverables.tsx, Milestones.tsx, Timeline.tsx: fields/owners/status/bulk, milestone date cascades/rebaseline/reopen/cancel and timeline shifts.
- projects/Registers.tsx, Decisions.tsx and components/hub/registers.tsx: risk/issue fields/owners/workflows/resolution, decision context/outcome/defer/reopen, record-panel adapters and linked durable facts.
- projects/Meetings.tsx, Handoffs.tsx, Reviews.tsx, Changes.tsx, Submissions.tsx and WorkspaceCoordination.tsx: action fields/conversion/reuse, assignment, source/review/change/submission and issued/published evidence.
- projects/Team.tsx, WorkspaceTeam.tsx, Time.tsx, Vacation.tsx, Workload.tsx and planner components: membership/roles, time, leave, allocation/date/publication commands with independent authority.
- projects/ProjectList.tsx, ProjectSettings.tsx, Portfolio.tsx, admin screens and ReassignWork.tsx non-task kinds: project/status/bulk, settings/roles and departure work beyond task assignee.

These families need exact adapter/command/permission ownership before broad completion claims; this inventory is not an audited inverse specification. FR-SCR-20 entity breadth and 053-D9 remain Open. Root acceptance of S12-A architecture does not close S12 or activate work_screens.

## Meaningful checks for root execution

Provider-backed:
1. Exact AC-SCR-19 eight tasks/three projects: six valid plus two terminal refused in ONE Tx/receipt; six actual task activities, plus only legitimate canonical membership/follow/audit effects. Wrong/hidden/deleted/mismatched project targets disclose only unavailable. Initial current permissions cannot be widened by staged membership.
2. All seven operations: canonical rights/reasons, inactive/self-review targets, date bounds/nulls/zero-shift/no-op behavior, same-project deliverable mapping and coupled milestone clearing, actual hold/cancel workflow/lifecycle checks; unchanged legacy off properties/order.
3. Stale row before/between preview and apply produces terminal refusal while other valid rows commit. Unexpected provider/constraint/save failure rolls back ALL task/membership/follow/activity/evaluation/notification/receipt changes. If any savepoint is introduced, prove full tracker/audit/notice state cleanup.
4. Concurrent identical requests and lost committed response return the same result with exactly one mutation/log/summary family; changed canonical payload under same actor/request refuses. After full rollback retry executes once. No-op/refused receipt rows stay terminal after later rights/data changes.
5. Current-visible terminal read succeeds after initiating supervisor loses mutation rights. Revoked visibility yields exact unavailable shape with NO true outcome/name/ID/version/activity/receipt/before-after/inverse and visible-only counts/warnings. Foreign actor cannot recover another actor's command.
6. Preview with/without Idempotency-Key leaves all receipt/audit/notification/evaluation state unchanged, observes fresh eligibility and never invokes generic cache. Flag off rejects new endpoints/replays before disclosure, preserving stored receipt bytes for later safe projection.
7. Recipient is assignee, supervisor and PM across three projects: ONE summary per recipient/channel; EnsureMember/follow/PM/supervisor obligations preserved and coalesced. Inject failures before/after Tx commit and retry: same IDs, no duplicates or collapse increments. Refused/no-op rows contribute nothing; existing user preferences/setup/mute/current-access/actor suppression preserved.
8. Notification.RequiredProjectIds ALL-visible filtering precedes paging, unread/pulse and mark-read; lose access to one required project and new summary disappears from all relevant surfaces. Legacy null notifications stay unchanged. Email guard gets the union; no names/raw reasons leak in summaries.

Real browser, desktop and375px:
- Select across three projects/two pages, select-page vs total, cap200 and filter/scope reset; inspect exact submitted versions and all seven parameter dialogs/multi-project deliverable choices.
- Actual preview, changed eligibility/version between preview/apply, six changes/two refusal details; result has no Undo/pending offer. Phone layout, keyboard/focus/scroll, URL and panel context remain usable.
- Lost committed reply retries identical request ID/body/versions, no second mutation; infrastructure failure yields no partial change and retry preserves original intent. Permission loss sanitizes replay/counts without requiring lost assignment rights.
- Departure link opens the real authorized filtered task list; other departure kinds remain available. Actual flag-off path retains legacy UI/body; no enhanced reads/writes or unsafe stored receipt.
- Inspect actual summary/unread/pulse visibility after required-project access loss, plus one count-only message per recipient/channel rather than three project messages. Fixture behavior alone cannot establish provider/notification ACL correctness.

Verification: **UNRUN by A for G57.** This is a documentation-only source-grounded revision. No application/test/fixture/schema/locale/config edits, helpers, scripts, parser/types/build/generation/test/browser operations, flags, services or commits. Root owns source freeze, execution and acceptance evidence.
