# G60 proposed interface — own Time reminder

2026-11-02. **READY / HOLD — documentation only; root freeze required before source.** Canonical checkout `/Users/jaypatel/.codex/worktrees/ef94/PM-Tool/.build-052-053`, branch `codex/build-052-053`. This loan changes this document only. G59 is locally closed at 43/49; that is not packet acceptance. No application/test source, flags, jobs, mail, compiler, generator, browser or runtime checks are authorized by this proposal.

## Authority and scope

FR-SCR-80 in `spec-parts/32-screen-completeness.md` offers every user an opt-in reminder **to oneself only**, at a chosen weekday/time. The held G58 technical plan and `capacity-people/requests/g58-notification-delivery-model-contract.md` govern the existing two private tables. Its G59 bulk-summary addendum remains unchanged. This increment adds one app-only reminder job and its focused tests, not an email path, Time quota, hours inspection, deputy workflow or general exception to the no-self notification rule.

Use `NoticeDeliverySlot` with Kind=`TimeReminder`, unique `(UserId, Kind, LocalDate)`, where `LocalDate` is the Monday starting the current organisation-local week. The identity does not contain chosen weekday, time, zone or offset. An `Emitted` slot remains consumed after preference edits or cleanup of its linked notification. `NoticeDeliveryIntent` is never used by this job; no third private table or new scheduler is needed.

## Exact proposed source ownership

| Owner | Later source loan, pending root freeze |
| --- | --- |
| B | NEW `src/Hub.Api/Infrastructure/TimeEntryReminderJob.cs`: job, read-only due reader, small due result types and dedicated own-app constructor in this file. NEW `tests/Hub.Tests/Api/TimeEntryReminderTests.cs`: focused integration tests. |
| D | NEW `Infrastructure/NotificationDelivery.cs`: shared transaction-bound recipient context, fresh recipient policy and **one** shared DST resolver. D also owns that resolver's focused pure tests. |
| C | Digest composition/digest due policy only. It consumes the same low-level schedule resolver, and does not implement reminder eligibility. |
| Root | All existing source, including `Features/Modules.cs` registration, Time/notification UI link handling, event classification/locales, Operations/watchdog integration, settings/permission writers, models/guards/migration and final checks. B does not edit these files or duplicate their rules. |

Root registers exactly `s.AddSingleton<IJob, TimeEntryReminderJob>()` alongside existing jobs after the source freeze. The existing `JobHost` runs jobs through its work scope and `AuditContext.AsSystem()`. `Jobs:Enabled=false` still disables its hosted loop. A forced run must independently recheck current eligibility; bypassing `IsDue` never bypasses the feature flag or opt-in.

## B interface, in the one new job file

Namespace `Hub.Api.Infrastructure`; existing `Hub.Api.Data`, `Hub.Domain`, EF and DI imports. The following declaration skeleton is compilable against the root-frozen D types; throwing bodies are documentation placeholders, never shippable job behavior. Root's latest convention is consistent public shared CLR DTOs/methods, with opaque context/preparation constructors internal. These types do not become HTTP schemas without endpoint binding. B follows that convention for the job, due helper and result types; no new DI framework or HTTP endpoint is proposed.

```csharp
public enum TimeEntryReminderDueKind
{
    Disabled, NotDue, AlreadyEmitted, Due, Unavailable
}

public sealed record TimeEntryReminderDue(
    TimeEntryReminderDueKind Kind,
    DateOnly? LocalMonday,
    NoticeLocalSchedule? Schedule,
    Guid? ExistingSlotId);

public sealed class TimeEntryReminderJob : IJob
{
    public string Name => "time-entry-reminder";

    public bool IsDue(DateTimeOffset nowUtc, DateTimeOffset? lastSuccessUtc,
        OrgSettings settings) =>
        Schedule.Every(TimeSpan.FromMinutes(1), nowUtc, lastSuccessUtc);

    public Task<object?> Run(IServiceProvider sp, CancellationToken ct) =>
        throw new NotImplementedException();

    public static Task<TimeEntryReminderDue> ReadDueAsync(
        NoticeRecipientContext context, DateTimeOffset nowUtc,
        CancellationToken ct = default) =>
        throw new NotImplementedException();
}
```

`Run` resolves the existing scoped `HubDb` and injected `TimeProvider` from `sp`; it never constructs a real/system clock. The coarse one-minute interval is only polling policy. The authoritative flag/active-user/self-preference check is fresh inside each recipient transaction. JobHost's per-name advisory lock is an optimization; recipient serialization and the database unique constraint protect forced/direct/concurrent runs too.

`ReadDueAsync` is the **single** reminder eligibility/slot reader reused by the job and root's Operations/watchdog. It starts or commits nothing, stages nothing, creates no default settings and inspects no Time entries. It requires a live context bound to the actual recipient/`HubDb`/ReadCommitted transaction and must reject a disposed, mismatched or foreign transaction. `nowUtc` is sampled after acquiring that recipient lock, not while enumerating candidates. `Due` requires nonnull `LocalMonday` and `Schedule`; `ExistingSlotId` is null for a new slot and identifies an existing current-week Ready slot otherwise. `AlreadyEmitted` stays distinct from missing work; invalid persisted preferences/slot proof yield `Unavailable`, never an invented default or a second slot. `Disabled` covers flag off, missing/inactive user or current opt-out; an absent settings row supplies existing null=false semantics without insertion.

## Shared D seam and calendar rules

D owns `capacity-people/requests/g60-notice-delivery-core-interface.md`. Following root's latest public-CLR convention, public static `NotificationDelivery.LockRecipientAsync(HubDb, Guid, CancellationToken=default)` returns public `NoticeRecipientContext`; public `ReadRecipientPolicyAsync(NoticeRecipientContext, CancellationToken=default)` returns public `NoticeRecipientPolicy`. The lock method calls the existing exact `NoticeRecipientLock.Acquire`; it starts/commits no transaction. The public sealed context has internal `HubDb Db`, public get-only `UserId`/`TransactionId`, and an internal constructor; operations validate the actual bound database/transaction. It cannot be constructed from caller-supplied HTTP state.

Consume the exact policy accessors `UserId`, `IsActive`, `Settings` (fresh `OrgSettings`) and `Preferences` (existing `NoticePreferencesRead`). Admission checks `policy.Settings.IsFlagOn("people_screens")` and `policy.Preferences.TimeReminderEnabled`, `TimeReminderWeekday`, `TimeReminderTimeLocal`. D's other policy fields are `EmailAddress`, `DigestEnabled`, `HoldDigestWhenAway`, `DigestTimeLocal` and `SectionsOff` (`ImmutableArray<string>`); reminders do not use those as email/digest admission rules. B does not require a policy `ReadAt` property: the caller samples its injected clock after acquiring the lock. Base policy reads fresh active/flags/address/preferences only, with **no eager capacity or Time-hours evaluation**. Reminder never invokes the separate lazy availability/day-policy seam. No cached `SettingsStore`, caller-supplied policy DTO or parallel B policy definition is allowed.

The agreed low-level names and shapes, implemented **only in D's new file**, are below. Shared schedule DTO/methods are public consistently with root's convention.

```csharp
public sealed record NoticeLocalSchedule(
    string OrgTimeZoneId,
    DateOnly RequestedDate, TimeOnly RequestedTime,
    DateOnly EffectiveDate, TimeOnly EffectiveTime,
    DateTimeOffset ScheduledAtUtc);

// Static methods on NotificationDelivery:
// NoticeLocalSchedule ResolveLocal(OrgSettings settings,
//     DateOnly requestedDate, TimeOnly requestedTime);
// DateOnly WeekMonday(DateTimeOffset nowUtc, OrgSettings settings);
```

Resolve in `Clock.Zone(settings)`, preserving its invalid-zone UTC fallback. Build the requested date from current local Monday plus the saved ISO weekday minus one; parse **exact** invariant `HH:mm`, require weekday1–7 and explicit enabled=true. Never infer a weekday/time. A nonexistent wall time advances to the first valid instant at the gap boundary; do not add the gap length to the requested minute. For an ambiguous wall time, choose the minimum/earlier UTC candidate. The weekly slot, rather than an offset-specific key, prevents a second emission during the repeated hour.

`OrgTimeZoneId` persists the resolved actual zone ID. Persist the requested date and chosen exact `HH:mm` as `ScheduledLocalDate`/`ScheduledLocalTime`, with the resolved `ScheduledAtUtc`. Effective date/time are transient resolver facts; they do not justify new columns. Existing captured slot basis never changes on schedule edits or retries. No copied shortcut from the legacy digest offset calculation, no separate `NoticeDeliverySchedule` class and no reminder due reader in the digest composer.

## Recipient transaction and persistence

1. Enumerate candidates in bounded keyset batches from actual active users joined to explicitly enabled self settings. This is only a hint; do not load the full directory, create settings rows or query anyone's hours. Use a fresh DI scope/context/transaction per recipient so one failed attempt does not leave tracked pending rows for the next recipient. Set that scope's `AuditContext.AsSystem()` explicitly; JobHost's work-scope setting does not propagate to a separately created scope.
2. Open the existing ReadCommitted transaction pattern, call D's lock method, then read fresh policy and sample the injected clock. Use `ReadDueAsync` under that context. Re-read active state, flag and opt-in after waiting: cached `SettingsStore`, candidate values and a pre-lock clock are not authoritative.
3. Look up the **current** local-Monday slot before creating anything. An existing `Emitted` slot is a terminal no-op even when `ReminderNotificationId` was set null by notification cleanup. Do not delete, re-key, repair into Ready or create a second slot.
4. For no existing slot, return NotDue until the current week's chosen resolved instant is at or before `nowUtc`. Do not persist a Ready row before due. On due, stage the own notification and new `Emitted` slot in this same transaction. Capture one schedule basis, created/updated instant and notification ID. Null window/frequency, CompositionVersion=0 and ExternalAttempts=0 remain exact.
5. If a current-week Ready slot already exists, validate its complete captured basis against the shared resolver using its stored zone (an `OrgSettings` copy with that captured `OrgTimeZone`). Its immutable requested date must belong to this Monday's week; its due instant must match. Use that basis only while current active/flag/opt-in remain true; do not replace it from edited preferences. Malformed/inconsistent data is Unavailable and held. An earlier-week Ready slot is never caught up. Ordinary new execution never commits a standalone Ready row.
6. Before staging, sample the clock again if work could cross the local week boundary; recompute current due state instead of emitting the former week's reminder. Commit exactly once for this recipient. Any notification/slot/unique/database failure rolls back **both**; a later attempt can re-read and create exactly one. Cancellation cannot leave a committed notification without the receipt. No one transaction covers multiple recipients.

`Run` may return aggregate counts only (for example scanned, emitted, already emitted, not due, disabled and unavailable). No user IDs, names, hours or preference values enter JobRun details. Provider failures are surfaced to JobHost as failures; do not hide a failed write inside a successful count. Do not change digest watermarks, legacy weekly summaries, email protocol claims or existing budgets.

## Dedicated own app row and Time authorization

Proposed event code is `TimeEntryReminder`, subject to root's classification/locale freeze. Set UserId to the locked user, ActorUserId=null, ProjectId/ItemType/ItemId/ItemKey=null, Count=1, CreatedAt/UpdatedAt to the sampled injected time, and LinkPath=`/time`. Title/body are generic localized personal reminder text; no claimed missing hours, target, quota, deadline, colleague or HR facts. Bind the exact new row through `ReminderNotificationId`. No generic `Notifier.Send` self-filter change, EmailMessage, NoticeDeliveryIntent, transport or reservation is involved.

The existing unscoped notification representation is `RequiredProjectIds=null`; an empty array can be hidden by current ACL predicates. Root must freeze the event's self-only classification and real Time-link/open/read behavior. Do not fabricate a Task item or add a broad no-self exception to obtain a link.

The destination `/time` uses the existing default own scope (`TimeEndpoints.Visible`, Scope != all). Do not append another user ID, all-scope query or task/project route. Existing `Permissions.EnterTime`/`EditTime` and ReadOnly guards continue to control writes. Current self preferences deliberately allow ReadOnly; this informational opt-in does **not** grant time-entry permission. Do not add a PM/Supervisor/task-assignee requirement or infer eligibility from peer hours, roles, totals, a target or a quota. Root owns any existing-file integration needed to preserve these exact boundaries.

## Focused later acceptance — all unrun

| Case | Required evidence |
| --- | --- |
| Admission | No settings/default false, opt-out, inactive, flag off including forced Run, malformed weekday/time: no notification, slot, intent or email; no settings row created. Actual self PATCH preferences establishes eligible state. |
| Due/week | Just before/equal/after chosen instant, Sunday→Monday and delayed polling; one current-week result, next week can emit once, no prior-week catch-up. The due reader and job classify the same state. |
| DST/zone | First valid gap instant, earlier UTC ambiguity with later repetition, a non-one-hour gap and invalid-zone UTC fallback. D tests pure resolution; B integration tests fixed independent expected instants plus persisted once-only behavior. |
| Fresh after lock | Block behind a real recipient lock while opt-out, schedule edit, deactivation, flag change or week rollover occurs. The post-lock policy/time decides; pre-lock candidate data does not. |
| Identity | Same-week weekday/time edits, off/on and removed notification cannot reemit after Emitted. Unique key stays user/TimeReminder/localMonday. Valid existing Ready preserves captured basis; invalid/old Ready stays held. |
| Atomicity/concurrency | Independent job instances/direct calls create one notification and one Emitted slot. Inject failure before commit and show neither persists; retry creates one. Cleanup sets the link null without reopening. |
| Boundaries | No EmailMessage/Intent/claim/budget/digest-watermark/TimeEntry/peer-data changes. Own Time read scope and existing ReadOnly create/edit refusals remain intact. Root verifies actual browser navigation separately. |
| Hosting | Root registration, one-minute coarse IsDue, forced flag-off refusal and aggregate JobRun details. Use isolated factory/clock fixtures and no real outbound mail or production job run. |

## Root freeze gates

Check the saved core/composition/reminder documents all follow root's public shared CLR convention with opaque constructors internal and base policy free of eager capacity/Time reads. Freeze the validated context access, exact event/locales/Time-link classification, existing Ready recovery rule above, bounded candidate scan and scoped-context construction before assigning the two source files. This document proposes these choices; it does not edit models, decisions or acceptance ledgers. S47's email/digest/away delivery and S12 Undo remain separate open work. Passing this future reminder test file alone cannot close S47, packet053, hosted behavior or human acceptance.
