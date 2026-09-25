using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 014: the risk register (US1), the issue register with A-07 and Red health (US2), realised risks (US3), and
/// raising from a task, the dashboard, Weekly Coordination and the Open Issues / High Risks report (US4).
[Collection("api")]
public sealed class RegistersApiTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    Task<JsonNode> Risk(Guid projectId, string title, int p, int i, string? review = null, string as_ = TestData.Alex, Guid? owner = null, object[]? links = null, int expect = 201) =>
        f.As(as_).Post($"/api/v1/projects/{projectId}/risks", new
        {
            title, description = "What could happen", probability = p, impact = i, reviewDate = review, ownerId = owner, mitigation = "Book the permit meeting early",
            triggerIndicator = "No reply within two weeks", links,
        }).Result.Json(expect);

    Task<JsonNode> Issue(Guid projectId, string title, string severity, string? target = null, string as_ = TestData.Alex, Guid? owner = null, object[]? links = null,
        string? raised = null, int expect = 201) =>
        f.As(as_).Post($"/api/v1/projects/{projectId}/issues", new { title, description = "What happened", severity, targetResolutionDate = target, ownerId = owner, links, dateRaised = raised })
            .Result.Json(expect);

    Task<int> RiskVersion(JsonNode r) => f.DbAsync(db => db.Risks.Where(x => x.Id == r.G("id")).Select(x => x.RowVersion).FirstAsync());
    Task<int> IssueVersion(JsonNode i) => f.DbAsync(db => db.Issues.Where(x => x.Id == i.G("id")).Select(x => x.RowVersion).FirstAsync());

    async Task<HttpResponseMessage> MoveIssue(string as_, JsonNode i, object body)
    {
        var dict = new Dictionary<string, object?> { ["rowVersion"] = await IssueVersion(i) };
        foreach (var p in body.GetType().GetProperties()) dict[p.Name] = p.GetValue(body);
        return await f.As(as_).Post($"/api/v1/issues/{i.S("id")}/transition", dict);
    }

    async Task<HttpResponseMessage> MoveRisk(string as_, JsonNode r, object body)
    {
        var dict = new Dictionary<string, object?> { ["rowVersion"] = await RiskVersion(r) };
        foreach (var p in body.GetType().GetProperties()) dict[p.Name] = p.GetValue(body);
        return await f.As(as_).Post($"/api/v1/risks/{r.S("id")}/transition", dict);
    }

    [Fact]
    public async Task Risks_are_scored_banded_and_flagged_when_their_review_is_overdue() // US1, FR-001, FR-004, RSK-01, RSK-02
    {
        var p = await d.Project();
        var high = await Risk(p.Id, "Environmental approval may delay fieldwork", 3, 2, review: "2026-09-10"); // reviewed 4 days ago
        await Risk(p.Id, "Utility records may be incomplete", 2, 2, review: "2026-09-30");
        await Risk(p.Id, "Survey crew availability", 1, 1);
        var closed = await Risk(p.Id, "Night work restrictions", 3, 3);
        Assert.Matches(@"^P\w+-R01$", high.S("key"));
        (await MoveRisk(TestData.Pm, closed, new { toStatus = "Closed", reason = "Restrictions lifted" })).EnsureSuccessStatusCode();

        var list = (await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{p.Id}/risks").Result.Json()).AsArray();
        Assert.Equal(new[] { 9, 6, 4, 1 }, list.Select(x => x!.I("score"))); // severity descending (§13.13)
        Assert.Equal(new[] { "High", "High", "Medium", "Low" }, list.Select(x => x!.S("band")));
        var h = list[1]!;
        Assert.True(h["isReviewOverdue"]!.GetValue<bool>());
        Assert.Equal((4, "Alex Chen"), (h.I("reviewOverdueDays"), h.S("ownerName")));
        Assert.False(list[0]!["isReviewOverdue"]!.GetValue<bool>()); // closed: never overdue
        Assert.Equal(new[] { high.S("key") }, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/risks?severity=High&indicator=open").Result.Json()).AsArray().Select(x => x!.S("key")));
        Assert.Single((await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/risks?indicator=reviewOverdue").Result.Json()).AsArray());

        await f.Evaluate(p.Id);
        var dash = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/dashboard").Result.Json();
        Assert.Equal(1, dash["risks"]!.I("high")); // the closed High risk has left the dashboard
        var wc = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/coordination").Result.Json();
        Assert.Equal(new[] { high.S("key") }, wc["risks"]!.AsArray().Select(x => x!.S("key")));

        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/risks", new { title = "Bad", probability = 4, impact = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post($"/api/v1/projects/{p.Id}/risks", new { title = "No", probability = 1, impact = 1 })).StatusCode);
        var v = await RiskVersion(high);
        (await f.As(TestData.Alex).Patch($"/api/v1/risks/{high.S("id")}", new { impact = 1, reviewDate = "2026-09-21" }, v)).EnsureSuccessStatusCode();
        var edited = await f.As(TestData.Alex).GetAsync($"/api/v1/risks/{high.S("key")}").Result.Json();
        Assert.Equal((3, "Medium", false), (edited["risk"]!.I("score"), edited["risk"]!.S("band"), edited["risk"]!["isReviewOverdue"]!.GetValue<bool>()));
    }

    [Fact]
    public async Task A_High_open_issue_is_a_Critical_attention_item_and_turns_health_Red_until_resolved() // US2, FR-005..FR-007, ISS-01..03, A-07
    {
        var p = await d.Project();
        var gate = await Issue(p.Id, "Survey crew cannot access site: gate locked", "High", target: "2026-09-12", owner: U(TestData.Marc), raised: "2026-09-08");
        await Issue(p.Id, "Missing topo survey tile", "Low", target: "2026-09-30");
        Assert.Matches(@"^P\w+-I01$", gate.S("key"));
        await d.NewTask(p.Id, TestData.Marc); // health needs open work to evaluate (§16.3)
        await f.Evaluate(p.Id);

        var att = await f.DbAsync(db => db.Attention.Where(a => a.ProjectId == p.Id && a.RuleId == "A-07").ToListAsync());
        var a07 = Assert.Single(att);
        Assert.Equal((Severity.Critical, gate.G("id")), (a07.Severity, a07.ItemId));
        Assert.Contains(U(TestData.Marc), a07.RouteToUserIds);
        Assert.Contains(U(TestData.Pm), a07.RouteToUserIds);
        Assert.Equal("Red", (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["health"]!.S("computed"));
        var dash = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/dashboard").Result.Json();
        Assert.Equal((2, 1), (dash["issues"]!.I("open"), dash["issues"]!.I("high")));

        var list = (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues").Result.Json()).AsArray();
        Assert.Equal(new[] { "High", "Low" }, list.Select(x => x!.S("severity")));
        Assert.Equal((true, 2), (list[0]!["isOverdue"]!.GetValue<bool>(), list[0]!.I("daysOverdue"))); // ISS-03
        Assert.Single((await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues?indicator=overdue").Result.Json()).AsArray());

        var noText = await MoveIssue(TestData.Marc, gate, new { toStatus = "Resolved" });
        Assert.Equal(HttpStatusCode.BadRequest, noText.StatusCode); // ISS-02
        Assert.Contains("resolution", (await noText.Json(400))["errors"]!.AsObject().Select(x => x.Key));
        Assert.Equal(HttpStatusCode.Forbidden, (await MoveIssue(TestData.Rita, gate, new { toStatus = "Resolved", resolution = "x" })).StatusCode);
        (await MoveIssue(TestData.Marc, gate, new { toStatus = "Resolved", resolution = "Owner gave the crew a key; access every day from 7:00" })).EnsureSuccessStatusCode();
        var resolved = (await f.As(TestData.Pm).GetAsync($"/api/v1/issues/{gate.S("id")}").Result.Json())["issue"]!;
        Assert.Equal(("Resolved", "2026-09-14", false), (resolved.S("status"), resolved.S("resolvedDate"), resolved["isOverdue"]!.GetValue<bool>()));
        await f.Evaluate(p.Id);
        Assert.Empty(await f.DbAsync(db => db.Attention.Where(a => a.ProjectId == p.Id && a.RuleId == "A-07").ToListAsync()));
        Assert.NotEqual("Red", (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["health"]!.S("computed"));

        Assert.Equal(HttpStatusCode.BadRequest, (await MoveIssue(TestData.Marc, gate, new { toStatus = "In Progress" })).StatusCode); // reopening needs a reason
        (await MoveIssue(TestData.Marc, gate, new { toStatus = "In Progress", reason = "Gate locked again" })).EnsureSuccessStatusCode();
        Assert.Null((await f.As(TestData.Pm).GetAsync($"/api/v1/issues/{gate.S("id")}").Result.Json())["issue"]!["resolvedDate"]);
        Assert.Equal(1, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ItemId == gate.G("id") && a.Action == "Resolved")));
    }

    [Fact]
    public async Task A_realised_risk_needs_its_issue_and_keeps_the_link_on_both() // US3, FR-003, RSK-03, SC-003
    {
        var p = await d.Project();
        var risk = await Risk(p.Id, "Environmental approval may delay fieldwork", 3, 2);
        var none = await MoveRisk(TestData.Alex, risk, new { toStatus = "Realised" });
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(RiskStatus.Open, await f.DbAsync(db => db.Risks.Where(x => x.Id == risk.G("id")).Select(x => x.Status).FirstAsync())); // unchanged

        var r = await (await MoveRisk(TestData.Alex, risk, new
        {
            toStatus = "Realised", issue = new { title = "Fieldwork delayed: approval not received", severity = "High", targetResolutionDate = "2026-10-01" },
        })).Json();
        var issue = (await f.As(TestData.Pm).GetAsync($"/api/v1/issues/{r.S("issueId")}").Result.Json());
        Assert.Equal((risk.S("id"), risk.S("key")), (issue["originRisk"]!.S("id"), issue["issue"]!.S("originRiskKey")));
        var back = await f.As(TestData.Pm).GetAsync($"/api/v1/risks/{risk.S("id")}").Result.Json();
        Assert.Equal(("Realised", r.S("issueKey")), (back["risk"]!.S("status"), back["realisedIssue"]!.S("key")));
        Assert.Empty(back["permissions"]!["transitions"]!.AsArray()); // Realised is final
        Assert.Equal(1, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ItemId == risk.G("id") && a.Action == "Realised")));

        // Linking an existing issue; one that already records another risk is refused.
        var second = await Risk(p.Id, "Permit office backlog", 2, 3);
        var existing = await Issue(p.Id, "Permit not issued", "Medium");
        (await MoveRisk(TestData.Alex, second, new { toStatus = "Realised", issueId = existing.G("id") })).EnsureSuccessStatusCode();
        Assert.Equal(second.G("id"), await f.DbAsync(db => db.Issues.Where(x => x.Id == existing.G("id")).Select(x => x.OriginRiskId).FirstAsync()));
        var third = await Risk(p.Id, "Another view of the backlog", 1, 3);
        var taken = await MoveRisk(TestData.Alex, third, new { toStatus = "Realised", issueId = existing.G("id") });
        Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);
        Assert.Equal(0, await f.DbAsync(db => db.Risks.CountAsync(x => x.ProjectId == p.Id && x.Status == RiskStatus.Realised && x.RealisedIssueId == null))); // SC-003
    }

    [Fact]
    public async Task An_issue_raised_from_a_task_is_linked_and_reported_with_the_High_risks() // US4, FR-008..FR-010
    {
        var p = await d.Project();
        var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Topographic survey" });
        var issue = await Issue(p.Id, "Survey crew cannot access site", "High", target: "2026-09-18", links: [new { targetType = "Task", targetId = task.G("id") }]);
        var detail = await f.As(TestData.Pm).GetAsync($"/api/v1/issues/{issue.S("id")}").Result.Json();
        var link = detail["links"]!.AsArray().Single()!;
        Assert.Equal((task.S("key"), "related"), (link.S("key"), link.S("relation")));
        await Issue(p.Id, "Late client comments", "Medium");
        await Risk(p.Id, "Approval delay", 3, 2);
        await Risk(p.Id, "Records incomplete", 2, 2);

        var rep = await f.As(TestData.Pm).GetAsync($"/api/v1/reports/open-issues-high-risks?projectId={p.Id}").Result.Json();
        var rows = rep["rows"]!.AsArray();
        Assert.Equal(new[] { "Issue", "Risk", "Issue" }, rows.Select(x => x!.S("itemType"))); // High issue and High risk, then the Medium issue; not the Medium risk
        Assert.Equal(("High (6)", p.ProjectNumber), (rows[1]!.S("severity"), rows[1]!.S("projectNumber")));
        var medium = (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/open-issues-high-risks?projectId={p.Id}&severity=Medium").Result.Json())["rows"]!.AsArray();
        Assert.Equal(new[] { "Late client comments", "Records incomplete" }, medium.Select(x => x!.S("title")).Order());
        var csv = await f.As(TestData.Pm).GetAsync($"/api/v1/reports/open-issues-high-risks/export?projectId={p.Id}&format=csv");
        Assert.StartsWith("Type,Key,Title,Project,Owner,Severity,Status,Date,Days overdue", Encoding.UTF8.GetString((await csv.Content.ReadAsByteArrayAsync())[3..]));
        var reg = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=csv");
        Assert.Contains("Survey crew cannot access site", Encoding.UTF8.GetString(await reg.Content.ReadAsByteArrayAsync()));

        // Unlinking goes through the shared item-link endpoint and the issue's own permission.
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).DeleteAsync($"/api/v1/item-links/{link.S("id")}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await f.As(TestData.Alex).DeleteAsync($"/api/v1/item-links/{link.S("id")}")).StatusCode);
        Assert.Empty((await f.As(TestData.Pm).GetAsync($"/api/v1/issues/{issue.S("id")}").Result.Json())["links"]!.AsArray());
    }
}
