using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 045 Part B (§49.3): every count opens the list it counts, and no existing count changes value (FR-RCV-07 to FR-RCV-09).
[Collection("api")]
public sealed class CountLinkTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);
    DateOnly Today => DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);

    async Task<JsonNode> Deliverable(Guid projectId, string discipline, object extra)
    {
        var body = new Dictionary<string, object?> { ["name"] = "Package " + Guid.NewGuid().ToString("N")[..4], ["projectDisciplineId"] = d.ProjectDiscipline(projectId, discipline),
            ["deliverableTypeId"] = await d.DeliverableType(), ["requiresReview"] = false };
        foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/deliverables", body).Result.Json(201);
    }

    Task SetStatus(JsonNode deliverable, string status) =>
        f.DbAsync(db => db.Deliverables.Where(x => x.Id == deliverable.G("id")).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, status)));

    [Fact]
    [Trait("Spec", "AC-RCV-08")]
    public async Task Dashboard_issue_and_risk_counts_equal_their_register_lists()
    {
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        async Task Issue(string severity, Guid? discipline = null) =>
            await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/issues", new { title = "Issue " + severity, description = "What happened", severity, projectDisciplineId = discipline }).Result.Json(201);
        async Task<JsonNode> Risk(int prob, int impact) =>
            await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/risks", new { title = $"Risk {prob}x{impact}", description = "What could happen", probability = prob, impact }).Result.Json(201);
        await Issue(Impact.High, civil);
        await Issue(Impact.High);
        await Issue(Impact.Low);
        await Risk(3, 3);
        await Risk(3, 2);
        await Risk(1, 1);
        var closed = await Risk(3, 3);
        await f.DbAsync(db => db.Risks.Where(x => x.Id == closed.G("id")).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, RiskStatus.Closed)));
        await d.NewTask(p.Id, TestData.Pm);
        await f.Evaluate(p.Id);

        foreach (var who in new[] { TestData.Pm, TestData.Marc, TestData.Sam })
            foreach (var scope in new[] { "", $"?disciplineId={civil}" })
            {
                var c = f.As(who);
                var dash = await c.GetAsync($"/api/v1/projects/{p.Id}/dashboard{scope}").Result.Json();
                var reg = dash["registers"]!;
                foreach (var (key, list) in new[] { ("issuesOpen", "issues"), ("issuesHigh", "issues"), ("risksHigh", "risks") })
                {
                    var rows = (await c.GetAsync($"/api/v1/projects/{p.Id}/{list}?{reg[key]!.S("link")}").Result.Json()).AsArray();
                    Assert.True(reg[key]!.I("value") == rows.Count, $"{who}{scope} {key}: {reg[key]!.I("value")} vs {rows.Count}");
                }
                // FR-RCV-08: the existing fields keep their values, and the new counts reproduce them.
                Assert.Equal((3, 2, 2), (reg["issuesOpen"]!.I("value"), reg["issuesHigh"]!.I("value"), reg["risksHigh"]!.I("value")));
                Assert.Equal((dash["issues"]!.I("open"), dash["issues"]!.I("high"), dash["risks"]!.I("high")), (3, 2, 2));
            }
    }

    [Fact]
    [Trait("Spec", "AC-RCV-08")]
    public async Task Discipline_due_14_count_equals_its_deliverable_list()
    {
        var p = await d.Project();
        string In(int days) => Today.AddDays(days).ToString("yyyy-MM-dd");
        await Deliverable(p.Id, "Civil", new { dueDate = In(3) });
        await Deliverable(p.Id, "Civil", new { dueDate = In(14) });
        await Deliverable(p.Id, "Civil", new { dueDate = In(20) });
        await SetStatus(await Deliverable(p.Id, "Civil", new { dueDate = In(5) }), DeliverableStatus.Issued);
        await SetStatus(await Deliverable(p.Id, "Civil", new { dueDate = In(6) }), DeliverableStatus.OnHold);
        await Deliverable(p.Id, "Electrical", new { dueDate = In(2) });
        await f.Evaluate(p.Id);

        var civil = d.ProjectDiscipline(p.Id, "Civil");
        foreach (var who in new[] { TestData.Pm, TestData.Marc, TestData.Sam })
            foreach (var scope in new[] { "", $"?disciplineId={civil}" })
            {
                var c = f.As(who);
                var dash = await c.GetAsync($"/api/v1/projects/{p.Id}/dashboard{scope}").Result.Json();
                foreach (var row in dash["disciplines"]!.AsArray())
                {
                    var count = dash["disciplineDue14"]![row!.S("disciplineId")]!;
                    Assert.Contains($"disciplineId={row.S("disciplineId")}", count.S("link"));
                    var rows = (await c.GetAsync($"/api/v1/projects/{p.Id}/deliverables?{count.S("link")}").Result.Json()).AsArray();
                    Assert.Equal(count.I("value"), rows.Count);
                    Assert.Equal(row.I("deliverablesDue14"), count.I("value")); // FR-RCV-08: the value is unchanged
                }
            }
        var all = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/dashboard").Result.Json();
        Assert.Equal(3, all["disciplineDue14"]![civil.ToString()]!.I("value")); // 3, 14 and On Hold; not 20 days or Issued
        Assert.Equal(1, all["disciplineDue14"]![d.ProjectDiscipline(p.Id, "Electrical").ToString()]!.I("value"));
    }

    [Fact]
    [Trait("Spec", "AC-RCV-09")]
    public async Task Milestone_counts_equal_the_lists_they_open()
    {
        var p = await d.Project();
        var m = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Design submission", milestoneType = "Other", date = Today.AddDays(30).ToString("yyyy-MM-dd") }).Result.Json(201);
        var mid = m.G("id");
        var open = await Deliverable(p.Id, "Civil", new { milestoneId = mid });
        await SetStatus(await Deliverable(p.Id, "Civil", new { milestoneId = mid }), DeliverableStatus.Issued);
        await SetStatus(await Deliverable(p.Id, "Electrical", new { milestoneId = mid }), DeliverableStatus.Accepted);
        await SetStatus(await Deliverable(p.Id, "Civil", new { milestoneId = mid }), DeliverableStatus.Cancelled);
        await Deliverable(p.Id, "Civil", new { }); // targets nothing

        var pred = await d.NewTask(p.Id, TestData.Pm, new { name = "Overdue", milestoneId = mid, assigneeId = U(TestData.Alex), dueDate = Today.AddDays(-3).ToString("yyyy-MM-dd") });
        await d.NewTask(p.Id, TestData.Pm, new { name = "Blocked", deliverableId = open.G("id"), assigneeId = U(TestData.Alex), dependsOn = new[] { pred.G("id") } });
        var done = await d.NewTask(p.Id, TestData.Pm, new { name = "Done", milestoneId = mid, assigneeId = U(TestData.Alex) });
        await d.Move(TestData.Alex, done, TaskStatuses.Complete);
        var cancelled = await d.NewTask(p.Id, TestData.Pm, new { name = "Dropped", milestoneId = mid });
        await f.DbAsync(db => db.Tasks.Where(x => x.Id == cancelled.G("id")).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, TaskStatuses.Cancelled)));
        await d.NewTask(p.Id, TestData.Pm, new { name = "Elsewhere" });
        await f.Evaluate(p.Id);

        var pm = f.As(TestData.Pm);
        var row = (await pm.GetAsync($"/api/v1/projects/{p.Id}/milestones").Result.Json()).AsArray().Single(x => x!.G("id") == mid)!;
        Assert.Equal((3, 2, 3, 1, 1, 1), (row.I("deliverableTotal"), row.I("deliverableIssued"), row.I("taskTotal"), row.I("taskComplete"), row.I("taskOverdue"), row.I("taskBlocked")));
        // The links the Milestones view builds (milestoneLinks in web/src/pages/projects/Milestones.tsx).
        var notCancelled = Uri.EscapeDataString(string.Join(",", DeliverableStatus.All.Where(x => x != DeliverableStatus.Cancelled)));
        async Task<int> Deliverables(string q) => (await pm.GetAsync($"/api/v1/projects/{p.Id}/deliverables?milestoneId={mid}&{q}").Result.Json()).AsArray().Count;
        async Task<int> Tasks(string q) => (await pm.GetAsync($"/api/v1/projects/{p.Id}/tasks?milestoneId={mid}{q}").Result.Json()).I("totalCount");
        Assert.Equal(row.I("deliverableTotal"), await Deliverables($"status={notCancelled}"));
        Assert.Equal(row.I("deliverableIssued"), await Deliverables("status=Issued%2CAccepted"));
        Assert.Equal(row.I("taskTotal"), await Tasks(""));
        Assert.Equal(row.I("taskComplete"), await Tasks("&status=Complete"));
        Assert.Equal(row.I("taskOverdue"), await Tasks("&overdue=true"));
        Assert.Equal(row.I("taskBlocked"), await Tasks("&blocked=true"));
    }
}
