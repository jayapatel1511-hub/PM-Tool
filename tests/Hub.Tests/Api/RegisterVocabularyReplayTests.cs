using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// New guarded vocabulary routes recheck current access before replaying a historical receipt.
public sealed class RegisterVocabularyReplayTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public RegisterVocabularyReplayTests() => data = new(f);
    public void Dispose() => f.Dispose();

    [Theory]
    [InlineData("issuePurposes")]
    [InlineData("issueReturnCodes")]
    public async Task Replay_retains_the_original_result_but_cannot_bypass_disabled_or_revoked_access(string kind)
    {
        async Task Flag(bool value) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value })).EnsureSuccessStatusCode();
        var key = Guid.NewGuid().ToString("N"); var freshKey = Guid.NewGuid().ToString("N");
        var body = new Dictionary<string, object> { ["name"] = "Synthetic replay vocabulary " + key };
        if (kind == "issueReturnCodes") body["code"] = "SYNTH";
        async Task<HttpResponseMessage> Create(string receipt)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/{kind}") { Content = JsonContent.Create(body) };
            request.Headers.Add("Idempotency-Key", receipt);
            return await f.As(TestData.Admin).SendAsync(request);
        }
        await Flag(true);
        using var first = await Create(key); Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var bytes = await first.Content.ReadAsByteArrayAsync(); var id = JsonNode.Parse(bytes)!.G("id");
        var rows = await f.DbAsync(db => kind == "issuePurposes" ? db.IssuePurposes.CountAsync(row => row.Id == id) : db.IssueReturnCodes.CountAsync(row => row.Id == id));
        var events = await f.DbAsync(db => db.ActivityLog.CountAsync(row => row.ItemId == id));
        Assert.Equal(1, rows); Assert.Equal(1, events);
        var receiptId = await f.DbAsync(db => db.Idempotency.Where(row => row.Key == key).Select(row => row.Id).SingleAsync());
        using var replay = await Create(key); Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(bytes, await replay.Content.ReadAsByteArrayAsync());
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));

        await Flag(false);
        using var disabled = await Create(key); Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        using var freshDisabled = await Create(freshKey); Assert.Equal(HttpStatusCode.NotFound, freshDisabled.StatusCode);
        Assert.False(await f.DbAsync(db => db.Idempotency.AnyAsync(row => row.Key == freshKey)));
        await Flag(true);
        // A different Admin performs the real role removal; no development-header role shortcut.
        (await f.As(TestData.Admin).Post($"/api/v1/admin/users/{data.User(TestData.Lena)}/roles", new { role = "Admin" })).EnsureSuccessStatusCode();
        (await f.As(TestData.Lena).DeleteAsync($"/api/v1/admin/users/{data.User(TestData.Admin)}/roles/Admin")).EnsureSuccessStatusCode();
        using var revoked = await Create(key); Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
        using var freshRevoked = await Create(freshKey); Assert.Equal(HttpStatusCode.Forbidden, freshRevoked.StatusCode);
        Assert.False(await f.DbAsync(db => db.Idempotency.AnyAsync(row => row.Key == freshKey)));
        Assert.Equal(receiptId, await f.DbAsync(db => db.Idempotency.Where(row => row.Key == key).Select(row => row.Id).SingleAsync()));
        Assert.Equal(bytes, await f.DbAsync(db => db.Idempotency.Where(row => row.Key == key).Select(row => row.Body).SingleAsync()));
        Assert.Equal(events, await f.DbAsync(db => db.ActivityLog.CountAsync(row => row.ItemId == id)));
        Assert.Equal(rows, await f.DbAsync(db => kind == "issuePurposes" ? db.IssuePurposes.CountAsync(row => row.Id == id) : db.IssueReturnCodes.CountAsync(row => row.Id == id)));
    }
}
