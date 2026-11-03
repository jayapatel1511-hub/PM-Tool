using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hub.Api.Infrastructure;

public sealed record NoticeRequiredScope(ImmutableArray<Guid> ProjectIds, ImmutableArray<Guid> PlanningEntryIds);
public sealed record NoticeDigestWindow(DateTimeOffset From, DateTimeOffset Through);
public sealed record NoticeLocalSchedule(string OrgTimeZoneId, DateOnly RequestedDate, TimeOnly RequestedTime,
    DateOnly EffectiveDate, TimeOnly EffectiveTime, DateTimeOffset ScheduledAtUtc);
public sealed class NoticeRecipientContext
{
    internal HubDb Db { get; }
    public Guid UserId { get; }
    public Guid TransactionId { get; }
    internal NoticeRecipientContext(HubDb db, Guid userId, Guid transactionId) => (Db, UserId, TransactionId) = (db, userId, transactionId);
}
public sealed record NoticeRecipientPolicy(Guid UserId, bool IsActive, string EmailAddress, OrgSettings Settings,
    NoticePreferencesRead Preferences, bool DigestEnabled, bool HoldDigestWhenAway, string DigestTimeLocal, ImmutableArray<string> SectionsOff);
public sealed record NoticeDayAvailability(string State, bool? IsAway);
public enum NoticeSourceKind { Work, PlanningEntry, PlanningWithdrawal, PlanningGroup }
public enum NoticeCaptureMode { Ordinary, TaskBulk }
public sealed record NoticeAppReference(string? ItemType, Guid? ItemId, string? ItemKey, string? LinkPath);
public sealed record NoticeRawSource(NoticeSourceKind Kind, Guid? ProjectId, string? ItemType, Guid? ItemId,
    string? ItemKey, string? LinkPath, NoticeRequiredScope Scope, string EmailKind, NoticeAppReference AppReference);
public sealed record NoticeRawContent(string AppTitle, string? AppBody, string MailSubject, string MailBodyText, string? MailBodyHtml);
public sealed record NoticeRawCall(int CallOrdinal, Guid CorrelationId, string EventType, ImmutableArray<Guid> RecipientIds,
    Guid? ActualActorId, ImmutableArray<Guid>? AssignedTo, NoticeRawSource Source, NoticeRawContent Content);
public sealed class NoticeCaptureBuffer
{
    internal HubDb Db { get; }
    internal Guid CorrelationId { get; }
    internal Guid TransactionId { get; }
    internal NoticeCaptureMode Mode { get; }
    internal List<NoticeRawCall> Calls { get; } = [];
    internal bool Sealed;
    internal NoticeCaptureBuffer(HubDb db, Guid correlationId, NoticeCaptureMode mode)
        => (Db, CorrelationId, TransactionId, Mode) = (db, correlationId, db.Database.CurrentTransaction!.TransactionId, mode);
}
public sealed class NoticeCaptureSession
{
    internal NoticeCaptureBuffer Buffer { get; }
    internal Dictionary<Guid, NoticeRecipientContext> Recipients { get; }
    internal NoticeCaptureEvaluation? Evaluation;
    internal bool Staged;
    internal NoticeCaptureSession(NoticeCaptureBuffer buffer, Dictionary<Guid, NoticeRecipientContext> recipients) => (Buffer, Recipients) = (buffer, recipients);
}
public sealed record NoticeCaptureDecision(int CallOrdinal, Guid UserId, bool AppEligible, bool EmailEligible,
    bool IsDirectRecipient, string Disposition, string? ReasonCode);
public sealed record NoticeCaptureEvaluation(ImmutableArray<NoticeCaptureDecision> Decisions);
public sealed record NoticeAppBinding(Guid UserId, int? CallOrdinal, Guid NotificationId, int CountAtCapture);
public sealed record NoticeBulkCaptureBasis(Guid CommandReceiptId, Guid OriginalActorId, ImmutableArray<NoticeChangedTask> ChangedTasks);
public sealed record NoticeCaptureResult(ImmutableArray<Guid> IntentIds, ImmutableArray<Guid> EmailIds);
public sealed record NoticeHeldCandidate(Guid IntentId, Guid UserId, DateTimeOffset CreatedAt, string Subject, string BodyText,
    string? LinkPath, NoticeRequiredScope Scope, NoticeBulkRenderedMembership? BulkMembership);
public sealed record NoticeRenderedMembership(ImmutableArray<Guid> IntentIds, ImmutableArray<NoticeBulkRenderedMembership> BulkMemberships);
public enum NoticeDigestPreparationDecision { Compose, Dormant, NotEligible, InFlight, Uncertain, Exhausted, AlreadyTerminal }
public sealed class NoticeDigestPreparation
{
    internal NoticeRecipientContext Context { get; }
    public NoticeDigestPreparationDecision Decision { get; }
    public Guid SlotId { get; }
    public int NextCompositionVersion { get; }
    public NoticeDigestWindow Window { get; }
    public ImmutableArray<NoticeHeldCandidate> Held { get; }
    internal NoticeDigestPreparation(NoticeRecipientContext context, NoticeDigestPreparationDecision decision, NoticeDeliverySlot slot,
        ImmutableArray<NoticeHeldCandidate> held)
        => (Context, Decision, SlotId, NextCompositionVersion, Window, Held) = (context, decision, slot.Id,
            slot.CompositionVersion + 1, new(slot.WindowFrom!.Value, slot.WindowThrough!.Value), held);
}
public sealed record NoticeDigestReservationResult(Guid SlotId, int CompositionVersion, Guid? EmailId, string State, ImmutableArray<Guid> ReservedIntentIds);
public enum NoticePolicyChangeKind { EventChannels, EventReset, Digest, Follow, FollowReset, EnhancedPreferences, PlanningWithdrawal }
public sealed record NoticePolicyChange(NoticePolicyChangeKind Kind, string? EventType = null, Guid? ProjectId = null, Guid? PlanningEntryId = null);
public sealed record NoticePolicyChangeResult(int SuppressedIntents, int HeldIntents, int SuppressedEmails, ImmutableArray<Guid> SlotsNeedingSafeRecomposition);
public sealed record NoticePolicySweepResult(int Examined, int Changed, int Parked);
public sealed record NoticePendingCandidate(Guid IntentId, Guid UserId, DateTimeOffset NextCheckAtUtc);
public enum NoticeClaimPurpose { Initial, KnownFailureRetry }
public sealed record NoticeEmailEnvelope(string ToAddress, string Subject, string BodyText, string? BodyHtml, NoticeRequiredScope Scope);
public sealed record NoticeSendClaim(Guid EmailId, Guid UserId, Guid Token, int Version, DateTimeOffset ClaimedAt, string TransportKind,
    Guid? SlotId, int? CompositionVersion, NoticeEmailEnvelope Envelope, NoticeRenderedMembership Membership);
public sealed record NoticeClaimResult(string Decision, NoticeSendClaim? Claim, string? ReasonCode);
public sealed record NoticeSendObservation(string TransportKind, string Outcome, DateTimeOffset ObservedAt, DateTimeOffset? ProviderAcceptedAt, string FailureCertainty);
public sealed record NoticeCompletionResult(bool Applied, string Outcome, int AcceptedOccurrences, bool HasUncertainty);
public sealed record NoticeDigestProjectionFlag(string Code, bool EnabledAtRender);
public sealed record NoticeDigestPolicyDocument(int SchemaVersion, Guid UserId, Guid SlotId, int CompositionVersion, NoticeDigestWindow Window,
    NoticeRequiredScope Scope, NoticeDigestRenderPolicy RenderedPolicy, ImmutableArray<NoticeDigestProjectionFlag> ProjectionFlags);
public sealed record NoticeDeliveryAggregate(Guid NotificationId, int CurrentCount, int KnownCapturedCount, int RealAcceptedCount,
    int UncertainCount, string State, string HistoryCoverage, DateTimeOffset? AcceptedAt);

/// Private policy/state only. The caller owns the actual transaction and persistence, never this helper.
public static class NotificationDelivery
{
    internal static readonly string[] ProjectionCodes = ["register_screens", "people_screens", "capacity_screens", "visual_system", "work_screens"];
    static string Id(Guid value) => value.ToString("D").ToLowerInvariant();
    static string Token(Guid? value) => value is { } id ? Id(id) : "none";
    static void Demand(bool condition, string code = "InvalidNoticeBinding") { if (!condition) throw new NoticeProofException(code); }
    static Guid Transaction(HubDb db)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Notice work requires the owner's transaction.");
        if (tx.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted) throw new InvalidOperationException("Notice policy requires ReadCommitted.");
        return tx.TransactionId;
    }
    static HubDb Db(NoticeRecipientContext context)
    { Demand(context is not null && context.UserId != Guid.Empty && Transaction(context.Db) == context.TransactionId); return context.Db; }
    static void Utc(DateTimeOffset now) => Demand(now.Offset == TimeSpan.Zero, "InvalidNoticeTime");
    static void Enabled(OrgSettings settings)
    { if (!settings.IsFlagOn("people_screens")) throw ApiException.Conflict("notice_policy_changed", "notice.policyChanged"); }
    static ApiException IdentityConflict() => ApiException.Conflict("notice_identity_conflict", "notice.identityConflict");
    static bool EqualIds(IEnumerable<Guid> a, IEnumerable<Guid> b) => a.OrderBy(Id, StringComparer.Ordinal).SequenceEqual(b.OrderBy(Id, StringComparer.Ordinal));
    static bool EqualScope(NoticeRequiredScope a, NoticeRequiredScope b) => EqualIds(a.ProjectIds, b.ProjectIds) && EqualIds(a.PlanningEntryIds, b.PlanningEntryIds);
    internal static NoticeRequiredScope Scope(NoticeDeliveryIntent intent) => new(intent.RequiredProjectIds.ToImmutableArray(), intent.RequiredPlanningEntryIds.ToImmutableArray());
    internal static NoticeRequiredScope Scope(EmailMessage email) => new(email.RequiredProjectIds.ToImmutableArray(), email.RequiredPlanningEntryIds.ToImmutableArray());
    internal static NoticeRequiredScope Union(IEnumerable<NoticeRequiredScope> scopes) => new(scopes.SelectMany(s => s.ProjectIds).Distinct().OrderBy(Id, StringComparer.Ordinal).ToImmutableArray(),
        scopes.SelectMany(s => s.PlanningEntryIds).Distinct().OrderBy(Id, StringComparer.Ordinal).ToImmutableArray());
    static async Task<OrgSettings> Settings(HubDb db, CancellationToken ct)
    {
        var rows = await db.Settings.AsNoTracking().ToListAsync(ct);
        return OrgSettings.From(rows.ToDictionary(row => row.Key, row => JsonDocument.Parse(row.Value).RootElement.Clone()));
    }
    public static async Task<NoticeRecipientContext> LockRecipientAsync(HubDb db, Guid userId, CancellationToken ct = default)
    { await NoticeRecipientLock.Acquire(db, userId, ct); return new(db, userId, Transaction(db)); }
    public static async Task<NoticeRecipientPolicy> ReadRecipientPolicyAsync(NoticeRecipientContext context, CancellationToken ct = default)
    {
        var db = Db(context); var settings = await Settings(db, ct);
        var user = await db.Users.AsNoTracking().Where(row => row.Id == context.UserId).Select(row => new { row.IsActive, row.Email }).SingleOrDefaultAsync(ct);
        var row = await db.UserSettings.AsNoTracking().SingleOrDefaultAsync(value => value.UserId == context.UserId, ct);
        var off = JsonSerializer.Deserialize<string[]>(row?.DigestSectionsOff ?? "[]") ?? throw new NoticeProofException("InvalidDigestSections");
        Demand(off.All(Digest.SectionCodes.Contains), "InvalidDigestSections");
        var frequency = row?.NoticeDigestFrequency ?? NoticeDigestFrequency.Daily;
        Demand(NoticeDigestFrequency.All.Contains(frequency), "InvalidDigestFrequency");
        return new(context.UserId, user?.IsActive == true, user?.Email ?? "", settings,
            new(row?.NoticePreferencesRevision ?? 0, frequency, row?.HoldImmediateEmailWhenAway ?? false,
                row?.TimeReminderEnabled ?? false, row?.TimeReminderWeekday, row?.TimeReminderTimeLocal),
            row?.DigestEnabled ?? true, row?.HoldDigestWhenAway ?? true, row?.DigestTimeLocal ?? settings.DigestSendTimeLocal,
            off.Distinct().Order(StringComparer.Ordinal).ToImmutableArray());
    }
    public static async Task<NoticeDayAvailability> ReadDayAvailabilityAsync(NoticeRecipientContext context, DateOnly day, CancellationToken ct = default)
    {
        var policy = await ReadRecipientPolicyAsync(context, ct);
        return await ReadDay(context, policy, day);
    }
    static async Task<NoticeDayAvailability> ReadDay(NoticeRecipientContext context, NoticeRecipientPolicy policy, DateOnly day)
    {
        if (!policy.IsActive) return new("Unavailable", null);
        var days = await PersonCapacity.Load(Db(context), [context.UserId], day, day, policy.Settings);
        return days.TryGetValue((context.UserId, day), out var value) ? new("Known", value.Normal > 0 && value.Available == 0) : new("Unavailable", null);
    }
    // An operation-local snapshot under the recipient lock. Never survives a capture/claim boundary.
    sealed class PolicyFrame(NoticeRecipientContext context, NoticeRecipientPolicy policy)
    {
        public NoticeRecipientPolicy Policy { get; } = policy;
        readonly Dictionary<string, (bool App, bool Email)> channels = new(StringComparer.Ordinal);
        readonly Dictionary<Guid, bool> muted = [];
        readonly Dictionary<string, bool> scopes = new(StringComparer.Ordinal);
        readonly Dictionary<Guid, string?> statuses = [];
        readonly Dictionary<DateOnly, NoticeDayAvailability> days = [];
        public async Task<(bool App, bool Email)> ChannelsFor(string code, CancellationToken ct)
        { if (!channels.TryGetValue(code, out var value)) channels[code] = value = await Channels(context, Policy, code, ct); return value; }
        public async Task<bool> MutedFor(string code, Guid? project, bool direct, CancellationToken ct)
        {
            if (project is not { } id || NotificationEvents.Get(code).DirectAssignment && direct) return false;
            if (!muted.TryGetValue(id, out var value)) muted[id] = value = await Muted(context, code, id, false, ct); return value;
        }
        public async Task<bool> AllowedFor(NoticeRequiredScope scope, CancellationToken ct)
        {
            var key = string.Join(',', scope.ProjectIds.OrderBy(Id, StringComparer.Ordinal).Select(Id)) + ":"
                + string.Join(',', scope.PlanningEntryIds.OrderBy(Id, StringComparer.Ordinal).Select(Id));
            if (!scopes.TryGetValue(key, out var value)) scopes[key] = value = await Allowed(context, scope, ct); return value;
        }
        public async Task<string?> Status(Guid project, CancellationToken ct)
        {
            if (!statuses.TryGetValue(project, out var value)) statuses[project] = value = await context.Db.Projects.AsNoTracking()
                .Where(row => row.Id == project).Select(row => row.Status).SingleOrDefaultAsync(ct); return value;
        }
        public async Task<NoticeDayAvailability> Day(DateOnly date)
        { if (!days.TryGetValue(date, out var value)) days[date] = value = await ReadDay(context, Policy, date); return value; }
    }
    public static DateOnly WeekMonday(DateTimeOffset nowUtc, OrgSettings settings) => Workload.WeekOf(Clock.LocalDate(nowUtc, settings));
    public static NoticeLocalSchedule ResolveLocal(OrgSettings settings, DateOnly requestedDate, TimeOnly requestedTime)
    {
        var zone = Clock.Zone(settings); var wall = requestedDate.ToDateTime(requestedTime, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(wall))
        {
            var low = wall.Ticks; var high = wall;
            while (zone.IsInvalidTime(high)) high = high.AddMinutes(1);
            var upper = high.Ticks;
            while (upper - low > 1)
            {
                var middle = low + (upper - low) / 2;
                if (zone.IsInvalidTime(new DateTime(middle, DateTimeKind.Unspecified))) low = middle; else upper = middle;
            }
            wall = new DateTime(upper, DateTimeKind.Unspecified);
        }
        var utc = zone.IsAmbiguousTime(wall)
            ? zone.GetAmbiguousTimeOffsets(wall).Select(offset => new DateTimeOffset(wall, offset).ToUniversalTime()).Min()
            : new DateTimeOffset(wall, zone.GetUtcOffset(wall)).ToUniversalTime();
        return new(zone.Id, requestedDate, requestedTime, DateOnly.FromDateTime(wall), TimeOnly.FromDateTime(wall), utc);
    }
    public static NoticeCaptureBuffer BeginCapture(HubDb db, Guid correlationId, NoticeCaptureMode mode = NoticeCaptureMode.Ordinary)
    { Demand(correlationId != Guid.Empty && Enum.IsDefined(mode)); _ = Transaction(db); return new(db, correlationId, mode); }
    public static void Buffer(NoticeCaptureBuffer buffer, NoticeRawCall call)
    {
        Demand(!buffer.Sealed && Transaction(buffer.Db) == buffer.TransactionId && call is not null && call.CorrelationId == buffer.CorrelationId
            && call.CallOrdinal == buffer.Calls.Count + 1 && !call.RecipientIds.IsDefault && call.RecipientIds.All(id => id != Guid.Empty)
            && call.RecipientIds.Distinct().Count() == call.RecipientIds.Length && (call.AssignedTo is null || !call.AssignedTo.Value.IsDefault));
        NotificationDigestComposition.DemandScope(call.Source.Scope);
        Demand(NotificationEvents.All.Any(value => value.Code == call.EventType) && Enum.IsDefined(call.Source.Kind)
            && (call.Source.ItemType is null) == (call.Source.ItemId is null) && call.Source.ItemId != Guid.Empty
            && call.Source.ProjectId != Guid.Empty && (call.Source.ProjectId is not { } project || call.Source.Scope.ProjectIds.Contains(project))
            && call.Source.EmailKind is "Immediate" or "PlanningEntryChanged" or "PlanningWithdrawal"
            && call.Content.MailBodyHtml is null);
        buffer.Calls.Add(call);
    }
    public static async Task<NoticeCaptureSession> LockCaptureAsync(NoticeCaptureBuffer buffer, ImmutableArray<Guid> alreadyLockedProjectIds, CancellationToken ct = default)
    {
        Demand(!buffer.Sealed && Transaction(buffer.Db) == buffer.TransactionId && !alreadyLockedProjectIds.IsDefault);
        Demand(buffer.Calls.SelectMany(call => call.Source.Scope.ProjectIds).All(alreadyLockedProjectIds.Contains), "MissingCanonicalProjectLock");
        buffer.Sealed = true;
        var ids = buffer.Calls.SelectMany(call => call.RecipientIds).Distinct().OrderBy(Id, StringComparer.Ordinal).ToArray();
        await NoticeRecipientLock.AcquireMany(buffer.Db, ids, ct);
        return new(buffer, ids.ToDictionary(id => id, id => new NoticeRecipientContext(buffer.Db, id, buffer.TransactionId)));
    }
    static string Occurrence(NoticeRawCall call, Guid user) => $"notice:v1:{Id(call.CorrelationId)}:{call.EventType}:{call.Source.ItemType ?? "none"}:{Token(call.Source.ItemId)}:{Token(call.ActualActorId)}:{Id(user)}";
    static string Suppression(NoticeRawCall call, Guid user) => $"{call.EventType}:{call.Source.ItemId}:{user}";
    static bool Compatible(NoticeRawCall a, NoticeRawCall b) => a.ActualActorId == b.ActualActorId && a.EventType == b.EventType
        && a.Source.Kind == b.Source.Kind && a.Source.ProjectId == b.Source.ProjectId && a.Source.ItemType == b.Source.ItemType
        && a.Source.ItemId == b.Source.ItemId && a.Source.ItemKey == b.Source.ItemKey && a.Source.LinkPath == b.Source.LinkPath
        && a.Source.EmailKind == b.Source.EmailKind && a.Source.AppReference == b.Source.AppReference && EqualScope(a.Source.Scope, b.Source.Scope)
        && (a.AssignedTo is null ? b.AssignedTo is null : b.AssignedTo is { } assigned && EqualIds(a.AssignedTo.Value, assigned));
    public static async Task<NoticeCaptureEvaluation> EvaluateCaptureAsync(NoticeCaptureSession session, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Utc(nowUtc); Demand(!session.Staged && session.Evaluation is null && Transaction(session.Buffer.Db) == session.Buffer.TransactionId);
        Enabled(await Settings(session.Buffer.Db, ct));
        if (session.Buffer.Mode == NoticeCaptureMode.Ordinary)
        {
            var identities = new Dictionary<string, NoticeRawCall>(StringComparer.Ordinal);
            foreach (var call in session.Buffer.Calls) foreach (var user in call.RecipientIds.Where(id => id != call.ActualActorId))
            {
                // Legacy collapse ignores type/project; refuse aliases instead of altering the existing collapse rule.
                var identity = $"{call.EventType}:{call.Source.ItemId}:{call.ActualActorId}:{user}";
                if (identities.TryGetValue(identity, out var old) && !Compatible(old, call)) throw IdentityConflict();
                identities.TryAdd(identity, call);
            }
        }
        var decisions = ImmutableArray.CreateBuilder<NoticeCaptureDecision>(); var frames = new Dictionary<Guid, PolicyFrame>();
        foreach (var call in session.Buffer.Calls) foreach (var user in call.RecipientIds)
        {
            var context = session.Recipients[user];
            if (!frames.TryGetValue(user, out var frame)) frames[user] = frame = new(context, await ReadRecipientPolicyAsync(context, ct));
            var policy = frame.Policy; Enabled(policy.Settings);
            var direct = NotificationEvents.Get(call.EventType).DirectAssignment && (call.AssignedTo is null || call.AssignedTo.Value.Contains(user));
            var reason = user == call.ActualActorId ? "Self" : !policy.IsActive ? NoticeIntentDisposition.RecipientInactive
                : !await frame.AllowedFor(call.Source.Scope, ct) ? NoticeIntentDisposition.AccessRemoved : null;
            if (reason is null && call.Source.ProjectId is { } project)
            {
                var status = await frame.Status(project, ct);
                if (status is ProjectStatus.Archived or ProjectStatus.Cancelled || status == ProjectStatus.Setup && Notifier.HeldWhileSetup(call.EventType)) reason = "SourceUnavailable";
            }
            if (reason is null && await frame.MutedFor(call.EventType, call.Source.ProjectId, direct, ct)) reason = NoticeIntentDisposition.Muted;
            if (reason is not null) { decisions.Add(new(call.CallOrdinal, user, false, false, direct, reason, reason)); continue; }
            var channels = await frame.ChannelsFor(call.EventType, ct);
            var disposition = !channels.Email ? NoticeIntentDisposition.ChannelOff : string.IsNullOrEmpty(policy.EmailAddress) ? NoticeIntentDisposition.NoAddress
                : !policy.Preferences.HoldImmediateEmailWhenAway ? NoticeIntentDisposition.Queued
                : (await frame.Day(Clock.LocalDate(nowUtc, policy.Settings))).IsAway switch
                { null => NoticeIntentDisposition.PendingAvailability, true => NoDigest(policy) ? NoticeIntentDisposition.HeldNoDigest : NoticeIntentDisposition.HeldAway, false => NoticeIntentDisposition.Queued };
            decisions.Add(new(call.CallOrdinal, user, channels.App, channels.Email && !string.IsNullOrEmpty(policy.EmailAddress), direct, disposition,
                disposition is NoticeIntentDisposition.ChannelOff or NoticeIntentDisposition.NoAddress ? disposition : null));
        }
        return session.Evaluation = new(decisions.ToImmutable());
    }
    static async Task<(bool App, bool Email)> Channels(NoticeRecipientContext context, NoticeRecipientPolicy policy, string eventType, CancellationToken ct)
    {
        var definition = NotificationEvents.Get(eventType);
        var defaults = policy.Settings.NotificationDefaults.GetValueOrDefault(eventType) ?? new Hub.Domain.Channels(definition.App, definition.Email);
        var preference = await Db(context).NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == context.UserId && row.EventType == eventType, ct);
        return (preference?.InApp ?? defaults.App, preference?.Email ?? defaults.Email);
    }
    static async Task<bool> Muted(NoticeRecipientContext context, string eventType, Guid? project, bool direct, CancellationToken ct) => project is { } id
        && !(NotificationEvents.Get(eventType).DirectAssignment && direct)
        && await Db(context).Follows.AsNoTracking().AnyAsync(row => row.UserId == context.UserId && row.ProjectId == id && row.Level == FollowLevel.Muted, ct);
    static async Task<bool> Allowed(NoticeRecipientContext context, NoticeRequiredScope scope, CancellationToken ct)
    {
        NotificationDigestComposition.DemandScope(scope); var db = Db(context);
        return (scope.ProjectIds.IsEmpty || await EmailProjectAccess.Allowed(db, context.UserId, scope.ProjectIds.ToArray()))
            && (scope.PlanningEntryIds.IsEmpty || await PlanningEndpoints.PlanningEmailAllowed(db, context.UserId, scope.PlanningEntryIds.ToArray()));
    }
    static bool NoDigest(NoticeRecipientPolicy policy) => !policy.DigestEnabled || Digest.SectionCodes.All(policy.SectionsOff.Contains);
    static async Task<string> InitialDisposition(NoticeRecipientContext context, NoticeRecipientPolicy policy, DateTimeOffset now, CancellationToken ct)
    {
        if (!policy.Preferences.HoldImmediateEmailWhenAway) return NoticeIntentDisposition.Queued;
        var day = await ReadDayAvailabilityAsync(context, Clock.LocalDate(now, policy.Settings), ct);
        return day.IsAway is null ? NoticeIntentDisposition.PendingAvailability : day.IsAway == true
            ? NoDigest(policy) ? NoticeIntentDisposition.HeldNoDigest : NoticeIntentDisposition.HeldAway : NoticeIntentDisposition.Queued;
    }
    static async Task<bool> Duplicate(NoticeRecipientContext context, string key, DateTimeOffset now, CancellationToken ct)
    {
        var db = Db(context); var since = now.AddHours(-24);
        if (db.Emails.Local.Any(row => row.UserId == context.UserId && row.DedupKey == key && row.CreatedAt >= since)
            || await db.Emails.AsNoTracking().AnyAsync(row => row.UserId == context.UserId && row.DedupKey == key && row.CreatedAt >= since, ct)
            || db.NoticeDeliveryIntents.Local.Any(row => row.UserId == context.UserId && row.SuppressionKey == key && row.SuppressionAnchorAt >= since)
            || await db.NoticeDeliveryIntents.AsNoTracking().AnyAsync(row => row.UserId == context.UserId && row.SuppressionKey == key && row.SuppressionAnchorAt >= since, ct)) return true;
        var bulk = await db.NoticeDeliveryIntents.AsNoTracking().Where(row => row.UserId == context.UserId && row.CreatedAt >= since && row.BulkPolicyJson != null).ToListAsync(ct);
        foreach (var row in bulk.Concat(db.NoticeDeliveryIntents.Local.Where(row => row.UserId == context.UserId && row.BulkPolicyJson != null)))
        {
            var proof = NoticeDeliveryProof.Decode(row.BulkPolicyJson!, context.UserId, row.ItemId ?? throw new NoticeProofException("InvalidBulkSource"));
            if (proof.Origin.Obligations.Any(value => value.SuppressionKey == key && value.SuppressionAnchorAt >= since)) return true;
        }
        return false;
    }
    static async Task AppBinding(NoticeRecipientContext context, NoticeAppBinding? binding, bool needed, CancellationToken ct)
    {
        if (!needed) { Demand(binding is null, "UnexpectedAppBinding"); return; }
        Demand(binding is not null && binding.UserId == context.UserId && binding.CountAtCapture > 0);
        var db = Db(context); var row = db.Notifications.Local.FirstOrDefault(value => value.Id == binding!.NotificationId)
            ?? await db.Notifications.AsNoTracking().SingleOrDefaultAsync(value => value.Id == binding!.NotificationId, ct);
        Demand(row is not null && row.UserId == context.UserId && row.Count == binding!.CountAtCapture, "ForeignAppBinding");
    }
    static void Session(NoticeCaptureSession session, NoticeCaptureEvaluation evaluation)
    { Demand(!session.Staged && ReferenceEquals(session.Evaluation, evaluation) && Transaction(session.Buffer.Db) == session.Buffer.TransactionId); }
    public static async Task<NoticeCaptureResult> StageOrdinaryAsync(NoticeCaptureSession session, NoticeCaptureEvaluation evaluation,
        ImmutableArray<NoticeAppBinding> appBindings, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Session(session, evaluation); Demand(session.Buffer.Mode == NoticeCaptureMode.Ordinary); Enabled(await Settings(session.Buffer.Db, ct)); Utc(nowUtc);
        var intents = ImmutableArray.CreateBuilder<Guid>(); var emails = ImmutableArray.CreateBuilder<Guid>();
        var byOrdinal = session.Buffer.Calls.ToDictionary(call => call.CallOrdinal);
        foreach (var group in evaluation.Decisions.Where(value => value.AppEligible || value.EmailEligible)
            .GroupBy(value => Occurrence(byOrdinal[value.CallOrdinal], value.UserId)))
        {
            var decision = group.OrderBy(value => value.CallOrdinal).First(); var call = byOrdinal[decision.CallOrdinal]; var context = session.Recipients[decision.UserId];
            var db = Db(context); var existing = db.NoticeDeliveryIntents.Local.FirstOrDefault(row => row.UserId == context.UserId && row.OccurrenceKey == group.Key)
                ?? await db.NoticeDeliveryIntents.SingleOrDefaultAsync(row => row.UserId == context.UserId && row.OccurrenceKey == group.Key, ct);
            if (existing is not null)
            {
                if (existing.EventType != call.EventType || existing.ActorUserId != call.ActualActorId || existing.ProjectId != call.Source.ProjectId
                    || existing.ItemType != call.Source.ItemType || existing.ItemId != call.Source.ItemId || existing.ItemKey != call.Source.ItemKey
                    || existing.OriginalLinkPath != call.Source.LinkPath || !EqualScope(Scope(existing), call.Source.Scope)
                    || existing.IsDirectRecipient != decision.IsDirectRecipient) throw IdentityConflict();
                intents.Add(existing.Id); continue;
            }
            var binding = appBindings.SingleOrDefault(value => value.UserId == context.UserId && value.CallOrdinal == decision.CallOrdinal);
            await AppBinding(context, binding, decision.AppEligible, ct);
            var duplicate = decision.EmailEligible && await Duplicate(context, Suppression(call, context.UserId), nowUtc, ct);
            var intent = new NoticeDeliveryIntent { UserId = context.UserId, OccurrenceKey = group.Key, EventType = call.EventType,
                ItemType = call.Source.ItemType, ItemId = call.Source.ItemId, ItemKey = call.Source.ItemKey, ProjectId = call.Source.ProjectId,
                ActorUserId = call.ActualActorId, IsDirectRecipient = decision.IsDirectRecipient, RequiredProjectIds = call.Source.Scope.ProjectIds.ToArray(),
                RequiredPlanningEntryIds = call.Source.Scope.PlanningEntryIds.ToArray(), NotificationId = binding?.NotificationId,
                NotificationCountAtCapture = binding?.CountAtCapture, OriginalSubject = call.Content.MailSubject, OriginalBodyText = call.Content.MailBodyText,
                OriginalLinkPath = call.Source.LinkPath, SuppressionKey = Suppression(call, context.UserId),
                SuppressionAnchorAt = decision.EmailEligible && !duplicate ? nowUtc : null,
                CreatedAt = nowUtc, UpdatedAt = nowUtc, Disposition = duplicate ? NoticeIntentDisposition.Duplicate : decision.Disposition };
            db.NoticeDeliveryIntents.Add(intent); intents.Add(intent.Id);
            if (intent.Disposition == NoticeIntentDisposition.Queued)
            {
                var email = await QueueImmediate(context, intent, call.Source.EmailKind, nowUtc, ct); if (email is not null) emails.Add(email.Id);
            }
            else if (!NoticeIntentDisposition.Terminal.Contains(intent.Disposition)) ScheduleCheck(intent, nowUtc, (await ReadRecipientPolicyAsync(context, ct)).Settings, true);
        }
        session.Staged = true; return new(intents.ToImmutable(), emails.ToImmutable());
    }
    public static async Task<NoticeCaptureResult> StageBulkAsync(NoticeCaptureSession session, NoticeCaptureEvaluation evaluation,
        NoticeBulkCaptureBasis basis, ImmutableArray<NoticeAppBinding> appBindings, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Session(session, evaluation); Demand(session.Buffer.Mode == NoticeCaptureMode.TaskBulk); Enabled(await Settings(session.Buffer.Db, ct)); Utc(nowUtc);
        Demand(basis.CommandReceiptId == session.Buffer.CorrelationId && basis.OriginalActorId != Guid.Empty && !basis.ChangedTasks.IsDefault
            && basis.ChangedTasks.Length <= NoticeProofLimits.MaxChangedTasks && basis.ChangedTasks.Select(row => row.TaskId).Distinct().Count() == basis.ChangedTasks.Length);
        var calls = session.Buffer.Calls.ToDictionary(call => call.CallOrdinal);
        var intentIds = ImmutableArray.CreateBuilder<Guid>(); var emailIds = ImmutableArray.CreateBuilder<Guid>();
        foreach (var recipient in evaluation.Decisions.GroupBy(row => row.UserId))
        {
            var included = recipient.Where(decision => calls[decision.CallOrdinal].Source.ProjectId is { } project
                && basis.ChangedTasks.Any(task => task.ProjectId == project)
                && (calls[decision.CallOrdinal].Source.ItemType != ItemType.Task
                    || basis.ChangedTasks.Any(task => task.TaskId == calls[decision.CallOrdinal].Source.ItemId && task.ProjectId == project)))
                .OrderBy(decision => decision.CallOrdinal).ToArray();
            if (!included.Any(value => value.AppEligible || value.EmailEligible)) continue;
            var context = session.Recipients[recipient.Key]; var db = Db(context);
            var occurrence = $"task-bulk:v1:{Id(basis.CommandReceiptId)}:{Id(recipient.Key)}";
            var existing = db.NoticeDeliveryIntents.Local.FirstOrDefault(row => row.UserId == recipient.Key && row.OccurrenceKey == occurrence)
                ?? await db.NoticeDeliveryIntents.SingleOrDefaultAsync(row => row.UserId == recipient.Key && row.OccurrenceKey == occurrence, ct);
            if (existing is not null) { intentIds.Add(existing.Id); continue; }
            var obligations = ImmutableArray.CreateBuilder<NoticeBulkObligation>(); var domains = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var decision in included)
            {
                var call = calls[decision.CallOrdinal]; var eligible = decision.EmailEligible; var reason = decision.ReasonCode;
                if (eligible)
                {
                    var key = Suppression(call, recipient.Key);
                    if (!domains.TryGetValue(key, out var duplicate)) domains[key] = duplicate = await Duplicate(context, key, nowUtc, ct);
                    if (duplicate) { eligible = false; reason = NoticeRefusalReason.Duplicate; }
                }
                if (!eligible && reason is not (NoticeRefusalReason.ChannelOff or NoticeRefusalReason.NoAddress or NoticeRefusalReason.Muted
                    or NoticeRefusalReason.AccessRemoved or NoticeRefusalReason.RecipientInactive or NoticeRefusalReason.Duplicate))
                    reason = NoticeRefusalReason.AccessRemoved;
                Demand(call.Source.ProjectId is not null && call.Source.ItemType is not null && call.Source.ItemId is not null, "InvalidBulkSource");
                obligations.Add(new(obligations.Count + 1, call.EventType, call.Source.ItemType!, call.Source.ItemId!.Value, call.Source.ProjectId!.Value,
                    call.Source.Scope.ProjectIds, call.Source.Scope.PlanningEntryIds, call.ActualActorId, decision.IsDirectRecipient,
                    decision.AppEligible, eligible, eligible ? null : reason, Suppression(call, recipient.Key), eligible ? nowUtc : null,
                    call.Source.ItemType == ItemType.Task ? NoticeBulkCoverage.Task : NoticeBulkCoverage.ChangedTasksInSourceProject));
            }
            var items = obligations.ToImmutable(); var appKeys = items.Where(value => value.AppEligible).Select(value => value.Key).ToImmutableArray();
            var mailKeys = items.Where(value => value.EmailEligible).Select(value => value.Key).ToImmutableArray();
            var binding = appBindings.SingleOrDefault(value => value.UserId == recipient.Key && value.CallOrdinal is null);
            await AppBinding(context, binding, !appKeys.IsEmpty, ct);
            ImmutableArray<Guid> Tasks(ImmutableArray<int> keys) => basis.ChangedTasks.Where(task => items.Where(value => keys.Contains(value.Key))
                .Any(value => value.Coverage == NoticeBulkCoverage.Task ? value.SourceItemId == task.TaskId && value.SourceProjectId == task.ProjectId : value.SourceProjectId == task.ProjectId))
                .Select(task => task.TaskId).Distinct().ToImmutableArray();
            var origin = new NoticeBulkOrigin(1, NoticeProofLimits.Kind, basis.CommandReceiptId, basis.OriginalActorId, recipient.Key, nowUtc,
                basis.ChangedTasks, items, appKeys, mailKeys, binding?.NotificationId, binding?.CountAtCapture, Tasks(appKeys), Tasks(mailKeys));
            var proof = NoticeDeliveryProof.Admit(origin);
            var firstMail = included.FirstOrDefault(value => value.EmailEligible);
            var state = firstMail?.Disposition ?? NoticeIntentDisposition.ChannelOff;
            proof = NoticeDeliveryProof.WithProgress(proof, proof.Progress.Select(value => mailKeys.Contains(value.Key)
                ? value with { State = state == NoticeIntentDisposition.Queued ? NoticeIntentDisposition.Pending : state } : value).ToImmutableArray());
            var scope = Union(items.Where(value => value.EmailEligible).Select(value => new NoticeRequiredScope(value.RequiredProjectIds, value.RequiredPlanningEntryIds)));
            var title = Text.Get("notify.task_bulk_summary", origin.EmailTaskIds.Length);
            var completeApp = !appKeys.IsEmpty && appKeys.All(mailKeys.Contains);
            var intent = new NoticeDeliveryIntent { UserId = recipient.Key, OccurrenceKey = occurrence, EventType = NoticeProofLimits.Kind,
                ItemType = "TaskBulkCommand", ItemId = basis.CommandReceiptId, ActorUserId = basis.OriginalActorId,
                RequiredProjectIds = scope.ProjectIds.ToArray(), RequiredPlanningEntryIds = [],
                NotificationId = completeApp ? binding?.NotificationId : null, NotificationCountAtCapture = completeApp ? binding?.CountAtCapture : null,
                OriginalSubject = title, OriginalBodyText = BulkBody(db, title), OriginalLinkPath = "/tasks", SuppressionKey = occurrence,
                SuppressionAnchorAt = !mailKeys.IsEmpty ? nowUtc : null, CreatedAt = nowUtc, UpdatedAt = nowUtc,
                Disposition = BulkState(proof), BulkPolicyJson = NoticeDeliveryProof.Encode(proof) };
            db.NoticeDeliveryIntents.Add(intent); intentIds.Add(intent.Id);
            if (!mailKeys.IsEmpty && state == NoticeIntentDisposition.Queued)
            { var email = await QueueImmediate(context, intent, "Immediate", nowUtc, ct); if (email is not null) emailIds.Add(email.Id); }
            else if (!NoticeIntentDisposition.Terminal.Contains(intent.Disposition) && !PendingKeys(proof).IsEmpty)
                ScheduleCheck(intent, nowUtc, (await ReadRecipientPolicyAsync(context, ct)).Settings, true);
        }
        session.Staged = true; return new(intentIds.ToImmutable(), emailIds.ToImmutable());
    }
    static string BulkBody(HubDb db, string title) => $"{title}\n\n{Text.Get("email.open")}: {db.NoticeEmailBaseUrl}/tasks\n\n{Text.Get("email.footer")}";
    static NoticeBulkProof Proof(NoticeDeliveryIntent intent) => NoticeDeliveryProof.Decode(intent.BulkPolicyJson!, intent.UserId,
        intent.ItemId ?? throw new NoticeProofException("InvalidBulkSource"));
    static string BulkState(NoticeBulkProof proof)
    {
        var states = proof.Progress.Where(value => proof.Origin.EmailCohortKeys.IsEmpty || proof.Origin.EmailCohortKeys.Contains(value.Key)).Select(value => value.State).Distinct().ToArray();
        return states.Length == 1 ? states[0] : NoticeIntentDisposition.Mixed;
    }
    static void SetProof(NoticeDeliveryIntent intent, NoticeBulkProof before, ImmutableArray<NoticeBulkProgress> after, DateTimeOffset now)
    {
        var proof = NoticeDeliveryProof.WithProgress(before, after); intent.BulkPolicyJson = NoticeDeliveryProof.Encode(proof);
        intent.Disposition = BulkState(proof); intent.UpdatedAt = now;
        if (PendingKeys(proof).IsEmpty) intent.NextPolicyCheckAt = null;
    }
    static ImmutableArray<int> PendingKeys(NoticeBulkProof proof) => proof.Progress.Where(value => proof.Origin.EmailCohortKeys.Contains(value.Key)
        && !NoticeIntentDisposition.Terminal.Contains(value.State)).Select(value => value.Key).ToImmutableArray();
    static NoticeRequiredScope BulkScope(NoticeBulkProof proof, ImmutableArray<int> keys) => Union(proof.Origin.Obligations.Where(value => keys.Contains(value.Key))
        .Select(value => new NoticeRequiredScope(value.RequiredProjectIds, value.RequiredPlanningEntryIds)));
    static int BulkTaskCount(NoticeBulkProof proof, ImmutableArray<int> keys) => proof.Origin.ChangedTasks.Count(task => proof.Origin.Obligations.Where(value => keys.Contains(value.Key))
        .Any(value => value.Coverage == NoticeBulkCoverage.Task ? value.SourceItemId == task.TaskId && value.SourceProjectId == task.ProjectId : value.SourceProjectId == task.ProjectId));
    static async Task<EmailMessage?> QueueImmediate(NoticeRecipientContext context, NoticeDeliveryIntent intent, string kind, DateTimeOffset now, CancellationToken ct)
    {
        var db = Db(context); Demand(intent.DeliveryEmailId is null && intent.SuppressionAnchorAt is not null);
        var policy = await ReadRecipientPolicyAsync(context, ct); Enabled(policy.Settings);
        var proof = intent.BulkPolicyJson is null ? null : Proof(intent);
        var keys = proof is null ? ImmutableArray<int>.Empty : PendingKeys(proof);
        if (proof is not null && keys.IsEmpty) { intent.NextPolicyCheckAt = null; return null; }
        var scope = proof is null ? Scope(intent) : BulkScope(proof, keys);
        var title = proof is null ? intent.OriginalSubject : Text.Get("notify.task_bulk_summary", BulkTaskCount(proof, keys));
        var member = proof is null ? null : NoticeDeliveryProof.Render(intent.Id, proof, keys);
        var map = NoticeDeliveryProof.EncodeRenderedBulkMap(context.UserId, [intent.Id], member is null ? [] : [member],
            proof is null ? new Dictionary<Guid, NoticeBulkProof>() : new Dictionary<Guid, NoticeBulkProof> { [intent.Id] = proof });
        // A held immediate payload keeps its original invocation budget and immutable envelope.
        var previous = await db.Emails.Where(row => row.UserId == context.UserId && row.NoticeProtocolVersion == 1
            && row.NoticeSlotId == null && row.RenderedOccurrenceIds != null && row.RenderedOccurrenceIds.Contains(intent.Id))
            .OrderBy(row => row.CreatedAt).FirstOrDefaultAsync(ct);
        previous ??= db.Emails.Local.FirstOrDefault(row => row.UserId == context.UserId && row.NoticeProtocolVersion == 1
            && row.NoticeSlotId == null && row.RenderedOccurrenceIds?.Contains(intent.Id) == true);
        if (previous is not null)
        {
            if (PossiblyAccepted(previous)) return null;
            if (previous.ToAddress != policy.EmailAddress)
            { await SetRefusal(context, intent, NoticeIntentDisposition.AccessRemoved, NoticeRefusalReason.AddressChanged, now, ct); return null; }
            var oldMembership = await Membership(context, previous, ct);
            var same = previous.Subject == title && previous.BodyText == (proof is null ? intent.OriginalBodyText : BulkBody(db, title))
                && previous.BodyHtml is null && EqualScope(Scope(previous), scope) && oldMembership.IntentIds.SequenceEqual(new[] { intent.Id })
                && (proof is null ? oldMembership.BulkMemberships.IsEmpty : oldMembership.BulkMemberships.Length == 1
                    && oldMembership.BulkMemberships[0].ObligationKeys.SequenceEqual(keys));
            if (!same)
            {
                // The old combined payload cannot describe a narrower cohort. Keep its envelope and
                // budget, refuse only the source obligations actually refused, and park the remainder.
                await SuppressPayload(context, previous, now, "ImmediateMembershipChanged", ct);
                Hold(intent, NoDigest(policy) ? NoticeIntentDisposition.HeldNoDigest : NoticeIntentDisposition.HeldAway, now);
                ScheduleCheck(intent, now, policy.Settings); return null;
            }
            if (previous.Attempts >= 8)
            {
                await SetRefusal(context, intent, NoticeIntentDisposition.Exhausted, NoticeRefusalReason.AttemptsExhausted, now, ct); return null;
            }
            previous.SuppressedAt = null; previous.LastError = null;
            previous.TransportOutcome = previous.Attempts > 0 ? NoticeTransportOutcome.Failed : NoticeTransportOutcome.Queued;
            previous.NextAttemptAt = previous.NextAttemptAt > now ? previous.NextAttemptAt : now;
        }
        var email = previous ?? new EmailMessage { UserId = context.UserId, NoticeProtocolVersion = 1, Kind = kind, RenderedOccurrenceIds = [intent.Id],
            ToAddress = policy.EmailAddress, Subject = title, BodyText = proof is null ? intent.OriginalBodyText : BulkBody(db, title),
            RequiredProjectIds = scope.ProjectIds.ToArray(), RequiredPlanningEntryIds = scope.PlanningEntryIds.ToArray(),
            DedupKey = proof is null ? intent.SuppressionKey : $"task-bulk:{proof.Origin.CommandId}:{context.UserId}:Email",
            CreatedAt = now, NextAttemptAt = now, TransportOutcome = NoticeTransportOutcome.Queued,
            BulkRenderedMembershipJson = map };
        if (previous is null) db.Emails.Add(email);
        intent.DeliveryEmailId = email.Id; intent.NextPolicyCheckAt = null;
        if (proof is null) { intent.Disposition = NoticeIntentDisposition.Queued; intent.UpdatedAt = now; }
        else SetProof(intent, proof, proof.Progress.Select(value => keys.Contains(value.Key)
            ? value with { State = NoticeIntentDisposition.Queued, UpdatedAt = now, EmailId = email.Id } : value).ToImmutableArray(), now);
        return email;
    }
    static void ScheduleCheck(NoticeDeliveryIntent intent, DateTimeOffset now, OrgSettings settings, bool initial = false)
    {
        var today = Clock.LocalDate(now, settings);
        if (intent.PolicyCheckLocalDate != today) { intent.PolicyCheckLocalDate = today; intent.PolicyChecksInDay = 0; }
        if (!initial) intent.PolicyChecksInDay = Math.Min(8, (intent.PolicyChecksInDay ?? 0) + 1);
        var count = intent.PolicyChecksInDay ?? 0;
        var next = count >= 8 ? ResolveLocal(settings, today.AddDays(1), TimeOnly.MinValue).ScheduledAtUtc
            : now.AddMinutes(count == 0 ? 1 : new[] { 1, 2, 4, 8, 16, 32, 60 }[count - 1]);
        intent.NextPolicyCheckAt = next < now.AddMinutes(1) ? now.AddMinutes(1) : next;
    }
    static async Task<NoticeDeliverySlot> Slot(NoticeRecipientContext context, Guid id, CancellationToken ct)
    {
        var db = Db(context); var slot = db.NoticeDeliverySlots.Local.FirstOrDefault(value => value.Id == id)
            ?? await db.NoticeDeliverySlots.SingleOrDefaultAsync(value => value.Id == id, ct);
        Demand(slot is not null && slot.UserId == context.UserId && slot.Kind == NoticeSlotKind.Digest && slot.WindowFrom <= slot.WindowThrough, "ForeignDigestSlot");
        return slot!;
    }
    static async Task<List<NoticeDeliveryIntent>> Intents(NoticeRecipientContext context, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var db = Db(context); var wanted = ids.Distinct().ToArray();
        var found = await db.NoticeDeliveryIntents.Where(row => wanted.Contains(row.Id)).ToListAsync(ct);
        foreach (var row in db.NoticeDeliveryIntents.Local.Where(row => wanted.Contains(row.Id))) if (found.All(value => value.Id != row.Id)) found.Add(row);
        Demand(found.Count == wanted.Length && found.All(row => row.UserId == context.UserId), "ForeignRenderedIntent"); return found;
    }
    static async Task<EmailMessage> Email(NoticeRecipientContext context, Guid id, CancellationToken ct)
    {
        var db = Db(context); var email = db.Emails.Local.FirstOrDefault(row => row.Id == id) ?? await db.Emails.SingleOrDefaultAsync(row => row.Id == id, ct);
        Demand(email is not null && email.UserId == context.UserId, "ForeignNoticeEmail"); return email!;
    }
    static bool PossiblyAccepted(EmailMessage email) => email.AcceptanceUncertainAt is not null || email.ProviderAcceptedAt is not null
        || email.TransportOutcome is NoticeTransportOutcome.InFlight or NoticeTransportOutcome.Uncertain or NoticeTransportOutcome.Accepted or NoticeTransportOutcome.Simulated or NoticeTransportOutcome.Unknown;
    static async Task<string?> CurrentRefusal(PolicyFrame frame, string eventType,
        Guid? projectId, bool direct, NoticeRequiredScope scope, CancellationToken ct)
    {
        var policy = frame.Policy;
        if (!policy.IsActive) return NoticeIntentDisposition.RecipientInactive;
        if (string.IsNullOrEmpty(policy.EmailAddress)) return NoticeIntentDisposition.NoAddress;
        if (!await frame.AllowedFor(scope, ct)) return NoticeIntentDisposition.AccessRemoved;
        if (!(await frame.ChannelsFor(eventType, ct)).Email) return NoticeIntentDisposition.ChannelOff;
        if (await frame.MutedFor(eventType, projectId, direct, ct)) return NoticeIntentDisposition.Muted;
        return null;
    }
    static async Task RefreshIntent(NoticeRecipientContext context, NoticeDeliveryIntent intent, DateTimeOffset now, CancellationToken ct)
    {
        var frame = new PolicyFrame(context, await ReadRecipientPolicyAsync(context, ct));
        if (intent.BulkPolicyJson is null)
        {
            if (NoticeIntentDisposition.Terminal.Contains(intent.Disposition)) return;
            var reason = await CurrentRefusal(frame, intent.EventType, intent.ProjectId, intent.IsDirectRecipient, Scope(intent), ct);
            if (reason is not null) { intent.Disposition = reason; intent.UpdatedAt = now; intent.NextPolicyCheckAt = null; }
            return;
        }
        var proof = Proof(intent); var progress = proof.Progress.ToBuilder();
        foreach (var original in proof.Origin.Obligations.Where(value => value.EmailEligible))
        {
            var index = original.Key - 1; // v1 keys/progress are the closed sequential1..N set.
            if (NoticeIntentDisposition.Terminal.Contains(progress[index].State)) continue;
            var reason = await CurrentRefusal(frame, original.EventType, original.SourceProjectId, original.IsDirectRecipient,
                new(original.RequiredProjectIds, original.RequiredPlanningEntryIds), ct);
            if (reason is not null) progress[index] = progress[index] with { State = reason, RefusalReason = reason, UpdatedAt = now };
        }
        SetProof(intent, proof, progress.ToImmutable(), now);
    }
    static async Task SetRefusal(NoticeRecipientContext context, NoticeDeliveryIntent intent, string state, string reason, DateTimeOffset now, CancellationToken ct)
    {
        _ = Db(context); intent.NextPolicyCheckAt = null;
        if (intent.BulkPolicyJson is null)
        {
            if (NoticeIntentDisposition.Terminal.Contains(intent.Disposition) && intent.Disposition != state
                && !(state == NoticeIntentDisposition.AccessRemoved && intent.Disposition is NoticeIntentDisposition.Accepted
                    or NoticeIntentDisposition.Simulated or NoticeIntentDisposition.Unknown or NoticeIntentDisposition.Exhausted)) return;
            intent.Disposition = state; intent.UpdatedAt = now; return;
        }
        var proof = Proof(intent);
        var after = proof.Progress.Select(value => !NoticeIntentDisposition.Terminal.Contains(value.State)
            ? value with { State = state, RefusalReason = reason, UpdatedAt = now } : value).ToImmutableArray();
        SetProof(intent, proof, after, now);
        if (state == NoticeIntentDisposition.AccessRemoved && NoticeIntentDisposition.Terminal.Contains(intent.Disposition)) intent.Disposition = state;
        await Task.CompletedTask;
    }
    static async Task<NoticeRenderedMembership> Membership(NoticeRecipientContext context, EmailMessage email, CancellationToken ct)
    {
        Demand(email.NoticeProtocolVersion == 1 && email.RenderedOccurrenceIds is not null && email.RenderedOccurrenceIds.Length <= 10
            && email.RenderedOccurrenceIds.Distinct().Count() == email.RenderedOccurrenceIds.Length, "InvalidNoticeProtocol");
        var intents = await Intents(context, email.RenderedOccurrenceIds!, ct);
        var proofs = intents.Where(row => row.BulkPolicyJson != null).ToDictionary(row => row.Id, Proof);
        var bulk = NoticeDeliveryProof.DecodeRenderedBulkMap(email.BulkRenderedMembershipJson, context.UserId, email.RenderedOccurrenceIds!.ToImmutableArray(), proofs);
        return new(email.RenderedOccurrenceIds.ToImmutableArray(), bulk);
    }
    static async Task SuppressPayload(NoticeRecipientContext context, EmailMessage email, DateTimeOffset now, string reason, CancellationToken ct)
    {
        if (PossiblyAccepted(email)) return;
        email.SuppressedAt ??= now; email.LastError = reason; email.TransportOutcome = NoticeTransportOutcome.SuppressedBeforeSend; email.TransportObservedAt = now;
        await ReleaseSafeMembership(context, email, now, ct);
    }
    static async Task ReleaseSafeMembership(NoticeRecipientContext context, EmailMessage email, DateTimeOffset now, CancellationToken ct)
    {
        Demand(!PossiblyAccepted(email), "CannotReleasePossibleAcceptance");
        var membership = await Membership(context, email, ct); var intents = await Intents(context, membership.IntentIds, ct);
        foreach (var intent in intents.Where(row => row.DeliveryEmailId == email.Id))
        {
            if (NoticeIntentDisposition.Terminal.Contains(context.Db.Entry(intent).Property(row => row.Disposition).OriginalValue)) continue;
            intent.DeliveryEmailId = null;
            if (intent.BulkPolicyJson is { })
            {
                var proof = Proof(intent);
                SetProof(intent, proof, proof.Progress.Select(value => value.EmailId == email.Id && !NoticeIntentDisposition.Terminal.Contains(value.State)
                    ? value with { State = NoticeIntentDisposition.HeldAway, EmailId = null, UpdatedAt = now } : value).ToImmutableArray(), now);
            }
            else if (!NoticeIntentDisposition.Terminal.Contains(intent.Disposition)) { intent.Disposition = NoticeIntentDisposition.HeldAway; intent.UpdatedAt = now; }
            if (!NoticeIntentDisposition.Terminal.Contains(intent.Disposition)) ScheduleCheck(intent, now, (await ReadRecipientPolicyAsync(context, ct)).Settings, true);
        }
    }
    public static async Task<NoticePolicyChangeResult> ObservePolicyChangeAsync(NoticeRecipientContext context, NoticePolicyChange change,
        DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        var db = Db(context); Utc(nowUtc);
        var all = await db.NoticeDeliveryIntents.Where(row => row.UserId == context.UserId).ToListAsync(ct);
        foreach (var local in db.NoticeDeliveryIntents.Local.Where(row => row.UserId == context.UserId)) if (all.All(row => row.Id != local.Id)) all.Add(local);
        var affected = all.Where(row => change.Kind == NoticePolicyChangeKind.PlanningWithdrawal
                ? change.PlanningEntryId is { } entry && row.RequiredPlanningEntryIds.Contains(entry)
                : row.BulkPolicyJson is not null || (change.EventType is null || row.EventType == change.EventType)
                    && (change.ProjectId is null || row.ProjectId == change.ProjectId || row.RequiredProjectIds.Contains(change.ProjectId.Value))).ToArray();
        var emails = new HashSet<Guid>(); var slots = new HashSet<Guid>(); int suppressed = 0, held = 0;
        foreach (var intent in affected)
        {
            var before = intent.Disposition;
            if (change.Kind == NoticePolicyChangeKind.PlanningWithdrawal)
                await SetRefusal(context, intent, NoticeIntentDisposition.AccessRemoved, NoticeRefusalReason.PlanningAssignmentWithdrawn, nowUtc, ct);
            else await RefreshIntent(context, intent, nowUtc, ct);
            if (before != intent.Disposition && NoticeIntentDisposition.Refusals.Contains(intent.Disposition)) suppressed++;
            if (intent.DeliveryEmailId is { } emailId)
            {
                var email = (await Email(context, emailId, ct))!;
                var policy = await ReadRecipientPolicyAsync(context, ct);
                var unsafeSource = NoticeIntentDisposition.Refusals.Contains(intent.Disposition)
                    || intent.BulkPolicyJson is not null && !await ExactBulkStillEligible(context, intent, email, nowUtc, ct);
                var disabledDigest = email.NoticeSlotId is not null && NoDigest(policy);
                if ((unsafeSource || disabledDigest) && !PossiblyAccepted(email))
                {
                    await SuppressPayload(context, email, nowUtc, change.Kind.ToString(), ct); emails.Add(email.Id);
                    if (email.NoticeSlotId is { } slotId) { var slot = (await Slot(context, slotId, ct))!; slot.State = NoticeSlotState.Ready; slot.UpdatedAt = nowUtc; slots.Add(slotId); }
                }
            }
            if (intent.DeliveryEmailId is null && !NoticeIntentDisposition.Terminal.Contains(intent.Disposition))
            {
                var policy = await ReadRecipientPolicyAsync(context, ct);
                if (change.Kind == NoticePolicyChangeKind.EnhancedPreferences && !policy.Preferences.HoldImmediateEmailWhenAway && policy.Settings.IsFlagOn("people_screens"))
                {
                    await QueueImmediate(context, intent, intent.RequiredPlanningEntryIds.Length > 0 ? "PlanningEntryChanged" : "Immediate", nowUtc, ct);
                }
                else
                {
                    Hold(intent, NoDigest(policy) ? NoticeIntentDisposition.HeldNoDigest : NoticeIntentDisposition.HeldAway, nowUtc);
                    ScheduleCheck(intent, nowUtc, policy.Settings, true); held++;
                }
            }
        }
        return new(suppressed, held, emails.Count, slots.OrderBy(Id, StringComparer.Ordinal).ToImmutableArray());
    }
    static void Hold(NoticeDeliveryIntent intent, string state, DateTimeOffset now)
    {
        if (intent.BulkPolicyJson is null) { intent.Disposition = state; intent.UpdatedAt = now; return; }
        var proof = Proof(intent);
        SetProof(intent, proof, proof.Progress.Select(value => !NoticeIntentDisposition.Terminal.Contains(value.State)
            ? value with { State = state, UpdatedAt = now, EmailId = null } : value).ToImmutableArray(), now);
    }
    public static async Task<ImmutableArray<NoticePendingCandidate>> SelectPendingAsync(HubDb db, DateTimeOffset nowUtc,
        int maxCandidates = 100, CancellationToken ct = default)
    {
        Demand(maxCandidates is > 0 and <= 100); Utc(nowUtc);
        if (!(await Settings(db, ct)).IsFlagOn("people_screens")) return [];
        return (await db.NoticeDeliveryIntents.AsNoTracking().Where(row => row.DeliveryEmailId == null && row.NextPolicyCheckAt <= nowUtc
            && (row.Disposition == NoticeIntentDisposition.PendingAvailability || row.Disposition == NoticeIntentDisposition.HeldAway
                || row.Disposition == NoticeIntentDisposition.HeldNoDigest || row.Disposition == NoticeIntentDisposition.Mixed))
            .OrderBy(row => row.NextPolicyCheckAt).ThenBy(row => row.Id).Take(maxCandidates)
            .Select(row => new NoticePendingCandidate(row.Id, row.UserId, row.NextPolicyCheckAt!.Value)).ToListAsync(ct)).ToImmutableArray();
    }
    public static async Task<NoticePolicySweepResult> ReconsiderPendingAsync(NoticeRecipientContext context, ImmutableArray<Guid> intentIds,
        DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Utc(nowUtc); Demand(!intentIds.IsDefault && intentIds.Length <= 100); var policy = await ReadRecipientPolicyAsync(context, ct);
        if (!policy.Settings.IsFlagOn("people_screens")) return new(0, 0, 0);
        int examined = 0, changed = 0, parked = 0;
        foreach (var intent in await Intents(context, intentIds, ct))
        {
            if (intent.DeliveryEmailId is not null || intent.NextPolicyCheckAt > nowUtc || intent.NextPolicyCheckAt is null || NoticeIntentDisposition.Terminal.Contains(intent.Disposition)) continue;
            examined++; var previous = intent.Disposition; await RefreshIntent(context, intent, nowUtc, ct);
            if (!NoticeIntentDisposition.Terminal.Contains(intent.Disposition))
            {
                if (!policy.Preferences.HoldImmediateEmailWhenAway)
                {
                    var queued = await QueueImmediate(context, intent, intent.RequiredPlanningEntryIds.Length > 0 ? "PlanningEntryChanged" : "Immediate", nowUtc, ct);
                    if (queued is null && intent.NextPolicyCheckAt > nowUtc) parked++;
                }
                else
                {
                    var day = await ReadDayAvailabilityAsync(context, Clock.LocalDate(nowUtc, policy.Settings), ct);
                    Hold(intent, day.IsAway is null ? NoticeIntentDisposition.PendingAvailability : NoDigest(policy) ? NoticeIntentDisposition.HeldNoDigest : NoticeIntentDisposition.HeldAway, nowUtc);
                    ScheduleCheck(intent, nowUtc, policy.Settings); parked++;
                }
            }
            if (previous != intent.Disposition) changed++;
        }
        return new(examined, changed, parked);
    }
    public static async Task<NoticeDigestPreparation> PrepareDigestAsync(NoticeRecipientContext context, Guid slotId, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Utc(nowUtc); var db = Db(context); var slot = (await Slot(context, slotId, ct))!; var policy = await ReadRecipientPolicyAsync(context, ct);
        NoticeDigestPreparation Result(NoticeDigestPreparationDecision decision, ImmutableArray<NoticeHeldCandidate> held = default)
            => new(context, decision, slot, held.IsDefault ? [] : held);
        if (!policy.Settings.IsFlagOn("people_screens")) return Result(NoticeDigestPreparationDecision.Dormant);
        if (slot.State is NoticeSlotState.Accepted or NoticeSlotState.Simulated or NoticeSlotState.Unknown or NoticeSlotState.Empty) return Result(NoticeDigestPreparationDecision.AlreadyTerminal);
        var versions = await db.Emails.Where(row => row.NoticeSlotId == slotId).ToListAsync(ct);
        versions.AddRange(db.Emails.Local.Where(row => row.NoticeSlotId == slotId && versions.All(old => old.Id != row.Id)));
        if (versions.Any(row => row.AcceptanceUncertainAt is not null || row.TransportOutcome == NoticeTransportOutcome.Uncertain)) return Result(NoticeDigestPreparationDecision.Uncertain);
        if (versions.Any(row => row.TransportOutcome == NoticeTransportOutcome.InFlight)) return Result(NoticeDigestPreparationDecision.InFlight);
        if (versions.Any(row => row.ProviderAcceptedAt is not null || row.TransportOutcome is NoticeTransportOutcome.Accepted or NoticeTransportOutcome.Simulated or NoticeTransportOutcome.Unknown))
            return Result(NoticeDigestPreparationDecision.AlreadyTerminal);
        if (versions.Any(row => row.TransportOutcome == NoticeTransportOutcome.SuppressedBeforeSend && row.LastError == "TransportChanged"))
            return Result(NoticeDigestPreparationDecision.Dormant);
        if (slot.CompositionVersion >= 3 || slot.ExternalAttempts >= 8)
        {
            foreach (var email in versions.Where(row => row.SuppressedAt is null)) await SuppressPayload(context, email, nowUtc, "CompositionExhausted", ct);
            slot.State = NoticeSlotState.Exhausted; slot.UpdatedAt = nowUtc;
            return Result(NoticeDigestPreparationDecision.Exhausted);
        }
        if (versions.Any(row => row.NoticeCompositionVersion == slot.CompositionVersion && row.SuppressedAt is null)) return Result(NoticeDigestPreparationDecision.NotEligible);
        if (!policy.IsActive || string.IsNullOrEmpty(policy.EmailAddress) || NoDigest(policy)) return Result(NoticeDigestPreparationDecision.NotEligible);
        if (policy.HoldDigestWhenAway)
        {
            var day = await ReadDayAvailabilityAsync(context, Clock.LocalDate(nowUtc, policy.Settings), ct);
            if (day.IsAway is null || day.IsAway == true) return Result(NoticeDigestPreparationDecision.NotEligible);
        }
        var pending = await db.NoticeDeliveryIntents.Where(row => row.UserId == context.UserId && row.CreatedAt <= slot.WindowThrough
            && (row.Disposition == NoticeIntentDisposition.HeldAway || row.Disposition == NoticeIntentDisposition.HeldNoDigest
                || row.Disposition == NoticeIntentDisposition.PendingAvailability || row.Disposition == NoticeIntentDisposition.Pending
                || row.Disposition == NoticeIntentDisposition.Mixed && row.NextPolicyCheckAt != null))
            .OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).ToListAsync(ct);
        pending.AddRange(db.NoticeDeliveryIntents.Local.Where(row => row.UserId == context.UserId && row.CreatedAt <= slot.WindowThrough
            && pending.All(old => old.Id != row.Id) && !NoticeIntentDisposition.Terminal.Contains(row.Disposition)));
        var candidates = ImmutableArray.CreateBuilder<NoticeHeldCandidate>();
        foreach (var intent in pending.OrderBy(row => row.CreatedAt).ThenBy(row => row.Id))
        {
            if (candidates.Count == 10) break;
            if (intent.DeliveryEmailId is { } previous)
            {
                var email = (await Email(context, previous, ct))!;
                if (email.NoticeSlotId is not null || PossiblyAccepted(email)) continue;
                await SuppressPayload(context, email, nowUtc, "MoveHeldToDigest", ct);
            }
            await RefreshIntent(context, intent, nowUtc, ct);
            if (NoticeIntentDisposition.Terminal.Contains(intent.Disposition)) continue;
            if (intent.BulkPolicyJson is null)
                candidates.Add(new(intent.Id, intent.UserId, intent.CreatedAt, intent.OriginalSubject, intent.OriginalBodyText, intent.OriginalLinkPath, Scope(intent), null));
            else
            {
                var proof = Proof(intent); var keys = PendingKeys(proof);
                if (keys.IsEmpty) { intent.NextPolicyCheckAt = null; continue; }
                var title = Text.Get("notify.task_bulk_summary", BulkTaskCount(proof, keys));
                candidates.Add(new(intent.Id, intent.UserId, intent.CreatedAt, title, BulkBody(db, title), "/tasks", BulkScope(proof, keys), NoticeDeliveryProof.Render(intent.Id, proof, keys)));
            }
        }
        return Result(NoticeDigestPreparationDecision.Compose, candidates.ToImmutable());
    }
    static void Preparation(NoticeRecipientContext context, NoticeDigestPreparation preparation)
    { _ = Db(context); Demand(ReferenceEquals(context, preparation.Context) && preparation.Decision == NoticeDigestPreparationDecision.Compose, "InvalidDigestPreparation"); }
    public static async Task<NoticeDigestReservationResult> ReserveDigestAsync(NoticeRecipientContext context, NoticeDigestPreparation preparation,
        NoticeDigestRenderedPayload payload, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Utc(nowUtc); Preparation(context, preparation); var db = Db(context); var slot = (await Slot(context, preparation.SlotId, ct))!;
        var policy = await ReadRecipientPolicyAsync(context, ct); Enabled(policy.Settings);
        Demand(slot.CompositionVersion + 1 == preparation.NextCompositionVersion && slot.CompositionVersion < 3 && slot.ExternalAttempts < 8
            && payload.SlotId == slot.Id && payload.Window == preparation.Window && policy.IsActive && !string.IsNullOrEmpty(policy.EmailAddress)
            && !NoDigest(policy), "StaleDigestPreparation");
        if (policy.HoldDigestWhenAway) Demand((await ReadDay(context, policy, Clock.LocalDate(nowUtc, policy.Settings))).IsAway == false, "DigestAvailabilityChanged");
        NotificationDigestComposition.DemandScope(payload.Scope);
        Demand(await Allowed(context, payload.Scope, ct), "DigestScopeRemoved");
        var ids = payload.Membership.IntentIds;
        Demand(!ids.IsDefault && ids.Length <= 10 && ids.Distinct().Count() == ids.Length
            && EqualIds(ids, preparation.Held.Select(row => row.IntentId)), "InvalidDigestMembership");
        var intents = await Intents(context, ids, ct); var proofs = new Dictionary<Guid, NoticeBulkProof>();
        foreach (var intent in intents)
        {
            await RefreshIntent(context, intent, nowUtc, ct);
            Demand(intent.DeliveryEmailId is null && !NoticeIntentDisposition.Terminal.Contains(intent.Disposition), "StaleHeldMembership");
            var candidate = preparation.Held.Single(row => row.IntentId == intent.Id);
            Demand(candidate.Scope.ProjectIds.All(payload.Scope.ProjectIds.Contains) && candidate.Scope.PlanningEntryIds.All(payload.Scope.PlanningEntryIds.Contains), "MissingRenderedScope");
            if (intent.BulkPolicyJson is not null)
            {
                var proof = Proof(intent); proofs[intent.Id] = proof;
                var member = payload.Membership.BulkMemberships.SingleOrDefault(row => row.IntentId == intent.Id);
                Demand(member is not null && candidate.BulkMembership is not null && member.ObligationKeys.SequenceEqual(candidate.BulkMembership.ObligationKeys)
                    && member.ObligationKeys.SequenceEqual(PendingKeys(proof)), "StaleBulkMembership");
            }
        }
        var bulkJson = NoticeDeliveryProof.EncodeRenderedBulkMap(context.UserId, ids, payload.Membership.BulkMemberships, proofs);
        var document = new NoticeDigestPolicyDocument(1, context.UserId, slot.Id, preparation.NextCompositionVersion, preparation.Window, payload.Scope,
            payload.RenderedPolicy, payload.RenderedPolicy.UpdateActivity.IsEmpty ? [] : ProjectionCodes.Select(code => new NoticeDigestProjectionFlag(code, policy.Settings.IsFlagOn(code))).ToImmutableArray());
        var policyJson = NoticeDeliveryProof.EncodeDigestPolicy(document);
        Demand(await DigestPolicyAllowed(context, document, policy, ct), "DigestPolicyRemoved");
        var email = new EmailMessage { UserId = context.UserId, Kind = "Digest", NoticeProtocolVersion = 1, NoticeSlotId = slot.Id,
            NoticeCompositionVersion = preparation.NextCompositionVersion, RenderedOccurrenceIds = ids.ToArray(), BulkRenderedMembershipJson = bulkJson,
            NoticeDigestPolicyJson = policyJson, ToAddress = policy.EmailAddress, Subject = payload.Subject, BodyText = payload.BodyText,
            RequiredProjectIds = payload.Scope.ProjectIds.ToArray(), RequiredPlanningEntryIds = payload.Scope.PlanningEntryIds.ToArray(),
            DedupKey = $"notice-digest:{slot.Id}:{preparation.NextCompositionVersion}", CreatedAt = nowUtc, NextAttemptAt = nowUtc, TransportOutcome = NoticeTransportOutcome.Queued };
        db.Emails.Add(email);
        foreach (var intent in intents)
        {
            intent.DeliveryEmailId = email.Id; intent.NextPolicyCheckAt = null;
            if (intent.BulkPolicyJson is null) { intent.Disposition = NoticeIntentDisposition.ReservedForDigest; intent.UpdatedAt = nowUtc; }
            else
            {
                var proof = proofs[intent.Id]; var member = payload.Membership.BulkMemberships.Single(row => row.IntentId == intent.Id);
                SetProof(intent, proof, proof.Progress.Select(value => member.ObligationKeys.Contains(value.Key)
                    ? value with { State = NoticeIntentDisposition.ReservedForDigest, UpdatedAt = nowUtc, EmailId = email.Id } : value).ToImmutableArray(), nowUtc);
            }
        }
        slot.CompositionVersion++; slot.State = NoticeSlotState.Queued; slot.UpdatedAt = nowUtc;
        var preferences = await UserPreferences.GetOrCreate(db, context.UserId, ct);
        if (preferences.LastDigestAt is null || preferences.LastDigestAt < slot.WindowThrough) preferences.LastDigestAt = slot.WindowThrough;
        return new(slot.Id, slot.CompositionVersion, email.Id, slot.State, ids);
    }
    public static async Task<NoticeDigestReservationResult> CompleteEmptyDigestAsync(NoticeRecipientContext context, NoticeDigestPreparation preparation,
        DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Utc(nowUtc); Preparation(context, preparation); Demand(preparation.Held.IsEmpty, "OmittedHeldMembership");
        var slot = (await Slot(context, preparation.SlotId, ct))!; var policy = await ReadRecipientPolicyAsync(context, ct); Enabled(policy.Settings);
        Demand(policy.IsActive && !string.IsNullOrEmpty(policy.EmailAddress) && !NoDigest(policy)
            && slot.CompositionVersion + 1 == preparation.NextCompositionVersion);
        if (policy.HoldDigestWhenAway) Demand((await ReadDay(context, policy, Clock.LocalDate(nowUtc, policy.Settings))).IsAway == false, "DigestAvailabilityChanged");
        slot.State = NoticeSlotState.Empty; slot.UpdatedAt = nowUtc;
        var preferences = await UserPreferences.GetOrCreate(context.Db, context.UserId, ct);
        if (preferences.LastDigestAt is null || preferences.LastDigestAt < slot.WindowThrough) preferences.LastDigestAt = slot.WindowThrough;
        return new(slot.Id, slot.CompositionVersion, null, slot.State, []);
    }
    static async Task<bool> SafeExhausted(NoticeRecipientContext context, NoticeDeliverySlot slot, CancellationToken ct)
    {
        if (slot.UserId != context.UserId || slot.Kind != NoticeSlotKind.Digest || slot.State != NoticeSlotState.Exhausted || slot.CarryForwardToSlotId is not null
            || slot.CompositionVersion is < 1 or > 3 || slot.WindowFrom is null || slot.WindowThrough is null) return false;
        var emails = await context.Db.Emails.AsNoTracking().Where(row => row.NoticeSlotId == slot.Id).ToListAsync(ct);
        if (emails.Count != slot.CompositionVersion || emails.Select(row => row.NoticeCompositionVersion).Distinct().Count() != emails.Count) return false;
        foreach (var email in emails)
        {
            if (email.UserId != context.UserId || email.NoticeProtocolVersion != 1 || email.NoticeCompositionVersion is null
                || email.NoticeCompositionVersion < 1 || email.NoticeCompositionVersion > slot.CompositionVersion || PossiblyAccepted(email)
                || email.TransportOutcome is not (NoticeTransportOutcome.SuppressedBeforeSend or NoticeTransportOutcome.Failed or NoticeTransportOutcome.Exhausted)) return false;
            try
            {
                await Membership(context, email, ct);
                _ = NoticeDeliveryProof.DecodeDigestPolicy(email.NoticeDigestPolicyJson!, context.UserId, slot.Id, email.NoticeCompositionVersion.Value,
                    new(slot.WindowFrom.Value, slot.WindowThrough.Value), Scope(email));
            }
            catch (NoticeProofException) { return false; }
        }
        return true;
    }
    public static async Task<DateTimeOffset?> ReadCarryForwardFromAsync(NoticeRecipientContext context, CancellationToken ct = default)
    {
        var db = Db(context); var leaves = await db.NoticeDeliverySlots.Where(row => row.UserId == context.UserId && row.Kind == NoticeSlotKind.Digest
            && row.State == NoticeSlotState.Exhausted && row.CarryForwardToSlotId == null).ToListAsync(ct);
        DateTimeOffset? from = null;
        foreach (var leaf in leaves) if (await SafeExhausted(context, leaf, ct) && (from is null || leaf.WindowFrom < from)) from = leaf.WindowFrom;
        return from;
    }
    public static async Task<int> TransferCarryForwardAsync(NoticeRecipientContext context, Guid destinationSlotId, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Utc(nowUtc); var db = Db(context); var destination = (await Slot(context, destinationSlotId, ct))!; int transferred = 0;
        var leaves = await db.NoticeDeliverySlots.Where(row => row.UserId == context.UserId && row.Kind == NoticeSlotKind.Digest
            && row.State == NoticeSlotState.Exhausted && row.CarryForwardToSlotId == null && row.Id != destinationSlotId).ToListAsync(ct);
        foreach (var leaf in leaves)
        {
            if (leaf.WindowFrom < destination.WindowFrom || leaf.WindowThrough >= destination.WindowThrough || !await SafeExhausted(context, leaf, ct)) continue;
            leaf.CarryForwardToSlotId = destination.Id; leaf.UpdatedAt = nowUtc; transferred++;
        }
        return transferred;
    }
    static async Task<bool> DigestPolicyAllowed(NoticeRecipientContext context, NoticeDigestPolicyDocument document, NoticeRecipientPolicy policy, CancellationToken ct)
    {
        if (NoDigest(policy) || document.RenderedPolicy.RenderedSectionCodes.Any(policy.SectionsOff.Contains)) return false;
        var db = Db(context); var rendered = document.RenderedPolicy;
        if (await db.Follows.AsNoTracking().CountAsync(row => row.UserId == context.UserId && row.Level == FollowLevel.AllActivity
            && rendered.UpdateProjectIds.Contains(row.ProjectId), ct) != rendered.UpdateProjectIds.Length) return false;
        if (await db.Users.AsNoTracking().CountAsync(row => rendered.StaffDirectReportIds.Contains(row.Id)
            && row.IsActive && row.SupervisorId == context.UserId, ct) != rendered.StaffDirectReportIds.Length) return false;
        if (!rendered.StaffAssignmentNotificationIds.IsEmpty)
        {
            var roles = (await db.UserRoles.AsNoTracking().Where(row => row.UserId == context.UserId).Select(row => row.Role).ToListAsync(ct)).ToHashSet();
            var me = new CurrentUser { Id = context.UserId, IsActive = policy.IsActive, Resolved = true, Roles = roles };
            var visible = await PlanningEndpoints.VisibleNotifications(db, new Access(db, me));
            if (await visible.CountAsync(row => rendered.StaffAssignmentNotificationIds.Contains(row.Id) && row.EventType == NotificationEvents.StaffAssignment, ct)
                != rendered.StaffAssignmentNotificationIds.Length) return false;
        }
        foreach (var source in rendered.UpdateActivity)
        {
            var visible = await Digest.VisibleActivity(db, context.UserId, source.ProjectId);
            if (await visible.CountAsync(row => source.ActivityIds.Contains(row.Id), ct) != source.ActivityIds.Length) return false;
        }
        if (document.ProjectionFlags.Any(flag => flag.EnabledAtRender && !policy.Settings.IsFlagOn(flag.Code))) return false;
        if (rendered.IncludesAdminReview && (!policy.Settings.IsFlagOn(AdminReviewEndpoints.Flag)
            || !await db.UserRoles.AsNoTracking().AnyAsync(row => row.UserId == context.UserId && row.Role == SystemRole.Admin, ct))) return false;
        return true;
    }
    static async Task<bool> ExactBulkStillEligible(NoticeRecipientContext context, NoticeDeliveryIntent intent, EmailMessage email, DateTimeOffset now, CancellationToken ct)
    {
        var members = await Membership(context, email, ct); var map = members.BulkMemberships.Single(row => row.IntentId == intent.Id);
        var proof = Proof(intent);
        return map.ObligationKeys.All(key => !NoticeIntentDisposition.Refusals.Contains(proof.Progress.Single(value => value.Key == key).State));
    }
    public static async Task<NoticeClaimResult> ClaimAsync(NoticeRecipientContext context, Guid emailId, string declaredTransportKind,
        NoticeClaimPurpose purpose, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        var db = Db(context); Utc(nowUtc); Demand(NoticeTransportKind.All.Contains(declaredTransportKind) && Enum.IsDefined(purpose));
        var email = (await Email(context, emailId, ct))!;
        NoticeClaimResult Decline(string decision, string? reason = null) => new(decision, null, reason);
        if (IsLegacy(email)) return Decline(NoticeClaimDecision.Legacy);
        var policy = await ReadRecipientPolicyAsync(context, ct);
        if (!policy.Settings.IsFlagOn("people_screens")) return Decline(NoticeClaimDecision.Dormant);
        if (email.ProviderAcceptedAt is not null || email.TransportOutcome is NoticeTransportOutcome.Accepted or NoticeTransportOutcome.Simulated or NoticeTransportOutcome.Unknown)
            return Decline(NoticeClaimDecision.AlreadyTerminal);
        if (email.AcceptanceUncertainAt is not null || email.TransportOutcome == NoticeTransportOutcome.Uncertain) return Decline(NoticeClaimDecision.Uncertain);
        if (email.TransportOutcome == NoticeTransportOutcome.InFlight) return Decline(NoticeClaimDecision.InFlight);
        if (email.SuppressedAt is not null || email.TransportOutcome is NoticeTransportOutcome.Exhausted or NoticeTransportOutcome.SuppressedBeforeSend)
            return Decline(NoticeClaimDecision.AlreadyTerminal);
        if (email.NextAttemptAt > nowUtc) return Decline(NoticeClaimDecision.AlreadyTerminal, "NotDue");
        NoticeRenderedMembership membership; NoticeDigestPolicyDocument? document = null; NoticeDeliverySlot? slot = null;
        try
        {
            membership = await Membership(context, email, ct);
            Demand(email.NoticeProtocolVersion == 1 && email.TransportOutcome is NoticeTransportOutcome.Queued or NoticeTransportOutcome.Failed, "InvalidNoticeState");
            if (email.NoticeSlotId is { } slotId)
            {
                slot = (await Slot(context, slotId, ct))!;
                Demand(email.Kind == "Digest" && email.NoticeCompositionVersion == slot.CompositionVersion, "StaleDigestPayload");
                document = NoticeDeliveryProof.DecodeDigestPolicy(email.NoticeDigestPolicyJson!, context.UserId, slot.Id, slot.CompositionVersion,
                    new(slot.WindowFrom!.Value, slot.WindowThrough!.Value), Scope(email));
                Demand(slot.State is NoticeSlotState.Queued or NoticeSlotState.Ready or NoticeSlotState.Paused, "InvalidDigestState");
            }
            else Demand(email.NoticeCompositionVersion is null && email.NoticeDigestPolicyJson is null && membership.IntentIds.Length == 1
                && email.Kind is "Immediate" or "PlanningEntryChanged" or "PlanningWithdrawal", "InvalidImmediatePayload");
            foreach (var intent in await Intents(context, membership.IntentIds, ct)) Demand(intent.DeliveryEmailId == email.Id, "StaleIntentPointer");
        }
        catch (NoticeProofException)
        { email.SuppressedAt ??= nowUtc; email.LastError = "InvalidNoticeMetadata"; return Decline(NoticeClaimDecision.Suppress, email.LastError); }
        var intents = await Intents(context, membership.IntentIds, ct);
        if (email.TransportKind is not null && email.TransportKind != declaredTransportKind)
        {
            // A conclusively unsent payload cannot be relabelled or reopened by changing configuration.
            email.NextAttemptAt = null; email.SuppressedAt ??= nowUtc; email.LastError = "TransportChanged";
            email.TransportOutcome = NoticeTransportOutcome.SuppressedBeforeSend; email.TransportObservedAt = nowUtc;
            foreach (var intent in intents.Where(row => !NoticeIntentDisposition.Terminal.Contains(row.Disposition)))
                await SetRefusal(context, intent, NoticeIntentDisposition.AccessRemoved, NoticeRefusalReason.TransportChanged, nowUtc, ct);
            if (slot is not null) { slot.State = NoticeSlotState.Paused; slot.UpdatedAt = nowUtc; }
            return Decline(NoticeClaimDecision.Suppress, email.LastError);
        }
        var refused = false;
        foreach (var intent in intents)
        {
            await RefreshIntent(context, intent, nowUtc, ct);
            if (intent.BulkPolicyJson is not null) refused |= !await ExactBulkStillEligible(context, intent, email, nowUtc, ct);
            else refused |= NoticeIntentDisposition.Refusals.Contains(intent.Disposition);
        }
        var addressChanged = !string.IsNullOrEmpty(policy.EmailAddress) && !string.Equals(email.ToAddress, policy.EmailAddress, StringComparison.Ordinal);
        var sourceAllowed = policy.IsActive && !string.IsNullOrEmpty(policy.EmailAddress) && await Allowed(context, Scope(email), ct);
        var digestAllowed = document is null || await DigestPolicyAllowed(context, document, policy, ct);
        if (refused || addressChanged || !sourceAllowed || !digestAllowed)
        {
            if (addressChanged)
                foreach (var intent in intents) await SetRefusal(context, intent, NoticeIntentDisposition.AccessRemoved, NoticeRefusalReason.AddressChanged, nowUtc, ct);
            if (slot is null)
                foreach (var intent in intents.Where(row => !NoticeIntentDisposition.Terminal.Contains(row.Disposition)
                    && (row.BulkPolicyJson is null || !policy.IsActive || string.IsNullOrEmpty(policy.EmailAddress))))
                    await SetRefusal(context, intent, !policy.IsActive ? NoticeIntentDisposition.RecipientInactive : string.IsNullOrEmpty(policy.EmailAddress)
                        ? NoticeIntentDisposition.NoAddress : NoticeIntentDisposition.AccessRemoved,
                        !policy.IsActive ? NoticeRefusalReason.RecipientInactive : string.IsNullOrEmpty(policy.EmailAddress)
                            ? NoticeRefusalReason.NoAddress : addressChanged ? NoticeRefusalReason.AddressChanged : NoticeRefusalReason.AccessRemoved, nowUtc, ct);
            await SuppressPayload(context, email, nowUtc, addressChanged ? NoticeRefusalReason.AddressChanged : "CurrentPolicyRefused", ct);
            if (slot is not null) { slot.State = NoticeSlotState.Ready; slot.UpdatedAt = nowUtc; }
            return Decline(slot is null ? NoticeClaimDecision.Suppress : NoticeClaimDecision.Recompose, email.LastError);
        }
        var hold = slot is null ? policy.Preferences.HoldImmediateEmailWhenAway : policy.HoldDigestWhenAway;
        if (hold)
        {
            var day = await ReadDayAvailabilityAsync(context, Clock.LocalDate(nowUtc, policy.Settings), ct);
            if (day.IsAway is null || day.IsAway == true)
            {
                var state = day.IsAway is null ? NoticeIntentDisposition.PendingAvailability : NoDigest(policy) ? NoticeIntentDisposition.HeldNoDigest : NoticeIntentDisposition.HeldAway;
                foreach (var intent in intents)
                {
                    if (intent.BulkPolicyJson is null) { intent.Disposition = state; intent.UpdatedAt = nowUtc; }
                    else
                    {
                        var proof = Proof(intent); var keys = membership.BulkMemberships.Single(value => value.IntentId == intent.Id).ObligationKeys;
                        SetProof(intent, proof, proof.Progress.Select(value => keys.Contains(value.Key) && !NoticeIntentDisposition.Terminal.Contains(value.State)
                            ? value with { State = state, UpdatedAt = nowUtc } : value).ToImmutableArray(), nowUtc);
                    }
                    ScheduleCheck(intent, nowUtc, policy.Settings);
                }
                email.NextAttemptAt = intents.Count > 0 ? intents.Min(row => row.NextPolicyCheckAt) : nowUtc.AddHours(1);
                if (slot is not null) { slot.State = NoticeSlotState.Paused; slot.UpdatedAt = nowUtc; }
                return Decline(day.IsAway is null ? NoticeClaimDecision.PendingAvailability : NoDigest(policy) ? NoticeClaimDecision.HoldNoDigest : NoticeClaimDecision.HoldAway);
            }
        }
        if (email.Attempts >= 8 || slot?.ExternalAttempts >= 8)
        {
            email.TransportOutcome = NoticeTransportOutcome.Exhausted; email.TransportObservedAt = nowUtc;
            if (slot is not null)
            { await ReleaseSafeMembership(context, email, nowUtc, ct); slot.State = NoticeSlotState.Exhausted; slot.UpdatedAt = nowUtc; }
            else foreach (var intent in intents.Where(row => !NoticeIntentDisposition.Terminal.Contains(row.Disposition)))
                await SetRefusal(context, intent, NoticeIntentDisposition.Exhausted, NoticeRefusalReason.AttemptsExhausted, nowUtc, ct);
            return Decline(NoticeClaimDecision.Exhausted);
        }
        if (purpose == NoticeClaimPurpose.KnownFailureRetry && email.TransportOutcome != NoticeTransportOutcome.Failed
            || purpose == NoticeClaimPurpose.Initial && email.TransportOutcome != NoticeTransportOutcome.Queued) return Decline(NoticeClaimDecision.AlreadyTerminal, "WrongClaimPurpose");
        email.Attempts++; if (slot is not null) { slot.ExternalAttempts++; slot.State = NoticeSlotState.Queued; slot.UpdatedAt = nowUtc; }
        email.NoticeClaimToken = Guid.NewGuid(); email.NoticeClaimVersion = checked((email.NoticeClaimVersion ?? 0) + 1); email.NoticeClaimedAt = nowUtc;
        email.TransportKind ??= declaredTransportKind; email.TransportOutcome = NoticeTransportOutcome.InFlight; email.TransportObservedAt = nowUtc;
        return new(NoticeClaimDecision.Send, new(email.Id, context.UserId, email.NoticeClaimToken.Value, email.NoticeClaimVersion.Value,
            nowUtc, email.TransportKind, email.NoticeSlotId, email.NoticeCompositionVersion,
            new(email.ToAddress, email.Subject, email.BodyText, email.BodyHtml, Scope(email)), membership), null);
    }
    public static async Task<NoticeCompletionResult> CompleteAsync(NoticeRecipientContext context, NoticeSendClaim claim,
        NoticeSendObservation observation, CancellationToken ct = default)
    {
        var email = (await Email(context, claim.EmailId, ct))!;
        if (claim.UserId != context.UserId || email.NoticeClaimToken != claim.Token || email.NoticeClaimVersion != claim.Version
            || email.NoticeSlotId != claim.SlotId || email.NoticeCompositionVersion != claim.CompositionVersion || email.TransportKind != claim.TransportKind
            || email.NoticeClaimedAt != claim.ClaimedAt)
            return new(false, email.TransportOutcome ?? NoticeTransportOutcome.Unknown, 0, email.AcceptanceUncertainAt is not null);
        if (email.ProviderAcceptedAt is not null || email.TransportOutcome is NoticeTransportOutcome.Accepted or NoticeTransportOutcome.Simulated or NoticeTransportOutcome.Unknown)
            return new(false, email.TransportOutcome!, 0, email.AcceptanceUncertainAt is not null);
        if (email.TransportOutcome is not (NoticeTransportOutcome.InFlight or NoticeTransportOutcome.Uncertain))
            return new(false, email.TransportOutcome ?? NoticeTransportOutcome.Unknown, 0, email.AcceptanceUncertainAt is not null);
        Demand(observation.TransportKind == claim.TransportKind && NoticeTransportOutcome.All.Contains(observation.Outcome)
            && NoticeFailureCertainty.All.Contains(observation.FailureCertainty) && observation.ObservedAt.Offset == TimeSpan.Zero, "InvalidTransportObservation");
        var membership = await Membership(context, email, ct);
        Demand(email.Attempts is >= 1 and <= 8 && claim.Version is >= 1 and <= 8 && claim.Version <= email.Attempts, "InvalidClaimBudget");
        Demand(membership.IntentIds.SequenceEqual(claim.Membership.IntentIds) && email.ToAddress == claim.Envelope.ToAddress && email.Subject == claim.Envelope.Subject
            && email.BodyText == claim.Envelope.BodyText && email.BodyHtml == claim.Envelope.BodyHtml && EqualScope(Scope(email), claim.Envelope.Scope)
            && membership.BulkMemberships.Length == claim.Membership.BulkMemberships.Length
            && membership.BulkMemberships.All(member => claim.Membership.BulkMemberships.Any(other => member.IntentId == other.IntentId
                && member.UserId == other.UserId && member.CommandId == other.CommandId && member.OriginHash == other.OriginHash
                && member.ObligationKeys.SequenceEqual(other.ObligationKeys))), "ChangedClaimEnvelope");
        var intents = await Intents(context, membership.IntentIds, ct);
        if (intents.Any(intent => intent.DeliveryEmailId != email.Id)) return new(false, email.TransportOutcome ?? NoticeTransportOutcome.Unknown, 0, email.AcceptanceUncertainAt is not null);
        NoticeDeliverySlot? slot = null;
        if (email.NoticeSlotId is { } id)
        {
            slot = (await Slot(context, id, ct))!;
            if (slot.CompositionVersion != email.NoticeCompositionVersion) return new(false, email.TransportOutcome ?? "Unknown", 0, email.AcceptanceUncertainAt is not null);
            Demand(slot.ExternalAttempts is >= 1 and <= 8 && slot.ExternalAttempts >= email.Attempts, "InvalidClaimBudget");
        }
        var accepted = observation.Outcome == NoticeTransportOutcome.Accepted;
        Demand(!accepted || claim.TransportKind is NoticeTransportKind.Smtp or NoticeTransportKind.Graph && observation.ProviderAcceptedAt is not null
            && observation.FailureCertainty == NoticeFailureCertainty.None, "UnprovedAcceptance");
        Demand(accepted || observation.ProviderAcceptedAt is null, "UnprovedAcceptance");
        Demand(observation.ProviderAcceptedAt is null || observation.ProviderAcceptedAt.Value.Offset == TimeSpan.Zero
            && observation.ProviderAcceptedAt >= claim.ClaimedAt && observation.ProviderAcceptedAt <= observation.ObservedAt, "InvalidAcceptanceTime");
        Demand(observation.ObservedAt >= claim.ClaimedAt && (observation.Outcome != NoticeTransportOutcome.Simulated || claim.TransportKind == NoticeTransportKind.Log)
            && (observation.Outcome != NoticeTransportOutcome.Unknown || claim.TransportKind == NoticeTransportKind.Unknown), "InvalidTransportObservation");
        var outcome = observation.Outcome;
        if (outcome == NoticeTransportOutcome.Failed && observation.FailureCertainty != NoticeFailureCertainty.DefinitelyNotAccepted) outcome = NoticeTransportOutcome.Uncertain;
        if (outcome is NoticeTransportOutcome.InFlight or NoticeTransportOutcome.Queued or NoticeTransportOutcome.SuppressedBeforeSend or NoticeTransportOutcome.Exhausted)
            throw new NoticeProofException("InvalidCompletionOutcome");
        if (outcome == NoticeTransportOutcome.Uncertain || email.AcceptanceUncertainAt is not null && !accepted)
        { email.AcceptanceUncertainAt ??= observation.ObservedAt; outcome = NoticeTransportOutcome.Uncertain; }
        if (outcome == NoticeTransportOutcome.Failed && (email.Attempts >= 8 || slot?.ExternalAttempts >= 8)) outcome = NoticeTransportOutcome.Exhausted;
        email.TransportOutcome = outcome; email.TransportObservedAt = observation.ObservedAt;
        if (accepted) email.ProviderAcceptedAt = observation.ProviderAcceptedAt;
        if (outcome is NoticeTransportOutcome.Accepted or NoticeTransportOutcome.Simulated or NoticeTransportOutcome.Unknown) email.SentAt = observation.ObservedAt;
        email.NextAttemptAt = outcome == NoticeTransportOutcome.Failed ? observation.ObservedAt.AddMinutes(Math.Pow(2, email.Attempts)) : null;
        email.LastError = outcome is NoticeTransportOutcome.Failed or NoticeTransportOutcome.Uncertain or NoticeTransportOutcome.Exhausted ? outcome : null;
        var safeDigestExhausted = outcome == NoticeTransportOutcome.Exhausted && slot is not null;
        if (safeDigestExhausted) await ReleaseSafeMembership(context, email, observation.ObservedAt, ct);
        foreach (var intent in intents)
        {
            if (safeDigestExhausted || NoticeIntentDisposition.Terminal.Contains(intent.Disposition)) continue;
            var state = outcome == NoticeTransportOutcome.Failed ? intent.Disposition : outcome;
            if (outcome != NoticeTransportOutcome.Failed) intent.NextPolicyCheckAt = null;
            if (intent.BulkPolicyJson is null) { intent.Disposition = state; intent.UpdatedAt = observation.ObservedAt; }
            else if (outcome != NoticeTransportOutcome.Failed)
            {
                var proof = Proof(intent); var keys = membership.BulkMemberships.Single(value => value.IntentId == intent.Id).ObligationKeys;
                SetProof(intent, proof, proof.Progress.Select(value => keys.Contains(value.Key) && !NoticeIntentDisposition.Terminal.Contains(value.State)
                    ? value with { State = state, UpdatedAt = observation.ObservedAt, EmailId = email.Id,
                        RefusalReason = state == NoticeIntentDisposition.Exhausted ? NoticeRefusalReason.AttemptsExhausted : null } : value).ToImmutableArray(), observation.ObservedAt);
            }
        }
        if (slot is not null)
        { slot.State = outcome == NoticeTransportOutcome.Failed ? NoticeSlotState.Queued : outcome; slot.UpdatedAt = observation.ObservedAt; }
        return new(true, outcome, accepted ? membership.IntentIds.Length : 0, email.AcceptanceUncertainAt is not null);
    }
    public static async Task<bool> ObserveOrphanedClaimAsync(NoticeRecipientContext context, Guid emailId, Guid expectedToken,
        int expectedVersion, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Utc(nowUtc);
        var email = (await Email(context, emailId, ct))!;
        if (email.TransportOutcome != NoticeTransportOutcome.InFlight || email.NoticeClaimToken != expectedToken || email.NoticeClaimVersion != expectedVersion || email.ProviderAcceptedAt is not null) return false;
        email.AcceptanceUncertainAt ??= nowUtc; email.TransportOutcome = NoticeTransportOutcome.Uncertain; email.TransportObservedAt = nowUtc;
        NoticeRenderedMembership membership;
        try { membership = await Membership(context, email, ct); }
        catch (NoticeProofException) { email.LastError = "InvalidNoticeMetadata"; return true; }
        foreach (var intent in await Intents(context, membership.IntentIds, ct))
        {
            if (NoticeIntentDisposition.Terminal.Contains(intent.Disposition)) continue;
            intent.NextPolicyCheckAt = null;
            if (intent.BulkPolicyJson is null) { intent.Disposition = NoticeIntentDisposition.Uncertain; intent.UpdatedAt = nowUtc; }
            else
            {
                var proof = Proof(intent); var keys = membership.BulkMemberships.Single(value => value.IntentId == intent.Id).ObligationKeys;
                SetProof(intent, proof, proof.Progress.Select(value => keys.Contains(value.Key) && !NoticeIntentDisposition.Terminal.Contains(value.State)
                    ? value with { State = NoticeIntentDisposition.Uncertain, EmailId = email.Id, UpdatedAt = nowUtc } : value).ToImmutableArray(), nowUtc);
            }
        }
        if (email.NoticeSlotId is { } slotId) { var slot = (await Slot(context, slotId, ct))!; slot.State = NoticeSlotState.Uncertain; slot.UpdatedAt = nowUtc; }
        return true;
    }
    static readonly Expression<Func<EmailMessage, bool>> LegacyDefinition = email => email.NoticeProtocolVersion == null && email.NoticeSlotId == null && email.NoticeCompositionVersion == null
        && email.RenderedOccurrenceIds == null && email.BulkRenderedMembershipJson == null && email.NoticeDigestPolicyJson == null
        && email.NoticeClaimToken == null && email.NoticeClaimVersion == null && email.NoticeClaimedAt == null && email.TransportKind == null
        && email.TransportOutcome == null && email.TransportObservedAt == null && email.ProviderAcceptedAt == null && email.AcceptanceUncertainAt == null;
    static readonly Func<EmailMessage, bool> LegacyRow = LegacyDefinition.Compile();
    public static IQueryable<EmailMessage> LegacyEmails(IQueryable<EmailMessage> source) => source.Where(LegacyDefinition);
    public static bool IsLegacyEmail(EmailMessage row) => LegacyRow(row);
    public static bool IsLegacy(EmailMessage row) => IsLegacyEmail(row);
    public static async Task<bool> QuarantineMalformedAsync(HubDb db, Guid emailId, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        Utc(nowUtc); _ = Transaction(db);
        var email = await db.Emails.SingleOrDefaultAsync(row => row.Id == emailId, ct);
        if (email is null || IsLegacy(email)) return false;
        var malformed = email.NoticeProtocolVersion != 1 || email.UserId is null || email.RenderedOccurrenceIds is null
            || email.RenderedOccurrenceIds.Length > 10 || email.RenderedOccurrenceIds.Contains(Guid.Empty)
            || email.RenderedOccurrenceIds.Distinct().Count() != email.RenderedOccurrenceIds.Length
            || email.NoticeSlotId is not null && (email.NoticeDigestPolicyJson is null || email.NoticeCompositionVersion is null || email.Kind != "Digest")
            || email.NoticeSlotId is null && (email.NoticeCompositionVersion is not null || email.NoticeDigestPolicyJson is not null
                || email.RenderedOccurrenceIds.Length != 1 || email.Kind is not ("Immediate" or "PlanningEntryChanged" or "PlanningWithdrawal"))
            || email.TransportOutcome == NoticeTransportOutcome.InFlight
                && (email.NoticeClaimToken is null || email.NoticeClaimVersion is null || email.NoticeClaimedAt is null || email.TransportKind is null);
        if (!malformed) return false;
        email.SuppressedAt ??= nowUtc; email.LastError = "InvalidNoticeMetadata";
        if (email.TransportOutcome == NoticeTransportOutcome.InFlight)
        { email.AcceptanceUncertainAt ??= nowUtc; email.TransportOutcome = NoticeTransportOutcome.Uncertain; email.TransportObservedAt = nowUtc; }
        return true;
    }
    public static async Task<ImmutableArray<NoticeDeliveryAggregate>> ReadAggregatesAsync(HubDb db, Guid ownerUserId,
        ImmutableArray<(Guid NotificationId, int Count)> visibleRows, CancellationToken ct = default)
    {
        Demand(ownerUserId != Guid.Empty && !visibleRows.IsDefault && visibleRows.Length <= 200
            && visibleRows.All(row => row.NotificationId != Guid.Empty && row.Count > 0)
            && visibleRows.Select(row => row.NotificationId).Distinct().Count() == visibleRows.Length);
        if (visibleRows.IsEmpty) return [];
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(row => row.Id == ownerUserId && row.IsActive, ct);
        if (user is null) return [];
        var roles = (await db.UserRoles.AsNoTracking().Where(row => row.UserId == ownerUserId).Select(row => row.Role).ToListAsync(ct)).ToHashSet();
        var me = new CurrentUser { Id = ownerUserId, IsActive = true, Resolved = true, Roles = roles };
        var ids = visibleRows.Select(row => row.NotificationId).ToArray(); var textIds = ids.Select(Id).ToArray();
        var visible = await PlanningEndpoints.VisibleNotifications(db, new Access(db, me));
        var actual = await visible.AsNoTracking().Where(row => ids.Contains(row.Id)).ToDictionaryAsync(row => row.Id, ct);
        // Bind the JSON app origin in one parameterised owner-scoped query, including partial bulk links.
        var intents = await db.NoticeDeliveryIntents.FromSqlInterpolated($"""
            SELECT * FROM hub.notice_delivery_intent WHERE user_id={ownerUserId}
            AND (notification_id=ANY({ids}) OR bulk_policy_json->'origin'->>'appNotificationId'=ANY({textIds}))
            """).AsNoTracking().ToListAsync(ct);
        var intentIds = intents.Select(row => row.Id).ToArray();
        List<EmailMessage> emails = intentIds.Length == 0 ? [] : await db.Emails.AsNoTracking().Where(row => row.UserId == ownerUserId
            && row.NoticeProtocolVersion == 1 && row.RenderedOccurrenceIds != null && row.RenderedOccurrenceIds.Any(id => intentIds.Contains(id))).ToListAsync(ct);
        var referenced = emails.Where(row => row.RenderedOccurrenceIds is { Length: <= 10 }).SelectMany(row => row.RenderedOccurrenceIds!).Distinct().ToArray();
        var renderedSources = referenced.Length == 0 ? new Dictionary<Guid, NoticeDeliveryIntent>() : await db.NoticeDeliveryIntents.AsNoTracking()
            .Where(row => row.UserId == ownerUserId && referenced.Contains(row.Id)).ToDictionaryAsync(row => row.Id, ct);
        var slotIds = emails.Where(row => row.NoticeSlotId is not null).Select(row => row.NoticeSlotId!.Value).Distinct().ToArray();
        var renderedSlots = slotIds.Length == 0 ? new Dictionary<Guid, NoticeDeliverySlot>() : await db.NoticeDeliverySlots.AsNoTracking()
            .Where(row => row.UserId == ownerUserId && slotIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, ct);
        var output = ImmutableArray.CreateBuilder<NoticeDeliveryAggregate>();
        foreach (var input in visibleRows)
        {
            if (!actual.TryGetValue(input.NotificationId, out var notification) || notification.Count != input.Count) continue;
            var known = new HashSet<int>(); var accepted = new HashSet<int>(); var uncertain = new HashSet<int>();
            var states = new HashSet<string>(StringComparer.Ordinal); var acceptedTimes = new List<DateTimeOffset>(); bool partialMail = false;
            foreach (var intent in intents)
            {
                NoticeBulkProof? proof = null;
                try { if (intent.BulkPolicyJson is not null) proof = Proof(intent); }
                catch (NoticeProofException) { continue; }
                var appId = proof?.Origin.AppNotificationId ?? intent.NotificationId;
                var ordinal = proof?.Origin.NotificationCountAtCapture ?? intent.NotificationCountAtCapture;
                if (appId != notification.Id || ordinal is not > 0 || ordinal > notification.Count) continue;
                known.Add(ordinal.Value);
                var candidates = emails.Where(email => email.RenderedOccurrenceIds!.Contains(intent.Id)).ToArray();
                var realKeys = new HashSet<int>(); bool realScalar = false, uncertainPart = false;
                foreach (var email in candidates)
                {
                    try
                    {
                        Demand(email.RenderedOccurrenceIds is { Length: <= 10 } && email.RenderedOccurrenceIds.All(renderedSources.ContainsKey)
                            && email.RenderedOccurrenceIds.Distinct().Count() == email.RenderedOccurrenceIds.Length, "InvalidRenderedMembership");
                        var bulkProofs = email.RenderedOccurrenceIds!.Select(id => renderedSources[id]).Where(row => row.BulkPolicyJson != null).ToDictionary(row => row.Id, Proof);
                        var maps = NoticeDeliveryProof.DecodeRenderedBulkMap(email.BulkRenderedMembershipJson, ownerUserId, email.RenderedOccurrenceIds!.ToImmutableArray(), bulkProofs);
                        if (email.NoticeSlotId is { } slotId)
                        {
                            Demand(renderedSlots.TryGetValue(slotId, out var slot) && slot.Kind == NoticeSlotKind.Digest
                                && slot.WindowFrom is not null && slot.WindowThrough is not null && email.NoticeCompositionVersion is >= 1 and <= 3, "InvalidRenderedSlot");
                            _ = NoticeDeliveryProof.DecodeDigestPolicy(email.NoticeDigestPolicyJson!, ownerUserId, slotId, email.NoticeCompositionVersion!.Value,
                                new(slot!.WindowFrom!.Value, slot.WindowThrough!.Value), Scope(email));
                        }
                        else Demand(email.NoticeDigestPolicyJson is null && email.NoticeCompositionVersion is null && email.RenderedOccurrenceIds.Length == 1, "InvalidRenderedImmediate");
                        var real = email.ProviderAcceptedAt is not null && email.TransportOutcome == NoticeTransportOutcome.Accepted
                            && email.TransportKind is NoticeTransportKind.Smtp or NoticeTransportKind.Graph
                            && email.NoticeClaimToken is not null && email.NoticeClaimVersion > 0 && email.NoticeClaimedAt <= email.ProviderAcceptedAt
                            && email.TransportObservedAt >= email.ProviderAcceptedAt;
                        if (real)
                        {
                            if (proof is null) realScalar = true;
                            else realKeys.UnionWith(maps.Single(map => map.IntentId == intent.Id).ObligationKeys);
                            if (proof is null || maps.Single(map => map.IntentId == intent.Id).ObligationKeys.Any(proof.Origin.AppCohortKeys.Contains))
                                acceptedTimes.Add(email.ProviderAcceptedAt!.Value);
                        }
                        else if (email.AcceptanceUncertainAt is not null || email.TransportOutcome is NoticeTransportOutcome.InFlight or NoticeTransportOutcome.Uncertain) uncertainPart = true;
                    }
                    catch (NoticeProofException) { }
                }
                var complete = proof is null ? realScalar : !proof.Origin.AppCohortKeys.IsEmpty && proof.Origin.AppCohortKeys.All(realKeys.Contains);
                if (complete) accepted.Add(ordinal.Value);
                else if (proof is not null && proof.Origin.AppCohortKeys.Any(realKeys.Contains)) partialMail = true;
                if (uncertainPart && !complete) uncertain.Add(ordinal.Value);
                states.Add(intent.Disposition == NoticeIntentDisposition.Accepted && !complete ? "Unknown" : intent.Disposition);
            }
            var coverage = known.Count == notification.Count ? "Known" : known.Count == 0 ? "Unavailable" : "Partial";
            var full = accepted.Count == notification.Count;
            var state = full ? "Emailed" : accepted.Count > 0 || partialMail ? "Partial" : known.Count == 0 ? "Unknown"
                : known.Count != notification.Count || states.Count > 1 ? "Mixed" : states.Single();
            if (!full && uncertain.Count > 0 && accepted.Count == 0 && !partialMail) state = "Uncertain";
            output.Add(new(notification.Id, notification.Count, known.Count, accepted.Count, uncertain.Count, state, coverage,
                full && acceptedTimes.Count > 0 ? acceptedTimes.Max() : null));
        }
        return output.ToImmutable();
    }
}
