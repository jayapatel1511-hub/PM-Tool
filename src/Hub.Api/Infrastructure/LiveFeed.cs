using System.Buffers.Binary;
using System.Threading.Channels;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// One committed activity row with a project. The actor is kept for the visibility rule only and never leaves the server (FR-LIVE-15, FR-LIVE-16).
public sealed record LiveRow(Guid Id, Guid ProjectId, string ItemType, Guid? ActorUserId);
sealed record LiveEval(Guid ProjectId, DateTimeOffset EvaluatedAt);

/// What one read of the feed found: new activity rows and projects evaluated since the last read.
public sealed record LiveBatch(IReadOnlyList<LiveRow> Rows, IReadOnlyList<Guid> Evaluated)
{
    public bool IsEmpty => Rows.Count == 0 && Evaluated.Count == 0;
}

/// Where the feed has read up to. Ids are uuid v7, created from the system clock, so the cursor uses it too (not TimeProvider).
public sealed class LiveFeedState
{
    public DateTimeOffset Cursor { get; set; } = DateTimeOffset.UtcNow;
    internal readonly Dictionary<Guid, DateTimeOffset> Seen = [];
    internal Dictionary<Guid, DateTimeOffset>? Evaluations;
}

/// A stream's place on the feed: a bounded channel of batches. Disposing it unsubscribes.
public sealed class LiveSubscription(LiveFeed feed, Channel<LiveBatch> channel) : IDisposable
{
    internal Channel<LiveBatch> Channel => channel;
    public ChannelReader<LiveBatch> Reader => channel.Reader;
    public void Dispose() => feed.Unsubscribe(this);
}

/// §46.6 change feed: reads committed activity_log rows by uuid v7 key range and project_state.evaluated_at, and fans them out to
/// subscribed streams. It reads only while a stream is subscribed, writes nothing and never reads the outbox (FR-LIVE-15).
public sealed class LiveFeed(IServiceProvider root, IConfiguration cfg, ILogger<LiveFeed> log) : BackgroundService
{
    const int ChannelCapacity = 64;
    readonly Lock gate = new();
    readonly List<LiveSubscription> subs = [];

    public LiveSubscription Subscribe()
    {
        var sub = new LiveSubscription(this, Channel.CreateBounded<LiveBatch>(new BoundedChannelOptions(ChannelCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait }));
        lock (gate) subs.Add(sub);
        return sub;
    }

    internal void Unsubscribe(LiveSubscription sub)
    {
        lock (gate) subs.Remove(sub);
        sub.Channel.Writer.TryComplete();
    }

    public int Subscribers { get { lock (gate) return subs.Count; } }

    /// Hands a batch to every subscriber. A full channel closes that stream; the client's reconnect refresh covers the gap.
    public void Publish(LiveBatch batch)
    {
        LiveSubscription[] targets;
        lock (gate) targets = [.. subs];
        foreach (var s in targets)
            if (!s.Channel.Writer.TryWrite(batch)) Unsubscribe(s);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var read = TimeSpan.FromMilliseconds(cfg.GetValue("Live:ReadMs", 1000));
        var overlap = TimeSpan.FromSeconds(cfg.GetValue("Live:OverlapSeconds", 3));
        LiveFeedState? state = null;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(read, ct);
                if (Subscribers == 0) { state = null; continue; } // runs only while a stream is open
                state ??= new LiveFeedState();
                using var scope = root.CreateScope();
                var batch = await TickOnce(scope.ServiceProvider.GetRequiredService<HubDb>(), state, overlap, ct);
                if (!batch.IsEmpty) Publish(batch);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e) { log.LogError(e, "Live feed read failed"); }
        }
        lock (gate) foreach (var s in subs) s.Channel.Writer.TryComplete(); // streams end cleanly on shutdown
    }

    /// One read. Activity rows from the cursor minus the overlap, de-duplicated by id, so a row that commits late within the overlap
    /// is still sent exactly once; then every project's evaluation time, sending projects whose time moved since the last read.
    /// The first read only records evaluation times.
    public static async Task<LiveBatch> TickOnce(HubDb db, LiveFeedState state, TimeSpan overlap, CancellationToken ct = default)
    {
        var lower = state.Cursor - overlap;
        var from = LowerBound(lower);
        var found = await db.Database.SqlQuery<LiveRow>($"""
            SELECT id AS "Id", project_id AS "ProjectId", item_type AS "ItemType", actor_user_id AS "ActorUserId"
            FROM hub.activity_log WHERE id >= {from} AND project_id IS NOT NULL ORDER BY id
            """).ToListAsync(ct);
        var rows = new List<LiveRow>();
        foreach (var r in found)
        {
            var at = Timestamp(r.Id);
            if (at > state.Cursor) state.Cursor = at;
            if (state.Seen.TryAdd(r.Id, at)) rows.Add(r);
        }
        var floor = Timestamp(LowerBound(state.Cursor - overlap)); // the next read's bound, floored to the millisecond like the query
        foreach (var id in state.Seen.Where(x => x.Value < floor).Select(x => x.Key).ToList()) state.Seen.Remove(id);

        // ponytail: reads every project_state row (one per project) each second; filter on evaluated_at if projects reach the thousands.
        var evals = await db.Database.SqlQuery<LiveEval>($"""
            SELECT project_id AS "ProjectId", evaluated_at AS "EvaluatedAt" FROM hub.project_state
            """).ToListAsync(ct);
        var evaluated = new List<Guid>();
        var first = state.Evaluations is null;
        state.Evaluations ??= [];
        foreach (var e in evals)
        {
            if (!first && (!state.Evaluations.TryGetValue(e.ProjectId, out var was) || was != e.EvaluatedAt)) evaluated.Add(e.ProjectId);
            state.Evaluations[e.ProjectId] = e.EvaluatedAt;
        }
        return new LiveBatch(rows, evaluated);
    }

    /// The smallest uuid v7 for a time: the 48-bit Unix milliseconds big-endian, the rest zero. PostgreSQL compares uuids byte by byte.
    public static Guid LowerBound(DateTimeOffset at)
    {
        Span<byte> b = stackalloc byte[16];
        BinaryPrimitives.WriteInt64BigEndian(b, Math.Max(0, at.ToUnixTimeMilliseconds()) << 16);
        return new Guid(b, bigEndian: true);
    }

    public static DateTimeOffset Timestamp(Guid v7)
    {
        Span<byte> b = stackalloc byte[16];
        v7.TryWriteBytes(b, bigEndian: true, out _);
        return DateTimeOffset.FromUnixTimeMilliseconds(BinaryPrimitives.ReadInt64BigEndian(b) >>> 16);
    }
}
