using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;

namespace Hub.Tests.Api;

/// AC-SCR-61 / AC-SCR-84: the extension preserves pre-extension serialized payloads and real receipts.
public sealed class ReviewResponseReceiptTests
{
    // This deliberately models the old six-property record, independently of the extended record's attributes.
    sealed record LegacyFindingAction(Guid RequestId, int RowVersion, string Action, string Reason, string? EvidenceUrl, Guid? OwnerId);
    static readonly string[] LegacyKeys = ["requestId", "rowVersion", "action", "reason", "evidenceUrl", "ownerId"];

    [Theory] // AC-SCR-84: exact pre-extension serialization and canonical envelope hash.
    [InlineData(FindingStatus.Responded, "  Unicode é / 桥 response with raw whitespace  ", null, false)]
    [InlineData(FindingStatus.Responded, "Recorded explanation", "https://example.test/evidence.pdf", false)]
    [InlineData("assignResolver", "Reassign this finding", null, true)]
    [InlineData(FindingStatus.Open, "Return this response for correction", null, false)]
    public void Omitted_and_explicit_null_extension_preserve_exact_old_serialization_and_envelope_hash(string action, string reason, string? evidence, bool hasOwner)
    {
        var id = Guid.NewGuid(); var findingId = Guid.NewGuid(); Guid? owner = hasOwner ? Guid.NewGuid() : null;
        var old = new LegacyFindingAction(Guid.NewGuid(), 17, action, reason, evidence, owner);
        var current = new ReviewEndpoints.FindingAction(old.RequestId, old.RowVersion, old.Action, old.Reason, old.EvidenceUrl, old.OwnerId);
        var oldJson = JsonSerializer.Serialize(old, JsonOpts.Web);
        Assert.Equal(oldJson, JsonSerializer.Serialize(current, JsonOpts.Web));
        Assert.Equal(LegacyKeys, JsonNode.Parse(oldJson)!.AsObject().Select(property => property.Key));
        var explicitNull = JsonNode.Parse(oldJson)!.AsObject(); explicitNull["responseOutcome"] = null;
        var rebound = JsonSerializer.Deserialize<ReviewEndpoints.FindingAction>(explicitNull.ToJsonString(), JsonOpts.Web)!;
        Assert.Equal(oldJson, JsonSerializer.Serialize(rebound, JsonOpts.Web));
        Assert.Equal(Coordination.Hash(new { operation = "review.finding.action", id, findingId, body = old }),
            Coordination.Hash(new { operation = "review.finding.action", id, findingId, body = rebound }));
        // Old explicit-null properties remain serialized. A global ignore-null policy would fail these checks.
        Assert.True(JsonNode.Parse(oldJson)!.AsObject().ContainsKey("evidenceUrl"));
        Assert.True(JsonNode.Parse(oldJson)!.AsObject().ContainsKey("ownerId"));
        var changed = current with { ResponseOutcome = "Noted" };
        Assert.Equal(LegacyKeys.Append("responseOutcome"), JsonSerializer.SerializeToNode(changed, JsonOpts.Web)!.AsObject().Select(property => property.Key));
        Assert.NotEqual(Coordination.Hash(new { operation = "review.finding.action", id, findingId, body = old }),
            Coordination.Hash(new { operation = "review.finding.action", id, findingId, body = changed }));
        if (reason != reason.Trim())
            Assert.NotEqual(Coordination.Hash(new { operation = "review.finding.action", id, findingId, body = old }),
                Coordination.Hash(new { operation = "review.finding.action", id, findingId, body = current with { Reason = reason.Trim() } }));
    }

    [Theory] // AC-SCR-84: lookup of an old committed receipt precedes new required-outcome and stale-version checks.
    [InlineData(false)]
    [InlineData(true)]
    public async Task Genuine_six_field_receipt_replays_after_flag_on_without_new_event_or_legacy_backfill(bool explicitNull)
    {
        using var t = new ReviewResponseFixture(); await t.Flag(false); var s = await t.New(); var finding = await t.Finding(s);
        var id = s.Package; var findingId = finding;
        var body = new LegacyFindingAction(Guid.NewGuid(), t.Version<ReviewFinding>(finding), FindingStatus.Responded,
            "  Retain the legacy é response and raw request whitespace  ", ReviewResponseFixture.Evidence, null);
        var first = await t.Post(TestData.Alex, t.ActionPath(s, finding), body);
        var receipt = t.F.Db(db => db.CoordinationCommands.Single(row => row.ProjectId == s.Project.Id && row.RequestId == body.RequestId));
        Assert.Equal(Coordination.Hash(new { operation = "review.finding.action", id, findingId, body }), receipt.PayloadHash);
        Assert.Equal(1, t.F.Db(db => db.FindingEvents.Count(row => row.FindingId == finding)));
        Assert.Null(t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).ResponseOutcome));
        Assert.Equal(body.Reason.Trim(), t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).Response));
        await t.Flag(true); var before = t.State(s.Project.Id);
        var retry = JsonSerializer.SerializeToNode(body, JsonOpts.Web)!.AsObject();
        if (explicitNull) retry["responseOutcome"] = null;
        using var raw = new StringContent(retry.ToJsonString(), Encoding.UTF8, "application/json");
        var replay = await (await t.F.As(TestData.Alex).PostAsync(t.ActionPath(s, finding), raw)).Json();
        Assert.True(JsonNode.DeepEquals(first, replay)); Assert.Equal(before, t.State(s.Project.Id));
        Assert.Null(t.F.Db(db => db.FindingEvents.Single(row => row.FindingId == finding).ResponseOutcome));
        var detail = await t.Detail(s);
        Assert.False(t.Find(detail, finding).AsObject().ContainsKey("responseOutcome"));
        Assert.False(Assert.Single(detail["events"]!.AsArray())!.AsObject().ContainsKey("responseOutcome"));

        var changed = JsonSerializer.SerializeToNode(body, JsonOpts.Web)!.AsObject(); changed["responseOutcome"] = "Incorporated";
        var mismatch = await t.Post(TestData.Alex, t.ActionPath(s, finding), changed, 422);
        Assert.Equal("idempotency_key_reused", mismatch.S("code")); Assert.Equal(before, t.State(s.Project.Id));
        var changedReason = body with { Reason = body.Reason.Trim() };
        Assert.Equal("idempotency_key_reused", (await t.Post(TestData.Alex, t.ActionPath(s, finding), changedReason, 422)).S("code"));
        Assert.Equal(before, t.State(s.Project.Id));
    }

    [Fact] // AC-SCR-61: real new-outcome replay is exactly once; off guards even previously committed new-field bodies.
    public async Task New_outcome_receipt_replays_once_and_is_hidden_before_lookup_while_flag_off()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        var request = Guid.NewGuid(); var version = t.Version<ReviewFinding>(finding);
        var first = await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Noted", request: request, version: version);
        var before = t.State(s.Project.Id);
        var again = await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Noted", request: request, version: version);
        Assert.True(JsonNode.DeepEquals(first, again)); Assert.Equal(before, t.State(s.Project.Id));
        var mismatch = await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Clarified", request: request, version: version, status: 422);
        Assert.Equal("idempotency_key_reused", mismatch.S("code")); Assert.Equal(before, t.State(s.Project.Id));
        await t.Flag(false); before = t.State(s.Project.Id);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Noted", request: request, version: version, status: 404);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Noted", status: 404);
        Assert.Equal(before, t.State(s.Project.Id));
        var off = await t.Detail(s);
        Assert.False(t.Find(off, finding).AsObject().ContainsKey("responseOutcome"));
        Assert.All(off["events"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("responseOutcome")));
        Assert.Equal("Noted", t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).ResponseOutcome));
        await t.Flag(true);
        Assert.Equal("Noted", t.Find(await t.Detail(s), finding).S("responseOutcome"));
    }

    [Fact] // AC-SCR-84: omitted and explicit-null fresh legacy bodies retain off behavior, but are not fresh flag-on responses.
    public async Task Fresh_flag_on_missing_outcome_is_refused_while_fresh_flag_off_legacy_body_keeps_its_reply()
    {
        using var t = new ReviewResponseFixture(); var s = await t.New(); var finding = await t.Finding(s);
        await t.Flag(true); var before = t.State(s.Project.Id);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, status: 400);
        Assert.Equal(before, t.State(s.Project.Id));
        await t.Flag(false);
        var result = await t.Action(s, finding, FindingStatus.Responded, TestData.Alex);
        Assert.Equal(new[] { "id", "rowVersion" }, result.AsObject().Select(property => property.Key));
        var off = await t.Detail(s);
        Assert.False(t.Find(off, finding).AsObject().ContainsKey("responseOutcome"));
        Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Rita).GetAsync(t.Root(s) + $"/reviews/{s.Package}/response-sheet?format=csv")).StatusCode);
    }
}
