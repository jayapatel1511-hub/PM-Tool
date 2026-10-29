using System.Net;
using System.Text;
using System.Text.Json;
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
    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static readonly string[] RiskRowKeys = ["id", "projectId", "key", "title", "status", "ownerId", "ownerName", "probability", "impact", "score", "band", "mitigation", "triggerIndicator",
        "reviewDate", "isReviewOverdue", "reviewOverdueDays", "projectDisciplineId", "disciplineName", "realisedIssueId", "realisedIssueKey", "rowVersion", "lastActivityAt"];
    static readonly string[] IssueRowKeys = ["id", "projectId", "key", "title", "status", "issueType", "raisedById", "raisedByName", "ownerId", "ownerName", "severity", "dateRaised",
        "targetResolutionDate", "isOverdue", "daysOverdue", "resolution", "resolvedDate", "originRiskId", "originRiskKey", "projectDisciplineId", "disciplineName", "locationLabels",
        "locationSummary", "affectedDisciplineIds", "affectedDisciplineNames", "affectedDisciplineSummary", "documentSummary", "documentIdentifiers", "documentRevisions",
        "verificationStatus", "rowVersion", "lastActivityAt"];

    [Fact]
    public async Task Risk_commands_keep_their_answers_and_shape() // the typed records must serialise to the bytes the app already reads (040-S15-Registers)
    {
        var p = await d.Project();
        var (pm, alex, rita) = (f.As(TestData.Pm), f.As(TestData.Alex), f.As(TestData.Rita));
        var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Storm sewer profile" });

        // Creating: 201 with a Location and the id, the key and the version, in that order.
        var made = await alex.Post($"/api/v1/projects/{p.Id}/risks", new { title = "Permit office backlog", probability = 2, impact = 3, links = new[] { new { targetType = "Task", targetId = task.G("id") } } });
        var r = await made.Json(201);
        Assert.Equal(["id", "key", "rowVersion"], Keys(r));
        Assert.Equal($"/api/v1/risks/{r.S("id")}", made.Headers.Location!.OriginalString);
        Assert.Equal(await RiskVersion(r), r.I("rowVersion"));

        // Editing: 200 with the id and the new version, the ETag header the page reads, and the refusals.
        var v0 = await RiskVersion(r);
        var edit = await alex.Patch($"/api/v1/risks/{r.S("id")}", new { mitigation = "Book the meeting early", reviewDate = "2026-09-30" }, v0);
        var edited = await edit.Json();
        Assert.Equal(["id", "rowVersion"], Keys(edited));
        Assert.Equal((r.G("id"), v0 + 1), (edited.G("id"), edited.I("rowVersion")));
        Assert.Equal($"\"{v0 + 1}\"", edit.Headers.ETag!.Tag);
        Assert.Equal(428, (int)(await alex.Patch($"/api/v1/risks/{r.S("id")}", new { title = "No version" })).StatusCode);
        Assert.Equal("concurrency_conflict", (await alex.Patch($"/api/v1/risks/{r.S("id")}", new { title = "Stale" }, v0).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Patch($"/api/v1/risks/{r.S("id")}", new { title = "No" }, await RiskVersion(r))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alex.Patch($"/api/v1/risks/{r.S("id")}", new { probability = 4 }, await RiskVersion(r))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alex.Patch($"/api/v1/risks/{Guid.NewGuid()}", new { title = "Gone" }, 1)).StatusCode);

        // Moving: 200 with the id, the status, the version and the issue the risk became, nulls written when it did not become one.
        var monitored = await (await MoveRisk(TestData.Alex, r, new { toStatus = "Monitoring" })).Json();
        Assert.Equal(["id", "status", "rowVersion", "issueId", "issueKey"], Keys(monitored));
        Assert.Equal((r.G("id"), "Monitoring", await RiskVersion(r)), (monitored.G("id"), monitored.S("status"), monitored.I("rowVersion")));
        Assert.Null(monitored["issueId"]); Assert.Null(monitored["issueKey"]);
        var realised = await (await MoveRisk(TestData.Alex, r, new { toStatus = "Realised", issue = new { title = "Permit not issued", severity = "High" } })).Json();
        Assert.Equal(["id", "status", "rowVersion", "issueId", "issueKey"], Keys(realised));
        Assert.Equal(("Realised", realised.S("issueKey")), (realised.S("status"), (await f.DbAsync(db => db.Issues.Where(x => x.Id == realised.G("issueId")).Select(x => x.Key).FirstAsync()))));
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveRisk(TestData.Alex, r, new { toStatus = "Nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await MoveRisk(TestData.Rita, await Risk(p.Id, "Another", 1, 1), new { toStatus = "Closed", reason = "x" })).StatusCode);

        // The list and the detail: every property written, nulls included.
        var list = (await alex.GetAsync($"/api/v1/projects/{p.Id}/risks").Result.Json()).AsArray();
        Assert.All(list, x => Assert.Equal(RiskRowKeys, Keys(x)));
        var named = (await alex.GetAsync($"/api/v1/projects/{p.Id}/risks?Q=backlog&Severity=Medium&Status=Realised&DisciplineId={Guid.Empty}").Result.Json()).AsArray(); // the contract's names bind like the lower-case ones
        Assert.Empty(named);
        Assert.Equal(list.Count, (await alex.GetAsync($"/api/v1/projects/{p.Id}/risks?Q=&Status=").Result.Json()).AsArray().Count);
        var detail = await rita.GetAsync($"/api/v1/risks/{r.S("id")}").Result.Json();
        Assert.Equal(["risk", "description", "project", "realisedIssue", "links", "permissions"], Keys(detail));
        Assert.Equal(["id", "projectNumber", "name", "status"], Keys(detail["project"]));
        Assert.Equal(["id", "key", "title", "status"], Keys(detail["realisedIssue"]));
        Assert.Equal(["edit", "transitions", "comment"], Keys(detail["permissions"]));
        Assert.Equal(["ok", "reason"], Keys(detail["permissions"]!["edit"]));
        Assert.False(detail["permissions"]!["edit"]!["ok"]!.GetValue<bool>()); Assert.False(string.IsNullOrEmpty(detail["permissions"]!["edit"]!.S("reason")));
        var open = await alex.GetAsync($"/api/v1/risks/{(await Risk(p.Id, "Plain", 1, 2)).S("key")}").Result.Json();
        Assert.True(open.AsObject().ContainsKey("realisedIssue")); Assert.Null(open["realisedIssue"]);
        Assert.All(open["permissions"]!["transitions"]!.AsArray(), x => Assert.Equal(["to", "ok", "reason"], Keys(x)));
    }

    [Fact]
    public async Task Item_links_and_the_registers_export_were_uncalled_and_keep_their_answers() // POST risks|issues/{id}/links, GET risks|issues/export (040-S15-Registers)
    {
        var p = await d.Project();
        var (pm, alex, rita) = (f.As(TestData.Pm), f.As(TestData.Alex), f.As(TestData.Rita));
        var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Storm sewer profile" });
        var deliverable = await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "85% package", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), requiresReview = false, ownerId = U(TestData.Marc) }).Result.Json(201);
        var other = await d.Project();
        var elsewhere = await d.NewTask(other.Id, TestData.Marc);
        var risk = await Risk(p.Id, "Permit office backlog", 3, 2, review: "2026-09-10", owner: U(TestData.Alex));
        var issue = await Issue(p.Id, "Survey crew cannot access site", "High", target: "2026-09-12", owner: U(TestData.Alex), raised: "2026-09-08");

        foreach (var (kind, item) in new[] { ("risks", risk), ("issues", issue) })
        {
            // Linking: 201 with a Location on the shared item-link route and just the id; the page then reads it back on the detail.
            var made = await alex.Post($"/api/v1/{kind}/{item.S("id")}/links", new { targetType = "Task", targetId = task.G("id") });
            var link = await made.Json(201);
            Assert.Equal(["id"], Keys(link));
            Assert.Equal($"/api/v1/item-links/{link.S("id")}", made.Headers.Location!.OriginalString);
            var stored = await f.DbAsync(db => db.ItemLinks.AsNoTracking().SingleAsync(x => x.Id == link.G("id")));
            Assert.Equal((item.G("id"), "Task", task.G("id"), ItemRelation.Related), (stored.SourceId, stored.TargetType, stored.TargetId, stored.Relation));
            Assert.Equal(task.S("key"), (await pm.GetAsync($"/api/v1/{kind}/{item.S("id")}").Result.Json())["links"]![0]!.S("key"));
            Assert.Equal("link_exists", (await alex.Post($"/api/v1/{kind}/{item.S("id")}/links", new { targetType = "Task", targetId = task.G("id") }).Result.Json(409)).S("code"));
            Assert.Equal(["id"], Keys(await alex.Post($"/api/v1/{kind}/{item.S("id")}/links", new { targetType = "Deliverable", targetId = deliverable.G("id") }).Result.Json(201)));
            Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post($"/api/v1/{kind}/{item.S("id")}/links", new { targetType = "Task", targetId = elsewhere.G("id") })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post($"/api/v1/{kind}/{item.S("id")}/links", new { targetType = "Risk", targetId = task.G("id") })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post($"/api/v1/{kind}/{item.S("id")}/links", new { targetType = "Task", targetId = task.G("id") })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await alex.Post($"/api/v1/{kind}/{Guid.NewGuid()}/links", new { targetType = "Task", targetId = task.G("id") })).StatusCode);
        }

        // The risk register export: the columns in order, the rows by severity, and the register's filters.
        var plain = await Risk(p.Id, "Records may be incomplete", 1, 2);
        var csv = await pm.GetAsync($"/api/v1/projects/{p.Id}/risks/export?format=csv");
        Assert.Equal(("text/csv", true), (csv.Content.Headers.ContentType!.MediaType, csv.Content.Headers.ContentDisposition!.FileName!.Contains("-risks-")));
        var lines = (await csv.Content.ReadAsStringAsync()).TrimEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal("Key,Title,Status,Severity,Score,Probability,Impact,Owner,Discipline,Review date,Mitigation,Trigger indicator,Realised as", lines[0].TrimStart('﻿'));
        Assert.Equal(3, lines.Length);
        Assert.Equal($"{risk.S("key")},Permit office backlog,Open,High,6,3,2,Alex Chen,,2026-09-10,Book the permit meeting early,No reply within two weeks,", lines[1]);
        Assert.StartsWith($"{plain.S("key")},Records may be incomplete,Open,Low,2,1,2,Alex Chen", lines[2]);
        var high = (await alex.GetAsync($"/api/v1/projects/{p.Id}/risks/export?format=csv&severity=High&indicator=reviewOverdue").Result.Content.ReadAsStringAsync()).TrimEnd().Split('\n');
        Assert.Equal(2, high.Length); Assert.StartsWith(risk.S("key"), high[1]);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", (await pm.GetAsync($"/api/v1/projects/{p.Id}/risks/export?format=xlsx")).Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/risks/export?format=csv")).StatusCode);

        // The issue register export: the same, with the issue columns and filters.
        var low = await Issue(p.Id, "Missing topo survey tile", "Low", target: "2026-09-30");
        var icsv = await pm.GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=csv");
        Assert.Equal(("text/csv", true), (icsv.Content.Headers.ContentType!.MediaType, icsv.Content.Headers.ContentDisposition!.FileName!.Contains("-issues-")));
        var ilines = (await icsv.Content.ReadAsStringAsync()).TrimEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal("Key,Title,Issue type,Status,Severity,Owner,Raised by,Discipline,Affected disciplines,Date raised,Target resolution,Days overdue,Resolved,Resolution,From risk,Location,References,Verification", ilines[0].TrimStart('﻿'));
        Assert.Equal(3, ilines.Length);
        Assert.StartsWith($"{issue.S("key")},Survey crew cannot access site,General,Open,High,Alex Chen,Alex Chen,,,", ilines[1]);
        Assert.StartsWith($"{low.S("key")},Missing topo survey tile,General,Open,Low,Alex Chen", ilines[2]);
        var overdue = (await alex.GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=csv&severity=High&indicator=overdue&status=Open").Result.Content.ReadAsStringAsync()).TrimEnd().Split('\n');
        Assert.Equal(2, overdue.Length); Assert.StartsWith(issue.S("key"), overdue[1]);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", (await pm.GetAsync($"/api/v1/projects/{p.Id}/issues/export?format=xlsx")).Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/issues/export?format=csv")).StatusCode);
    }

    [Fact]
    public async Task Issue_commands_list_and_detail_keep_their_answers_and_shape() // GET issues, GET issues/{id} was a Task<object> (040-S15-Registers)
    {
        var p = await d.Project();
        var (pm, alex, rita) = (f.As(TestData.Pm), f.As(TestData.Alex), f.As(TestData.Rita));
        var civil = d.ProjectDiscipline(p.Id, "Civil"); var electrical = d.ProjectDiscipline(p.Id, "Electrical");

        // Creating: 201 with a Location and the id, the key and the version.
        var made = await alex.Post($"/api/v1/projects/{p.Id}/issues", new { title = "Survey crew cannot access site", description = "Gate locked", severity = "High", targetResolutionDate = "2026-09-12",
            dateRaised = "2026-09-08", ownerId = U(TestData.Marc), projectDisciplineId = civil, affectedDisciplineIds = new[] { electrical } });
        var i = await made.Json(201);
        Assert.Equal(["id", "key", "rowVersion"], Keys(i));
        Assert.Equal($"/api/v1/issues/{i.S("id")}", made.Headers.Location!.OriginalString);
        Assert.Equal(await IssueVersion(i), i.I("rowVersion"));

        // Editing: 200 with the id and the new version, the ETag header, and the refusals.
        var v0 = await IssueVersion(i);
        var edit = await alex.Patch($"/api/v1/issues/{i.S("id")}", new { description = "Gate locked after hours", severity = "Medium", targetResolutionDate = "2026-09-20" }, v0);
        var edited = await edit.Json();
        Assert.Equal(["id", "rowVersion"], Keys(edited));
        Assert.Equal((i.G("id"), v0 + 1), (edited.G("id"), edited.I("rowVersion")));
        Assert.Equal($"\"{v0 + 1}\"", edit.Headers.ETag!.Tag);
        Assert.Equal(428, (int)(await alex.Patch($"/api/v1/issues/{i.S("id")}", new { title = "No version" })).StatusCode);
        Assert.Equal("concurrency_conflict", (await alex.Patch($"/api/v1/issues/{i.S("id")}", new { title = "Stale" }, v0).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Patch($"/api/v1/issues/{i.S("id")}", new { title = "No" }, await IssueVersion(i))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alex.Patch($"/api/v1/issues/{i.S("id")}", new { severity = "Nope" }, await IssueVersion(i))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alex.Patch($"/api/v1/issues/{Guid.NewGuid()}", new { title = "Gone" }, 1)).StatusCode);
        Assert.Equal(["id", "rowVersion"], Keys(await pm.Patch($"/api/v1/issues/{i.S("id")}", new { severity = "High", affectedDisciplineIds = Array.Empty<Guid>() }, await IssueVersion(i)).Result.Json()));

        // The list: every property written, nulls included, the affected discipline and the location text where they exist.
        var list = (await alex.GetAsync($"/api/v1/projects/{p.Id}/issues").Result.Json()).AsArray();
        Assert.All(list, x => Assert.Equal(IssueRowKeys, Keys(x)));
        var row = list.Single()!;
        Assert.Equal(("High", "Open", "General", "Marc Dubois", "Alex Chen", "Civil", 0), (row.S("severity"), row.S("status"), row.S("issueType"), row.S("ownerName"), row.S("raisedByName"), row.S("disciplineName"), row["affectedDisciplineIds"]!.AsArray().Count));
        Assert.False(row["isOverdue"]!.GetValue<bool>()); Assert.Equal(0, row.I("daysOverdue")); // the target moved to the 20th
        Assert.Empty(row["locationLabels"]!.AsArray()); Assert.Equal("", row.S("locationSummary")); Assert.Equal("None", row.S("verificationStatus"));
        Assert.Null(row["resolution"]); Assert.Null(row["resolvedDate"]); Assert.Null(row["originRiskId"]); Assert.Null(row["originRiskKey"]);
        Assert.Equal(await IssueVersion(i), row.I("rowVersion"));
        await pm.Patch($"/api/v1/issues/{i.S("id")}", new { affectedDisciplineIds = new[] { electrical } }, await IssueVersion(i)).Result.Json();
        Assert.Equal(new[] { "Electrical" }, (await alex.GetAsync($"/api/v1/projects/{p.Id}/issues").Result.Json()).AsArray().Single()!["affectedDisciplineNames"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(1, (await alex.GetAsync($"/api/v1/projects/{p.Id}/issues?disciplineId={electrical}").Result.Json()).AsArray().Count);
        Assert.Empty((await alex.GetAsync($"/api/v1/projects/{p.Id}/issues?verification=Verified").Result.Json()).AsArray());
        Assert.Single((await alex.GetAsync($"/api/v1/projects/{p.Id}/issues?verification=None").Result.Json()).AsArray());
        Assert.Single((await alex.GetAsync($"/api/v1/projects/{p.Id}/issues?Q=crew&Severity=High&Status=Open&Indicator=open&Verification=None&IssueType=General").Result.Json()).AsArray()); // the contract's names bind like the lower-case ones
        Assert.Empty((await alex.GetAsync($"/api/v1/projects/{p.Id}/issues?Q=crew&IssueType=Coordination&StationFrom=1&StationTo=2").Result.Json()).AsArray());

        // The detail: the whole envelope for the person who can edit, and the same issue for a Read Only account.
        var detail = await alex.GetAsync($"/api/v1/issues/{i.S("id")}").Result.Json();
        Assert.Equal(["issue", "description", "project", "originRisk", "links", "permissions"], Keys(detail));
        Assert.Equal(IssueRowKeys, Keys(detail["issue"]));
        Assert.Equal("Gate locked after hours", detail.S("description"));
        Assert.Equal(["id", "projectNumber", "name", "status"], Keys(detail["project"]));
        Assert.Equal((p.Id, p.ProjectNumber), (detail["project"]!.G("id"), detail["project"]!.S("projectNumber")));
        Assert.True(detail.AsObject().ContainsKey("originRisk")); Assert.Null(detail["originRisk"]);
        Assert.Equal(["edit", "changeType", "transitions", "comment"], Keys(detail["permissions"]));
        Assert.Equal(["ok", "reason"], Keys(detail["permissions"]!["edit"]));
        Assert.Equal(["ok", "reason"], Keys(detail["permissions"]!["changeType"]));
        Assert.True(detail["permissions"]!["edit"]!["ok"]!.GetValue<bool>()); Assert.Null(detail["permissions"]!["edit"]!["reason"]);
        var steps = detail["permissions"]!["transitions"]!.AsArray();
        Assert.Equal(["In Progress", "Resolved", "Cancelled"], steps.Select(x => x!.S("to")));
        Assert.All(steps, x => Assert.Equal(["to", "ok", "reason"], Keys(x)));
        Assert.True(detail["permissions"]!["comment"]!.GetValue<bool>());
        Assert.Equal(i.S("key"), (await pm.GetAsync($"/api/v1/issues/{i.S("key")}").Result.Json())["issue"]!.S("key")); // by key too
        var view = await rita.GetAsync($"/api/v1/issues/{i.S("id")}").Result.Json();
        Assert.False(view["permissions"]!["edit"]!["ok"]!.GetValue<bool>());
        var why = view["permissions"]!["edit"]!.S("reason"); Assert.False(string.IsNullOrEmpty(why));
        Assert.False(view["permissions"]!["changeType"]!["ok"]!.GetValue<bool>()); Assert.Equal(why, view["permissions"]!["changeType"]!.S("reason"));
        Assert.All(view["permissions"]!["transitions"]!.AsArray(), x => Assert.Equal((false, why), (x!["ok"]!.GetValue<bool>(), x.S("reason"))));
        Assert.False(view["permissions"]!["comment"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/issues/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync("/api/v1/issues/NO-SUCH-I99")).StatusCode);

        // Moving: 200 with the id, the status and the version; resolving writes the date; the origin risk shows once realised.
        var moved = await (await MoveIssue(TestData.Marc, i, new { toStatus = "In Progress" })).Json();
        Assert.Equal(["id", "status", "rowVersion"], Keys(moved));
        Assert.Equal((i.G("id"), "In Progress", await IssueVersion(i)), (moved.G("id"), moved.S("status"), moved.I("rowVersion")));
        var resolved = await (await MoveIssue(TestData.Marc, i, new { toStatus = "Resolved", resolution = "Key issued", resolvedDate = "2026-09-13" })).Json();
        Assert.Equal(["id", "status", "rowVersion"], Keys(resolved));
        var after = await pm.GetAsync($"/api/v1/issues/{i.S("id")}").Result.Json();
        Assert.Equal(("Resolved", "Key issued", "2026-09-13", false), (after["issue"]!.S("status"), after["issue"]!.S("resolution"), after["issue"]!.S("resolvedDate"), after["issue"]!["isOverdue"]!.GetValue<bool>()));
        Assert.Equal(HttpStatusCode.BadRequest, (await MoveIssue(TestData.Marc, i, new { toStatus = "Nope" })).StatusCode);
        var risk = await Risk(p.Id, "Permit delay", 3, 3);
        var became = await (await MoveRisk(TestData.Alex, risk, new { toStatus = "Realised", issue = new { title = "Permit not issued", severity = "Medium" } })).Json();
        var fromRisk = await pm.GetAsync($"/api/v1/issues/{became.S("issueId")}").Result.Json();
        Assert.Equal(["id", "key", "title", "status"], Keys(fromRisk["originRisk"]));
        Assert.Equal((risk.S("key"), "Permit delay"), (fromRisk["originRisk"]!.S("key"), fromRisk["originRisk"]!.S("title")));
    }

    [Fact]
    public async Task Issue_locations_documents_and_verification_keep_their_answers_and_shape() // POST/GET locations, documents, verification (040-S15-Registers)
    {
        var p = await d.Project();
        var (pm, alex, rita) = (f.As(TestData.Pm), f.As(TestData.Alex), f.As(TestData.Rita));
        var issue = await Issue(p.Id, "Utility clash", "High", owner: U(TestData.Alex));
        var id = issue.S("id");
        Assert.Equal("[]", await alex.GetAsync($"/api/v1/issues/{id}/locations").Result.Content.ReadAsStringAsync());
        Assert.Equal("[]", await alex.GetAsync($"/api/v1/issues/{id}/documents").Result.Content.ReadAsStringAsync());
        Assert.Equal("[]", await alex.GetAsync($"/api/v1/issues/{id}/verification").Result.Content.ReadAsStringAsync());

        // A location: 201 with a Location header, the id and the version the issue moved to; refused for a Read Only account and a stale version.
        var v0 = await IssueVersion(issue);
        var made = await alex.Post($"/api/v1/issues/{id}/locations", new { kind = "Alignment", alignment = "Main Street", startStation = 1000.5, endStation = 1100, stationUnits = "m", rowVersion = v0 });
        var loc = await made.Json(201);
        Assert.Equal(["id", "rowVersion"], Keys(loc));
        Assert.Equal($"/api/v1/issue-locations/{loc.S("id")}", made.Headers.Location!.OriginalString);
        Assert.Equal(await f.DbAsync(db => db.IssueLocations.Where(x => x.Id == loc.G("id")).Select(x => x.RowVersion).FirstAsync()), loc.I("rowVersion")); // the new row's own version
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post($"/api/v1/issues/{id}/locations", new { kind = "SiteArea", siteArea = "Yard", rowVersion = await IssueVersion(issue) })).StatusCode);
        Assert.Equal("concurrency_conflict", (await alex.Post($"/api/v1/issues/{id}/locations", new { kind = "SiteArea", siteArea = "Yard", rowVersion = v0 }).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post($"/api/v1/issues/{id}/locations", new { kind = "SiteArea", rowVersion = await IssueVersion(issue) })).StatusCode);
        await alex.Post($"/api/v1/issues/{id}/locations", new { kind = "Coordinate", coordinateX = 500000.25, coordinateY = 4800000.75, coordinateZ = 98.5, coordinateReferenceSystem = "EPSG:26917", coordinateUnits = "m", rowVersion = await IssueVersion(issue) }).Result.Json(201);
        var locations = (await alex.GetAsync($"/api/v1/issues/{id}/locations").Result.Json()).AsArray();
        Assert.Equal(2, locations.Count);
        Assert.All(locations, x => Assert.Equal(["id", "kind", "siteArea", "building", "level", "room", "assetSystem", "alignment", "startStation", "endStation", "stationUnits", "coordinateX",
            "coordinateY", "coordinateZ", "coordinateReferenceSystem", "coordinateUnits", "rowVersion"], Keys(x)));
        Assert.Equal(("Alignment", "Main Street", 1000.5m, 1100m, "m"), (locations[0]!.S("kind"), locations[0]!.S("alignment"), locations[0]["startStation"]!.GetValue<decimal>(), locations[0]["endStation"]!.GetValue<decimal>(), locations[0]!.S("stationUnits")));
        Assert.Null(locations[0]["siteArea"]); Assert.Null(locations[0]["coordinateX"]);
        Assert.Equal(("Coordinate", 500000.25m, "EPSG:26917"), (locations[1]!.S("kind"), locations[1]["coordinateX"]!.GetValue<decimal>(), locations[1]!.S("coordinateReferenceSystem")));

        // A document: 201 with its own Location, the id and the version; a duplicate is refused; the list writes provenance and null source ids.
        var doc = await alex.Post($"/api/v1/issues/{id}/documents", new { kind = "Drawing", identifier = "C-101", revision = "A", sourceUrl = "https://review.example.test/c-101", isAvailable = true, rowVersion = await IssueVersion(issue) });
        var d1 = await doc.Json(201);
        Assert.Equal(["id", "rowVersion"], Keys(d1));
        Assert.Equal($"/api/v1/issue-document-references/{d1.S("id")}", doc.Headers.Location!.OriginalString);
        Assert.Equal(await f.DbAsync(db => db.IssueDocumentReferences.Where(x => x.Id == d1.G("id")).Select(x => x.RowVersion).FirstAsync()), d1.I("rowVersion"));
        Assert.Equal(HttpStatusCode.Conflict, (await alex.Post($"/api/v1/issues/{id}/documents", new { kind = "Drawing", identifier = "C-101", revision = "A", sourceUrl = "https://review.example.test/c-101", isAvailable = true, rowVersion = await IssueVersion(issue) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post($"/api/v1/issues/{id}/documents", new { kind = "Drawing", identifier = "", revision = "A", sourceUrl = "https://review.example.test/x", isAvailable = true, rowVersion = await IssueVersion(issue) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post($"/api/v1/issues/{id}/documents", new { kind = "Drawing", identifier = "C-102", revision = "A", sourceUrl = "https://review.example.test/x", isAvailable = true, rowVersion = await IssueVersion(issue) })).StatusCode);
        var documents = (await alex.GetAsync($"/api/v1/issues/{id}/documents").Result.Json()).AsArray();
        var reference = Assert.Single(documents)!;
        Assert.Equal(["id", "kind", "identifier", "revision", "sourceUrl", "sourceSystem", "stableSourceId", "registeredBy", "registeredAt", "registrationMethod", "externalTopicId",
            "modelElementGuid", "viewpointUrl", "isAvailable", "replacedById", "rowVersion"], Keys(reference));
        Assert.Equal(("Drawing", "C-101", "A", "review.example.test", "Manual", U(TestData.Alex)), (reference.S("kind"), reference.S("identifier"), reference.S("revision"), reference.S("sourceSystem"), reference.S("registrationMethod"), reference.G("registeredBy")));
        Assert.Null(reference["stableSourceId"]); Assert.Null(reference["replacedById"]); Assert.True(reference["isAvailable"]!.GetValue<bool>());
        Assert.Equal(JsonValueKind.String, reference["registeredAt"]!.GetValueKind());

        // The verification: 201 on the appointment and on the decision, newest first, the version the issue moved to.
        var appoint = await pm.Post($"/api/v1/issues/{id}/verification", new { verifierId = U(TestData.Marc), status = "Proposed", note = "Appoint Marc", rowVersion = await IssueVersion(issue) });
        var proposed = await appoint.Json(201);
        Assert.Equal(["id", "rowVersion"], Keys(proposed));
        Assert.Equal($"/api/v1/issue-verifications/{proposed.S("id")}", appoint.Headers.Location!.OriginalString);
        Assert.Equal(await f.DbAsync(db => db.IssueVerifications.Where(x => x.Id == proposed.G("id")).Select(x => x.RowVersion).FirstAsync()), proposed.I("rowVersion"));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Marc).Post($"/api/v1/issues/{id}/verification", new { verifierId = U(TestData.Marc), status = "Verified", rowVersion = await IssueVersion(issue) })).StatusCode); // evidence required
        var verified = await f.As(TestData.Marc).Post($"/api/v1/issues/{id}/verification", new { verifierId = U(TestData.Marc), status = "Verified", evidenceUrl = "https://review.example.test/verify", rowVersion = await IssueVersion(issue) }).Result.Json(201);
        Assert.Equal(["id", "rowVersion"], Keys(verified));
        var steps = (await alex.GetAsync($"/api/v1/issues/{id}/verification").Result.Json()).AsArray();
        Assert.Equal(["Verified", "Proposed"], steps.Select(x => x!.S("status")));
        Assert.All(steps, x => Assert.Equal(["id", "verifierId", "status", "evidenceUrl", "note", "verifiedAt", "rowVersion"], Keys(x)));
        Assert.Equal((U(TestData.Marc), "https://review.example.test/verify"), (steps[0].G("verifierId"), steps[0].S("evidenceUrl")));
        Assert.Equal(JsonValueKind.String, steps[0]["verifiedAt"]!.GetValueKind()); Assert.Null(steps[1]["verifiedAt"]); Assert.Null(steps[1]["evidenceUrl"]); Assert.Equal("Appoint Marc", steps[1].S("note"));
        Assert.Equal(HttpStatusCode.NotFound, (await alex.GetAsync($"/api/v1/issues/{Guid.NewGuid()}/documents")).StatusCode);
    }
}