using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hub.Tests.Api;

/// The host for these tests: the feed never reads on its own (batches are published by the test) and the heartbeat is 1 second.
public sealed class LiveEventsHost : IDisposable
{
    public HubFactory F { get; } = new() { Settings = { ["Live:ReadMs"] = "3600000", ["Live:HeartbeatSeconds"] = "1" } };
    public void Dispose() => F.Dispose();
}

/// Packet 042 S10: GET /api/v1/events (FR-LIVE-14, FR-LIVE-16, FR-LIVE-17; AC-LIVE-14, AC-LIVE-15, AC-LIVE-16).
public sealed class LiveEventsTests(LiveEventsHost host) : IClassFixture<LiveEventsHost>
{
    readonly HubFactory f = host.F;
    TestData d => new(f);

    /// One open stream: reads lines, keeping `data:` payloads and counting heartbeats.
    sealed class Live(HttpResponseMessage response, StreamReader reader) : IDisposable
    {
        public HttpResponseMessage Response => response;

        public async Task<string?> Line(TimeSpan timeout)
        {
            return await reader.ReadLineAsync().WaitAsync(timeout);
        }

        /// Payloads up to and including the first one `stop` matches.
        public async Task<List<JsonObject>> Until(Func<JsonObject, bool> stop)
        {
            var got = new List<JsonObject>();
            var deadline = Stopwatch.StartNew();
            while (await Line(TimeSpan.FromSeconds(10)) is { } line)
            {
                if (deadline.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("no end marker; got " + string.Join(", ", got));
                if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;
                var o = JsonNode.Parse(line[6..])!.AsObject();
                if (stop(o)) return got;
                got.Add(o);
            }
            throw new InvalidOperationException("stream ended");
        }

        public void Dispose() { reader.Dispose(); response.Dispose(); }
    }

    static async Task<HttpResponseMessage> Request(HubFactory hub, string email) =>
        await hub.As(email).SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/v1/events"), HttpCompletionOption.ResponseHeadersRead);

    /// Opens a stream and waits for its opening comment, so it is subscribed before the test publishes.
    static async Task<Live> Open(HubFactory hub, string email)
    {
        var r = await Request(hub, email);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var live = new Live(r, new StreamReader(await r.Content.ReadAsStreamAsync()));
        Assert.Equal(": open", await live.Line(TimeSpan.FromSeconds(10)));
        return live;
    }

    static async Task SetFlag(HubFactory hub, bool on) =>
        await hub.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{Hub.Api.Features.EventEndpoints.Flag}", new { value = on }).Result.Json();

    static LiveRow Row(Guid projectId, string itemType, Guid actor) => new(Guid.CreateVersion7(), projectId, itemType, actor);

    [Fact]
    [Trait("flag-off", "live_events")]
    public async Task Flag_off_answers_404_and_opens_nothing() // FR-LIVE-14, AC-LIVE-15
    {
        await SetFlag(f, false);
        using var r = await Request(f, TestData.Alex);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal(0, f.Services.GetRequiredService<LiveFeed>().Subscribers);
    }

    [Fact]
    [Trait("flag-on", "live_events")]
    public async Task Stream_authenticates_by_header_without_a_cookie_and_is_unbuffered() // FR-LIVE-14, FR-LIVE-17, AC-LIVE-15
    {
        await SetFlag(f, true);
        using var live = await Open(f, TestData.Alex);
        var req = live.Response.RequestMessage!;
        Assert.False(req.Headers.Contains("Cookie"));
        Assert.True(req.Headers.Contains("X-Dev-User")); // the header-based test scheme
        Assert.Equal("/api/v1/events", req.RequestUri!.PathAndQuery); // nothing in the URL
        Assert.Equal("text/event-stream", live.Response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("no-store", live.Response.Headers.CacheControl!.ToString());
        Assert.Equal("no", live.Response.Headers.GetValues("X-Accel-Buffering").Single());
        Assert.Empty(live.Response.Content.Headers.ContentEncoding);
    }

    [Fact]
    [Trait("flag-on", "live_events")]
    public async Task Each_viewer_receives_only_what_the_visibility_rule_allows() // FR-LIVE-15, FR-LIVE-16, AC-LIVE-14
    {
        await SetFlag(f, true);
        var data = d;
        var r = await data.Project();     // PM priya, alex a member
        var open = await data.Project();  // open to everyone
        var diane = data.User(TestData.Diane);
        await f.As(TestData.Pm).Post($"/api/v1/projects/{r.Id}/members", new { userId = diane, roles = new[] { ProjectRole.Viewer } }).Result.Json(201);
        await f.DbAsync(async db => { (await db.Projects.FindAsync(r.Id))!.Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        var marc = data.User(TestData.Marc);
        var alex = data.User(TestData.Alex);

        string[] viewers = [TestData.Admin, TestData.Lena, TestData.Pm, TestData.Alex, TestData.Jill, TestData.Rita, TestData.Diane];
        var streams = new Dictionary<string, Live>();
        foreach (var v in viewers) streams[v] = await Open(f, v);
        try
        {
            var feed = f.Services.GetRequiredService<LiveFeed>();
            feed.Publish(new LiveBatch([Row(r.Id, ItemType.Task, marc), Row(r.Id, "OpenRole", marc), Row(r.Id, ItemType.Task, marc),
                Row(open.Id, ItemType.Task, marc), Row(open.Id, ItemType.TimeEntry, alex), Row(open.Id, ItemType.CalendarEvent, alex),
                Row(open.Id, ItemType.TimeEntry, marc)], [r.Id]));
            // Diane is removed from R while connected: her set is recomputed on the Member row, so nothing more for R reaches her.
            await f.DbAsync(async db => { db.ProjectMembers.Single(m => m.ProjectId == r.Id && m.UserId == diane).RemovedAt = DateTimeOffset.UtcNow; return await db.SaveChangesAsync(); });
            feed.Publish(new LiveBatch([Row(r.Id, ItemType.Member, marc)], []));
            feed.Publish(new LiveBatch([Row(r.Id, ItemType.Risk, marc)], [r.Id]));
            feed.Publish(new LiveBatch([], [open.Id])); // end marker every viewer may see

            string E(JsonObject o) => $"{o["topic"]}|{(o["projectId"]!.GetValue<Guid>() == r.Id ? "R" : "O")}|{o["itemType"]}";
            string[] inR = ["project|R|Task", "project|R|OpenRole", "evaluated|R|", "project|R|Member", "project|R|Risk", "evaluated|R|"];
            string[] openAll = ["project|O|Task"];
            var expected = new Dictionary<string, string[]>
            {
                [TestData.Admin] = [.. inR, .. openAll], [TestData.Lena] = [.. inR, .. openAll], [TestData.Pm] = [.. inR, .. openAll],
                [TestData.Alex] = [.. inR, .. openAll, "project|O|TimeEntry", "project|O|CalendarEvent"],
                [TestData.Jill] = openAll, [TestData.Rita] = openAll,
                [TestData.Diane] = ["project|R|Task", "project|R|OpenRole", "evaluated|R|", .. openAll],
            };
            foreach (var v in viewers)
            {
                var got = await streams[v].Until(o => (string?)o["topic"] == "evaluated" && o["projectId"]!.GetValue<Guid>() == open.Id);
                Assert.Equal(expected[v].Order(), got.Select(E).Order());
                Assert.All(got, o => Assert.Equal((string)o["topic"]! == "project" ? ["topic", "projectId", "itemType"] : ["topic", "projectId"], o.Select(p => p.Key)));
            }
        }
        finally { foreach (var s in streams.Values) s.Dispose(); }
    }

    [Fact]
    [Trait("flag-on", "live_events")]
    public async Task A_real_write_reaches_a_member_through_the_feed() // FR-LIVE-15, AC-LIVE-14
    {
        await SetFlag(f, true);
        var data = d;
        var r = await data.Project();
        var marker = await data.Project(); // visible to both when the streams open
        await f.DbAsync(async db => { (await db.Projects.FindAsync(r.Id))!.Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        using var member = await Open(f, TestData.Alex);
        using var outsider = await Open(f, TestData.Jill);
        var state = new LiveFeedState();
        await data.NewTask(r.Id);
        var feed = f.Services.GetRequiredService<LiveFeed>();
        using (var scope = f.Services.CreateScope())
            feed.Publish(await LiveFeed.TickOnce(scope.ServiceProvider.GetRequiredService<Hub.Api.Data.HubDb>(), state, TimeSpan.FromSeconds(3)));
        feed.Publish(new LiveBatch([], [marker.Id]));
        bool End(JsonObject o) => (string?)o["topic"] == "evaluated" && o["projectId"]!.GetValue<Guid>() == marker.Id;
        Assert.Contains(await member.Until(End), o => (string?)o["itemType"] == ItemType.Task && o["projectId"]!.GetValue<Guid>() == r.Id);
        Assert.DoesNotContain(await outsider.Until(End), o => o["projectId"]!.GetValue<Guid>() == r.Id);
    }

    [Fact]
    [Trait("flag-on", "live_events")]
    public async Task Heartbeat_arrives_on_the_configured_interval() // FR-LIVE-17, AC-LIVE-16
    {
        await SetFlag(f, true);
        using var live = await Open(f, TestData.Alex);
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 3; i++)
        {
            string? line;
            do line = await live.Line(TimeSpan.FromSeconds(3)); while (line == "");
            Assert.Equal(": heartbeat", line);
        }
        Assert.InRange(watch.Elapsed.TotalSeconds, 2.5, 6);
    }

    [Fact]
    [Trait("flag-on", "live_events")]
    public async Task A_sixth_stream_gets_429_and_a_closed_one_frees_its_place() // FR-LIVE-17, AC-LIVE-16
    {
        await SetFlag(f, true);
        var five = new List<Live>();
        try
        {
            for (var i = 0; i < 5; i++) five.Add(await Open(f, TestData.Sam));
            using (var sixth = await Request(f, TestData.Sam)) Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
            using (var other = await Open(f, TestData.Omar)) { } // the cap is per user
            five[0].Dispose();
            five.RemoveAt(0);
            var deadline = Stopwatch.StartNew();
            HttpStatusCode status;
            do
            {
                using var again = await Request(f, TestData.Sam);
                status = again.StatusCode;
                if (status != HttpStatusCode.OK) await Task.Delay(100);
            } while (status != HttpStatusCode.OK && deadline.Elapsed < TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.OK, status);
        }
        finally { foreach (var s in five) s.Dispose(); }
    }

    [Fact]
    [Trait("flag-on", "live_events")]
    public async Task Stream_ends_at_its_life_limit() // FR-LIVE-16, AC-LIVE-16
    {
        using var hub = new HubFactory { Settings = { ["Live:LifetimeMinutes"] = "0.05", ["Live:HeartbeatSeconds"] = "1" } }; // 3 seconds
        await SetFlag(hub, true);
        using var live = await Open(hub, TestData.Alex);
        var watch = Stopwatch.StartNew();
        while (await live.Line(TimeSpan.FromSeconds(15)) is not null) { }
        Assert.InRange(watch.Elapsed.TotalSeconds, 1.5, 10);
    }

    [Fact]
    [Trait("flag-on", "live_events")]
    public async Task Streams_end_when_the_host_stops_and_it_does_not_wait_on_them() // FR-LIVE-17, AC-LIVE-16
    {
        var hub = new HubFactory();
        try
        {
            await SetFlag(hub, true);
            using var live = await Open(hub, TestData.Alex);
            var watch = Stopwatch.StartNew();
            hub.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            while (await live.Line(TimeSpan.FromSeconds(10)) is not null) { }
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"stream ended after {watch.Elapsed}");
        }
        finally
        {
            var watch = Stopwatch.StartNew();
            hub.Dispose();
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), $"host stop took {watch.Elapsed}");
        }
    }
}
