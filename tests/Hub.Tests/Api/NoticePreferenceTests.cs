using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

// Provider/API foundation proofs. Root must register the API after legacy lock adoption before running these.
public sealed class NoticePreferenceTests
{
    const string Path = "/api/v1/me/notice-preferences";
    static async Task Flag(HubFactory f, bool on = true) =>
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = on })).EnsureSuccessStatusCode();
    static string State(HubFactory f) => f.Db(db => JsonSerializer.Serialize(new
    {
        Preferences = db.UserSettings.AsNoTracking().OrderBy(row => row.UserId).Select(row => new
        {
            row.UserId, row.NoticePreferencesRevision, row.NoticeDigestFrequency, row.HoldImmediateEmailWhenAway,
            row.TimeReminderEnabled, row.TimeReminderWeekday, row.TimeReminderTimeLocal, row.DigestEnabled, row.HoldDigestWhenAway,
            row.DigestTimeLocal, row.DenseRows, row.WeeklySummaryEnabled, row.DigestSectionsOff, row.LastDigestAt, row.LastWeeklySummaryAt,
            row.MyWorkFirstVisitAt, row.MyWorkSeenRevision,
        }).ToArray(),
        Activity = db.ActivityLog.Count(), Notifications = db.Notifications.Count(), Email = db.Emails.Count(), Outbox = db.Outbox.Count(),
        Intents = db.Set<NoticeDeliveryIntent>().Count(), Slots = db.Set<NoticeDeliverySlot>().Count(),
    }));

    [Fact]
    public async Task Own_defaults_and_noop_reads_do_not_create_settings_or_delivery_work_and_off_is404()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f);
        var user = d.User(TestData.Alex);
        await f.DbAsync(db => db.UserSettings.Where(row => row.UserId == user).ExecuteDeleteAsync());
        var before = State(f);
        var read = await (await f.As(TestData.Alex).GetAsync(Path)).Json();
        Assert.Equal(new[] { "revision", "noticeDigestFrequency", "holdImmediateEmailWhenAway", "timeReminderEnabled", "timeReminderWeekday", "timeReminderTimeLocal" }, read.AsObject().Select(value => value.Key));
        Assert.Equal(0, read.I("revision")); Assert.Equal("Daily", read.S("noticeDigestFrequency"));
        Assert.False(read["holdImmediateEmailWhenAway"]!.GetValue<bool>()); Assert.False(read["timeReminderEnabled"]!.GetValue<bool>());
        Assert.Null(read["timeReminderWeekday"]); Assert.Null(read["timeReminderTimeLocal"]);
        await (await f.As(TestData.Alex).Patch(Path, new { revision = 0, noticeDigestFrequency = "Daily", holdImmediateEmailWhenAway = false })).Json();
        await (await f.As(TestData.Alex).Patch(Path, new { revision = 0, timeReminderEnabled = (bool?)null })).Json();
        Assert.Equal(before, State(f));
        await Flag(f, false); var off = State(f);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).Patch(Path, new { revision = 0, holdImmediateEmailWhenAway = true })).StatusCode);
        Assert.Equal(off, State(f));
    }

    [Fact]
    public async Task Changed_fragment_preserves_legacy_fields_and_disabled_reminder_retains_choices()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f); var user = d.User(TestData.Alex);
        await f.DbAsync(db => Tx.Run(db, async () => {
            await NoticeRecipientLock.Acquire(db, user); var row = await UserPreferences.GetOrCreate(db, user);
            row.DigestEnabled = false; row.HoldDigestWhenAway = false; row.DenseRows = false; row.DigestTimeLocal = "07:30";
            row.LastDigestAt = f.Clock.Now; row.MyWorkSeenRevision = 9; return await db.SaveChangesAsync();
        }));
        var before = f.Db(db => db.UserSettings.AsNoTracking().Single(row => row.UserId == user));
        var saved = await (await f.As(TestData.Alex).Patch(Path, new { revision = 0, noticeDigestFrequency = "Weekly",
            holdImmediateEmailWhenAway = true, timeReminderEnabled = true, timeReminderWeekday = 5, timeReminderTimeLocal = "16:30" })).Json();
        Assert.Equal(1, saved.I("revision")); Assert.Equal("Weekly", saved.S("noticeDigestFrequency"));
        var disabled = await (await f.As(TestData.Alex).Patch(Path, new { revision = 1, timeReminderEnabled = false })).Json();
        Assert.Equal(2, disabled.I("revision")); Assert.Equal(5, disabled.I("timeReminderWeekday")); Assert.Equal("16:30", disabled.S("timeReminderTimeLocal"));
        var noOp = await (await f.As(TestData.Alex).Patch(Path, new { revision = 2, timeReminderEnabled = false })).Json();
        Assert.Equal(2, noOp.I("revision"));
        var after = f.Db(db => db.UserSettings.AsNoTracking().Single(row => row.UserId == user));
        Assert.Equal((before.DigestEnabled, before.HoldDigestWhenAway, before.DenseRows, before.DigestTimeLocal, before.LastDigestAt, before.MyWorkSeenRevision),
            (after.DigestEnabled, after.HoldDigestWhenAway, after.DenseRows, after.DigestTimeLocal, after.LastDigestAt, after.MyWorkSeenRevision));
        Assert.False(f.Db(db => db.Set<NoticeDeliveryIntent>().Any())); Assert.False(f.Db(db => db.Set<NoticeDeliverySlot>().Any()));
        Assert.Null(f.Db(db => db.UserSettings.Where(row => row.UserId == d.User(TestData.Jill)).Select(row => row.NoticeDigestFrequency).FirstOrDefault()));
    }

    [Fact]
    public async Task Invalid_or_unknown_fragments_missing_revision_and_stale_revision_write_nothing()
    {
        using var f = new HubFactory(); await Flag(f); var client = f.As(TestData.Alex);
        object[] invalid = [new { revision = -1 }, new { revision = 0, noticeDigestFrequency = "weekly" },
            new { revision = 0, timeReminderWeekday = 0 }, new { revision = 0, timeReminderWeekday = 8 },
            new { revision = 0, timeReminderTimeLocal = "9:30" }, new { revision = 0, timeReminderTimeLocal = "24:00" },
            new { revision = 0, timeReminderEnabled = true }, new { revision = 0, timeReminderEnabled = true, timeReminderWeekday = 2 },
            new { revision = 0, timeReminderEnabled = true, timeReminderTimeLocal = "12:00" }, new { revision = 0, userId = Guid.NewGuid() },
            new { holdImmediateEmailWhenAway = true }];
        var before = State(f);
        foreach (var body in invalid)
        { Assert.Equal(HttpStatusCode.BadRequest, (await client.Patch(Path, body)).StatusCode); Assert.Equal(before, State(f)); }
        await (await client.Patch(Path, new { revision = 0, holdImmediateEmailWhenAway = true })).Json();
        var committed = State(f);
        var conflict = await (await client.Patch(Path, new { revision = 0, timeReminderWeekday = 3, timeReminderTimeLocal = "14:00" })).Json(409);
        Assert.Equal("notice_preferences_conflict", conflict.S("code")); Assert.Equal(committed, State(f));
    }

    [Fact]
    public async Task ReadOnly_person_can_change_only_their_own_preference_fragment()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f);
        var result = await (await f.As(TestData.Rita).Patch(Path, new { revision = 0, timeReminderWeekday = 2,
            timeReminderTimeLocal = "15:45", timeReminderEnabled = true })).Json();
        Assert.Equal(1, result.I("revision")); Assert.True(result["timeReminderEnabled"]!.GetValue<bool>());
        var rita = d.User(TestData.Rita); var alex = d.User(TestData.Alex);
        Assert.True(f.Db(db => db.UserSettings.Single(row => row.UserId == rita).TimeReminderEnabled));
        Assert.Null(f.Db(db => db.UserSettings.Where(row => row.UserId == alex).Select(row => row.TimeReminderEnabled).FirstOrDefault()));
        var before = State(f);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Patch(Path, new { revision = 0, userId = alex, holdImmediateEmailWhenAway = true })).StatusCode);
        Assert.Equal(before, State(f));
    }

    [Fact]
    public async Task Real_recipient_lock_serializes_first_row_race_and_preserves_separate_legacy_fields()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f); var user = d.User(TestData.Alex);
        await f.DbAsync(db => db.UserSettings.Where(row => row.UserId == user).ExecuteDeleteAsync());
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = f.DbAsync(db => Tx.Run(db, async () => {
            await NoticeRecipientLock.Acquire(db, user); locked.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(30)); return 0;
        }));
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var first = f.As(TestData.Alex).Patch(Path, new { revision = 0, noticeDigestFrequency = "Weekly" });
        var second = f.As(TestData.Alex).Patch(Path, new { revision = 0, holdImmediateEmailWhenAway = true });
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var waiting = 0;
            while (waiting < 2)
            {
                waiting = await f.DbAsync(db => db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*)::int AS "Value" FROM pg_stat_activity
                    WHERE datname=current_database() AND pid<>pg_backend_pid()
                      AND wait_event_type='Lock' AND query LIKE '%pg_advisory_xact_lock%'
                    """).SingleAsync(deadline.Token));
                deadline.Token.ThrowIfCancellationRequested(); if (waiting < 2) await Task.Yield();
            }
            Assert.False(first.IsCompleted); Assert.False(second.IsCompleted);
        }
        finally { release.TrySetResult(); }
        await holder;
        var answers = await Task.WhenAll(first, second);
        Assert.Single(answers.Where(value => value.StatusCode == HttpStatusCode.OK));
        Assert.Single(answers.Where(value => value.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, f.Db(db => db.UserSettings.Count(row => row.UserId == user)));
        Assert.Equal(1, f.Db(db => db.UserSettings.Single(row => row.UserId == user).NoticePreferencesRevision));
        // Legacy settings write must use root's same lock but must not touch the enhanced revision/fields.
        var fragment = await (await f.As(TestData.Alex).GetAsync(Path)).Json();
        (await f.As(TestData.Alex).Put("/api/v1/me/preferences/density", new { denseRows = false })).EnsureSuccessStatusCode();
        Assert.Equal(fragment.ToJsonString(), (await (await f.As(TestData.Alex).GetAsync(Path)).Json()).ToJsonString());
        Assert.False(f.Db(db => db.UserSettings.Single(row => row.UserId == user).DenseRows));
    }

    [Fact]
    public async Task Flag_turned_off_while_patch_waits_is_read_fresh_after_the_lock()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f); var user = d.User(TestData.Alex);
        await (await f.As(TestData.Alex).GetAsync(Path)).Json();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = f.DbAsync(db => Tx.Run(db, async () => {
            await NoticeRecipientLock.Acquire(db, user); locked.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(30)); return 0;
        }));
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var patch = f.As(TestData.Alex).Patch(Path, new { revision = 0, holdImmediateEmailWhenAway = true });
        string before;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var waiting = false;
            while (!waiting)
            {
                waiting = await f.DbAsync(db => db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*)::int AS "Value" FROM pg_stat_activity
                    WHERE datname=current_database() AND pid<>pg_backend_pid()
                      AND wait_event_type='Lock' AND query LIKE '%pg_advisory_xact_lock%'
                    """).SingleAsync(deadline.Token)) > 0;
                deadline.Token.ThrowIfCancellationRequested(); if (!waiting) await Task.Yield();
            }
            await Flag(f, false); Assert.False(patch.IsCompleted);
            before = State(f); // The flag's own audit is legitimate; measure only the refused PATCH afterward.
        }
        finally { release.TrySetResult(); }
        await holder;
        Assert.Equal(HttpStatusCode.NotFound, (await patch).StatusCode); Assert.Equal(before, State(f));
    }

    [Fact]
    public async Task Lock_requires_originating_read_committed_transaction_and_exact_canonical_key()
    {
        using var f = new HubFactory(); var d = new TestData(f); var user = d.User(TestData.Alex);
        Assert.Equal("notice-recipient:" + user.ToString("D").ToLowerInvariant(), NoticeRecipientLock.Key(user));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => { await NoticeRecipientLock.Acquire(db, user); return 0; }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
            await NoticeRecipientLock.Acquire(db, user); return 0;
        }));
        await f.DbAsync(db => Tx.Run(db, async () => { await NoticeRecipientLock.AcquireMany(db, [d.User(TestData.Jill), user, user]); return 0; }));
    }
}
