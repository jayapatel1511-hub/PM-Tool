using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class MyWorkSeenRaceTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Late_committed_assignment_is_not_in_visit_or_MarkAll_cut_even_with_same_source_clock(bool markAll)
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Jill) });
        if (markAll) await s.Visit();
        var written = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producer = s.Factory.DbAsync(async db => {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var row = await db.Tasks.SingleAsync(row => row.Id == task.G("id"));
            row.AssigneeId = s.Data.User(TestData.Alex);
            await db.SaveChangesAsync(); written.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await transaction.CommitAsync(); return 0;
        });
        try
        {
            await written.Task.WaitAsync(TimeSpan.FromSeconds(15));
            if (markAll) await s.MarkAll(); else await s.Visit();
        }
        finally { release.TrySetResult(); }
        await producer;
        Assert.Equal("New", (await s.Read([task.G("id")]))[new("Task", task.G("id"))].State);
        Assert.False(s.Factory.Db(db => db.WorkSeenAcknowledgements.Any(row => row.Token == s.Latest(task.G("id")))));
    }

    [Fact]
    public async Task Canonical_outer_rollback_removes_published_occurrence_and_source_log_together()
    {
        using var s = new WorkSeenTestContext(); var p = await s.Data.Project();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Jill) }); var id = task.G("id");
        var before = s.Factory.Db(db => db.WorkAssignmentOccurrences.Count(row => row.ItemType == "Task" && row.ItemId == id));
        await s.Factory.DbAsync(async db => {
            await using var transaction = await db.Database.BeginTransactionAsync();
            (await db.Tasks.SingleAsync(row => row.Id == id)).AssigneeId = s.Data.User(TestData.Alex);
            await db.SaveChangesAsync();
            Assert.True(await db.WorkAssignmentOccurrences.AnyAsync(row => row.ItemId == id && row.RecipientId == s.Data.User(TestData.Alex)));
            await transaction.RollbackAsync(); return 0;
        });
        Assert.Equal(before, s.Factory.Db(db => db.WorkAssignmentOccurrences.Count(row => row.ItemType == "Task" && row.ItemId == id)));
        Assert.Equal(s.Data.User(TestData.Jill), s.Factory.Db(db => db.Tasks.Single(row => row.Id == id).AssigneeId));
    }

    [Fact]
    public async Task Producers_commit_under_actual_personal_lock_contention_then_competing_commands_replay_once()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var a = await s.Data.Project(); var b = await s.Data.Project(); await s.Visit();
        var tasks = new List<Guid>();
        foreach (var project in new[] { a, b, b, a })
            tasks.Add((await s.Data.NewTask(project.Id, extra: new { assigneeId = s.Data.User(TestData.Jill) })).G("id"));
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var me = s.Data.User(TestData.Alex);
        var holder = s.Factory.DbAsync(async db => {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.Database.SqlQuery<Guid>($"SELECT user_id AS \"Value\" FROM hub.user_setting WHERE user_id={me} FOR UPDATE").ToListAsync();
            locked.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(60));
            await transaction.CommitAsync(); return 0;
        });
        Task<int> Change(Guid[] ids) => s.Factory.DbAsync(async db => {
            foreach (var id in ids) (await db.Tasks.SingleAsync(row => row.Id == id)).AssigneeId = s.Data.User(TestData.Alex);
            return await db.SaveChangesAsync();
        });
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var duplicateRequest = Guid.NewGuid(); var allRequest = Guid.NewGuid();
        var firstVisit = s.Visit(request: duplicateRequest); var duplicateVisit = s.Visit(request: duplicateRequest);
        var markAll = s.MarkAll(request: allRequest);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var waiting = 0;
            while (waiting < 3)
            {
                // Observe actual PostgreSQL lock waiters in this factory's isolated database, not task scheduling.
                waiting = await s.Factory.DbAsync(db => db.Database.SqlQuery<int>($"""
                    SELECT COUNT(*)::int AS "Value" FROM pg_stat_activity
                    WHERE datname=current_database() AND pid<>pg_backend_pid()
                      AND wait_event_type='Lock' AND query LIKE '%user_setting%'
                    """).SingleAsync(deadline.Token));
                if (waiting < 3) await Task.Yield();
                deadline.Token.ThrowIfCancellationRequested();
            }
            await Task.WhenAll(Change([tasks[0], tasks[1]]), Change([tasks[2], tasks[3]])).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.False(firstVisit.IsCompleted); Assert.False(duplicateVisit.IsCompleted); Assert.False(markAll.IsCompleted);
            Assert.Equal(4, s.Factory.Db(db => db.WorkAssignmentOccurrences.Count(row => tasks.Contains(row.ItemId) && row.RecipientId == me)));
        }
        finally { release.TrySetResult(); }
        await holder;
        var replies = await Task.WhenAll(firstVisit, duplicateVisit);
        Assert.Equal(replies[0].ToJsonString(), replies[1].ToJsonString());
        await markAll;
        var states = await s.Read(tasks.ToArray());
        Assert.Equal(4, states.Count); Assert.All(states.Values, value => Assert.Equal("Seen", value.State));
        Assert.Equal(3, s.Factory.Db(db => db.UserSettings.Single(row => row.UserId == me).MyWorkSeenRevision));
        Assert.Equal(1, s.Factory.Db(db => db.WorkSeenCommandReceipts.Count(row => row.UserId == me && row.RequestId == duplicateRequest)));
    }

    [Fact]
    public async Task Preference_writer_and_visit_preserve_separate_fields_without_global_revision_conflict()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); await s.Visit(); var me = s.Data.User(TestData.Alex);
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherWriter = s.Factory.DbAsync(async db => {
            var preferences = await UserPreferences.GetOrCreate(db, me); loaded.SetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
            preferences.DenseRows = false;
            preferences.LastDigestAt = s.Factory.Clock.Now; return await db.SaveChangesAsync();
        });
        try { await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15)); await s.Visit(); }
        finally { release.TrySetResult(); }
        await otherWriter;
        s.Factory.Db(db => {
            var preferences = db.UserSettings.Single(row => row.UserId == me);
            Assert.False(preferences.DenseRows); Assert.Equal(s.Factory.Clock.Now, preferences.LastDigestAt);
            Assert.NotNull(preferences.MyWorkFirstVisitAt); Assert.NotNull(preferences.MyWorkPreviousVisitAt);
            Assert.Equal(2, preferences.MyWorkSeenRevision);
            Assert.False(db.Model.FindEntityType(typeof(UserSetting))!.FindProperty(nameof(UserSetting.MyWorkSeenRevision))!.IsConcurrencyToken);
            Assert.False(typeof(IVersioned).IsAssignableFrom(typeof(UserSetting))); return 0;
        });
    }

    [Fact]
    public async Task Missing_projection_fails_whole_MarkAll_without_acknowledging_other_new_tokens()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var missing = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        var valid = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        var validToken = s.Latest(valid.G("id"));
        // Deliberate storage-corruption fault injection bypasses the application append-only guard.
        await s.Factory.DbAsync(db => db.WorkAssignmentOccurrences.Where(row => row.ItemType == "Task" && row.ItemId == missing.G("id")).ExecuteDeleteAsync());
        await s.MarkAll(status: 409);
        Assert.False(s.Factory.Db(db => db.WorkSeenAcknowledgements.Any(row => row.Token == validToken)));
        Assert.Equal("Unavailable", (await s.Read([missing.G("id")]))[new("Task", missing.G("id"))].State);
        Assert.Equal("New", (await s.Read([valid.G("id")]))[new("Task", valid.G("id"))].State);
    }
}
