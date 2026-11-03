using System.Data;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

// Isolated PostgreSQL/fake-clock proofs. Authored only; root registers the job/shared core and runs the filter.
public sealed class TimeEntryReminderTests : IDisposable
{
    const string PreferencesPath = "/api/v1/me/notice-preferences";
    readonly HubFactory f = new();
    readonly TestData data;
    static DateTimeOffset At(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    public TimeEntryReminderTests()
    {
        f.Clock.Now = At("2026-11-02T09:00:00Z");
        data = new(f);
    }
    public void Dispose() => f.Dispose();

    async Task Flag(bool on = true) =>
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = on })).EnsureSuccessStatusCode();

    async Task Zone(string value = "UTC") =>
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value })).EnsureSuccessStatusCode();

    async Task Set(string person, bool enabled = true, int weekday = 1, string time = "09:00")
    {
        var before = await (await f.As(person).GetAsync(PreferencesPath)).Json();
        await (await f.As(person).Patch(PreferencesPath, new { revision = before.I("revision"),
            timeReminderEnabled = enabled, timeReminderWeekday = weekday, timeReminderTimeLocal = time })).Json();
    }

    async Task<Guid> Enable(string person = TestData.Alex, int weekday = 1, string time = "09:00")
    {
        await Flag(); await Zone(); await Set(person, weekday: weekday, time: time);
        return data.User(person);
    }

    Task<object?> Run(CancellationToken ct = default) => new TimeEntryReminderJob().Run(f.Services, ct);
    Task<TimeEntryReminderDue> Due(Guid user) => f.DbAsync(db => Tx.Run(db, async () => {
        var context = await NotificationDelivery.LockRecipientAsync(db, user);
        return await TimeEntryReminderJob.ReadDueAsync(context, f.Clock.GetUtcNow());
    }));

    Notification[] Notices() => f.Db(db => db.Notifications.AsNoTracking().Where(row => row.EventType == "TimeEntryReminder").ToArray());
    NoticeDeliverySlot[] Slots() => f.Db(db => db.NoticeDeliverySlots.AsNoTracking().Where(row => row.Kind == NoticeSlotKind.Reminder).ToArray());
    string OtherState() => f.Db(db => JsonSerializer.Serialize(new {
        Activity = db.ActivityLog.Count(), Outbox = db.Outbox.Count(), Emails = db.Emails.Count(),
        Intents = db.NoticeDeliveryIntents.Count(), Time = db.TimeEntries.Count(),
        Preferences = db.UserSettings.AsNoTracking().OrderBy(row => row.UserId).Select(row => new {
            row.UserId, row.NoticePreferencesRevision, row.NoticeDigestFrequency, row.HoldImmediateEmailWhenAway,
            row.TimeReminderEnabled, row.TimeReminderWeekday, row.TimeReminderTimeLocal,
            row.DigestEnabled, row.HoldDigestWhenAway, row.DigestTimeLocal, row.DigestSectionsOff,
            row.LastDigestAt, row.LastWeeklySummaryAt, row.WeeklySummaryEnabled, row.DenseRows,
        }).ToArray(),
    }));

    [Fact]
    public void Poll_interval_is_one_minute_and_job_identity_is_stable()
    {
        var job = new TimeEntryReminderJob(); var now = At("2026-11-02T09:00:00Z"); var settings = new OrgSettings();
        Assert.Equal("time-entry-reminder", job.Name);
        Assert.True(job.IsDue(now, null, settings));
        Assert.False(job.IsDue(now, now.AddSeconds(-59), settings));
        Assert.True(job.IsDue(now, now.AddMinutes(-1), settings));
        Assert.True(job.IsDue(now, now.AddMinutes(-2), settings));
        Assert.False(job.IsDue(now, now.AddMinutes(1), settings));
    }

    [Fact]
    public async Task Only_explicit_active_flagged_opt_in_runs_even_when_forced_and_reads_create_no_defaults()
    {
        await Flag(); await Zone(); var user = data.User(TestData.Alex);
        await f.DbAsync(db => db.UserSettings.Where(row => row.UserId == user).ExecuteDeleteAsync());
        var before = OtherState(); await Run();
        Assert.Equal(before, OtherState());
        Assert.False(f.Db(db => db.UserSettings.Any(row => row.UserId == user)));
        Assert.Equal(TimeEntryReminderDueKind.Disabled, (await Due(user)).Kind);
        await Set(TestData.Alex, enabled: false);
        before = OtherState(); await Run(); Assert.Equal(before, OtherState());
        await Set(TestData.Alex);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(row => row.Id == user)).IsActive = false; return await db.SaveChangesAsync(); });
        Assert.Equal(TimeEntryReminderDueKind.Disabled, (await Due(user)).Kind);
        before = OtherState(); await Run(); Assert.Equal(before, OtherState());
        await f.DbAsync(async db => { (await db.Users.SingleAsync(row => row.Id == user)).IsActive = true; return await db.SaveChangesAsync(); });
        await Flag(false);
        // JobHost's force bypasses coarse IsDue but cannot authorize the addition.
        before = OtherState(); await f.RunJob<TimeEntryReminderJob>();
        Assert.Equal(before, OtherState()); Assert.Empty(Notices()); Assert.Empty(Slots());
        await Flag(); await Run(); Assert.Single(Notices()); Assert.Single(Slots());
    }

    [Fact]
    public async Task Exact_due_and_once_per_local_week_survive_edits_disable_enable_cleanup_and_next_week()
    {
        var user = await Enable();
        f.Clock.Now = At("2026-11-02T08:59:59Z");
        var before = OtherState(); Assert.Equal(TimeEntryReminderDueKind.NotDue, (await Due(user)).Kind);
        await Run(); Assert.Empty(Notices()); Assert.Empty(Slots()); Assert.Equal(before, OtherState());
        f.Clock.Now = At("2026-11-02T09:00:00Z");
        var due = await Due(user); Assert.Equal(TimeEntryReminderDueKind.Due, due.Kind);
        Assert.Equal(new DateOnly(2026, 11, 2), due.LocalMonday); Assert.Null(due.ExistingSlotId);
        await f.RunJob<TimeEntryReminderJob>();
        var notice = Assert.Single(Notices()); var slot = Assert.Single(Slots());
        Assert.Equal(user, notice.UserId); Assert.Equal("/time", notice.LinkPath);
        Assert.Equal(Text.Get("noticeReminder.title"), notice.Title); Assert.Equal(Text.Get("noticeReminder.body"), notice.Body);
        Assert.NotEqual("noticeReminder.title", notice.Title); Assert.NotEqual("noticeReminder.body", notice.Body);
        Assert.Null(notice.ActorUserId); Assert.Null(notice.ProjectId); Assert.Null(notice.RequiredProjectIds);
        Assert.Null(notice.ItemType); Assert.Null(notice.ItemId); Assert.Null(notice.ItemKey); Assert.Equal(1, notice.Count);
        Assert.Equal(notice.Id, slot.ReminderNotificationId); Assert.Equal(NoticeSlotState.Emitted, slot.State);
        Assert.Equal(new DateOnly(2026, 11, 2), slot.LocalDate); Assert.Equal(slot.LocalDate, slot.ScheduledLocalDate);
        Assert.Equal("09:00", slot.ScheduledLocalTime); Assert.Equal(f.Clock.Now, slot.ScheduledAtUtc);
        Assert.Null(slot.WindowFrom); Assert.Null(slot.WindowThrough); Assert.Null(slot.FrequencyAtCreation);
        Assert.Equal(0, slot.CompositionVersion); Assert.Equal(0, slot.ExternalAttempts); Assert.Equal(before, OtherState());

        await Set(TestData.Alex, enabled: false, weekday: 2, time: "08:00");
        await Set(TestData.Alex, weekday: 2, time: "08:00");
        f.Clock.Now = At("2026-11-03T10:00:00Z");
        before = OtherState(); await Run(); Assert.Single(Notices()); Assert.Single(Slots()); Assert.Equal(before, OtherState());
        await f.DbAsync(db => db.Notifications.Where(row => row.Id == notice.Id).ExecuteDeleteAsync());
        Assert.Null(Assert.Single(Slots()).ReminderNotificationId);
        Assert.Equal(TimeEntryReminderDueKind.AlreadyEmitted, (await Due(user)).Kind);
        await Run(); Assert.Empty(Notices()); Assert.Single(Slots());
        f.Clock.Now = At("2026-11-10T08:00:00Z");
        await Run(); Assert.Single(Notices()); Assert.Equal(2, Slots().Length);
        Assert.Contains(Slots(), row => row.LocalDate == new DateOnly(2026, 11, 9) && row.State == NoticeSlotState.Emitted);
        var run = f.Db(db => db.JobRuns.AsNoTracking().Where(row => row.JobName == "time-entry-reminder").Single());
        Assert.Equal("Succeeded", run.Status); Assert.DoesNotContain(user.ToString(), run.Details ?? "");
    }

    [Theory]
    [InlineData("America/Halifax", "2026-03-08T06:00:00Z", 7, "02:30", "2026-03-02", "03:00")]
    [InlineData("America/Halifax", "2026-11-01T04:30:00Z", 7, "01:30", "2026-10-26", "01:30")]
    [InlineData("Australia/Lord_Howe", "2026-10-03T15:30:00Z", 7, "02:15", "2026-09-28", "02:30")]
    [InlineData("Synthetic/InvalidZone", "2026-11-02T09:00:00Z", 1, "09:00", "2026-11-02", "09:00")]
    public async Task Calendar_resolution_uses_fixed_independent_UTC_expectations_and_emits_only_once(
        string zone, string instant, int weekday, string chosenTime, string monday, string effectiveTime)
    {
        var expected = At(instant); f.Clock.Now = expected.AddSeconds(-1);
        var user = await Enable(weekday: weekday, time: chosenTime);
        // Raw invalid zone is a synthetic corruption fixture: Clock.Zone's existing fallback must remain UTC.
        await f.DbAsync(async db => { (await db.Settings.SingleAsync(row => row.Key == "org_time_zone")).Value = JsonSerializer.Serialize(zone); return await db.SaveChangesAsync(); });
        var due = await Due(user); Assert.Equal(TimeEntryReminderDueKind.NotDue, due.Kind);
        Assert.Equal(expected, due.Schedule!.ScheduledAtUtc);
        Assert.Equal(effectiveTime, due.Schedule.EffectiveTime.ToString("HH:mm", CultureInfo.InvariantCulture));
        await Run(); Assert.Empty(Slots());
        f.Clock.Now = expected; await Run();
        var slot = Assert.Single(Slots()); Assert.Equal(expected, slot.ScheduledAtUtc);
        Assert.Equal(DateOnly.ParseExact(monday, "yyyy-MM-dd", CultureInfo.InvariantCulture), slot.LocalDate);
        Assert.Equal(chosenTime, slot.ScheduledLocalTime); Assert.Equal(zone == "Synthetic/InvalidZone" ? "UTC" : zone, slot.OrgTimeZoneId);
        // Includes the second occurrence of the repeated Halifax hour, without an offset-specific second key.
        f.Clock.Now = expected.AddHours(2); await Run(); Assert.Single(Notices()); Assert.Single(Slots());
    }

    [Fact]
    public async Task Missed_previous_weeks_never_backfill_and_due_reader_does_not_create_a_slot()
    {
        var user = await Enable(weekday: 7); f.Clock.Now = At("2026-11-02T10:00:00Z");
        await f.DbAsync(async db => { db.NoticeDeliverySlots.Add(Ready(user, new(2026, 10, 26), new(2026, 10, 26), At("2026-10-26T09:00:00Z"))); return await db.SaveChangesAsync(); });
        Assert.Equal(TimeEntryReminderDueKind.NotDue, (await Due(user)).Kind);
        Assert.Single(Slots()); await Run(); Assert.Empty(Notices());
        f.Clock.Now = At("2026-11-08T09:00:00Z"); await Run();
        Assert.Equal(2, Slots().Length); Assert.Single(Notices());
        Assert.Equal(NoticeSlotState.Ready, Slots().Single(row => row.LocalDate == new DateOnly(2026, 10, 26)).State);
        Assert.Equal(new DateOnly(2026, 11, 2), Slots().Single(row => row.State == NoticeSlotState.Emitted).LocalDate);
    }

    static NoticeDeliverySlot Ready(Guid user, DateOnly monday, DateOnly chosenDate, DateTimeOffset instant) => new() {
        UserId = user, Kind = NoticeSlotKind.Reminder, LocalDate = monday, OrgTimeZoneId = "UTC",
        ScheduledLocalDate = chosenDate, ScheduledLocalTime = "09:00", ScheduledAtUtc = instant,
        CreatedAt = instant.AddDays(-1), UpdatedAt = instant.AddDays(-1), State = NoticeSlotState.Ready,
    };

    [Theory]
    [InlineData("valid")]
    [InlineData("zone")]
    [InlineData("instant")]
    [InlineData("date")]
    public async Task Existing_Ready_preserves_captured_basis_or_stays_unavailable_without_replacement(string variant)
    {
        var user = await Enable(weekday: 3, time: "11:00"); f.Clock.Now = At("2026-11-02T10:00:00Z");
        var slot = Ready(user, new(2026, 11, 2), new(2026, 11, 2), At("2026-11-02T09:00:00Z"));
        if (variant == "zone") slot.OrgTimeZoneId = "Synthetic/InvalidZone";
        if (variant == "instant") slot.ScheduledAtUtc = slot.ScheduledAtUtc.AddMinutes(1);
        if (variant == "date") slot.ScheduledLocalDate = new(2026, 11, 1);
        await f.DbAsync(async db => { db.NoticeDeliverySlots.Add(slot); return await db.SaveChangesAsync(); });
        var original = JsonSerializer.Serialize(Assert.Single(Slots()));
        var due = await Due(user);
        Assert.Equal(variant == "valid" ? TimeEntryReminderDueKind.Due : TimeEntryReminderDueKind.Unavailable, due.Kind);
        Assert.Equal(slot.Id, due.ExistingSlotId); await Run();
        var current = Assert.Single(Slots()); Assert.Equal(slot.Id, current.Id);
        if (variant == "valid") {
            Assert.Single(Notices()); Assert.Equal(NoticeSlotState.Emitted, current.State);
            Assert.Equal("09:00", current.ScheduledLocalTime); Assert.Equal(slot.ScheduledAtUtc, current.ScheduledAtUtc);
            Assert.Equal(slot.CreatedAt, current.CreatedAt);
        } else {
            Assert.Empty(Notices()); Assert.Equal(original, JsonSerializer.Serialize(current));
        }
    }

    static async Task WaitForRecipientWait(HubFactory factory)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!await factory.DbAsync(db => db.Database.SqlQuery<bool>($"""
            SELECT EXISTS(SELECT 1 FROM pg_stat_activity
              WHERE datname=current_database() AND pid<>pg_backend_pid()
                AND wait_event_type='Lock' AND query LIKE '%pg_advisory_xact_lock%') AS "Value"
            """).SingleAsync(timeout.Token)))
            await Task.Delay(10, timeout.Token);
    }

    [Theory]
    [InlineData("optOut")]
    [InlineData("schedule")]
    [InlineData("inactive")]
    [InlineData("flag")]
    [InlineData("week")]
    public async Task Waiting_job_rechecks_current_policy_and_clock_after_the_real_recipient_lock(string change)
    {
        var user = await Enable(weekday: change == "week" ? 7 : 1);
        f.Clock.Now = At(change == "week" ? "2026-11-08T10:00:00Z" : "2026-11-02T10:00:00Z");
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = f.DbAsync(db => Tx.Run(db, async () => {
            await NoticeRecipientLock.Acquire(db, user); locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var setting = await db.UserSettings.SingleAsync(row => row.UserId == user);
            if (change == "optOut") setting.TimeReminderEnabled = false;
            if (change == "schedule") setting.TimeReminderWeekday = 2;
            if (change == "inactive") (await db.Users.SingleAsync(row => row.Id == user)).IsActive = false;
            if (change == "flag") (await db.Settings.SingleAsync(row => row.Key == "flag.people_screens")).Value = "false";
            if (change == "week") f.Clock.Now = At("2026-11-09T00:00:00Z");
            return await db.SaveChangesAsync();
        }));
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(15)); var job = Run();
        try { await WaitForRecipientWait(f); Assert.False(job.IsCompleted); }
        finally { release.TrySetResult(); }
        await holder; await job; Assert.Empty(Notices()); Assert.Empty(Slots());
        Assert.Equal(change is "schedule" or "week" ? TimeEntryReminderDueKind.NotDue : TimeEntryReminderDueKind.Disabled,
            (await Due(user)).Kind);
    }

    [Fact]
    public async Task Independent_concurrent_workers_and_bounded_keyset_batches_emit_one_row_per_recipient()
    {
        var alex = await Enable();
        var users = Enumerable.Range(0, 100).Select(i => new AppUser { Email = $"synthetic-reminder-{i}-{Guid.NewGuid():N}@hub.test", DisplayName = "Synthetic reminder recipient" }).ToArray();
        await f.DbAsync(async db => {
            db.Users.AddRange(users); db.UserSettings.AddRange(users.Select(user => new UserSetting {
                UserId = user.Id, TimeReminderEnabled = true, TimeReminderWeekday = 1, TimeReminderTimeLocal = "09:00",
            })); return await db.SaveChangesAsync();
        });
        var before = OtherState(); await Task.WhenAll(Run(), Run());
        var notices = Notices(); var slots = Slots();
        Assert.Equal(101, notices.Length); Assert.Equal(101, slots.Length);
        Assert.Equal(101, slots.Select(row => row.UserId).Distinct().Count());
        Assert.All(slots, slot => Assert.Equal(slot.UserId, notices.Single(row => row.Id == slot.ReminderNotificationId).UserId));
        Assert.Contains(notices, row => row.UserId == alex); Assert.Equal(before, OtherState());
        var result = JsonSerializer.SerializeToNode(await Run())!;
        Assert.Equal(101, result["scanned"]!.GetValue<int>()); Assert.Equal(0, result["emitted"]!.GetValue<int>());
        Assert.Equal(101, result["alreadyEmitted"]!.GetValue<int>());
        Assert.Equal(new[] { "scanned", "emitted", "alreadyEmitted", "notDue", "disabled", "unavailable" }, result.AsObject().Select(pair => pair.Key));
    }

    [Fact]
    public async Task Database_fault_rolls_back_notification_and_slot_together_then_retry_emits_once()
    {
        await Enable(); var before = OtherState();
        var table = f.Db(db => db.Model.FindEntityType(typeof(NoticeDeliverySlot))!.GetTableName()!);
        await f.DbAsync(async db => {
            await db.Database.ExecuteSqlRawAsync($"""
                CREATE FUNCTION synthetic_reminder_fail() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'synthetic reminder slot failure'; RETURN NEW; END $$;
                CREATE TRIGGER synthetic_reminder_fail BEFORE INSERT ON "{table}"
                FOR EACH ROW EXECUTE FUNCTION synthetic_reminder_fail();
                """); return 0;
        });
        try {
            await Assert.ThrowsAsync<DbUpdateException>(() => Run());
            Assert.Empty(Notices()); Assert.Empty(Slots()); Assert.Equal(before, OtherState());
        } finally {
            await f.DbAsync(async db => { await db.Database.ExecuteSqlRawAsync($"""
                DROP TRIGGER synthetic_reminder_fail ON "{table}";
                DROP FUNCTION synthetic_reminder_fail();
                """); return 0; });
        }
        await Run(); await Run(); Assert.Single(Notices()); Assert.Single(Slots()); Assert.Equal(before, OtherState());
    }

    [Fact]
    public async Task Cancellation_while_waiting_propagates_without_committing_and_retry_is_safe()
    {
        var user = await Enable();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = f.DbAsync(db => Tx.Run(db, async () => {
            await NoticeRecipientLock.Acquire(db, user); locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(30)); return 0;
        }));
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(15));
        using var cancellation = new CancellationTokenSource(); var job = Run(cancellation.Token);
        try {
            await WaitForRecipientWait(f); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => job);
            Assert.Empty(Notices()); Assert.Empty(Slots());
        } finally { release.TrySetResult(); await holder; }
        await Run(); Assert.Single(Notices()); Assert.Single(Slots());
    }

    [Fact]
    public async Task Due_context_cannot_be_reused_in_a_different_transaction()
    {
        var user = await Enable();
        await f.DbAsync(async db => {
            NoticeRecipientContext context;
            await using (var first = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted)) {
                context = await NotificationDelivery.LockRecipientAsync(db, user); await first.CommitAsync();
            }
            await using var second = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            await Assert.ThrowsAsync<InvalidOperationException>(() => TimeEntryReminderJob.ReadDueAsync(context, f.Clock.Now));
            return 0;
        });
        Assert.Empty(Notices()); Assert.Empty(Slots());
    }

    [Fact]
    public async Task ReadOnly_opt_in_keeps_personal_Time_scope_and_existing_create_edit_refusals()
    {
        var user = await Enable(TestData.Rita); var project = await data.Project();
        var task = await data.NewTask(project.Id, TestData.Pm);
        var peer = await (await f.As(TestData.Alex).Post("/api/v1/time", new {
            taskId = task.G("id"), workDate = "2026-11-02", hours = 1m,
        })).Json(201);
        var mine = new TaskTimeEntry { ProjectId = project.Id, TaskId = task.G("id"), UserId = user,
            WorkDate = new(2026, 11, 2), Hours = 0.5m, AuditKey = task.S("key") };
        await f.DbAsync(async db => { db.TimeEntries.Add(mine); return await db.SaveChangesAsync(); });
        var before = OtherState(); await Run(); Assert.Equal(before, OtherState());
        Assert.Equal(user, Assert.Single(Notices()).UserId);
        var view = await (await f.As(TestData.Rita).GetAsync("/api/v1/time?from=2026-11-02&to=2026-11-08")).Json();
        var own = Assert.Single(view["entries"]!.AsArray()); Assert.Equal(user, own!.G("userId"));
        Assert.NotEqual(peer.G("id"), own.G("id"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post("/api/v1/time", new {
            taskId = task.G("id"), workDate = "2026-11-02", hours = 1m,
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Patch($"/api/v1/time/{mine.Id}", new { hours = 1m }, own.I("rowVersion"))).StatusCode);
        Assert.Equal(0.5m, f.Db(db => db.TimeEntries.Single(row => row.Id == mine.Id).Hours));
    }
}
