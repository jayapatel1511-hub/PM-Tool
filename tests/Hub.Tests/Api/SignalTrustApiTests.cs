using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 036 Part B at API level: counts equal the lists they open (AC-CNT-02 to -05).
[Collection("api")]
public sealed class SignalTrustApiTests(HubFactory f)
{
    readonly TestData d = new(f);
    Task<JsonNode> Get(string url) => f.As(TestData.Pm).GetAsync($"/api/v1/{url}").Result.Json();

    async Task<JsonNode> Deliverable(Guid projectId, object extra)
    {
        var body = new Dictionary<string, object?> { ["name"] = "Package " + Guid.NewGuid().ToString("N")[..4], ["projectDisciplineId"] = d.ProjectDiscipline(projectId, "Civil"),
            ["deliverableTypeId"] = await d.DeliverableType(), ["requiresReview"] = false };
        foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/deliverables", body).Result.Json(201);
    }

    [Fact]
    public async Task The_discipline_Open_count_and_its_link_both_leave_out_On_Hold() // AC-CNT-02
    {
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        await d.NewTask(p.Id, TestData.Pm, new { name = "Open work" });
        var held = await d.NewTask(p.Id, TestData.Pm, new { name = "Held work" });
        await d.Move(TestData.Pm, held, TaskStatuses.OnHold, new { reason = "Client asked to pause" });
        await f.Evaluate(p.Id);

        var row = (await Get($"projects/{p.Id}/dashboard"))["disciplines"]!.AsArray().Single(x => x!.G("disciplineId") == civil)!;
        Assert.Equal(1, row.I("open"));
        Assert.Equal(row.I("open"), (await Get($"projects/{p.Id}/tasks?disciplineId={civil}&open=true&onHold=false")).I("totalCount"));
        Assert.Equal(2, (await Get($"projects/{p.Id}/tasks?disciplineId={civil}&open=true")).I("totalCount")); // the old link's mismatch
        Assert.Equal(held.S("id"), (await Get($"projects/{p.Id}/tasks?disciplineId={civil}&onHold=true"))["items"]!.AsArray().Single()!.S("id"));
    }

    [Fact]
    public async Task The_unassigned_deliverable_filter_follows_the_chip_rule() // AC-CNT-03
    {
        var p = await d.Project();
        var open = await Deliverable(p.Id, new { name = "Open, no owner" });
        var issued = await Deliverable(p.Id, new { name = "Issued, no owner" });
        await f.As(TestData.Pm).Post($"/api/v1/deliverables/{issued.S("id")}/issue", new { rowVersion = issued.I("rowVersion") }).Result.Json();
        // Creation needs an owner; a copied structure or a removed owner leaves none.
        await f.DbAsync(db => db.Deliverables.Where(x => x.ProjectId == p.Id).ExecuteUpdateAsync(x => x.SetProperty(y => y.OwnerId, (Guid?)null)));
        await f.Evaluate(p.Id);

        var rows = (await Get($"projects/{p.Id}/deliverables?indicator=unassigned")).AsArray();
        Assert.Equal(open.S("id"), Assert.Single(rows)!.S("id"));
        var chips = await f.DbAsync(db => db.DeliverableStates.Where(s => s.ProjectId == p.Id && s.IsUnassigned).Select(s => s.DeliverableId).ToListAsync());
        Assert.Equal(chips, rows.Select(r => r!.G("id")));
    }

    [Fact]
    public async Task Upcoming_deadlines_keep_the_soonest_items_when_one_type_has_more_than_101() // AC-CNT-04
    {
        var p = await d.Project();
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        for (var i = 150; i >= 1; i--) // created latest first, so storage order is the reverse of due order
            await Deliverable(p.Id, new { name = $"Sheet {i:000}", dueDate = today.AddDays(i).ToString("yyyy-MM-dd") });

        var deadlines = (await Get($"home?projects={p.Id}&from={today:yyyy-MM-dd}&to={today.AddDays(200):yyyy-MM-dd}"))["upcomingDeadlines"]!;
        Assert.True(deadlines["truncated"]!.GetValue<bool>());
        Assert.Equal(101, deadlines.I("total"));
        var dates = deadlines["items"]!.AsArray().Select(x => DateOnly.Parse(x!.S("date"))).ToList();
        Assert.Equal(Enumerable.Range(1, 100).Select(i => today.AddDays(i)), dates); // the 100 soonest, in date order
    }

    [Fact]
    public async Task Home_lanes_and_the_On_hold_count_add_up_to_Open_Tasks() // AC-CNT-05
    {
        var p = await d.Project();
        await d.NewTask(p.Id, TestData.Pm, new { name = "To do" });
        var doing = await d.NewTask(p.Id, TestData.Pm, new { name = "Doing" });
        await d.Move(TestData.Pm, doing, TaskStatuses.InProgress);
        var held = await d.NewTask(p.Id, TestData.Pm, new { name = "Held" });
        await d.Move(TestData.Pm, held, TaskStatuses.OnHold, new { reason = "Client asked to pause" });
        var done = await d.NewTask(p.Id, TestData.Pm, new { name = "Done" });
        await d.Move(TestData.Pm, done, TaskStatuses.InProgress);
        await d.Move(TestData.Pm, done, TaskStatuses.Complete);
        await f.Evaluate(p.Id);

        var h = await Get($"home?projects={p.Id}");
        var onHold = h["onHold"]!;
        Assert.Equal(1, onHold.I("count"));
        Assert.Equal(onHold.I("count"), (await Get($"tasks?projects={p.Id}&status={Uri.EscapeDataString(onHold.S("status"))}")).I("totalCount"));
        var notDone = h["tasksByStatus"]!.AsArray().Where(l => l!.S("lane") != "Done").Sum(l => l!.I("count"));
        Assert.Equal(h["metrics"]!.I("openTasks"), notDone + onHold.I("count"));
        Assert.Equal(3, h["metrics"]!.I("openTasks"));
    }
}
