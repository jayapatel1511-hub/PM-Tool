using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.Features;

namespace Hub.Api.Features;

/// Open streams per user, for the cap of FR-LIVE-17.
public sealed class LiveStreams
{
    readonly ConcurrentDictionary<Guid, int> open = new();

    public bool TryOpen(Guid user, int cap)
    {
        if (open.AddOrUpdate(user, 1, (_, n) => n + 1) <= cap) return true;
        Close(user);
        return false;
    }

    public void Close(Guid user) => open.AddOrUpdate(user, 0, (_, n) => Math.Max(0, n - 1));
}

/// §46.6 change events, behind `flag.live_events`: GET /api/v1/events streams `{topic, projectId, itemType?}` for the projects the
/// viewer may see (FR-LIVE-14, FR-LIVE-16, FR-LIVE-17). Authentication is the API's own, so cookie, bearer and development schemes all work.
public static class EventEndpoints
{
    public const string Flag = "live_events";
    static readonly string[] Recompute = [ItemType.Project, ItemType.Member, ItemType.User];
    static readonly string[] ActorOnly = [ItemType.TimeEntry, ItemType.CalendarEvent];
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static void Map(RouteGroupBuilder api) =>
        api.MapGet("/events", async (HttpContext ctx, Access access, LiveFeed feed, LiveStreams streams, IConfiguration cfg,
            IHostApplicationLifetime host, TimeProvider clock) =>
        {
            var me = access.Me.Id;
            if (!streams.TryOpen(me, cfg.GetValue("Live:StreamsPerUser", 5))) return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            try
            {
                var heartbeat = TimeSpan.FromSeconds(Math.Clamp(cfg.GetValue("Live:HeartbeatSeconds", 25.0), 0.1, 59));
                var life = TimeSpan.FromMinutes(Math.Clamp(cfg.GetValue("Live:LifetimeMinutes", 10.0), 0, 10));
                // A bearer token ends the stream when it expires, if sooner (FR-LIVE-16); `exp` is Unix seconds.
                if (long.TryParse(ctx.User.FindFirst("exp")?.Value, out var exp))
                    life = TimeSpan.FromTicks(Math.Clamp((DateTimeOffset.FromUnixTimeSeconds(exp) - clock.GetUtcNow()).Ticks, 0, life.Ticks));
                using var end = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted, host.ApplicationStopping);
                end.CancelAfter(life);
                var ct = end.Token;

                using var sub = feed.Subscribe();
                var visible = await access.VisibleProjectIdSet();
                ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.Headers["X-Accel-Buffering"] = "no"; // Cache-Control: no-store comes from SecurityHeaders for every /api response
                try
                {
                    await Write(ctx, ": open\n\n", ct);
                    while (true)
                    {
                        using (var beat = CancellationTokenSource.CreateLinkedTokenSource(ct))
                        {
                            beat.CancelAfter(heartbeat);
                            try { if (!await sub.Reader.WaitToReadAsync(beat.Token)) break; } // completed: shutdown or a full channel
                            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { await Write(ctx, ": heartbeat\n\n", ct); continue; }
                        }
                        while (sub.Reader.TryRead(out var batch))
                        {
                            if (batch.Rows.Any(r => Recompute.Contains(r.ItemType))) visible = await access.VisibleProjectIdSet();
                            var text = Frame(batch, visible, me);
                            if (text.Length > 0) await Write(ctx, text, ct);
                        }
                    }
                }
                catch (OperationCanceledException) { } // life limit, client gone or host stopping: end cleanly
                return Results.Empty;
            }
            finally { streams.Close(me); }
        }).RequireFlag(Flag);

    /// The events of one batch this viewer may receive, one per project and item type, as SSE `data:` lines (FR-LIVE-15, FR-LIVE-16).
    public static string Frame(LiveBatch batch, IReadOnlySet<Guid> visible, Guid viewer)
    {
        var sb = new StringBuilder();
        foreach (var (projectId, itemType) in batch.Rows
            .Where(r => visible.Contains(r.ProjectId) && (!ActorOnly.Contains(r.ItemType) || r.ActorUserId == viewer))
            .Select(r => (r.ProjectId, r.ItemType)).Distinct())
            sb.Append("data: ").Append(JsonSerializer.Serialize(new { topic = "project", projectId, itemType }, Web)).Append("\n\n");
        foreach (var projectId in batch.Evaluated.Where(visible.Contains).Distinct())
            sb.Append("data: ").Append(JsonSerializer.Serialize(new { topic = "evaluated", projectId }, Web)).Append("\n\n");
        return sb.ToString();
    }

    static async Task Write(HttpContext ctx, string text, CancellationToken ct)
    {
        await ctx.Response.WriteAsync(text, ct);
        await ctx.Response.Body.FlushAsync(ct);
    }
}
