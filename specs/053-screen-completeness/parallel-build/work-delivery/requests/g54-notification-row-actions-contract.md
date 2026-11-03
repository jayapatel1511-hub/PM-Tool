# G54 — S45 notification row actions contract

Status: READY for root review; documentation only, not implemented or verified.
Date: 2026-11-02. Author: Work and delivery (B).
Checkout: `.build-052-053`, branch `codex/build-052-053`.
Authority: CONTROL generation 54; inspected during source authoring, then rechecked during `generation54-frozen-backend-isolated-bootstrap-build`. B's documentation-only loan remains unchanged.
Only this file is B's current write loan. Prior app/test sources remain HOLD.

## Scope and acceptance boundary

FR-SCR-78 and AC-SCR-78, task S45, require three additions to the notification centre: Needs your action, the project follow menu, and a truthful per-row delivery line. Part G is gated by `people_screens`.

“Row actions” does not authorize a second business-command channel. An optional action affordance opens the exact current item or its existing canonical resolver; the normal detail workflow supplies permissions, required reasons, readiness checks, versions and command handling. This contract adds no permission, status, notification-delivery policy, workflow or generic mutation dispatcher.

This draft proposes an implementation split and wire contract for root to freeze. It does not edit specifications, decisions, task status, flags, generated contracts or application code. Root owns shared seams and all execution. Only Jay can mark Accepted.

## Observed source, not assumptions

Paths below are relative to the checkout above; line anchors describe the source inspected for this draft.

| Evidence | Current behavior and consequence |
| --- | --- |
| `spec-parts/32-screen-completeness.md:376`, `:504`; `specs/053-screen-completeness/tasks.md:54` | FR/AC-78 require five notifications to filter to two when the viewer is next actor, saving Muted through the project chip, and a real held email showing “Held: away”. S45 remains unchecked. |
| `src/Hub.Api/Features/Notifications.cs:21`, `:41` | `NotificationRow` has historic notification facts, no current next-actor/follow/delivery metadata or business row version. GET applies existing filters, counts, then pages by UpdatedAt desc / Id desc. |
| `src/Hub.Api/Features/Planning.cs:597` | `VisibleNotifications` enforces the recipient, visible project and the extra PlanningEntry scope. Replacing it with a plain project visibility query would broaden access. |
| `src/Hub.Api/Infrastructure/RecordContextReader.cs:23`; `src/Hub.Domain/RecordContextRules.cs` | The shared reader hydrates explicit same-project facts and the pure rules derive next actors/actions. Public context reads are currently gated by `screen_patterns`; that wrapper cannot simply be called for independent `people_screens` behavior. ReviewPackage summaries are available as context, but the pure action switch does not establish a package next actor. |
| `src/Hub.Api/Features/Notifications.cs:92`, `:181` | Existing personal read and project-follow commands are reusable as-is. Follow PUT needs project visibility, not permission to change project work; it writes only the viewer's follow. |
| `src/Hub.Api/Infrastructure/Notify.cs:19–96` | App collapse is by event/item/actor within five minutes; mail dedup is by event/item/recipient within 24 hours. They have different identities. Ordinary Muted emission skips both app and mail, with the existing direct-assignment exception. |
| `src/Hub.Api/Data/Entities.cs:838–877` | Notifications have EmailedAt and DigestIncludedAt, but no per-email link or held/muted outcome. EmailMessage has SentAt and SuppressedAt, but no notification link. |
| Maintained `Infrastructure` and `Features` search for `EmailedAt\|DigestIncludedAt` | No matches. Those schema fields are not currently populated by the maintained send paths. Their existence is not send evidence. |
| `src/Hub.Api/Infrastructure/Email.cs:70–85` | Sender success sets EmailMessage.SentAt; current access can instead suppress queued mail. No notification delivery stamp is written. SentAt proves successful handoff to the sender, not receipt in an inbox. |
| `src/Hub.Api/Infrastructure/Digest.cs:327–365` | Existing daily-digest hold skips a person before building digest rows; no per-notification held outcome is retained. The digest is assembled from operational records, not a personal-notification delivery ledger. |
| `web/src/components/hub/notifications.tsx:74–104`; `web/src/pages/Notifications.tsx` | Opening an unread notification already submits one personal mark-read call and navigates. The mark-read button is a sibling control. The bell reuses this widget. Following has a separate explicit read command. |
| `web/src/lib/keys.ts:94–105`; `web/src/lib/record-context-navigation.ts`; `web/src/app/panel-host.tsx` | Notification invalidation roots, retained context navigation and nested Person return already exist. Preserve them; do not invent a second return stack. |

## 1. Current next actor, with server authority

The server computes the cohort from current canonical facts in the same read, after `VisibleNotifications` and existing unread/type/project filters. It does not interpret Title, Body, EventType, ActorUserId, stored LinkPath or notification recipient as next actor.

Resolve only a declared `(ProjectId, canonical ItemType, ItemId)` to that exact actual record. Do not find a record in a different project and treat its real project as authorization. Missing/deleted/mismatched/inaccessible/unsupported targets are unavailable or not assessed, not candidates for a parent, child or similarly named replacement. Projectless planning rows retain their existing planning visibility; S45 does not invent planning next-actor rules.

Reuse `RecordContextRules.Evaluate` and its fact loading, including blocking-input priority, terminal precedence, inactive/missing/external/unassigned states and multi-actor results. Root must expose a shared batch facts/evaluation seam independent of the context presentation flag. Do not clone OrgSettings to enable `screen_patterns`, duplicate the rule switch, store next actor on Notification, or infer a new package rule.

For a non-terminal, assessed item, Needs your action is true iff at least one current next actor is the viewer's exact active User identity. Use actor kind and current eligibility, not matching display name or a parsed role label. A known eligible viewer among multiple actors qualifies; overall Partial/Ambiguous must not erase a known eligible actor. Unknown or unavailable attribution is null, terminal/no-next-actor is false, and only true is included by the filter.

Examples that must be preserved:

- A notification addressed to Alex after a task was reassigned to Sam does not qualify for Alex.
- A blocked task can qualify for its primary blocking-input owner rather than its task assignee. ResolveInput remains an instruction to use the existing input workflow.
- A ReviewPackage notification identifies the package. It must not be turned into an arbitrary ReviewFinding notification by selecting its first unresolved finding or reading its title. An unmapped package action stays not assessed until canonical rules support it.
- Terminal items have no next action. Current archived/cancelled project history can remain visible while business commands are unavailable.
- Attribution can still name the viewer on visible read-only work. Keep that attribution; do not call it command authority. Show View item until the canonical detail supplies an allowed action.

### Paging and freshness

Proposed additive query: `GET /api/v1/me/notifications?needsYourAction=true`, combined with all existing filters/page/pageSize. Root should bind the new control as raw optional text and evaluate it only when `people_screens` is on, so a stale malformed new control cannot change legacy flag-off behavior. When on, absent/empty/false means the ordinary list, true enables the cohort, and other non-empty values receive the established query-validation error. Reset page to one when the control changes.

The filtered total is the number of matching notification rows before pagination, not the sum of their collapse Counts and not the matches on a fetched page of 50. Preserve UpdatedAt desc / Id desc order, page size limits and the existing page envelope.

Proposed exact implementation: under a read-only consistent snapshot, scan candidates in bounded keyset batches (initial batch size 200), deduplicate typed canonical targets per batch, hydrate their facts in batches and evaluate once per target. Count all matching rows while retaining only the requested page. Do not instantiate a reader per row or silently cap the candidate scan. The repository's existing safe read transaction/provider pattern must be chosen by root. Performance on a large notification history is a release check; if it is inadequate, optimize the shared batch read, not the truth of the count. An equivalent set-based path needs parity tests against the pure rules.

Current target state can change without emitting a new personal notification or changing its pulse. Pulse polling alone is insufficient. Invalidate active notification action reads after relevant canonical mutations and use established active-page/focus/reconnect freshness behavior. Do not broadcast private read/follow state as project activity.

## 2. Typed additive read contract

Root owns the maintained DTO and golden/type generation. The proposed new property on NotificationRow is nullable `screen`, omitted entirely when `people_screens` is off. Preserve all old fields, their meanings, ordinary counts/order and bell behavior.

Suggested exact conceptual shape, to freeze as named C# records and generate into the client:

```text
NotificationScreenContext
  assessedAt: timestamp
  currentItem: NotificationCurrentItem
  follow: NotificationProjectFollow?          # null without a visible project
  delivery: NotificationDelivery

NotificationCurrentItem
  assessment: Assessed | NotAssessed | Unavailable
  needsYourAction: boolean?                  # null means not assessed/unavailable
  terminal: boolean?                        # not false when unknown
  nextAction: canonical action code?         # no new workflow vocabulary
  nextActors: RecordContextPerson[]          # same known User/kind/state semantics
  target: canonical kind/id locator?         # authorized actual FK only

NotificationProjectFollow
  present: boolean
  level: (AllActivity | MyItemsOnly | Muted)? # null when no follow row exists
  source: canonical follow source?

NotificationDelivery
  state: Emailed | HeldAway | NotSentMuted | Queued | NotSent | NotRecorded
  occurredAt: timestamp?                     # actual recorded outcome time only
  channel: Immediate | Digest?               # only if actually associated
  coverage: Complete | Partial | Unknown
  reasonCode: safe delivery reason code?     # e.g. recorded access suppression, never raw errors
```

These are proposed wire names, not implemented types. If the existing shared person/target DTO cannot represent these facts without disclosure or inappropriate coupling, root provides an equivalent named DTO. No local shadow DTO, `any`, generated-file hand edit or serialized entity.

`target` is a locator, not an authorization token. Do not include a generic `canAct=true` derived from next actor. The original item loader owns current command availability and the command's actual version. Do not confuse a notification's UpdatedAt/read state with a Task RowVersion or a nested finding's version. Unsupported kinds may still retain the legacy historic row; null metadata must not remove it from the ordinary list.

Add query key input for the Needs your action control. Preserve actor/flag scoping and clear incompatible cached flag-on data on a flag/person change through established patterns. No follow/read state for another person, mail address/body, raw send errors, private leave reason or hidden project names in this DTO.

## 3. Follow, read and business-action behavior

| User intent | Reused command/read | Exact boundary |
| --- | --- | --- |
| Change this project's follow level | `PUT /api/v1/projects/{id}/follow`, `{ level }` | Use the row's actual authorized ProjectId and one of the three existing codes. It writes only the viewer's ProjectFollow and sets Source=Manual. No new business permission, reason or row version. |
| Mark this notification read | `POST /api/v1/me/notifications/read`, `{ ids: [notificationId] }` | Existing own-visible-unread query is authority; repeating has no additional read marker effect and returns zero newly marked. No business version or reason. |
| Mark all notifications read | Existing `{ all: true }` command | Preserve explicit existing scope and behavior. Do not silently redefine it as current page or Needs your action cohort. |
| Open/Act on a row | Existing item route/panel/resolver | Fetch exact current canonical detail first. The normal permission, version, readiness and reason UI owns the subsequent business command. Row opening itself executes no business transition. |
| Open follow menu, Person, filters, project context; render or refresh | Reads/navigation only | No notification mark-read, Following read, work acknowledgement, digest advancement, email send or preference mutation merely from these interactions. |

### Follow semantics and failures

The three labels map to AllActivity / MyItemsOnly / Muted; they do not add a fourth stored level. An absent follow row is explicitly absent, with no checked level fabricated from a default. Choosing one creates the existing Manual follow. Do not replace Muted with DELETE/unfollow.

A visible read-only project's personal follow stays editable under the existing command. If access is lost, preserve the command's generic refusal and refresh; do not retry through an elevated route. While pending, disable that row's duplicate submission and keep the menu's state coherent. On error retain the previous selection and show the existing error treatment.

Follow PUT is logically repeatable for the same level, with existing last-writer-wins behavior; it is not a versioned business command or a new receipt protocol. UpdatedAt can change on retry. Preserve the canonical command's new-follow LastSeenAt initialization and an existing follow's LastSeenAt. Do not add a separate Following-read call or change work acknowledgement markers.

After success invalidate notification roots, follow consumers and Preferences via their existing helpers/query roots. Refresh related pulse/count reads as needed; do not assume the notification list invalidation also refreshes Preferences. Do not optimistically claim mail was suppressed: changing follow does not prove what happened to an earlier row or cancel already queued mail. Existing direct-assignment exceptions remain.

### Read and navigation semantics

The normal primary Open/Act intent preserves the existing one mark-read invocation for an unread row; compose with `useOpenNotification` or factor that same behavior once. Do not wrap it in a second mark-read, S10 seen-state or Following-read call. A read request and canonical navigation may finish independently, as today; a successful read is not a successful business action. The explicit mark-read control stays available as its own sibling.

Refresh notification/unread consumers after mark-read. A row can disappear under Unread while its detail/Person panel is open. Its return must retain URL/filter/page/scroll and use a stable nearby list or heading fallback when the original trigger no longer exists; do not resurrect an already-read row to restore focus.

### Current command authority, version, reason and idempotency

The item's existing workflow must re-authorize on execution, even if the actor hint, route, notification or loaded form is stale. Preserve current project status, membership, discipline, actor role, ownership, independence, readiness and terminal-state rules. No new “notification permission” bypasses them.

For example, Task transition already uses `POST /tasks/{id}/transition`, `Http.CheckVersion` and canonical ApplyTransition; detail exposes allowed transitions and reason requirements. Review/finding commands use their own current nested RowVersion, RequestId and atomic Coordination command path. A parent's If-Match is not a substitute for its finding's version. Other kinds retain their original command contracts; no normalized generic write payload is introduced here.

Require the current command's version/precondition and required non-empty user reason exactly where that command requires them; do not manufacture a reason from notification text. Keep the existing refusal payload and accessible error handling. A version conflict reloads the current detail and requires a fresh deliberate attempt; do not automatically select a new version and resubmit. Missing/inaccessible records produce the canonical generic unavailable result without leaking foreign details or substituting another item.

Reuse each command's existing idempotency mechanism: the same RequestId or Idempotency-Key for retries of the same submission, not a fresh key for every network retry. The general POST middleware and atomic Coordination/Handoff/S10 command receipts have different replay/authority contracts; do not assume the middleware applies to every endpoint or duplicate its effects. A new user-confirmed attempt after refreshed facts follows that workflow's normal new-attempt rules. Follow/read keep the semantics above and do not acquire business receipts. S45 does not repair or broaden unrelated command idempotency behavior.

## 4. Truthful delivery line and the provenance seam

The delivery line describes this notification's recorded mail outcome, not the current global preference. Historic unknown is visible as “Delivery not recorded” using root-owned copy; it is not zero, muted, failed or held.

| Line | Evidence required | Evidence that is insufficient |
| --- | --- | --- |
| Emailed 07:00 | Stable association of this notification/event coverage to an EmailMessage with actual SentAt after successful send; display in the established user/org time convention. | CreatedAt, scheduled digest time, current Email=true, an unlinked SentAt, non-null schema field with no verified producer. |
| Held: away | Recorded hold outcome for the exact associated email/event under the existing applicable policy, with time and enough identity to survive notification collapse. | Person has zero availability now; today's global digest hold; private leave reason; an email not sent yet. |
| Not sent: muted | Actual captured mute decision associated with the row/event, distinguishing direct exceptions and collapse coverage. | User's current project follow is Muted; a missing mail row; a parsed dedup string. |
| Queued | Stable associated unsent outbox row, not held or suppressed. | A historic notification for which any similarly titled email happens to exist. |
| Not sent | Stable associated recorded suppression/other non-send outcome with a safe reason. | Inventing a delivery failure or revealing hidden project/transport details from a raw error string. |
| Delivery not recorded | No trustworthy linkage/outcome or unknown historical coverage. | Never use it to claim a known successful/held/muted outcome was verified. |

Observed blockers to a truthful renderer:

1. `Notifier.Send` independently creates/collapses in-app rows and deduplicates mail. Notification.CorrelationId can be overwritten on collapse; a send for one event must not imply every event in Count was emailed.
2. Current ordinary Muted handling returns before making the notification row. A historic row can later be muted, and direct assignments can still send. Therefore many existing rows cannot acquire a truthful NotSentMuted label by recomputation.
3. `EmailJob` is where actual SentAt becomes known and can instead record access suppression. GET must not queue, retry, send, cancel or alter that process.
4. The current digest hold stores no row-level held-email evidence. FR-SCR-80/S47 and existing 053-D10 are the immediate-email hold boundary. S45 must not silently implement that policy or pretend that a skipped digest proves a particular notification email was held.

Root-owned minimal shared seam: retain stable notification/event-to-email provenance and captured delivery decisions at the existing producer/send boundaries, including collapse coverage. Capture actual mute/hold decisions where they happen, sent outcomes after sender success and the existing suppression outcome without exposing transport errors. Reuse existing Notify/Email/Digest producers; do not add a second notification engine. The outbound EmailMessage queue is distinct from the general OutboxEvent/live-event mechanism; an outbox event alone is not mail evidence. Root chooses the smallest adequate maintained schema/read model and owns any migration. Leave historical unlinked rows unknown; no title/time/dedup-guess backfill. Complete coverage permits an unqualified Emailed line; partial coverage must explicitly say only some activity was emailed, with unknown coverage retaining the qualification rather than claiming the entire collapsed row was sent.

No producer policy changes are authorized by this document. If S47 has not supplied actual held-email provenance, a partial S45 UI can show recorded outcomes/unknown, but AC-SCR-78's Held: away clause remains UNPROVEN and S45 must remain unchecked. A mocked held row alone proves rendering, not a real hold lifecycle. This dependency is not a new decision or a change to 053-D10's status.

## 5. Interaction, Person return and 375px

Keep the notification row as sibling controls: primary item opener, project navigation chip, project follow-menu trigger, actual Person trigger(s), explicit mark-read. Do not nest buttons or put these controls inside a row-wide button/link. Opening a menu or Person must not bubble into primary Open/read.

Use only actual ActorUserId/next-actor User FK for Person. System, external, missing and unknown actors remain appropriately labelled text; no guessed directory match by name. Give every trigger a stable unique focus key containing notification ID, role, target kind/ID where applicable and UserId, e.g. `notification:{notificationId}:next-actor:{kind}:{itemId}:{userId}`. The same person on two notifications, two roles or a collapsed target must still have distinct triggers.

Reuse `captureRecordContextOrigin`, `recordContextReturn`, existing Person panel and canonical item panel. Preserve personal/Following tab, unread/type/project/action filters, page, scroll and any existing parent return chain. Closing Person returns to that exact trigger if it survives; otherwise the documented visible fallback. No direct DOM focus injection to hide a broken return chain.

At 375px, titles and delivery text wrap without page overflow, the project/follow controls remain reachable, hit targets and focus rings stay visible, and the row has a clear reading order. Use existing popover/sheet patterns and wait for their actual closed/open state in browser tests. Do not hide required text solely to make the screenshot fit. Root owns translations, type generation and final visual review.

## 6. Proposed disjoint implementation ownership

No source loan is granted by this table. Root must freeze contracts and issue exact loans before authors edit.

| Owner | Proposed files/seam | Required handoff |
| --- | --- | --- |
| B backend | New `src/Hub.Api/Features/NotificationScreenReads.cs`; new `tests/Hub.Tests/Api/NotificationScreenTests.cs` | Pure/read-only additive enrichment + exact cohort/paging tests, consuming root's canonical batch/provenance seam; no new command engine. |
| One UI author selected by root | New `web/src/pages/notification-screen-row.tsx`; new `web/tests/notification-screen.cjs` and `.fixture.cjs` | Generated types only; filter, row follow/delivery, retained return, refusal/off/375 evidence scripts. |
| Root shared API | Existing `Features/Notifications.cs` DTO/mapping/query wiring; RecordContextReader/fact batching and DTO seam | Preserve legacy endpoint; independent people/screens flags; exact authority and counts. |
| Root shared delivery/schema | Existing Notify/Email/Digest, Notification/Email models and migration if required | Stable evidence/read model and collapse coverage; no inferred historical states or policy expansion. |
| Root shared web | Existing Notifications page/shared bell widget, keys, registrations/panel seams, Text/i18n, generated API types and test registration | Mount only the centre additions; preserve bell/Following and adjacent Person behavior. |
| Root verification | All generators, EF, builds, tests, disposable runtime, browser and final evidence | Frozen source hashes, focused checks below, candid partial/unproven gates. |

Dependencies: freeze named read types and canonical facts seam first; backend/UI may then author disjoint new files. Root integrates existing-file mounts, provenance and cache updates after authors HOLD. Root alone executes once all relevant source is frozen. Do not restart old helpers or add concurrent reviewers without the shared capacity allocation.

## 7. Focused acceptance and regression checks for root

These checks are proposed and UNRUN by B; this document contains no runtime proof.

1. **AC-SCR-78 cohort:** five actual personal notifications, two exact active viewer next actors, expected two and exact total. Also cover changed assignee, viewer among multiple actors, blocked-input owner, terminal, external/inactive/unknown, unmapped ReviewPackage and missing/mismatched targets. A next-actor viewer on read-only work still has no granted command. Use actual canonical facts, not a fixture-only boolean implementation.
2. **Pagination/privacy/no writes:** more than 50 candidates with matches across page boundaries, filter combinations and collapse Count>1. Exact row count/order under the chosen snapshot; foreign recipient/project/planning-scope rows absent. Capture entity/audit/version/read/follow/outbox state before/after repeated GET/filter calls and prove no mutations, acknowledgements or email side effects.
3. **Independent flags:** people on / screen_patterns off still computes the S45 cohort through shared facts. people off / screen_patterns on and both off retain legacy JSON, order, counts, controls and malformed-new-filter behavior. No new row metadata when off; legacy bell, primary open/read and Following remain intact.
4. **Follow/read:** choose each real level; absent→Manual, existing Manual retained, repeat same-level logical result, two-person isolation and lost project access. Preserve an existing LastSeenAt and canonical initialization on new follow; no extra Following/S10 acknowledgement. Prove direct-notice exceptions unchanged and a later mute does not rewrite history. Mark-read retries only affect own visible unread IDs; explicit All preserves its old scope. Assert exact request counts for open versus Person/menu/filter.
5. **Canonical action/refusal:** row opens exact current target. Exercise current allowed command through its normal UI, stale version conflict, missing required reason, role/access loss, terminal/readiness refusal and existing duplicate-submission receipt behavior. No auto-resubmit with new version, foreign target fallback or second command path; compare business/audit/notification side effects with normal canonical workflow.
6. **Delivery lifecycle:** actual producer + fake sender success proves recorded link/SentAt→Emailed, pending linked mail→Queued, real captured policy decision→Muted/HeldAway only when applicable, access suppression does not display Emailed, historical unlinked→NotRecorded. Collapsed mixed coverage never claims all events emailed. Real sender/inbox delivery is not required or claimed by a local sender stub. S47 held-email acceptance remains a separate dependency until implemented and verified.
7. **Person/keyboard/375:** two rows for the same person and item, multiple roles, primary action versus Person/menu propagation, Escape and return through item→Person→notification, unchanged URL/query/page/scroll, removed-row fallback and generic unavailable Person. At 375px, inspect wrap/overflow, all required controls, keyboard order and visible focus. Preserve assertions when diagnosing popover/sheet timing.
8. **Required root integration:** DTO golden diff/type generation and plain recheck; focused API contract/visibility/paging/read/follow tests; affected web type/lint checks; new fixture type inventory; focused browser plus adjacent notification bell, Following and Person return checks. Run a broader suite only for a discovered regression or project requirement. Record exact executed commands and evidence in root-owned verification; do not label this checklist PASS by inspection.

## Handoff

READY: this bounded contract identifies current command reuse, shared server-authority/facts and delivery-provenance gaps, additive read/UI shape, ownership and focused checks.

HOLD: all application/tests/schema/flags/runtime remain untouched by B for this task. No build, test, EF/generator, browser, service, dependency, commit, push or deploy operation was run. No S45 completion, local pass or Accepted claim is made. Root review is required before any implementation loan; a real held-email evidence path remains necessary for full AC-SCR-78.
