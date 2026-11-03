using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hub.Api.Infrastructure;

public sealed record NoticeDigestBuildRequest(Guid SlotId, Guid UserId, string FirstName, DateOnly Today,
    DateTimeOffset ReadAtUtc, NoticeDigestWindow Window, OrgSettings Settings, string BaseUrl,
    ImmutableHashSet<string> SectionsOff, ImmutableArray<NoticeHeldCandidate> Held);
public sealed record NoticeDigestActivityScope(Guid ProjectId, ImmutableArray<Guid> ActivityIds);
public sealed record NoticeDigestRenderPolicy(ImmutableArray<string> RenderedSectionCodes,
    ImmutableArray<Guid> UpdateProjectIds, ImmutableArray<Guid> StaffDirectReportIds,
    ImmutableArray<Guid> StaffAssignmentNotificationIds, ImmutableArray<NoticeDigestActivityScope> UpdateActivity,
    bool IncludesAdminReview);
public sealed record NoticeDigestRenderedPayload(Guid SlotId, NoticeDigestWindow Window, string Subject, string BodyText,
    NoticeRequiredScope Scope, NoticeRenderedMembership Membership, NoticeDigestRenderPolicy RenderedPolicy);
public enum NoticeDigestWorkKind { Compose, Recompose, DispatchExisting, InFlight, Uncertain, Terminal, Dormant }
public sealed record NoticeDigestSlotDue(Guid SlotId, NoticeDigestWorkKind Work, string State, DateOnly CreationDate,
    DateTimeOffset ScheduledAtUtc, NoticeDigestWindow Window, int CompositionVersion, int ExternalAttempts, DateTimeOffset? NextCheckAtUtc);
public sealed record NoticeDigestDue(Guid UserId, DateOnly Today, string Frequency, string CreationDecision,
    NoticeLocalSchedule? CreationSchedule, DateTimeOffset? CarryForwardFrom, ImmutableArray<NoticeDigestSlotDue> Existing, bool HasMoreExisting);
public enum NoticeDigestCompositionDecision
{ Queued, Empty, HeldNoDigest, Dormant, NotEligible, InFlight, Uncertain, Exhausted, AlreadyPrepared }
public sealed record NoticeDigestCompositionResult(NoticeDigestCompositionDecision Decision, Guid? SlotId,
    int CompositionVersion, Guid? EmailId, ImmutableArray<Guid> RenderedIntentIds);

/// Scheduling and formatting only. D's delivery core owns preparation, reservations, attempts and send authority.
public static class NotificationDigestComposition
{
    internal const int MaxHeldParents = 10;
    const int ExistingBatchSize = 100;

    /// Side-effect-free scheduling facts for the job and Operations, under the actual recipient transaction/lock.
    public static async Task<NoticeDigestDue> ReadDueAsync(NoticeRecipientContext context, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        DemandContext(context);
        DemandUtc(nowUtc);
        var policy = await NotificationDelivery.ReadRecipientPolicyAsync(context, ct);
        if (policy.UserId != context.UserId) throw new InvalidOperationException("Digest policy recipient mismatch.");
        var today = Clock.LocalDate(nowUtc, policy.Settings);
        var frequency = policy.Preferences.NoticeDigestFrequency;
        NoticeDigestDue Held(string reason) => new(context.UserId, today, frequency, reason, null, null, [], false);
        if (!policy.Settings.IsFlagOn("people_screens")) return Held("Dormant");
        if (!policy.IsActive) return Held("RecipientInactive");
        if (!policy.DigestEnabled) return Held("Disabled");
        if (Digest.SectionCodes.All(policy.SectionsOff.Contains)) return Held("AllSectionsOff");
        if (string.IsNullOrWhiteSpace(policy.EmailAddress)) return Held("NoAddress");
        if (policy.HoldDigestWhenAway)
        {
            var availability = await NotificationDelivery.ReadDayAvailabilityAsync(context, today, ct);
            if (availability.IsAway is null) return Held("PendingAvailability");
            if (availability.IsAway == true) return Held("HeldAway");
        }

        // Existing captured work is selected independently of today's frequency, weekday, chosen time or watermark.
        // Exclude terminal/backoff rows before bounded selection; a suppressed final version still needs D's exhaustion check.
        var candidates = context.Db.NoticeDeliverySlots.AsNoTracking().Where(slot => slot.UserId == context.UserId
            && slot.Kind == NoticeSlotKind.Digest && slot.ScheduledAtUtc <= nowUtc
            && (!context.Db.Emails.Any(email => email.UserId == context.UserId && email.NoticeProtocolVersion == 1
                    && email.NoticeSlotId == slot.Id && email.TransportOutcome == NoticeTransportOutcome.SuppressedBeforeSend && email.LastError == "TransportChanged")
                || context.Db.Emails.Any(email => email.UserId == context.UserId && email.NoticeProtocolVersion == 1 && email.NoticeSlotId == slot.Id
                    && (email.ProviderAcceptedAt != null || email.SentAt != null || email.TransportOutcome == NoticeTransportOutcome.Accepted
                        || email.TransportOutcome == NoticeTransportOutcome.Simulated || email.TransportOutcome == NoticeTransportOutcome.Unknown
                        || email.TransportOutcome == NoticeTransportOutcome.InFlight || email.AcceptanceUncertainAt != null || email.TransportOutcome == NoticeTransportOutcome.Uncertain)))
            && (slot.State == NoticeSlotState.Ready && slot.CompositionVersion == 0
                || (slot.State == NoticeSlotState.Ready || slot.State == NoticeSlotState.Paused || slot.State == NoticeSlotState.Queued || slot.State == NoticeSlotState.Uncertain)
                    && context.Db.Emails.Any(email => email.UserId == context.UserId && email.NoticeProtocolVersion == 1
                        && email.NoticeSlotId == slot.Id && email.NoticeCompositionVersion == slot.CompositionVersion
                        && (email.TransportOutcome == NoticeTransportOutcome.InFlight || email.AcceptanceUncertainAt != null
                            || email.TransportOutcome == NoticeTransportOutcome.Uncertain
                            || email.SuppressedAt != null && email.TransportOutcome == NoticeTransportOutcome.SuppressedBeforeSend
                            || email.SentAt == null && email.ProviderAcceptedAt == null
                                && email.SuppressedAt == null && (email.TransportOutcome == NoticeTransportOutcome.Queued || email.TransportOutcome == NoticeTransportOutcome.Failed)
                                && (email.NextAttemptAt == null || email.NextAttemptAt <= nowUtc)))))
            .OrderBy(slot => slot.State == NoticeSlotState.Ready && slot.CompositionVersion == 0
                || context.Db.Emails.Any(email => email.NoticeSlotId == slot.Id && email.NoticeCompositionVersion == slot.CompositionVersion
                    && email.SuppressedAt != null && email.TransportOutcome == NoticeTransportOutcome.SuppressedBeforeSend && email.LastError != "TransportChanged") ? 0
                : slot.State == NoticeSlotState.Uncertain || context.Db.Emails.Any(email => email.NoticeSlotId == slot.Id
                    && email.NoticeCompositionVersion == slot.CompositionVersion && (email.TransportOutcome == NoticeTransportOutcome.InFlight
                        || email.AcceptanceUncertainAt != null || email.TransportOutcome == NoticeTransportOutcome.Uncertain)) ? 2 : 1)
            .ThenBy(slot => slot.ScheduledAtUtc).ThenBy(slot => slot.Id);
        var slots = await candidates.Take(ExistingBatchSize + 1).ToArrayAsync(ct);
        var selected = slots.Take(ExistingBatchSize).ToArray();
        var ids = selected.Select(slot => slot.Id).ToArray();
        var emails = await context.Db.Emails.AsNoTracking().Where(email => email.UserId == context.UserId && ids.Contains(email.NoticeSlotId ?? Guid.Empty)
            && email.NoticeProtocolVersion == 1).ToArrayAsync(ct); // At most three immutable compositions per selected slot.
        var existing = selected.Select(slot => Describe(slot, emails.Where(email => email.NoticeSlotId == slot.Id).ToArray(), policy.Settings, nowUtc)).ToImmutableArray();
        NoticeDigestDue Result(string reason, NoticeLocalSchedule? schedule = null, DateTimeOffset? carry = null) =>
            new(context.UserId, today, frequency, reason, schedule, carry, existing, slots.Length > ExistingBatchSize);
        var capturedToday = context.Db.NoticeDeliverySlots.Local.Any(slot => slot.UserId == context.UserId && slot.Kind == NoticeSlotKind.Digest && slot.LocalDate == today)
            || await context.Db.NoticeDeliverySlots.AsNoTracking().AnyAsync(slot => slot.UserId == context.UserId && slot.Kind == NoticeSlotKind.Digest && slot.LocalDate == today, ct);
        if (capturedToday) return Result("ExistingSlot");
        if (!NoticeDigestFrequency.All.Contains(frequency) || !TryTime(policy.DigestTimeLocal, out var at)) return Result("Dormant");
        if (frequency == NoticeDigestFrequency.Weekly && today.DayOfWeek != DayOfWeek.Monday
            || frequency == NoticeDigestFrequency.Daily && !policy.Settings.WeekendDigests && today.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return Result("NotCreationDay");
        NoticeLocalSchedule schedule;
        try { schedule = NotificationDelivery.ResolveLocal(policy.Settings, today, at); }
        catch (ArgumentException) { return Result("Dormant"); }
        if (nowUtc < schedule.ScheduledAtUtc) return Result("BeforeTime", schedule);
        var watermark = await Watermark(context, ct);
        if (watermark >= schedule.ScheduledAtUtc) return Result("ExistingSlot", schedule);
        var carry = await NotificationDelivery.ReadCarryForwardFromAsync(context, ct);
        return Result("Due", schedule, carry);
    }

    /// Stage a new immutable slot and one-time safe-leaf coverage transfers; caller owns the eventual Save/commit.
    public static async Task<NoticeDeliverySlot?> GetOrCreateDueSlotAsync(NoticeRecipientContext context, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        DemandContext(context);
        DemandUtc(nowUtc);
        var due = await ReadDueAsync(context, nowUtc, ct);
        if (due.CreationDecision == "ExistingSlot")
            return context.Db.NoticeDeliverySlots.Local.SingleOrDefault(slot => slot.UserId == context.UserId && slot.Kind == NoticeSlotKind.Digest && slot.LocalDate == due.Today)
                ?? await context.Db.NoticeDeliverySlots.SingleOrDefaultAsync(slot => slot.UserId == context.UserId && slot.Kind == NoticeSlotKind.Digest && slot.LocalDate == due.Today, ct);
        if (due.CreationDecision != "Due") return null;
        var schedule = due.CreationSchedule ?? throw new InvalidOperationException("Due digest has no resolved schedule.");
        var from = await Watermark(context, ct) ?? nowUtc.AddDays(due.Frequency == NoticeDigestFrequency.Weekly ? -7 : -1);
        if (due.CarryForwardFrom is { } carry && carry < from) from = carry;
        if (from > nowUtc) throw new InvalidOperationException("Digest preparation watermark is later than its immutable window.");
        var slot = new NoticeDeliverySlot
        {
            UserId = context.UserId, Kind = NoticeSlotKind.Digest, LocalDate = due.Today,
            OrgTimeZoneId = schedule.OrgTimeZoneId, ScheduledLocalDate = schedule.RequestedDate,
            ScheduledLocalTime = schedule.RequestedTime.ToString("HH:mm", CultureInfo.InvariantCulture), ScheduledAtUtc = schedule.ScheduledAtUtc,
            FrequencyAtCreation = due.Frequency, WindowFrom = from, WindowThrough = nowUtc,
            State = NoticeSlotState.Ready, CompositionVersion = 0, ExternalAttempts = 0, CreatedAt = nowUtc, UpdatedAt = nowUtc,
        };
        context.Db.NoticeDeliverySlots.Add(slot);
        await NotificationDelivery.TransferCarryForwardAsync(context, slot.Id, nowUtc, ct);
        return slot;
    }

    /// Compose only from D's current admitted preparation and original immutable slot window. No sender is called.
    public static async Task<NoticeDigestCompositionResult> ComposeAsync(NoticeRecipientContext context, Guid slotId,
        DateTimeOffset nowUtc, string baseUrl, CancellationToken ct = default)
    {
        DemandContext(context);
        DemandUtc(nowUtc);
        var slot = context.Db.NoticeDeliverySlots.Local.SingleOrDefault(value => value.Id == slotId)
            ?? await context.Db.NoticeDeliverySlots.SingleOrDefaultAsync(value => value.Id == slotId, ct)
            ?? throw new InvalidOperationException("Digest slot is missing.");
        if (slot.UserId != context.UserId || slot.Kind != NoticeSlotKind.Digest)
            throw new InvalidOperationException("Digest slot recipient/kind mismatch.");
        NoticeDigestCompositionResult Held(NoticeDigestCompositionDecision decision) => new(decision, slot.Id, slot.CompositionVersion, null, []);
        var policy = await NotificationDelivery.ReadRecipientPolicyAsync(context, ct);
        if (policy.UserId != context.UserId) throw new InvalidOperationException("Digest policy recipient mismatch.");
        if (!policy.Settings.IsFlagOn("people_screens")) return Held(NoticeDigestCompositionDecision.Dormant);
        if (!policy.IsActive || !policy.DigestEnabled || string.IsNullOrWhiteSpace(policy.EmailAddress)) return Held(NoticeDigestCompositionDecision.NotEligible);
        if (Digest.SectionCodes.All(policy.SectionsOff.Contains)) return Held(NoticeDigestCompositionDecision.HeldNoDigest);
        var today = Clock.LocalDate(nowUtc, policy.Settings);
        if (policy.HoldDigestWhenAway && (await NotificationDelivery.ReadDayAvailabilityAsync(context, today, ct)).IsAway != false)
            return Held(NoticeDigestCompositionDecision.NotEligible);
        var preparation = await NotificationDelivery.PrepareDigestAsync(context, slotId, nowUtc, ct);
        if (preparation.Decision != NoticeDigestPreparationDecision.Compose)
            return Held(preparation.Decision switch
            {
                NoticeDigestPreparationDecision.Dormant => NoticeDigestCompositionDecision.Dormant,
                NoticeDigestPreparationDecision.NotEligible => NoticeDigestCompositionDecision.NotEligible,
                NoticeDigestPreparationDecision.InFlight => NoticeDigestCompositionDecision.InFlight,
                NoticeDigestPreparationDecision.Uncertain => NoticeDigestCompositionDecision.Uncertain,
                NoticeDigestPreparationDecision.Exhausted => NoticeDigestCompositionDecision.Exhausted,
                NoticeDigestPreparationDecision.AlreadyTerminal => NoticeDigestCompositionDecision.AlreadyPrepared,
                _ => throw new InvalidOperationException("Unknown digest preparation decision."),
            });
        var displayName = await context.Db.Users.AsNoTracking().Where(user => user.Id == context.UserId).Select(user => user.DisplayName).SingleAsync(ct);
        var request = new NoticeDigestBuildRequest(slot.Id, context.UserId, displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "",
            today, nowUtc, preparation.Window, policy.Settings, baseUrl.TrimEnd('/'), policy.SectionsOff.ToImmutableHashSet(StringComparer.Ordinal), preparation.Held);
        var payload = await Digest.Build(context.Db, request, ct);
        var reserved = payload is null
            ? await NotificationDelivery.CompleteEmptyDigestAsync(context, preparation, nowUtc, ct)
            : await NotificationDelivery.ReserveDigestAsync(context, preparation, payload, nowUtc, ct);
        if (reserved.SlotId != slot.Id) throw new InvalidOperationException("Digest reservation changed its slot.");
        var decision = reserved.State switch
        {
            NoticeSlotState.Queued => NoticeDigestCompositionDecision.Queued,
            NoticeSlotState.Empty => NoticeDigestCompositionDecision.Empty,
            NoticeSlotState.Exhausted => NoticeDigestCompositionDecision.Exhausted,
            NoticeSlotState.Uncertain => NoticeDigestCompositionDecision.Uncertain,
            NoticeSlotState.Paused => NoticeDigestCompositionDecision.NotEligible,
            _ => NoticeDigestCompositionDecision.AlreadyPrepared,
        };
        return new(decision, reserved.SlotId, reserved.CompositionVersion, reserved.EmailId, reserved.ReservedIntentIds);
    }

    static async Task<DateTimeOffset?> Watermark(NoticeRecipientContext context, CancellationToken ct)
    {
        var tracked = context.Db.UserSettings.Local.SingleOrDefault(row => row.UserId == context.UserId);
        return tracked is not null ? tracked.LastDigestAt
            : await context.Db.UserSettings.AsNoTracking().Where(row => row.UserId == context.UserId).Select(row => row.LastDigestAt).SingleOrDefaultAsync(ct);
    }

    static NoticeDigestSlotDue Describe(NoticeDeliverySlot slot, IReadOnlyList<EmailMessage> versions, OrgSettings settings, DateTimeOffset nowUtc)
    {
        var email = versions.SingleOrDefault(value => value.NoticeCompositionVersion == slot.CompositionVersion);
        var window = new NoticeDigestWindow(slot.WindowFrom ?? default, slot.WindowThrough ?? default);
        NoticeDigestWorkKind work;
        if (!ValidCaptured(slot, settings, nowUtc)) work = NoticeDigestWorkKind.Dormant;
        else if (versions.Any(value => value.TransportOutcome == NoticeTransportOutcome.InFlight)) work = NoticeDigestWorkKind.InFlight;
        else if (slot.State == NoticeSlotState.Uncertain || versions.Any(value => value.AcceptanceUncertainAt is not null || value.TransportOutcome == NoticeTransportOutcome.Uncertain))
            work = NoticeDigestWorkKind.Uncertain;
        else if (versions.Any(value => value.SentAt is not null || value.ProviderAcceptedAt is not null || value.TransportOutcome is NoticeTransportOutcome.Accepted
            or NoticeTransportOutcome.Simulated or NoticeTransportOutcome.Unknown) || email?.TransportOutcome == NoticeTransportOutcome.Exhausted)
            work = NoticeDigestWorkKind.Terminal;
        // The actual linked marker parks the immutable slot. It cannot turn into ordinary safe recomposition/carry work.
        else if (versions.Any(value => value.TransportOutcome == NoticeTransportOutcome.SuppressedBeforeSend && value.LastError == "TransportChanged"))
            work = NoticeDigestWorkKind.Dormant;
        // A safely suppressed final version still visits D preparation so it can become Exhausted and establish carry safety.
        else if (email is not null && email.SuppressedAt is not null && email.TransportOutcome == NoticeTransportOutcome.SuppressedBeforeSend)
            work = NoticeDigestWorkKind.Recompose;
        else if (slot.ExternalAttempts >= NoticeProofLimits.MaxSenderReservations) work = NoticeDigestWorkKind.Terminal;
        else if (email is not null && email.SuppressedAt is null && email.TransportOutcome is NoticeTransportOutcome.Queued or NoticeTransportOutcome.Failed)
            work = email.NextAttemptAt > nowUtc ? NoticeDigestWorkKind.Dormant : NoticeDigestWorkKind.DispatchExisting;
        else if (slot.CompositionVersion >= NoticeProofLimits.MaxCompositions) work = NoticeDigestWorkKind.Terminal;
        else if (slot.CompositionVersion == 0 && slot.State == NoticeSlotState.Ready) work = NoticeDigestWorkKind.Compose;
        else work = NoticeDigestWorkKind.Dormant;
        return new(slot.Id, work, slot.State, slot.LocalDate, slot.ScheduledAtUtc, window,
            slot.CompositionVersion, slot.ExternalAttempts, email?.NextAttemptAt);
    }

    static bool ValidCaptured(NoticeDeliverySlot slot, OrgSettings settings, DateTimeOffset nowUtc)
    {
        if (slot.WindowFrom is not { } from || slot.WindowThrough is not { } through || from > through || through > nowUtc
            || slot.ScheduledLocalDate != slot.LocalDate || slot.ScheduledAtUtc > nowUtc
            || !NoticeDigestFrequency.All.Contains(slot.FrequencyAtCreation ?? "") || !TryTime(slot.ScheduledLocalTime, out var at)
            || slot.FrequencyAtCreation == NoticeDigestFrequency.Weekly && slot.LocalDate.DayOfWeek != DayOfWeek.Monday
            || slot.CompositionVersion is < 0 or > NoticeProofLimits.MaxCompositions || slot.ExternalAttempts is < 0 or > NoticeProofLimits.MaxSenderReservations)
            return false;
        try
        {
            var schedule = NotificationDelivery.ResolveLocal(settings with { OrgTimeZone = slot.OrgTimeZoneId }, slot.ScheduledLocalDate, at);
            return schedule.OrgTimeZoneId == slot.OrgTimeZoneId && schedule.ScheduledAtUtc == slot.ScheduledAtUtc;
        }
        catch (ArgumentException) { return false; }
    }

    static bool TryTime(string? value, out TimeOnly at)
    {
        at = default;
        return value is { Length: 5 } && TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out at);
    }

    static void DemandUtc(DateTimeOffset nowUtc)
    {
        if (nowUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Digest scheduling requires a UTC instant.", nameof(nowUtc));
    }

    static void DemandContext(NoticeRecipientContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var transaction = context.Db.Database.CurrentTransaction;
        if (context.UserId == Guid.Empty || transaction is null || transaction.TransactionId != context.TransactionId
            || transaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Digest composition requires its bound live ReadCommitted recipient transaction.");
    }

    internal static void Validate(NoticeDigestBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SlotId == Guid.Empty || request.UserId == Guid.Empty || request.Window is null
            || request.Window.From > request.Window.Through || request.ReadAtUtc < request.Window.Through
            || request.Settings is null || request.SectionsOff is null || request.Held.IsDefault
            || request.Today != DateOnly.FromDateTime(Clock.Local(request.ReadAtUtc, request.Settings).DateTime))
            throw new ArgumentException("Invalid digest build binding/window.", nameof(request));
    }

    internal static ImmutableArray<NoticeHeldCandidate> HeldLines(NoticeDigestBuildRequest request)
    {
        var ids = new HashSet<Guid>();
        foreach (var candidate in request.Held)
        {
            if (candidate is null || candidate.IntentId == Guid.Empty || candidate.UserId != request.UserId || !ids.Add(candidate.IntentId)
                || string.IsNullOrWhiteSpace(candidate.Subject) || string.IsNullOrWhiteSpace(candidate.BodyText))
                throw new ArgumentException("Invalid digest held-parent binding.", nameof(request));
            DemandScope(candidate.Scope);
            if (candidate.LinkPath is { } link && (!link.StartsWith('/') || link.StartsWith("//", StringComparison.Ordinal) || link.Contains('\\')))
                throw new ArgumentException("Invalid digest held link.", nameof(request));
            if (candidate.BulkMembership is { } bulk && (bulk.IntentId != candidate.IntentId || bulk.UserId != request.UserId
                || bulk.CommandId == Guid.Empty || bulk.OriginHash is null || bulk.OriginHash.Length != 64 || !bulk.OriginHash.All(Uri.IsHexDigit)
                || bulk.ObligationKeys.IsDefaultOrEmpty || bulk.ObligationKeys.Length > NoticeProofLimits.MaxObligations
                || bulk.ObligationKeys.Any(k => k < 1 || k > NoticeProofLimits.MaxObligations) || bulk.ObligationKeys.Distinct().Count() != bulk.ObligationKeys.Length))
                throw new ArgumentException("Invalid digest rendered bulk binding.", nameof(request));
        }
        // Earlier pending overflow remains eligible; later occurrences belong to a later immutable window.
        return request.Held.Where(candidate => candidate.CreatedAt <= request.Window.Through)
            .OrderBy(candidate => candidate.CreatedAt).ThenBy(candidate => candidate.IntentId).Take(MaxHeldParents).ToImmutableArray();
    }

    internal static void DemandScope(NoticeRequiredScope scope)
    {
        if (scope is null || scope.ProjectIds.IsDefault || scope.PlanningEntryIds.IsDefault
            || scope.ProjectIds.Any(id => id == Guid.Empty) || scope.PlanningEntryIds.Any(id => id == Guid.Empty)
            || scope.ProjectIds.Distinct().Count() != scope.ProjectIds.Length || scope.PlanningEntryIds.Distinct().Count() != scope.PlanningEntryIds.Length)
            throw new ArgumentException("Invalid digest required scope.", nameof(scope));
    }

    internal sealed class RenderLedger
    {
        sealed record Dependency(Guid[] Projects, Guid[] Planning, Guid? Report = null, Guid? Notification = null);
        readonly Dictionary<Digest.Row, Dependency> rows = new(ReferenceEqualityComparer.Instance);
        readonly HashSet<Guid> projects = [], planning = [], reports = [], notifications = [], updates = [];
        readonly HashSet<string> sections = new(StringComparer.Ordinal);
        readonly Dictionary<Guid, HashSet<Guid>> activities = [];
        bool adminReview;

        public Digest.Row Project(Digest.Row row, Guid projectId)
        {
            Track(row, new([projectId], [])); return row;
        }
        public Digest.Row Planning(Digest.Row row, Guid entryId)
        {
            Track(row, new([], [entryId])); return row;
        }
        public Digest.Row Staff(Digest.Row row, IEnumerable<Guid> projectIds, Guid reportId)
        {
            Track(row, new(projectIds.Distinct().ToArray(), [], Report: reportId)); return row;
        }
        public Digest.Row Notification(Digest.Row row, Hub.Api.Data.Notification notice)
        {
            var required = (notice.RequiredProjectIds ?? []).Concat(notice.ProjectId is { } id ? [id] : Array.Empty<Guid>()).Distinct().ToArray();
            Track(row, new(required, [], Notification: notice.Id)); return row;
        }
        void Track(Digest.Row row, Dependency dependency)
        {
            if (dependency.Projects.Any(id => id == Guid.Empty) || dependency.Planning.Any(id => id == Guid.Empty))
                throw new InvalidOperationException("Digest output has invalid source provenance.");
            rows.Add(row, dependency);
        }
        public void Include(string code, IReadOnlyList<Digest.Row> contributors)
        {
            if (contributors.Count == 0) return;
            sections.Add(code);
            // Total and more-count are output too, so counted overflow retains its required source scope.
            foreach (var row in contributors)
            {
                if (!rows.TryGetValue(row, out var source)) throw new InvalidOperationException("Digest output lacks source provenance.");
                projects.UnionWith(source.Projects); planning.UnionWith(source.Planning);
                if (source.Report is { } report) reports.Add(report);
                if (source.Notification is { } notification) notifications.Add(notification);
            }
        }
        public void Updates(Guid projectId, IEnumerable<Guid> contributingActivityIds)
        {
            if (projectId == Guid.Empty) throw new InvalidOperationException("Digest update output lacks its project.");
            var ids = contributingActivityIds.ToHashSet();
            if (ids.Count == 0 || ids.Contains(Guid.Empty)) throw new InvalidOperationException("Digest update output lacks source activity.");
            projects.Add(projectId); updates.Add(projectId); sections.Add("updates");
            if (!activities.TryGetValue(projectId, out var existing)) activities[projectId] = ids; else existing.UnionWith(ids);
        }
        public void AdminReview() => adminReview = true;

        public NoticeDigestRenderedPayload Complete(NoticeDigestBuildRequest request, Digest.Result result, ImmutableArray<NoticeHeldCandidate> held)
        {
            foreach (var candidate in held) { projects.UnionWith(candidate.Scope.ProjectIds); planning.UnionWith(candidate.Scope.PlanningEntryIds); }
            var scope = new NoticeRequiredScope(projects.Order().ToImmutableArray(), planning.Order().ToImmutableArray());
            DemandScope(scope);
            var membership = new NoticeRenderedMembership(held.Select(candidate => candidate.IntentId).Order().ToImmutableArray(),
                held.Where(candidate => candidate.BulkMembership is not null).Select(candidate => candidate.BulkMembership!)
                    .OrderBy(value => value.IntentId).ToImmutableArray());
            var policy = new NoticeDigestRenderPolicy(sections.Order(StringComparer.Ordinal).ToImmutableArray(), updates.Order().ToImmutableArray(),
                reports.Order().ToImmutableArray(), notifications.Order().ToImmutableArray(),
                activities.OrderBy(pair => pair.Key).Select(pair => new NoticeDigestActivityScope(pair.Key, pair.Value.Order().ToImmutableArray())).ToImmutableArray(), adminReview);
            return new(request.SlotId, request.Window, result.Subject, result.Body, scope, membership, policy);
        }
    }
}
