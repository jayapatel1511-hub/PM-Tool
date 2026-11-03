using System.Data;
using System.Globalization;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hub.Api.Infrastructure;

public enum TimeEntryReminderDueKind
{
    Disabled, NotDue, AlreadyEmitted, Due, Unavailable
}

/// Read-only scheduling facts shared with Operations; never an HTTP response or permission grant.
public sealed record TimeEntryReminderDue(
    TimeEntryReminderDueKind Kind, DateOnly? LocalMonday,
    NoticeLocalSchedule? Schedule, Guid? ExistingSlotId);

/// An explicitly requested own in-app reminder, independent of hours, quotas and email delivery.
public sealed class TimeEntryReminderJob : IJob
{
    const int BatchSize = 100;
    public string Name => "time-entry-reminder";

    public bool IsDue(DateTimeOffset nowUtc, DateTimeOffset? lastSuccessUtc, OrgSettings settings) =>
        Schedule.Every(TimeSpan.FromMinutes(1), nowUtc, lastSuccessUtc);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var clock = sp.GetRequiredService<TimeProvider>();
        await using var scan = sp.CreateAsyncScope();
        var scanner = scan.ServiceProvider.GetRequiredService<HubDb>();
        Guid? cursor = null;
        int scanned = 0, emitted = 0, alreadyEmitted = 0, notDue = 0, disabled = 0, unavailable = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var candidates = scanner.UserSettings.AsNoTracking().Where(row => row.TimeReminderEnabled == true
                && scanner.Users.Any(user => user.Id == row.UserId && user.IsActive));
            if (cursor is { } after) candidates = candidates.Where(row => row.UserId.CompareTo(after) > 0);
            var batch = await candidates.OrderBy(row => row.UserId).Select(row => row.UserId).Take(BatchSize).ToArrayAsync(ct);
            if (batch.Length == 0) break;

            foreach (var userId in batch)
            {
                ct.ThrowIfCancellationRequested();
                // A separate scope also isolates the tracker after a failed transaction. JobHost's audit scope
                // does not propagate into this scope, so establish system authorship explicitly.
                await using var scope = sp.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<AuditContext>().AsSystem();
                var db = scope.ServiceProvider.GetRequiredService<HubDb>();
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
                var context = await NotificationDelivery.LockRecipientAsync(db, userId, ct);
                var due = await ReadDueAsync(context, clock.GetUtcNow(), ct);
                if (due.Kind == TimeEntryReminderDueKind.Due)
                {
                    // Lock waits, fresh reads and a week rollover cannot reuse a scanner's earlier clock/policy.
                    var emittedAt = clock.GetUtcNow();
                    due = await ReadDueAsync(context, emittedAt, ct);
                    if (due.Kind == TimeEntryReminderDueKind.Due)
                    {
                        await Stage(db, userId, due, emittedAt, ct);
                        await db.SaveChangesAsync(ct);
                    }
                }
                await tx.CommitAsync(ct);
                scanned++;
                switch (due.Kind)
                {
                    case TimeEntryReminderDueKind.Due: emitted++; break;
                    case TimeEntryReminderDueKind.AlreadyEmitted: alreadyEmitted++; break;
                    case TimeEntryReminderDueKind.NotDue: notDue++; break;
                    case TimeEntryReminderDueKind.Disabled: disabled++; break;
                    case TimeEntryReminderDueKind.Unavailable: unavailable++; break;
                }
            }
            cursor = batch[^1];
            if (batch.Length < BatchSize) break;
        }
        return new { scanned, emitted, alreadyEmitted, notDue, disabled, unavailable };
    }

    /// Requires the actual recipient lock and caller-owned ReadCommitted transaction. No writes, default-row
    /// creation, capacity reads or Time-entry inspection; the job and Operations use the same due rule.
    public static async Task<TimeEntryReminderDue> ReadDueAsync(
        NoticeRecipientContext context, DateTimeOffset nowUtc, CancellationToken ct = default)
    {
        DemandContext(context);
        var policy = await NotificationDelivery.ReadRecipientPolicyAsync(context, ct);
        if (policy.UserId != context.UserId) throw new InvalidOperationException("Reminder policy recipient mismatch.");
        if (!policy.IsActive || !policy.Settings.IsFlagOn("people_screens") || !policy.Preferences.TimeReminderEnabled)
            return new(TimeEntryReminderDueKind.Disabled, null, null, null);

        var monday = NotificationDelivery.WeekMonday(nowUtc, policy.Settings);
        var slot = await context.Db.NoticeDeliverySlots.AsNoTracking().SingleOrDefaultAsync(row =>
            row.UserId == context.UserId && row.Kind == NoticeSlotKind.Reminder && row.LocalDate == monday, ct);
        // A cleaned-up app row does not undo the weekly receipt; even invalid later preference data cannot reopen it.
        if (slot?.State == NoticeSlotState.Emitted)
            return new(TimeEntryReminderDueKind.AlreadyEmitted, monday, null, slot.Id);
        var preferences = policy.Preferences;
        if (preferences.TimeReminderWeekday is not (>= 1 and <= 7)
            || !TryTime(preferences.TimeReminderTimeLocal, out var time))
            return new(TimeEntryReminderDueKind.Unavailable, monday, null, slot?.Id);

        NoticeLocalSchedule schedule;
        if (slot is not null)
        {
            if (!TryCapturedSchedule(slot, monday, policy.Settings, out var captured))
                return new(TimeEntryReminderDueKind.Unavailable, monday, null, slot.Id);
            schedule = captured!;
        }
        else
        {
            schedule = NotificationDelivery.ResolveLocal(policy.Settings,
                monday.AddDays(preferences.TimeReminderWeekday.Value - 1), time);
        }
        return new(schedule.ScheduledAtUtc <= nowUtc ? TimeEntryReminderDueKind.Due : TimeEntryReminderDueKind.NotDue,
            monday, schedule, slot?.Id);
    }

    static void DemandContext(NoticeRecipientContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tx = context.Db.Database.CurrentTransaction;
        if (context.UserId == Guid.Empty || tx is null || tx.TransactionId != context.TransactionId
            || tx.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Reminder policy requires the bound live ReadCommitted recipient transaction.");
    }

    static bool TryTime(string? value, out TimeOnly time)
    {
        time = default;
        return value is { Length: 5 } && TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out time);
    }

    static bool TryCapturedSchedule(NoticeDeliverySlot slot, DateOnly monday, OrgSettings settings,
        out NoticeLocalSchedule? schedule)
    {
        schedule = null;
        if (slot.State != NoticeSlotState.Ready || slot.CompositionVersion != 0 || slot.ExternalAttempts != 0
            || slot.WindowFrom is not null || slot.WindowThrough is not null || slot.FrequencyAtCreation is not null
            || slot.ReminderNotificationId is not null || slot.ScheduledLocalDate < monday
            || slot.ScheduledLocalDate.DayNumber - monday.DayNumber > 6
            || string.IsNullOrWhiteSpace(slot.OrgTimeZoneId) || !TryTime(slot.ScheduledLocalTime, out var time)) return false;
        try
        {
            var resolved = NotificationDelivery.ResolveLocal(settings with { OrgTimeZone = slot.OrgTimeZoneId },
                slot.ScheduledLocalDate, time);
            if (!string.Equals(resolved.OrgTimeZoneId, slot.OrgTimeZoneId, StringComparison.Ordinal)
                || resolved.ScheduledAtUtc != slot.ScheduledAtUtc) return false;
            schedule = resolved;
            return true;
        }
        catch (ArgumentException) { return false; } // Malformed captured basis stays held, never re-keyed/replaced.
    }

    static async Task Stage(HubDb db, Guid userId, TimeEntryReminderDue due, DateTimeOffset now, CancellationToken ct)
    {
        var schedule = due.Schedule ?? throw new InvalidOperationException("A due reminder requires its captured schedule.");
        var monday = due.LocalMonday ?? throw new InvalidOperationException("A due reminder requires its local week.");
        var notification = new Notification
        {
            UserId = userId, EventType = "TimeEntryReminder", RequiredProjectIds = null,
            ActorUserId = null, ProjectId = null, ItemType = null, ItemId = null, ItemKey = null,
            Title = Text.Get("noticeReminder.title"), Body = Text.Get("noticeReminder.body"),
            LinkPath = "/time", Count = 1, CreatedAt = now, UpdatedAt = now,
        };
        db.Notifications.Add(notification);
        NoticeDeliverySlot slot;
        if (due.ExistingSlotId is { } id)
        {
            slot = await db.NoticeDeliverySlots.SingleAsync(row => row.Id == id && row.UserId == userId
                && row.Kind == NoticeSlotKind.Reminder && row.LocalDate == monday, ct);
            if (slot.State != NoticeSlotState.Ready) throw new InvalidOperationException("Reminder slot is no longer ready.");
        }
        else
        {
            slot = new NoticeDeliverySlot
            {
                UserId = userId, Kind = NoticeSlotKind.Reminder, LocalDate = monday,
                OrgTimeZoneId = schedule.OrgTimeZoneId, ScheduledLocalDate = schedule.RequestedDate,
                ScheduledLocalTime = schedule.RequestedTime.ToString("HH:mm", CultureInfo.InvariantCulture),
                ScheduledAtUtc = schedule.ScheduledAtUtc, FrequencyAtCreation = null,
                WindowFrom = null, WindowThrough = null, CompositionVersion = 0, ExternalAttempts = 0, CreatedAt = now,
            };
            db.NoticeDeliverySlots.Add(slot);
        }
        slot.State = NoticeSlotState.Emitted;
        slot.ReminderNotificationId = notification.Id;
        slot.UpdatedAt = now;
    }
}
