using System.Data.Common;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Hub.Tests.Api;

/// Packet 042 S09: the change feed (FR-LIVE-15, AC-LIVE-19).
[Collection("api")]
public sealed class LiveFeedTests(HubFactory f)
{
    readonly TestData d = new(f);
    static readonly TimeSpan Overlap = TimeSpan.FromSeconds(3);
    /// Touching Services starts the host, which creates and migrates the test database.
    string Cs => f.Services is null ? "" : $"Host=localhost;Port=55432;Database={f.Database};Username=hub";

    sealed class Capture : DbCommandInterceptor
    {
        public readonly List<string> Sql = [];
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { Sql.Add(c.CommandText); return r; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken ct = default) { Sql.Add(c.CommandText); return ValueTask.FromResult(r); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken ct = default) { Sql.Add(c.CommandText); return ValueTask.FromResult(r); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<object> r, CancellationToken ct = default) { Sql.Add(c.CommandText); return ValueTask.FromResult(r); }
    }

    /// Ticks with a context that records every command it sends.
    async Task<LiveBatch> Tick(LiveFeedState state, Capture? cap = null)
    {
        using var scope = f.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var opts = new DbContextOptionsBuilder<HubDb>(sp.GetRequiredService<DbContextOptions<HubDb>>()).AddInterceptors(cap ?? new Capture()).Options;
        await using var db = new HubDb(opts, sp.GetRequiredService<AuditContext>(), sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<EvaluationSignal>(), sp.GetRequiredService<SettingsStore>(), sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>());
        return await LiveFeed.TickOnce(db, state, Overlap);
    }

    static async Task Insert(NpgsqlConnection c, NpgsqlTransaction? tx, Guid id, Guid projectId, string itemType = "Task")
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO hub.activity_log (id, occurred_at, actor_type, project_id, item_type, item_id, action, categories, changes, source)
            VALUES (@id, now(), 'User', @p, @t, gen_random_uuid(), 'Updated', '{}', '[]', 'API')
            """, c, tx);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("p", projectId);
        cmd.Parameters.AddWithValue("t", itemType);
        await cmd.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task PostgreSQL_orders_version7_ids_as_created() // AC-LIVE-19 (D11)
    {
        var start = DateTimeOffset.UtcNow.AddDays(-400);
        var ids = new List<Guid>();
        for (var i = 0; i < 400; i++) ids.Add(Guid.CreateVersion7(start.AddMilliseconds(i * 977L + i * i * 131L))); // crosses many byte boundaries
        for (var i = 0; i < 20; i++) { ids.Add(Guid.CreateVersion7()); await Task.Delay(2); }
        await using var c = new NpgsqlConnection(Cs);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT x FROM unnest(@ids) AS x ORDER BY x", c);
        cmd.Parameters.AddWithValue("ids", ids.OrderBy(_ => Random.Shared.Next()).ToArray());
        var ordered = new List<Guid>();
        await using (var r = await cmd.ExecuteReaderAsync()) while (await r.ReadAsync()) ordered.Add(r.GetGuid(0));
        Assert.Equal(ids, ordered);
        Assert.Equal(LiveFeed.Timestamp(ids[7]), LiveFeed.Timestamp(LiveFeed.LowerBound(LiveFeed.Timestamp(ids[7]))));
        Assert.Equal(start.AddMilliseconds(977 + 131).ToUnixTimeMilliseconds(), LiveFeed.Timestamp(ids[1]).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task Late_committing_row_is_delivered_exactly_once() // FR-LIVE-15, AC-LIVE-19
    {
        var state = new LiveFeedState();
        var pid = Guid.NewGuid();
        await using var late = new NpgsqlConnection(Cs);
        await late.OpenAsync();
        await using var tx = await late.BeginTransactionAsync();
        var early = Guid.CreateVersion7();
        await Insert(late, tx, early, pid);
        await Task.Delay(5);
        var later = Guid.CreateVersion7();
        var noProject = Guid.CreateVersion7();
        await using (var c = new NpgsqlConnection(Cs))
        {
            await c.OpenAsync();
            await Insert(c, null, later, pid, "Milestone");
            await using var none = new NpgsqlCommand("INSERT INTO hub.activity_log (id, occurred_at, actor_type, item_type, item_id, action, categories, changes, source) VALUES (@id, now(), 'User', 'Setting', gen_random_uuid(), 'Updated', '{}', '[]', 'API')", c);
            none.Parameters.AddWithValue("id", noProject);
            await none.ExecuteNonQueryAsync();
        }

        var first = await Tick(state);
        Assert.Equal([later], first.Rows.Where(r => r.ProjectId == pid).Select(r => r.Id));
        Assert.Equal("Milestone", first.Rows.Single(r => r.Id == later).ItemType);
        Assert.DoesNotContain(first.Rows, r => r.Id == noProject); // rows without a project are never streamed

        await tx.CommitAsync();
        var second = await Tick(state);
        Assert.Equal([early], second.Rows.Where(r => r.ProjectId == pid).Select(r => r.Id));
        var third = await Tick(state);
        Assert.DoesNotContain(third.Rows, r => r.ProjectId == pid);
    }

    [Fact]
    public async Task Row_in_the_boundary_millisecond_is_delivered_once() // FR-LIVE-15, AC-LIVE-19
    {
        var state = new LiveFeedState { Cursor = DateTimeOffset.UtcNow.AddTicks(5_000) }; // sub-millisecond part, as UtcNow carries
        var id = Guid.CreateVersion7(LiveFeed.Timestamp(LiveFeed.LowerBound(state.Cursor - Overlap)));
        var pid = Guid.NewGuid();
        await using (var c = new NpgsqlConnection(Cs)) { await c.OpenAsync(); await Insert(c, null, id, pid); }
        var delivered = 0;
        for (var i = 0; i < 3; i++) delivered += (await Tick(state)).Rows.Count(r => r.Id == id);
        Assert.Equal(1, delivered);
    }

    [Fact]
    public async Task Rows_written_before_the_overlap_are_not_read()
    {
        var pid = Guid.NewGuid();
        await using (var c = new NpgsqlConnection(Cs)) { await c.OpenAsync(); await Insert(c, null, Guid.CreateVersion7(DateTimeOffset.UtcNow.AddSeconds(-10)), pid); }
        var batch = await Tick(new LiveFeedState());
        Assert.DoesNotContain(batch.Rows, r => r.ProjectId == pid);
    }

    [Fact]
    public async Task Evaluations_are_delivered_once_per_evaluation() // FR-LIVE-15
    {
        var p = await d.Project();
        var pid = p.Id;
        var saved = f.Clock.Now;
        try
        {
            await f.Evaluate(pid);
            var state = new LiveFeedState();
            Assert.Empty((await Tick(state)).Evaluated); // first read records times only
            f.Clock.Now = f.Clock.Now.AddMinutes(1);
            await f.Evaluate(pid);
            Assert.Contains(pid, (await Tick(state)).Evaluated);
            Assert.DoesNotContain(pid, (await Tick(state)).Evaluated);
            f.Clock.Now = f.Clock.Now.AddMinutes(1);
            await f.Evaluate(pid);
            Assert.Equal(1, (await Tick(state)).Evaluated.Count(x => x == pid));
        }
        finally { f.Clock.Now = saved; }
    }

    [Fact]
    public async Task Feed_writes_nothing_and_never_reads_the_outbox() // FR-LIVE-15, AC-LIVE-19
    {
        var p = await d.Project();
        await d.NewTask(p.Id, TestData.Pm);
        var before = await f.DbAsync(async db => (await db.ActivityLog.CountAsync(), await db.Outbox.CountAsync(), await db.Outbox.CountAsync(o => o.ProcessedAt != null)));
        var cap = new Capture();
        var state = new LiveFeedState { Cursor = DateTimeOffset.UtcNow.AddMinutes(-1) };
        await Tick(state, cap);
        await Tick(state, cap);
        Assert.NotEmpty(cap.Sql);
        Assert.All(cap.Sql, sql =>
        {
            Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("outbox", sql, StringComparison.OrdinalIgnoreCase);
        });
        var after = await f.DbAsync(async db => (await db.ActivityLog.CountAsync(), await db.Outbox.CountAsync(), await db.Outbox.CountAsync(o => o.ProcessedAt != null)));
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Subscribers_receive_batches_and_a_full_channel_closes_its_stream()
    {
        var feed = f.Services.GetRequiredService<LiveFeed>();
        var n = feed.Subscribers;
        using var sub = feed.Subscribe();
        var batch = new LiveBatch([new LiveRow(Guid.CreateVersion7(), Guid.NewGuid(), "Task", null)], []);
        feed.Publish(batch);
        Assert.True(sub.Reader.TryRead(out var got));
        Assert.Same(batch, got);
        for (var i = 0; i < 100; i++) feed.Publish(batch);
        Assert.Equal(n, feed.Subscribers);
        var read = 0;
        await foreach (var _ in sub.Reader.ReadAllAsync()) read++; // ends: the channel was completed on overflow
        Assert.True(read is > 0 and <= 64);
    }
}
