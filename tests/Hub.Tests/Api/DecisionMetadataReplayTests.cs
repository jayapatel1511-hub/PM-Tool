using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class DecisionMetadataReplayTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public DecisionMetadataReplayTests() { d = new(f); f.Clock.Now = new(2026, 11, 14, 14, 0, 0, TimeSpan.Zero); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool value) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value })).Json();
    async Task<HttpResponseMessage> Send(string who, string path, object body, string key, Encoding encoding)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(JsonSerializer.Serialize(body), encoding, "application/json") };
        request.Headers.Add("Idempotency-Key", key); return await f.As(who).SendAsync(request);
    }
    object Body(Guid owner, bool feature) => feature ? new { subject = "Synthetic UTF café", description = "Synthetic context", ownerUserId = owner, dateRequested = "2026-11-01", requiredByDate = "2026-11-20", impactLevel = "Low", impactDescription = "Synthetic impact", optionsConsidered = Array.Empty<string>() }
        : new { subject = "Synthetic legacy", description = "Synthetic context", ownerUserId = owner, dateRequested = "2026-11-01", requiredByDate = "2026-11-20", impactLevel = "Low", impactDescription = "Synthetic impact" };

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Feature_create_charset_replay_rechecks_flag_and_current_authority_without_creating_twice(bool utf16)
    {
        await Flag(true); var p = await d.Project(); var path = $"/api/v1/projects/{p.Id}/decisions"; var key = Guid.NewGuid().ToString(); var body = Body(d.User(TestData.Alex), true); var encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
        var first = await (await Send(TestData.Alex, path, body, key, encoding)).Json(201);
        var count = f.Db(db => db.ActivityLog.Count()); var replay = await Send(TestData.Alex, path, body, key, encoding);
        Assert.True(replay.Headers.Contains("Idempotent-Replayed")); Assert.Equal(first.ToJsonString(), (await replay.Json(201)).ToJsonString()); Assert.Equal(count, f.Db(db => db.ActivityLog.Count()));
        await Flag(false); await (await Send(TestData.Alex, path, body, key, encoding)).Json(404); await Flag(true);
        await f.DbAsync(async db => { var member = await db.ProjectMembers.SingleAsync(row => row.ProjectId == p.Id && row.UserId == d.User(TestData.Alex) && row.RemovedAt == null); member.RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        await (await Send(TestData.Alex, path, body, key, encoding)).Json(403); Assert.Equal(1, f.Db(db => db.Decisions.Count(row => row.ProjectId == p.Id)));
    }

    [Fact]
    public async Task Successful_feature_transition_replays_original_stale_version_but_not_after_flag_or_owner_revocation()
    {
        await Flag(true); var p = await d.Project(); var created = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/decisions", Body(d.User(TestData.Alex), false))).Json(201); var id = created.G("id");
        var path = $"/api/v1/decisions/{id}/transition"; var key = Guid.NewGuid().ToString(); var body = new { toStatus = "Decided", decisionText = "Synthetic outcome", decisionDate = "2026-11-11", rationale = "Synthetic rationale", rowVersion = created.I("rowVersion") };
        var first = await (await Send(TestData.Alex, path, body, key, Encoding.Unicode)).Json(); var count = f.Db(db => db.ActivityLog.Count());
        var replay = await Send(TestData.Alex, path, body, key, Encoding.Unicode); Assert.True(replay.Headers.Contains("Idempotent-Replayed")); Assert.Equal(first.ToJsonString(), (await replay.Json()).ToJsonString()); Assert.Equal(count, f.Db(db => db.ActivityLog.Count()));
        await Flag(false); await (await Send(TestData.Alex, path, body, key, Encoding.Unicode)).Json(404); await Flag(true);
        await f.DbAsync(async db => { var row = await db.Decisions.SingleAsync(row => row.Id == id); row.OwnerUserId = d.User(TestData.Jill); return await db.SaveChangesAsync(); });
        await (await Send(TestData.Alex, path, body, key, Encoding.Unicode)).Json(403);
    }

    [Fact]
    public async Task Explicit_null_legacy_create_replays_after_enable_without_backfilling_new_metadata()
    {
        await Flag(false); var p = await d.Project(); var path = $"/api/v1/projects/{p.Id}/decisions"; var key = Guid.NewGuid().ToString();
        var body = JsonSerializer.SerializeToNode(Body(d.User(TestData.Alex), false))!.AsObject(); body["categoryId"] = null; body["optionsConsidered"] = null;
        var first = await (await Send(TestData.Pm, path, body, key, Encoding.UTF8)).Json(201); await Flag(true);
        var replay = await Send(TestData.Pm, path, body, key, Encoding.UTF8); Assert.True(replay.Headers.Contains("Idempotent-Replayed")); Assert.Equal(first.ToJsonString(), (await replay.Json(201)).ToJsonString());
        var id = first.G("id"); Assert.Null(f.Db(db => db.Decisions.Single(row => row.Id == id).CategoryId)); Assert.Null(f.Db(db => db.Decisions.Single(row => row.Id == id).OptionsConsidered)); Assert.Null(f.Db(db => db.Decisions.Single(row => row.Id == id).Rationale));
    }
}
