using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record NoticePreferencesRead(int Revision, string NoticeDigestFrequency, bool HoldImmediateEmailWhenAway,
    bool TimeReminderEnabled, int? TimeReminderWeekday, string? TimeReminderTimeLocal);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NoticePreferencesSetBody([property: JsonRequired] int Revision, string? NoticeDigestFrequency = null,
    bool? HoldImmediateEmailWhenAway = null, bool? TimeReminderEnabled = null, int? TimeReminderWeekday = null, string? TimeReminderTimeLocal = null);

/// S47 foundation only. Root mounts after every legacy preference writer adopts the same recipient lock.
public static class NoticePreferencesEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me/notice-preferences", async Task<Ok<NoticePreferencesRead>> (HubDb db, CurrentUser me, CancellationToken ct) =>
        {
            await Own(db, me, ct);
            var current = await db.UserSettings.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == me.Id, ct);
            return TypedResults.Ok(Read(current)); // Reading defaults never inserts a settings row.
        });
        api.MapPatch("/me/notice-preferences", async Task<Ok<NoticePreferencesRead>> (NoticePreferencesSetBody body, HubDb db, CurrentUser me, TimeProvider clock, CancellationToken ct) =>
            TypedResults.Ok(await Tx.Run(db, async () =>
            {
                var recipient = await NotificationDelivery.LockRecipientAsync(db, me.Id, ct);
                await Own(db, me, ct);
                Check.That(body.Revision >= 0, "revision", "error.invalid");
                var current = await db.UserSettings.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == me.Id, ct);
                var before = Read(current);
                if (before.Revision != body.Revision) throw ApiException.Conflict("notice_preferences_conflict", "error.concurrency");
                if (body.NoticeDigestFrequency is { } frequency)
                    Check.That(NoticeDigestFrequency.All.Contains(frequency), "noticeDigestFrequency", "error.one_of", "Daily, Weekly");
                if (body.TimeReminderWeekday is { } weekday)
                    Check.That(weekday is >= 1 and <= 7, "timeReminderWeekday", "error.one_of", "1–7");
                if (body.TimeReminderTimeLocal is { } time)
                    Check.That(ValidTime(time), "timeReminderTimeLocal", "setting.time");
                var next = new NoticePreferencesRead(before.Revision,
                    body.NoticeDigestFrequency ?? before.NoticeDigestFrequency,
                    body.HoldImmediateEmailWhenAway ?? before.HoldImmediateEmailWhenAway,
                    body.TimeReminderEnabled ?? before.TimeReminderEnabled,
                    body.TimeReminderWeekday ?? before.TimeReminderWeekday,
                    body.TimeReminderTimeLocal ?? before.TimeReminderTimeLocal);
                if (next.TimeReminderEnabled)
                {
                    Check.That(next.TimeReminderWeekday is >= 1 and <= 7, "timeReminderWeekday", "error.required");
                    Check.That(next.TimeReminderTimeLocal is not null && ValidTime(next.TimeReminderTimeLocal), "timeReminderTimeLocal", "setting.time");
                }
                if (next == before) return before; // Null/default/no-op fragments do not insert or advance revision.
                if (before.Revision == int.MaxValue) throw ApiException.Conflict("notice_preferences_revision_exhausted", "error.concurrency");
                await UserPreferences.GetOrCreate(db, me.Id, ct);
                var frequencyValue = body.NoticeDigestFrequency ?? current?.NoticeDigestFrequency;
                var holdValue = body.HoldImmediateEmailWhenAway ?? current?.HoldImmediateEmailWhenAway;
                var enabledValue = body.TimeReminderEnabled ?? current?.TimeReminderEnabled;
                var weekdayValue = body.TimeReminderWeekday ?? current?.TimeReminderWeekday;
                var timeValue = body.TimeReminderTimeLocal ?? current?.TimeReminderTimeLocal;
                var updated = await db.UserSettings.Where(row => row.UserId == me.Id && (row.NoticePreferencesRevision ?? 0) == body.Revision)
                    .ExecuteUpdateAsync(set => set.SetProperty(row => row.NoticeDigestFrequency, frequencyValue)
                        .SetProperty(row => row.HoldImmediateEmailWhenAway, holdValue).SetProperty(row => row.TimeReminderEnabled, enabledValue)
                        .SetProperty(row => row.TimeReminderWeekday, weekdayValue).SetProperty(row => row.TimeReminderTimeLocal, timeValue)
                        .SetProperty(row => row.NoticePreferencesRevision, before.Revision + 1), ct);
                if (updated != 1) throw ApiException.Conflict("notice_preferences_conflict", "error.concurrency");
                await NotificationDelivery.ObservePolicyChangeAsync(recipient,
                    new(NoticePolicyChangeKind.EnhancedPreferences), clock.GetUtcNow(), ct);
                await db.SaveChangesAsync(ct);
                return next with { Revision = before.Revision + 1 };
            })));
    }

    static NoticePreferencesRead Read(UserSetting? row) => new(row?.NoticePreferencesRevision ?? 0,
        row?.NoticeDigestFrequency ?? NoticeDigestFrequency.Daily, row?.HoldImmediateEmailWhenAway ?? false,
        row?.TimeReminderEnabled ?? false, row?.TimeReminderWeekday, row?.TimeReminderTimeLocal);

    static bool ValidTime(string value) => value.Length == 5 && TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    static async Task Own(HubDb db, CurrentUser me, CancellationToken ct)
    {
        // A cached pre-lock settings object cannot prove that this addition is still enabled.
        var flag = await db.Settings.AsNoTracking().Where(row => row.Key == "flag.people_screens").Select(row => row.Value).SingleOrDefaultAsync(ct);
        if (flag is null) throw ApiException.NotFound();
        using var current = JsonDocument.Parse(flag);
        if (current.RootElement.ValueKind != JsonValueKind.True) throw ApiException.NotFound();
        if (!me.Resolved || me.Id == Guid.Empty || !me.IsActive || !await db.Users.AsNoTracking().AnyAsync(user => user.Id == me.Id && user.IsActive, ct))
            throw ApiException.Forbidden("error.inactive_account");
        // ReadOnly limits business commands, not this person's own settings. There is no target user input.
    }
}
