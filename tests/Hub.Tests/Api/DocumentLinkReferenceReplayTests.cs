using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// AC-SCR-77 / AC-SCR-84: feature-bearing retries cannot bypass today's flag or canonical project authority.
public sealed class DocumentLinkReferenceReplayTests
{
    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    public async Task Reference_receipt_replays_exactly_but_refuses_disabled_archived_and_deleted_owner_without_claiming(string encoding)
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New();
        var key = Guid.NewGuid().ToString("N"); var fresh = Guid.NewGuid().ToString("N");
        // Pascal-case input also exercises the web binder's case-insensitive property semantics.
        var body = new { Url = DocumentLinkReferenceFixture.Url, Reference = new DocumentLinkReferenceInput("Manual register", "001", "C") };
        async Task<HttpResponseMessage> Create(string receipt)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, t.AddPath(s)) {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.GetEncoding(encoding), "application/json") };
            request.Headers.Add("Idempotency-Key", receipt); return await t.F.As(TestData.Alex).SendAsync(request);
        }
        using var first = await Create(key); Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var bytes = await first.Content.ReadAsByteArrayAsync(); var id = JsonNode.Parse(bytes)!.G("id");
        var stored = t.Stored(id); var state = t.State(s.Project.Id);
        using var replay = await Create(key); Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(bytes, await replay.Content.ReadAsByteArrayAsync());
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(state, t.State(s.Project.Id)); Assert.Equal("C", stored.DeclaredRevision);
        await t.Flag(false);
        using var disabled = await Create(key); Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        using var freshDisabled = await Create(fresh); Assert.Equal(HttpStatusCode.NotFound, freshDisabled.StatusCode);
        Assert.False(t.F.Db(db => db.Idempotency.Any(row => row.Key == fresh)));
        Assert.Equal(state, t.State(s.Project.Id));
        await t.Flag(true);
        await t.F.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == s.Project.Id)).Status = ProjectStatus.Archived; return await db.SaveChangesAsync(); });
        state = t.State(s.Project.Id);
        using var archived = await Create(key); Assert.Equal(HttpStatusCode.Forbidden, archived.StatusCode);
        using var freshArchived = await Create(fresh); Assert.Equal(HttpStatusCode.Forbidden, freshArchived.StatusCode);
        Assert.Equal(state, t.State(s.Project.Id));
        await t.F.DbAsync(async db => { (await db.Tasks.SingleAsync(row => row.Id == s.Task)).DeletedAt = t.F.Clock.Now; return await db.SaveChangesAsync(); });
        state = t.State(s.Project.Id);
        using var deleted = await Create(key); Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Equal(state, t.State(s.Project.Id));
        Assert.Equal(bytes, t.F.Db(db => db.Idempotency.Single(row => row.Key == key).Body));
        Assert.False(t.F.Db(db => db.Idempotency.Any(row => row.Key == fresh)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Omitted_and_explicit_null_reference_keep_old_create_and_replay_when_feature_is_off(bool explicitNull)
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(false); var s = await t.New();
        var key = Guid.NewGuid().ToString("N");
        var body = new JsonObject { ["url"] = DocumentLinkReferenceFixture.Url };
        if (explicitNull) body["reference"] = null;
        async Task<HttpResponseMessage> Create()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, t.AddPath(s)) { Content = JsonContent.Create(body) };
            request.Headers.Add("Idempotency-Key", key); return await t.F.As(TestData.Alex).SendAsync(request);
        }
        using var first = await Create(); Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var bytes = await first.Content.ReadAsByteArrayAsync(); var id = JsonNode.Parse(bytes)!.G("id"); var state = t.State(s.Project.Id);
        using var retry = await Create(); Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(bytes, await retry.Content.ReadAsByteArrayAsync()); Assert.Equal(state, t.State(s.Project.Id));
        t.AssertClear(t.Stored(id));
    }
}
