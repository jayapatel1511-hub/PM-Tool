# G60 — notice delivery core interface proposal

2026-11-02. **READY/HOLD: D documentation only; Root reconciliation/freeze remains required.** This is the only file authored in this task. G59 application/test sources remain HOLD. Root reports G59 locally closed at43/49, with70 .NET,33 client and8 browser checks passing; those are coordinator results, not checks run by D. This document performs no app/test edit, compiler, parser, build, generator, browser, scheduler, job, provider, mail, flag, commit or deployment operation. It does not authorize source work or mark S45/S47 Accepted.

Authoritative baseline: [G58 model contract](g58-notification-delivery-model-contract.md), SHA256 `d707e164ca57a4b96a0a675b6c9fdcf997c69dc4405464316968797f0b60c735`; [G59 addendum](g59-bulk-summary-delivery-addendum.md), SHA256 `3e3b26e0c4ae836895b7131f60674bb083945717992f4b8f2510aed30396264b`; and G59's actual six-file preference/lock/proof foundation. Later explicit Root freezes take precedence over this proposal. Compilable *shapes* below are documentation specimens with throwing bodies, not implementations or claims that compilation ran.

## Preserved full next-wave handoff

### Outcome and disjoint proposed ownership

The smallest complete next wave is one dark integration covering trusted source capture, current-policy transitions, bounded digest composition, pinned email claim/completion, own in-app reminders and truthful notification delivery display. Preferences remain unmounted until those paths and their verification integrate. Reuse existing NoticePreferences, NoticeRecipientLock, NoticeDeliveryProof and two private Intent/Slot tables. Do not recreate NotificationNoticePreferences.cs, introduce a bus/attempt table, add a second policy polling job or create another rollout flag.

| Proposed owner | Exact source responsibility, subject to the next Root loan |
|---|---|
| Root | Existing Notify.cs; TaskBulkCommands.cs; Planning.cs; WeeklyPlanning.cs grouped scope; EvaluationService.cs notice failure/commit boundary; Keys.cs/HubDb.cs buffer/outer flush and model guards; explicit actual producer transaction wrappers; Notifications.cs/NoticePreferences.cs locked policy hooks; Modules.cs; Operations.cs; NotificationScreenReads.cs; Text/en.ts/cache/generated/migration inventory; final Preferences.tsx mount. Root owns PickupCommandFilter/manual transaction seams and rejection of preview/rollback contexts. |
| D core/transport | NEW Infrastructure/NotificationDelivery.cs; exclusive existing Infrastructure/Email.cs; exact reloan of NoticeDeliveryProof.cs for closed rendered-map encoding; NEW Api/NotificationAwayDeliveryTests.cs. Claim/complete/policy/pending sweep and membership state transitions have one writer. No edits are authorized by this table. |
| C composition | NEW Infrastructure/NotificationDigestComposition.cs; enhanced-only Digest.cs core/overload and DigestJob branch; proposed new Api/NotificationDigestCompositionTests.cs if Root loans it. Existing20-section policy is shared, never copied; WeeklySummary remains independent. C owns its interface proposal. |
| B reminder | NEW Infrastructure/TimeEntryReminderJob.cs and Api/TimeEntryReminderTests.cs. Existing JobHost is the scheduler; Root registers the reviewed one-minute IJob. B owns reminder eligibility and persistence proposal. |
| A UI | Exact notification-row-actions.tsx delivery-line hunk; existing unmounted notice-preferences component only as needed; finite notice-delivery browser proof/fixture and existing notice-preferences proof after Root mounting. Root freezes additive public delivery DTO/locales first. No manager delivery dashboard, proof JSON, claim details or mail bytes. |

G59 governs initial held rendering: at most10 explicit parent occurrences, no grouping, all20 ordinary sections off means HeldNoDigest, and no new held-section switch. Bulk raw obligations are not additional digest parent lines. Preserve3 compositions,8 aggregate digest sender reservations,8 pinned-immediate reservations,819202-byte complete Progress reservation inside its independent1 MiB allowance,2 MiB immutable Origin and64-byte envelope overhead. Persisted reminder kind is TimeReminder.

### Required source/transaction order

1. The actual source-command owner opens/joins one ReadCommitted transaction across all business saves and all canonical Send calls. Acquire all source business/project/task locks in established canonical order before the complete sorted distinct recipient set. UserSetting/slot/email row locking, if needed, follows recipient locking. No helper begins or commits a source transaction or takes a new business lock after a recipient lock.
2. Root's current integration candidate is scoped full raw-call buffering and **one actual-owner flush** after business saves/hooks discover all recipients. Nested Tx.Run must not flush. Manual transactions flush explicitly before commit. Explicit actual producer wrappers are required; a blanket endpoint filter is rejected because AllocationPreviewFilter deliberately rolls back and Admin settings invalidation has commit-sensitive timing. Preview/rollback commands must discard their buffer and create no notices.
3. Ordinary capture must not acquire recipient keys at each Send: later side effects can discover an earlier-sorting recipient. TaskBulkCommands remains its dedicated raw capture/immutable receipt path. Its existing command→project→task locks precede Stage; replay returns before capture. Validate every recipient's full proof/reserved future Progress before any app/Intent/mail/receipt commit. Later email lineage never rewrites the command receipt.
4. Planning source kind/scope/app projection must be final before Notify. Wrap create/edit/move/visibility/delete independently of optional POST idempotency headers. A move collects old/new affected recipients first. WeeklyPlanning.Apply publishes groups of up to500 entries: all actual group planning/project scopes are required, including entries with null ProjectId; first.Id alone is not the summary's access proof. This ordinary planning group is not the200-task/4096-obligation bulk protocol.
5. Preserve legacy unlinked planning withdrawal rewriting. Enhanced withdrawal observes terminal source refusal, including held work without mail and while the feature is off; current projections redact/hide unauthorized content. Queued immutable linked bytes/scope/membership never change. A safely unsent mixed digest can recompose permitted members within its original window/budgets; unresolved InFlight/Uncertain membership cannot release. Actual prior acceptance remains historical fact.
6. Per-user digest transaction: fresh policy under recipient lock; find/create unique slot; capture schedule and immutable From/Through before watermark advance; build canonical body and exact output-dependency scope; queue email/reservations/watermark atomically. Safe recomposition reuses the same window, versions and aggregate attempts. Held overflow remains pending; ordinary same-item rows/counts consume no held ID. Monday-away creates no weekly slot or advanced window; existing Monday slot retry Tuesday is allowed, new Tuesday catch-up is not. WeeklySummary is unchanged.
7. Existing EmailJob excludes every enhanced marker before legacy Take100 and uses separate bounded100 enhanced/100 unqueued-policy scans. Pending checks are max8/local day with1/2/4/8/16/32/60-minute backoff then next-day parking. Claims reserve counters/token/version/time under a current recipient transaction; immutable I/O occurs outside it. Matching completion reacquires the key. Default unknown sender, Log simulation, real provider acceptance and uncertain/failure/exhaustion stay distinct. No automatic uncertain resend is introduced.
8. Reminder uses a current own recipient transaction and unique TimeReminder/Monday slot, app notification only, no Intent/email/quota/deputy/other-person hours. Current week only; choice edits cannot reemit. One shared low-level resolver implements Clock.Zone fallback, first-valid DST gap and earlier UTC ambiguity. Existing own-Time/ReadOnly guards remain.

### Source-backed gaps and verification seams

- EvaluationService.cs:163–171 owns a transaction/project lock but catches all Notices exceptions and then commits. Enhanced capture/proof failure must propagate/rollback; legacy off behavior is preserved deliberately. The unrelated later nightly Home RepeatableRead snapshot starts after evaluation/notice completion.
- HubDb.SaveChanges may take business project/task locks. Therefore per-Send recipient acquisition before a later source save is unsafe unless Root has already obtained the full business lock set. One post-business-save flush is feasible only if no pending business mutation/late producer remains and its private/app save cannot recurse into another business lock acquisition.
- Digest.Build's ordinary updates/staff queries have only lower timestamps; the enhanced core adds <=WindowThrough. Its current RequiredProjectIds is all visible pids; enhanced scope includes exact sources of rows **and displayed Total/+n more/update counts**, plus rendered planning scopes. Do not silently use capped rows as the whole output scope.
- Existing channel/follow writers now lock but still need current-effective-post-save policy observation, including DELETE/default changes and mixed bulk obligation selection. A parent representative EventType is insufficient. Digest disable/all-sections-off changes eligibility to HeldNoDigest rather than reviving or automatically mailing held work.
- Root's current model guard allows accepted/simulated/unknown/exhausted parent→AccessRemoved with email provenance retained. Keep historical accepted bulk progress distinct from current source refusal; AccessRemoved is not evidence that old mail was never accepted.
- Reviewed contracts permit only explicitly admitted same-payload uncertain retries, but define no public trigger/lease. Proposed first complete wave parks/reports uncertainty, allows matching late completion and exposes no retry route. A future retry admission needs its own Root freeze.

Future D provider seams: source rollback, app-off/email-on, real collapse ordinals, exact24h legacy/held/bulk suppression and no expiry extension, complete ordered locks, opt-out races, mixed event/direct/scope cohorts, planning withdrawal matrix,100 dormant+1 legacy starvation, persisted policy backoff, concurrent claims/stale completion, pinned transport and generic Task throw/cancel/orphan uncertainty. C covers immutable window after watermark advance, newer updates excluded, output contributor scopes,10-parent overflow/no grouping, ordinary nonconsumption, safe recompose/carry-forward, Monday/Tuesday and watchdog agreement. B covers repeated/concurrent reminders, DST/current-week/edits/off/ReadOnly/no-email. Root runs existing TaskBulk/Digest/PlanningPrivacy/Operations and foundation tests plus new model guards; A supplies finite actual-SPA preference/delivery/privacy proof. Fakes establish local behavior, not hosted mail. S45/S47 remain open until the complete wave is implemented and verified.

## Canonical shared names: D owns these specimens in the proposed core file

Following Root's final visibility freeze, shared CLR records/classes/methods are public and opaque constructors are internal. They are application infrastructure, never HTTP/schema/registry records merely because they are public CLR. C's shared payload and B's due reader use the same convention; no InternalsVisibleTo or reflection workaround is proposed. Root creates trusted inputs from canonical handlers. No HTTP caller supplies actor, source scope, app IDs, channel decisions, clock, claim purpose or policy witness.

The code blocks use current Hub.Api/Hub.Domain types and ImmutableArray. Opaque contexts/preparations have internal constructors. Their factories bind actual HubDb identity and CurrentTransaction.TransactionId; every operation rechecks that binding and the held recipient. Contexts never outlive their transaction or cross into sender I/O. The bodies shown are intentionally nonfunctional documentation placeholders.

```csharp
using System.Collections.Immutable;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;

namespace Hub.Api.Infrastructure;

public sealed record NoticeRequiredScope(
    ImmutableArray<Guid> ProjectIds,
    ImmutableArray<Guid> PlanningEntryIds);

// Shared D-owned value; C consumes it without a duplicate declaration.
public sealed record NoticeDigestWindow(DateTimeOffset From, DateTimeOffset Through);
public sealed record NoticeLocalSchedule(
    string OrgTimeZoneId, DateOnly RequestedDate, TimeOnly RequestedTime,
    DateOnly EffectiveDate, TimeOnly EffectiveTime, DateTimeOffset ScheduledAtUtc);

public sealed class NoticeRecipientContext
{
    internal HubDb Db { get; }
    public Guid UserId { get; }
    public Guid TransactionId { get; }
    internal NoticeRecipientContext(HubDb db, Guid userId, Guid transactionId)
        => (Db, UserId, TransactionId) = (db, userId, transactionId);
}

public sealed record NoticeRecipientPolicy(
    Guid UserId, bool IsActive, string EmailAddress, OrgSettings Settings,
    NoticePreferencesRead Preferences, bool DigestEnabled, bool HoldDigestWhenAway,
    string DigestTimeLocal, ImmutableArray<string> SectionsOff);
public sealed record NoticeDayAvailability(string State, bool? IsAway);

public enum NoticeSourceKind { Work, PlanningEntry, PlanningWithdrawal, PlanningGroup }
public enum NoticeCaptureMode { Ordinary, TaskBulk }
public sealed record NoticeAppReference(string? ItemType, Guid? ItemId, string? ItemKey, string? LinkPath);
public sealed record NoticeRawSource(
    NoticeSourceKind Kind, Guid? ProjectId, string? ItemType, Guid? ItemId,
    string? ItemKey, string? LinkPath, NoticeRequiredScope Scope, string EmailKind,
    NoticeAppReference AppReference);
public sealed record NoticeRawContent(
    string AppTitle, string? AppBody, string MailSubject, string MailBodyText,
    string? MailBodyHtml);
public sealed record NoticeRawCall(
    int CallOrdinal, Guid CorrelationId, string EventType,
    ImmutableArray<Guid> RecipientIds, Guid? ActualActorId,
    ImmutableArray<Guid>? AssignedTo, NoticeRawSource Source, NoticeRawContent Content);

public sealed class NoticeCaptureBuffer
{
    internal NoticeCaptureBuffer() { }
}
public sealed class NoticeCaptureSession
{
    internal NoticeCaptureSession() { }
}
public sealed record NoticeCaptureDecision(
    int CallOrdinal, Guid UserId, bool AppEligible, bool EmailEligible,
    bool IsDirectRecipient, string Disposition, string? ReasonCode);
public sealed record NoticeCaptureEvaluation(ImmutableArray<NoticeCaptureDecision> Decisions);
public sealed record NoticeAppBinding(
    Guid UserId, int? CallOrdinal, Guid NotificationId, int CountAtCapture);
public sealed record NoticeBulkCaptureBasis(
    Guid CommandReceiptId, Guid OriginalActorId, ImmutableArray<NoticeChangedTask> ChangedTasks);
public sealed record NoticeCaptureResult(
    ImmutableArray<Guid> IntentIds, ImmutableArray<Guid> EmailIds);

public sealed record NoticeHeldCandidate(
    Guid IntentId, Guid UserId, DateTimeOffset CreatedAt,
    string Subject, string BodyText, string? LinkPath, NoticeRequiredScope Scope,
    NoticeBulkRenderedMembership? BulkMembership);
public sealed record NoticeRenderedMembership(
    ImmutableArray<Guid> IntentIds,
    ImmutableArray<NoticeBulkRenderedMembership> BulkMemberships);

public enum NoticeDigestPreparationDecision
{ Compose, Dormant, NotEligible, InFlight, Uncertain, Exhausted, AlreadyTerminal }
public sealed class NoticeDigestPreparation
{
    public NoticeDigestPreparationDecision Decision { get; }
    public Guid SlotId { get; }
    public int NextCompositionVersion { get; }
    public NoticeDigestWindow Window { get; }
    public ImmutableArray<NoticeHeldCandidate> Held { get; }
    internal NoticeDigestPreparation(NoticeDigestPreparationDecision decision, Guid slotId,
        int nextCompositionVersion, NoticeDigestWindow window, ImmutableArray<NoticeHeldCandidate> held)
        => (Decision, SlotId, NextCompositionVersion, Window, Held)
            = (decision, slotId, nextCompositionVersion, window, held);
}
public sealed record NoticeDigestReservationResult(
    Guid SlotId, int CompositionVersion, Guid? EmailId, string State,
    ImmutableArray<Guid> ReservedIntentIds);

public enum NoticePolicyChangeKind
{ EventChannels, EventReset, Digest, Follow, FollowReset, EnhancedPreferences, PlanningWithdrawal }
public sealed record NoticePolicyChange(
    NoticePolicyChangeKind Kind, string? EventType = null,
    Guid? ProjectId = null, Guid? PlanningEntryId = null);
public sealed record NoticePolicyChangeResult(
    int SuppressedIntents, int HeldIntents, int SuppressedEmails,
    ImmutableArray<Guid> SlotsNeedingSafeRecomposition);
public sealed record NoticePolicySweepResult(int Examined, int Changed, int Parked);
public sealed record NoticePendingCandidate(Guid IntentId, Guid UserId, DateTimeOffset NextCheckAtUtc);

public enum NoticeClaimPurpose { Initial, KnownFailureRetry }
public sealed record NoticeEmailEnvelope(
    string ToAddress, string Subject, string BodyText, string? BodyHtml,
    NoticeRequiredScope Scope);
public sealed record NoticeSendClaim(
    Guid EmailId, Guid UserId, Guid Token, int Version, DateTimeOffset ClaimedAt,
    string TransportKind, Guid? SlotId, int? CompositionVersion,
    NoticeEmailEnvelope Envelope, NoticeRenderedMembership Membership);
public sealed record NoticeClaimResult(string Decision, NoticeSendClaim? Claim, string? ReasonCode);
public sealed record NoticeSendObservation(
    string TransportKind, string Outcome, DateTimeOffset ObservedAt,
    DateTimeOffset? ProviderAcceptedAt, string FailureCertainty);
public sealed record NoticeCompletionResult(
    bool Applied, string Outcome, int AcceptedOccurrences, bool HasUncertainty);

// C owns NoticeDigestRenderPolicy; the persisted wrapper is D's proposed closed codec input.
public sealed record NoticeDigestProjectionFlag(string Code, bool EnabledAtRender);
public sealed record NoticeDigestPolicyDocument(
    int SchemaVersion, Guid UserId, Guid SlotId, int CompositionVersion,
    NoticeDigestWindow Window, NoticeRequiredScope Scope,
    NoticeDigestRenderPolicy RenderedPolicy,
    ImmutableArray<NoticeDigestProjectionFlag> ProjectionFlags);

public sealed record NoticeDeliveryAggregate(
    Guid NotificationId, int CurrentCount, int KnownCapturedCount,
    int RealAcceptedCount, int UncertainCount, string State, string HistoryCoverage,
    DateTimeOffset? AcceptedAt);

public static class NotificationDelivery
{
    static Exception ContractOnly() => new NotSupportedException("Documentation contract only.");
    public static Task<NoticeRecipientContext> LockRecipientAsync(
        HubDb db, Guid userId, CancellationToken ct = default) => throw ContractOnly();
    public static Task<NoticeRecipientPolicy> ReadRecipientPolicyAsync(
        NoticeRecipientContext context, CancellationToken ct = default) => throw ContractOnly();
    public static Task<NoticeDayAvailability> ReadDayAvailabilityAsync(
        NoticeRecipientContext context, DateOnly day,
        CancellationToken ct = default) => throw ContractOnly();
    public static NoticeLocalSchedule ResolveLocal(
        OrgSettings settings, DateOnly requestedDate, TimeOnly requestedTime) => throw ContractOnly();
    public static DateOnly WeekMonday(DateTimeOffset nowUtc, OrgSettings settings) => throw ContractOnly();

    public static NoticeCaptureBuffer BeginCapture(HubDb db, Guid correlationId,
        NoticeCaptureMode mode = NoticeCaptureMode.Ordinary) => throw ContractOnly();
    public static void Buffer(NoticeCaptureBuffer buffer, NoticeRawCall call) => throw ContractOnly();
    public static Task<NoticeCaptureSession> LockCaptureAsync(
        NoticeCaptureBuffer buffer, ImmutableArray<Guid> alreadyLockedProjectIds,
        CancellationToken ct = default) => throw ContractOnly();
    public static Task<NoticeCaptureEvaluation> EvaluateCaptureAsync(
        NoticeCaptureSession session, DateTimeOffset nowUtc, CancellationToken ct = default) => throw ContractOnly();
    public static Task<NoticeCaptureResult> StageOrdinaryAsync(
        NoticeCaptureSession session, NoticeCaptureEvaluation evaluation,
        ImmutableArray<NoticeAppBinding> appBindings, DateTimeOffset nowUtc,
        CancellationToken ct = default) => throw ContractOnly();
    public static Task<NoticeCaptureResult> StageBulkAsync(
        NoticeCaptureSession session, NoticeCaptureEvaluation evaluation,
        NoticeBulkCaptureBasis basis, ImmutableArray<NoticeAppBinding> appBindings,
        DateTimeOffset nowUtc, CancellationToken ct = default) => throw ContractOnly();

    public static Task<NoticePolicyChangeResult> ObservePolicyChangeAsync(
        NoticeRecipientContext context, NoticePolicyChange change,
        DateTimeOffset nowUtc, CancellationToken ct = default) => throw ContractOnly();
    public static Task<ImmutableArray<NoticePendingCandidate>> SelectPendingAsync(
        HubDb db, DateTimeOffset nowUtc, int maxCandidates = 100,
        CancellationToken ct = default) => throw ContractOnly();
    public static Task<NoticePolicySweepResult> ReconsiderPendingAsync(
        NoticeRecipientContext context, ImmutableArray<Guid> intentIds,
        DateTimeOffset nowUtc, CancellationToken ct = default) => throw ContractOnly();

    public static Task<NoticeDigestPreparation> PrepareDigestAsync(
        NoticeRecipientContext context, Guid slotId, DateTimeOffset nowUtc,
        CancellationToken ct = default) => throw ContractOnly();
    public static Task<NoticeDigestReservationResult> ReserveDigestAsync(
        NoticeRecipientContext context, NoticeDigestPreparation preparation,
        NoticeDigestRenderedPayload payload, DateTimeOffset nowUtc,
        CancellationToken ct = default) => throw ContractOnly();
    public static Task<NoticeDigestReservationResult> CompleteEmptyDigestAsync(
        NoticeRecipientContext context, NoticeDigestPreparation preparation,
        DateTimeOffset nowUtc, CancellationToken ct = default) => throw ContractOnly();

    public static Task<NoticeClaimResult> ClaimAsync(
        NoticeRecipientContext context, Guid emailId, string declaredTransportKind,
        NoticeClaimPurpose purpose, DateTimeOffset nowUtc,
        CancellationToken ct = default) => throw ContractOnly();
    public static Task<NoticeCompletionResult> CompleteAsync(
        NoticeRecipientContext context, NoticeSendClaim claim, NoticeSendObservation observation,
        CancellationToken ct = default) => throw ContractOnly();
    public static Task<bool> ObserveOrphanedClaimAsync(
        NoticeRecipientContext context, Guid emailId, Guid expectedToken,
        int expectedVersion, DateTimeOffset nowUtc, CancellationToken ct = default) => throw ContractOnly();
    public static Task<bool> QuarantineMalformedAsync(
        HubDb db, Guid emailId, DateTimeOffset nowUtc,
        CancellationToken ct = default) => throw ContractOnly();
    public static Task<ImmutableArray<NoticeDeliveryAggregate>> ReadAggregatesAsync(
        HubDb db, Guid ownerUserId, ImmutableArray<(Guid NotificationId, int Count)> visibleRows,
        CancellationToken ct = default) => throw ContractOnly();
}
```

`NoticeDigestRenderedPayload` and `NoticeDigestRenderPolicy` belong to C's proposal and are referenced here, never independently implemented in D's file. D owns the shared NoticeDigestWindow value, scope/candidate/membership records and preparation. No core method begins or commits any transaction. SelectPendingAsync is read-only, maxCandidates1..100, and confers no send authority. EmailJob owns short per-recipient Tx.Run units over the selected IDs and calls ReconsiderPendingAsync after obtaining a context. Every mutation validates the actual same transaction/recipient and persists only through the owner's save/commit.

## Raw facts, ordinary/bulk identity and lock preconditions

Raw buffering is before active/Setup/access/mute/channel/address filtering. Recipient IDs are the actual canonical distinct nonnull routing list; no-self is evaluated against each call's ActualActorId, including its actual override, not merely the original command actor. AssignedTo=null retains today's bypass-for-all direct routing; an explicit empty array means nobody is direct. A default/uninitialised ImmutableArray is invalid. The trusted original source item and exact full scopes are retained separately from the app projection: PlanningWithdrawal may have a private source entry ID but an already-redacted public app ItemId=null. Work/email text comes from the canonical producer, with no copied HR reason or deputy content.

Root's proposed ordinary key is **`notice:v1:{CorrelationId:D}:{EventType}:{ItemType-or-none}:{ItemId:D-or-none}:{RecipientId:D}`**, with lower-case Guid-D, exact catalogue event/type text and a reserved `none` token for null. CorrelationId is the server AuditContext.CorrelationId (actually initialised to a version7 Guid), never an HTTP idempotency value or a newly generated key per retry. The suppression key stays today's **`{EventType}:{ItemId}:{RecipientId}`**, including the empty interpolation for null ItemId. Occurrence and rolling suppression are different identities.

Repeated compatible source/event/recipient calls in one correlation use the first canonical content and one actual app occurrence. Root's existing collapse is still authoritative: only a genuinely incremented Count/new row supplies a new captured ordinal; repeating the same correlation never increments twice. Core validates actual own tracked/new app row IDs and Count, not a guessed count of events/tasks. App-off/email-on has no invented app link. The buffer/session/evaluation must bind one another by actual transaction identity; Stage never trusts a free-standing caller-created Decisions array as permission evidence. Implementation must make evaluation provenance opaque or validate it against its exact originating session before applying app bindings.

**Identity conflict requiring Root resolution:** today's app collapse key includes actual actor and omits ItemType, whereas the ordinary key above includes ItemType and omits actual actor. Two calls with the proposed same key but different ActualActorId can represent two canonical app collapse identities. Distinct ItemTypes/null-item project scopes can also alias one actual app ordinal. The implementation must not silently pick a scope, union unrelated sources or violate the unique app-row/ordinal constraint. Proposed strict admission: compatible duplicate actor/source/scope retains first content; incompatible duplicates fail the whole enhanced transaction before capture commits. Root should instead extend the ordinary key with the actual actor token if its producer inventory proves legitimate actor variants. This is explicitly unresolved, not an authorised behaviour change or a request to alter the old collapse rule.

Bulk key is **`task-bulk:v1:{CommandReceiptId:D}:{RecipientId:D}`**. The dedicated bulk buffer has TaskBulk mode and uses the immutable server receipt/correlation identity. It cannot also enter the ordinary collector. Original App and Email cohorts derive from every trusted raw obligation after fresh canonical policy; one representative EventType never governs them. Exactly200 distinct changed task/project pairs and4096 obligations remain the proof limits. Replaying a command returns before either buffer/capture path and creates no new anchor/row. Ordinary planning groups keep all up-to500 canonical planning scopes and their source-defined count; they cannot be disguised as task-bulk proof to obtain subset semantics.

LockCaptureAsync starts from a complete sealed buffer, demands an actual ReadCommitted owner transaction, validates that Root's alreadyLockedProjectIds contains every canonical source project needed, then obtains the full sorted distinct recipient set via the existing exact NoticeRecipientLock namespace. Root obtains missing project/task/record locks before calling it; the array is a trusted server declaration, not an external permission proof. Capture fails before recipient locking if scope/preconditions are incomplete. The session seals the raw list and forbids late Buffer calls. No later stage acquires a previously unheld business lock. Null-project planning is explicitly classified/scoped, not a failed source lookup.

Root owns app construction/collapse. Bindings have a positive CallOrdinal for ordinary calls and null for each recipient's one bulk summary. StageOrdinary/StageBulk admit all source/Origin/progress sizes before committing app/Intent/mail side effects; neither saves/commits independently. Staged app mutations before admission are still in the same rollback boundary. Root flush performs the final private/app SaveChanges after staging, with no pending business mutation and no recursive collector flush. Controlled proof failure rolls back the entire outer source transaction.

## Fresh policy, source access and availability

ReadRecipientPolicyAsync reads persisted flags/org settings, active recipient/address and old/new effective preferences after locking, AsNoTracking or explicit fresh reload. It never accepts a SettingsStore cache, tracked pre-lock preference or current-user role snapshot as proof. Its Preferences is the existing effective NoticePreferencesRead; no ReadAtUtc accessor or default reminder weekday/time is introduced. The snapshot is transient and never reused after transaction completion or as a later sender authorisation.

**Base policy does not read capacity or Time hours.** ReadDayAvailabilityAsync is the separately lazy delivery-hold seam. It returns State=Known with IsAway=(canonical Normal>0 && Available==0), or State=Unavailable with IsAway=null. It returns no numeric hours, leave category/reference or HR data. Reduced positive/zero-normal/nonworking remain not-away under the reviewed rule; a provisional numeric canonical day is not automatically missing. It uses PersonCapacity only for the current required recipient/day when a hold policy needs it. B's reminder never calls this method or checks quota/Time records.

Core reuses EmailProjectAccess.Allowed and Planning.PlanningEmailAllowed through Root-owned current canonical scope adapters, including staged newly added membership. Ordinary scope is all-or-nothing. Task-bulk scope/prefs/mute/direct routing are evaluated per immutable obligation; narrower email text/count comes only from admitted original email keys. Initial/observed ChannelOff/Muted/NoAddress/RecipientInactive/AccessRemoved/Duplicate do not revive on later opt-in. Rolling anchors include eligible original legacy/held/scalar/bulk work at the exact inclusive24h boundary; retries, envelopes and duplicates never become fresh anchors.

ObservePolicyChangeAsync runs after the legacy/new writer's successful field save under that same key, reads effective post-save values and stages state changes before the same commit. Event reset/unknown legacy DELETE must preserve the existing no-op wire rather than gain a new400. A digest disabled/all-off change holds eligible work with HeldNoDigest, not ChannelOff; explicit immediate-hold opt-out may release only the same still-eligible occurrence. Source withdrawal observations persist while people_screens is off. A compound parent is selected by its raw event/scope obligations, not by its discriminator scalar.

Existing pinned immediate mail must not be replaced merely to reset Attempts. A safe pause/resume reuses its pinned payload/budget. An originally held occurrence with no mail may create its first payload on a currently eligible explicit hold-off release. Moving a definitely unsent held occurrence into a digest must suppress/transfer its old safe membership under the same key; uncertain/accepted membership never transfers. Address mismatch never rewrites ToAddress in place. Exact persisted reason mapping for a changed-but-nonempty address remains a vocabulary item for Root review, not NoAddress by guess.

## C-owned composition/due seam and guarded reservation

C's saved proposal is `projects-reports/requests/g60-notice-composition-interface.md`, reported SHA256 `d1d2b4c4276dba2d540e75de9fc61a7400d535923d9cf343ea05029a02a2f668`. B's reminder proposal is `work-delivery/requests/g60-own-time-reminder-interface.md`, reported SHA256 `0b86c43babcfbcde5f4907f5887fbfe8da779965274d673a6fa3165f105e5cb3`. Their owners reported these hashes; D did not execute checks on their artifacts.

Agreed names are NotificationDelivery.LockRecipientAsync/ReadRecipientPolicyAsync/ResolveLocal/WeekMonday. There is no competing WithRecipientAsync or NoticeDeliverySchedule helper. C owns NotificationDigestComposition.ReadDueAsync(context,nowUtc,ct), GetOrCreateDueSlotAsync(context,nowUtc,ct), ComposeAsync(context,slotId,nowUtc,baseUrl,ct), its result/due types and enhanced Digest.Build overload; C consumes D's shared NoticeDigestWindow. B owns TimeEntryReminderJob.ReadDueAsync(context,nowUtc,ct). Those readers create no slot/mail/settings, advance no watermark and consume no attempt. Root's Operations uses these truth sources; the legacy08:00 alert does not itself define new enhanced grace/severity thresholds.

This document's exact D reservation signatures include **nowUtc** and use **no maxParents argument**: PrepareDigestAsync(context,slotId,nowUtc,ct), ReserveDigestAsync(context,preparation,payload,nowUtc,ct), CompleteEmptyDigestAsync(context,preparation,nowUtc,ct). Max parents is the fixed reviewed10, not a caller setting. D returns NoticeDigestReservationResult; C maps it to C's NoticeDigestCompositionResult. C's earlier commented alternatives are superseded only at this proposed shared boundary, subject to Root's final freeze.

PrepareDigestAsync binds actual context/user/slot/current email/window/next version, reads current eligibility, rejects unsafe InFlight/sticky uncertainty, and returns at most10 exact candidates. Candidate bulk text/scope is regenerated from the original email keys currently admitted, never copied from the original full app title/union. No counter is spent until a valid queue reservation. ReserveDigestAsync rechecks the preparation binding, remaining3/8 budget and actual payload slot/window, encodes the immutable policy/bulk witnesses, admits exact subset membership and stages Email+intent pointers+composition/watermark together. No extra source/business lock is acquired. CompleteEmptyDigestAsync stages Empty/preparation progress without a fabricated email; a hold/disabled/unknown source is not Empty. C never directly increments Attempts or releases uncertain reservations.

Exact output scope includes every source that contributes visible rows, Total/+n more figures and update summary counts/top lines, not only capped Rows. Ordinary contributors reserve/consume no held intent. Upper cutoffs apply to ordinary timestamped updates/staff rows; dated sections remain current canonical authorised records, not reconstructed historical snapshots. Recomposition preserves From/Through after LastDigestAt changes. Safe exhausted coverage carries forward; the closure issue below must be resolved so the same old window is not carried forever.

## Closed persisted bulk rendered-map codec — existing NoticeDeliveryProof.cs only

Keep all G59 Origin/progress encoding and guards unchanged. Add only these methods to the existing codec; public types here already exist in the foundation. They are not endpoint DTOs.

```csharp
// New methods on existing public static NoticeDeliveryProof; declarations shown as signatures.
// public static string? EncodeRenderedBulkMap(Guid expectedUserId,
//     ImmutableArray<Guid> renderedOccurrenceIds,
//     ImmutableArray<NoticeBulkRenderedMembership> memberships,
//     IReadOnlyDictionary<Guid, NoticeBulkProof> renderedBulkProofs);
// public static ImmutableArray<NoticeBulkRenderedMembership> DecodeRenderedBulkMap(string? json,
//     Guid expectedUserId, ImmutableArray<Guid> renderedOccurrenceIds,
//     IReadOnlyDictionary<Guid, NoticeBulkProof> renderedBulkProofs);
```

Canonical value is a minified array of closed objects in ordinal Intent Guid-D order: `[{"v":1,"i":"intent-guid","u":"user-guid","c":"command-guid","h":"64-lowercase-sha256","k":[1,2]}]`. At most10 objects,4096 distinct sorted positive keys/object,1 MiB canonical UTF-8 total; no free text, missing members, duplicate object properties/intent IDs, foreign user/command/hash, unsupported version, null/default arrays or extra members. Guid-D/lowercase hash/canonical integer encodings match the foundation. The shape's complete count/key bound is substantially below1 MiB; a conservative2 MiB parse ceiling tolerates jsonb read formatting without treating jsonb::text length as canonical storage size. No mutable completion data enters this map.

Core obtains renderedOccurrenceIds and the exact bulk-proof dictionary from actual same-recipient intent rows under the recipient lock; the dictionary contains **every** rendered bulk intent and no other row. The codec validates equality of member/dictionary keys, their subset of all actual rendered IDs, every origin/user/command/hash, and each key's inclusion in that origin's immutable email cohort. It cannot infer whether an arbitrary GUID is bulk without that trusted row-bound dictionary. Empty bulk set requires JSON null and returns an empty typed array; a nonempty set requires the complete map. Ordinary-only enhanced digests can have RenderedOccurrenceIds=[] and BulkRenderedMembershipJson=null. The marker still distinguishes them from legacy.

Only real matching accepted Email outcomes confer actual inclusion. The map is exact rendered proof, not acceptance proof. No ordinary same-item line, cap counter, app link, hash or source command can manufacture a member. Origin remains independently<=2 MiB and Progress has reserved future1 MiB/64 envelope headroom; this immutable rendered map is admitted before queueing and never grows after I/O.

## One necessary ordinary digest policy witness — Root model change pending

Root chose the direction of **one nullable private `EmailMessage.NoticeDigestPolicyJson` jsonb string**, JsonIgnore and immutable once linked; Root alone owns model/migration/guard changes. It is separate from the closed BulkRenderedMembershipJson array. Bare RenderedDigestSectionCodes[] alone is insufficient for the actual source predicates below. C's exact proposed NoticeDigestRenderPolicy contains RenderedSectionCodes, UpdateProjectIds, StaffDirectReportIds, StaffAssignmentNotificationIds, grouped ProjectId/ActivityIds for update output dependencies, and IncludesAdminReview. D wraps it in NoticeDigestPolicyDocument above with version/user/slot/composition/window/scope and a projection-flag witness; it does not duplicate C's admin-review boolean. No general predicate language is added.

Proposed closed encoding is lower camel-case version1 JSON with precisely those declared record fields and no extensions. Every known ordinary code occurs at most once and belongs to Digest.SectionCodes (max20). Held-only has an explicit empty ordinary-code array. UpdateProjectIds are distinct and a subset of output project scope; each activity group has one known contributing project and unique actual ActivityIds. StaffAssignment IDs are actual own visible stored notifications, distinct from direct-report person IDs. The admin-review line is recorded separately because it has no ordinary section toggle. Source scope also includes count-only contributors.

At claim: a currently off **rendered** code requests safe recomposition; an unchanged off/unrendered code does not. Update projects must remain AllActivity, and contributing activities are rechecked through Digest.VisibleActivity, including existing TimeEntry readership/private Calendar exclusions. Printed staff aggregate contributors must still satisfy the actual active `Users.SupervisorId==recipient` predicate; do not invent a Supervisor/ActOnStaff role gate. Own StaffAssignment notification sources use their own current scope/visibility. The admin-review contribution requires its current canonical role/flag. Field-level projection gates used to render data must still permit those fields; visibility of an ActivityId alone does not prove every projected field remains allowed.

**Proposed bounds, not yet a source freeze:** canonical witness<=1 MiB; parse ceiling2 MiB; source arrays are unique and bounded by that byte budget and exact actually-rendered/count contributors. No arbitrary prefix/truncation is allowed. ProjectionFlags codes must come from a Root-frozen closed list of actual canonical projection dependencies, with exact current booleans, not arbitrary policy expressions. Unknown/missing witness, flags or dependencies fails closed before send. Enhanced Digest requires a valid witness, including held-only; enhanced Immediate forbids it; legacy has null across this and every other enhanced marker. Admission failure rolls back the composition transaction with no email/reservation/watermark advance, preserves safe held/window coverage and reports a generic operational refusal. No witness admission happens after a provider call.

This exact byte/count refusal and projection dependency list remain a **genuine Root/C freeze requirement**. Root may choose a more compact exact canonical witness instead, but cannot approve a codes-only representation as complete against these observed privacy predicates or silently put ordinary policy into a bulk hash. D adds proposed EncodeDigestPolicy/DecodeDigestPolicy methods in the existing NoticeDeliveryProof file only after that review; C supplies the trusted actual-output policy, not a caller-entered object.

```csharp
// Proposed methods on existing public static NoticeDeliveryProof, after Root's witness review.
// public static string EncodeDigestPolicy(NoticeDigestPolicyDocument document);
// public static NoticeDigestPolicyDocument DecodeDigestPolicy(string json,
//     Guid expectedUserId, Guid expectedSlotId, int expectedCompositionVersion,
//     NoticeDigestWindow expectedWindow, NoticeRequiredScope expectedScope);
```

The decoder binds every header to the actual queue/slot row and compares canonical scope sets/window, not record/ImmutableArray reference identity. It never substitutes today's configuration for the queued witness. Its exceptions contain bounded codes only. Encoding/decoding remains in the existing codec file; no third policy-proof table, new helper file or public mail-inspection route is implied.

## Email adapter, claim/completion and recovery contract

```csharp
// Existing interface in Email.cs: additive default declaration preserves old implementations/fakes.
public interface IEmailSender
{
    string TransportKind => NoticeTransportKind.Unknown;
    Task Send(EmailMessage message, CancellationToken ct);
}

// New adapter type, SAME existing Email.cs, no new file or provider client.
public static class NoticeEmailTransport
{
    public static Task<NoticeSendObservation> SendAsync(IEmailSender sender,
        NoticeSendClaim claim, TimeProvider clock, CancellationToken ct) =>
        throw new NotSupportedException("Documentation contract only.");
}
```

Builtin Log/Smtp/Graph declare the corresponding checked kind; the actual sender object is read once for a claim and pinned on that payload. A later configuration/kind cannot relabel or replay it. The adapter validates sender kind against claim, constructs a **detached** EmailMessage copy of exactly the immutable claim envelope for the existing Task method, never passes the tracked queue entity, and never rewrites address/body/scope from current settings. Source config selects the sender only; it is not historic evidence.

EmailJob selects only truly legacy rows into the old100-row branch, excluding NoticeDigestPolicyJson as well as every existing protocol/linkage/proof/claim/outcome marker **before Take**. Its separate bounded enhanced query excludes dormant/terminal/not-due work before its limit. Malformed/unbound enhanced metadata is quarantined with no legacy fallback. QuarantineMalformedAsync requires a caller-owned transaction and an exact conditional malformed-row predicate; if a valid user cannot be established, it takes no invented recipient lock and releases no membership. A malformed marker must not suppress otherwise valid legacy security/admin mail.

For valid enhanced work, the adapter owns Tx.Run→LockRecipientAsync→ClaimAsync→commit, invokes SendAsync outside the transaction, then owns a fresh Tx.Run→LockRecipientAsync→CompleteAsync. Claims bind stored user/email/current pointer/slot/version/rendered map/witness, current flag/prefs/mute/direct/address/ACL and lazy hold-day state. No source or address override comes from the candidate scan. Claim atomically increments the reserved invocation counters before I/O and persists token/version/time on Email only. A policy-declined result has Claim=null and no envelope. Parent/slot state and candidate queries alone cannot authorise sending.

| Observed adapter result | Persisted truth |
|---|---|
| Successful declared Smtp/Graph Task | Accepted with actual local observation/provider-ack time; not inbox/read proof. |
| Successful declared Log Task | Simulated; no ProviderAcceptedAt. |
| Successful undeclared sender | Unknown terminal; no actual-mail proof or automatic replay on config change. |
| Positive verified refusal before invocation/acceptance | Failed/DefinitelyNotAccepted as appropriate; no invented provider certainty. Pre-claim refusal spends no attempt. An already reserved invocation is counted conservatively. |
| Arbitrary Task exception, cancellation after invocation or recovered orphan | Uncertain/MayHaveAccepted, first AcceptanceUncertainAt sticky. Exception class/missing SentAt does not prove no acceptance. |

CompleteAsync reloads and conditionally matches email/token/version/pinned kind/current composition/exact membership and pointers; duplicate/stale/foreign completion cannot consume newer work. It records factual Email outcome even if a later source refusal prevents restoring Intent eligibility. Actual accepted source history can coexist with current AccessRemoved; do not make a refused intent eligible again. Only rendered members transition/confirm. Sticky uncertainty survives later definite failure/exhaustion; the same logical payload never releases/recomposes after possible acceptance. Budget-exhausted uncertainty remains Uncertain with zero retries, not unsent.

NoticeClaimPurpose deliberately has only Initial/KnownFailureRetry. ObserveOrphanedClaimAsync conditions on the existing email token/version, marks conservative uncertainty and retains membership; it does not issue a replacement token, free a pointer or return an envelope. Matching late completion remains possible. There is **no automatic uncertain resend and no new public retry endpoint**. Defining an explicit future uncertain-retry admission is a separate Root freeze, not a timeout that erases uncertainty. Cancellation must still attempt factual completion with an appropriate non-request cancellation token while the process lives; a persistence failure leaves recoverable InFlight/uncertainty, not false success.

## Bounded self-only delivery aggregate for Root's existing notification mapper

ReadAggregatesAsync is a read only helper, no transaction creation/recipient lock requirement and no writes. Root passes owner=CurrentUser.Id and at most200 distinct **already-visible** selected notification IDs with their actual Count under the current read snapshot. The helper validates own actual rows/counts and source visibility; it returns nothing for a foreign/hidden/missing row, never accepts a target user from HTTP and never scans a directory. It can execute inside NotificationScreenReads' existing RepeatableRead snapshot because it does not invoke lock/mutation methods.

For each app row, union distinct actual captured ordinals1..Count. Gaps from legacy/collapsed history leave HistoryCoverage=Partial or Unavailable. A scalar ordinal counts RealAccepted only with matching own immutable rendered membership and persisted real Smtp/Graph acceptance. A bulk ordinal counts fully accepted only when every original app obligation key is included in matching accepted maps of the same source/hash/recipient; accepted proper intersection yields Partial, never a full EmailedAt. Union earlier conclusive email memberships as well as the current pointer. Equal task totals, current provider config, legacy EmailedAt/SentAt or an ordinary same-item digest line supply no evidence.

Proposed aggregate states are Emailed, Partial, HeldAway, HeldNoDigest, Queued, PendingAvailability, Simulated, Unknown, Uncertain, Exhausted, Suppressed and Mixed; HistoryCoverage is Known/Partial/Unavailable. Emailed requires full proven real coverage of **all current app ordinals/cohorts**. Any real accepted subset plus other/unknown/uncertain history is Partial; differing known nonaccepted dispositions can be Mixed. No evidence is Unknown, with Unavailable coverage. AcceptedAt is the real timestamp completing full coverage, null for an unqualified whole-row claim; partial receipts may remain private. Root alone freezes the final additive public NotificationScreenDelivery DTO/locales and maps these bounded results; raw ordinal lists/claim IDs/proofs/email bytes never go to the UI.

## Concrete outer-flush feasibility and unresolved source contradictions

Root reports its producer inventory and explicit wrappers are complete; this is not independently executed validation by D. The candidate is feasible **conditionally**, not through a blanket endpoint wrapper:

- Tasks.NewTask already owns one Tx.Run containing Team notices, all three business saves and final assignment/reviewer Send (Root re-derived Tasks.cs232–254). It is covered by the actual outer-owner flush, **not a missing-wrapper example**. Generally an implicit transaction around an early SaveChanges is not evidence of a whole-command boundary; Root's completed producer inventory/explicit wrappers supply that boundary. Nested Tx.Run only joins. App-result/receipt construction must not assume an ordinary app row exists before deferred flush. Bulk's actual staged receipt path remains separate.
- AllocationPreviewFilter deliberately rolls back and clears its tracker; its capture buffer must be transaction-bound and discarded without flush. PickupCommandFilter owns its own transaction and needs one explicit success flush before commit. Manual EvaluationService must remove/bypass its blanket swallowed-notice failure for enhanced work and flush before its own commit. Admin SettingsStore invalidation stays after its owning commit. No second commit is introduced by core helpers.
- Root reports it now detects tracked changes and permits only Notification/Email/Intent/Slot dirty rows both before LockPending/seal and after ordinary/bulk Stage, before final persistence. The flush obtains all missing source project/task locks before sealing/locking recipients once; late calls, recursive flush or newly dirtied business entries must fail/rollback. Those are authored Root integration guards, still uncompiled/unrun at this handoff; focused rollback/concurrency proof remains required. A globally hookable Tx.Run alone is insufficient evidence of the discipline.
- Root's pending capture never switches back to legacy mid-batch. Already linked rows can never fall through legacy either. If the flag is disabled before a raw command's actual capture, D proposes controlled whole-source rollback (for example409 notice_policy_changed), with no new linked rows and no omitted notices; a subsequent explicit request can follow stable-off legacy. The exact refusal outcome needs Root freeze. Do not silently drop the buffer, create new enhanced work while off or call malformed linkage legacy. Stable-off behavior and on/off wire compatibility remain required.
- Ordinary key/canonical collapse actor/type/scope mismatch is unresolved above. Compatible duplicate content is first-wins; different immutable source/scope cannot be silently selected or widened. Root's exact producer inventory determines whether adding the actual actor token or rejecting incompatible sources is necessary.
- WeeklyPlanning's full group scopes must protect the app summary too. Current Planning.VisibleNotifications checks the first PlanningEntry ItemId; private Intent scopes alone do not automatically guard the old app list/pulse/count/read path when another group entry becomes hidden/withdrawn. Root must enforce ALL enhanced grouped planning dependencies through its private linkage or another explicit scoped app seam. No narrowed group count/title or first-entry-only claim is safe.
- Ordinary policy witness/projection dependencies need the one Root-owned field/closed codec/admission review above. C also reports safe-window **carry-forward closure** needs one derivation: once a later accepted composition/slot covers an old safely unsent window, do not carry that same earlier From forever or merge uncertain windows. Root/C must freeze the exact persisted lineage test against current slot/email provenance; no invented transport receipt or new table.
- Source address mismatch needs a truthful closed refusal code; max8 intent-policy checks is not a sender attempt counter. Unknown availability stays pending; malformed/unavailable current policy never becomes Available0 or permission to send. Root finalises those code mappings and Ops enhanced due/late classifications before source loans.

## Proposed freeze disposition

The complete handoff, shared public CLR names/internal constructors, base no-capacity policy, lazy day seam, C/B ownership, capture/claim/completion signatures and closed bulk map are **READY for Root review / HOLD for source**. The one ordinary-policy witness field is the chosen direction; exact witness fields/projection list/bounds, ordinary identity collision handling, flag-transition buffer handling and safe coverage-closure derivation remain explicit reconciliation items. This document grants nobody source authority. Root alone loans files, updates CONTROL, integrates models/callers, runs checks and records local versus deployed/Accepted gates.
